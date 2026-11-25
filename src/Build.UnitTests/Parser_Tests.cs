// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
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

    [Fact]
    public void ParsesOperatorsWithGrammarPrecedence()
    {
        OrExpressionNode root = Parse("!a == b or c and d").Root.ShouldBeOfType<OrExpressionNode>();
        EqualExpressionNode comparison = root.LeftChild.ShouldBeOfType<EqualExpressionNode>();

        _ = comparison.LeftChild.ShouldBeOfType<NotExpressionNode>();
        _ = root.RightChild.ShouldBeOfType<AndExpressionNode>();
    }

    [Fact]
    public void ParsesBooleanChainsLeftAssociatively()
    {
        OrExpressionNode orNode = Parse("a or b or c").Root.ShouldBeOfType<OrExpressionNode>();
        _ = orNode.LeftChild.ShouldBeOfType<OrExpressionNode>();
        orNode.RightChild.ShouldBeOfType<StringExpressionNode>().GetUnexpandedValue(null).ShouldBe("c");

        AndExpressionNode andNode = Parse("a and b and c").Root.ShouldBeOfType<AndExpressionNode>();
        _ = andNode.LeftChild.ShouldBeOfType<AndExpressionNode>();
        andNode.RightChild.ShouldBeOfType<StringExpressionNode>().GetUnexpandedValue(null).ShouldBe("c");
    }

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
    [InlineData("SimpleFunctionCall(,)")]
    [InlineData("SimpleFunctionCall(a,)")]
    [InlineData("SimpleFunctionCall(,a)")]
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
        => ParseError(condition, ParserOptions.AllowProperties)
            .ResourceName.ShouldBe("ItemListNotAllowedInThisConditional");

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
        => ParseError(condition, ParserOptions.AllowProperties | ParserOptions.AllowItemLists)
            .ResourceName.ShouldBe("CustomMetadataNotAllowedInThisConditional");

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
    [InlineData("f(a == b)")]
    [InlineData("f((a))")]
    [InlineData("f(g(a))")]
    public void RejectsMalformedConditions(string condition)
        => ParseError(condition);

    [Theory]
    [InlineData(" @(foo", 2)]
    [InlineData("1234=5678", 6)]
    [InlineData("1==0xFG", 7)]
    [InlineData("1==-0xF", 6)]
    [InlineData(" ", 2)]
    [InlineData(" (", 3)]
    [InlineData(" false or  ", 12)]
    [InlineData("true false", 6)]
    [InlineData("(true", 6)]
    [InlineData("true == )", 9)]
    [InlineData("f(a", 4)]
    public void ReportsErrorPosition(string expression, int expectedPosition)
        => ParseError(expression).Position.ShouldBe(expectedPosition);

    [Theory]
    [InlineData("true false", "false", 6)]
    [InlineData("true == )", ")", 9)]
    [InlineData("f(a", "", 4)]
    public void CollectsUnexpectedTokenDiagnostics(string condition, string unexpectedToken, int expectedPosition)
    {
        ParseDiagnostic diagnostic = ParseError(condition);

        diagnostic.ResourceName.ShouldBe("UnexpectedTokenInCondition");
        diagnostic.Position.ShouldBe(expectedPosition);
        diagnostic.Arguments.ShouldBe([condition, unexpectedToken, expectedPosition]);
    }

    [Theory]
    [InlineData("?", 1)]
    [InlineData("true ? or false and true", 6)]
    [InlineData("true == ?", 9)]
    public void CollectsScannerDiagnosticsWithoutAddingParserErrors(string condition, int expectedPosition)
    {
        ParseDiagnostic diagnostic = ParseError(condition);

        diagnostic.ResourceName.ShouldBe("UnexpectedCharacterInCondition");
        diagnostic.Position.ShouldBe(expectedPosition);
        diagnostic.Arguments.ShouldBe([condition, expectedPosition, "?"]);
    }

    [Fact]
    public void OmitsUnusedScannerFormatArgument()
    {
        const string condition = "true == '@(foo";
        ParseDiagnostic diagnostic = ParseError(condition);

        diagnostic.ResourceName.ShouldBe("IllFormedQuotedStringInCondition");
        diagnostic.Position.ShouldBe(9);
        diagnostic.Arguments.ShouldBe([condition, 9]);
    }

    [Theory]
    [InlineData("%(Filename)", "BuiltInMetadataNotAllowedInThisConditional", 11, "Filename")]
    [InlineData("'%(Filename)'", "BuiltInMetadataNotAllowedInThisConditional", 1, "Filename")]
    [InlineData("%(Custom)", "CustomMetadataNotAllowedInThisConditional", 9, "Custom")]
    [InlineData("'%(Custom)'", "CustomMetadataNotAllowedInThisConditional", 1, "Custom")]
    public void PreservesMetadataDiagnosticPositions(
        string condition, string expectedResource, int expectedPosition, string metadataName)
    {
        ParseDiagnostic diagnostic = ParseError(condition, ParserOptions.AllowProperties);

        diagnostic.ResourceName.ShouldBe(expectedResource);
        diagnostic.Position.ShouldBe(expectedPosition);
        diagnostic.Arguments.ShouldBe([condition, expectedPosition, metadataName]);
    }

    [Fact]
    public void PreservesWhitespaceDiagnosticPosition()
    {
        const string condition = "$( x )";
        ParseDiagnostic diagnostic = ParseError(condition);

        diagnostic.ResourceName.ShouldBe("IllFormedPropertySpaceInCondition");
        diagnostic.Position.ShouldBe(4);
        diagnostic.Arguments.ShouldBe([condition, 4, " "]);
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
        ParseResult result = Parser.Parse(condition, ParserOptions.AllowAll);
        result.Succeeded.ShouldBeTrue();

        if (expectedWarning)
        {
            ParseDiagnostic diagnostic = result.Diagnostics.ShouldHaveSingleItem();
            diagnostic.Severity.ShouldBe(ParseDiagnosticSeverity.Warning);
            diagnostic.ResourceName.ShouldBe("ConditionMaybeEvaluatedIncorrectly");
            diagnostic.Position.ShouldBe(0);
            diagnostic.Arguments.ShouldBe([condition]);
        }
        else
        {
            result.Diagnostics.ShouldBeEmpty();
        }
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
        ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll);
        result.Succeeded.ShouldBeTrue();
        result.Diagnostics.Length.ShouldBe(expectedWarningCount);

        Parser.Parse("true", ParserOptions.AllowAll).Diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("true or false and true false", 1, "UnexpectedTokenInCondition")]
    [InlineData("true or false and", 0, "UnexpectedTokenInCondition")]
    [InlineData("true and false or true and false or ?", 0, "UnexpectedCharacterInCondition")]
    [InlineData("true or false and true and ?", 0, "UnexpectedCharacterInCondition")]
    [InlineData("(true or false and true) ?", 1, "UnexpectedCharacterInCondition")]
    public void RetainsRecordedWarningsWhenParsingFails(
        string expression, int expectedWarningCount, string expectedErrorResource)
    {
        ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll);
        result.Succeeded.ShouldBeFalse();
        result.Diagnostics.Length.ShouldBe(expectedWarningCount + 1);
        result.Diagnostics[^1].Severity.ShouldBe(ParseDiagnosticSeverity.Error);
        result.Diagnostics[^1].ResourceName.ShouldBe(expectedErrorResource);

        if (expectedWarningCount != 0)
        {
            result.Diagnostics[0].Severity.ShouldBe(ParseDiagnosticSeverity.Warning);
            result.Diagnostics[0].ResourceName.ShouldBe("ConditionMaybeEvaluatedIncorrectly");
            result.Diagnostics[0].Position.ShouldBe(0);
            result.Diagnostics[0].Arguments.ShouldBe([expression]);
        }
    }

    private static ParsedCondition Parse(string condition, ParserOptions options = ParserOptions.AllowAll)
    {
        ParseResult result = Parser.Parse(condition, options);
        result.Succeeded.ShouldBeTrue();
        return result.Condition.ShouldNotBeNull();
    }

    private static ParseDiagnostic ParseError(string condition, ParserOptions options = ParserOptions.AllowAll)
    {
        ParseResult result = Parser.Parse(condition, options);
        result.Succeeded.ShouldBeFalse();
        ParseDiagnostic diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(ParseDiagnosticSeverity.Error);
        return diagnostic;
    }
}
