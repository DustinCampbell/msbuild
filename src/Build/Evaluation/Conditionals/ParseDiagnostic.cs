// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Describes a condition-parsing error or warning without formatting or reporting it.
/// </summary>
/// <param name="Severity">Whether the diagnostic is an error or warning.</param>
/// <param name="ResourceName">The resource name used to produce the diagnostic message.</param>
/// <param name="Position">
///  The diagnostic-specific position in the condition text, or <c>0</c> for the entire condition.
///  Existing scanner and parser position conventions are preserved.
/// </param>
/// <param name="Arguments">The complete resource-formatting arguments, including any diagnostic position.</param>
/// <remarks>
///  The diagnostic owns <paramref name="Arguments"/>. Callers must not modify the array.
///  <paramref name="Position"/> is metadata and is not automatically inserted into the arguments.
/// </remarks>
internal readonly record struct ParseDiagnostic(
    ParseDiagnosticSeverity Severity,
    string ResourceName,
    int Position,
    object?[] Arguments);
