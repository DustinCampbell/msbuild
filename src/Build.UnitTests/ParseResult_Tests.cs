// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ParseResult_Tests
{
    [Fact]
    public void RejectsUninitializedDiagnostics()
    {
        ParsedCondition condition = Parser.Parse("true", ParserOptions.AllowAll).Condition.ShouldNotBeNull();
        ImmutableArray<ParseDiagnostic> diagnostics = default;

        Should.Throw<InternalErrorException>(() => new ParseResult(condition, diagnostics));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsMissingConditionWithoutError(bool warningsOnly)
    {
        ImmutableArray<ParseDiagnostic> diagnostics = warningsOnly
            ? Parser.Parse("true or false and true", ParserOptions.AllowAll).Diagnostics
            : [];

        Should.Throw<InternalErrorException>(() => new ParseResult(null, diagnostics));
    }

    [Fact]
    public void RejectsConditionWithErrors()
    {
        ParsedCondition condition = Parser.Parse("true", ParserOptions.AllowAll).Condition.ShouldNotBeNull();
        ImmutableArray<ParseDiagnostic> diagnostics = Parser.Parse(")", ParserOptions.AllowAll).Diagnostics;

        Should.Throw<InternalErrorException>(() => new ParseResult(condition, diagnostics));
    }
}
