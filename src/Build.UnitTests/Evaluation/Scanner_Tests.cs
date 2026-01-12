// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Shouldly;
using VerifyTests;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation;

public class ScannerTest
{
    /// <summary>
    /// Tests that we give a useful error position (not 0 for example)
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
    [InlineData("    0x", 6)] // Position of x
    internal void ErrorPosition(string expression, int errorPosition, ParserOptions options = ParserOptions.AllowAll)
    {
        // Some errors are caught by the Parser, not merely by the Lexer/Scanner. So we have to do a full Parse,
        // rather than just calling AdvanceToScannerError(). (The error location is still supplied by the Scanner.)
        int actualErrorPosition = -1;

        _ = Assert.Throws<InvalidProjectFileException>(() =>
        {
            Parser parser = default;
            try
            {
                parser = new Parser(expression, options, MockElementLocation.Instance);
                parser.Parse();
            }
            catch (InvalidProjectFileException)
            {
                actualErrorPosition = parser.errorPosition;
                throw;
            }
        });

        Assert.Equal(errorPosition, actualErrorPosition);
    }

    /// <summary>
    /// Advance to the point of the lexer error. If the error is only caught by the parser, this isn't useful.
    /// </summary>
    /// <param name="lexer"></param>
    private void AdvanceToScannerError(ref Scanner lexer)
    {
        while (lexer.Advance() && !lexer.IsCurrent(TokenKind.EndOfInput))
        {
            ;
        }
    }

    /// <summary>
    /// Tests the special error for "=".
    /// </summary>
    [Fact]
    public void SingleEquals()
    {
        Scanner lexer = new Scanner("a=b", ParserOptions.AllowProperties);
        AdvanceToScannerError(ref lexer);
        Assert.Equal("IllFormedEqualsInCondition", lexer.GetErrorResource());
        Assert.Equal("b", lexer.UnexpectedlyFound);
    }

    /// <summary>
    /// Tests the space not next to end so no errors case.
    /// </summary>
    [Theory]
    [InlineData("$(x.StartsWith( 'y' ))")]
    [InlineData("$(x.StartsWith ('y'))")]
    [InlineData("$( x.StartsWith( $(SpacelessProperty) ) )")]
    [InlineData("$( x.StartsWith( $(_SpacelessProperty) ) )")]
    [InlineData("$(x.StartsWith('Foo', StringComparison.InvariantCultureIgnoreCase))")]
    public void SpaceInMiddleOfProperty(string pattern)
    {
        var lexer = new TestScanner(pattern, ParserOptions.AllowProperties);
        lexer.AdvanceToErrorOrEnd();
        lexer.ErrorState.ShouldBeFalse();
    }

    /// <summary>
    /// Tests the special error for unterminated quotes.
    /// Note, scanner only understands single quotes.
    /// </summary>
    [Fact]
    public void IllFormedQuotedString()
    {
        Scanner lexer = new Scanner("false or 'abc", ParserOptions.AllowAll);
        AdvanceToScannerError(ref lexer);
        Assert.Equal("IllFormedQuotedStringInCondition", lexer.GetErrorResource());
        Assert.Null(lexer.UnexpectedlyFound);

        lexer = new Scanner("\'", ParserOptions.AllowAll);
        AdvanceToScannerError(ref lexer);
        Assert.Equal("IllFormedQuotedStringInCondition", lexer.GetErrorResource());
        Assert.Null(lexer.UnexpectedlyFound);
    }

    [Fact]
    public void NumericSingleTokenTests()
    {
        var scanner = new TestScanner("1234");
        scanner.Number("1234", 0);
        scanner.EndOfInput(4);

        scanner = new TestScanner("-1234");
        scanner.Number("-1234", 0);
        scanner.EndOfInput(5);

        scanner = new TestScanner("+1234");
        scanner.Number("+1234", 0);
        scanner.EndOfInput(5);

        scanner = new TestScanner("1234.1234");
        scanner.Number("1234.1234", 0);
        scanner.EndOfInput(9);

        scanner = new TestScanner(".1234");
        scanner.Number(".1234", 0);
        scanner.EndOfInput(5);

        scanner = new TestScanner("+.1234");
        scanner.Number("+.1234", 0);
        scanner.EndOfInput(6);

        scanner = new TestScanner("1234.");
        scanner.Number("1234.", 0);
        scanner.EndOfInput(5);

        scanner = new TestScanner("0x1234");
        scanner.Number("0x1234", 0);
        scanner.EndOfInput(6);

        scanner = new TestScanner("0X1234abcd");
        scanner.Number("0X1234abcd", 0);
        scanner.EndOfInput(10);

        scanner = new TestScanner("0x1234ABCD");
        scanner.Number("0x1234ABCD", 0);
        scanner.EndOfInput(10);

        scanner = new TestScanner("0xx");
        scanner.Number("0", 0);
        scanner.Identifier("xx", 1);
        scanner.EndOfInput(3);
    }

    [Fact]
    public void AllowedDecimalNumbers()
    {
        Assert.True(ValidDecimalNumber("1234", out _));
        Assert.True(ValidDecimalNumber("-1234", out _));
        Assert.True(ValidDecimalNumber("+1234", out _));
        Assert.True(ValidDecimalNumber("1234.1234", out _));
        Assert.True(ValidDecimalNumber(".1234", out _));
        Assert.True(ValidDecimalNumber("+.1234", out _));
        Assert.True(ValidDecimalNumber("1234.", out _));

        static bool ValidDecimalNumber(string number, out double value)
        {
            return Double.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture.NumberFormat, out value) && !double.IsInfinity(value);
        }
    }

    [Fact]
    public void PropsStringsAndBooleanSingleTokenTests()
    {
        var scanner = new TestScanner("$(foo)");
        scanner.Dollar(0);
        scanner.LeftParen(1);
        scanner.Identifier("foo", 2);
        scanner.RightParen(5);
        scanner.EndOfInput(6);

        scanner = new TestScanner("@(foo)");
        scanner.At(0);
        scanner.LeftParen(1);
        scanner.Identifier("foo", 2);
        scanner.RightParen(5);
        scanner.EndOfInput(6);

        scanner = new TestScanner("abcde");
        scanner.Identifier("abcde", 0);
        scanner.EndOfInput(5);

        scanner = new TestScanner("'abc-efg'");
        scanner.String("abc-efg", 0);
        scanner.EndOfInput(9);

        scanner = new TestScanner("and");
        scanner.And(0);
        scanner.EndOfInput(3);

        scanner = new TestScanner("or");
        scanner.Or(0);
        scanner.EndOfInput(2);

        scanner = new TestScanner("AnD");
        scanner.And(0);
        scanner.EndOfInput(3);

        scanner = new TestScanner("Or");
        scanner.Or(0);
        scanner.EndOfInput(2);
    }

    [Theory]
    [InlineData("true", TokenKind.True)]
    [InlineData("TRUE", TokenKind.True)]
    [InlineData("false", TokenKind.False)]
    [InlineData("FALSE", TokenKind.False)]
    [InlineData("on", TokenKind.On)]
    [InlineData("ON", TokenKind.On)]
    [InlineData("off", TokenKind.Off)]
    [InlineData("OFF", TokenKind.Off)]
    [InlineData("yes", TokenKind.Yes)]
    [InlineData("YES", TokenKind.Yes)]
    [InlineData("no", TokenKind.No)]
    [InlineData("NO", TokenKind.No)]
    internal void BooleanLiterals(string expression, TokenKind expectedKind)
    {
        var scanner = new TestScanner(expression);
        Assert.True(scanner.Advance());

        Token current = scanner.Current;
        Assert.Equal(expectedKind, current.Kind);
    }

    [Theory]
    [InlineData("'true'", true)]
    [InlineData("'TRUE'", true)]
    [InlineData("'false'", false)]
    [InlineData("'FALSE'", false)]
    [InlineData("'on'", true)]
    [InlineData("'ON'", true)]
    [InlineData("'off'", false)]
    [InlineData("'OFF'", false)]
    [InlineData("'yes'", true)]
    [InlineData("'YES'", true)]
    [InlineData("'no'", false)]
    [InlineData("'NO'", false)]
    [InlineData("'!true'", false)]
    [InlineData("'!TRUE'", false)]
    [InlineData("'!false'", true)]
    [InlineData("'!FALSE'", true)]
    [InlineData("'!on'", false)]
    [InlineData("'!ON'", false)]
    [InlineData("'!off'", true)]
    [InlineData("'!OFF'", true)]
    [InlineData("'!yes'", false)]
    [InlineData("'!YES'", false)]
    [InlineData("'!no'", true)]
    [InlineData("'!NO'", true)]
    public void BooleanStringLiterals(string expression, bool isTrue)
    {
        var scanner = new TestScanner(expression);
        Assert.True(scanner.Advance());

        Token current = scanner.Current;
        Assert.Equal(TokenKind.String, current.Kind);
        Assert.False(current.IsExpandable);

        if (isTrue)
        {
            Assert.False(current.IsBooleanFalse);
            Assert.True(current.IsBooleanTrue);
        }
        else
        {
            Assert.True(current.IsBooleanFalse);
            Assert.False(current.IsBooleanTrue);
        }
    }

    [Fact]
    public void SimpleSingleTokenTests()
    {
        var scanner = new TestScanner("(");
        scanner.LeftParen(0);
        scanner.EndOfInput(1);

        scanner = new TestScanner(")");
        scanner.RightParen(0);
        scanner.EndOfInput(1);

        scanner = new TestScanner(",");
        scanner.Comma(0);
        scanner.EndOfInput(1);

        scanner = new TestScanner("==");
        scanner.EqualTo(0);
        scanner.EndOfInput(2);

        scanner = new TestScanner("!=");
        scanner.NotEqualTo(0);
        scanner.EndOfInput(2);

        scanner = new TestScanner("<");
        scanner.LessThan(0);
        scanner.EndOfInput(1);

        scanner = new TestScanner(">");
        scanner.GreaterThan(0);
        scanner.EndOfInput(1);

        scanner = new TestScanner("<=");
        scanner.LessThanOrEqualTo(0);
        scanner.EndOfInput(2);

        scanner = new TestScanner(">=");
        scanner.GreaterThanOrEqualTo(0);
        scanner.EndOfInput(2);

        scanner = new TestScanner("!");
        scanner.Next(TokenKind.Not, 0);
        scanner.EndOfInput(1);
    }

    [Fact]
    public void StringEdgeTests()
    {
        var scanner = new TestScanner("@(Foo, ' ')");
        scanner.At(0);
        scanner.LeftParen(1);
        scanner.Identifier("Foo", 2);
        scanner.Comma(5);
        scanner.String(" ", 7);
        scanner.RightParen(10);
        scanner.EndOfInput(11);

        scanner = new TestScanner("'@(Foo, ' ')'");
        scanner.String("@(Foo, ' ')", 0);
        scanner.EndOfInput(13);

        scanner = new TestScanner("'%40(( '");
        scanner.String("%40(( ", 0);
        scanner.EndOfInput(8);

        scanner = new TestScanner("'@(Complex_ItemType-123, ';')' == ''");
        scanner.String("@(Complex_ItemType-123, ';')", 0);
        scanner.EqualTo(31);
        scanner.String("", 34);
        scanner.EndOfInput(36);
    }

    [Fact]
    public void FunctionTests()
    {
        var scanner = new TestScanner("Foo()");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.RightParen(4);
        scanner.EndOfInput(5);

        scanner = new TestScanner("Foo( 1 )");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.Number("1", 5);
        scanner.RightParen(7);
        scanner.EndOfInput(8);

        scanner = new TestScanner("Foo( $(Property) )");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.Dollar(5);
        scanner.LeftParen(6);
        scanner.Identifier("Property", 7);
        scanner.RightParen(15);
        scanner.RightParen(17);
        scanner.EndOfInput(18);

        scanner = new TestScanner("Foo( @(ItemList) )");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.At(5);
        scanner.LeftParen(6);
        scanner.Identifier("ItemList", 7);
        scanner.RightParen(15);
        scanner.RightParen(17);
        scanner.EndOfInput(18);

        scanner = new TestScanner("Foo( simplestring )");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.Identifier("simplestring", 5);
        scanner.RightParen(18);
        scanner.EndOfInput(19);

        scanner = new TestScanner("Foo( 'Not a Simple String' )");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.String("Not a Simple String", 5);
        scanner.RightParen(27);
        scanner.EndOfInput(28);

        scanner = new TestScanner("Foo( 'Not a Simple String', 1234 )");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.String("Not a Simple String", 5);
        scanner.Comma(26);
        scanner.Number("1234", 28);
        scanner.RightParen(33);
        scanner.EndOfInput(34);

        scanner = new TestScanner("Foo( $(Property), 'Not a Simple String', 1234 )");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.Dollar(5);
        scanner.LeftParen(6);
        scanner.Identifier("Property", 7);
        scanner.RightParen(15);
        scanner.Comma(16);
        scanner.String("Not a Simple String", 18);
        scanner.Comma(39);
        scanner.Number("1234", 41);
        scanner.RightParen(46);
        scanner.EndOfInput(47);

        scanner = new TestScanner("Foo( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )");
        scanner.Identifier("Foo", 0);
        scanner.LeftParen(3);
        scanner.At(5);
        scanner.LeftParen(6);
        scanner.Identifier("ItemList", 7);
        scanner.RightParen(15);
        scanner.Comma(16);
        scanner.Dollar(18);
        scanner.LeftParen(19);
        scanner.Identifier("Property", 20);
        scanner.RightParen(28);
        scanner.Comma(29);
        scanner.Identifier("simplestring", 31);
        scanner.Comma(43);
        scanner.String("Not a Simple String", 45);
        scanner.Comma(66);
        scanner.Number("1234", 68);
        scanner.RightParen(73);
        scanner.EndOfInput(74);
    }

    [Fact]
    public void ComplexTests1()
    {
        var scanner = new TestScanner("'String with a $(Property) inside'");
        scanner.String("String with a $(Property) inside", 0);
        scanner.EndOfInput(34);

        scanner = new TestScanner("@(list, ' ')");
        scanner.At(0);
        scanner.LeftParen(1);
        scanner.Identifier("list", 2);
        scanner.Comma(6);
        scanner.String(" ", 8);
        scanner.RightParen(11);
        scanner.EndOfInput(12);

        scanner = new TestScanner("@(files->'%(Filename)')");
        scanner.At(0);
        scanner.LeftParen(1);
        scanner.Identifier("files", 2);
        scanner.Arrow(7);
        scanner.String("%(Filename)", 9);
        scanner.RightParen(22);
        scanner.EndOfInput(23);
    }

    [Fact]
    public void ComplexTests2()
    {
        var scanner = new TestScanner("1234");
        scanner.Number("1234", 0);

        scanner = new TestScanner("'abc-efg'==$(foo)");
        scanner.String("abc-efg", 0);
        scanner.EqualTo(9);
        scanner.Dollar(11);
        scanner.LeftParen(12);
        scanner.Identifier("foo", 13);
        scanner.RightParen(16);
        scanner.EndOfInput(17);

        scanner = new TestScanner("$(debug)!=true");
        scanner.Dollar(0);
        scanner.LeftParen(1);
        scanner.Identifier("debug", 2);
        scanner.RightParen(7);
        scanner.NotEqualTo(8);
        scanner.True(10);
        scanner.EndOfInput(14);

        scanner = new TestScanner("$(VERSION)<5");
        scanner.Dollar(0);
        scanner.LeftParen(1);
        scanner.Identifier("VERSION", 2);
        scanner.RightParen(9);
        scanner.LessThan(10);
        scanner.Number("5", 11);
        scanner.EndOfInput(12);

        scanner = new TestScanner("$([MSBuild]::DoesTaskHostExist(`CLR2`,`CurrentArchitecture`))");
        scanner.Dollar(0);
        scanner.LeftParen(1);
        scanner.LeftBracket(2);
        scanner.Identifier("MSBuild", 3);
        scanner.RightBracket(10);
        scanner.DoubleColon(11);
        scanner.Identifier("DoesTaskHostExist", 13);
        scanner.LeftParen(30);
        scanner.String("CLR2", 31);
        scanner.Comma(37);
        scanner.String("CurrentArchitecture", 38);
        scanner.RightParen(59);
        scanner.RightParen(60);
        scanner.EndOfInput(61);
    }

    /// <summary>
    /// Tests all tokens with no whitespace and whitespace.
    /// </summary>
    [Fact]
    public void WhitespaceTests()
    {
        var scanner = new TestScanner("$(DEBUG) and $(FOO)");
        scanner.Dollar(0);
        scanner.LeftParen(1);
        scanner.Identifier("DEBUG", 2);
        scanner.RightParen(7);
        scanner.And(9);
        scanner.Dollar(13);
        scanner.LeftParen(14);
        scanner.Identifier("FOO", 15);
        scanner.RightParen(18);
        scanner.EndOfInput(19);

        scanner = new TestScanner("1234$(DEBUG)0xabcd@(foo)asdf<>'foo'<=false>=true==1234!=");
        scanner.Number("1234", 0);
        scanner.Dollar(4);
        scanner.LeftParen(5);
        scanner.Identifier("DEBUG", 6);
        scanner.RightParen(11);
        scanner.Number("0xabcd", 12);
        scanner.At(18);
        scanner.LeftParen(19);
        scanner.Identifier("foo", 20);
        scanner.RightParen(23);
        scanner.Identifier("asdf", 24);
        scanner.LessThan(28);
        scanner.GreaterThan(29);
        scanner.String("foo", 30);
        scanner.LessThanOrEqualTo(35);
        scanner.False(37);
        scanner.GreaterThanOrEqualTo(42);
        scanner.True(44);
        scanner.EqualTo(48);
        scanner.Number("1234", 50);
        scanner.NotEqualTo(54);
        scanner.EndOfInput(56);

        scanner = new TestScanner("   1234    $(DEBUG)    0xabcd  \n@(foo)    \nasdf  \n<     \n>     \n'foo'  \n<=    \nfalse     \n>=    \ntrue  \n== \n 1234    \n!=     ");
        scanner.Number("1234", 3);
        scanner.Dollar(11);
        scanner.LeftParen(12);
        scanner.Identifier("DEBUG", 13);
        scanner.RightParen(18);
        scanner.Number("0xabcd", 23);
        scanner.At(32);
        scanner.LeftParen(33);
        scanner.Identifier("foo", 34);
        scanner.RightParen(37);
        scanner.Identifier("asdf", 43);
        scanner.LessThan(50);
        scanner.GreaterThan(57);
        scanner.String("foo", 64);
        scanner.LessThanOrEqualTo(72);
        scanner.False(79);
        scanner.GreaterThanOrEqualTo(90);
        scanner.True(97);
        scanner.EqualTo(104);
        scanner.Number("1234", 109);
        scanner.NotEqualTo(118);
        scanner.EndOfInput(125);
    }

    /// <summary>
    /// Tests that shouldn't work.
    /// </summary>
    [Fact]
    public void NegativeTests()
    {
        var scanner = new TestScanner("'$(DEBUG) == true");
        Assert.False(scanner.Advance());
    }

    /// <summary>
    /// Tests that tokens have correct position information.
    /// </summary>
    [Fact]
    public void TokenPositionTests()
    {
        var scanner = new TestScanner("$(foo) == 'bar'");

        scanner.Dollar(0);
        scanner.LeftParen(1);
        scanner.Identifier("foo", 2);
        scanner.RightParen(5);
        scanner.EqualTo(7);
        scanner.String("bar", 10);
        scanner.EndOfInput(15);
    }

    /// <summary>
    /// Tests that numeric tokens have correct position information.
    /// </summary>
    [Fact]
    public void NumericTokenPositionTests()
    {
        var scanner = new TestScanner("  1234 x 0x5678");

        scanner.Number("1234", 2);
        scanner.Identifier("x", 7);
        scanner.Number("0x5678", 9);
        scanner.EndOfInput(15);
    }

    /// <summary>
    /// Tests that operator tokens have correct position information.
    /// </summary>
    [Fact]
    public void OperatorTokenPositionTests()
    {
        var scanner = new TestScanner("( ) , < > <= >= == !=");

        scanner.LeftParen(0);
        scanner.RightParen(2);
        scanner.Comma(4);
        scanner.LessThan(6);
        scanner.GreaterThan(8);
        scanner.LessThanOrEqualTo(10);
        scanner.GreaterThanOrEqualTo(13);
        scanner.EqualTo(16);
        scanner.NotEqualTo(19);
        scanner.EndOfInput(21);
    }

    /// <summary>
    /// Tests that keyword tokens have correct position information.
    /// </summary>
    [Fact]
    public void KeywordTokenPositionTests()
    {
        var scanner = new TestScanner("true and false or yes");

        scanner.True(0);
        scanner.And(5);
        scanner.False(9);
        scanner.Or(15);
        scanner.Yes(18);
        scanner.EndOfInput(21);
    }

    /// <summary>
    /// Tests that item list and metadata tokens have correct position information.
    /// </summary>
    [Fact]
    public void ItemAndMetadataTokenPositionTests()
    {
        var scanner = new TestScanner("@(ItemList) != %(Metadata)");

        scanner.At(0);
        scanner.LeftParen(1);
        scanner.Identifier("ItemList", 2);
        scanner.RightParen(10);
        scanner.NotEqualTo(12);
        scanner.Percent(15);
        scanner.LeftParen(16);
        scanner.Identifier("Metadata", 17);
        scanner.RightParen(25);
        scanner.EndOfInput(26);
    }

    private ref struct TestScanner(
        string expression,
        ParserOptions options = ParserOptions.AllowAll)
    {
        private Scanner _scanner = new(expression, options);

        public void EndOfInput(int position)
            => Next(TokenKind.EndOfInput, position);

        public void Comma(int position)
            => Next(TokenKind.Comma, position);

        public void Dot(int position)
            => Next(TokenKind.Dot, position);

        public void LeftParen(int position)
            => Next(TokenKind.LeftParenthesis, position);

        public void RightParen(int position)
            => Next(TokenKind.RightParenthesis, position);

        public void LeftBracket(int position)
            => Next(TokenKind.LeftBracket, position);

        public void RightBracket(int position)
            => Next(TokenKind.RightBracket, position);

        public void DoubleColon(int position)
            => Next(TokenKind.DoubleColon, position);

        public void Arrow(int position)
            => Next(TokenKind.Arrow, position);

        public void Dollar(int position)
            => Next(TokenKind.DollarSign, position);

        public void At(int position)
            => Next(TokenKind.AtSign, position);

        public void Percent(int position)
            => Next(TokenKind.PercentSign, position);

        public void EqualTo(int position)
            => Next(TokenKind.EqualTo, position);

        public void NotEqualTo(int position)
            => Next(TokenKind.NotEqualTo, position);

        public void LessThan(int position)
            => Next(TokenKind.LessThan, position);

        public void LessThanOrEqualTo(int position)
            => Next(TokenKind.LessThanOrEqualTo, position);

        public void GreaterThan(int position)
            => Next(TokenKind.GreaterThan, position);

        public void GreaterThanOrEqualTo(int position)
            => Next(TokenKind.GreaterThanOrEqualTo, position);

        public void And(int position)
            => Next(TokenKind.And, position);

        public void Or(int position)
            => Next(TokenKind.Or, position);

        public void True(int position)
            => Next(TokenKind.True, position);

        public void False(int position)
            => Next(TokenKind.False, position);

        public void On(int position)
            => Next(TokenKind.On, position);

        public void Off(int position)
            => Next(TokenKind.Off, position);

        public void Yes(int position)
            => Next(TokenKind.Yes, position);

        public void No(int position)
            => Next(TokenKind.No, position);

        public void Number(string text, int position)
            => Next(TokenKind.Numeric, text, position);

        public void String(string text, int position)
            => Next(TokenKind.String, text, position);

        public void Identifier(string text, int position)
            => Next(TokenKind.Identifier, text, position);

        public void Next(TokenKind kind, string text)
        {
            _scanner.Advance();
            Assert.Equal(kind, _scanner.Current.Kind);
            Assert.Equal(text, _scanner.Current.Text.ToString());
        }

        public void Next(TokenKind kind, int position)
        {
            _scanner.Advance();
            Assert.Equal(kind, _scanner.Current.Kind);
            Assert.Equal(position, _scanner.Current.Position);
        }

        public void Next(TokenKind kind, string text, int position)
        {
            _scanner.Advance();
            Assert.Equal(kind, _scanner.Current.Kind);
            Assert.Equal(text, _scanner.Current.Text.ToString());
            Assert.Equal(position, _scanner.Current.Position);
        }

        public bool Advance()
            => _scanner.Advance();

        public void AdvanceToErrorOrEnd()
        {
            while (!_scanner.IsCurrent(TokenKind.EndOfInput) && _scanner.Advance())
            {
            }
        }

        public bool ErrorState => _scanner._errorState;

        public Token Current => _scanner.Current;
        public bool IsCurrent(TokenKind kind) => _scanner.IsCurrent(kind);
        public string GetErrorResource() => _scanner.GetErrorResource();
    }
}
