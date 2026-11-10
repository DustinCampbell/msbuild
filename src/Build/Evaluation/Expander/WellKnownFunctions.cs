// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander
{
    internal class WellKnownFunctions
    {
        private static InvokerResult Handled(object? value) => InvokerResult.Handled(value);

        private static InvokerResult Unhandled => InvokerResult.Unhandled;

        internal static bool ElementsOfType(object?[] args, Type type)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i]!.GetType() != type)
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogFunctionCall(Type receiverType, string methodName, string fileName, object? objectInstance, object?[] args)
        {
            var logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            var argSignature = args != null
                ? string.Join(", ", args.Select(a => a?.GetType().Name ?? "null"))
                : string.Empty;

            File.AppendAllText(logFile, $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName}({argSignature})\n");
        }

        internal static InvokerResult TryExecutePathFunction(string methodName, object?[] args)
        {
            if (string.Equals(methodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
            {
                // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                switch (args.Length)
                {
                    case 0:
                        return Unhandled;

                    case 1:
                        if (ParseArgs.TryGetArg(args, out string? arg0))
                        {
                            return Handled(Path.Combine(arg0));
                        }

                        break;

                    case 2:
                        if (ParseArgs.TryGetArgs(args, out arg0, out string? arg1))
                        {
                            return Handled(Path.Combine(arg0, arg1));
                        }

                        break;

                    case 3:
                        if (ParseArgs.TryGetArgs(args, out arg0, out arg1, out string? arg2))
                        {
                            return Handled(Path.Combine(arg0, arg1, arg2));
                        }

                        break;

                    case 4:
                        if (ParseArgs.TryGetArgs(args, out arg0, out arg1, out arg2, out string? arg3))
                        {
                            return Handled(Path.Combine(arg0, arg1, arg2, arg3));
                        }

                        break;

                    default:
                        if (ElementsOfType(args, typeof(string)))
                        {
                            return Handled(Path.Combine(Array.ConvertAll(args, o => (string)o!)));
                        }

                        break;
                }
            }
            else if (string.Equals(methodName, nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(Path.DirectorySeparatorChar);
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(!string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                        ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                        : Path.GetFullPath(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(Path.IsPathRooted(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(Path.GetTempPath());
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(Path.GetFileName(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(Path.GetDirectoryName(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(Path.GetFileNameWithoutExtension(arg0));
                }
            }

            return Unhandled;
        }

        /// <summary>
        ///  Executes recognized string instance members without reflection binding.
        /// </summary>
        /// <param name="text">The receiver instance.</param>
        /// <param name="methodName">The name of the member.</param>
        /// <param name="args">The prepared function arguments.</param>
        /// <returns>
        ///  The handled member's result, or an unhandled result when the member is not recognized.
        /// </returns>
        internal static InvokerResult TryExecuteStringFunction(string text, string methodName, object?[] args)
        {
            if (string.Equals(methodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(text.StartsWith(arg0, StringComparison.CurrentCulture));
                }
            }
            else if (string.Equals(methodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(text.Replace(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(text.Contains(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(text.ToUpperInvariant());
                }
            }
            else if (string.Equals(methodName, nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(text.ToLowerInvariant());
                }
            }
            else if (string.Equals(methodName, nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(text.EndsWith(arg0, StringComparison.CurrentCulture));
                }
                else if (ParseArgs.TryGetArgs(args, out arg0, out StringComparison arg1))
                {
                    return Handled(text.EndsWith(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(text.ToLower());
                }
            }
            else if (string.Equals(methodName, nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out StringComparison arg1))
                {
                    return Handled(text.IndexOf(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(text.AsSpan().IndexOfAny(arg0.AsSpan()));
                }
            }
            else if (string.Equals(methodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(text.LastIndexOf(arg0, StringComparison.CurrentCulture));
                }
                else if (ParseArgs.TryGetArgs(args, out arg0, out int startIndex))
                {
                    return Handled(text.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture));
                }
                else if (ParseArgs.TryGetArgs(args, out arg0, out StringComparison arg1))
                {
                    return Handled(text.LastIndexOf(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(text.AsSpan().LastIndexOfAny(arg0.AsSpan()));
                }
            }
            else if (string.Equals(methodName, nameof(string.Length), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(text.Length);
                }
            }
            else if (string.Equals(methodName, nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int startIndex))
                {
                    return Handled(text.Substring(startIndex));
                }
                else if (ParseArgs.TryGetArgs(args, out startIndex, out int length))
                {
                    return Handled(text.Substring(startIndex, length));
                }
            }
            else if (string.Equals(methodName, nameof(string.Split), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out char separator))
                {
                    return Handled(text.Split(separator));
                }
            }
            else if (string.Equals(methodName, nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int totalWidth))
                {
                    return Handled(text.PadLeft(totalWidth));
                }
                else if (ParseArgs.TryGetArgs(args, out totalWidth, out char paddingChar))
                {
                    return Handled(text.PadLeft(totalWidth, paddingChar));
                }
            }
            else if (string.Equals(methodName, nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int totalWidth))
                {
                    return Handled(text.PadRight(totalWidth));
                }
                else if (ParseArgs.TryGetArgs(args, out totalWidth, out char paddingChar))
                {
                    return Handled(text.PadRight(totalWidth, paddingChar));
                }
            }
            else if (string.Equals(methodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? trimChars) && trimChars.Length > 0)
                {
                    return Handled(text.TrimStart(trimChars.ToCharArray()));
                }
            }
            else if (string.Equals(methodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? trimChars) && trimChars.Length > 0)
                {
                    return Handled(text.TrimEnd(trimChars.ToCharArray()));
                }
            }
            else if (string.Equals(methodName, "get_Chars", StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int index))
                {
                    return Handled(text[index]);
                }
            }
            else if (string.Equals(methodName, nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(text.Equals(arg0));
                }
            }

            return Unhandled;
        }

        internal static InvokerResult TryExecuteIntrinsicFunction(
            string methodName,
            object?[] args,
            ref readonly ExpansionContext context)
        {
            if (string.Equals(methodName, nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.EnsureTrailingSlash(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.ValueOrDefault(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
            {
                if (ElementsOfType(args, typeof(string)))
                {
                    return Handled(IntrinsicFunctions.NormalizePath(Array.ConvertAll(args, o => (string)o!)));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, context.FileSystem));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length >= 4 &&
                    ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], new ArraySegment<object?>(args, 3, args.Length - 3)));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(IntrinsicFunctions.IsRunningFromVisualStudio());
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.Escape(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.Unescape(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                // Preserve the case-sensitive implicit-directory form.
                if (methodName.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.Ordinal) &&
                    ParseArgs.TryGetArg(args, out string? file))
                {
                    string? startingDirectory = context.StartingDirectory;
                    if (startingDirectory is not null)
                    {
                        return Handled(IntrinsicFunctions.GetPathOfFileAbove(file, startingDirectory, context.FileSystem));
                    }
                }

                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, context.FileSystem));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out object? returnVal))
                {
                    return Handled(returnVal);
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out object? returnVal))
                {
                    return Handled(returnVal);
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out object? returnVal))
                {
                    return Handled(returnVal);
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out object? returnVal))
                {
                    return Handled(returnVal);
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out object? returnVal))
                {
                    return Handled(returnVal);
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(IntrinsicFunctions.GetCurrentToolsDirectory());
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(IntrinsicFunctions.GetToolsDirectory32());
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(IntrinsicFunctions.GetToolsDirectory64());
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(IntrinsicFunctions.GetMSBuildSDKsPath());
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(IntrinsicFunctions.GetVsInstallRoot());
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(IntrinsicFunctions.GetMSBuildExtensionsPath());
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 0)
                {
                    return Handled(IntrinsicFunctions.GetProgramFiles32());
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.VersionEquals(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.VersionNotEquals(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.VersionGreaterThan(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.VersionLessThan(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.GetTargetFrameworkVersion(arg0));
                }

                if (ParseArgs.TryGetArgs(args, out string? arg1, out int arg2))
                {
                    return Handled(IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    return Handled(IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.GetTargetPlatformIdentifier(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.GetTargetPlatformVersion(arg0));
                }

                if (ParseArgs.TryGetArgs(args, out string? arg1, out int arg2))
                {
                    return Handled(IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.ConvertToBase64(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.ConvertFromBase64(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.StableStringHash(arg0));
                }
                else if (ParseArgs.TryGetArgs(args, out string? arg1, out string? arg2) &&
                    Enum.TryParse(arg2, ignoreCase: true, out IntrinsicFunctions.StringHashingAlgorithm hashAlgorithm))
                {
                    return Handled(IntrinsicFunctions.StableStringHash(arg1, hashAlgorithm));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out Version? arg0))
                {
                    return Handled(IntrinsicFunctions.AreFeaturesEnabled(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out int arg1, out int arg2))
                {
                    return Handled(IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.CheckFeatureAvailability(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    return Handled(IntrinsicFunctions.BitwiseOr(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    return Handled(IntrinsicFunctions.BitwiseAnd(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    return Handled(IntrinsicFunctions.BitwiseXor(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int arg0))
                {
                    return Handled(IntrinsicFunctions.BitwiseNot(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    return Handled(IntrinsicFunctions.LeftShift(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    return Handled(IntrinsicFunctions.RightShift(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    return Handled(IntrinsicFunctions.RightShiftUnsigned(arg0, arg1));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.NormalizeDirectory(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.IsOSPlatform(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.FileExists(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.DirectoryExists(arg0));
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
            {
                string projectPath = context.Properties!.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                var loggingContext = context.LoggingContext;
                Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext));
                }
            }

            return Unhandled;
        }

        /// <summary>
        ///  Executes recognized instance members without reflection binding.
        /// </summary>
        /// <param name="objectInstance">The receiver instance.</param>
        /// <param name="methodName">The name of the member.</param>
        /// <param name="args">The prepared function arguments.</param>
        /// <returns>
        ///  The handled member's result, or an unhandled result when the member is not recognized.
        /// </returns>
        /// <remarks>
        ///  Avoids the cost of reflection binding and first-chance missing-method exceptions.
        ///  See https://github.com/dotnet/msbuild/issues/2217.
        /// </remarks>
        public static InvokerResult TryInvokeInstanceMember(
            object objectInstance,
            string methodName,
            object?[] args)
        {
            if (objectInstance is string text)
            {
                return TryExecuteStringFunction(text, methodName, args);
            }
            else if (objectInstance is string[] stringArray)
            {
                if (string.Equals(methodName, "GetValue", StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out int index))
                    {
                        return Handled(stringArray[index]);
                    }
                }
            }
            else if (objectInstance is Version v && string.Equals(methodName, nameof(Version.ToString), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int arg0))
                {
                    return Handled(v.ToString(arg0));
                }
            }
            else if (objectInstance is int i && string.Equals(methodName, nameof(Int32.ToString), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(i.ToString(arg0));
                }
            }

            if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
            {
                LogFunctionCall(objectInstance.GetType(), methodName, "PropertyFunctionsRequiringReflection", objectInstance, args);
            }

            return Unhandled;
        }

        /// <summary>
        ///  Executes recognized static members without reflection binding.
        /// </summary>
        /// <param name="receiverType">The type on which to look up a static member.</param>
        /// <param name="methodName">The name of the member.</param>
        /// <param name="args">The prepared function arguments.</param>
        /// <param name="context">The properties, options, and services used during expansion.</param>
        /// <returns>
        ///  The handled member's result, or an unhandled result when the member is not recognized.
        /// </returns>
        public static InvokerResult TryInvokeStaticMember(
            Type receiverType,
            string methodName,
            object?[] args,
            ref readonly ExpansionContext context)
        {
            if (receiverType == typeof(string))
            {
                if (string.Equals(methodName, nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0))
                    {
                        return Handled(string.IsNullOrWhiteSpace(arg0));
                    }
                }
                else if (string.Equals(methodName, nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0))
                    {
                        return Handled(string.IsNullOrEmpty(arg0));
                    }
                }
                else if (string.Equals(methodName, nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0))
                    {
                        return Handled(arg0);
                    }
                }
            }
            else if (receiverType == typeof(Math))
            {
                if (string.Equals(methodName, nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArgs(args, out double arg0, out double arg1))
                    {
                        return Handled(Math.Max(arg0, arg1));
                    }
                }
                else if (string.Equals(methodName, nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArgs(args, out double arg0, out double arg1))
                    {
                        return Handled(Math.Min(arg0, arg1));
                    }
                }
            }
            else if (receiverType == typeof(IntrinsicFunctions))
            {
                return TryExecuteIntrinsicFunction(methodName, args, in context);
            }
            else if (receiverType == typeof(Path))
            {
                return TryExecutePathFunction(methodName, args);
            }
            else if (receiverType == typeof(Version))
            {
                if (string.Equals(methodName, nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0))
                    {
                        return Handled(Version.Parse(arg0));
                    }
                }
            }
            else if (receiverType == typeof(Guid))
            {
                if (string.Equals(methodName, nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length == 0)
                    {
                        return Handled(Guid.NewGuid());
                    }
                }
            }
            else if (receiverType == typeof(char))
            {
                if (string.Equals(methodName, nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out char c))
                    {
                        return Handled(char.IsDigit(c));
                    }

                    if (ParseArgs.TryGetArgs(args, out string? str, out int index))
                    {
                        return Handled(char.IsDigit(str, index));
                    }
                }
            }
            else if (receiverType == typeof(Regex))
            {
                if (string.Equals(methodName, nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) && args.Length == 3)
                {
                    if (ParseArgs.TryGetArgs(args, out string? arg1, out string? arg2, out string? arg3))
                    {
                        return Handled(Regex.Replace(arg1, arg2, arg3));
                    }
                }
            }

            if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
            {
                LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", null, args);
            }

            return Unhandled;
        }

        /// <summary>
        ///  Executes recognized constructors without reflection binding.
        /// </summary>
        /// <param name="receiverType">The receiver type for the constructor.</param>
        /// <param name="args">The prepared constructor arguments.</param>
        /// <returns>
        ///  The handled constructor's result, or an unhandled result when the constructor is not recognized.
        /// </returns>
        public static InvokerResult TryInvokeConstructor(Type? receiverType, object?[] args)
        {
            if (receiverType == typeof(string))
            {
                if (args.Length == 0)
                {
                    return Handled(string.Empty);
                }

                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    return Handled(arg0);
                }
            }

            return Unhandled;
        }
    }
}
