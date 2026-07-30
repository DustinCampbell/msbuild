// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
/// Compares the items and metadata that ExpressionShredder finds
/// with the results from the old regexes to make sure they're identical
/// in every case.
/// </summary>
public class ExpressionShredder_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    // Expressions that must be parsed identically by the shredder and the old regexes.
    // NOTE: every expression here must be parsable by the old regex; this is a regression
    // test against that regex. New expression types should be added in other tests.
    public static TheoryData<string> MedleyExpressions { get; } = new(
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
        "@(x->)",
        "@(x->'x','')",
        "@(x->'x',''",
        "@(x->'x','",
        "@(x->')",
        "@(x->''",
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
        "@(",
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
        "",
        "!@#$%^&*",
        " @(foo->'', '')",
        " ->       ';abc;def;'   ,     'ghi;jkl'   )",
        " %(A . x)%%%%%%%%(b . x) ",
        "  ;  a   ;b   ;   ;c",
        "                $(AssemblyOriginatorKeyFile);\n\t                @(Compile);",
        "@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);");

    [Theory]
    [MemberData(nameof(MedleyExpressions))]
    public void Medley(string expression)
    {
        VerifyExpression(expression);
    }

    /// <summary>
    /// Compare the results of the expression shredder based item expression extractor with the original regex based one.
    /// NOTE: The medley of tests needs to be parsable by the old regex. This is a regression test against that regex.
    /// New expression types should be added in other tests.
    /// </summary>
    [Theory]
    [MemberData(nameof(MedleyExpressions))]
    public void ItemExpressionMedleyRegressionTestAgainstOldRegex(string expression)
    {
        ItemVectorEnumerator enumerator = ExpressionShredder.GetReferencedItemExpressions(expression);
        MatchCollection matches = s_itemVectorPattern.Matches(expression);
        int expressionCount = 0;

        while (enumerator.MoveNext())
        {
            Match match = matches[expressionCount];
            ItemVector itemVector = enumerator.Current;

            itemVector.Text.ShouldBe(match.Value);

            Group transformGroup = match.Groups["TRANSFORM"];

            if (!itemVector.Transforms.IsEmpty)
            {
                for (int i = 0; i < transformGroup.Captures.Count; i++)
                {
                    itemVector.Transforms[i].Text.ShouldBe(transformGroup.Captures[i].Value);
                }
            }
            else
            {
                transformGroup.Length.ShouldBe(0);
            }

            ++expressionCount;
        }

        if (expressionCount == 0)
        {
            matches.Count.ShouldBe(0);
        }
        else
        {
            expressionCount.ShouldBe(matches.Count);
        }
    }

    private const string RealisticExampleInput = """
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

    private static readonly string[] RealisticExampleExpected =
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
        "@(CustomAdditionalCompileInputs)"
    ];

    public static TheoryData<string, string[]> SplitCases => new(
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

        // Invalid item expressions shouldn't cause an error in the splitting function.
        // The caller will emit an error later when it tries to parse the results.
        ("@(x", ["@(x"]),
        ("@(x->')", ["@(x->')"]),
        ("@(x->)", ["@(x->)"]),
        ("@(x->''", ["@(x->''"]),
        ("@(x->", ["@(x->"]),
        ("@(x,')", ["@(x,')"]),

        // This one doesn't remove the ';' because it thinks it's in an item list. This isn't worth
        // tweaking, because the invalid expression is going to lead to an error in the caller whether
        // there's a ';' or not.
        ("@(x''';", ["@(x''';"]),

        (RealisticExampleInput, RealisticExampleExpected),

        // For reference, this is the authoritative definition of an item expression:
        //  @"@\(\s*
        //      (?<TYPE>[\w\x20-]*[\w-]+)
        //      (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
        //      (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
        //  \s*\)";
        // We need to support any item expressions that satisfy this expression.
        // Try spaces everywhere that the regex allows spaces:
        ("@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )", ["@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )"]));

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void Split(string input, string[] expected)
    {
        _output.WriteLine(input);

        string[] actual = [.. ExpressionShredder.Split(input)];

        actual.ShouldBe(expected);
    }

    [Fact]
    public void ExtractItemVectorTransform1()
        => Verify(
            "@(i->'%(Meta0)'->'%(Filename)'->Substring($(Val)))",
            itemType: "i",
            QuotedTransform("%(Meta0)"),
            QuotedTransform("%(Filename)"),
            FunctionTransform("Substring", "$(Val)"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpressionInvalid1()
        => VerifyInvalid("@(type-&gt;'%($(a)), '%'')");

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression1()
        => Verify("@(Foo)", itemType: "Foo");

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression2()
        => Verify("@(Foo, ';')", itemType: "Foo", separator: ";");

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression3()
        => Verify("@(Foo->'%(Fullpath)')", itemType: "Foo", QuotedTransform("%(Fullpath)"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression4()
        => Verify("@(Foo->'%(Fullpath)',';')", itemType: "Foo", separator: ";", QuotedTransform("%(Fullpath)"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression5()
        => Verify("@(Foo->Bar(a,b))", itemType: "Foo", FunctionTransform("Bar", "a,b"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression6()
        => Verify("@(Foo->Bar(a,b),';')", itemType: "Foo", separator: ";", FunctionTransform("Bar", "a,b"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression7()
        => Verify(
            "@(Foo->Metadata('Meta0')->Directory())",
            itemType: "Foo",
            FunctionTransform("Metadata", "'Meta0'"),
            FunctionTransform("Directory"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression8()
        => Verify(
            "@(Foo->Metadata('Meta0')->Directory(),';')",
            itemType: "Foo",
            separator: ";",
            FunctionTransform("Metadata", "'Meta0'"),
            FunctionTransform("Directory"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression9()
        => Verify(
            "@(Foo->'%(Fullpath)'->Directory(), '|')",
            itemType: "Foo",
            separator: "|",
            QuotedTransform("%(Fullpath)"),
            FunctionTransform("Directory"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression10()
        => Verify(
            "@(Foo->'%(Fullpath)'->Directory(),';')",
            itemType: "Foo",
            separator: ";",
            QuotedTransform("%(Fullpath)"),
            FunctionTransform("Directory"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression11()
        => Verify(
            "@(Foo->'$(SOMEPROP)%(Fullpath)')",
            itemType: "Foo",
            QuotedTransform("$(SOMEPROP)%(Fullpath)"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression12()
        => Verify(
            "@(Foo->'%(Filename)'->Substring($(Val), $(Boo)))",
            itemType: "Foo",
            QuotedTransform("%(Filename)"),
            FunctionTransform("Substring", "$(Val), $(Boo)"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression13()
        => Verify(
            "@(Foo->'%(Filename)'->Substring(\"AA\", 'BB', `cc`))",
            itemType: "Foo",
            QuotedTransform("%(Filename)"),
            FunctionTransform("Substring", "\"AA\", 'BB', `cc`"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression14()
        => Verify(
            "@(Foo->'%(Filename)'->Substring('()', $(Boo), ')('))",
            itemType: "Foo",
            QuotedTransform("%(Filename)"),
            FunctionTransform("Substring", "'()', $(Boo), ')('"));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression15()
        => Verify(
            "@(Foo->'%(Filename)'->Substring(`()`, $(Boo), \"AA\"))",
            itemType: "Foo",
            QuotedTransform("%(Filename)"),
            FunctionTransform("Substring", "`()`, $(Boo), \"AA\""));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression16()
        => Verify(
            "@(Foo->'%(Filename)'->Substring(`()`, $(Boo), \")(\"))",
            itemType: "Foo",
            QuotedTransform("%(Filename)"),
            FunctionTransform("Substring", "`()`, $(Boo), \")(\""));

    [Fact]
    public void ExtractItemVectorExpressionsSingleExpression17()
        => Verify(
            "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`))",
            itemType: "Foo",
            QuotedTransform("%(Filename)"),
            FunctionTransform("Substring", "\"()\", $(Boo), `)(`"));

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression1()
        => Verify(
            "@(Bar);@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`))",
            ItemVector("Bar"),
            ItemVector(itemType: "Foo", QuotedTransform("%(Filename)"), FunctionTransform("Substring", "\"()\", $(Boo), `)(`")));

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression2()
        => Verify(
            "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`));@(Bar)",
            ItemVector(itemType: "Foo", QuotedTransform("%(Filename)"), FunctionTransform("Substring", "\"()\", $(Boo), `)(`")),
            ItemVector("Bar"));

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression3()
        => Verify(
            "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`));AAAAAA;@(Bar)",
            ItemVector(itemType: "Foo", QuotedTransform("%(Filename)"), FunctionTransform("Substring", "\"()\", $(Boo), `)(`")),
            ItemVector("Bar"));

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression4()
        => Verify(
            "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(\"`));@(;);@(aaa->;b);@(bbb->'d);@(`Foo->'%(Filename)'->Distinct());@(Bar)",
            ItemVector(itemType: "Foo", QuotedTransform("%(Filename)"), FunctionTransform("Substring", "\"()\", $(Boo), `)(\"`")),
            ItemVector("Bar"));

    [Fact]
    public void ExtractItemVectorExpressionsMultipleExpression5()
        => Verify(
            "@(foo);@(foo,'-');@(foo);@(foo,',');@(foo)",
            ItemVector("foo"),
            ItemVector("foo", separator: "-"),
            ItemVector("foo"),
            ItemVector("foo", separator: ","),
            ItemVector("foo"));

    /// <summary>
    /// Test that item function chaining works with whitespace before arrow operators.
    /// </summary>
    [Fact]
    public void ExtractItemVectorExpressionsChainedFunctionsWithWhitespace()
    {
        // Test with space before second arrow: ") ->"
        Verify(
            "@(I -> WithMetadataValue('M', 'T') -> WithMetadataValue('M', 'T'))",
            itemType: "I",
            FunctionTransform("WithMetadataValue", "'M', 'T'"),
            FunctionTransform("WithMetadataValue", "'M', 'T'"));

        // Test without space before second arrow: ")->"
        Verify(
            "@(I -> WithMetadataValue('M', 'T')-> WithMetadataValue('M', 'T'))",
            itemType: "I",
            FunctionTransform("WithMetadataValue", "'M', 'T'"),
            FunctionTransform("WithMetadataValue", "'M', 'T'"));

        // Test with multiple spaces and chained functions
        Verify(
            "@(I->Distinct() -> Reverse() ->Count())",
            itemType: "I",
            FunctionTransform("Distinct"),
            FunctionTransform("Reverse"),
            FunctionTransform("Count"));

        // Test trailing whitespace after function call
        Verify(
            "@(I -> Count() )", itemType: "I", FunctionTransform("Count"));

        // Test trailing whitespace after quoted transform
        Verify(
            "@(I -> 'Replacement' )", itemType: "I", QuotedTransform("Replacement"));
    }

    /// <summary>
    /// Test that invalid syntax after whitespace is properly rejected.
    /// </summary>
    [Fact]
    public void ExtractItemVectorExpressionsInvalidSyntaxAfterWhitespace()
    {
        // Invalid syntax after whitespace - should not be parsed as item expression.
        ItemVectorEnumerator enumerator = ExpressionShredder.GetReferencedItemExpressions("@(I -> Count() invalid)");
        enumerator.MoveNext().ShouldBeFalse();
    }

    private static void Verify(
        string expression,
        string itemType,
        string separator,
        params ImmutableArray<Action<ItemTransform>> transformVerifiers)
        => Verify(expression, ItemVector(itemType, separator, transformVerifiers));

    private static void Verify(
        string expression,
        string itemType,
        params ImmutableArray<Action<ItemTransform>> transformVerifiers)
        => Verify(expression, ItemVector(itemType, transformVerifiers));

    private static void Verify(string expression, params ImmutableArray<Action<ItemVector>> verifiers)
    {
        ItemVectorEnumerator enumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

        foreach (Action<ItemVector> verifier in verifiers)
        {
            enumerator.MoveNext().ShouldBeTrue();
            verifier(enumerator.Current);
        }

        enumerator.MoveNext().ShouldBeFalse();
    }

    private static void VerifyInvalid(string expression)
    {
        ItemVectorEnumerator enumerator = ExpressionShredder.GetReferencedItemExpressions(expression);
        enumerator.MoveNext().ShouldBeFalse();
    }

    private static Action<ItemVector> ItemVector(string itemType, params ImmutableArray<Action<ItemTransform>> verifiers)
        => ItemVector(itemType, separator: null, verifiers);

    private static Action<ItemVector> ItemVector(string itemType, string? separator, params ImmutableArray<Action<ItemTransform>> verifiers)
        => itemVector =>
        {
            itemVector.ItemType.ShouldBe(itemType);

            if (separator is null)
            {
                itemVector.Separator.ShouldBeNull();
            }
            else
            {
                itemVector.Separator.ShouldBe(separator);
            }

            if (verifiers.Length == 0)
            {
                itemVector.Transforms.ShouldBeEmpty();
            }
            else
            {
                itemVector.Transforms.Length.ShouldBe(verifiers.Length);

                for (int i = 0; i < verifiers.Length; i++)
                {
                    verifiers[i](itemVector.Transforms[i]);
                }
            }
        };

    private static Action<ItemTransform> QuotedTransform(string text)
        => transform =>
        {
            transform.Text.ShouldBe(text);
            transform.FunctionName.ShouldBeNull();
            transform.FunctionArguments.ShouldBeNull();
        };

    private static Action<ItemTransform> FunctionTransform(string name, string? arguments = null)
        => transform =>
        {
            transform.FunctionName.ShouldBe(name);

            if (arguments is null)
            {
                transform.FunctionArguments.ShouldBeNull();
            }
            else
            {
                transform.FunctionArguments.ShouldBe(arguments);
            }
        };

    private void VerifyExpression(string test)
    {
        ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata([test]);

        HashSet<string> expectedItems = GetConsumedItemReferences_OriginalImplementation(test);
        _output.WriteLine("verifying item names...");
        VerifyAgainstCanonicalResults(test, pair.Items, expectedItems);

        Dictionary<string, MetadataReference> expectedMetadata = GetConsumedMetadataReferences_OriginalImplementation(test);
        _output.WriteLine("verifying metadata ...");
        VerifyAgainstCanonicalResults(test, pair.Metadata, expectedMetadata);

        _output.WriteLine("===OK===");
    }

    private void VerifyAgainstCanonicalResults(string test, HashSet<string>? actual, HashSet<string> expected)
    {
        if (actual is null or { Count: 0 })
        {
            expected.ShouldBeEmpty();
            return;
        }

        List<string> messages = [];

        _output.WriteLine($"Expecting {expected.Count} distinct values for <{test}>");

        foreach (string result in actual)
        {
            if (!expected.Contains(result))
            {
                messages.Add($"Found <{result}> in <{test}> but it wasn't expected");
            }
        }

        foreach (string result in expected)
        {
            if (!actual.Contains(result))
            {
                messages.Add($"Did not find <{result}> in <{test}>");
            }
        }

        if (messages.Count > 0)
        {
            StringBuilder builder = new();
            builder.Append("FOUND:");

            foreach (string result in actual)
            {
                builder.Append($" <{result}>");
            }

            _output.WriteLine(builder.ToString());
        }

        foreach (string message in messages)
        {
            _output.WriteLine(message);
        }

        messages.ShouldBeEmpty();
    }

    private void VerifyAgainstCanonicalResults(
        string test,
        Dictionary<string, MetadataReference>? actual,
        Dictionary<string, MetadataReference> expected)
    {
        if (actual is null or { Count: 0 })
        {
            expected.ShouldBeEmpty();
            return;
        }

        List<string> messages = [];

        _output.WriteLine($"Expecting {expected.Count} distinct values for <{test}>");

        foreach (string result in actual.Keys)
        {
            if (!expected.ContainsKey(result))
            {
                messages.Add($"Found <{result}> in <{test}> but it wasn't expected");
            }
        }

        foreach (string result in expected.Keys)
        {
            if (!actual.ContainsKey(result))
            {
                messages.Add($"Did not find <{result}> in <{test}>");
            }
        }

        if (messages.Count > 0)
        {
            StringBuilder builder = new();
            builder.Append("FOUND:");

            foreach (string result in actual.Keys)
            {
                builder.Append($" <{result}>");
            }

            _output.WriteLine(builder.ToString());
        }

        foreach (string message in messages)
        {
            _output.WriteLine(message);
        }

        messages.ShouldBeEmpty();
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
    private const string itemVectorPrefix = "@(";

    // complete description of an item vector, including the optional transform expression and separator specification
    private const string itemVectorSpecification =
        @"@\(\s*
            (?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
            (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
            (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
        \s*\)";

    // description of an item vector, including the optional transform expression, but not the separator specification
    private const string itemVectorWithoutSeparatorSpecification =
        @"@\(\s*
            (?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
            (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
        \s*\)";

    // regular expression used to match item vectors, including those embedded in strings
    private static readonly Regex s_itemVectorPattern = new Regex(itemVectorSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // regular expression used to match a list of item vectors that have no separator specification -- the item vectors
    // themselves may be optionally separated by semi-colons, or they might be all jammed together
    private static readonly Regex s_listOfItemVectorsWithoutSeparatorsPattern =
        new Regex(@"^\s*(;\s*)*(" +
                  itemVectorWithoutSeparatorSpecification +
                  @"\s*(;\s*)*)+$",
                  RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // the leading characters that indicate the start of an item metadata reference
    private const string itemMetadataPrefix = "%(";

    // complete description of an item metadata reference, including the optional qualifying item type
    private const string itemMetadataSpecification =
        @"%\(\s*
            (?<ITEM_SPECIFICATION>(?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")\s*\.\s*)?
            (?<NAME>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
        \s*\)";

    // regular expression used to match item metadata references embedded in strings
    private static readonly Regex s_itemMetadataPattern = new Regex(itemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

    // description of an item vector with a transform, split into two halves along the transform expression
    private const string itemVectorWithTransformLHS = @"@\(\s*" + ProjectWriter.itemTypeOrMetadataNameSpecification + @"\s*->\s*'[^']*";
    private const string itemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

    // PERF WARNING: this Regex is complex and tends to run slowly
    // regular expression used to match item metadata references outside of item vector expressions
    private static readonly Regex s_nonTransformItemMetadataPattern =
        new Regex(@"((?<=" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?!" + itemVectorWithTransformRHS + @")) |
                    ((?<!" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?=" + itemVectorWithTransformRHS + @")) |
                    ((?<!" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?!" + itemVectorWithTransformRHS + @"))",
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
