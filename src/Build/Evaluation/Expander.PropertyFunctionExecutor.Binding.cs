// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Text;
using FeatureSwitches = Microsoft.Build.Framework.FeatureSwitches;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private static partial class PropertyFunctionExecutor
    {
        public static bool Execute(
            in PropertyFunctionInvocation invocation,
            object? receiverValue,
            in ExpansionContext context,
            out object? result)
        {
            Bind(
                invocation,
                receiverValue,
                context.Errors,
                out Type receiverType,
                out object? boundReceiverValue,
                out StringSegment memberName,
                out BindingFlags bindingFlags);

            FunctionArguments arguments = new(invocation.Arguments);
            WellKnownExecutionStatus status = BoundFunction.TryExecuteWellKnownFunction(
                receiverType,
                boundReceiverValue,
                memberName,
                bindingFlags,
                ref arguments,
                in context,
                context.PropertyLoggingContext,
                out result);

            if (status != WellKnownExecutionStatus.NotHandled)
            {
                return status == WellKnownExecutionStatus.Handled;
            }

            var function = new BoundFunction(
                receiverType,
                boundReceiverValue,
                invocation.Text,
                invocation.ReceiverKind,
                memberName,
                arguments,
                bindingFlags);

            return function.Execute(in context, out result);
        }

        public static bool ExecuteStringFunction(
            string functionName,
            string[] arguments,
            string receiverValue,
            in ExpansionContext context,
            out object? result)
        {
            FunctionArguments argumentsState = new(arguments);
            WellKnownExecutionStatus status = BoundFunction.TryExecuteWellKnownFunction(
                typeof(string),
                receiverValue,
                functionName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod,
                ref argumentsState,
                in context,
                context.LoggingContext,
                out result);

            if (status != WellKnownExecutionStatus.NotHandled)
            {
                return status == WellKnownExecutionStatus.Handled;
            }

            var function = new BoundFunction(
                receiverType: typeof(string),
                receiverValue,
                invocationText: default,
                receiverKind: ReceiverKind.Chained,
                functionName,
                argumentsState,
                bindingFlags: BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);

            return function.Execute(in context, out result);
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2067",
            Justification = "Receiver type comes from the static-member allowlist (public members preserved by AvailableStaticMembers.PropertyFunctionMembers) or a runtime GetType(); only public members are bound.")]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2072",
            Justification = "Runtime receiver types are restricted to the property-function receiver allowlist under trimming, whose public members are preserved.")]
        private static void Bind(
            in PropertyFunctionInvocation invocation,
            object? receiverValue,
            ErrorReporter errors,
            out Type receiverType,
            out object? boundReceiverValue,
            out StringSegment memberName,
            out BindingFlags bindingFlags)
        {
            ReceiverKind receiverKind = invocation.ReceiverKind;
            MemberKind memberKind = invocation.MemberKind;

            if (receiverKind is ReceiverKind.MSBuildProperty or ReceiverKind.Chained
                && memberKind != MemberKind.Indexer)
            {
                receiverType = receiverValue?.GetType() ?? typeof(string);
                boundReceiverValue = receiverValue;
                memberName = invocation.MemberName;
                bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;
                bindingFlags |= memberKind == MemberKind.Method
                    ? BindingFlags.InvokeMethod
                    : BindingFlags.GetProperty | BindingFlags.GetField;
                VerifyInstanceMemberAvailable(receiverType, memberName, errors);
                return;
            }

            BindUncommon(
                invocation,
                receiverValue,
                errors,
                out receiverType,
                out boundReceiverValue,
                out memberName,
                out bindingFlags);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BindUncommon(
            in PropertyFunctionInvocation invocation,
            object? receiverValue,
            ErrorReporter errors,
            out Type receiverType,
            out object? boundReceiverValue,
            out StringSegment memberName,
            out BindingFlags bindingFlags)
        {
            Type? resolvedReceiverType = null;
            boundReceiverValue = receiverValue;
            memberName = invocation.MemberName;
            bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

            bindingFlags |= invocation.MemberKind switch
            {
                MemberKind.Method or MemberKind.Indexer => BindingFlags.InvokeMethod,
                MemberKind.PropertyOrField => BindingFlags.GetProperty | BindingFlags.GetField,
                _ => Assumed.Unreachable<BindingFlags>(),
            };

            switch (invocation.ReceiverKind)
            {
                case ReceiverKind.Static:
                    StringSegment staticReceiver = invocation.Receiver;
                    if (!AvailableStaticMembers.TryResolveType(staticReceiver, memberName, out resolvedReceiverType))
                    {
                        errors.UnavailablePropertyFunctionType.Throw(
                            invocation.Text.ValueOrEmpty,
                            staticReceiver.ValueOrEmpty);
                    }

                    Assumed.NotNull(resolvedReceiverType);
                    if (!AvailableStaticMembers.IsAvailable(resolvedReceiverType, memberName))
                    {
                        errors.UnavailablePropertyFunction.Throw(memberName.ValueOrEmpty, resolvedReceiverType.FullName);
                    }

                    boundReceiverValue = null;
                    bindingFlags |= BindingFlags.Static;
                    break;

                case ReceiverKind.MSBuildProperty:
                case ReceiverKind.Chained:
                    resolvedReceiverType = receiverValue?.GetType() ?? typeof(string);
                    if (invocation.MemberKind == MemberKind.Indexer)
                    {
                        Assumed.NotNull(boundReceiverValue);
                        memberName = boundReceiverValue switch
                        {
                            Array => (StringSegment)"GetValue",
                            string => "get_Chars",
                            _ => "get_Item",
                        };
                    }

                    VerifyInstanceMemberAvailable(resolvedReceiverType, memberName, errors);
                    bindingFlags |= BindingFlags.Instance;
                    break;

                default:
                    Assumed.Unreachable();
                    break;
            }

            Assumed.NotNull(resolvedReceiverType);
            receiverType = resolvedReceiverType;
        }

        private static void VerifyInstanceMemberAvailable(
            Type receiverType,
            StringSegment memberName,
            ErrorReporter errors)
        {
            if (FeatureSwitches.EnableAllPropertyFunctions)
            {
                return;
            }

            if (memberName.Equals("GetType", StringComparison.OrdinalIgnoreCase))
            {
                errors.UnavailablePropertyFunction.Throw(memberName.ValueOrEmpty, receiverType.FullName);
            }

            if (FeatureSwitches.RestrictPropertyFunctionReceivers
                && !PropertyFunctionReceiver.IsAllowed(receiverType, memberName.ValueOrEmpty))
            {
                errors.UnavailablePropertyFunction.Throw(memberName.ValueOrEmpty, receiverType.FullName);
            }
        }
    }
}
