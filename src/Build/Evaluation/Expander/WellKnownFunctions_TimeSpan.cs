// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class TimeSpanHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(TimeSpan.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var left, var right] &&
                       TryConvertToTimeSpan(left, out TimeSpan convertedLeft) &&
                       TryConvertToTimeSpan(right, out TimeSpan convertedRight)
                    ? Invoked(TimeSpan.Equals(convertedLeft, convertedRight))
                    : NotHandled;
            }

            return NotHandled;
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
    }
}
