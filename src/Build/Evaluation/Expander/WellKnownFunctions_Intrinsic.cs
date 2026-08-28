// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal partial class WellKnownFunctions
{
    private enum IntrinsicFunction : byte
    {
        None,
        Add,
        AreFeaturesEnabled,
        BitwiseAnd,
        BitwiseNot,
        BitwiseOr,
        BitwiseXor,
        CheckFeatureAvailability,
        ConvertFromBase64,
        ConvertToBase64,
        DirectoryExists,
        Divide,
        DoesTaskHostExist,
        EnsureTrailingSlash,
        Escape,
        FileExists,
        FilterTargetFrameworks,
        GetCurrentToolsDirectory,
        GetDirectoryNameOfFileAbove,
        GetMSBuildExtensionsPath,
        GetMSBuildSDKsPath,
        GetPathOfFileAbove,
        GetProgramFiles32,
        GetRegistryValue,
        GetRegistryValueFromView,
        GetTargetFrameworkIdentifier,
        GetTargetFrameworkVersion,
        GetTargetPlatformIdentifier,
        GetTargetPlatformVersion,
        GetToolsDirectory32,
        GetToolsDirectory64,
        GetVsInstallRoot,
        IsOSPlatform,
        IsOsBsdLike,
        IsOsUnixLike,
        IsRunningFromVisualStudio,
        IsTargetFrameworkCompatible,
        LeftShift,
        MakeRelative,
        Modulo,
        Multiply,
        NormalizeDirectory,
        NormalizePath,
        RegisterBuildCheck,
        RightShift,
        RightShiftUnsigned,
        StableStringHash,
        SubstringByAsciiChars,
        Subtract,
        Unescape,
        ValueOrDefault,
        VersionEquals,
        VersionGreaterThan,
        VersionGreaterThanOrEquals,
        VersionLessThan,
        VersionLessThanOrEquals,
        VersionNotEquals,
    }

    internal static bool TryExecuteIntrinsicFunction<T>(
        StringSegment methodName,
        ref FunctionArguments args,
        in PropertyFunctionExecutionContext<T> context,
        out object? result)
        where T : class, IProperty
    {
        IntrinsicFunction function = GetIntrinsicFunction(methodName);
        switch (function)
        {
            case IntrinsicFunction.EnsureTrailingSlash:
                if (args.TryGetArg(out StringSegment trailingSlash))
                {
                    result = IntrinsicFunctions.EnsureTrailingSlash(trailingSlash);
                    return true;
                }

                break;

            case IntrinsicFunction.ValueOrDefault:
                if (args.TryGetArgs(out StringSegment conditionValue, out StringSegment defaultValue))
                {
                    result = IntrinsicFunctions.ValueOrDefault(conditionValue, defaultValue);
                    return true;
                }

                break;

            case IntrinsicFunction.NormalizePath:
                if (TryGetStringArguments(ref args, out string[]? paths))
                {
                    result = IntrinsicFunctions.NormalizePath(paths);
                    return true;
                }

                break;

            case IntrinsicFunction.NormalizeDirectory:
                if (args.TryGetArg(out StringSegment directory))
                {
                    result = IntrinsicFunctions.NormalizeDirectory(directory.ValueOrEmpty);
                    return true;
                }

                if (TryGetStringArguments(ref args, out paths))
                {
                    result = IntrinsicFunctions.NormalizeDirectory(paths);
                    return true;
                }

                break;

            case IntrinsicFunction.MakeRelative:
                if (args.TryGetArgs(out string? basePath, out string? path))
                {
                    result = IntrinsicFunctions.MakeRelative(basePath, path);
                    return true;
                }

                break;

            case IntrinsicFunction.GetDirectoryNameOfFileAbove:
                if (args.TryGetArgs(out string? startingDirectory, out string? fileName))
                {
                    result = IntrinsicFunctions.GetDirectoryNameOfFileAbove(startingDirectory, fileName, context.FileSystem);
                    return true;
                }

                break;

            case IntrinsicFunction.GetPathOfFileAbove:
                if (args.TryGetArg(out string? file))
                {
                    result = IntrinsicFunctions.GetPathOfFileAbove(file, context.StartingDirectory, context.FileSystem);
                    return true;
                }

                if (args.TryGetArgs(out file, out startingDirectory))
                {
                    result = IntrinsicFunctions.GetPathOfFileAbove(file, startingDirectory, context.FileSystem);
                    return true;
                }

                break;

            case IntrinsicFunction.GetRegistryValue:
                if (args.TryGetArgs(out string? keyName, out string? valueName))
                {
                    result = IntrinsicFunctions.GetRegistryValue(keyName, valueName);
                    return true;
                }

                if (args.Length == 3 &&
                    args.TryGetSegment(0, out StringSegment keyNameSegment) &&
                    args.TryGetSegment(1, out StringSegment valueNameSegment))
                {
                    result = IntrinsicFunctions.GetRegistryValue(
                        keyNameSegment.ValueOrEmpty,
                        valueNameSegment.ValueOrEmpty,
                        args.GetValue(2));
                    return true;
                }

                break;

            case IntrinsicFunction.GetRegistryValueFromView:
                if (args.Length >= 3 &&
                    args.TryGetSegment(0, out keyNameSegment) &&
                    args.TryGetSegment(1, out valueNameSegment))
                {
                    object?[] values = args.MaterializeAll();
                    result = IntrinsicFunctions.GetRegistryValueFromView(
                        keyNameSegment.ValueOrEmpty,
                        valueNameSegment.ValueOrEmpty,
                        values[2],
                        new ArraySegment<object?>(values, 3, values.Length - 3));
                    return true;
                }

                break;

            case IntrinsicFunction.IsRunningFromVisualStudio when args.Length == 0:
                result = IntrinsicFunctions.IsRunningFromVisualStudio();
                return true;

            case IntrinsicFunction.Escape:
                if (args.TryGetArg(out string? escaped))
                {
                    result = IntrinsicFunctions.Escape(escaped);
                    return true;
                }

                break;

            case IntrinsicFunction.Unescape:
                if (args.TryGetArg(out string? unescaped))
                {
                    result = IntrinsicFunctions.Unescape(unescaped);
                    return true;
                }

                break;

            case IntrinsicFunction.RegisterBuildCheck:
                if (args.TryGetArg(out string? assemblyPath) && assemblyPath is not null)
                {
                    string projectPath = context.Properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                    LoggingContext loggingContext = context.LoggingContext;
                    Assumed.NotNull(
                        loggingContext,
                        $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                    result = IntrinsicFunctions.RegisterBuildCheck(projectPath, assemblyPath, loggingContext);
                    return true;
                }

                break;

            case IntrinsicFunction.Add:
                return TryExecuteArithmetic(ref args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out result);

            case IntrinsicFunction.Subtract:
                return TryExecuteArithmetic(ref args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out result);

            case IntrinsicFunction.Multiply:
                return TryExecuteArithmetic(ref args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out result);

            case IntrinsicFunction.Divide:
                return TryExecuteArithmetic(ref args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out result);

            case IntrinsicFunction.Modulo:
                return TryExecuteArithmetic(ref args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out result);

            case IntrinsicFunction.GetCurrentToolsDirectory when args.Length == 0:
                result = IntrinsicFunctions.GetCurrentToolsDirectory();
                return true;

            case IntrinsicFunction.GetToolsDirectory32 when args.Length == 0:
                result = IntrinsicFunctions.GetToolsDirectory32();
                return true;

            case IntrinsicFunction.GetToolsDirectory64 when args.Length == 0:
                result = IntrinsicFunctions.GetToolsDirectory64();
                return true;

            case IntrinsicFunction.GetMSBuildSDKsPath when args.Length == 0:
                result = IntrinsicFunctions.GetMSBuildSDKsPath();
                return true;

            case IntrinsicFunction.GetVsInstallRoot when args.Length == 0:
                result = IntrinsicFunctions.GetVsInstallRoot();
                return true;

            case IntrinsicFunction.GetMSBuildExtensionsPath when args.Length == 0:
                result = IntrinsicFunctions.GetMSBuildExtensionsPath();
                return true;

            case IntrinsicFunction.GetProgramFiles32 when args.Length == 0:
                result = IntrinsicFunctions.GetProgramFiles32();
                return true;

            case IntrinsicFunction.VersionEquals:
            case IntrinsicFunction.VersionNotEquals:
            case IntrinsicFunction.VersionGreaterThan:
            case IntrinsicFunction.VersionGreaterThanOrEquals:
            case IntrinsicFunction.VersionLessThan:
            case IntrinsicFunction.VersionLessThanOrEquals:
                if (args.TryGetArgs(out StringSegment versionA, out StringSegment versionB))
                {
                    result = function switch
                    {
                        IntrinsicFunction.VersionEquals => IntrinsicFunctions.VersionEquals(versionA, versionB),
                        IntrinsicFunction.VersionNotEquals => IntrinsicFunctions.VersionNotEquals(versionA, versionB),
                        IntrinsicFunction.VersionGreaterThan => IntrinsicFunctions.VersionGreaterThan(versionA, versionB),
                        IntrinsicFunction.VersionGreaterThanOrEquals => IntrinsicFunctions.VersionGreaterThanOrEquals(versionA, versionB),
                        IntrinsicFunction.VersionLessThan => IntrinsicFunctions.VersionLessThan(versionA, versionB),
                        _ => IntrinsicFunctions.VersionLessThanOrEquals(versionA, versionB),
                    };
                    return true;
                }

                break;

            case IntrinsicFunction.GetTargetFrameworkIdentifier:
                if (args.TryGetArg(out string? targetFrameworkIdentifier))
                {
                    result = IntrinsicFunctions.GetTargetFrameworkIdentifier(targetFrameworkIdentifier);
                    return true;
                }

                break;

            case IntrinsicFunction.GetTargetFrameworkVersion:
                if (args.TryGetArg(out string? targetFrameworkVersion))
                {
                    result = IntrinsicFunctions.GetTargetFrameworkVersion(targetFrameworkVersion);
                    return true;
                }

                if (args.TryGetArgs(out targetFrameworkVersion, out int targetFrameworkVersionPartCount))
                {
                    result = IntrinsicFunctions.GetTargetFrameworkVersion(targetFrameworkVersion, targetFrameworkVersionPartCount);
                    return true;
                }

                break;

            case IntrinsicFunction.IsTargetFrameworkCompatible:
                if (args.TryGetArgs(out string? targetFramework, out string? candidateFramework))
                {
                    result = IntrinsicFunctions.IsTargetFrameworkCompatible(targetFramework, candidateFramework);
                    return true;
                }

                break;

            case IntrinsicFunction.GetTargetPlatformIdentifier:
                if (args.TryGetArg(out string? targetPlatformIdentifier))
                {
                    result = IntrinsicFunctions.GetTargetPlatformIdentifier(targetPlatformIdentifier);
                    return true;
                }

                break;

            case IntrinsicFunction.GetTargetPlatformVersion:
                if (args.TryGetArg(out string? targetPlatformVersion))
                {
                    result = IntrinsicFunctions.GetTargetPlatformVersion(targetPlatformVersion);
                    return true;
                }

                if (args.TryGetArgs(out targetPlatformVersion, out int targetPlatformVersionPartCount))
                {
                    result = IntrinsicFunctions.GetTargetPlatformVersion(targetPlatformVersion, targetPlatformVersionPartCount);
                    return true;
                }

                break;

            case IntrinsicFunction.FilterTargetFrameworks:
                if (args.TryGetArgs(out string? incomingFrameworks, out string? frameworkFilter))
                {
                    result = IntrinsicFunctions.FilterTargetFrameworks(incomingFrameworks, frameworkFilter);
                    return true;
                }

                break;

            case IntrinsicFunction.ConvertToBase64:
                if (args.TryGetArg(out string? toEncode))
                {
                    result = IntrinsicFunctions.ConvertToBase64(toEncode);
                    return true;
                }

                break;

            case IntrinsicFunction.ConvertFromBase64:
                if (args.TryGetArg(out string? toDecode))
                {
                    result = IntrinsicFunctions.ConvertFromBase64(toDecode);
                    return true;
                }

                break;

            case IntrinsicFunction.StableStringHash:
                if (args.TryGetArg(out string? valueToHash))
                {
                    result = IntrinsicFunctions.StableStringHash(valueToHash);
                    return true;
                }

                if (args.TryGetArgs(out valueToHash, out string? hashAlgorithmName) &&
                    Enum.TryParse(hashAlgorithmName, ignoreCase: true, out IntrinsicFunctions.StringHashingAlgorithm hashAlgorithm) &&
                    valueToHash is not null &&
                    hashAlgorithmName is not null)
                {
                    result = IntrinsicFunctions.StableStringHash(valueToHash, hashAlgorithm);
                    return true;
                }

                break;

            case IntrinsicFunction.AreFeaturesEnabled:
                if (args.TryGetArg(out Version? wave) && wave is not null)
                {
                    result = IntrinsicFunctions.AreFeaturesEnabled(wave);
                    return true;
                }

                break;

            case IntrinsicFunction.SubstringByAsciiChars:
                if (args.TryGetArgs(out string? asciiInput, out int asciiStart, out int asciiLength) &&
                    asciiInput is not null)
                {
                    result = IntrinsicFunctions.SubstringByAsciiChars(asciiInput, asciiStart, asciiLength);
                    return true;
                }

                break;

            case IntrinsicFunction.CheckFeatureAvailability:
                if (args.TryGetArg(out string? featureName) && featureName is not null)
                {
                    result = IntrinsicFunctions.CheckFeatureAvailability(featureName);
                    return true;
                }

                break;

            case IntrinsicFunction.BitwiseOr:
                return TryExecuteIntegerBinary(ref args, IntrinsicFunctions.BitwiseOr, out result);

            case IntrinsicFunction.BitwiseAnd:
                return TryExecuteIntegerBinary(ref args, IntrinsicFunctions.BitwiseAnd, out result);

            case IntrinsicFunction.BitwiseXor:
                return TryExecuteIntegerBinary(ref args, IntrinsicFunctions.BitwiseXor, out result);

            case IntrinsicFunction.BitwiseNot:
                if (args.TryGetArg(out int bitwiseOperand))
                {
                    result = IntrinsicFunctions.BitwiseNot(bitwiseOperand);
                    return true;
                }

                break;

            case IntrinsicFunction.LeftShift:
                return TryExecuteIntegerBinary(ref args, IntrinsicFunctions.LeftShift, out result);

            case IntrinsicFunction.RightShift:
                return TryExecuteIntegerBinary(ref args, IntrinsicFunctions.RightShift, out result);

            case IntrinsicFunction.RightShiftUnsigned:
                return TryExecuteIntegerBinary(ref args, IntrinsicFunctions.RightShiftUnsigned, out result);

            case IntrinsicFunction.IsOSPlatform:
                if (args.TryGetArg(out StringSegment platform))
                {
                    result = IntrinsicFunctions.IsOSPlatform(platform);
                    return true;
                }

                break;

            case IntrinsicFunction.IsOsUnixLike when args.Length == 0:
                result = IntrinsicFunctions.IsOsUnixLike();
                return true;

            case IntrinsicFunction.IsOsBsdLike when args.Length == 0:
                result = IntrinsicFunctions.IsOsBsdLike();
                return true;

            case IntrinsicFunction.FileExists:
                if (args.TryGetArg(out string? filePath))
                {
                    result = IntrinsicFunctions.FileExists(filePath);
                    return true;
                }

                break;

            case IntrinsicFunction.DirectoryExists:
                if (args.TryGetArg(out string? directoryPath))
                {
                    result = IntrinsicFunctions.DirectoryExists(directoryPath);
                    return true;
                }

                break;

            case IntrinsicFunction.DoesTaskHostExist:
                if (args.TryGetArgs(out string? runtime, out string? architecture))
                {
                    result = IntrinsicFunctions.DoesTaskHostExist(runtime, architecture);
                    return true;
                }

                break;
        }

        result = null;
        return false;
    }

    private static bool TryExecuteArithmetic(
        ref FunctionArguments args,
        Func<long, long, long> integerOperation,
        Func<double, double, double> realOperation,
        out object? result)
        => args.TryExecuteArithmeticOverload(integerOperation, realOperation, out result);

    private static bool TryExecuteIntegerBinary(
        ref FunctionArguments args,
        Func<int, int, int> operation,
        out object? result)
    {
        if (args.TryGetArgs(out int left, out int right))
        {
            result = operation(left, right);
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryGetStringArguments(
        ref FunctionArguments args,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string[]? values)
    {
        values = new string[args.Length];
        for (int i = 0; i < values.Length; i++)
        {
            if (!args.TryGetSegment(i, out StringSegment value))
            {
                values = null;
                return false;
            }

            values[i] = value.ValueOrEmpty;
        }

        return true;
    }

    internal static bool IsIntrinsicFunctionHandled(StringSegment methodName)
        => GetIntrinsicFunction(methodName) != IntrinsicFunction.None;

    private static IntrinsicFunction GetIntrinsicFunction(StringSegment name)
    {
        switch (name.Length)
        {
            case 3 when name.Equals(nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase):
                return IntrinsicFunction.Add;
            case 6:
                if (name.Equals(nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.Divide;
                }

                if (name.Equals(nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.Escape;
                }

                if (name.Equals(nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.Modulo;
                }

                break;
            case 8:
                if (name.Equals(nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.Multiply;
                }

                if (name.Equals(nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.Subtract;
                }

                if (name.Equals(nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.Unescape;
                }

                break;
            case 9:
                if (name.Equals(nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.BitwiseOr;
                }

                if (name.Equals(nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.LeftShift;
                }

                break;
            case 10:
                if (name.Equals(nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.BitwiseAnd;
                }

                if (name.Equals(nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.BitwiseNot;
                }

                if (name.Equals(nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.BitwiseXor;
                }

                if (name.Equals(nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.FileExists;
                }

                if (name.Equals(nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.RightShift;
                }

                break;
            case 11 when name.Equals(nameof(IntrinsicFunctions.IsOsBsdLike), StringComparison.OrdinalIgnoreCase):
                return IntrinsicFunction.IsOsBsdLike;
            case 12:
                if (name.Equals(nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.IsOSPlatform;
                }

                if (name.Equals(nameof(IntrinsicFunctions.IsOsUnixLike), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.IsOsUnixLike;
                }

                if (name.Equals(nameof(IntrinsicFunctions.MakeRelative), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.MakeRelative;
                }

                break;
            case 13:
                if (name.Equals(nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.NormalizePath;
                }

                if (name.Equals(nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.VersionEquals;
                }

                break;
            case 14 when name.Equals(nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase):
                return IntrinsicFunction.ValueOrDefault;
            case 15:
                if (name.Equals(nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.ConvertToBase64;
                }

                if (name.Equals(nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.DirectoryExists;
                }

                if (name.Equals(nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.VersionLessThan;
                }

                break;
            case 16:
                if (name.Equals(nameof(IntrinsicFunctions.GetRegistryValue), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetRegistryValue;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetVsInstallRoot;
                }

                if (name.Equals(nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.StableStringHash;
                }

                if (name.Equals(nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.VersionNotEquals;
                }

                break;
            case 17:
                if (name.Equals(nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.ConvertFromBase64;
                }

                if (name.Equals(nameof(IntrinsicFunctions.DoesTaskHostExist), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.DoesTaskHostExist;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetProgramFiles32;
                }

                break;
            case 18:
                if (name.Equals(nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.AreFeaturesEnabled;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetMSBuildSDKsPath;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetPathOfFileAbove;
                }

                if (name.Equals(nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.NormalizeDirectory;
                }

                if (name.Equals(nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.RegisterBuildCheck;
                }

                if (name.Equals(nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.RightShiftUnsigned;
                }

                if (name.Equals(nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.VersionGreaterThan;
                }

                break;
            case 19:
                if (name.Equals(nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.EnsureTrailingSlash;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetToolsDirectory32;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetToolsDirectory64;
                }

                break;
            case 21 when name.Equals(nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase):
                return IntrinsicFunction.SubstringByAsciiChars;
            case 22 when name.Equals(nameof(IntrinsicFunctions.FilterTargetFrameworks), StringComparison.OrdinalIgnoreCase):
                return IntrinsicFunction.FilterTargetFrameworks;
            case 23 when name.Equals(nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase):
                return IntrinsicFunction.VersionLessThanOrEquals;
            case 24:
                if (name.Equals(nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.CheckFeatureAvailability;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetCurrentToolsDirectory;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetMSBuildExtensionsPath;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetRegistryValueFromView;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetTargetPlatformVersion;
                }

                break;
            case 25:
                if (name.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetTargetFrameworkVersion;
                }

                if (name.Equals(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.IsRunningFromVisualStudio;
                }

                break;
            case 26 when name.Equals(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase):
                return IntrinsicFunction.VersionGreaterThanOrEquals;
            case 27:
                if (name.Equals(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetDirectoryNameOfFileAbove;
                }

                if (name.Equals(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.GetTargetPlatformIdentifier;
                }

                if (name.Equals(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
                {
                    return IntrinsicFunction.IsTargetFrameworkCompatible;
                }

                break;
            case 28 when name.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase):
                return IntrinsicFunction.GetTargetFrameworkIdentifier;
        }

        return IntrinsicFunction.None;
    }
}
