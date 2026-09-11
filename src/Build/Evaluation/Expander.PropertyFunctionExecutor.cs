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
using Microsoft.Build.BackEnd.Logging;
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
                    _arguments.ConfigureMaterialization(argumentMaterializer, materializeOnAccess: true);
                }

                bool isConstructor = _methodName.Equals("new", StringComparison.OrdinalIgnoreCase);
                if (isConstructor)
                {
                    if (WellKnownFunctions.TryInvokeConstructor(_receiverType, ref _arguments, out functionResult))
                    {
                        result = CompleteExecution(functionResult);
                        return true;
                    }
                }
                else
                {
                    WellKnownExecutionStatus wellKnownStatus = TryExecuteWellKnownFunction(
                        objectInstance,
                        ref _arguments,
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
                }

                if (argumentMaterializer is null && _arguments.Count > 0)
                {
                    argumentMaterializer = CreateArgumentMaterializer(in context);
                    _arguments.ConfigureMaterialization(argumentMaterializer, materializeOnAccess: false);
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
                if (isConstructor)
                {
                    functionResult = PropertyFunctionInvoker.InvokeConstructor(_receiverType, args);
                }
                else
                {
                    functionResult = PropertyFunctionInvoker.InvokeMember(
                        _receiverType, _methodName, _bindingFlags, objectInstance, args);
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
            ref FunctionArguments args,
            in ExpansionContext context,
            LoggingContext? loggingContext,
            out object? functionResult)
        {
            try
            {
                ExecutionContext executionContext = new(context.Properties, context.Location, context.FileSystem, loggingContext);
                bool handled = objectInstance is null
                    ? WellKnownFunctions.TryInvokeStatic(_receiverType, _methodName, ref args, in executionContext, out functionResult)
                    : WellKnownFunctions.TryInvokeInstance(objectInstance, _methodName, ref args, out functionResult);

                if (handled)
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
    }
}
