// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ParserTest
{
    [Theory]
    [InlineData("$(foo)")]
    [InlineData("$(foo)=='hello'")]
    [InlineData("$(foo)==''")]
    [InlineData("$(debug) and $(buildlab) and $(full)")]
    [InlineData("$(debug) or $(buildlab) or $(full)")]
    [InlineData("$(debug) and $(buildlab) or $(full)")]
    [InlineData("$(full) or $(debug) and $(buildlab)")]
    [InlineData("%(culture)")]
    [InlineData("%(culture)=='french'")]
    [InlineData("'foo_%(culture)'=='foo_french'")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("0.0 == 0")]
    public void ParsesSimpleConditions(string condition)
        => Parse(condition);

    [Theory]
    [InlineData("$(foo)")]
    [InlineData("($(foo) or $(bar)) and $(baz)")]
    [InlineData("$(foo) <= 5 and $(bar) >= 15")]
    [InlineData("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == simplestring) and 'a more complex string' != $(quux)")]
    [InlineData("(($(foo) or $(bar) == false) and !($(baz) == simplestring))")]
    [InlineData(@"(($(foo) or Exists('c:\foo.txt')) and !(($(baz) == simplestring)))")]
    [InlineData("'CONTAINS%27QUOTE%27' == '$(TestQuote)'")]
    public void ParsesComplexConditions(string condition)
        => Parse(condition);

    [Theory]
    [InlineData("!true")]
    [InlineData("!(true)")]
    [InlineData("!($(foo) <= 5)")]
    [InlineData("!(%(foo) <= 5)")]
    [InlineData("!($(foo) <= 5 and $(bar) >= 15)")]
    public void ParsesNegatedConditions(string condition)
        => Parse(condition);

    [Theory]
    [InlineData("SimpleFunctionCall()")]
    [InlineData("SimpleFunctionCall( 1234 )")]
    [InlineData("SimpleFunctionCall( true )")]
    [InlineData("SimpleFunctionCall( $(property) )")]
    [InlineData("SimpleFunctionCall( $(property), 1234, abcd, 'abcd efgh' )")]
    public void ParsesFunctionCalls(string condition)
        => Parse(condition);

    [Theory]
    [InlineData("@(foo) == 'a.cs;b.cs'")]
    [InlineData("'a.cs;b.cs' == @(foo)")]
    [InlineData("'@(foo)' == 'a.cs;b.cs'")]
    [InlineData("'otherstuff@(foo)' == 'a.cs;b.cs'")]
    [InlineData("'@(foo)otherstuff' == 'a.cs;b.cs'")]
    [InlineData("somefunction(@(foo), 'otherstuff')")]
    public void RejectsItemListsWhenDisallowed(string condition)
        => Should.Throw<InvalidProjectFileException>(()
                => Parse(condition, ParserOptions.AllowProperties));

    [Fact]
    public void ParsesItemFunctionAsString()
    {
        ParsedCondition condition = Parse("@(item->foo('ab'))", ParserOptions.AllowProperties | ParserOptions.AllowItemLists);

        condition.Root.ShouldBeOfType<StringExpressionNode>()
            .GetUnexpandedValue(null)
            .ShouldBe("@(item->foo('ab'))");
    }

    [Fact]
    public void ParsesNegatedItemFunction()
    {
        ParsedCondition condition = Parse("!@(item->foo())", ParserOptions.AllowProperties | ParserOptions.AllowItemLists);

        _ = condition.Root.ShouldBeOfType<NotExpressionNode>();
    }

    [Fact]
    public void ParsesItemFunctionsInAndExpression()
    {
        ParsedCondition condition = Parse(
            "(@(item->foo('ab')) and @(item->foo('bc')))",
            ParserOptions.AllowProperties | ParserOptions.AllowItemLists);

        _ = condition.Root.ShouldBeOfType<AndExpressionNode>();
    }

    [Theory]
    [InlineData("%(foo) == 'a.cs;b.cs'")]
    [InlineData("'a.cs;b.cs' == %(foo)")]
    [InlineData("'%(foo)' == 'a.cs;b.cs'")]
    [InlineData("'otherstuff%(foo)' == 'a.cs;b.cs'")]
    [InlineData("'%(foo)otherstuff' == 'a.cs;b.cs'")]
    [InlineData("somefunction(%(foo), 'otherstuff')")]
    public void RejectsMetadataWhenDisallowed(string condition)
        => Should.Throw<InvalidProjectFileException>(() =>
            Parse(condition, ParserOptions.AllowProperties | ParserOptions.AllowItemLists));

    [Theory]
    [InlineData("'a more complex' == 'asdf")]
    [InlineData("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == 'simple string) and 'a more complex string' != $(quux)")]
    [InlineData("($(foo) == 'simple string') $(bar)")]
    [InlineData("=='x'")]
    [InlineData("==")]
    [InlineData(">")]
    [InlineData("true!=false==")]
    [InlineData("true!=false==true")]
    [InlineData("1==(2")]
    public void RejectsMalformedConditions(string condition)
        => Should.Throw<InvalidProjectFileException>(() =>
            Parse(condition));

    [Theory]
    [InlineData(" @(foo", 2)]
    [InlineData("1234=5678", 6)]
    [InlineData("1==0xFG", 7)]
    [InlineData("1==-0xF", 6)]
    [InlineData(" ", 2)]
    [InlineData(" (", 3)]
    [InlineData(" false or  ", 12)]
    public void ReportsErrorPosition(string expression, int expectedPosition)
    {
        Parser parser = new();
        _ = Should.Throw<InvalidProjectFileException>(() => parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance));
        parser.errorPosition.ShouldBe(expectedPosition);
    }

    [Theory]
    [InlineData("$(a) == 1 and $(b) == 2 or $(c) == 3", true)]
    [InlineData("$(a) == 1 or $(b) == 2 and $(c) == 3", true)]
    [InlineData("($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4", true)]
    [InlineData("$(a) == 1 and $(b) == 2 and $(c) == 3", false)]
    [InlineData("$(a) == 1 or $(b) == 2 or $(c) == 3", false)]
    [InlineData("($(a) == 1 and $(b) == 2) or $(c) == 3", false)]
    [InlineData("($(a) == 1 or $(b) == 2) and $(c) == 3", false)]
    public void ReportsCompatibilityWarningForMixedAndOr(string condition, bool expectedWarning)
    {
        bool warningLogged = false;
        MockLoggingService loggingService = new(message =>
        {
            if (message == "ConditionMaybeEvaluatedIncorrectly")
            {
                warningLogged = true;
            }
        });

        EvaluationLoggingContext loggingContext = new(
            loggingService, BuildEventContext.Invalid, MockElementLocation.Instance.File);

        Parser parser = new();

        _ = parser.Parse(condition, ParserOptions.AllowAll, MockElementLocation.Instance, loggingContext);
        parser.LogWarnings(condition, loggingContext);

        warningLogged.ShouldBe(expectedWarning);
    }

    [Theory]
    [InlineData("(true Or false) And (false Or true)", 0)]
    [InlineData("((true And false) Or true) And false", 0)]
    [InlineData("!(true Or false) And true", 0)]
    [InlineData("true Or (false And true) And true", 1)]
    [InlineData("(true Or false And true) And (false Or true And false)", 1)]
    [InlineData("((true Or false And true))", 1)]
    public void OperatorWarningsRespectParentheses(string expression, int expectedWarningCount)
    {
        int warningCount = 0;
        MockLoggingService loggingService = new(message =>
        {
            if (message == "ConditionMaybeEvaluatedIncorrectly")
            {
                warningCount++;
            }
        });

        EvaluationLoggingContext loggingContext = new(
            loggingService, BuildEventContext.Invalid, MockElementLocation.Instance.File);

        Parser parser = new();

        _ = parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance, loggingContext);

        warningCount.ShouldBe(0);
        parser.LogWarnings(expression, loggingContext);
        warningCount.ShouldBe(expectedWarningCount);

        _ = parser.Parse("true", ParserOptions.AllowAll, MockElementLocation.Instance, loggingContext);
        parser.LogWarnings("true", loggingContext);
        warningCount.ShouldBe(expectedWarningCount);
    }

    private static ParsedCondition Parse(string condition, ParserOptions options = ParserOptions.AllowAll) =>
        new Parser().Parse(condition, options, MockElementLocation.Instance);
}
