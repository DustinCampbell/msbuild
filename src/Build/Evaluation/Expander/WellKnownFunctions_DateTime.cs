// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class DateTimeHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(DateTime.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string value] => Invoked(DateTime.Parse(value)),
                    [string value, IFormatProvider provider] => Invoked(DateTime.Parse(value, provider)),
                    [string value, IFormatProvider provider, var styles]
                        when ArgumentParser.TryConvertToEnum(styles, out System.Globalization.DateTimeStyles convertedStyles)
                        => Invoked(DateTime.Parse(value, provider, convertedStyles)),
                    _ => NotHandled,
                };
            }

            if (args is not [])
            {
                return NotHandled;
            }

            if (methodName.Equals(nameof(DateTime.MaxValue), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(DateTime.MaxValue);
            }

            if (methodName.Equals(nameof(DateTime.MinValue), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(DateTime.MinValue);
            }

            if (methodName.Equals(nameof(DateTime.Now), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(DateTime.Now);
            }

            if (methodName.Equals(nameof(DateTime.Today), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(DateTime.Today);
            }

            if (methodName.Equals(nameof(DateTime.UtcNow), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(DateTime.UtcNow);
            }

#if NET
            if (methodName.Equals(nameof(DateTime.UnixEpoch), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(DateTime.UnixEpoch);
            }
#endif

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, DateTime value, object?[] args)
        {
            if (args is [] && TryInvokeProperty(methodName, value) is { Status: WellKnownFunctionStatus.Invoked } propertyResult)
            {
                return propertyResult;
            }

            if (methodName.Equals(nameof(DateTime.GetHashCode), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(value.GetHashCode()) : NotHandled;
            }

            if (methodName.Equals(nameof(DateTime.GetTypeCode), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(value.GetTypeCode()) : NotHandled;
            }

            if (methodName.Equals(nameof(DateTime.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [] => Invoked(value.ToString()),
                    [string format] => Invoked(value.ToString(format)),
                    [IFormatProvider provider] => Invoked(value.ToString(provider)),
                    [string format, IFormatProvider provider] => Invoked(value.ToString(format, provider)),
                    _ => NotHandled,
                };
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeProperty(string methodName, DateTime value)
        {
            if (methodName.Equals(nameof(DateTime.Date), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Date);
            }

            if (methodName.Equals(nameof(DateTime.Day), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Day);
            }

            if (methodName.Equals(nameof(DateTime.DayOfWeek), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.DayOfWeek);
            }

            if (methodName.Equals(nameof(DateTime.DayOfYear), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.DayOfYear);
            }

            if (methodName.Equals(nameof(DateTime.Hour), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Hour);
            }

            if (methodName.Equals(nameof(DateTime.Kind), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Kind);
            }

            if (methodName.Equals(nameof(DateTime.Millisecond), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Millisecond);
            }

            if (methodName.Equals(nameof(DateTime.Minute), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Minute);
            }

            if (methodName.Equals(nameof(DateTime.Month), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Month);
            }

            if (methodName.Equals(nameof(DateTime.Second), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Second);
            }

            if (methodName.Equals(nameof(DateTime.Ticks), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Ticks);
            }

            if (methodName.Equals(nameof(DateTime.TimeOfDay), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.TimeOfDay);
            }

            if (methodName.Equals(nameof(DateTime.Year), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Year);
            }

#if NET
            if (methodName.Equals(nameof(DateTime.Microsecond), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Microsecond);
            }

            if (methodName.Equals(nameof(DateTime.Nanosecond), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Nanosecond);
            }
#endif

            return NotHandled;
        }
    }
}
