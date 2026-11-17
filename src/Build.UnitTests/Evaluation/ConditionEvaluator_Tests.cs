// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;
using static Microsoft.Build.UnitTests.Evaluation.EvaluationTestHelpers;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Tests condition expression parsing and evaluation.
/// </summary>
/// <param name="output">The output helper for the test.</param>
public sealed class ConditionEvaluatorTests(ITestOutputHelper output) : TestBase(output)
{
    private static readonly ImmutableArray<(string Text, bool Value)> BooleanLiterals =
    [
        ("true", true),
        ("on", true),
        ("yes", true),
        ("false", false),
        ("off", false),
        ("no", false),
    ];

    public static readonly TheoryData<string, bool> BooleanLiteralTests = CreateBooleanLiteralTests();

    public static readonly TheoryData<string, bool, string, bool> BooleanLiteralPairs = CreateBooleanLiteralPairs();

    /// <summary>
    ///  Verifies lowercase and uppercase spellings of each boolean literal.
    /// </summary>
    /// <param name="expression">The boolean expression to evaluate.</param>
    /// <param name="expected">The expected boolean value.</param>
    [Theory]
    [MemberData(nameof(BooleanLiteralTests))]
    public void EvaluateBooleanLiteral(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies quoted boolean literals.
    /// </summary>
    /// <param name="expression">The quoted literal to evaluate.</param>
    /// <param name="expected">The expected boolean value.</param>
    [Theory]
    [InlineData("'true'", true)]
    [InlineData("'false'", false)]
    public void EvaluateQuotedBooleanLiteral(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies parenthesized boolean literals with and without quotes.
    /// </summary>
    /// <param name="expression">The parenthesized literal to evaluate.</param>
    /// <param name="expected">The expected boolean value.</param>
    [Theory]
    [InlineData("(true)", true)]
    [InlineData("(false)", false)]
    [InlineData("('true')", true)]
    [InlineData("('false')", false)]
    public void EvaluateParenthesizedBooleanLiteral(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies boolean negation with quoting, parentheses, and double negation.
    /// </summary>
    /// <param name="expression">The negated expression to evaluate.</param>
    /// <param name="expected">The expected boolean value.</param>
    [Theory]
    [InlineData("!true", false)]
    [InlineData("!false", true)]
    [InlineData("!'true'", false)]
    [InlineData("!'false'", true)]
    [InlineData("!(true)", false)]
    [InlineData("!(false)", true)]
    [InlineData("!('true')", false)]
    [InlineData("!('false')", true)]
    [InlineData("!!true", true)]
    [InlineData("!!false", false)]
    [InlineData("!!'true'", true)]
    [InlineData("!!'false'", false)]
    public void EvaluateBooleanNegation(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies conjunction with parenthesized and negated operands, and negation of the result.
    /// </summary>
    /// <param name="left">The left boolean literal.</param>
    /// <param name="leftValue">The left literal's boolean value.</param>
    /// <param name="right">The right boolean literal.</param>
    /// <param name="rightValue">The right literal's boolean value.</param>
    [Theory]
    [MemberData(nameof(BooleanLiteralPairs))]
    public void EvaluateBooleanConjunction(string left, bool leftValue, string right, bool rightValue)
    {
        EvaluateExpression($"{left} and {right}").ShouldBe(leftValue && rightValue);
        EvaluateExpression($"({left}) and ({right})").ShouldBe(leftValue && rightValue);
        EvaluateExpression($"!{left} and {right}").ShouldBe(!leftValue && rightValue);
        EvaluateExpression($"{left} and !{right}").ShouldBe(leftValue && !rightValue);
        EvaluateExpression($"!({left}) and !({right})").ShouldBe(!leftValue && !rightValue);
        EvaluateExpression($"!({left} and {right})").ShouldBe(!(leftValue && rightValue));
    }

    /// <summary>
    ///  Verifies disjunction with parenthesized and negated operands, and negation of the result.
    /// </summary>
    /// <param name="left">The left boolean literal.</param>
    /// <param name="leftValue">The left literal's boolean value.</param>
    /// <param name="right">The right boolean literal.</param>
    /// <param name="rightValue">The right literal's boolean value.</param>
    [Theory]
    [MemberData(nameof(BooleanLiteralPairs))]
    public void EvaluateBooleanDisjunction(string left, bool leftValue, string right, bool rightValue)
    {
        EvaluateExpression($"{left} or {right}").ShouldBe(leftValue || rightValue);
        EvaluateExpression($"({left}) or ({right})").ShouldBe(leftValue || rightValue);
        EvaluateExpression($"!{left} or {right}").ShouldBe(!leftValue || rightValue);
        EvaluateExpression($"{left} or !{right}").ShouldBe(leftValue || !rightValue);
        EvaluateExpression($"!({left}) or !({right})").ShouldBe(!leftValue || !rightValue);
        EvaluateExpression($"!({left} or {right})").ShouldBe(!(leftValue || rightValue));
    }

    /// <summary>
    ///  Verifies equality with parenthesized and negated operands, and negation of the result.
    /// </summary>
    /// <param name="left">The left boolean literal.</param>
    /// <param name="leftValue">The left literal's boolean value.</param>
    /// <param name="right">The right boolean literal.</param>
    /// <param name="rightValue">The right literal's boolean value.</param>
    [Theory]
    [MemberData(nameof(BooleanLiteralPairs))]
    public void EvaluateBooleanEquality(string left, bool leftValue, string right, bool rightValue)
    {
        EvaluateExpression($"{left} == {right}").ShouldBe(leftValue == rightValue);
        EvaluateExpression($"({left}) == ({right})").ShouldBe(leftValue == rightValue);
        EvaluateExpression($"!{left} == {right}").ShouldBe(!leftValue == rightValue);
        EvaluateExpression($"(!({left})) == {right}").ShouldBe(!leftValue == rightValue);
        EvaluateExpression($"{left} == !{right}").ShouldBe(leftValue == !rightValue);
        EvaluateExpression($"!{left} == !{right}").ShouldBe(!leftValue == !rightValue);
        EvaluateExpression($"!({left} == {right})").ShouldBe(!(leftValue == rightValue));
    }

    /// <summary>
    ///  Verifies inequality with parenthesized and negated operands, and negation of the result.
    /// </summary>
    /// <param name="left">The left boolean literal.</param>
    /// <param name="leftValue">The left literal's boolean value.</param>
    /// <param name="right">The right boolean literal.</param>
    /// <param name="rightValue">The right literal's boolean value.</param>
    [Theory]
    [MemberData(nameof(BooleanLiteralPairs))]
    public void EvaluateBooleanInequality(string left, bool leftValue, string right, bool rightValue)
    {
        EvaluateExpression($"{left} != {right}").ShouldBe(leftValue != rightValue);
        EvaluateExpression($"({left}) != ({right})").ShouldBe(leftValue != rightValue);
        EvaluateExpression($"!{left} != {right}").ShouldBe(!leftValue != rightValue);
        EvaluateExpression($"(!({left})) != {right}").ShouldBe(!leftValue != rightValue);
        EvaluateExpression($"{left} != !{right}").ShouldBe(leftValue != !rightValue);
        EvaluateExpression($"!{left} != !{right}").ShouldBe(!leftValue != !rightValue);
        EvaluateExpression($"!({left} != {right})").ShouldBe(!(leftValue != rightValue));
    }

    /// <summary>
    ///  Verifies that boolean operator names are case-insensitive.
    /// </summary>
    /// <param name="operatorName">The operator spelling to evaluate.</param>
    /// <param name="expected">The result when the operands have opposite boolean values.</param>
    [Theory]
    [InlineData("and", false)]
    [InlineData("AND", false)]
    [InlineData("aNd", false)]
    [InlineData("or", true)]
    [InlineData("OR", true)]
    [InlineData("oR", true)]
    public void BooleanOperatorNamesAreCaseInsensitive(string operatorName, bool expected)
    {
        EvaluateExpression($"true {operatorName} false").ShouldBe(expected);
        EvaluateExpression($"false {operatorName} true").ShouldBe(expected);
    }

    /// <summary>
    ///  Verifies boolean operators with varied whitespace and adjacent parenthesized operands.
    /// </summary>
    /// <param name="expression">The condition to evaluate.</param>
    /// <param name="expected">The expected boolean value.</param>
    [Theory]
    [InlineData("(true)and(false)", false)]
    [InlineData("(false)or(true)", true)]
    [InlineData("!(false)and!(false)", true)]
    [InlineData("!(true)or!(false)", true)]
    [InlineData("(1==1)and('a'=='a')", true)]
    [InlineData("true  and  false", false)]
    [InlineData("false  or  true", true)]
    [InlineData("true\tand\tfalse", false)]
    [InlineData("false\tor\ttrue", true)]
    [InlineData("true\r\nand\r\nfalse", false)]
    [InlineData("false\r\nor\r\ntrue", true)]
    public void BooleanOperatorsAllowVariedWhitespace(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies boolean operators with the left operand, right operand, or both operands quoted.
    /// </summary>
    /// <param name="expression">The condition containing quoted boolean operands.</param>
    /// <param name="expected">The expected boolean value.</param>
    [Theory]
    [InlineData("'true' and false", false)]
    [InlineData("true and 'false'", false)]
    [InlineData("'true' and 'false'", false)]
    [InlineData("'false' or true", true)]
    [InlineData("false or 'true'", true)]
    [InlineData("'false' or 'true'", true)]
    [InlineData("'true' == true", true)]
    [InlineData("true == 'true'", true)]
    [InlineData("'true' == 'false'", false)]
    [InlineData("'true' != true", false)]
    [InlineData("true != 'false'", true)]
    [InlineData("'true' != 'false'", true)]
    public void BooleanOperatorsAllowQuotedOperands(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies that boolean operators evaluate the right operand only when the left operand requires it.
    /// </summary>
    /// <param name="expression">The condition containing the observed property reference.</param>
    /// <param name="rightValue">The boolean value returned by the property provider.</param>
    /// <param name="expected">The expected condition result.</param>
    /// <param name="expectedReads">The expected number of right-operand property reads.</param>
    [Theory]
    [InlineData("true or ($(Right))", true, true, 0)]
    [InlineData("true or ($(Right))", false, true, 0)]
    [InlineData("false and $(Right)", true, false, 0)]
    [InlineData("false and $(Right)", false, false, 0)]
    [InlineData("false or ($(Right))", true, true, 1)]
    [InlineData("false or ($(Right))", false, false, 1)]
    [InlineData("true and $(Right)", true, true, 1)]
    [InlineData("true and $(Right)", false, false, 1)]
    public void BooleanOperatorsShortCircuit(string expression, bool rightValue, bool expected, int expectedReads)
    {
        TrackingPropertyProvider properties = new(rightValue);

        EvaluateExpression(expression, properties: properties).ShouldBe(expected);
        properties.ReadCount.ShouldBe(expectedReads);
    }

    /// <summary>
    ///  Verifies that a non-boolean operand is not evaluated when an earlier operand determines the result.
    /// </summary>
    /// <param name="expression">The condition containing an unreachable non-boolean operand.</param>
    [Theory]
    [InlineData("true or 1")]
    [InlineData("false or true or !true or'1'")]
    public void ShortCircuitSkipsNonBooleanOperands(string expression)
        => EvaluateExpression(expression).ShouldBeTrue();

    /// <summary>
    ///  Verifies precedence, associativity, and nested condition evaluation.
    /// </summary>
    /// <param name="expression">The compound condition to evaluate.</param>
    /// <param name="expected">The expected condition result.</param>
    [Theory]
    [InlineData("(true and false) or true", true)]
    [InlineData("true or false and false", true)]
    [InlineData("false and true or true", true)]
    [InlineData("(true or false) and false", false)]
    [InlineData("false or true or false", true)]
    [InlineData("true or true or false", true)]
    [InlineData("false or false or false", false)]
    [InlineData("false and !((true and false))", false)]
    [InlineData("false or (false or (false or (false or (false or (true)))))", true)]
    [InlineData("false or (false or (false or (false or (false or (false)))))", false)]
    [InlineData("44==+44.0 and -44==-44.0", true)]
    public void EvaluateCompoundConditions(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies all six numeric comparison operators for equal and differently ordered values.
    /// </summary>
    /// <param name="left">The left numeric literal.</param>
    /// <param name="right">The right numeric literal.</param>
    /// <param name="expectedComparison">-1 if the left value is less than the right, 0 if equal, or 1 if greater.</param>
    [Theory]
    [InlineData("1", "2", -1)]
    [InlineData("2", "2", 0)]
    [InlineData("3", "2", 1)]
    [InlineData("-1", "-1", 0)]
    [InlineData("-1", "0", -1)]
    [InlineData("0", "0.1", -1)]
    [InlineData("+4", "-4", 1)]
    [InlineData("123", "0123.01", -1)]
    public void EvaluateNumericComparisons(string left, string right, int expectedComparison)
        => AssertComparisons(left, right, expectedComparison);

    /// <summary>
    ///  Verifies equivalent decimal, hexadecimal, signed, and zero-padded numeric spellings.
    /// </summary>
    /// <param name="left">The left numeric representation.</param>
    /// <param name="right">The equivalent numeric representation.</param>
    [Theory]
    [InlineData("0x1", "1.0")]
    [InlineData("0xa", "10")]
    [InlineData("123", "0123")]
    [InlineData("123", "0123.0")]
    [InlineData("00", "0")]
    [InlineData("0", "0.0")]
    [InlineData("+4", "4")]
    [InlineData("0x11", "17")]
    [InlineData("0x01a", "26")]
    [InlineData("0xa", "0x0A")]
    public void EvaluateEquivalentNumericLiterals(string left, string right)
        => AssertComparisons(left, right, expectedComparison: 0);

    /// <summary>
    ///  Verifies numeric comparisons when either or both operands are quoted.
    /// </summary>
    /// <param name="expression">The comparison containing quoted numeric literals.</param>
    /// <param name="expected">The expected comparison result.</param>
    [Theory]
    [InlineData("'1'=='1'", true)]
    [InlineData("'1'==1", true)]
    [InlineData("123=='0123.0'", true)]
    [InlineData("'2'>1", true)]
    [InlineData("2<'1'", false)]
    public void EvaluateQuotedNumericComparisons(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies numeric comparisons with whitespace around the operator.
    /// </summary>
    /// <param name="expression">The comparison containing whitespace.</param>
    [Theory]
    [InlineData("1\n==1")]
    [InlineData("1\t==\t\r\n1")]
    [InlineData("1\n\t==1")]
    [InlineData("-1  <  0")]
    public void NumericComparisonsAllowWhitespace(string expression)
        => EvaluateExpression(expression).ShouldBeTrue();

    /// <summary>
    ///  Verifies comparisons of version literals, including different component counts.
    /// </summary>
    /// <param name="left">The left version literal.</param>
    /// <param name="right">The right version literal.</param>
    /// <param name="expectedComparison">-1 if the left value is less than the right, 0 if equal, or 1 if greater.</param>
    [Theory]
    [InlineData("1.2.3", "1.2.3.0", -1)]
    [InlineData("12.23.34", "12.23.34", 0)]
    [InlineData("0.8.0.0", "8.0.0", -1)]
    [InlineData("1.1.2", "1.0.1.2", 1)]
    [InlineData("1.3.5.8", "1.3.6.8", -1)]
    [InlineData("8.0.0.0", "8.0.0", 1)]
    public void EvaluateVersionComparisons(string left, string right, int expectedComparison)
        => AssertComparisons(left, right, expectedComparison);

    /// <summary>
    ///  Verifies quoted version operands use version ordering while equality preserves their spelling.
    /// </summary>
    /// <param name="expression">The comparison containing quoted version literals.</param>
    /// <param name="expected">The expected comparison result.</param>
    [Theory]
    [InlineData("'1.2.3' < 1.2.10", true)]
    [InlineData("1.2.3 < '1.2.10'", true)]
    [InlineData("'1.2.3' < '1.2.10'", true)]
    [InlineData("'1.2.10' > '1.2.3'", true)]
    [InlineData("'1.2.3' <= '1.2.3'", true)]
    [InlineData("'1.2.3' >= '1.2.10'", false)]
    [InlineData("'1.2.3' == '01.2.3'", false)]
    [InlineData("'1.2.3' != '01.2.3'", true)]
    public void EvaluateQuotedVersionComparisons(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies ordering and equality where decimal, integer, and version interpretations meet.
    /// </summary>
    /// <param name="left">The left literal.</param>
    /// <param name="right">The right literal.</param>
    /// <param name="expectedComparison">-1 if the left value is less than the right, 0 if equal, or 1 if greater.</param>
    [Theory]
    [InlineData("1.2.0", "1.2", 1)]
    [InlineData("8.1", "8.0.16.23", 1)]
    [InlineData("8.0.0", "8", 1)]
    [InlineData("8.0.0", "8.0", 1)]
    [InlineData("6", "6.0.0.1", -1)]
    [InlineData("7", "6.8.2", 1)]
    [InlineData("4", "5.9.9135.4", -1)]
    [InlineData("3", "3.0.0", -1)]
    [InlineData("8.1.2", "8", 1)]
    public void EvaluateNumericAndVersionComparisons(string left, string right, int expectedComparison)
        => AssertComparisons(left, right, expectedComparison);

    /// <summary>
    ///  Verifies that version equality preserves string spelling rather than normalizing version components.
    /// </summary>
    [Fact]
    public void VersionEqualityUsesStringComparison()
    {
        EvaluateExpression("1.2.3<=01.2.3").ShouldBeTrue();
        EvaluateExpression("1.2.3>=01.2.3").ShouldBeTrue();
        EvaluateExpression("1.2.3==01.2.3").ShouldBeFalse();
        EvaluateExpression("1.2.3!=01.2.3").ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies string equality for literals that exceed supported numeric or version formats.
    /// </summary>
    [Fact]
    public void UnsupportedNumericAndVersionLiteralsCompareAsStrings()
    {
        string oversizedNumber = $"1{new string('0', 500)}";

        EvaluateExpression($"{oversizedNumber}=={oversizedNumber}").ShouldBeTrue();
        EvaluateExpression($"'{oversizedNumber}'=='{oversizedNumber}'").ShouldBeTrue();
        EvaluateExpression($"{oversizedNumber}==2").ShouldBeFalse();
        EvaluateExpression($"'{oversizedNumber}'=='2'").ShouldBeFalse();
        EvaluateExpression($"'{oversizedNumber}'=='0{oversizedNumber}'").ShouldBeFalse();
        EvaluateExpression("1.2.3.4.5.6.7==1.2.3.4.5.6.7").ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies case-insensitive string equality, escaping, and comparisons between unlike literal types.
    /// </summary>
    /// <param name="expression">The string comparison to evaluate.</param>
    /// <param name="expected">The expected comparison result.</param>
    [Theory]
    [InlineData("a_a==a_a", true)]
    [InlineData("a_a=='a_a'", true)]
    [InlineData("_a== _a", true)]
    [InlineData("'and'=='AND'", true)]
    [InlineData("'%77'=='w'", true)]
    [InlineData("'%zz'=='%zz'", true)]
    [InlineData("1==a", false)]
    [InlineData("true==1", false)]
    [InlineData("false==0", false)]
    [InlineData("false!=1", true)]
    [InlineData("(!(true))=='x'", false)]
    [InlineData("oops==false", false)]
    [InlineData("oops==!false", false)]
    [InlineData("'59264.59264'=='59264.59264'", true)]
    [InlineData("'59264.59264'=='59264.59265'", false)]
    public void EvaluateStringComparisons(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    /// <summary>
    ///  Verifies trailing-slash checks for both separators, an empty argument, and a missing separator.
    /// </summary>
    /// <param name="expression">The trailing-slash condition to evaluate.</param>
    /// <param name="expected">The expected condition result.</param>
    [Theory]
    [InlineData(@"HASTRAILINGSLASH('foo\')", true)]
    [InlineData("!HasTrailingSlash('foo')", true)]
    [InlineData("HasTrailingSlash('foo/')", true)]
    [InlineData("hastrailingslash('foo')", false)]
    [InlineData("hastrailingslash('')", false)]
    public void EvaluateHasTrailingSlashExpressions(string expression, bool expected)
        => EvaluateExpression(expression).ShouldBe(expected);

    public static readonly TheoryData<string> ParseErrorTests =
    [
        // Whitespace alone is not a condition.
        "\n",
        "\t",

        // Invalid characters and malformed numeric literals.
        "%00",
        "text#==text#",
        "#!=#",
        "!@#$%^&*",
        "+-1==1",
        "1==-+1",
        "1==+0xa",
        "--1==1",
        "1.2.3==1,2,3",

        // Split, incomplete, or chained comparison operators.
        "1=1=1",
        "'text'=='text'=='text'",
        "1> =0",
        "1= =1",
        "=",
        "true!=false==",
        "true!=false==true",
        "1<=1<=1",
        "$(Property)==1>1==2",

        // Missing operands or operators.
        "or true",
        "1 and",
        "and",
        "or",
        "not true",
        "!",
        "or=or",
        "1==",
        "!and",
        "=='text'",
        "==",
        ">",
        "true false",
        "true $(Property) true",

        // Unterminated quotes and unbalanced parentheses.
        "()",
        "'true",
        "'false''",
        "'text'=='text",
        "('text'=='text'",
        "('text'=='text'))",
        "'text'=='text')",
        "1==(2",

        // Incomplete property references and adjacent unquoted operands.
        "$",
        "$(",
        "$(Property",
        "$(Property)$(Property)=='texttext'",
        "$(Property)==and",
        "-($(Property))==-1",
        "$(Property)@(Item)>1",

        // Incomplete item references and transforms, or adjacent item operands.
        "@",
        "@(",
        "@(Item -> '",
        "@(Item -> ''",
        "@(Item-",
        "@(Item -> 'text', '",
        "@(Item -> 'text', ''",
        "@(Item)@(Item)!=1",
        "@(Item) @(Item)!=1",

        // Incomplete metadata references.
        "%",
        "%(",

        // Unterminated function calls, adjacent arguments, and invalid argument characters.
        "exists(",
        "exists(@(Item)text)",
        "exists(@(Item)$(Property))",
        "exists(|||||)",
    ];

    /// <summary>
    ///  Verifies that malformed conditions fail during parsing without any evaluation setup.
    /// </summary>
    /// <param name="expression">The malformed condition to parse.</param>
    [Theory]
    [MemberData(nameof(ParseErrorTests))]
    public void MalformedConditionsFailToParse(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ParseExpression(expression));

    public static readonly TheoryData<string> EvaluationErrorTests =
    [
        // Numeric comparison operators cannot convert these string operands.
        "-1 > text",
        "1 > 'text'",
        "text1 <= 1",
        "1 <= text",
        "1 > text",
        "text < text",
        "text > text",
        "text >= text",
        "text <= text",
        "text > 1",
        "text >= 1",
        "1 >= text",
        "'text' > 'text'",

        // Numeric literals are not boolean values, including under negation.
        "0",
        "1",
        "!0",
        "!1",
        "1 or true",
        "false or 1",
        "1 and true",
        "true and 1",
        "false or !1",

        // These strings are not boolean values; function names alone are just strings.
        "not",
        "(text)",
        "!'text'",
        "false or 'text'",
        "exists",
        "existsX",

        // Comparing a string with a nested boolean comparison requires an unsupported string conversion.
        "'text' == ('text' == 'text')",
        "'text' != ('text' == 'text')",
        "('text' == 'text') != text",
        "('text' == 'text') == text",

        // Function names and argument counts are validated during evaluation.
        "nonexistentfunction('text')",
        "HasTrailingSlash(text, 'other')",
        "HasTrailingSlash(,,)",
    ];

    /// <summary>
    ///  Verifies evaluation errors in conditions that parse successfully without expansion.
    /// </summary>
    /// <param name="expression">The syntactically valid condition whose evaluation must fail.</param>
    [Theory]
    [MemberData(nameof(EvaluationErrorTests))]
    public void ParsedConditionsFailToEvaluate(string expression)
        => AssertEvaluationError(expression);

    /// <summary>
    ///  Verifies true conditions using a single property with an explicit value.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> property.</param>
    /// <param name="value">The property value to expand.</param>
    [Theory]
    [InlineData("($(Value) or true)", "no")]
    [InlineData("('$(Value)'==1 and (!false))", "1")]
    [InlineData("$(Value)=='and'", "and")]
    [InlineData("'-$(Value)'==-1", "1")]
    [InlineData("$(Value)==faLse", "no")]
    [InlineData("$(Value)==oFF", "no")]
    [InlineData("$(Value)==no", "no")]
    [InlineData("$(Value)!=true", "no")]
    [InlineData("$(Value)== True", "true")]
    [InlineData("$(Value)==on", "true")]
    [InlineData("$(Value)==yes", "true")]
    [InlineData("$(Value)!=1", "true")]
    [InlineData("$(Value)==1", "1")]
    [InlineData("$(Value)=='xxx'", "xxx")]
    [InlineData("$(Value)==$(Value)", "no")]
    [InlineData("$(Value)>0", "1")]
    [InlineData("!$(Value)", "no")]
    [InlineData("$(Value)", "true")]
    [InlineData("'$(Value)'==(false)", "no")]
    [InlineData("('$(Value)'==(false))", "no")]
    [InlineData("(true) and ($(Value)==off)", "no")]
    [InlineData("(true) and ($(Value)==xxx)", "xxx")]
    [InlineData("(false)     or($(Value)==xxx)", "xxx")]
    [InlineData("$(Value)=='XxX'", "xxx")]
    [InlineData("$(Value) or true", "no")]
    [InlineData("HasTrailingSlash($(Value))", @"foo\")]
    public void EvaluateTruePropertyExpressions(string expression, string value)
        => EvaluateExpression(expression, Properties("value", value))
            .ShouldBeTrue();

    /// <summary>
    ///  Verifies false conditions using a single property with an explicit value.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> property.</param>
    /// <param name="value">The property value to expand.</param>
    [Theory]
    [InlineData("$(Value)!=no", "no")]
    [InlineData("$(Value)==1.1", "true")]
    [InlineData("!$(Value)", "true")]
    [InlineData("!$(Value)and true", "true")]
    [InlineData("$(Value) and true", "no")]
    [InlineData("$(Value)!=$(Value)", "1.9.5")]
    public void EvaluateFalsePropertyExpressions(string expression, string value)
        => EvaluateExpression(expression, Properties("value", value))
            .ShouldBeFalse();

    /// <summary>
    ///  Verifies true conditions combining two independently expanded properties.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Left</c> and <c>Right</c> properties.</param>
    /// <param name="left">The left property value.</param>
    /// <param name="right">The right property value.</param>
    [Theory]
    [InlineData("$(Left)==$(Right)", "xxx", "xxx")]
    [InlineData("$(Left)=='$(Right)'", "xxx", "xxx")]
    [InlineData("($(Left)==$(Right))", "xxx", "xxx")]
    [InlineData("$(Left) or $(Right)", "no", "true")]
    public void EvaluateTrueTwoPropertyExpressions(string expression, string left, string right)
        => EvaluateExpression(expression, Properties(("Left", left), ("Right", right)))
            .ShouldBeTrue();

    /// <summary>
    ///  Verifies false conditions combining two independently expanded properties.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Left</c> and <c>Right</c> properties.</param>
    /// <param name="left">The left property value.</param>
    /// <param name="right">The right property value.</param>
    [Theory]
    [InlineData("$(Left)==$(Right)", "1", "no")]
    [InlineData("$(Left)!=$(Right)", "xxx", "xxx")]
    [InlineData("!($(Left)==$(Right))", "xxx", "xxx")]
    public void EvaluateFalseTwoPropertyExpressions(string expression, string left, string right)
        => EvaluateExpression(expression, Properties(("Left", left), ("Right", right)))
            .ShouldBeFalse();

    /// <summary>
    ///  Verifies that an undefined property expands to an empty argument for <c>HasTrailingSlash</c>.
    /// </summary>
    [Fact]
    public void HasTrailingSlashWithUndefinedPropertyReturnsFalse()
        => EvaluateExpression("HasTrailingSlash($(nonexistent))", NoProperties)
            .ShouldBeFalse();

    /// <summary>
    ///  Verifies evaluation errors using one explicitly defined property or an empty property provider.
    /// </summary>
    /// <param name="expression">The condition whose property expansion or evaluation must fail.</param>
    /// <param name="value">The <c>Value</c> property value, or <see langword="null"/> to define no properties.</param>
    [Theory]
    [InlineData("$()", null)]
    [InlineData("!$(Value)", "1")]
    [InlineData("!'$(Value)'", "text")]
    [InlineData("1>$(Value)", "text")]
    [InlineData("$(Value)>=1", "text")]
    [InlineData("1>=$(Value)", "true")]
    [InlineData("$(Value)>0", "no")]
    [InlineData("!$(Value)", "text")]
    [InlineData("a==b or $(Value)", "text")]
    [InlineData("false or $()", null)]
    [InlineData("$(Value) or true", "text")]
    [InlineData("$(Value) and true", null)]
    [InlineData("$(Value)", null)]
    public void EvaluateInvalidPropertyExpressions(string expression, string? value)
        => AssertEvaluationError(expression, value is null ? NoProperties : Properties("Value", value));

    /// <summary>
    ///  Verifies true conditions using a single item with an explicit include.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> item.</param>
    /// <param name="include">The item include to expand.</param>
    [Theory]
    [InlineData("@(Value)", "true")]
    [InlineData("@(Value)>0", "1")]
    [InlineData("1<=@(Value)", "1")]
    public void EvaluateTrueItemExpressions(string expression, string include)
        => EvaluateExpression(expression, items: Items("Value", include))
            .ShouldBeTrue();

    /// <summary>
    ///  Verifies item transforms using built-in filename and defining-project metadata.
    /// </summary>
    /// <param name="expression">The condition containing an item transform.</param>
    /// <param name="include">The item include to transform.</param>
    [Theory]
    [InlineData("@(Value -> '%(filename)')=='file'", "file")]
    [InlineData("@(Value -> '%(definingprojectname).barproj')=='foo.barproj'", "file")]
    public void EvaluateItemTransforms(string expression, string include)
        => EvaluateExpression(expression, items: Items("Value", include))
            .ShouldBeTrue();

    /// <summary>
    ///  Verifies ordered multi-item expansion with default and custom separators.
    /// </summary>
    /// <param name="expression">The condition referencing both <c>Value</c> items.</param>
    [Theory]
    [InlineData("@(Value -> '%(filename).z', '$')=='first.z$second.z'")]
    [InlineData("'@(Value)'=='first;second'")]
    [InlineData("@(Value -> '%(filename)', '!')=='first!second'")]
    [InlineData("'first!second'==@(Value -> '%(filename)', '!')")]
    public void EvaluateMultipleItemExpressions(string expression)
        => EvaluateExpression(expression, items: Items(("Value", "first"), ("Value", "second")))
            .ShouldBeTrue();

    /// <summary>
    ///  Verifies evaluation errors for empty item expressions that the parser accepts.
    /// </summary>
    /// <param name="expression">The syntactically valid condition whose item expansion must fail.</param>
    [Theory]
    [InlineData("@()")]
    [InlineData("@() and true")]
    public void EmptyItemExpressionsFailToEvaluate(string expression)
        => AssertEvaluationError(expression, items: NoItems);

    /// <summary>
    ///  Verifies evaluation errors in successfully parsed conditions using a single item.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> item.</param>
    /// <param name="include">The item include to expand.</param>
    [Theory]
    [InlineData("@(Value)<text", "true")]
    [InlineData("@(Value)<=1", "text")]
    [InlineData("@(Value->'text','')", "true")]
    public void EvaluateInvalidItemExpressions(string expression, string include)
        => AssertEvaluationError(expression, items: Items("Value", include));

    /// <summary>
    ///  Verifies that an undefined item list cannot be used as a boolean operand.
    /// </summary>
    /// <param name="expression">The condition referencing an undefined item list.</param>
    [Theory]
    [InlineData("@(Missing) and true")]
    [InlineData("@(Missing)")]
    public void EvaluateUndefinedItemsAsBooleanThrows(string expression)
        => AssertEvaluationError(expression, items: NoItems);

    /// <summary>
    ///  Verifies that multiple items cannot be used as a scalar numeric or boolean operand.
    /// </summary>
    /// <param name="expression">The condition referencing both <c>Value</c> items.</param>
    [Theory]
    [InlineData("1<=@(Value)")]
    [InlineData("@(Value) and true")]
    public void EvaluateMultipleItemsAsScalarThrows(string expression)
        => AssertEvaluationError(expression, items: Items(("Value", "first"), ("Value", "second")));

    /// <summary>
    ///  Verifies true conditions using a single metadata value.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> metadata.</param>
    /// <param name="value">The metadata value to expand.</param>
    [Theory]
    [InlineData("%(value)=='TEXT'", "text")]
    [InlineData("'%(value) suffix'=='TEXT SUFFIX' ", "text")]
    public void EvaluateTrueMetadataExpressions(string expression, string value)
        => EvaluateExpression(expression, metadata: Metadata("Value", value))
            .ShouldBeTrue();

    /// <summary>
    ///  Verifies false conditions using a single metadata value.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> metadata.</param>
    /// <param name="value">The metadata value to expand.</param>
    [Theory]
    [InlineData("%(value)=='other'", "text")]
    [InlineData("'%(value) suffix'=='other suffix' ", "text")]
    public void EvaluateFalseMetadataExpressions(string expression, string value)
        => EvaluateExpression(expression, metadata: Metadata("Value", value))
            .ShouldBeFalse();

    /// <summary>
    ///  Verifies that undefined metadata expands to an empty string in quoted and unquoted comparisons.
    /// </summary>
    /// <param name="expression">The condition referencing undefined metadata.</param>
    /// <param name="expected">The expected comparison result.</param>
    [Theory]
    [InlineData("'%(Missing)'==''", true)]
    [InlineData("%(Missing)!='other'", true)]
    [InlineData("'%(Missing)'=='other'", false)]
    [InlineData("%(Missing)=='other'", false)]
    public void EvaluateUndefinedMetadataExpressions(string expression, bool expected)
        => EvaluateExpression(expression, metadata: NoMetadata).ShouldBe(expected);

    /// <summary>
    ///  Verifies that an empty metadata expression parses but fails during evaluation.
    /// </summary>
    [Fact]
    public void EmptyMetadataExpressionFailsToEvaluate()
        => AssertEvaluationError("%()", metadata: NoMetadata);

    /// <summary>
    ///  Verifies that a non-boolean metadata value cannot be used as a boolean operand.
    /// </summary>
    [Fact]
    public void EvaluateNonBooleanMetadataAsBooleanThrows()
        => AssertEvaluationError("%(Value) or true", metadata: Metadata("Value", "text"));

    /// <summary>
    ///  Verifies true conditions combining one property and one item.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Property</c> property and <c>Item</c> item.</param>
    /// <param name="propertyValue">The property value to expand.</param>
    /// <param name="include">The item include to expand.</param>
    [Theory]
    [InlineData("@(Item)==$(Property)", "text", "text")]
    [InlineData("'$(Property)1@(Item)'=='text1text'", "text", "text")]
    public void EvaluateTrueMixedExpressions(string expression, string propertyValue, string include)
        => EvaluateExpression(expression, Properties("Property", propertyValue), Items("Item", include))
            .ShouldBeTrue();

    /// <summary>
    ///  Verifies evaluation errors in parsed numeric comparisons combining one property and one item.
    /// </summary>
    [Fact]
    public void EvaluateInvalidMixedExpressions()
        => AssertEvaluationError("'$(Property)@(Item)'>1", Properties("Property", "1"), Items("Item", "text"));

    /// <summary>
    ///  Verifies quoted and unquoted literal paths, including negation of existence checks.
    /// </summary>
    /// <param name="expression">The existence condition to evaluate.</param>
    /// <param name="expected">The expected condition result.</param>
    [Theory]
    [InlineData("exists('file')", true)]
    [InlineData("exists(file)", true)]
    [InlineData("!exists('file')", false)]
    [InlineData("!!!exists(file)", false)]
    public void EvaluateLiteralExistsExpressions(string expression, bool expected)
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, "file", string.Empty);

        EvaluateExpression(expression, evaluationDirectory: root.Path)
            .ShouldBe(expected);
    }

    /// <summary>
    ///  Verifies that empty, whitespace-only, invalid, and missing paths do not exist.
    /// </summary>
    /// <param name="expression">The existence condition to evaluate in an empty directory.</param>
    [Theory]
    [InlineData("exists('')")]
    [InlineData("exists(' ')")]
    [InlineData("exists('\t')")]
    [InlineData("exists('|||||')")]
    [InlineData("exists('missing')")]
    public void EvaluateMissingExistsPaths(string expression)
    {
        TransientTestFolder root = Environment.CreateFolder();

        EvaluateExpression(expression, evaluationDirectory: root.Path)
            .ShouldBeFalse();
    }

    /// <summary>
    ///  Verifies that every literal path must exist, including when a missing path occurs first or in the middle.
    /// </summary>
    /// <param name="expression">The existence condition containing multiple paths.</param>
    /// <param name="expected">The expected condition result.</param>
    [Theory]
    [InlineData("exists('first;last')", true)]
    [InlineData("!exists('first;last')", false)]
    [InlineData("exists('first;missing;last')", false)]
    [InlineData("exists('missing;last;first')", false)]
    public void ExistsChecksEveryLiteralPath(string expression, bool expected)
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, "first", string.Empty);
        Environment.CreateFile(root, "last", string.Empty);

        EvaluateExpression(expression, evaluationDirectory: root.Path).ShouldBe(expected);
    }

    /// <summary>
    ///  Verifies escaped semicolons and apostrophes in literal filenames.
    /// </summary>
    /// <param name="expression">The existence condition containing an escaped filename.</param>
    /// <param name="fileName">The literal filename to create.</param>
    [Theory]
    [InlineData("exists('first%3bsecond')", "first;second")]
    [InlineData("exists('first%27second')", "first'second")]
    [InlineData("exists('%3b')", ";")]
    [InlineData("exists('%27')", "'")]
    public void ExistsUnescapesLiteralFilenames(string expression, string fileName)
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, fileName, string.Empty);

        EvaluateExpression(expression, evaluationDirectory: root.Path)
            .ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies quoted and unquoted existence checks using a single property.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> property.</param>
    [Theory]
    [InlineData("exists($(Value))")]
    [InlineData("exists('$(Value)')")]
    public void EvaluatePropertyExistsExpressions(string expression)
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, "file", string.Empty);

        EvaluateExpression(expression, Properties("Value", "file"), evaluationDirectory: root.Path).ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies multi-path property expansion and checks for missing paths appended to the property.
    /// </summary>
    /// <param name="expression">The condition referencing the multi-path <c>Value</c> property.</param>
    /// <param name="expected">The expected condition result.</param>
    [Theory]
    [InlineData("exists($(Value))", true)]
    [InlineData("!exists('$(Value)')", false)]
    [InlineData("exists('$(Value);missing')", false)]
    public void ExistsChecksEveryPropertyPath(string expression, bool expected)
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, "first", string.Empty);
        Environment.CreateFile(root, "last", string.Empty);

        EvaluateExpression(expression, Properties("Value", "first;last"), evaluationDirectory: root.Path).ShouldBe(expected);
    }

    /// <summary>
    ///  Verifies property expansion containing escaped or literal filename punctuation.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> property.</param>
    /// <param name="value">The property value to expand.</param>
    /// <param name="fileName">The literal filename to create.</param>
    [Theory]
    [InlineData("exists($(Value))", "first%3bsecond", "first;second")]
    [InlineData("exists('$(Value)')", "first%3bsecond", "first;second")]
    [InlineData("exists($(Value))", "first%27second", "first'second")]
    [InlineData("exists('$(Value)')", "first%27second", "first'second")]
    [InlineData("exists($(Value))", "first'second", "first'second")]
    [InlineData("exists('$(Value)')", "first'second", "first'second")]
    public void ExistsExpandsSpecialFilenames(string expression, string value, string fileName)
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, fileName, string.Empty);

        EvaluateExpression(expression, Properties("Value", value), evaluationDirectory: root.Path).ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies that an undefined property supplies no paths to an existence check.
    /// </summary>
    /// <param name="expression">The condition referencing an undefined property.</param>
    [Theory]
    [InlineData("exists($(Missing))")]
    [InlineData("exists('$(Missing)')")]
    public void ExistsWithUndefinedPropertyReturnsFalse(string expression)
        => EvaluateExpression(expression, NoProperties).ShouldBeFalse();

    /// <summary>
    ///  Verifies that an apostrophe in an expanded missing path does not cause a parse error.
    /// </summary>
    [Fact]
    public void ExistsWithMissingPropertyPathReturnsFalse()
    {
        TransientTestFolder root = Environment.CreateFolder();

        EvaluateExpression("exists('$(Value)')", Properties("Value", "missing'file"), evaluationDirectory: root.Path)
            .ShouldBeFalse();
    }

    /// <summary>
    ///  Verifies quoted and unquoted existence checks using a single item.
    /// </summary>
    /// <param name="expression">The condition referencing the <c>Value</c> item.</param>
    [Theory]
    [InlineData("exists(@(Value))")]
    [InlineData("exists('@(Value)')")]
    public void EvaluateItemExistsExpressions(string expression)
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, "file", string.Empty);

        EvaluateExpression(expression, items: Items("Value", "file"), evaluationDirectory: root.Path)
            .ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies that every item path must exist, not just the first item.
    /// </summary>
    /// <param name="expression">The existence condition referencing both <c>Value</c> items.</param>
    /// <param name="secondFileExists">Whether to create the second file and expect the condition to succeed.</param>
    [Theory]
    [InlineData("exists(@(Value))", true)]
    [InlineData("exists(@(Value))", false)]
    [InlineData("exists('@(Value)')", true)]
    [InlineData("exists('@(Value)')", false)]
    public void ExistsChecksEveryItemPath(string expression, bool secondFileExists)
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, "first", string.Empty);
        if (secondFileExists)
        {
            Environment.CreateFile(root, "last", string.Empty);
        }

        EvaluateExpression(
            expression,
            items: Items(("Value", "first"), ("Value", "last")),
            evaluationDirectory: root.Path).ShouldBe(secondFileExists);
    }

    /// <summary>
    ///  Verifies that an undefined item list supplies no paths to an existence check.
    /// </summary>
    /// <param name="expression">The condition referencing an undefined item list.</param>
    [Theory]
    [InlineData("exists(@(Missing))")]
    [InlineData("exists('@(Missing)')")]
    public void ExistsWithUndefinedItemsReturnsFalse(string expression)
        => EvaluateExpression(expression, items: NoItems).ShouldBeFalse();

    /// <summary>
    ///  Verifies that an undefined item list does not discard an existing path in the same argument.
    /// </summary>
    [Fact]
    public void ExistsIgnoresUndefinedItems()
    {
        TransientTestFolder root = Environment.CreateFolder();
        Environment.CreateFile(root, "file", string.Empty);

        EvaluateExpression(
            "exists('@(Value);@(Missing)')",
            items: Items("Value", "file"),
            evaluationDirectory: root.Path).ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies that punctuation in an expanded missing item path does not cause a parse error.
    /// </summary>
    [Fact]
    public void ExistsWithMissingItemPathReturnsFalse()
    {
        TransientTestFolder root = Environment.CreateFolder();

        EvaluateExpression(
            "exists('@(Value)')",
            items: Items("Value", "missing'file;other"),
            evaluationDirectory: root.Path).ShouldBeFalse();
    }

    /// <summary>
    ///  Verifies that existence checks require exactly one argument.
    /// </summary>
    /// <param name="expression">The function call with an invalid argument count.</param>
    [Theory]
    [InlineData("exists()")]
    [InlineData("exists( )")]
    [InlineData("exists(,)")]
    public void ExistsRejectsInvalidArgumentCounts(string expression)
        => AssertEvaluationError(expression);

    /// <summary>
    ///  Verifies that an item expression cannot be concatenated with a property inside an existence-check argument.
    /// </summary>
    [Fact]
    public void ExistsRejectsMixedExpansion()
        => AssertEvaluationError("exists('@(Item)$(Property)')", Properties("Property", "text"), Items("Item", "file"));

    private static TheoryData<string, bool> CreateBooleanLiteralTests()
    {
        TheoryData<string, bool> data = [];

        foreach ((string text, bool value) in BooleanLiterals)
        {
            data.Add(text, value);
            data.Add(text.ToUpperInvariant(), value);
        }

        return data;
    }

    private static TheoryData<string, bool, string, bool> CreateBooleanLiteralPairs()
    {
        TheoryData<string, bool, string, bool> data = [];

        foreach ((string left, bool leftValue) in BooleanLiterals)
        {
            foreach ((string right, bool rightValue) in BooleanLiterals)
            {
                data.Add(left, leftValue, right, rightValue);
            }
        }

        return data;
    }

    /// <summary>
    ///  Verifies all six comparison operators against the expected relationship between two literals.
    /// </summary>
    /// <param name="left">The left literal.</param>
    /// <param name="right">The right literal.</param>
    /// <param name="expectedComparison">-1 if the left value is less than the right, 0 if equal, or 1 if greater.</param>
    private static void AssertComparisons(string left, string right, int expectedComparison)
    {
        EvaluateExpression($"{left} == {right}").ShouldBe(expectedComparison == 0);
        EvaluateExpression($"{left} != {right}").ShouldBe(expectedComparison != 0);
        EvaluateExpression($"{left} < {right}").ShouldBe(expectedComparison < 0);
        EvaluateExpression($"{left} <= {right}").ShouldBe(expectedComparison <= 0);
        EvaluateExpression($"{left} > {right}").ShouldBe(expectedComparison > 0);
        EvaluateExpression($"{left} >= {right}").ShouldBe(expectedComparison >= 0);
    }

    /// <summary>
    ///  Verifies an evaluation error after successfully parsing the condition.
    /// </summary>
    /// <param name="expression">The condition whose evaluation must fail.</param>
    /// <param name="properties">The available properties, if needed.</param>
    /// <param name="items">The available items, if needed.</param>
    /// <param name="metadata">The available metadata, if needed.</param>
    private static void AssertEvaluationError(
        string expression,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null)
    {
        GenericExpressionNode tree = Should.NotThrow(
            () => ParseExpression(expression),
            $"Expected '{expression}' to parse successfully before testing evaluation.");

        _ = Should.Throw<InvalidProjectFileException>(() =>
            EvaluateExpression(tree, expression, properties, items, metadata));
    }

    /// <summary>
    ///  Parses and evaluates a condition with only the expansion inputs supplied by its test.
    /// </summary>
    /// <param name="expression">The condition to parse and evaluate.</param>
    /// <param name="properties">The available properties, if needed.</param>
    /// <param name="items">The available items, if needed.</param>
    /// <param name="metadata">The available metadata, if needed.</param>
    /// <param name="evaluationDirectory">The base directory for existence checks; otherwise, the test assembly directory is used.</param>
    /// <returns>
    ///  The evaluated condition result.
    /// </returns>
    private static bool EvaluateExpression(
        string expression,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null,
        string? evaluationDirectory = null)
        => EvaluateExpression(ParseExpression(expression), expression, properties, items, metadata, evaluationDirectory);

    /// <summary>
    ///  Evaluates an already parsed condition with only the expansion inputs supplied by its test.
    /// </summary>
    /// <param name="tree">The successfully parsed expression tree.</param>
    /// <param name="expression">The original condition text.</param>
    /// <param name="properties">The available properties, if needed.</param>
    /// <param name="items">The available items, if needed.</param>
    /// <param name="metadata">The available metadata, if needed.</param>
    /// <param name="evaluationDirectory">The base directory for existence checks; otherwise, the test assembly directory is used.</param>
    /// <returns>
    ///  The evaluated condition result.
    /// </returns>
    private static bool EvaluateExpression(
        GenericExpressionNode tree,
        string expression,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null,
        string? evaluationDirectory = null)
    {
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(properties, items, metadata, FileSystems.Default);

        ExpanderOptions options = 0;
        if (properties is not null)
        {
            options |= ExpanderOptions.ExpandProperties;
        }

        if (items is not null)
        {
            options |= ExpanderOptions.ExpandItems;
        }

        if (metadata is not null)
        {
            options |= ExpanderOptions.ExpandMetadata;
        }

        ConditionEvaluator.IConditionEvaluationState state =
            new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                expression,
                expander,
                options,
                conditionedPropertiesInProject: null,
                evaluationDirectory ?? AppContext.BaseDirectory,
                ElementLocation.Empty,
                FileSystems.Default);

        return tree.Evaluate(state);
    }

    /// <summary>
    ///  Parses a condition without performing expansion or evaluation.
    /// </summary>
    /// <param name="expression">The condition to parse.</param>
    /// <returns>
    ///  The parsed expression tree.
    /// </returns>
    private static GenericExpressionNode ParseExpression(string expression)
    {
        Parser parser = new();
        return parser.Parse(expression, ParserOptions.AllowAll, ElementLocation.Empty);
    }

    private sealed class TrackingPropertyProvider(bool value) : IPropertyProvider<ProjectPropertyInstance>
    {
        public int ReadCount { get; private set; }

        public ProjectPropertyInstance GetProperty(string name)
        {
            name.ShouldBe("Right");
            ReadCount++;
            return ProjectPropertyInstance.Create(name, value ? "true" : "false");
        }

        public ProjectPropertyInstance GetProperty(string name, int startIndex, int length)
            => GetProperty(name.Substring(startIndex, length));
    }
}
