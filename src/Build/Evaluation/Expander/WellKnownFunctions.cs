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

    private static WellKnownFunctionResult TryInvokeStaticPathFunction(string methodName, Type receiverType, object?[] args)
    {
        if (receiverType != typeof(Path))
        {
            return WellKnownFunctionResult.NotHandled;
        }

        switch (methodName.Length)
        {
            case 7 when methodName.Equals(nameof(Path.Combine), StringComparison.OrdinalIgnoreCase):
                {
                    // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                    switch (args.Length)
                    {
                        case 0:
                            return WellKnownFunctionResult.NotHandled;

                        case 1 when args.TryGetArg(0, out string? arg0):
                            return WellKnownFunctionResult.Invoked(Path.Combine(arg0));

                        case 2 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out string? arg1):
                            return WellKnownFunctionResult.Invoked(Path.Combine(arg0, arg1));

                        case 3 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out string? arg1)
                                 && args.TryGetArg(2, out string? arg2):
                            return WellKnownFunctionResult.Invoked(Path.Combine(arg0, arg1, arg2));

                        case 4 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out string? arg1)
                                 && args.TryGetArg(2, out string? arg2)
                                 && args.TryGetArg(3, out string? arg3):
                            return WellKnownFunctionResult.Invoked(Path.Combine(arg0, arg1, arg2, arg3));

                        default:
                            if (ArgumentParser.TryConvertToStrings(args, out string[]? stringArgs))
                            {
                                return WellKnownFunctionResult.Invoked(Path.Combine(stringArgs));
                            }

                            return WellKnownFunctionResult.NotHandled;
                    }
                }

            case 22 when methodName.Equals(nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(Path.DirectorySeparatorChar);
                }

                break;

            case 11 when methodName.Equals(nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        string fullPath = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                            ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                            : Path.GetFullPath(arg0);
                        return WellKnownFunctionResult.Invoked(fullPath);
                    }
                }

                break;

            case 12 when methodName.Equals(nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(Path.IsPathRooted(arg0));
                    }
                }

                break;

            case 11 when methodName.Equals(nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(Path.GetTempPath());
                }

                break;

            case 11 when methodName.Equals(nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(Path.GetFileName(arg0));
                    }
                }

                break;

            case 16 when methodName.Equals(nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(Path.GetDirectoryName(arg0));
                    }
                }

                break;

            case 27 when methodName.Equals(nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(Path.GetFileNameWithoutExtension(arg0));
                    }
                }

                break;
        }

        return WellKnownFunctionResult.NotHandled;
    }

    /// <summary>
    ///  Handles execution of well-known string functions.
    /// </summary>
    private static WellKnownFunctionResult TryInvokeStringInstanceFunction(string methodName, object objectInstance, object?[] args)
    {
        if (objectInstance is not string text)
        {
            return WellKnownFunctionResult.NotHandled;
        }

        switch (methodName.Length)
        {
            case 5 when methodName.Equals(nameof(string.Split), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out char separator))
                    {
                        return WellKnownFunctionResult.Invoked(text.Split(separator));
                    }
                }

                break;

            case 6 when methodName.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(text.Length);
                }

                break;

            case 6 when methodName.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(text.Equals(arg0));
                    }
                }

                break;

            case 7 when methodName.Equals(nameof(string.Replace), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(text.Replace(arg0, arg1));
                    }
                }

                break;

            case 7 when methodName.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(text.ToLower());
                }

                break;

            case 7 when methodName.Equals(nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out StringComparison arg1))
                    {
                        return WellKnownFunctionResult.Invoked(text.IndexOf(arg0, arg1));
                    }
                }

                break;

            case 7 when methodName.Equals(nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase):
                {
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out int totalWidth):
                            return WellKnownFunctionResult.Invoked(text.PadLeft(totalWidth));

                        case 2 when args.TryGetArg(0, out int totalWidth)
                                 && args.TryGetArg(1, out char paddingChar):
                            return WellKnownFunctionResult.Invoked(text.PadLeft(totalWidth, paddingChar));
                    }
                }

                break;

            case 7 when methodName.Equals(nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? trimChars) && trimChars.Length > 0)
                    {
                        return WellKnownFunctionResult.Invoked(text.TrimEnd(trimChars.ToCharArray()));
                    }
                }

                break;

            case 8 when methodName.Equals(nameof(string.Contains), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(text.Contains(arg0));
                    }
                }

                break;

            case 8 when methodName.Equals(nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase):
                {
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out string? arg0):
                            return WellKnownFunctionResult.Invoked(text.EndsWith(arg0, StringComparison.CurrentCulture));

                        case 2 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out StringComparison arg1):
                            return WellKnownFunctionResult.Invoked(text.EndsWith(arg0, arg1));
                    }
                }

                break;

            case 8 when methodName.Equals(nameof(string.PadRight), StringComparison.OrdinalIgnoreCase):
                {
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out int totalWidth):
                            return WellKnownFunctionResult.Invoked(text.PadRight(totalWidth));

                        case 2 when args.TryGetArg(0, out int totalWidth)
                                 && args.TryGetArg(1, out char paddingChar):
                            return WellKnownFunctionResult.Invoked(text.PadRight(totalWidth, paddingChar));
                    }
                }

                break;

            case 9 when methodName.Equals(nameof(string.Substring), StringComparison.OrdinalIgnoreCase):
                {
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out int startIndex):
                            return WellKnownFunctionResult.Invoked(text.Substring(startIndex));

                        case 2 when args.TryGetArg(0, out int startIndex)
                                 && args.TryGetArg(1, out int length):
                            return WellKnownFunctionResult.Invoked(text.Substring(startIndex, length));
                    }
                }

                break;

            case 9 when methodName.Equals(nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? trimChars) && trimChars.Length > 0)
                    {
                        return WellKnownFunctionResult.Invoked(text.TrimStart(trimChars.ToCharArray()));
                    }
                }

                break;

            case 9 when methodName.Equals("get_Chars", StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out int index))
                    {
                        return WellKnownFunctionResult.Invoked(text[index]);
                    }
                }

                break;

            case 10 when methodName.Equals(nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(text.StartsWith(arg0, StringComparison.CurrentCulture));
                    }
                }

                break;

            case 10 when methodName.Equals(nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(text.AsSpan().IndexOfAny(arg0.AsSpan()));
                    }
                }

                break;

            case 11 when methodName.Equals(nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase):
                {
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out string? arg0):
                            return WellKnownFunctionResult.Invoked(text.LastIndexOf(arg0, StringComparison.CurrentCulture));

                        case 2 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out int startIndex):
                            return WellKnownFunctionResult.Invoked(text.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture));

                        case 2 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out StringComparison arg1):
                            return WellKnownFunctionResult.Invoked(text.LastIndexOf(arg0, arg1));
                    }
                }

                break;

            case 14 when methodName.Equals(nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(text.AsSpan().LastIndexOfAny(arg0.AsSpan()));
                    }
                }

                break;

            case 16 when methodName.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(text.ToUpperInvariant());
                }

                break;

            case 16 when methodName.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(text.ToLowerInvariant());
                }

                break;
        }

        return WellKnownFunctionResult.NotHandled;
    }

    private static WellKnownFunctionResult TryInvokeStaticIntrinsicFunction(string methodName, Type receiverType, object?[] args, IFileSystem fileSystem)
    {
        if (receiverType != typeof(IntrinsicFunctions))
        {
            return WellKnownFunctionResult.NotHandled;
        }

        switch (methodName.Length)
        {
            case 3 when methodName.Equals(nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase):
                {
                    if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out object? result))
                    {
                        return WellKnownFunctionResult.Invoked(result);
                    }
                }

                break;

            case 6 when methodName.Equals(nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.Escape(arg0));
                    }
                }

                break;

            case 6 when methodName.Equals(nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase):
                {
                    if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out object? result))
                    {
                        return WellKnownFunctionResult.Invoked(result);
                    }
                }

                break;

            case 6 when methodName.Equals(nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase):
                {
                    if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out object? result))
                    {
                        return WellKnownFunctionResult.Invoked(result);
                    }
                }

                break;

            case 8 when methodName.Equals(nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.Unescape(arg0));
                    }
                }

                break;

            case 8 when methodName.Equals(nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase):
                {
                    if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out object? result))
                    {
                        return WellKnownFunctionResult.Invoked(result);
                    }
                }

                break;

            case 8 when methodName.Equals(nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase):
                {
                    if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out object? result))
                    {
                        return WellKnownFunctionResult.Invoked(result);
                    }
                }

                break;

            case 9 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out int arg0) &&
                        args.TryGetArg(1, out int arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.BitwiseOr(arg0, arg1));
                    }
                }

                break;

            case 9 when methodName.Equals(nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out int arg0) &&
                        args.TryGetArg(1, out int arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.LeftShift(arg0, arg1));
                    }
                }

                break;

            case 10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out int arg0) &&
                        args.TryGetArg(1, out int arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.BitwiseAnd(arg0, arg1));
                    }
                }

                break;

            case 10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out int arg0) &&
                        args.TryGetArg(1, out int arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.BitwiseXor(arg0, arg1));
                    }
                }

                break;

            case 10 when methodName.Equals(nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out int arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.BitwiseNot(arg0));
                    }
                }

                break;

            case 10 when methodName.Equals(nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out int arg0) &&
                        args.TryGetArg(1, out int arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.RightShift(arg0, arg1));
                    }
                }

                break;

            case 10 when methodName.Equals(nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.FileExists(arg0));
                    }
                }

                break;

            case 12 when methodName.Equals(nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.IsOSPlatform(arg0));
                    }
                }

                break;

            case 13 when methodName.Equals(nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase):
                {
                    if (ArgumentParser.TryConvertToStrings(args, out string[]? stringArgs))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.NormalizePath(stringArgs));
                    }
                }

                break;

            case 13 when methodName.Equals(nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionEquals(arg0, arg1));
                    }
                }

                break;

            case 14 when methodName.Equals(nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.ValueOrDefault(arg0, arg1));
                    }
                }

                break;

            case 15 when methodName.Equals(nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.ConvertToBase64(arg0));
                    }
                }

                break;

            case 15 when methodName.Equals(nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionLessThan(arg0, arg1));
                    }
                }

                break;

            case 15 when methodName.Equals(nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.DirectoryExists(arg0));
                    }
                }

                break;

            case 16 when methodName.Equals(nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 0)
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetVsInstallRoot());
                    }
                }

                break;

            case 16 when methodName.Equals(nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionNotEquals(arg0, arg1));
                    }
                }

                break;

            case 16 when methodName.Equals(nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase):
                {
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out string? arg0):
                            return WellKnownFunctionResult.Invoked(IntrinsicFunctions.StableStringHash(arg0));

                        case 2 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out string? arg1)
                                 && Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg1, true, out var hashAlgorithm):
                            return WellKnownFunctionResult.Invoked(IntrinsicFunctions.StableStringHash(arg0, hashAlgorithm));
                    }
                }

                break;

            case 17 when methodName.Equals(nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.ConvertFromBase64(arg0));
                    }
                }

                break;

            case 17 when methodName.Equals(nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 0)
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetProgramFiles32());
                    }
                }

                break;

            case 18 when methodName.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, fileSystem));
                    }
                }

                break;

            case 18 when methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetMSBuildSDKsPath());
                }

                break;

            case 18 when methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionGreaterThan(arg0, arg1));
                    }
                }

                break;

            case 18 when methodName.Equals(nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out Version? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.AreFeaturesEnabled(arg0));
                    }
                }

                break;

            case 18 when methodName.Equals(nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out int arg0) &&
                        args.TryGetArg(1, out int arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.RightShiftUnsigned(arg0, arg1));
                    }
                }

                break;

            case 18 when methodName.Equals(nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.NormalizeDirectory(arg0));
                    }
                }

                break;

            case 19 when methodName.Equals(nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.EnsureTrailingSlash(arg0));
                    }
                }

                break;

            case 19 when methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetToolsDirectory32());
                }

                break;

            case 19 when methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetToolsDirectory64());
                }

                break;

            case 21 when methodName.Equals(nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 3 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out int arg1) &&
                        args.TryGetArg(2, out int arg2))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2));
                    }
                }

                break;

            case 23 when methodName.Equals(nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1));
                    }
                }

                break;

            case 24 when methodName.Equals(nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length >= 4 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], new ArraySegment<object?>(args, 3, args.Length - 3)));
                    }
                }

                break;

            case 24 when methodName.Equals(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetCurrentToolsDirectory());
                }

                break;

            case 24 when methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetMSBuildExtensionsPath());
                }

                break;

            case 24 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase):
                {
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out string? arg0):
                            return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetPlatformVersion(arg0));

                        case 2 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out int arg1):
                            return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetPlatformVersion(arg0, arg1));
                    }
                }

                break;

            case 24 when methodName.Equals(nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.CheckFeatureAvailability(arg0));
                    }
                }

                break;

            case 25 when methodName.Equals(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase):
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(IntrinsicFunctions.IsRunningFromVisualStudio());
                }

                break;

            case 25 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase):
                {
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out string? arg0):
                            return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetFrameworkVersion(arg0));

                        case 2 when args.TryGetArg(0, out string? arg0)
                                 && args.TryGetArg(1, out int arg1):
                            return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetFrameworkVersion(arg0, arg1));
                    }
                }

                break;

            case 26 when methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1));
                    }
                }

                break;

            case 27 when methodName.Equals(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, fileSystem));
                    }
                }

                break;

            case 27 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetPlatformIdentifier(arg0));
                    }
                }

                break;

            case 27 when methodName.Equals(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 2 &&
                        args.TryGetArg(0, out string? arg0) &&
                        args.TryGetArg(1, out string? arg1))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1));
                    }
                }

                break;

            case 28 when methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase):
                {
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0));
                    }
                }

                break;
        }

        return WellKnownFunctionResult.NotHandled;
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
    /// <param name="args">The function arguments.</param>
    /// <param name="fileSystem">The file system used by intrinsic functions.</param>
    /// <returns>
    ///  The invocation status and result.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeStatic(string methodName, Type receiverType, object?[] args, IFileSystem fileSystem)
    {
        if (receiverType == typeof(string))
        {
            switch (methodName.Length)
            {
                case 4 when methodName.Equals(nameof(string.Copy), StringComparison.OrdinalIgnoreCase):
                    {
                        if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                        {
                            return WellKnownFunctionResult.Invoked(arg0);
                        }
                    }

                    break;

                case 13 when methodName.Equals(nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase):
                    {
                        if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                        {
                            return WellKnownFunctionResult.Invoked(string.IsNullOrEmpty(arg0));
                        }
                    }

                    break;

                case 18 when methodName.Equals(nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase):
                    {
                        if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                        {
                            return WellKnownFunctionResult.Invoked(string.IsNullOrWhiteSpace(arg0));
                        }
                    }

                    break;
            }
        }
        else if (receiverType == typeof(Math))
        {
            switch (methodName.Length)
            {
                case 3 when methodName.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase):
                    {
                        if (args.Length == 2 &&
                            args.TryGetArg(0, out double arg0) &&
                            args.TryGetArg(1, out double arg1))
                        {
                            return WellKnownFunctionResult.Invoked(Math.Max(arg0, arg1));
                        }
                    }

                    break;

                case 3 when methodName.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase):
                    {
                        if (args.Length == 2 &&
                            args.TryGetArg(0, out double arg0) &&
                            args.TryGetArg(1, out double arg1))
                        {
                            return WellKnownFunctionResult.Invoked(Math.Min(arg0, arg1));
                        }
                    }

                    break;
            }
        }
        else if (receiverType == typeof(IntrinsicFunctions))
        {
            return TryInvokeStaticIntrinsicFunction(methodName, receiverType, args, fileSystem);
        }
        else if (receiverType == typeof(Path))
        {
            return TryInvokeStaticPathFunction(methodName, receiverType, args);
        }
        else if (receiverType == typeof(Version))
        {
            switch (methodName.Length)
            {
                case 5 when methodName.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase):
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(Version.Parse(arg0));
                    }

                    break;
            }
        }
        else if (receiverType == typeof(Guid))
        {
            switch (methodName.Length)
            {
                case 7 when methodName.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase):
                    if (args.Length == 0)
                    {
                        return WellKnownFunctionResult.Invoked(Guid.NewGuid());
                    }

                    break;
            }
        }
        else if (receiverType == typeof(char))
        {
            switch (methodName.Length)
            {
                case 7 when methodName.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase):
                    switch (args.Length)
                    {
                        case 1 when args.TryGetArg(0, out char c):
                            return WellKnownFunctionResult.Invoked(char.IsDigit(c));

                        case 2 when args.TryGetArg(0, out string? s)
                                     && args.TryGetArg(1, out int index):
                            return WellKnownFunctionResult.Invoked(char.IsDigit(s, index));
                    }

                    break;
            }
        }
        else if (receiverType == typeof(Regex))
        {
            switch (methodName.Length)
            {
                case 7 when methodName.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase):
                    if (args.Length == 3 &&
                        args.TryGetArg(0, out string? arg1) &&
                        args.TryGetArg(1, out string? arg2) &&
                        args.TryGetArg(2, out string? arg3))
                    {
                        return WellKnownFunctionResult.Invoked(Regex.Replace(arg1, arg2, arg3));
                    }

                    break;
            }
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", null, args);
        }

        return WellKnownFunctionResult.NotHandled;
    }

    public static WellKnownFunctionResult TryInvokeInstance(string methodName, object objectInstance, object?[] args)
    {
        if (objectInstance is string)
        {
            return TryInvokeStringInstanceFunction(methodName, objectInstance, args);
        }
        else if (objectInstance is string[] stringArray)
        {
            switch (methodName.Length)
            {
                case 8 when methodName.Equals(nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase):
                    if (args.Length == 1 && args.TryGetArg(0, out int index))
                    {
                        return WellKnownFunctionResult.Invoked(stringArray[index]);
                    }

                    break;
            }
        }
        else if (objectInstance is Version v)
        {
            switch (methodName.Length)
            {
                case 8 when methodName.Equals(nameof(Version.ToString), StringComparison.OrdinalIgnoreCase):
                    if (args.Length == 1 && args.TryGetArg(0, out int arg0))
                    {
                        return WellKnownFunctionResult.Invoked(v.ToString(arg0));
                    }

                    break;
            }
        }
        else if (objectInstance is int i)
        {
            switch (methodName.Length)
            {
                case 8 when methodName.Equals(nameof(int.ToString), StringComparison.OrdinalIgnoreCase):
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(i.ToString(arg0));
                    }

                    break;
            }
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(objectInstance.GetType(), methodName, "PropertyFunctionsRequiringReflection", objectInstance, args);
        }

        return WellKnownFunctionResult.NotHandled;
    }

    public static WellKnownFunctionResult TryInvokeStatic<T>(
        string methodName,
        Type receiverType,
        object?[] args,
        LoggingContext loggingContext,
        IPropertyProvider<T> properties)
        where T : class, IProperty
    {
        if (receiverType == typeof(IntrinsicFunctions))
        {
            switch (methodName.Length)
            {
                case 18 when methodName.Equals(nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase):
                    string projectPath = properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                    Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                    if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
                    {
                        return WellKnownFunctionResult.Invoked(IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext));
                    }

                    break;
            }
        }

        return WellKnownFunctionResult.NotHandled;
    }

    /// <summary>
    ///  Shortcut to avoid calling into binding if we recognize some of the most common constructors.
    ///  Analogous to <see cref="TryInvokeStatic"/> but guaranteed not to throw.
    /// </summary>
    /// <param name="receiverType">The receiver type for the constructor.</param>
    /// <param name="args">Arguments.</param>
    /// <returns>
    ///  The invocation status and result.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeWellKnownConstructorNoThrow(Type? receiverType, object?[] args)
    {
        if (receiverType == typeof(string))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(string.Empty);
            }

            if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(arg0);
            }
        }

        return WellKnownFunctionResult.NotHandled;
    }
}
