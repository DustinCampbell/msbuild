// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
/// Compares the items and metadata that ExpressionParser.finds
/// with the results from the old regexes to make sure they're identical
/// in every case.
/// </summary>
public class ExpressionShredderToParser_Tests
{
    public static readonly TheoryData<string> MedleyTestData =
    [
        "a;@(foo,');');b",
        "x@(z);@(zz)y",
        "exists('@(u)')",
        "a;b",
        "a;;",
        "a",
        "@A->'%(x)'",
        "@@(",
        "@@",
        "@(z1234567890_-AZaz->'z1234567890_-AZaz','a1234567890_-AZaz')",
        "@(z1234567890_-AZaz,'a1234567890_-AZaz')",
        "@(z1234567890_-AZaz)",
        "@(z1234567890_-AXZaxz  -> '%(a1234567890_-AXZaxz).%(adfas)'   )",
        "@(z123456.7890_-AXZaxz  -> '%(a1234567890_-AXZaxz).%(adfas)'  )",
        "@(z->'%(x)",
        "@(z->%(x)",
        "@(z,'%(x)",
        "@(z,%(x)",
        "@(z) and true",
        "@(z%(x)",
        "@(z -> '%(filename).z', '$')=='xxx.z$yyy.z'",
        "@(z -> '%(filename)', '!')=='xxx!yyy'",
        "@(y)==$(d)",
        "@(y)<=1",
        "@(y -> '%(filename)')=='xxx'",
        "@(x\u00DF)",
        "@(x1234567890_-AZaz->'x1234567890_-AZaz')",
        "@(x1234567890_-AZaz)",
        "@(x123 4567890_-AZaz->'x1234567890_-AZaz')",
        "@(x->)",
        "@(x->'x','')",
        "@(x->'x',''",
        "@(x->'x','",
        "@(x->')",
        "@(x->''",
        "@(x->'",
        "@(x->",
        "@(x-",
        "@(x,')",
        "@(x)@(x)",
        "@(x)<x",
        "@(x);@(x)",
        "@(x)",
        "@(x''';",
        "@(x",
        "@(x!)",
        "@(w)>0",
        "@(nonexistent)",
        "@(nonexistent) and true",
        "@(foo->'x')",
        "@(foo->'abc;def', 'ghi;jkl')",
        "@(foo->';());', ';@();')",
        "@(foo->';');def;@ghi;",
        "@(foo->';')",
        "@(foo-->'x')", // "foo-" is a legit item type
        "@(foo, ';')",
        "@(a1234:567890_-AZaz->'z1234567890_-AZaz')",
        "@(a1234567890_-AZaz->'z1234567890_-AZaz')",
        "@(a1234567890_-AXZaxz  -> 'a1234567890_-AXZaxz'   ,  'z1234567890_-AXZaxz'   )",
        "@(a1234567890_-AXZaxz  , 'z123%%4567890_-AXZaxz'   )",
        "@(a->'a')",
        "@(a->'a'  ,  'a')",
        "@(a)@(x)!=1",
        "@(a)",
        "@(a) @(x)!=1",
        "@(a  ,  'a')",
        "@(_X->'_X','X')",
        "@(_X->'_X')",
        "@(_X,'X')",
        "@(_X)",
        "@(_->'@#$%$%^&*&*)','@#$%$%^&*&*)')",
        "@(_->'@#$%$%^&*&*)')",
        "@(_,'@#$%$%^&*&*)')",
        "@(_)",
        "@(\u1234%(x)",
        "@(\u00DF)",
        "@(Z1234567890_-AZaz)",
        "@(Z1234567890_-AZaz -> 'Z1234567890_-AZaz')",
        "@(Com:pile)",
        "@(Com.pile)",
        "@(Com%pile)",
        "@(Com pile)",
        "@(A1234567890_-AZaz,'!@#$%^&*)(_+'))",
        "@(A1234567890_-AZaz)",
        "@(A1234567890_-AZaz ->'A1234567890_-AZaz')",
        "@(A1234567890_-AZaz ->'A1234567890_-AZaz' , '!@#$%^&*)(_+'))",
        "@(A->'foo%(x)bar',',')",
        "@(A->'%(x))",
        "@(A->'%(x)')@(B->'%(x);%(y)')@(C->'%(z)')",
        "@(A->'%(x)');@(B->'%(x);%(y)');;@(C->'%(z)')",
        "@(A->'%(x)')",
        "@(A->%(x))",
        "@(A,'%(x)')",
        "@(A, '%(x)->%(y)')",
        "@(A, '%(x)%(y)')",
        "@(A > '%(x)','+')",
        "@(:Z1234567890_-AZaz -> 'Z1234567890_-AZaz')",
        "@(:Compile)",
        "@(1x->'@#$%$%^&*&*)')",
        "@(1Compile)",
        "@(1->'a')",
        "@(.Compile)",
        "@(.A1234567890_-AZaz ->'A1234567890_-AZaz')",
        "@(-x->'_X')",
        "@(-Compile)",
        "@()",
        "@() and true",
        "@(%Compile)",
        "@(%(x)",
        "@(",
        "@",
        "@( foo -> ';);' , ';);' )",
        "@( foo -> ');' )",
        "@( A -> '%(Directory)%(Filename)%(Extension)', ' ** ')",
        "@( )",
        "@(   foo  )",
        "@(   foo  ",
        "@(   a1234567890_-AXZaxz   )",
        "@ (x)",
        "@(x,'@(y)%(x)@(z->')",
        "@(x,'@(y)')",   // verify items inside separators aren't found
        "@(x,'@(y, '%(z)')')",
        "@(x,'@(y)%(z)')",
        "@(x,'@(y)%(x')",
        "@(x,'')",
        "@(x->'','')",
        "@(x->'%(z)','')",
        ";a;bbb;;c;;",
        ";;a",
        ";;;@(A->'%(x)');@(B)@(C->'%(y)');%(x)@(D->'%(y)');;",
        ";;",
        ";",
        ";  ",
        "1<=@(z)",
        "1<=@(w)",
        "'xxx!yyy'==@(z -> '%(filename)', '!')",
        "'@(z)'=='xxx;yyy'",
        "'$(e)1@(y)'=='xxx1xxx'",
        "'$(c)@(y)'>1",
        "%x)",
        "%x",
        "%(z1234567890_-AZaz.z1234567890_-AZaz)",
        "%(z1234567890_-AZaz)",
        "%(x1234567890_-AZaz.x1234567890_-AZaz)",
        "%(x1234567890_-AZaz)",
        "%(x._)",
        "%(x)",
        "%(x",
        "%(x )",
        "%(foo.goo.baz)",
        "%(foo.goo baz)",
        "%(foo goo.rhu barb)",
        "%(abc._X)",
        "%(a@(z)",
        "%(a1234567890_-AXZaxz)",
        "%(a12.a)",
        "%(a.x)",
        "%(a.x )",
        "%(a.a@(z)",
        "%(a.@(z)",
        "%(a. x)",
        "%(a)",
        "%(a . x)",
        "%(_X)",
        "%(_)",
        "%(Z1234567890_-AZaz.Z1234567890_-AZaz)",
        "%(Z1234567890_-AZaz)",
        "%(MyType.attr)",
        "%(InvalidAttrWithA Space)",
        "%(Foo.Bar.)",
        "%(Compile.)",
        "%(Com:pile.Com:pile)",
        "%(Com:pile)",
        "%(Com.pile.Com.pile)",
        "%(Com%pile.Com%pile)",
        "%(Com%pile)",
        "%(Com pile.Com pile)",
        "%(Com pile)",
        "%(A1234567890_-AZaz.A1234567890_-AZaz)",
        "%(A1234567890_-AZaz)",
        "%(A.x)%(b.x)",
        "%(A.x)",
        "%(A.x)  %( x )",
        "%(A.)",
        "%(A. )",
        "%(A .x)",
        "%(A .)",
        "%(A . )",
        "%(@(z)",
        "%(:Compile.:Compile)",
        "%(:Compile)",
        "%(1Compile.1Compile)",
        "%(1Compile)",
        "%(.x)",
        "%(.x )",
        "%(.foo.bar)",
        "%(.Compile)",
        "%(.)",
        "%(. x)",
        "%(. x )",
        "%(-Compile.-Compile)",
        "%(-Compile)",
        "%()",
        "%(%Compile.%Compile)",
        "%(%Compile)",
        "%( x)",
        "%( MyType . attr  )",
        "%( A.x)",
        "%( A.x )",
        "%( A.)",
        "%( A .)",
        "%( A . x )",
        "%( .x)",
        "%( . x)",
        "%( . x )",
        "%( )",
        "%(  foo  )",
        "%(  Invalid AttrWithASpace  )",
        "%(  A  .  )",
        "%(   x   )",
        "%(   a1234567890_-AXZaxz.a1234567890_-AXZaxz   )",
        "% x",
        "% (x)",
        "$(c)@(y)>1",
        "",
        "!@#$%^&*",
        " @(foo->'', '')",
        " ->       ';abc;def;'   ,     'ghi;jkl'   )",
        " %(A . x)%%%%%%%%(b . x) ",
        "  ;  a   ;b   ;   ;c",
        "                $(AssemblyOriginatorKeyFile);\n\t                @(Compile);",
        "@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);"
    ];

    [Theory]
    [MemberData(nameof(MedleyTestData))]
    public void Medley(string expression)
    {
        VerifyExpression(expression);
    }

    [Fact]
    public void NoOpSplit()
    {
        VerifySplitSemiColonSeparatedList("a", "a");
    }

    [Fact]
    public void BasicSplit()
    {
        VerifySplitSemiColonSeparatedList("a;b", "a", "b");
    }

    [Fact]
    public void Empty()
    {
        VerifySplitSemiColonSeparatedList("");
    }

    [Fact]
    public void SemicolonOnly()
    {
        VerifySplitSemiColonSeparatedList(";");
    }

    [Fact]
    public void TwoSemicolons()
    {
        VerifySplitSemiColonSeparatedList(";;");
    }

    [Fact]
    public void TwoSemicolonsAndOneEntryAtStart()
    {
        VerifySplitSemiColonSeparatedList("a;;", "a");
    }

    [Fact]
    public void TwoSemicolonsAndOneEntryAtEnd()
    {
        VerifySplitSemiColonSeparatedList(";;a", "a");
    }

    [Fact]
    public void AtSignAtEnd()
    {
        VerifySplitSemiColonSeparatedList("@", "@");
    }

    [Fact]
    public void AtSignParenAtEnd()
    {
        VerifySplitSemiColonSeparatedList("foo@(", "foo@(");
    }

    [Fact]
    public void EmptyEntriesRemoved()
    {
        VerifySplitSemiColonSeparatedList(";a;bbb;;c;;", "a", "bbb", "c");
    }

    [Fact]
    public void EntriesTrimmed()
    {
        VerifySplitSemiColonSeparatedList("  ;  a   ;b   ;   ;c\n;  \r;  ", "a", "b", "c");
    }

    [Fact]
    public void NoSplittingOnMacros()
    {
        VerifySplitSemiColonSeparatedList("@(foo->';')", "@(foo->';')");
    }

    [Fact]
    public void NoSplittingOnSeparators()
    {
        VerifySplitSemiColonSeparatedList("@(foo, ';')", "@(foo, ';')");
    }

    [Fact]
    public void NoSplittingOnSeparatorsAndMacros()
    {
        VerifySplitSemiColonSeparatedList("@(foo->'abc;def', 'ghi;jkl')", "@(foo->'abc;def', 'ghi;jkl')");
    }

    [Fact]
    public void CloseParensInMacro()
    {
        VerifySplitSemiColonSeparatedList("@(foo->');')", "@(foo->');')");
    }

    [Fact]
    public void CloseParensInSeparator()
    {
        VerifySplitSemiColonSeparatedList("a;@(foo,');');b", "a", "@(foo,');')", "b");
    }

    [Fact]
    public void CloseParensInMacroAndSeparator()
    {
        VerifySplitSemiColonSeparatedList("@(foo->';);', ';);')", "@(foo->';);', ';);')");
    }

    [Fact]
    public void EmptyQuotesInMacroAndSeparator()
    {
        VerifySplitSemiColonSeparatedList(" @(foo->'', '')", "@(foo->'', '')");
    }

    [Fact]
    public void MoreParensAndAtSigns()
    {
        VerifySplitSemiColonSeparatedList("@(foo->';());', ';@();')", "@(foo->';());', ';@();')");
    }

    [Fact]
    public void SplittingExceptForMacros()
    {
        VerifySplitSemiColonSeparatedList("@(foo->';');def;@ghi;", "@(foo->';')", "def", "@ghi");
    }

    // Invalid item expressions shouldn't cause an error in the splitting function.
    // The caller will emit an error later when it tries to parse the results.
    [Fact]
    public void InvalidItemExpressions()
    {
        VerifySplitSemiColonSeparatedList("@(x", "@(x");
        VerifySplitSemiColonSeparatedList("@(x->')", "@(x->')");
        VerifySplitSemiColonSeparatedList("@(x->)", "@(x->)");
        VerifySplitSemiColonSeparatedList("@(x->''", "@(x->''");
        VerifySplitSemiColonSeparatedList("@(x->)", "@(x->)");
        VerifySplitSemiColonSeparatedList("@(x->", "@(x->");
        VerifySplitSemiColonSeparatedList("@(x,')", "@(x,')");

        // This one doesn't remove the ';' because it thinks it's in
        // an item list. This isn't worth tweaking, because the invalid expression is
        // going to lead to an error in the caller whether there's a ';' or not.
        VerifySplitSemiColonSeparatedList("@(x''';", "@(x''';");
    }

    [Fact]
    public void RealisticExample()
    {
        VerifySplitSemiColonSeparatedList("@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);\n                @(Compile);\n                @(ManifestResourceWithNoCulture);\n                $(ApplicationIcon);\n                $(AssemblyOriginatorKeyFile);\n                @(ManifestNonResxWithNoCultureOnDisk);\n                @(ReferencePath);\n                @(CompiledLicenseFile);\n                @(EmbeddedDocumentation);                \n                @(CustomAdditionalCompileInputs)",
            "@(_OutputPathItem->'%(FullPath)', ';')", "$(MSBuildAllProjects)", "@(Compile)", "@(ManifestResourceWithNoCulture)", "$(ApplicationIcon)", "$(AssemblyOriginatorKeyFile)", "@(ManifestNonResxWithNoCultureOnDisk)", "@(ReferencePath)", "@(CompiledLicenseFile)", "@(EmbeddedDocumentation)", "@(CustomAdditionalCompileInputs)");
    }

    // For reference, this is the authoritative definition of an item expression:
    //  @"@\(\s*
    //      (?<TYPE>[\w\x20-]*[\w-]+)
    //      (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
    //      (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
    //  \s*\)";
    // We need to support any item expressions that satisfy this expression.
    //
    // Try spaces everywhere that regex allows spaces:
    [Fact]
    public void SpacingInItemListExpression()
    {
        VerifySplitSemiColonSeparatedList("@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )", "@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )");
    }

    /// <summary>
    /// Helper method for SplitSemiColonSeparatedList tests
    /// </summary>
    /// <param name="input"></param>
    /// <param name="expected"></param>
    private void VerifySplitSemiColonSeparatedList(string input, params string[] expected)
    {
        var actual = ExpressionParser.SplitSemiColonSeparatedList(input);
        Console.WriteLine(input);

        // passing "null" means you expect an empty array back
        expected ??= [];

        int index = 0;
        var enumerator = actual.GetEnumerator();

        while (enumerator.MoveNext())
        {
            Assert.Equal(expected[index++], enumerator.Current, StringComparer.Ordinal);
        }
    }

    private void VerifyExpression(string test)
    {
        List<string> list = new List<string>();
        list.Add(test);
        ItemsAndMetadataPair pair = ExpressionParser.GetReferencedItemNamesAndMetadata(list);

        HashSet<string> actualItems = pair.Items;
        Dictionary<string, MetadataReference> actualMetadata = pair.Metadata;

        HashSet<string> expectedItems = GetConsumedItemReferences_OriginalImplementation(test);
        Console.WriteLine("verifying item names...");
        VerifyAgainstCanonicalResults(test, actualItems, expectedItems);

        Dictionary<string, MetadataReference> expectedMetadata = GetConsumedMetadataReferences_OriginalImplementation(test);
        Console.WriteLine("verifying metadata ...");
        VerifyAgainstCanonicalResults(test, actualMetadata, expectedMetadata);

        Console.WriteLine("===OK===");
    }

    private static void VerifyAgainstCanonicalResults(string test, HashSet<string> actual, HashSet<string> expected)
    {
        List<string> messages = new List<string>();

        Console.WriteLine("Expecting " + expected.Count + " distinct values for <" + test + ">");

        if (actual != null)
        {
            foreach (string result in actual)
            {
                if (expected?.Contains(result) != true)
                {
                    messages.Add("Found <" + result + "> in <" + test + "> but it wasn't expected");
                }
            }
        }

        if (expected != null)
        {
            foreach (string expect in expected)
            {
                if (actual?.Contains(expect) != true)
                {
                    messages.Add("Did not find <" + expect + "> in <" + test + ">");
                }
            }
        }

        if (messages.Count > 0)
        {
            if (actual != null)
            {
                Console.Write("FOUND: ");
                foreach (string result in actual)
                {
                    Console.Write("<" + result + "> ");
                }
                Console.WriteLine();
            }
        }

        foreach (string message in messages)
        {
            Console.WriteLine(message);
        }

        Assert.Empty(messages);
    }

    private static void VerifyAgainstCanonicalResults(string test, IDictionary actual, IDictionary expected)
    {
        List<string> messages = new List<string>();

        Console.WriteLine("Expecting " + expected.Count + " distinct values for <" + test + ">");

        if (actual != null)
        {
            foreach (DictionaryEntry result in actual)
            {
                if (expected?.Contains(result.Key) != true)
                {
                    messages.Add("Found <" + result.Key + "> in <" + test + "> but it wasn't expected");
                }
            }
        }

        if (expected != null)
        {
            foreach (DictionaryEntry expect in expected)
            {
                if (actual?.Contains(expect.Key) != true)
                {
                    messages.Add("Did not find <" + expect.Key + "> in <" + test + ">");
                }
            }
        }

        if (messages.Count > 0)
        {
            if (actual != null)
            {
                Console.Write("FOUND: ");
                foreach (string result in actual.Keys)
                {
                    Console.Write("<" + result + "> ");
                }
                Console.WriteLine();
            }
        }

        foreach (string message in messages)
        {
            Console.WriteLine(message);
        }

        Assert.Empty(messages);
    }

    [Fact]
    public void ExtractItemVectorTransform1()
    {
        string expression = "@(i->'%(Meta0)'->'%(Filename)'->Substring($(Val)))";
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());

        ExpressionParser.ItemExpressionCapture capture = expressions.Current;

        Assert.False(expressions.MoveNext());
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("i", capture.ItemType.ToString());
        Assert.Equal("%(Meta0)", capture.Captures[0].Value.ToString());
        Assert.Equal("%(Filename)", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring($(Val))", capture.Captures[2].Value.ToString());
    }

    /// <summary>
    /// Compare the results of the expression shredder based item expression extractor with the original regex based one
    /// NOTE: The medley of tests needs to be parsable by the old regex. This is a regression test against that
    /// regex. New expression types should be added in other tests
    /// </summary>
    [Theory]
    [MemberData(nameof(MedleyTestData))]
    public void ItemExpressionMedleyRegressionTestAgainstOldRegex(string expression)
    {
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        MatchCollection matches = s_itemVectorPattern.Matches(expression);
        int expressionCount = 0;

        while (expressions.MoveNext())
        {
            Match match = matches[expressionCount];
            ExpressionParser.ItemExpressionCapture capture = expressions.Current;

            Assert.Equal(match.Value, capture.Value.ToString());

            Group transformGroup = match.Groups["TRANSFORM"];

            if (capture.Captures != null)
            {
                for (int i = 0; i < transformGroup.Captures.Count; i++)
                {
                    Assert.Equal(transformGroup.Captures[i].Value.ToString(), capture.Captures[i].Value.ToString());
                }
            }
            else
            {
                Assert.Equal(0, transformGroup.Length);
            }

            ++expressionCount;
        }

        if (expressionCount == 0)
        {
            Assert.Empty(matches);
        }
        else
        {
            Assert.Equal(matches.Count, expressionCount);
        }
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpressionInvalid1()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;

        expression = "@(type-&gt;'%($(a)), '%'')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.False(expressions.MoveNext());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression1()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo)";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.True(capture.Separator.IsEmpty);
        Assert.True(capture.Captures.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression2()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo, ';')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.True(capture.Captures.IsEmpty);
        Assert.Equal(";", capture.Separator.ToString());
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.True(capture.Captures.IsEmpty);
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression3()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Fullpath)')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Single(capture.Captures);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Single(capture.Captures);
        Assert.Equal("%(Fullpath)", capture.Captures[0].Value.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression4()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Fullpath)',';')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Single(capture.Captures);
        Assert.Equal(";", capture.Separator.ToString());
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Single(capture.Captures);
        Assert.Equal("%(Fullpath)", capture.Captures[0].Value.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression5()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->Bar(a,b))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Single(capture.Captures);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Single(capture.Captures);
        Assert.Equal("Bar(a,b)", capture.Captures[0].Value.ToString());
        Assert.Equal("Bar", capture.Captures[0].FunctionName.ToString());
        Assert.Equal("a,b", capture.Captures[0].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression6()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->Bar(a,b),';')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Single(capture.Captures);
        Assert.Equal(";", capture.Separator.ToString());
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Single(capture.Captures);
        Assert.Equal("Bar(a,b)", capture.Captures[0].Value.ToString());
        Assert.Equal("Bar", capture.Captures[0].FunctionName.ToString());
        Assert.Equal("a,b", capture.Captures[0].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression7()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->Metadata('Meta0')->Directory())";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("Metadata('Meta0')", capture.Captures[0].Value.ToString());
        Assert.Equal("Metadata", capture.Captures[0].FunctionName.ToString());
        Assert.Equal("'Meta0'", capture.Captures[0].FunctionArguments.ToString());
        Assert.Equal("Directory()", capture.Captures[1].Value.ToString());
        Assert.Equal("Directory", capture.Captures[1].FunctionName.ToString());
        Assert.True(capture.Captures[1].FunctionArguments.IsEmpty);
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression8()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->Metadata('Meta0')->Directory(),';')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.Equal(";", capture.Separator.ToString());
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("Metadata('Meta0')", capture.Captures[0].Value.ToString());
        Assert.Equal("Metadata", capture.Captures[0].FunctionName.ToString());
        Assert.Equal("'Meta0'", capture.Captures[0].FunctionArguments.ToString());
        Assert.Equal("Directory()", capture.Captures[1].Value.ToString());
        Assert.Equal("Directory", capture.Captures[1].FunctionName.ToString());
        Assert.True(capture.Captures[1].FunctionArguments.IsEmpty);
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression9()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Fullpath)'->Directory(), '|')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.Equal("|", capture.Separator.ToString());
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Fullpath)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Directory()", capture.Captures[1].Value.ToString());
        Assert.Equal("Directory", capture.Captures[1].FunctionName.ToString());
        Assert.True(capture.Captures[1].FunctionArguments.IsEmpty);
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression10()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Fullpath)'->Directory(),';')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.Equal(";", capture.Separator.ToString());
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Fullpath)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Directory()", capture.Captures[1].Value.ToString());
        Assert.Equal("Directory", capture.Captures[1].FunctionName.ToString());
        Assert.True(capture.Captures[1].FunctionArguments.IsEmpty);
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression11()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'$(SOMEPROP)%(Fullpath)')";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Single(capture.Captures);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("$(SOMEPROP)%(Fullpath)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression12()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Filename)'->Substring($(Val), $(Boo)))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring($(Val), $(Boo))", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("$(Val), $(Boo)", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression13()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Filename)'->Substring(\"AA\", 'BB', `cc`))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring(\"AA\", 'BB', `cc`)", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("\"AA\", 'BB', `cc`", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression14()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Filename)'->Substring('()', $(Boo), ')('))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring('()', $(Boo), ')(')", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("'()', $(Boo), ')('", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression15()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Filename)'->Substring(`()`, $(Boo), \"AA\"))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring(`()`, $(Boo), \"AA\")", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("`()`, $(Boo), \"AA\"", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression16()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Filename)'->Substring(`()`, $(Boo), \")(\"))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring(`()`, $(Boo), \")(\")", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("`()`, $(Boo), \")(\"", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression17()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring(\"()\", $(Boo), `)(`)", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("\"()\", $(Boo), `)(`", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression1()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture firstCapture;
        ExpressionParser.ItemExpressionCapture capture;

        expression = "@(Bar);@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        firstCapture = expressions.Current;
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("Bar", firstCapture.ItemType.ToString());
        Assert.True(firstCapture.Captures.IsEmpty);
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring(\"()\", $(Boo), `)(`)", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("\"()\", $(Boo), `)(`", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression2()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture firstCapture;
        ExpressionParser.ItemExpressionCapture secondCapture;

        expression = "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`));@(Bar)";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        firstCapture = expressions.Current;
        Assert.True(expressions.MoveNext());
        secondCapture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("Bar", secondCapture.ItemType.ToString());
        Assert.True(secondCapture.Captures.IsEmpty);
        Assert.Equal(2, firstCapture.Captures.Length);
        Assert.True(firstCapture.Separator.IsEmpty);
        Assert.Equal("Foo", firstCapture.ItemType.ToString());
        Assert.Equal("%(Filename)", firstCapture.Captures[0].Value.ToString());
        Assert.True(firstCapture.Captures[0].FunctionName.IsEmpty);
        Assert.True(firstCapture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring(\"()\", $(Boo), `)(`)", firstCapture.Captures[1].Value.ToString());
        Assert.Equal("Substring", firstCapture.Captures[1].FunctionName.ToString());
        Assert.Equal("\"()\", $(Boo), `)(`", firstCapture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression3()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;
        ExpressionParser.ItemExpressionCapture secondCapture;

        expression = "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`));AAAAAA;@(Bar)";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.True(expressions.MoveNext());
        secondCapture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("Bar", secondCapture.ItemType.ToString());
        Assert.True(secondCapture.Captures.IsEmpty);
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring(\"()\", $(Boo), `)(`)", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("\"()\", $(Boo), `)(`", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression4()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;
        ExpressionParser.ItemExpressionCapture secondCapture;

        expression = "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(\"`));@(;);@(aaa->;b);@(bbb->'d);@(`Foo->'%(Filename)'->Distinct());@(Bar)";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.True(expressions.MoveNext());
        secondCapture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("Bar", secondCapture.ItemType.ToString());
        Assert.True(secondCapture.Captures.IsEmpty);
        Assert.Equal(2, capture.Captures.Length);
        Assert.True(capture.Separator.IsEmpty);
        Assert.Equal("Foo", capture.ItemType.ToString());
        Assert.Equal("%(Filename)", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
        Assert.True(capture.Captures[0].FunctionArguments.IsEmpty);
        Assert.Equal("Substring(\"()\", $(Boo), `)(\"`)", capture.Captures[1].Value.ToString());
        Assert.Equal("Substring", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("\"()\", $(Boo), `)(\"`", capture.Captures[1].FunctionArguments.ToString());
    }

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression5()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;

        expression = "@(foo);@(foo,'-');@(foo);@(foo,',');@(foo)";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);

        Assert.True(expressions.MoveNext());
        Assert.Equal("foo", expressions.Current.ItemType.ToString());
        Assert.True(expressions.Current.Separator.IsEmpty);

        Assert.True(expressions.MoveNext());
        Assert.Equal("foo", expressions.Current.ItemType.ToString());
        Assert.Equal("-", expressions.Current.Separator.ToString());

        Assert.True(expressions.MoveNext());
        Assert.Equal("foo", expressions.Current.ItemType.ToString());
        Assert.True(expressions.Current.Separator.IsEmpty);

        Assert.True(expressions.MoveNext());
        Assert.Equal("foo", expressions.Current.ItemType.ToString());
        Assert.Equal(",", expressions.Current.Separator.ToString());

        Assert.True(expressions.MoveNext());
        Assert.Equal("foo", expressions.Current.ItemType.ToString());
        Assert.True(expressions.Current.Separator.IsEmpty);

        Assert.False(expressions.MoveNext());
    }

    /// <summary>
    /// Test that item function chaining works with whitespace before arrow operators
    /// </summary>
    [Fact]
    public void ExtractItemVectorExpressionsChainedFunctionsWithWhitespace()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;
        ExpressionParser.ItemExpressionCapture capture;

        // Test with space before second arrow: ") ->"
        expression = "@(I -> WithMetadataValue('M', 'T') -> WithMetadataValue('M', 'T'))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("I", capture.ItemType.ToString());
        Assert.Equal(2, capture.Captures.Length);
        Assert.Equal("WithMetadataValue", capture.Captures[0].FunctionName.ToString());
        Assert.Equal("'M', 'T'", capture.Captures[0].FunctionArguments.ToString());
        Assert.Equal("WithMetadataValue", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("'M', 'T'", capture.Captures[1].FunctionArguments.ToString());

        // Test without space before second arrow: ")->"
        expression = "@(I -> WithMetadataValue('M', 'T')-> WithMetadataValue('M', 'T'))";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("I", capture.ItemType.ToString());
        Assert.Equal(2, capture.Captures.Length);
        Assert.Equal("WithMetadataValue", capture.Captures[0].FunctionName.ToString());
        Assert.Equal("'M', 'T'", capture.Captures[0].FunctionArguments.ToString());
        Assert.Equal("WithMetadataValue", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("'M', 'T'", capture.Captures[1].FunctionArguments.ToString());

        // Test with multiple spaces and chained functions
        expression = "@(I->Distinct() -> Reverse() ->Count())";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("I", capture.ItemType.ToString());
        Assert.Equal(3, capture.Captures.Length);
        Assert.Equal("Distinct", capture.Captures[0].FunctionName.ToString());
        Assert.Equal("Reverse", capture.Captures[1].FunctionName.ToString());
        Assert.Equal("Count", capture.Captures[2].FunctionName.ToString());

        // Test trailing whitespace after function call
        expression = "@(I -> Count() )";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("I", capture.ItemType.ToString());
        Assert.Equal(1, capture.Captures.Length);
        Assert.Equal("Count", capture.Captures[0].FunctionName.ToString());

        // Test trailing whitespace after quoted transform
        expression = "@(I -> 'Replacement' )";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        Assert.True(expressions.MoveNext());
        capture = expressions.Current;
        Assert.False(expressions.MoveNext());
        Assert.Equal("I", capture.ItemType.ToString());
        Assert.Equal(1, capture.Captures.Length);
        Assert.Equal("Replacement", capture.Captures[0].Value.ToString());
        Assert.True(capture.Captures[0].FunctionName.IsEmpty);
    }

    /// <summary>
    /// Test that invalid syntax after whitespace is properly rejected
    /// </summary>
    [Fact]
    public void ExtractItemVectorExpressionsInvalidSyntaxAfterWhitespace()
    {
        string expression;
        ExpressionParser.ReferencedItemExpressionsEnumerator expressions;

        // Invalid syntax after whitespace - should not be parsed as item expression
        expression = "@(I -> Count() invalid)";
        expressions = ExpressionParser.GetReferencedItemExpressions(expression);
        // Should not find a valid expression due to invalid syntax
        Assert.False(expressions.MoveNext());
    }

    #region Original code to produce canonical results

    /// <summary>
    /// Looks through the parameters of the batchable object, and finds all referenced item lists.
    /// Returns a hashtable containing the item lists, where the key is the item name, and the
    /// value is always String.Empty (not used).
    /// </summary>
    private static HashSet<string> GetConsumedItemReferences_OriginalImplementation(string expression)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match itemVector in s_itemVectorPattern.Matches(expression))
        {
            result.Add(itemVector.Groups["TYPE"].Value);
        }

        return result;
    }

    /// <summary>
    /// Looks through the parameters of the batchable object, and finds all references to item metadata
    /// (that aren't part of an item transform).  Returns a Hashtable containing a bunch of MetadataReference
    /// structs.  Each reference to item metadata may or may not be qualified with an item name (e.g.,
    /// %(Culture) vs. %(EmbeddedResource.Culture).
    /// </summary>
    /// <returns>Hashtable containing the metadata references.</returns>
    private static Dictionary<string, MetadataReference> GetConsumedMetadataReferences_OriginalImplementation(string expression)
    {
        // The keys in the hash table are the qualified metadata names (e.g. "EmbeddedResource.Culture"
        // or just "Culture").  The values are MetadataReference structs, which simply split out the item
        // name (possibly null) and the actual metadata name.
        Dictionary<string, MetadataReference> consumedMetadataReferences = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        FindEmbeddedMetadataReferences_OriginalImplementation(expression, consumedMetadataReferences);

        return consumedMetadataReferences;
    }

    /// <summary>
    /// Looks through a single parameter of the batchable object, and finds all references to item metadata
    /// (that aren't part of an item transform).  Populates a Hashtable containing a bunch of MetadataReference
    /// structs.  Each reference to item metadata may or may not be qualified with an item name (e.g.,
    /// %(Culture) vs. %(EmbeddedResource.Culture).
    /// </summary>
    /// <param name="batchableObjectParameter"></param>
    /// <param name="consumedMetadataReferences"></param>
    private static void FindEmbeddedMetadataReferences_OriginalImplementation(
        string batchableObjectParameter,
        Dictionary<string, MetadataReference> consumedMetadataReferences)
    {
        MatchCollection? embeddedMetadataReferences = FindEmbeddedMetadataReferenceMatches_OriginalImplementation(batchableObjectParameter);

        if (embeddedMetadataReferences != null)
        {
            foreach (Match embeddedMetadataReference in embeddedMetadataReferences)
            {
                string metadataName = embeddedMetadataReference.Groups["NAME"].Value;
                string qualifiedMetadataName = metadataName;

                // Check if the metadata is qualified with the item name.
                string? itemName = null;
                if (embeddedMetadataReference.Groups["ITEM_SPECIFICATION"].Length > 0)
                {
                    itemName = embeddedMetadataReference.Groups["TYPE"].Value;
                    qualifiedMetadataName = itemName + "." + metadataName;
                }

                consumedMetadataReferences[qualifiedMetadataName] = new MetadataReference(itemName, metadataName);
            }
        }
    }

    // the leading characters that indicate the start of an item vector
    private const string ItemVectorPrefix = "@(";

    // complete description of an item vector, including the optional transform expression and separator specification
    private const string ItemVectorSpecification =
        $@"@\(\s*
                (?<TYPE>{ProjectWriter.ItemTypeOrMetadataNameSpecification})
                (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
                (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
            \s*\)";

    // description of an item vector, including the optional transform expression, but not the separator specification
    private const string ItemVectorWithoutSeparatorSpecification =
        $@"@\(\s*
                (?<TYPE>{ProjectWriter.ItemTypeOrMetadataNameSpecification})
                (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
            \s*\)";

    // regular expression used to match item vectors, including those embedded in strings
    private static readonly Regex s_itemVectorPattern = new(ItemVectorSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // regular expression used to match a list of item vectors that have no separator specification -- the item vectors
    // themselves may be optionally separated by semi-colons, or they might be all jammed together
    private static readonly Regex s_listOfItemVectorsWithoutSeparatorsPattern =
        new($@"^\s*(;\s*)*({ItemVectorWithoutSeparatorSpecification}\s*(;\s*)*)+$",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // the leading characters that indicate the start of an item metadata reference
    private const string ItemMetadataPrefix = "%(";

    // complete description of an item metadata reference, including the optional qualifying item type
    private const string ItemMetadataSpecification =
        $@"%\(\s*
                (?<ITEM_SPECIFICATION>(?<TYPE>{ProjectWriter.ItemTypeOrMetadataNameSpecification})\s*\.\s*)?
                (?<NAME>{ProjectWriter.ItemTypeOrMetadataNameSpecification})
            \s*\)";

    // regular expression used to match item metadata references embedded in strings
    private static readonly Regex s_itemMetadataPattern = new(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // description of an item vector with a transform, split into two halves along the transform expression
    private const string ItemVectorWithTransformLHS = $@"@\(\s*{ProjectWriter.ItemTypeOrMetadataNameSpecification}\s*->\s*'[^']*";
    private const string ItemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

    // PERF WARNING: this Regex is complex and tends to run slowly
    // regular expression used to match item metadata references outside of item vector expressions
    private static readonly Regex s_nonTransformItemMetadataPattern =
        new($@"((?<={ItemVectorWithTransformLHS}){ItemMetadataSpecification}(?!{ItemVectorWithTransformRHS})) |
               ((?<!{ItemVectorWithTransformLHS}){ItemMetadataSpecification}(?={ItemVectorWithTransformRHS})) |
               ((?<!{ItemVectorWithTransformLHS}){ItemMetadataSpecification}(?!{ItemVectorWithTransformRHS}))",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    /// <summary>
    /// Looks through a single parameter of the batchable object, and finds all references to item metadata
    /// (that aren't part of an item transform).  Populates a MatchCollection object with any regex matches
    /// found in the input.  Each reference to item metadata may or may not be qualified with an item name (e.g.,
    /// %(Culture) vs. %(EmbeddedResource.Culture).
    /// </summary>
    /// <param name="batchableObjectParameter"></param>
    private static MatchCollection? FindEmbeddedMetadataReferenceMatches_OriginalImplementation(string batchableObjectParameter)
    {
        MatchCollection? embeddedMetadataReferences = null;

        // PERF NOTE: Regex matching is expensive, so if the string doesn't contain any item attribute references, just bail
        // out -- pre-scanning the string is actually cheaper than running the Regex, even when there are no matches!

        if (batchableObjectParameter.IndexOf(ItemMetadataPrefix, StringComparison.Ordinal) != -1)
        {
            // if there are no item vectors in the string
            if (batchableObjectParameter.IndexOf(ItemVectorPrefix, StringComparison.Ordinal) == -1)
            {
                // run a simpler Regex to find item metadata references
                embeddedMetadataReferences = s_itemMetadataPattern.Matches(batchableObjectParameter);
            }
            // PERF NOTE: this is a highly targeted optimization for a common pattern observed during profiling
            // if the string is a list of item vectors with no separator specifications
            else if (s_listOfItemVectorsWithoutSeparatorsPattern.IsMatch(batchableObjectParameter))
            {
                // then even if the string contains item metadata references, those references will only be inside transform
                // expressions, and can be safely skipped
                embeddedMetadataReferences = null;
            }
            else
            {
                // otherwise, run the more complex Regex to find item metadata references not contained in expressions
                embeddedMetadataReferences = s_nonTransformItemMetadataPattern.Matches(batchableObjectParameter);
            }
        }

        return embeddedMetadataReferences;
    }

    #endregion
}
