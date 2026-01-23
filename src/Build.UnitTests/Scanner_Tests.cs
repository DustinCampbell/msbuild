// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Tests for the Scanner class which tokenizes MSBuild conditional expressions.
/// </summary>
public class ScannerTest
{
    /// <summary>
    ///  Verifies that simple single-character operators are tokenized correctly.
    /// </summary>
    [Theory]
    [InlineData("(", TokenKind.LeftParenthesis)]
    [InlineData(")", TokenKind.RightParenthesis)]
    [InlineData(",", TokenKind.Comma)]
    [InlineData("<", TokenKind.LessThan)]
    [InlineData(">", TokenKind.GreaterThan)]
    [InlineData("!", TokenKind.Not)]
    internal void SimpleOperators_ShouldTokenizeCorrectly(string input, TokenKind expectedKind)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(expectedKind);
    }

    /// <summary>
    ///  Verifies that compound two-character operators are tokenized correctly.
    /// </summary>
    [Theory]
    [InlineData("==", TokenKind.EqualTo)]
    [InlineData("!=", TokenKind.NotEqualTo)]
    [InlineData("<=", TokenKind.LessThanOrEqualTo)]
    [InlineData(">=", TokenKind.GreaterThanOrEqualTo)]
    internal void CompoundOperators_ShouldTokenizeCorrectly(string input, TokenKind expectedKind)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(expectedKind);
    }

    /// <summary>
    ///  Verifies that various numeric literal formats are tokenized correctly,
    ///  including decimal, hexadecimal, positive, negative, and floating-point numbers.
    /// </summary>
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
    public void NumericLiterals_ShouldTokenizeCorrectly(string input)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(Numeric(input));
    }

    /// <summary>
    ///  Verifies that boolean and logical keywords (and, or, true, false, on, off, yes, no)
    ///  are recognized case-insensitively and normalized to lowercase.
    /// </summary>
    [Theory]
    [InlineData("and", TokenKind.And, "and")]
    [InlineData("AND", TokenKind.And, "and")]
    [InlineData("AnD", TokenKind.And, "and")]
    [InlineData("or", TokenKind.Or, "or")]
    [InlineData("OR", TokenKind.Or, "or")]
    [InlineData("Or", TokenKind.Or, "or")]
    [InlineData("true", TokenKind.True, "true")]
    [InlineData("TRUE", TokenKind.True, "true")]
    [InlineData("True", TokenKind.True, "true")]
    [InlineData("false", TokenKind.False, "false")]
    [InlineData("FALSE", TokenKind.False, "false")]
    [InlineData("False", TokenKind.False, "false")]
    [InlineData("on", TokenKind.On, "on")]
    [InlineData("ON", TokenKind.On, "on")]
    [InlineData("On", TokenKind.On, "on")]
    [InlineData("off", TokenKind.Off, "off")]
    [InlineData("OFF", TokenKind.Off, "off")]
    [InlineData("Off", TokenKind.Off, "off")]
    [InlineData("yes", TokenKind.Yes, "yes")]
    [InlineData("YES", TokenKind.Yes, "yes")]
    [InlineData("Yes", TokenKind.Yes, "yes")]
    [InlineData("no", TokenKind.No, "no")]
    [InlineData("NO", TokenKind.No, "no")]
    [InlineData("No", TokenKind.No, "no")]
    internal void Keywords_ShouldBeRecognized_CaseInsensitively(string input, TokenKind expectedKind, string expectedText)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll((expectedKind, expectedText));
    }

    /// <summary>
    ///  Verifies that simple unquoted strings and quoted strings are tokenized correctly,
    ///  with quoted strings having their quotes removed.
    /// </summary>
    [Theory]
    [InlineData("abcde", "abcde")]
    [InlineData("'abc-efg'", "abc-efg")]
    public void StringLiterals_ShouldTokenizeCorrectly(string input, string expectedText)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(String(expectedText));
    }

    /// <summary>
    ///  Verifies that property references $(name) and item list references @(name) are tokenized correctly.
    /// </summary>
    [Theory]
    [InlineData("$(foo)", TokenKind.Property)]
    [InlineData("@(foo)", TokenKind.ItemList)]
    internal void PropertyAndItemReferences_ShouldTokenizeCorrectly(string input, TokenKind expectedKind)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(expectedKind);
    }

    /// <summary>
    ///  Verifies that item lists with separators and strings containing special characters
    ///  or escaped sequences are handled correctly.
    /// </summary>
    [Theory]
    [InlineData("@(Foo, ' ')", TokenKind.ItemList)]
    [InlineData("'@(Foo, ' ')'", TokenKind.String)]
    [InlineData("'%40(( '", TokenKind.String)]
    internal void String_WithSpecialCharacters_ShouldTokenizeCorrectly(string input, TokenKind expectedKind)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(expectedKind);
    }

    /// <summary>
    ///  Verifies that quoted strings containing property or item references are tokenized
    ///  as single string tokens with the embedded references preserved.
    /// </summary>
    [Theory]
    [InlineData("'String with a $(Property) inside'", "String with a $(Property) inside")]
    [InlineData("'Text with @(Item) reference'", "Text with @(Item) reference")]
    public void QuotedString_WithEmbeddedReferences_ShouldTokenizeAsString(string input, string expectedText)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(String(expectedText));
    }

    /// <summary>
    ///  Verifies that item lists with transforms, separators, and metadata references are tokenized correctly.
    /// </summary>
    [Theory]
    [InlineData("@(list, ' ')")]
    [InlineData("@(files->'%(Filename)')")]
    [InlineData("@(items->Trim())")]
    [InlineData("@(sources, ';')")]
    public void ItemList_WithTransformOrSeparator_ShouldTokenizeCorrectly(string input)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(ItemList(input));
    }

    /// <summary>
    ///  Verifies that function calls with no arguments are tokenized as function name, left parenthesis, right parenthesis.
    /// </summary>
    [Fact]
    public void Function_WithNoArguments_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("Foo()");

        scanner.VerifyAll(
            Function("Foo"),
            TokenKind.LeftParenthesis,
            TokenKind.RightParenthesis);
    }

    /// <summary>
    ///  Verifies that function calls with single arguments of various types are tokenized correctly.
    /// </summary>
    [Theory]
    [InlineData("Foo( 1 )", TokenKind.Numeric)]
    [InlineData("Foo( $(Property) )", TokenKind.Property)]
    [InlineData("Foo( @(ItemList) )", TokenKind.ItemList)]
    [InlineData("Foo( simplestring )", TokenKind.String)]
    [InlineData("Foo( 'Not a Simple String' )", TokenKind.String)]
    internal void Function_WithSingleArgument_ShouldTokenizeCorrectly(string input, TokenKind argumentKind)
    {
        var scanner = new TestScanner(input);

        scanner.VerifyAll(
            Function("Foo"),
            TokenKind.LeftParenthesis,
            argumentKind,
            TokenKind.RightParenthesis);
    }

    /// <summary>
    ///  Verifies that function calls with two arguments separated by a comma are tokenized correctly.
    /// </summary>
    [Fact]
    public void Function_WithTwoArguments_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("Foo( 'Not a Simple String', 1234 )");

        scanner.VerifyAll(
            Function("Foo"),
            TokenKind.LeftParenthesis,
            TokenKind.String,
            TokenKind.Comma,
            TokenKind.Numeric,
            TokenKind.RightParenthesis);
    }

    /// <summary>
    ///  Verifies that function calls with three comma-separated arguments are tokenized correctly.
    /// </summary>
    [Fact]
    public void Function_WithThreeArguments_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("Foo( $(Property), 'Not a Simple String', 1234 )");

        scanner.VerifyAll(
            Function("Foo"),
            TokenKind.LeftParenthesis,
            TokenKind.Property,
            TokenKind.Comma,
            TokenKind.String,
            TokenKind.Comma,
            TokenKind.Numeric,
            TokenKind.RightParenthesis);
    }

    /// <summary>
    ///  Verifies that function calls with five arguments of mixed types (item list, property, strings, numeric)
    ///  are tokenized correctly.
    /// </summary>
    [Fact]
    public void Function_WithMixedArguments_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("Foo( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )");

        scanner.VerifyAll(
            Function("Foo"),
            TokenKind.LeftParenthesis,
            TokenKind.ItemList,
            TokenKind.Comma,
            TokenKind.Property,
            TokenKind.Comma,
            TokenKind.String,
            TokenKind.Comma,
            TokenKind.String,
            TokenKind.Comma,
            TokenKind.Numeric,
            TokenKind.RightParenthesis);
    }

    /// <summary>
    ///  Verifies that equality comparison expressions (string == property) are tokenized correctly.
    /// </summary>
    [Fact]
    public void Expression_EqualityComparison_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("'abc-efg'==$(foo)");

        scanner.VerifyAll(TokenKind.String, TokenKind.EqualTo, TokenKind.Property);
    }

    /// <summary>
    ///  Verifies that inequality comparison expressions (property != keyword) are tokenized correctly.
    /// </summary>
    [Fact]
    public void Expression_Inequality_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("$(debug)!=true");

        scanner.VerifyAll(TokenKind.Property, TokenKind.NotEqualTo, TokenKind.True);
    }

    /// <summary>
    ///  Verifies that relational comparison expressions (property &lt; number) are tokenized correctly.
    /// </summary>
    [Fact]
    public void Expression_RelationalComparison_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("$(VERSION)<5");

        scanner.VerifyAll(TokenKind.Property, TokenKind.LessThan, TokenKind.Numeric);
    }

    /// <summary>
    ///  Verifies that logical AND expressions (property and property) are tokenized correctly.
    /// </summary>
    [Fact]
    public void Expression_LogicalAnd_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("$(DEBUG) and $(FOO)");

        scanner.VerifyAll(TokenKind.Property, TokenKind.And, TokenKind.Property);
    }

    /// <summary>
    ///  Verifies that complex expressions with item lists embedded in quoted strings are tokenized correctly.
    /// </summary>
    [Fact]
    public void Expression_WithItemListInString_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("'@(Complex_ItemType-123, ';')' == ''");

        scanner.VerifyAll(
            TokenKind.String,
            TokenKind.EqualTo,
            TokenKind.String);
    }

    /// <summary>
    ///  Verifies that a complex sequence of tokens without any whitespace is tokenized correctly.
    /// </summary>
    [Fact]
    public void Tokens_WithoutWhitespace_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("1234$(DEBUG)0xabcd@(foo)asdf<>'foo'<=false>=true==1234!=");

        scanner.VerifyAll(
            TokenKind.Numeric,
            TokenKind.Property,
            TokenKind.Numeric,
            TokenKind.ItemList,
            TokenKind.String,
            TokenKind.LessThan,
            TokenKind.GreaterThan,
            TokenKind.String,
            TokenKind.LessThanOrEqualTo,
            TokenKind.False,
            TokenKind.GreaterThanOrEqualTo,
            TokenKind.True,
            TokenKind.EqualTo,
            TokenKind.Numeric,
            TokenKind.NotEqualTo);
    }

    /// <summary>
    ///  Verifies that a complex sequence of tokens with various whitespace and newlines is tokenized correctly.
    /// </summary>
    [Fact]
    public void Tokens_WithWhitespace_ShouldTokenizeCorrectly()
    {
        var scanner = new TestScanner("   1234    $(DEBUG)    0xabcd  \n@(foo)    \nasdf  \n<     \n>     \n'foo'  \n<=    \nfalse     \n>=    \ntrue  \n== \n 1234    \n!=     ");

        scanner.VerifyAll(
            TokenKind.Numeric,
            TokenKind.Property,
            TokenKind.Numeric,
            TokenKind.ItemList,
            TokenKind.String,
            TokenKind.LessThan,
            TokenKind.GreaterThan,
            TokenKind.String,
            TokenKind.LessThanOrEqualTo,
            TokenKind.False,
            TokenKind.GreaterThanOrEqualTo,
            TokenKind.True,
            TokenKind.EqualTo,
            TokenKind.Numeric,
            TokenKind.NotEqualTo);
    }

    /// <summary>
    ///  Verifies that property function calls with spaces in the middle (not at boundaries)
    ///  are tokenized correctly without producing errors.
    /// </summary>
    [Theory]
    [InlineData("$(x.StartsWith( 'y' ))")]
    [InlineData("$(x.StartsWith ('y'))")]
    [InlineData("$( x.StartsWith( $(SpacelessProperty) ) )")]
    [InlineData("$( x.StartsWith( $(_SpacelessProperty) ) )")]
    [InlineData("$(x.StartsWith('Foo', StringComparison.InvariantCultureIgnoreCase))")]
    public void Property_WithMiddleSpace_ShouldTokenizeCorrectly(string input)
    {
        var scanner = new TestScanner(input, ParserOptions.AllowProperties);

        scanner.VerifyAll(TokenKind.Property);
    }

    /// <summary>
    ///  Verifies that error positions are reported correctly for various malformed expressions,
    ///  ensuring the parser points to the exact character position where the error occurs.
    /// </summary>
    [Theory]
    [InlineData("1==0xFG", 7, ParserOptions.AllowAll)]                // Position of G
    [InlineData("1==-0xF", 6, ParserOptions.AllowAll)]                // Position of x
    [InlineData("1234=5678", 6, ParserOptions.AllowAll)]              // Position of '5'
    [InlineData(" ", 2, ParserOptions.AllowAll)]                      // Position of End of Input
    [InlineData(" (", 3, ParserOptions.AllowAll)]                     // Position of End of Input
    [InlineData(" false or  ", 12, ParserOptions.AllowAll)]           // Position of End of Input
    [InlineData(" \"foo", 2, ParserOptions.AllowAll)]                 // Position of open quote
    [InlineData(" @(foo", 2, ParserOptions.AllowAll)]                 // Position of @
    [InlineData(" @(", 2, ParserOptions.AllowAll)]                    // Position of @
    [InlineData(" $", 2, ParserOptions.AllowAll)]                     // Position of $
    [InlineData(" $(foo", 2, ParserOptions.AllowAll)]                 // Position of $
    [InlineData(" $(", 2, ParserOptions.AllowAll)]                    // Position of $
    [InlineData(" @(foo)", 2, ParserOptions.AllowProperties)]         // Position of @
    [InlineData(" '@(foo)'", 3, ParserOptions.AllowProperties)]       // Position of @
    [InlineData("'%24%28x' == '%24(x''", 21, ParserOptions.AllowAll)] // Position of extra quote (test escaped chars)
    internal void ErrorPosition_ShouldBeReportedCorrectly(string input, int expectedPosition, ParserOptions options)
    {
        var parser = new Parser();

        Should.Throw<InvalidProjectFileException>(() =>
        {
            parser.Parse(input, options, MockElementLocation.Instance);
        });

        parser.errorPosition.ShouldBe(expectedPosition);
    }

    /// <summary>
    ///  Verifies that the special error for single "=" (instead of "==") is produced,
    ///  and that the character found after the "=" is reported in UnexpectedlyFound.
    /// </summary>
    [Fact]
    public void SingleEquals_ShouldProduceSpecialError()
    {
        var scanner = new TestScanner("a=b", ParserOptions.AllowProperties);

        scanner.AdvanceToError("IllFormedEqualsInCondition", expectedUnexpectedlyFound: "b");
    }

    /// <summary>
    ///  Verifies that ill-formed property references (missing parentheses) produce the correct error messages.
    /// </summary>
    [Theory]
    [InlineData("$(", "IllFormedPropertyCloseParenthesisInCondition", "end of input")]
    [InlineData("$x", "IllFormedPropertyOpenParenthesisInCondition", "x")]
    public void Property_IllFormed_ShouldProduceCorrectError(string input, string expectedError, string expectedUnexpectedlyFound)
    {
        var scanner = new TestScanner(input, ParserOptions.AllowProperties);

        scanner.AdvanceToError(expectedError, expectedUnexpectedlyFound);
    }

    /// <summary>
    ///  Verifies that properties with spaces at the beginning or end (boundary spaces) produce an error,
    ///  as MSBuild does not allow leading or trailing whitespace in property names.
    /// </summary>
    [Theory]
    [InlineData("$(x )")]
    [InlineData("$( x)")]
    [InlineData("$([MSBuild]::DoSomething($(space ))")]
    [InlineData("$([MSBuild]::DoSomething($(_space ))")]
    public void Property_WithBoundarySpace_ShouldProduceError(string input)
    {
        var scanner = new TestScanner(input, ParserOptions.AllowProperties);

        scanner.AdvanceToError("IllFormedPropertySpaceInCondition", expectedUnexpectedlyFound: " ");
    }

    /// <summary>
    ///  Verifies that various ill-formed item list references (missing parentheses, unterminated quotes)
    ///  produce the correct error messages.
    /// </summary>
    [Theory]
    [InlineData("@(", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@x", "IllFormedItemListOpenParenthesisInCondition")]
    [InlineData("@(x", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@(x->'%(y)", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x'", "IllFormedItemListCloseParenthesisInCondition")]
    public void ItemList_IllFormed_ShouldProduceCorrectError(string input, string expectedError)
    {
        var scanner = new TestScanner(input);

        scanner.AdvanceToError(expectedError);
    }

    /// <summary>
    ///  Verifies that unterminated quoted strings (missing closing quote) produce the correct error.
    /// </summary>
    [Theory]
    [InlineData("false or 'abc")]
    [InlineData("'")]
    [InlineData("'$(DEBUG) == true")]
    public void String_Unterminated_ShouldProduceError(string input)
    {
        var scanner = new TestScanner(input);

        scanner.AdvanceToError("IllFormedQuotedStringInCondition", expectedUnexpectedlyFound: null);
    }

    /// <summary>
    ///  Verifies that item lists are not allowed when ParserOptions.AllowProperties is set
    ///  (which excludes AllowItemLists), producing the appropriate error.
    /// </summary>
    [Theory]
    [InlineData("@(foo)")]
    [InlineData("1234 '@(foo)'")]
    [InlineData("'1234 @(foo)'")]
    public void ItemList_WhenNotAllowed_ShouldFail(string input)
    {
        var scanner = new TestScanner(input, ParserOptions.AllowProperties);

        scanner.AdvanceToError("ItemListNotAllowedInThisConditional");
    }

    /// <summary>
    ///  Creates a token verifier for a string token with the specified expected text.
    /// </summary>
    private static TokenVerifier String(string expectedText)
        => (TokenKind.String, expectedText);

    /// <summary>
    ///  Creates a token verifier for an item list token with the specified expected text.
    /// </summary>
    private static TokenVerifier ItemList(string expectedText)
        => (TokenKind.ItemList, expectedText);

    /// <summary>
    ///  Creates a token verifier for a function token with the specified expected name.
    /// </summary>
    private static TokenVerifier Function(string expectedText)
        => (TokenKind.Function, expectedText);

    /// <summary>
    ///  Creates a token verifier for a numeric token with the specified expected text.
    /// </summary>
    private static TokenVerifier Numeric(string expectedText)
        => (TokenKind.Numeric, expectedText);

    /// <summary>
    ///  Represents an expectation for a token's kind and optionally its text.
    ///  Supports implicit conversion from TokenKind and (TokenKind, string) tuples.
    /// </summary>
    private readonly struct TokenVerifier
    {
        private readonly TokenKind _expectedKind;
        private readonly string? _expectedText;

        private TokenVerifier(TokenKind kind, string? text = null)
        {
            _expectedKind = kind;
            _expectedText = text;
        }

        public static implicit operator TokenVerifier(TokenKind kind)
            => new(kind);

        public static implicit operator TokenVerifier((TokenKind Kind, string Text) value)
            => new(value.Kind, value.Text);

        /// <summary>
        ///  Verifies that the token matches the expected kind and text (if specified).
        /// </summary>
        public void Verify(Token token)
        {
            token.Kind.ShouldBe(_expectedKind);

            if (_expectedText is not null)
            {
                token.Text.ShouldBe(_expectedText);
            }
        }
    }

    /// <summary>
    ///  A test helper that wraps a Scanner and provides convenient methods for verifying token sequences
    ///  and error conditions.
    /// </summary>
    private readonly ref struct TestScanner(string expression, ParserOptions options = ParserOptions.AllowAll)
    {
        private readonly Scanner _scanner = new(expression, options);

        /// <summary>
        ///  Verifies that the scanner produces the expected sequence of tokens followed by EndOfInput.
        /// </summary>
        public void VerifyAll(params IEnumerable<TokenVerifier> expectations)
        {
            foreach (TokenVerifier expectation in expectations)
            {
                _scanner.Advance().ShouldBeTrue();
                expectation.Verify(_scanner.CurrentToken);
            }

            _scanner.Advance().ShouldBeTrue();
            _scanner.IsNext(TokenKind.EndOfInput).ShouldBeTrue();
        }

        /// <summary>
        ///  Advances the scanner to the point of error and verifies the expected error resource
        ///  and optionally the character(s) unexpectedly found.
        /// </summary>
        public void AdvanceToError(string expectedErrorResource, string? expectedUnexpectedlyFound = null)
        {
            while (_scanner.Advance() && !_scanner.IsNext(TokenKind.EndOfInput))
            {
            }

            _scanner.GetErrorResource().ShouldBe(expectedErrorResource);

            if (expectedUnexpectedlyFound is null)
            {
                _scanner.UnexpectedlyFound.ShouldBeNull();
            }
            else
            {
                _scanner.UnexpectedlyFound.ShouldBe(expectedUnexpectedlyFound);
            }
        }
    }
}
