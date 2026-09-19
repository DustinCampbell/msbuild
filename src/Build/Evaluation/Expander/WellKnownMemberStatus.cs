// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Describes the outcome of attempting to access a well-known member.
/// </summary>
internal enum WellKnownMemberStatus
{
    /// <summary>
    ///  The member or overload was not recognized and may fall back to reflection.
    /// </summary>
    NotRecognized,

    /// <summary>
    ///  The member was accessed successfully.
    /// </summary>
    Handled,

    /// <summary>
    ///  The member was recognized, but its arguments were invalid and must not fall back to reflection.
    /// </summary>
    InvalidArguments,
}
