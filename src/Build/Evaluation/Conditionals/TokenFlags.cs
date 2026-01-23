// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Flags that provide additional metadata about a token.
/// </summary>
[Flags]
internal enum TokenFlags : byte
{
    /// <summary>
    ///  No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    ///  Indicates that the token contains expandable content such as property expressions or escaped characters.
    /// </summary>
    IsExpandable = 1 << 0,
}
