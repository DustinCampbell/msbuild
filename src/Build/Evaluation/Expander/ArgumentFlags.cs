// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Describes the processing required before a function argument can be consumed.
/// </summary>
[Flags]
internal enum ArgumentFlags : byte
{
    /// <summary>
    ///  The source can be consumed directly.
    /// </summary>
    None = 0,

    /// <summary>
    ///  The source contains a property expression that must be expanded.
    /// </summary>
    ExpandProperties = 1,

    /// <summary>
    ///  The source contains an MSBuild <c>%XX</c> escape sequence that must be decoded.
    /// </summary>
    Unescape = 2,

    /// <summary>
    ///  The source requires all supported processing.
    /// </summary>
    All = ExpandProperties | Unescape,
}
