// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Publishes and reuses a parsed condition for one condition text and parser-options pair.
/// </summary>
internal sealed class CachedCondition
{
    private ParsedCondition? _condition;

    /// <summary>
    /// Gets the published condition or parses and attempts to publish it.
    /// </summary>
    /// <remarks>
    /// Concurrent cache misses may parse independently; only the caller that wins publication reports successful-parse warnings.
    /// Parse failures are not cached.
    /// </remarks>
    public ParsedCondition GetOrParse(
        string condition,
        ParserOptions options,
        ElementLocation location,
        LoggingContext? loggingContext = null)
    {
        ParsedCondition? parsed = Volatile.Read(ref _condition);
        return parsed ?? Initialize(condition, options, location, loggingContext);
    }

    private ParsedCondition Initialize(
        string condition,
        ParserOptions options,
        ElementLocation location,
        LoggingContext? loggingContext)
    {
        ParseResult result = Parser.Parse(condition, options);
        if (!result.Succeeded)
        {
            Parser.ReportDiagnostics(result.Diagnostics, location, loggingContext);
            return Assumed.Unreachable<ParsedCondition>("A failed parse must have an error diagnostic.");
        }

        ParsedCondition parsed = result.Condition;
        ParsedCondition? existing = Interlocked.CompareExchange(ref _condition, parsed, null);
        if (existing is not null)
        {
            return existing;
        }

        Parser.ReportDiagnostics(result.Diagnostics, location, loggingContext);
        return parsed;
    }
}
