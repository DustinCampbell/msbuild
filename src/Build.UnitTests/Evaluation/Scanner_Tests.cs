// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;
using static Microsoft.Build.Evaluation.Token;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Tests condition tokenization and scanner diagnostics.
/// </summary>
/// <param name="output">The output helper for the test.</param>
public sealed class ScannerTest(ITestOutputHelper output) : TestBase(output)
{
    /// <summary>
    ///  Verifies the diagnostic and unexpected character for a single equals sign.
    /// </summary>
    [Fact]
    public void SingleEquals()
    {
        Scanner scanner = new("a=b", ParserOptions.AllowProperties);
        ScanToEnd(scanner);

        scanner._errorState.ShouldBeTrue();
        scanner.GetErrorResource().ShouldBe("IllFormedEqualsInCondition");
        scanner.UnexpectedlyFound.ShouldBe("b");
    }

    /// <summary>
    ///  Verifies diagnostics for incomplete property references.
    /// </summary>
    /// <param name="expression">The malformed property reference.</param>
    /// <param name="expectedResource">The expected diagnostic resource name.</param>
    [Theory]
    [InlineData("$(", "IllFormedPropertyCloseParenthesisInCondition")]
    [InlineData("$x", "IllFormedPropertyOpenParenthesisInCondition")]
    public void IllFormedProperty(string expression, string expectedResource)
    {
        Scanner scanner = new(expression, ParserOptions.AllowProperties);
        ScanToEnd(scanner);

        scanner._errorState.ShouldBeTrue();
        scanner.GetErrorResource().ShouldBe(expectedResource);
    }

    /// <summary>
    ///  Verifies that whitespace next to a simple property name is rejected.
    /// </summary>
    /// <param name="expression">The property reference containing invalid whitespace.</param>
    [Theory]
    [InlineData("$(x )")]
    [InlineData("$( x)")]
    [InlineData("$([MSBuild]::DoSomething($(space ))")]
    [InlineData("$([MSBuild]::DoSomething($(_space ))")]
    public void SpaceProperty(string expression)
    {
        Scanner scanner = new(expression, ParserOptions.AllowProperties);
        ScanToEnd(scanner);

        scanner._errorState.ShouldBeTrue();
        scanner.GetErrorResource().ShouldBe("IllFormedPropertySpaceInCondition");
    }

    /// <summary>
    ///  Verifies that property-function references permit whitespace around function arguments and member access.
    /// </summary>
    /// <param name="expression">The property-function reference to scan.</param>
    [Theory]
    [InlineData("$(x.StartsWith( 'y' ))")]
    [InlineData("$(x.StartsWith ('y'))")]
    [InlineData("$( x.StartsWith( $(SpacelessProperty) ) )")]
    [InlineData("$( x.StartsWith( $(_SpacelessProperty) ) )")]
    [InlineData("$(x.StartsWith('Foo', StringComparison.InvariantCultureIgnoreCase))")]
    public void SpaceInMiddleOfProperty(string expression)
    {
        Scanner scanner = new(expression, ParserOptions.AllowProperties);
        ScanToEnd(scanner);

        scanner._errorState.ShouldBeFalse();
        scanner.IsNext(TokenType.EndOfInput).ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies diagnostics for incomplete item lists, transforms, and separators.
    /// </summary>
    /// <param name="expression">The malformed item expression.</param>
    /// <param name="expectedResource">The expected diagnostic resource name.</param>
    [Theory]
    [InlineData("@(", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@x", "IllFormedItemListOpenParenthesisInCondition")]
    [InlineData("@(x", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@(x->'%(y)", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x'", "IllFormedItemListCloseParenthesisInCondition")]
    public void IllFormedItemList(string expression, string expectedResource)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        ScanToEnd(scanner);

        scanner._errorState.ShouldBeTrue();
        scanner.GetErrorResource().ShouldBe(expectedResource);
        scanner.UnexpectedlyFound.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies diagnostics for unterminated single-quoted strings.
    /// </summary>
    /// <param name="expression">The condition containing an unterminated string.</param>
    [Theory]
    [InlineData("false or 'abc")]
    [InlineData("'")]
    [InlineData("'$(DEBUG) == true")]
    public void IllFormedQuotedString(string expression)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);
        ScanToEnd(scanner);

        scanner._errorState.ShouldBeTrue();
        scanner.GetErrorResource().ShouldBe("IllFormedQuotedStringInCondition");
        scanner.UnexpectedlyFound.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies disallowed item lists are rejected at the expected point, including inside quoted strings.
    /// </summary>
    /// <param name="expression">The condition containing a disallowed item list.</param>
    /// <param name="precedingTokens">The number of tokens that must scan successfully before the error.</param>
    [Theory]
    [InlineData("@(foo)", 0)]
    [InlineData("1234 '@(foo)'", 1)]
    [InlineData("'1234 @(foo)'", 0)]
    public void ItemListsNotAllowed(string expression, int precedingTokens)
    {
        Scanner scanner = new(expression, ParserOptions.AllowProperties);
        for (int index = 0; index < precedingTokens; index++)
        {
            scanner.Advance().ShouldBeTrue();
        }

        scanner.Advance().ShouldBeFalse();
        scanner._errorState.ShouldBeTrue();
        scanner.GetErrorResource().ShouldBe("ItemListNotAllowedInThisConditional");
    }

    /// <summary>
    ///  Verifies numeric token kinds and text without converting their values.
    /// </summary>
    /// <param name="expression">The numeric literal to scan.</param>
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
    public void NumericSingleTokenTests(string expression)
        => AssertSingleToken(expression, TokenType.Numeric, expression);

    public static readonly TheoryData<string, int, string> SingleTokens =
    [
        ("$(foo)", (int)TokenType.Property, "$(foo)"),
        ("@(foo)", (int)TokenType.ItemList, "@(foo)"),
        ("abcde", (int)TokenType.String, "abcde"),
        ("'abc-efg'", (int)TokenType.String, "abc-efg"),
        ("and", (int)TokenType.And, "and"),
        ("or", (int)TokenType.Or, "or"),
        ("AnD", (int)TokenType.And, "and"),
        ("Or", (int)TokenType.Or, "or"),
        ("(", (int)TokenType.LeftParenthesis, "("),
        (")", (int)TokenType.RightParenthesis, ")"),
        (",", (int)TokenType.Comma, ","),
        ("==", (int)TokenType.EqualTo, "=="),
        ("!=", (int)TokenType.NotEqualTo, "!="),
        ("<", (int)TokenType.LessThan, "<"),
        (">", (int)TokenType.GreaterThan, ">"),
        ("<=", (int)TokenType.LessThanOrEqualTo, "<="),
        (">=", (int)TokenType.GreaterThanOrEqualTo, ">="),
        ("!", (int)TokenType.Not, "!"),
        ("@(Foo, ' ')", (int)TokenType.ItemList, "@(Foo, ' ')"),
        ("'@(Foo, ' ')'", (int)TokenType.String, "@(Foo, ' ')"),
        ("'%40(( '", (int)TokenType.String, "%40(( "),
        ("'String with a $(Property) inside'", (int)TokenType.String, "String with a $(Property) inside"),
        ("@(list, ' ')", (int)TokenType.ItemList, "@(list, ' ')"),
        ("@(files->'%(Filename)')", (int)TokenType.ItemList, "@(files->'%(Filename)')"),
    ];

    /// <summary>
    ///  Verifies token kinds, text, and complete input consumption for individual tokens.
    /// </summary>
    /// <param name="expression">The expression containing one token.</param>
    /// <param name="tokenType">The expected token-kind value.</param>
    /// <param name="expectedText">The expected token text.</param>
    [Theory]
    [MemberData(nameof(SingleTokens))]
    public void SingleTokenTests(string expression, int tokenType, string expectedText)
        => AssertSingleToken(expression, (TokenType)tokenType, expectedText);

    /// <summary>
    ///  Verifies that a backslash does not escape a single-quote delimiter.
    /// </summary>
    [Fact]
    public void BackslashDoesNotEscapeSingleQuotes()
    {
        Scanner scanner = new("'String with an embedded \\' in it'", ParserOptions.AllowAll);

        scanner.Advance().ShouldBeTrue();
        scanner.IsNext(TokenType.String).ShouldBeTrue();
        scanner.IsNextString().ShouldBe("String with an embedded \\");
    }

    public static readonly TheoryData<string, int[]> FunctionCalls =
    [
        ("Foo()", [(int)TokenType.LeftParenthesis, (int)TokenType.RightParenthesis]),
        ("Foo( 1 )", [(int)TokenType.LeftParenthesis, (int)TokenType.Numeric, (int)TokenType.RightParenthesis]),
        ("Foo( $(Property) )", [(int)TokenType.LeftParenthesis, (int)TokenType.Property, (int)TokenType.RightParenthesis]),
        ("Foo( @(ItemList) )", [(int)TokenType.LeftParenthesis, (int)TokenType.ItemList, (int)TokenType.RightParenthesis]),
        ("Foo( simplestring )", [(int)TokenType.LeftParenthesis, (int)TokenType.String, (int)TokenType.RightParenthesis]),
        ("Foo( 'Not a Simple String' )", [(int)TokenType.LeftParenthesis, (int)TokenType.String, (int)TokenType.RightParenthesis]),
        (
            "Foo( 'Not a Simple String', 1234 )",
            [(int)TokenType.LeftParenthesis, (int)TokenType.String, (int)TokenType.Comma, (int)TokenType.Numeric, (int)TokenType.RightParenthesis]
        ),
        (
            "Foo( $(Property), 'Not a Simple String', 1234 )",
            [
                (int)TokenType.LeftParenthesis, (int)TokenType.Property, (int)TokenType.Comma, (int)TokenType.String,
                (int)TokenType.Comma, (int)TokenType.Numeric, (int)TokenType.RightParenthesis,
            ]
        ),
        (
            "Foo( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )",
            [
                (int)TokenType.LeftParenthesis, (int)TokenType.ItemList, (int)TokenType.Comma, (int)TokenType.Property,
                (int)TokenType.Comma, (int)TokenType.String, (int)TokenType.Comma, (int)TokenType.String,
                (int)TokenType.Comma, (int)TokenType.Numeric, (int)TokenType.RightParenthesis,
            ]
        ),
    ];

    /// <summary>
    ///  Verifies function-name text and ordered tokens for different argument forms.
    /// </summary>
    /// <param name="expression">The function call to scan.</param>
    /// <param name="argumentTokens">The expected token-kind values after the function name.</param>
    [Theory]
    [MemberData(nameof(FunctionCalls))]
    public void FunctionTokens(string expression, int[] argumentTokens)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);

        scanner.Advance().ShouldBeTrue();
        scanner.IsNext(TokenType.Function).ShouldBeTrue();
        scanner.IsNextString().ShouldBe("Foo");

        AssertTokenSequence(scanner, argumentTokens);
    }

    private static readonly int[] WhitespaceTokens =
    [
        (int)TokenType.Numeric, (int)TokenType.Property, (int)TokenType.Numeric, (int)TokenType.ItemList, (int)TokenType.String,
        (int)TokenType.LessThan, (int)TokenType.GreaterThan, (int)TokenType.String, (int)TokenType.LessThanOrEqualTo,
        (int)TokenType.String, (int)TokenType.GreaterThanOrEqualTo, (int)TokenType.String, (int)TokenType.EqualTo,
        (int)TokenType.Numeric, (int)TokenType.NotEqualTo,
    ];

    public static readonly TheoryData<string, int[]> TokenSequences =
    [
        (
            "'@(Complex_ItemType-123, ';')' == ''",
            [(int)TokenType.String, (int)TokenType.EqualTo, (int)TokenType.String]
        ),
        ("'abc-efg'==$(foo)", [(int)TokenType.String, (int)TokenType.EqualTo, (int)TokenType.Property]),
        ("$(debug)!=true", [(int)TokenType.Property, (int)TokenType.NotEqualTo, (int)TokenType.String]),
        ("$(VERSION)<5", [(int)TokenType.Property, (int)TokenType.LessThan, (int)TokenType.Numeric]),
        ("$(DEBUG) and $(FOO)", [(int)TokenType.Property, (int)TokenType.And, (int)TokenType.Property]),
        ("1234$(DEBUG)0xabcd@(foo)asdf<>'foo'<=false>=true==1234!=", WhitespaceTokens),
        (
            """
               1234    $(DEBUG)    0xabcd
            @(foo)
              asdf
            <
            >
            'foo'
            <=
            false
            >=
            true
            ==
             1234
            !=

            """,
            WhitespaceTokens
        ),
    ];

    /// <summary>
    ///  Verifies ordered tokens and complete input consumption for conditions and whitespace variants.
    /// </summary>
    /// <param name="expression">The condition to scan.</param>
    /// <param name="expectedTokens">The expected token-kind values.</param>
    [Theory]
    [MemberData(nameof(TokenSequences))]
    public void TokenSequenceTests(string expression, int[] expectedTokens)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);

        AssertTokenSequence(scanner, expectedTokens);
    }

    private static void ScanToEnd(Scanner scanner)
    {
        while (scanner.Advance() && !scanner.IsNext(TokenType.EndOfInput))
        {
        }
    }

    private static void AssertSingleToken(string expression, TokenType tokenType, string expectedText)
    {
        Scanner scanner = new(expression, ParserOptions.AllowAll);

        scanner.Advance().ShouldBeTrue();
        scanner.IsNext(tokenType).ShouldBeTrue();
        scanner.IsNextString().ShouldBe(expectedText);
        scanner.Advance().ShouldBeTrue();
        scanner.IsNext(TokenType.EndOfInput).ShouldBeTrue();
    }

    private static void AssertTokenSequence(Scanner scanner, int[] expectedTokens)
    {
        foreach (int token in expectedTokens)
        {
            TokenType tokenType = (TokenType)token;
            scanner.Advance().ShouldBeTrue();
            scanner.IsNext(tokenType).ShouldBeTrue($"Expected a {tokenType} token.");
        }

        scanner.Advance().ShouldBeTrue();
        scanner.IsNext(TokenType.EndOfInput).ShouldBeTrue();
    }
}
