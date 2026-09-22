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

        if (string.Equals(methodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
        {
            // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
            switch (args.Length)
            {
                case 0:
                    return WellKnownFunctionResult.NotHandled;

                case 1 when ArgumentParser.TryGetArg(args, out string? arg0):
                    return WellKnownFunctionResult.Invoked(Path.Combine(arg0));

                case 2 when ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1):
                    return WellKnownFunctionResult.Invoked(Path.Combine(arg0, arg1));

                case 3 when ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1, out string? arg2):
                    return WellKnownFunctionResult.Invoked(Path.Combine(arg0, arg1, arg2));

                case 4 when ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1, out string? arg2, out string? arg3):
                    return WellKnownFunctionResult.Invoked(Path.Combine(arg0, arg1, arg2, arg3));

                default:
                    if (ArgumentParser.TryConvertToStrings(args, out string[]? stringArgs))
                    {
                        return WellKnownFunctionResult.Invoked(Path.Combine(stringArgs));
                    }

                    return WellKnownFunctionResult.NotHandled;
            }
        }
        else if (string.Equals(methodName, nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(Path.DirectorySeparatorChar);
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                string fullPath = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                    ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                    : Path.GetFullPath(arg0);
                return WellKnownFunctionResult.Invoked(fullPath);
            }
        }
        else if (string.Equals(methodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(Path.IsPathRooted(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(Path.GetTempPath());
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(Path.GetFileName(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(Path.GetDirectoryName(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(Path.GetFileNameWithoutExtension(arg0));
            }
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

        if (string.Equals(methodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(text.StartsWith(arg0, StringComparison.CurrentCulture));
            }
        }
        else if (string.Equals(methodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(text.Replace(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(text.Contains(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(text.ToUpperInvariant());
            }
        }
        else if (string.Equals(methodName, nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(text.ToLowerInvariant());
            }
        }
        else if (string.Equals(methodName, nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(text.EndsWith(arg0, StringComparison.CurrentCulture));
            }
            else if (ArgumentParser.TryGetArgs(args, out arg0, out StringComparison arg1) && arg0 is not null)
            {
                return WellKnownFunctionResult.Invoked(text.EndsWith(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(text.ToLower());
            }
        }
        else if (string.Equals(methodName, nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out StringComparison arg1) && arg0 is not null)
            {
                return WellKnownFunctionResult.Invoked(text.IndexOf(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(text.AsSpan().IndexOfAny(arg0.AsSpan()));
            }
        }
        else if (string.Equals(methodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(text.LastIndexOf(arg0, StringComparison.CurrentCulture));
            }
            else if (ArgumentParser.TryGetArgs(args, out arg0, out int startIndex) && arg0 is not null)
            {
                return WellKnownFunctionResult.Invoked(text.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture));
            }
            else if (ArgumentParser.TryGetArgs(args, out arg0, out StringComparison arg1) && arg0 is not null)
            {
                return WellKnownFunctionResult.Invoked(text.LastIndexOf(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0) && arg0 is not null)
            {
                return WellKnownFunctionResult.Invoked(text.AsSpan().LastIndexOfAny(arg0.AsSpan()));
            }
        }
        else if (string.Equals(methodName, nameof(string.Length), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(text.Length);
            }
        }
        else if (string.Equals(methodName, nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int startIndex))
            {
                return WellKnownFunctionResult.Invoked(text.Substring(startIndex));
            }
            else if (ArgumentParser.TryGetArgs(args, out startIndex, out int length))
            {
                return WellKnownFunctionResult.Invoked(text.Substring(startIndex, length));
            }
        }
        else if (string.Equals(methodName, nameof(string.Split), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? separator) && separator?.Length == 1)
            {
                return WellKnownFunctionResult.Invoked(text.Split(separator[0]));
            }
        }
        else if (string.Equals(methodName, nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int totalWidth))
            {
                return WellKnownFunctionResult.Invoked(text.PadLeft(totalWidth));
            }
            else if (ArgumentParser.TryGetArgs(args, out totalWidth, out char paddingChar))
            {
                return WellKnownFunctionResult.Invoked(text.PadLeft(totalWidth, paddingChar));
            }
        }
        else if (string.Equals(methodName, nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int totalWidth))
            {
                return WellKnownFunctionResult.Invoked(text.PadRight(totalWidth));
            }
            else if (ArgumentParser.TryGetArgs(args, out totalWidth, out char paddingChar))
            {
                return WellKnownFunctionResult.Invoked(text.PadRight(totalWidth, paddingChar));
            }
        }
        else if (string.Equals(methodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? trimChars) && trimChars?.Length > 0)
            {
                return WellKnownFunctionResult.Invoked(text.TrimStart(trimChars.ToCharArray()));
            }
        }
        else if (string.Equals(methodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? trimChars) && trimChars?.Length > 0)
            {
                return WellKnownFunctionResult.Invoked(text.TrimEnd(trimChars.ToCharArray()));
            }
        }
        else if (string.Equals(methodName, "get_Chars", StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int index))
            {
                return WellKnownFunctionResult.Invoked(text[index]);
            }
        }
        else if (string.Equals(methodName, nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(text.Equals(arg0));
            }
        }

        return WellKnownFunctionResult.NotHandled;
    }

    private static WellKnownFunctionResult TryInvokeStaticIntrinsicFunction(string methodName, Type receiverType, object?[] args, IFileSystem fileSystem)
    {
        if (receiverType != typeof(IntrinsicFunctions))
        {
            return WellKnownFunctionResult.NotHandled;
        }

        if (string.Equals(methodName, nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.EnsureTrailingSlash(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.ValueOrDefault(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryConvertToStrings(args, out string[]? stringArgs))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.NormalizePath(stringArgs));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, fileSystem));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length >= 4 &&
                ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], new ArraySegment<object?>(args, 3, args.Length - 3)));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.IsRunningFromVisualStudio());
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.Escape(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.Unescape(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, fileSystem));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out object? result))
            {
                return WellKnownFunctionResult.Invoked(result);
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out object? result))
            {
                return WellKnownFunctionResult.Invoked(result);
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out object? result))
            {
                return WellKnownFunctionResult.Invoked(result);
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out object? result))
            {
                return WellKnownFunctionResult.Invoked(result);
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out object? result))
            {
                return WellKnownFunctionResult.Invoked(result);
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetCurrentToolsDirectory());
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetToolsDirectory32());
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetToolsDirectory64());
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetMSBuildSDKsPath());
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetVsInstallRoot());
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetMSBuildExtensionsPath());
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetProgramFiles32());
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionEquals(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionNotEquals(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionGreaterThan(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionLessThan(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetFrameworkVersion(arg0));
            }

            if (ArgumentParser.TryGetArgs(args, out string? arg1, out int arg2))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetPlatformIdentifier(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetPlatformVersion(arg0));
            }

            if (ArgumentParser.TryGetArgs(args, out string? arg1, out int arg2))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.ConvertToBase64(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.ConvertFromBase64(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.StableStringHash(arg0));
            }
            else if (ArgumentParser.TryGetArgs(args, out string? arg1, out string? arg2) && Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg2, true, out var hashAlgorithm) && arg1 is not null && arg2 is not null)
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.StableStringHash(arg1, hashAlgorithm));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out Version? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.AreFeaturesEnabled(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out string? arg0, out int arg1, out int arg2))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.CheckFeatureAvailability(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.BitwiseOr(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.BitwiseAnd(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.BitwiseXor(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out int arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.BitwiseNot(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.LeftShift(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.RightShift(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArgs(args, out int arg0, out int arg1))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.RightShiftUnsigned(arg0, arg1));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.NormalizeDirectory(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.IsOSPlatform(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.FileExists(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(IntrinsicFunctions.DirectoryExists(arg0));
            }
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
            if (string.Equals(methodName, nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
            {
                if (ArgumentParser.TryGetArg(args, out string? arg0))
                {
                    return WellKnownFunctionResult.Invoked(string.IsNullOrWhiteSpace(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
            {
                if (ArgumentParser.TryGetArg(args, out string? arg0))
                {
                    return WellKnownFunctionResult.Invoked(string.IsNullOrEmpty(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
            {
                if (ArgumentParser.TryGetArg(args, out string? arg0))
                {
                    return WellKnownFunctionResult.Invoked(arg0);
                }
            }
        }
        else if (receiverType == typeof(Math))
        {
            if (string.Equals(methodName, nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
            {
                if (ArgumentParser.TryGetArgs(args, out double arg0, out double arg1))
                {
                    return WellKnownFunctionResult.Invoked(Math.Max(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
            {
                if (ArgumentParser.TryGetArgs(args, out double arg0, out double arg1))
                {
                    return WellKnownFunctionResult.Invoked(Math.Min(arg0, arg1));
                }
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
            if (string.Equals(methodName, nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
            {
                if (ArgumentParser.TryGetArg(args, out string? arg0))
                {
                    return WellKnownFunctionResult.Invoked(Version.Parse(arg0));
                }
            }
        }
        else if (receiverType == typeof(Guid))
        {
            if (string.Equals(methodName, nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return WellKnownFunctionResult.Invoked(Guid.NewGuid());
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
                    return WellKnownFunctionResult.Invoked(result.Value);
                }
            }
        }
        else if (receiverType == typeof(Regex))
        {
            if (string.Equals(methodName, nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) && args.Length == 3)
            {
                if (ArgumentParser.TryGetArgs(args, out string? arg1, out string? arg2, out string? arg3))
                {
                    return WellKnownFunctionResult.Invoked(Regex.Replace(arg1, arg2, arg3));
                }
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
            if (string.Equals(methodName, nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase))
            {
                if (ArgumentParser.TryGetArg(args, out int index))
                {
                    return WellKnownFunctionResult.Invoked(stringArray[index]);
                }
            }
        }
        else if (string.Equals(methodName, nameof(Version.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is Version v)
        {
            if (ArgumentParser.TryGetArg(args, out int arg0))
            {
                return WellKnownFunctionResult.Invoked(v.ToString(arg0));
            }
        }
        else if (string.Equals(methodName, nameof(int.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is int i)
        {
            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(i.ToString(arg0));
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
            if (string.Equals(methodName, nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
            {
                string projectPath = properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                if (ArgumentParser.TryGetArg(args, out string? arg0))
                {
                    return WellKnownFunctionResult.Invoked(IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext));
                }
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

            if (ArgumentParser.TryGetArg(args, out string? arg0))
            {
                return WellKnownFunctionResult.Invoked(arg0);
            }
        }

        return WellKnownFunctionResult.NotHandled;
    }
}
