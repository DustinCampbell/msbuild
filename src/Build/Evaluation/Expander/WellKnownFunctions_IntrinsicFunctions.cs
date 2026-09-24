// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class IntrinsicFunctionsHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args, ref readonly ExpanderContext context)
            => methodName.Length switch
            {
                3 when methodName.Equals(nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeAdd(args),
                6 when methodName.Equals(nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEscape(args),
                6 when methodName.Equals(nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDivide(args),
                6 when methodName.Equals(nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeModulo(args),
                8 when methodName.Equals(nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeUnescape(args),
                8 when methodName.Equals(nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSubtract(args),
                8 when methodName.Equals(nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMultiply(args),
                9 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseOr(args),
                9 when methodName.Equals(nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLeftShift(args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseAnd(args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseXor(args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseNot(args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRightShift(args),
                10 when methodName.Equals(nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeFileExists(args),
                12 when methodName.Equals(nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsOSPlatform(args),
                13 when methodName.Equals(nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNormalizePath(args),
                13 when methodName.Equals(nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionEquals(args),
                14 when methodName.Equals(nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeValueOrDefault(args),
                15 when methodName.Equals(nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeConvertToBase64(args),
                15 when methodName.Equals(nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionLessThan(args),
                15 when methodName.Equals(nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDirectoryExists(args),
                16 when methodName.Equals(nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetVsInstallRoot(args),
                16 when methodName.Equals(nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionNotEquals(args),
                16 when methodName.Equals(nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStableStringHash(args),
                17 when methodName.Equals(nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeConvertFromBase64(args),
                17 when methodName.Equals(nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetProgramFiles32(args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetPathOfFileAbove(args, in context),
                18 when methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetMSBuildSDKsPath(args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionGreaterThan(args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeAreFeaturesEnabled(args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRightShiftUnsigned(args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNormalizeDirectory(args),
                18 when methodName.Equals(nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRegisterBuildCheck(args, in context),
                19 when methodName.Equals(nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEnsureTrailingSlash(args),
                19 when methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetToolsDirectory32(args),
                19 when methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetToolsDirectory64(args),
                21 when methodName.Equals(nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSubstringByAsciiChars(args),
                23 when methodName.Equals(nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionLessThanOrEquals(args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetRegistryValueFromView(args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetCurrentToolsDirectory(args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetMSBuildExtensionsPath(args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetPlatformVersion(args),
                24 when methodName.Equals(nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCheckFeatureAvailability(args),
                25 when methodName.Equals(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsRunningFromVisualStudio(args),
                25 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetFrameworkVersion(args),
                26 when methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionGreaterThanOrEquals(args),
                27 when methodName.Equals(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetDirectoryNameOfFileAbove(args, in context),
                27 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetPlatformIdentifier(args),
                27 when methodName.Equals(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsTargetFrameworkCompatible(args),
                28 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetFrameworkIdentifier(args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeAdd(object?[] args)
        {
            if (!ArgumentParser.TryGetArithmeticArguments(args, out var arguments))
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

        private static WellKnownFunctionResult TryInvokeAreFeaturesEnabled(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out Version? arg0)
                ? Invoked(IntrinsicFunctions.AreFeaturesEnabled(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeBitwiseAnd(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.BitwiseAnd(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeBitwiseNot(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out int arg0)
                ? Invoked(IntrinsicFunctions.BitwiseNot(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeBitwiseOr(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.BitwiseOr(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeBitwiseXor(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.BitwiseXor(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeCheckFeatureAvailability(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.CheckFeatureAvailability(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeConvertFromBase64(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.ConvertFromBase64(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeConvertToBase64(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.ConvertToBase64(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeDirectoryExists(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.DirectoryExists(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeDivide(object?[] args)
        {
            if (!ArgumentParser.TryGetArithmeticArguments(args, out var arguments))
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

        private static WellKnownFunctionResult TryInvokeEnsureTrailingSlash(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.EnsureTrailingSlash(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeEscape(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.Escape(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeFileExists(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.FileExists(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetCurrentToolsDirectory(object?[] args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetCurrentToolsDirectory())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetDirectoryNameOfFileAbove(object?[] args, ref readonly ExpanderContext context)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, context.FileSystem))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetMSBuildExtensionsPath(object?[] args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetMSBuildExtensionsPath())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetMSBuildSDKsPath(object?[] args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetMSBuildSDKsPath())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetPathOfFileAbove(object?[] args, ref readonly ExpanderContext context)
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

        private static WellKnownFunctionResult TryInvokeGetProgramFiles32(object?[] args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetProgramFiles32())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetRegistryValueFromView(object?[] args)
            => args.Length >= 4
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], new ArraySegment<object?>(args, 3, args.Length - 3)))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetTargetFrameworkIdentifier(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetTargetFrameworkVersion(object?[] args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(IntrinsicFunctions.GetTargetFrameworkVersion(arg0)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out int arg1)
                    => Invoked(IntrinsicFunctions.GetTargetFrameworkVersion(arg0, arg1)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetTargetPlatformIdentifier(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.GetTargetPlatformIdentifier(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetTargetPlatformVersion(object?[] args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(IntrinsicFunctions.GetTargetPlatformVersion(arg0)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out int arg1)
                    => Invoked(IntrinsicFunctions.GetTargetPlatformVersion(arg0, arg1)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetToolsDirectory32(object?[] args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetToolsDirectory32())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetToolsDirectory64(object?[] args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetToolsDirectory64())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetVsInstallRoot(object?[] args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.GetVsInstallRoot())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsOSPlatform(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.IsOSPlatform(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsRunningFromVisualStudio(object?[] args)
            => args.Length == 0
                ? Invoked(IntrinsicFunctions.IsRunningFromVisualStudio())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsTargetFrameworkCompatible(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeLeftShift(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.LeftShift(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeModulo(object?[] args)
        {
            if (!ArgumentParser.TryGetArithmeticArguments(args, out var arguments))
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

        private static WellKnownFunctionResult TryInvokeMultiply(object?[] args)
        {
            if (!ArgumentParser.TryGetArithmeticArguments(args, out var arguments))
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

        private static WellKnownFunctionResult TryInvokeNormalizeDirectory(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.NormalizeDirectory(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeNormalizePath(object?[] args)
            => ArgumentParser.TryConvertToStrings(args, out string[]? stringArgs)
                ? Invoked(IntrinsicFunctions.NormalizePath(stringArgs))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeRegisterBuildCheck(object?[] args, ref readonly ExpanderContext context)
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

        private static WellKnownFunctionResult TryInvokeRightShift(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.RightShift(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeRightShiftUnsigned(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out int arg0)
            && args.TryGetArg(1, out int arg1)
                ? Invoked(IntrinsicFunctions.RightShiftUnsigned(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeStableStringHash(object?[] args)
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

        private static WellKnownFunctionResult TryInvokeSubstringByAsciiChars(object?[] args)
            => args.Length == 3
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out int arg1)
            && args.TryGetArg(2, out int arg2)
                ? Invoked(IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeSubtract(object?[] args)
        {
            if (!ArgumentParser.TryGetArithmeticArguments(args, out var arguments))
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

        private static WellKnownFunctionResult TryInvokeUnescape(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(IntrinsicFunctions.Unescape(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeValueOrDefault(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.ValueOrDefault(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionEquals(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionEquals(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionGreaterThan(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionGreaterThan(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionGreaterThanOrEquals(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionLessThan(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionLessThan(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionLessThanOrEquals(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionNotEquals(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(IntrinsicFunctions.VersionNotEquals(arg0, arg1))
                : NotHandled;
    }
}
