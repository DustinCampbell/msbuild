// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Internal;

/// <summary>
///  Identifies a diagnostic subcategory.
/// </summary>
internal enum Subcategory
{
    /// <summary>
    ///  No diagnostic subcategory.
    /// </summary>
    None,

    /// <summary>
    ///  A diagnostic associated with a solution file.
    /// </summary>
    SolutionFile,
}
