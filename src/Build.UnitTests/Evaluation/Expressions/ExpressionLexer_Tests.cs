// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation.Expressions;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation.Expressions;

public class ExpressionLexer_Tests
{
    [Theory]
    [InlineData("(", TokenKind.LeftParenthesis)]
    [InlineData(")", TokenKind.RightParenthesis)]
    [InlineData("[", TokenKind.LeftBracket)]
    [InlineData("]", TokenKind.RightBracket)]
    [InlineData(",", TokenKind.Comma)]
    [InlineData(";", TokenKind.Semicolon)]
    [InlineData(".", TokenKind.Dot)]
    [InlineData("->", TokenKind.Arrow)]
    [InlineData("::", TokenKind.DoubleColon)]
    [InlineData("$", TokenKind.DollarSign)]
    [InlineData("@", TokenKind.AtSign)]
    [InlineData("%", TokenKind.PercentSign)]
    [InlineData("==", TokenKind.EqualTo)]
    [InlineData("!=", TokenKind.NotEqualTo)]
    [InlineData("<", TokenKind.LessThan)]
    [InlineData("<=", TokenKind.LessThanOrEqualTo)]
    [InlineData(">", TokenKind.GreaterThan)]
    [InlineData(">=", TokenKind.GreaterThanOrEqualTo)]
    [InlineData("!", TokenKind.Not)]
    internal void SingleTokens(string input, TokenKind expectedKind)
    {
        var lexer = new ExpressionLexer(input);

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Kind.ShouldBe(expectedKind);
        lexer.Current.Text.ToString().ShouldBe(input);

        lexer.MoveNext().ShouldBeFalse();
        lexer.Current.Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Theory]
    [InlineData("and", TokenKind.And)]
    [InlineData("AND", TokenKind.And)]
    [InlineData("And", TokenKind.And)]
    [InlineData("or", TokenKind.Or)]
    [InlineData("OR", TokenKind.Or)]
    [InlineData("Or", TokenKind.Or)]
    internal void Keywords(string input, TokenKind expectedKind)
    {
        var lexer = new ExpressionLexer(input);

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Kind.ShouldBe(expectedKind);
        lexer.Current.Text.ToString().ShouldBe(input);
    }

    [Theory]
    [InlineData("42", "42")]
    [InlineData("3.14", "3.14")]
    [InlineData("0xFF", "0xFF")]
    [InlineData("0x10", "0x10")]
    [InlineData("-5", "-5")]
    [InlineData("+10", "+10")]
    [InlineData("1.23.45", "1.23")] // Parser will handle this error
    public void Numbers(string input, string expectedText)
    {
        var lexer = new ExpressionLexer(input);

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Kind.ShouldBe(TokenKind.Number);
        lexer.Current.Text.ToString().ShouldBe(expectedText);
    }

    [Theory]
    [InlineData("'foo'", "'foo'")]
    [InlineData("\"bar\"", "\"bar\"")]
    [InlineData("`baz`", "`baz`")]
    [InlineData("'multiple words'", "'multiple words'")]
    [InlineData("'$(Property)'", "'$(Property)'")]
    public void Strings(string input, string expectedText)
    {
        var lexer = new ExpressionLexer(input);

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Kind.ShouldBe(TokenKind.String);
        lexer.Current.Text.ToString().ShouldBe(expectedText);
    }

    [Theory]
    [InlineData("PropertyName")]
    [InlineData("_underscore")]
    [InlineData("With123Numbers")]
    public void Identifiers(string input)
    {
        var lexer = new ExpressionLexer(input);

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Kind.ShouldBe(TokenKind.Identifier);
        lexer.Current.Text.ToString().ShouldBe(input);
    }

    [Theory]
    [InlineData("Item-With-Hyphens")]
    [InlineData("Item_With_Underscores")]
    [InlineData("Mixed-Hyphens_And_Underscores")]
    [InlineData("Item123-With-Numbers")]
    [InlineData("_Leading-Underscore-With-Hyphens")]
    public void IdentifiersWithSpecialCharacters(string input)
    {
        var lexer = new ExpressionLexer(input);

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Kind.ShouldBe(TokenKind.Identifier);
        lexer.Current.Text.ToString().ShouldBe(input);

        lexer.MoveNext().ShouldBeFalse();
        lexer.Current.Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Fact]
    public void PropertyReference_IsTokenizedAtomically()
    {
        // $(Configuration) should produce: $, (, Configuration, )
        var tokens = LexTokens("$(Configuration)");

        tokens.Count.ShouldBe(5); // $, (, Configuration, ), EOF
        tokens[0].Kind.ShouldBe(TokenKind.DollarSign);
        tokens[1].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.Identifier);
        tokens[2].Text.ToString().ShouldBe("Configuration");
        tokens[3].Kind.ShouldBe(TokenKind.RightParenthesis);
        tokens[4].Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Fact]
    public void PropertyFunctionCall_IsTokenizedAtomically()
    {
        // $(Property.ToUpper()) should produce: $, (, Property, ., ToUpper, (, ), )
        var tokens = LexTokens("$(Property.ToUpper())");

        tokens[0].Kind.ShouldBe(TokenKind.DollarSign);
        tokens[1].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.Identifier);
        tokens[2].Text.ToString().ShouldBe("Property");
        tokens[3].Kind.ShouldBe(TokenKind.Dot);
        tokens[4].Kind.ShouldBe(TokenKind.Identifier);
        tokens[4].Text.ToString().ShouldBe("ToUpper");
        tokens[5].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[6].Kind.ShouldBe(TokenKind.RightParenthesis);
        tokens[7].Kind.ShouldBe(TokenKind.RightParenthesis);
    }

    [Fact]
    public void ItemVector_IsTokenizedAtomically()
    {
        // @(Items) should produce: @, (, Items, )
        var tokens = LexTokens("@(Items)");

        tokens.Count.ShouldBe(5); // @, (, Items, ), EOF
        tokens[0].Kind.ShouldBe(TokenKind.AtSign);
        tokens[1].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.Identifier);
        tokens[2].Text.ToString().ShouldBe("Items");
        tokens[3].Kind.ShouldBe(TokenKind.RightParenthesis);
    }

    [Fact]
    public void ItemTransform_IsTokenizedAtomically()
    {
        // @(Items->'%(FullPath)') should produce: @, (, Items, ->, ', %, (, FullPath, ), ', )
        var tokens = LexTokens("@(Items->'%(FullPath)')");

        tokens[0].Kind.ShouldBe(TokenKind.AtSign);
        tokens[1].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.Identifier);
        tokens[2].Text.ToString().ShouldBe("Items");
        tokens[3].Kind.ShouldBe(TokenKind.Arrow);
        tokens[4].Kind.ShouldBe(TokenKind.String);
        tokens[4].Text.ToString().ShouldBe("'%(FullPath)'");
        tokens[5].Kind.ShouldBe(TokenKind.RightParenthesis);
    }

    [Fact]
    public void ConditionalExpression_IsTokenizedCorrectly()
    {
        // '$(Configuration)' == 'Debug'
        var tokens = LexTokens("'$(Configuration)' == 'Debug'");

        tokens[0].Kind.ShouldBe(TokenKind.String);
        tokens[0].Text.ToString().ShouldBe("'$(Configuration)'");
        tokens[1].Kind.ShouldBe(TokenKind.EqualTo);
        tokens[2].Kind.ShouldBe(TokenKind.String);
        tokens[2].Text.ToString().ShouldBe("'Debug'");
        tokens[3].Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Fact]
    public void WhitespaceIsSkipped()
    {
        var tokens = LexTokens("  (  )  ");

        tokens.Count.ShouldBe(3); // (, ), EOF
        tokens[0].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[1].Kind.ShouldBe(TokenKind.RightParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Fact]
    public void ComplexExpression()
    {
        // @(Compile->'%(Filename).obj', ';')
        var tokens = LexTokens("@(Compile->'%(Filename).obj', ';')");

        // Verify key tokens
        tokens[0].Kind.ShouldBe(TokenKind.AtSign);
        tokens[1].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.Identifier);
        tokens[2].Text.ToString().ShouldBe("Compile");
        tokens[3].Kind.ShouldBe(TokenKind.Arrow);
        tokens[4].Kind.ShouldBe(TokenKind.String);
        tokens[4].Text.ToString().ShouldBe("'%(Filename).obj'");
        tokens[5].Kind.ShouldBe(TokenKind.Comma);
        tokens[6].Kind.ShouldBe(TokenKind.String);
        tokens[6].Text.ToString().ShouldBe("';'");
        tokens[7].Kind.ShouldBe(TokenKind.RightParenthesis);
    }

    [Fact]
    public void TokenPositions_AreCorrect()
    {
        var lexer = new ExpressionLexer("( abc )");

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Start.ShouldBe(0); // (

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Start.ShouldBe(2); // abc (position 2 after skipping space)

        lexer.MoveNext().ShouldBeTrue();
        lexer.Current.Start.ShouldBe(6); // )
    }

    [Fact]
    public void ItemVectorWithHyphensInName_IsTokenizedCorrectly()
    {
        // @(Item-With-Hyphens) should produce: @, (, Item-With-Hyphens, )
        var tokens = LexTokens("@(Item-With-Hyphens)");

        tokens.Count.ShouldBe(5); // @, (, Item-With-Hyphens, ), EOF
        tokens[0].Kind.ShouldBe(TokenKind.AtSign);
        tokens[1].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.Identifier);
        tokens[2].Text.ToString().ShouldBe("Item-With-Hyphens");
        tokens[3].Kind.ShouldBe(TokenKind.RightParenthesis);
        tokens[4].Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Fact]
    public void ItemTransformWithHyphensInName_IsTokenizedCorrectly()
    {
        // @(Item-With-Hyphens->'%(FullPath)') 
        // Should produce: @, (, Item-With-Hyphens, ->, '%(FullPath)', )
        var tokens = LexTokens("@(Item-With-Hyphens->'%(FullPath)')");

        tokens[0].Kind.ShouldBe(TokenKind.AtSign);
        tokens[1].Kind.ShouldBe(TokenKind.LeftParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.Identifier);
        tokens[2].Text.ToString().ShouldBe("Item-With-Hyphens");
        tokens[3].Kind.ShouldBe(TokenKind.Arrow);
        tokens[4].Kind.ShouldBe(TokenKind.String);
        tokens[4].Text.ToString().ShouldBe("'%(FullPath)'");
        tokens[5].Kind.ShouldBe(TokenKind.RightParenthesis);
    }

    [Fact]
    public void HyphenBeforeArrow_DoesNotIncludeHyphenInIdentifier()
    {
        // Item-> should produce: Item, ->
        // The hyphen is part of the arrow, not the identifier
        var tokens = LexTokens("Item->");

        tokens.Count.ShouldBe(3); // Item, ->, EOF
        tokens[0].Kind.ShouldBe(TokenKind.Identifier);
        tokens[0].Text.ToString().ShouldBe("Item");
        tokens[1].Kind.ShouldBe(TokenKind.Arrow);
        tokens[1].Text.ToString().ShouldBe("->");
        tokens[2].Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Fact]
    public void MultipleHyphensInIdentifier()
    {
        // Item--With--Double--Hyphens should be one token
        var tokens = LexTokens("Item--With--Double--Hyphens");

        tokens.Count.ShouldBe(2); // Identifier, EOF
        tokens[0].Kind.ShouldBe(TokenKind.Identifier);
        tokens[0].Text.ToString().ShouldBe("Item--With--Double--Hyphens");
        tokens[1].Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Fact]
    public void TrailingHyphenFollowedByNonArrow_IncludesHyphenInIdentifier()
    {
        // Item- ) should produce: Item-, )
        var tokens = LexTokens("Item- )");

        tokens.Count.ShouldBe(3); // Item-, ), EOF
        tokens[0].Kind.ShouldBe(TokenKind.Identifier);
        tokens[0].Text.ToString().ShouldBe("Item-");
        tokens[1].Kind.ShouldBe(TokenKind.RightParenthesis);
        tokens[2].Kind.ShouldBe(TokenKind.EndOfInput);
    }

    [Fact]
    public void HyphenAtEndOfInput_IncludedInIdentifier()
    {
        // Item- at end of input
        var tokens = LexTokens("Item-");

        tokens.Count.ShouldBe(2); // Item-, EOF
        tokens[0].Kind.ShouldBe(TokenKind.Identifier);
        tokens[0].Text.ToString().ShouldBe("Item-");
        tokens[1].Kind.ShouldBe(TokenKind.EndOfInput);
    }

    private static List<Token> LexTokens(string expression)
    {
        var lexer = new ExpressionLexer(expression);
        var tokens = new List<Token>();

        while (lexer.MoveNext())
        {
            tokens.Add(lexer.Current);
        }

        // Add the final EndOfInput token
        tokens.Add(lexer.Current);

        return tokens;
    }
}
