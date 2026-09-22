// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation.Expander;

internal static class WellKnownFunctions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogFunctionCall(Type receiverType, string methodName, string fileName, object? objectInstance, object?[] args)
    {
        string logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);

        string argSignature = args is not null
            ? string.Join(", ", args.Select(a => a?.GetType().Name ?? "null"))
            : string.Empty;

        File.AppendAllText(logFile, $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName}({argSignature})\n");
    }

    private static bool TryExecutePathFunction(string methodName, out object? returnVal, object?[] args)
    {
        returnVal = default;
        if (string.Equals(methodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
        {
            // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
            switch (args.Length)
            {
                case 0:
                    return false;

                case 1 when ArgumentParser.TryGetArg(args, out string? arg0):
                    returnVal = Path.Combine(arg0);
                    return true;

                case 2 when ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1):
                    returnVal = Path.Combine(arg0, arg1);
                    return true;

                case 3 when ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1, out string? arg2):
                    returnVal = Path.Combine(arg0, arg1, arg2);
                    return true;

                case 4 when ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1, out string? arg2, out string? arg3):
                    returnVal = Path.Combine(arg0, arg1, arg2, arg3);
                    return true;

                default:
                    if (ArgumentParser.TryConvertToStrings(args, out string[]? stringArgs))
                    {
                        returnVal = Path.Combine(stringArgs);
                        return true;
                    }

                    return false;
            }
        }
        else if (string.Equals(methodName, nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = Path.DirectorySeparatorChar;
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                    ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                    : Path.GetFullPath(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = Path.IsPathRooted(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = Path.GetTempPath();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = Path.GetFileName(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = Path.GetDirectoryName(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = Path.GetFileNameWithoutExtension(arg0);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Handles execution of well-known string functions.
    /// </summary>
    private static bool TryExecuteStringFunction(string methodName, out object? returnVal, string text, object?[] args)
    {
        returnVal = null;
        if (string.Equals(methodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = text.StartsWith(arg0, StringComparison.CurrentCulture);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = text.Replace(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = text.Contains(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = text.ToUpperInvariant();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = text.ToLowerInvariant();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = text.EndsWith(arg0, StringComparison.CurrentCulture);
                return true;
            }
            else if (ArgumentParser.TryGetArgs(args, out arg0, out StringComparison arg1) && arg0 is not null)
            {
                returnVal = text.EndsWith(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = text.ToLower();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out StringComparison arg1) && arg0 is not null)
            {
                returnVal = text.IndexOf(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = text.AsSpan().IndexOfAny(arg0.AsSpan());
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = text.LastIndexOf(arg0, StringComparison.CurrentCulture);
                return true;
            }
            else if (ArgumentParser.TryGetArgs(args, out arg0, out int startIndex) && arg0 is not null)
            {
                returnVal = text.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture);
                return true;
            }
            else if (ArgumentParser.TryGetArgs(args, out arg0, out StringComparison arg1) && arg0 is not null)
            {
                returnVal = text.LastIndexOf(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0) && arg0 is not null)
            {
                returnVal = text.AsSpan().LastIndexOfAny(arg0.AsSpan());
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Length), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = text.Length;
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int startIndex))
            {
                returnVal = text.Substring(startIndex);
                return true;
            }
            else if (ArgumentParser.TryGetArgs(args, out startIndex, out int length))
            {
                returnVal = text.Substring(startIndex, length);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Split), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? separator) && separator?.Length == 1)
            {
                returnVal = text.Split(separator[0]);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int totalWidth))
            {
                returnVal = text.PadLeft(totalWidth);
                return true;
            }
            else if (ArgumentParser.TryGetArgs(args, out totalWidth, out char paddingChar))
            {
                returnVal = text.PadLeft(totalWidth, paddingChar);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int totalWidth))
            {
                returnVal = text.PadRight(totalWidth);
                return true;
            }
            else if (ArgumentParser.TryGetArgs(args, out totalWidth, out char paddingChar))
            {
                returnVal = text.PadRight(totalWidth, paddingChar);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? trimChars) && trimChars?.Length > 0)
            {
                returnVal = text.TrimStart(trimChars.ToCharArray());
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? trimChars) && trimChars?.Length > 0)
            {
                returnVal = text.TrimEnd(trimChars.ToCharArray());
                return true;
            }
        }
        else if (string.Equals(methodName, "get_Chars", StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int index))
            {
                returnVal = text[index];
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = text.Equals(arg0);
                return true;
            }
        }

        return false;
    }

    private static bool TryExecuteIntrinsicFunction(string methodName, out object? returnVal, IFileSystem fileSystem, object?[] args)
    {
        returnVal = default;
        if (string.Equals(methodName, nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryConvertToStrings(args, out string[]? stringArgs))
            {
                returnVal = IntrinsicFunctions.NormalizePath(stringArgs);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, fileSystem);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length >= 4 &&
                ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], new ArraySegment<object?>(args, 3, args.Length - 3));
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = IntrinsicFunctions.IsRunningFromVisualStudio();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.Escape(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.Unescape(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, fileSystem);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = IntrinsicFunctions.GetCurrentToolsDirectory();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = IntrinsicFunctions.GetToolsDirectory32();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = IntrinsicFunctions.GetToolsDirectory64();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = IntrinsicFunctions.GetMSBuildSDKsPath();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = IntrinsicFunctions.GetVsInstallRoot();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = IntrinsicFunctions.GetMSBuildExtensionsPath();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                returnVal = IntrinsicFunctions.GetProgramFiles32();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionEquals(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg0);
                return true;
            }

            if (ArgumentParser.TryGetArgs(args, out string? arg1, out int arg2))
            {
                returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg0);
                return true;
            }

            if (ArgumentParser.TryGetArgs(args, out string? arg1, out int arg2))
            {
                returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.ConvertToBase64(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.ConvertFromBase64(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.StableStringHash(arg0);
                return true;
            }
            else if (ArgumentParser.TryGetArgs(args, out string? arg1, out string? arg2) && Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg2, true, out var hashAlgorithm) && arg1 is not null && arg2 is not null)
            {
                returnVal = IntrinsicFunctions.StableStringHash(arg1, hashAlgorithm);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out Version? arg0) && arg0 is not null)
            {
                returnVal = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out int arg1, out int arg2) && arg0 is not null)
            {
                returnVal = IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.CheckFeatureAvailability(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.BitwiseOr(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.BitwiseAnd(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.BitwiseXor(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int arg0))
            {
                returnVal = IntrinsicFunctions.BitwiseNot(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.LeftShift(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.RightShift(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.RightShiftUnsigned(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.NormalizeDirectory(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.IsOSPlatform(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.FileExists(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = IntrinsicFunctions.DirectoryExists(arg0);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Shortcut to avoid calling into binding if we recognize some of the most common functions.
    ///  Binding is expensive and throws first-chance <see cref="MissingMethodException"/> exceptions,
    ///  which is bad for the debugging experience and has a performance cost.
    ///  A typical binding operation with an exception can take ~1.500 ms; this call is ~0.050 ms
    ///  (rough numbers just for comparison).
    ///  See https://github.com/dotnet/msbuild/issues/2217.
    /// </summary>
    /// <param name="methodName">The name of the function to call.</param>
    /// <param name="receiverType">The type that declares the function.</param>
    /// <param name="fileSystem">The file system used by intrinsic functions.</param>
    /// <param name="returnVal">The value returned from the function call.</param>
    /// <param name="objectInstance">Object that the function is called on.</param>
    /// <param name="args">The function arguments.</param>
    /// <returns>
    ///  <see langword="true"/> if the well-known function call binding was successful.
    /// </returns>
    public static bool TryExecuteWellKnownFunction(
        string methodName,
        Type receiverType,
        IFileSystem fileSystem,
        out object? returnVal,
        object? objectInstance,
        object?[] args)
    {
        returnVal = null;

        if (objectInstance is string text)
        {
            return TryExecuteStringFunction(methodName, out returnVal, text, args);
        }
        else if (objectInstance is string[] stringArray)
        {
            if (string.Equals(methodName, nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase))
            {
                if (ArgumentParser.TryGetArg(args, out int index))
                {
                    returnVal = stringArray[index];
                    return true;
                }
            }
        }
        else if (objectInstance is null) // Calling a well-known static function
        {
            if (receiverType == typeof(string))
            {
                if (string.Equals(methodName, nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
                {
                    if (ArgumentParser.TryGetArg(args, out string? arg0))
                    {
                        returnVal = string.IsNullOrWhiteSpace(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                {
                    if (ArgumentParser.TryGetArg(args, out string? arg0))
                    {
                        returnVal = string.IsNullOrEmpty(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                {
                    if (ArgumentParser.TryGetArg(args, out string? arg0))
                    {
                        returnVal = arg0;
                        return true;
                    }
                }
            }
            else if (receiverType == typeof(Math))
            {
                if (string.Equals(methodName, nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
                {
                    if (ArgumentParser.TryGetArgs(args, out double arg0, out double arg1))
                    {
                        returnVal = Math.Max(arg0, arg1);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                {
                    if (ArgumentParser.TryGetArgs(args, out double arg0, out double arg1))
                    {
                        returnVal = Math.Min(arg0, arg1);
                        return true;
                    }
                }
            }
            else if (receiverType == typeof(IntrinsicFunctions))
            {
                return TryExecuteIntrinsicFunction(methodName, out returnVal, fileSystem, args);
            }
            else if (receiverType == typeof(Path))
            {
                return TryExecutePathFunction(methodName, out returnVal, args);
            }
            else if (receiverType == typeof(Version))
            {
                if (string.Equals(methodName, nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                {
                    if (ArgumentParser.TryGetArg(args, out string? arg0))
                    {
                        returnVal = Version.Parse(arg0);
                        return true;
                    }
                }
            }
            else if (receiverType == typeof(Guid))
            {
                if (string.Equals(methodName, nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length == 0)
                    {
                        returnVal = Guid.NewGuid();
                        return true;
                    }
                }
            }
            else if (receiverType == typeof(char))
            {
                if (string.Equals(methodName, nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase))
                {
                    bool? result = null;

                    if (ArgumentParser.TryGetArg(args, out string? arg0) && arg0?.Length == 1)
                    {
                        char c = arg0[0];
                        result = char.IsDigit(c);
                    }
                    else if (ArgumentParser.TryGetArgs(args, out string? str, out int index) && str is not null)
                    {
                        result = char.IsDigit(str, index);
                    }

                    if (result.HasValue)
                    {
                        returnVal = result.Value;
                        return true;
                    }
                }
            }
            else if (receiverType == typeof(Regex))
            {
                if (string.Equals(methodName, nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) && args.Length == 3)
                {
                    if (ArgumentParser.TryGetArgs(args, out string? arg1, out string? arg2, out string? arg3))
                    {
                        returnVal = Regex.Replace(arg1, arg2, arg3);
                        return true;
                    }
                }
            }
        }
        else if (string.Equals(methodName, nameof(Version.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is Version v)
        {
            if (ArgumentParser.TryGetArg(args, out int arg0))
            {
                returnVal = v.ToString(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(int.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is int i)
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = i.ToString(arg0);
                return true;
            }
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", objectInstance, args);
        }

        return false;
    }

    public static bool TryExecuteWellKnownFunctionWithPropertiesParam<T>(
        string methodName,
        Type receiverType,
        LoggingContext loggingContext,
        IPropertyProvider<T> properties,
        out object? returnVal,
        object? objectInstance,
        object?[] args)
        where T : class, IProperty
    {
        returnVal = null;

        if (receiverType == typeof(IntrinsicFunctions))
        {
            if (string.Equals(methodName, nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
            {
                string projectPath = properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                if (ArgumentParser.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///  Shortcut to avoid calling into binding if we recognize some of the most common constructors.
    ///  Analogous to <see cref="TryExecuteWellKnownFunction"/> but guaranteed not to throw.
    /// </summary>
    /// <param name="receiverType">The receiver type for the constructor.</param>
    /// <param name="returnVal">The instance as created by the constructor call.</param>
    /// <param name="args">Arguments.</param>
    /// <returns>
    ///  <see langword="true"/> if the well-known constructor call binding was successful.
    /// </returns>
    public static bool TryExecuteWellKnownConstructorNoThrow(Type? receiverType, out object? returnVal, object?[] args)
    {
        returnVal = null;

        if (receiverType == typeof(string))
        {
            if (args.Length == 0)
            {
                returnVal = string.Empty;
                return true;
            }

            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                returnVal = arg0;
                return true;
            }
        }

        return false;
    }
}
