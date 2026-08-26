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
    /// <summary>
    /// This class represents the function as extracted from an expression
    /// It is also responsible for executing the function.
    /// </summary>
    private sealed class Function
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

        /// <summary>
        /// The expression that this function is part of.
        /// </summary>
        private readonly StringSegment _expression;
        private readonly int _expressionStartIndex;
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

        private readonly ExpansionContext _context;
        private readonly LoggingContext? _loggingContext;

        /// <summary>
        /// Construct a function that will be executed during property evaluation.
        /// </summary>
        internal Function(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicConstructors |
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.PublicProperties |
                DynamicallyAccessedMemberTypes.PublicFields)]
            Type receiverType,
            object? receiverValue,
            StringSegment expression,
            int expressionStartIndex,
            StringSegment methodName,
            FunctionArguments arguments,
            BindingFlags bindingFlags,
            ExpansionContext context,
            LoggingContext? loggingContext)
        {
            _methodName = methodName;
            _arguments = arguments;

            _receiverValue = receiverValue;
            _expression = expression;
            _expressionStartIndex = expressionStartIndex;
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

            _context = context;
            _loggingContext = loggingContext;
        }

        private bool IsFileSystemReceiver()
            => IsFileOrDirectoryReceiver()
            || _receiverType == typeof(System.IO.Path);

        private bool IsFileOrDirectoryReceiver()
            => _receiverType == typeof(System.IO.File)
            || _receiverType == typeof(System.IO.Directory);

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

        /// <summary>
        ///  Executes the function on its bound receiver.
        /// </summary>
        /// <param name="succeeded">
        ///  <see langword="true"/> when execution succeeds; otherwise, <see langword="false"/> when
        ///  the expansion options convert an execution failure to a partially evaluated result.
        /// </param>
        /// <returns>
        ///  The function result.
        /// </returns>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2074:UnrecognizedReflectionPattern",
            Justification = "_receiverType is reassigned from a runtime property value whose type is restricted to the property-function allowlist, whose members are preserved for trimming.")]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2080:UnrecognizedReflectionPattern",
            Justification = "_bindingFlags is masked to AllowedBindingFlags at construction, so it never carries BindingFlags.NonPublic; GetMethods(_bindingFlags) therefore binds only public methods of the property-function allowlist receiver, whose public members are preserved for trimming.")]
        internal object? Execute(out bool succeeded)
        {
            succeeded = true;
            object? functionResult = string.Empty;
            object?[]? args = null;
            object? objectInstance = _receiverValue;
            try
            {
                // The object that we're about to call methods on may have escaped characters
                // in it, we want to operate on the unescaped string in the function, just as we
                // want to pass arguments that are unescaped (see below)
                if (objectInstance is string objectInstanceString)
                {
                    objectInstance = EscapingUtilities.UnescapeAll(objectInstanceString);
                }

                bool wellKnownFunctionAttempted = false;
                if (!_methodName.Equals("new", StringComparison.OrdinalIgnoreCase)
                    && CanExecuteWellKnownWithoutExpandingArguments())
                {
                    wellKnownFunctionAttempted = true;
                    WellKnownExecutionStatus status = TryExecuteWellKnownFunction(
                        objectInstance,
                        _arguments,
                        out functionResult);

                    if (status == WellKnownExecutionStatus.ReturnImmediately)
                    {
                        succeeded = false;
                        return functionResult;
                    }

                    if (status == WellKnownExecutionStatus.Handled)
                    {
                        return CompleteExecution(functionResult);
                    }
                }

                // We have a methodinfo match, need to plug in the arguments
                args = new object?[_arguments.Length];

                // Assemble our arguments ready for passing to our method
                for (int n = 0; n < _arguments.Length; n++)
                {
                    object argument = PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(
                        _arguments.GetSource(n).Value,
                        _context);

                    if (argument is string argumentValue)
                    {
                        // Unescape the value since we're about to send it out of the engine and into
                        // the function being called. If a file or a directory function, fix the path
                        // AvailableStaticMembers always registers the System.IO types, including
                        // in FEATURE_MSIOREDIST builds.
                        if (IsFileSystemReceiver())
                        {
                            argumentValue = FileUtilities.FixFilePath(argumentValue);
                        }

                        args[n] = EscapingUtilities.UnescapeAll(argumentValue);

                        // In -mt mode, resolve relative path arguments for File/Directory methods
                        // against the thread-local working directory instead of the process-global
                        // Environment.CurrentDirectory which may point to a different project's directory.
                        // In multiprocess mode, CurrentThreadWorkingDirectory is null and
                        // MakeFullPathFromThreadWorkingDirectory returns null — this is a no-op.
                        // This must happen AFTER UnescapeAll so that the working directory path
                        // (a real filesystem path) is not corrupted by MSBuild unescape processing.
                        if (IsFileOrDirectoryReceiver()
                            && IsFileOrDirectoryPathArgument(_methodName, n))
                        {
                            AbsolutePath? resolved = FileUtilities.MakeFullPathFromThreadWorkingDirectory((string)args[n]!);
                            if (resolved.HasValue)
                            {
                                args[n] = (string)resolved.GetValueOrDefault();
                            }
                        }
                    }
                    else
                    {
                        args[n] = argument;
                    }
                }

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
                        if (double.TryParse(objectInstance.ToString(), NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double result))
                        {
                            objectInstance = result;
                            _receiverType = objectInstance.GetType();
                        }
                    }

                    // change the type of the final unescaped string into the destination
                    args[0] = Convert.ChangeType(args[0], objectInstance.GetType(), CultureInfo.InvariantCulture);
                }

                if (_receiverType == typeof(IntrinsicFunctions)
                    && _methodName.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase)
                    && args.Length == 1)
                {
                    string? startingDirectory = string.IsNullOrWhiteSpace(_context.Location.File)
                        ? string.Empty
                        : Path.GetDirectoryName(_context.Location.File);
                    args = [args[0], startingDirectory];
                }

                _arguments.SetMaterialized(args);

                // If we've been asked to construct an instance, then we
                // need to locate an appropriate constructor and invoke it
                if (_methodName.Equals("new", StringComparison.OrdinalIgnoreCase))
                {
                    if (!WellKnownFunctions.TryExecuteWellKnownConstructorNoThrow(_receiverType, _arguments, out functionResult))
                    {
                        functionResult = LateBindExecute(ex: null, BindingFlags.Public | BindingFlags.Instance, objectInstance: null, args, isConstructor: true);
                    }
                }
                else
                {
                    WellKnownExecutionStatus status = wellKnownFunctionAttempted
                        ? WellKnownExecutionStatus.NotHandled
                        : TryExecuteWellKnownFunction(
                            objectInstance,
                            _arguments,
                            out functionResult);

                    if (status == WellKnownExecutionStatus.ReturnImmediately)
                    {
                        succeeded = false;
                        return functionResult;
                    }

                    if (status == WellKnownExecutionStatus.NotHandled)
                    {
                        // Execute the function given converted arguments
                        // The only exception that we should catch to try a late bind here is missing method
                        // otherwise there is the potential of running a function twice!
                        try
                        {
                            // If there are any out parameters, try to figure out their type and create defaults
                            // for them as appropriate before calling the method.
                            using RefArrayBuilder<int> outArgIndices = new(stackalloc int[4]);
                            for (int i = 0; i < args.Length; i++)
                            {
                                if ("out _".Equals(args[i]))
                                {
                                    outArgIndices.Add(i);
                                }
                            }

                            if (!outArgIndices.IsEmpty)
                            {
                                MethodInfo[] methods = _receiverType.GetMethods(_bindingFlags);
                                using RefArrayBuilder<ParameterInfo[]> candidates = new(initialCapacity: methods.Length);

                                for (int i = 0; i < methods.Length; i++)
                                {
                                    MethodInfo method = methods[i];

                                    if (_methodName.Equals(method.Name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ParameterInfo[] parameters = method.GetParameters();

                                        if (parameters.Length == args.Length)
                                        {
                                            candidates.Add(parameters);
                                        }
                                    }
                                }

                                functionResult = GetMethodResult(objectInstance, args, candidates.AsSpan(), outArgIndices.AsSpan());
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

                return CompleteExecution(functionResult);
            }

            // Exceptions coming from the actual function called are wrapped in a TargetInvocationException
            catch (TargetInvocationException ex)
            {
                // We ended up with something other than a function expression
                string partiallyEvaluated = GenerateStringOfMethodExecuted(objectInstance, _methodName, args);

                if (_context.Options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    // If the caller wants to ignore errors (in a log statement for example), just return the partially evaluated value
                    succeeded = false;
                    return partiallyEvaluated;
                }

                _context.Errors.InvalidPropertyFunction.Throw(
                    partiallyEvaluated,
                    ex.InnerException?.Message.Replace("\r\n", " ") ?? string.Empty);
                return null;
            }

            // Any other exception was thrown by trying to call it
            catch (Exception ex) when (!ExceptionHandling.NotExpectedFunctionException(ex))
            {
                // If there's a :: in this operation, they were probably trying for a static function
                // invocation. Give them some more relevant info in that case.
                if (_expression.IndexOf("::", _expressionStartIndex, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string expression = _expression[_expressionStartIndex..].ValueOrEmpty;
                    _context.Errors.InvalidStaticPropertyFunction.Throw(
                        expression,
                        ex.Message.Replace("Microsoft.Build.Evaluation.IntrinsicFunctions.", "[MSBuild]::"));
                }
                else
                {
                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(objectInstance, _methodName, args);
                    _context.Errors.InvalidPropertyFunction.Throw(partiallyEvaluated, ex.Message);
                }

                return null;
            }

            bool CanExecuteWellKnownWithoutExpandingArguments()
                => !IsFileSystemReceiver()
                && !(_receiverType == typeof(IntrinsicFunctions)
                    && _arguments.Count == 1
                    && _methodName.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
                && !_methodName.Equals("Equals", StringComparison.OrdinalIgnoreCase)
                && !_methodName.Equals("CompareTo", StringComparison.OrdinalIgnoreCase)
                && !_arguments.ContainsExpandableExpression();

            // If the result of the function call is a string, then we need to escape the result
            // so that we maintain the "engine contains escaped data" state.
            // The exception is that the user is explicitly calling MSBuild::Unescape, MSBuild::Escape, or ConvertFromBase64
            object? CompleteExecution(object? functionResult)
                => functionResult is string s
                && !_methodName.Equals("Unescape", StringComparison.OrdinalIgnoreCase)
                && !_methodName.Equals("Escape", StringComparison.OrdinalIgnoreCase)
                && !_methodName.Equals("ConvertFromBase64", StringComparison.OrdinalIgnoreCase)
                    ? EscapingUtilities.Escape(s)
                    : functionResult;
        }

        private WellKnownExecutionStatus TryExecuteWellKnownFunction(
            object? objectInstance,
            FunctionArguments args,
            out object? functionResult)
        {
            try
            {
                if (WellKnownFunctions.TryExecuteWellKnownFunction(
                    _methodName,
                    _receiverType,
                    _context.FileSystem,
                    out functionResult,
                    objectInstance,
                    args)
                    || WellKnownFunctions.TryExecuteWellKnownFunctionWithPropertiesParam(
                        _methodName,
                        _receiverType,
                        _loggingContext,
                        _context.Properties,
                        out functionResult,
                        objectInstance,
                        args))
                {
                    return WellKnownExecutionStatus.Handled;
                }
            }
            catch (Exception ex)
            {
                string partiallyEvaluated = GenerateStringOfMethodExecuted(objectInstance, _methodName, args.ToObjectArray());

                if (_context.Options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    functionResult = partiallyEvaluated;
                    return WellKnownExecutionStatus.ReturnImmediately;
                }

                _context.Errors.InvalidPropertyFunction.Throw(
                    partiallyEvaluated,
                    ex.Message.Replace("\r\n", " "));
            }

            functionResult = null;
            return WellKnownExecutionStatus.NotHandled;
        }

        private object? GetMethodResult(
            object? objectInstance,
            object?[] args,
            ReadOnlySpan<ParameterInfo[]> candidates,
            ReadOnlySpan<int> outArgIndices)
        {
            string methodName = _methodName.ValueOrEmpty;
            object? result = null;
            bool hasResult = false;

            foreach (ParameterInfo[] parameters in candidates)
            {
                foreach (int index in outArgIndices)
                {
                    args[index] = parameters[index].ParameterType.CreateDefault();
                }

                object? currentResult;
                try
                {
                    currentResult = _receiverType.InvokePublicMember(methodName, _bindingFlags, objectInstance, args);
                }
                catch
                {
                    // This isn't a viable option, but perhaps another set of parameters will work.
                    continue;
                }

                if (!hasResult)
                {
                    result = currentResult;
                    hasResult = true;
                }
                else if (!Equals(result, currentResult))
                {
                    // There were multiple methods that seemed viable and gave different results.
                    // We can't differentiate between them so throw.
                    string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotDifferentiateBetweenCompatibleMethods", methodName, args.Length);
                    throw new ArgumentException(message);
                }
            }

            return result;
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
            object?[]? args)
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
                        string? startingDirectory = string.IsNullOrWhiteSpace(_context.Location.File)
                            ? string.Empty
                            : Path.GetDirectoryName(_context.Location.File);
                        AppendFunctionArgument(builder, startingDirectory);
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
                    members = _receiverType.GetMethods(_bindingFlags).Where(m => _methodName.Equals(m.Name, StringComparison.OrdinalIgnoreCase));
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
