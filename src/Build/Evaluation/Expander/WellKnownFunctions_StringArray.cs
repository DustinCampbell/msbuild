// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class ArrayHandler
    {
        public WellKnownFunctionResult TryInvokeInstance(string methodName, Array array, object?[] args)
            => methodName.Length switch
            {
                4 when methodName.Equals(nameof(Array.Rank), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.Rank) : NotHandled,
                6 when methodName.Equals(nameof(Array.Length), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.Length) : NotHandled,
                8 when methodName.Equals(nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetValue(array, args),
                10 when methodName.Equals(nameof(Array.LongLength), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(array.LongLength) : NotHandled,

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetValue(Array array, object?[] args)
            => args is [var index] && ArgumentParser.TryConvertToInt(index, out int convertedIndex)
                ? Invoked(array.GetValue(convertedIndex))
                : NotHandled;
    }
}
