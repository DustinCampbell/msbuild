// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class IntrinsicLibrary : FunctionLibrary
    {
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
        }

        private static bool IntrinsicFunction_EnsureTrailingSlash(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_ValueOrDefault(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_NormalizePath(ReadOnlySpan<object?> args, out object? result)
        {
            string[] paths = new string[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is not string stringArg)
                {
                    result = null;
                    return false;
                }

                paths[i] = stringArg;
            }

            result = IntrinsicFunctions.NormalizePath(paths);
            return true;
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
            if (args.Length >= 4 &&
                ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], args[3..]);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_IsRunningFromVisualStudio(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = IntrinsicFunctions.IsRunningFromVisualStudio();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Escape(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.Escape(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Unescape(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.Unescape(arg0);
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
        {
            if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out result))
            {
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Subtract(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out result))
            {
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Multiply(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out result))
            {
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Divide(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out result))
            {
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_Modulo(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out result))
            {
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetCurrentToolsDirectory(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = IntrinsicFunctions.GetCurrentToolsDirectory();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetToolsDirectory32(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = IntrinsicFunctions.GetToolsDirectory32();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetToolsDirectory64(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = IntrinsicFunctions.GetToolsDirectory64();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetMSBuildSDKsPath(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = IntrinsicFunctions.GetMSBuildSDKsPath();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetVsInstallRoot(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = IntrinsicFunctions.GetVsInstallRoot();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetMSBuildExtensionsPath(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = IntrinsicFunctions.GetMSBuildExtensionsPath();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetProgramFiles32(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = IntrinsicFunctions.GetProgramFiles32();
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionEquals(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.VersionEquals(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionNotEquals(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionGreaterThan(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionGreaterThanOrEquals(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionLessThan(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_VersionLessThanOrEquals(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetTargetFrameworkIdentifier(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetTargetFrameworkVersion(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.GetTargetFrameworkVersion(arg0);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out arg0, out int arg1))
            {
                result = IntrinsicFunctions.GetTargetFrameworkVersion(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_IsTargetFrameworkCompatible(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetTargetPlatformIdentifier(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_GetTargetPlatformVersion(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.GetTargetPlatformVersion(arg0);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out arg0, out int arg1))
            {
                result = IntrinsicFunctions.GetTargetPlatformVersion(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_ConvertToBase64(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.ConvertToBase64(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_ConvertFromBase64(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.ConvertFromBase64(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_StableStringHash(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                // Prevent loading methods refs from StringTools if ChangeWave opted out.
                result = ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10)
                    ? IntrinsicFunctions.StableStringHash(arg0)
                    : IntrinsicFunctions.StableStringHashLegacy(arg0);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out arg0, out string? arg1) &&
                Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg1, true, out var hashAlgorithm))
            {
                result = IntrinsicFunctions.StableStringHash(arg0, hashAlgorithm);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_AreFeaturesEnabled(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out Version? arg0))
            {
                result = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_SubstringByAsciiChars(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out int arg1, out int arg2))
            {
                result = IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_CheckFeatureAvailability(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.CheckFeatureAvailability(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_BitwiseOr(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
            {
                result = IntrinsicFunctions.BitwiseOr(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_BitwiseAnd(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
            {
                result = IntrinsicFunctions.BitwiseAnd(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_BitwiseXor(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
            {
                result = IntrinsicFunctions.BitwiseXor(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_BitwiseNot(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out int arg0))
            {
                result = IntrinsicFunctions.BitwiseNot(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_LeftShift(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
            {
                result = IntrinsicFunctions.LeftShift(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_RightShift(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
            {
                result = IntrinsicFunctions.RightShift(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_RightShiftUnsigned(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
            {
                result = IntrinsicFunctions.RightShiftUnsigned(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_NormalizeDirectory(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.NormalizeDirectory(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_IsOSPlatform(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.IsOSPlatform(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_FileExists(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.FileExists(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool IntrinsicFunction_DirectoryExists(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = IntrinsicFunctions.DirectoryExists(arg0);
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
    }
}
