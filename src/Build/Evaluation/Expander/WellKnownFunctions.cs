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
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander
{
    internal class WellKnownFunctions
    {
        internal static bool ElementsOfType(FunctionArguments args, Type type)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i]?.GetType() != type)
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogFunctionCall(Type receiverType, StringSegment methodName, string fileName, object? objectInstance, FunctionArguments args)
        {
            var logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            var argSignature = string.Join(", ", args.ToObjectArray().Select(a => a?.GetType().Name ?? "null"));

            File.AppendAllText(logFile, $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName.ValueOrEmpty}({argSignature})\n");
        }

        internal static bool TryExecutePathFunction(StringSegment methodName, out object? returnVal, FunctionArguments args)
        {
            returnVal = default;
            if (methodName.Equals(nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
            {
                string? arg0, arg1, arg2, arg3;

                // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                switch (args.Length)
                {
                    case 0:
                        return false;
                    case 1:
                        if (args.TryGetArg(out arg0) && arg0 != null)
                        {
                            returnVal = Path.Combine(arg0);
                            return true;
                        }
                        break;
                    case 2:
                        if (args.TryGetArgs(out arg0, out arg1) && arg0 != null && arg1 != null)
                        {
                            returnVal = Path.Combine(arg0, arg1);
                            return true;
                        }
                        break;
                    case 3:
                        if (args.TryGetArgs(out arg0, out arg1, out arg2) && arg0 != null && arg1 != null && arg2 != null)
                        {
                            returnVal = Path.Combine(arg0, arg1, arg2);
                            return true;
                        }
                        break;
                    case 4:
                        if (args.TryGetArgs(out arg0, out arg1, out arg2, out arg3) && arg0 != null && arg1 != null && arg2 != null && arg3 != null)
                        {
                            returnVal = Path.Combine(arg0, arg1, arg2, arg3);
                            return true;
                        }
                        break;
                    default:
                        if (ElementsOfType(args, typeof(string)))
                        {
                            returnVal = Path.Combine(Array.ConvertAll(args.ToObjectArray(), o => (string)o));
                            return true;
                        }
                        break;
                }
            }
            else if (methodName.Equals(nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = Path.DirectorySeparatorChar;
                    return true;
                }
            }
            else if (methodName.Equals(nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                        ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                        : Path.GetFullPath(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = Path.IsPathRooted(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = Path.GetTempPath();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = Path.GetFileName(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = Path.GetDirectoryName(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = Path.GetFileNameWithoutExtension(arg0);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Handler for executing well known string functions
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="returnVal"></param>
        /// <param name="text"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static bool TryExecuteStringFunction(StringSegment methodName, out object? returnVal, string text, FunctionArguments args)
        {
            returnVal = null;
            StringSegment receiver = text;
            if (methodName.Equals(nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    returnVal = receiver.StartsWith(arg0, StringComparison.CurrentCulture);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1) && arg0 != null)
                {
                    returnVal = text.Replace(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    returnVal = receiver.Contains(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = text.ToUpperInvariant();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = text.ToLowerInvariant();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    returnVal = receiver.EndsWith(arg0, StringComparison.CurrentCulture);
                    return true;
                }
                else if (args.TryGetArgs(out arg0, out StringComparison arg1))
                {
                    returnVal = receiver.EndsWith(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = text.ToLower();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out StringSegment arg0, out StringComparison arg1))
                {
                    returnVal = receiver.IndexOf(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    returnVal = receiver.IndexOfAny(arg0.AsSpan());
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    returnVal = receiver.LastIndexOf(arg0, StringComparison.CurrentCulture);
                    return true;
                }
                else if (args.TryGetArgs(out arg0, out int startIndex))
                {
                    returnVal = receiver.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture);
                    return true;
                }
                else if (args.TryGetArgs(out arg0, out StringComparison arg1))
                {
                    returnVal = receiver.LastIndexOf(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    returnVal = receiver.LastIndexOfAny(arg0.AsSpan());
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = text.Length;
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int startIndex))
                {
                    returnVal = text.Substring(startIndex);
                    return true;
                }
                else if (args.TryGetArgs(out startIndex, out int length))
                {
                    returnVal = text.Substring(startIndex, length);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.Split), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment separator) && separator.Length == 1)
                {
                    returnVal = text.Split(separator[0]);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int totalWidth))
                {
                    returnVal = text.PadLeft(totalWidth);
                    return true;
                }
                else if (args.TryGetArgs(out totalWidth, out StringSegment paddingChar) && paddingChar.Length == 1)
                {
                    returnVal = text.PadLeft(totalWidth, paddingChar[0]);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int totalWidth))
                {
                    returnVal = text.PadRight(totalWidth);
                    return true;
                }
                else if (args.TryGetArgs(out totalWidth, out StringSegment paddingChar) && paddingChar.Length == 1)
                {
                    returnVal = text.PadRight(totalWidth, paddingChar[0]);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment trimChars) && trimChars.Length > 0)
                {
                    returnVal = text.TrimStart(trimChars.AsSpan().ToArray());
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment trimChars) && trimChars.Length > 0)
                {
                    returnVal = text.TrimEnd(trimChars.AsSpan().ToArray());
                    return true;
                }
            }
            else if (methodName.Equals("get_Chars", StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int index))
                {
                    returnVal = text[index];
                    return true;
                }
            }
            else if (methodName.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    returnVal = receiver.Equals(arg0);
                    return true;
                }
            }
            return false;
        }

        internal static bool TryExecuteIntrinsicFunction(StringSegment methodName, out object? returnVal, IFileSystem fileSystem, FunctionArguments args)
        {
            returnVal = default;
            if (methodName.Equals(nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
            {
                if (ElementsOfType(args, typeof(string)))
                {
                    returnVal = IntrinsicFunctions.NormalizePath(Array.ConvertAll(args.ToObjectArray(), o => (string)o));
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, fileSystem);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length >= 4 &&
                    args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    object[] values = args.ToObjectArray();
                    returnVal = IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, values[2], new ArraySegment<object>(values, 3, values.Length - 3));
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = IntrinsicFunctions.IsRunningFromVisualStudio();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.Escape(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.Unescape(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, fileSystem);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Add, IntrinsicFunctions.Add, out returnVal))
                {
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out returnVal))
                {
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out returnVal))
                {
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out returnVal))
                {
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out returnVal))
                {
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = IntrinsicFunctions.GetCurrentToolsDirectory();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = IntrinsicFunctions.GetToolsDirectory32();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = IntrinsicFunctions.GetToolsDirectory64();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = IntrinsicFunctions.GetMSBuildSDKsPath();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = IntrinsicFunctions.GetVsInstallRoot();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = IntrinsicFunctions.GetMSBuildExtensionsPath();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    returnVal = IntrinsicFunctions.GetProgramFiles32();
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionEquals(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg0);
                    return true;
                }
                if (args.TryGetArgs(out string? arg1, out int arg2))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg0);
                    return true;
                }
                if (args.TryGetArgs(out string? arg1, out int arg2))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.ConvertToBase64(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.ConvertFromBase64(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.StableStringHash(arg0);
                    return true;
                }
                else if (args.TryGetArgs(out string? arg1, out string? arg2) && Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg2, true, out var hashAlgorithm) && arg1 != null && arg2 != null)
                {
                    returnVal = IntrinsicFunctions.StableStringHash(arg1, hashAlgorithm);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out Version? arg0) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out int arg1, out int arg2) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.CheckFeatureAvailability(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseOr(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseAnd(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseXor(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int arg0))
                {
                    returnVal = IntrinsicFunctions.BitwiseNot(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.LeftShift(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.RightShift(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.RightShiftUnsigned(arg0, arg1);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.NormalizeDirectory(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.IsOSPlatform(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.FileExists(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    returnVal = IntrinsicFunctions.DirectoryExists(arg0);
                    return true;
                }
            }
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
        /// <param name="fileSystem"> </param>
        /// <param name="returnVal">The value returned from the function call.</param>
        /// <param name="objectInstance">Object that the function is called on.</param>
        /// <param name="args">arguments.</param>
        /// <returns>True if the well known function call binding was successful.</returns>
        internal static bool TryExecuteWellKnownFunction(StringSegment methodName, Type receiverType, IFileSystem fileSystem, out object? returnVal, object objectInstance, FunctionArguments args)
        {
            returnVal = null;

            if (objectInstance is string text)
            {
                return TryExecuteStringFunction(methodName, out returnVal, text, args);
            }
            else if (objectInstance is string[] stringArray)
            {
                if (methodName.Equals("GetValue", StringComparison.OrdinalIgnoreCase))
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
                    if (methodName.Equals(nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArg(out string? arg0))
                        {
                            returnVal = string.IsNullOrWhiteSpace(arg0);
                            return true;
                        }
                    }
                    else if (methodName.Equals(nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArg(out string? arg0))
                        {
                            returnVal = string.IsNullOrEmpty(arg0);
                            return true;
                        }
                    }
                    else if (methodName.Equals(nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
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
                    if (methodName.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArgs(out double arg0, out double arg1))
                        {
                            returnVal = Math.Max(arg0, arg1);
                            return true;
                        }
                    }
                    else if (methodName.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
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
                    return TryExecuteIntrinsicFunction(methodName, out returnVal, fileSystem, args);
                }
                else if (receiverType == typeof(Path))
                {
                    return TryExecutePathFunction(methodName, out returnVal, args);
                }
                else if (receiverType == typeof(Version))
                {
                    if (methodName.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArg(out string? arg0) && arg0 != null)
                        {
                            returnVal = Version.Parse(arg0);
                            return true;
                        }
                    }
                }
                else if (receiverType == typeof(Guid))
                {
                    if (methodName.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
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
                    if (methodName.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase))
                    {
                        bool? result = null;

                        if (args.TryGetArg(out string? arg0) && arg0?.Length == 1)
                        {
                            char c = arg0[0];
                            result = char.IsDigit(c);
                        }
                        else if (args.TryGetArgs(out string? str, out int index) && str != null)
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
                    if (methodName.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) && args.Length == 3)
                    {
                        if (args.TryGetArgs(out string? arg1, out string? arg2, out string? arg3) && arg1 != null && arg2 != null && arg3 != null)
                        {
                            returnVal = Regex.Replace(arg1, arg2, arg3);
                            return true;
                        }
                    }
                }
            }
            else if (methodName.Equals(nameof(Version.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is Version v)
            {
                if (args.TryGetArg(out int arg0))
                {
                    returnVal = v.ToString(arg0);
                    return true;
                }
            }
            else if (methodName.Equals(nameof(Int32.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is int i)
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
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

        internal static bool TryExecuteWellKnownFunctionWithPropertiesParam<T>(StringSegment methodName, Type receiverType, LoggingContext loggingContext,
                                                                            IPropertyProvider<T> properties, out object? returnVal, object objectInstance, FunctionArguments args)
            where T : class, IProperty
        {
            returnVal = null;

            if (receiverType == typeof(IntrinsicFunctions))
            {
                if (methodName.Equals(nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
                {
                    string projectPath = properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                    Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                    if (args.TryGetArg(out string? arg0) && arg0 != null)
                    {
                        returnVal = IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Shortcut to avoid calling into binding if we recognize some most common constructors.
        /// Analogous to TryExecuteWellKnownFunction but guaranteed to not throw.
        /// </summary>
        /// <param name="receiverType"> Receiver type for the constructor. </param>
        /// <param name="returnVal">The instance as created by the constructor call.</param>
        /// <param name="args">Arguments.</param>
        /// <returns>True if the well known constructor call binding was successful.</returns>
        internal static bool TryExecuteWellKnownConstructorNoThrow(Type? receiverType, out object? returnVal, FunctionArguments args)
        {
            returnVal = null;

            if (receiverType == typeof(string))
            {
                if (args.Length == 0)
                {
                    returnVal = String.Empty;
                    return true;
                }
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    returnVal = arg0;
                    return true;
                }
            }
            return false;
        }
    }
}
