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
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private struct BoundFunction
    {
        private enum WellKnownExecutionStatus
        {
            NotHandled,
            Handled,
            ReturnImmediately,
        }

        /// <summary>
        /// The type of this function's receiver.
        /// </summary>
        /// <remarks>
        /// Property-function evaluation only ever binds public members (BindingFlags.NonPublic is
        /// never set on this path), so only the public member surface needs to be preserved for
        /// trimming. Keep in sync with AvailableStaticMembers.PropertyFunctionMembers, which preserves the same
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
        private readonly StringSegment _methodName;

        /// <summary>
        /// The arguments for the function.
        /// </summary>
        private FunctionArguments _arguments;

        private readonly StringSegment _invocationText;
        private readonly ReceiverKind _receiverKind;
        private readonly object? _receiverValue;

        /// <summary>
        /// The complete set of <see cref="BindingFlags"/> the property-function binder is permitted
        /// to use. This set intentionally excludes <see cref="BindingFlags.NonPublic"/>: property
        /// functions only ever bind public members. That exclusion is what lets a receiver type
        /// preserve only its public member surface for trimming (see AvailableStaticMembers.PropertyFunctionMembers)
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
        ///  Always a subset of <see cref="AllowedBindingFlags"/>, constrained at construction so it can
        ///  never carry <see cref="BindingFlags.NonPublic"/>.
        /// </remarks>
        private readonly BindingFlags _bindingFlags;

        /// <summary>
        /// List of properties which have been used but have not been initialized yet.
        /// </summary>
        internal BoundFunction(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicFields)]
            Type receiverType,
            object? receiverValue,
            StringSegment invocationText,
            ReceiverKind receiverKind,
            StringSegment methodName,
            FunctionArguments arguments,
            BindingFlags bindingFlags)
        {
            _methodName = methodName;
            _arguments = arguments;

            _receiverValue = receiverValue;
            _invocationText = invocationText;
            _receiverKind = receiverKind;
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
        }

        private static bool IsFileSystemReceiver(Type receiverType)
            => IsFileOrDirectoryReceiver(receiverType)
            || receiverType == typeof(System.IO.Path);

        private static bool IsFileOrDirectoryReceiver(Type receiverType)
            => receiverType == typeof(System.IO.File)
            || receiverType == typeof(System.IO.Directory);

        private static bool ShouldMaterializeArgumentsOnAccess(Type receiverType, StringSegment methodName)
            => receiverType == typeof(System.IO.Path)
            || methodName.Equals("new", StringComparison.OrdinalIgnoreCase)
            || methodName.Equals("Equals", StringComparison.OrdinalIgnoreCase)
            || methodName.Equals("CompareTo", StringComparison.OrdinalIgnoreCase)
            || Traits.Instance.LogPropertyFunctionsRequiringReflection;

        private ArgumentMaterializer CreateArgumentMaterializer(in ExpansionContext context)
            => new(context, _receiverType, _methodName);

        private static string? GetStartingDirectory(in ExpansionContext context)
            => string.IsNullOrWhiteSpace(context.Location.File)
                ? string.Empty
                : Path.GetDirectoryName(context.Location.File);

        /// <summary>
        /// Determines whether the argument at <paramref name="argIndex"/> for a System.IO.File
        /// or System.IO.Directory method is a file/directory path that should be resolved
        /// against the thread-local working directory.
        /// </summary>
        private static bool IsFileOrDirectoryPathArgument(StringSegment methodName, int argIndex)
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
                return methodName.Equals("Copy", StringComparison.OrdinalIgnoreCase)
                    || methodName.Equals("Move", StringComparison.OrdinalIgnoreCase)
                    || methodName.Equals("Replace", StringComparison.OrdinalIgnoreCase);
            }

            // Third argument is the backup path for Replace.
            if (argIndex == 2)
            {
                return methodName.Equals("Replace", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private sealed class ArgumentMaterializer(
            ExpansionContext context,
            Type receiverType,
            StringSegment methodName) : IFunctionArgumentMaterializer
        {
            public object? Materialize(StringSegment source, int index)
            {
                object? argument = PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(
                    source.Value,
                    context);

                if (argument is not string argumentValue)
                {
                    return argument;
                }

                if (IsFileSystemReceiver(receiverType))
                {
                    argumentValue = FileUtilities.FixFilePath(argumentValue);
                }

                argumentValue = EscapingUtilities.UnescapeAll(argumentValue);

                // In -mt mode, resolve File/Directory path arguments against the thread-local project directory.
                // Resolve only after unescaping so MSBuild escape processing cannot corrupt the filesystem path.
                if (IsFileOrDirectoryReceiver(receiverType)
                    && IsFileOrDirectoryPathArgument(methodName, index))
                {
                    AbsolutePath? resolved = FileUtilities.MakeFullPathFromThreadWorkingDirectory(argumentValue);
                    if (resolved.HasValue)
                    {
                        argumentValue = (string)resolved.GetValueOrDefault();
                    }
                }

                return argumentValue;
            }
        }

        /// <summary>
        ///  Executes the function on its bound receiver.
        /// </summary>
        /// <param name="context">The expansion context.</param>
        /// <param name="loggingContext">The logging context for the invocation.</param>
        /// <param name="result">The function result.</param>
        /// <returns>
        ///  <see langword="true"/> when execution succeeds; otherwise, <see langword="false"/> when
        ///  the expansion options convert an execution failure to a partially evaluated result.
        /// </returns>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2074:UnrecognizedReflectionPattern",
            Justification = "_receiverType is reassigned from a runtime property value whose type is restricted to the property-function allowlist, whose members are preserved for trimming.")]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2080:UnrecognizedReflectionPattern",
            Justification = "_bindingFlags is masked to AllowedBindingFlags at construction, so it never carries BindingFlags.NonPublic; GetMethods(_bindingFlags) therefore binds only public methods of the property-function allowlist receiver, whose public members are preserved for trimming.")]
        public bool Execute(in ExpansionContext context, LoggingContext? loggingContext, out object? result)
        {
            object? functionResult = string.Empty;
            object?[]? args = null;
            object? objectInstance = _receiverValue;
            ArgumentMaterializer? argumentMaterializer = null;

            try
            {
                // The object that we're about to call methods on may have escaped characters
                // in it, we want to operate on the unescaped string in the function, just as we
                // want to pass arguments that are unescaped (see below)
                if (objectInstance is string objectInstanceString)
                {
                    objectInstance = EscapingUtilities.UnescapeAll(objectInstanceString);
                }

                if (_arguments.Count > 0
                    && (ShouldMaterializeArgumentsOnAccess(_receiverType, _methodName)
                        || _arguments.ContainsExpandableExpression()))
                {
                    argumentMaterializer = CreateArgumentMaterializer(in context);
                    _arguments.ConfigureMaterialization(argumentMaterializer, materializeAllArguments: ShouldMaterializeArgumentsOnAccess(_receiverType, _methodName));
                }

                WellKnownExecutionStatus wellKnownStatus = TryExecuteWellKnownFunction(
                    objectInstance,
                    _arguments,
                    in context,
                    loggingContext,
                    out functionResult);

                if (wellKnownStatus == WellKnownExecutionStatus.ReturnImmediately)
                {
                    result = functionResult;
                    return false;
                }

                if (wellKnownStatus == WellKnownExecutionStatus.Handled)
                {
                    result = CompleteExecution(functionResult);
                    return true;
                }

                if (argumentMaterializer is null && _arguments.Count > 0)
                {
                    argumentMaterializer = CreateArgumentMaterializer(in context);
                    _arguments.ConfigureMaterialization(argumentMaterializer, materializeAllArguments: false);
                }

                args = _arguments.MaterializeAll();

                // Handle special cases where the object type needs to affect the choice of method
                // The default binder and method invoke, often chooses the incorrect Equals and CompareTo and
                // fails the comparison, because what we have on the right is generally a string.
                // This special casing is to realize that its a comparison that is taking place and handle the
                // argument type coercion accordingly; effectively pre-preparing the argument type so
                // that it matches the left hand side ready for the default binder’s method invoke.
                if (objectInstance != null
                    && args.Length == 1
                    && (_methodName.Equals("Equals", StringComparison.OrdinalIgnoreCase)
                        || _methodName.Equals("CompareTo", StringComparison.OrdinalIgnoreCase)))
                {
                    // Support comparison when the lhs is an integer
                    if (FunctionArguments.IsFloatingPointRepresentation(args[0]))
                    {
                        if (double.TryParse(objectInstance.ToString(), NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double numericReceiver))
                        {
                            objectInstance = numericReceiver;
                            _receiverType = objectInstance.GetType();
                        }
                    }

                    // change the type of the final unescaped string into the destination
                    args[0] = Convert.ChangeType(args[0], objectInstance.GetType(), CultureInfo.InvariantCulture);
                }

                // If we've been asked to construct an instance, then we
                // need to locate an appropriate constructor and invoke it
                if (_methodName.Equals("new", StringComparison.OrdinalIgnoreCase))
                {
                    functionResult = LateBindExecute(ex: null, BindingFlags.Public | BindingFlags.Instance, objectInstance: null, args, isConstructor: true);
                }
                else
                {
                    using RefArrayBuilder<int> outArgIndices = default;
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] is string argument
                            && argument.Equals("out _", StringComparison.Ordinal))
                        {
                            outArgIndices.Add(i);
                        }
                    }

                    if (!outArgIndices.IsEmpty)
                    {
                        // Bind out placeholders before invoking so overload probing cannot execute user code.
                        functionResult = BindAndInvokeMethodWithOutArguments(objectInstance, args, outArgIndices.AsSpan());
                    }
                    else
                    {
                        // Execute the function given converted arguments. The only exception that should trigger
                        // late binding is a missing method; otherwise user code could execute twice.
                        try
                        {
                            functionResult = _receiverType.InvokePublicMember(_methodName.ValueOrEmpty, _bindingFlags, objectInstance, args);
                        }
                        catch (MissingMethodException ex) when ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                        {
                            // The standard binder failed, so do our best to coerce types into the arguments for the function.
                            functionResult = LateBindExecute(ex, _bindingFlags, objectInstance, args, isConstructor: false);
                        }
                    }
                }

                result = CompleteExecution(functionResult);
                return true;
            }

            // Exceptions coming from the actual function called are wrapped in a TargetInvocationException
            catch (TargetInvocationException ex)
            {
                // We ended up with something other than a function expression
                string partiallyEvaluated = GenerateStringOfMethodExecuted(objectInstance, _methodName, args, in context);

                if (context.Options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    // If the caller wants to ignore errors (in a log statement for example), just return the partially evaluated value
                    result = partiallyEvaluated;
                    return false;
                }

                context.Errors.InvalidPropertyFunction.Throw(
                    partiallyEvaluated,
                    ex.InnerException?.Message.Replace("\r\n", " ") ?? string.Empty);
                result = null;
                return false;
            }

            // Any other exception was thrown by trying to call it
            catch (Exception ex) when (!ExceptionHandling.NotExpectedFunctionException(ex))
            {
                // If there's a :: in this operation, they were probably trying for a static function
                // invocation. Give them some more relevant info in that case.
                if (_receiverKind == ReceiverKind.Static)
                {
                    context.Errors.InvalidStaticPropertyFunction.Throw(
                        _invocationText,
                        ex.Message.Replace("Microsoft.Build.Evaluation.IntrinsicFunctions.", "[MSBuild]::"));
                }
                else
                {
                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(objectInstance, _methodName, args, in context);
                    context.Errors.InvalidPropertyFunction.Throw(partiallyEvaluated, ex.Message);
                }

                result = null;
                return false;
            }
            finally
            {
                _arguments.ClearMaterializer();
            }
        }

        // If the result of the function call is a string, escape it to maintain the engine's escaped-data state.
        // Escape/Unescape/ConvertFromBase64 already return data in their intended representation.
        private readonly object? CompleteExecution(object? functionResult)
            => functionResult is string s
            && !_methodName.Equals("Unescape", StringComparison.OrdinalIgnoreCase)
            && !_methodName.Equals("Escape", StringComparison.OrdinalIgnoreCase)
            && !_methodName.Equals("ConvertFromBase64", StringComparison.OrdinalIgnoreCase)
                ? EscapingUtilities.Escape(s)
                : functionResult;

        private WellKnownExecutionStatus TryExecuteWellKnownFunction(
            object? objectInstance,
            FunctionArguments args,
            in ExpansionContext context,
            LoggingContext? loggingContext,
            out object? functionResult)
        {
            try
            {
                if (WellKnownFunctions.TryExecuteWellKnownFunction(
                    _methodName,
                    _receiverType,
                    context.FileSystem,
                    GetStartingDirectory(in context),
                    out functionResult,
                    objectInstance,
                    args)
                    || WellKnownFunctions.TryExecuteWellKnownFunctionWithPropertiesParam(
                        _methodName,
                        _receiverType,
                        loggingContext,
                        context.Properties,
                        out functionResult,
                        objectInstance,
                        args))
                {
                    return WellKnownExecutionStatus.Handled;
                }
            }
            catch (Exception ex)
            {
                string partiallyEvaluated = GenerateStringOfMethodExecuted(objectInstance, _methodName, args.ToObjectArray(), in context);

                if (context.Options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    functionResult = partiallyEvaluated;
                    return WellKnownExecutionStatus.ReturnImmediately;
                }

                context.Errors.InvalidPropertyFunction.Throw(
                    partiallyEvaluated,
                    ex.Message.Replace("\r\n", " "));
            }

            functionResult = null;
            return WellKnownExecutionStatus.NotHandled;
        }

        /// <summary>
        ///  Binds and invokes a method call containing discarded <c>out</c> arguments.
        /// </summary>
        /// <param name="objectInstance">The instance receiver, or <see langword="null"/> for a static method.</param>
        /// <param name="args">The materialized method arguments.</param>
        /// <param name="outArgIndices">The indices of arguments written as <c>out _</c>.</param>
        /// <returns>
        ///  The method result, or <see langword="null"/> when no compatible method is found or the method returns
        ///  <see langword="null"/>.
        /// </returns>
        /// <remarks>
        ///  Candidate discovery and binding do not execute user code. Only the method selected by the default binder
        ///  is invoked.
        /// </remarks>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2080:UnrecognizedReflectionPattern",
            Justification = "_bindingFlags is masked to AllowedBindingFlags at construction, so it never carries BindingFlags.NonPublic; GetMethods(_bindingFlags) therefore binds only public methods of the property-function allowlist receiver, whose public members are preserved for trimming.")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private object? BindAndInvokeMethodWithOutArguments(object? objectInstance, object?[] args, ReadOnlySpan<int> outArgIndices)
        {
            MethodInfo[] candidates = _receiverType.GetMethods(_bindingFlags);
            int candidateCount = 0;

            // Compact compatible declarations in place so the binder cannot consider another member name,
            // arity, or a normal parameter at a discarded-out position.
            for (int i = 0; i < candidates.Length; i++)
            {
                MethodInfo candidate = candidates[i];
                if (!_methodName.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length == args.Length
                    && HasOutParameters(parameters, outArgIndices))
                {
                    candidates[candidateCount++] = candidate;
                }
            }

            if (candidateCount == 0)
            {
                return null;
            }

            Array.Resize(ref candidates, candidateCount);

            // BindToMethod may mutate or replace its argument array. Clone it so FunctionArguments retains the
            // original materialized values for diagnostics.
            object?[] boundArgs = (object?[])args.Clone();

            // An out argument has no input value. Null leaves its type unconstrained so the binder can use the
            // candidate parameter metadata and the remaining arguments to select an overload.
            foreach (int index in outArgIndices)
            {
                boundArgs[index] = null;
            }

            MethodBase? method;
            try
            {
                // Bind without invoking. The binder may replace this local args array with converted arguments,
                // which is the array passed to the selected method below.
                method = Type.DefaultBinder.BindToMethod(
                    _bindingFlags,
                    candidates,
                    ref boundArgs,
                    modifiers: null,
                    culture: CultureInfo.InvariantCulture,
                    names: null,
                    out _);
            }
            catch (MissingMethodException)
            {
                return null;
            }
            catch (AmbiguousMatchException)
            {
                string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
                    "CouldNotDifferentiateBetweenCompatibleMethods",
                    _methodName,
                    boundArgs.Length);

                throw new ArgumentException(message);
            }

            // Invoke exactly once after binding; method-body exceptions must propagate to the normal error path.
            return method?.Invoke(objectInstance, boundArgs);

            static bool HasOutParameters(ParameterInfo[] parameters, ReadOnlySpan<int> outArgIndices)
            {
                foreach (int index in outArgIndices)
                {
                    if (!parameters[index].IsOut)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Coerce the arguments according to the parameter types
        /// Will only return null if the coercion didn't work due to an InvalidCastException.
        /// </summary>
        private static object?[]? CoerceArguments(object?[] args, ParameterInfo[] parameters)
        {
            object?[] coercedArguments = new object?[args.Length];

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
                        coercedArguments[n] = (args[n]?.ToString() ?? string.Empty).ToCharArray();
                    }
                    else if (
                        parameters[n].ParameterType.GetTypeInfo().IsEnum
                        && args[n] is string v
                        && v.IndexOf('.') >= 0)
                    {
                        Type enumType = parameters[n].ParameterType;
                        string typeLeafName = $"{enumType.Name}.";
                        string typeFullName = $"{enumType.FullName}.";

                        // Enum.parse expects commas between enum components
                        // We'll support the C# type | syntax too
                        // We'll also allow the user to specify the leaf or full type name on the enum
                        string argument = (args[n]?.ToString() ?? string.Empty).Replace('|', ',').Replace(typeFullName, "").Replace(typeLeafName, "");

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
        private string GenerateStringOfMethodExecuted(
            object? objectInstance,
            StringSegment name,
            object?[]? args,
            in ExpansionContext context)
        {
            StringBuilder builder = new();
            if (objectInstance == null)
            {
                builder.Append('[');
                builder.Append(_receiverType == typeof(IntrinsicFunctions) ? "MSBuild" : _receiverType.FullName);
                builder.Append("]::");
            }
            else
            {
                builder.Append('"');
                builder.Append(objectInstance as string);
                builder.Append("\".");
            }

            if (name.HasValue)
            {
                builder.Append(name.Buffer, name.Offset, name.Length);
            }

            if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
            {
                builder.Append('(');
                bool hasArgument = false;

                if (args != null)
                {
                    foreach (object? arg in args)
                    {
                        if (hasArgument)
                        {
                            builder.Append(", ");
                        }

                        AppendFunctionArgument(builder, arg);
                        hasArgument = true;
                    }

                    // To aid in diagnostics, we include the starting directory as an extra argument to 'GetPathOfFileAbove'
                    // when only one argument is provided.
                    if (_receiverType == typeof(IntrinsicFunctions)
                        && name.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase)
                        && args.Length == 1)
                    {
                        builder.Append(", ");
                        AppendFunctionArgument(builder, GetStartingDirectory(in context));
                    }
                }

                builder.Append(')');
            }

            return builder.ToString();

            static void AppendFunctionArgument(StringBuilder builder, object? argument)
            {
                if (argument == null)
                {
                    builder.Append("null");
                }
                else if (argument is string text)
                {
                    builder.Append(text.Length == 0 ? "''" : text);
                }
                else
                {
                    builder.Append(argument);
                }
            }
        }

        /// <summary>
        /// Finds a public method on the receiver type by name (case-insensitive) and exact
        /// parameter-type signature, filtering by the current binding flags (instance/static).
        /// </summary>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2080:UnrecognizedReflectionPattern",
            Justification = "_bindingFlags is masked to AllowedBindingFlags at construction, so it never carries BindingFlags.NonPublic; GetMethods(_bindingFlags) therefore binds only public methods of the property-function allowlist receiver, whose public members are preserved for trimming.")]
        private MethodInfo? FindPublicMethodBySignature(StringSegment methodName, Type[] parameterTypes)
        {
            foreach (MethodInfo method in _receiverType.GetMethods(_bindingFlags))
            {
                if (!methodName.Equals(method.Name, StringComparison.OrdinalIgnoreCase))
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
        [UnconditionalSuppressMessage(
            "AOT",
            "IL3050:RequiresDynamicCode",
            Justification = "The only RDC method reachable here is Enum.GetValues(Type), which is unreachable via property functions; see comment above.")]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2080:UnrecognizedReflectionPattern",
            Justification = "_bindingFlags is masked to AllowedBindingFlags at construction, so it never carries BindingFlags.NonPublic; GetMethods(_bindingFlags) therefore binds only public methods of the property-function allowlist receiver, whose public members are preserved for trimming.")]
        private object? LateBindExecute(Exception? ex, BindingFlags bindingFlags, object? objectInstance, object?[] args, bool isConstructor)
        {
            // First let's try for a method where all arguments are strings..
            Type[] types = new Type[_arguments.Length];
            for (int n = 0; n < _arguments.Length; n++)
            {
                types[n] = typeof(string);
            }

            MethodBase? memberInfo;
            if (isConstructor)
            {
                memberInfo = _receiverType.GetConstructor(types);
            }
            else
            {
                // Match a public method by name (case-insensitive) and exact parameter signature.
                // Equivalent to the prior GetMethod(..., BindingFlags, ...) call but uses the
                // public-only GetMethods(_bindingFlags) call, since BindingFlags.NonPublic is never set here.
                memberInfo = FindPublicMethodBySignature(_methodName, types);
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
                else if (_receiverType == typeof(IntrinsicFunctions) && IntrinsicFunctionOverload.IsKnownOverloadMethodName(_methodName))
                {
                    // FindMembers is invoked on the statically-known IntrinsicFunctions type (the
                    // only receiver that reaches this branch), so its broad reflection contract is
                    // satisfied by that concrete, rooted type rather than the receiver-type field.
                    MemberInfo[] foundMembers = typeof(IntrinsicFunctions).FindMembers(
                        MemberTypes.Method,
                        bindingFlags,
                        (info, criteria) => string.Equals(info.Name, (string?)criteria, StringComparison.OrdinalIgnoreCase),
                        _methodName.ValueOrEmpty);
                    Array.Sort(foundMembers, IntrinsicFunctionOverload.IntrinsicFunctionOverloadMethodComparer);
                    members = foundMembers.Cast<MethodBase>();
                }
                else
                {
                    StringSegment methodName = _methodName;
                    members = _receiverType.GetMethods(_bindingFlags).Where(m => methodName.Equals(m.Name, StringComparison.OrdinalIgnoreCase));
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
                        object?[]? coercedArguments = CoerceArguments(args, parameters);

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

            object? functionResult = null;

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
                Assumed.NotNull(ex);
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
