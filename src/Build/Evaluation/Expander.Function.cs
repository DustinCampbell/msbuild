// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using System.Reflection;
using System.Text;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;
using AvailableStaticMethods = Microsoft.Build.Internal.AvailableStaticMethods;
using FeatureSwitches = Microsoft.Build.Framework.FeatureSwitches;

#if FEATURE_MSIOREDIST
// File is intentionally NOT aliased — all typeof() comparisons use fully-qualified
// System.IO.File to match the types registered in AvailableStaticMethods.
using Path = Microsoft.IO.Path;
#endif

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// This class represents the function as extracted from an expression
    /// It is also responsible for executing the function.
    /// </summary>
    internal class Function : IArgumentMaterializer
    {
        /// <summary>
        /// The type of this function's receiver.
        /// </summary>
        /// <remarks>
        /// Property-function evaluation only ever binds public members (BindingFlags.NonPublic is
        /// never set on this path), so only the public member surface needs to be preserved for
        /// trimming. Keep in sync with Constants.PropertyFunctionMembers, which preserves the same
        /// set on every allowlisted receiver type.
        /// </remarks>
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicMethods |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.PublicFields)]
        private Type _receiverType;

        /// <summary>
        /// The name of the function.
        /// </summary>
        private readonly string _methodMethodName;

        /// <summary>
        /// The arguments for the function.
        /// </summary>
        private readonly string[] _arguments;

        /// <summary>
        /// The expression that this function is part of.
        /// </summary>
        private readonly string _expression;

        /// <summary>
        /// The property name that this function is applied on.
        /// </summary>
        private readonly string _receiver;

        /// <summary>
        /// The complete set of <see cref="BindingFlags"/> the property-function binder is permitted
        /// to use. This set intentionally excludes <see cref="BindingFlags.NonPublic"/>: property
        /// functions only ever bind public members. That exclusion is what lets a receiver type
        /// preserve only its public member surface for trimming (see Constants.PropertyFunctionMembers)
        /// and keeps the flags handed to <c>TypeExtensions.InvokePublicMember</c> free of
        /// <see cref="BindingFlags.NonPublic"/>.
        /// </summary>
        private const BindingFlags AllowedBindingFlags =
            BindingFlags.IgnoreCase
            | BindingFlags.Public
            | BindingFlags.Static
            | BindingFlags.Instance
            | BindingFlags.InvokeMethod
            | BindingFlags.GetProperty
            | BindingFlags.GetField;

        private enum InvocationOutcome
        {
            Invoked,
            NotHandled,
            Failed,
        }

        /// <summary>
        /// The binding flags that will be used during invocation of this function.
        /// </summary>
        /// <remarks>
        /// Always a subset of <see cref="AllowedBindingFlags"/> - constrained at construction and only
        /// ever augmented with <see cref="BindingFlags.Static"/> / <see cref="BindingFlags.Instance"/>
        /// thereafter - so it can never carry <see cref="BindingFlags.NonPublic"/>.
        /// </remarks>
        private BindingFlags _bindingFlags;

        /// <summary>
        /// The remainder of the body once the function and arguments have been extracted.
        /// </summary>
        private readonly string _remainder;

        /// <summary>
        /// Construct a function that will be executed during property evaluation.
        /// </summary>
        internal Function(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicFields)] Type receiverType,
            string expression,
            string receiver,
            string methodName,
            string[] arguments,
            BindingFlags bindingFlags,
            string remainder)
        {
            _methodMethodName = methodName;
            if (arguments == null)
            {
                _arguments = [];
            }
            else
            {
                _arguments = arguments;
            }

            _receiver = receiver;
            _expression = expression;
            _receiverType = receiverType;

            // Property functions never bind non-public members. Constrain the incoming flags to the
            // allowed set so that invariant holds by construction: the only in-class mutations after
            // this add Static/Instance (both already allowed), so _bindingFlags can never carry
            // BindingFlags.NonPublic, so the flags handed to TypeExtensions.InvokePublicMember never
            // request non-public members.
            System.Diagnostics.Debug.Assert(
                (bindingFlags & ~AllowedBindingFlags) == 0,
                $"Property-function binding flags '{bindingFlags}' include flags outside the allowed set; BindingFlags.NonPublic in particular is never permitted.");
            _bindingFlags = bindingFlags & AllowedBindingFlags;

            _remainder = remainder;
        }

        /// <summary>
        /// Part of the extraction may result in the name of the property
        /// This accessor is used by the Expander
        /// Examples of expression root:
        ///     [System.Diagnostics.Process]::Start
        ///     SomeMSBuildProperty.
        /// </summary>
        internal string Receiver
        {
            get { return _receiver; }
        }

        /// <summary>
        /// Extract the function details from the given property function expression.
        /// </summary>
        /// <param name="expressionFunction">The property-function body, e.g. <c>SomeProp.ToLower()</c> or <c>[System.Math]::Max(1, 2)</c>.</param>
        /// <param name="location">Location used for error reporting.</param>
        /// <param name="propertyValue">
        /// The receiver instance the function binds against. It is used here only to derive the receiver
        /// <see cref="Type"/> (via <c>GetType()</c>); the instance itself is passed to <c>Execute</c> later.
        /// Legitimate values are:
        /// <list type="bullet">
        /// <item><description><see langword="null"/> for a static call (<c>[Type]::Method()</c>) or the first
        /// instance call in a chain, where the receiver type defaults to <see cref="string"/>.</description></item>
        /// <item><description>A <see cref="string"/>, the evaluated value of an MSBuild property (the common case;
        /// property values are always strings).</description></item>
        /// <item><description>The return value of a preceding function in a chain such as <c>$(Prop.A().B())</c>,
        /// which can be any type that function produced.</description></item>
        /// </list>
        /// Only the receiver type's public member surface (constructors, methods, properties, fields) is reflected
        /// over. Because that runtime type is open-ended it cannot be statically preserved nor expressed as a
        /// <c>DynamicallyAccessedMembers</c> constraint on an <see cref="object"/> parameter, so the unavoidable
        /// trim suppression lives, minimized, in <c>FunctionBuilder.SetReceiverType</c>.
        /// </param>
        internal static Function ExtractPropertyFunction(string expressionFunction, IElementLocation location, object propertyValue)
        {
            // Used to aggregate all the components needed for a Function
            FunctionBuilder functionBuilder = new();

            // By default the expression root is the whole function expression
            ReadOnlySpan<char> expressionRoot = expressionFunction == null ? ReadOnlySpan<char>.Empty : expressionFunction.AsSpan();

            // The arguments for this function start at the first '('
            // If there are no arguments, then we're a property getter
            var argumentStartIndex = expressionFunction.IndexOf('(');

            // If we have arguments, then we only want the content up to but not including the '('
            if (argumentStartIndex > -1)
            {
                expressionRoot = expressionRoot.Slice(0, argumentStartIndex);
            }

            // In case we ended up with something we don't understand
            ProjectErrorUtilities.VerifyThrowInvalidProject(!expressionRoot.IsEmpty, location, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
            functionBuilder.Expression = expressionFunction;

            // This is a static method call
            // A static method is the content that follows the last "::", the rest being the type
            if (propertyValue == null && expressionRoot[0] == '[')
            {
                var typeEndIndex = expressionRoot.IndexOf(']');

                if (typeEndIndex < 1)
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(location, "InvalidFunctionStaticMethodSyntax", expressionFunction, String.Empty);
                }

                var typeName = Strings.WeakIntern(expressionRoot.Slice(1, typeEndIndex - 1));
                var methodStartIndex = typeEndIndex + 1;

                if (expressionRoot.Length > methodStartIndex + 2 && expressionRoot[methodStartIndex] == ':' && expressionRoot[methodStartIndex + 1] == ':')
                {
                    // skip over the "::"
                    methodStartIndex += 2;
                }
                else
                {
                    // We ended up with something other than a static function expression
                    ProjectErrorUtilities.ThrowInvalidProject(location, "InvalidFunctionStaticMethodSyntax", expressionFunction, String.Empty);
                }

                ConstructFunction(location, expressionFunction, argumentStartIndex, methodStartIndex, ref functionBuilder);

                // Locate a type that matches the body of the expression.
                var receiverType = GetTypeForStaticMethod(typeName, functionBuilder.Name);

                if (receiverType == null)
                {
                    // We ended up with something other than a type
                    ProjectErrorUtilities.ThrowInvalidProject(location, "InvalidFunctionTypeUnavailable", expressionFunction, typeName);
                }

                functionBuilder.SetReceiverType(receiverType);
            }
            else if (expressionFunction[0] == '[') // We have an indexer
            {
                var indexerEndIndex = expressionFunction.IndexOf(']', 1);
                if (indexerEndIndex < 1)
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(location, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                }

                var methodStartIndex = indexerEndIndex + 1;

                functionBuilder.SetReceiverType(propertyValue.GetType());

                ConstructIndexerFunction(expressionFunction, location, propertyValue, methodStartIndex, indexerEndIndex, ref functionBuilder);
            }
            else // This could be a property reference, or a chain of function calls
            {
                // Look for an instance function call next, such as in SomeStuff.ToLower()
                var methodStartIndex = expressionRoot.IndexOf('.');
                if (methodStartIndex == -1)
                {
                    // We don't have a function invocation in the expression root, return null
                    return null;
                }

                // skip over the '.';
                methodStartIndex++;

                var rootEndIndex = expressionRoot.IndexOf('.');

                // If this is an instance function rather than a static, then we'll capture the name of the property referenced
                var functionReceiver = Strings.WeakIntern(expressionRoot.Slice(0, rootEndIndex).Trim());

                // If propertyValue is null (we're not recursing), then we're expecting a valid property name
                if (propertyValue == null && !IsValidPropertyName(functionReceiver))
                {
                    // We extracted something that wasn't a valid property name, fail.
                    ProjectErrorUtilities.ThrowInvalidProject(location, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                }

                // If we are recursively acting on a type that has been already produced then pass that type inwards (e.g. we are interpreting a function call chain)
                // Otherwise, the receiver of the function is a string
                var receiverType = propertyValue?.GetType() ?? typeof(string);

                functionBuilder.Receiver = functionReceiver;
                functionBuilder.SetReceiverType(receiverType);

                ConstructFunction(location, expressionFunction, argumentStartIndex, methodStartIndex, ref functionBuilder);
            }

            return functionBuilder.Build();
        }

        private static bool IsFileOrDirectoryType(Type type)
            => type == typeof(System.IO.File)
            || type == typeof(System.IO.Directory);

        private static bool IsPathType(Type type)
            => type == typeof(System.IO.Path);

        /// <summary>
        /// Determines whether the argument at <paramref name="argIndex"/> for a System.IO.File
        /// or System.IO.Directory method is a file/directory path that should be resolved
        /// against the thread-local working directory.
        /// </summary>
        private static bool IsFileOrDirectoryPathArgument(string methodName, int argIndex)
        {
            // First argument is always a path for all File/Directory static methods.
            if (argIndex == 0)
            {
                return true;
            }

            // Second argument is a destination path for Copy, Move, Replace.
            // CreateSymbolicLink is intentionally excluded — its arg1 (pathToTarget) is the
            // symlink target and relative values are semantically meaningful (stored as-is).
            if (argIndex == 1)
            {
                return string.Equals(methodName, "Copy", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(methodName, "Move", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(methodName, "Replace", StringComparison.OrdinalIgnoreCase);
            }

            // Third argument is the backup path for Replace.
            if (argIndex == 2)
            {
                return string.Equals(methodName, "Replace", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        object IArgumentMaterializer.MaterializeArgument(string argText, int argIndex, ExpanderOptions options, ref readonly ExpanderContext context)
        {
            object argument = PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(argText, options, in context);

            if (argument is not string argValue)
            {
                return argument;
            }

            // Unescape the value since we're about to send it out of the engine and into
            // the function being called. If a file or a directory function, fix the path
            // Use fully qualified type names because FEATURE_MSIOREDIST aliases
            // Directory and Path to Microsoft.IO.* in this file, but _receiverType
            // from AvailableStaticMethods is always System.IO.*.
            if (IsFileOrDirectoryType(_receiverType) || IsPathType(_receiverType))
            {
                argValue = FileUtilities.FixFilePath(argValue);
            }

            argValue = EscapingUtilities.UnescapeAll(argValue);

            // In -mt mode, resolve relative path arguments for File/Directory methods
            // against the thread-local working directory instead of the process-global
            // Environment.CurrentDirectory which may point to a different project's directory.
            // In multiprocess mode, CurrentThreadWorkingDirectory is null and
            // MakeFullPathFromThreadWorkingDirectory returns null — this is a no-op.
            // This must happen AFTER UnescapeAll so that the working directory path
            // (a real filesystem path) is not corrupted by MSBuild unescape processing.
            if (IsFileOrDirectoryType(_receiverType) && IsFileOrDirectoryPathArgument(_methodMethodName, argIndex))
            {
                AbsolutePath? resolved = FileUtilities.MakeFullPathFromThreadWorkingDirectory(argValue);
                if (resolved.HasValue)
                {
                    argValue = (string)resolved.GetValueOrDefault();
                }
            }

            return argValue;
        }

        /// <summary>
        ///  Executes this property function against the specified receiver.
        /// </summary>
        /// <param name="objectInstance">
        ///  The receiver instance, or <see langword="null"/> for a static member or constructor invocation.
        /// </param>
        /// <param name="options">The options controlling expansion and invocation error handling.</param>
        /// <param name="context">The context in which the property function is expanded.</param>
        /// <returns>
        ///  The invocation result after required escaping and expansion of any remaining expression, or the
        ///  partially evaluated invocation when <see cref="ExpanderOptions.LeavePropertiesUnexpandedOnError"/> is
        ///  specified and invocation fails.
        /// </returns>
        /// <remarks>
        ///  Execution validates member availability before attempting well-known dispatch. Well-known handlers
        ///  materialize arguments on demand; reflection fallback materializes all remaining arguments before
        ///  invocation.
        /// </remarks>
        internal object Execute(object objectInstance, ExpanderOptions options, ref readonly ExpanderContext context)
        {
            // If there is no object instance, then the method invocation will be a static
            if (objectInstance == null)
            {
                // Check that the function that we're going to call is valid to call
                if (!IsStaticMethodAvailable(_receiverType, _methodMethodName))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(context.Location, "InvalidFunctionMethodUnavailable", _methodMethodName, _receiverType.FullName);
                }

                _bindingFlags |= BindingFlags.Static;
            }
            else
            {
                // Check that the function that we're going to call is valid to call
                if (!IsInstanceMethodAvailable(_receiverType, _methodMethodName))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(context.Location, "InvalidFunctionMethodUnavailable", _methodMethodName, _receiverType.FullName);
                }

                _bindingFlags |= BindingFlags.Instance;

                // The object that we're about to call methods on may have escaped characters
                // in it, we want to operate on the unescaped string in the function, just as we
                // want to pass arguments that are unescaped (see below)
                if (objectInstance is string objectInstanceString)
                {
                    objectInstance = EscapingUtilities.UnescapeAll(objectInstanceString);
                }
            }

            Arguments arguments = new(_arguments, materializer: this, options, in context);
            bool isConstructor = string.Equals("new", _methodMethodName, StringComparison.OrdinalIgnoreCase);

            InvocationOutcome outcome = TryInvokeWellKnown(objectInstance, ref arguments, isConstructor, options, in context, out object result);

            if (outcome == InvocationOutcome.NotHandled)
            {
                outcome = TryInvokeWithReflection(objectInstance, ref arguments, isConstructor, options, in context, out result);
            }

            if (outcome == InvocationOutcome.Failed)
            {
                return result;
            }

            // If the result of the function call is a string, then we need to escape the result
            // so that we maintain the "engine contains escaped data" state.
            // The exception is that the user is explicitly calling MSBuild::Unescape, MSBuild::Escape, or ConvertFromBase64
            if (result is string s &&
                !string.Equals("Unescape", _methodMethodName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals("Escape", _methodMethodName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals("ConvertFromBase64", _methodMethodName, StringComparison.OrdinalIgnoreCase))
            {
                result = EscapingUtilities.Escape(s);
            }

            // We have nothing left to parse, so we'll return what we have
            if (_remainder.IsNullOrEmpty())
            {
                return result;
            }

            // Recursively expand the remaining property body after execution
            return PropertyExpander.ExpandPropertyBody(_remainder, result, options, in context);
        }

        private InvocationOutcome TryInvokeWellKnown(
            object objectInstance,
            ref Arguments arguments,
            bool isConstructor,
            ExpanderOptions options,
            ref readonly ExpanderContext context,
            out object result)
        {
            try
            {
                WellKnownFunctionResult functionResult = isConstructor
                    ? WellKnownFunctions.TryInvokeConstructor(_receiverType, ref arguments, in context)
                    : objectInstance is null
                        ? WellKnownFunctions.TryInvokeStatic(_receiverType, _methodMethodName, ref arguments, in context)
                        : WellKnownFunctions.TryInvokeInstance(objectInstance, _methodMethodName, ref arguments, in context);

                if (functionResult.Status == WellKnownFunctionStatus.Invoked)
                {
                    result = functionResult.Result;
                    return InvocationOutcome.Invoked;
                }
            }
            catch (Exception ex) when (arguments.AllMaterialized)
            {
                // Well-known handlers materialize every supplied argument before invoking the underlying function,
                // so the filter lets materialization exceptions propagate unchanged. Direct invocation exceptions
                // are not wrapped in TargetInvocationException; handle them consistently with reflection.
                string partiallyEvaluated = FormatEvaluatedFunctionInvocation(objectInstance, _methodMethodName, arguments.ToObjectArray(), in context);
                if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    result = partiallyEvaluated;
                    return InvocationOutcome.Failed;
                }

                ProjectErrorUtilities.ThrowInvalidProject(
                    context.Location,
                    "InvalidFunctionPropertyExpression",
                    partiallyEvaluated,
                    ex.Message.Replace("\r\n", " "));
                result = null;
                return InvocationOutcome.Failed;
            }

            result = null;
            return InvocationOutcome.NotHandled;
        }

        private InvocationOutcome TryInvokeWithReflection(
            object objectInstance,
            ref Arguments arguments,
            bool isConstructor,
            ExpanderOptions options,
            ref readonly ExpanderContext context,
            out object result)
        {
            object[] args = arguments.ToObjectArray();

            try
            {
                if (!isConstructor && objectInstance is not null)
                {
                    AdjustForEqualsAndCompareTo(ref objectInstance, args);
                }

                LogFunctionCallRequiringReflection(objectInstance, args);

                result = isConstructor
                    ? InvokeConstructorWithReflection(args)
                    : objectInstance is null
                        ? InvokeStaticMemberWithReflection(args)
                        : InvokeInstanceMemberWithReflection(objectInstance, args);

                return InvocationOutcome.Invoked;
            }
            catch (TargetInvocationException ex)
            {
                // Exceptions coming from the actual function called are wrapped in a TargetInvocationException.
                string partiallyEvaluated = FormatEvaluatedFunctionInvocation(objectInstance, _methodMethodName, args, in context);
                if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    // If the caller wants to ignore errors (in a log statement for example), just return the partially evaluated value
                    result = partiallyEvaluated;
                    return InvocationOutcome.Failed;
                }

                ProjectErrorUtilities.ThrowInvalidProject(
                    context.Location,
                    "InvalidFunctionPropertyExpression",
                    partiallyEvaluated,
                    ex.InnerException.Message.Replace("\r\n", " "));
            }
            catch (Exception ex) when (!ExceptionHandling.NotExpectedFunctionException(ex))
            {
                // Translate expected function evaluation, binding, and reflection failures into MSBuild diagnostics.
                if (s_invariantCompareInfo.IndexOf(_expression, "::", CompareOptions.OrdinalIgnoreCase) > -1)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        context.Location,
                        "InvalidFunctionStaticMethodSyntax",
                        _expression,
                        ex.Message.Replace("Microsoft.Build.Evaluation.IntrinsicFunctions.", "[MSBuild]::"));
                }
                else
                {
                    string partiallyEvaluated = FormatEvaluatedFunctionInvocation(objectInstance, _methodMethodName, args, in context);
                    ProjectErrorUtilities.ThrowInvalidProject(
                        context.Location,
                        "InvalidFunctionPropertyExpression",
                        partiallyEvaluated,
                        ex.Message);
                }
            }

            result = null;
            return InvocationOutcome.Failed;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2074:UnrecognizedReflectionPattern",
            Justification = "_receiverType is reassigned from a runtime property value whose type is restricted to the property-function allowlist, whose members are preserved for trimming.")]
        private void AdjustForEqualsAndCompareTo(ref object objectInstance, object[] args)
        {
            // The default binder often chooses the incorrect Equals or CompareTo overload because the argument on
            // the right is generally a string. Coerce it to the receiver type before invoking reflection.
            if (args.Length == 1 &&
                (string.Equals(nameof(Equals), _methodMethodName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(nameof(IComparable.CompareTo), _methodMethodName, StringComparison.OrdinalIgnoreCase)))
            {
                object arg0 = args[0];

                // Support comparisons between integral and floating-point representations.
                if (ArgumentParser.IsFloatingPointRepresentation(arg0) &&
                    double.TryParse(objectInstance.ToString(), NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                {
                    objectInstance = d;
                    _receiverType = objectInstance.GetType();
                }

                args[0] = Convert.ChangeType(arg0, objectInstance.GetType(), CultureInfo.InvariantCulture);
            }
        }

        private object InvokeConstructorWithReflection(object[] args)
        {
            var reflectionInvoker = new ReflectionInvoker(_receiverType, _methodMethodName, BindingFlags.Public | BindingFlags.Instance);
            return reflectionInvoker.InvokeConstructor(args);
        }

        private object InvokeStaticMemberWithReflection(object[] args)
        {
            var reflectionInvoker = new ReflectionInvoker(_receiverType, _methodMethodName, _bindingFlags);
            return reflectionInvoker.InvokeMember(objectInstance: null, args);
        }

        private object InvokeInstanceMemberWithReflection(object objectInstance, object[] args)
        {
            var reflectionInvoker = new ReflectionInvoker(_receiverType, _methodMethodName, _bindingFlags);
            return reflectionInvoker.InvokeMember(objectInstance, args);
        }

        private void LogFunctionCallRequiringReflection(object objectInstance, object[] args)
        {
            if (!Traits.Instance.LogPropertyFunctionsRequiringReflection)
            {
                return;
            }

            // Note: EnsureDotnetCommonProjectPropertyFunctionsOnFastPath currently depends on the format of this log file.
            // If the format is changed, that test should be updated as well.
            // See https://github.com/dotnet/dotnet/blob/c567e8ff13b8ada7c5285c60bc2ac1111ae602e7/src/sdk/test/Microsoft.NET.Build.Tests/EvaluatorFastPathTests.cs
            string logFile = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "PropertyFunctionsRequiringReflection");

            var builder = StringBuilderCache.Acquire();

            builder.Append("ReceiverType=");
            builder.Append(_receiverType.FullName);
            builder.Append("; ObjectInstanceType=");

            if (objectInstance is not null)
            {
                builder.Append(objectInstance.GetType().FullName);
            }

            builder.Append("; MethodName=");
            builder.Append(_methodMethodName);
            builder.Append('(');

            bool isFirst = true;

            foreach (object arg in args)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    builder.Append(", ");
                }

                builder.Append(arg?.GetType().Name ?? "null");
            }

            builder.Append(")\n");

            System.IO.File.AppendAllText(logFile, StringBuilderCache.GetStringAndRelease(builder));
        }

        /// <summary>
        /// Given a type name and method name, try to resolve the type.
        /// </summary>
        /// <param name="typeName">May be full name or assembly qualified name.</param>
        /// <param name="simpleMethodName">simple name of the method.</param>
        /// <returns></returns>
        [UnconditionalSuppressMessage("Trimming", "IL2096:UnrecognizedReflectionPattern",
            Justification = "The type name is resolved against the curated AvailableStaticMethods allowlist; the case-insensitive lookup only resolves to allowlist types, whose members are preserved for trimming.")]
        private static Type GetTypeForStaticMethod(string typeName, string simpleMethodName)
        {
            Type receiverType;
            Tuple<string, Type> cachedTypeInformation;

            // If we don't have a type name, we already know that we won't be able to find a type.
            // Go ahead and return here -- otherwise the Type.GetType() calls below will throw.
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            // Check if the type is in the allowlist cache. If it is, use it or load it.
            cachedTypeInformation = AvailableStaticMethods.GetTypeInformationFromTypeCache(typeName, simpleMethodName);
            if (cachedTypeInformation != null)
            {
                // We need at least one of these set
                Assumed.True(cachedTypeInformation.Item1 != null || cachedTypeInformation.Item2 != null, "Function type information needs either string or type represented.");

                // If we have the type information in Type form, then just return that
                if (cachedTypeInformation.Item2 != null)
                {
                    return cachedTypeInformation.Item2;
                }
                else if (cachedTypeInformation.Item1 != null)
                {
                    // This is a case where the Type is not available at compile time, so
                    // we are forced to bind by name instead
                    var assemblyQualifiedTypeName = cachedTypeInformation.Item1;

                    // Get the type from the assembly qualified type name from AvailableStaticMethods
                    receiverType = Type.GetType(assemblyQualifiedTypeName, throwOnError: false, ignoreCase: true);

                    // If the type information from the cache is not loadable, it means the cache information got corrupted somehow
                    // Throw here to prevent adding null types in the cache
                    Assumed.NotNull(receiverType, $"Type information for {typeName} was present in the allowlist cache as {assemblyQualifiedTypeName} but the type could not be loaded.");

                    // If we've used it once, chances are that we'll be using it again
                    // We can record the type here since we know it's available for calling from the fact that is was in the AvailableStaticMethods table
                    AvailableStaticMethods.TryAdd(typeName, simpleMethodName, new Tuple<string, Type>(assemblyQualifiedTypeName, receiverType));

                    return receiverType;
                }
            }

            // Get the type from mscorlib (or the currently running assembly)
            receiverType = Type.GetType(typeName, throwOnError: false, ignoreCase: true);

            if (receiverType != null)
            {
                // DO NOT CACHE THE TYPE HERE!
                // We don't add the resolved type here in the AvailableStaticMethods table. This is because that table is used
                // during function parse, but only later during execution do we check for the ability to call specific methods on specific types.
                // Caching it here would load any type into the allow list.
                return receiverType;
            }

            // The following reflective probing runs only when the EnableAllPropertyFunctions feature
            // switch is enabled (or, in untrimmed builds, the legacy MSBUILDENABLEALLPROPERTYFUNCTIONS
            // environment variable is set). That switch is a [FeatureGuard] for RequiresUnreferencedCode,
            // so the analyzer treats this branch as the trim-unsafe region (no suppression needed). In
            // trimmed / AOT applications the trimmer substitutes the switch false and removes this branch,
            // so only the curated allowlist of receiver types is supported.
            if (FeatureSwitches.EnableAllPropertyFunctions)
            {
                // We didn't find the type, so go probing. First in System
                receiverType = GetTypeFromAssembly(typeName, "System");

                // Next in System.Core
                if (receiverType == null)
                {
                    receiverType = GetTypeFromAssembly(typeName, "System.Core");
                }

                // We didn't find the type, so try to find it using the namespace
                if (receiverType == null)
                {
                    receiverType = GetTypeFromAssemblyUsingNamespace(typeName);
                }

                if (receiverType != null)
                {
                    // If we've used it once, chances are that we'll be using it again
                    // We can cache the type here, since all functions are enabled
                    AvailableStaticMethods.TryAdd(typeName, new Tuple<string, Type>(typeName, receiverType));
                }
            }

            return receiverType;
        }

        /// <summary>
        /// Gets the specified type using the namespace to guess the assembly that its in.
        /// </summary>
        [RequiresUnreferencedCode("Resolves a property-function receiver type by probing and loading assemblies at runtime; reachable only via the MSBUILDENABLEALLPROPERTYFUNCTIONS feature switch, which is disabled under trimming.")]
        private static Type GetTypeFromAssemblyUsingNamespace(string typeName)
        {
            string baseName = typeName;
            int assemblyNameEnd = baseName.Length;

            // If the string has no dot, or is nothing but a dot, we have no
            // namespace to look for, so we can't help.
            if (assemblyNameEnd <= 0)
            {
                return null;
            }

            // We will work our way up the namespace looking for an assembly that matches
            while (assemblyNameEnd > 0)
            {
                string candidateAssemblyName = baseName.Substring(0, assemblyNameEnd);

                // Try to load the assembly with the computed name
                Type foundType = GetTypeFromAssembly(typeName, candidateAssemblyName);

                if (foundType != null)
                {
                    // We have a match, so get the type from that assembly
                    return foundType;
                }
                else
                {
                    // Keep looking as we haven't found a match yet
                    baseName = candidateAssemblyName;
                    assemblyNameEnd = baseName.LastIndexOf('.');
                }
            }

            // We didn't find it, so we need to give up
            return null;
        }

        /// <summary>
        /// Get the specified type from the assembly partial name supplied.
        /// </summary>
        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName", Justification = "Necessary since we don't have the full assembly name. ")]
        [RequiresUnreferencedCode("Resolves a property-function receiver type by loading an assembly by partial name at runtime; reachable only via the MSBUILDENABLEALLPROPERTYFUNCTIONS feature switch, which is disabled under trimming.")]
        private static Type GetTypeFromAssembly(string typeName, string candidateAssemblyName)
        {
            Type objectType = null;

            // Try to load the assembly with the computed name
#if FEATURE_GAC
#pragma warning disable 618, 612
            // Unfortunately Assembly.Load is not an alternative to LoadWithPartialName, since
            // Assembly.Load requires the full assembly name to be passed to it.
            // Therefore we must ignore the deprecated warning.
            Assembly candidateAssembly = Assembly.LoadWithPartialName(candidateAssemblyName);
#pragma warning restore 618, 612
#else
            Assembly candidateAssembly = null;
            try
            {
                candidateAssembly = Assembly.Load(new AssemblyName(candidateAssemblyName));
            }
            catch (FileNotFoundException)
            {
                // Swallow the error; LoadWithPartialName returned null when the partial name
                // was not found but Load throws.  Either way we'll provide a nice "couldn't
                // resolve this" error later.
            }
#endif

            if (candidateAssembly != null)
            {
                objectType = candidateAssembly.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);
            }

            return objectType;
        }

        /// <summary>
        /// Extracts the name, arguments, binding flags, and invocation type for an indexer
        /// Also extracts the remainder of the expression that is not part of this indexer.
        /// </summary>
        private static void ConstructIndexerFunction(string expressionFunction, IElementLocation elementLocation, object propertyValue, int methodStartIndex, int indexerEndIndex, ref FunctionBuilder functionBuilder)
        {
            ReadOnlyMemory<char> argumentsContent = expressionFunction.AsMemory().Slice(1, indexerEndIndex - 1);
            string[] functionArguments;

            // If there are no arguments, then just create an empty array
            if (argumentsContent.IsEmpty)
            {
                functionArguments = [];
            }
            else
            {
                // We will keep empty entries so that we can treat them as null
                functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
            }

            // choose the name of the function based on the type of the object that we
            // are using.
            string functionName;
            if (propertyValue is Array)
            {
                functionName = "GetValue";
            }
            else if (propertyValue is string)
            {
                functionName = "get_Chars";
            }
            else // a regular indexer
            {
                functionName = "get_Item";
            }

            functionBuilder.Name = functionName;
            functionBuilder.Arguments = functionArguments;
            functionBuilder.BindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod;
            functionBuilder.Remainder = expressionFunction.Substring(methodStartIndex);
        }

        /// <summary>
        /// Extracts the name, arguments, binding flags, and invocation type for a static or instance function.
        /// Also extracts the remainder of the expression that is not part of this function.
        /// </summary>
        private static void ConstructFunction(IElementLocation elementLocation, string expressionFunction, int argumentStartIndex, int methodStartIndex, ref FunctionBuilder functionBuilder)
        {
            // The unevaluated and unexpanded arguments for this function
            string[] functionArguments;

            // The name of the function that will be invoked
            ReadOnlySpan<char> functionName;

            // What's left of the expression once the function has been constructed
            ReadOnlySpan<char> remainder = ReadOnlySpan<char>.Empty;

            // The binding flags that we will use for this function's execution
            BindingFlags defaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

            ReadOnlySpan<char> expressionFunctionAsSpan = expressionFunction.AsSpan();

            ReadOnlySpan<char> expressionSubstringAsSpan = argumentStartIndex > -1 ? expressionFunctionAsSpan.Slice(methodStartIndex, argumentStartIndex - methodStartIndex) : ReadOnlySpan<char>.Empty;

            // There are arguments that need to be passed to the function
            if (argumentStartIndex > -1 && !expressionSubstringAsSpan.Contains(".".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                // separate the function and the arguments
                functionName = expressionSubstringAsSpan.Trim();

                // Skip the '('
                argumentStartIndex++;

                // Scan for the matching closing bracket, skipping any nested ones
                int argumentsEndIndex = ScanForClosingParenthesis(expressionFunctionAsSpan, argumentStartIndex);

                if (argumentsEndIndex == -1)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                }

                // We have been asked for a method invocation
                defaultBindingFlags |= BindingFlags.InvokeMethod;

                // It may be that there are '()' but no actual arguments content
                if (argumentStartIndex == expressionFunction.Length - 1)
                {
                    functionArguments = [];
                }
                else
                {
                    // we have content within the '()' so let's extract and deal with it
                    ReadOnlyMemory<char> argumentsContent = expressionFunction.AsMemory().Slice(argumentStartIndex, argumentsEndIndex - argumentStartIndex);

                    // If there are no arguments, then just create an empty array
                    if (argumentsContent.IsEmpty)
                    {
                        functionArguments = [];
                    }
                    else
                    {
                        // We will keep empty entries so that we can treat them as null
                        functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                    }

                    remainder = expressionFunctionAsSpan.Slice(argumentsEndIndex + 1).Trim();
                }
            }
            else
            {
                int nextMethodIndex = expressionFunction.IndexOf('.', methodStartIndex);
                int methodLength = expressionFunction.Length - methodStartIndex;
                int indexerIndex = expressionFunction.IndexOf('[', methodStartIndex);

                // We don't want to consume the indexer
                if (indexerIndex >= 0 && indexerIndex < nextMethodIndex)
                {
                    nextMethodIndex = indexerIndex;
                }

                functionArguments = [];

                if (nextMethodIndex > 0)
                {
                    methodLength = nextMethodIndex - methodStartIndex;
                    remainder = expressionFunctionAsSpan.Slice(nextMethodIndex).Trim();
                }

                ReadOnlySpan<char> netPropertyName = expressionFunctionAsSpan.Slice(methodStartIndex, methodLength).Trim();

                ProjectErrorUtilities.VerifyThrowInvalidProject(netPropertyName.Length > 0, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                // We have been asked for a property or a field
                defaultBindingFlags |= (BindingFlags.GetProperty | BindingFlags.GetField);

                functionName = netPropertyName;
            }

            // either there are no functions left or what we have is another function or an indexer
            if (remainder.IsEmpty || remainder[0] == '.' || remainder[0] == '[')
            {
                functionBuilder.Name = functionName.ToString();
                functionBuilder.Arguments = functionArguments;
                functionBuilder.BindingFlags = defaultBindingFlags;
                functionBuilder.Remainder = remainder.ToString();
            }
            else
            {
                // We ended up with something other than a function expression
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
            }
        }

        /// <summary>
        ///  Formats an attempted property-function invocation for diagnostic output using its evaluated receiver
        ///  and arguments.
        /// </summary>
        /// <param name="objectInstance">The evaluated receiver, or <see langword="null"/> for a static invocation.</param>
        /// <param name="name">The name of the invoked member.</param>
        /// <param name="args">The evaluated arguments, or <see langword="null"/> if they are unavailable.</param>
        /// <param name="context">The expansion context used to format implicit arguments.</param>
        /// <returns>
        ///  A diagnostic representation of the attempted invocation.
        /// </returns>
        private string FormatEvaluatedFunctionInvocation(
            object objectInstance,
            string name,
            object[] args,
            ref readonly ExpanderContext context)
        {
            var builder = StringBuilderCache.Acquire();

            if (objectInstance is null)
            {
                // We don't want to expose the real type name of our intrinsics
                // so we'll replace it with "MSBuild"
                string typeName = _receiverType == typeof(IntrinsicFunctions)
                    ? "MSBuild"
                    : _receiverType.FullName;

                builder.Append('[');
                builder.Append(typeName);
                builder.Append("]::");
                builder.Append(name);
            }
            else
            {
                builder.Append('"');
                builder.Append(objectInstance as string);
                builder.Append('"');
                builder.Append('.');
                builder.Append(name);
            }

            if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
            {
                builder.Append('(');

                if (args is not null)
                {
                    bool isFirst = true;

                    foreach (object arg in args)
                    {
                        AppendMethodArgument(builder, arg, ref isFirst);
                    }

                    if (_receiverType == typeof(IntrinsicFunctions)
                        && name.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase)
                        && args.Length == 1)
                    {
                        AppendMethodArgument(
                            builder,
                            context.LocationDirectory,
                            ref isFirst);
                    }
                }

                builder.Append(')');
            }

            return StringBuilderCache.GetStringAndRelease(builder);

            static void AppendMethodArgument(StringBuilder builder, object argument, ref bool isFirst)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    builder.Append(", ");
                }

                if (argument is null)
                {
                    builder.Append("null");
                }
                else if (argument is string { Length: 0 })
                {
                    builder.Append("''");
                }
                else
                {
                    builder.Append(argument);
                }
            }
        }

        /// <summary>
        /// Check the property function allowlist whether this method is available.
        /// </summary>
        private static bool IsStaticMethodAvailable(Type receiverType, string methodName)
        {
            if (receiverType == typeof(IntrinsicFunctions))
            {
                // These are our intrinsic functions, so we're OK with those
                return true;
            }

            // The escape hatch opens everything. The feature switch also preserves the legacy
            // MSBUILDENABLEALLPROPERTYFUNCTIONS environment-variable behavior in untrimmed builds; under
            // trimming it is substituted false, so this wide gate is removed.
            if (FeatureSwitches.EnableAllPropertyFunctions)
            {
                // anything goes
                return true;
            }

            return AvailableStaticMethods.GetTypeInformationFromTypeCache(receiverType.FullName, methodName) != null;
        }

        private static bool IsInstanceMethodAvailable(Type receiverType, string methodName)
        {
            // The escape hatch opens everything (this preserves the historical behavior, including
            // allowing GetType). The feature switch also preserves the legacy
            // MSBUILDENABLEALLPROPERTYFUNCTIONS environment-variable behavior in untrimmed builds; under
            // trimming it is substituted false, so this wide gate is removed.
            if (FeatureSwitches.EnableAllPropertyFunctions)
            {
                return true;
            }

            // GetType is excluded outside the escape hatch: it returns an open-ended Type that would
            // make the reachable member surface unpredictable.
            if (string.Equals("GetType", methodName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // When restriction is on - the default under trimming, opt-in otherwise - instance
            // "dotting in" is limited to a curated set of receiver types so the members reachable by
            // reflection are predictable and statically known. Under trimming
            // RestrictPropertyFunctionReceivers is substituted true, so the unrestricted 'return true'
            // below is removed, keeping the property-function path trim compatible.
            if (FeatureSwitches.RestrictPropertyFunctionReceivers)
            {
                return PropertyFunctionReceiver.IsAllowed(receiverType, methodName);
            }

            return true;
        }
    }
}
