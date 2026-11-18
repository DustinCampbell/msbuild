// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Tests expression shredding and its compatibility with the original regex-based implementation.
/// </summary>
/// <param name="output">The output helper for the test.</param>
public sealed class ExpressionShredder_Tests(ITestOutputHelper output) : TestBase(output)
{
    public static readonly TheoryData<string> MedleyExpressions =
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
        "@( foo -> ';);' , ';);' )",
        "@( foo -> ');' )",
        "@( A -> '%(Directory)%(Filename)%(Extension)', ' ** ')",
        "@( )",
        "@(   foo  )",
        "@(   foo  ",
        "@(   a1234567890_-AXZaxz   )",
        "@",
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
        "@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);",
    ];

    /// <summary>
    ///  Verifies the expression marker constants.
    /// </summary>
    [Fact]
    public void MarkerConstantsHaveExpectedValues()
    {
        ExpressionShredder.PropertyMarker.ShouldBe("$(");
        ExpressionShredder.ItemVectorMarker.ShouldBe("@(");
        ExpressionShredder.MetadataMarker.ShouldBe("%(");
    }

    /// <summary>
    ///  Verifies marker searches from different starting positions.
    /// </summary>
    /// <param name="marker">The marker to search for.</param>
    [Theory]
    [InlineData(ExpressionShredder.PropertyMarker)]
    [InlineData(ExpressionShredder.ItemVectorMarker)]
    [InlineData(ExpressionShredder.MetadataMarker)]
    public void MarkerSearchesReturnExpectedIndexes(string marker)
    {
        string expression = $"x{marker}y{marker}z";

        IndexOfMarker(marker, string.Empty, 0).ShouldBe(-1);
        IndexOfMarker(marker, "value", 0).ShouldBe(-1);
        IndexOfMarker(marker, expression, 0).ShouldBe(1);
        IndexOfMarker(marker, expression, 2).ShouldBe(4);
        IndexOfMarker(marker, expression, 4).ShouldBe(4);
        IndexOfMarker(marker, expression, 6).ShouldBe(-1);
        IndexOfMarker(marker, $"{marker[0]}x{marker}", 0).ShouldBe(2);
        IndexOfMarker(marker, $"value{marker[0]}", 0).ShouldBe(-1);
        IndexOfMarker(marker, marker, marker.Length).ShouldBe(-1);

        static int IndexOfMarker(string marker, string expression, int startIndex)
            => marker switch
            {
                ExpressionShredder.PropertyMarker => ExpressionShredder.IndexOfPropertyMarker(expression, startIndex),
                ExpressionShredder.ItemVectorMarker => ExpressionShredder.IndexOfItemVectorMarker(expression, startIndex),
                ExpressionShredder.MetadataMarker => ExpressionShredder.IndexOfMetadataMarker(expression, startIndex),

                _ => Assumed.Unreachable<int>($"Unexpected marker: {marker}"),
            };
    }

    /// <summary>
    ///  Verifies marker searches within bounded ranges.
    /// </summary>
    /// <param name="marker">The marker to search for.</param>
    [Theory]
    [InlineData(ExpressionShredder.PropertyMarker)]
    [InlineData(ExpressionShredder.ItemVectorMarker)]
    [InlineData(ExpressionShredder.MetadataMarker)]
    public void BoundedMarkerSearchesReturnExpectedIndexes(string marker)
    {
        string expression = $"x{marker}y{marker}z";

        IndexOfMarker(marker, expression, 0, 0).ShouldBe(-1);
        IndexOfMarker(marker, expression, 0, 2).ShouldBe(-1);
        IndexOfMarker(marker, expression, 0, 3).ShouldBe(1);
        IndexOfMarker(marker, expression, 2, 3).ShouldBe(-1);
        IndexOfMarker(marker, expression, 2, 4).ShouldBe(4);
        IndexOfMarker(marker, expression, 4, 2).ShouldBe(4);
        IndexOfMarker(marker, expression, expression.Length, 0).ShouldBe(-1);
        IndexOfMarker(marker, $"{marker[0]}x{marker}", 0, 3).ShouldBe(-1);
        IndexOfMarker(marker, $"{marker[0]}x{marker}", 0, 4).ShouldBe(2);

        static int IndexOfMarker(string marker, string expression, int startIndex, int count)
            => marker switch
            {
                ExpressionShredder.PropertyMarker => ExpressionShredder.IndexOfPropertyMarker(expression, startIndex, count),
                ExpressionShredder.ItemVectorMarker => ExpressionShredder.IndexOfItemVectorMarker(expression, startIndex, count),
                ExpressionShredder.MetadataMarker => ExpressionShredder.IndexOfMetadataMarker(expression, startIndex, count),

                _ => Assumed.Unreachable<int>($"Unexpected marker: {marker}"),
            };
    }

    /// <summary>
    ///  Verifies marker presence checks.
    /// </summary>
    /// <param name="marker">The marker to search for.</param>
    [Theory]
    [InlineData(ExpressionShredder.PropertyMarker)]
    [InlineData(ExpressionShredder.ItemVectorMarker)]
    [InlineData(ExpressionShredder.MetadataMarker)]
    public void MarkerContainsChecksReturnExpectedResults(string marker)
    {
        ContainsMarker(marker, string.Empty).ShouldBeFalse();
        ContainsMarker(marker, "value").ShouldBeFalse();
        ContainsMarker(marker, $"value{marker[0]}").ShouldBeFalse();
        ContainsMarker(marker, $"{marker[0]}x{marker}").ShouldBeTrue();
        ContainsMarker(marker, marker).ShouldBeTrue();

        static bool ContainsMarker(string marker, string expression)
            => marker switch
            {
                ExpressionShredder.PropertyMarker => ExpressionShredder.ContainsPropertyMarker(expression),
                ExpressionShredder.ItemVectorMarker => ExpressionShredder.ContainsItemVectorMarker(expression),
                ExpressionShredder.MetadataMarker => ExpressionShredder.ContainsMetadataMarker(expression),

                _ => Assumed.Unreachable<bool>($"Unexpected marker: {marker}"),
            };
    }

    /// <summary>
    ///  Verifies successive item-vector searches skip malformed candidates.
    /// </summary>
    [Fact]
    public void TryGetNextItemVectorExpressionFindsValidExpressions()
    {
        const string expression = "x@x@(; )@(First);@(Second, '|')";

        ExpressionShredder.TryGetNextItemVectorExpression(expression, out ExpressionShredder.ItemExpressionCapture first).ShouldBeTrue();
        first.Value.ShouldBe("@(First)");

        int nextIndex = first.Index + first.Length;
        ExpressionShredder.TryGetNextItemVectorExpression(expression, nextIndex, out ExpressionShredder.ItemExpressionCapture second).ShouldBeTrue();
        second.Value.ShouldBe("@(Second, '|')");

        nextIndex = second.Index + second.Length;
        ExpressionShredder.TryGetNextItemVectorExpression(expression, nextIndex, out _).ShouldBeFalse();
    }

    /// <summary>
    ///  Compares referenced item and metadata names with the original regex-based implementation.
    /// </summary>
    /// <param name="expression">The expression to shred.</param>
    [Theory]
    [MemberData(nameof(MedleyExpressions))]
    public void ReferencedNamesMatchOriginalImplementation(string expression)
    {
        ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata([expression]);
        HashSet<string> expectedItems = GetConsumedItemReferencesUsingRegex(expression);
        Dictionary<string, MetadataReference> expectedMetadata = GetConsumedMetadataReferencesUsingRegex(expression);

        if (pair.Items is { } items)
        {
            items.Count.ShouldBe(expectedItems.Count);
            foreach (string item in items)
            {
                expectedItems.Contains(item).ShouldBeTrue();
            }

            foreach (string item in expectedItems)
            {
                items.Contains(item).ShouldBeTrue();
            }
        }
        else
        {
            expectedItems.ShouldBeEmpty();
        }

        if (pair.Metadata is { } metadata)
        {
            metadata.Count.ShouldBe(expectedMetadata.Count);
            foreach (string name in metadata.Keys)
            {
                expectedMetadata.ContainsKey(name).ShouldBeTrue();
            }

            foreach (string name in expectedMetadata.Keys)
            {
                metadata.ContainsKey(name).ShouldBeTrue();
            }
        }
        else
        {
            expectedMetadata.ShouldBeEmpty();
        }
    }

    public static readonly TheoryData<string, string[]> SplitExpressions =
    [
        ("a", ["a"]),
        ("a;b", ["a", "b"]),
        ("", []),
        (";", []),
        (";;", []),
        ("a;;", ["a"]),
        (";;a", ["a"]),
        ("@", ["@"]),
        ("foo@(", ["foo@("]),
        (";a;bbb;;c;;", ["a", "bbb", "c"]),
        ("  ;  a   ;b   ;   ;c\n;  \r;  ", ["a", "b", "c"]),
        ("@(foo->';')", ["@(foo->';')"]),
        ("@(foo, ';')", ["@(foo, ';')"]),
        ("@(foo->'abc;def', 'ghi;jkl')", ["@(foo->'abc;def', 'ghi;jkl')"]),
        ("@(foo->');')", ["@(foo->');')"]),
        ("a;@(foo,');');b", ["a", "@(foo,');')", "b"]),
        ("@(foo->';);', ';);')", ["@(foo->';);', ';);')"]),
        (" @(foo->'', '')", ["@(foo->'', '')"]),
        ("@(foo->';());', ';@();')", ["@(foo->';());', ';@();')"]),
        ("@(foo->';');def;@ghi;", ["@(foo->';')", "def", "@ghi"]),
        ("@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )", ["@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )"]),

        // Invalid expressions are left for the caller to diagnose.
        ("@(x", ["@(x"]),
        ("@(x->')", ["@(x->')"]),
        ("@(x->)", ["@(x->)"]),
        ("@(x->''", ["@(x->''"]),
        ("@(x->", ["@(x->"]),
        ("@(x,')", ["@(x,')"]),

        // The trailing semicolon is considered part of the unfinished item expression.
        ("@(x''';", ["@(x''';"]),
    ];

    /// <summary>
    ///  Verifies semicolon splitting, including item expressions and malformed input.
    /// </summary>
    /// <param name="expression">The list to split.</param>
    /// <param name="expected">The expected entries.</param>
    [Theory]
    [MemberData(nameof(SplitExpressions))]
    public void SplitSemicolonSeparatedList(string expression, string[] expected)
    {
        IEnumerable<string> actual = ExpressionShredder.SplitSemiColonSeparatedList(expression);
        actual.ShouldBe(expected);
    }

    /// <summary>
    ///  Verifies splitting a realistic multiline list of properties and item expressions.
    /// </summary>
    [Fact]
    public void SplitMultilineList()
    {
        string expression = """
            @(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);
            @(Compile);
            @(ManifestResourceWithNoCulture);
            $(ApplicationIcon);
            $(AssemblyOriginatorKeyFile);
            @(ManifestNonResxWithNoCultureOnDisk);
            @(ReferencePath);
            @(CompiledLicenseFile);
            @(EmbeddedDocumentation);
            @(CustomAdditionalCompileInputs)
            """;

        string[] expected =
        [
            "@(_OutputPathItem->'%(FullPath)', ';')",
            "$(MSBuildAllProjects)",
            "@(Compile)",
            "@(ManifestResourceWithNoCulture)",
            "$(ApplicationIcon)",
            "$(AssemblyOriginatorKeyFile)",
            "@(ManifestNonResxWithNoCultureOnDisk)",
            "@(ReferencePath)",
            "@(CompiledLicenseFile)",
            "@(EmbeddedDocumentation)",
            "@(CustomAdditionalCompileInputs)",
        ];

        IEnumerable<string> actual = ExpressionShredder.SplitSemiColonSeparatedList(expression);
        actual.ShouldBe(expected);
    }

    /// <summary>
    ///  Verifies extraction of a chain of quoted transforms and an item function.
    /// </summary>
    [Fact]
    public void ExtractItemVectorTransformChain()
    {
        string expression = "@(i->'%(Meta0)'->'%(Filename)'->Substring($(Val)))";
        ExpressionShredder.ItemExpressionCapture itemVector = GetSingleItemExpression(expression);

        itemVector.Separator.ShouldBeNull();
        itemVector.ItemType.ShouldBe("i");
        itemVector.Captures.Count.ShouldBe(3);
        itemVector.Captures[0].Value.ShouldBe("%(Meta0)");
        itemVector.Captures[1].Value.ShouldBe("%(Filename)");
        itemVector.Captures[2].Value.ShouldBe("Substring($(Val))");
    }

    /// <summary>
    ///  Compares item expression captures with the original regex-based implementation.
    /// </summary>
    /// <param name="expression">The expression to shred.</param>
    /// <remarks>
    ///  The medley exercises syntax understood by the old regex; newer expression types are tested separately.
    /// </remarks>
    [Theory]
    [MemberData(nameof(MedleyExpressions))]
    public void ItemExpressionsMatchOriginalRegex(string expression)
    {
        List<ExpressionShredder.ItemExpressionCapture> expressions = GetItemExpressions(expression);
        MatchCollection matches = s_itemVectorPattern.Matches(expression);
        expressions.Count.ShouldBe(matches.Count);

        for (int i = 0; i < expressions.Count; i++)
        {
            Match match = matches[i];
            ExpressionShredder.ItemExpressionCapture capture = expressions[i];
            capture.Value.ShouldBe(match.Value);

            Group transformGroup = match.Groups["TRANSFORM"];
            if (capture.Captures is { } captures)
            {
                captures.Count.ShouldBe(transformGroup.Captures.Count);
                for (int j = 0; j < transformGroup.Captures.Count; j++)
                {
                    captures[j].Value.ShouldBe(transformGroup.Captures[j].Value);
                }
            }
            else
            {
                transformGroup.Length.ShouldBe(0);
            }
        }
    }

    /// <summary>
    ///  Verifies that malformed item expressions are not captured.
    /// </summary>
    /// <param name="expression">The malformed item expression.</param>
    [Theory]
    [InlineData("@(type-&gt;'%($(a)), '%'')")]
    [InlineData("@(I -> Count() invalid)")]
    public void InvalidItemExpressionsAreNotCaptured(string expression)
        => GetItemExpressions(expression).ShouldBeEmpty();

    /// <summary>
    ///  Verifies item types, separators, and individual transforms or functions.
    /// </summary>
    /// <param name="expression">The item expression to shred.</param>
    /// <param name="separator">The expected separator, if any.</param>
    /// <param name="value">The expected transform or function, if any.</param>
    /// <param name="functionName">The expected function name, if any.</param>
    /// <param name="functionArguments">The expected function arguments, if any.</param>
    [Theory]
    [InlineData("@(Foo)", null, null, null, null)]
    [InlineData("@(Foo, ';')", ";", null, null, null)]
    [InlineData("@(Foo->'%(Fullpath)')", null, "%(Fullpath)", null, null)]
    [InlineData("@(Foo->'%(Fullpath)',';')", ";", "%(Fullpath)", null, null)]
    [InlineData("@(Foo->Bar(a,b))", null, "Bar(a,b)", "Bar", "a,b")]
    [InlineData("@(Foo->Bar(a,b),';')", ";", "Bar(a,b)", "Bar", "a,b")]
    [InlineData("@(Foo->'$(SOMEPROP)%(Fullpath)')", null, "$(SOMEPROP)%(Fullpath)", null, null)]
    public void ExtractSingleItemExpression(string expression, string? separator, string? value, string? functionName, string? functionArguments)
    {
        ExpressionShredder.ItemExpressionCapture capture = GetSingleItemExpression(expression);
        capture.ItemType.ShouldBe("Foo");
        capture.Separator.ShouldBe(separator);

        if (value is null)
        {
            capture.Captures.ShouldBeNull();
        }
        else
        {
            ExpressionShredder.ItemExpressionCapture transform = capture.Captures.ShouldHaveSingleItem();
            transform.Value.ShouldBe(value);
            transform.FunctionName.ShouldBe(functionName);
            transform.FunctionArguments.ShouldBe(functionArguments);
        }
    }

    /// <summary>
    ///  Verifies a chain of two item functions with and without a separator.
    /// </summary>
    /// <param name="expression">The item expression to shred.</param>
    /// <param name="separator">The expected separator, if any.</param>
    [Theory]
    [InlineData("@(Foo->Metadata('Meta0')->Directory())", null)]
    [InlineData("@(Foo->Metadata('Meta0')->Directory(),';')", ";")]
    public void ExtractItemFunctionChain(string expression, string? separator)
    {
        ExpressionShredder.ItemExpressionCapture capture = GetSingleItemExpression(expression);
        capture.ItemType.ShouldBe("Foo");
        capture.Separator.ShouldBe(separator);
        capture.Captures.Count.ShouldBe(2);
        capture.Captures[0].Value.ShouldBe("Metadata('Meta0')");
        capture.Captures[0].FunctionName.ShouldBe("Metadata");
        capture.Captures[0].FunctionArguments.ShouldBe("'Meta0'");
        capture.Captures[1].Value.ShouldBe("Directory()");
        capture.Captures[1].FunctionName.ShouldBe("Directory");
        capture.Captures[1].FunctionArguments.ShouldBeNull();
    }

    public static readonly TheoryData<string, string?, string, string, string, string?> TransformAndFunctionExpressions =
    [
        ("@(Foo->'%(Fullpath)'->Directory(), '|')", "|", "%(Fullpath)", "Directory()", "Directory", null),
        ("@(Foo->'%(Fullpath)'->Directory(),';')", ";", "%(Fullpath)", "Directory()", "Directory", null),
        (
            "@(Foo->'%(Filename)'->Substring($(Val), $(Boo)))",
            null, "%(Filename)", "Substring($(Val), $(Boo))", "Substring", "$(Val), $(Boo)"
        ),
        (
            """@(Foo->'%(Filename)'->Substring("AA", 'BB', `cc`))""",
            null, "%(Filename)", """Substring("AA", 'BB', `cc`)""", "Substring", "\"AA\", 'BB', `cc`"
        ),
        (
            "@(Foo->'%(Filename)'->Substring('()', $(Boo), ')('))",
            null, "%(Filename)", "Substring('()', $(Boo), ')(')", "Substring", "'()', $(Boo), ')('"
        ),
        (
            """@(Foo->'%(Filename)'->Substring(`()`, $(Boo), "AA"))""",
            null, "%(Filename)", """Substring(`()`, $(Boo), "AA")""", "Substring", "`()`, $(Boo), \"AA\""
        ),
        (
            """@(Foo->'%(Filename)'->Substring(`()`, $(Boo), ")("))""",
            null, "%(Filename)", "Substring(`()`, $(Boo), \")(\")", "Substring", "`()`, $(Boo), \")(\""
        ),
        (
            """@(Foo->'%(Filename)'->Substring("()", $(Boo), `)(`))""",
            null, "%(Filename)", """Substring("()", $(Boo), `)(`)""", "Substring", "\"()\", $(Boo), `)(`"
        ),
    ];

    /// <summary>
    ///  Verifies a quoted transform followed by an item function.
    /// </summary>
    /// <param name="expression">The item expression to shred.</param>
    /// <param name="separator">The expected separator, if any.</param>
    /// <param name="transform">The expected quoted transform.</param>
    /// <param name="function">The expected function text.</param>
    /// <param name="functionName">The expected function name.</param>
    /// <param name="arguments">The expected function arguments, if any.</param>
    [Theory]
    [MemberData(nameof(TransformAndFunctionExpressions))]
    public void ExtractTransformAndFunction(
        string expression, string? separator, string transform, string function, string functionName, string? arguments)
    {
        ExpressionShredder.ItemExpressionCapture capture = GetSingleItemExpression(expression);
        capture.ItemType.ShouldBe("Foo");
        capture.Separator.ShouldBe(separator);
        capture.Captures.Count.ShouldBe(2);
        capture.Captures[0].Value.ShouldBe(transform);
        capture.Captures[0].FunctionName.ShouldBeNull();
        capture.Captures[0].FunctionArguments.ShouldBeNull();
        capture.Captures[1].Value.ShouldBe(function);
        capture.Captures[1].FunctionName.ShouldBe(functionName);
        capture.Captures[1].FunctionArguments.ShouldBe(arguments);
    }

    public static readonly TheoryData<string, int, string> MultipleItemExpressions =
    [
        ("""@(Bar);@(Foo->'%(Filename)'->Substring("()", $(Boo), `)(`))""", 1, "\"()\", $(Boo), `)(`"),
        ("""@(Foo->'%(Filename)'->Substring("()", $(Boo), `)(`));@(Bar)""", 0, "\"()\", $(Boo), `)(`"),
        ("""@(Foo->'%(Filename)'->Substring("()", $(Boo), `)(`));AAAAAA;@(Bar)""", 0, "\"()\", $(Boo), `)(`"),
        (
            """@(Foo->'%(Filename)'->Substring("()", $(Boo), `)("`));@(;);@(aaa->;b);@(bbb->'d);@(`Foo->'%(Filename)'->Distinct());@(Bar)""",
            0, "\"()\", $(Boo), `)(\"`"
        ),
    ];

    /// <summary>
    ///  Verifies multiple item expressions, including recovery from malformed expressions between them.
    /// </summary>
    /// <param name="expression">The expression to shred.</param>
    /// <param name="fooIndex">The index of the transformed item expression.</param>
    /// <param name="arguments">The expected substring arguments.</param>
    [Theory]
    [MemberData(nameof(MultipleItemExpressions))]
    public void ExtractMultipleItemExpressions(string expression, int fooIndex, string arguments)
    {
        List<ExpressionShredder.ItemExpressionCapture> expressions = GetItemExpressions(expression);
        expressions.Count.ShouldBe(2);

        ExpressionShredder.ItemExpressionCapture bar = expressions[1 - fooIndex];
        bar.ItemType.ShouldBe("Bar");
        bar.Captures.ShouldBeNull();

        ExpressionShredder.ItemExpressionCapture foo = expressions[fooIndex];
        foo.ItemType.ShouldBe("Foo");
        foo.Separator.ShouldBeNull();
        foo.Captures.Count.ShouldBe(2);
        foo.Captures[0].Value.ShouldBe("%(Filename)");
        foo.Captures[0].FunctionName.ShouldBeNull();
        foo.Captures[0].FunctionArguments.ShouldBeNull();
        foo.Captures[1].Value.ShouldBe($"Substring({arguments})");
        foo.Captures[1].FunctionName.ShouldBe("Substring");
        foo.Captures[1].FunctionArguments.ShouldBe(arguments);
    }

    /// <summary>
    ///  Verifies that each repeated item expression keeps its own separator.
    /// </summary>
    [Fact]
    public void ExtractRepeatedItemExpressions()
    {
        string expression = "@(foo);@(foo,'-');@(foo);@(foo,',');@(foo)";
        List<ExpressionShredder.ItemExpressionCapture> expressions = GetItemExpressions(expression);
        expressions.Count.ShouldBe(5);

        foreach (ExpressionShredder.ItemExpressionCapture expressionCapture in expressions)
        {
            expressionCapture.ItemType.ShouldBe("foo");
        }

        expressions[0].Separator.ShouldBeNull();
        expressions[1].Separator.ShouldBe("-");
        expressions[2].Separator.ShouldBeNull();
        expressions[3].Separator.ShouldBe(",");
        expressions[4].Separator.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies whitespace around chained item functions.
    /// </summary>
    /// <param name="expression">The item expression to shred.</param>
    [Theory]
    [InlineData("@(I -> WithMetadataValue('M', 'T') -> WithMetadataValue('M', 'T'))")]
    [InlineData("@(I -> WithMetadataValue('M', 'T')-> WithMetadataValue('M', 'T'))")]
    public void ExtractChainedFunctionsWithWhitespace(string expression)
    {
        ExpressionShredder.ItemExpressionCapture capture = GetSingleItemExpression(expression);
        capture.ItemType.ShouldBe("I");
        capture.Captures.Count.ShouldBe(2);
        foreach (ExpressionShredder.ItemExpressionCapture function in capture.Captures)
        {
            function.FunctionName.ShouldBe("WithMetadataValue");
            function.FunctionArguments.ShouldBe("'M', 'T'");
        }
    }

    /// <summary>
    ///  Verifies multiple chained functions with varied whitespace.
    /// </summary>
    [Fact]
    public void ExtractMultipleChainedFunctionsWithWhitespace()
    {
        ExpressionShredder.ItemExpressionCapture capture = GetSingleItemExpression("@(I->Distinct() -> Reverse() ->Count())");
        capture.ItemType.ShouldBe("I");
        capture.Captures.Count.ShouldBe(3);
        capture.Captures[0].FunctionName.ShouldBe("Distinct");
        capture.Captures[1].FunctionName.ShouldBe("Reverse");
        capture.Captures[2].FunctionName.ShouldBe("Count");
    }

    /// <summary>
    ///  Verifies trailing whitespace after an item function or quoted transform.
    /// </summary>
    /// <param name="expression">The item expression to shred.</param>
    /// <param name="value">The expected capture text.</param>
    /// <param name="functionName">The expected function name, if any.</param>
    [Theory]
    [InlineData("@(I -> Count() )", "Count()", "Count")]
    [InlineData("@(I -> 'Replacement' )", "Replacement", null)]
    public void ExtractTransformWithTrailingWhitespace(string expression, string value, string? functionName)
    {
        ExpressionShredder.ItemExpressionCapture capture = GetSingleItemExpression(expression);
        capture.ItemType.ShouldBe("I");
        ExpressionShredder.ItemExpressionCapture transform = capture.Captures.ShouldHaveSingleItem();
        transform.Value.ShouldBe(value);
        transform.FunctionName.ShouldBe(functionName);
    }

    private static List<ExpressionShredder.ItemExpressionCapture> GetItemExpressions(string expression)
    {
        List<ExpressionShredder.ItemExpressionCapture> captures = [];
        int startIndex = 0;

        while (ExpressionShredder.TryGetNextItemVectorExpression(expression, startIndex, out ExpressionShredder.ItemExpressionCapture capture))
        {
            captures.Add(capture);
            startIndex = capture.Index + capture.Length;
        }

        return captures;
    }

    private static ExpressionShredder.ItemExpressionCapture GetSingleItemExpression(string expression)
        => GetItemExpressions(expression).ShouldHaveSingleItem();

    #region Original code to produce canonical results

    /// <summary>
    ///  Finds referenced item names using the original regex.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns>
    ///  The referenced item names.
    /// </returns>
    private static HashSet<string> GetConsumedItemReferencesUsingRegex(string expression)
    {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match itemVector in s_itemVectorPattern.Matches(expression))
        {
            result.Add(itemVector.Groups["TYPE"].Value);
        }

        return result;
    }

    /// <summary>
    ///  Finds qualified and unqualified metadata references outside item transforms using the original regex.
    /// </summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns>
    ///  The metadata references keyed by their qualified names.
    /// </returns>
    private static Dictionary<string, MetadataReference> GetConsumedMetadataReferencesUsingRegex(string expression)
    {
        Dictionary<string, MetadataReference> consumedMetadataReferences = new(StringComparer.OrdinalIgnoreCase);

        FindEmbeddedMetadataReferencesUsingRegex(expression, consumedMetadataReferences);

        return consumedMetadataReferences;
    }

    /// <summary>
    ///  Adds qualified and unqualified metadata references outside item transforms.
    /// </summary>
    /// <param name="batchableObjectParameter">The expression to inspect.</param>
    /// <param name="consumedMetadataReferences">The dictionary to populate.</param>
    private static void FindEmbeddedMetadataReferencesUsingRegex(
        string batchableObjectParameter, Dictionary<string, MetadataReference> consumedMetadataReferences)
    {
        MatchCollection? embeddedMetadataReferences = FindEmbeddedMetadataReferenceMatchesUsingRegex(batchableObjectParameter);

        if (embeddedMetadataReferences is not null)
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

                consumedMetadataReferences[qualifiedMetadataName] = new MetadataReference(itemName!, metadataName);
            }
        }
    }

    // the leading characters that indicate the start of an item vector
    private const string itemVectorPrefix = "@(";

    // complete description of an item vector, including the optional transform expression and separator specification
    private const string itemVectorSpecification = $"""
        @\(\s*
            (?<TYPE>{ProjectWriter.itemTypeOrMetadataNameSpecification})
            (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
            (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
        \s*\)
        """;

    // description of an item vector, including the optional transform expression, but not the separator specification
    private const string itemVectorWithoutSeparatorSpecification = $"""
        @\(\s*
            (?<TYPE>{ProjectWriter.itemTypeOrMetadataNameSpecification})
            (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
        \s*\)
        """;

    // regular expression used to match item vectors, including those embedded in strings
    private static readonly Regex s_itemVectorPattern = new(itemVectorSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // regular expression used to match a list of item vectors that have no separator specification -- the item vectors
    // themselves may be optionally separated by semi-colons, or they might be all jammed together
    private static readonly Regex s_listOfItemVectorsWithoutSeparatorsPattern =
        new($"""^\s*(;\s*)*({itemVectorWithoutSeparatorSpecification}\s*(;\s*)*)+$""", RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // the leading characters that indicate the start of an item metadata reference
    private const string itemMetadataPrefix = "%(";

    // complete description of an item metadata reference, including the optional qualifying item type
    private const string itemMetadataSpecification = $"""
        %\(\s*
            (?<ITEM_SPECIFICATION>(?<TYPE>{ProjectWriter.itemTypeOrMetadataNameSpecification})\s*\.\s*)?
            (?<NAME>{ProjectWriter.itemTypeOrMetadataNameSpecification})
        \s*\)
        """;

    // regular expression used to match item metadata references embedded in strings
    private static readonly Regex s_itemMetadataPattern = new(itemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // description of an item vector with a transform, split into two halves along the transform expression
    private const string itemVectorWithTransformLHS = $"""@\(\s*{ProjectWriter.itemTypeOrMetadataNameSpecification}\s*->\s*'[^']*""";
    private const string itemVectorWithTransformRHS = """[^']*'(\s*,\s*'[^']*')?\s*\)""";

    // PERF WARNING: this Regex is complex and tends to run slowly
    // regular expression used to match item metadata references outside of item vector expressions
    private static readonly Regex s_nonTransformItemMetadataPattern = new($"""
        ((?<={itemVectorWithTransformLHS}){itemMetadataSpecification}(?!{itemVectorWithTransformRHS})) |
        ((?<!{itemVectorWithTransformLHS}){itemMetadataSpecification}(?={itemVectorWithTransformRHS})) |
        ((?<!{itemVectorWithTransformLHS}){itemMetadataSpecification}(?!{itemVectorWithTransformRHS}))
        """, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    /// <summary>
    ///  Finds metadata references outside item transforms using the original regexes.
    /// </summary>
    /// <param name="batchableObjectParameter">The expression to inspect.</param>
    /// <returns>
    ///  The regex matches, or <see langword="null"/> when the expression cannot contain relevant references.
    /// </returns>
    private static MatchCollection? FindEmbeddedMetadataReferenceMatchesUsingRegex(string batchableObjectParameter)
    {
        MatchCollection? embeddedMetadataReferences = null;

        // PERF NOTE: Regex matching is expensive, so if the string doesn't contain any item attribute references, just bail
        // out -- pre-scanning the string is actually cheaper than running the Regex, even when there are no matches!

        if (batchableObjectParameter.IndexOf(itemMetadataPrefix, StringComparison.Ordinal) != -1)
        {
            // if there are no item vectors in the string
            if (batchableObjectParameter.IndexOf(itemVectorPrefix, StringComparison.Ordinal) == -1)
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
