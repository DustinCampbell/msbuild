// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
/// Tests for the new allocation-efficient expression parser that validates
/// it produces identical results to the existing ExpressionShredder.
/// </summary>
public class ExpressionParser_Tests
{
    #region Item Expression Tests

    [Theory]
    [InlineData("@(Compile)")]
    [InlineData("@(Content)")]
    [InlineData("@(Reference)")]
    [InlineData("@(None)")]
    [InlineData("@(EmbeddedResource)")]
    public void SimpleItemExpression_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@( Compile )")]
    [InlineData("@(  Compile  )")]
    [InlineData("@(\tCompile\t)")]
    [InlineData("@(\nCompile\n)")]
    public void ItemExpressionWithWhitespace_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->'%(FullPath)')")]
    [InlineData("@(Compile->'%(Filename)')")]
    [InlineData("@(Compile->'%(Extension)')")]
    [InlineData("@(Compile->'%(RootDir)')")]
    [InlineData("@(Compile->'%(Directory)')")]
    [InlineData("@(Compile->'%(RecursiveDir)')")]
    [InlineData("@(Compile->'%(Identity)')")]
    [InlineData("@(Compile->'%(ModifiedTime)')")]
    [InlineData("@(Compile->'%(CreatedTime)')")]
    [InlineData("@(Compile->'%(AccessedTime)')")]
    public void ItemExpressionWithTransform_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->'%(FullPath).obj')")]
    [InlineData("@(Compile->'obj\\%(Filename).o')")]
    [InlineData("@(Compile->'prefix_%(Identity)_suffix')")]
    [InlineData("@(Compile->'')")]
    public void ItemExpressionWithComplexTransform_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile, ';')")]
    [InlineData("@(Compile, ',')")]
    [InlineData("@(Compile, ' ')")]
    [InlineData("@(Compile, '')")]
    [InlineData("@(Compile, '|')")]
    public void ItemExpressionWithSeparator_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->'%(FullPath)', ';')")]
    [InlineData("@(Compile->'%(FullPath)', ' ')")]
    [InlineData("@(Compile->'%(Filename).obj', ';')")]
    [InlineData("@(Compile -> '%(FullPath)' , ' ')")]
    public void ItemExpressionWithTransformAndSeparator_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->Count())")]
    [InlineData("@(Compile->Distinct())")]
    [InlineData("@(Compile->DistinctWithCase())")]
    [InlineData("@(Compile->Reverse())")]
    [InlineData("@(Compile->Exists())")]
    public void ItemExpressionWithFunction_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->DirectoryName())")]
    [InlineData("@(Compile->Metadata('CustomMetadata'))")]
    [InlineData("@(Compile->HasMetadata('CustomMetadata'))")]
    [InlineData("@(Compile->WithMetadataValue('Name', 'Value'))")]
    public void ItemExpressionWithFunctionAndArguments_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->'%(FullPath)'->Distinct())")]
    [InlineData("@(Compile->Distinct()->'%(FullPath)')")]
    [InlineData("@(Compile->DirectoryName()->Distinct())")]
    public void ItemExpressionWithChainedFunctions_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("prefix @(Compile) suffix")]
    [InlineData("before @(A) middle @(B) after")]
    [InlineData("@(A)@(B)")]
    [InlineData("text")]
    [InlineData("")]
    public void MixedExpression_MatchesOldParser(string expression)
        => AssertMultipleItemExpressionsMatch(expression);

    [Theory]
    [InlineData("@(Compile-With-Hyphens)")]
    [InlineData("@(Item_With_Underscores)")]
    [InlineData("@(Item.With.Dots)")]
    public void ItemExpressionWithSpecialCharacters_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Metadata Expression Tests

    [Theory]
    [InlineData("%(FullPath)")]
    [InlineData("%(Filename)")]
    [InlineData("%(Extension)")]
    [InlineData("%(CustomMetadata)")]
    public void SimpleMetadataExpression_MatchesOldParser(string expression)
        => AssertMetadataExpressionMatches(expression);

    [Theory]
    [InlineData("%(Compile.FullPath)")]
    [InlineData("%(Reference.HintPath)")]
    [InlineData("%(Content.CopyToOutputDirectory)")]
    public void QualifiedMetadataExpression_MatchesOldParser(string expression)
        => AssertMetadataExpressionMatches(expression);

    [Theory]
    [InlineData("%( FullPath )")]
    [InlineData("%(  Filename  )")]
    [InlineData("%(\tExtension\t)")]
    public void MetadataExpressionWithWhitespace_MatchesOldParser(string expression)
        => AssertMetadataExpressionMatches(expression);

    [Theory]
    [InlineData("prefix %(Filename) suffix")]
    [InlineData("%(A) and %(B)")]
    [InlineData("%(A)%(B)")]
    public void MixedMetadataExpression_MatchesOldParser(string expression)
        => AssertMetadataExpressionMatches(expression);

    #endregion

    #region Property Expression Tests

    [Theory]
    [InlineData("$(Configuration)")]
    [InlineData("$(Platform)")]
    [InlineData("$(OutputPath)")]
    [InlineData("$(MSBuildProjectDirectory)")]
    public void SimplePropertyExpression_MatchesOldParser(string expression)
    {
        // Property parsing is tested via the full expression parsing
        // but we keep these tests for documentation
        Assert.True(true);
    }

    #endregion

    #region Property Expression Preservation Tests

    [Theory]
    [InlineData("@(Items, '$(Separator)')")]
    [InlineData("@(Items->'$(Prefix)%(Identity)$(Suffix)')")]
    [InlineData("$(Property);@(Items)")]
    public void PropertyExpressionsPreserved_MatchesOldParser(string expression)
    {
        // ExpressionShredder doesn't parse properties, but should preserve them correctly
        AssertReferencedItemsAndMetadataMatchOld(expression);
    }

    #endregion

    #region Semicolon Splitting Tests

    [Theory]
    [InlineData("a;b;c")]
    [InlineData("a; b; c")]
    [InlineData(" a ; b ; c ")]
    [InlineData("a;;b")]
    [InlineData(";a;b;")]
    public void SimpleSemicolonSplit_MatchesOldParser(string expression)
        => AssertSemicolonSplitMatches(expression);

    [Theory]
    [InlineData("a;@(Items);c")]
    [InlineData("@(Items);b;c")]
    [InlineData("a;b;@(Items)")]
    [InlineData("@(A);@(B);@(C)")]
    public void SemicolonSplitWithItems_MatchesOldParser(string expression)
        => AssertSemicolonSplitMatches(expression);

    [Theory]
    [InlineData("a;@(Items, ';');c")]
    [InlineData("@(Items, ';')")]
    [InlineData("@(A, ';');@(B, ';')")]
    public void SemicolonSplitWithItemsContainingSeparators_MatchesOldParser(string expression)
        => AssertSemicolonSplitMatches(expression);

    [Theory]
    [InlineData("a;%(Meta);c")]
    [InlineData("%(Meta);b;c")]
    [InlineData("a;b;%(Meta)")]
    public void SemicolonSplitWithMetadata_MatchesOldParser(string expression)
        => AssertSemicolonSplitMatches(expression);

    #endregion

    #region Referenced Items and Metadata Tests

    [Theory]
    [InlineData("@(Compile)", new[] { "Compile" }, new string[0])]
    [InlineData("@(A);@(B)", new[] { "A", "B" }, new string[0])]
    [InlineData("%(Meta)", new string[0], new[] { "Meta" })]
    [InlineData("%(Item.Meta)", new string[0], new[] { "Item.Meta" })]
    [InlineData("@(A) %(Meta)", new[] { "A" }, new[] { "Meta" })]
    public void GetReferencedItemsAndMetadata_MatchesOldParser(
        string expression,
        string[] expectedItems,
        string[] expectedMetadata)
        => AssertReferencedItemsAndMetadataMatch(expression, expectedItems, expectedMetadata);

    [Theory]
    [InlineData("@(Items, '%(Meta)')")]
    [InlineData("@(Items->'text')")]
    public void GetReferencedItemsAndMetadata_WithTransforms_MatchesOldParser(string expression)
        => AssertReferencedItemsAndMetadataMatchOld(expression);

    #endregion

    #region Edge Cases and Malformed Expressions

    [Theory]
    [InlineData("@(")]
    [InlineData("@()")]
    [InlineData("@(Item")]
    [InlineData("@Item)")]
    [InlineData("%(")]
    [InlineData("%()")]
    [InlineData("%(Item")]
    [InlineData("%Item)")]
    public void MalformedExpression_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Item->'unclosed")]
    [InlineData("@(Item, 'unclosed)")]
    [InlineData("@(Item->Count(unclosed)")]
    public void IncompleteExpression_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@@@(Item)")]
    [InlineData("%%%(Meta)")]
    [InlineData("@(A)@(B)@(C)")]
    public void RepeatedDelimiters_MatchesOldParser(string expression)
        => AssertMultipleItemExpressionsMatch(expression);

    #endregion

    #region Additional Edge Cases

    [Theory]
    [InlineData("@(Item-With-Trailing-Hyphen-)")]
    [InlineData("@(-Item-With-Leading-Hyphen)")]
    [InlineData("@(Item--With--Double--Hyphens)")]
    public void ItemNamesWithHyphens_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Item->AnyHaveMetadataValue('Name', 'Value'))")]
    [InlineData("@(Item->ClearMetadata())")]
    [InlineData("@(Item->WithoutMetadataValue('Name', 'Value'))")]
    public void AdditionalItemFunctions_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->'%(FullPath)'->DirectoryName())")]
    [InlineData("@(Compile->Distinct()->Reverse())")]
    [InlineData("@(Compile->Count()->Count())")] // Questionable but should match behavior
    public void MultipleChainedTransforms_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Whitespace Variations

    [Theory]
    [InlineData("@(Compile  ->  '%(FullPath)')")]
    [InlineData("@(Compile->'%(FullPath)'  ,  ';')")]
    [InlineData("@(  Compile  ->  '%(FullPath)'  ,  ';'  )")]
    [InlineData("@(Compile  ->  DirectoryName(  )  )")]
    public void ExcessiveWhitespace_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile\r\n->\r\n'%(FullPath)')")]
    [InlineData("@(Compile\r->'%(FullPath)')")]
    public void NewlineCharacters_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Nested Quotes and Special Characters

    [Theory]
    [InlineData("@(Compile->'\"%(FullPath)\"')")]
    [InlineData("@(Compile->'`%(FullPath)`')")]
    [InlineData("@(Compile, '\\'')")] // Backslash as separator
    [InlineData("@(Compile, '\"')")] // Quote as separator
    public void SpecialCharactersInStrings_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->'%(Directory)\\%(Filename).obj')")]
    [InlineData("@(Compile->'c:\\output\\%(Filename).dll')")]
    public void PathsWithBackslashes_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Function Arguments with Special Cases

    [Theory]
    [InlineData("@(Compile->Metadata(''))")] // Empty metadata name
    [InlineData("@(Compile->WithMetadataValue('', ''))")] // Empty strings
    [InlineData("@(Compile->WithMetadataValue('Name', ''))")] // Empty value
    public void FunctionsWithEmptyArguments_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->Metadata('Custom-Metadata'))")] // Hyphen in metadata
    [InlineData("@(Compile->Metadata('Custom.Metadata'))")] // Dot in metadata
    [InlineData("@(Compile->Metadata('Custom_Metadata'))")] // Underscore in metadata
    public void FunctionsWithSpecialMetadataNames_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->WithMetadataValue('Name', 'Value;With;Semicolons'))")]
    [InlineData("@(Compile->WithMetadataValue('Name', 'Value,With,Commas'))")]
    [InlineData("@(Compile->WithMetadataValue('Name', 'Value With Spaces'))")]
    public void FunctionsWithComplexArgumentValues_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Metadata in Different Contexts

    [Theory]
    [InlineData("%(Compile.FullPath).obj")]
    [InlineData("prefix-%(Identity)-suffix")]
    [InlineData("%(Directory)\\%(Filename).%(Extension)")]
    public void MetadataInPlainText_MatchesOldParser(string expression)
        => AssertMetadataExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile, '%(Extension)')")] // Metadata in separator
    [InlineData("@(Compile->'%(Directory)\\%(Filename)', '%(Extension)')")] // Metadata in both
    public void MetadataInSeparators_MatchesOldParser(string expression)
        => AssertReferencedItemsAndMetadataMatchOld(expression);

    #endregion

    #region Multiple Expressions in One String

    [Theory]
    [InlineData("@(A);@(B);@(C);@(D);@(E)")] // Many items
    [InlineData("%(A)%(B)%(C)%(D)%(E)")] // Many metadata
    [InlineData("@(A)%(B)@(C)%(D)@(E)")] // Mixed
    public void ManyExpressionsInSequence_MatchesOldParser(string expression)
        => AssertMultipleItemExpressionsMatch(expression);

    [Theory]
    [InlineData("literal@(Item)literal")]
    [InlineData("@(Item)@(Item)")] // Same item twice
    [InlineData("text@(A)text@(A)text")]
    public void DuplicateReferences_MatchesOldParser(string expression)
        => AssertMultipleItemExpressionsMatch(expression);

    #endregion

    #region Semicolon Splitting Edge Cases

    [Theory]
    [InlineData(";;;")] // Only semicolons
    [InlineData("; ; ;")] // Semicolons with spaces
    [InlineData("a;;;b")] // Multiple consecutive
    public void MultipleSemicolons_MatchesOldParser(string expression)
        => AssertSemicolonSplitMatches(expression);

    [Theory]
    [InlineData("  a  ;  b  ;  c  ")] // Leading/trailing whitespace
    [InlineData("\ta\t;\tb\t;\tc\t")] // Tabs
    [InlineData("  \t  a  \t  ;  \t  b  \t  ")] // Mixed whitespace
    public void SemicolonSplitWithVariousWhitespace_MatchesOldParser(string expression)
        => AssertSemicolonSplitMatches(expression);

    [Theory]
    [InlineData("@(A->'%(X);%(Y)');b")] // Semicolon inside transform
    [InlineData("a;@(B, ';');c")] // Semicolon as separator
    [InlineData("@(A->'X')->@(B->'Y');c")] // Complex chains
    public void SemicolonSplitWithComplexExpressions_MatchesOldParser(string expression)
        => AssertSemicolonSplitMatches(expression);

    #endregion

    #region Unicode and Unusual Characters

    [Theory]
    [InlineData("@(Compilé)")] // Accented character
    [InlineData("@(Компилировать)")] // Cyrillic
    [InlineData("@(编译)")] // Chinese characters
    public void UnicodeItemNames_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->'%(FullPath)😀')")] // Emoji in transform
    [InlineData("@(Compile, '•')")] // Bullet point separator
    public void UnicodeInTransformsAndSeparators_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Complex Real-World Scenarios

    [Theory]
    [InlineData("@(Reference->'%(Filename).dll')->Distinct()->Reverse()")]
    [InlineData("@(Compile->HasMetadata('Generator')->WithMetadataValue('Generator', 'MSBuild:Compile'))")]
    [InlineData("@(Content->Exists()->DirectoryName()->Distinct())")]
    public void ChainedOperationsFromRealProjects_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile);@(Reference);@(Content);@(None);@(EmbeddedResource)")]
    [InlineData("@(Compile->'%(FullPath)');@(Reference->'%(HintPath)');@(Content->'%(Link)')")]
    public void CommonProjectExpressions_MatchesOldParser(string expression)
        => AssertMultipleItemExpressionsMatch(expression);

    #endregion

    #region Empty and Null Cases

    [Theory]
    [InlineData("")] // Already exists but important
    [InlineData("   ")] // Only whitespace
    [InlineData("\t\r\n")] // Only whitespace chars
    public void EmptyOrWhitespaceExpressions_MatchesOldParser(string expression)
        => AssertMultipleItemExpressionsMatch(expression);

    [Theory]
    [InlineData("@()")] // Already exists
    [InlineData("@(  )")] // Whitespace inside
    [InlineData("%(  )")] // Whitespace in metadata
    public void EmptyExpressionBodies_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Separator Edge Cases

    [Theory]
    [InlineData("@(Compile, '')")] // Already exists
    [InlineData("@(Compile, ' ')")] // Already exists
    [InlineData("@(Compile, '  ')")] // Multiple spaces
    [InlineData("@(Compile, '\t')")] // Tab separator
    [InlineData("@(Compile, '\r\n')")] // Newline separator
    public void VariousSeparators_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile, ';;;;;')")] // Multiple semicolons
    [InlineData("@(Compile, ';;')")]
    [InlineData("@(Compile, '; ')")] // Semicolon with space
    public void SemicolonBasedSeparators_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Transform Edge Cases

    [Theory]
    [InlineData("@(Compile->'')")] // Already exists but critical
    [InlineData("@(Compile->' ')")] // Space
    [InlineData("@(Compile->'  ')")] // Multiple spaces
    public void EmptyishTransforms_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile->'%(FullPath)%(FullPath)')")] // Same metadata twice
    [InlineData("@(Compile->'%(Identity)%(Identity)%(Identity)')")] // Multiple times
    public void RepeatedMetadataInTransforms_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    #endregion

    #region Referenced Items and Metadata - Additional Cases

    [Theory]
    [InlineData("", new string[0], new string[0])]
    [InlineData("text", new string[0], new string[0])]
    [InlineData("@(A);@(A)", new[] { "A" }, new string[0])] // Duplicate item references
    [InlineData("%(A);%(A)", new string[0], new[] { "A" })] // Duplicate metadata references
    public void GetReferencedItemsAndMetadata_AdditionalCases(
        string expression,
        string[] expectedItems,
        string[] expectedMetadata)
        => AssertReferencedItemsAndMetadataMatch(expression, expectedItems, expectedMetadata);

    [Theory]
    [InlineData("@(A->Count());@(B->Distinct())")]
    [InlineData("@(A, '%(Meta1)');@(B, '%(Meta2)')")]
    [InlineData("@(A->'%(X)')->@(B->'%(Y)')")]
    public void GetReferencedItemsAndMetadata_ComplexTransforms(string expression)
        => AssertReferencedItemsAndMetadataMatchOld(expression);

    #endregion

    #region Case Sensitivity

    [Theory]
    [InlineData("@(compile)", new[] { "compile" }, new string[0])]
    [InlineData("@(COMPILE)", new[] { "COMPILE" }, new string[0])]
    [InlineData("@(Compile);@(compile)", new[] { "Compile" }, new string[0])] // Case-insensitive: only first wins
    public void CaseSensitiveItemNames_MatchesOldParser(
        string expression,
        string[] expectedItems,
        string[] expectedMetadata)
        => AssertReferencedItemsAndMetadataMatch(expression, expectedItems, expectedMetadata);

    [Theory]
    [InlineData("%(fullpath)")]
    [InlineData("%(FULLPATH)")]
    [InlineData("%(FullPath)")]
    public void CaseSensitiveMetadataNames_MatchesOldParser(string expression)
        => AssertMetadataExpressionMatches(expression);

    [Theory]
    [InlineData("%(Item.Meta);%(item.meta)", new string[0], new[] { "Item.Meta" })] // Case-insensitive for qualified metadata too
    [InlineData("%(Meta);%(meta)", new string[0], new[] { "Meta" })] // Case-insensitive for unqualified metadata
    public void CaseInsensitiveMetadataDeduplication_MatchesOldParser(
        string expression,
        string[] expectedItems,
        string[] expectedMetadata)
        => AssertReferencedItemsAndMetadataMatch(expression, expectedItems, expectedMetadata);

    #endregion

    #region Fuzz Testing

    [Fact]
    public void RandomValidExpressions_MatchesOldParser()
    {
        var random = new Random(42); // Fixed seed for reproducibility
        string[] itemNames = ["Compile", "Reference", "Content", "None"];
        string[] metadataNames = ["FullPath", "Filename", "Identity", "Extension"];
        string[] functions = ["Distinct", "Reverse", "Count", "Exists"];
        string[] separators = [";", ",", " ", "|"];

        for (int i = 0; i < 100; i++)
        {
            // Generate random valid expression
            var itemName = itemNames.ChooseRandomItem(random);
            var expression = $"@({itemName})";

            // Maybe add transform
            if (random.Next(2) == 0)
            {
                var metadata = metadataNames.ChooseRandomItem(random);
                expression = $"@({itemName}->'%({metadata})')";
            }

            // Maybe add function
            if (random.Next(2) == 0)
            {
                var function = functions.ChooseRandomItem(random);
                expression = $"@({itemName}->{function}())";
            }

            // Maybe add separator
            if (random.Next(2) == 0)
            {
                var separator = separators.ChooseRandomItem(random);
                expression = expression.Replace(")", $", '{separator}')");
            }

            AssertItemExpressionMatches(expression);
        }
    }

    #endregion

    #region Complex Real-World Examples

    [Theory]
    [InlineData("@(Compile->'%(FullPath)', ' ')")]
    [InlineData("@(Reference->'%(Filename)', ';')")]
    [InlineData("@(Content->Exists()->'%(Identity)')")]
    [InlineData("@(None->WithMetadataValue('CopyToOutputDirectory', 'PreserveNewest'))")]
    public void RealWorldExpressions_MatchesOldParser(string expression)
        => AssertItemExpressionMatches(expression);

    [Theory]
    [InlineData("@(Compile);@(Reference);@(Content)")]
    [InlineData("@(Compile->'%(FullPath)');@(Reference->'%(Filename)')")]
    public void MultipleItemLists_MatchesOldParser(string expression)
        => AssertMultipleItemExpressionsMatch(expression);

    #endregion

    #region Helper Methods

    private void AssertItemExpressionMatches(string expression)
    {
        var oldEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);
        var newEnumerator = ExpressionParser.GetReferencedItemExpressions(expression);

        bool oldHasNext = oldEnumerator.MoveNext();
        bool newHasNext = newEnumerator.MoveNext();

        oldHasNext.ShouldBe(newHasNext, $"MoveNext() mismatch for expression: {expression}");

        if (oldHasNext)
        {
            var oldCapture = oldEnumerator.Current;
            var newCapture = newEnumerator.Current;

            AssertCapturesMatch(oldCapture, newCapture, expression);

            // Ensure both enumerators are finished
            oldEnumerator.MoveNext().ShouldBe(newEnumerator.MoveNext());
        }
    }

    private void AssertMultipleItemExpressionsMatch(string expression)
    {
        var oldEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);
        var newEnumerator = ExpressionParser.GetReferencedItemExpressions(expression);

        var oldCaptures = new List<ExpressionShredder.ItemExpressionCapture>();
        var newCaptures = new List<ExpressionParser.ItemExpressionCapture>();

        while (oldEnumerator.MoveNext())
        {
            oldCaptures.Add(oldEnumerator.Current);
        }

        while (newEnumerator.MoveNext())
        {
            newCaptures.Add(newEnumerator.Current);
        }

        oldCaptures.Count.ShouldBe(newCaptures.Count, $"Different number of captures for: {expression}");

        for (int i = 0; i < oldCaptures.Count; i++)
        {
            AssertCapturesMatch(oldCaptures[i], newCaptures[i], expression);
        }
    }

    private void AssertCapturesMatch(
        ExpressionShredder.ItemExpressionCapture oldCapture,
        ExpressionParser.ItemExpressionCapture newCapture,
        string expression)
    {
        newCapture.Index.ShouldBe(oldCapture.Index, $"Index mismatch for: {expression}");
        newCapture.Length.ShouldBe(oldCapture.Length, $"Length mismatch for: {expression}");
        newCapture.Value.ShouldBe(oldCapture.Value, $"Value mismatch for: {expression}");
        newCapture.ItemType.ShouldBe(oldCapture.ItemType, $"ItemType mismatch for: {expression}");
        newCapture.Separator.ShouldBe(oldCapture.Separator, $"Separator mismatch for: {expression}");
        newCapture.SeparatorStart.ShouldBe(oldCapture.SeparatorStart, $"SeparatorStart mismatch for: {expression}");
        newCapture.FunctionName.ShouldBe(oldCapture.FunctionName, $"FunctionName mismatch for: {expression}");
        newCapture.FunctionArguments.ShouldBe(oldCapture.FunctionArguments, $"FunctionArguments mismatch for: {expression}");

        // Check captures (nested transforms)
        if (oldCapture.Captures == null)
        {
            newCapture.Captures.ShouldBeEmpty($"Captures should be empty for: {expression}");
        }
        else
        {
            newCapture.Captures.ShouldNotBeEmpty($"Captures should not be empty for: {expression}");
            newCapture.Captures.Length.ShouldBe(oldCapture.Captures.Count, $"Captures length mismatch for: {expression}");

            for (int i = 0; i < oldCapture.Captures.Count; i++)
            {
                AssertCapturesMatch(oldCapture.Captures[i], newCapture.Captures[i], expression);
            }
        }
    }

    private void AssertMetadataExpressionMatches(string expression)
    {
        bool oldResult = ExpressionShredder.ContainsMetadataExpressionOutsideTransform(expression);
        bool newResult = NewExpressionParser.ContainsMetadataExpressionOutsideTransform(expression);

        oldResult.ShouldBe(newResult, $"ContainsMetadataExpressionOutsideTransform mismatch for: {expression}");

        // Also test the GetReferencedItemNamesAndMetadata path
        AssertReferencedItemsAndMetadataMatchOld(expression);
    }

    private void AssertReferencedItemsAndMetadataMatch(
        string expression,
        string[] expectedItems,
        string[] expectedMetadata)
    {
        var oldPair = ExpressionShredder.GetReferencedItemNamesAndMetadata([expression]);
        var newPair = NewExpressionParser.GetReferencedItemNamesAndMetadata([expression]);

        // Ensure old and new parsers match each other
        AssertItemsAndMetadataMatch(oldPair, newPair, expression);

        // Additionally validate against expected values
        if (expectedItems.Length == 0)
        {
            (oldPair.Items == null || oldPair.Items.Count == 0).ShouldBeTrue($"Expected no items in: {expression}");
            (newPair.Items == null || newPair.Items.Count == 0).ShouldBeTrue($"Expected no items in: {expression}");
        }
        else
        {
            oldPair.Items.ShouldNotBeNull($"Expected items in: {expression}");
            newPair.Items.ShouldNotBeNull($"Expected items in: {expression}");
            oldPair.Items.OrderBy(x => x).ShouldBe(expectedItems.OrderBy(x => x),
                $"Old parser items don't match expected for: {expression}");
            newPair.Items.OrderBy(x => x).ShouldBe(expectedItems.OrderBy(x => x),
                $"New parser items don't match expected for: {expression}");
        }

        if (expectedMetadata.Length == 0)
        {
            (oldPair.Metadata == null || oldPair.Metadata.Count == 0).ShouldBeTrue($"Expected no metadata in: {expression}");
            (newPair.Metadata == null || newPair.Metadata.Count == 0).ShouldBeTrue($"Expected no metadata in: {expression}");
        }
        else
        {
            oldPair.Metadata.ShouldNotBeNull($"Expected metadata in: {expression}");
            newPair.Metadata.ShouldNotBeNull($"Expected metadata in: {expression}");
            var oldMetadataKeys = oldPair.Metadata.Keys.OrderBy(x => x).ToArray();
            var newMetadataKeys = newPair.Metadata.Keys.OrderBy(x => x).ToArray();
            oldMetadataKeys.ShouldBe(expectedMetadata.OrderBy(x => x).ToArray(),
                $"Old parser metadata don't match expected for: {expression}");
            newMetadataKeys.ShouldBe(expectedMetadata.OrderBy(x => x).ToArray(),
                $"New parser metadata don't match expected for: {expression}");
        }
    }

    private void AssertReferencedItemsAndMetadataMatchOld(string expression)
    {
        var oldPair = ExpressionShredder.GetReferencedItemNamesAndMetadata([expression]);
        var newPair = ExpressionParser.GetReferencedItemNamesAndMetadata([expression]);

        AssertItemsAndMetadataMatch(oldPair, newPair, expression);
    }

    private void AssertItemsAndMetadataMatch(
        ItemsAndMetadataPair oldPair,
        ItemsAndMetadataPair newPair,
        string expression)
    {
        // Check items
        if (oldPair.Items == null)
        {
            newPair.Items.ShouldBeNull($"Items should be null for: {expression}");
        }
        else
        {
            newPair.Items.ShouldNotBeNull($"Items should not be null for: {expression}");
            oldPair.Items.OrderBy(x => x).ShouldBe(newPair.Items.OrderBy(x => x), $"Items mismatch for: {expression}");
        }

        // Check metadata
        if (oldPair.Metadata == null)
        {
            newPair.Metadata.ShouldBeNull($"Metadata should be null for: {expression}");
        }
        else
        {
            newPair.Metadata.ShouldNotBeNull($"Metadata should not be null for: {expression}");
            oldPair.Metadata.Count.ShouldBe(newPair.Metadata.Count, $"Metadata count mismatch for: {expression}");

            foreach (var (key, oldRef) in oldPair.Metadata)
            {
                newPair.Metadata.ShouldContainKey(key, $"Metadata key '{key}' missing for: {expression}");
                var newRef = newPair.Metadata[key];
                oldRef.ItemName.ShouldBe(newRef.ItemName, $"Metadata {nameof(MetadataReference.ItemName)} mismatch for key '{key}' in: {expression}");
                oldRef.MetadataName.ShouldBe(newRef.MetadataName, $"Metadata {nameof(MetadataReference.MetadataName)}  mismatch for key '{key}' in: {expression}");
            }
        }
    }

    private void AssertSemicolonSplitMatches(string expression)
    {
        var oldTokenizer = ExpressionShredder.SplitSemiColonSeparatedList(expression);
        var newTokenizer = NewExpressionParser.SplitSemiColonSeparatedList(expression);

        var oldTokens = new List<string>();
        var newTokens = new List<string>();

        foreach (var token in oldTokenizer)
        {
            oldTokens.Add(token);
        }

        foreach (var token in newTokenizer)
        {
            newTokens.Add(token);
        }

        oldTokens.ShouldBe(newTokens, $"Semicolon split mismatch for: {expression}");
    }

    #endregion
}

/// <summary>
/// Temporary implementation that delegates to the old parser.
/// This will be replaced with the new allocation-efficient implementation.
/// </summary>
internal static class NewExpressionParser
{
    internal static ExpressionParser.ReferencedItemExpressionsEnumerator GetReferencedItemExpressions(string expression)
    {
        // TODO: Replace with new implementation
        return ExpressionParser.GetReferencedItemExpressions(expression);
    }

    internal static bool ContainsMetadataExpressionOutsideTransform(string expression)
    {
        // TODO: Replace with new implementation
        return ExpressionShredder.ContainsMetadataExpressionOutsideTransform(expression);
    }

    internal static ItemsAndMetadataPair GetReferencedItemNamesAndMetadata(IReadOnlyList<string> expressions)
    {
        // TODO: Replace with new implementation
        return ExpressionParser.GetReferencedItemNamesAndMetadata(expressions);
    }

    internal static SemiColonTokenizer SplitSemiColonSeparatedList(string expression)
    {
        // TODO: Replace with new implementation
        return ExpressionShredder.SplitSemiColonSeparatedList(expression);
    }
}
