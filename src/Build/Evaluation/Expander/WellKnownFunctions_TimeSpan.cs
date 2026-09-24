// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class TimeSpanHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
        {
            if (args is [var ticks] && ArgumentParser.TryConvertToLong(ticks, out long convertedTicks))
            {
                return Invoked(new TimeSpan(convertedTicks));
            }

            if (TryGetInt32Arguments(args, out int days, out int hours, out int minutes, out int seconds, out int milliseconds))
            {
                return Invoked(new TimeSpan(days, hours, minutes, seconds, milliseconds));
            }

#if NET
            if (TryGetInt32Arguments(
                    args,
                    out days,
                    out hours,
                    out minutes,
                    out seconds,
                    out milliseconds,
                    out int microseconds))
            {
                return Invoked(new TimeSpan(days, hours, minutes, seconds, milliseconds, microseconds));
            }
#endif

            if (TryGetInt32Arguments(args, out days, out hours, out minutes, out seconds))
            {
                return Invoked(new TimeSpan(days, hours, minutes, seconds));
            }

            if (TryGetInt32Arguments(args, out hours, out minutes, out seconds))
            {
                return Invoked(new TimeSpan(hours, minutes, seconds));
            }

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(TimeSpan.Compare), StringComparison.OrdinalIgnoreCase))
            {
                return TryGetTimeSpanArguments(args, out TimeSpan left, out TimeSpan right)
                    ? Invoked(TimeSpan.Compare(left, right))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(TimeSpan.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return TryGetConvertedTimeSpans(args, out TimeSpan left, out TimeSpan right)
                    ? Invoked(TimeSpan.Equals(left, right))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(TimeSpan.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string value] => Invoked(TimeSpan.Parse(value)),
                    [string value, IFormatProvider provider] => Invoked(TimeSpan.Parse(value, provider)),
                    [string value, null] => Invoked(TimeSpan.Parse(value, formatProvider: null)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(TimeSpan.ParseExact), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParseExact(args);
            }

            if (TryInvokeFactory(methodName, args) is { Status: WellKnownFunctionStatus.Invoked } factoryResult)
            {
                return factoryResult;
            }

            if (TryInvokeOperator(methodName, args) is { Status: WellKnownFunctionStatus.Invoked } operatorResult)
            {
                return operatorResult;
            }

            if (args is not [])
            {
                return NotHandled;
            }

            return methodName switch
            {
                var name when name.Equals(nameof(TimeSpan.MaxValue), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.MaxValue),
                var name when name.Equals(nameof(TimeSpan.MinValue), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.MinValue),
                var name when name.Equals(nameof(TimeSpan.Zero), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.Zero),
#if NET
                var name when name.Equals(nameof(TimeSpan.HoursPerDay), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.HoursPerDay),
                var name when name.Equals(nameof(TimeSpan.MicrosecondsPerDay), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MicrosecondsPerDay),
                var name when name.Equals(nameof(TimeSpan.MicrosecondsPerHour), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MicrosecondsPerHour),
                var name when name.Equals(nameof(TimeSpan.MicrosecondsPerMillisecond), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MicrosecondsPerMillisecond),
                var name when name.Equals(nameof(TimeSpan.MicrosecondsPerMinute), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MicrosecondsPerMinute),
                var name when name.Equals(nameof(TimeSpan.MicrosecondsPerSecond), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MicrosecondsPerSecond),
                var name when name.Equals(nameof(TimeSpan.MillisecondsPerDay), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MillisecondsPerDay),
                var name when name.Equals(nameof(TimeSpan.MillisecondsPerHour), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MillisecondsPerHour),
                var name when name.Equals(nameof(TimeSpan.MillisecondsPerMinute), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MillisecondsPerMinute),
                var name when name.Equals(nameof(TimeSpan.MillisecondsPerSecond), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.MillisecondsPerSecond),
                var name when name.Equals(nameof(TimeSpan.MinutesPerDay), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.MinutesPerDay),
                var name when name.Equals(nameof(TimeSpan.MinutesPerHour), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.MinutesPerHour),
                var name when name.Equals(nameof(TimeSpan.NanosecondsPerTick), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.NanosecondsPerTick),
                var name when name.Equals(nameof(TimeSpan.SecondsPerDay), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.SecondsPerDay),
                var name when name.Equals(nameof(TimeSpan.SecondsPerHour), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.SecondsPerHour),
                var name when name.Equals(nameof(TimeSpan.SecondsPerMinute), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.SecondsPerMinute),
                var name when name.Equals(nameof(TimeSpan.TicksPerDay), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.TicksPerDay),
                var name when name.Equals(nameof(TimeSpan.TicksPerHour), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.TicksPerHour),
                var name when name.Equals(nameof(TimeSpan.TicksPerMicrosecond), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.TicksPerMicrosecond),
                var name when name.Equals(nameof(TimeSpan.TicksPerMillisecond), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.TicksPerMillisecond),
                var name when name.Equals(nameof(TimeSpan.TicksPerMinute), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.TicksPerMinute),
                var name when name.Equals(nameof(TimeSpan.TicksPerSecond), StringComparison.OrdinalIgnoreCase) => Invoked(TimeSpan.TicksPerSecond),
#endif
                _ => NotHandled,
            };
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, TimeSpan value, object?[] args)
        {
            if (args is [] && TryInvokeProperty(methodName, value) is { Status: WellKnownFunctionStatus.Invoked } propertyResult)
            {
                return propertyResult;
            }

            if (methodName.Equals(nameof(TimeSpan.CompareTo), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var compareValue] ? Invoked(value.CompareTo(compareValue)) : NotHandled;
            }

            if (methodName.Equals(nameof(TimeSpan.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var equalsValue] ? Invoked(value.Equals(equalsValue)) : NotHandled;
            }

            if (methodName.Equals(nameof(TimeSpan.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [] => Invoked(value.ToString()),
                    [string format] => Invoked(value.ToString(format)),
                    [string format, IFormatProvider provider] => Invoked(value.ToString(format, provider)),
                    [string format, null] => Invoked(value.ToString(format, formatProvider: null)),
                    _ => NotHandled,
                };
            }

            if (args is [] && methodName.Equals(nameof(TimeSpan.Duration), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Duration());
            }

            if (args is [] && methodName.Equals(nameof(TimeSpan.GetHashCode), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.GetHashCode());
            }

            if (args is [] && methodName.Equals(nameof(TimeSpan.Negate), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Negate());
            }

            if (args is [var other] && TryGetTimeSpan(other, out TimeSpan converted))
            {
                if (methodName.Equals(nameof(TimeSpan.Add), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(value.Add(converted));
                }

                if (methodName.Equals(nameof(TimeSpan.Subtract), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(value.Subtract(converted));
                }

#if NET
                if (methodName.Equals(nameof(TimeSpan.Divide), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(value.Divide(converted));
                }
#endif
            }

#if NET
            if (args is [var factor] && ArgumentParser.TryConvertToDouble(factor, out double convertedFactor))
            {
                if (methodName.Equals(nameof(TimeSpan.Divide), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(value.Divide(convertedFactor));
                }

                if (methodName.Equals(nameof(TimeSpan.Multiply), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(value.Multiply(convertedFactor));
                }
            }
#endif

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeFactory(string methodName, object?[] args)
        {
            if (args is not [var value])
            {
#if NET
                if (methodName.Equals(nameof(TimeSpan.FromDays), StringComparison.OrdinalIgnoreCase) &&
                    TryGetInt32Int32Int64Int64Int64Int64(
                        args,
                        out int days,
                        out int hours,
                        out long minutes,
                        out long seconds,
                        out long milliseconds,
                        out long microseconds))
                {
                    return Invoked(TimeSpan.FromDays(days, hours, minutes, seconds, milliseconds, microseconds));
                }

                if (methodName.Equals(nameof(TimeSpan.FromHours), StringComparison.OrdinalIgnoreCase) &&
                    TryGetInt32Int64Int64Int64Int64(args, out int hours2, out long minutes2, out long seconds2, out long milliseconds2, out long microseconds2))
                {
                    return Invoked(TimeSpan.FromHours(hours2, minutes2, seconds2, milliseconds2, microseconds2));
                }

                if (methodName.Equals(nameof(TimeSpan.FromMinutes), StringComparison.OrdinalIgnoreCase) &&
                    TryGetInt64Arguments(args, out long minutes3, out long seconds3, out long milliseconds3, out long microseconds3))
                {
                    return Invoked(TimeSpan.FromMinutes(minutes3, seconds3, milliseconds3, microseconds3));
                }

                if (methodName.Equals(nameof(TimeSpan.FromSeconds), StringComparison.OrdinalIgnoreCase) &&
                    TryGetInt64Arguments(args, out long seconds4, out long milliseconds4, out long microseconds4))
                {
                    return Invoked(TimeSpan.FromSeconds(seconds4, milliseconds4, microseconds4));
                }

                if (methodName.Equals(nameof(TimeSpan.FromMilliseconds), StringComparison.OrdinalIgnoreCase) &&
                    TryGetInt64Arguments(args, out long milliseconds5, out long microseconds5))
                {
                    return Invoked(TimeSpan.FromMilliseconds(milliseconds5, microseconds5));
                }
#endif
                return NotHandled;
            }

            if (methodName.Equals(nameof(TimeSpan.FromTicks), StringComparison.OrdinalIgnoreCase) &&
                ArgumentParser.TryConvertToLong(value, out long ticks))
            {
                return Invoked(TimeSpan.FromTicks(ticks));
            }

#if NET
            if (methodName.Equals(nameof(TimeSpan.FromDays), StringComparison.OrdinalIgnoreCase) && value is int intDays)
            {
                return Invoked(TimeSpan.FromDays(intDays));
            }

            if (methodName.Equals(nameof(TimeSpan.FromHours), StringComparison.OrdinalIgnoreCase) && value is int intHours)
            {
                return Invoked(TimeSpan.FromHours(intHours));
            }

            if (methodName.Equals(nameof(TimeSpan.FromMicroseconds), StringComparison.OrdinalIgnoreCase))
            {
                return value is double doubleMicroseconds
                    ? Invoked(TimeSpan.FromMicroseconds(doubleMicroseconds))
                    : ArgumentParser.TryConvertToLong(value, out long microseconds)
                        ? Invoked(TimeSpan.FromMicroseconds(microseconds))
                        : ArgumentParser.TryConvertToDouble(value, out double convertedMicroseconds)
                            ? Invoked(TimeSpan.FromMicroseconds(convertedMicroseconds))
                            : NotHandled;
            }

            if (methodName.Equals(nameof(TimeSpan.FromMilliseconds), StringComparison.OrdinalIgnoreCase) &&
                value is not double &&
                ArgumentParser.TryConvertToLong(value, out long convertedMilliseconds))
            {
                return Invoked(TimeSpan.FromMilliseconds(convertedMilliseconds));
            }

            if (methodName.Equals(nameof(TimeSpan.FromMinutes), StringComparison.OrdinalIgnoreCase) && value is long longMinutes)
            {
                return Invoked(TimeSpan.FromMinutes(longMinutes));
            }

            if (methodName.Equals(nameof(TimeSpan.FromSeconds), StringComparison.OrdinalIgnoreCase) && value is long longSeconds)
            {
                return Invoked(TimeSpan.FromSeconds(longSeconds));
            }
#endif

            if (!ArgumentParser.TryConvertToDouble(value, out double convertedValue))
            {
                return NotHandled;
            }

            return methodName switch
            {
                var name when name.Equals(nameof(TimeSpan.FromDays), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.FromDays(convertedValue)),
                var name when name.Equals(nameof(TimeSpan.FromHours), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.FromHours(convertedValue)),
                var name when name.Equals(nameof(TimeSpan.FromMilliseconds), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.FromMilliseconds(convertedValue)),
                var name when name.Equals(nameof(TimeSpan.FromMinutes), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.FromMinutes(convertedValue)),
                var name when name.Equals(nameof(TimeSpan.FromSeconds), StringComparison.OrdinalIgnoreCase)
                    => Invoked(TimeSpan.FromSeconds(convertedValue)),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeParseExact(object?[] args)
        {
            if (args is [string input, string format, var provider] &&
                TryGetFormatProvider(provider, out IFormatProvider? convertedProvider))
            {
                return Invoked(TimeSpan.ParseExact(input, format, convertedProvider));
            }

            if (args is [string input2, string[] formats, var provider2] &&
                TryGetFormatProvider(provider2, out convertedProvider))
            {
                return Invoked(TimeSpan.ParseExact(input2, formats, convertedProvider));
            }

            if (args is [string input3, string format2, var provider3, TimeSpanStyles styles] &&
                TryGetFormatProvider(provider3, out convertedProvider))
            {
                return Invoked(TimeSpan.ParseExact(input3, format2, convertedProvider, styles));
            }

            if (args is [string input4, string[] formats2, var provider4, TimeSpanStyles styles2] &&
                TryGetFormatProvider(provider4, out convertedProvider))
            {
                return Invoked(TimeSpan.ParseExact(input4, formats2, convertedProvider, styles2));
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeOperator(string methodName, object?[] args)
        {
            if (args is [var value] && TryGetTimeSpan(value, out TimeSpan operand))
            {
                return methodName switch
                {
                    "op_UnaryNegation" => Invoked(-operand),
                    "op_UnaryPlus" => Invoked(+operand),
                    _ => NotHandled,
                };
            }

            if (TryGetTimeSpanArguments(args, out TimeSpan left, out TimeSpan right))
            {
                return methodName switch
                {
                    "op_Addition" => Invoked(left + right),
#if NET
                    "op_Division" => Invoked(left / right),
#endif
                    "op_Equality" => Invoked(left == right),
                    "op_GreaterThan" => Invoked(left > right),
                    "op_GreaterThanOrEqual" => Invoked(left >= right),
                    "op_Inequality" => Invoked(left != right),
                    "op_LessThan" => Invoked(left < right),
                    "op_LessThanOrEqual" => Invoked(left <= right),
                    "op_Subtraction" => Invoked(left - right),
                    _ => NotHandled,
                };
            }

#if NET
            if (args is [var timeSpan, var divisor] &&
                TryGetTimeSpan(timeSpan, out TimeSpan convertedTimeSpan) &&
                ArgumentParser.TryConvertToDouble(divisor, out double convertedDivisor))
            {
                return methodName switch
                {
                    "op_Division" => Invoked(convertedTimeSpan / convertedDivisor),
                    "op_Multiply" => Invoked(convertedTimeSpan * convertedDivisor),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals("op_Multiply", StringComparison.OrdinalIgnoreCase) &&
                args is [var factor, var timeSpan2] &&
                ArgumentParser.TryConvertToDouble(factor, out double convertedFactor) &&
                TryGetTimeSpan(timeSpan2, out TimeSpan convertedTimeSpan2))
            {
                return Invoked(convertedFactor * convertedTimeSpan2);
            }
#endif

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeProperty(string methodName, TimeSpan value)
            => methodName switch
            {
                var name when name.Equals(nameof(TimeSpan.Days), StringComparison.OrdinalIgnoreCase) => Invoked(value.Days),
                var name when name.Equals(nameof(TimeSpan.Hours), StringComparison.OrdinalIgnoreCase) => Invoked(value.Hours),
                var name when name.Equals(nameof(TimeSpan.Milliseconds), StringComparison.OrdinalIgnoreCase) => Invoked(value.Milliseconds),
                var name when name.Equals(nameof(TimeSpan.Minutes), StringComparison.OrdinalIgnoreCase) => Invoked(value.Minutes),
                var name when name.Equals(nameof(TimeSpan.Seconds), StringComparison.OrdinalIgnoreCase) => Invoked(value.Seconds),
                var name when name.Equals(nameof(TimeSpan.Ticks), StringComparison.OrdinalIgnoreCase) => Invoked(value.Ticks),
                var name when name.Equals(nameof(TimeSpan.TotalDays), StringComparison.OrdinalIgnoreCase) => Invoked(value.TotalDays),
                var name when name.Equals(nameof(TimeSpan.TotalHours), StringComparison.OrdinalIgnoreCase) => Invoked(value.TotalHours),
                var name when name.Equals(nameof(TimeSpan.TotalMilliseconds), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.TotalMilliseconds),
                var name when name.Equals(nameof(TimeSpan.TotalMinutes), StringComparison.OrdinalIgnoreCase) => Invoked(value.TotalMinutes),
                var name when name.Equals(nameof(TimeSpan.TotalSeconds), StringComparison.OrdinalIgnoreCase) => Invoked(value.TotalSeconds),
#if NET
                var name when name.Equals(nameof(TimeSpan.Microseconds), StringComparison.OrdinalIgnoreCase) => Invoked(value.Microseconds),
                var name when name.Equals(nameof(TimeSpan.Nanoseconds), StringComparison.OrdinalIgnoreCase) => Invoked(value.Nanoseconds),
                var name when name.Equals(nameof(TimeSpan.TotalMicroseconds), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.TotalMicroseconds),
                var name when name.Equals(nameof(TimeSpan.TotalNanoseconds), StringComparison.OrdinalIgnoreCase)
                    => Invoked(value.TotalNanoseconds),
#endif
                _ => NotHandled,
            };

        private static bool TryGetTimeSpanArguments(object?[] args, out TimeSpan left, out TimeSpan right)
        {
            if (args is [var leftValue, var rightValue] &&
                TryGetTimeSpan(leftValue, out left) &&
                TryGetTimeSpan(rightValue, out right))
            {
                return true;
            }

            left = default;
            right = default;
            return false;
        }

        private static bool TryGetConvertedTimeSpans(object?[] args, out TimeSpan left, out TimeSpan right)
        {
            if (args is [var leftValue, var rightValue] &&
                TryConvertToTimeSpan(leftValue, out left) &&
                TryConvertToTimeSpan(rightValue, out right))
            {
                return true;
            }

            left = default;
            right = default;
            return false;
        }

        private static bool TryGetTimeSpan(object? value, out TimeSpan result)
        {
            if (value is TimeSpan timeSpan)
            {
                result = timeSpan;
                return true;
            }

            result = default;
            return value is null;
        }

        private static bool TryGetFormatProvider(object? value, out IFormatProvider? provider)
        {
            provider = value as IFormatProvider;
            return value is null || provider is not null;
        }

        private static bool TryConvertToTimeSpan(object? value, out TimeSpan result)
        {
            if (value is TimeSpan timeSpan)
            {
                result = timeSpan;
                return true;
            }

            if (value is null)
            {
                result = default;
                return true;
            }

            if (value is string text && TimeSpan.TryParse(text, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryGetInt32Arguments(object?[] args, out int first, out int second, out int third)
        {
            if (args is [var firstValue, var secondValue, var thirdValue] &&
                ArgumentParser.TryConvertToInt(firstValue, out first) &&
                ArgumentParser.TryConvertToInt(secondValue, out second) &&
                ArgumentParser.TryConvertToInt(thirdValue, out third))
            {
                return true;
            }

            first = second = third = default;
            return false;
        }

        private static bool TryGetInt32Arguments(object?[] args, out int first, out int second, out int third, out int fourth)
        {
            if (args is [var firstValue, var secondValue, var thirdValue, var fourthValue] &&
                ArgumentParser.TryConvertToInt(firstValue, out first) &&
                ArgumentParser.TryConvertToInt(secondValue, out second) &&
                ArgumentParser.TryConvertToInt(thirdValue, out third) &&
                ArgumentParser.TryConvertToInt(fourthValue, out fourth))
            {
                return true;
            }

            first = second = third = fourth = default;
            return false;
        }

        private static bool TryGetInt32Arguments(
            object?[] args,
            out int first,
            out int second,
            out int third,
            out int fourth,
            out int fifth)
        {
            if (args is [var firstValue, var secondValue, var thirdValue, var fourthValue, var fifthValue] &&
                ArgumentParser.TryConvertToInt(firstValue, out first) &&
                ArgumentParser.TryConvertToInt(secondValue, out second) &&
                ArgumentParser.TryConvertToInt(thirdValue, out third) &&
                ArgumentParser.TryConvertToInt(fourthValue, out fourth) &&
                ArgumentParser.TryConvertToInt(fifthValue, out fifth))
            {
                return true;
            }

            first = second = third = fourth = fifth = default;
            return false;
        }

#if NET
        private static bool TryGetInt32Arguments(
            object?[] args,
            out int first,
            out int second,
            out int third,
            out int fourth,
            out int fifth,
            out int sixth)
        {
            if (args is [var firstValue, var secondValue, var thirdValue, var fourthValue, var fifthValue, var sixthValue] &&
                ArgumentParser.TryConvertToInt(firstValue, out first) &&
                ArgumentParser.TryConvertToInt(secondValue, out second) &&
                ArgumentParser.TryConvertToInt(thirdValue, out third) &&
                ArgumentParser.TryConvertToInt(fourthValue, out fourth) &&
                ArgumentParser.TryConvertToInt(fifthValue, out fifth) &&
                ArgumentParser.TryConvertToInt(sixthValue, out sixth))
            {
                return true;
            }

            first = second = third = fourth = fifth = sixth = default;
            return false;
        }

        private static bool TryGetInt64Arguments(object?[] args, out long first, out long second)
        {
            if (args is [var firstValue, var secondValue] &&
                ArgumentParser.TryConvertToLong(firstValue, out first) &&
                ArgumentParser.TryConvertToLong(secondValue, out second))
            {
                return true;
            }

            first = second = default;
            return false;
        }

        private static bool TryGetInt64Arguments(object?[] args, out long first, out long second, out long third)
        {
            if (args is [var firstValue, var secondValue, var thirdValue] &&
                ArgumentParser.TryConvertToLong(firstValue, out first) &&
                ArgumentParser.TryConvertToLong(secondValue, out second) &&
                ArgumentParser.TryConvertToLong(thirdValue, out third))
            {
                return true;
            }

            first = second = third = default;
            return false;
        }

        private static bool TryGetInt64Arguments(object?[] args, out long first, out long second, out long third, out long fourth)
        {
            if (args is [var firstValue, var secondValue, var thirdValue, var fourthValue] &&
                ArgumentParser.TryConvertToLong(firstValue, out first) &&
                ArgumentParser.TryConvertToLong(secondValue, out second) &&
                ArgumentParser.TryConvertToLong(thirdValue, out third) &&
                ArgumentParser.TryConvertToLong(fourthValue, out fourth))
            {
                return true;
            }

            first = second = third = fourth = default;
            return false;
        }

        private static bool TryGetInt32Int64Int64Int64Int64(
            object?[] args,
            out int first,
            out long second,
            out long third,
            out long fourth,
            out long fifth)
        {
            if (args is [var firstValue, var secondValue, var thirdValue, var fourthValue, var fifthValue] &&
                ArgumentParser.TryConvertToInt(firstValue, out first) &&
                ArgumentParser.TryConvertToLong(secondValue, out second) &&
                ArgumentParser.TryConvertToLong(thirdValue, out third) &&
                ArgumentParser.TryConvertToLong(fourthValue, out fourth) &&
                ArgumentParser.TryConvertToLong(fifthValue, out fifth))
            {
                return true;
            }

            first = default;
            second = third = fourth = fifth = default;
            return false;
        }

        private static bool TryGetInt32Int32Int64Int64Int64Int64(
            object?[] args,
            out int first,
            out int second,
            out long third,
            out long fourth,
            out long fifth,
            out long sixth)
        {
            if (args is [var firstValue, var secondValue, var thirdValue, var fourthValue, var fifthValue, var sixthValue] &&
                ArgumentParser.TryConvertToInt(firstValue, out first) &&
                ArgumentParser.TryConvertToInt(secondValue, out second) &&
                ArgumentParser.TryConvertToLong(thirdValue, out third) &&
                ArgumentParser.TryConvertToLong(fourthValue, out fourth) &&
                ArgumentParser.TryConvertToLong(fifthValue, out fifth) &&
                ArgumentParser.TryConvertToLong(sixthValue, out sixth))
            {
                return true;
            }

            first = second = default;
            third = fourth = fifth = sixth = default;
            return false;
        }
#endif
    }
}
