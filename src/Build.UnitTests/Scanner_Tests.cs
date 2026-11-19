// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;
using TokenType = Microsoft.Build.Evaluation.Token.TokenType;

namespace Microsoft.Build.UnitTests;

public class ScannerTest
{
    [Theory]
    [InlineData("1234=5678", 6)]
    [InlineData(" \"foo", 2)]
    [InlineData(" @(foo", 2)]
    [InlineData(" @(", 2)]
    [InlineData(" $", 2)]
    [InlineData(" $(foo", 2)]
    [InlineData(" $(", 2)]
    [InlineData("'%24%28x' == '%24(x''", 21)]
    public void ReportsErrorPosition(string expression, int expectedPosition)
        => AssertErrorPosition(expression, ParserOptions.AllowAll, expectedPosition);

    [Theory]
    [InlineData(" @(foo)", 2)]
    [InlineData(" '@(foo)'", 3)]
    public void ReportsPositionOfDisallowedItemLists(string expression, int expectedPosition)
        => AssertErrorPosition(expression, ParserOptions.AllowProperties, expectedPosition);

    [Fact]
    public void ScansNumericTokenWithMultipleDots()
    {
        Scanner scanner = new("1==1.1.", ParserOptions.AllowAll);
        AssertNextToken(scanner, TokenType.Numeric);
        AssertNextToken(scanner, TokenType.EqualTo);
        AssertNextToken(scanner, TokenType.Numeric);
        scanner.IsNextString().ShouldBe("1.1.");
        AssertNextToken(scanner, TokenType.EndOfInput);
    }

    [Fact]
    public void ReportsSingleEqualsError()
    {
        Scanner scanner = ScanToError("a=b", ParserOptions.AllowProperties);
        scanner.GetErrorResource().ShouldBe("IllFormedEqualsInCondition");
        scanner.UnexpectedlyFound.ShouldBe("b");
    }

    [Theory]
    [InlineData("$(", "IllFormedPropertyCloseParenthesisInCondition")]
    [InlineData("$x", "IllFormedPropertyOpenParenthesisInCondition")]
    [InlineData("$(x )", "IllFormedPropertySpaceInCondition")]
    [InlineData("$( x)", "IllFormedPropertySpaceInCondition")]
    [InlineData("$([MSBuild]::DoSomething($(space ))", "IllFormedPropertySpaceInCondition")]
    [InlineData("$([MSBuild]::DoSomething($(_space ))", "IllFormedPropertySpaceInCondition")]
    public void ReportsMalformedProperty(string expression, string expectedResource)
    {
        Scanner scanner = ScanToError(expression, ParserOptions.AllowProperties);
        scanner.GetErrorResource().ShouldBe(expectedResource);
    }

    [Theory]
    [InlineData("$(x.StartsWith( 'y' ))")]
    [InlineData("$(x.StartsWith ('y'))")]
    [InlineData("$( x.StartsWith( $(SpacelessProperty) ) )")]
    [InlineData("$( x.StartsWith( $(_SpacelessProperty) ) )")]
    [InlineData("$(x.StartsWith('Foo', StringComparison.InvariantCultureIgnoreCase))")]
    public void AllowsWhitespaceInPropertyFunctions(string expression)
    {
        Scanner scanner = new(expression, ParserOptions.AllowProperties);
        AssertNextToken(scanner, TokenType.Property);
        AssertNextToken(scanner, TokenType.EndOfInput);
    }

    [Theory]
    [InlineData("@(", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@x", "IllFormedItemListOpenParenthesisInCondition")]
    [InlineData("@(x", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@(x->'%(y)", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x'", "IllFormedItemListCloseParenthesisInCondition")]
    public void ReportsMalformedItemList(string expression, string expectedResource)
    {
        Scanner scanner = ScanToError(expression);
        scanner.GetErrorResource().ShouldBe(expectedResource);
        scanner.UnexpectedlyFound.ShouldBeNull();
    }

    [Theory]
    [InlineData("false or 'abc")]
    [InlineData("'")]
    [InlineData("'$(DEBUG) == true")]
    public void ReportsUnterminatedQuotedString(string expression)
    {
        Scanner scanner = ScanToError(expression);
        scanner.GetErrorResource().ShouldBe("IllFormedQuotedStringInCondition");
        scanner.UnexpectedlyFound.ShouldBeNull();
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("-1234")]
    [InlineData("+1234")]
    [InlineData("1234.1234")]
    [InlineData(".1234")]
    [InlineData("1234.")]
    [InlineData("0x1234")]
    [InlineData("0X1234abcd")]
    [InlineData("0x1234ABCD")]
    public void ScansNumericTokens(string expression)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        AssertNextToken(scanner, TokenType.Numeric);
        scanner.IsNextString().ShouldBe(expression);
    }

    [Theory]
    [InlineData("$(foo)", (int)TokenType.Property)]
    [InlineData("@(foo)", (int)TokenType.ItemList)]
    [InlineData("(", (int)TokenType.LeftParenthesis)]
    [InlineData(")", (int)TokenType.RightParenthesis)]
    [InlineData(",", (int)TokenType.Comma)]
    [InlineData("==", (int)TokenType.EqualTo)]
    [InlineData("!=", (int)TokenType.NotEqualTo)]
    [InlineData("<", (int)TokenType.LessThan)]
    [InlineData(">", (int)TokenType.GreaterThan)]
    [InlineData("<=", (int)TokenType.LessThanOrEqualTo)]
    [InlineData(">=", (int)TokenType.GreaterThanOrEqualTo)]
    [InlineData("!", (int)TokenType.Not)]
    [InlineData("'String with an embedded \\' in it'", (int)TokenType.String)]
    public void ScansFirstToken(string expression, int expectedType)
        => AssertTokens(expression, (TokenType)expectedType);

    [Theory]
    [InlineData("abcde", (int)TokenType.String, "abcde")]
    [InlineData("'abc-efg'", (int)TokenType.String, "abc-efg")]
    [InlineData("and", (int)TokenType.And, "and")]
    [InlineData("or", (int)TokenType.Or, "or")]
    [InlineData("AnD", (int)TokenType.And, "and")]
    [InlineData("Or", (int)TokenType.Or, "or")]
    [InlineData("'String with a $(Property) inside'", (int)TokenType.String, "String with a $(Property) inside")]
    [InlineData("@(list, ' ')", (int)TokenType.ItemList, "@(list, ' ')")]
    [InlineData("@(files->'%(Filename)')", (int)TokenType.ItemList, "@(files->'%(Filename)')")]
    public void ScansTokenValues(string expression, int expectedType, string expectedValue)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        AssertNextToken(scanner, (TokenType)expectedType);
        scanner.IsNextString().ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData("@(Foo, ' ')", (int)TokenType.ItemList)]
    [InlineData("'@(Foo, ' ')'", (int)TokenType.String)]
    [InlineData("'%40(( '", (int)TokenType.String)]
    public void ScansStringEdgeCases(string expression, int expectedType)
        => AssertTokens(expression, (TokenType)expectedType, TokenType.EndOfInput);

    [Fact]
    public void ScansQuotedItemListInComparison()
        => AssertTokens(
            "'@(Complex_ItemType-123, ';')' == ''",
            TokenType.String,
            TokenType.EqualTo,
            TokenType.String,
            TokenType.EndOfInput);

    [Theory]
    [InlineData("Foo()", new int[0])]
    [InlineData("Foo( 1 )", new[] { (int)TokenType.Numeric })]
    [InlineData("Foo( $(Property) )", new[] { (int)TokenType.Property })]
    [InlineData("Foo( @(ItemList) )", new[] { (int)TokenType.ItemList })]
    [InlineData("Foo( simplestring )", new[] { (int)TokenType.String })]
    [InlineData("Foo( 'Not a Simple String' )", new[] { (int)TokenType.String })]
    [InlineData(
        "Foo( 'Not a Simple String', 1234 )",
        new[] { (int)TokenType.String, (int)TokenType.Comma, (int)TokenType.Numeric })]
    [InlineData(
        "Foo( $(Property), 'Not a Simple String', 1234 )",
        new[] { (int)TokenType.Property, (int)TokenType.Comma, (int)TokenType.String, (int)TokenType.Comma, (int)TokenType.Numeric })]
    [InlineData(
        "Foo( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )",
        new[]
        {
            (int)TokenType.ItemList, (int)TokenType.Comma, (int)TokenType.Property, (int)TokenType.Comma,
            (int)TokenType.String, (int)TokenType.Comma, (int)TokenType.String, (int)TokenType.Comma, (int)TokenType.Numeric,
        })]
    public void ScansFunctionTokens(string expression, int[] argumentTypes)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        AssertNextToken(scanner, TokenType.Function);
        scanner.IsNextString().ShouldBe("Foo");
        AssertNextToken(scanner, TokenType.LeftParenthesis);

        foreach (int argumentType in argumentTypes)
        {
            AssertNextToken(scanner, (TokenType)argumentType);
        }

        AssertNextToken(scanner, TokenType.RightParenthesis);
    }

    [Theory]
    [InlineData("'abc-efg'==$(foo)", (int)TokenType.String, (int)TokenType.EqualTo, (int)TokenType.Property)]
    [InlineData("$(debug)!=true", (int)TokenType.Property, (int)TokenType.NotEqualTo, (int)TokenType.String)]
    [InlineData("$(VERSION)<5", (int)TokenType.Property, (int)TokenType.LessThan, (int)TokenType.Numeric)]
    public void ScansComparisonTokens(string expression, int leftType, int comparisonType, int rightType)
        => AssertTokens(expression, (TokenType)leftType, (TokenType)comparisonType, (TokenType)rightType, TokenType.EndOfInput);

    [Fact]
    public void ScansWhitespaceSeparatedTokens()
        => AssertTokens("$(DEBUG) and $(FOO)", TokenType.Property, TokenType.And, TokenType.Property);

    [Theory]
    [InlineData("1234$(DEBUG)0xabcd@(foo)asdf<>'foo'<=false>=true==1234!=")]
    [InlineData("   1234    $(DEBUG)    0xabcd  \n@(foo)    \nasdf  \n<     \n>     \n'foo'  \n<=    \nfalse     \n>=    \ntrue  \n== \n 1234    \n!=     ")]
    public void ScansTokensWithAndWithoutWhitespace(string expression)
        => AssertTokens(
            expression,
            TokenType.Numeric,
            TokenType.Property,
            TokenType.Numeric,
            TokenType.ItemList,
            TokenType.String,
            TokenType.LessThan,
            TokenType.GreaterThan,
            TokenType.String,
            TokenType.LessThanOrEqualTo,
            TokenType.String,
            TokenType.GreaterThanOrEqualTo,
            TokenType.String,
            TokenType.EqualTo,
            TokenType.Numeric,
            TokenType.NotEqualTo,
            TokenType.EndOfInput);

    [Theory]
    [InlineData("@(foo)")]
    [InlineData("'1234 @(foo)'")]
    public void RejectsItemListsWhenDisallowed(string expression)
    {
        Scanner scanner = new(expression, ParserOptions.AllowProperties);
        scanner.Advance().ShouldBeFalse();
        scanner.GetErrorResource().ShouldBe("ItemListNotAllowedInThisConditional");
    }

    [Fact]
    public void RejectsItemListAfterAnotherTokenWhenDisallowed()
    {
        Scanner scanner = new("1234 '@(foo)'", ParserOptions.AllowProperties);
        scanner.Advance().ShouldBeTrue();
        scanner.Advance().ShouldBeFalse();
        scanner.GetErrorResource().ShouldBe("ItemListNotAllowedInThisConditional");
    }

    private static void AssertErrorPosition(string expression, ParserOptions options, int expectedPosition)
    {
        Scanner scanner = ScanToError(expression, options);
        scanner.GetErrorPosition().ShouldBe(expectedPosition);
    }

    private static Scanner ScanToError(string expression, ParserOptions options = ParserOptions.AllowAll)
    {
        Scanner scanner = new(expression, options);
        while (scanner.Advance() && !scanner.IsNext(TokenType.EndOfInput))
        {
        }

        scanner._errorState.ShouldBeTrue();
        return scanner;
    }

    private static void AssertTokens(string expression, params ReadOnlySpan<TokenType> expectedTokens)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        foreach (TokenType expectedToken in expectedTokens)
        {
            AssertNextToken(scanner, expectedToken);
        }
    }

    private static void AssertNextToken(Scanner scanner, TokenType expectedToken)
    {
        scanner.Advance().ShouldBeTrue();
        scanner.IsNext(expectedToken).ShouldBeTrue();
    }
}
