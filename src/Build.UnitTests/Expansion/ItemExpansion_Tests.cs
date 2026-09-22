// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class ItemExpansion_Tests(ITestOutputHelper output)
{
    private static readonly string s_rootPathPrefix = NativeMethodsShared.IsWindows ? @"C:\" : Path.VolumeSeparatorChar.ToString();

    private readonly ITestOutputHelper _output = output;

    /// <summary>
    ///  Expand an item vector into items of the specified type.
    /// </summary>
    [Fact]
    public void ExpandItemVectorsIntoProjectItemInstancesSpecifyingItemType()
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateExpander(project);
        var itemFactory = ItemFactory(project, itemType: "j");

        IList<ProjectItemInstance> items = expander.ExpandIntoItemsLeaveEscaped(
            "@(i)",
            itemFactory,
            ExpanderOptions.ExpandItems,
            MockElementLocation.Instance).ShouldNotBeNull();

        items.Count.ShouldBe(2);
        items[0].ItemType.ShouldBe("j");
        items[0].EvaluatedInclude.ShouldBe("i0");
        items[1].ItemType.ShouldBe("j");
        items[1].EvaluatedInclude.ShouldBe("i1");
    }

    /// <summary>
    ///  Expand an item vector into items of the type of the item vector.
    /// </summary>
    [Fact]
    public void ExpandItemVectorsIntoProjectItemInstancesWithoutSpecifyingItemType()
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateExpander(project);
        var itemFactory = ItemFactory(project);

        IList<ProjectItemInstance> items = expander.ExpandIntoItemsLeaveEscaped(
            "@(i)",
            itemFactory,
            ExpanderOptions.ExpandItems,
            MockElementLocation.Instance).ShouldNotBeNull();

        items.Count.ShouldBe(2);
        items[0].ItemType.ShouldBe("i");
        items[0].EvaluatedInclude.ShouldBe("i0");
        items[1].ItemType.ShouldBe("i");
        items[1].EvaluatedInclude.ShouldBe("i1");
    }

    /// <summary>
    ///  Expand an item vector function AnyHaveMetadataValue.
    /// </summary>
    [Theory]
    [MemberData(nameof(AnyHaveMetadataValueData))]
    public void ItemFunctionTransformsApplyAnyHaveMetadataValue(string expression, string expectedInclude)
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems(expression, itemType: "i");

        var item = items.ShouldHaveSingleItem();
        item.ItemType.ShouldBe("i");
        item.EvaluatedInclude.ShouldBe(expectedInclude);
    }

    public static TheoryData<string, string> AnyHaveMetadataValueData => new()
    {
        { "@(i->AnyHaveMetadataValue('Even', 'true'))", "true" },
        { "@(i->AnyHaveMetadataValue('Even', 'goop'))", "false" },
    };

    [Fact]
    public void ExpandEmptyItemVectorFunctionWithAnyHaveMetadataValue()
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems(
            "@(unsetItem->AnyHaveMetadataValue('Metadatum', 'value'))", itemType: "i");

        var item = items.ShouldHaveSingleItem();
        item.EvaluatedInclude.ShouldBe("false");
    }

    [Theory]
    [InlineData("@(unsetItem)", false)]
    [InlineData("@(unsetItem->Distinct())", true)]
    public void EmptyItemVectorReportsWhetherExpressionIsTransform(string expression, bool expected)
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander(project);
        var itemFactory = ItemFactory(project, itemType: "i");

        IList<ProjectItemInstance> items = expander.ExpandSingleItemVectorExpressionIntoItems(
            expression,
            itemFactory,
            ExpanderOptions.ExpandItems,
            includeNullItems: false,
            out bool isTransformExpression,
            MockElementLocation.Instance).ShouldNotBeNull();

        items.ShouldBeEmpty();
        isTransformExpression.ShouldBe(expected);
    }

    /// <summary>
    ///  Expand an item vector function Metadata()->DirectoryName()->Distinct().
    /// </summary>
    [Theory]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    [MemberData(nameof(ItemFunctionTransformDistinctDirectoryNameData))]
    public void ItemFunctionTransformsApplyDistinctToDirectoryNameResults(string expression, string expectedPath)
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems(expression, itemType: "i");

        var item = items.ShouldHaveSingleItem();
        item.ItemType.ShouldBe("i");
        item.EvaluatedInclude.ShouldBe(Path.GetFullPath(expectedPath));
    }

    public static TheoryData<string, string> ItemFunctionTransformDistinctDirectoryNameData => new()
    {
        { "@(i->Metadata('Meta0')->DirectoryName()->Distinct())", Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory") },
        { "@(i->Metadata('Meta9')->DirectoryName()->Distinct())", "seconddirectory" },
    };

    /// <summary>
    ///  Expand an item vector function that is an itemspec modifier.
    /// </summary>
    [Theory]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    [MemberData(nameof(ItemFunctionTransformItemSpecModifierData))]
    public void ItemFunctionTransformsApplyItemSpecModifiers(string expression, string expectedInclude)
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems(expression, itemType: "i");

        items.Count.ShouldBe(10);

        foreach (var item in items)
        {
            item.ItemType.ShouldBe("i");
            item.EvaluatedInclude.ShouldBe(expectedInclude);
        }
    }

    public static TheoryData<string, string> ItemFunctionTransformItemSpecModifierData => new()
    {
        { "@(i->Metadata('Meta0')->Directory())", Path.Combine("firstdirectory", "seconddirectory") + Path.DirectorySeparatorChar },
        { "@(i->Metadata('Meta0')->Filename())", "file0" },
        { "@(i->Metadata('Meta0')->Extension())", ".ext" },
    };

    /// <summary>
    ///  Expand an item vector function that is an itemspec modifier.
    /// </summary>
    [Theory]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    [MemberData(nameof(MetadataTransformItemSpecModifierData))]
    public void MetadataTransformsApplyItemSpecModifiers(string expression, string expectedInclude)
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems(expression, itemType: "i");

        items.Count.ShouldBe(10);

        foreach (var item in items)
        {
            item.ItemType.ShouldBe("i");
            item.EvaluatedInclude.ShouldBe(expectedInclude);
        }
    }

    public static TheoryData<string, string> MetadataTransformItemSpecModifierData => new()
    {
        { "@(i->'%(Meta0)'->'%(Directory)')", Path.Combine("firstdirectory", "seconddirectory") + Path.DirectorySeparatorChar },
        { "@(i->'%(Meta0)'->'%(Filename)')", "file0" },
        { "@(i->'%(Meta0)'->'%(Filename)'->Substring($(Val)))", "le0" },
    };

    /// <summary>
    ///  Expand an item vector function transform that applies Distinct().
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void MetadataTransformAppliesDistinctToItemSpecModifiers()
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems("@(i->'%(Meta0)'->'%(Extension)'->Distinct())", itemType: "i");

        var item = items.ShouldHaveSingleItem();
        item.ItemType.ShouldBe("i");
        item.EvaluatedInclude.ShouldBe(".ext");
    }

    /// <summary>
    ///  Expand an item expression (that isn't a real expression) but includes a property reference nested within a metadata reference.
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsInvalid1()
    {
        var expander = CreateItemFunctionExpander();

        expander.ExpandIntoStringLeaveEscaped("[@(type-&gt;'%($(a)), '%'')]", ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe("[@(type-&gt;'%(filename), '%'')]");
    }

    /// <summary>
    ///  Expand an item expression (that isn't a real expression) but includes a metadata reference that till needs to be expanded.
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsInvalid2()
    {
        var expander = CreateItemFunctionExpander();

        expander.ExpandIntoStringLeaveEscaped("[@(i->'%(Meta9))']", ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe("[@(i->')']");
    }

    /// <summary>
    ///  Expand an item vector function that is chained into a string.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsChained1()
        => ExpandItemFunctionIntoString("@(i->'%(Meta0)'->'%(Directory)'->Distinct())")
            .ShouldBe($"{Path.Combine("firstdirectory", "seconddirectory")}{Path.DirectorySeparatorChar}");

    /// <summary>
    ///  Expand an item vector function that is chained and has constants into a string.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsChained2()
        => ExpandItemFunctionIntoString("[@(i->'%(Meta0)'->'%(Directory)'->Distinct())]")
            .ShouldBe(@"[firstdirectory\seconddirectory\]");

    /// <summary>
    ///  Expand an item vector function that is chained and has constants into a string.
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsChained3()
        => ExpandItemFunctionIntoString("@(i->'%(MetaBlank)'->'%(Directory)'->Distinct())")
            .ShouldBe("");

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsChainedProject1()
    {
        const string Content = """
            <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                <ItemGroup>
                    <Compile Include=`a.cpp`>
                        <SomeMeta>C:\Value1\file1.txt</SomeMeta>
                        <A>||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||</A>
                        <B>##</B>
                    </Compile>
                    <Compile Include=`b.cpp`>
                        <SomeMeta>C:\Value2\file2.txt</SomeMeta>
                        <A>||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||</A>
                        <B>##</B>
                    </Compile>
                    <Compile Include=`c.cpp`>
                        <SomeMeta>C:\Value2\file3.txt</SomeMeta>
                        <A>||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||</A>
                        <B>##</B>
                    </Compile>
                    <Compile Include=`c.cpp`>
                        <SomeMeta>C:\Value2\file3.txt</SomeMeta>
                        <A>||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||</A>
                        <B>##</B>
                    </Compile>
                </ItemGroup>

                <Target Name=`Build`>
                    <Message Text=`DirChain0: @(Compile->'%(SomeMeta)'->'%(Directory)'->Distinct())`/>
                    <Message Text=`DirChain1: @(Compile->'%(SomeMeta)'->'%(Directory)'->Distinct(), '%(A)')`/>
                    <Message Text=`DirChain2: @(Compile->'%(SomeMeta)'->'%(Directory)'->Distinct(), '%(A)%(B)')`/>
                    <Message Text=`DirChain3: @(Compile->'%(SomeMeta)'->'%(Directory)'->Distinct(), '%(A)$%(B)')`/>
                    <Message Text=`DirChain4: @(Compile->'%(SomeMeta)'->'%(Directory)'->Distinct(), '$%(A)$%(B)')`/>
                    <Message Text=`DirChain5: @(Compile->'%(SomeMeta)'->'%(Directory)'->Distinct(), '$%(A)$%(B)$')`/>
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains(@"DirChain0: Value1\;Value2\");
        log.AssertLogContains(@"DirChain1: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||Value2\");
        log.AssertLogContains(@"DirChain2: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||##Value2\");
        log.AssertLogContains(@"DirChain3: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##Value2\");
        log.AssertLogContains(@"DirChain4: Value1\$||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##Value2\");
        log.AssertLogContains(@"DirChain5: Value1\$||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##$Value2\");
    }

    /// <summary>
    ///  Test that chained item functions work with whitespace before the second arrow operator.
    /// </summary>
    [Fact]
    public void ItemFunctionChainingWithWhitespaceBeforeArrow()
    {
        const string Content = """
            <Project>
                <ItemGroup>
                    <I Include=`A`>
                        <M>F</M>
                    </I>
                    <I Include=`B`>
                        <M>T</M>
                    </I>
                    <I Include=`C`>
                        <M>T</M>
                    </I>
                    <!-- Test with space before second arrow -->
                    <Test1 Include=`@(I -> WithMetadataValue('M', 'T') -> WithMetadataValue('M', 'T'))` />
                    <!-- Test without space before second arrow -->
                    <Test2 Include=`@(I -> WithMetadataValue('M', 'T')-> WithMetadataValue('M', 'T'))` />
                </ItemGroup>

                <Target Name=`Build`>
                    <Message Text=`Test1: [@(Test1)]` />
                    <Message Text=`Test2: [@(Test2)]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        // Both should produce the same result: B and C
        log.AssertLogContains("Test1: [B;C]");
        log.AssertLogContains("Test2: [B;C]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsCount1()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`foo;bar`/>
                        <J Include=`;`/>
                    </ItemGroup>

                    <Message Text=`[@(I->Count())][@(J->Count())]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("[2][0]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsCount2()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`foo;bar`/>
                        <J Include=`;`/>
                        <K Include=`@(I->Count());@(J->Count())`/>
                    </ItemGroup>

                    <Message Text=`@(K)` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("2;0");
    }

    [Fact]
    public void ExpandItemVectorFunctionsCountOperatingOnEmptyResult1()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`foo;bar`/>
                        <J Include=`;`/>
                    </ItemGroup>

                    <Message Text=`[@(I->Metadata('foo')->Count())][@(J->Metadata('foo')->Count())]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("[0][0]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsCountOperatingOnEmptyResult2()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`foo;bar`/>
                        <J Include=`;`/>
                        <K Include=`@(I->Metadata('foo')->Count());@(J->Metadata('foo')->Count())`/>
                    </ItemGroup>

                    <Message Text=`@(K)` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("0;0");
    }

    [Fact]
    public void ExpandItemVectorFunctionsBuiltIn1()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`foo;bar`/>
                    </ItemGroup>

                    <Message Text=`[@(I->FullPath())]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        string currentDir = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
        log.AssertLogContains($"[{currentDir}foo;{currentDir}bar]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsBuiltIn2()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`foo;bar`/>
                    </ItemGroup>

                    <Message Text=`[@(I->FullPath()->Distinct())]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        string currentDir = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
        log.AssertLogContains($"[{currentDir}foo;{currentDir}bar]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsBuiltIn3()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`foo;bar;foo;bar;foo`/>
                    </ItemGroup>

                    <Message Text=`[@(I->FullPath()->Distinct())]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        string currentDir = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
        log.AssertLogContains($"[{currentDir}foo;{currentDir}bar]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsBuiltIn4()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`foo;bar;foo;bar;foo`/>
                    </ItemGroup>

                    <Message Text=`[@(I->Identity()->Distinct())]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("[foo;bar]");
    }

    [LongPathSupportDisabledFact(fullFrameworkOnly: true, additionalMessage: "https://github.com/dotnet/msbuild/issues/4363")]
    public void ExpandItemVectorFunctionsBuiltIn_PathTooLongError()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo`/>
                    </ItemGroup>

                    <Message Text=`[@(I->FullPath())]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        log.AssertLogContains("MSB4198");
    }

    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486. Cannot have invalid characters in file name on Unix.")]
    public void ExpandItemVectorFunctionsBuiltIn_InvalidCharsError()
    {
        const string Content = """
            <Project DefaultTargets=`t`>
                <Target Name=`t`>
                    <ItemGroup>
                        <I Include=`aaa|||bbb\ccc.txt`/>
                    </ItemGroup>

                    <Message Text=`[@(I->Directory())]` />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        log.AssertLogContains("MSB4198");
    }

    /// <summary>
    ///  Expand an item vector function Metadata()->DirectoryName().
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsGetDirectoryNameOfMetadataValue()
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems("@(i->Metadata('Meta0')->DirectoryName())", itemType: "i");

        items.Count.ShouldBe(10);

        foreach (var item in items)
        {
            item.ItemType.ShouldBe("i");
            item.EvaluatedInclude.ShouldBe(Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory"));
        }
    }

    /// <summary>
    ///  Expand an item vector function Metadata() that contains semi-colon delimited sub-items.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsMetadataValueMultiItem()
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems("@(i->Metadata('Meta10')->DirectoryName())", itemType: "i");

        items.Count.ShouldBe(20);
        items[5].ItemType.ShouldBe("i");
        items[5].EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), "secondd;rectory"));
        items[6].ItemType.ShouldBe("i");
        items[6].EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), "someo;herplace"));
    }

    /// <summary>
    ///  Expand an item vector function Items->ClearMetadata().
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsClearMetadata()
    {
        IList<ProjectItemInstance> items = ExpandItemFunctionIntoItems("@(i->ClearMetadata())", itemType: "i");

        items.Count.ShouldBe(10);
        items[5].ItemType.ShouldBe("i");
        items[5].Metadata.ShouldBeEmpty();
    }

    /// <summary>
    ///  Regression test for bug when there are literally zero items declared
    ///  in the project, we should continue to expand item list references to empty-string
    ///  rather than not expand them at all.
    /// </summary>
    [Fact]
    public void ZeroItemsInProjectExpandsToEmpty()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                <Target Name=`Build` Condition=`'@(foo)'!=''` >
                    <Message Text=`This target should NOT run.`/>
                </Target>
            </Project>
            """);

        log.AssertLogDoesntContain("This target should NOT run.");

        log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                <ItemGroup>
                    <foo Include=`abc` Condition=` '@(foo)' == '' ` />
                </ItemGroup>
                <Target Name=`Build`>
                    <Message Text=`Item list foo contains @(foo)`/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("Item list foo contains abc");
    }

    [Fact]
    public void ItemIncludeContainsMultipleItemReferences()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project DefaultTarget=`ShowProps` ToolsVersion=`msbuilddefaulttoolsversion` >
                <PropertyGroup>
                    <OutputType>Library</OutputType>
                </PropertyGroup>
                <ItemGroup>
                    <CFiles Include=`foo.c;bar.c`/>
                    <ObjFiles Include=`@(CFiles->'%(filename).obj')`/>
                    <ObjFiles Include=`@(CPPFiles->'%(filename).obj')`/>
                    <CleanFiles Condition=`'$(OutputType)'=='Library'` Include=`@(ObjFiles);@(TargetLib)`/>
                </ItemGroup>
                <Target Name=`ShowProps`>
                    <Message Text=`Property OutputType=$(OutputType)`/>
                    <Message Text=`Item ObjFiles=@(ObjFiles)`/>
                    <Message Text=`Item CleanFiles=@(CleanFiles)`/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("Property OutputType=Library");
        log.AssertLogContains("Item ObjFiles=foo.obj;bar.obj");
        log.AssertLogContains("Item CleanFiles=foo.obj;bar.obj");
    }

    /// <summary>
    ///  Bad path with illegal windows chars when getting metadata through ->Metadata function.
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathAndMetadataItemFunctionInvalidWindowsPathChars()
    {
        const string Content = """
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include=':|?*'/>
                </ItemGroup>
                <Target Name='Build'>
                    <Message Text="@(x->Metadata('FullPath'))" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        log.AssertLogContains("MSB4023");
    }

    /// <summary>
    ///  Asking for blank metadata.
    /// </summary>
    [Fact]
    public void InvalidMetadataName()
    {
        const string Content = """
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include='x'/>
                </ItemGroup>
                <Target Name='Build'>
                    <Message Text="@(x->Metadata(''))" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        log.AssertLogContains("MSB4023");
    }

    /// <summary>
    ///  Bad path with illegal windows chars when getting metadata through ->WithMetadataValue function.
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathAndMetadataItemFunctionInvalidWindowsPathChars2()
    {
        const string Content = """
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include=':|?*'/>
                </ItemGroup>
                <Target Name='Build'>
                    <Message Text="@(x->WithMetadataValue('FullPath', 'x'))" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        log.AssertLogContains("MSB4023");
    }

    /// <summary>
    ///  Asking for blank metadata with ->WithMetadataValue.
    /// </summary>
    [Fact]
    public void InvalidMetadataName2()
    {
        const string Content = """
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include='x'/>
                </ItemGroup>
                <Target Name='Build'>
                    <Message Text="@(x->WithMetadataValue('', 'x'))" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        log.AssertLogContains("MSB4023");
    }

    /// <summary>
    ///  Bad path with illegal windows chars when getting metadata through ->AnyHaveMetadataValue function.
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathAndMetadataItemInvalidWindowsPathChars3()
    {
        const string Content = """
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include=':|?*'/>
                </ItemGroup>
                <Target Name='Build'>
                    <Message Text="@(x->AnyHaveMetadataValue('FullPath', 'x'))" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        log.AssertLogContains("MSB4023");
    }

    /// <summary>
    ///  Asking for blank metadata with ->AnyHaveMetadataValue.
    /// </summary>
    [Fact]
    public void InvalidMetadataName3()
    {
        const string Content = """
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include='x'/>
                </ItemGroup>
                <Target Name='Build'>
                    <Message Text="@(x->AnyHaveMetadataValue('', 'x'))" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        log.AssertLogContains("MSB4023");
    }

    /// <summary>
    ///  Filter by metadata presence.
    /// </summary>
    [Fact]
    public void HasMetadata()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project ToolsVersion="msbuilddefaulttoolsversion">
              <ItemGroup>
                <_Item Include="One">
                  <A>aa</A>
                  <B>bb</B>
                  <C>cc</C>
                </_Item>
                <_Item Include="Two">
                  <B>bb</B>
                  <C>cc</C>
                </_Item>
                <_Item Include="Three">
                  <A>aa</A>
                  <C>cc</C>
                </_Item>
                <_Item Include="Four">
                  <A>aa</A>
                  <B>bb</B>
                  <C>cc</C>
                </_Item>
                <_Item Include="Five">
                  <A></A>
                </_Item>
              </ItemGroup>

              <Target Name="AfterBuild">
                <Message Text="[@(_Item->HasMetadata('a'), '|')]"/>
              </Target>
            </Project>
            """);

        log.AssertLogContains("[One|Three|Four]");
    }

    /// <summary>
    ///  Filter items by WithoutMetadataValue function.
    /// </summary>
    [Fact]
    public void WithoutMetadataValue()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <ItemGroup>
                    <_Item Include="One">
                        <A>true</A>
                    </_Item>
                    <_Item Include="Two">
                        <A>false</A>
                    </_Item>
                    <_Item Include="Three">
                        <A></A>
                    </_Item>
                    <_Item Include="Four">
                        <B></B>
                    </_Item>
                </ItemGroup>
                <Target Name="AfterBuild">
                    <Message Text="[@(_Item->WithoutMetadataValue('a', 'true'),'|')]"/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("[Two|Three|Four]");
    }

    [Theory]

    // These modifiers do not require project context.
    [InlineData(ItemSpecModifiers.Filename, false, false)]
    [InlineData(ItemSpecModifiers.Extension, false, false)]
    [InlineData(ItemSpecModifiers.RelativeDir, false, false)]
    [InlineData(ItemSpecModifiers.Identity, false, false)]
    [InlineData(ItemSpecModifiers.ModifiedTime, false, false)]
    [InlineData(ItemSpecModifiers.CreatedTime, false, false)]
    [InlineData(ItemSpecModifiers.AccessedTime, false, false)]

    // These modifiers require the project directory.
    [InlineData(ItemSpecModifiers.FullPath, true, false)]
    [InlineData(ItemSpecModifiers.RootDir, true, false)]
    [InlineData(ItemSpecModifiers.Directory, true, false)]

    // These modifiers require both the project directory and defining-project metadata.
    [InlineData(ItemSpecModifiers.DefiningProjectFullPath, true, true)]
    [InlineData(ItemSpecModifiers.DefiningProjectDirectory, true, true)]
    [InlineData(ItemSpecModifiers.DefiningProjectName, true, true)]
    [InlineData(ItemSpecModifiers.DefiningProjectExtension, true, true)]
    public void QuotedTransformDerivableItemSpecModifierUsesRequiredContext(string modifier, bool usesProjectDirectory, bool usesDefiningProject)
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        string itemSpec = Path.Combine("src", "directory", "File.cs");
        string definingProject = Path.Combine(project.Directory, "Imported.targets");
        var item = new ContextTrackingItem("Compile", itemSpec, project.Directory, definingProject);
        var items = new ItemDictionary<ContextTrackingItem> { item };
        var properties = Properties();
        TestLoggingContext loggingContext = new(null, new BuildEventContext(1, 2, 3, 4));
        var expander = ExpanderFactory.Create(properties, items, loggingContext);

        string? actual = expander.ExpandIntoStringLeaveEscaped(
            $"@(Compile->'%({modifier})')",
            ExpanderOptions.ExpandItems,
            MockElementLocation.Instance);

        string expected = ItemSpecModifiers.GetItemSpecModifier(itemSpec, modifier, project.Directory, definingProject);

        actual.ShouldBe(expected);
        item.ProjectDirectoryAccessCount.ShouldBe(usesProjectDirectory ? 1 : 0);
        item.DefiningProjectAccessCount.ShouldBe(usesDefiningProject ? 1 : 0);
    }

    private sealed class ContextTrackingItem(string itemType, string itemSpec, string projectDirectory, string definingProject) : IItem
    {
        public string Key => itemType;
        public string EvaluatedInclude => itemSpec;
        public string EvaluatedIncludeEscaped => itemSpec;

        public string ProjectDirectory
        {
            get
            {
                ProjectDirectoryAccessCount++;
                return projectDirectory;
            }
        }

        public int ProjectDirectoryAccessCount { get; private set; }

        public int DefiningProjectAccessCount { get; private set; }

        public string GetMetadataValue(string name)
            => GetMetadataValueEscaped(name);

        public string GetMetadataValueEscaped(string name)
        {
            if (name.Equals(ItemSpecModifiers.DefiningProjectFullPath, StringComparison.OrdinalIgnoreCase))
            {
                DefiningProjectAccessCount++;
                return definingProject;
            }

            return string.Empty;
        }
    }

    /// <summary>
    ///  Parity test for the rewritten transform scanner (<c>GetQuotedExpressionMatches</c>): metadata
    ///  references inside a transform must not be qualified with an item name. This pins the error path
    ///  (and its message arguments) so the de-regexed scanner keeps rejecting qualified references,
    ///  including when surrounded by internal whitespace.
    /// </summary>
    [Theory]
    [InlineData("@(i->'%(i.Meta0)')", "%(i.Meta0)")]
    [InlineData("@(i->'%( i . Meta0 )')", "%( i . Meta0 )")]
    public void Transform_QualifiedMetadataThrows(string input, string qualifiedReference)
    {
        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            ExpandItemFunctionIntoString(input));

        // The error reports the offending qualified reference and suggests the unqualified form.
        exception.Message.ShouldContain(qualifiedReference);
        exception.Message.ShouldContain("%(Meta0)");
    }

    [Fact]
    public void TestItemSpecModiferEscaping()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetCurrentDirectory(env.CreateFolder().Path);

        const string Content = """
            <Project DefaultTargets="Build">
                <Target Name="Build">
                    <WriteLinesToFile Overwrite="true" File="unittest.%28msbuild%29.file" Lines="Nothing much here"/>

                    <ItemGroup>
                        <TestFile Include="unittest.%28msbuild%29.file" />
                    </ItemGroup>

                    <Message Text="@(TestFile->FullPath())" />
                    <Message Text="@(TestFile->'%(FullPath)'->Distinct())" />
                    <Delete Files="unittest.%28msbuild%29.file" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content, logger: new MockLogger(_output));

        log.AssertLogDoesntContain("%28");
        log.AssertLogDoesntContain("%29");
    }

    [Fact]
    public void ExpandItemVectorFunctions_GetPathsOfAllDirectoriesAbove()
    {
        // Directory structure:
        // <temp>\
        //    alpha\
        //        .proj
        //        One.cs
        //        beta\
        //            Two.cs
        //            Three.cs
        //        gamma\
        using var env = TestEnvironment.Create(_output);
        var root = env.CreateFolder();

        var alpha = root.CreateDirectory("alpha");
        string content = $"""
            <Project>
                <ItemGroup>
                    <Compile Include="One.cs" />
                    <Compile Include="{Path.Combine("beta", "Two.cs")}" />
                    <Compile Include="{Path.Combine("beta", "Three.cs")}" />
                </ItemGroup>
                <ItemGroup>
                    <MyDirectories Include="@(Compile->GetPathsOfAllDirectoriesAbove())" />
                </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(alpha, ".proj", content);

        var beta = alpha.CreateDirectory("beta");
        var gamma = alpha.CreateDirectory("gamma");

        ProjectInstance projectInstance = new(projectFile.Path);
        ICollection<ProjectItemInstance> myDirectories = projectInstance.GetItems("MyDirectories");

        var includes = myDirectories.Select(i => i.EvaluatedInclude);
        includes.ShouldBeUnique();
        includes.ShouldContain(root.Path);
        includes.ShouldContain(alpha.Path);
        includes.ShouldContain(beta.Path);
        includes.ShouldNotContain(gamma.Path);
    }

    [Fact]
    public void ExpandItemVectorFunctions_GetPathsOfAllDirectoriesAbove_ReturnCanonicalPaths()
    {
        // Directory structure:
        // <temp>\
        //    alpha\
        //        .proj
        //        One.cs
        //        beta\
        //            Two.cs
        //    gamma\
        //        Three.cs
        using var env = TestEnvironment.Create(_output);
        var root = env.CreateFolder();

        var alpha = root.CreateDirectory("alpha");
        string content = $"""
            <Project>
                <ItemGroup>
                    <Compile Include="One.cs" />
                    <Compile Include="{Path.Combine("beta", "Two.cs")}" />
                    <Compile Include="{Path.Combine("..", "gamma", "Three.cs")}" />
                </ItemGroup>
                <ItemGroup>
                    <MyDirectories Include="@(Compile->GetPathsOfAllDirectoriesAbove())" />
                </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(alpha, ".proj", content);

        var beta = alpha.CreateDirectory("beta");
        var gamma = root.CreateDirectory("gamma");

        ProjectInstance projectInstance = new(projectFile.Path);
        ICollection<ProjectItemInstance> myDirectories = projectInstance.GetItems("MyDirectories");

        var includes = myDirectories.Select(i => i.EvaluatedInclude);
        includes.ShouldBeUnique();
        includes.ShouldContain(root.Path);
        includes.ShouldContain(alpha.Path);
        includes.ShouldContain(beta.Path);
        includes.ShouldContain(gamma.Path);
    }

    [Fact]
    public void ExpandItemVectorFunctions_Combine()
    {
        using var env = TestEnvironment.Create(_output);
        var root = env.CreateFolder();

        string content = $"""
            <Project>
                <ItemGroup>
                    <MyDirectory Include="Alpha;Beta;{Path.Combine("Alpha", "Gamma")}" />
                </ItemGroup>
                <ItemGroup>
                    <Squiggle Include="@(MyDirectory->Combine('.squiggle'))" />
                </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", content);

        ProjectInstance projectInstance = new(projectFile.Path);
        ICollection<ProjectItemInstance> squiggles = projectInstance.GetItems("Squiggle");

        var expectedAlphaSquigglePath = Path.Combine("Alpha", ".squiggle");
        var expectedBetaSquigglePath = Path.Combine("Beta", ".squiggle");
        var expectedAlphaGammaSquigglePath = Path.Combine("Alpha", "Gamma", ".squiggle");
        squiggles.Select(i => i.EvaluatedInclude).ShouldBe([
            expectedAlphaSquigglePath,
            expectedBetaSquigglePath,
            expectedAlphaGammaSquigglePath
        ], Case.Insensitive);
    }

    [Fact]
    public void ExpandItemVectorFunctions_Exists_Files()
    {
        // Directory structure:
        // <temp>\
        //    .proj
        //    alpha\
        //        One.cs   // exists
        //        Two.cs   // does not exist
        //        Three.cs // exists
        //        Four.cs  // does not exist
        using var env = TestEnvironment.Create(_output);
        var root = env.CreateFolder();

        string content = $"""
            <Project>
                <ItemGroup>
                    <PotentialCompile Include="{Path.Combine("alpha", "One.cs")}" />
                    <PotentialCompile Include="{Path.Combine("alpha", "Two.cs")}" />
                    <PotentialCompile Include="{Path.Combine("alpha", "Three.cs")}" />
                    <PotentialCompile Include="{Path.Combine("alpha", "Four.cs")}" />
                </ItemGroup>
                <ItemGroup>
                    <Compile Include="@(PotentialCompile->Exists())" />
                </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", content);

        var alpha = root.CreateDirectory("alpha");
        var one = alpha.CreateFile("One.cs");
        var three = alpha.CreateFile("Three.cs");

        ProjectInstance projectInstance = new(projectFile.Path);
        ICollection<ProjectItemInstance> squiggleItems = projectInstance.GetItems("Compile");

        var alphaOnePath = Path.Combine("alpha", "One.cs");
        var alphaThreePath = Path.Combine("alpha", "Three.cs");
        squiggleItems.Select(i => i.EvaluatedInclude).ShouldBe([alphaOnePath, alphaThreePath], Case.Insensitive);
    }

    [Fact]
    public void ExpandItemVectorFunctions_Exists_Directories()
    {
        // Directory structure:
        // <temp>\
        //    .proj
        //    alpha\
        //        beta\    // exists
        //        gamma\   // does not exist
        //        delta\   // exists
        //        epsilon\ // does not exist
        using var env = TestEnvironment.Create(_output);
        var root = env.CreateFolder();

        string content = $"""
            <Project>
                <ItemGroup>
                    <PotentialDirectory Include="{Path.Combine("alpha", "beta")}" />
                    <PotentialDirectory Include="{Path.Combine("alpha", "gamma")}" />
                    <PotentialDirectory Include="{Path.Combine("alpha", "delta")}" />
                    <PotentialDirectory Include="{Path.Combine("alpha", "epsilon")}" />
                </ItemGroup>
                <ItemGroup>
                    <MyDirectory Include="@(PotentialDirectory->Exists())" />
                </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", content);

        var alpha = root.CreateDirectory("alpha");
        var beta = alpha.CreateDirectory("beta");
        var delta = alpha.CreateDirectory("delta");

        ProjectInstance projectInstance = new(projectFile.Path);
        ICollection<ProjectItemInstance> squiggleItems = projectInstance.GetItems("MyDirectory");

        var alphaBetaPath = Path.Combine("alpha", "beta");
        var alphaDeltaPath = Path.Combine("alpha", "delta");
        squiggleItems.Select(i => i.EvaluatedInclude).ShouldBe([alphaBetaPath, alphaDeltaPath], Case.Insensitive);
    }

    /// <summary>
    ///  Test for issue where chained item functions with empty results incorrectly evaluate as non-empty in conditions
    /// </summary>
    [Fact]
    public void ChainedItemFunctionEmptyResultInCondition()
    {
        const string Content = """
            <Project>
                <Target Name="Test">
                    <ItemGroup>
                        <TestItem Include="Test1" Foo="Bar" />
                        <TestItem Include="Test2" />
                    </ItemGroup>

                    <!-- This should be empty because Test1 has Foo='Bar', not 'Baz' -->
                    <PropertyGroup Condition="'@(TestItem->WithMetadataValue('Identity', 'Test1')->WithMetadataValue('Foo', 'Baz'))' == ''">
                        <EmptyResult>TRUE</EmptyResult>
                    </PropertyGroup>

                    <Message Text="EmptyResult=$(EmptyResult)" Importance="high" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content, logger: new MockLogger(_output));

        // The chained WithMetadataValue should return empty, so the condition should be true and EmptyResult should be set
        log.AssertLogContains("EmptyResult=TRUE");
    }

    private static IList<ProjectItemInstance> ExpandItemFunctionIntoItems(string expression, string itemType)
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander(project);
        var itemFactory = ItemFactory(project, itemType);

        return expander.ExpandIntoItemsLeaveEscaped(expression, itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance)
            .ShouldNotBeNull();
    }

    private static string? ExpandItemFunctionIntoString(string expression)
    {
        var expander = CreateItemFunctionExpander();

        return expander.ExpandIntoStringLeaveEscaped(expression, ExpanderOptions.ExpandItems, MockElementLocation.Instance);
    }

    /// <summary>
    ///  Creates an expander populated with some ProjectPropertyInstances and ProjectPropertyItems.
    /// </summary>
    private static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateItemFunctionExpander(ProjectInstance? project = null)
    {
        project ??= ProjectHelpers.CreateEmptyProjectInstance();

        PropertyDictionary<ProjectPropertyInstance> properties = Properties(
            ("p", "v0"),
            ("p", "v1"),
            ("Val", "2"),
            ("a", "filename"));

        ItemDictionary<ProjectItemInstance> items = GenerateItems(count: 10, project, CreateItem);

        IMetadataTable metadata = Metadata(
            ("Culture", "abc%253bdef;$(Gee_Aych_Ayee)"),
            ("Language", "english"));

        return ExpanderFactory.Create(properties, items, metadata);

        static ProjectItemInstance CreateItem(ProjectInstance project, int index)
        {
            ProjectItemInstance item = new(project, itemType: "i", includeEscaped: $"i{index}", project.FullPath);

            string fileBasePath = Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory", "file");

            for (int j = 0; j < 5; j++)
            {
                item.SetMetadata($"Meta{j}", $"{fileBasePath}{j}.ext");
            }

            item.SetMetadata("Meta9", Path.Combine("seconddirectory", "file.ext"));
            item.SetMetadata("Meta10", $";{Path.Combine("someo%3bherplace", "foo.txt")};{Path.Combine("secondd%3brectory", "file.ext")};");
            item.SetMetadata("MetaBlank", "");

            if (index % 2 > 0)
            {
                item.SetMetadata("Even", "true");
                item.SetMetadata("Odd", "false");
            }
            else
            {
                item.SetMetadata("Even", "false");
                item.SetMetadata("Odd", "true");
            }

            return item;
        }
    }

    /// <summary>
    ///  Creates an expander populated with some ProjectPropertyInstances and ProjectPropertyItems.
    /// </summary>
    private static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(ProjectInstance project)
        => ExpanderFactory.Create(
            Properties(
                ("p", "v0"),
                ("p", "v1")),
            GenerateItems(
                count: 2,
                project,
                generator: static (project, i) => new(project, "i", $"i{i}", project.FullPath)),
            new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)));
}
