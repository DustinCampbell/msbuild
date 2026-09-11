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

namespace Microsoft.Build.Evaluation.Expander;

internal static class WellKnownFunctions
{
    internal static bool TryExecutePathFunction(
        string methodName,
        ref FunctionArguments args,
        ref readonly ExecutionContext context,
        out object? returnVal)
    {
        if (string.Equals(methodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
        {
            // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
            switch (args.Count)
            {
                case 0:
                    returnVal = null;
                    return false;

                case 1 when args.TryGetArg(out string? arg0):
                    returnVal = Path.Combine(arg0);
                    return true;

                case 2 when args.TryGetArgs(out string? arg0, out string? arg1):
                    returnVal = Path.Combine(arg0, arg1);
                    return true;

                case 3 when args.TryGetArgs(out string? arg0, out string? arg1, out string? arg2):
                    returnVal = Path.Combine(arg0, arg1, arg2);
                    return true;

                case 4 when args.TryGetArgs(out string? arg0, out string? arg1, out string? arg2, out string? arg3):
                    returnVal = Path.Combine(arg0, arg1, arg2, arg3);
                    return true;

                case > 4 when args.TryGetArgs(out string[]? paths):
                    returnVal = Path.Combine(paths);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = Path.DirectorySeparatorChar;
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                    ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                    : Path.GetFullPath(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = Path.IsPathRooted(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = Path.GetTempPath();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = Path.GetFileName(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = Path.GetDirectoryName(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = Path.GetFileNameWithoutExtension(arg0);
                return true;
            }
        }

        returnVal = null;
        return false;
    }

    /// <summary>
    ///  Executes a well-known <see cref="string"/> function.
    /// </summary>
    /// <param name="methodName">The function name.</param>
    /// <param name="text">The receiver value.</param>
    /// <param name="args">The function arguments.</param>
    /// <param name="context">Dependencies used to execute contextual functions.</param>
    /// <param name="returnVal">The function result.</param>
    /// <returns>
    ///  <see langword="true"/> when the function was handled; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryExecuteStringFunction(
        string methodName,
        string text,
        ref FunctionArguments args,
        ref readonly ExecutionContext context,
        out object? returnVal)
    {
        if (string.Equals(methodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = text.StartsWith(arg0, StringComparison.CurrentCulture);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = text.Replace(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = text.Contains(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = text.ToUpperInvariant();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = text.ToLowerInvariant();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out string? arg0):
                    returnVal = text.EndsWith(arg0, StringComparison.CurrentCulture);
                    return true;

                case 2 when args.TryGetArgs(out string? arg0, out StringComparison arg1):
                    returnVal = text.EndsWith(arg0, arg1);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = text.ToLower();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out StringComparison arg1))
            {
                returnVal = text.IndexOf(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = text.AsSpan().IndexOfAny(arg0.AsSpan());
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out string? value):
                    returnVal = text.LastIndexOf(value, StringComparison.CurrentCulture);
                    return true;

                case 2 when args.TryGetArgs(out string? value, out int startIndex):
                    returnVal = text.LastIndexOf(value, startIndex, StringComparison.CurrentCulture);
                    return true;

                case 2 when args.TryGetArgs(out string? value, out StringComparison comparisonType):
                    returnVal = text.LastIndexOf(value, comparisonType);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = text.AsSpan().LastIndexOfAny(arg0.AsSpan());
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Length), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = text.Length;
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out int startIndex):
                    returnVal = text.Substring(startIndex);
                    return true;

                case 2 when args.TryGetArgs(out int startIndex, out int length):
                    returnVal = text.Substring(startIndex, length);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Split), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out char separator))
            {
                returnVal = text.Split(separator);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out int totalWidth):
                    returnVal = text.PadLeft(totalWidth);
                    return true;

                case 2 when args.TryGetArgs(out int totalWidth, out char paddingChar):
                    returnVal = text.PadLeft(totalWidth, paddingChar);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out int totalWidth):
                    returnVal = text.PadRight(totalWidth);
                    return true;

                case 2 when args.TryGetArgs(out int totalWidth, out char paddingChar):
                    returnVal = text.PadRight(totalWidth, paddingChar);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? trimChars) && trimChars.Length > 0)
            {
                returnVal = text.TrimStart(trimChars.ToCharArray());
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? trimChars) && trimChars.Length > 0)
            {
                returnVal = text.TrimEnd(trimChars.ToCharArray());
                return true;
            }
        }
        else if (string.Equals(methodName, "get_Chars", StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out int index))
            {
                returnVal = text[index];
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = text.Equals(arg0);
                return true;
            }
        }

        returnVal = null;
        return false;
    }

    internal static bool TryExecuteIntrinsicFunction(
        string methodName,
        ref FunctionArguments args,
        ref readonly ExecutionContext context,
        out object? returnVal)
    {
        if (string.Equals(methodName, nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string[]? paths))
            {
                returnVal = IntrinsicFunctions.NormalizePath(paths);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, context.FileSystem);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count >= 4)
            {
                object?[] values = args.MaterializeAll();
                string? keyName = values[0] as string;
                string? valueName = values[1] as string;
                object? defaultValue = values[2];
                ArraySegment<object?> views = new(values, offset: 3, count: values.Length - 3);

                if (keyName is not null && valueName is not null)
                {
                    returnVal = IntrinsicFunctions.GetRegistryValueFromView(keyName, valueName, defaultValue, views);
                    return true;
                }
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = IntrinsicFunctions.IsRunningFromVisualStudio();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.Escape(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.Unescape(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out string? file):
                    returnVal = IntrinsicFunctions.GetPathOfFileAbove(file, context.GetStartingDirectory(), context.FileSystem);
                    return true;

                case 2 when args.TryGetArgs(out string? file, out string? startingDirectory):
                    returnVal = IntrinsicFunctions.GetPathOfFileAbove(file, startingDirectory, context.FileSystem);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Add, IntrinsicFunctions.Add, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out returnVal))
            {
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = IntrinsicFunctions.GetCurrentToolsDirectory();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = IntrinsicFunctions.GetToolsDirectory32();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = IntrinsicFunctions.GetToolsDirectory64();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = IntrinsicFunctions.GetMSBuildSDKsPath();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = IntrinsicFunctions.GetVsInstallRoot();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = IntrinsicFunctions.GetMSBuildExtensionsPath();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count == 0)
            {
                returnVal = IntrinsicFunctions.GetProgramFiles32();
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionEquals(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out string? tfm):
                    returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(tfm);
                    return true;

                case 2 when args.TryGetArgs(out string? tfm, out int versionPartCount):
                    returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(tfm, versionPartCount);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out string? arg1))
            {
                returnVal = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out string? tfm):
                    returnVal = IntrinsicFunctions.GetTargetPlatformVersion(tfm);
                    return true;

                case 2 when args.TryGetArgs(out string? tfm, out int versionPartCount):
                    returnVal = IntrinsicFunctions.GetTargetPlatformVersion(tfm, versionPartCount);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.ConvertToBase64(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.ConvertFromBase64(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
        {
            switch (args.Count)
            {
                case 1 when args.TryGetArg(out string? toHash):
                    returnVal = IntrinsicFunctions.StableStringHash(toHash);
                    return true;

                case 2 when args.TryGetArgs(out string? toHash, out string? arg2) &&
                            Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg2, true, out var hashAlgorithm):
                    returnVal = IntrinsicFunctions.StableStringHash(toHash, hashAlgorithm);
                    return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out Version? arg0))
            {
                returnVal = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out string? arg0, out int arg1, out int arg2))
            {
                returnVal = IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.CheckFeatureAvailability(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.BitwiseOr(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.BitwiseAnd(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.BitwiseXor(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out int arg0))
            {
                returnVal = IntrinsicFunctions.BitwiseNot(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.LeftShift(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.RightShift(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArgs(out int arg0, out int arg1))
            {
                returnVal = IntrinsicFunctions.RightShiftUnsigned(arg0, arg1);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.NormalizeDirectory(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.IsOSPlatform(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.FileExists(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.DirectoryExists(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
        {
            string projectPath = context.Properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
            LoggingContext? loggingContext = context.LoggingContext;
            Assumed.NotNull(
                loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");

            if (args.TryGetArg(out string? arg0))
            {
                returnVal = IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext);
                return true;
            }
        }

        returnVal = null;
        return false;
    }

    /// <summary>
    /// Shortcut to avoid calling into binding if we recognize some most common functions.
    /// Binding is expensive and throws first-chance MissingMethodExceptions, which is
    /// bad for debugging experience and has a performance cost.
    /// A typical binding operation with exception can take ~1.500 ms; this call is ~0.050 ms
    /// (rough numbers just for comparison).
    /// See https://github.com/dotnet/msbuild/issues/2217.
    /// </summary>
    /// <param name="methodName"> </param>
    /// <param name="receiverType"> </param>
    /// <param name="objectInstance">Object that the function is called on.</param>
    /// <param name="args">arguments.</param>
    /// <param name="context">Dependencies used to execute contextual functions.</param>
    /// <param name="returnVal">The value returned from the function call.</param>
    /// <returns>True if the well known function call binding was successful.</returns>
    internal static bool TryExecuteWellKnownFunction(
        string methodName,
        Type receiverType,
        object? objectInstance,
        ref FunctionArguments args,
        ref readonly ExecutionContext context,
        out object? returnVal)
    {
        if (objectInstance is string text)
        {
            return TryExecuteStringFunction(methodName, text, ref args, in context, out returnVal);
        }
        else if (objectInstance is string[] stringArray)
        {
            if (string.Equals(methodName, "GetValue", StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int index))
                {
                    returnVal = stringArray[index];
                    return true;
                }
            }
        }
        else if (objectInstance == null) // Calling a well-known static function
        {
            if (receiverType == typeof(string))
            {
                if (string.Equals(methodName, nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
                {
                    if (args.TryGetArg(out string? arg0))
                    {
                        returnVal = string.IsNullOrWhiteSpace(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                {
                    if (args.TryGetArg(out string? arg0))
                    {
                        returnVal = string.IsNullOrEmpty(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                {
                    if (args.TryGetArg(out string? arg0))
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
                    if (args.TryGetArgs(out double arg0, out double arg1))
                    {
                        returnVal = Math.Max(arg0, arg1);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                {
                    if (args.TryGetArgs(out double arg0, out double arg1))
                    {
                        returnVal = Math.Min(arg0, arg1);
                        return true;
                    }
                }
            }
            else if (receiverType == typeof(IntrinsicFunctions))
            {
                return TryExecuteIntrinsicFunction(methodName, ref args, in context, out returnVal);
            }
            else if (receiverType == typeof(Path))
            {
                return TryExecutePathFunction(methodName, ref args, in context, out returnVal);
            }
            else if (receiverType == typeof(Version))
            {
                if (string.Equals(methodName, nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                {
                    if (args.TryGetArg(out string? arg0))
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
                    if (args.Count == 0)
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
                    switch (args.Count)
                    {
                        case 1 when args.TryGetArg(out char c):
                            returnVal = char.IsDigit(c);
                            return true;

                        case 2 when args.TryGetArgs(out string? s, out int index):
                            returnVal = char.IsDigit(s, index);
                            return true;
                    }
                }
            }
            else if (receiverType == typeof(Regex))
            {
                if (string.Equals(methodName, nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) && args.Count == 3)
                {
                    if (args.TryGetArgs(out string? arg1, out string? arg2, out string? arg3))
                    {
                        returnVal = Regex.Replace(arg1, arg2, arg3);
                        return true;
                    }
                }
            }
        }
        else if (string.Equals(methodName, nameof(Version.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is Version v)
        {
            if (args.TryGetArg(out int arg0))
            {
                returnVal = v.ToString(arg0);
                return true;
            }
        }
        else if (string.Equals(methodName, nameof(Int32.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is int i)
        {
            if (args.TryGetArg(out string? arg0))
            {
                returnVal = i.ToString(arg0);
                return true;
            }
        }
        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", objectInstance, ref args);
        }

        returnVal = null;
        return false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void LogFunctionCall(Type receiverType, string methodName, string fileName, object? objectInstance, ref FunctionArguments args)
        {
            var logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            var argSignature = string.Join(", ", args.MaterializeAll().Select(a => a?.GetType().Name ?? "null"));

            File.AppendAllText(logFile, $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName}({argSignature})\n");
        }
    }

    /// <summary>
    /// Shortcut to avoid calling into binding if we recognize some most common constructors.
    /// Analogous to TryExecuteWellKnownFunction but guaranteed to not throw.
    /// </summary>
    /// <param name="receiverType"> Receiver type for the constructor. </param>
    /// <param name="args">Arguments.</param>
    /// <param name="context">Dependencies used to execute contextual functions.</param>
    /// <param name="returnVal">The instance as created by the constructor call.</param>
    /// <returns>True if the well known constructor call binding was successful.</returns>
    internal static bool TryExecuteWellKnownConstructorNoThrow(
        Type? receiverType,
        ref FunctionArguments args,
        ref readonly ExecutionContext context,
        out object? returnVal)
    {
        returnVal = null;

        if (receiverType == typeof(string))
        {
            if (args.Count == 0)
            {
                returnVal = string.Empty;
                return true;
            }

            if (args.TryGetArg(out string? arg0))
            {
                returnVal = arg0;
                return true;
            }
        }

        return false;
    }
}
