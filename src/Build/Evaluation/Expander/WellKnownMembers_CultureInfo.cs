// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class CultureInfoHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                14 when name.Equals(nameof(CultureInfo.GetCultureInfo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetCultureInfo(ref arguments),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryInvokeInstance(CultureInfo c, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                8 when name.Equals(nameof(CultureInfo.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(c, ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeGetCultureInfo(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? name))
            {
                return Handled(CultureInfo.GetCultureInfo(name!));
            }

            return NotRecognized;
        }

        private WellKnownMemberResult TryInvokeToString(CultureInfo c, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(c.ToString());
            }

            return NotRecognized;
        }
    }
}
