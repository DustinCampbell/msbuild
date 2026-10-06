// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Expansion.FunctionInvocation;

internal static partial class ArgumentBinder
{
    /// <summary>
    ///  Describes the result of comparing two applicable overloads or parameter types.
    /// </summary>
    private enum Specificity
    {
        /// <summary>
        ///  Neither candidate is more specific.
        /// </summary>
        Neither,

        /// <summary>
        ///  The first candidate is more specific.
        /// </summary>
        First,

        /// <summary>
        ///  The second candidate is more specific.
        /// </summary>
        Second,
    }
}
