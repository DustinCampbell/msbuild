// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

#if NET
using OperatingSystem = System.OperatingSystem;
#else
using OperatingSystem = Microsoft.Build.Framework.OperatingSystem;
#endif

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class OperatingSystemHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(OperatingSystem.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string platform]
                    ? Invoked(OperatingSystem.IsOSPlatform(platform))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsOSPlatformVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokePlatformVersionAtLeast(args);
            }

            if (methodName.Equals(nameof(OperatingSystem.IsFreeBSD), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsFreeBSD()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsFreeBSDVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeVersionAtLeast(args, OperatingSystem.IsFreeBSDVersionAtLeast);
            }

            if (methodName.Equals(nameof(OperatingSystem.IsLinux), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsLinux()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsMacOS), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsMacOS()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsMacOSVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeVersionAtLeast(args, OperatingSystem.IsMacOSVersionAtLeast);
            }

            if (methodName.Equals(nameof(OperatingSystem.IsWindows), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsWindows()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsWindowsVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeVersionAtLeast(args, OperatingSystem.IsWindowsVersionAtLeast);
            }

#if NET
            if (methodName.Equals(nameof(OperatingSystem.IsAndroid), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsAndroid()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsAndroidVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeVersionAtLeast(args, OperatingSystem.IsAndroidVersionAtLeast);
            }

            if (methodName.Equals(nameof(OperatingSystem.IsBrowser), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsBrowser()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsIOS), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsIOS()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsIOSVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeVersionAtLeast(args, OperatingSystem.IsIOSVersionAtLeast);
            }

            if (methodName.Equals(nameof(OperatingSystem.IsMacCatalyst), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsMacCatalyst()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsMacCatalystVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeVersionAtLeast(args, OperatingSystem.IsMacCatalystVersionAtLeast);
            }

            if (methodName.Equals(nameof(OperatingSystem.IsTvOS), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsTvOS()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsTvOSVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeVersionAtLeast(args, OperatingSystem.IsTvOSVersionAtLeast);
            }

            if (methodName.Equals(nameof(OperatingSystem.IsWasi), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsWasi()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsWatchOS), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(OperatingSystem.IsWatchOS()) : NotHandled;
            }

            if (methodName.Equals(nameof(OperatingSystem.IsWatchOSVersionAtLeast), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeVersionAtLeast(args, OperatingSystem.IsWatchOSVersionAtLeast);
            }
#endif

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokePlatformVersionAtLeast(object?[] args)
        {
            if (!args.TryGetArg(0, out string? platform))
            {
                return NotHandled;
            }

            return args is [_, var majorValue, var minorValue, var buildValue, var revisionValue] &&
                   ArgumentParser.TryConvertToInt(majorValue, out int major) &&
                   ArgumentParser.TryConvertToInt(minorValue, out int minor) &&
                   ArgumentParser.TryConvertToInt(buildValue, out int build) &&
                   ArgumentParser.TryConvertToInt(revisionValue, out int revision)
                ? Invoked(OperatingSystem.IsOSPlatformVersionAtLeast(platform, major, minor, build, revision))
                : NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeVersionAtLeast(object?[] args, Func<int, int, int, bool> versionCheck)
            => args.Length == 3 &&
               args.TryGetArg(0, out int major) &&
               args.TryGetArg(1, out int minor) &&
               args.TryGetArg(2, out int build)
                ? Invoked(versionCheck(major, minor, build))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeVersionAtLeast(object?[] args, Func<int, int, int, int, bool> versionCheck)
            => args.Length == 4 &&
               args.TryGetArg(0, out int major) &&
               args.TryGetArg(1, out int minor) &&
               args.TryGetArg(2, out int build) &&
               args.TryGetArg(3, out int revision)
                ? Invoked(versionCheck(major, minor, build, revision))
                : NotHandled;
    }

    private sealed class OSPlatformHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(OSPlatform.Create), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string value]
                    ? Invoked(OSPlatform.Create(value))
                    : NotHandled;
            }

            if (args is not [])
            {
                return TryInvokeOperator(methodName, args);
            }

            if (methodName.Equals(nameof(OSPlatform.Linux), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(OSPlatform.Linux);
            }

            if (methodName.Equals(nameof(OSPlatform.OSX), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(OSPlatform.OSX);
            }

            if (methodName.Equals(nameof(OSPlatform.Windows), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(OSPlatform.Windows);
            }

#if NET
            if (methodName.Equals(nameof(OSPlatform.FreeBSD), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(OSPlatform.FreeBSD);
            }
#endif

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, OSPlatform platform, object?[] args)
        {
            if (methodName.Equals(nameof(OSPlatform.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [OSPlatform other] => Invoked(platform.Equals(other)),
                    [var other] => Invoked(platform.Equals(other)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(OSPlatform.GetHashCode), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(platform.GetHashCode()) : NotHandled;
            }

            if (methodName.Equals(nameof(OSPlatform.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(platform.ToString()) : NotHandled;
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeOperator(string methodName, object?[] args)
        {
            if (args is not [OSPlatform left, OSPlatform right])
            {
                return NotHandled;
            }

            if (methodName.Equals("op_Equality", StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(left == right);
            }

            if (methodName.Equals("op_Inequality", StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(left != right);
            }

            return NotHandled;
        }
    }

    private sealed class RuntimeInformationHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(RuntimeInformation.IsOSPlatform), StringComparison.OrdinalIgnoreCase))
            {
                return args is [OSPlatform platform]
                    ? Invoked(RuntimeInformation.IsOSPlatform(platform))
                    : NotHandled;
            }

            if (args is not [])
            {
                return NotHandled;
            }

            if (methodName.Equals(nameof(RuntimeInformation.FrameworkDescription), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(RuntimeInformation.FrameworkDescription);
            }

            if (methodName.Equals(nameof(RuntimeInformation.OSArchitecture), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(RuntimeInformation.OSArchitecture);
            }

            if (methodName.Equals(nameof(RuntimeInformation.OSDescription), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(RuntimeInformation.OSDescription);
            }

            if (methodName.Equals(nameof(RuntimeInformation.ProcessArchitecture), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(RuntimeInformation.ProcessArchitecture);
            }

#if NET
            if (methodName.Equals(nameof(RuntimeInformation.RuntimeIdentifier), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(RuntimeInformation.RuntimeIdentifier);
            }
#endif

            return NotHandled;
        }
    }
}
