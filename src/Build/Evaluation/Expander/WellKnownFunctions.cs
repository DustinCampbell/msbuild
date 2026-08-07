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
#if !NET
using Microsoft.NET.StringTools;
#endif

namespace Microsoft.Build.Evaluation.Expander
{
    internal class WellKnownFunctions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogFunctionCall(Type receiverType, string methodName, string fileName, object? objectInstance, ref FunctionArguments args)
        {
            var logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            object[] materializedArguments = args.MaterializeAll();

            var argSignature = materializedArguments.Length != 0
                ? string.Join(", ", materializedArguments.Select(a => a?.GetType().Name ?? "null"))
                : string.Empty;

            File.AppendAllText(logFile, $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName}({argSignature})\n");
        }

        internal static bool TryExecutePathFunction(string methodName, out object? returnVal, ref FunctionArguments args)
        {
            returnVal = default;
            if (string.Equals(methodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
            {
                string? arg0, arg1, arg2, arg3;

                // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                switch (args.Count)
                {
                    case 0:
                        return false;
                    case 1:
                        if (args.TryGetString(0, out arg0) && arg0 != null)
                        {
                            returnVal = Path.Combine(arg0);
                            return true;
                        }
                        break;
                    case 2:
                        if (args.TryGetString(0, out arg0) && arg0 != null
                            && args.TryGetString(1, out arg1) && arg1 != null)
                        {
                            returnVal = Path.Combine(arg0, arg1);
                            return true;
                        }
                        break;
                    case 3:
                        if (args.TryGetString(0, out arg0) && arg0 != null
                            && args.TryGetString(1, out arg1) && arg1 != null
                            && args.TryGetString(2, out arg2) && arg2 != null)
                        {
                            returnVal = Path.Combine(arg0, arg1, arg2);
                            return true;
                        }
                        break;
                    case 4:
                        if (args.TryGetString(0, out arg0) && arg0 != null
                            && args.TryGetString(1, out arg1) && arg1 != null
                            && args.TryGetString(2, out arg2) && arg2 != null
                            && args.TryGetString(3, out arg3) && arg3 != null)
                        {
                            returnVal = Path.Combine(arg0, arg1, arg2, arg3);
                            return true;
                        }
                        break;
                    default:
                        if (args.TryGetStrings(out string[] values))
                        {
                            returnVal = Path.Combine(values);
                            return true;
                        }
                        break;
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
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                        ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                        : Path.GetFullPath(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
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
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = Path.GetFileName(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = Path.GetDirectoryName(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = Path.GetFileNameWithoutExtension(arg0);
                    return true;
                }
            }
            return false;
        }

        private static bool TryExecuteArithmeticOverload(
            ref FunctionArguments args,
            Func<long, long, long> integerOperation,
            Func<double, double, double> realOperation,
            out object? result)
        {
            if (args.Count == 2
                && args.TryGetInt64(0, out long integer0)
                && args.TryGetInt64(1, out long integer1))
            {
                result = integerOperation(integer0, integer1);
                return true;
            }

            if (args.Count == 2
                && args.TryGetDouble(0, out double real0)
                && args.TryGetDouble(1, out double real1))
            {
                result = realOperation(real0, real1);
                return true;
            }

            result = null;
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
        internal static bool TryExecuteStringFunction(string methodName, out object? returnVal, string text, ref FunctionArguments args)
        {
            if (args.Count == 0 && TryExecuteStringFunctionNoArguments(methodName, out returnVal, text))
            {
                return true;
            }

            returnVal = null;
            if (string.Equals(methodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = text.StartsWith(arg0, StringComparison.CurrentCulture);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0) && arg0 != null
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = text.Replace(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = text.Contains(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = text.EndsWith(arg0, StringComparison.CurrentCulture);
                    return true;
                }
                else if (args.Count == 2
                    && args.TryGetString(0, out arg0) && arg0 != null
                    && args.TryGetStringComparison(1, out StringComparison arg1))
                {
                    returnVal = text.EndsWith(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0) && arg0 != null
                    && args.TryGetStringComparison(1, out StringComparison arg1))
                {
                    returnVal = text.IndexOf(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = text.AsSpan().IndexOfAny(arg0.AsSpan());
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = text.LastIndexOf(arg0, StringComparison.CurrentCulture);
                    return true;
                }
                else if (args.Count == 2
                    && args.TryGetString(0, out arg0) && arg0 != null
                    && args.TryGetInt32(1, out int startIndex))
                {
                    returnVal = text.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture);
                    return true;
                }
                else if (args.Count == 2
                    && args.TryGetString(0, out arg0) && arg0 != null
                    && args.TryGetStringComparison(1, out StringComparison arg1))
                {
                    returnVal = text.LastIndexOf(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = text.AsSpan().LastIndexOfAny(arg0.AsSpan());
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetInt32(0, out int startIndex))
                {
                    returnVal = text.Substring(startIndex);
                    return true;
                }
                else if (args.Count == 2
                    && args.TryGetInt32(0, out startIndex)
                    && args.TryGetInt32(1, out int length))
                {
                    returnVal = text.Substring(startIndex, length);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Split), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetChar(0, out char separator))
                {
                    returnVal = text.Split(separator);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetInt32(0, out int totalWidth))
                {
                    returnVal = text.PadLeft(totalWidth);
                    return true;
                }
                else if (args.Count == 2
                    && args.TryGetInt32(0, out totalWidth)
                    && args.TryGetChar(1, out char paddingChar))
                {
                    returnVal = text.PadLeft(totalWidth, paddingChar);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetInt32(0, out int totalWidth))
                {
                    returnVal = text.PadRight(totalWidth);
                    return true;
                }
                else if (args.Count == 2
                    && args.TryGetInt32(0, out totalWidth)
                    && args.TryGetChar(1, out char paddingChar))
                {
                    returnVal = text.PadRight(totalWidth, paddingChar);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? trimChars) && trimChars?.Length > 0)
                {
                    returnVal = text.TrimStart(trimChars.ToCharArray());
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? trimChars) && trimChars?.Length > 0)
                {
                    returnVal = text.TrimEnd(trimChars.ToCharArray());
                    return true;
                }
            }
            else if (string.Equals(methodName, "get_Chars", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetInt32(0, out int index))
                {
                    returnVal = text[index];
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = text.Equals(arg0);
                    return true;
                }
            }
            return false;
        }

        internal static bool TryExecuteStringFunctionNoArguments(
            string methodName,
            out object? returnVal,
            string text)
            => TryExecuteStringFunctionNoArguments((StringSegment)methodName, out returnVal, text);

        internal static bool TryExecuteStringFunctionNoArguments(
            StringSegment methodName,
            out object? returnVal,
            string text)
        {
            if (methodName.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
            {
                returnVal = text.ToUpperInvariant();
                return true;
            }

            if (methodName.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
            {
                returnVal = text.ToLowerInvariant();
                return true;
            }

            if (methodName.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
            {
                returnVal = text.ToLower();
                return true;
            }

            if (methodName.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase))
            {
                returnVal = text.Length;
                return true;
            }

            returnVal = null;
            return false;
        }

        internal static bool TryExecuteIntrinsicFunction(string methodName, out object? returnVal, IFileSystem fileSystem, ref FunctionArguments args)
        {
            returnVal = default;
            if (string.Equals(methodName, nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetUnescapedText(0, out StringSegment conditionValue)
                    && args.TryGetUnescapedText(1, out StringSegment defaultValue))
                {
                    returnVal = (conditionValue.IsEmpty ? defaultValue : conditionValue).Value;
                    return true;
                }

                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetStrings(out string[] values))
                {
                    returnVal = IntrinsicFunctions.NormalizePath(values);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, fileSystem);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count >= 4
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    object[] values = args.MaterializeAll();
                    returnVal = IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, values[2], new ArraySegment<object>(values, 3, values.Length - 3));
                    return true;
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
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.Escape(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.Unescape(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, fileSystem);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
            {
                if (TryExecuteArithmeticOverload(ref args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out returnVal))
                {
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
            {
                if (TryExecuteArithmeticOverload(ref args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out returnVal))
                {
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
            {
                if (TryExecuteArithmeticOverload(ref args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out returnVal))
                {
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
            {
                if (TryExecuteArithmeticOverload(ref args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out returnVal))
                {
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
            {
                if (TryExecuteArithmeticOverload(ref args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out returnVal))
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
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionEquals(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg0);
                    return true;
                }
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg1)
                    && args.TryGetInt32(1, out int arg2))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg0)
                    && args.TryGetString(1, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg0);
                    return true;
                }
                if (args.Count == 2
                    && args.TryGetString(0, out string? arg1)
                    && args.TryGetInt32(1, out int arg2))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.ConvertToBase64(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.ConvertFromBase64(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.StableStringHash(arg0);
                    return true;
                }
                else if (args.Count == 2
                    && args.TryGetString(0, out string? arg1) && arg1 != null
                    && args.TryGetString(1, out string? arg2) && arg2 != null
                    && Enum.TryParse(arg2, true, out IntrinsicFunctions.StringHashingAlgorithm hashAlgorithm))
                {
                    returnVal = IntrinsicFunctions.StableStringHash(arg1, hashAlgorithm);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetVersion(0, out Version? arg0) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 3
                    && args.TryGetString(0, out string? arg0) && arg0 != null
                    && args.TryGetInt32(1, out int arg1)
                    && args.TryGetInt32(2, out int arg2))
                {
                    returnVal = IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.CheckFeatureAvailability(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2 && args.TryGetInt32(0, out int arg0) && args.TryGetInt32(1, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseOr(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2 && args.TryGetInt32(0, out int arg0) && args.TryGetInt32(1, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseAnd(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2 && args.TryGetInt32(0, out int arg0) && args.TryGetInt32(1, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseXor(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetInt32(0, out int arg0))
                {
                    returnVal = IntrinsicFunctions.BitwiseNot(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2 && args.TryGetInt32(0, out int arg0) && args.TryGetInt32(1, out int arg1))
                {
                    returnVal = IntrinsicFunctions.LeftShift(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2 && args.TryGetInt32(0, out int arg0) && args.TryGetInt32(1, out int arg1))
                {
                    returnVal = IntrinsicFunctions.RightShift(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 2 && args.TryGetInt32(0, out int arg0) && args.TryGetInt32(1, out int arg1))
                {
                    returnVal = IntrinsicFunctions.RightShiftUnsigned(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.NormalizeDirectory(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = IntrinsicFunctions.IsOSPlatform(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.FileExists(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0))
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
        internal static bool TryExecuteWellKnownFunction(string methodName, Type receiverType, IFileSystem fileSystem, out object? returnVal, object? objectInstance, ref FunctionArguments args)
        {
            returnVal = null;

            if (objectInstance is string text)
            {
                return TryExecuteStringFunction(methodName, out returnVal, text, ref args);
            }
            else if (objectInstance is string[] stringArray)
            {
                if (string.Equals(methodName, "GetValue", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Count == 1 && args.TryGetInt32(0, out int index))
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
                        if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                        {
                            returnVal = string.IsNullOrWhiteSpace(arg0);
                            return true;
                        }
                    }
                    else if (string.Equals(methodName, nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                        {
                            returnVal = string.IsNullOrEmpty(arg0);
                            return true;
                        }
                    }
                    else if (string.Equals(methodName, nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Count == 1 && args.TryGetString(0, out string? arg0))
                        {
                            returnVal = arg0;
                            return true;
                        }
                    }
                    else if (string.Equals(methodName, nameof(string.Concat), StringComparison.OrdinalIgnoreCase)
                        && args.Count == 2)
                    {
                        if (args.TryGetUnescapedText(0, out StringSegment text0)
                            && args.TryGetUnescapedText(1, out StringSegment text1))
                        {
#if NET
                            returnVal = string.Concat(text0.AsSpan(), text1.AsSpan());
#else
                            using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();
                            builder.Append(text0.Buffer!, text0.Offset, text0.Length);
                            builder.Append(text1.Buffer!, text1.Offset, text1.Length);
                            returnVal = builder.ToString();
#endif
                            return true;
                        }

                        if (args.TryGetString(0, out string? arg0)
                            && args.TryGetString(1, out string? arg1))
                        {
                            returnVal = string.Concat(arg0, arg1);
                            return true;
                        }
                    }
                }
                else if (receiverType == typeof(Math))
                {
                    if (string.Equals(methodName, nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Count == 2
                            && args.TryGetDouble(0, out double arg0)
                            && args.TryGetDouble(1, out double arg1))
                        {
                            returnVal = Math.Max(arg0, arg1);
                            return true;
                        }
                    }
                    else if (string.Equals(methodName, nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Count == 2
                            && args.TryGetDouble(0, out double arg0)
                            && args.TryGetDouble(1, out double arg1))
                        {
                            returnVal = Math.Min(arg0, arg1);
                            return true;
                        }
                    }
                }
                else if (receiverType == typeof(IntrinsicFunctions))
                {
                    return TryExecuteIntrinsicFunction(methodName, out returnVal, fileSystem, ref args);
                }
                else if (receiverType == typeof(Path))
                {
                    return TryExecutePathFunction(methodName, out returnVal, ref args);
                }
                else if (receiverType == typeof(Version))
                {
                    if (string.Equals(methodName, nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
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
                        bool? result = null;

                        if (args.Count == 1 && args.TryGetChar(0, out char character))
                        {
                            result = char.IsDigit(character);
                        }
                        else if (args.Count == 2
                            && args.TryGetString(0, out string? str) && str != null
                            && args.TryGetInt32(1, out int index))
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
                    if (string.Equals(methodName, nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase)
                        && args.Count == 3)
                    {
                        if (args.TryGetString(0, out string? arg1) && arg1 != null
                            && args.TryGetString(1, out string? arg2) && arg2 != null
                            && args.TryGetString(2, out string? arg3) && arg3 != null)
                        {
                            returnVal = Regex.Replace(arg1, arg2, arg3);
                            return true;
                        }
                    }
                }
            }
            else if (string.Equals(methodName, nameof(Version.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is Version v)
            {
                if (args.Count == 1 && args.TryGetInt32(0, out int arg0))
                {
                    returnVal = v.ToString(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(Int32.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is int i)
            {
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = i.ToString(arg0);
                    return true;
                }
            }
            if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
            {
                LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", objectInstance, ref args);
            }

            return false;
        }

        internal static bool TryExecuteWellKnownFunctionWithPropertiesParam<T>(string methodName, Type receiverType, LoggingContext loggingContext,
                                                                            IPropertyProvider<T> properties, out object? returnVal, object? objectInstance, ref FunctionArguments args)
            where T : class, IProperty
        {
            returnVal = null;

            if (receiverType == typeof(IntrinsicFunctions))
            {
                if (string.Equals(methodName, nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
                {
                    string projectPath = properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                    Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                    if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
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
        internal static bool TryExecuteWellKnownConstructorNoThrow(Type? receiverType, out object? returnVal, ref FunctionArguments args)
        {
            returnVal = null;

            if (receiverType == typeof(string))
            {
                if (args.Count == 0)
                {
                    returnVal = String.Empty;
                    return true;
                }
                if (args.Count == 1 && args.TryGetString(0, out string? arg0) && arg0 != null)
                {
                    returnVal = arg0;
                    return true;
                }
            }
            return false;
        }
    }
}
