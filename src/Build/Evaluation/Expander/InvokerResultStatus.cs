// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Describes whether a function call was handled.
/// </summary>
internal enum InvokerResultStatus
{
    /// <summary>
    ///  The call was not handled and may fall back to reflection.
    /// </summary>
    Unhandled,

    /// <summary>
    ///  The call was handled, including calls that return <see langword="null"/> or <see langword="void"/>.
    /// </summary>
    Handled,
}
