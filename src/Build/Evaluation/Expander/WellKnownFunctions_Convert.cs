// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class ConvertHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                6 when methodName.Equals(nameof(Convert.ToByte), StringComparison.OrdinalIgnoreCase) => TryInvokeToByte(args),
                6 when methodName.Equals(nameof(Convert.ToChar), StringComparison.OrdinalIgnoreCase) => TryInvokeToChar(args),
                7 when methodName.Equals(nameof(Convert.ToInt16), StringComparison.OrdinalIgnoreCase) => TryInvokeToInt16(args),
                7 when methodName.Equals(nameof(Convert.ToInt32), StringComparison.OrdinalIgnoreCase) => TryInvokeToInt32(args),
                7 when methodName.Equals(nameof(Convert.ToInt64), StringComparison.OrdinalIgnoreCase) => TryInvokeToInt64(args),
                7 when methodName.Equals(nameof(Convert.ToSByte), StringComparison.OrdinalIgnoreCase) => TryInvokeToSByte(args),
                8 when methodName.Equals(nameof(Convert.ToDouble), StringComparison.OrdinalIgnoreCase) => TryInvokeToDouble(args),
                8 when methodName.Equals(nameof(Convert.ToSingle), StringComparison.OrdinalIgnoreCase) => TryInvokeToSingle(args),
                8 when methodName.Equals(nameof(Convert.ToString), StringComparison.OrdinalIgnoreCase) => TryInvokeToString(args),
                8 when methodName.Equals(nameof(Convert.ToUInt16), StringComparison.OrdinalIgnoreCase) => TryInvokeToUInt16(args),
                8 when methodName.Equals(nameof(Convert.ToUInt32), StringComparison.OrdinalIgnoreCase) => TryInvokeToUInt32(args),
                8 when methodName.Equals(nameof(Convert.ToUInt64), StringComparison.OrdinalIgnoreCase) => TryInvokeToUInt64(args),
                8 when methodName.Equals(nameof(Convert.IsDBNull), StringComparison.OrdinalIgnoreCase) => TryInvokeIsDBNull(args),
                9 when methodName.Equals(nameof(Convert.ToBoolean), StringComparison.OrdinalIgnoreCase) => TryInvokeToBoolean(args),
                9 when methodName.Equals(nameof(Convert.ToDecimal), StringComparison.OrdinalIgnoreCase) => TryInvokeToDecimal(args),
                10 when methodName.Equals(nameof(Convert.ChangeType), StringComparison.OrdinalIgnoreCase) => TryInvokeChangeType(args),
                10 when methodName.Equals(nameof(Convert.ToDateTime), StringComparison.OrdinalIgnoreCase) => TryInvokeToDateTime(args),
                11 when methodName.Equals(nameof(Convert.GetTypeCode), StringComparison.OrdinalIgnoreCase) => TryInvokeGetTypeCode(args),
#if NET
                11 when methodName.Equals(nameof(Convert.ToHexString), StringComparison.OrdinalIgnoreCase) => TryInvokeToHexString(args),
#endif
                14 when methodName.Equals(nameof(Convert.ToBase64String), StringComparison.OrdinalIgnoreCase) => TryInvokeToBase64String(args),
                16 when methodName.Equals(nameof(Convert.FromBase64String), StringComparison.OrdinalIgnoreCase) => TryInvokeFromBase64String(args),
                17 when methodName.Equals(nameof(Convert.ToBase64CharArray), StringComparison.OrdinalIgnoreCase) => TryInvokeToBase64CharArray(args),
                19 when methodName.Equals(nameof(Convert.FromBase64CharArray), StringComparison.OrdinalIgnoreCase) => TryInvokeFromBase64CharArray(args),
#if NET
                13 when methodName.Equals(nameof(Convert.FromHexString), StringComparison.OrdinalIgnoreCase) => TryInvokeFromHexString(args),
                16 when methodName.Equals(nameof(Convert.ToHexStringLower), StringComparison.OrdinalIgnoreCase) => TryInvokeToHexStringLower(args),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeChangeType(object?[] args)
            => args switch
            {
                [var value, TypeCode typeCode] => Invoked(Convert.ChangeType(value, typeCode)),
                [var value, Type conversionType] => Invoked(Convert.ChangeType(value, conversionType)),
                [var value, TypeCode typeCode, IFormatProvider provider] => Invoked(Convert.ChangeType(value, typeCode, provider)),
                [var value, Type conversionType, IFormatProvider provider] => Invoked(Convert.ChangeType(value, conversionType, provider)),
                [var value, var typeCode] when ArgumentParser.TryConvertToEnum(typeCode, out TypeCode convertedTypeCode)
                    => Invoked(Convert.ChangeType(value, convertedTypeCode)),
                [var value, var typeCode, IFormatProvider provider] when ArgumentParser.TryConvertToEnum(typeCode, out TypeCode convertedTypeCode)
                    => Invoked(Convert.ChangeType(value, convertedTypeCode, provider)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeFromBase64CharArray(object?[] args)
            => args is [char[] value, int offset, int length]
                ? Invoked(Convert.FromBase64CharArray(value, offset, length))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeFromBase64String(object?[] args)
            => args is [string value]
                ? Invoked(Convert.FromBase64String(value))
                : NotHandled;

#if NET
        private static WellKnownFunctionResult TryInvokeFromHexString(object?[] args)
            => args is [string value]
                ? Invoked(Convert.FromHexString(value))
                : NotHandled;
#endif

        private static WellKnownFunctionResult TryInvokeGetTypeCode(object?[] args)
            => args is [var value]
                ? Invoked(Convert.GetTypeCode(value))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsDBNull(object?[] args)
            => args is [var value]
                ? Invoked(Convert.IsDBNull(value))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeToBase64CharArray(object?[] args)
            => args switch
            {
                [byte[] input, int offsetIn, int length, char[] output, int offsetOut]
                    => Invoked(Convert.ToBase64CharArray(input, offsetIn, length, output, offsetOut)),
                [byte[] input, int offsetIn, int length, char[] output, int offsetOut, Base64FormattingOptions options]
                    => Invoked(Convert.ToBase64CharArray(input, offsetIn, length, output, offsetOut, options)),
                [byte[] input, int offsetIn, int length, char[] output, int offsetOut, var options]
                    when ArgumentParser.TryConvertToEnum(options, out Base64FormattingOptions convertedOptions)
                    => Invoked(Convert.ToBase64CharArray(input, offsetIn, length, output, offsetOut, convertedOptions)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToBase64String(object?[] args)
            => args switch
            {
                [byte[] value] => Invoked(Convert.ToBase64String(value)),
                [byte[] value, Base64FormattingOptions options] => Invoked(Convert.ToBase64String(value, options)),
                [byte[] value, var options] when ArgumentParser.TryConvertToEnum(options, out Base64FormattingOptions convertedOptions)
                    => Invoked(Convert.ToBase64String(value, convertedOptions)),
                [byte[] value, int offset, int length] => Invoked(Convert.ToBase64String(value, offset, length)),
                [byte[] value, int offset, int length, Base64FormattingOptions options]
                    => Invoked(Convert.ToBase64String(value, offset, length, options)),
                [byte[] value, int offset, int length, var options]
                    when ArgumentParser.TryConvertToEnum(options, out Base64FormattingOptions convertedOptions)
                    => Invoked(Convert.ToBase64String(value, offset, length, convertedOptions)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToBoolean(object?[] args)
            => TryInvokeConversion(args, Convert.ToBoolean, Convert.ToBoolean);

        private static WellKnownFunctionResult TryInvokeToByte(object?[] args)
            => args switch
            {
                [string value, int fromBase] => Invoked(Convert.ToByte(value, fromBase)),
                _ => TryInvokeConversion(args, Convert.ToByte, Convert.ToByte),
            };

        private static WellKnownFunctionResult TryInvokeToChar(object?[] args)
            => TryInvokeConversion(args, Convert.ToChar, Convert.ToChar);

        private static WellKnownFunctionResult TryInvokeToDateTime(object?[] args)
            => TryInvokeConversion(args, Convert.ToDateTime, Convert.ToDateTime);

        private static WellKnownFunctionResult TryInvokeToDecimal(object?[] args)
            => TryInvokeConversion(args, Convert.ToDecimal, Convert.ToDecimal);

        private static WellKnownFunctionResult TryInvokeToDouble(object?[] args)
            => TryInvokeConversion(args, Convert.ToDouble, Convert.ToDouble);

        private static WellKnownFunctionResult TryInvokeToInt16(object?[] args)
            => args switch
            {
                [string value, int fromBase] => Invoked(Convert.ToInt16(value, fromBase)),
                _ => TryInvokeConversion(args, Convert.ToInt16, Convert.ToInt16),
            };

        private static WellKnownFunctionResult TryInvokeToInt32(object?[] args)
            => args switch
            {
                [string value, int fromBase] => Invoked(Convert.ToInt32(value, fromBase)),
                _ => TryInvokeConversion(args, Convert.ToInt32, Convert.ToInt32),
            };

        private static WellKnownFunctionResult TryInvokeToInt64(object?[] args)
            => args switch
            {
                [string value, int fromBase] => Invoked(Convert.ToInt64(value, fromBase)),
                _ => TryInvokeConversion(args, Convert.ToInt64, Convert.ToInt64),
            };

        private static WellKnownFunctionResult TryInvokeToSByte(object?[] args)
            => args switch
            {
                [string value, int fromBase] => Invoked(Convert.ToSByte(value, fromBase)),
                _ => TryInvokeConversion(args, Convert.ToSByte, Convert.ToSByte),
            };

        private static WellKnownFunctionResult TryInvokeToSingle(object?[] args)
            => TryInvokeConversion(args, Convert.ToSingle, Convert.ToSingle);

        private static WellKnownFunctionResult TryInvokeToString(object?[] args)
            => args switch
            {
                [byte value, int toBase] => Invoked(Convert.ToString(value, toBase)),
                [short value, int toBase] => Invoked(Convert.ToString(value, toBase)),
                [int value, int toBase] => Invoked(Convert.ToString(value, toBase)),
                [long value, int toBase] => Invoked(Convert.ToString(value, toBase)),
                [var value] => Invoked(Convert.ToString(value)),
                [var value, IFormatProvider provider] => Invoked(Convert.ToString(value, provider)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToUInt16(object?[] args)
            => args switch
            {
                [string value, int fromBase] => Invoked(Convert.ToUInt16(value, fromBase)),
                _ => TryInvokeConversion(args, Convert.ToUInt16, Convert.ToUInt16),
            };

        private static WellKnownFunctionResult TryInvokeToUInt32(object?[] args)
            => args switch
            {
                [string value, int fromBase] => Invoked(Convert.ToUInt32(value, fromBase)),
                _ => TryInvokeConversion(args, Convert.ToUInt32, Convert.ToUInt32),
            };

        private static WellKnownFunctionResult TryInvokeToUInt64(object?[] args)
            => args switch
            {
                [string value, int fromBase] => Invoked(Convert.ToUInt64(value, fromBase)),
                _ => TryInvokeConversion(args, Convert.ToUInt64, Convert.ToUInt64),
            };

#if NET
        private static WellKnownFunctionResult TryInvokeToHexString(object?[] args)
            => args switch
            {
                [byte[] value] => Invoked(Convert.ToHexString(value)),
                [byte[] value, int offset, int length] => Invoked(Convert.ToHexString(value, offset, length)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToHexStringLower(object?[] args)
            => args switch
            {
                [byte[] value] => Invoked(Convert.ToHexStringLower(value)),
                [byte[] value, int offset, int length] => Invoked(Convert.ToHexStringLower(value, offset, length)),
                _ => NotHandled,
            };
#endif

        private static WellKnownFunctionResult TryInvokeConversion<T>(
            object?[] args,
            Func<object?, T> conversion,
            Func<object?, IFormatProvider?, T> conversionWithProvider)
            => args switch
            {
                [var value] => Invoked(conversion(value)),
                [var value, IFormatProvider provider] => Invoked(conversionWithProvider(value, provider)),
                _ => NotHandled,
            };
    }
}
