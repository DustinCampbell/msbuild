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
    private sealed class IntrinsicLibrary : BaseMemberLibrary, IStaticMethodLibrary
    {
        public static readonly IntrinsicLibrary Instance = new();

        private enum StaticId
        {
            EnsureTrailingSlash,
            ValueOrDefault,
            NormalizePath,
            GetDirectoryNameOfFileAbove,
            GetRegistryValueFromView,
            IsRunningFromVisualStudio,
            Escape,
            Unescape,
            GetPathOfFileAbove,
            Add,
            Subtract,
            Multiply,
            Divide,
            Modulo,
            GetCurrentToolsDirectory,
            GetToolsDirectory32,
            GetToolsDirectory64,
            GetMSBuildSDKsPath,
            GetVsInstallRoot,
            GetMSBuildExtensionsPath,
            GetProgramFiles32,
            VersionEquals,
            VersionNotEquals,
            VersionGreaterThan,
            VersionGreaterThanOrEquals,
            VersionLessThan,
            VersionLessThanOrEquals,
            GetTargetFrameworkIdentifier,
            GetTargetFrameworkVersion,
            IsTargetFrameworkCompatible,
            GetTargetPlatformIdentifier,
            GetTargetPlatformVersion,
            ConvertToBase64,
            ConvertFromBase64,
            StableStringHash,
            AreFeaturesEnabled,
            SubstringByAsciiChars,
            CheckFeatureAvailability,
            BitwiseOr,
            BitwiseAnd,
            BitwiseXor,
            BitwiseNot,
            LeftShift,
            RightShift,
            RightShiftUnsigned,
            NormalizeDirectory,
            IsOSPlatform,
            FileExists,
            DirectoryExists,
            RegisterBuildCheck,
            IsOsUnixLike,
            DoesTaskHostExist,
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;

        private static readonly Func<int, int, int> s_bitwiseOr = IntrinsicFunctions.BitwiseOr;
        private static readonly Func<int, int, int> s_bitwiseAnd = IntrinsicFunctions.BitwiseAnd;
        private static readonly Func<int, int, int> s_bitwiseXor = IntrinsicFunctions.BitwiseXor;
        private static readonly Func<int, int, int> s_leftShift = IntrinsicFunctions.LeftShift;
        private static readonly Func<int, int, int> s_rightShift = IntrinsicFunctions.RightShift;
        private static readonly Func<int, int, int> s_rightShiftUnsigned = IntrinsicFunctions.RightShiftUnsigned;

        private IntrinsicLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFind(name, out StaticId id)
                ? id switch
                {
                    StaticId.EnsureTrailingSlash => IntrinsicFunction_EnsureTrailingSlash(args),
                    StaticId.ValueOrDefault => IntrinsicFunction_ValueOrDefault(args),
                    StaticId.NormalizePath => IntrinsicFunction_NormalizePath(args),
                    StaticId.GetDirectoryNameOfFileAbove => IntrinsicFunction_GetDirectoryNameOfFileAbove(args),
                    StaticId.GetRegistryValueFromView => IntrinsicFunction_GetRegistryValueFromView(args),
                    StaticId.IsRunningFromVisualStudio => IntrinsicFunction_IsRunningFromVisualStudio(args),
                    StaticId.Escape => IntrinsicFunction_Escape(args),
                    StaticId.Unescape => IntrinsicFunction_Unescape(args),
                    StaticId.GetPathOfFileAbove => IntrinsicFunction_GetPathOfFileAbove(args),
                    StaticId.Add => IntrinsicFunction_Add(args),
                    StaticId.Subtract => IntrinsicFunction_Subtract(args),
                    StaticId.Multiply => IntrinsicFunction_Multiply(args),
                    StaticId.Divide => IntrinsicFunction_Divide(args),
                    StaticId.Modulo => IntrinsicFunction_Modulo(args),
                    StaticId.GetCurrentToolsDirectory => IntrinsicFunction_GetCurrentToolsDirectory(args),
                    StaticId.GetToolsDirectory32 => IntrinsicFunction_GetToolsDirectory32(args),
                    StaticId.GetToolsDirectory64 => IntrinsicFunction_GetToolsDirectory64(args),
                    StaticId.GetMSBuildSDKsPath => IntrinsicFunction_GetMSBuildSDKsPath(args),
                    StaticId.GetVsInstallRoot => IntrinsicFunction_GetVsInstallRoot(args),
                    StaticId.GetMSBuildExtensionsPath => IntrinsicFunction_GetMSBuildExtensionsPath(args),
                    StaticId.GetProgramFiles32 => IntrinsicFunction_GetProgramFiles32(args),
                    StaticId.VersionEquals => IntrinsicFunction_VersionEquals(args),
                    StaticId.VersionNotEquals => IntrinsicFunction_VersionNotEquals(args),
                    StaticId.VersionGreaterThan => IntrinsicFunction_VersionGreaterThan(args),
                    StaticId.VersionGreaterThanOrEquals => IntrinsicFunction_VersionGreaterThanOrEquals(args),
                    StaticId.VersionLessThan => IntrinsicFunction_VersionLessThan(args),
                    StaticId.VersionLessThanOrEquals => IntrinsicFunction_VersionLessThanOrEquals(args),
                    StaticId.GetTargetFrameworkIdentifier => IntrinsicFunction_GetTargetFrameworkIdentifier(args),
                    StaticId.GetTargetFrameworkVersion => IntrinsicFunction_GetTargetFrameworkVersion(args),
                    StaticId.IsTargetFrameworkCompatible => IntrinsicFunction_IsTargetFrameworkCompatible(args),
                    StaticId.GetTargetPlatformIdentifier => IntrinsicFunction_GetTargetPlatformIdentifier(args),
                    StaticId.GetTargetPlatformVersion => IntrinsicFunction_GetTargetPlatformVersion(args),
                    StaticId.ConvertToBase64 => IntrinsicFunction_ConvertToBase64(args),
                    StaticId.ConvertFromBase64 => IntrinsicFunction_ConvertFromBase64(args),
                    StaticId.StableStringHash => IntrinsicFunction_StableStringHash(args),
                    StaticId.AreFeaturesEnabled => IntrinsicFunction_AreFeaturesEnabled(args),
                    StaticId.SubstringByAsciiChars => IntrinsicFunction_SubstringByAsciiChars(args),
                    StaticId.CheckFeatureAvailability => IntrinsicFunction_CheckFeatureAvailability(args),
                    StaticId.BitwiseOr => IntrinsicFunction_BitwiseOr(args),
                    StaticId.BitwiseAnd => IntrinsicFunction_BitwiseAnd(args),
                    StaticId.BitwiseXor => IntrinsicFunction_BitwiseXor(args),
                    StaticId.BitwiseNot => IntrinsicFunction_BitwiseNot(args),
                    StaticId.LeftShift => IntrinsicFunction_LeftShift(args),
                    StaticId.RightShift => IntrinsicFunction_RightShift(args),
                    StaticId.RightShiftUnsigned => IntrinsicFunction_RightShiftUnsigned(args),
                    StaticId.NormalizeDirectory => IntrinsicFunction_NormalizeDirectory(args),
                    StaticId.IsOSPlatform => IntrinsicFunction_IsOSPlatform(args),
                    StaticId.FileExists => IntrinsicFunction_FileExists(args),
                    StaticId.DirectoryExists => IntrinsicFunction_DirectoryExists(args),
                    StaticId.RegisterBuildCheck => IntrinsicFunction_RegisterBuildCheck(args),
                    StaticId.IsOsUnixLike => IntrinsicFunction_IsOsUnixLike(args),
                    StaticId.DoesTaskHostExist => IntrinsicFunction_DoesTaskHostExist(args),
                    _ => Result.None,
                }
                : Result.None;

        private static Result IntrinsicFunction_EnsureTrailingSlash(ReadOnlySpan<object?> args)
            => args is [string or null]
                ? Result.From(IntrinsicFunctions.EnsureTrailingSlash((string?)args[0]))
                : Result.None;

        private static Result IntrinsicFunction_ValueOrDefault(ReadOnlySpan<object?> args)
            => args is [string or null, string or null]
                ? Result.From(IntrinsicFunctions.ValueOrDefault(conditionValue: (string?)args[0], defaultValue: (string?)args[1]))
                : Result.None;

        private static Result IntrinsicFunction_NormalizePath(ReadOnlySpan<object?> args)
            => args switch
            {
                [] => Result.None,
                [string path] => Result.From(IntrinsicFunctions.NormalizePath(path)),
                _ => TryConvertToStringArray(args, out string[]? paths)
                    ? Result.From(IntrinsicFunctions.NormalizePath(paths))
                    : Result.None,
            };

        private static Result IntrinsicFunction_GetDirectoryNameOfFileAbove(ReadOnlySpan<object?> args)
            => args is [string startingDirectory, string fileName, IFileSystem fileSystem]
                ? Result.From(IntrinsicFunctions.GetDirectoryNameOfFileAbove(startingDirectory, fileName, fileSystem))
                : Result.None;

        private static Result IntrinsicFunction_GetRegistryValueFromView(ReadOnlySpan<object?> args)
            => args is [string keyName, string valueName, var defaultValue, .. var views]
                ? Result.From(IntrinsicFunctions.GetRegistryValueFromView(keyName, valueName, defaultValue, views))
                : Result.None;

        private static Result IntrinsicFunction_IsRunningFromVisualStudio(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.IsRunningFromVisualStudio())
                : Result.None;

        private static Result IntrinsicFunction_Escape(ReadOnlySpan<object?> args)
            => args is [string or null]
                ? Result.From(IntrinsicFunctions.Escape((string?)args[0]))
                : Result.None;

        private static Result IntrinsicFunction_Unescape(ReadOnlySpan<object?> args)
            => args is [string or null]
                ? Result.From(IntrinsicFunctions.Unescape((string?)args[0]))
                : Result.None;

        private static Result IntrinsicFunction_GetPathOfFileAbove(ReadOnlySpan<object?> args)
            => args is [string file, string startingDirectory, IFileSystem fileSystem]
                ? Result.From(IntrinsicFunctions.GetPathOfFileAbove(file, startingDirectory, fileSystem))
                : Result.None;

        private static Result IntrinsicFunction_Add(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Add, IntrinsicFunctions.Add);

        private static Result IntrinsicFunction_Subtract(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract);

        private static Result IntrinsicFunction_Multiply(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply);

        private static Result IntrinsicFunction_Divide(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide);

        private static Result IntrinsicFunction_Modulo(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunctionWithOverflow(args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo);

        private static Result IntrinsicFunction_GetCurrentToolsDirectory(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.GetCurrentToolsDirectory())
                : Result.None;

        private static Result IntrinsicFunction_GetToolsDirectory32(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.GetToolsDirectory32())
                : Result.None;

        private static Result IntrinsicFunction_GetToolsDirectory64(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.GetToolsDirectory64())
                : Result.None;

        private static Result IntrinsicFunction_GetMSBuildSDKsPath(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.GetMSBuildSDKsPath())
                : Result.None;

        private static Result IntrinsicFunction_GetVsInstallRoot(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.GetVsInstallRoot())
                : Result.None;

        private static Result IntrinsicFunction_GetMSBuildExtensionsPath(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.GetMSBuildExtensionsPath())
                : Result.None;

        private static Result IntrinsicFunction_GetProgramFiles32(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.GetProgramFiles32())
                : Result.None;

        private static Result IntrinsicFunction_VersionEquals(ReadOnlySpan<object?> args)
            => args is [string a, string b]
            ? Result.From(IntrinsicFunctions.VersionEquals(a, b))
            : Result.None;

        private static Result IntrinsicFunction_VersionNotEquals(ReadOnlySpan<object?> args)
            => args is [string a, string b]
                ? Result.From(IntrinsicFunctions.VersionNotEquals(a, b))
                : Result.None;

        private static Result IntrinsicFunction_VersionGreaterThan(ReadOnlySpan<object?> args)
            => args is [string a, string b]
                ? Result.From(IntrinsicFunctions.VersionGreaterThan(a, b))
                : Result.None;

        private static Result IntrinsicFunction_VersionGreaterThanOrEquals(ReadOnlySpan<object?> args)
            => args is [string a, string b]
                ? Result.From(IntrinsicFunctions.VersionGreaterThanOrEquals(a, b))
                : Result.None;

        private static Result IntrinsicFunction_VersionLessThan(ReadOnlySpan<object?> args)
            => args is [string a, string b]
                ? Result.From(IntrinsicFunctions.VersionLessThan(a, b))
                : Result.None;

        private static Result IntrinsicFunction_VersionLessThanOrEquals(ReadOnlySpan<object?> args)
            => args is [string a, string b]
                ? Result.From(IntrinsicFunctions.VersionLessThanOrEquals(a, b))
                : Result.None;

        private static Result IntrinsicFunction_GetTargetFrameworkIdentifier(ReadOnlySpan<object?> args)
            => args is [string tfm]
                ? Result.From(IntrinsicFunctions.GetTargetFrameworkIdentifier(tfm))
                : Result.None;

        private static Result IntrinsicFunction_GetTargetFrameworkVersion(ReadOnlySpan<object?> args)
            => args switch
            {
                [string tfm] => Result.From(IntrinsicFunctions.GetTargetFrameworkVersion(tfm)),
                [string tfm, var arg1] when TryConvertToInt(arg1, out int minVersionPartCount)
                    => Result.From(IntrinsicFunctions.GetTargetFrameworkVersion(tfm, minVersionPartCount)),

                _ => Result.None,
            };

        private static Result IntrinsicFunction_IsTargetFrameworkCompatible(ReadOnlySpan<object?> args)
            => args is [string target, string compatible]
                ? Result.From(IntrinsicFunctions.IsTargetFrameworkCompatible(target, compatible))
                : Result.None;

        private static Result IntrinsicFunction_GetTargetPlatformIdentifier(ReadOnlySpan<object?> args)
            => args is [string tfm]
                ? Result.From(IntrinsicFunctions.GetTargetPlatformIdentifier(tfm))
                : Result.None;

        private static Result IntrinsicFunction_GetTargetPlatformVersion(ReadOnlySpan<object?> args)
            => args switch
            {
                [string tfm] => Result.From(IntrinsicFunctions.GetTargetPlatformVersion(tfm)),
                [string tfm, var arg1] when TryConvertToInt(arg1, out int minVersionPartCount)
                    => Result.From(IntrinsicFunctions.GetTargetPlatformVersion(tfm, minVersionPartCount)),

                _ => Result.None,
            };

        private static Result IntrinsicFunction_ConvertToBase64(ReadOnlySpan<object?> args)
            => args is [string toEncode]
                ? Result.From(IntrinsicFunctions.ConvertToBase64(toEncode))
                : Result.None;

        private static Result IntrinsicFunction_ConvertFromBase64(ReadOnlySpan<object?> args)
            => args is [string toDecode]
                ? Result.From(IntrinsicFunctions.ConvertFromBase64(toDecode))
                : Result.None;

        private static Result IntrinsicFunction_StableStringHash(ReadOnlySpan<object?> args)
            => args switch
            {
                [string toHash] => ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10)
                    ? Result.From(IntrinsicFunctions.StableStringHash(toHash))
                    : Result.From(IntrinsicFunctions.StableStringHashLegacy(toHash)),// Prevent loading methods refs from StringTools if ChangeWave opted out.
                [string toHash, string arg1] when Enum.TryParse(arg1, ignoreCase: true, out IntrinsicFunctions.StringHashingAlgorithm algo)
                    => Result.From(IntrinsicFunctions.StableStringHash(toHash, algo)),

                _ => Result.None,
            };

        private static Result IntrinsicFunction_AreFeaturesEnabled(ReadOnlySpan<object?> args)
            => args is [string input] && Version.TryParse(input, out Version? wave)
                ? Result.From(IntrinsicFunctions.AreFeaturesEnabled(wave))
                : Result.None;

        private static Result IntrinsicFunction_SubstringByAsciiChars(ReadOnlySpan<object?> args)
            => args is [string input, var arg1, var arg2] &&
                TryConvertToInt(arg1, out int start) &&
                TryConvertToInt(arg2, out int length)
                ? Result.From(IntrinsicFunctions.SubstringByAsciiChars(input, start, length))
                : Result.None;

        private static Result IntrinsicFunction_CheckFeatureAvailability(ReadOnlySpan<object?> args)
            => args is [string featureName]
                ? Result.From(IntrinsicFunctions.CheckFeatureAvailability(featureName))
                : Result.None;

        private static Result IntrinsicFunction_BitwiseOr(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunction(args, s_bitwiseOr);

        private static Result IntrinsicFunction_BitwiseAnd(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunction(args, s_bitwiseAnd);

        private static Result IntrinsicFunction_BitwiseXor(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunction(args, s_bitwiseXor);

        private static Result IntrinsicFunction_BitwiseNot(ReadOnlySpan<object?> args)
            => args is [string arg0] && TryConvertToInt(arg0, out int first)
                ? Result.From(IntrinsicFunctions.BitwiseNot(first))
                : Result.None;

        private static Result IntrinsicFunction_LeftShift(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunction(args, s_leftShift);

        private static Result IntrinsicFunction_RightShift(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunction(args, s_rightShift);

        private static Result IntrinsicFunction_RightShiftUnsigned(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunction(args, s_rightShiftUnsigned);

        private static Result IntrinsicFunction_NormalizeDirectory(ReadOnlySpan<object?> args)
            => args switch
            {
                [] => Result.From(IntrinsicFunctions.NormalizeDirectory([])),
                [string path] => Result.From(IntrinsicFunctions.NormalizeDirectory(path)),
                _ => TryConvertToStringArray(args, out string[]? paths)
                    ? Result.From(IntrinsicFunctions.NormalizeDirectory(paths))
                    : Result.None,
            };

        private static Result IntrinsicFunction_IsOSPlatform(ReadOnlySpan<object?> args)
            => args is [string platformString]
                ? Result.From(IntrinsicFunctions.IsOSPlatform(platformString))
                : Result.None;

        private static Result IntrinsicFunction_FileExists(ReadOnlySpan<object?> args)
            => args is [string path]
                ? Result.From(IntrinsicFunctions.FileExists(path))
                : Result.None;

        private static Result IntrinsicFunction_DirectoryExists(ReadOnlySpan<object?> args)
            => args is [string path]
                ? Result.From(IntrinsicFunctions.DirectoryExists(path))
                : Result.None;

        private static Result IntrinsicFunction_RegisterBuildCheck(ReadOnlySpan<object?> args)
            => args is [string projectPath, string pathToAssembly, LoggingContext loggingContext]
                ? Result.From(IntrinsicFunctions.RegisterBuildCheck(projectPath, pathToAssembly, loggingContext))
                : Result.None;

        private static Result IntrinsicFunction_IsOsUnixLike(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(IntrinsicFunctions.IsOsUnixLike())
                : Result.None;

        private static Result IntrinsicFunction_DoesTaskHostExist(ReadOnlySpan<object?> args)
            => args is [string runtime, string architecture]
                ? Result.From(IntrinsicFunctions.DoesTaskHostExist(runtime, architecture))
                : Result.None;
    }
}
