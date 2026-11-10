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

        internal static bool TryExecutePathFunction(string methodName, object?[] args, out object? returnVal)
        {
            if (string.Equals(methodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
            {
                // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                switch (args.Length)
                {
                    case 0:
                        returnVal = null;
                        return false;

                    case 1:
                        if (ParseArgs.TryGetArg(args, out string? arg0))
                        {
                            returnVal = Path.Combine(arg0);
                            return true;
                        }

                        break;

                    case 2:
                        if (ParseArgs.TryGetArgs(args, out arg0, out string? arg1))
                        {
                            returnVal = Path.Combine(arg0, arg1);
                            return true;
                        }

                        break;

                    case 3:
                        if (ParseArgs.TryGetArgs(args, out arg0, out arg1, out string? arg2))
                        {
                            returnVal = Path.Combine(arg0, arg1, arg2);
                            return true;
                        }

                        break;

                    case 4:
                        if (ParseArgs.TryGetArgs(args, out arg0, out arg1, out arg2, out string? arg3))
                        {
                            returnVal = Path.Combine(arg0, arg1, arg2, arg3);
                            return true;
                        }

                        break;

                    default:
                        if (ElementsOfType(args, typeof(string)))
                        {
                            returnVal = Path.Combine(Array.ConvertAll(args, o => (string)o!));
                            return true;
                        }

                        break;
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
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                        ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                        : Path.GetFullPath(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
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
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = Path.GetFileName(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = Path.GetDirectoryName(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = Path.GetFileNameWithoutExtension(arg0);
                    return true;
                }
            }

            returnVal = null;
            return false;
        }

        /// <summary>
        ///  Executes recognized string instance members without reflection binding.
        /// </summary>
        /// <param name="text">The receiver instance.</param>
        /// <param name="methodName">The name of the member.</param>
        /// <param name="args">The prepared function arguments.</param>
        /// <param name="returnVal">The value returned when the member is handled.</param>
        /// <returns>
        ///  <see langword="true"/> if the member was handled; otherwise, <see langword="false"/>.
        /// </returns>
        internal static bool TryExecuteStringFunction(string text, string methodName, object?[] args, out object? returnVal)
        {
            if (string.Equals(methodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = text.StartsWith(arg0, StringComparison.CurrentCulture);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = text.Replace(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
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
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = text.EndsWith(arg0, StringComparison.CurrentCulture);
                    return true;
                }
                else if (ParseArgs.TryGetArgs(args, out arg0, out StringComparison arg1))
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
                if (ParseArgs.TryGetArgs(args, out string? arg0, out StringComparison arg1))
                {
                    returnVal = text.IndexOf(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = text.AsSpan().IndexOfAny(arg0.AsSpan());
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = text.LastIndexOf(arg0, StringComparison.CurrentCulture);
                    return true;
                }
                else if (ParseArgs.TryGetArgs(args, out arg0, out int startIndex))
                {
                    returnVal = text.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture);
                    return true;
                }
                else if (ParseArgs.TryGetArgs(args, out arg0, out StringComparison arg1))
                {
                    returnVal = text.LastIndexOf(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
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
                if (ParseArgs.TryGetArg(args, out int startIndex))
                {
                    returnVal = text.Substring(startIndex);
                    return true;
                }
                else if (ParseArgs.TryGetArgs(args, out startIndex, out int length))
                {
                    returnVal = text.Substring(startIndex, length);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Split), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out char separator))
                {
                    returnVal = text.Split(separator);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int totalWidth))
                {
                    returnVal = text.PadLeft(totalWidth);
                    return true;
                }
                else if (ParseArgs.TryGetArgs(args, out totalWidth, out char paddingChar))
                {
                    returnVal = text.PadLeft(totalWidth, paddingChar);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int totalWidth))
                {
                    returnVal = text.PadRight(totalWidth);
                    return true;
                }
                else if (ParseArgs.TryGetArgs(args, out totalWidth, out char paddingChar))
                {
                    returnVal = text.PadRight(totalWidth, paddingChar);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? trimChars) && trimChars.Length > 0)
                {
                    returnVal = text.TrimStart(trimChars.ToCharArray());
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? trimChars) && trimChars.Length > 0)
                {
                    returnVal = text.TrimEnd(trimChars.ToCharArray());
                    return true;
                }
            }
            else if (string.Equals(methodName, "get_Chars", StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int index))
                {
                    returnVal = text[index];
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
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
            object?[] args,
            ref readonly ExpansionContext context,
            out object? returnVal)
        {
            if (string.Equals(methodName, nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
            {
                if (ElementsOfType(args, typeof(string)))
                {
                    returnVal = IntrinsicFunctions.NormalizePath(Array.ConvertAll(args, o => (string)o!));
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, context.FileSystem);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length >= 4 &&
                    ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
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
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.Escape(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.Unescape(arg0);
                    return true;
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
                        returnVal = IntrinsicFunctions.GetPathOfFileAbove(file, startingDirectory, context.FileSystem);
                        return true;
                    }
                }

                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, context.FileSystem);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Add, IntrinsicFunctions.Add, out returnVal))
                {
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Subtract, IntrinsicFunctions.Subtract, out returnVal))
                {
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Multiply, IntrinsicFunctions.Multiply, out returnVal))
                {
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Divide, IntrinsicFunctions.Divide, out returnVal))
                {
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryExecuteArithmeticOverload(args, IntrinsicFunctions.Modulo, IntrinsicFunctions.Modulo, out returnVal))
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
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionEquals(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg0);
                    return true;
                }

                if (ParseArgs.TryGetArgs(args, out string? arg1, out int arg2))
                {
                    returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                {
                    returnVal = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg0);
                    return true;
                }

                if (ParseArgs.TryGetArgs(args, out string? arg1, out int arg2))
                {
                    returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.ConvertToBase64(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.ConvertFromBase64(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.StableStringHash(arg0);
                    return true;
                }
                else if (ParseArgs.TryGetArgs(args, out string? arg1, out string? arg2) &&
                    Enum.TryParse(arg2, ignoreCase: true, out IntrinsicFunctions.StringHashingAlgorithm hashAlgorithm))
                {
                    returnVal = IntrinsicFunctions.StableStringHash(arg1, hashAlgorithm);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out Version? arg0))
                {
                    returnVal = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out string? arg0, out int arg1, out int arg2))
                {
                    returnVal = IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.CheckFeatureAvailability(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseOr(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseAnd(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.BitwiseXor(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int arg0))
                {
                    returnVal = IntrinsicFunctions.BitwiseNot(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.LeftShift(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.RightShift(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArgs(args, out int arg0, out int arg1))
                {
                    returnVal = IntrinsicFunctions.RightShiftUnsigned(arg0, arg1);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.NormalizeDirectory(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.IsOSPlatform(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.FileExists(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.DirectoryExists(arg0);
                    return true;
                }
            }
            else if (string.Equals(methodName, nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase))
            {
                string projectPath = context.Properties!.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
                var loggingContext = context.LoggingContext;
                Assumed.NotNull(loggingContext, $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = IntrinsicFunctions.RegisterBuildCheck(projectPath, arg0, loggingContext);
                    return true;
                }
            }

            returnVal = null;
            return false;
        }

        /// <summary>
        ///  Executes recognized instance members without reflection binding.
        /// </summary>
        /// <param name="objectInstance">The receiver instance.</param>
        /// <param name="methodName">The name of the member.</param>
        /// <param name="args">The prepared function arguments.</param>
        /// <param name="returnVal">The value returned when the member is handled.</param>
        /// <returns>
        ///  <see langword="true"/> if the member was handled; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  Avoids the cost of reflection binding and first-chance missing-method exceptions.
        ///  See https://github.com/dotnet/msbuild/issues/2217.
        /// </remarks>
        public static bool TryInvokeInstanceMember(
            object objectInstance,
            string methodName,
            object?[] args,
            out object? returnVal)
        {
            if (objectInstance is string text)
            {
                return TryExecuteStringFunction(text, methodName, args, out returnVal);
            }
            else if (objectInstance is string[] stringArray)
            {
                if (string.Equals(methodName, "GetValue", StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out int index))
                    {
                        returnVal = stringArray[index];
                        return true;
                    }
                }
            }
            else if (objectInstance is Version v && string.Equals(methodName, nameof(Version.ToString), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out int arg0))
                {
                    returnVal = v.ToString(arg0);
                    return true;
                }
            }
            else if (objectInstance is int i && string.Equals(methodName, nameof(Int32.ToString), StringComparison.OrdinalIgnoreCase))
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = i.ToString(arg0);
                    return true;
                }
            }

            if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
            {
                LogFunctionCall(objectInstance.GetType(), methodName, "PropertyFunctionsRequiringReflection", objectInstance, args);
            }

            returnVal = null;
            return false;
        }

        /// <summary>
        ///  Executes recognized static members without reflection binding.
        /// </summary>
        /// <param name="receiverType">The type on which to look up a static member.</param>
        /// <param name="methodName">The name of the member.</param>
        /// <param name="args">The prepared function arguments.</param>
        /// <param name="context">The properties, options, and services used during expansion.</param>
        /// <param name="returnVal">The value returned when the member is handled.</param>
        /// <returns>
        ///  <see langword="true"/> if the member was handled; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool TryInvokeStaticMember(
            Type receiverType,
            string methodName,
            object?[] args,
            ref readonly ExpansionContext context,
            out object? returnVal)
        {
            if (receiverType == typeof(string))
            {
                if (string.Equals(methodName, nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0))
                    {
                        returnVal = string.IsNullOrWhiteSpace(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0))
                    {
                        returnVal = string.IsNullOrEmpty(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0))
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
                    if (ParseArgs.TryGetArgs(args, out double arg0, out double arg1))
                    {
                        returnVal = Math.Max(arg0, arg1);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArgs(args, out double arg0, out double arg1))
                    {
                        returnVal = Math.Min(arg0, arg1);
                        return true;
                    }
                }
            }
            else if (receiverType == typeof(IntrinsicFunctions))
            {
                return TryExecuteIntrinsicFunction(methodName, args, in context, out returnVal);
            }
            else if (receiverType == typeof(Path))
            {
                return TryExecutePathFunction(methodName, args, out returnVal);
            }
            else if (receiverType == typeof(Version))
            {
                if (string.Equals(methodName, nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0))
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
                    if (ParseArgs.TryGetArg(args, out char c))
                    {
                        returnVal = char.IsDigit(c);
                        return true;
                    }

                    if (ParseArgs.TryGetArgs(args, out string? str, out int index))
                    {
                        returnVal = char.IsDigit(str, index);
                        return true;
                    }
                }
            }
            else if (receiverType == typeof(Regex))
            {
                if (string.Equals(methodName, nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) && args.Length == 3)
                {
                    if (ParseArgs.TryGetArgs(args, out string? arg1, out string? arg2, out string? arg3))
                    {
                        returnVal = Regex.Replace(arg1, arg2, arg3);
                        return true;
                    }
                }
            }

            if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
            {
                LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", null, args);
            }

            returnVal = null;
            return false;
        }

        /// <summary>
        ///  Executes recognized constructors without reflection binding.
        /// </summary>
        /// <param name="receiverType">The receiver type for the constructor.</param>
        /// <param name="args">The prepared constructor arguments.</param>
        /// <param name="returnVal">The instance as created by the constructor call.</param>
        /// <returns>
        ///  <see langword="true"/> if the constructor was handled; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool TryInvokeConstructor(Type? receiverType, object?[] args, out object? returnVal)
        {
            returnVal = null;

            if (receiverType == typeof(string))
            {
                if (args.Length == 0)
                {
                    returnVal = string.Empty;
                    return true;
                }

                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    returnVal = arg0;
                    return true;
                }
            }

            return false;
        }
    }
}
