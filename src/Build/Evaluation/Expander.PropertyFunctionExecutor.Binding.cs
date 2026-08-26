// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
            BoundFunction function = Bind(invocation, receiverValue, context.Errors);
            return function.Execute(in context, context.PropertyLoggingContext, out result);
        }

        public static bool ExecuteStringFunction(
            string functionName,
            string[] arguments,
            string receiverValue,
            in ExpansionContext context,
            out object? result)
        {
            var function = new BoundFunction(
                receiverType: typeof(string),
                receiverValue,
                invocationText: default,
                receiverKind: ReceiverKind.Chained,
                functionName,
                new FunctionArguments(arguments),
                bindingFlags: BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);

            return function.Execute(in context, context.LoggingContext, out result);
        }

        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2067",
            Justification = "Receiver type comes from the static-member allowlist (public members preserved by AvailableStaticMembers.PropertyFunctionMembers) or a runtime GetType(); only public members are bound.")]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2072",
            Justification = "Runtime receiver types are restricted to the property-function receiver allowlist under trimming, whose public members are preserved.")]
        private static BoundFunction Bind(
            in PropertyFunctionInvocation invocation,
            object? receiverValue,
            ErrorReporter errors)
        {
            Type? receiverType = null;
            StringSegment memberName = invocation.MemberName;
            BindingFlags bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

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
                    if (!AvailableStaticMembers.TryResolveType(staticReceiver, memberName, out receiverType))
                    {
                        errors.UnavailablePropertyFunctionType.Throw(
                            invocation.Text.ValueOrEmpty,
                            staticReceiver.ValueOrEmpty);
                    }

                    Assumed.NotNull(receiverType);
                    if (!AvailableStaticMembers.IsAvailable(receiverType, memberName))
                    {
                        errors.UnavailablePropertyFunction.Throw(memberName.ValueOrEmpty, receiverType.FullName);
                    }

                    receiverValue = null;
                    bindingFlags |= BindingFlags.Static;
                    break;

                case ReceiverKind.MSBuildProperty:
                case ReceiverKind.Chained:
                    receiverType = receiverValue?.GetType() ?? typeof(string);
                    if (invocation.MemberKind == MemberKind.Indexer)
                    {
                        Assumed.NotNull(receiverValue);
                        memberName = receiverValue switch
                        {
                            Array => (StringSegment)"GetValue",
                            string => "get_Chars",
                            _ => "get_Item",
                        };
                    }

                    VerifyInstanceMemberAvailable(receiverType, memberName, errors);
                    bindingFlags |= BindingFlags.Instance;
                    break;

                default:
                    Assumed.Unreachable();
                    break;
            }

            Assumed.NotNull(receiverType);
            return new BoundFunction(
                receiverType,
                receiverValue,
                invocation.Text,
                invocation.ReceiverKind,
                memberName,
                new FunctionArguments(invocation.Arguments),
                bindingFlags);
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
