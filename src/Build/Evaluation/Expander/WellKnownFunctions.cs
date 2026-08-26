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
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander
{
    internal class WellKnownFunctions
    {
        private static bool ElementsOfType(FunctionArguments args, Type type)
        {
            for (int i = 0; i < args.Length; i++)
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
            string logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            string argSignature = string.Join(", ", args.ToObjectArray().Select(a => a?.GetType().Name ?? "null"));

            File.AppendAllText(logFile, $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName.ValueOrEmpty}({argSignature})\n");
        }

        internal static bool TryExecutePathFunction(StringSegment methodName, out object? result, FunctionArguments args)
        {
            if (methodName.Equals(nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
            {
                string? arg0, arg1, arg2;

                // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                switch (args.Length)
                {
                    case 0:
                        result = null;
                        return false;

                    case 1:
                        if (args.TryGetArg(out arg0) && arg0 != null)
                        {
                            result = Path.Combine(arg0);
                            return true;
                        }

                        break;

                    case 2:
                        if (args.TryGetArgs(out arg0, out arg1) && arg0 != null && arg1 != null)
                        {
                            result = Path.Combine(arg0, arg1);
                            return true;
                        }

                        break;

                    case 3:
                        if (args.TryGetArgs(out arg0, out arg1, out arg2) && arg0 != null && arg1 != null && arg2 != null)
                        {
                            result = Path.Combine(arg0, arg1, arg2);
                            return true;
                        }

                        break;

                    case 4:
                        if (args.TryGetArgs(out arg0, out arg1, out arg2, out string? arg3) && arg0 != null && arg1 != null && arg2 != null && arg3 != null)
                        {
                            result = Path.Combine(arg0, arg1, arg2, arg3);
                            return true;
                        }

                        break;

                    default:
                        if (ElementsOfType(args, typeof(string)))
                        {
                            result = Path.Combine(Array.ConvertAll(args.ToObjectArray(), o => (string)o!));
                            return true;
                        }

                        break;
                }
            }

            if (methodName.Equals(nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = Path.DirectorySeparatorChar;
                    return true;
                }
            }

            if (methodName.Equals(nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                        ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                        : Path.GetFullPath(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = Path.IsPathRooted(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = Path.GetTempPath();
                    return true;
                }
            }

            if (methodName.Equals(nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = Path.GetFileName(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = Path.GetDirectoryName(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = Path.GetFileNameWithoutExtension(arg0);
                    return true;
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Handler for executing well known string functions
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="text"></param>
        /// <param name="args"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal static bool TryExecuteStringFunction(StringSegment methodName, string text, FunctionArguments args, out object? result)
        {
            StringSegment receiver = text;
            if (methodName.Equals(nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    result = receiver.StartsWith(arg0, StringComparison.CurrentCulture);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1) && arg0 != null)
                {
                    result = text.Replace(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    result = receiver.Contains(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = text.ToUpperInvariant();
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = text.ToLowerInvariant();
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    result = receiver.EndsWith(arg0, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out arg0, out StringComparison arg1))
                {
                    result = receiver.EndsWith(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = text.ToLower();
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out StringSegment arg0, out StringComparison arg1))
                {
                    result = receiver.IndexOf(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    result = receiver.IndexOfAny(arg0.AsSpan());
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    result = receiver.LastIndexOf(arg0, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out arg0, out int startIndex))
                {
                    result = receiver.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out arg0, out StringComparison arg1))
                {
                    result = receiver.LastIndexOf(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    result = receiver.LastIndexOfAny(arg0.AsSpan());
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = text.Length;
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int startIndex))
                {
                    result = text.Substring(startIndex);
                    return true;
                }

                if (args.TryGetArgs(out startIndex, out int length))
                {
                    result = text.Substring(startIndex, length);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.Split), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment separator) && separator.Length == 1)
                {
                    result = text.Split(separator[0]);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int totalWidth))
                {
                    result = text.PadLeft(totalWidth);
                    return true;
                }

                if (args.TryGetArgs(out totalWidth, out StringSegment paddingChar) && paddingChar.Length == 1)
                {
                    result = text.PadLeft(totalWidth, paddingChar[0]);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int totalWidth))
                {
                    result = text.PadRight(totalWidth);
                    return true;
                }

                if (args.TryGetArgs(out totalWidth, out StringSegment paddingChar) && paddingChar.Length == 1)
                {
                    result = text.PadRight(totalWidth, paddingChar[0]);
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment trimChars) && trimChars.Length > 0)
                {
                    result = text.TrimStart(trimChars.AsSpan().ToArray());
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment trimChars) && trimChars.Length > 0)
                {
                    result = text.TrimEnd(trimChars.AsSpan().ToArray());
                    return true;
                }
            }

            if (methodName.Equals("get_Chars", StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int index))
                {
                    result = text[index];
                    return true;
                }
            }

            if (methodName.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment arg0))
                {
                    result = receiver.Equals(arg0);
                    return true;
                }
            }

            result = null;
            return false;
        }

        internal static bool TryExecuteIntrinsicFunction<T>(
            StringSegment methodName,
            FunctionArguments args,
            in PropertyFunctionExecutionContext<T> context,
            out object? result)
            where T : class, IProperty
        {
            if (methodName.Equals(nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
            {
                if (ElementsOfType(args, typeof(string)))
                {
                    result = IntrinsicFunctions.NormalizePath(Array.ConvertAll(args.ToObjectArray(), o => (string)o!));
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, context.FileSystem);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length >= 4 &&
                    args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    object?[] values = args.ToObjectArray();
                    result = IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, values[2], new ArraySegment<object?>(values, 3, values.Length - 3));
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = IntrinsicFunctions.IsRunningFromVisualStudio();
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.Escape(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.Unescape(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    // The one-argument form starts from the directory containing the invoking project file.
                    result = IntrinsicFunctions.GetPathOfFileAbove(arg0, context.StartingDirectory, context.FileSystem);
                    return true;
                }

                if (args.TryGetArgs(out arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, context.FileSystem);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
            {
                string projectPath = context.Properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                LoggingContext loggingContext = context.LoggingContext;
                Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Add, IntrinsicFunctions.Add, out result))
                {
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out result))
                {
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out result))
                {
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out result))
                {
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryExecuteArithmeticOverload(IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out result))
                {
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = IntrinsicFunctions.GetCurrentToolsDirectory();
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = IntrinsicFunctions.GetToolsDirectory32();
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = IntrinsicFunctions.GetToolsDirectory64();
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = IntrinsicFunctions.GetMSBuildSDKsPath();
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = IntrinsicFunctions.GetVsInstallRoot();
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = IntrinsicFunctions.GetMSBuildExtensionsPath();
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    result = IntrinsicFunctions.GetProgramFiles32();
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.VersionEquals(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.GetTargetFrameworkVersion(arg0);
                    return true;
                }

                if (args.TryGetArgs(out string? arg1, out int arg2))
                {
                    result = IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out string? arg1))
                {
                    result = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.GetTargetPlatformVersion(arg0);
                    return true;
                }

                if (args.TryGetArgs(out string? arg1, out int arg2))
                {
                    result = IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.ConvertToBase64(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.ConvertFromBase64(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.StableStringHash(arg0);
                    return true;
                }

                if (args.TryGetArgs(out arg0, out string? arg1) && Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg1, true, out var hashAlgorithm) && arg0 != null && arg1 != null)
                {
                    result = IntrinsicFunctions.StableStringHash(arg0, hashAlgorithm);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out Version? arg0) && arg0 != null)
                {
                    result = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out string? arg0, out int arg1, out int arg2) && arg0 != null)
                {
                    result = IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = IntrinsicFunctions.CheckFeatureAvailability(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    result = IntrinsicFunctions.BitwiseOr(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    result = IntrinsicFunctions.BitwiseAnd(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    result = IntrinsicFunctions.BitwiseXor(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out int arg0))
                {
                    result = IntrinsicFunctions.BitwiseNot(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    result = IntrinsicFunctions.LeftShift(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    result = IntrinsicFunctions.RightShift(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArgs(out int arg0, out int arg1))
                {
                    result = IntrinsicFunctions.RightShiftUnsigned(arg0, arg1);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = IntrinsicFunctions.NormalizeDirectory(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = IntrinsicFunctions.IsOSPlatform(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.FileExists(arg0);
                    return true;
                }
            }

            if (methodName.Equals(nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out string? arg0))
                {
                    result = IntrinsicFunctions.DirectoryExists(arg0);
                    return true;
                }
            }

            result = null;
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
        /// <param name="context">Context for executing functions that depend on evaluation state.</param>
        /// <param name="result">The value returned from the function call.</param>
        /// <returns>True if the well known function call binding was successful.</returns>
        internal static bool TryExecuteWellKnownFunction<T>(
            StringSegment methodName,
            Type receiverType,
            object? objectInstance,
            FunctionArguments args,
            in PropertyFunctionExecutionContext<T> context,
            out object? result)
            where T : class, IProperty
        {
            if (objectInstance is string text)
            {
                return TryExecuteStringFunction(methodName, text, args, out result);
            }

            if (objectInstance is string[] stringArray)
            {
                if (methodName.Equals("GetValue", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.TryGetArg(out int index))
                    {
                        result = stringArray[index];
                        return true;
                    }
                }
            }

            if (objectInstance == null) // Calling a well-known static function
            {
                if (receiverType == typeof(string))
                {
                    if (methodName.Equals(nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArg(out StringSegment arg0))
                        {
                            result = arg0.IsNullOrWhiteSpace();
                            return true;
                        }
                    }

                    if (methodName.Equals(nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArg(out StringSegment arg0))
                        {
                            result = arg0.IsNullOrEmpty;
                            return true;
                        }
                    }

                    if (methodName.Equals(nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArg(out string? arg0))
                        {
                            result = arg0;
                            return true;
                        }
                    }
                }

                if (receiverType == typeof(Math))
                {
                    if (methodName.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArgs(out double arg0, out double arg1))
                        {
                            result = Math.Max(arg0, arg1);
                            return true;
                        }
                    }

                    if (methodName.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArgs(out double arg0, out double arg1))
                        {
                            result = Math.Min(arg0, arg1);
                            return true;
                        }
                    }
                }

                if (receiverType == typeof(IntrinsicFunctions))
                {
                    return TryExecuteIntrinsicFunction(methodName, args, in context, out result);
                }

                if (receiverType == typeof(Path))
                {
                    return TryExecutePathFunction(methodName, out result, args);
                }

                if (receiverType == typeof(Version))
                {
                    if (methodName.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArg(out string? arg0) && arg0 != null)
                        {
                            result = Version.Parse(arg0);
                            return true;
                        }
                    }
                }

                if (receiverType == typeof(Guid))
                {
                    if (methodName.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            result = Guid.NewGuid();
                            return true;
                        }
                    }
                }

                if (receiverType == typeof(char))
                {
                    if (methodName.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.TryGetArg(out StringSegment segmentArg0) && segmentArg0.Length == 1)
                        {
                            result = char.IsDigit(segmentArg0[0]);
                            return true;
                        }

                        if (args.TryGetArgs(out string? stringArg0, out int index) && stringArg0 != null)
                        {
                            result = char.IsDigit(stringArg0, index);
                            return true;
                        }
                    }
                }

                if (receiverType == typeof(Regex))
                {
                    if (methodName.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) && args.Length == 3)
                    {
                        if (args.TryGetArgs(out string? arg1, out string? arg2, out string? arg3) && arg1 != null && arg2 != null && arg3 != null)
                        {
                            result = Regex.Replace(arg1, arg2, arg3);
                            return true;
                        }
                    }
                }
            }

            if (methodName.Equals(nameof(ToString), StringComparison.OrdinalIgnoreCase))
            {
                if (objectInstance is Version v)
                {
                    if (args.TryGetArg(out int arg0))
                    {
                        result = v.ToString(arg0);
                        return true;
                    }
                }

                if (objectInstance is int i)
                {
                    if (args.TryGetArg(out string? arg0) && arg0 != null)
                    {
                        result = i.ToString(arg0);
                        return true;
                    }
                }
            }

            if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
            {
                LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", objectInstance, args);
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Shortcut to avoid calling into binding if we recognize some most common constructors.
        /// Analogous to TryExecuteWellKnownFunction but guaranteed to not throw.
        /// </summary>
        /// <param name="receiverType"> Receiver type for the constructor. </param>
        /// <param name="args">Arguments.</param>
        /// <param name="result">The instance as created by the constructor call.</param>
        /// <returns>True if the well known constructor call binding was successful.</returns>
        internal static bool TryExecuteWellKnownConstructorNoThrow(Type? receiverType, FunctionArguments args, out object? result)
        {
            if (receiverType == typeof(string))
            {
                if (args.Length == 0)
                {
                    result = string.Empty;
                    return true;
                }

                if (args.TryGetArg(out string? arg0) && arg0 != null)
                {
                    result = arg0;
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}
