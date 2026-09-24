// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class CharHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (args is [var rawValue])
            {
                if (methodName.Equals(nameof(char.Parse), StringComparison.OrdinalIgnoreCase) && rawValue is string parseText)
                {
                    return Invoked(char.Parse(parseText));
                }

                if (methodName.Equals(nameof(char.ConvertFromUtf32), StringComparison.OrdinalIgnoreCase)
                    && ArgumentParser.TryConvertToInt(rawValue, out int utf32))
                {
                    return Invoked(char.ConvertFromUtf32(utf32));
                }

                if (args.TryGetArg(0, out char character))
                {
                    WellKnownFunctionResult result = TryInvokeCharacter(methodName, character);

                    if (result.Status == WellKnownFunctionStatus.Invoked)
                    {
                        return result;
                    }
                }
            }

            if (args is [string textValue, var index]
                && ArgumentParser.TryConvertToInt(index, out int convertedIndex))
            {
                WellKnownFunctionResult result = TryInvokeStringIndex(methodName, textValue, convertedIndex);

                if (result.Status == WellKnownFunctionStatus.Invoked)
                {
                    return result;
                }
            }

            if (args is [var left, var right]
                && ArgumentParser.TryConvertToChar(left, out char convertedLeft)
                && ArgumentParser.TryConvertToChar(right, out char convertedRight))
            {
                if (methodName.Equals(nameof(char.ConvertToUtf32), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(char.ConvertToUtf32(convertedLeft, convertedRight));
                }

                if (methodName.Equals(nameof(char.IsSurrogatePair), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(char.IsSurrogatePair(convertedLeft, convertedRight));
                }
            }

            if (args is [var valueWithCulture, CultureInfo culture]
                && ArgumentParser.TryConvertToChar(valueWithCulture, out char characterWithCulture))
            {
                if (methodName.Equals(nameof(char.ToLower), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(char.ToLower(characterWithCulture, culture));
                }

                if (methodName.Equals(nameof(char.ToUpper), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(char.ToUpper(characterWithCulture, culture));
                }
            }

#if NET
            if (args is [var charValue, var min, var max]
                && ArgumentParser.TryConvertToChar(charValue, out char convertedValue)
                && ArgumentParser.TryConvertToChar(min, out char convertedMin)
                && ArgumentParser.TryConvertToChar(max, out char convertedMax)
                && methodName.Equals(nameof(char.IsBetween), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(char.IsBetween(convertedValue, convertedMin, convertedMax));
            }
#endif

            return s_primitiveHandler.TryInvokeStatic(typeof(char), methodName, args);
        }

        private static WellKnownFunctionResult TryInvokeCharacter(string methodName, char value)
            => methodName switch
            {
                var name when name.Equals(nameof(char.GetNumericValue), StringComparison.OrdinalIgnoreCase) => Invoked(char.GetNumericValue(value)),
                var name when name.Equals(nameof(char.GetUnicodeCategory), StringComparison.OrdinalIgnoreCase) => Invoked(char.GetUnicodeCategory(value)),
                var name when name.Equals(nameof(char.IsControl), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsControl(value)),
                var name when name.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsDigit(value)),
                var name when name.Equals(nameof(char.IsHighSurrogate), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsHighSurrogate(value)),
                var name when name.Equals(nameof(char.IsLetter), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsLetter(value)),
                var name when name.Equals(nameof(char.IsLetterOrDigit), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsLetterOrDigit(value)),
                var name when name.Equals(nameof(char.IsLower), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsLower(value)),
                var name when name.Equals(nameof(char.IsLowSurrogate), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsLowSurrogate(value)),
                var name when name.Equals(nameof(char.IsNumber), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsNumber(value)),
                var name when name.Equals(nameof(char.IsPunctuation), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsPunctuation(value)),
                var name when name.Equals(nameof(char.IsSeparator), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsSeparator(value)),
                var name when name.Equals(nameof(char.IsSurrogate), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsSurrogate(value)),
                var name when name.Equals(nameof(char.IsSymbol), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsSymbol(value)),
                var name when name.Equals(nameof(char.IsUpper), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsUpper(value)),
                var name when name.Equals(nameof(char.IsWhiteSpace), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsWhiteSpace(value)),
                var name when name.Equals(nameof(char.ToLower), StringComparison.OrdinalIgnoreCase) => Invoked(char.ToLower(value)),
                var name when name.Equals(nameof(char.ToLowerInvariant), StringComparison.OrdinalIgnoreCase) => Invoked(char.ToLowerInvariant(value)),
                var name when name.Equals(nameof(char.ToString), StringComparison.OrdinalIgnoreCase) => Invoked(char.ToString(value)),
                var name when name.Equals(nameof(char.ToUpper), StringComparison.OrdinalIgnoreCase) => Invoked(char.ToUpper(value)),
                var name when name.Equals(nameof(char.ToUpperInvariant), StringComparison.OrdinalIgnoreCase) => Invoked(char.ToUpperInvariant(value)),
#if NET
                var name when name.Equals(nameof(char.IsAscii), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAscii(value)),
                var name when name.Equals(nameof(char.IsAsciiDigit), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAsciiDigit(value)),
                var name when name.Equals(nameof(char.IsAsciiHexDigit), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAsciiHexDigit(value)),
                var name when name.Equals(nameof(char.IsAsciiHexDigitLower), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAsciiHexDigitLower(value)),
                var name when name.Equals(nameof(char.IsAsciiHexDigitUpper), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAsciiHexDigitUpper(value)),
                var name when name.Equals(nameof(char.IsAsciiLetter), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAsciiLetter(value)),
                var name when name.Equals(nameof(char.IsAsciiLetterLower), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAsciiLetterLower(value)),
                var name when name.Equals(nameof(char.IsAsciiLetterOrDigit), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAsciiLetterOrDigit(value)),
                var name when name.Equals(nameof(char.IsAsciiLetterUpper), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsAsciiLetterUpper(value)),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeStringIndex(string methodName, string value, int index)
            => methodName switch
            {
                var name when name.Equals(nameof(char.ConvertToUtf32), StringComparison.OrdinalIgnoreCase) => Invoked(char.ConvertToUtf32(value, index)),
                var name when name.Equals(nameof(char.GetNumericValue), StringComparison.OrdinalIgnoreCase) => Invoked(char.GetNumericValue(value, index)),
                var name when name.Equals(nameof(char.GetUnicodeCategory), StringComparison.OrdinalIgnoreCase) =>
                    Invoked(char.GetUnicodeCategory(value, index)),
                var name when name.Equals(nameof(char.IsControl), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsControl(value, index)),
                var name when name.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsDigit(value, index)),
                var name when name.Equals(nameof(char.IsHighSurrogate), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsHighSurrogate(value, index)),
                var name when name.Equals(nameof(char.IsLetter), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsLetter(value, index)),
                var name when name.Equals(nameof(char.IsLetterOrDigit), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsLetterOrDigit(value, index)),
                var name when name.Equals(nameof(char.IsLower), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsLower(value, index)),
                var name when name.Equals(nameof(char.IsLowSurrogate), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsLowSurrogate(value, index)),
                var name when name.Equals(nameof(char.IsNumber), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsNumber(value, index)),
                var name when name.Equals(nameof(char.IsPunctuation), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsPunctuation(value, index)),
                var name when name.Equals(nameof(char.IsSeparator), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsSeparator(value, index)),
                var name when name.Equals(nameof(char.IsSurrogate), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsSurrogate(value, index)),
                var name when name.Equals(nameof(char.IsSurrogatePair), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsSurrogatePair(value, index)),
                var name when name.Equals(nameof(char.IsSymbol), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsSymbol(value, index)),
                var name when name.Equals(nameof(char.IsUpper), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsUpper(value, index)),
                var name when name.Equals(nameof(char.IsWhiteSpace), StringComparison.OrdinalIgnoreCase) => Invoked(char.IsWhiteSpace(value, index)),
                _ => NotHandled,
            };
    }
}
