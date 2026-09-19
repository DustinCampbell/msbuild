// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#if NET
using OperatingSystem = System.OperatingSystem;
#else
using OperatingSystem = Microsoft.Build.Framework.OperatingSystem;
#endif

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class OperatingSystemHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                7 when name.Equals(nameof(OperatingSystem.IsLinux), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsLinux(ref arguments),
                7 when name.Equals(nameof(OperatingSystem.IsMacOS), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsMacOS(ref arguments),
                9 when name.Equals(nameof(OperatingSystem.IsFreeBSD), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsFreeBSD(ref arguments),
                9 when name.Equals(nameof(OperatingSystem.IsWindows), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsWindows(ref arguments),
                12 when name.Equals(nameof(OperatingSystem.IsOSPlatform), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsOSPlatform(ref arguments),
                21 when name.Equals(nameof(OperatingSystem.IsMacOSVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsMacVersionAtLeast(ref arguments),
                23 when name.Equals(nameof(OperatingSystem.IsFreeBSDVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsFreeBSDVersionAtLeast(ref arguments),
                23 when name.Equals(nameof(OperatingSystem.IsWindowsVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsWindowsVersionAtLeast(ref arguments),
                26 when name.Equals(nameof(OperatingSystem.IsOSPlatformVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsOSPlatformVersionAtLeast(ref arguments),

#if NET5_0_OR_GREATER
                5 when name.Equals(nameof(OperatingSystem.IsIOS), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsIOS(ref arguments),
                6 when name.Equals(nameof(OperatingSystem.IsTvOS), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsTvOS(ref arguments),
                9 when name.Equals(nameof(OperatingSystem.IsAndroid), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsAndroid(ref arguments),
                9 when name.Equals(nameof(OperatingSystem.IsWatchOS), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsWatchOS(ref arguments),
                13 when name.Equals(nameof(OperatingSystem.IsMacCatalyst), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsMacCatalyst(ref arguments),
                19 when name.Equals(nameof(OperatingSystem.IsIOSVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsIOSVersionAtLeast(ref arguments),
                20 when name.Equals(nameof(OperatingSystem.IsTvOSVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsTvOSVersionAtLeast(ref arguments),
                23 when name.Equals(nameof(OperatingSystem.IsAndroidVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsAndroidVersionAtLeast(ref arguments),
                23 when name.Equals(nameof(OperatingSystem.IsWatchOSVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsWatchOSVersionAtLeast(ref arguments),
                27 when name.Equals(nameof(OperatingSystem.IsMacCatalystVersionAtLeast), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsMacCatalystVersionAtLeast(ref arguments),
#endif

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeIsFreeBSD(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsFreeBSD());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsFreeBSDVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int major))
            {
                return Handled(OperatingSystem.IsFreeBSDVersionAtLeast(major));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int minor))
            {
                return Handled(OperatingSystem.IsFreeBSDVersionAtLeast(major, minor));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int build))
            {
                return Handled(OperatingSystem.IsFreeBSDVersionAtLeast(major, minor, build));
            }

            if (arguments.Count == 4 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out build) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(3), out int revision))
            {
                return Handled(OperatingSystem.IsFreeBSDVersionAtLeast(major, minor, build, revision));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsLinux(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsLinux());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsMacOS(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsMacOS());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsMacVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int major))
            {
                return Handled(OperatingSystem.IsMacOSVersionAtLeast(major));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int minor))
            {
                return Handled(OperatingSystem.IsMacOSVersionAtLeast(major, minor));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int build))
            {
                return Handled(OperatingSystem.IsMacOSVersionAtLeast(major, minor, build));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsOSPlatform(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 && FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? platform))
            {
                return Handled(OperatingSystem.IsOSPlatform(platform!));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsOSPlatformVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? platform) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int major))
            {
                return Handled(OperatingSystem.IsOSPlatformVersionAtLeast(platform!, major));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out platform) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int minor))
            {
                return Handled(OperatingSystem.IsOSPlatformVersionAtLeast(platform!, major, minor));
            }

            if (arguments.Count == 4 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out platform) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(3), out int build))
            {
                return Handled(OperatingSystem.IsOSPlatformVersionAtLeast(platform!, major, minor, build));
            }

            if (arguments.Count == 5 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out platform) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(3), out build) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(4), out int revision))
            {
                return Handled(OperatingSystem.IsOSPlatformVersionAtLeast(platform!, major, minor, build, revision));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsWindows(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsWindows());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsWindowsVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int major))
            {
                return Handled(OperatingSystem.IsWindowsVersionAtLeast(major));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int minor))
            {
                return Handled(OperatingSystem.IsWindowsVersionAtLeast(major, minor));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int build))
            {
                return Handled(OperatingSystem.IsWindowsVersionAtLeast(major, minor, build));
            }

            if (arguments.Count == 4 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out build) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(3), out int revision))
            {
                return Handled(OperatingSystem.IsWindowsVersionAtLeast(major, minor, build, revision));
            }

            return NotRecognized;
        }

#if NET5_0_OR_GREATER
        private static WellKnownMemberResult TryInvokeIsAndroid(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsAndroid());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsAndroidVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int major))
            {
                return Handled(OperatingSystem.IsAndroidVersionAtLeast(major));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int minor))
            {
                return Handled(OperatingSystem.IsAndroidVersionAtLeast(major, minor));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int build))
            {
                return Handled(OperatingSystem.IsAndroidVersionAtLeast(major, minor, build));
            }

            if (arguments.Count == 4 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out build) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(3), out int revision))
            {
                return Handled(OperatingSystem.IsAndroidVersionAtLeast(major, minor, build, revision));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsIOS(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsIOS());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsIOSVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int major))
            {
                return Handled(OperatingSystem.IsIOSVersionAtLeast(major));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int minor))
            {
                return Handled(OperatingSystem.IsIOSVersionAtLeast(major, minor));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int build))
            {
                return Handled(OperatingSystem.IsIOSVersionAtLeast(major, minor, build));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsMacCatalyst(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsMacCatalyst());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsMacCatalystVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int major))
            {
                return Handled(OperatingSystem.IsMacCatalystVersionAtLeast(major));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int minor))
            {
                return Handled(OperatingSystem.IsMacCatalystVersionAtLeast(major, minor));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int build))
            {
                return Handled(OperatingSystem.IsMacCatalystVersionAtLeast(major, minor, build));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsTvOS(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsTvOS());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsTvOSVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int major))
            {
                return Handled(OperatingSystem.IsTvOSVersionAtLeast(major));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int minor))
            {
                return Handled(OperatingSystem.IsTvOSVersionAtLeast(major, minor));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int build))
            {
                return Handled(OperatingSystem.IsTvOSVersionAtLeast(major, minor, build));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsWatchOS(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(OperatingSystem.IsWatchOS());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsWatchOSVersionAtLeast(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int major))
            {
                return Handled(OperatingSystem.IsWatchOSVersionAtLeast(major));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int minor))
            {
                return Handled(OperatingSystem.IsWatchOSVersionAtLeast(major, minor));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out major) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out minor) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(2), out int build))
            {
                return Handled(OperatingSystem.IsWatchOSVersionAtLeast(major, minor, build));
            }

            return NotRecognized;
        }
#endif
    }
}
