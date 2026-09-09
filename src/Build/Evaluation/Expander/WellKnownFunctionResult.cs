// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Describes the result of attempting to execute a well-known function.
/// </summary>
internal enum WellKnownFunctionResult
{
    /// <summary>
    ///  The function or overload was not recognized and may fall back to reflection.
    /// </summary>
    NotRecognized,

    /// <summary>
    ///  The function was executed successfully.
    /// </summary>
    Handled,

    /// <summary>
    ///  The function was recognized, but its arguments were invalid and must not fall back to reflection.
    /// </summary>
    InvalidArguments,
}
