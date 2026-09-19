// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class IntrinsicHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments args, ref readonly ExecutionContext context)
            => name.Length switch
            {
                3 when name.Equals(nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeAdd(ref args),
                6 when name.Equals(nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDivide(ref args),
                6 when name.Equals(nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEscape(ref args),
                6 when name.Equals(nameof(IntrinsicFunctions.Modulo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeModulo(ref args),
                8 when name.Equals(nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMultiply(ref args),
                8 when name.Equals(nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSubtract(ref args),
                8 when name.Equals(nameof(IntrinsicFunctions.Unescape), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeUnescape(ref args),
                9 when name.Equals(nameof(IntrinsicFunctions.BitwiseOr), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseOr(ref args),
                9 when name.Equals(nameof(IntrinsicFunctions.LeftShift), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLeftShift(ref args),
                10 when name.Equals(nameof(IntrinsicFunctions.BitwiseAnd), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseAnd(ref args),
                10 when name.Equals(nameof(IntrinsicFunctions.BitwiseNot), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseNot(ref args),
                10 when name.Equals(nameof(IntrinsicFunctions.BitwiseXor), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeBitwiseXor(ref args),
                10 when name.Equals(nameof(IntrinsicFunctions.FileExists), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeFileExists(ref args),
                10 when name.Equals(nameof(IntrinsicFunctions.RightShift), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRightShift(ref args),
                12 when name.Equals(nameof(IntrinsicFunctions.IsOSPlatform), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsOSPlatform(ref args),
                12 when name.Equals(nameof(IntrinsicFunctions.MakeRelative), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMakeRelative(ref args),
                13 when name.Equals(nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNormalizePath(ref args),
                13 when name.Equals(nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionEquals(ref args),
                13 when name.Equals(nameof(IntrinsicFunctions.__GetListTest), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetListTest(ref args),
                14 when name.Equals(nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeValueOrDefault(ref args),
                15 when name.Equals(nameof(IntrinsicFunctions.ConvertToBase64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeConvertToBase64(ref args),
                15 when name.Equals(nameof(IntrinsicFunctions.DirectoryExists), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDirectoryExists(ref args),
                15 when name.Equals(nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionLessThan(ref args),
                16 when name.Equals(nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetVsInstallRoot(ref args),
                16 when name.Equals(nameof(IntrinsicFunctions.GetRegistryValue), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetRegistryValue(ref args),
                16 when name.Equals(nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStableStringHash(ref args),
                16 when name.Equals(nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionNotEquals(ref args),
                17 when name.Equals(nameof(IntrinsicFunctions.ConvertFromBase64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeConvertFromBase64(ref args),
                17 when name.Equals(nameof(IntrinsicFunctions.DoesTaskHostExist), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDoesTaskHostExist(ref args),
                17 when name.Equals(nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetProgramFiles32(ref args),
                18 when name.Equals(nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeAreFeaturesEnabled(ref args),
                18 when name.Equals(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetMSBuildSDKsPath(ref args),
                18 when name.Equals(nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetPathOfFileAbove(ref args, in context),
                18 when name.Equals(nameof(IntrinsicFunctions.NormalizeDirectory), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNormalizeDirectory(ref args),
                18 when name.Equals(nameof(IntrinsicFunctions.RegisterBuildCheck), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRegisterBuildCheck(ref args, in context),
                18 when name.Equals(nameof(IntrinsicFunctions.RightShiftUnsigned), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRightShiftUnsigned(ref args),
                18 when name.Equals(nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionGreaterThan(ref args),
                19 when name.Equals(nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEnsureTrailingSlash(ref args),
                19 when name.Equals(nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetToolsDirectory32(ref args),
                19 when name.Equals(nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetToolsDirectory64(ref args),
                21 when name.Equals(nameof(IntrinsicFunctions.SubstringByAsciiChars), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSubstringByAsciiChars(ref args),
                22 when name.Equals(nameof(IntrinsicFunctions.FilterTargetFrameworks), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeFilterTargetFrameworks(ref args),
                23 when name.Equals(nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionLessThanOrEquals(ref args),
                24 when name.Equals(nameof(IntrinsicFunctions.CheckFeatureAvailability), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCheckFeatureAvailability(ref args),
                24 when name.Equals(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetCurrentToolsDirectory(ref args),
                24 when name.Equals(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetMSBuildExtensionsPath(ref args),
                24 when name.Equals(nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetRegistryValueFromView(ref args),
                24 when name.Equals(nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetPlatformVersion(ref args),
                25 when name.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetFrameworkVersion(ref args),
                25 when name.Equals(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsRunningFromVisualStudio(ref args),
                26 when name.Equals(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeVersionGreaterThanOrEquals(ref args),
                27 when name.Equals(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase)
                     => TryInvokeGetDirectoryNameOfFileAbove(ref args, in context),
                27 when name.Equals(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetPlatformIdentifier(ref args),
                27 when name.Equals(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsTargetFrameworkCompatible(ref args),
                28 when name.Equals(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTargetFrameworkIdentifier(ref args),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeEnsureTrailingSlash(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? path))
            {
                return Handled(IntrinsicFunctions.EnsureTrailingSlash(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeDoesTaskHostExist(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? runtime) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? architecture))
            {
                return Handled(IntrinsicFunctions.DoesTaskHostExist(runtime, architecture));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeValueOrDefault(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? defaultValue))
            {
                return Handled(IntrinsicFunctions.ValueOrDefault(value, defaultValue));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeNormalizePath(ref FunctionArguments args)
        {
            string[] paths = new string[args.Count];
            for (int i = 0; i < paths.Length; i++)
            {
                if (!FunctionArgumentCoercion.TryCoerce(args.GetValue(i), out string? path))
                {
                    return NotRecognized;
                }

                paths[i] = path;
            }

            return Handled(IntrinsicFunctions.NormalizePath(paths));
        }

        private static WellKnownMemberResult TryInvokeGetDirectoryNameOfFileAbove(ref FunctionArguments args, ref readonly ExecutionContext context)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? startingDirectory) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? fileName))
            {
                return Handled(IntrinsicFunctions.GetDirectoryNameOfFileAbove(startingDirectory, fileName, context.FileSystem));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetRegistryValueFromView(ref FunctionArguments args)
        {
            if (args.Count >= 4)
            {
                object?[] values = args.MaterializeAll();
                string? keyName = values[0] as string;
                string? valueName = values[1] as string;
                object? defaultValue = values[2];
                ArraySegment<object?> views = new(values, offset: 3, count: values.Length - 3);

                if (keyName is not null)
                {
                    return Handled(IntrinsicFunctions.GetRegistryValueFromView(keyName, valueName!, defaultValue, views));
                }
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetRegistryValue(ref FunctionArguments args)
        {
            if (args.Count is 2 or 3 &&
                FunctionArgumentCoercion.TryCoerceOrNull(args.GetValue(0), out string? keyName) &&
                keyName is not null &&
                FunctionArgumentCoercion.TryCoerceOrNull(args.GetValue(1), out string? valueName))
            {
                if (args.Count == 2)
                {
                    return Handled(IntrinsicFunctions.GetRegistryValue(keyName, valueName!));
                }

                return Handled(IntrinsicFunctions.GetRegistryValue(keyName, valueName!, args.GetValue(2)));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetListTest(ref FunctionArguments args)
            => args.Count == 0
                ? Handled(IntrinsicFunctions.__GetListTest())
                : NotRecognized;

        private static WellKnownMemberResult TryInvokeFilterTargetFrameworks(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? incoming) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? filter))
            {
                return Handled(IntrinsicFunctions.FilterTargetFrameworks(incoming, filter));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsRunningFromVisualStudio(ref FunctionArguments args)
        {
            if (args.Count == 0)
            {
                return Handled(IntrinsicFunctions.IsRunningFromVisualStudio());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeEscape(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value))
            {
                return Handled(IntrinsicFunctions.Escape(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeUnescape(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value))
            {
                return Handled(IntrinsicFunctions.Unescape(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetPathOfFileAbove(ref FunctionArguments args, ref readonly ExecutionContext context)
        {
            switch (args.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? file):
                    return Handled(
                        IntrinsicFunctions.GetPathOfFileAbove(
                            file,
                            context.GetStartingDirectory(),
                            context.FileSystem));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? file) &&
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? startingDirectory):
                    return Handled(IntrinsicFunctions.GetPathOfFileAbove(file, startingDirectory, context.FileSystem));

                default:
                    return NotRecognized;
            }
        }

        private static WellKnownMemberResult TryInvokeAdd(ref FunctionArguments args)
        {
            if (TryGetArithmeticArguments(ref args, out ArithmeticArguments arguments))
            {
                return arguments.Kind == ArithmeticArgumentKind.Int64
                    ? Handled(arguments.LeftInt64 + arguments.RightInt64)
                    : Handled(arguments.LeftDouble + arguments.RightDouble);
            }

            return InvalidArguments;
        }

        private static WellKnownMemberResult TryInvokeSubtract(ref FunctionArguments args)
        {
            if (TryGetArithmeticArguments(ref args, out ArithmeticArguments arguments))
            {
                return arguments.Kind == ArithmeticArgumentKind.Int64
                    ? Handled(arguments.LeftInt64 - arguments.RightInt64)
                    : Handled(arguments.LeftDouble - arguments.RightDouble);
            }

            return InvalidArguments;
        }

        private static WellKnownMemberResult TryInvokeMultiply(ref FunctionArguments args)
        {
            if (TryGetArithmeticArguments(ref args, out ArithmeticArguments arguments))
            {
                return arguments.Kind == ArithmeticArgumentKind.Int64
                    ? Handled(arguments.LeftInt64 * arguments.RightInt64)
                    : Handled(arguments.LeftDouble * arguments.RightDouble);
            }

            return InvalidArguments;
        }

        private static WellKnownMemberResult TryInvokeDivide(ref FunctionArguments args)
        {
            if (TryGetArithmeticArguments(ref args, out ArithmeticArguments arguments))
            {
                return arguments.Kind == ArithmeticArgumentKind.Int64
                    ? Handled(arguments.LeftInt64 / arguments.RightInt64)
                    : Handled(arguments.LeftDouble / arguments.RightDouble);
            }

            return InvalidArguments;
        }

        private static WellKnownMemberResult TryInvokeModulo(ref FunctionArguments arg)
        {
            if (TryGetArithmeticArguments(ref arg, out ArithmeticArguments arguments))
            {
                return arguments.Kind == ArithmeticArgumentKind.Int64
                    ? Handled(arguments.LeftInt64 % arguments.RightInt64)
                    : Handled(arguments.LeftDouble % arguments.RightDouble);
            }

            return InvalidArguments;
        }

        private static bool TryGetArithmeticArguments(ref FunctionArguments args, out ArithmeticArguments result)
        {
            if (args.Count == 2)
            {
                object? left = args.GetValue(0);
                object? right = args.GetValue(1);
                return FunctionArgumentCoercion.TryCoerceArithmetic(left, right, out result);
            }

            result = default;
            return false;
        }

        private static WellKnownMemberResult TryInvokeGetCurrentToolsDirectory(ref FunctionArguments args)
        {
            if (args.Count == 0)
            {
                return Handled(IntrinsicFunctions.GetCurrentToolsDirectory());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetToolsDirectory32(ref FunctionArguments args)
        {
            if (args.Count == 0)
            {
                return Handled(IntrinsicFunctions.GetToolsDirectory32());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetToolsDirectory64(ref FunctionArguments args)
        {
            if (args.Count == 0)
            {
                return Handled(IntrinsicFunctions.GetToolsDirectory64());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetMSBuildSDKsPath(ref FunctionArguments args)
        {
            if (args.Count == 0)
            {
                return Handled(IntrinsicFunctions.GetMSBuildSDKsPath());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetVsInstallRoot(ref FunctionArguments args)
        {
            if (args.Count == 0)
            {
                return Handled(IntrinsicFunctions.GetVsInstallRoot());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetMSBuildExtensionsPath(ref FunctionArguments args)
        {
            if (args.Count == 0)
            {
                return Handled(IntrinsicFunctions.GetMSBuildExtensionsPath());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetProgramFiles32(ref FunctionArguments args)
        {
            if (args.Count == 0)
            {
                return Handled(IntrinsicFunctions.GetProgramFiles32());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeVersionEquals(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? right))
            {
                return Handled(IntrinsicFunctions.VersionEquals(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeVersionNotEquals(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? right))
            {
                return Handled(IntrinsicFunctions.VersionNotEquals(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeVersionGreaterThan(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? right))
            {
                return Handled(IntrinsicFunctions.VersionGreaterThan(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeVersionGreaterThanOrEquals(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? right))
            {
                return Handled(IntrinsicFunctions.VersionGreaterThanOrEquals(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeVersionLessThan(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? right))
            {
                return Handled(IntrinsicFunctions.VersionLessThan(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeVersionLessThanOrEquals(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? right))
            {
                return Handled(IntrinsicFunctions.VersionLessThanOrEquals(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTargetFrameworkIdentifier(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? targetFramework))
            {
                return Handled(IntrinsicFunctions.GetTargetFrameworkIdentifier(targetFramework));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTargetFrameworkVersion(ref FunctionArguments args)
        {
            switch (args.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? targetFramework):
                    return Handled(IntrinsicFunctions.GetTargetFrameworkVersion(targetFramework));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? targetFramework) &&
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int versionPartCount):
                    return Handled(IntrinsicFunctions.GetTargetFrameworkVersion(targetFramework, versionPartCount));

                default:
                    return NotRecognized;
            }
        }

        private static WellKnownMemberResult TryInvokeIsTargetFrameworkCompatible(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? targetFramework) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? candidateTargetFramework))
            {
                return Handled(IntrinsicFunctions.IsTargetFrameworkCompatible(targetFramework, candidateTargetFramework));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTargetPlatformIdentifier(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? targetFramework))
            {
                return Handled(IntrinsicFunctions.GetTargetPlatformIdentifier(targetFramework));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTargetPlatformVersion(ref FunctionArguments args)
        {
            switch (args.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? targetFramework):
                    return Handled(IntrinsicFunctions.GetTargetPlatformVersion(targetFramework));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? targetFramework) &&
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int versionPartCount):
                    return Handled(IntrinsicFunctions.GetTargetPlatformVersion(targetFramework, versionPartCount));

                default:
                    return NotRecognized;
            }
        }

        private static WellKnownMemberResult TryInvokeConvertToBase64(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value))
            {
                return Handled(IntrinsicFunctions.ConvertToBase64(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeConvertFromBase64(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value))
            {
                return Handled(IntrinsicFunctions.ConvertFromBase64(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeStableStringHash(ref FunctionArguments args)
        {
            switch (args.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value):
                    return Handled(IntrinsicFunctions.StableStringHash(value));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value) &&
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? algorithmName) &&
                            Enum.TryParse(
                                algorithmName,
                                ignoreCase: true,
                                out IntrinsicFunctions.StringHashingAlgorithm algorithm):
                    return Handled(IntrinsicFunctions.StableStringHash(value, algorithm));

                default:
                    return NotRecognized;
            }
        }

        private static WellKnownMemberResult TryInvokeAreFeaturesEnabled(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out Version? version))
            {
                return Handled(IntrinsicFunctions.AreFeaturesEnabled(version));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeSubstringByAsciiChars(ref FunctionArguments args)
        {
            if (args.Count == 3 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int start) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(2), out int length))
            {
                return Handled(IntrinsicFunctions.SubstringByAsciiChars(value, start, length));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeCheckFeatureAvailability(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? featureName))
            {
                return Handled(IntrinsicFunctions.CheckFeatureAvailability(featureName));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeBitwiseOr(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out int left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int right))
            {
                return Handled(IntrinsicFunctions.BitwiseOr(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeBitwiseAnd(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out int left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int right))
            {
                return Handled(IntrinsicFunctions.BitwiseAnd(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeBitwiseXor(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out int left) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int right))
            {
                return Handled(IntrinsicFunctions.BitwiseXor(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeBitwiseNot(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out int value))
            {
                return Handled(IntrinsicFunctions.BitwiseNot(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeLeftShift(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out int value) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int count))
            {
                return Handled(IntrinsicFunctions.LeftShift(value, count));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeRightShift(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out int value) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int count))
            {
                return Handled(IntrinsicFunctions.RightShift(value, count));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeRightShiftUnsigned(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out int value) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int count))
            {
                return Handled(IntrinsicFunctions.RightShiftUnsigned(value, count));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeNormalizeDirectory(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? directory))
            {
                return Handled(IntrinsicFunctions.NormalizeDirectory(directory));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsOSPlatform(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? platform))
            {
                return Handled(IntrinsicFunctions.IsOSPlatform(platform));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeFileExists(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? file))
            {
                return Handled(IntrinsicFunctions.FileExists(file));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeDirectoryExists(ref FunctionArguments args)
        {
            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? directory))
            {
                return Handled(IntrinsicFunctions.DirectoryExists(directory));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeRegisterBuildCheck(ref FunctionArguments args, ref readonly ExecutionContext context)
        {
            string projectPath = context.Properties.GetProperty("MSBuildProjectFullPath")?.EvaluatedValue ?? string.Empty;
            LoggingContext? loggingContext = context.LoggingContext;
            Assumed.NotNull(
                loggingContext,
                $"The logging context is missed. {nameof(IntrinsicFunctions.RegisterBuildCheck)} can not be invoked.");

            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? checkName))
            {
                return Handled(IntrinsicFunctions.RegisterBuildCheck(projectPath, checkName, loggingContext));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeMakeRelative(ref FunctionArguments args)
        {
            if (args.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? basePath) &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out string? path))
            {
                return Handled(IntrinsicFunctions.MakeRelative(basePath, path));
            }

            return NotRecognized;
        }
    }
}
