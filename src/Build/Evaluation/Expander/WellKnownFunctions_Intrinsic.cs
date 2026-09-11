// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class IntrinsicHandler
    {
        internal WellKnownFunctionResult TryInvokeStatic(
            string name,
            ref FunctionArguments arguments,
            ref readonly ExecutionContext context,
            out object? result)
        {
            switch (name.Length)
            {
                case 3 when name.Equals(nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeAdd(ref arguments, out result);

                case 6:
                    if (name.Equals(nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeDivide(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeEscape(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeModulo(ref arguments, out result);
                    }

                    break;

                case 8:
                    if (name.Equals(nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeMultiply(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeSubtract(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeUnescape(ref arguments, out result);
                    }

                    break;

                case 9:
                    if (name.Equals(nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeBitwiseOr(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeLeftShift(ref arguments, out result);
                    }

                    break;

                case 10:
                    if (name.Equals(nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeBitwiseAnd(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeBitwiseNot(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeBitwiseXor(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeFileExists(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeRightShift(ref arguments, out result);
                    }

                    break;

                case 12 when name.Equals(nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeIsOSPlatform(ref arguments, out result);

                case 13:
                    if (name.Equals(nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeNormalizePath(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeVersionEquals(ref arguments, out result);
                    }

                    break;

                case 14 when name.Equals(nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeValueOrDefault(ref arguments, out result);

                case 15:
                    if (name.Equals(nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeConvertToBase64(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeDirectoryExists(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeVersionLessThan(ref arguments, out result);
                    }

                    break;

                case 16:
                    if (name.Equals(nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetVsInstallRoot(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeStableStringHash(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeVersionNotEquals(ref arguments, out result);
                    }

                    break;

                case 17:
                    if (name.Equals(nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeConvertFromBase64(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetProgramFiles32(ref arguments, out result);
                    }

                    break;

                case 18:
                    if (name.Equals(nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeAreFeaturesEnabled(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetMSBuildSDKsPath(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetPathOfFileAbove(ref arguments, in context, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeNormalizeDirectory(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeRegisterBuildCheck(ref arguments, in context, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeRightShiftUnsigned(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeVersionGreaterThan(ref arguments, out result);
                    }

                    break;

                case 19:
                    if (name.Equals(nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeEnsureTrailingSlash(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetToolsDirectory32(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetToolsDirectory64(ref arguments, out result);
                    }

                    break;

                case 21 when name.Equals(nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeSubstringByAsciiChars(ref arguments, out result);

                case 23 when name.Equals(nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeVersionLessThanOrEquals(ref arguments, out result);

                case 24:
                    if (name.Equals(nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeCheckFeatureAvailability(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetCurrentToolsDirectory(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetMSBuildExtensionsPath(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetRegistryValueFromView(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetTargetPlatformVersion(ref arguments, out result);
                    }

                    break;

                case 25:
                    if (name.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetTargetFrameworkVersion(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeIsRunningFromVisualStudio(ref arguments, out result);
                    }

                    break;

                case 26 when name.Equals(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeVersionGreaterThanOrEquals(ref arguments, out result);

                case 27:
                    if (name.Equals(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetDirectoryNameOfFileAbove(ref arguments, in context, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetTargetPlatformIdentifier(ref arguments, out result);
                    }

                    if (name.Equals(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeIsTargetFrameworkCompatible(ref arguments, out result);
                    }

                    break;

                case 28 when name.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeGetTargetFrameworkIdentifier(ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeEnsureTrailingSlash(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? path))
            {
                result = IntrinsicFunctions.EnsureTrailingSlash(path);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeValueOrDefault(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out string? value, out string? defaultValue))
            {
                result = IntrinsicFunctions.ValueOrDefault(value, defaultValue);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeNormalizePath(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out string[]? paths))
            {
                result = IntrinsicFunctions.NormalizePath(paths);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetDirectoryNameOfFileAbove(
            ref FunctionArguments arguments,
            ref readonly ExecutionContext context,
            out object? result)
        {
            if (arguments.TryGetArgs(out string? startingDirectory, out string? fileName))
            {
                result = IntrinsicFunctions.GetDirectoryNameOfFileAbove(startingDirectory, fileName, context.FileSystem);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetRegistryValueFromView(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count >= 4)
            {
                object?[] values = arguments.MaterializeAll();
                string? keyName = values[0] as string;
                string? valueName = values[1] as string;
                object? defaultValue = values[2];
                ArraySegment<object?> views = new(values, offset: 3, count: values.Length - 3);

                if (keyName is not null && valueName is not null)
                {
                    result = IntrinsicFunctions.GetRegistryValueFromView(keyName, valueName, defaultValue, views);
                    return WellKnownFunctionResult.Handled;
                }
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeIsRunningFromVisualStudio(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 0)
            {
                result = IntrinsicFunctions.IsRunningFromVisualStudio();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeEscape(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = IntrinsicFunctions.Escape(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeUnescape(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = IntrinsicFunctions.Unescape(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetPathOfFileAbove(
            ref FunctionArguments arguments,
            ref readonly ExecutionContext context,
            out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out string? file):
                    result = IntrinsicFunctions.GetPathOfFileAbove(file, context.GetStartingDirectory(), context.FileSystem);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out string? file, out string? startingDirectory):
                    result = IntrinsicFunctions.GetPathOfFileAbove(file, startingDirectory, context.FileSystem);
                    return WellKnownFunctionResult.Handled;

                default:
                    return NotRecognized(out result);
            }
        }

        private static WellKnownFunctionResult TryInvokeAdd(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryExecuteArithmeticOverload(IntrinsicFunctions.Add, IntrinsicFunctions.Add, out result))
            {
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeSubtract(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryExecuteArithmeticOverload(
                    IntrinsicFunctions.Subtract,
                    IntrinsicFunctions.Subtract,
                    out result))
            {
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeMultiply(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryExecuteArithmeticOverload(
                    IntrinsicFunctions.Multiply,
                    IntrinsicFunctions.Multiply,
                    out result))
            {
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeDivide(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryExecuteArithmeticOverload(
                    IntrinsicFunctions.Divide,
                    IntrinsicFunctions.Divide,
                    out result))
            {
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeModulo(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryExecuteArithmeticOverload(IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out result))
            {
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetCurrentToolsDirectory(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = IntrinsicFunctions.GetCurrentToolsDirectory();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetToolsDirectory32(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = IntrinsicFunctions.GetToolsDirectory32();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetToolsDirectory64(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = IntrinsicFunctions.GetToolsDirectory64();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetMSBuildSDKsPath(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = IntrinsicFunctions.GetMSBuildSDKsPath();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetVsInstallRoot(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = IntrinsicFunctions.GetVsInstallRoot();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetMSBuildExtensionsPath(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = IntrinsicFunctions.GetMSBuildExtensionsPath();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetProgramFiles32(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = IntrinsicFunctions.GetProgramFiles32();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeVersionEquals(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out string? left, out string? right))
            {
                result = IntrinsicFunctions.VersionEquals(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeVersionNotEquals(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out string? left, out string? right))
            {
                result = IntrinsicFunctions.VersionNotEquals(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeVersionGreaterThan(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out string? left, out string? right))
            {
                result = IntrinsicFunctions.VersionGreaterThan(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeVersionGreaterThanOrEquals(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArgs(out string? left, out string? right))
            {
                result = IntrinsicFunctions.VersionGreaterThanOrEquals(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeVersionLessThan(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out string? left, out string? right))
            {
                result = IntrinsicFunctions.VersionLessThan(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeVersionLessThanOrEquals(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArgs(out string? left, out string? right))
            {
                result = IntrinsicFunctions.VersionLessThanOrEquals(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetTargetFrameworkIdentifier(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? targetFramework))
            {
                result = IntrinsicFunctions.GetTargetFrameworkIdentifier(targetFramework);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetTargetFrameworkVersion(
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out string? targetFramework):
                    result = IntrinsicFunctions.GetTargetFrameworkVersion(targetFramework);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out string? targetFramework, out int versionPartCount):
                    result = IntrinsicFunctions.GetTargetFrameworkVersion(targetFramework, versionPartCount);
                    return WellKnownFunctionResult.Handled;

                default:
                    return NotRecognized(out result);
            }
        }

        private static WellKnownFunctionResult TryInvokeIsTargetFrameworkCompatible(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArgs(out string? targetFramework, out string? candidateTargetFramework))
            {
                result = IntrinsicFunctions.IsTargetFrameworkCompatible(targetFramework, candidateTargetFramework);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetTargetPlatformIdentifier(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? targetFramework))
            {
                result = IntrinsicFunctions.GetTargetPlatformIdentifier(targetFramework);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetTargetPlatformVersion(
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out string? targetFramework):
                    result = IntrinsicFunctions.GetTargetPlatformVersion(targetFramework);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out string? targetFramework, out int versionPartCount):
                    result = IntrinsicFunctions.GetTargetPlatformVersion(targetFramework, versionPartCount);
                    return WellKnownFunctionResult.Handled;

                default:
                    return NotRecognized(out result);
            }
        }

        private static WellKnownFunctionResult TryInvokeConvertToBase64(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = IntrinsicFunctions.ConvertToBase64(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeConvertFromBase64(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = IntrinsicFunctions.ConvertFromBase64(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeStableStringHash(ref FunctionArguments arguments, out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out string? value):
                    result = IntrinsicFunctions.StableStringHash(value);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out string? value, out string? algorithmName) &&
                            Enum.TryParse(
                                algorithmName,
                                ignoreCase: true,
                                out IntrinsicFunctions.StringHashingAlgorithm algorithm):
                    result = IntrinsicFunctions.StableStringHash(value, algorithm);
                    return WellKnownFunctionResult.Handled;

                default:
                    return NotRecognized(out result);
            }
        }

        private static WellKnownFunctionResult TryInvokeAreFeaturesEnabled(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out Version? version))
            {
                result = IntrinsicFunctions.AreFeaturesEnabled(version);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeSubstringByAsciiChars(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out string? value, out int start, out int length))
            {
                result = IntrinsicFunctions.SubstringByAsciiChars(value, start, length);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeCheckFeatureAvailability(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? featureName))
            {
                result = IntrinsicFunctions.CheckFeatureAvailability(featureName);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeBitwiseOr(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out int left, out int right))
            {
                result = IntrinsicFunctions.BitwiseOr(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeBitwiseAnd(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out int left, out int right))
            {
                result = IntrinsicFunctions.BitwiseAnd(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeBitwiseXor(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out int left, out int right))
            {
                result = IntrinsicFunctions.BitwiseXor(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeBitwiseNot(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out int value))
            {
                result = IntrinsicFunctions.BitwiseNot(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeLeftShift(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out int value, out int count))
            {
                result = IntrinsicFunctions.LeftShift(value, count);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeRightShift(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out int value, out int count))
            {
                result = IntrinsicFunctions.RightShift(value, count);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeRightShiftUnsigned(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out int value, out int count))
            {
                result = IntrinsicFunctions.RightShiftUnsigned(value, count);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeNormalizeDirectory(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? directory))
            {
                result = IntrinsicFunctions.NormalizeDirectory(directory);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeIsOSPlatform(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? platform))
            {
                result = IntrinsicFunctions.IsOSPlatform(platform);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeFileExists(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? file))
            {
                result = IntrinsicFunctions.FileExists(file);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeDirectoryExists(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? directory))
            {
                result = IntrinsicFunctions.DirectoryExists(directory);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeRegisterBuildCheck(
            ref FunctionArguments arguments,
            ref readonly ExecutionContext context,
            out object? result)
        {
            string projectPath = context.Properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
            LoggingContext? loggingContext = context.LoggingContext;
            Assumed.NotNull(
                loggingContext,
                $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");

            if (arguments.TryGetArg(out string? checkName))
            {
                result = IntrinsicFunctions.RegisterBuildCheck(projectPath, checkName, loggingContext);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }
}
