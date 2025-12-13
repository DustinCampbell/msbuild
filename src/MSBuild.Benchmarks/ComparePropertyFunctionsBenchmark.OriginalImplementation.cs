// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

public partial class ComparePropertyFunctionsBenchmark
{
    private static class OriginalImplementation
    {
        internal class WellKnownFunctions
        {
            private static bool ElementsOfType(object[] args, Type type)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i].GetType() != type)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool TryExecutePathFunction(string methodName, out object? returnVal, object[] args)
            {
                returnVal = default;
                if (string.Equals(methodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
                {
                    string? arg0, arg1, arg2, arg3;

                    // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                    switch (args.Length)
                    {
                        case 0:
                            return false;
                        case 1:
                            if (ParseArgs.TryGetArg(args, out arg0) && arg0 != null)
                            {
                                returnVal = Path.Combine(arg0);
                                return true;
                            }
                            break;
                        case 2:
                            if (ParseArgs.TryGetArgs(args, out arg0, out arg1) && arg0 != null && arg1 != null)
                            {
                                returnVal = Path.Combine(arg0, arg1);
                                return true;
                            }
                            break;
                        case 3:
                            if (ParseArgs.TryGetArgs(args, out arg0, out arg1, out arg2) && arg0 != null && arg1 != null && arg2 != null)
                            {
                                returnVal = Path.Combine(arg0, arg1, arg2);
                                return true;
                            }
                            break;
                        case 4:
                            if (ParseArgs.TryGetArgs(args, out arg0, out arg1, out arg2, out arg3) && arg0 != null && arg1 != null && arg2 != null && arg3 != null)
                            {
                                returnVal = Path.Combine(arg0, arg1, arg2, arg3);
                                return true;
                            }
                            break;
                        default:
                            if (ElementsOfType(args, typeof(string)))
                            {
                                returnVal = Path.Combine(Array.ConvertAll(args, o => (string)o));
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
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = Path.GetFullPath(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
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
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = Path.GetFileName(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = Path.GetDirectoryName(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
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
            private static bool TryExecuteStringFunction(string methodName, out object? returnVal, string text, object[] args)
            {
                returnVal = null;
                if (string.Equals(methodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = text.StartsWith(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1) && arg0 != null)
                    {
                        returnVal = text.Replace(arg0, arg1);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
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
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = text.EndsWith(arg0);
                        return true;
                    }
                    else if (ParseArgs.TryGetArgs(args, out arg0, out StringComparison arg1) && arg0 != null)
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
                    if (ParseArgs.TryGetArgs(args, out string? arg0, out StringComparison arg1) && arg0 != null)
                    {
                        returnVal = text.IndexOf(arg0, arg1);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = text.AsSpan().IndexOfAny(arg0.AsSpan());
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = text.LastIndexOf(arg0);
                        return true;
                    }
                    else if (ParseArgs.TryGetArgs(args, out arg0, out int startIndex) && arg0 != null)
                    {
                        returnVal = text.LastIndexOf(arg0, startIndex);
                        return true;
                    }
                    else if (ParseArgs.TryGetArgs(args, out arg0, out StringComparison arg1) && arg0 != null)
                    {
                        returnVal = text.LastIndexOf(arg0, arg1);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
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
                    if (ParseArgs.TryGetArg(args, out string? separator) && separator?.Length == 1)
                    {
                        returnVal = text.Split(separator[0]);
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
                    else if (ParseArgs.TryGetArgs(args, out totalWidth, out string? paddingChar) && paddingChar?.Length == 1)
                    {
                        returnVal = text.PadLeft(totalWidth, paddingChar[0]);
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
                    else if (ParseArgs.TryGetArgs(args, out totalWidth, out string? paddingChar) && paddingChar?.Length == 1)
                    {
                        returnVal = text.PadRight(totalWidth, paddingChar[0]);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? trimChars) && trimChars?.Length > 0)
                    {
                        returnVal = text.TrimStart(trimChars.ToCharArray());
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? trimChars) && trimChars?.Length > 0)
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
                return false;
            }

            private static bool TryExecuteIntrinsicFunction(string methodName, out object? returnVal, IFileSystem fileSystem, object[] args)
            {
                returnVal = default;
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
                        returnVal = IntrinsicFunctions.NormalizePath(Array.ConvertAll(args, o => (string)o));
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                    {
                        returnVal = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, fileSystem);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length >= 4 &&
                        ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                    {
                        returnVal = IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], new ArraySegment<object>(args, 3, args.Length - 3));
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
                    if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                    {
                        returnVal = IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, fileSystem);
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
                        // Prevent loading methods refs from StringTools if ChangeWave opted out.
                        returnVal = ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10)
                            ? IntrinsicFunctions.StableStringHash(arg0)
                            : IntrinsicFunctions.StableStringHashLegacy(arg0);
                        return true;
                    }
                    else if (ParseArgs.TryGetArgs(args, out string? arg1, out string? arg2) && Enum.TryParse<IntrinsicFunctions.StringHashingAlgorithm>(arg2, true, out var hashAlgorithm) && arg1 != null && arg2 != null)
                    {
                        returnVal = IntrinsicFunctions.StableStringHash(arg1, hashAlgorithm);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out Version? arg0) && arg0 != null)
                    {
                        returnVal = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArgs(args, out string? arg0, out int arg1, out int arg2) && arg0 != null)
                    {
                        returnVal = IntrinsicFunctions.SubstringByAsciiChars(arg0, arg1, arg2);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
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
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = IntrinsicFunctions.NormalizeDirectory(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
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
            internal static bool TryExecuteWellKnownFunction(string methodName, Type receiverType, IFileSystem fileSystem, out object? returnVal, object? objectInstance, object[] args)
            {
                returnVal = null;

                if (objectInstance is string text)
                {
                    return TryExecuteStringFunction(methodName, out returnVal, text, args);
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
                else if (objectInstance == null) // Calling a well-known static function
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
                            if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
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

                            if (ParseArgs.TryGetArg(args, out string? arg0) && arg0?.Length == 1)
                            {
                                char c = arg0[0];
                                result = char.IsDigit(c);
                            }
                            else if (ParseArgs.TryGetArgs(args, out string? str, out int index) && str != null)
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
                            if (ParseArgs.TryGetArgs(args, out string? arg1, out string? arg2, out string? arg3) && arg1 != null && arg2 != null && arg3 != null)
                            {
                                returnVal = Regex.Replace(arg1, arg2, arg3);
                                return true;
                            }
                        }
                    }
                }
                else if (string.Equals(methodName, nameof(Version.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is Version v)
                {
                    if (ParseArgs.TryGetArg(args, out int arg0))
                    {
                        returnVal = v.ToString(arg0);
                        return true;
                    }
                }
                else if (string.Equals(methodName, nameof(Int32.ToString), StringComparison.OrdinalIgnoreCase) && objectInstance is int i)
                {
                    if (ParseArgs.TryGetArg(args, out string? arg0) && arg0 != null)
                    {
                        returnVal = i.ToString(arg0);
                        return true;
                    }
                }

                return false;
            }

            internal static class ParseArgs
            {
                internal static bool TryGetArgs(object[] args, out string? arg0, out string? arg1, bool enforceLength = true)
                {
                    arg0 = null;
                    arg1 = null;

                    if (enforceLength && args.Length != 2)
                    {
                        return false;
                    }

                    if (args[0] is string value0 &&
                        args[1] is string value1)
                    {
                        arg0 = value0;
                        arg1 = value1;

                        return true;
                    }

                    return false;
                }

                internal static bool TryGetArgs(object[] args, out string? arg0, out string? arg1, out string? arg2)
                {
                    arg0 = null;
                    arg1 = null;
                    arg2 = null;

                    if (args.Length != 3)
                    {
                        return false;
                    }

                    if (args[0] is string value0 &&
                        args[1] is string value1 &&
                        args[2] is string value2)
                    {
                        arg0 = value0;
                        arg1 = value1;
                        arg2 = value2;

                        return true;
                    }

                    return false;
                }

                internal static bool TryGetArgs(object[] args, out string? arg0, out string? arg1, out string? arg2, out string? arg3)
                {
                    arg0 = null;
                    arg1 = null;
                    arg2 = null;
                    arg3 = null;

                    if (args.Length != 4)
                    {
                        return false;
                    }

                    if (args[0] is string value0 &&
                        args[1] is string value1 &&
                        args[2] is string value2 &&
                        args[3] is string value3)
                    {
                        arg0 = value0;
                        arg1 = value1;
                        arg2 = value2;
                        arg3 = value3;

                        return true;
                    }

                    return false;
                }

                internal static bool TryGetArgs(object[] args, out string? arg0, out string? arg1)
                {
                    arg0 = null;
                    arg1 = null;

                    if (args.Length != 2)
                    {
                        return false;
                    }

                    if (args[0] is string value0 &&
                        args[1] is string value1)
                    {
                        arg0 = value0;
                        arg1 = value1;

                        return true;
                    }

                    return false;
                }

                internal static bool TryGetArgs(object[] args, out string? arg0, out int arg1, out int arg2)
                {
                    arg0 = null;
                    arg1 = 0;
                    arg2 = 0;

                    if (args.Length != 3)
                    {
                        return false;
                    }

                    var value1 = args[1] as string;
                    var value2 = args[2] as string;
                    arg0 = args[0] as string;
                    if (value1 != null &&
                        value2 != null &&
                        arg0 != null &&
                        int.TryParse(value1, out arg1) &&
                        int.TryParse(value2, out arg2))
                    {
                        return true;
                    }

                    return false;
                }

                internal static bool TryGetArg(object[] args, out int arg0)
                {
                    if (args.Length != 1)
                    {
                        arg0 = 0;
                        return false;
                    }

                    return TryConvertToInt(args[0], out arg0);
                }

                internal static bool TryGetArg(object[] args, out Version? arg0)
                {
                    if (args.Length != 1)
                    {
                        arg0 = default;
                        return false;
                    }

                    return TryConvertToVersion(args[0], out arg0);
                }

                internal static bool TryConvertToVersion(object value, out Version? arg0)
                {
                    string? val = value as string;

                    if (string.IsNullOrEmpty(val) || !Version.TryParse(val, out arg0))
                    {
                        arg0 = default;
                        return false;
                    }

                    return true;
                }

                /// <summary>
                /// Try to convert value to int.
                /// </summary>
                internal static bool TryConvertToInt(object? value, out int arg)
                {
                    switch (value)
                    {
                        case double d:
                            if (d >= int.MinValue && d <= int.MaxValue)
                            {
                                arg = Convert.ToInt32(d);
                                if (Math.Abs(arg - d) == 0)
                                {
                                    return true;
                                }
                            }

                            break;
                        case long l:
                            if (l >= int.MinValue && l <= int.MaxValue)
                            {
                                arg = Convert.ToInt32(l);
                                return true;
                            }

                            break;
                        case int i:
                            arg = i;
                            return true;
                        case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out arg):
                            return true;
                    }

                    arg = 0;
                    return false;
                }

                /// <summary>
                /// Try to convert value to long.
                /// </summary>
                internal static bool TryConvertToLong(object? value, out long arg)
                {
                    switch (value)
                    {
                        case double d:
                            if (d >= long.MinValue && d <= long.MaxValue)
                            {
                                arg = (long)d;
                                if (Math.Abs(arg - d) == 0)
                                {
                                    return true;
                                }
                            }

                            break;
                        case long l:
                            arg = l;
                            return true;
                        case int i:
                            arg = i;
                            return true;
                        case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out arg):
                            return true;
                    }

                    arg = 0;
                    return false;
                }

                /// <summary>
                /// Try to convert value to double.
                /// </summary>
                internal static bool TryConvertToDouble(object? value, out double arg)
                {
                    switch (value)
                    {
                        case double unboxed:
                            arg = unboxed;
                            return true;
                        case long l:
                            arg = l;
                            return true;
                        case int i:
                            arg = i;
                            return true;
                        case string str when double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out arg):
                            return true;
                        default:
                            arg = 0;
                            return false;
                    }
                }

                internal static bool TryGetArg(object[] args, out string? arg0)
                {
                    if (args.Length != 1)
                    {
                        arg0 = null;
                        return false;
                    }

                    arg0 = args[0] as string;
                    return arg0 != null;
                }

                internal static bool TryGetArgs(object[] args, out string? arg0, out StringComparison arg1)
                {
                    if (args.Length != 2)
                    {
                        arg0 = null;
                        arg1 = default;

                        return false;
                    }

                    arg0 = args[0] as string;

                    // reject enums as ints. In C# this would require a cast, which is not supported in msbuild expressions
                    if (arg0 == null || !(args[1] is string comparisonTypeName) || int.TryParse(comparisonTypeName, out _))
                    {
                        arg1 = default;
                        return false;
                    }

                    // Allow fully-qualified enum, e.g. "System.StringComparison.OrdinalIgnoreCase"
                    if (comparisonTypeName.Contains('.'))
                    {
                        comparisonTypeName = comparisonTypeName.Replace("System.StringComparison.", "").Replace("StringComparison.", "");
                    }

                    return Enum.TryParse(comparisonTypeName, out arg1);
                }

                internal static bool TryGetArgs(object[] args, out int arg0)
                {
                    arg0 = 0;

                    if (args.Length != 1)
                    {
                        return false;
                    }

                    return TryConvertToInt(args[0], out arg0);
                }

                internal static bool TryGetArgs(object[] args, out int arg0, out int arg1)
                {
                    arg0 = 0;
                    arg1 = 0;

                    if (args.Length != 2)
                    {
                        return false;
                    }

                    return TryConvertToInt(args[0], out arg0) &&
                           TryConvertToInt(args[1], out arg1);
                }

                internal static bool TryGetArgs(object[] args, out double arg0, out double arg1)
                {
                    arg0 = 0;
                    arg1 = 0;

                    if (args.Length != 2)
                    {
                        return false;
                    }

                    return TryConvertToDouble(args[0], out arg0) &&
                           TryConvertToDouble(args[1], out arg1);
                }

                internal static bool TryGetArgs(object[] args, out int arg0, out string? arg1)
                {
                    arg0 = 0;
                    arg1 = null;

                    if (args.Length != 2)
                    {
                        return false;
                    }

                    arg1 = args[1] as string;
                    if (arg1 == null && args[1] is char ch)
                    {
                        arg1 = ch.ToString();
                    }

                    if (TryConvertToInt(args[0], out arg0) &&
                        arg1 != null)
                    {
                        return true;
                    }

                    return false;
                }

                internal static bool TryGetArgs(object[] args, out string? arg0, out int arg1)
                {
                    arg0 = null;
                    arg1 = 0;

                    if (args.Length != 2)
                    {
                        return false;
                    }

                    var value1 = args[1] as string;
                    arg0 = args[0] as string;
                    if (value1 != null &&
                        arg0 != null &&
                        int.TryParse(value1, out arg1))
                    {
                        return true;
                    }

                    return false;
                }

                internal static bool IsFloatingPointRepresentation(object value)
                {
                    return value is double || (value is string str && double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double _));
                }

                internal static bool TryExecuteArithmeticOverload(object[] args, Func<long, long, long> integerOperation, Func<double, double, double> realOperation, out object? resultValue)
                {
                    resultValue = null;

                    if (args.Length != 2)
                    {
                        return false;
                    }

                    if (TryConvertToLong(args[0], out long argLong0) && TryConvertToLong(args[1], out long argLong1))
                    {
                        resultValue = integerOperation(argLong0, argLong1);
                        return true;
                    }

                    if (TryConvertToDouble(args[0], out double argDouble0) && TryConvertToDouble(args[1], out double argDouble1))
                    {
                        resultValue = realOperation(argDouble0, argDouble1);
                        return true;
                    }

                    return false;
                }
            }
        }

        private class ChangeWaves
        {
            internal static readonly Version Wave17_10 = new Version(17, 10);

            internal static bool AreFeaturesEnabled(Version wave) => true;
        }
    }
}
