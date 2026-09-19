// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class RegexHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(
            StringSegment name,
            ref FunctionArguments arguments)
        {
            switch (name.Length)
            {
                case 5 when name.Equals(nameof(Regex.Match), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeMatch(ref arguments);

                case 7 when name.Equals(nameof(Regex.IsMatch), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeIsMatch(ref arguments);

                case 7 when name.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeReplace(ref arguments);
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsMatch(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out string? pattern))
            {
                return Handled(Regex.IsMatch(input!, pattern!));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out pattern) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(2), out RegexOptions options))
            {
                return Handled(Regex.IsMatch(input!, pattern!, options));
            }

            if (arguments.Count == 4 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out pattern) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(2), out options) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(3), out TimeSpan matchTimeout))
            {
                return Handled(Regex.IsMatch(input!, pattern!, options, matchTimeout));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeMatch(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out string? pattern))
            {
                return Handled(Regex.Match(input!, pattern!));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out pattern) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(2), out RegexOptions options))
            {
                return Handled(Regex.Match(input!, pattern!, options));
            }

            if (arguments.Count == 4 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out pattern) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(2), out options) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(3), out TimeSpan matchTimeout))
            {
                return Handled(Regex.Match(input!, pattern!, options, matchTimeout));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeReplace(ref FunctionArguments arguments)
        {
            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out string? pattern) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(2), out string? replacement))
            {
                return Handled(Regex.Replace(input!, pattern!, replacement!));
            }

            if (arguments.Count == 4 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out pattern) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(2), out replacement) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(3), out RegexOptions options))
            {
                return Handled(Regex.Replace(input!, pattern!, replacement!, options));
            }

            if (arguments.Count == 5 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out input) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out pattern) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(2), out replacement) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(3), out options) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(4), out TimeSpan matchTimeout))
            {
                return Handled(Regex.Replace(input!, pattern!, replacement!, options, matchTimeout));
            }

            return NotRecognized;
        }
    }

    private sealed class RegexGroupHandler
    {
        internal WellKnownMemberResult TryGetInstance(Group receiver, StringSegment name)
            => name.Length == 5 && name.Equals(nameof(Group.Value), StringComparison.OrdinalIgnoreCase)
                ? Handled(receiver.Value)
                : NotRecognized;
    }

    private sealed class RegexGroupCollectionHandler
    {
        internal WellKnownMemberResult TryInvokeInstance(GroupCollection gc, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                8 when name.Equals("get_Item", StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetItem(gc, ref arguments),

                _ => NotRecognized,
            };

        private WellKnownMemberResult TryInvokeGetItem(GroupCollection gc, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? groupName))
            {
                return Handled(gc[groupName]);
            }

            return NotRecognized;
        }
    }

    private sealed class RegexMatchHandler
    {
        internal WellKnownMemberResult TryGetInstance(Match receiver, StringSegment name)
            => name.Length == 6 && name.Equals(nameof(Match.Groups), StringComparison.OrdinalIgnoreCase)
                ? Handled(receiver.Groups)
                : NotRecognized;
    }
}
