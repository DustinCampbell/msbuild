// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ParserTest
{
    /// <summary>
    ///  Verifies that a simple property reference parses into a StringExpressionNode.
    /// </summary>
    [Fact]
    public void SimpleProperty_ShouldParseToStringNode()
        => Parse<StringExpressionNode>("$(foo)")
            .Verify("$(foo)");

    /// <summary>
    ///  Verifies that property equality comparisons parse with correct structure:
    ///  EqualExpressionNode with StringExpressionNode children.
    /// </summary>
    [Theory]
    [InlineData("$(foo)=='hello'", "$(foo)", "hello")]
    [InlineData("$(foo)==''", "$(foo)", "")]
    public void PropertyEquality_ShouldParseWithCorrectStructure(string expression, string leftValue, string rightValue)
        => Parse<EqualExpressionNode>(expression)
            .Verify(
                (StringExpressionNode left) => left.Verify(leftValue),
                (StringExpressionNode right) => right.Verify(rightValue));

    /// <summary>
    ///  Verifies that chained AND expressions parse correctly:
    ///  ((first AND second) AND third) - left-associative.
    /// </summary>
    [Fact]
    public void ChainedAnd_ShouldParseLeftAssociative()
        => Parse<AndExpressionNode>("$(debug) and $(buildlab) and $(full)")
            .Verify(
                (AndExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("$(debug)"),
                    (StringExpressionNode right) => right.Verify("$(buildlab)")),
                (StringExpressionNode right) => right.Verify("$(full)"));

    /// <summary>
    ///  Verifies that chained OR expressions parse correctly:
    ///  ((first OR second) OR third) - left-associative.
    /// </summary>
    [Fact]
    public void ChainedOr_ShouldParseLeftAssociative()
        => Parse<OrExpressionNode>("$(debug) or $(buildlab) or $(full)")
            .Verify(
                (OrExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("$(debug)"),
                    (StringExpressionNode right) => right.Verify("$(buildlab)")),
                (StringExpressionNode right) => right.Verify("$(full)"));

    /// <summary>
    ///  Verifies that mixed AND/OR respects precedence:
    ///  "a and b or c" parses as "(a and b) or c" because AND has higher precedence.
    /// </summary>
    [Fact]
    public void MixedAndOr_ShouldRespectPrecedence()
        => Parse<OrExpressionNode>("$(debug) and $(buildlab) or $(full)")
            .Verify(
                (AndExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("$(debug)"),
                    (StringExpressionNode right) => right.Verify("$(buildlab)")),
                (StringExpressionNode right) => right.Verify("$(full)"));

    /// <summary>
    ///  Verifies that "a or b and c" parses as "a or (b and c)" because AND has higher precedence.
    /// </summary>
    [Fact]
    public void MixedOrAnd_ShouldRespectPrecedence()
        => Parse<OrExpressionNode>("$(full) or $(debug) and $(buildlab)")
            .Verify(
                (StringExpressionNode left) => left.Verify("$(full)"),
                (AndExpressionNode right) => right.Verify(
                    (StringExpressionNode left) => left.Verify("$(debug)"),
                    (StringExpressionNode right) => right.Verify("$(buildlab)")));

    /// <summary>
    ///  Verifies that metadata references parse into StringExpressionNode.
    /// </summary>
    [Fact]
    public void SimpleMetadata_ShouldParseToStringNode()
        => Parse<StringExpressionNode>("%(culture)")
            .Verify("%(culture)");

    /// <summary>
    ///  Verifies that metadata equality comparisons parse with correct structure.
    /// </summary>
    [Fact]
    public void MetadataEquality_ShouldParseWithCorrectStructure()
        => Parse<EqualExpressionNode>("%(culture)=='french'")
            .Verify(
                (StringExpressionNode left) => left.Verify("%(culture)"),
                (StringExpressionNode right) => right.Verify("french"));

    /// <summary>
    ///  Verifies that strings containing metadata references parse correctly.
    /// </summary>
    [Fact]
    public void StringWithMetadata_ShouldParseWithCorrectStructure()
        => Parse<EqualExpressionNode>("'foo_%(culture)'=='foo_french'")
            .Verify(
                (StringExpressionNode left) => left.Verify("foo_%(culture)"),
                (StringExpressionNode right) => right.Verify("foo_french"));

    /// <summary>
    ///  Verifies that boolean literals parse into BooleanLiteralNode with correct values.
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    public void BooleanLiteral_ShouldParseToBooleanNode(string expression, bool expectedValue)
        => Parse<BooleanLiteralNode>(expression)
            .Verify(expectedValue);

    /// <summary>
    ///  Verifies that numeric literals parse into NumericExpressionNode.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("0x1234")]
    [InlineData("0X1234ABCD")]
    [InlineData("-5")]
    [InlineData("+10")]
    [InlineData("3.14")]
    [InlineData(".5")]
    [InlineData("5.")]
    public void NumericLiteral_ShouldParseToNumericNode(string expression)
        => Parse<NumericExpressionNode>(expression)
            .Verify(expression);

    /// <summary>
    ///  Verifies that numeric equality comparisons parse with correct structure.
    /// </summary>
    [Fact]
    public void NumericEquality_ShouldParseWithCorrectStructure()
        => Parse<EqualExpressionNode>("0.0 == 0")
            .Verify(
                (NumericExpressionNode left) => left.Verify("0.0"),
                (NumericExpressionNode right) => right.Verify("0"));

    /// <summary>
    ///  Verifies that parentheses can override operator precedence.
    ///  "(a or b) and c" should parse as AND with grouped OR on left.
    /// </summary>
    [Fact]
    public void GroupedExpression_ShouldOverridePrecedence()
        => Parse<AndExpressionNode>("($(foo) or $(bar)) and $(baz)")
            .Verify(
                (OrExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("$(foo)"),
                    (StringExpressionNode right) => right.Verify("$(bar)")),
                (StringExpressionNode right) => right.Verify("$(baz)"));

    /// <summary>
    ///  Verifies that relational operators combine correctly with logical AND.
    /// </summary>
    [Fact]
    public void RelationalOperatorsWithAnd_ShouldParseCorrectly()
        => Parse<AndExpressionNode>("$(foo) <= 5 and $(bar) >= 15")
            .Verify(
                (LessThanOrEqualExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("$(foo)"),
                    (NumericExpressionNode right) => right.Verify("5")),
                (GreaterThanOrEqualExpressionNode right) => right.Verify(
                    (StringExpressionNode left) => left.Verify("$(bar)"),
                    (NumericExpressionNode right) => right.Verify("15")));

    /// <summary>
    ///  Verifies that less-than operator parses correctly.
    /// </summary>
    [Fact]
    public void LessThan_ShouldParseCorrectly()
        => Parse<LessThanExpressionNode>("$(foo) < 5")
            .Verify(
                (StringExpressionNode left) => left.Verify("$(foo)"),
                (NumericExpressionNode right) => right.Verify("5"));

    /// <summary>
    ///  Verifies that greater-than operator parses correctly.
    /// </summary>
    [Fact]
    public void GreaterThan_ShouldParseCorrectly()
        => Parse<GreaterThanExpressionNode>("$(foo) > 5")
            .Verify(
                (StringExpressionNode left) => left.Verify("$(foo)"),
                (NumericExpressionNode right) => right.Verify("5"));

    /// <summary>
    ///  Verifies that deeply nested expressions with multiple operators parse correctly.
    ///  Structure: (($(foo) <= 5 AND $(bar) >= 15) AND $(baz) == simplestring) AND 'a more complex string' != $(quux)
    /// </summary>
    [Fact]
    public void DeeplyNestedExpression_ShouldParseCorrectly()
        => Parse<AndExpressionNode>("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == simplestring) and 'a more complex string' != $(quux)")
            .Verify(
                (AndExpressionNode left) => left.Verify(
                    (AndExpressionNode left) => left.Verify(
                        (LessThanOrEqualExpressionNode left) => left.Verify(
                            (StringExpressionNode left) => left.Verify("$(foo)"),
                            (NumericExpressionNode right) => right.Verify("5")),
                        (GreaterThanOrEqualExpressionNode right) => right.Verify(
                            (StringExpressionNode left) => left.Verify("$(bar)"),
                            (NumericExpressionNode right) => right.Verify("15"))),
                    (EqualExpressionNode right) => right.Verify(
                        (StringExpressionNode left) => left.Verify("$(baz)"),
                        (StringExpressionNode right) => right.Verify("simplestring"))),
                (NotEqualExpressionNode right) => right.Verify(
                    (StringExpressionNode left) => left.Verify("a more complex string"),
                    (StringExpressionNode right) => right.Verify("$(quux)")));

    /// <summary>
    ///  Verifies that NOT operator works correctly in complex expressions.
    ///  Structure: (($(foo) OR $(bar) == false) AND !($(baz) == simplestring))
    /// </summary>
    [Fact]
    public void NotOperatorInComplexExpression_ShouldParseCorrectly()
        => Parse<AndExpressionNode>("(($(foo) or $(bar) == false) and !($(baz) == simplestring))")
            .Verify(
                (OrExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("$(foo)"),
                    (EqualExpressionNode right) => right.Verify(
                        (StringExpressionNode left) => left.Verify("$(bar)"),
                        (BooleanLiteralNode right) => right.Verify(false))),
                (NotExpressionNode right) => right.Verify(
                    (EqualExpressionNode child) => child.Verify(
                        (StringExpressionNode left) => left.Verify("$(baz)"),
                        (StringExpressionNode right) => right.Verify("simplestring"))));

    /// <summary>
    ///  Verifies that function calls can be used in logical expressions.
    ///  Structure: (($(foo) OR Exists('c:\foo.txt')) AND !(($(baz) == simplestring)))
    /// </summary>
    [Fact]
    public void FunctionCallInLogicalExpression_ShouldParseCorrectly()
        => Parse<AndExpressionNode>(@"(($(foo) or Exists('c:\foo.txt')) and !(($(baz) == simplestring)))")
            .Verify(
                (OrExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("$(foo)"),
                    (FunctionCallExpressionNode right) => right.Verify(
                        name: "Exists",
                        (StringExpressionNode arg) => arg.Verify(@"c:\foo.txt"))),
                (NotExpressionNode right) => right.Verify(
                    (EqualExpressionNode child) => child.Verify(
                        (StringExpressionNode left) => left.Verify("$(baz)"),
                        (StringExpressionNode right) => right.Verify("simplestring"))));

    /// <summary>
    ///  Verifies that NOT operator with boolean literal parses correctly.
    ///  Structure: !(true)
    /// </summary>
    [Theory]
    [InlineData("!true", true)]
    [InlineData("!false", false)]
    [InlineData("!on", true)]
    [InlineData("!off", false)]
    [InlineData("!yes", true)]
    [InlineData("!no", false)]
    public void NotOperator_WithBooleanLiteral_ShouldParseCorrectly(string expression, bool negatedValue)
        => Parse<NotExpressionNode>(expression)
            .Verify((BooleanLiteralNode child) => child.Verify(negatedValue));

    /// <summary>
    ///  Verifies that NOT operator with grouped boolean literal parses correctly.
    ///  Structure: !(true) - parentheses don't change structure
    /// </summary>
    [Fact]
    public void NotOperator_WithGroupedBooleanLiteral_ShouldParseCorrectly()
        => Parse<NotExpressionNode>("!(true)")
            .Verify((BooleanLiteralNode child) => child.Verify(true));

    /// <summary>
    ///  Verifies that NOT operator with property comparison parses correctly.
    ///  Structure: !($(foo) <= 5)
    /// </summary>
    [Fact]
    public void NotOperator_WithPropertyComparison_ShouldParseCorrectly()
        => Parse<NotExpressionNode>("!($(foo) <= 5)")
            .Verify((LessThanOrEqualExpressionNode child) => child.Verify(
                (StringExpressionNode left) => left.Verify("$(foo)"),
                (NumericExpressionNode right) => right.Verify("5")));

    /// <summary>
    ///  Verifies that NOT operator with metadata comparison parses correctly.
    ///  Structure: !(%(foo) <= 5)
    /// </summary>
    [Fact]
    public void NotOperator_WithMetadataComparison_ShouldParseCorrectly()
        => Parse<NotExpressionNode>("!(%(foo) <= 5)")
            .Verify((LessThanOrEqualExpressionNode child) => child.Verify(
                (StringExpressionNode left) => left.Verify("%(foo)"),
                (NumericExpressionNode right) => right.Verify("5")));

    /// <summary>
    ///  Verifies that NOT operator with complex AND expression parses correctly.
    ///  Structure: !(($(foo) <= 5) AND ($(bar) >= 15))
    /// </summary>
    [Fact]
    public void NotOperator_WithComplexAndExpression_ShouldParseCorrectly()
        => Parse<NotExpressionNode>("!($(foo) <= 5 and $(bar) >= 15)")
            .Verify((AndExpressionNode child) => child.Verify(
                (LessThanOrEqualExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("$(foo)"),
                    (NumericExpressionNode right) => right.Verify("5")),
                (GreaterThanOrEqualExpressionNode right) => right.Verify(
                    (StringExpressionNode left) => left.Verify("$(bar)"),
                    (NumericExpressionNode right) => right.Verify("15"))));

    /// <summary>
    ///  Verifies that function calls with no arguments parse correctly.
    /// </summary>
    [Fact]
    public void FunctionCall_WithNoArguments_ShouldParseCorrectly()
        => Parse<FunctionCallExpressionNode>(
            "SimpleFunctionCall()",
            ParserOptions.AllowAll | ParserOptions.AllowUnknownFunctions)
            .Verify(name: "SimpleFunctionCall");

    /// <summary>
    ///  Verifies that function calls with single numeric argument parse correctly.
    /// </summary>
    [Fact]
    public void FunctionCall_WithNumericArgument_ShouldParseCorrectly()
        => Parse<FunctionCallExpressionNode>(
            "SimpleFunctionCall( 1234 )",
            ParserOptions.AllowAll | ParserOptions.AllowUnknownFunctions)
            .Verify(
                name: "SimpleFunctionCall",
                (NumericExpressionNode arg) => arg.Verify("1234"));

    /// <summary>
    ///  Verifies that function calls with single boolean argument parse correctly.
    /// </summary>
    [Fact]
    public void FunctionCall_WithBooleanArgument_ShouldParseCorrectly()
        => Parse<FunctionCallExpressionNode>(
            "SimpleFunctionCall( true )",
            ParserOptions.AllowAll | ParserOptions.AllowUnknownFunctions)
            .Verify(
                name: "SimpleFunctionCall",
                (BooleanLiteralNode arg) => arg.Verify(true));

    /// <summary>
    ///  Verifies that function calls with single property argument parse correctly.
    /// </summary>
    [Fact]
    public void FunctionCall_WithPropertyArgument_ShouldParseCorrectly()
        => Parse<FunctionCallExpressionNode>(
            "SimpleFunctionCall( $(property) )",
            ParserOptions.AllowAll | ParserOptions.AllowUnknownFunctions)
            .Verify(
                name: "SimpleFunctionCall",
                (StringExpressionNode arg) => arg.Verify("$(property)"));

    /// <summary>
    ///  Verifies that function calls with multiple arguments of different types parse correctly.
    /// </summary>
    [Fact]
    public void FunctionCall_WithMultipleArguments_ShouldParseCorrectly()
        => Parse<FunctionCallExpressionNode>(
            "SimpleFunctionCall( $(property), 1234, abcd, 'abcd efgh' )",
            ParserOptions.AllowAll | ParserOptions.AllowUnknownFunctions)
            .Verify(
                name: "SimpleFunctionCall",
                (StringExpressionNode arg1) => arg1.Verify("$(property)"),
                (NumericExpressionNode arg2) => arg2.Verify("1234"),
                (StringExpressionNode arg3) => arg3.Verify("abcd"),
                (StringExpressionNode arg4) => arg4.Verify("abcd efgh"));

    /// <summary>
    ///  Verifies that keywords (and, or, true, false) are recognized case-insensitively.
    /// </summary>
    [Theory]
    [InlineData("TRUE and FALSE")]
    [InlineData("True AND False")]
    [InlineData("$(x) OR $(y)")]
    public void Keywords_ShouldBeRecognized_CaseInsensitively(string expression)
        => Should.NotThrow(() => Parse(expression));

    /// <summary>
    ///  Verifies that item lists are rejected when ParserOptions.AllowItemLists is not set.
    ///  Item lists should fail in various contexts: bare, quoted, and in function arguments.
    /// </summary>
    [Theory]
    [InlineData("@(foo) == 'a.cs;b.cs'")]
    [InlineData("'a.cs;b.cs' == @(foo)")]
    [InlineData("'@(foo)' == 'a.cs;b.cs'")]
    [InlineData("'otherstuff@(foo)' == 'a.cs;b.cs'")]
    [InlineData("'@(foo)otherstuff' == 'a.cs;b.cs'")]
    [InlineData("somefunction(@(foo), 'otherstuff')")]
    public void ItemList_WhenNotAllowed_ShouldFail(string expression)
    {
        var error = FailParse(expression, ParserOptions.AllowProperties | ParserOptions.AllowUnknownFunctions);

        error.ResourceName.ShouldBe("ItemListNotAllowedInThisConditional");
    }

    /// <summary>
    ///  Verifies that item transformations parse into StringExpressionNode with preserved value.
    /// </summary>
    [Fact]
    public void ItemTransformation_ShouldParseToStringNode()
        => Parse<StringExpressionNode>(
            "@(item->foo('ab'))",
            ParserOptions.AllowPropertiesAndItemLists)
            .Verify("@(item->foo('ab'))");

    /// <summary>
    ///  Verifies that negated item transformations parse correctly.
    ///  Structure: !@(item->foo())
    /// </summary>
    [Fact]
    public void ItemTransformation_Negated_ShouldParseCorrectly()
        => Parse<NotExpressionNode>(
            "!@(item->foo())",
            ParserOptions.AllowPropertiesAndItemLists)
            .Verify((StringExpressionNode child) => child.Verify("@(item->foo())"));

    /// <summary>
    ///  Verifies that item transformations work correctly in logical AND expressions.
    ///  Structure: (@(item->foo('ab')) AND @(item->foo('bc')))
    /// </summary>
    [Fact]
    public void ItemTransformation_InAndExpression_ShouldParseCorrectly()
        => Parse<AndExpressionNode>(
            "(@(item->foo('ab')) and @(item->foo('bc')))",
            ParserOptions.AllowPropertiesAndItemLists)
            .Verify(
                (StringExpressionNode left) => left.Verify("@(item->foo('ab'))"),
                (StringExpressionNode right) => right.Verify("@(item->foo('bc'))"));

    /// <summary>
    ///  Verifies that bare metadata references are rejected in conditions.
    ///  Metadata can only be used within strings, not as standalone expressions.
    ///  This restriction applies in various contexts: bare, quoted, and in function arguments.
    /// </summary>
    [Theory]
    [InlineData("%(foo) == 'a.cs;b.cs'")]
    [InlineData("'a.cs;b.cs' == %(foo)")]
    [InlineData("'%(foo)' == 'a.cs;b.cs'")]
    [InlineData("'otherstuff%(foo)' == 'a.cs;b.cs'")]
    [InlineData("'%(foo)otherstuff' == 'a.cs;b.cs'")]
    [InlineData("somefunction(%(foo), 'otherstuff')")]
    public void BareMetadata_InConditions_ShouldFail(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            Parse(expression, ParserOptions.AllowPropertiesAndItemLists));

    /// <summary>
    ///  Verifies that malformed expressions throw InvalidProjectFileException.
    ///  Tests various error scenarios: unclosed strings, invalid operators, 
    ///  chained equality operators, unmatched parentheses, and adjacent expressions.
    /// </summary>
    [Theory]
    [InlineData("'a more complex' == 'asdf")] // Unclosed quote
    [InlineData("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == 'simple string) and 'a more complex string' != $(quux)")] // Unclosed quote in complex expression
    [InlineData("($(foo) == 'simple string') $(bar)")] // Adjacent expressions without operator
    [InlineData("=='x'")] // Operator without left operand
    [InlineData("==")] // Operator without operands
    [InlineData(">")] // Operator without operands
    [InlineData("true!=false==")] // Chained equality operators (incomplete)
    [InlineData("true!=false==true")] // Chained equality operators (complete)
    [InlineData("1==(2")] // Unmatched parenthesis
    public void MalformedExpression_ShouldThrowInvalidProjectFileException(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            Parse(expression));

    /// <summary>
    ///  Verifies that error positions are reported correctly for various malformed expressions,
    ///  ensuring the parser points to the exact character position where the error occurs.
    /// </summary>
    [Theory]
    [InlineData("1==0xFG", 7, ParserOptions.AllowAll)] // Position of G
    [InlineData("1==-0xF", 6, ParserOptions.AllowAll)] // Position of x
    [InlineData("1234=5678", 6, ParserOptions.AllowAll)] // Position of '5'
    [InlineData(" ", 2, ParserOptions.AllowAll)] // Position of End of Input
    [InlineData(" (", 3, ParserOptions.AllowAll)] // Position of End of Input
    [InlineData(" false or  ", 12, ParserOptions.AllowAll)] // Position of End of Input
    [InlineData(" \"foo", 2, ParserOptions.AllowAll)] // Position of open quote
    [InlineData(" @(foo", 2, ParserOptions.AllowAll)] // Position of @
    [InlineData(" @(", 2, ParserOptions.AllowAll)] // Position of @
    [InlineData(" $", 2, ParserOptions.AllowAll)] // Position of $
    [InlineData(" $(foo", 2, ParserOptions.AllowAll)] // Position of $
    [InlineData(" $(", 2, ParserOptions.AllowAll)] // Position of $
    [InlineData(" @(foo)", 2, ParserOptions.AllowProperties)] // Position of @
    [InlineData(" '@(foo)'", 3, ParserOptions.AllowProperties)] // Position of @
    [InlineData("'%24%28x' == '%24(x''", 21, ParserOptions.AllowAll)] // Position of extra quote (test escaped chars)
    internal void ErrorPosition_ShouldBeReportedCorrectly(string input, int expectedPosition, ParserOptions options)
    {
        var error = FailParse(input, options);

        error.Position.ShouldBe(expectedPosition);
    }

    /// <summary>
    ///  Verifies that property function calls with spaces in the middle (not at boundaries)
    ///  parse successfully. MSBuild allows spaces within property function syntax like
    ///  $(x.StartsWith( 'y' )) but not at the start/end like $( x) or $(x ).
    /// </summary>
    [Theory]
    [InlineData("$(x.StartsWith( 'y' ))")]
    [InlineData("$(x.StartsWith ('y'))")]
    [InlineData("$( x.StartsWith( $(SpacelessProperty) ) )")]
    [InlineData("$( x.StartsWith( $(_SpacelessProperty) ) )")]
    [InlineData("$(x.StartsWith('Foo', StringComparison.InvariantCultureIgnoreCase))")]
    public void Property_WithMiddleSpace_ShouldParseSuccessfully(string expression)
        => Parse<StringExpressionNode>(expression, ParserOptions.AllowProperties)
            .Verify(expression);

    /// <summary>
    ///  Verifies that properties with spaces at the beginning or end (boundary spaces) fail to parse,
    ///  as MSBuild does not allow leading or trailing whitespace in property names.
    /// </summary>
    [Theory]
    [InlineData("$(x )")]
    [InlineData("$( x)")]
    [InlineData("$([MSBuild]::DoSomething($(space ))")]
    [InlineData("$([MSBuild]::DoSomething($(_space ))")]
    public void Property_WithBoundarySpace_ShouldFail(string expression)
    {
        var error = FailParse(expression);

        error.ResourceName.ShouldBe("IllFormedPropertySpaceInCondition");
        error.FormatArgs.Last().ShouldBe(" ");
    }

    /// <summary>
    ///  Verifies that single "=" (instead of "==") throws InvalidProjectFileException.
    ///  Common mistake: using assignment syntax instead of equality comparison.
    /// </summary>
    [Theory]
    [InlineData("a=b")]
    [InlineData("1234=5678")]
    [InlineData("$(foo)=$(bar)")]
    public void SingleEquals_ShouldProduceSpecialError(string expression)
    {
        var error = FailParse(expression);

        error.ResourceName.ShouldBe("IllFormedEqualsInCondition");
    }

    /// <summary>
    ///  Verifies that ill-formed property references throw InvalidProjectFileException.
    ///  Properties must be in the form $(name) with both opening and closing parentheses.
    /// </summary>
    [Theory]
    [InlineData("$(", "IllFormedPropertyCloseParenthesisInCondition")]
    [InlineData("$x", "IllFormedPropertyOpenParenthesisInCondition")]
    [InlineData("$(foo", "IllFormedPropertyCloseParenthesisInCondition")]
    public void Property_IllFormed_ShouldFail(string expression, string errorResourceName)
    {
        var error = FailParse(expression);

        error.ResourceName.ShouldBe(errorResourceName);
    }

    /// <summary>
    ///  Verifies that ill-formed item list references throw InvalidProjectFileException.
    ///  Item lists must be in the form @(name) with proper parentheses and quoted separators/transforms.
    /// </summary>
    [Theory]
    [InlineData("@(", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@x", "IllFormedItemListOpenParenthesisInCondition")]
    [InlineData("@(x", "IllFormedItemListCloseParenthesisInCondition")]
    [InlineData("@(x->'%(y)", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x", "IllFormedItemListQuoteInCondition")]
    [InlineData("@(x->'%(y)', 'x'", "IllFormedItemListCloseParenthesisInCondition")]
    public void ItemList_IllFormed_ShouldFail(string expression, string errorResourceName)
    {
        var error = FailParse(expression);

        error.ResourceName.ShouldBe(errorResourceName);
    }

    /// <summary>
    ///  Verifies that unterminated quoted strings throw InvalidProjectFileException.
    ///  All quoted strings must have matching closing quotes.
    /// </summary>
    [Theory]
    [InlineData("'")] // Just opening quote
    [InlineData("'abc")] // Missing closing quote
    [InlineData("false or 'abc")] // Unterminated string in expression
    [InlineData("'$(DEBUG) == true")] // Unterminated string with property
    public void String_Unterminated_ShouldFail(string expression)
    {
        var error = FailParse(expression);

        error.ResourceName.ShouldBe("IllFormedQuotedStringInCondition");
    }

    /// <summary>
    ///  Verifies that MSB4130 warning is triggered for "a and b or c" expression.
    ///  This could be misread as "a and (b or c)" but actually evaluates as "(a and b) or c".
    /// </summary>
    [Fact]
    public void MixedAndOr_WithoutParentheses_ShouldWarn()
    {
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess("""
            <Project ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <Target Name="Build">
                    <Message Text="expression 1 is true " Condition="$(a) == 1 and $(b) == 2 or $(c) == 3"/>
                </Target>
            </Project>
            """);

        ml.FullLog.ShouldContain("MSB4130:");
    }

    /// <summary>
    ///  Verifies that MSB4130 warning is triggered for "a or b and c" expression.
    ///  This could be misread as "(a or b) and c" but actually evaluates as "a or (b and c)".
    /// </summary>
    [Fact]
    public void MixedOrAnd_WithoutParentheses_ShouldWarn()
    {
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess("""
            <Project ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <Target Name="Build">
                    <Message Text="expression 1 is true " Condition="$(a) == 1 or $(b) == 2 and $(c) == 3"/>
                </Target>
            </Project>
            """);

        ml.FullLog.ShouldContain("MSB4130:");
    }

    /// <summary>
    ///  Verifies that MSB4130 warning is triggered for nested mixed AND/OR expressions.
    ///  Even with some parentheses, the expression "(a or b and c) or d" still has ambiguity.
    /// </summary>
    [Fact]
    public void NestedMixedAndOr_WithPartialParentheses_ShouldWarn()
    {
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess("""
            <Project ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <Target Name="Build">
                    <Message Text="expression 1 is true " Condition="($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4"/>
                </Target>
            </Project>
            """);

        ml.FullLog.ShouldContain("MSB4130:");
    }

    /// <summary>
    ///  Verifies that MSB4130 warning is NOT triggered for "a and b and c" expression.
    ///  All same operator - no ambiguity.
    /// </summary>
    [Fact]
    public void ChainedAnd_WithoutParentheses_ShouldNotWarn()
    {
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess("""
            <Project ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <Target Name="Build">
                    <Message Text="expression 1 is true " Condition="$(a) == 1 and $(b) == 2 and $(c) == 3"/>
                </Target>
            </Project>
            """);

        ml.FullLog.ShouldNotContain("MSB4130:");
    }

    /// <summary>
    ///  Verifies that MSB4130 warning is NOT triggered for "a or b or c" expression.
    ///  All same operator - no ambiguity.
    /// </summary>
    [Fact]
    public void ChainedOr_WithoutParentheses_ShouldNotWarn()
    {
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess("""
            <Project ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <Target Name="Build">
                    <Message Text="expression 1 is true " Condition="$(a) == 1 or $(b) == 2 or $(c) == 3"/>
                </Target>
            </Project>
            """);

        ml.FullLog.ShouldNotContain("MSB4130:");
    }

    /// <summary>
    ///  Verifies that MSB4130 warning is NOT triggered when parentheses clarify precedence.
    ///  "(a and b) or c" - parentheses make intent clear.
    /// </summary>
    [Fact]
    public void MixedAndOr_WithParentheses_ShouldNotWarn()
    {
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess("""
            <Project ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <Target Name="Build">
                    <Message Text="expression 1 is true " Condition="($(a) == 1 and $(b) == 2) or $(c) == 3"/>
                </Target>
            </Project>
            """);

        ml.FullLog.ShouldNotContain("MSB4130:");
    }

    /// <summary>
    ///  Verifies that MSB4130 warning is NOT triggered when parentheses clarify precedence.
    ///  "(a or b) and c" - parentheses make intent clear.
    /// </summary>
    [Fact]
    public void MixedOrAnd_WithParentheses_ShouldNotWarn()
    {
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess("""
            <Project ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <Target Name="Build">
                    <Message Text="expression 1 is true " Condition="($(a) == 1 or $(b) == 2) and $(c) == 3"/>
                </Target>
            </Project>
            """);

        ml.FullLog.ShouldNotContain("MSB4130:");
    }

    private static GenericExpressionNode Parse(string expression, ParserOptions options = ParserOptions.AllowAll)
        => Parser.Parse(expression, options, MockElementLocation.Instance);

    private static T Parse<T>(string expression, ParserOptions options = ParserOptions.AllowAll)
        where T : GenericExpressionNode
        => Parser.Parse(expression, options, MockElementLocation.Instance).ShouldBeAssignableTo<T>().ShouldNotBeNull();

    private static Parser.Error FailParse(string expression, ParserOptions options = ParserOptions.AllowAll)
    {
        Parser.TryParse(expression, options, MockElementLocation.Instance, out _, out Parser.Error? error).ShouldBeFalse();

        return error.ShouldNotBeNull();
    }
}
