// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class DateTimeHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
        {
            if (args is [var ticks] && ArgumentParser.TryConvertToLong(ticks, out long convertedTicks))
            {
                return Invoked(new DateTime(convertedTicks));
            }

            if (args is [var ticks2, var kind] &&
                ArgumentParser.TryConvertToLong(ticks2, out long convertedTicks2) &&
                ArgumentParser.TryConvertToEnum(kind, out DateTimeKind convertedKind))
            {
                return Invoked(new DateTime(convertedTicks2, convertedKind));
            }

            Span<int> parts = stackalloc int[8];
            if (args.Length >= 1 &&
                ArgumentParser.TryConvertToEnum(args[^1], out convertedKind) &&
                TryGetInt32Arguments(args, args.Length - 1, parts))
            {
                return (args.Length - 1) switch
                {
                    6 => Invoked(new DateTime(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], convertedKind)),
                    7 => Invoked(new DateTime(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], convertedKind)),
#if NET
                    8 => Invoked(new DateTime(
                            parts[0],
                            parts[1],
                            parts[2],
                            parts[3],
                            parts[4],
                            parts[5],
                            parts[6],
                            parts[7],
                            convertedKind)),
#endif
                    _ => NotHandled,
                };
            }

            if (!TryGetInt32Arguments(args, args.Length, parts))
            {
                return NotHandled;
            }

            return args.Length switch
            {
                3 => Invoked(new DateTime(parts[0], parts[1], parts[2])),
                6 => Invoked(new DateTime(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5])),
                7 => Invoked(new DateTime(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6])),
#if NET
                8 => Invoked(new DateTime(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], parts[7])),
#endif
                _ => NotHandled,
            };
        }

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(DateTime.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParse(args);
            }

            if (methodName.Equals(nameof(DateTime.ParseExact), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParseExact(args);
            }

            if (TryInvokeOperator(methodName, args) is { Status: WellKnownFunctionStatus.Invoked } operatorResult)
            {
                return operatorResult;
            }

            if (TryGetDateTimeArguments(args, out DateTime left, out DateTime right))
            {
                if (methodName.Equals(nameof(DateTime.Compare), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(DateTime.Compare(left, right));
                }

                if (methodName.Equals(nameof(DateTime.Equals), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(DateTime.Equals(left, right));
                }
            }

            if (args is [var value])
            {
                if (methodName.Equals(nameof(DateTime.FromBinary), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToLong(value, out long binary))
                {
                    return Invoked(DateTime.FromBinary(binary));
                }

                if (methodName.Equals(nameof(DateTime.FromFileTime), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToLong(value, out long fileTime))
                {
                    return Invoked(DateTime.FromFileTime(fileTime));
                }

                if (methodName.Equals(nameof(DateTime.FromFileTimeUtc), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToLong(value, out long fileTimeUtc))
                {
                    return Invoked(DateTime.FromFileTimeUtc(fileTimeUtc));
                }

                if (methodName.Equals(nameof(DateTime.FromOADate), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToDouble(value, out double oaDate))
                {
                    return Invoked(DateTime.FromOADate(oaDate));
                }

                if (methodName.Equals(nameof(DateTime.IsLeapYear), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToInt(value, out int year))
                {
                    return Invoked(DateTime.IsLeapYear(year));
                }
            }

            if (args is [var yearValue, var monthValue] &&
                methodName.Equals(nameof(DateTime.DaysInMonth), StringComparison.OrdinalIgnoreCase) &&
                ArgumentParser.TryConvertToInt(yearValue, out int convertedYear) &&
                ArgumentParser.TryConvertToInt(monthValue, out int convertedMonth))
            {
                return Invoked(DateTime.DaysInMonth(convertedYear, convertedMonth));
            }

            if (args is [var dateTime, var kind] &&
                methodName.Equals(nameof(DateTime.SpecifyKind), StringComparison.OrdinalIgnoreCase) &&
                TryConvertToDateTime(dateTime, out DateTime convertedDateTime) &&
                ArgumentParser.TryConvertToEnum(kind, out DateTimeKind convertedKind))
            {
                return Invoked(DateTime.SpecifyKind(convertedDateTime, convertedKind));
            }

            if (args is not [])
            {
                return NotHandled;
            }

            return methodName switch
            {
                var name when name.Equals(nameof(DateTime.MaxValue), StringComparison.OrdinalIgnoreCase) => Invoked(DateTime.MaxValue),
                var name when name.Equals(nameof(DateTime.MinValue), StringComparison.OrdinalIgnoreCase) => Invoked(DateTime.MinValue),
                var name when name.Equals(nameof(DateTime.Now), StringComparison.OrdinalIgnoreCase) => Invoked(DateTime.Now),
                var name when name.Equals(nameof(DateTime.Today), StringComparison.OrdinalIgnoreCase) => Invoked(DateTime.Today),
                var name when name.Equals(nameof(DateTime.UtcNow), StringComparison.OrdinalIgnoreCase) => Invoked(DateTime.UtcNow),
#if NET
                var name when name.Equals(nameof(DateTime.UnixEpoch), StringComparison.OrdinalIgnoreCase) => Invoked(DateTime.UnixEpoch),
#endif
                _ => NotHandled,
            };
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, DateTime value, object?[] args)
        {
            if (args is [] && TryInvokeProperty(methodName, value) is { Status: WellKnownFunctionStatus.Invoked } propertyResult)
            {
                return propertyResult;
            }

            if (methodName.Equals(nameof(DateTime.CompareTo), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [DateTime compareValue] => Invoked(value.CompareTo(compareValue)),
                    [var compareValue] => Invoked(value.CompareTo(compareValue)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(DateTime.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [DateTime equalsValue] => Invoked(value.Equals(equalsValue)),
                    [var equalsValue] => Invoked(value.Equals(equalsValue)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(DateTime.GetDateTimeFormats), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [] => Invoked(value.GetDateTimeFormats()),
                    [IFormatProvider provider] => Invoked(value.GetDateTimeFormats(provider)),
                    [null] => Invoked(value.GetDateTimeFormats(provider: null)),
                    [var format] when ArgumentParser.TryConvertToChar(format, out char convertedFormat)
                        => Invoked(value.GetDateTimeFormats(convertedFormat)),
                    [var format, IFormatProvider provider] when ArgumentParser.TryConvertToChar(format, out char convertedFormat)
                        => Invoked(value.GetDateTimeFormats(convertedFormat, provider)),
                    [var format, null] when ArgumentParser.TryConvertToChar(format, out char convertedFormat)
                        => Invoked(value.GetDateTimeFormats(convertedFormat, provider: null)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(DateTime.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [] => Invoked(value.ToString()),
                    [string format] => Invoked(value.ToString(format)),
                    [IFormatProvider provider] => Invoked(value.ToString(provider)),
                    [null] => Invoked(value.ToString(provider: null)),
                    [string format, IFormatProvider provider] => Invoked(value.ToString(format, provider)),
                    [string format, null] => Invoked(value.ToString(format, provider: null)),
                    _ => NotHandled,
                };
            }

            if (args is [])
            {
                return methodName switch
                {
                    var name when name.Equals(nameof(DateTime.GetHashCode), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.GetHashCode()),
                    var name when name.Equals(nameof(DateTime.GetTypeCode), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.GetTypeCode()),
                    var name when name.Equals(nameof(DateTime.IsDaylightSavingTime), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.IsDaylightSavingTime()),
                    var name when name.Equals(nameof(DateTime.ToBinary), StringComparison.OrdinalIgnoreCase) => Invoked(value.ToBinary()),
                    var name when name.Equals(nameof(DateTime.ToFileTime), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToFileTime()),
                    var name when name.Equals(nameof(DateTime.ToFileTimeUtc), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToFileTimeUtc()),
                    var name when name.Equals(nameof(DateTime.ToLocalTime), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToLocalTime()),
                    var name when name.Equals(nameof(DateTime.ToLongDateString), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToLongDateString()),
                    var name when name.Equals(nameof(DateTime.ToLongTimeString), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToLongTimeString()),
                    var name when name.Equals(nameof(DateTime.ToOADate), StringComparison.OrdinalIgnoreCase) => Invoked(value.ToOADate()),
                    var name when name.Equals(nameof(DateTime.ToShortDateString), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToShortDateString()),
                    var name when name.Equals(nameof(DateTime.ToShortTimeString), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToShortTimeString()),
                    var name when name.Equals(nameof(DateTime.ToUniversalTime), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToUniversalTime()),
                    _ => NotHandled,
                };
            }

            if (args is [var argument])
            {
                if (methodName.Equals(nameof(DateTime.Add), StringComparison.OrdinalIgnoreCase) &&
                    TryGetTimeSpan(argument, out TimeSpan timeSpan))
                {
                    return Invoked(value.Add(timeSpan));
                }

                if (methodName.Equals(nameof(DateTime.AddMonths), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToInt(argument, out int months))
                {
                    return Invoked(value.AddMonths(months));
                }

                if (methodName.Equals(nameof(DateTime.AddTicks), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToLong(argument, out long ticks))
                {
                    return Invoked(value.AddTicks(ticks));
                }

                if (methodName.Equals(nameof(DateTime.AddYears), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToInt(argument, out int years))
                {
                    return Invoked(value.AddYears(years));
                }

                if (methodName.Equals(nameof(DateTime.Subtract), StringComparison.OrdinalIgnoreCase))
                {
                    if (TryGetTimeSpan(argument, out TimeSpan subtractTimeSpan))
                    {
                        return Invoked(value.Subtract(subtractTimeSpan));
                    }

                    if (TryConvertToDateTime(argument, out DateTime subtractDateTime))
                    {
                        return Invoked(value.Subtract(subtractDateTime));
                    }
                }

                if (ArgumentParser.TryConvertToDouble(argument, out double convertedValue))
                {
                    return methodName switch
                    {
                        var name when name.Equals(nameof(DateTime.AddDays), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddDays(convertedValue)),
                        var name when name.Equals(nameof(DateTime.AddHours), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddHours(convertedValue)),
#if NET
                        var name when name.Equals(nameof(DateTime.AddMicroseconds), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddMicroseconds(convertedValue)),
#endif
                        var name when name.Equals(nameof(DateTime.AddMilliseconds), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddMilliseconds(convertedValue)),
                        var name when name.Equals(nameof(DateTime.AddMinutes), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddMinutes(convertedValue)),
                        var name when name.Equals(nameof(DateTime.AddSeconds), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddSeconds(convertedValue)),
                        _ => NotHandled,
                    };
                }
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeOperator(string methodName, object?[] args)
        {
            if (args is [var leftValue, var rightValue] && TryConvertToDateTime(leftValue, out DateTime left))
            {
                if (TryConvertToDateTime(rightValue, out DateTime right))
                {
                    return methodName switch
                    {
                        var name when name.Equals("op_Equality", StringComparison.OrdinalIgnoreCase) => Invoked(left == right),
                        var name when name.Equals("op_GreaterThan", StringComparison.OrdinalIgnoreCase) => Invoked(left > right),
                        var name when name.Equals("op_GreaterThanOrEqual", StringComparison.OrdinalIgnoreCase) => Invoked(left >= right),
                        var name when name.Equals("op_Inequality", StringComparison.OrdinalIgnoreCase) => Invoked(left != right),
                        var name when name.Equals("op_LessThan", StringComparison.OrdinalIgnoreCase) => Invoked(left < right),
                        var name when name.Equals("op_LessThanOrEqual", StringComparison.OrdinalIgnoreCase) => Invoked(left <= right),
                        var name when name.Equals("op_Subtraction", StringComparison.OrdinalIgnoreCase) => Invoked(left - right),
                        _ => NotHandled,
                    };
                }

                if (TryGetTimeSpan(rightValue, out TimeSpan timeSpan))
                {
                    if (methodName.Equals("op_Addition", StringComparison.OrdinalIgnoreCase))
                    {
                        return Invoked(left + timeSpan);
                    }

                    if (methodName.Equals("op_Subtraction", StringComparison.OrdinalIgnoreCase))
                    {
                        return Invoked(left - timeSpan);
                    }
                }
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeParse(object?[] args)
            => args switch
            {
                [string value] => Invoked(DateTime.Parse(value)),
                [string value, IFormatProvider provider] => Invoked(DateTime.Parse(value, provider)),
                [string value, null] => Invoked(DateTime.Parse(value, provider: null)),
                [string value, var provider, var styles]
                    when TryGetFormatProvider(provider, out IFormatProvider? convertedProvider) &&
                         ArgumentParser.TryConvertToEnum(styles, out DateTimeStyles convertedStyles)
                    => Invoked(DateTime.Parse(value, convertedProvider, convertedStyles)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeParseExact(object?[] args)
        {
            if (args is [string input, string format, var provider] &&
                TryGetFormatProvider(provider, out IFormatProvider? convertedProvider))
            {
                return Invoked(DateTime.ParseExact(input, format, convertedProvider));
            }

            if (args is [string input2, string format2, var provider2, var styles] &&
                TryGetFormatProvider(provider2, out convertedProvider) &&
                ArgumentParser.TryConvertToEnum(styles, out DateTimeStyles convertedStyles))
            {
                return Invoked(DateTime.ParseExact(input2, format2, convertedProvider, convertedStyles));
            }

            if (args is [string input3, string[] formats, var provider3, var styles2] &&
                TryGetFormatProvider(provider3, out convertedProvider) &&
                ArgumentParser.TryConvertToEnum(styles2, out DateTimeStyles convertedStyles2))
            {
                return Invoked(DateTime.ParseExact(input3, formats, convertedProvider, convertedStyles2));
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeProperty(string methodName, DateTime value)
            => methodName switch
            {
                var name when name.Equals(nameof(DateTime.Date), StringComparison.OrdinalIgnoreCase) => Invoked(value.Date),
                var name when name.Equals(nameof(DateTime.Day), StringComparison.OrdinalIgnoreCase) => Invoked(value.Day),
                var name when name.Equals(nameof(DateTime.DayOfWeek), StringComparison.OrdinalIgnoreCase) => Invoked(value.DayOfWeek),
                var name when name.Equals(nameof(DateTime.DayOfYear), StringComparison.OrdinalIgnoreCase) => Invoked(value.DayOfYear),
                var name when name.Equals(nameof(DateTime.Hour), StringComparison.OrdinalIgnoreCase) => Invoked(value.Hour),
                var name when name.Equals(nameof(DateTime.Kind), StringComparison.OrdinalIgnoreCase) => Invoked(value.Kind),
                var name when name.Equals(nameof(DateTime.Millisecond), StringComparison.OrdinalIgnoreCase) => Invoked(value.Millisecond),
                var name when name.Equals(nameof(DateTime.Minute), StringComparison.OrdinalIgnoreCase) => Invoked(value.Minute),
                var name when name.Equals(nameof(DateTime.Month), StringComparison.OrdinalIgnoreCase) => Invoked(value.Month),
                var name when name.Equals(nameof(DateTime.Second), StringComparison.OrdinalIgnoreCase) => Invoked(value.Second),
                var name when name.Equals(nameof(DateTime.Ticks), StringComparison.OrdinalIgnoreCase) => Invoked(value.Ticks),
                var name when name.Equals(nameof(DateTime.TimeOfDay), StringComparison.OrdinalIgnoreCase) => Invoked(value.TimeOfDay),
                var name when name.Equals(nameof(DateTime.Year), StringComparison.OrdinalIgnoreCase) => Invoked(value.Year),
#if NET
                var name when name.Equals(nameof(DateTime.Microsecond), StringComparison.OrdinalIgnoreCase) => Invoked(value.Microsecond),
                var name when name.Equals(nameof(DateTime.Nanosecond), StringComparison.OrdinalIgnoreCase) => Invoked(value.Nanosecond),
#endif
                _ => NotHandled,
            };

        private static bool TryConvertToDateTime(object? value, out DateTime result)
        {
            if (value is DateTime dateTime)
            {
                result = dateTime;
                return true;
            }

            if (value is string text &&
                DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryGetDateTimeArguments(object?[] args, out DateTime left, out DateTime right)
        {
            if (args is [var leftValue, var rightValue] &&
                TryConvertToDateTime(leftValue, out left) &&
                TryConvertToDateTime(rightValue, out right))
            {
                return true;
            }

            left = right = default;
            return false;
        }

        private static bool TryGetFormatProvider(object? value, out IFormatProvider? provider)
        {
            provider = value as IFormatProvider;
            return value is null || provider is not null;
        }

        private static bool TryGetInt32Arguments(object?[] args, int count, Span<int> result)
        {
            if (count > args.Length || count > result.Length)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!ArgumentParser.TryConvertToInt(args[i], out result[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetTimeSpan(object? value, out TimeSpan result)
        {
            if (value is TimeSpan timeSpan)
            {
                result = timeSpan;
                return true;
            }

            result = default;
            return false;
        }
    }
}
