// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class IntrinsicFunctionsHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, ref Arguments args, ref readonly ExpanderContext context)
            => methodName.Length switch
            {
                3 when methodName.Equals(nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeAdd(ref args),
                6 when methodName.Equals(nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEscape(ref args),
                6 when methodName.Equals(nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDivide(ref args),
                6 when methodName.Equals(nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeModulo(ref args),
                8 when methodName.Equals(nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeUnescape(ref args),
                8 when methodName.Equals(nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSubtract(ref args),
                8 when methodName.Equals(nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMultiply(ref args),
                9 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseOr(ref args),
                9 when methodName.Equals(nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLeftShift(ref args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseAnd(ref args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseXor(ref args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseNot(ref args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRightShift(ref args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeFileExists(ref args),
                12 when methodName.Equals(nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsOSPlatform(ref args),
                13 when methodName.Equals(nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNormalizePath(ref args),
                13 when methodName.Equals(nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionEquals(ref args),
                14 when methodName.Equals(nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeValueOrDefault(ref args),
                15 when methodName.Equals(nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeConvertToBase64(ref args),
                15 when methodName.Equals(nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionLessThan(ref args),
                15 when methodName.Equals(nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDirectoryExists(ref args),
                16 when methodName.Equals(nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetVsInstallRoot(ref args),
                16 when methodName.Equals(nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionNotEquals(ref args),
                16 when methodName.Equals(nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStableStringHash(ref args),
                17 when methodName.Equals(nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeConvertFromBase64(ref args),
                17 when methodName.Equals(nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetProgramFiles32(ref args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetPathOfFileAbove(ref args, in context),
                18 when methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetMSBuildSDKsPath(ref args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionGreaterThan(ref args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeAreFeaturesEnabled(ref args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRightShiftUnsigned(ref args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNormalizeDirectory(ref args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRegisterBuildCheck(ref args, in context),
                19 when methodName.Equals(nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEnsureTrailingSlash(ref args),
                19 when methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetToolsDirectory32(ref args),
                19 when methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetToolsDirectory64(ref args),
                21 when methodName.Equals(nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSubstringByAsciiChars(ref args),
                23 when methodName.Equals(nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionLessThanOrEquals(ref args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetRegistryValueFromView(ref args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetCurrentToolsDirectory(ref args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetMSBuildExtensionsPath(ref args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetPlatformVersion(ref args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCheckFeatureAvailability(ref args),
                25 when methodName.Equals(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsRunningFromVisualStudio(ref args),
                25 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetFrameworkVersion(ref args),
                26 when methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionGreaterThanOrEquals(ref args),
                27 when methodName.Equals(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetDirectoryNameOfFileAbove(ref args, in context),
                27 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetPlatformIdentifier(ref args),
                27 when methodName.Equals(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsTargetFrameworkCompatible(ref args),
                28 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetFrameworkIdentifier(ref args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeAdd(ref Arguments args)
        {
            if (!args.TryGetArithmeticArgs(out var arguments))
            {
                return NotHandled;
            }

            if (arguments.TryGetLongs(out long arg0, out long arg1))
            {
                return Invoked(IntrinsicFunctions.Add(arg0, arg1));
            }

            Assumed.True(arguments.TryGetDoubles(out double double0, out double double1));
            return Invoked(IntrinsicFunctions.Add(double0, double1));
        }

        private static WellKnownFunctionResult TryInvokeAreFeaturesEnabled(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out Version? arg0)
                ? Invoked(IntrinsicFunctions.AreFeaturesEnabled(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeBitwiseAnd(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.BitwiseAnd(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeBitwiseNot(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out int arg0)
                ? Invoked(IntrinsicFunctions.BitwiseNot(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeBitwiseOr(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.BitwiseOr(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeBitwiseXor(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.BitwiseXor(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeCheckFeatureAvailability(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.CheckFeatureAvailability(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeConvertFromBase64(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.ConvertFromBase64(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeConvertToBase64(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.ConvertToBase64(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeDirectoryExists(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.DirectoryExists(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeDivide(ref Arguments args)
        {
            if (!args.TryGetArithmeticArgs(out var arguments))
            {
                return NotHandled;
            }

            if (arguments.TryGetLongs(out long arg0, out long arg1))
            {
                return Invoked(IntrinsicFunctions.Divide(arg0, arg1));
            }

            Assumed.True(arguments.TryGetDoubles(out double double0, out double double1));
            return Invoked(IntrinsicFunctions.Divide(double0, double1));
        }

        private static WellKnownFunctionResult TryInvokeEnsureTrailingSlash(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.EnsureTrailingSlash(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeEscape(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.Escape(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeFileExists(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.FileExists(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetCurrentToolsDirectory(ref Arguments args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetCurrentToolsDirectory())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetDirectoryNameOfFileAbove(ref Arguments args, ref readonly ExpanderContext context)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, context.FileSystem))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetMSBuildExtensionsPath(ref Arguments args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetMSBuildExtensionsPath())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetMSBuildSDKsPath(ref Arguments args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetMSBuildSDKsPath())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetPathOfFileAbove(ref Arguments args, ref readonly ExpanderContext context)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(
                        IntrinsicFunctions.GetPathOfFileAbove(
                            arg0,
                            context.LocationDirectory,
                            context.FileSystem)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out string? arg1)
                    => Invoked(IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, context.FileSystem)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetProgramFiles32(ref Arguments args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetProgramFiles32())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetRegistryValueFromView(ref Arguments args)
            => args.Length >= 4
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
            && args.TryGetArg(2, out object? arg2)
            && args.TryGetArraySegment(3, out ArraySegment<object?> views)
                ? Invoked(IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, arg2, views))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetTargetFrameworkIdentifier(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetTargetFrameworkVersion(ref Arguments args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(IntrinsicFunctions.GetTargetFrameworkVersion(arg0)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out int arg1)
                    => Invoked(IntrinsicFunctions.GetTargetFrameworkVersion(arg0, arg1)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetTargetPlatformIdentifier(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.GetTargetPlatformIdentifier(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetTargetPlatformVersion(ref Arguments args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(IntrinsicFunctions.GetTargetPlatformVersion(arg0)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out int arg1)
                    => Invoked(IntrinsicFunctions.GetTargetPlatformVersion(arg0, arg1)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetToolsDirectory32(ref Arguments args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetToolsDirectory32())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetToolsDirectory64(ref Arguments args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetToolsDirectory64())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetVsInstallRoot(ref Arguments args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetVsInstallRoot())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsOSPlatform(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.IsOSPlatform(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsRunningFromVisualStudio(ref Arguments args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.IsRunningFromVisualStudio())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsTargetFrameworkCompatible(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeLeftShift(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.LeftShift(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeModulo(ref Arguments args)
        {
            if (!args.TryGetArithmeticArgs(out var arguments))
            {
                return NotHandled;
            }

            if (arguments.TryGetLongs(out long arg0, out long arg1))
            {
                return Invoked(IntrinsicFunctions.Modulo(arg0, arg1));
            }

            Assumed.True(arguments.TryGetDoubles(out double double0, out double double1));
            return Invoked(IntrinsicFunctions.Modulo(double0, double1));
        }

        private static WellKnownFunctionResult TryInvokeMultiply(ref Arguments args)
        {
            if (!args.TryGetArithmeticArgs(out var arguments))
            {
                return NotHandled;
            }

            if (arguments.TryGetLongs(out long arg0, out long arg1))
            {
                return Invoked(IntrinsicFunctions.Multiply(arg0, arg1));
            }

            Assumed.True(arguments.TryGetDoubles(out double double0, out double double1));
            return Invoked(IntrinsicFunctions.Multiply(double0, double1));
        }

        private static WellKnownFunctionResult TryInvokeNormalizeDirectory(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.NormalizeDirectory(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeNormalizePath(ref Arguments args)
            => args.TryConvertToStrings(out string[]? stringArgs)
                ? Invoked(IntrinsicFunctions.NormalizePath(stringArgs))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeRegisterBuildCheck(ref Arguments args, ref readonly ExpanderContext context)
        {
            IPropertyProvider<IProperty>? properties = context.Properties;
            Assumed.NotNull(properties, $"The property provider is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");

            string projectPath = properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
            LoggingContext? loggingContext = context.LoggingContext;
            Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");

            return args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext))
                : NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeRightShift(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.RightShift(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeRightShiftUnsigned(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.RightShiftUnsigned(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeStableStringHash(ref Arguments args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(IntrinsicFunctions.StableStringHash(arg0)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out string? arg1)
                    && Enum.TryParse(arg1, ignoreCase: true, out IntrinsicFunctions.StringHashingAlgorithm hashAlgorithm)
                    => Invoked(IntrinsicFunctions.StableStringHash(arg0, hashAlgorithm)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeSubstringByAsciiChars(ref Arguments args)
            => args.Length == 3
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out int arg1)
            && args.TryGetArg(2, out int arg2)
                ? Invoked(IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeSubtract(ref Arguments args)
        {
            if (!args.TryGetArithmeticArgs(out var arguments))
            {
                return NotHandled;
            }

            if (arguments.TryGetLongs(out long arg0, out long arg1))
            {
                return Invoked(IntrinsicFunctions.Subtract(arg0, arg1));
            }

            Assumed.True(arguments.TryGetDoubles(out double double0, out double double1));
            return Invoked(IntrinsicFunctions.Subtract(double0, double1));
        }

        private static WellKnownFunctionResult TryInvokeUnescape(ref Arguments args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.Unescape(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeValueOrDefault(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.ValueOrDefault(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionEquals(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionEquals(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionGreaterThan(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionGreaterThan(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionGreaterThanOrEquals(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionLessThan(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionLessThan(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionLessThanOrEquals(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionNotEquals(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionNotEquals(arg0, arg1))
                : NotHandled;
    }
}
