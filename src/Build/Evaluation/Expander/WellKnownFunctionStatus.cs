// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Identifies whether a well-known function was invoked without reflection.
/// </summary>
internal enum WellKnownFunctionStatus
{
    /// <summary>
    ///  The function was not handled and may require reflection.
    /// </summary>
    NotHandled,

    /// <summary>
    ///  The function was invoked without reflection.
    /// </summary>
    Invoked,
}
