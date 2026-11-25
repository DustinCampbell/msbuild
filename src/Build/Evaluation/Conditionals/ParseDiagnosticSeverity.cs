// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Identifies the severity of a condition-parsing diagnostic.
/// </summary>
internal enum ParseDiagnosticSeverity
{
    /// <summary>
    ///  The condition could not be parsed.
    /// </summary>
    Error,

    /// <summary>
    ///  The condition has a compatibility concern that does not prevent parsing.
    /// </summary>
    Warning,
}
