// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringComparerHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (args is [])
            {
                if (methodName.Equals(nameof(StringComparer.CurrentCulture), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(StringComparer.CurrentCulture);
                }

                if (methodName.Equals(nameof(StringComparer.CurrentCultureIgnoreCase), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(StringComparer.CurrentCultureIgnoreCase);
                }

                if (methodName.Equals(nameof(StringComparer.InvariantCulture), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(StringComparer.InvariantCulture);
                }

                if (methodName.Equals(nameof(StringComparer.InvariantCultureIgnoreCase), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(StringComparer.InvariantCultureIgnoreCase);
                }

                if (methodName.Equals(nameof(StringComparer.Ordinal), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(StringComparer.Ordinal);
                }

                if (methodName.Equals(nameof(StringComparer.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(StringComparer.OrdinalIgnoreCase);
                }
            }

            if (methodName.Equals(nameof(StringComparer.Create), StringComparison.OrdinalIgnoreCase))
            {
                return args is [CultureInfo culture, var ignoreCase]
                    && TryConvertToBoolean(ignoreCase, out bool convertedIgnoreCase)
                        ? Invoked(StringComparer.Create(culture, convertedIgnoreCase))
                        : NotHandled;
            }

#if NET
            if (methodName.Equals(nameof(StringComparer.FromComparison), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var comparison]
                    && ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                        ? Invoked(StringComparer.FromComparison(convertedComparison))
                        : NotHandled;
            }
#endif

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, StringComparer comparer, object?[] args)
        {
            if (methodName.Equals(nameof(StringComparer.Compare), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var left, var right] && left is null or string && right is null or string
                    ? Invoked(comparer.Compare((string?)left, (string?)right))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(StringComparer.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [var left, var right] when left is null or string && right is null or string
                        => Invoked(comparer.Equals((string?)left, (string?)right)),
                    [object other] => Invoked(comparer.Equals(other)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(StringComparer.GetHashCode), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string value] => Invoked(comparer.GetHashCode(value)),
                    [] => Invoked(comparer.GetHashCode()),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(StringComparer.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(comparer.ToString()) : NotHandled;
            }

            return NotHandled;
        }

        private static bool TryConvertToBoolean(object? value, out bool result)
        {
            if (value is bool boolean)
            {
                result = boolean;
                return true;
            }

            if (value is string text && bool.TryParse(text, out result))
            {
                return true;
            }

            result = default;
            return false;
        }
    }
}
