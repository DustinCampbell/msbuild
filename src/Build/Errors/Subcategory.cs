// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Internal;

/// <summary>
///  Identifies the subcategory of an invalid-project error.
/// </summary>
internal enum Subcategory
{
    /// <summary>
    ///  No error subcategory.
    /// </summary>
    None,

    /// <summary>
    ///  An error in a solution file.
    /// </summary>
    SolutionFile,
}
