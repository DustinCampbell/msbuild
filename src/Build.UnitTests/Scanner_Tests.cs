// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ScannerTest
{
    /// <summary>
    ///  Tests that we give a useful error position (not 0 for example).
    /// </summary>
    [Theory]
    [InlineData("1==0xFG", 7)] // Position of G
    [InlineData("1==-0xF", 6)] // Position of x
    [InlineData("1234=5678", 6)] // Position of '5'
    [InlineData(" ", 2)] // Position of End of Input
    [InlineData(" (", 3)] // Position of End of Input
    [InlineData(" false or  ", 12)] // Position of End of Input
    [InlineData(" \"foo", 2)] // Position of open quote
    [InlineData(" @(foo", 2)] // Position of @
    [InlineData(" @(", 2)] // Position of @
    [InlineData(" $", 2)] // Position of $
    [InlineData(" $(foo", 2)] // Position of $
    [InlineData(" $(", 2)] // Position of $
    [InlineData(" @(foo)", 2, ParserOptions.AllowProperties)] // Position of @
    [InlineData(" '@(foo)'", 3, ParserOptions.AllowProperties)] // Position of @
    [InlineData("'%24%28x' == '%24(x''", 21)] // Position of extra quote
    internal void ErrorPosition(string expression, int expectedPosition, ParserOptions options = ParserOptions.AllowAll)
    {
        // Some errors are caught by the Parser, not merely by the Scanner. So we have to do a full Parse,
        // rather than just calling AdvanceToScannerError(). (The error location is still supplied by the Scanner.)
        var parser = new Parser();

        var ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            parser.Parse(expression, options, MockElementLocation.Instance);
        });

        Console.WriteLine(ex.Message);
        Assert.Equal(expectedPosition, parser.errorPosition);
    }

    /// <summary>
    ///  Tests the special error for "=".
    /// </summary>
    [Fact]
    public void SingleEquals()
    {
        Scanner scanner = CreateScannerAndAdvanceToEnd("a=b", ParserOptions.AllowProperties);
        Assert.Equal(ConditionErrors.IllFormedEquals, scanner.GetErrorResource());
        Assert.Equal("b", scanner.UnexpectedlyFound);
    }

    /// <summary>
    ///  Tests the special errors for "$(" and "$x" and similar cases.
    /// </summary>
    [Fact]
    public void IllFormedProperty()
    {
        Scanner scanner = CreateScannerAndAdvanceToEnd("$(", ParserOptions.AllowProperties);
        Assert.Equal(ConditionErrors.IllFormedPropertyCloseParenthesis, scanner.GetErrorResource());

        scanner = CreateScannerAndAdvanceToEnd("$x", ParserOptions.AllowProperties);
        Assert.Equal(ConditionErrors.IllFormedPropertyOpenParenthesis, scanner.GetErrorResource());
    }

    /// <summary>
    ///  Tests the special errors for "%(" and "%x" and similar cases.
    /// </summary>
    [Fact]
    public void IllFormedItemMetadata()
    {
        Scanner scanner = CreateScannerAndAdvanceToEnd("%(", ParserOptions.AllowProperties | ParserOptions.AllowItemMetadata);
        Assert.Equal(ConditionErrors.IllFormedItemMetadataCloseParenthesis, scanner.GetErrorResource());

        scanner = CreateScannerAndAdvanceToEnd("%x", ParserOptions.AllowProperties | ParserOptions.AllowItemMetadata);
        Assert.Equal(ConditionErrors.IllFormedItemMetadataOpenParenthesis, scanner.GetErrorResource());
    }

    /// <summary>
    ///  Tests the space errors case.
    /// </summary>
    [Theory]
    [InlineData("$(x )")]
    [InlineData("$( x)")]
    [InlineData("$([MSBuild]::DoSomething($(space ))")]
    [InlineData("$([MSBuild]::DoSomething($(_space ))")]
    public void SpaceProperty(string pattern)
    {
        Scanner scanner = CreateScannerAndAdvanceToEnd(pattern, ParserOptions.AllowProperties);
        Assert.Equal(ConditionErrors.IllFormedPropertySpace, scanner.GetErrorResource());
    }

    /// <summary>
    ///  Tests the space not next to end so no errors case.
    /// </summary>
    [Theory]
    [InlineData("$(x.StartsWith( 'y' ))")]
    [InlineData("$(x.StartsWith ('y'))")]
    [InlineData("$( x.StartsWith( $(SpacelessProperty) ) )")]
    [InlineData("$( x.StartsWith( $(_SpacelessProperty) ) )")]
    [InlineData("$(x.StartsWith('Foo', StringComparison.InvariantCultureIgnoreCase))")]
    public void SpaceInMiddleOfProperty(string pattern)
    {
        Scanner scanner = CreateScannerAndAdvanceToEnd(pattern, ParserOptions.AllowProperties);
        Assert.False(scanner._errorState);
    }

    /// <summary>
    ///  Tests the special errors for "@(" and "@x" and similar cases.
    /// </summary>
    [Fact]
    public void IllFormedItemList()
    {
        Scanner scanner = CreateScannerAndAdvanceToEnd("@(", ParserOptions.AllowAll);
        Assert.Equal(ConditionErrors.IllFormedItemListCloseParenthesis, scanner.GetErrorResource());
        Assert.Null(scanner.UnexpectedlyFound);

        scanner = CreateScannerAndAdvanceToEnd("@x", ParserOptions.AllowAll);
        Assert.Equal(ConditionErrors.IllFormedItemListOpenParenthesis, scanner.GetErrorResource());
        Assert.Null(scanner.UnexpectedlyFound);

        scanner = CreateScannerAndAdvanceToEnd("@(x", ParserOptions.AllowAll);
        Assert.Equal(ConditionErrors.IllFormedItemListCloseParenthesis, scanner.GetErrorResource());
        Assert.Null(scanner.UnexpectedlyFound);

        scanner = CreateScannerAndAdvanceToEnd("@(x->'%(y)", ParserOptions.AllowAll);
        Assert.Equal(ConditionErrors.IllFormedItemListQuote, scanner.GetErrorResource());
        Assert.Null(scanner.UnexpectedlyFound);

        scanner = CreateScannerAndAdvanceToEnd("@(x->'%(y)', 'x", ParserOptions.AllowAll);
        Assert.Equal(ConditionErrors.IllFormedItemListQuote, scanner.GetErrorResource());
        Assert.Null(scanner.UnexpectedlyFound);

        scanner = CreateScannerAndAdvanceToEnd("@(x->'%(y)', 'x'", ParserOptions.AllowAll);
        Assert.Equal(ConditionErrors.IllFormedItemListCloseParenthesis, scanner.GetErrorResource());
        Assert.Null(scanner.UnexpectedlyFound);
    }

    /// <summary>
    ///  Tests the special error for unterminated quotes.
    ///  Note, scanner only understands single quotes.
    /// </summary>
    [Fact]
    public void IllFormedQuotedString()
    {
        Scanner scanner = CreateScannerAndAdvanceToEnd("false or 'abc");
        Assert.Equal(ConditionErrors.IllFormedQuotedString, scanner.GetErrorResource());
        Assert.Null(scanner.UnexpectedlyFound);

        scanner = CreateScannerAndAdvanceToEnd("\'");
        Assert.Equal(ConditionErrors.IllFormedQuotedString, scanner.GetErrorResource());
        Assert.Null(scanner.UnexpectedlyFound);
    }

    [Fact]
    public void NumericSingleTokenTests()
    {
        VerifyTokens("1234", Token(TokenKind.Numeric, "1234"));
        VerifyTokens("-1234", Token(TokenKind.Numeric, "-1234"));
        VerifyTokens("+1234", Token(TokenKind.Numeric, "+1234"));
        VerifyTokens("1234.1234", Token(TokenKind.Numeric, "1234.1234"));
        VerifyTokens(".1234", Token(TokenKind.Numeric, ".1234"));
        VerifyTokens("1234.", Token(TokenKind.Numeric, "1234."));
        VerifyTokens("0x1234", Token(TokenKind.Numeric, "0x1234"));
        VerifyTokens("0x1234ABCD", Token(TokenKind.Numeric, "0x1234ABCD"));
    }

    /// <summary>
    ///  The scanner incorrectly produces incorrect tokens for bad text in some cases.
    /// </summary>
    [Theory]
    [InlineData(".", TokenKind.Numeric, ".")]
    [InlineData("...", TokenKind.Numeric, "...")]
    [InlineData("0.0.0", TokenKind.Numeric, "0.0.0")]
    [InlineData("0x", TokenKind.Numeric, "0x")]
    internal void BadCases(string expression, TokenKind expectedKind, string expectedText)
    {
        Scanner scanner = CreateScannerAndVerifyTokens(expression, Token(expectedKind, expectedText));
        Assert.False(scanner._errorState);
    }

    [Fact]
    public void PropsStringsAndBooleanSingleTokenTests()
    {
        VerifyTokens("$(foo)", Token(TokenKind.Property));
        VerifyTokens("@(foo)", Token(TokenKind.ItemList));
        VerifyTokens("abcde", Token(TokenKind.String, "abcde"));
        VerifyTokens("'abc-efg'", Token(TokenKind.String, "abc-efg"));
        VerifyTokens("and", Token(TokenKind.And, "and"));
        VerifyTokens("or", Token(TokenKind.Or, "or"));
        VerifyTokens("And", Token(TokenKind.And, "and"));
        VerifyTokens("Or", Token(TokenKind.Or, "or"));
    }

    [Fact]
    public void SimpleSingleTokenTests()
    {
        VerifyTokens("(", Token(TokenKind.LeftParenthesis));
        VerifyTokens(")", Token(TokenKind.RightParenthesis));
        VerifyTokens(",", Token(TokenKind.Comma));
        VerifyTokens("==", Token(TokenKind.EqualTo));
        VerifyTokens("!=", Token(TokenKind.NotEqualTo));
        VerifyTokens("<", Token(TokenKind.LessThan));
        VerifyTokens(">", Token(TokenKind.GreaterThan));
        VerifyTokens("<=", Token(TokenKind.LessThanOrEqualTo));
        VerifyTokens(">=", Token(TokenKind.GreaterThanOrEqualTo));
        VerifyTokens("!", Token(TokenKind.Not));
    }

    [Fact]
    public void StringEdgeTests()
    {
        VerifyTokens(
            "@(Foo, ' ')",
            Token(TokenKind.ItemList));

        VerifyTokens(
            "'@(Foo, ' ')'",
            Token(TokenKind.String));

        VerifyTokens(
            "'%40(( '",
            Token(TokenKind.String));

        VerifyTokens(
            "'@(Complex_ItemType-123, ';')' == ''",
            Token(TokenKind.String),
            Token(TokenKind.EqualTo),
            Token(TokenKind.String));
    }

    [Fact]
    public void FunctionTests()
    {
        VerifyTokens(
            "Foo()",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.RightParenthesis));

        VerifyTokens(
            "Foo( 1 )",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.Numeric),
            Token(TokenKind.RightParenthesis));

        VerifyTokens(
            "Foo( $(Property) )",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.Property),
            Token(TokenKind.RightParenthesis));

        VerifyTokens(
            "Foo( @(ItemList) )",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.ItemList),
            Token(TokenKind.RightParenthesis));

        VerifyTokens(
            "Foo( simplestring )",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.String),
            Token(TokenKind.RightParenthesis));

        VerifyTokens(
            "Foo( 'Not a Simple String' )",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.String),
            Token(TokenKind.RightParenthesis));

        VerifyTokens(
            "Foo( 'Not a Simple String', 1234 )",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.String),
            Token(TokenKind.Comma),
            Token(TokenKind.Numeric),
            Token(TokenKind.RightParenthesis));

        VerifyTokens(
            "Foo( $(Property), 'Not a Simple String', 1234 )",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.Property),
            Token(TokenKind.Comma),
            Token(TokenKind.String),
            Token(TokenKind.Comma),
            Token(TokenKind.Numeric),
            Token(TokenKind.RightParenthesis));

        VerifyTokens(
            "Foo( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )",
            Token(TokenKind.Function, "Foo"),
            Token(TokenKind.LeftParenthesis),
            Token(TokenKind.ItemList),
            Token(TokenKind.Comma),
            Token(TokenKind.Property),
            Token(TokenKind.Comma),
            Token(TokenKind.String),
            Token(TokenKind.Comma),
            Token(TokenKind.String),
            Token(TokenKind.Comma),
            Token(TokenKind.Numeric),
            Token(TokenKind.RightParenthesis));
    }

    [Fact]
    public void ComplexTests1()
    {
        VerifyTokens(
            "'String with a $(Property) inside'",
            Token(TokenKind.String, "String with a $(Property) inside"));

        VerifyTokens(
            "@(list, ' ')",
            Token(TokenKind.ItemList, "@(list, ' ')"));
        VerifyTokens(
            "@(files->'%(Filename)')",
            Token(TokenKind.ItemList, "@(files->'%(Filename)')"));
    }

    [Fact]
    public void ComplexTests2()
    {
        VerifyTokens(
            "1234",
            Token(TokenKind.Numeric));

        VerifyTokens(
            "'abc-efg'==$(foo)",
            Token(TokenKind.String),
            Token(TokenKind.EqualTo),
            Token(TokenKind.Property));

        VerifyTokens(
            "$(debug)!=true",
            Token(TokenKind.Property),
            Token(TokenKind.NotEqualTo),
            Token(TokenKind.String));

        VerifyTokens(
            "$(VERSION)<5",
            Token(TokenKind.Property),
            Token(TokenKind.LessThan),
            Token(TokenKind.Numeric));
    }

    /// <summary>
    ///  Tests all tokens with no whitespace and whitespace.
    /// </summary>
    [Fact]
    public void WhitespaceTests()
    {
        VerifyTokens(
            "$(DEBUG) and $(FOO)",
            Token(TokenKind.Property),
            Token(TokenKind.And),
            Token(TokenKind.Property));

        // No whitespace
        VerifyTokens(
            "1234$(DEBUG)0xabcd@(foo)asdf<>'foo'<=false>=true==1234!=",
            Token(TokenKind.Numeric),
            Token(TokenKind.Property),
            Token(TokenKind.Numeric),
            Token(TokenKind.ItemList),
            Token(TokenKind.String),
            Token(TokenKind.LessThan),
            Token(TokenKind.GreaterThan),
            Token(TokenKind.String),
            Token(TokenKind.LessThanOrEqualTo),
            Token(TokenKind.String),
            Token(TokenKind.GreaterThanOrEqualTo),
            Token(TokenKind.String),
            Token(TokenKind.EqualTo),
            Token(TokenKind.Numeric),
            Token(TokenKind.NotEqualTo));

        // With whitespace
        VerifyTokens(
            "   1234    $(DEBUG)    0xabcd  \n@(foo)    \nasdf  \n<     \n>     \n'foo'  \n<=    \nfalse     \n>=    \ntrue  \n== \n 1234    \n!=     ",
            Token(TokenKind.Numeric),
            Token(TokenKind.Property),
            Token(TokenKind.Numeric),
            Token(TokenKind.ItemList),
            Token(TokenKind.String),
            Token(TokenKind.LessThan),
            Token(TokenKind.GreaterThan),
            Token(TokenKind.String),
            Token(TokenKind.LessThanOrEqualTo),
            Token(TokenKind.String),
            Token(TokenKind.GreaterThanOrEqualTo),
            Token(TokenKind.String),
            Token(TokenKind.EqualTo),
            Token(TokenKind.Numeric),
            Token(TokenKind.NotEqualTo));
    }

    /// <summary>
    ///  Tests the parsing of item lists.
    /// </summary>
    [Fact]
    public void ItemListTests()
    {
        Scanner scanner = CreateScannerAndAdvanceToEnd("@(foo)", ParserOptions.AllowProperties);
        Assert.Equal(ConditionErrors.ItemListNotAllowed, scanner.GetErrorResource());

        scanner = CreateScannerAndAdvanceToEnd("1234 '@(foo)'", ParserOptions.AllowProperties);
        Assert.Equal(ConditionErrors.ItemListNotAllowed, scanner.GetErrorResource());

        scanner = CreateScannerAndAdvanceToEnd("'1234 @(foo)'", ParserOptions.AllowProperties);
        Assert.Equal(ConditionErrors.ItemListNotAllowed, scanner.GetErrorResource());
    }

    /// <summary>
    ///  Tests that shouldn't work.
    /// </summary>
    [Fact]
    public void NegativeTests()
    {
        var scanner = new Scanner("'$(DEBUG) == true", ParserOptions.AllowAll);
        Assert.False(scanner.Advance());
    }

    /// <summary>
    ///  Advance to the point of the scanner error. If the error is only caught by the parser, this isn't useful.
    /// </summary>
    private Scanner CreateScannerAndAdvanceToEnd(string expression, ParserOptions options = ParserOptions.AllowAll)
    {
        var scanner = new Scanner(expression, options);
        AdvanceToEnd(scanner);

        return scanner;
    }

    /// <summary>
    ///  Advance to the point of the scanner error. If the error is only caught by the parser, this isn't useful.
    /// </summary>
    private void AdvanceToEnd(Scanner scanner)
    {
        while (scanner.Advance() && !scanner.IsNext(TokenKind.EndOfInput))
        {
        }
    }

    private static Scanner CreateScannerAndVerifyTokens(string expression, params ReadOnlySpan<Action<Token>> verifiers)
        => CreateScannerAndVerifyTokens(expression, ParserOptions.AllowAll, verifiers);

    private static Scanner CreateScannerAndVerifyTokens(string expression, ParserOptions options, params ReadOnlySpan<Action<Token>> verifiers)
    {
        var scanner = new Scanner(expression, options);
        VerifyTokens(scanner, verifiers);

        return scanner;
    }

    private static void VerifyTokens(string expression, params ReadOnlySpan<Action<Token>> verifiers)
        => VerifyTokens(expression, ParserOptions.AllowAll, verifiers);

    private static void VerifyTokens(string expression, ParserOptions options, params ReadOnlySpan<Action<Token>> verifiers)
    {
        var scanner = new Scanner(expression, options);
        VerifyTokens(scanner, verifiers);
    }

    private static void VerifyTokens(Scanner scanner, params ReadOnlySpan<Action<Token>> verifiers)
    {
        foreach (Action<Token> verifier in verifiers)
        {
            Assert.True(scanner.Advance());
            verifier(scanner.CurrentToken);
        }

        Assert.True(scanner.Advance());
        Assert.True(scanner.IsNext(TokenKind.EndOfInput));
    }

    private static Action<Token> Token(TokenKind kind)
        => token => Assert.Equal(kind, token.Kind);

    private static Action<Token> Token(TokenKind kind, string text)
        => token =>
        {
            Assert.Equal(kind, token.Kind);
            Assert.Equal(text, token.Text);
        };
}
