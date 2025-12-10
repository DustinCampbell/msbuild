// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class IntrinsicLibrary : FunctionLibrary
    {
        private static readonly Func<int, int, int> s_bitwiseOr = IntrinsicFunctions.BitwiseOr;
        private static readonly Func<int, int, int> s_bitwiseAnd = IntrinsicFunctions.BitwiseAnd;
        private static readonly Func<int, int, int> s_bitwiseXor = IntrinsicFunctions.BitwiseXor;
        private static readonly Func<int, int, int> s_leftShift = IntrinsicFunctions.LeftShift;
        private static readonly Func<int, int, int> s_rightShift = IntrinsicFunctions.RightShift;
        private static readonly Func<int, int, int> s_rightShiftUnsigned = IntrinsicFunctions.RightShiftUnsigned;

        public static readonly IntrinsicLibrary Instance = new();

        private IntrinsicLibrary()
        {
        }

        protected override void Initialize(ref Builder builder)
        {
            builder.Add(nameof(IntrinsicFunctions.EnsureTrailingSlash), IntrinsicFunction_EnsureTrailingSlash);
            builder.Add(nameof(IntrinsicFunctions.ValueOrDefault), IntrinsicFunction_ValueOrDefault);
            builder.Add(nameof(IntrinsicFunctions.NormalizePath), IntrinsicFunction_NormalizePath);
            builder.Add(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), IntrinsicFunction_GetDirectoryNameOfFileAbove);
            builder.Add(nameof(IntrinsicFunctions.GetRegistryValueFromView), IntrinsicFunction_GetRegistryValueFromView);
            builder.Add(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), IntrinsicFunction_IsRunningFromVisualStudio);
            builder.Add(nameof(IntrinsicFunctions.Escape), IntrinsicFunction_Escape);
            builder.Add(nameof(IntrinsicFunctions.Unescape), IntrinsicFunction_Unescape);
            builder.Add(nameof(IntrinsicFunctions.GetPathOfFileAbove), IntrinsicFunction_GetPathOfFileAbove);
            builder.Add(nameof(IntrinsicFunctions.Add), IntrinsicFunction_Add);
            builder.Add(nameof(IntrinsicFunctions.Subtract), IntrinsicFunction_Subtract);
            builder.Add(nameof(IntrinsicFunctions.Multiply), IntrinsicFunction_Multiply);
            builder.Add(nameof(IntrinsicFunctions.Divide), IntrinsicFunction_Divide);
            builder.Add(nameof(IntrinsicFunctions.Modulo), IntrinsicFunction_Modulo);
            builder.Add(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), IntrinsicFunction_GetCurrentToolsDirectory);
            builder.Add(nameof(IntrinsicFunctions.GetToolsDirectory32), IntrinsicFunction_GetToolsDirectory32);
            builder.Add(nameof(IntrinsicFunctions.GetToolsDirectory64), IntrinsicFunction_GetToolsDirectory64);
            builder.Add(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), IntrinsicFunction_GetMSBuildSDKsPath);
            builder.Add(nameof(IntrinsicFunctions.GetVsInstallRoot), IntrinsicFunction_GetVsInstallRoot);
            builder.Add(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), IntrinsicFunction_GetMSBuildExtensionsPath);
            builder.Add(nameof(IntrinsicFunctions.GetProgramFiles32), IntrinsicFunction_GetProgramFiles32);
            builder.Add(nameof(IntrinsicFunctions.VersionEquals), IntrinsicFunction_VersionEquals);
            builder.Add(nameof(IntrinsicFunctions.VersionNotEquals), IntrinsicFunction_VersionNotEquals);
            builder.Add(nameof(IntrinsicFunctions.VersionGreaterThan), IntrinsicFunction_VersionGreaterThan);
            builder.Add(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), IntrinsicFunction_VersionGreaterThanOrEquals);
            builder.Add(nameof(IntrinsicFunctions.VersionLessThan), IntrinsicFunction_VersionLessThan);
            builder.Add(nameof(IntrinsicFunctions.VersionLessThanOrEquals), IntrinsicFunction_VersionLessThanOrEquals);
            builder.Add(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), IntrinsicFunction_GetTargetFrameworkIdentifier);
            builder.Add(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), IntrinsicFunction_GetTargetFrameworkVersion);
            builder.Add(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), IntrinsicFunction_IsTargetFrameworkCompatible);
            builder.Add(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), IntrinsicFunction_GetTargetPlatformIdentifier);
            builder.Add(nameof(IntrinsicFunctions.GetTargetPlatformVersion), IntrinsicFunction_GetTargetPlatformVersion);
            builder.Add(nameof(IntrinsicFunctions.ConvertToBase64), IntrinsicFunction_ConvertToBase64);
            builder.Add(nameof(IntrinsicFunctions.ConvertFromBase64), IntrinsicFunction_ConvertFromBase64);
            builder.Add(nameof(IntrinsicFunctions.StableStringHash), IntrinsicFunction_StableStringHash);
            builder.Add(nameof(IntrinsicFunctions.AreFeaturesEnabled), IntrinsicFunction_AreFeaturesEnabled);
            builder.Add(nameof(IntrinsicFunctions.SubstringByAsciiChars), IntrinsicFunction_SubstringByAsciiChars);
            builder.Add(nameof(IntrinsicFunctions.CheckFeatureAvailability), IntrinsicFunction_CheckFeatureAvailability);
            builder.Add(nameof(IntrinsicFunctions.BitwiseOr), IntrinsicFunction_BitwiseOr);
            builder.Add(nameof(IntrinsicFunctions.BitwiseAnd), IntrinsicFunction_BitwiseAnd);
            builder.Add(nameof(IntrinsicFunctions.BitwiseXor), IntrinsicFunction_BitwiseXor);
            builder.Add(nameof(IntrinsicFunctions.BitwiseNot), IntrinsicFunction_BitwiseNot);
            builder.Add(nameof(IntrinsicFunctions.LeftShift), IntrinsicFunction_LeftShift);
            builder.Add(nameof(IntrinsicFunctions.RightShift), IntrinsicFunction_RightShift);
            builder.Add(nameof(IntrinsicFunctions.RightShiftUnsigned), IntrinsicFunction_RightShiftUnsigned);
            builder.Add(nameof(IntrinsicFunctions.NormalizeDirectory), IntrinsicFunction_NormalizeDirectory);
            builder.Add(nameof(IntrinsicFunctions.IsOSPlatform), IntrinsicFunction_IsOSPlatform);
            builder.Add(nameof(IntrinsicFunctions.FileExists), IntrinsicFunction_FileExists);
            builder.Add(nameof(IntrinsicFunctions.DirectoryExists), IntrinsicFunction_DirectoryExists);
            builder.Add(nameof(IntrinsicFunctions.RegisterBuildCheck), IntrinsicFunction_RegisterBuildCheck);
            builder.Add(nameof(IntrinsicFunctions.IsOsUnixLike), IntrinsicFunction_IsUnixLike);
            builder.Add(nameof(IntrinsicFunctions.DoesTaskHostExist), IntrinsicFunction_DoesTaskHostExist);
        }

        private static bool IntrinsicFunction_EnsureTrailingSlash(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string or null])
            {
                var path = (string?)args[0];
                result = IntrinsicFunctions.EnsureTrailingSlash(path);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_ValueOrDefault(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [var arg0, var arg1] &&
                arg0 is string or null &&
                arg1 is string or null)
            {
                result = IntrinsicFunctions.ValueOrDefault(conditionValue: (string?)arg0, defaultValue: (string?)arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_NormalizePath(ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case []:
                    result = null;
                    return false;

                case [string path]:
                    result = IntrinsicFunctions.NormalizePath(path);
                    return true;

                default:
                    if (TryConvertToStringArray(args, out string[]? paths))
                    {
                        result = IntrinsicFunctions.NormalizePath(paths);
                        return true;
                    }

                    result = null;
                    return false;
            }
        }

        private static bool IntrinsicFunction_GetDirectoryNameOfFileAbove(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string startingDirectory, string fileName, IFileSystem fileSystem])
            {
                result = IntrinsicFunctions.GetDirectoryNameOfFileAbove(startingDirectory, fileName, fileSystem);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetRegistryValueFromView(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string keyName, string valueName, var defaultValue, .. var views])
            {
                result = IntrinsicFunctions.GetRegistryValueFromView(keyName, valueName, defaultValue, views);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_IsRunningFromVisualStudio(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.IsRunningFromVisualStudio();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Escape(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [var arg0] && arg0 is string or null)
            {
                string? unescaped = (string?)arg0;
                result = IntrinsicFunctions.Escape(unescaped);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Unescape(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [var arg0] && arg0 is string or null)
            {
                string? escaped = (string?)arg0;
                result = IntrinsicFunctions.Unescape(escaped);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetPathOfFileAbove(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string file, string startingDirectory, IFileSystem fileSystem])
            {
                result = IntrinsicFunctions.GetPathOfFileAbove(file, startingDirectory, fileSystem);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Add(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out result);

        private static bool IntrinsicFunction_Subtract(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out result);

        private static bool IntrinsicFunction_Multiply(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out result);

        private static bool IntrinsicFunction_Divide(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out result);

        private static bool IntrinsicFunction_Modulo(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out result);

        private static bool IntrinsicFunction_GetCurrentToolsDirectory(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.GetCurrentToolsDirectory();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetToolsDirectory32(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.GetToolsDirectory32();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetToolsDirectory64(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.GetToolsDirectory64();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetMSBuildSDKsPath(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.GetMSBuildSDKsPath();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetVsInstallRoot(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.GetVsInstallRoot();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetMSBuildExtensionsPath(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.GetMSBuildExtensionsPath();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetProgramFiles32(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.GetProgramFiles32();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionEquals(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string a, string b])
            {
                result = IntrinsicFunctions.VersionEquals(a, b);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionNotEquals(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string a, string b])
            {
                result = IntrinsicFunctions.VersionNotEquals(a, b);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionGreaterThan(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string a, string b])
            {
                result = IntrinsicFunctions.VersionGreaterThan(a, b);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionGreaterThanOrEquals(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string a, string b])
            {
                result = IntrinsicFunctions.VersionGreaterThanOrEquals(a, b);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionLessThan(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string a, string b])
            {
                result = IntrinsicFunctions.VersionLessThan(a, b);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionLessThanOrEquals(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string a, string b])
            {
                result = IntrinsicFunctions.VersionLessThanOrEquals(a, b);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetTargetFrameworkIdentifier(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string tfm])
            {
                result = IntrinsicFunctions.GetTargetFrameworkIdentifier(tfm);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetTargetFrameworkVersion(ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [string tfm]:
                    result = IntrinsicFunctions.GetTargetFrameworkVersion(tfm);
                    return true;

                case [string tfm, var arg1] when TryConvertToInt(arg1, out int minVersionPartCount):
                    result = IntrinsicFunctions.GetTargetFrameworkVersion(tfm, minVersionPartCount);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool IntrinsicFunction_IsTargetFrameworkCompatible(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string target, string compatible])
            {
                result = IntrinsicFunctions.IsTargetFrameworkCompatible(target, compatible);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetTargetPlatformIdentifier(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string tfm])
            {
                result = IntrinsicFunctions.GetTargetPlatformIdentifier(tfm);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetTargetPlatformVersion(ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [string tfm]:
                    result = IntrinsicFunctions.GetTargetPlatformVersion(tfm);
                    return true;

                case [string tfm, var arg1] when TryConvertToInt(arg1, out int minVersionPartCount):
                    result = IntrinsicFunctions.GetTargetPlatformVersion(tfm, minVersionPartCount);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool IntrinsicFunction_ConvertToBase64(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string toEncode])
            {
                result = IntrinsicFunctions.ConvertToBase64(toEncode);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_ConvertFromBase64(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string toDecode])
            {
                result = IntrinsicFunctions.ConvertFromBase64(toDecode);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_StableStringHash(ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [string toHash]:
                    // Prevent loading methods refs from StringTools if ChangeWave opted out.
                    result = ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10)
                        ? IntrinsicFunctions.StableStringHash(toHash)
                        : IntrinsicFunctions.StableStringHashLegacy(toHash);

                    return true;

                case [string toHash, string arg1] when Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg1, ignoreCase: true, out var algo):
                    result = IntrinsicFunctions.StableStringHash(toHash, algo);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool IntrinsicFunction_AreFeaturesEnabled(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string input] && Version.TryParse(input, out var wave))
            {
                result = IntrinsicFunctions.AreFeaturesEnabled(wave);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_SubstringByAsciiChars(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string input, var arg1, var arg2] &&
                TryConvertToInt(arg1, out var start) &&
                TryConvertToInt(arg2, out var length))
            {
                result = IntrinsicFunctions.SubstringByAsciiChars(input, start, length);
                return true;
            }


            result = null;
            return false;
        }

        private static bool IntrinsicFunction_CheckFeatureAvailability(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string featureName])
            {
                result = IntrinsicFunctions.CheckFeatureAvailability(featureName);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_BitwiseOr(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunction(args, s_bitwiseOr, out result);

        private static bool IntrinsicFunction_BitwiseAnd(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunction(args, s_bitwiseAnd, out result);

        private static bool IntrinsicFunction_BitwiseXor(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunction(args, s_bitwiseXor, out result);

        private static bool IntrinsicFunction_BitwiseNot(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string arg0] && TryConvertToInt(arg0, out var first))
            {
                result = IntrinsicFunctions.BitwiseNot(first);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_LeftShift(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunction(args, s_leftShift, out result);

        private static bool IntrinsicFunction_RightShift(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunction(args, s_rightShift, out result);

        private static bool IntrinsicFunction_RightShiftUnsigned(ReadOnlySpan<object?> args, out object? result)
            => TryExecuteArithmeticFunction(args, s_rightShiftUnsigned, out result);

        private static bool IntrinsicFunction_NormalizeDirectory(ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case []:
                    result = IntrinsicFunctions.NormalizeDirectory([]);
                    return false;

                case [string path]:
                    result = IntrinsicFunctions.NormalizeDirectory(path);
                    return true;

                default:
                    if (TryConvertToStringArray(args, out string[]? paths))
                    {
                        result = IntrinsicFunctions.NormalizeDirectory(paths);
                        return true;
                    }

                    result = null;
                    return false;
            }
        }

        private static bool IntrinsicFunction_IsOSPlatform(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string platformString])
            {
                result = IntrinsicFunctions.IsOSPlatform(platformString);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_FileExists(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string path])
            {
                result = IntrinsicFunctions.FileExists(path);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_DirectoryExists(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string path])
            {
                result = IntrinsicFunctions.DirectoryExists(path);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_RegisterBuildCheck(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string projectPath, string pathToAssembly, LoggingContext loggingContext])
            {
                result = IntrinsicFunctions.RegisterBuildCheck(projectPath, pathToAssembly, loggingContext);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_IsUnixLike(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = IntrinsicFunctions.IsOsUnixLike();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_DoesTaskHostExist(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string runtime, string architecture])
            {
                result = IntrinsicFunctions.DoesTaskHostExist(runtime, architecture);
                return true;
            }

            result = null;
            return false;
        }
    }
}
