// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Describes the state storage required for each evaluation of a parsed condition.
/// </summary>
/// <param name="StringSlotCount">
/// Number of per-evaluation slots, assigned only to expandable string nodes.
/// Each slot indexes one node's cached expansion.
/// </param>
internal readonly record struct NodeStateLayout(int StringSlotCount);

/// <summary>
/// A reusable parsed condition with a shared expression tree and its node-state layout.
/// </summary>
/// <param name="Root">Root of the shared expression tree.</param>
/// <param name="StateLayout">Layout for each evaluation's private node-state storage.</param>
internal sealed record ParsedCondition(GenericExpressionNode Root, NodeStateLayout StateLayout)
{
    /// <summary>
    ///  Parses an uncached condition and reports its diagnostics.
    /// </summary>
    /// <param name="condition">The condition text to parse.</param>
    /// <param name="options">The permitted reference forms. Must include <see cref="ParserOptions.AllowProperties"/>.</param>
    /// <param name="location">The project-file location associated with reported diagnostics.</param>
    /// <param name="loggingContext">
    ///  The destination for collected warnings, or <see langword="null"/> to omit them.
    /// </param>
    /// <returns>
    ///  The completed condition and its evaluation-state layout.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">The condition produced an error diagnostic.</exception>
    /// <remarks>
    ///  Warnings are reported for both successful and failed parses. Use
    ///  <see cref="Parser.Parse(string, ParserOptions)"/> to inspect diagnostics without reporting them.
    ///  Error diagnostics are reported regardless of whether <paramref name="loggingContext"/> is supplied.
    /// </remarks>
    internal static ParsedCondition Parse(
        string condition,
        ParserOptions options,
        ElementLocation location,
        LoggingContext? loggingContext = null)
    {
        ParseResult result = Parser.Parse(condition, options);
        Parser.ReportDiagnostics(result.Diagnostics, location, loggingContext);

        if (result.Succeeded)
        {
            return result.Condition;
        }

        return Assumed.Unreachable<ParsedCondition>("A failed parse must have an error diagnostic.");
    }
}
