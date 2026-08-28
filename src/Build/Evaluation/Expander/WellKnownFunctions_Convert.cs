// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal partial class WellKnownFunctions
{
    internal static bool TryExecuteConvertFunction(
        StringSegment methodName,
        ref FunctionArguments args,
        out object? result)
    {
        if (methodName.Equals(nameof(Convert.ToUInt32), StringComparison.OrdinalIgnoreCase) &&
            args.Length == 1)
        {
            if (args.TryGetSegment(0, out StringSegment value))
            {
                if (uint.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out uint converted))
                {
                    result = converted;
                    return true;
                }
            }
            else if (args.GetValue(0) is null)
            {
                result = 0u;
                return true;
            }
        }

        result = null;
        return false;
    }
}
