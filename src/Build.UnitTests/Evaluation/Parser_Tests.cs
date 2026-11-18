// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Tests condition parsing, diagnostics, and precedence warnings.
/// </summary>
/// <param name="output">The output helper for the test.</param>
public sealed class ParserTest(ITestOutputHelper output) : TestBase(output)
{
    /// <summary>
    ///  Verifies parsing of properties, metadata, boolean operators, and numeric literals.
    /// </summary>
    /// <param name="expression">The condition to parse without evaluating it.</param>
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
    public void SimpleParseTest(string expression)
        => Should.NotThrow(() =>
            ParseExpression(expression));

    /// <summary>
    ///  Verifies nested conditions, function calls, and escaped strings.
    /// </summary>
    /// <param name="expression">The condition to parse without evaluating it.</param>
    [Theory]
    [InlineData("($(foo) or $(bar)) and $(baz)")]
    [InlineData("$(foo) <= 5 and $(bar) >= 15")]
    [InlineData("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == simplestring) and 'a more complex string' != $(quux)")]
    [InlineData("(($(foo) or $(bar) == false) and !($(baz) == simplestring))")]
    [InlineData("(($(foo) or Exists('c:\\foo.txt')) and !(($(baz) == simplestring)))")]
    [InlineData("'CONTAINS%27QUOTE%27' == '$(TestQuote)'")]
    public void ComplexParseTest(string expression)
        => Should.NotThrow(() =>
            ParseExpression(expression));

    /// <summary>
    ///  Verifies negation of literals, expanded operands, and compound conditions.
    /// </summary>
    /// <param name="expression">The negated condition to parse.</param>
    [Theory]
    [InlineData("!true")]
    [InlineData("!(true)")]
    [InlineData("!($(foo) <= 5)")]
    [InlineData("!(%(foo) <= 5)")]
    [InlineData("!($(foo) <= 5 and $(bar) >= 15)")]
    public void NotParseTest(string expression)
        => Should.NotThrow(() =>
            ParseExpression(expression));

    /// <summary>
    ///  Verifies function-call syntax with empty, numeric, boolean, property, and string arguments.
    /// </summary>
    /// <param name="expression">The function call to parse without invoking it.</param>
    [Theory]
    [InlineData("SimpleFunctionCall()")]
    [InlineData("SimpleFunctionCall( 1234 )")]
    [InlineData("SimpleFunctionCall( true )")]
    [InlineData("SimpleFunctionCall( $(property) )")]
    [InlineData("SimpleFunctionCall( $(property), 1234, abcd, 'abcd efgh' )")]
    public void FunctionCallParseTest(string expression)
        => Should.NotThrow(() =>
            ParseExpression(expression));

    /// <summary>
    ///  Verifies that a parser can parse subsequent conditions after successful and failed parses.
    /// </summary>
    [Fact]
    public void ParserCanBeReused()
    {
        Parser parser = new();

        parser.Parse("$(Value)", ParserOptions.AllowAll, ElementLocation.Empty).ShouldBeOfType<StringExpressionNode>();
        parser.Parse("$(Value) == 'text'", ParserOptions.AllowAll, ElementLocation.Empty).ShouldBeOfType<EqualExpressionNode>();

        _ = Should.Throw<InvalidProjectFileException>(() => parser.Parse("==", ParserOptions.AllowAll, ElementLocation.Empty));

        parser.Parse("!true", ParserOptions.AllowAll, ElementLocation.Empty).ShouldBeOfType<NotExpressionNode>();
    }

    /// <summary>
    ///  Verifies that parser options reject item lists, including references inside quoted strings and function arguments.
    /// </summary>
    /// <param name="expression">The condition containing a disallowed item list.</param>
    [Theory]
    [InlineData("@(foo) == 'a.cs;b.cs'")]
    [InlineData("'a.cs;b.cs' == @(foo)")]
    [InlineData("'@(foo)' == 'a.cs;b.cs'")]
    [InlineData("'otherstuff@(foo)' == 'a.cs;b.cs'")]
    [InlineData("'@(foo)otherstuff' == 'a.cs;b.cs'")]
    [InlineData("somefunction(@(foo), 'otherstuff')")]
    public void ItemListParseTest(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ParseExpression(expression, ParserOptions.AllowProperties));

    /// <summary>
    ///  Verifies the expression-node types produced for item-function references and their boolean combinations.
    /// </summary>
    /// <param name="expression">The condition containing an item-function reference.</param>
    /// <param name="expectedType">The expected root expression-node type.</param>
    [Theory]
    [InlineData("@(item->foo('ab'))", typeof(StringExpressionNode))]
    [InlineData("!@(item->foo())", typeof(NotExpressionNode))]
    [InlineData("(@(item->foo('ab')) and @(item->foo('bc')))", typeof(AndExpressionNode))]
    public void ItemFuncParseTest(string expression, Type expectedType)
        => ParseExpression(expression, ParserOptions.AllowPropertiesAndItemLists).ShouldBeOfType(expectedType);

    /// <summary>
    ///  Verifies that parsing an item-function reference preserves its unexpanded text.
    /// </summary>
    [Fact]
    public void ItemFunctionTextIsPreserved()
    {
        const string expression = "@(item->foo('ab'))";

        StringExpressionNode tree = ParseExpression(expression, ParserOptions.AllowPropertiesAndItemLists)
            .ShouldBeOfType<StringExpressionNode>();

        tree.GetUnexpandedValue(null).ShouldBe(expression);
    }

    /// <summary>
    ///  Verifies that parser options reject metadata, including references inside quoted strings and function arguments.
    /// </summary>
    /// <param name="expression">The condition containing disallowed metadata.</param>
    [Theory]
    [InlineData("%(foo) == 'a.cs;b.cs'")]
    [InlineData("'a.cs;b.cs' == %(foo)")]
    [InlineData("'%(foo)' == 'a.cs;b.cs'")]
    [InlineData("'otherstuff%(foo)' == 'a.cs;b.cs'")]
    [InlineData("'%(foo)otherstuff' == 'a.cs;b.cs'")]
    [InlineData("somefunction(%(foo), 'otherstuff')")]
    public void MetadataParseTest(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ParseExpression(expression, ParserOptions.AllowPropertiesAndItemLists));

    /// <summary>
    ///  Verifies rejection of unterminated strings, adjacent operands, and incomplete or chained comparisons.
    /// </summary>
    /// <param name="expression">The malformed condition to parse.</param>
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
    public void NegativeTests(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ParseExpression(expression));

    /// <summary>
    ///  Verifies one-based error positions, including errors detected by the parser after scanning.
    /// </summary>
    /// <param name="expression">The malformed condition.</param>
    /// <param name="expectedPosition">The expected one-based error position.</param>
    [Theory]
    [InlineData("1==0xFG", 7)]
    [InlineData("1==-0xF", 6)]
    [InlineData("1234=5678", 6)]
    [InlineData(" ", 2)]
    [InlineData(" (", 3)]
    [InlineData(" false or  ", 12)]
    [InlineData(" \"foo", 2)]
    [InlineData(" @(foo", 2)]
    [InlineData(" @(", 2)]
    [InlineData(" $", 2)]
    [InlineData(" $(foo", 2)]
    [InlineData(" $(", 2)]
    [InlineData("'%24%28x' == '%24(x''", 21)] // Escaped characters still contribute to the position.
    public void ErrorPosition(string expression, int expectedPosition)
        => AssertParseErrorPosition(expression, expectedPosition, ParserOptions.AllowAll);

    /// <summary>
    ///  Verifies error positions for item lists that are disallowed by the parser options.
    /// </summary>
    /// <param name="expression">The condition containing a disallowed item list.</param>
    /// <param name="expectedPosition">The expected one-based error position.</param>
    [Theory]
    [InlineData(" @(foo)", 2)]
    [InlineData(" '@(foo)'", 3)]
    public void DisallowedItemListErrorPosition(string expression, int expectedPosition)
        => AssertParseErrorPosition(expression, expectedPosition, ParserOptions.AllowProperties);

    /// <summary>
    ///  Verifies that numeric conversion is not validated while parsing a numeric token.
    /// </summary>
    [Fact]
    public void NumericLiteralValidationIsDeferred()
        => Should.NotThrow(() =>
            ParseExpression("1==1.1."));

    /// <summary>
    ///  Verifies precedence warnings for mixed boolean operators without sufficient parentheses.
    /// </summary>
    /// <param name="condition">The ambiguous condition to evaluate during a build.</param>
    [Theory]
    [InlineData("$(a) == 1 and $(b) == 2 or $(c) == 3")]
    [InlineData("$(a) == 1 or $(b) == 2 and $(c) == 3")]
    [InlineData("($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4")]
    public void VerifyWarningForOrder(string condition)
        => BuildConditionProject(condition).FullLog.ShouldContain("MSB4130:");

    /// <summary>
    ///  Verifies that uniform operators and explicitly grouped mixed operators do not produce precedence warnings.
    /// </summary>
    /// <param name="condition">The unambiguous condition to evaluate during a build.</param>
    [Theory]
    [InlineData("$(a) == 1 and $(b) == 2 and $(c) == 3")]
    [InlineData("$(a) == 1 or $(b) == 2 or $(c) == 3")]
    [InlineData("($(a) == 1 and $(b) == 2) or $(c) == 3")]
    [InlineData("($(a) == 1 or $(b) == 2) and $(c) == 3")]
    public void VerifyNoWarningForOrder(string condition)
        => BuildConditionProject(condition).FullLog.ShouldNotContain("MSB4130:");

    private static GenericExpressionNode ParseExpression(string expression, ParserOptions options = ParserOptions.AllowAll)
    {
        Parser parser = new();
        return parser.Parse(expression, options, ElementLocation.Empty);
    }

    private static void AssertParseErrorPosition(string expression, int expectedPosition, ParserOptions options)
    {
        Parser parser = new();

        _ = Should.Throw<InvalidProjectFileException>(() => parser.Parse(expression, options, ElementLocation.Empty));
        parser.errorPosition.ShouldBe(expectedPosition);
    }

    private MockLogger BuildConditionProject(string condition)
    {
        string projectContent = $"""
            <Project ToolsVersion="msbuilddefaulttoolsversion">
                <Target Name="Build">
                    <Message Text="condition is true" Condition="{condition}"/>
                </Target>
            </Project>
            """.Cleanup();

        return ObjectModelHelpers.BuildProjectExpectSuccess(projectContent, Output);
    }
}
