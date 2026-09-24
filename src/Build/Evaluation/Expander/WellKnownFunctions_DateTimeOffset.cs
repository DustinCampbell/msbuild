// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class DateTimeOffsetHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
        {
            if (args is [DateTime dateTime])
            {
                return Invoked(new DateTimeOffset(dateTime));
            }

            if (args is [DateTime dateTime2, TimeSpan offset])
            {
                return Invoked(new DateTimeOffset(dateTime2, offset));
            }

            if (args is [var ticks, TimeSpan offset2] &&
                ArgumentParser.TryConvertToLong(ticks, out long convertedTicks))
            {
                return Invoked(new DateTimeOffset(convertedTicks, offset2));
            }

            if (args is not [.., TimeSpan convertedOffset])
            {
                return NotHandled;
            }

            Span<int> parts = stackalloc int[8];
            int count = args.Length - 1;
            if (!TryGetInt32Arguments(args, count, parts))
            {
                return NotHandled;
            }

            return count switch
            {
                6 => Invoked(new DateTimeOffset(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], convertedOffset)),
                7 => Invoked(new DateTimeOffset(
                    parts[0],
                    parts[1],
                    parts[2],
                    parts[3],
                    parts[4],
                    parts[5],
                    parts[6],
                    convertedOffset)),
#if NET
                8 => Invoked(new DateTimeOffset(
                    parts[0],
                    parts[1],
                    parts[2],
                    parts[3],
                    parts[4],
                    parts[5],
                    parts[6],
                    parts[7],
                    convertedOffset)),
#endif
                _ => NotHandled,
            };
        }

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(DateTimeOffset.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParse(args);
            }

            if (methodName.Equals(nameof(DateTimeOffset.ParseExact), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParseExact(args);
            }

            if (TryInvokeOperator(methodName, args) is { Status: WellKnownFunctionStatus.Invoked } operatorResult)
            {
                return operatorResult;
            }

            if (TryGetDateTimeOffsetArguments(args, out DateTimeOffset left, out DateTimeOffset right))
            {
                if (methodName.Equals(nameof(DateTimeOffset.Compare), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(DateTimeOffset.Compare(left, right));
                }

                if (methodName.Equals(nameof(DateTimeOffset.Equals), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(DateTimeOffset.Equals(left, right));
                }
            }

            if (args is [var value] && ArgumentParser.TryConvertToLong(value, out long convertedValue))
            {
                return methodName switch
                {
                    var name when name.Equals(nameof(DateTimeOffset.FromFileTime), StringComparison.OrdinalIgnoreCase)
                        => Invoked(DateTimeOffset.FromFileTime(convertedValue)),
                    var name when name.Equals(nameof(DateTimeOffset.FromUnixTimeMilliseconds), StringComparison.OrdinalIgnoreCase)
                        => Invoked(DateTimeOffset.FromUnixTimeMilliseconds(convertedValue)),
                    var name when name.Equals(nameof(DateTimeOffset.FromUnixTimeSeconds), StringComparison.OrdinalIgnoreCase)
                        => Invoked(DateTimeOffset.FromUnixTimeSeconds(convertedValue)),
                    _ => NotHandled,
                };
            }

            if (args is not [])
            {
                return NotHandled;
            }

            return methodName switch
            {
                var name when name.Equals(nameof(DateTimeOffset.MaxValue), StringComparison.OrdinalIgnoreCase)
                    => Invoked(DateTimeOffset.MaxValue),
                var name when name.Equals(nameof(DateTimeOffset.MinValue), StringComparison.OrdinalIgnoreCase)
                    => Invoked(DateTimeOffset.MinValue),
                var name when name.Equals(nameof(DateTimeOffset.Now), StringComparison.OrdinalIgnoreCase)
                    => Invoked(DateTimeOffset.Now),
                var name when name.Equals(nameof(DateTimeOffset.UtcNow), StringComparison.OrdinalIgnoreCase)
                    => Invoked(DateTimeOffset.UtcNow),
#if NET
                var name when name.Equals(nameof(DateTimeOffset.UnixEpoch), StringComparison.OrdinalIgnoreCase)
                    => Invoked(DateTimeOffset.UnixEpoch),
#endif
                _ => NotHandled,
            };
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, DateTimeOffset value, object?[] args)
        {
            if (args is [] && TryInvokeProperty(methodName, value) is { Status: WellKnownFunctionStatus.Invoked } propertyResult)
            {
                return propertyResult;
            }

            if (methodName.Equals(nameof(DateTimeOffset.CompareTo), StringComparison.OrdinalIgnoreCase))
            {
                return args is [DateTimeOffset other] ? Invoked(value.CompareTo(other)) : NotHandled;
            }

            if (methodName.Equals(nameof(DateTimeOffset.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [DateTimeOffset other] => Invoked(value.Equals(other)),
                    [var other] => Invoked(value.Equals(other)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(DateTimeOffset.EqualsExact), StringComparison.OrdinalIgnoreCase))
            {
                return args is [DateTimeOffset other] ? Invoked(value.EqualsExact(other)) : NotHandled;
            }

            if (methodName.Equals(nameof(DateTimeOffset.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [] => Invoked(value.ToString()),
                    [string format] => Invoked(value.ToString(format)),
                    [IFormatProvider provider] => Invoked(value.ToString(provider)),
                    [null] => Invoked(value.ToString((IFormatProvider?)null)),
                    [string format, IFormatProvider provider] => Invoked(value.ToString(format, provider)),
                    [string format, null] => Invoked(value.ToString(format, formatProvider: null)),
                    _ => NotHandled,
                };
            }

            if (args is [])
            {
                return methodName switch
                {
                    var name when name.Equals(nameof(DateTimeOffset.GetHashCode), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.GetHashCode()),
                    var name when name.Equals(nameof(DateTimeOffset.ToFileTime), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToFileTime()),
                    var name when name.Equals(nameof(DateTimeOffset.ToLocalTime), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToLocalTime()),
                    var name when name.Equals(nameof(DateTimeOffset.ToUniversalTime), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToUniversalTime()),
                    var name when name.Equals(nameof(DateTimeOffset.ToUnixTimeMilliseconds), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToUnixTimeMilliseconds()),
                    var name when name.Equals(nameof(DateTimeOffset.ToUnixTimeSeconds), StringComparison.OrdinalIgnoreCase)
                        => Invoked(value.ToUnixTimeSeconds()),
                    _ => NotHandled,
                };
            }

            if (args is [var argument])
            {
                if (methodName.Equals(nameof(DateTimeOffset.Add), StringComparison.OrdinalIgnoreCase) &&
                    TryGetTimeSpan(argument, out TimeSpan timeSpan))
                {
                    return Invoked(value.Add(timeSpan));
                }

                if (methodName.Equals(nameof(DateTimeOffset.AddMonths), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToInt(argument, out int months))
                {
                    return Invoked(value.AddMonths(months));
                }

                if (methodName.Equals(nameof(DateTimeOffset.AddTicks), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToLong(argument, out long ticks))
                {
                    return Invoked(value.AddTicks(ticks));
                }

                if (methodName.Equals(nameof(DateTimeOffset.AddYears), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToInt(argument, out int years))
                {
                    return Invoked(value.AddYears(years));
                }

                if (methodName.Equals(nameof(DateTimeOffset.Subtract), StringComparison.OrdinalIgnoreCase))
                {
                    if (TryGetTimeSpan(argument, out TimeSpan subtractTimeSpan))
                    {
                        return Invoked(value.Subtract(subtractTimeSpan));
                    }

                    if (argument is DateTimeOffset subtractDateTimeOffset)
                    {
                        return Invoked(value.Subtract(subtractDateTimeOffset));
                    }
                }

                if (methodName.Equals(nameof(DateTimeOffset.ToOffset), StringComparison.OrdinalIgnoreCase) &&
                    TryGetTimeSpan(argument, out TimeSpan offset))
                {
                    return Invoked(value.ToOffset(offset));
                }

                if (ArgumentParser.TryConvertToDouble(argument, out double convertedValue))
                {
                    return methodName switch
                    {
                        var name when name.Equals(nameof(DateTimeOffset.AddDays), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddDays(convertedValue)),
                        var name when name.Equals(nameof(DateTimeOffset.AddHours), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddHours(convertedValue)),
#if NET
                        var name when name.Equals(nameof(DateTimeOffset.AddMicroseconds), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddMicroseconds(convertedValue)),
#endif
                        var name when name.Equals(nameof(DateTimeOffset.AddMilliseconds), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddMilliseconds(convertedValue)),
                        var name when name.Equals(nameof(DateTimeOffset.AddMinutes), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddMinutes(convertedValue)),
                        var name when name.Equals(nameof(DateTimeOffset.AddSeconds), StringComparison.OrdinalIgnoreCase)
                            => Invoked(value.AddSeconds(convertedValue)),
                        _ => NotHandled,
                    };
                }
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeOperator(string methodName, object?[] args)
        {
            if (args is [DateTime dateTime] && methodName.Equals("op_Implicit", StringComparison.OrdinalIgnoreCase))
            {
                return Invoked((DateTimeOffset)dateTime);
            }

            if (TryGetDateTimeOffsetArguments(args, out DateTimeOffset left, out DateTimeOffset right))
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

            if (args is [DateTimeOffset dateTimeOffset, TimeSpan timeSpan])
            {
                if (methodName.Equals("op_Addition", StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(dateTimeOffset + timeSpan);
                }

                if (methodName.Equals("op_Subtraction", StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(dateTimeOffset - timeSpan);
                }
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeParse(object?[] args)
            => args switch
            {
                [string value] => Invoked(DateTimeOffset.Parse(value)),
                [string value, IFormatProvider provider] => Invoked(DateTimeOffset.Parse(value, provider)),
                [string value, null] => Invoked(DateTimeOffset.Parse(value, formatProvider: null)),
                [string value, var provider, var styles]
                    when TryGetFormatProvider(provider, out IFormatProvider? convertedProvider) &&
                         ArgumentParser.TryConvertToEnum(styles, out DateTimeStyles convertedStyles)
                    => Invoked(DateTimeOffset.Parse(value, convertedProvider, convertedStyles)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeParseExact(object?[] args)
        {
            if (args is [string input, string format, var provider] &&
                TryGetFormatProvider(provider, out IFormatProvider? convertedProvider))
            {
                return Invoked(DateTimeOffset.ParseExact(input, format, convertedProvider));
            }

            if (args is [string input2, string format2, var provider2, var styles] &&
                TryGetFormatProvider(provider2, out convertedProvider) &&
                ArgumentParser.TryConvertToEnum(styles, out DateTimeStyles convertedStyles))
            {
                return Invoked(DateTimeOffset.ParseExact(input2, format2, convertedProvider, convertedStyles));
            }

            if (args is [string input3, string[] formats, var provider3, var styles2] &&
                TryGetFormatProvider(provider3, out convertedProvider) &&
                ArgumentParser.TryConvertToEnum(styles2, out DateTimeStyles convertedStyles2))
            {
                return Invoked(DateTimeOffset.ParseExact(input3, formats, convertedProvider, convertedStyles2));
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeProperty(string methodName, DateTimeOffset value)
            => methodName switch
            {
                var name when name.Equals(nameof(DateTimeOffset.Date), StringComparison.OrdinalIgnoreCase) => Invoked(value.Date),
                var name when name.Equals(nameof(DateTimeOffset.DateTime), StringComparison.OrdinalIgnoreCase) => Invoked(value.DateTime),
                var name when name.Equals(nameof(DateTimeOffset.Day), StringComparison.OrdinalIgnoreCase) => Invoked(value.Day),
                var name when name.Equals(nameof(DateTimeOffset.DayOfWeek), StringComparison.OrdinalIgnoreCase) => Invoked(value.DayOfWeek),
                var name when name.Equals(nameof(DateTimeOffset.DayOfYear), StringComparison.OrdinalIgnoreCase) => Invoked(value.DayOfYear),
                var name when name.Equals(nameof(DateTimeOffset.Hour), StringComparison.OrdinalIgnoreCase) => Invoked(value.Hour),
                var name when name.Equals(nameof(DateTimeOffset.LocalDateTime), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.LocalDateTime),
                var name when name.Equals(nameof(DateTimeOffset.Millisecond), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.Millisecond),
                var name when name.Equals(nameof(DateTimeOffset.Minute), StringComparison.OrdinalIgnoreCase) => Invoked(value.Minute),
                var name when name.Equals(nameof(DateTimeOffset.Month), StringComparison.OrdinalIgnoreCase) => Invoked(value.Month),
                var name when name.Equals(nameof(DateTimeOffset.Offset), StringComparison.OrdinalIgnoreCase) => Invoked(value.Offset),
                var name when name.Equals(nameof(DateTimeOffset.Second), StringComparison.OrdinalIgnoreCase) => Invoked(value.Second),
                var name when name.Equals(nameof(DateTimeOffset.Ticks), StringComparison.OrdinalIgnoreCase) => Invoked(value.Ticks),
                var name when name.Equals(nameof(DateTimeOffset.TimeOfDay), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.TimeOfDay),
                var name when name.Equals(nameof(DateTimeOffset.UtcDateTime), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.UtcDateTime),
                var name when name.Equals(nameof(DateTimeOffset.Year), StringComparison.OrdinalIgnoreCase) => Invoked(value.Year),
#if NET
                var name when name.Equals(nameof(DateTimeOffset.Microsecond), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.Microsecond),
                var name when name.Equals(nameof(DateTimeOffset.Nanosecond), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.Nanosecond),
                var name when name.Equals(nameof(DateTimeOffset.TotalOffsetMinutes), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.TotalOffsetMinutes),
                var name when name.Equals(nameof(DateTimeOffset.UtcTicks), StringComparison.OrdinalIgnoreCase) => Invoked(value.UtcTicks),
#endif
                _ => NotHandled,
            };

        private static bool TryGetDateTimeOffsetArguments(object?[] args, out DateTimeOffset left, out DateTimeOffset right)
        {
            if (args is [DateTimeOffset leftValue, DateTimeOffset rightValue])
            {
                left = leftValue;
                right = rightValue;
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
