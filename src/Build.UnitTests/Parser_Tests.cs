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
    ///  Verifies that qualified metadata (ItemType.MetadataName) parses into StringExpressionNode.
    /// </summary>
    [Fact]
    public void QualifiedMetadata_ShouldParseToStringNode()
        => Parse<StringExpressionNode>("%(Compile.Culture)")
            .Verify("%(Compile.Culture)");

    /// <summary>
    ///  Verifies that qualified metadata equality comparisons parse with correct structure.
    /// </summary>
    [Fact]
    public void QualifiedMetadataEquality_ShouldParseWithCorrectStructure()
        => Parse<EqualExpressionNode>("%(Compile.Culture)=='french'")
            .Verify(
                (StringExpressionNode left) => left.Verify("%(Compile.Culture)"),
                (StringExpressionNode right) => right.Verify("french"));

    /// <summary>
    ///  Verifies that built-in metadata references parse correctly.
    ///  Built-in metadata includes Identity, FullPath, Filename, Extension, etc.
    /// </summary>
    [Theory]
    [InlineData("%(Identity)")]
    [InlineData("%(FullPath)")]
    [InlineData("%(RootDir)")]
    [InlineData("%(Filename)")]
    [InlineData("%(Extension)")]
    [InlineData("%(RelativeDir)")]
    [InlineData("%(Directory)")]
    [InlineData("%(RecursiveDir)")]
    [InlineData("%(ModifiedTime)")]
    [InlineData("%(CreatedTime)")]
    [InlineData("%(AccessedTime)")]
    public void BuiltInMetadata_ShouldParseToStringNode(string expression)
        => Parse<StringExpressionNode>(expression)
            .Verify(expression);

    /// <summary>
    ///  Verifies that qualified built-in metadata references parse correctly.
    ///  Format: %(ItemType.MetadataName) where MetadataName is a built-in metadata.
    /// </summary>
    [Theory]
    [InlineData("%(Compile.Identity)")]
    [InlineData("%(Compile.FullPath)")]
    [InlineData("%(Compile.Filename)")]
    [InlineData("%(Compile.Extension)")]
    [InlineData("%(Compile.Directory)")]
    [InlineData("%(CSFile.RelativeDir)")]
    [InlineData("%(Resource.ModifiedTime)")]
    public void QualifiedBuiltInMetadata_ShouldParseToStringNode(string expression)
        => Parse<StringExpressionNode>(expression)
            .Verify(expression);

    /// <summary>
    ///  Verifies that built-in metadata works correctly in equality comparisons.
    /// </summary>
    [Fact]
    public void BuiltInMetadataEquality_ShouldParseWithCorrectStructure()
        => Parse<EqualExpressionNode>("%(Filename)=='Program'")
            .Verify(
                (StringExpressionNode left) => left.Verify("%(Filename)"),
                (StringExpressionNode right) => right.Verify("Program"));

    /// <summary>
    ///  Verifies that qualified built-in metadata works correctly in equality comparisons.
    /// </summary>
    [Fact]
    public void QualifiedBuiltInMetadataEquality_ShouldParseWithCorrectStructure()
        => Parse<EqualExpressionNode>("%(Compile.Extension)=='.cs'")
            .Verify(
                (StringExpressionNode left) => left.Verify("%(Compile.Extension)"),
                (StringExpressionNode right) => right.Verify(".cs"));

    /// <summary>
    ///  Verifies that metadata references work correctly in complex logical expressions.
    ///  Structure: (%(Compile.Filename) == 'Program' AND %(Compile.Extension) == '.cs')
    /// </summary>
    [Fact]
    public void QualifiedMetadata_InComplexExpression_ShouldParseCorrectly()
        => Parse<AndExpressionNode>("%(Compile.Filename) == 'Program' and %(Compile.Extension) == '.cs'")
            .Verify(
                (EqualExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("%(Compile.Filename)"),
                    (StringExpressionNode right) => right.Verify("Program")),
                (EqualExpressionNode right) => right.Verify(
                    (StringExpressionNode left) => left.Verify("%(Compile.Extension)"),
                    (StringExpressionNode right) => right.Verify(".cs")));

    /// <summary>
    ///  Verifies that metadata references work correctly with NOT operator.
    ///  Structure: !(%(Compile.Extension) == '.cs')
    /// </summary>
    [Fact]
    public void QualifiedMetadata_WithNotOperator_ShouldParseCorrectly()
        => Parse<NotExpressionNode>("!(%(Compile.Extension) == '.cs')")
            .Verify((EqualExpressionNode child) => child.Verify(
                (StringExpressionNode left) => left.Verify("%(Compile.Extension)"),
                (StringExpressionNode right) => right.Verify(".cs")));

    /// <summary>
    ///  Verifies that strings containing qualified metadata references parse correctly.
    /// </summary>
    [Fact]
    public void StringWithQualifiedMetadata_ShouldParseWithCorrectStructure()
        => Parse<EqualExpressionNode>("'bin_%(Compile.Filename)'=='bin_Program'")
            .Verify(
                (StringExpressionNode left) => left.Verify("bin_%(Compile.Filename)"),
                (StringExpressionNode right) => right.Verify("bin_Program"));

    /// <summary>
    ///  Verifies that DefiningProject metadata references parse correctly.
    ///  These are special built-in metadata that refer to the project file itself.
    /// </summary>
    [Theory]
    [InlineData("%(DefiningProjectFullPath)")]
    [InlineData("%(DefiningProjectDirectory)")]
    [InlineData("%(DefiningProjectName)")]
    [InlineData("%(DefiningProjectExtension)")]
    [InlineData("%(Compile.DefiningProjectFullPath)")]
    [InlineData("%(Compile.DefiningProjectName)")]
    public void DefiningProjectMetadata_ShouldParseToStringNode(string expression)
        => Parse<StringExpressionNode>(expression)
            .Verify(expression);

    /// <summary>
    ///  Verifies that metadata with numeric comparisons parse correctly.
    ///  Useful for comparing ModifiedTime, CreatedTime, or AccessedTime metadata.
    /// </summary>
    [Fact]
    public void MetadataWithNumericComparison_ShouldParseCorrectly()
        => Parse<GreaterThanExpressionNode>("%(Compile.ModifiedTime) > 0")
            .Verify(
                (StringExpressionNode left) => left.Verify("%(Compile.ModifiedTime)"),
                (NumericExpressionNode right) => right.Verify("0"));

    /// <summary>
    ///  Verifies that mixed unqualified and qualified metadata references work in the same expression.
    ///  Structure: (%(Identity) == 'file.cs' OR %(Compile.Extension) == '.cs')
    /// </summary>
    [Fact]
    public void MixedQualifiedAndUnqualifiedMetadata_ShouldParseCorrectly()
        => Parse<OrExpressionNode>("%(Identity) == 'file.cs' or %(Compile.Extension) == '.cs'")
            .Verify(
                (EqualExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("%(Identity)"),
                    (StringExpressionNode right) => right.Verify("file.cs")),
                (EqualExpressionNode right) => right.Verify(
                    (StringExpressionNode left) => left.Verify("%(Compile.Extension)"),
                    (StringExpressionNode right) => right.Verify(".cs")));

    /// <summary>
    ///  Verifies that custom metadata in qualified form parses correctly.
    ///  Custom metadata is user-defined, not built-in metadata.
    /// </summary>
    [Theory]
    [InlineData("%(Compile.CustomMetadata)")]
    [InlineData("%(Resource.TargetDirectory)")]
    [InlineData("%(EmbeddedResource.Culture)")]
    [InlineData("%(CSFile.LinkResource)")]
    public void CustomQualifiedMetadata_ShouldParseToStringNode(string expression)
        => Parse<StringExpressionNode>(expression)
            .Verify(expression);

    /// <summary>
    ///  Verifies that metadata references work correctly in function call arguments.
    ///  Structure: Exists(%(Compile.FullPath))
    /// </summary>
    [Fact]
    public void QualifiedMetadata_InFunctionCall_ShouldParseCorrectly()
        => Parse<FunctionCallExpressionNode>(
            "Exists(%(Compile.FullPath))",
            ParserOptions.AllowAll)
            .Verify(
                name: "Exists",
                (StringExpressionNode arg) => arg.Verify("%(Compile.FullPath)"));

    /// <summary>
    ///  Verifies that built-in metadata is rejected when ParserOptions.AllowBuiltInMetadata is not set.
    /// </summary>
    [Theory]
    [InlineData("%(Filename) == 'Program'")] // Unqualified
    [InlineData("%(Compile.Extension) == '.cs'")] // Qualified
    public void BuiltInMetadata_WhenNotAllowed_ShouldFail(string expression)
    {
        var error = FailParse(expression, ParserOptions.AllowAll & ~ParserOptions.AllowBuiltInMetadata);

        error.ResourceName.ShouldBe(ParseErrors.BuiltInMetadataNotAllowed);
    }

    /// <summary>
    ///  Verifies that custom metadata is rejected when ParserOptions.AllowCustomMetadata is not set.
    /// </summary>
    [Theory]
    [InlineData("%(Culture) == 'fr'")] // Unqualified
    [InlineData("%(Compile.Culture) == 'fr'")] // Qualified
    public void CustomMetadata_WhenNotAllowed_ShouldFail(string expression)
    {
        var error = FailParse(expression, ParserOptions.AllowAll & ~ParserOptions.AllowCustomMetadata);

        error.ResourceName.ShouldBe(ParseErrors.CustomMetadataNotAllowed);
    }

    /// <summary>
    ///  Verifies that metadata in quoted strings is rejected when not allowed.
    /// </summary>
    [Theory]
    [InlineData("'%(Filename)' == 'Program'")]
    [InlineData("'%(Culture)' == 'fr'")]
    public void Metadata_InQuotedString_WhenNotAllowed_ShouldFail(string expression)
    {
        var error = FailParse(expression, ParserOptions.AllowProperties);

        error.ResourceName.ShouldBe(ParseErrors.ItemMetadataNotAllowed);
    }

    /// <summary>
    ///  Verifies that metadata in function call arguments is rejected when not allowed.
    /// </summary>
    [Theory]
    [InlineData("Exists(%(Compile.FullPath))")]
    [InlineData("SomeFunction(%(Culture))")]
    public void Metadata_InFunctionCall_WhenNotAllowed_ShouldFail(string expression)
    {
        var options = ParserOptions.AllowProperties | ParserOptions.AllowUndefinedFunctions;
        var error = FailParse(expression, options);

        error.ResourceName.ShouldBe(ParseErrors.ItemMetadataNotAllowed);
    }

    /// <summary>
    ///  Verifies that built-in metadata is allowed when ParserOptions.AllowBuiltInMetadata is set,
    ///  even if AllowCustomMetadata is not set.
    /// </summary>
    [Fact]
    public void BuiltInMetadata_WithAllowBuiltInMetadata_ShouldSucceed()
        => Should.NotThrow(() => Parse("%(Filename) == 'Program'", ParserOptions.AllowProperties | ParserOptions.AllowBuiltInMetadata));

    /// <summary>
    ///  Verifies that custom metadata is allowed when ParserOptions.AllowCustomMetadata is set,
    ///  even if AllowBuiltInMetadata is not set.
    /// </summary>
    [Fact]
    public void CustomMetadata_WithAllowCustomMetadata_ShouldSucceed()
        => Should.NotThrow(() => Parse("%(Culture) == 'fr'", ParserOptions.AllowPropertiesAndCustomMetadata));

    /// <summary>
    ///  Verifies that both built-in and custom metadata are allowed when ParserOptions.AllowItemMetadata is set.
    ///  AllowItemMetadata is equivalent to AllowBuiltInMetadata | AllowCustomMetadata.
    /// </summary>
    [Theory]
    [InlineData("%(Filename) == 'Program'")] // Built-in
    [InlineData("%(Culture) == 'fr'")] // Custom
    [InlineData("%(Filename) == 'Program' and %(Culture) == 'fr'")] // Mixed
    public void Metadata_WithAllowItemMetadata_ShouldSucceed(string expression)
        => Should.NotThrow(() => Parse(expression, ParserOptions.AllowPropertiesAndItemMetadata));

    /// <summary>
    ///  Verifies that when only AllowBuiltInMetadata is set, built-in metadata succeeds
    ///  but custom metadata fails.
    /// </summary>
    [Fact]
    public void Metadata_WithOnlyAllowBuiltInMetadata_ShouldAllowBuiltInOnly()
    {
        // Built-in should succeed
        Should.NotThrow(() => Parse("%(Filename) == 'Program'", ParserOptions.AllowProperties | ParserOptions.AllowBuiltInMetadata));

        // Custom should fail
        var error = FailParse("%(Culture) == 'fr'", ParserOptions.AllowProperties | ParserOptions.AllowBuiltInMetadata);
        error.ResourceName.ShouldBe(ParseErrors.CustomMetadataNotAllowed);
    }

    /// <summary>
    ///  Verifies that when only AllowCustomMetadata is set, custom metadata succeeds
    ///  but built-in metadata fails.
    /// </summary>
    [Fact]
    public void Metadata_WithOnlyAllowCustomMetadata_ShouldAllowCustomOnly()
    {
        // Custom should succeed
        Should.NotThrow(() => Parse("%(Culture) == 'fr'", ParserOptions.AllowPropertiesAndCustomMetadata));

        // Built-in should fail
        var error = FailParse("%(Filename) == 'Program'", ParserOptions.AllowPropertiesAndCustomMetadata);
        error.ResourceName.ShouldBe(ParseErrors.BuiltInMetadataNotAllowed);
    }

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
            ParserOptions.AllowAll | ParserOptions.AllowUndefinedFunctions)
            .Verify(name: "SimpleFunctionCall");

    /// <summary>
    ///  Verifies that function calls with single numeric argument parse correctly.
    /// </summary>
    [Fact]
    public void FunctionCall_WithNumericArgument_ShouldParseCorrectly()
        => Parse<FunctionCallExpressionNode>(
            "SimpleFunctionCall( 1234 )",
            ParserOptions.AllowAll | ParserOptions.AllowUndefinedFunctions)
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
            ParserOptions.AllowAll | ParserOptions.AllowUndefinedFunctions)
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
            ParserOptions.AllowAll | ParserOptions.AllowUndefinedFunctions)
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
            ParserOptions.AllowAll | ParserOptions.AllowUndefinedFunctions)
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
        var error = FailParse(expression, ParserOptions.AllowProperties | ParserOptions.AllowUndefinedFunctions);

        error.ResourceName.ShouldBe(ParseErrors.ItemListNotAllowed);
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

        error.ResourceName.ShouldBe(ParseErrors.IllFormedSpace);
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

        error.ResourceName.ShouldBe(ParseErrors.IllFormedEquals);
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
    [InlineData("@(", ParseErrors.IllFormedItemListCloseParenthesis)]
    [InlineData("@x", ParseErrors.IllFormedItemListOpenParenthesis)]
    [InlineData("@(x", ParseErrors.IllFormedItemListCloseParenthesis)]
    [InlineData("@(x->'%(y)", ParseErrors.IllFormedItemListQuote)]
    [InlineData("@(x->'%(y)', 'x", ParseErrors.IllFormedItemListQuote)]
    [InlineData("@(x->'%(y)', 'x'", ParseErrors.IllFormedItemListCloseParenthesis)]
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

        error.ResourceName.ShouldBe(ParseErrors.IllFormedQuotedString);
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

    /// <summary>
    ///  Verifies that escaped property references are treated as literal strings.
    ///  %24 = '$', %28 = '(', %29 = ')'
    /// </summary>
    [Theory]
    [InlineData("'%24(foo)' == '$(foo)'", "%24(foo)", "$(foo)")]
    [InlineData("'%24%28foo%29' == '$(foo)'", "%24%28foo%29", "$(foo)")]
    [InlineData("'prefix%24(prop)suffix' == 'prefix$(prop)suffix'", "prefix%24(prop)suffix", "prefix$(prop)suffix")]
    public void EscapedPropertyReference_ShouldBeTreedAsLiteral(string expression, string leftValue, string rightValue)
        => Parse<EqualExpressionNode>(expression)
            .Verify(
                (StringExpressionNode left) => left.Verify(leftValue),
                (StringExpressionNode right) => right.Verify(rightValue));

    /// <summary>
    ///  Verifies that escaped item list references are treated as literal strings.
    ///  %40 = '@', %28 = '(', %29 = ')'
    /// </summary>
    [Theory]
    [InlineData("'%40(foo)' == '@(foo)'", "%40(foo)", "@(foo)")]
    [InlineData("'%40%28foo%29' == '@(foo)'", "%40%28foo%29", "@(foo)")]
    [InlineData("'items%40(list)here' == 'items@(list)here'", "items%40(list)here", "items@(list)here")]
    public void EscapedItemListReference_ShouldBeTreedAsLiteral(string expression, string leftValue, string rightValue)
        => Parse<EqualExpressionNode>(expression)
            .Verify(
                (StringExpressionNode left) => left.Verify(leftValue),
                (StringExpressionNode right) => right.Verify(rightValue));

    /// <summary>
    ///  Verifies that escaped metadata references are treated as literal strings.
    ///  %25 = '%', %28 = '(', %29 = ')'
    /// </summary>
    [Theory]
    [InlineData("'%25(foo)' == '%(foo)'", "%25(foo)", "%(foo)")]
    [InlineData("'%25%28foo%29' == '%(foo)'", "%25%28foo%29", "%(foo)")]
    [InlineData("'meta%25(data)end' == 'meta%(data)end'", "meta%25(data)end", "meta%(data)end")]
    public void EscapedMetadataReference_ShouldBeTreedAsLiteral(string expression, string leftValue, string rightValue)
        => Parse<EqualExpressionNode>(expression)
            .Verify(
                (StringExpressionNode left) => left.Verify(leftValue),
                (StringExpressionNode right) => right.Verify(rightValue));

    /// <summary>
    ///  Verifies that escaped semicolons and other special characters are handled correctly.
    ///  %3B = ';', %2A = '*', %3F = '?'
    /// </summary>
    [Theory]
    [InlineData("'a%3Bb%3Bc' == 'a;b;c'", "a%3Bb%3Bc", "a;b;c")]
    [InlineData("'file%2A.txt' == 'file*.txt'", "file%2A.txt", "file*.txt")]
    [InlineData("'what%3F' == 'what?'", "what%3F", "what?")]
    public void EscapedSpecialCharacters_ShouldBeTreedAsLiteral(string expression, string leftValue, string rightValue)
        => Parse<EqualExpressionNode>(expression)
            .Verify(
                (StringExpressionNode left) => left.Verify(leftValue),
                (StringExpressionNode right) => right.Verify(rightValue));

    /// <summary>
    ///  Verifies that mixed escaped and unescaped syntax works correctly.
    ///  Escaped characters should not trigger expansion while unescaped ones should.
    /// </summary>
    [Theory]
    [InlineData("'%24(foo)$(bar)' == 'literal$(bar)'", "%24(foo)$(bar)", "literal$(bar)")]
    [InlineData("'$(prop)%40(item)' == '$(prop)literal'", "$(prop)%40(item)", "$(prop)literal")]
    public void MixedEscapedAndUnescaped_ShouldParseCorrectly(string expression, string leftValue, string rightValue)
        => Parse<EqualExpressionNode>(expression)
            .Verify(
                (StringExpressionNode left) => left.Verify(leftValue),
                (StringExpressionNode right) => right.Verify(rightValue));

    /// <summary>
    ///  Verifies that escaped characters work correctly in complex expressions.
    /// </summary>
    [Fact]
    public void EscapedCharacters_InComplexExpression_ShouldParseCorrectly()
        => Parse<AndExpressionNode>("'%24(x)' == '$(x)' and '%40(y)' == '@(y)'")
            .Verify(
                (EqualExpressionNode left) => left.Verify(
                    (StringExpressionNode left) => left.Verify("%24(x)"),
                    (StringExpressionNode right) => right.Verify("$(x)")),
                (EqualExpressionNode right) => right.Verify(
                    (StringExpressionNode left) => left.Verify("%40(y)"),
                    (StringExpressionNode right) => right.Verify("@(y)")));

    /// <summary>
    ///  Verifies that escape sequences themselves can be compared.
    /// </summary>
    [Theory]
    [InlineData("'%24' == '%24'", "%24", "%24")]
    [InlineData("'%40%28%29' == '%40%28%29'", "%40%28%29", "%40%28%29")]
    public void EscapeSequences_CanBeCompared(string expression, string leftValue, string rightValue)
        => Parse<EqualExpressionNode>(expression)
            .Verify(
                (StringExpressionNode left) => left.Verify(leftValue),
                (StringExpressionNode right) => right.Verify(rightValue));

    /// <summary>
    ///  Verifies that quoted boolean literals parse into BooleanLiteralNode with correct values.
    ///  Quoted strings containing only boolean keywords should be recognized as boolean literals.
    /// </summary>
    [Theory]
    [InlineData("'true'", true)]
    [InlineData("'false'", false)]
    [InlineData("'on'", true)]
    [InlineData("'off'", false)]
    [InlineData("'yes'", true)]
    [InlineData("'no'", false)]
    [InlineData("'TRUE'", true)]
    [InlineData("'FALSE'", false)]
    [InlineData("'ON'", true)]
    [InlineData("'OFF'", false)]
    [InlineData("'YES'", true)]
    [InlineData("'NO'", false)]
    public void QuotedBooleanLiteral_ShouldParseToBooleanNode(string expression, bool expectedValue)
        => Parse<BooleanLiteralNode>(expression)
            .Verify(expectedValue);

    /// <summary>
    ///  Verifies that quoted negated boolean literals parse into BooleanLiteralNode with inverted values.
    ///  The negation is precomputed, so '!true' becomes BooleanLiteralNode(false).
    /// </summary>
    [Theory]
    [InlineData("'!true'", false)]
    [InlineData("'!false'", true)]
    [InlineData("'!on'", false)]
    [InlineData("'!off'", true)]
    [InlineData("'!yes'", false)]
    [InlineData("'!no'", true)]
    [InlineData("'!TRUE'", false)]
    [InlineData("'!FALSE'", true)]
    public void QuotedNegatedBooleanLiteral_ShouldParseWithInvertedValue(string expression, bool expectedValue)
        => Parse<BooleanLiteralNode>(expression)
            .Verify(expectedValue);

    /// <summary>
    ///  Verifies that quoted boolean literals work correctly in equality comparisons.
    /// </summary>
    [Theory]
    [InlineData("'true' == true", true, true)]
    [InlineData("'false' == false", false, false)]
    [InlineData("'!on' == false", false, false)]
    [InlineData("'!off' == true", true, true)]
    public void QuotedBooleanLiteral_InEquality_ShouldParseCorrectly(string expression, bool leftValue, bool rightValue)
        => Parse<EqualExpressionNode>(expression)
            .Verify(
                (BooleanLiteralNode left) => left.Verify(leftValue),
                (BooleanLiteralNode right) => right.Verify(rightValue));

    /// <summary>
    ///  Verifies that quoted boolean literals work correctly in logical AND expressions.
    /// </summary>
    [Fact]
    public void QuotedBooleanLiteral_InAndExpression_ShouldParseCorrectly()
        => Parse<AndExpressionNode>("'true' and 'false'")
            .Verify(
                (BooleanLiteralNode left) => left.Verify(true),
                (BooleanLiteralNode right) => right.Verify(false));

    /// <summary>
    ///  Verifies that quoted boolean literals work correctly in logical OR expressions.
    /// </summary>
    [Fact]
    public void QuotedBooleanLiteral_InOrExpression_ShouldParseCorrectly()
        => Parse<OrExpressionNode>("'on' or 'off'")
            .Verify(
                (BooleanLiteralNode left) => left.Verify(true),
                (BooleanLiteralNode right) => right.Verify(false));

    /// <summary>
    ///  Verifies that quoted boolean literals work correctly with NOT operator.
    ///  Structure: !('true') - NOT of BooleanLiteralNode(true)
    /// </summary>
    [Fact]
    public void QuotedBooleanLiteral_WithNotOperator_ShouldParseCorrectly()
        => Parse<NotExpressionNode>("!('true')")
            .Verify((BooleanLiteralNode child) => child.Verify(true));

    /// <summary>
    ///  Verifies that quoted strings containing expandable content are NOT recognized as boolean literals.
    ///  If a quoted string contains properties, item lists, or metadata, it should remain a StringExpressionNode.
    /// </summary>
    [Theory]
    [InlineData("'true$(foo)'")]
    [InlineData("'$(foo)true'")]
    [InlineData("'true@(items)'")]
    [InlineData("'%(meta)true'")]
    public void QuotedString_WithExpandableContent_ShouldNotBeBooleanLiteral(string expression)
        => Parse<StringExpressionNode>(expression)
            .ShouldBeOfType<StringExpressionNode>();

    /// <summary>
    ///  Verifies that quoted strings containing text beyond boolean keywords are NOT recognized as boolean literals.
    /// </summary>
    [Theory]
    [InlineData("'true '", "true ")] // Trailing space after parsing
    [InlineData("'truevalue'", "truevalue")]
    [InlineData("'prefix_true'", "prefix_true")]
    [InlineData("'false_suffix'", "false_suffix")]
    [InlineData("'!true!'", "!true!")]
    [InlineData("'not true'", "not true")]
    public void QuotedString_WithAdditionalText_ShouldNotBeBooleanLiteral(string expression, string expectedValue)
        => Parse<StringExpressionNode>(expression)
            .Verify(expectedValue);

    /// <summary>
    ///  Verifies that empty quoted strings are NOT recognized as boolean literals.
    /// </summary>
    [Theory]
    [InlineData("''")]
    [InlineData("'  '")]
    [InlineData("' \t '")]
    public void QuotedString_Empty_ShouldNotBeBooleanLiteral(string expression)
        => Parse<StringExpressionNode>(expression)
            .ShouldBeOfType<StringExpressionNode>();

    /// <summary>
    ///  Verifies that quoted boolean literals work in complex nested expressions.
    /// </summary>
    [Fact]
    public void QuotedBooleanLiteral_InComplexExpression_ShouldParseCorrectly()
        => Parse<AndExpressionNode>("('true' or '!on') and 'yes'")
            .Verify(
                (OrExpressionNode left) => left.Verify(
                    (BooleanLiteralNode left) => left.Verify(true),
                    (BooleanLiteralNode right) => right.Verify(false)),
                (BooleanLiteralNode right) => right.Verify(true));

    /// <summary>
    ///  Verifies that quoted boolean literals work correctly in function call arguments.
    /// </summary>
    [Fact]
    public void QuotedBooleanLiteral_InFunctionCall_ShouldParseCorrectly()
        => Parse<FunctionCallExpressionNode>(
            "SomeFunction('true', '!false', 'yes')",
            ParserOptions.AllowAll | ParserOptions.AllowUndefinedFunctions)
            .Verify(
                name: "SomeFunction",
                (BooleanLiteralNode arg1) => arg1.Verify(true),
                (BooleanLiteralNode arg2) => arg2.Verify(true),
                (BooleanLiteralNode arg3) => arg3.Verify(true));

    /// <summary>
    ///  Verifies that quoted boolean literals work correctly with comparison operators.
    /// </summary>
    [Fact]
    public void QuotedBooleanLiteral_WithComparisonOperators_ShouldParseCorrectly()
        => Parse<NotEqualExpressionNode>("'true' != 'false'")
            .Verify(
                (BooleanLiteralNode left) => left.Verify(true),
                (BooleanLiteralNode right) => right.Verify(false));

    /// <summary>
    ///  Verifies that mixing quoted and unquoted boolean literals works correctly.
    /// </summary>
    [Fact]
    public void MixedQuotedAndUnquotedBooleans_ShouldParseCorrectly()
        => Parse<AndExpressionNode>("'true' and false")
            .Verify(
                (BooleanLiteralNode left) => left.Verify(true),
                (BooleanLiteralNode right) => right.Verify(false));

    /// <summary>
    ///  Verifies that quoted boolean literals with property comparison work correctly.
    /// </summary>
    [Fact]
    public void QuotedBooleanLiteral_WithPropertyComparison_ShouldParseCorrectly()
        => Parse<EqualExpressionNode>("$(IsEnabled) == 'true'")
            .Verify(
                (StringExpressionNode left) => left.Verify("$(IsEnabled)"),
                (BooleanLiteralNode right) => right.Verify(true));

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
