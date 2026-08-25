// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Shared.FileSystem;
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
            FunctionParser.ParsedFunction parsedFunction,
            object? receiverValue,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem,
            LoggingContext loggingContext)
        {
            FunctionParser.ParsedMember member = parsedFunction.Member;
            Type? receiverType = null;
            string memberName = member.Name;
            BindingFlags bindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;
            var errors = new FunctionParser.ErrorReporter(parsedFunction.Text, parsedFunction.Location);

            bindingFlags |= member.Kind switch
            {
                FunctionParser.MemberKind.Method or FunctionParser.MemberKind.Indexer => BindingFlags.InvokeMethod,
                FunctionParser.MemberKind.PropertyOrField => BindingFlags.GetProperty | BindingFlags.GetField,
                _ => Assumed.Unreachable<BindingFlags>(),
            };

            switch (parsedFunction.ReceiverKind)
            {
                case FunctionParser.ReceiverKind.Static:
                    Assumed.NotNull(parsedFunction.Receiver);
                    if (!AvailableStaticMembers.TryResolveType(parsedFunction.Receiver, memberName, out receiverType))
                    {
                        errors.ThrowInvalidFunctionTypeUnavailable(parsedFunction.Receiver);
                    }

                    Assumed.NotNull(receiverType);
                    if (!AvailableStaticMembers.IsAvailable(receiverType, memberName))
                    {
                        errors.ThrowInvalidFunctionMethodUnavailable(memberName, receiverType.FullName);
                    }

                    receiverValue = null;
                    bindingFlags |= BindingFlags.Static;
                    break;

                case FunctionParser.ReceiverKind.MSBuildProperty:
                case FunctionParser.ReceiverKind.Chained:
                    receiverType = receiverValue?.GetType() ?? typeof(string);
                    if (member.Kind == FunctionParser.MemberKind.Indexer)
                    {
                        Assumed.NotNull(receiverValue);
                        memberName = receiverValue switch
                        {
                            Array => "GetValue",
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
            return new Function(
                receiverType,
                receiverValue,
                parsedFunction.Text,
                parsedFunction.StartIndex,
                memberName,
                member.Arguments,
                bindingFlags,
                propertiesUseTracker,
                fileSystem,
                loggingContext,
                parsedFunction.Location);
        }

        private static void VerifyInstanceMemberAvailable(
            Type receiverType,
            string memberName,
            FunctionParser.ErrorReporter errors)
        {
            if (FeatureSwitches.EnableAllPropertyFunctions)
            {
                return;
            }

            if (string.Equals("GetType", memberName, StringComparison.OrdinalIgnoreCase))
            {
                errors.ThrowInvalidFunctionMethodUnavailable(memberName, receiverType.FullName);
            }

            if (FeatureSwitches.RestrictPropertyFunctionReceivers
                && !PropertyFunctionReceiver.IsAllowed(receiverType, memberName))
            {
                errors.ThrowInvalidFunctionMethodUnavailable(memberName, receiverType.FullName);
            }
        }
    }
}
