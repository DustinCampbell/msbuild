// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class OSPlatformHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length == 6 && name.Equals(nameof(OSPlatform.Create), StringComparison.OrdinalIgnoreCase)
                ? TryInvokeCreate(ref arguments)
                : NotRecognized;

        internal WellKnownMemberResult TryGetStatic(StringSegment name)
            => name.Length switch
            {
                3 when name.Equals(nameof(OSPlatform.OSX), StringComparison.OrdinalIgnoreCase)
                    => Handled(OSPlatform.OSX),
                5 when name.Equals(nameof(OSPlatform.Linux), StringComparison.OrdinalIgnoreCase)
                    => Handled(OSPlatform.Linux),
                7 when name.Equals(nameof(OSPlatform.Windows), StringComparison.OrdinalIgnoreCase)
                    => Handled(OSPlatform.Windows),
#if NET
                7 when name.Equals(nameof(OSPlatform.FreeBSD), StringComparison.OrdinalIgnoreCase)
                    => Handled(OSPlatform.FreeBSD),
#endif

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryInvokeInstance(OSPlatform value, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                8 when name.Equals(nameof(OSPlatform.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(value, ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeCreate(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? osPlatform))
            {
                return Handled(OSPlatform.Create(osPlatform!));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToString(OSPlatform value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(value.ToString());
            }

            return NotRecognized;
        }
    }
}
