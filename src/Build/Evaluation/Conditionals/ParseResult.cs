// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Contains a condition-parsing outcome and its unreported diagnostics.
/// </summary>
/// <remarks>
///  Construct results through the validating constructor. A default-initialized value is not a valid parse outcome.
/// </remarks>
internal readonly struct ParseResult
{
    /// <summary>
    ///  Initializes a result whose condition and diagnostics describe the same parsing outcome.
    /// </summary>
    /// <param name="condition">The completed condition, or <see langword="null"/> if parsing failed.</param>
    /// <param name="diagnostics">The initialized diagnostics collection, in discovery order.</param>
    /// <exception cref="InternalErrorException">
    ///  <paramref name="diagnostics"/> is uninitialized, contains errors alongside a completed condition,
    ///  or contains no errors when <paramref name="condition"/> is <see langword="null"/>.
    /// </exception>
    public ParseResult(ParsedCondition? condition, ImmutableArray<ParseDiagnostic> diagnostics)
    {
        Assumed.False(diagnostics.IsDefault, "Diagnostics must be initialized.");

        bool hasError = false;
        foreach (ParseDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Severity == ParseDiagnosticSeverity.Error)
            {
                hasError = true;
                break;
            }
        }

        if (condition is null)
        {
            Assumed.True(hasError, "Diagnostics must contain an error when no condition is supplied.");
        }
        else
        {
            Assumed.False(hasError, "Diagnostics must not contain errors when a condition is supplied.");
        }

        Condition = condition;
        Diagnostics = diagnostics;
    }

    /// <summary>
    ///  Gets the completed condition, or <see langword="null"/> if parsing failed.
    /// </summary>
    public ParsedCondition? Condition { get; }

    /// <summary>
    ///  Gets the diagnostics in discovery order, including warnings collected before a failure.
    /// </summary>
    public ImmutableArray<ParseDiagnostic> Diagnostics { get; }

    /// <summary>
    ///  Gets whether parsing produced a complete condition.
    /// </summary>
    /// <remarks>
    ///  A successful result can contain warnings. A failed result contains an error and no partial condition.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(Condition))]
    public bool Succeeded => Condition is not null;
}
