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
    /// <summary>
    ///  Binds parsed property-function syntax to a runtime receiver and executable member.
    /// </summary>
    private static class FunctionBinder
    {
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2067",
            Justification = "Receiver type comes from the static-member allowlist (public members preserved by AvailableStaticMembers.PropertyFunctionMembers) or a runtime GetType(); only public members are bound.")]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2072",
            Justification = "Runtime receiver types are restricted to the property-function receiver allowlist under trimming, whose public members are preserved.")]
        public static Function Bind(
            PropertyFunctionInvocation invocation,
            StringSegment expression,
            object? receiverValue,
            ExpansionContext context)
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
                        context.Errors.UnavailablePropertyFunctionType.Throw(
                            expression.ValueOrEmpty,
                            staticReceiver.ValueOrEmpty);
                    }

                    Assumed.NotNull(receiverType);
                    if (!AvailableStaticMembers.IsAvailable(receiverType, memberName))
                    {
                        context.Errors.UnavailablePropertyFunction.Throw(memberName.ValueOrEmpty, receiverType.FullName);
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

                    VerifyInstanceMemberAvailable(receiverType, memberName, context);
                    bindingFlags |= BindingFlags.Instance;
                    break;

                default:
                    Assumed.Unreachable();
                    break;
            }

            Assumed.NotNull(receiverType);
            return new Function(
                receiverType,
                receiverValue,
                expression,
                invocation.Text.Offset - expression.Offset,
                memberName.ValueOrEmpty,
                new FunctionArgumentList(invocation.Arguments),
                bindingFlags,
                context,
                context.PropertyLoggingContext);
        }

        private static void VerifyInstanceMemberAvailable(
            Type receiverType,
            StringSegment memberName,
            ExpansionContext context)
        {
            if (FeatureSwitches.EnableAllPropertyFunctions)
            {
                return;
            }

            if (memberName.Equals("GetType", StringComparison.OrdinalIgnoreCase))
            {
                context.Errors.UnavailablePropertyFunction.Throw(memberName.ValueOrEmpty, receiverType.FullName);
            }

            if (FeatureSwitches.RestrictPropertyFunctionReceivers
                && !PropertyFunctionReceiver.IsAllowed(receiverType, memberName.ValueOrEmpty))
            {
                context.Errors.UnavailablePropertyFunction.Throw(memberName.ValueOrEmpty, receiverType.FullName);
            }
        }
    }
}
