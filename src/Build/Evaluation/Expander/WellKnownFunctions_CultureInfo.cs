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
            if (methodName.Equals(nameof(CultureInfo.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(culture.ToString()) : NotHandled;
            }

            return NotHandled;
        }
    }
}
