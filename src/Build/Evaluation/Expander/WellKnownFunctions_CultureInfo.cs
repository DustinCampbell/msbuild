// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class CultureInfoHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
            => args switch
            {
                [string name] => Invoked(new CultureInfo(name)),
                [string name, bool useUserOverride] => Invoked(new CultureInfo(name, useUserOverride)),
                [int culture] => Invoked(new CultureInfo(culture)),
                [int culture, bool useUserOverride] => Invoked(new CultureInfo(culture, useUserOverride)),
                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(CultureInfo.GetCultureInfo), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string name] => Invoked(CultureInfo.GetCultureInfo(name)),
                    [int culture] => Invoked(CultureInfo.GetCultureInfo(culture)),
                    [string name, string altName] => Invoked(CultureInfo.GetCultureInfo(name, altName)),
#if NET
                    [string name, bool predefinedOnly] => Invoked(CultureInfo.GetCultureInfo(name, predefinedOnly)),
#endif
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(CultureInfo.CurrentUICulture), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(CultureInfo.CurrentUICulture) : NotHandled;
            }

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, CultureInfo culture, object?[] args)
        {
            if (args is [])
            {
                WellKnownFunctionResult propertyResult = TryInvokeProperty(methodName, culture);
                if (propertyResult.Status == WellKnownFunctionStatus.Invoked)
                {
                    return propertyResult;
                }
            }

            return methodName switch
            {
                var name when name.Equals(nameof(CultureInfo.ClearCachedData), StringComparison.OrdinalIgnoreCase) && args is []
                    => ClearCachedData(culture),
                var name when name.Equals(nameof(CultureInfo.Clone), StringComparison.OrdinalIgnoreCase) && args is []
                    => Invoked(culture.Clone()),
                var name when name.Equals(nameof(CultureInfo.Equals), StringComparison.OrdinalIgnoreCase) && args is [var other]
                    => Invoked(culture.Equals(other)),
                var name when name.Equals(nameof(CultureInfo.GetConsoleFallbackUICulture), StringComparison.OrdinalIgnoreCase) && args is []
                    => Invoked(culture.GetConsoleFallbackUICulture()),
                var name when name.Equals(nameof(CultureInfo.GetFormat), StringComparison.OrdinalIgnoreCase) && args is [Type formatType]
                    => Invoked(culture.GetFormat(formatType)),
                var name when name.Equals(nameof(CultureInfo.GetHashCode), StringComparison.OrdinalIgnoreCase) && args is []
                    => Invoked(culture.GetHashCode()),
                var name when name.Equals("set_DateTimeFormat", StringComparison.OrdinalIgnoreCase) && args is [DateTimeFormatInfo value]
                    => SetDateTimeFormat(culture, value),
                var name when name.Equals("set_NumberFormat", StringComparison.OrdinalIgnoreCase) && args is [NumberFormatInfo value]
                    => SetNumberFormat(culture, value),
                var name when name.Equals(nameof(CultureInfo.ToString), StringComparison.OrdinalIgnoreCase) && args is []
                    => Invoked(culture.ToString()),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeProperty(string methodName, CultureInfo culture)
            => methodName switch
            {
                var name when name.Equals(nameof(CultureInfo.Calendar), StringComparison.OrdinalIgnoreCase) => Invoked(culture.Calendar),
                var name when name.Equals(nameof(CultureInfo.CompareInfo), StringComparison.OrdinalIgnoreCase) => Invoked(culture.CompareInfo),
                var name when name.Equals(nameof(CultureInfo.CultureTypes), StringComparison.OrdinalIgnoreCase) => Invoked(culture.CultureTypes),
                var name when name.Equals(nameof(CultureInfo.DateTimeFormat), StringComparison.OrdinalIgnoreCase) => Invoked(culture.DateTimeFormat),
                var name when name.Equals(nameof(CultureInfo.DisplayName), StringComparison.OrdinalIgnoreCase) => Invoked(culture.DisplayName),
                var name when name.Equals(nameof(CultureInfo.EnglishName), StringComparison.OrdinalIgnoreCase) => Invoked(culture.EnglishName),
                var name when name.Equals(nameof(CultureInfo.IetfLanguageTag), StringComparison.OrdinalIgnoreCase) => Invoked(culture.IetfLanguageTag),
                var name when name.Equals(nameof(CultureInfo.IsNeutralCulture), StringComparison.OrdinalIgnoreCase) => Invoked(culture.IsNeutralCulture),
                var name when name.Equals(nameof(CultureInfo.IsReadOnly), StringComparison.OrdinalIgnoreCase) => Invoked(culture.IsReadOnly),
                var name when name.Equals(nameof(CultureInfo.KeyboardLayoutId), StringComparison.OrdinalIgnoreCase) => Invoked(culture.KeyboardLayoutId),
                var name when name.Equals(nameof(CultureInfo.LCID), StringComparison.OrdinalIgnoreCase) => Invoked(culture.LCID),
                var name when name.Equals(nameof(CultureInfo.Name), StringComparison.OrdinalIgnoreCase) => Invoked(culture.Name),
                var name when name.Equals(nameof(CultureInfo.NativeName), StringComparison.OrdinalIgnoreCase) => Invoked(culture.NativeName),
                var name when name.Equals(nameof(CultureInfo.NumberFormat), StringComparison.OrdinalIgnoreCase) => Invoked(culture.NumberFormat),
                var name when name.Equals(nameof(CultureInfo.OptionalCalendars), StringComparison.OrdinalIgnoreCase) => Invoked(culture.OptionalCalendars),
                var name when name.Equals(nameof(CultureInfo.Parent), StringComparison.OrdinalIgnoreCase) => Invoked(culture.Parent),
                var name when name.Equals(nameof(CultureInfo.TextInfo), StringComparison.OrdinalIgnoreCase) => Invoked(culture.TextInfo),
                var name when name.Equals(nameof(CultureInfo.ThreeLetterISOLanguageName), StringComparison.OrdinalIgnoreCase)
                    => Invoked(culture.ThreeLetterISOLanguageName),
                var name when name.Equals(nameof(CultureInfo.ThreeLetterWindowsLanguageName), StringComparison.OrdinalIgnoreCase)
                    => Invoked(culture.ThreeLetterWindowsLanguageName),
                var name when name.Equals(nameof(CultureInfo.TwoLetterISOLanguageName), StringComparison.OrdinalIgnoreCase)
                    => Invoked(culture.TwoLetterISOLanguageName),
                var name when name.Equals(nameof(CultureInfo.UseUserOverride), StringComparison.OrdinalIgnoreCase) => Invoked(culture.UseUserOverride),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult ClearCachedData(CultureInfo culture)
        {
            culture.ClearCachedData();
            return Invoked(result: null);
        }

        private static WellKnownFunctionResult SetDateTimeFormat(CultureInfo culture, DateTimeFormatInfo value)
        {
            culture.DateTimeFormat = value;
            return Invoked(result: null);
        }

        private static WellKnownFunctionResult SetNumberFormat(CultureInfo culture, NumberFormatInfo value)
        {
            culture.NumberFormat = value;
            return Invoked(result: null);
        }
    }
}
