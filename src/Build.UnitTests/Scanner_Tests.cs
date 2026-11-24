// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

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
    [InlineData("$( x )", 4)]
    [InlineData("$(A$( x ))", 7)]
    [InlineData("'%24%28x' == '%24(x''", 21)]
    public void ReportsErrorPosition(string expression, int expectedPosition)
        => AssertErrorPosition(expression, ParserOptions.AllowAll, expectedPosition);

    [Theory]
    [InlineData(" @(foo)", 2)]
    [InlineData(" '@(foo)'", 3)]
    public void ReportsPositionOfDisallowedItemLists(string expression, int expectedPosition)
        => AssertErrorPosition(expression, ParserOptions.AllowProperties, expectedPosition);

    [Theory]
    [InlineData("true false", new[] { 1, 6, 11 })]
    [InlineData("  true == false  ", new[] { 3, 8, 11, 18 })]
    public void ReportsTokenPositions(string expression, int[] expectedPositions)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        foreach (int expectedPosition in expectedPositions)
        {
            scanner.Advance().ShouldBeTrue();
            scanner.TokenPosition.ShouldBe(expectedPosition);
        }

        scanner.IsKind(TokenKind.EndOfInput).ShouldBeTrue();
        scanner.Advance().ShouldBeTrue();
        scanner.TokenPosition.ShouldBe(expectedPositions[^1]);
    }

    [Fact]
    public void ScansNumericTokenWithMultipleDots()
    {
        Scanner scanner = new("1==1.1.", ParserOptions.AllowAll);
        AssertNextToken(scanner, TokenKind.Number);
        AssertNextToken(scanner, TokenKind.EqualTo);
        AssertNextToken(scanner, TokenKind.Number);
        scanner.CurrentTokenText.ShouldBe("1.1.");
        AssertNextToken(scanner, TokenKind.EndOfInput);
    }

    [Fact]
    public void ReportsSingleEqualsError()
    {
        Scanner scanner = ScanToError("a=b", ParserOptions.AllowProperties);
        scanner.ErrorResource.ShouldBe("IllFormedEqualsInCondition");
        scanner.ErrorFormatArg.ShouldBe("b");
        scanner.TokenPosition.ShouldBe(2);
        scanner.ErrorPosition.ShouldBe(3);
    }

    [Theory]
    [InlineData("$", "IllFormedPropertyCloseParenthesisInCondition")]
    [InlineData("$(", "IllFormedPropertyCloseParenthesisInCondition")]
    [InlineData("$x", "IllFormedPropertyOpenParenthesisInCondition")]
    [InlineData("$(x )", "IllFormedPropertySpaceInCondition")]
    [InlineData("$( x)", "IllFormedPropertySpaceInCondition")]
    [InlineData("$([MSBuild]::DoSomething($(space ))", "IllFormedPropertySpaceInCondition")]
    [InlineData("$([MSBuild]::DoSomething($(_space ))", "IllFormedPropertySpaceInCondition")]
    public void ReportsMalformedProperty(string expression, string expectedResource)
    {
        Scanner scanner = ScanToError(expression, ParserOptions.AllowProperties);
        scanner.ErrorResource.ShouldBe(expectedResource);
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
        AssertNextToken(scanner, TokenKind.Property);
        AssertNextToken(scanner, TokenKind.EndOfInput);
    }

    [Theory]
    [InlineData("@", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@(", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@x", "IllFormedItemListOpenParenthesisInCondition")]
    [InlineData("@(x", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@(x->'%(y)", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x'", "IllFormedItemListCloseParenthesisInCondition")]
    public void ReportsMalformedItemList(string expression, string expectedResource)
    {
        Scanner scanner = ScanToError(expression);
        scanner.ErrorResource.ShouldBe(expectedResource);
        scanner.ErrorFormatArg.ShouldBeNull();
    }

    [Theory]
    [InlineData("false or 'abc")]
    [InlineData("'")]
    [InlineData("'$(DEBUG) == true")]
    public void ReportsUnterminatedQuotedString(string expression)
    {
        Scanner scanner = ScanToError(expression);
        scanner.ErrorResource.ShouldBe("IllFormedQuotedStringInCondition");
        scanner.ErrorFormatArg.ShouldBeNull();
    }

    [Theory]
    [InlineData("'@(foo")]
    [InlineData("'@(foo'")]
    public void ReportsQuotedStringErrorForMalformedEmbeddedItemList(string expression)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        scanner.Advance().ShouldBeFalse();
        scanner.ErrorResource.ShouldBe("IllFormedQuotedStringInCondition");
        scanner.ErrorPosition.ShouldBe(1);
        scanner.ErrorFormatArg.ShouldBeNull();
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
        AssertNextToken(scanner, TokenKind.Number);
        scanner.CurrentTokenText.ShouldBe(expression);
    }

    [Theory]
    [InlineData("$(foo)", (int)TokenKind.Property)]
    [InlineData("@(foo)", (int)TokenKind.ItemList)]
    [InlineData("(", (int)TokenKind.LeftParenthesis)]
    [InlineData(")", (int)TokenKind.RightParenthesis)]
    [InlineData(",", (int)TokenKind.Comma)]
    [InlineData("==", (int)TokenKind.EqualTo)]
    [InlineData("!=", (int)TokenKind.NotEqualTo)]
    [InlineData("<", (int)TokenKind.LessThan)]
    [InlineData(">", (int)TokenKind.GreaterThan)]
    [InlineData("<=", (int)TokenKind.LessThanOrEqualTo)]
    [InlineData(">=", (int)TokenKind.GreaterThanOrEqualTo)]
    [InlineData("!", (int)TokenKind.Not)]
    [InlineData("'String with an embedded \\' in it'", (int)TokenKind.String)]
    public void ScansFirstToken(string expression, int expectedType)
        => AssertTokens(expression, (TokenKind)expectedType);

    [Theory]
    [InlineData("abcde", (int)TokenKind.String, "abcde")]
    [InlineData("'abc-efg'", (int)TokenKind.String, "abc-efg")]
    [InlineData("and", (int)TokenKind.And, "and")]
    [InlineData("or", (int)TokenKind.Or, "or")]
    [InlineData("AnD", (int)TokenKind.And, "and")]
    [InlineData("Or", (int)TokenKind.Or, "or")]
    [InlineData("'String with a $(Property) inside'", (int)TokenKind.String, "String with a $(Property) inside")]
    [InlineData("@(list, ' ')", (int)TokenKind.ItemList, "@(list, ' ')")]
    [InlineData("@(files->'%(Filename)')", (int)TokenKind.ItemList, "@(files->'%(Filename)')")]
    public void ScansTokenValues(string expression, int expectedKind, string expectedValue)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        AssertNextToken(scanner, (TokenKind)expectedKind);
        scanner.CurrentTokenText.ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData("@(Foo, ' ')", (int)TokenKind.ItemList)]
    [InlineData("'@(Foo, ' ')'", (int)TokenKind.String)]
    [InlineData("'%40(( '", (int)TokenKind.String)]
    public void ScansStringEdgeCases(string expression, int expectedKind)
        => AssertTokens(expression, (TokenKind)expectedKind, TokenKind.EndOfInput);

    [Fact]
    public void ScansQuotedItemListInComparison()
        => AssertTokens(
            "'@(Complex_ItemType-123, ';')' == ''",
            TokenKind.String,
            TokenKind.EqualTo,
            TokenKind.String,
            TokenKind.EndOfInput);

    [Theory]
    [InlineData("Foo()", new int[0])]
    [InlineData("Foo( 1 )", new[] { (int)TokenKind.Number })]
    [InlineData("Foo( $(Property) )", new[] { (int)TokenKind.Property })]
    [InlineData("Foo( @(ItemList) )", new[] { (int)TokenKind.ItemList })]
    [InlineData("Foo( simplestring )", new[] { (int)TokenKind.String })]
    [InlineData("Foo( 'Not a Simple String' )", new[] { (int)TokenKind.String })]
    [InlineData(
        "Foo( 'Not a Simple String', 1234 )",
        new[] { (int)TokenKind.String, (int)TokenKind.Comma, (int)TokenKind.Number })]
    [InlineData(
        "Foo( $(Property), 'Not a Simple String', 1234 )",
        new[] { (int)TokenKind.Property, (int)TokenKind.Comma, (int)TokenKind.String, (int)TokenKind.Comma, (int)TokenKind.Number })]
    [InlineData(
        "Foo( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )",
        new[]
        {
            (int)TokenKind.ItemList, (int)TokenKind.Comma, (int)TokenKind.Property, (int)TokenKind.Comma,
            (int)TokenKind.String, (int)TokenKind.Comma, (int)TokenKind.String, (int)TokenKind.Comma, (int)TokenKind.Number,
        })]
    public void ScansFunctionTokens(string expression, int[] argumentKinds)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        AssertNextToken(scanner, TokenKind.FunctionName);
        scanner.CurrentTokenText.ShouldBe("Foo");
        AssertNextToken(scanner, TokenKind.LeftParenthesis);

        foreach (int argumentType in argumentKinds)
        {
            AssertNextToken(scanner, (TokenKind)argumentType);
        }

        AssertNextToken(scanner, TokenKind.RightParenthesis);
    }

    [Theory]
    [InlineData("'abc-efg'==$(foo)", (int)TokenKind.String, (int)TokenKind.EqualTo, (int)TokenKind.Property)]
    [InlineData("$(debug)!=true", (int)TokenKind.Property, (int)TokenKind.NotEqualTo, (int)TokenKind.String)]
    [InlineData("$(VERSION)<5", (int)TokenKind.Property, (int)TokenKind.LessThan, (int)TokenKind.Number)]
    public void ScansComparisonTokens(string expression, int leftType, int comparisonType, int rightType)
        => AssertTokens(expression, (TokenKind)leftType, (TokenKind)comparisonType, (TokenKind)rightType, TokenKind.EndOfInput);

    [Fact]
    public void ScansWhitespaceSeparatedTokens()
        => AssertTokens("$(DEBUG) and $(FOO)", TokenKind.Property, TokenKind.And, TokenKind.Property);

    [Theory]
    [InlineData("1234$(DEBUG)0xabcd@(foo)asdf<>'foo'<=false>=true==1234!=")]
    [InlineData("   1234    $(DEBUG)    0xabcd  \n@(foo)    \nasdf  \n<     \n>     \n'foo'  \n<=    \nfalse     \n>=    \ntrue  \n== \n 1234    \n!=     ")]
    public void ScansTokensWithAndWithoutWhitespace(string expression)
        => AssertTokens(
            expression,
            TokenKind.Number,
            TokenKind.Property,
            TokenKind.Number,
            TokenKind.ItemList,
            TokenKind.String,
            TokenKind.LessThan,
            TokenKind.GreaterThan,
            TokenKind.String,
            TokenKind.LessThanOrEqualTo,
            TokenKind.String,
            TokenKind.GreaterThanOrEqualTo,
            TokenKind.String,
            TokenKind.EqualTo,
            TokenKind.Number,
            TokenKind.NotEqualTo,
            TokenKind.EndOfInput);

    [Theory]
    [InlineData("@(foo)")]
    [InlineData("'1234 @(foo)'")]
    public void RejectsItemListsWhenDisallowed(string expression)
    {
        Scanner scanner = new(expression, ParserOptions.AllowProperties);
        scanner.Advance().ShouldBeFalse();
        scanner.ErrorResource.ShouldBe("ItemListNotAllowedInThisConditional");
    }

    [Fact]
    public void RejectsItemListAfterAnotherTokenWhenDisallowed()
    {
        Scanner scanner = new("1234 '@(foo)'", ParserOptions.AllowProperties);
        scanner.Advance().ShouldBeTrue();
        scanner.Advance().ShouldBeFalse();
        scanner.ErrorResource.ShouldBe("ItemListNotAllowedInThisConditional");
    }

    private static void AssertErrorPosition(string expression, ParserOptions options, int expectedPosition)
    {
        Scanner scanner = ScanToError(expression, options);
        scanner.ErrorPosition.ShouldBe(expectedPosition);
    }

    private static Scanner ScanToError(string expression, ParserOptions options = ParserOptions.AllowAll)
    {
        Scanner scanner = new(expression, options);
        while (scanner.Advance() && !scanner.IsKind(TokenKind.EndOfInput))
        {
        }

        scanner.HasError.ShouldBeTrue();
        return scanner;
    }

    private static void AssertTokens(string expression, params ReadOnlySpan<TokenKind> expectedKinds)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        foreach (TokenKind expectedKind in expectedKinds)
        {
            AssertNextToken(scanner, expectedKind);
        }
    }

    private static void AssertNextToken(Scanner scanner, TokenKind expectedKind)
    {
        scanner.Advance().ShouldBeTrue();
        scanner.IsKind(expectedKind).ShouldBeTrue();
    }
}
