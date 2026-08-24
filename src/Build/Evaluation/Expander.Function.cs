// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Text;
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
    internal struct Function
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
        /// The source-backed name of the function.
        /// </summary>
        private readonly StringSegment _methodName;

        /// <summary>
        /// The arguments for the function.
        /// </summary>
        private readonly StringSegment[] _arguments;

        /// <summary>
        /// The expression that this function is part of.
        /// </summary>
        private readonly StringSegment _expression;

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
        private readonly StringSegment _remainder;

        /// <summary>
        /// List of properties which have been used but have not been initialized yet.
        /// </summary>
        private PropertiesUseTracker _propertiesUseTracker;

        private readonly IFileSystem _fileSystem;

        private readonly LoggingContext _loggingContext;

        /// <summary>
        /// Construct a function that will be executed during property evaluation.
        /// </summary>
        internal Function(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicFields)]                 Type receiverType,
                StringSegment expression,
                StringSegment methodName,
            StringSegment[] arguments,
            BindingFlags bindingFlags,
            StringSegment remainder,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem,
            LoggingContext loggingContext)
        {
            _methodName = methodName;
            if (arguments == null)
            {
                _arguments = [];
            }
            else
            {
                _arguments = arguments;
            }

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
            _propertiesUseTracker = propertiesUseTracker;
            _fileSystem = fileSystem;
            _loggingContext = loggingContext;
        }

        /// <summary>
        ///  Tries to extract function details from a property function expression.
        /// </summary>
        /// <param name="expressionFunction">The property-function body, e.g. <c>SomeProp.ToLower()</c> or <c>[System.Math]::Max(1, 2)</c>.</param>
        /// <param name="elementLocation">Location used for error reporting.</param>
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
        /// <param name="propertiesUseTracker">Tracks property reads performed while evaluating the function.</param>
        /// <param name="fileSystem">File system abstraction used by file and directory property functions.</param>
        /// <param name="loggingContext">Logging context for the operation; may be <see langword="null"/>.</param>
        /// <param name="function">Receives the parsed function.</param>
        /// <param name="receiver">
        ///  Receives the source-backed property name for an instance function, or a null segment for
        ///  a static function.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> when a function was parsed; otherwise, <see langword="false"/>.
        /// </returns>
        internal static bool TryExtractPropertyFunction(
            string expressionFunction,
            IElementLocation elementLocation,
            object propertyValue,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem,
            LoggingContext loggingContext,
            out Function function,
            out StringSegment receiver)
            => TryExtractPropertyFunction(
                expressionFunction.AsSegment(),
                elementLocation,
                propertyValue,
                propertiesUseTracker,
                fileSystem,
                loggingContext,
                out function,
                out receiver);

        /// <inheritdoc cref="TryExtractPropertyFunction(string, IElementLocation, object, PropertiesUseTracker, IFileSystem, LoggingContext, out Function, out StringSegment)"/>
        internal static bool TryExtractPropertyFunction(
            StringSegment expressionFunction,
            IElementLocation elementLocation,
            object propertyValue,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem,
            LoggingContext loggingContext,
            out Function function,
            out StringSegment receiver)
        {
            function = default;
            receiver = default;

            // Used to aggregate all the components needed for a Function
            FunctionBuilder functionBuilder = new FunctionBuilder { FileSystem = fileSystem, LoggingContext = loggingContext };

            // By default the expression root is the whole function expression
            StringSegment expressionRoot = expressionFunction;

            // The arguments for this function start at the first '('
            // If there are no arguments, then we're a property getter
            var argumentStartIndex = expressionFunction.IndexOf('(');

            // If we have arguments, then we only want the content up to but not including the '('
            if (argumentStartIndex > -1)
            {
                expressionRoot = expressionRoot.Slice(0, argumentStartIndex);
            }

            // In case we ended up with something we don't understand
            ProjectErrorUtilities.VerifyThrowInvalidProject(!expressionRoot.IsEmpty, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction.ValueOrEmpty, String.Empty);

            functionBuilder.Expression = expressionFunction;
            functionBuilder.PropertiesUseTracker = propertiesUseTracker;

            // This is a static method call
            // A static method is the content that follows the last "::", the rest being the type
            if (propertyValue == null && expressionRoot[0] == '[')
            {
                var typeEndIndex = expressionRoot.IndexOf(']');

                if (typeEndIndex < 1)
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", expressionFunction.ValueOrEmpty, String.Empty);
                }

                StringSegment typeName = expressionRoot.Slice(1, typeEndIndex - 1);
                var methodStartIndex = typeEndIndex + 1;

                if (expressionRoot.Length > methodStartIndex + 2 && expressionRoot[methodStartIndex] == ':' && expressionRoot[methodStartIndex + 1] == ':')
                {
                    // skip over the "::"
                    methodStartIndex += 2;
                }
                else
                {
                    // We ended up with something other than a static function expression
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", expressionFunction.ValueOrEmpty, String.Empty);
                }

                ConstructFunction(elementLocation, expressionFunction, argumentStartIndex, methodStartIndex, ref functionBuilder);

                // Locate a type that matches the body of the expression.
                var receiverType = GetTypeForStaticMethod(typeName, functionBuilder.Name);

                if (receiverType == null)
                {
                    // We ended up with something other than a type
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionTypeUnavailable", expressionFunction.ValueOrEmpty, typeName.ValueOrEmpty);
                }

                functionBuilder.SetReceiverType(receiverType);
            }
            else if (expressionFunction[0] == '[') // We have an indexer
            {
                var indexerEndIndex = expressionFunction.IndexOf(']', 1);
                if (indexerEndIndex < 1)
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction.ValueOrEmpty, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                }

                var methodStartIndex = indexerEndIndex + 1;

                functionBuilder.SetReceiverType(propertyValue.GetType());

                ConstructIndexerFunction(expressionFunction, elementLocation, propertyValue, methodStartIndex, indexerEndIndex, ref functionBuilder);
            }
            else // This could be a property reference, or a chain of function calls
            {
                // Look for an instance function call next, such as in SomeStuff.ToLower()
                var methodStartIndex = expressionRoot.IndexOf('.');
                if (methodStartIndex == -1)
                {
                    // We don't have a function invocation in the expression root, return null
                    return false;
                }

                // skip over the '.';
                methodStartIndex++;

                var rootEndIndex = expressionRoot.IndexOf('.');

                // If this is an instance function rather than a static, then we'll capture the name of the property referenced
                StringSegment functionReceiver = expressionRoot.Slice(0, rootEndIndex).Trim();

                // If propertyValue is null (we're not recursing), then we're expecting a valid property name
                if (propertyValue == null && !IsValidPropertyName(functionReceiver))
                {
                    // We extracted something that wasn't a valid property name, fail.
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction.ValueOrEmpty, String.Empty);
                }

                // If we are recursively acting on a type that has been already produced then pass that type inwards (e.g. we are interpreting a function call chain)
                // Otherwise, the receiver of the function is a string
                var receiverType = propertyValue?.GetType() ?? typeof(string);

                functionBuilder.Receiver = functionReceiver;
                functionBuilder.SetReceiverType(receiverType);

                ConstructFunction(elementLocation, expressionFunction, argumentStartIndex, methodStartIndex, ref functionBuilder);
            }

            receiver = functionBuilder.Receiver;
            function = functionBuilder.Build();
            return true;
        }

        /// <summary>
        /// Execute the function on the given instance.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2074:UnrecognizedReflectionPattern",
            Justification = "_receiverType is reassigned from a runtime property value whose type is restricted to the property-function allowlist, whose members are preserved for trimming.")]
        [UnconditionalSuppressMessage("Trimming", "IL2080:UnrecognizedReflectionPattern",
            Justification = "_bindingFlags is masked to AllowedBindingFlags at construction, so it never carries BindingFlags.NonPublic; GetMethods(_bindingFlags) therefore binds only public methods of the property-function allowlist receiver, whose public members are preserved for trimming.")]
        internal object Execute(object objectInstance, IPropertyProvider<P> properties, ExpanderOptions options, IElementLocation elementLocation)
        {
            object functionResult = String.Empty;
            object[] args = null;
            FunctionArguments arguments = new(
                _arguments,
                properties,
                options,
                elementLocation,
                _propertiesUseTracker,
                _fileSystem,
                _receiverType,
                _methodName);

            try
            {
                // If there is no object instance, then the method invocation will be a static
                if (objectInstance == null)
                {
                    // Check that the function that we're going to call is valid to call
                    if (!IsStaticMethodAvailable(_receiverType, _methodName))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _methodName.ValueOrEmpty, _receiverType.FullName);
                    }

                    _bindingFlags |= BindingFlags.Static;
                }
                else
                {
                    // Check that the function that we're going to call is valid to call
                    if (!IsInstanceMethodAvailable(_receiverType, _methodName))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _methodName.ValueOrEmpty, _receiverType.FullName);
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

                // Handle special cases where the object type needs to affect the choice of method
                // The default binder and method invoke, often chooses the incorrect Equals and CompareTo and
                // fails the comparison, because what we have on the right is generally a string.
                // This special casing is to realize that its a comparison that is taking place and handle the
                // argument type coercion accordingly; effectively pre-preparing the argument type so
                // that it matches the left hand side ready for the default binder’s method invoke.
                if (objectInstance != null && arguments.Length == 1 &&
                    (_methodName.Equals("Equals", StringComparison.OrdinalIgnoreCase) ||
                     _methodName.Equals("CompareTo", StringComparison.OrdinalIgnoreCase)))
                {
                    // Support comparison when the lhs is an integer
                    if (arguments.IsFloatingPointRepresentation(0))
                    {
                        if (double.TryParse(objectInstance.ToString(), NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double result))
                        {
                            objectInstance = result;
                            _receiverType = objectInstance.GetType();
                        }
                    }

                    // change the type of the final unescaped string into the destination
                    arguments[0] = Convert.ChangeType(arguments[0], objectInstance.GetType(), CultureInfo.InvariantCulture);
                }

                if (_receiverType == typeof(IntrinsicFunctions))
                {
                    // Special case a few methods that take extra parameters that can't be passed in by the user
                    if (_methodName.Equals("GetPathOfFileAbove") && arguments.Length == 1)
                    {
                        // Append the IElementLocation as a parameter to GetPathOfFileAbove if the user only
                        // specified the file name.  This is syntactic sugar so they don't have to always
                        // include $(MSBuildThisFileDirectory) as a parameter.
                        string startingDirectory = String.IsNullOrWhiteSpace(elementLocation.File) ? String.Empty : Path.GetDirectoryName(elementLocation.File);

                        arguments.Append(startingDirectory);
                    }
                }

                // If we've been asked to construct an instance, then we
                // need to locate an appropriate constructor and invoke it
                if (_methodName.Equals("new", StringComparison.OrdinalIgnoreCase))
                {
                    if (!WellKnownFunctions.TryExecuteWellKnownConstructorNoThrow(_receiverType, out functionResult, ref arguments))
                    {
                        args = arguments.ToObjectArray();
                        functionResult = LateBindExecute(null /* no previous exception */, BindingFlags.Public | BindingFlags.Instance, null /* no instance for a constructor */, args, true /* is constructor */);
                    }
                }
                else
                {
                    bool wellKnownFunctionSuccess = false;

                    try
                    {
                        // First attempt to recognize some well-known functions to avoid binding
                        // and potential first-chance MissingMethodExceptions.
                        wellKnownFunctionSuccess = WellKnownFunctions.TryExecuteWellKnownFunction(_methodName, _receiverType, _fileSystem, out functionResult, objectInstance, ref arguments);

                        if (!wellKnownFunctionSuccess)
                        {
                            // Some well-known functions need evaluated value from properties.
                            wellKnownFunctionSuccess = WellKnownFunctions.TryExecuteWellKnownFunctionWithPropertiesParam(_methodName, _receiverType, _loggingContext, properties, out functionResult, objectInstance, ref arguments);
                        }
                    }
                    // we need to preserve the same behavior on exceptions as the actual binder
                    catch (Exception ex)
                    {
                        string partiallyEvaluated = GenerateStringOfMethodExecuted(
                            _expression.ValueOrEmpty,
                            objectInstance,
                            _methodName,
                            arguments.GetEvaluatedValuesForError());
                        if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                        {
                            return partiallyEvaluated;
                        }

                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message.Replace("\r\n", " "));
                    }

                    if (!wellKnownFunctionSuccess)
                    {
                        // Execute the function given converted arguments
                        // The only exception that we should catch to try a late bind here is missing method
                        // otherwise there is the potential of running a function twice!
                        try
                        {
                            args = arguments.ToObjectArray();

                            // If there are any out parameters, try to figure out their type and create defaults for them as appropriate before calling the method.
                            if (args.Any(a => "out _".Equals(a)))
                            {
                                string methodName = _methodName.ValueOrEmpty;
                                IEnumerable<MethodInfo> methods = _receiverType
                                    .GetMethods(_bindingFlags)
                                    .Where(m => m.Name.Equals(methodName) && m.GetParameters().Length == args.Length);
                                functionResult = GetMethodResult(objectInstance, methods, args, 0);
                            }
                            else
                            {
                                // If there are no out parameters, use InvokeMember using the standard binder - this will match and coerce as needed
                                functionResult = _receiverType.InvokePublicMember(_methodName.ValueOrEmpty, _bindingFlags, objectInstance, args);
                            }
                        }
                        // If we're invoking a method, then there are deeper attempts that can be made to invoke the method.
                        // If not, we were asked to get a property or field but found that we cannot locate it. No further argument coercion is possible, so throw.
                        catch (MissingMethodException ex) when ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                        {
                            // The standard binder failed, so do our best to coerce types into the arguments for the function
                            // This may happen if the types need coercion, but it may also happen if the object represents a type that contains open type parameters, that is, ContainsGenericParameters returns true.
                            functionResult = LateBindExecute(ex, _bindingFlags, objectInstance, args, false /* is not constructor */);
                        }
                    }
                }

                // If the result of the function call is a string, then we need to escape the result
                // so that we maintain the "engine contains escaped data" state.
                // The exception is that the user is explicitly calling MSBuild::Unescape, MSBuild::Escape, or ConvertFromBase64
                if (functionResult is string functionResultString &&
                    !_methodName.Equals("Unescape", StringComparison.OrdinalIgnoreCase) &&
                    !_methodName.Equals("Escape", StringComparison.OrdinalIgnoreCase) &&
                    !_methodName.Equals("ConvertFromBase64", StringComparison.OrdinalIgnoreCase))
                {
                    functionResult = EscapingUtilities.Escape(functionResultString);
                }

                // We have nothing left to parse, so we'll return what we have
                if (_remainder.IsNullOrEmpty)
                {
                    return functionResult;
                }

                // Recursively expand the remaining property body after execution
                return PropertyExpander.ExpandPropertyBody(
                    _remainder,
                    functionResult,
                    properties,
                    options,
                    elementLocation,
                    _propertiesUseTracker,
                    _fileSystem);
            }

            // Exceptions coming from the actual function called are wrapped in a TargetInvocationException
            catch (TargetInvocationException ex)
            {
                // We ended up with something other than a function expression
                string partiallyEvaluated = GenerateStringOfMethodExecuted(
                    _expression.ValueOrEmpty,
                    objectInstance,
                    _methodName,
                    args ?? arguments.GetEvaluatedValuesForError());
                if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    // If the caller wants to ignore errors (in a log statement for example), just return the partially evaluated value
                    return partiallyEvaluated;
                }
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.InnerException.Message.Replace("\r\n", " "));
                return null;
            }

            // Any other exception was thrown by trying to call it
            catch (Exception ex) when (!ExceptionHandling.NotExpectedFunctionException(ex))
            {
                // If there's a :: in the expression, they were probably trying for a static function
                // invocation. Give them some more relevant info in that case
                if (_expression.Contains("::", StringComparison.OrdinalIgnoreCase))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", _expression.ValueOrEmpty, ex.Message.Replace("Microsoft.Build.Evaluation.IntrinsicFunctions.", "[MSBuild]::"));
                }
                else
                {
                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(
                        _expression.ValueOrEmpty,
                        objectInstance,
                        _methodName,
                        args ?? arguments.GetEvaluatedValuesForError());
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message);
                }

                return null;
            }
        }

        private object GetMethodResult(object objectInstance, IEnumerable<MethodInfo> methods, object[] args, int index)
        {
            for (int i = index; i < args.Length; i++)
            {
                if (args[i].Equals("out _"))
                {
                    object toReturn = null;
                    foreach (MethodInfo method in methods)
                    {
                        Type t = method.GetParameters()[i].ParameterType;
                        args[i] = t.CreateDefault();
                        object currentReturnValue = GetMethodResult(objectInstance, methods, args, i + 1);
                        if (currentReturnValue is not null)
                        {
                            if (toReturn is null)
                            {
                                toReturn = currentReturnValue;
                            }
                            else if (!toReturn.Equals(currentReturnValue))
                            {
                                // There were multiple methods that seemed viable and gave different results. We can't differentiate between them so throw.
                                ErrorUtilities.ThrowArgument("CouldNotDifferentiateBetweenCompatibleMethods", _methodName.ValueOrEmpty, args.Length);
                                return null;
                            }
                        }
                    }

                    return toReturn;
                }
            }

            try
            {
                return _receiverType.InvokePublicMember(_methodName.ValueOrEmpty, _bindingFlags, objectInstance, args) ?? "null";
            }
            catch (Exception)
            {
                // This isn't a viable option, but perhaps another set of parameters will work.
                return null;
            }
        }

        /// <summary>
        /// Given a type name and method name, try to resolve the type.
        /// </summary>
        /// <param name="typeName">May be full name or assembly qualified name.</param>
        /// <param name="simpleMethodName">simple name of the method.</param>
        /// <returns></returns>
        [UnconditionalSuppressMessage("Trimming", "IL2096:UnrecognizedReflectionPattern",
            Justification = "The type name is resolved against the curated AvailableStaticMethods allowlist; the case-insensitive lookup only resolves to allowlist types, whose members are preserved for trimming.")]
        private static Type GetTypeForStaticMethod(StringSegment typeName, StringSegment simpleMethodName)
        {
            Type receiverType;
            Tuple<string, Type> cachedTypeInformation;

            // If we don't have a type name, we already know that we won't be able to find a type.
            // Go ahead and return here -- otherwise the Type.GetType() calls below will throw.
            if (typeName.IsNullOrWhiteSpace())
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
                    Assumed.NotNull(receiverType, $"Type information for {typeName.ValueOrEmpty} was present in the allowlist cache as {assemblyQualifiedTypeName} but the type could not be loaded.");

                    // If we've used it once, chances are that we'll be using it again
                    // We can record the type here since we know it's available for calling from the fact that is was in the AvailableStaticMethods table
                    AvailableStaticMethods.TryAdd(
                        typeName.ValueOrEmpty,
                        simpleMethodName.ValueOrEmpty,
                        new Tuple<string, Type>(assemblyQualifiedTypeName, receiverType));

                    return receiverType;
                }
            }

            string typeNameString = typeName.ValueOrEmpty;

            // Get the type from mscorlib (or the currently running assembly)
            receiverType = Type.GetType(typeNameString, throwOnError: false, ignoreCase: true);

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
                receiverType = GetTypeFromAssembly(typeNameString, "System");

                // Next in System.Core
                if (receiverType == null)
                {
                    receiverType = GetTypeFromAssembly(typeNameString, "System.Core");
                }

                // We didn't find the type, so try to find it using the namespace
                if (receiverType == null)
                {
                    receiverType = GetTypeFromAssemblyUsingNamespace(typeNameString);
                }

                if (receiverType != null)
                {
                    // If we've used it once, chances are that we'll be using it again
                    // We can cache the type here, since all functions are enabled
                    AvailableStaticMethods.TryAdd(typeNameString, new Tuple<string, Type>(typeNameString, receiverType));
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
        private static void ConstructIndexerFunction(
            StringSegment expressionFunction,
            IElementLocation elementLocation,
            object propertyValue,
            int methodStartIndex,
            int indexerEndIndex,
            ref FunctionBuilder functionBuilder)
        {
            StringSegment argumentsContent = expressionFunction.Slice(1, indexerEndIndex - 1);
            StringSegment[] functionArguments;

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
            StringSegment functionName;
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
            functionBuilder.Remainder = expressionFunction.Slice(methodStartIndex);
        }

        /// <summary>
        /// Extracts the name, arguments, binding flags, and invocation type for a static or instance function.
        /// Also extracts the remainder of the expression that is not part of this function.
        /// </summary>
        private static void ConstructFunction(
            IElementLocation elementLocation,
            StringSegment expressionFunction,
            int argumentStartIndex,
            int methodStartIndex,
            ref FunctionBuilder functionBuilder)
        {
            // The unevaluated and unexpanded arguments for this function
            StringSegment[] functionArguments;

            // The name of the function that will be invoked
            StringSegment functionName;

            // What's left of the expression once the function has been constructed
            StringSegment remainder = default;

            // The binding flags that we will use for this function's execution
            BindingFlags defaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

            StringSegment expressionSubstring =
                argumentStartIndex > -1
                    ? expressionFunction.Slice(methodStartIndex, argumentStartIndex - methodStartIndex)
                    : default;

            // There are arguments that need to be passed to the function
            if (argumentStartIndex > -1 && !expressionSubstring.Contains('.'))
            {
                // separate the function and the arguments
                functionName = expressionSubstring.Trim();

                // Skip the '('
                argumentStartIndex++;

                // Scan for the matching closing bracket, skipping any nested ones
                int argumentsEndIndex = ScanForClosingParenthesis(expressionFunction.AsSpan(), argumentStartIndex);

                if (argumentsEndIndex == -1)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction.ValueOrEmpty, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
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
                    StringSegment argumentsContent =
                        expressionFunction.Slice(argumentStartIndex, argumentsEndIndex - argumentStartIndex);

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

                    remainder = expressionFunction.Slice(argumentsEndIndex + 1).Trim();
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
                    remainder = expressionFunction.Slice(nextMethodIndex).Trim();
                }

                StringSegment netPropertyName = expressionFunction.Slice(methodStartIndex, methodLength).Trim();

                ProjectErrorUtilities.VerifyThrowInvalidProject(netPropertyName.Length > 0, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction.ValueOrEmpty, String.Empty);

                // We have been asked for a property or a field
                defaultBindingFlags |= (BindingFlags.GetProperty | BindingFlags.GetField);

                functionName = netPropertyName;
            }

            // either there are no functions left or what we have is another function or an indexer
            if (remainder.IsNullOrEmpty || remainder[0] == '.' || remainder[0] == '[')
            {
                functionBuilder.Name = functionName;
                functionBuilder.Arguments = functionArguments;
                functionBuilder.BindingFlags = defaultBindingFlags;
                functionBuilder.Remainder = remainder;
            }
            else
            {
                // We ended up with something other than a function expression
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction.ValueOrEmpty, String.Empty);
            }
        }

        /// <summary>
        /// Coerce the arguments according to the parameter types
        /// Will only return null if the coercion didn't work due to an InvalidCastException.
        /// </summary>
        private static object[] CoerceArguments(object[] args, ParameterInfo[] parameters)
        {
            object[] coercedArguments = new object[args.Length];

            try
            {
                // Do our best to coerce types into the arguments for the function
                for (int n = 0; n < parameters.Length; n++)
                {
                    if (args[n] == null)
                    {
                        // We can't coerce (object)null -- that's as general
                        // as it can get!
                        continue;
                    }

                    // Here we have special case conversions on a type basis
                    if (parameters[n].ParameterType == typeof(char[]))
                    {
                        coercedArguments[n] = args[n].ToString().ToCharArray();
                    }
                    else if (parameters[n].ParameterType.GetTypeInfo().IsEnum && args[n] is string v && v.Contains('.'))
                    {
                        Type enumType = parameters[n].ParameterType;
                        string typeLeafName = $"{enumType.Name}.";
                        string typeFullName = $"{enumType.FullName}.";

                        // Enum.parse expects commas between enum components
                        // We'll support the C# type | syntax too
                        // We'll also allow the user to specify the leaf or full type name on the enum
                        string argument = args[n].ToString().Replace('|', ',').Replace(typeFullName, "").Replace(typeLeafName, "");

                        // Parse the string representation of the argument into the destination enum
                        coercedArguments[n] = Enum.Parse(enumType, argument);
                    }
                    else
                    {
                        // change the type of the final unescaped string into the destination
                        coercedArguments[n] = Convert.ChangeType(args[n], parameters[n].ParameterType, CultureInfo.InvariantCulture);
                    }
                }
            }
            // The coercion failed therefore we return null
            catch (InvalidCastException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                // https://github.com/dotnet/msbuild/issues/2882
                // test: PropertyFunctionMathMaxOverflow
                return null;
            }

            return coercedArguments;
        }

        /// <summary>
        /// Make an attempt to create a string showing what we were trying to execute when we failed.
        /// This will show any intermediate evaluation which may help the user figure out what happened.
        /// </summary>
        private string GenerateStringOfMethodExecuted(string expression, object objectInstance, StringSegment name, object[] args)
        {
            string methodName = name.ValueOrEmpty;
            string parameters = String.Empty;
            if (args != null)
            {
                foreach (object arg in args)
                {
                    if (arg == null)
                    {
                        parameters += "null";
                    }
                    else
                    {
                        string argString = arg.ToString();
                        if (arg is string && argString.Length == 0)
                        {
                            parameters += "''";
                        }
                        else
                        {
                            parameters += arg.ToString();
                        }
                    }

                    parameters += ", ";
                }

                if (parameters.Length > 2)
                {
                    parameters = parameters.Substring(0, parameters.Length - 2);
                }
            }

            if (objectInstance == null)
            {
                string typeName = _receiverType.FullName;

                // We don't want to expose the real type name of our intrinsics
                // so we'll replace it with "MSBuild"
                if (_receiverType == typeof(IntrinsicFunctions))
                {
                    typeName = "MSBuild";
                }
                if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                {
                    return $"[{typeName}]::{methodName}({parameters})";
                }
                else
                {
                    return $"[{typeName}]::{methodName}";
                }
            }
            else
            {
                string propertyValue = $"\"{objectInstance as string}\"";

                if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                {
                    return $"{propertyValue}.{methodName}({parameters})";
                }
                else
                {
                    return $"{propertyValue}.{methodName}";
                }
            }
        }

        /// <summary>
        /// Check the property function allowlist whether this method is available.
        /// </summary>
        private static bool IsStaticMethodAvailable(Type receiverType, StringSegment methodName)
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

        private static bool IsInstanceMethodAvailable(Type receiverType, StringSegment methodName)
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
            if (methodName.Equals("GetType", StringComparison.OrdinalIgnoreCase))
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

        /// <summary>
        /// Finds a public method on the receiver type by name (case-insensitive) and exact
        /// parameter-type signature, filtering by the current binding flags (instance/static).
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2080:UnrecognizedReflectionPattern",
            Justification = "_bindingFlags is masked to AllowedBindingFlags at construction, so it never carries BindingFlags.NonPublic; GetMethods(_bindingFlags) therefore binds only public methods of the property-function allowlist receiver, whose public members are preserved for trimming.")]
        private MethodInfo FindPublicMethodBySignature(string methodName, Type[] parameterTypes)
        {
            foreach (MethodInfo method in _receiverType.GetMethods(_bindingFlags))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length)
                {
                    continue;
                }

                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != parameterTypes[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return method;
                }
            }

            return null;
        }

        /// <summary>
        /// Construct and instance of objectType based on the constructor or method arguments provided.
        /// Arguments must never be null.
        /// </summary>
        // This reflective invoke can in principle reach any public method of an allowlisted receiver type.
        // The only such method carrying [RequiresDynamicCode] is Enum.GetValues(Type) (on System.Enum) -
        // this is the IL3050 suppressed below.
        //
        // Reaching it would require an author to pass a System.Type argument, and a property function has no
        // way to produce one: string does not coerce to Type (evaluation reports MSB4186, "method not
        // found"), and [System.Type]::GetType(...) is not an available property function (MSB4185, even with
        // MSBUILDENABLEALLPROPERTYFUNCTIONS=1). The receiver is a runtime Type, so the static
        // Enum.GetValues<TEnum>() overload cannot be substituted either. The case is therefore blocked before
        // this invoke (identically on JIT and AOT) and would still fail observably (InvalidProjectFileException)
        // if reached - never silently. Verified under Native AOT by src/aot-validation/PropertyFunctionAotTests.cs.
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification = "The only RDC method reachable here is Enum.GetValues(Type), which is unreachable via property functions; see comment above.")]
        [UnconditionalSuppressMessage("Trimming", "IL2080:UnrecognizedReflectionPattern",
            Justification = "_bindingFlags is masked to AllowedBindingFlags at construction, so it never carries BindingFlags.NonPublic; GetMethods(_bindingFlags) therefore binds only public methods of the property-function allowlist receiver, whose public members are preserved for trimming.")]
        private object LateBindExecute(Exception ex, BindingFlags bindingFlags, object objectInstance /* null unless instance method */, object[] args, bool isConstructor)
        {
            // First let's try for a method where all arguments are strings..
            Type[] types = new Type[_arguments.Length];
            for (int n = 0; n < _arguments.Length; n++)
            {
                types[n] = typeof(string);
            }

            MethodBase memberInfo;
            if (isConstructor)
            {
                memberInfo = _receiverType.GetConstructor(types);
            }
            else
            {
                // Match a public method by name (case-insensitive) and exact parameter signature.
                // Equivalent to the prior GetMethod(..., BindingFlags, ...) call but uses the
                // public-only GetMethods(_bindingFlags) call, since BindingFlags.NonPublic is never set here.
                memberInfo = FindPublicMethodBySignature(_methodName.ValueOrEmpty, types);
            }

            // If we didn't get a match on all string arguments,
            // search for a method with the right number of arguments
            if (memberInfo == null)
            {
                // Gather all methods that may match
                IEnumerable<MethodBase> members;
                if (isConstructor)
                {
                    members = _receiverType.GetConstructors();
                }
                else if (_receiverType == typeof(IntrinsicFunctions) && IntrinsicFunctionOverload.IsKnownOverloadMethodName(_methodName.ValueOrEmpty))
                {
                    // FindMembers is invoked on the statically-known IntrinsicFunctions type (the
                    // only receiver that reaches this branch), so its broad reflection contract is
                    // satisfied by that concrete, rooted type rather than the receiver-type field.
                    MemberInfo[] foundMembers = typeof(IntrinsicFunctions).FindMembers(
                        MemberTypes.Method,
                        bindingFlags,
                        (info, criteria) => string.Equals(info.Name, (string)criteria, StringComparison.OrdinalIgnoreCase),
                        _methodName.ValueOrEmpty);
                    Array.Sort(foundMembers, IntrinsicFunctionOverload.IntrinsicFunctionOverloadMethodComparer);
                    members = foundMembers.Cast<MethodBase>();
                }
                else
                {
                    string methodName = _methodName.ValueOrEmpty;
                    members = _receiverType
                        .GetMethods(_bindingFlags)
                        .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase));
                }

                foreach (MethodBase member in members)
                {
                    ParameterInfo[] parameters = member.GetParameters();

                    // Simple match on name and number of params, we will be case insensitive
                    if (parameters.Length == _arguments.Length)
                    {
                        // Try to find a method with the right name, number of arguments and
                        // compatible argument types
                        // we have a match on the name and argument number
                        // now let's try to coerce the arguments we have
                        // into the arguments on the matching method
                        object[] coercedArguments = CoerceArguments(args, parameters);

                        if (coercedArguments != null)
                        {
                            // We have a complete match
                            memberInfo = member;
                            args = coercedArguments;
                            break;
                        }
                    }
                }
            }

            object functionResult = null;

            // We have a match and coerced arguments, let's construct..
            if (memberInfo != null && args != null)
            {
                if (isConstructor)
                {
                    functionResult = ((ConstructorInfo)memberInfo).Invoke(args);
                }
                else
                {
                    functionResult = ((MethodInfo)memberInfo).Invoke(objectInstance /* null if static method */, args);
                }
            }
            else if (!isConstructor)
            {
                throw ex;
            }

            if (functionResult == null && isConstructor)
            {
                throw new TargetInvocationException(new MissingMethodException());
            }

            return functionResult;
        }
    }
}
