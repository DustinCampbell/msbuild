// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class CharHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments args)
            => name.Length switch
            {
                7 when name.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsDigit(ref args),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeIsDigit(ref FunctionArguments args)
        {
            switch (args.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out char value):
                    return Handled(char.IsDigit(value));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value) &&
                    FunctionArgumentCoercion.TryCoerce(args.GetValue(1), out int index):
                    return Handled(char.IsDigit(value, index));

                default:
                    return NotRecognized;
            }
        }
    }
}
