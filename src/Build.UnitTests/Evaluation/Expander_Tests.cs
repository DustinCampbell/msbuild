// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.BackEnd;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;
using Xunit.Sdk;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#if NET
using OperatingSystem = System.OperatingSystem;
#else
using OperatingSystem = Microsoft.Build.Framework.OperatingSystem;
#endif

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation;

[Trait("Category", "expansion")]
public class Expander_Tests(ITestOutputHelper output)
{
    private static readonly string s_rootPathPrefix = NativeMethodsShared.IsWindows ? @"C:\" : Path.VolumeSeparatorChar.ToString();

    private readonly ITestOutputHelper _output = output;
    private readonly string _dateToParse = new DateTime(2010, 12, 25).ToString(CultureInfo.CurrentCulture);

    [Fact]
    public void ExpandAllIntoTaskItems0()
    {
        var expander = ExpanderFactory.Create(Properties());

        IList<TaskItem> itemsOut = expander.ExpandIntoTaskItemsLeaveEscaped(
            "",
            ExpanderOptions.ExpandProperties,
            location: null);

        ObjectModelHelpers.AssertItemsMatch("", GetTaskArrayFromItemList(itemsOut));
    }

    [Fact]
    public void ExpandAllIntoTaskItems1()
    {
        var expander = ExpanderFactory.Create(Properties());

        IList<TaskItem> itemsOut = expander.ExpandIntoTaskItemsLeaveEscaped(
            "foo",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch(@"foo", GetTaskArrayFromItemList(itemsOut));
    }

    [Fact]
    public void ExpandAllIntoTaskItems2()
    {
        var expander = ExpanderFactory.Create(Properties());

        IList<TaskItem> items = expander.ExpandIntoTaskItemsLeaveEscaped(
            "foo;bar",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch(
            """
            foo
            bar
            """,
            GetTaskArrayFromItemList(items));
    }

    [Fact]
    public void ExpandAllIntoTaskItems3()
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();

        List<ProjectItemInstance> items1 = [
            new(project, "Compile", "foo.cs", project.FullPath),
            new(project, "Compile", "bar.cs", project.FullPath),
        ];

        List<ProjectItemInstance> items2 = [
            new(project, "Resource", "bing.resx", project.FullPath)
        ];

        var itemsByType = Items();
        itemsByType.ImportItems(items1);
        itemsByType.ImportItems(items2);

        TestLoggingContext loggingContext = new(loggingService: null, new BuildEventContext(1, 2, 3, 4));

        var expander = ExpanderFactory.Create(Properties(), itemsByType, loggingContext);

        IList<TaskItem> items = expander.ExpandIntoTaskItemsLeaveEscaped(
            "foo;bar;@(compile);@(resource)",
            ExpanderOptions.ExpandPropertiesAndItems,
            MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch(
            """
            foo
            bar
            foo.cs
            bar.cs
            bing.resx
            """,
            GetTaskArrayFromItemList(items));
    }

    [Fact]
    public void ExpandAllIntoTaskItems4()
    {
        var expander = ExpanderFactory.Create(Properties(
            ("a", "aaa"),
            ("b", "bbb"),
            ("c", "cc;dd")));

        IList<TaskItem> items = expander.ExpandIntoTaskItemsLeaveEscaped(
            "foo$(a);$(b);$(c)",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch(
            """
            fooaaa
            bbb
            cc
            dd
            """,
            GetTaskArrayFromItemList(items));
    }

    /// <summary>
    ///  Expand property expressions into ProjectPropertyInstance items.
    /// </summary>
    [Fact]
    public void ExpandPropertiesIntoProjectPropertyInstances()
    {
        var expander = ExpanderFactory.Create(Properties(
            ("a", "aaa"),
            ("b", "bbb"),
            ("c", "cc;dd")));

        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var itemFactory = ItemFactory(project, itemType: "i");

        IList<ProjectItemInstance> items = expander.ExpandIntoItemsLeaveEscaped(
            "foo$(a);$(b);$(c);$(d",
            itemFactory,
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        items.Count.ShouldBe(5);
    }

    [Fact]
    public void ExpandEmptyPropertyExpressionToEmpty()
        => ExpandProperties("$()").ShouldBeEmpty();

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
            MockElementLocation.Instance);

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
            MockElementLocation.Instance);

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
            MockElementLocation.Instance);

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

        string result = expander.ExpandIntoStringLeaveEscaped(
            "[@(type-&gt;'%($(a)), '%'')]",
            ExpanderOptions.ExpandAll,
            MockElementLocation.Instance);

        result.ShouldBe(@"[@(type-&gt;'%(filename), '%'')]");
    }

    /// <summary>
    ///  Expand an item expression (that isn't a real expression) but includes a metadata reference that till needs to be expanded.
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsInvalid2()
    {
        var expander = CreateItemFunctionExpander();

        string result = expander.ExpandIntoStringLeaveEscaped(
            "[@(i->'%(Meta9))']",
            ExpanderOptions.ExpandAll,
            MockElementLocation.Instance);

        result.ShouldBe(@"[@(i->')']");
    }

    /// <summary>
    ///  Expand an item vector function that is chained into a string.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsChained1()
        => ExpandItemFunctionIntoString("@(i->'%(Meta0)'->'%(Directory)'->Distinct())")
            .ShouldBe(Path.Combine("firstdirectory", "seconddirectory") + Path.DirectorySeparatorChar);

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
            .ShouldBe(@"");

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
        items[5].EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), @"secondd;rectory"));
        items[6].ItemType.ShouldBe("i");
        items[6].EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), @"someo;herplace"));
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

    private static IList<ProjectItemInstance> ExpandItemFunctionIntoItems(string expression, string itemType)
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander(project);
        var itemFactory = ItemFactory(project, itemType);

        return expander.ExpandIntoItemsLeaveEscaped(expression, itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);
    }

    private static string ExpandItemFunctionIntoString(string expression)
    {
        var expander = CreateItemFunctionExpander();

        return expander.ExpandIntoStringLeaveEscaped(expression, ExpanderOptions.ExpandItems, MockElementLocation.Instance);
    }

    /// <summary>
    ///  Creates an expander populated with some ProjectPropertyInstances and ProjectPropertyItems.
    /// </summary>
    private static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateItemFunctionExpander(ProjectInstance project = null)
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
            item.SetMetadata("MetaBlank", @"");

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

    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathInDirectMetadata()
    {
        const string Content = """
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include=':|?*'>
                        <m>%(FullPath)</m>
                    </x>
                </ItemGroup>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectContentUsingBuildManagerExpectResult(Content, BuildResultCode.Failure);

        log.AssertLogContains("MSB4248");
    }

    [LongPathSupportDisabledFact(fullFrameworkOnly: true, additionalMessage: "new enough dotnet.exe transparently opts into long paths")]
    public void PathTooLongInDirectMetadata()
    {
        string content = $"""
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include='{new string('x', 250)}'>
                        <m>%(FullPath)</m>
                    </x>
                </ItemGroup>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectContentUsingBuildManagerExpectResult(content, BuildResultCode.Failure);

        log.AssertLogContains("MSB4248");
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

    [Fact]
    public void DirectItemMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <ItemGroup>
                    <Foo Include=`Foo`>
                        <SENSITIVE>X</SENSITIVE>
                    </Foo>
                </ItemGroup>
                <Target Name=`Build`>
                    <Message Importance=`high` Text=`QualifiedNotMatchCase %(Foo.FileName)=%(Foo.sensitive)`/>
                    <Message Importance=`high` Text=`QualifiedMatchCase %(Foo.FileName)=%(Foo.SENSITIVE)`/>

                    <Message Importance=`high` Text=`UnqualifiedNotMatchCase %(Foo.FileName)=%(sensitive)`/>
                    <Message Importance=`high` Text=`UnqualifiedMatchCase %(Foo.FileName)=%(SENSITIVE)`/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("QualifiedNotMatchCase Foo=X");
        log.AssertLogContains("QualifiedMatchCase Foo=X");
        log.AssertLogContains("UnqualifiedNotMatchCase Foo=X");
        log.AssertLogContains("UnqualifiedMatchCase Foo=X");
    }

    [Fact]
    public void ItemDefinitionGroupMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <ItemDefinitionGroup>
                    <Foo>
                        <SENSITIVE>X</SENSITIVE>
                    </Foo>
                </ItemDefinitionGroup>
                <ItemGroup>
                    <Foo Include=`Foo`/>
                </ItemGroup>
                <Target Name=`Build`>
                    <Message Importance=`high` Text=`QualifiedNotMatchCase %(Foo.FileName)=%(Foo.sensitive)`/>
                    <Message Importance=`high` Text=`QualifiedMatchCase %(Foo.FileName)=%(Foo.SENSITIVE)`/>

                    <Message Importance=`high` Text=`UnqualifiedNotMatchCase %(Foo.FileName)=%(sensitive)`/>
                    <Message Importance=`high` Text=`UnqualifiedMatchCase %(Foo.FileName)=%(SENSITIVE)`/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("QualifiedNotMatchCase Foo=X");
        log.AssertLogContains("QualifiedMatchCase Foo=X");
        log.AssertLogContains("UnqualifiedNotMatchCase Foo=X");
        log.AssertLogContains("UnqualifiedMatchCase Foo=X");
    }

    [Fact]
    public void WellKnownMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <ItemGroup>
                    <Foo Include=`Foo`/>
                </ItemGroup>
                <Target Name=`Build`>
                    <Message Importance=`high` Text=`QualifiedNotMatchCase %(Foo.Identity)=%(Foo.FILENAME)`/>
                    <Message Importance=`high` Text=`QualifiedMatchCase %(Foo.Identity)=%(Foo.FileName)`/>

                    <Message Importance=`high` Text=`UnqualifiedNotMatchCase %(Foo.Identity)=%(FILENAME)`/>
                    <Message Importance=`high` Text=`UnqualifiedMatchCase %(Foo.Identity)=%(FileName)`/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("QualifiedNotMatchCase Foo=Foo");
        log.AssertLogContains("QualifiedMatchCase Foo=Foo");
        log.AssertLogContains("UnqualifiedNotMatchCase Foo=Foo");
        log.AssertLogContains("UnqualifiedMatchCase Foo=Foo");
    }

    /// <summary>
    ///  Verify when there is an error due to an attempt to use a static method that we report the method name.
    /// </summary>
    [Fact]
    public void StaticMethodErrorMessageHaveMethodName()
    {
        var ex = Should.Throw<InvalidProjectFileException>(() =>
        {
            const string Content = """
                <Project DefaultTargets='Build'>
                    <PropertyGroup>
                        <Function>$([System.IO.Path]::Combine(null,''))</Function>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <Message Text='[ $(Function) ]' />
                    </Target>
                </Project>
                """;

            Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);
        });

        ex.Message.ShouldContain("[System.IO.Path]::Combine(null, '')", Case.Insensitive);
    }

    /// <summary>
    ///  Verify when there is an error due to an attempt to use a static method that we report the method name.
    /// </summary>
    [Fact]
    public void StaticMethodErrorMessageHaveMethodName1()
    {
        var ex = Should.Throw<InvalidProjectFileException>(() =>
        {
            const string Content = """
                <Project DefaultTargets='Build'>
                    <PropertyGroup>
                        <Function>$(System.IO.Path::Combine('a','b'))</Function>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <Message Text='[ $(Function) ]' />
                    </Target>
                </Project>
                """;

            Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);
        });

        ex.Message.ShouldContain("System.IO.Path::Combine('a','b')", Case.Insensitive);
    }

    [Fact]
    public void StaticMethodWithThrowawayParameterSupported()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.Int32]::TryParse("3", out _))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """);

        log.FullLog.ShouldContain("Value is True");
    }

    [Fact]
    public void StaticMethodWithThrowawayParameterSupported2()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.Int32]::TryParse("notANumber", out _))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """);

        log.FullLog.ShouldContain("Value is False");
    }

    [Fact]
    public void StaticMethodWithUnderscoreNotConfusedWithThrowaway()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.String]::Join('_', 'asdf', 'jkl'))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """);

        log.FullLog.ShouldContain("Value is asdf_jkl");
    }

    /// <summary>
    ///  Creates a set of complicated item metadata and properties, and items to exercise
    ///  the Expander class.  The data here contains escaped characters, metadata that
    ///  references properties, properties that reference items, and other complex scenarios.
    /// </summary>
    private static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateComplexExpander()
    {
        var metadata = Metadata(
            ("Culture", "abc%253bdef;$(Gee_Aych_Ayee)"),
            ("Language", "english"));

        var project = ProjectHelpers.CreateEmptyProjectInstance();

        ProjectItemInstance i1 = new(
            project,
            "IntermediateAssembly",
            NativeMethodsShared.IsWindows ? @"subdir1\engine.dll" : "subdir1/engine.dll",
            project.FullPath);
        i1.SetMetadata("aaa", "111");

        ProjectItemInstance i2 = new(
            project,
            "IntermediateAssembly",
            NativeMethodsShared.IsWindows ? @"subdir2\tasks.dll" : "subdir2/tasks.dll",
            project.FullPath);
        i2.SetMetadata("bbb", "222");

        List<ProjectItemInstance> intermediateAssemblyItemGroup = [i1, i2];

        ProjectItemInstance i3 = new(project, "Content", "splash.bmp", project.FullPath);
        i3.SetMetadata("ccc", "333");
        List<ProjectItemInstance> contentItemGroup = [i3];

        ProjectItemInstance i4 = new(project, "Resource", "string$(p).resx", project.FullPath);
        i4.SetMetadata("ddd", "444");

        ProjectItemInstance i5 = new(project, "Resource", "dialogs%253b.resx", project.FullPath);
        i5.SetMetadata("eee", "555");

        List<ProjectItemInstance> resourceItemGroup = [i4, i5];

        ProjectItemInstance i6 = new ProjectItemInstance(project, "Content", "about.bmp", project.FullPath);
        i6.SetMetadata("fff", "666");

        List<ProjectItemInstance> contentItemGroup2 = [i6];

        ItemDictionary<ProjectItemInstance> secondaryItemsByName = new ItemDictionary<ProjectItemInstance>();
        secondaryItemsByName.ImportItems(resourceItemGroup);
        secondaryItemsByName.ImportItems(contentItemGroup2);

        var properties = Properties(
            ("Gee_Aych_Ayee", "ghi"),
            ("OutputPath", @"\jk ; l\mno%253bpqr\stu"),
            ("TargetPath", "@(IntermediateAssembly->'%(RelativeDir)')"));

        Lookup lookup = new(secondaryItemsByName, properties);

        // Add primary items
        lookup.EnterScope("x");
        lookup.PopulateWithItems("IntermediateAssembly", intermediateAssemblyItemGroup);
        lookup.PopulateWithItems("Content", contentItemGroup);

        return ExpanderFactory.Create(lookup, lookup, metadata);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoTaskItems with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoTaskItemsComplex()
    {
        var expander = CreateComplexExpander();

        IList<TaskItem> taskItems = expander.ExpandIntoTaskItemsLeaveEscaped(
            "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)",
            ExpanderOptions.ExpandAll,
            MockElementLocation.Instance);

        // the following items are passed to the TaskItem constructor, and thus their ItemSpecs should be
        // in escaped form.
        string expectedItemsString = $"""
            string$(p): ddd=444
            dialogs%253b: eee=555
            splash.bmp: ccc=333
            \jk
            l\mno%253bpqr\stu
            subdir1{Path.DirectorySeparatorChar}: aaa=111
            subdir2{Path.DirectorySeparatorChar}: bbb=222
            english_abc%253bdef
            ghi
            """;

        ObjectModelHelpers.AssertItemsMatch(expectedItemsString, GetTaskArrayFromItemList(taskItems));
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data but in a piecemeal fashion.
    /// </summary>
    [Theory]
    [MemberData(nameof(ComplexStringExpansionPieces))]
    public void ExpandAllIntoStringExpandsComplexExpressionPieces(string expression, string expected)
    {
        var expander = CreateComplexExpander();

        expander.ExpandIntoStringAndUnescape(expression, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

    public static TheoryData<string, string> ComplexStringExpansionPieces => new()
    {
        { "@(Resource->'%(Filename)') ;", "string$(p);dialogs%3b ;" },
        { "@(Content)", "splash.bmp" },
        { "@(NonExistent)", "" },
        { "$(NonExistent)", "" },
        { "%(NonExistent)", "" },
        { "$(OutputPath)", @"\jk ; l\mno%3bpqr\stu" },
        { "$(TargetPath)", $"subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar}" },
        { "%(Language)_%(Culture)", "english_abc%3bdef;ghi" },
    };

    /// <summary>
    ///  Exercises ExpandAllIntoString with an item list using a transform that is empty.
    /// </summary>
    [Theory]
    [InlineData("@(IntermediateAssembly->'')")]
    [InlineData("@(IntermediateAssembly->'%(goop)')")]
    public void ExpandAllIntoStringWithEmptyTransformReturnsSeparators(string expression)
    {
        var expander = CreateComplexExpander();

        expander.ExpandIntoStringAndUnescape(expression, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(";");
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

        string actual = expander.ExpandIntoStringLeaveEscaped(
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
    ///  Exercises ExpandAllIntoString with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringComplex()
    {
        var expander = CreateComplexExpander();

        string input = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        expander.ExpandIntoStringAndUnescape(input, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe($"""
                string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%3bdef;ghi
                """);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringLeaveEscapedComplex()
    {
        var expander = CreateComplexExpander();

        string input = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        expander.ExpandIntoStringLeaveEscaped(input, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe($"""
                string$(p);dialogs%253b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%253bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%253bdef;ghi
                """);
    }

    /// <summary>
    ///  Exercises ExpandIntoStringAndUnescape and ExpanderOptions.Truncate.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringTruncated()
    {
        string manySpaces = new(' ', 2000);
        var properties = Properties("ManySpacesProperty", manySpaces);
        var metadata = Metadata("ManySpacesMetadata", manySpaces);

        var projectItemGroups = new ItemDictionary<ProjectItemInstance>();

        var lookup = new Lookup(projectItemGroups, properties);
        lookup.EnterScope("x");

        var project = ProjectHelpers.CreateEmptyProjectInstance();

        lookup.PopulateWithItems("ManySpacesItem", [
            new(project, "ManySpacesItem", "Foo", project.FullPath),
            new(project, "ManySpacesItem", manySpaces, project.FullPath),
            new(project, "ManySpacesItem", "Bar", project.FullPath),
        ]);

        lookup.PopulateWithItems("Exactly1024", [
            new(project, "Exactly1024", "".PadLeft(1024), project.FullPath),
            new(project, "Exactly1024", "Foo", project.FullPath),
        ]);

        lookup.PopulateWithItems("ManyItems", [
            .. Enumerable.Range(0, 50).Select(index =>
                {
                    ProjectItemInstance item = new(project, "ManyItems", $"ThisIsAFairlyLongFileName_{index}.bmp", project.FullPath);
                    item.SetMetadata("Foo", $"ThisIsAFairlyLongMetadataValue_{index}");
                    return item;
                })
            ]);

        var expander = ExpanderFactory.Create(lookup, lookup, metadata);

        string input = "'%(ManySpacesMetadata)' != '' and '$(ManySpacesProperty)' != '' and '@(ManySpacesItem)' != '' and '@(Exactly1024)' != '' and '@(ManyItems)' != '' and '@(ManyItems->'%(Foo)')' != '' and '@(ManyItems->'%(Nonexistent)')' != ''";

        string expected =
            $"'{"",1021}...' != '' and " +
            $"'{"",1021}...' != '' and " +
            $"'Foo;{"",1017}...' != '' and " +
            $"'{"",1024};...' != '' and " +
            "'ThisIsAFairlyLongFileName_0.bmp;ThisIsAFairlyLongFileName_1.bmp;ThisIsAFairlyLongFileName_2.bmp;...' != '' and " +
            "'ThisIsAFairlyLongMetadataValue_0;ThisIsAFairlyLongMetadataValue_1;ThisIsAFairlyLongMetadataValue_2;...' != '' and " +
            $"';;;...' != ''";

        // NOTE: semicolons in the last part are *weird* because they don't actually mean anything and you get logging like
        //     Target "Build" skipped, due to false condition; ( '@(I->'%(nonexistent)')' == '' ) was evaluated as ( ';' == '' ).
        // but that goes back to MSBuild 4.something so I'm codifying it in this test. If you're here because you cleaned it up
        // and want to fix the test my current opinion is that's fine.

        expander.ExpandIntoStringAndUnescape(input, ExpanderOptions.ExpandAll | ExpanderOptions.Truncate, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a string that does not need expanding.
    ///  In this case the expanded string should be reference identical to the passed in string.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringExpectIdenticalReference()
    {
        var expander = CreateComplexExpander();

        // Create a non-literal string to represent input constructed at runtime during a build.
        string input = $"abc123{new Random().Next()}";
        string expandedString = expander.ExpandIntoStringLeaveEscaped(input, ExpanderOptions.ExpandAll, MockElementLocation.Instance);

        // Verify the input and result remain non-interned.
        string.IsInterned(input).ShouldBeNull();
        string.IsInterned(expandedString).ShouldBeNull();

        // Finally verify Expander indeed didn't create a new string.
        input.ShouldBeSameAs(expandedString);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data and various expander options.
    /// </summary>
    [Theory]
    [MemberData(nameof(ComplexStringExpansionOptions))]
    internal void ExpandAllIntoStringHonorsExpanderOptions(ExpanderOptions options, string expected)
    {
        var expander = CreateComplexExpander();

        const string Expression = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        expander.ExpandIntoStringAndUnescape(Expression, options, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

    public static IEnumerable<object[]> ComplexStringExpansionOptions =>
    [
        [
            ExpanderOptions.ExpandProperties,
            @"@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ; %(NonExistent) ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; %(Language)_%(Culture)"
        ],
        [
            ExpanderOptions.ExpandPropertiesAndMetadata,
            @"@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ;  ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; english_abc%3bdef;ghi"
        ],
        [
            ExpanderOptions.ExpandAll,
            $@"string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%3bdef;ghi"
        ],
        [
            ExpanderOptions.ExpandItems,
            @"string$(p);dialogs%3b ; splash.bmp ;  ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)"
        ],
    ];

    private static IMetadataTable CreateMetadata()
        => Metadata(
            ("Culture", "en-US"),
            ("Foo", "Bar"),
            ("Compile.Link", "Link.cs"),
            ("Filename", "App"));

    /// <summary>
    ///  Parity tests for the hand-written metadata scanner. These pin the exact expanded result for
    ///  whitespace handling, malformed references, nested references, qualified vs. unqualified
    ///  names, and missing metadata so future edits to the scanner cannot silently regress them.
    /// </summary>
    [Theory]
    [MemberData(nameof(MetadataScannerEdgeCasesData))]
    public void ExpandMetadata_ScannerEdgeCases(string input, string expected)
        => ExpandMetadata(input, CreateMetadata())
            .ShouldBe(expected);

    public static TheoryData<string, string> MetadataScannerEdgeCasesData => new()
    {
        // Simple expansion, unqualified and qualified.
        { "%(Culture)", "en-US" },
        { "%(Foo)", "Bar" },
        { "%(Compile.Link)", "Link.cs" },

        // Whitespace around the parentheses and the dot separator is allowed.
        { "%( Culture )", "en-US" },
        { "%( Compile . Link )", "Link.cs" },

        // Missing metadata expands to empty; a missing qualifier does not fall back to the unqualified key.
        { "%(DoesNotExist)", "" },
        { "%(Other.Foo)", "" },

        // Malformed references are emitted verbatim.
        { "%(", "%(" },
        { "%()", "%()" },
        { "%( )", "%( )" },
        { "%(.x)", "%(.x)" },

        // The outer reference is not closed by ')', so only the inner reference expands.
        { "%(Culture%(Foo))", "%(CultureBar)" },

        // Mixed with surrounding literal text and adjacent references.
        { "prefix_%(Culture)_suffix", "prefix_en-US_suffix" },
        { "%(Culture)%(Foo)", "en-USBar" },
    };

    [Theory]
    [MemberData(nameof(MetadataNoExpansionReturnsOriginalStringData))]
    internal void ExpandMetadata_NoExpansionReturnsOriginalString(string input, ExpanderOptions options)
        => ExpandMetadata(input, CreateMetadata(), options)
            .ShouldBeSameAs(input);

    public static IEnumerable<object[]> MetadataNoExpansionReturnsOriginalStringData =>
    [
        ["%(", ExpanderOptions.ExpandMetadata],
        ["%(Culture)", ExpanderOptions.ExpandBuiltInMetadata],
        ["%(Filename)", ExpanderOptions.ExpandCustomMetadata],
    ];

    /// <summary>
    ///  Parity tests for metadata expansion in the gaps between (and within the separators of) item
    ///  vector expressions. Items are intentionally left unexpanded (ExpandMetadata only) so the
    ///  assertions isolate the gap/separator boundary handling in ScanAndExpandMetadataInGaps,
    ///  including the case where "@(" appears but does not form a well-formed item vector.
    /// </summary>
    [Theory]
    [MemberData(nameof(MetadataItemVectorGapsAndSeparatorsData))]
    public void ExpandMetadata_ItemVectorGapsAndSeparators(string input, string expected)
        => ExpandMetadata(input, CreateMetadata())
            .ShouldBe(expected);

    public static TheoryData<string, string> MetadataItemVectorGapsAndSeparatorsData => new()
    {
        // Metadata after, before, and between item vectors.
        { "@(Compile)%(Culture)", "@(Compile)en-US" },
        { "%(Culture)@(Compile)", "en-US@(Compile)" },
        { "@(A)%(Culture)@(B)", "@(A)en-US@(B)" },

        // A lone item vector has no gaps and is returned unchanged, even with embedded metadata in a transform.
        { "@(Compile)", "@(Compile)" },
        { "@(Compile->'%(Filename)')", "@(Compile->'%(Filename)')" },

        // Metadata embedded in an item vector's separator is expanded in place.
        { "@(Compile, '%(Culture)')", "@(Compile, 'en-US')" },

        // "@(" that does not form a valid item vector still has its surrounding metadata expanded.
        { "%(Culture)@(", "en-US@(" },
    };

    /// <summary>
    ///  Verifies the built-in vs. custom metadata gating in the scanner: a reference is expanded only
    ///  when the matching <see cref="ExpanderOptions"/> flag is set; otherwise it is emitted verbatim.
    /// </summary>
    /// <remarks>
    ///  Declared <c>internal</c> because <see cref="ExpanderOptions"/> is internal; this assembly is
    ///  configured to discover non-public test methods.
    /// </remarks>
    [Theory]
    [MemberData(nameof(MetadataBuiltInVsCustomGatingData))]
    internal void ExpandMetadata_BuiltInVsCustomGating(string input, ExpanderOptions options, string expected)
        => ExpandMetadata(input, CreateMetadata(), options)
            .ShouldBe(expected);

    public static IEnumerable<object[]> MetadataBuiltInVsCustomGatingData =>
    [
        // Custom metadata (Culture) only expands with ExpandCustomMetadata.
        ["%(Culture)", ExpanderOptions.ExpandCustomMetadata, "en-US"],
        ["%(Culture)", ExpanderOptions.ExpandBuiltInMetadata, "%(Culture)"],

        // Built-in metadata (Filename) only expands with ExpandBuiltInMetadata.
        ["%(Filename)", ExpanderOptions.ExpandBuiltInMetadata, "App"],
        ["%(Filename)", ExpanderOptions.ExpandCustomMetadata, "%(Filename)"],
    ];

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

    /// <summary>
    ///  Exercises ExpandAllIntoStringListLeaveEscaped with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringListLeaveEscapedComplex()
    {
        var expander = CreateComplexExpander();

        string value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        IList<string> expanded = [.. expander.ExpandIntoStringListLeaveEscaped(value, ExpanderOptions.ExpandAll, MockElementLocation.Instance)];

        expanded.Count.ShouldBe(9);
        expanded[0].ShouldBe("string$(p)");
        expanded[1].ShouldBe("dialogs%253b");
        expanded[2].ShouldBe("splash.bmp");
        expanded[3].ShouldBe("\\jk");
        expanded[4].ShouldBe("l\\mno%253bpqr\\stu");
        expanded[5].ShouldBe($"subdir1{Path.DirectorySeparatorChar}");
        expanded[6].ShouldBe($"subdir2{Path.DirectorySeparatorChar}");
        expanded[7].ShouldBe("english_abc%253bdef");
        expanded[8].ShouldBe("ghi");
    }

    private static ITaskItem[] GetTaskArrayFromItemList(IList<TaskItem> list)
    {
        ITaskItem[] items = new ITaskItem[list.Count];
        for (int i = 0; i < list.Count; ++i)
        {
            items[i] = list[i];
        }

        return items;
    }

    /// <summary>
    ///  v10.0\TeamData\Microsoft.Data.Schema.Common.targets shipped with bad syntax:
    ///  $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory)
    ///  this was evaluating to blank before, now it errors; we have to special case it to
    ///  evaluate to blank.
    ///  Note that this still works whether or not the key exists and has a value.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixSpecialCase()
        => ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory)")
            .ShouldBeEmpty();

    // Compat hack: WebProjects may have an import with a condition like:
    //       Condition=" '$(Solutions.VSVersion)' == '8.0'"
    // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
    // Be compatible by returning an empty string here.
    [Fact]
    public void Regress692569()
        => ExpandProperties(@"$(Solutions.VSVersion)")
            .ShouldBeEmpty();

    /// <summary>
    ///  In the general case, we should still error for properties that incorrectly miss the Registry: prefix.
    ///  Note that this still fails whether or not the key exists.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixError()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@XXXXDBDirectory)"));

    /// <summary>
    ///  In the general case, we should still error for properties that incorrectly miss the Registry: prefix, like
    ///  the special case, but with extra char on the end.
    ///  Note that this still fails whether or not the key exists.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixError2()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectoryX)"));

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyString()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", "String", RegistryValueKind.String);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("String");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyBinary()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", Encoding.UTF8.GetBytes("String"), RegistryValueKind.Binary);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("83;116;114;105;110;103");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyDWord()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", 123456, RegistryValueKind.DWord);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("123456");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyExpandString()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue("Value", $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyQWord()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", 123456789123456789, RegistryValueKind.QWord);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("123456789123456789");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyMultiString()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", new[] { "A", "B", "C", "D" }, RegistryValueKind.MultiString);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("A;B;C;D");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
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
    public void TestGetPathToReferenceAssembliesAsFunction()
    {
        if (ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version48) is null)
        {
            // if there aren't any reference assemblies installed on the machine in the first place, of course
            // we're not going to find them. :)
            return;
        }

        const string Content = $"""
            <Project ToolsVersion="msbuilddefaulttoolsversion">

                <PropertyGroup>
                    <TargetFrameworkIdentifier>.NETFramework</TargetFrameworkIdentifier>
                    <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                    <TargetFrameworkProfile></TargetFrameworkProfile>
                    <TargetFrameworkMoniker>$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)</TargetFrameworkMoniker>
                </PropertyGroup>

                <Target Name="Build">
                    <GetReferenceAssemblyPaths
                        Condition=" '$(TargetFrameworkDirectory)' == '' and '$(TargetFrameworkMoniker)' !=''"
                        TargetFrameworkMoniker="$(TargetFrameworkMoniker)"
                        RootPath="$(TargetFrameworkRootPath)"
                    >
                        <Output TaskParameter="ReferenceAssemblyPaths" PropertyName="ReferenceAssemblyPathsFromTask"/>
                    </GetReferenceAssemblyPaths>

                    <PropertyGroup>
                        <ReferenceAssemblyPathsFromFunction>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToStandardLibraries($(TargetFrameworkIdentifier), $(TargetFrameworkVersion), $(TargetFrameworkProfile)))\</ReferenceAssemblyPathsFromFunction>
                    </PropertyGroup>

                    <Message Text="Task:     $(ReferenceAssemblyPathsFromTask)" Importance="High" />
                    <Message Text="Function: $(ReferenceAssemblyPathsFromFunction)" Importance="High" />

                    <Warning Text="Reference assembly paths do not match!" Condition="'$(ReferenceAssemblyPathsFromFunction)' != '$(ReferenceAssemblyPathsFromTask)'" />
                </Target>

            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogDoesntContain("Reference assembly paths do not match");
    }

    /// <summary>
    ///  Expand property function that takes a null argument.
    /// </summary>
    [Fact]
    public void PropertyFunctionNullArgument()
        => ExpandProperties("$([System.Convert]::ChangeType('null',$(SomeStuff.GetTypeCode())))")
            .ShouldBe("null");

    /// <summary>
    ///  Expand a null-returning property function alone or concatenated with literal text.
    /// </summary>
    [Theory]
    [InlineData("$([System.Environment]::GetEnvironmentVariable(`_NonExistentVar`))", "")]
    [InlineData("prefix_$([System.Environment]::GetEnvironmentVariable(`_NonExistentVar`))", "prefix_")]
    public void NullReturningPropertyFunctionExpandsToEmptyString(string expression, string expected)
    {
        using var env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("_NonExistentVar", null);

        ExpandProperties(expression)
            .ShouldBe(expected);
    }

    /// <summary>
    ///  Expand property function that takes no arguments and returns a string.
    /// </summary>
    [Fact]
    public void PropertyFunctionNoArguments()
        => ExpandProperties("$(SomeStuff.ToUpperInvariant())", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("THIS IS SOME STUFF");

    /// <summary>
    ///  Expand property function that takes no arguments and returns a string (trimmed).
    /// </summary>
    [Fact]
    public void PropertyFunctionNoArgumentsTrim()
        => ExpandProperties("$(FileName.Trim())", Properties("FileName", "    foo.ext   "))
            .ShouldBe("foo.ext");

    /// <summary>
    ///  Expand property function that is a get property accessor.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyGet()
        => ExpandProperties("$(SomeStuff.Length)", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("18");

    /// <summary>
    ///  Expand property function which is a manual get property accessor.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyManualGet()
        => ExpandProperties("$(SomeStuff.get_Length())", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("18");

    /// <summary>
    ///  Expand a property function followed by literal text.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyNoArgumentsConcat()
        => ExpandProperties("$(SomeStuff.ToLowerInvariant())_goop", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("this is some stuff_goop");

    /// <summary>
    ///  Expand property function with a constant argument.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgument()
        => ExpandProperties("$(SomeStuff.SubString(13))", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("STUff");

    /// <summary>
    ///  Expand a property function whose returned substring contains spaces.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentWithSpaces()
        => ExpandProperties("$(SomeStuff.SubString(8))", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("SOME STUff");

    /// <summary>
    ///  Expand property function with a constant argument.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyPathRootSubtraction()
        => ExpandProperties(
            "$(MyPath.SubString($(RootPath.Length)))",
            Properties(
                ("RootPath", Path.Combine(s_rootPathPrefix, "this", "is", "the", "root")),
                ("MyPath", Path.Combine(s_rootPathPrefix, "this", "is", "the", "root", "my", "project", "is", "here.proj"))))
            .ShouldBe(Path.Combine(Path.DirectorySeparatorChar.ToString(), "my", "project", "is", "here.proj"));

    /// <summary>
    ///  Expand property function with an argument that is a property.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentExpandedProperty()
        => ExpandProperties(
            "$(SomeStuff.SubString(1$(Value)))",
            Properties(
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("STUff");

    /// <summary>
    ///  Expand property function that has a boolean return value.
    /// </summary>
    [Theory]
    [InlineData("PathRoot2", "True")]
    [InlineData("PathRoot", "False")]
    public void PropertyFunctionPropertyWithArgumentBooleanReturn(string propertyName, string expected)
        => ExpandProperties(
            $"$({propertyName}.Endswith({Path.DirectorySeparatorChar}))",
            Properties(
                ("PathRoot", Path.Combine(s_rootPathPrefix, "goo")),
                ("PathRoot2", $"{Path.Combine(s_rootPathPrefix, "goop")}{Path.DirectorySeparatorChar}")))
            .ShouldBe(expected);

    /// <summary>
    ///  Expand property function with an argument that is expanded, and a chaining of other functions.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentNestedAndChainedFunction()
        => ExpandProperties(
            "$(SomeStuff.SubString(1$(Value)).ToLowerInvariant().SubString($(Value)))",
            Properties(
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("ff");

    /// <summary>
    ///  Expand property function with chained functions on its results.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentChained()
        => ExpandProperties(
            "$(SomeStuff.ToUpperInvariant().ToLowerInvariant())",
            Properties(
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("this is some stuff");

    /// <summary>
    ///  Expand property function with an argument that is a function.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentNested()
        => ExpandProperties(
            "$(SomeStuff.SubString($(Value.get_Length())))",
            Properties(
                ("Value", "12345"),
                ("SomeStuff", "1234567890")))
            .ShouldBe("67890");

    /// <summary>
    ///  Expand property function that returns a generic list.
    /// </summary>
    [Fact]
    public void PropertyFunctionGenericListReturn()
        => ExpandProperties("$([MSBuild]::__GetListTest())")
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Expand property function that returns an array.
    /// </summary>
    [Fact]
    public void PropertyFunctionArrayReturn()
        => ExpandProperties("$(List.Split(-))", Properties("List", "A-B-C-D"))
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Expand property function that returns a Dictionary.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionDictionaryReturn()
    {
        string result = ExpandProperties("$([System.Environment]::GetEnvironmentVariables())");

        string expected = "OS=" + Environment.GetEnvironmentVariable("OS");

        result.ShouldContain(expected, Case.Insensitive);
    }

    /// <summary>
    ///  Expand property function that returns an array.
    /// </summary>
    [Fact]
    public void PropertyFunctionArrayReturnManualSplitter()
        => ExpandProperties(
            "$(List.Split($(Splitter.ToCharArray())))",
            Properties(
                ("List", "A-B-C-D"),
                ("Splitter", "-")))
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Evaluate conditions containing boolean-returning property functions.
    /// </summary>
    [Theory]
    [MemberData(nameof(PropertyFunctionConditions))]
    public void PropertyFunctionInCondition(string expression)
    {
        var expander = ExpanderFactory.Create(Properties(
            ("PathRoot", Path.Combine(s_rootPathPrefix, "goo")),
            ("PathRoot2", Path.Combine(s_rootPathPrefix, "goop") + Path.DirectorySeparatorChar)));

        bool result = ConditionEvaluator.EvaluateCondition(
            expression,
            ParserOptions.AllowAll,
            expander,
            ExpanderOptions.ExpandProperties,
            Directory.GetCurrentDirectory(),
            MockElementLocation.Instance,
            FileSystems.Default,
            new TestLoggingContext(null, new BuildEventContext(1, 2, 3, 4)));

        result.ShouldBeTrue();
    }

    public static TheoryData<string> PropertyFunctionConditions => new()
    {
        $"'$(PathRoot2.Endswith(`{Path.DirectorySeparatorChar}`))' == 'true'",
        $"'$(PathRoot.EndsWith({Path.DirectorySeparatorChar}))' == 'false'",
    };

    /// <summary>
    ///  Reject property references used as functions or with unsupported members.
    /// </summary>
    [Theory]
    [InlineData("[$(SomeStuff($(Value)))]")]
    [InlineData("[$(SomeStuff.Lgg)]")]
    [InlineData("$(SomeStuff.ToUpperInvariant().Foo)")]
    [InlineData("[$(SomeStuff($(System.DateTime.Now)))]")]
    public void InvalidPropertyMemberExpressionsThrow(string expression)
    {
        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                expression,
                Properties(
                    ("Value", "3"),
                    ("SomeStuff", "This IS SOME STUff"))));
    }

    /// <summary>
    ///  Expand property function - invalid expression.
    /// </summary>
    [Fact]
    public void PropertyFunctionWithTextInsideCallThrows()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(SomeStuff.ToLowerInvariant()_goop)", Properties("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    ///  Reject substring arguments that cannot be converted or are out of range.
    /// </summary>
    [Theory]
    [InlineData("[$(SomeStuff.Substring(HELLO!))]")]
    [InlineData("[$(SomeStuff.Substring(-10))]")]
    public void PropertyFunctionWithInvalidSubstringArgumentThrows(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(expression, Properties("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    ///  Expand property function that calls a static method with quoted arguments.
    /// </summary>
    [Fact]
    public void ParenthesizedStaticPropertyFunctionThrows()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(([System.DateTime]::Now).ToString(\"MM.dd.yyyy\"))"));

    /// <summary>
    ///  Expand property function - we don't handle metadata functions.
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalidNoMetadataFunctions()
        => ExpandProperties("[%(LowerLetterList.Identity.ToUpper())]")
            .ShouldBe("[%(LowerLetterList.Identity.ToUpper())]");

    /// <summary>
    ///  Expand property function - properties won't get confused with a type or namespace.
    /// </summary>
    [Fact]
    public void PropertyFunctionNoCollisionsOnType()
        => ExpandProperties("$(System)", Properties("System", "The System Namespace"))
            .ShouldBe("The System Namespace");

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodMakeRelative()
        => ExpandProperties(
            @"$([MSBuild]::MakeRelative($(ParentPath), `$(FilePath)`))",
            Properties(
                ("ParentPath", Path.Combine(s_rootPathPrefix, "abc", "def")),
                ("FilePath", Path.Combine(s_rootPathPrefix, "abc", "def", "foo.cpp"))))
            .ShouldBe(@"foo.cpp");

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethod1()
        => ExpandProperties(
            @"$([System.IO.Path]::Combine($(Drive), `$(File)`))",
            Properties(
                ("Drive", s_rootPathPrefix),
                ("File", Path.Combine("foo", "file.txt"))))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Expand property function that creates an instance of a type.
    /// </summary>
    [Fact]
    public void PropertyFunctionConstructor1()
        => ExpandProperties(@"$([System.Version]::new($(ver1)).ToString())", Properties("ver1", @"1.2.3.4"))
            .ShouldBe("1.2.3.4");

    /// <summary>
    ///  Expand property function that creates an instance of a type.
    /// </summary>
    [Fact]
    public void PropertyFunctionConstructor2()
        => ExpandProperties(
            @"$([System.Version]::new($(ver1)).CompareTo($([System.Version]::new($(ver2)))))",
            Properties(
                ("ver1", @"1.2.3.4"),
                ("ver2", @"2.2.3.4")))
            .ShouldBe(@"-1");

    /// <summary>
    ///  Expand property function that is only available when MSBUILDENABLEALLPROPERTYFUNCTIONS=1.
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: "https://github.com/dotnet/coreclr/issues/15662")]
    public void PropertyStaticFunctionAllEnabled()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());

        try
        {
            ExpandProperties("$([System.Type]::GetType(`System.Type`))")
                .ShouldBe("System.Type");
        }
        finally
        {
            AvailableStaticMethods.Reset_ForUnitTestsOnly();
        }
    }

    /// <summary>
    ///  Expand property function that is defined (on CoreFX) in an assembly named after its full namespace.
    /// </summary>
    [Fact]
    public void PropertyStaticFunctionLocatedFromAssemblyWithNamespaceName()
    {
        AppContext.TryGetSwitch("Microsoft.Build.EnableAllPropertyFunctions", out bool originalSwitch);

        try
        {
            AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", true);

            string result = ExpandProperties("$([System.Diagnostics.Process]::GetCurrentProcess().Id)");

            int.TryParse(result, out int pid).ShouldBeTrue();
            pid.ShouldBe(EnvironmentUtilities.CurrentProcessId);
        }
        finally
        {
            AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", originalSwitch);
            AvailableStaticMethods.Reset_ForUnitTestsOnly();
        }
    }

    /// <summary>
    ///  Expand property function that is only available when MSBUILDENABLEALLPROPERTYFUNCTIONS=1, but cannot be found.
    /// </summary>
    [Theory]
    [InlineData("$([Microsoft.FOO.FileIO.FileSystem]::CurrentDirectory)")]
    [InlineData("$([Foo.Baz]::new())")]
    [InlineData("$([Foo]::new())")]
    [InlineData("$([Foo.]::new())")]
    [InlineData("$([.Foo]::new())")]
    [InlineData("$([.]::new())")]
    [InlineData("$([]::new())")]
    public void PropertyStaticFunctionUsingNamespaceNotFound(string expression)
    {
        using var env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

        try
        {
            Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties(expression));
        }
        finally
        {
            AvailableStaticMethods.Reset_ForUnitTestsOnly();
        }
    }

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodQuoted1()
        => ExpandProperties(
            @"$([System.IO.Path]::Combine(`" + s_rootPathPrefix + "`, `$(File)`))",
            Properties("File", Path.Combine("foo", "file.txt")))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Preserve spaces inside quoted path arguments, including trailing spaces.
    /// </summary>
    [Theory]
    [InlineData("foo goo", "foo goo", "file.txt")]
    [InlineData("foo baz", "foo bar", "baz.txt")]
    [InlineData("foo baz ", "foo bar", "baz.txt")]
    public void QuotedPathArgumentsPreserveSpaces(string parentDirectory, string childDirectory, string fileName)
    {
        string parentPath = Path.Combine(s_rootPathPrefix, parentDirectory);

        ExpandProperties(
            $"$([System.IO.Path]::Combine(`{parentPath}`, `$(File)`))",
            Properties("File", Path.Combine(childDirectory, fileName)))
            .ShouldBe(Path.Combine(parentPath, childDirectory, fileName));
    }

    /// <summary>
    ///  Expand property function that calls a static method with quoted arguments
    /// </summary>
    [Theory]
    [InlineData("yyyy/MM/dd HH:mm:ss")]
    [InlineData("MM.dd.yyyy")]
    public void PropertyFunctionDateTimeParseWithQuotedFormat(string format)
        => ExpandProperties($"$([System.DateTime]::Parse('{_dateToParse}').ToString(\"{format}\"))")
            .ShouldBe(DateTime.Parse(_dateToParse).ToString(format));

    /// <summary>
    ///  Expand property function that calls a static method with quoted arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted4()
        => ExpandProperties("$([System.DateTime]::Now.ToString(\"MM.dd.yyyy\"))")
            .ShouldBe(DateTime.Now.ToString("MM.dd.yyyy"));

    /// <summary>
    ///  Expand property function that calls a static method
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodNested()
        => ExpandProperties(
            @"$([System.IO.Path]::Combine(`" +
            s_rootPathPrefix +
            @"`, $([System.IO.Path]::Combine(`foo`,`file.txt`))))",
            Properties("File", "foo" + Path.DirectorySeparatorChar + "file.txt"))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Expand property function that calls a static method regex
    /// </summary>
    [Theory]
    [MemberData(nameof(RegexIsMatchExpressions))]
    public void PropertyFunctionRegexIsMatch(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    public static TheoryData<string, string> RegexIsMatchExpressions => new()
    {
        { @"$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, `RegexOptions.IgnoreCase,RegexOptions.Singleline`))", @"True" },
        { @"$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, System.Text.RegularExpressions.RegexOptions.IgnoreCase|RegexOptions.Singleline))", @"True" },
        { @"$([System.Text.RegularExpressions.Regex]::IsMatch(`100 GBP`, `^-?\d+(\.\d{2})?$`))", @"False" },
    };

    /// <summary>
    ///  Expand property function that calls a static method  with an instance method chained
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodChained()
    {
        string dateTime = "'" + _dateToParse + "'";
        ExpandProperties(@"$([System.DateTime]::Parse(" + dateTime + ").ToString(`yyyy/MM/dd HH:mm:ss`))")
            .ShouldBe(DateTime.Parse(_dateToParse).ToString("yyyy/MM/dd HH:mm:ss"));
    }

    /// <summary>
    ///  Expand property function that calls a static method available only on net46 (Environment.GetFolderPath)
    /// </summary>
    [Fact]
    public void PropertyFunctionGetFolderPath()
        => ExpandProperties(@"$([System.Environment]::GetFolderPath(SpecialFolder.System))")
            .ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.System));

    /// <summary>
    ///  The test exercises: RuntimeInformation / OSPlatform usage, static method invocation, static property invocation, method invocation expression as argument, call chain expression as argument
    /// </summary>
    [Theory]
    [InlineData(
        "$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::Create($([System.Runtime.InteropServices.OSPlatform]::$$platform$$.ToString())))))",
        "True")]
    [InlineData(
        @"$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::$$platform$$)))",
        "True")]
    [InlineData(
        "$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)",
        "$$architecture$$")]
    [InlineData(
        "$([MSBuild]::IsOSPlatform($$platform$$))",
        "True")]
    public void PropertyFunctionRuntimeInformation(string propertyFunction, string expectedExpansion)
    {
        Func<string, string, string, string> formatString = (aString, platform, architecture) => aString
            .Replace("$$platform$$", platform)
            .Replace("$$architecture$$", architecture);

        string currentPlatformString = Helpers.GetOSPlatformAsString();

        var currentArchitectureString = RuntimeInformation.OSArchitecture.ToString();

        propertyFunction = formatString(propertyFunction, currentPlatformString, currentArchitectureString);
        expectedExpansion = formatString(expectedExpansion, currentPlatformString, currentArchitectureString);

        ExpandProperties(propertyFunction)
            .ShouldBe(expectedExpansion);
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("linux")]
    [InlineData("macos")]
    [InlineData("osx")]
    public void IsOSPlatform(string platform)
    {
        string expected = OperatingSystem.IsOSPlatform(platform) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsOSPlatform('{platform}'))")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData("windows", 4, 0, 0, 0)]
    [InlineData("windows", 999, 0, 0, 0)]
    [InlineData("linux", 0, 0, 0, 0)]
    [InlineData("macos", 10, 15, 0, 0)]
    [InlineData("macos", 999, 0, 0, 0)]
    [InlineData("osx", 0, 0, 0, 0)]
    public void IsOSPlatformVersionAtLeast(string platform, int major, int minor, int build, int revision)
    {
        string expected = OperatingSystem.IsOSPlatformVersionAtLeast(platform, major, minor, build, revision) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsOSPlatformVersionAtLeast('{platform}', {major}, {minor}, {build}, {revision}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsLinux()
    {
        string expected = OperatingSystem.IsLinux() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsLinux())")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsFreeBSD()
    {
        string expected = OperatingSystem.IsFreeBSD() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsFreeBSD())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsFreeBSDVersionAtLeast(int major, int minor, int build, int revision)
    {
        string expected = OperatingSystem.IsFreeBSDVersionAtLeast(major, minor, build, revision) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsFreeBSDVersionAtLeast({major}, {minor}, {build}, {revision}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsMacOS()
    {
        string expected = OperatingSystem.IsMacOS() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsMacOS())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 15, 0)]
    [InlineData(999, 0, 0)]
    public void IsMacOSVersionAtLeast(int major, int minor, int build)
    {
        string expected = OperatingSystem.IsMacOSVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsMacOSVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsWindows()
    {
        string expected = OperatingSystem.IsWindows() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsWindows())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(4, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsWindowsVersionAtLeast(int major, int minor, int build, int revision)
    {
        string expected = OperatingSystem.IsWindowsVersionAtLeast(major, minor, build, revision) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsWindowsVersionAtLeast({major}, {minor}, {build}, {revision}))")
            .ShouldBe(expected);
    }

#if NET
    [Fact]
    public void IsAndroid()
    {
        string expected = OperatingSystem.IsAndroid() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsAndroid())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsAndroidVersionAtLeast(int major, int minor, int build, int revision)
    {
        string expected = OperatingSystem.IsAndroidVersionAtLeast(major, minor, build, revision) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsAndroidVersionAtLeast({major}, {minor}, {build}, {revision}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsIOS()
    {
        string expected = OperatingSystem.IsIOS() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsIOS())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(16, 5, 1)]
    [InlineData(999, 0, 0)]
    public void IsIOSVersionAtLeast(int major, int minor, int build)
    {
        string expected = OperatingSystem.IsIOSVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsIOSVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsMacCatalyst()
    {
        string expected = OperatingSystem.IsMacCatalyst() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsMacCatalyst())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(999, 0, 0)]
    public void IsMacCatalystVersionAtLeast(int major, int minor, int build)
    {
        string expected = OperatingSystem.IsMacCatalystVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsMacCatalystVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsTvOS()
    {
        string expected = OperatingSystem.IsTvOS() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsTvOS())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(16, 5, 0)]
    [InlineData(999, 0, 0)]
    public void IsTvOSVersionAtLeast(int major, int minor, int build)
    {
        string expected = OperatingSystem.IsTvOSVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsTvOSVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsWatchOS()
    {
        string expected = OperatingSystem.IsWatchOS() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsWatchOS())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(9, 5, 2)]
    [InlineData(999, 0, 0)]
    public void IsWatchOSVersionAtLeast(int major, int minor, int build)
    {
        string expected = OperatingSystem.IsWatchOSVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsWatchOSVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(expected);
    }
#endif

    [Theory]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('x', 1))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('x45', 1))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('x', 1, 4))", "3")]
    // 9 is not a valid StringComparison enum value
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('x', 9))", "10")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X', 'StringComparison.Ordinal'))", "-1")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X', 'StringComparison.OrdinalIgnoreCase'))", "0")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X4', 'StringComparison.OrdinalIgnoreCase'))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X4', 1, 'StringComparison.OrdinalIgnoreCase'))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X', 1, 3, 'StringComparison.OrdinalIgnoreCase'))", "3")]
    public void StringIndexOfTests(string propertyName, string propertyValue, string propertyFunction, string expectedExpansion)
        => ExpandProperties(propertyFunction, Properties(propertyName, propertyValue))
            .ShouldBe(expectedExpansion);

    [Theory]
    [InlineData("AString", "HelloWorld", "$(AString.EndsWith('World'))", "True")]
    [InlineData("AString", "HelloWorld", "$(AString.EndsWith('world'))", "False")]
    [InlineData("AString", "HelloWorld", "$(AString.EndsWith('WORLD', 'StringComparison.Ordinal'))", "False")]
    [InlineData("AString", "HelloWorld", "$(AString.EndsWith('WORLD', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "HelloWorld", "$(AString.EndsWith('world', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "HelloWorld", "$(AString.EndsWith('Hello', 'StringComparison.Ordinal'))", "False")]
    [InlineData("AString", "HelloWorld", "$(AString.EndsWith('', 'StringComparison.Ordinal'))", "True")]
    [InlineData("AString", "C:\\Path\\File.txt", "$(AString.EndsWith('.TXT', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "C:\\Path\\File.txt", "$(AString.EndsWith('.TXT', 'StringComparison.Ordinal'))", "False")]
    public void StringEndsWithTests(string propertyName, string propertyValue, string propertyFunction, string expected)
        => ExpandProperties(propertyFunction, Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Theory]
    [InlineData("AString", "linux", "$(AString.Equals($(AString.ToLower()), 'StringComparison.InvariantCulture'))", "True")]
    [InlineData("AString", "Linux", "$(AString.Equals($(AString.ToLower()), 'StringComparison.InvariantCulture'))", "False")]
    [InlineData("AString", "hello", "$(AString.Equals('hello', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "hello", "$(AString.Equals('HELLO', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "hello", "$(AString.Equals('HELLO', 'StringComparison.Ordinal'))", "False")]
    public void StringEqualsWithStringComparisonTests(string propertyName, string propertyValue, string propertyFunction, string expected)
        => ExpandProperties(propertyFunction, Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Fact]
    public void IsOsPlatformShouldBeCaseInsensitiveToParameter()
        => ExpandProperties($"$([MSBuild]::IsOsPlatform({Helpers.GetOSPlatformAsString().ToLower()}))")
            .ShouldBe("True");

    [Theory]
    [InlineData("NotAVersion")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1,2,3,4")]
    public void PropertyFunctionVersionComparisonsFailsWithInvalidArguments(string badVersion)
    {
        string expectedMessage = ResourceUtilities.GetResourceString("InvalidVersionFormat");

        AssertThrows($"$([MSBuild]::VersionGreaterThan('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionGreaterThan('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionGreaterThanOrEquals('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionGreaterThanOrEquals('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionLessThan('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionLessThan('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionLessThanOrEquals('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionLessThanOrEquals('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionEquals('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionEquals('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionNotEquals('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionNotEquals('1.0.0', '{badVersion}'))", expectedMessage);
    }

    [Theory]
    [InlineData("v1.0", "2.1", -1)]
    [InlineData("3.2", "3.14-pre", -1)]
    [InlineData("3+metadata", "3.0", 0)]
    [InlineData("2.1", "2.1.0", 0)]
    [InlineData("v1.2.3-pre+metadata", "1.2.3.0", 0)]
    [InlineData("3.14", "3.2", 1)]
    [InlineData("42.43.44.45", "42.43.44.5", 1)]
    public void PropertyFunctionVersionComparisons(string a, string b, int expectedSign)
    {
        AssertSuccess($"$([MSBuild]::VersionGreaterThan('{a}', '{b}'))", expectedSign > 0);
        AssertSuccess($"$([MSBuild]::VersionGreaterThanOrEquals('{a}', '{b}'))", expectedSign >= 0);
        AssertSuccess($"$([MSBuild]::VersionLessThan('{a}', '{b}'))", expectedSign < 0);
        AssertSuccess($"$([MSBuild]::VersionLessThanOrEquals('{a}', '{b}'))", expectedSign <= 0);
        AssertSuccess($"$([MSBuild]::VersionEquals('{a}', '{b}'))", expectedSign == 0);
        AssertSuccess($"$([MSBuild]::VersionNotEquals('{a}', '{b}'))", expectedSign != 0);
    }

    [Theory]
    [InlineData("net45", ".NETFramework", "4.5")]
    [InlineData("netcoreapp3.1", ".NETCoreApp", "3.1")]
    [InlineData("netstandard2.1", ".NETStandard", "2.1")]
    [InlineData("net5.0-ios12.0", ".NETCoreApp", "5.0")]
    [InlineData("foo", "Unsupported", "0.0")]
    public void PropertyFunctionTargetFrameworkParsing(string tfm, string expectedIdentifier, string expectedVersion)
    {
        AssertSuccess($"$([MSBuild]::GetTargetFrameworkIdentifier('{tfm}'))", expectedIdentifier);
        AssertSuccess($"$([MSBuild]::GetTargetFrameworkVersion('{tfm}'))", expectedVersion);
    }

    [Theory]
    [InlineData("net45", 2, "4.5")]
    [InlineData("net45", 3, "4.5.0")]
    [InlineData("net472", 3, "4.7.2")]
    [InlineData("net472", 2, "4.7.2")]
    public void PropertyFunctionTargetFrameworkVersionMultipartParsing(string tfm, int versionPartCount, string expectedVersion)
    {
        AssertSuccess($"$([MSBuild]::GetTargetFrameworkVersion('{tfm}', {versionPartCount}))", expectedVersion);
    }

    [Theory]
    [InlineData("net5.0-windows10.1.2.3", 4, "10.1.2.3")]
    [InlineData("net5.0-windows10.1.2.3", 2, "10.1.2.3")]
    [InlineData("net5.0-windows10.0.0.3", 2, "10.0.0.3")]
    [InlineData("net5.0-windows0.0.0.3", 2, "0.0.0.3")]
    public void PropertyFunctionTargetPlatformVersionMultipartParsing(string tfm, int versionPartCount, string expectedVersion)
    {
        AssertSuccess($"$([MSBuild]::GetTargetPlatformVersion('{tfm}', {versionPartCount}))", expectedVersion);
    }

    [Theory]
    [InlineData("net5.0-ios12.0", "ios", "12.0")]
    [InlineData("net5.1-android1.1", "android", "1.1")]
    [InlineData("net6.0-windows99.99", "windows", "99.99")]
    [InlineData("net5.0-ios", "ios", "0.0")]
    [InlineData("foo", "", "0.0")]
    public void PropertyFunctionTargetPlatformParsing(string tfm, string expectedIdentifier, string expectedVersion)
    {
        AssertSuccess($"$([MSBuild]::GetTargetPlatformIdentifier('{tfm}'))", expectedIdentifier);
        AssertSuccess($"$([MSBuild]::GetTargetPlatformVersion('{tfm}'))", expectedVersion);
    }

    [Theory]
    [InlineData("net5.0", "net5.0", true)]
    [InlineData("net5.0-windows10.0", "net5.0-windows10.0", true)]
    [InlineData("net5.0-ios", "net5.0-andriod", false)]
    [InlineData("net5.0-ios12.0", "net5.0-ios11.0", true)]
    [InlineData("net5.0-ios11.0", "net5.0-ios12.0", false)]
    [InlineData("net45", "net46", false)]
    [InlineData("net46", "net45", true)]
    [InlineData("netcoreapp3.1", "netcoreapp1.0", true)]
    [InlineData("netstandard1.6", "netstandard2.1", false)]
    [InlineData("netcoreapp3.0", "netstandard2.1", true)]
    [InlineData("net461", "netstandard1.0", true)]
    [InlineData("foo", "netstandard1.0", false)]
    public void PropertyFunctionTargetFrameworkComparisons(string tfm1, string tfm2, bool expected)
        => ExpandProperties($"$([MSBuild]::IsTargetFrameworkCompatible('{tfm1}', '{tfm2}'))")
            .ShouldBe(expected.ToString(CultureInfo.InvariantCulture));

    private static void AssertThrows(string expression, string expectedMessage)
    {
        InvalidProjectFileException ex = Should.Throw<InvalidProjectFileException>(
            () => ExpandProperties(expression));

        ex.Message.ShouldContain(expectedMessage);
    }

    private static void AssertSuccess(string expression, object expected)
        => ExpandProperties(expression)
            .ShouldBe(Convert.ToString(expected, CultureInfo.InvariantCulture));

    /// <summary>
    ///  Expand property function that calls a method with an enum parameter.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodEnumArgument()
        => ExpandProperties("$([System.String]::Equals(`a`, `A`, StringComparison.OrdinalIgnoreCase))")
            .ShouldBe(true.ToString());

    /// <summary>
    ///  Expand intrinsic property function to locate the directory of a file above.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodDirectoryNameOfFileAbove()
    {
        using var env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        var file = env.CreateFile(folder, "marker.tmp");
        string directoryStart = Path.Combine(folder.Path, "one", "two", "three", "four", "five");

        var expander = ExpanderFactory.Create(Properties(
            ("StartingDirectory", directoryStart),
            ("FileToFind", Path.GetFileName(file.Path))));

        string result = expander.ExpandIntoStringAndUnescape(
            "$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), $(FileToFind)))",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        FileUtilities.EnsureTrailingSlash(result).ShouldBe(FileUtilities.EnsureTrailingSlash(folder.Path));

        result = expander.ExpandIntoStringAndUnescape(
            "$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), Hobbits))",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        result.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> returns the correct path if a file exists
    ///  or an empty string if it doesn't.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAbove()
    {
        using var env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        var file = env.CreateFile(folder, "marker.tmp");
        MockElementLocation location = new(Path.Combine(folder.Path, "one", "two", "three", "four", "five", "test.proj"));

        var expander = ExpanderFactory.Create(Properties("FileToFind", Path.GetFileName(file.Path)));

        string result = expander.ExpandIntoStringAndUnescape(
            "$([MSBuild]::GetPathOfFileAbove($(FileToFind)))",
            ExpanderOptions.ExpandProperties,
            location);

        result.ShouldBe(file.Path);

        result = expander.ExpandIntoStringAndUnescape(
            "$([MSBuild]::GetPathOfFileAbove('Hobbits'))",
            ExpanderOptions.ExpandProperties,
            location);

        result.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that the usage of GetPathOfFileAbove() within an in-memory project throws an exception.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAboveInMemoryProject()
    {
        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
        {
            ObjectModelHelpers.CreateInMemoryProject("<Project><PropertyGroup><foo>$([MSBuild]::GetPathOfFileAbove('foo'))</foo></PropertyGroup></Project>");
        });

        exception.Message.ShouldStartWith("The expression \"[MSBuild]::GetPathOfFileAbove(foo, \'\')\" cannot be evaluated.");
    }

    /// <summary>
    ///  Verifies that <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> only accepts a file name.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAboveFileNameOnly()
    {
        string fileWithPath = Path.Combine("foo", "bar", "file.txt");

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
        {
            ExpandProperties(
                "$([MSBuild]::GetPathOfFileAbove($(FileWithPath)))",
                Properties("FileWithPath", fileWithPath));
        });

        exception.Message.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("InvalidGetPathOfFileAboveParameter", fileWithPath));
    }

    /// <summary>
    ///  Expand property function that calls GetCultureInfo.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetCultureInfo()
        => ExpandProperties("$([System.Globalization.CultureInfo]::GetCultureInfo(`en-US`).ToString())")
            .ShouldBe(new CultureInfo("en-US").ToString());

    /// <summary>
    ///  Expand property function that calls a static arithmetic method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodArithmeticAddInt32()
        => ExpandProperties("$([MSBuild]::Add(40, 2))")
            .ShouldBe((40 + 2).ToString());

    /// <summary>
    ///  Expand property function that calls a static arithmetic method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodArithmeticAddDouble()
        => ExpandProperties("$([MSBuild]::Add(39.9, 2.1))")
            .ShouldBe((39.9 + 2.1).ToString());

    /// <summary>
    ///  Expand property function choosing either the value (if not empty) or the default specified.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValueOrDefaultData))]
    public void PropertyFunctionValueOrDefault(string expression, string expected)
        => ExpandProperties(expression).ShouldBe(expected);

    public static TheoryData<string, string> ValueOrDefaultData => new()
    {
        { @"$([MSBuild]::ValueOrDefault('', '42'))", "42" },
        { @"$([MSBuild]::ValueOrDefault('42', '43'))", "42" },
    };

    /// <summary>
    ///  Observe property value changes when choosing between the value and a default.
    /// </summary>
    [Fact]
    public void PropertyFunctionValueOrDefaultFromEnvironment()
    {
        var properties = Properties("DifferentTargetsPath", "Different");

        ExpandProperties("$([MSBuild]::ValueOrDefault('$(DifferentTargetsPath)', '42'))", properties)
            .ShouldBe("Different");

        properties.Set(ProjectPropertyInstance.Create("DifferentTargetsPath", string.Empty));

        ExpandProperties("$([MSBuild]::ValueOrDefault('$(DifferentTargetsPath)', '43'))", properties)
            .ShouldBe("43");
    }

#if FEATURE_APPDOMAIN
    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist()
        => ExpandProperties("$([MSBuild]::DoesTaskHostExist('CurrentRuntime', 'CurrentArchitecture'))")
            .ShouldBe("true", StringCompareShould.IgnoreCase);

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Whitespace()
        => ExpandProperties("$([MSBuild]::DoesTaskHostExist('   CurrentRuntime    ', 'CurrentArchitecture'))")
        .ShouldBe("true", StringCompareShould.IgnoreCase);
#endif

    [Theory]
    [MemberData(nameof(NormalizeDirectoryExpressions))]
    public void PropertyFunctionNormalizeDirectory(string expression, string expectedRelativePath)
    {
        var expander = ExpanderFactory.Create(Properties(
            ("MyPath", "one"),
            ("MySecondPath", "two")));

        string result = expander.ExpandIntoStringAndUnescape(
            expression,
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        result.ShouldBe(Path.GetFullPath(expectedRelativePath) + Path.DirectorySeparatorChar);
    }

    public static TheoryData<string, string> NormalizeDirectoryExpressions => new()
    {
        { "$([MSBuild]::NormalizeDirectory($(MyPath)))", "one" },
        { "$([MSBuild]::NormalizeDirectory($(MyPath), $(MySecondPath)))", Path.Combine("one", "two") },
    };

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Error()
        => Should.Throw<InvalidProjectFileException>(() =>
        {
            ExpandProperties("$([MSBuild]::DoesTaskHostExist('ASDF', 'CurrentArchitecture'))");
        });

#if FEATURE_APPDOMAIN
    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Evaluated()
        => ExpandProperties(
            "$([MSBuild]::DoesTaskHostExist('$(Runtime)', '$(Architecture)'))",
            Properties(
                ("Runtime", "CurrentRuntime"),
                ("Architecture", "CurrentArchitecture")))
            .ShouldBe("true", StringCompareShould.IgnoreCase);
#endif

#if FEATURE_APPDOMAIN
    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_NonexistentTaskHost()
    {
        try
        {
            using var env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME", "asdfghjkl.exe");
            NodeProviderOutOfProcTaskHost.ClearCachedTaskHostPaths();

            // CLR has been forced to pretend not to exist, whether it actually does or not
            ExpandProperties("$([MSBuild]::DoesTaskHostExist('CLR2', 'CurrentArchitecture'))")
                .ShouldBe("false", StringCompareShould.IgnoreCase);
        }
        finally
        {
            NodeProviderOutOfProcTaskHost.ClearCachedTaskHostPaths();
        }
    }
#endif

    /// <summary>
    ///  Expand property function that calls a static bitwise method to retrieve file attribute.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodFileAttributes()
    {
        using var env = TestEnvironment.Create(_output);
        var file = env.CreateFile();

        try
        {
            File.SetAttributes(file.Path, FileAttributes.ReadOnly | FileAttributes.Archive);

            ExpandProperties($"$([MSBuild]::BitwiseAnd(32,$([System.IO.File]::GetAttributes({file.Path}))))")
                .ShouldBe("32");
        }
        finally
        {
            File.SetAttributes(file.Path, FileAttributes.Normal);
        }
    }

    /// <summary>
    ///  Expand intrinsic property function calls a static arithmetic method.
    /// </summary>
    [Theory]
    [UseInvariantCulture]
    [MemberData(nameof(StaticMethodIntrinsicMathsData))]
    public void PropertyFunctionStaticMethodIntrinsicMaths(string expression, string expected)
        => ExpandProperties(expression).ShouldBe(expected);

    public static TheoryData<string, string> StaticMethodIntrinsicMathsData
    {
        get
        {
            return new()
            {
                { "$([MSBuild]::Add(39.9, 2.1))", Result(39.9 + 2.1) },
                { "$([MSBuild]::Add(40, 2))", Result(40 + 2) },
                { "$([MSBuild]::Subtract(44, 2))", Result(44 - 2) },
                { "$([MSBuild]::Subtract(42.9, 0.9))", Result(42.9 - 0.9d) },
                { "$([MSBuild]::Multiply(21, 2))", Result(21d * 2d) },
                { "$([MSBuild]::Multiply(84.0, 0.5))", Result(84.0d * 0.5d) },
                { "$([MSBuild]::Divide(84, 2))", Result(84d / 2d) },
                { "$([MSBuild]::Divide(84.4, 2.0))", Result(84.4 / 2.0) },
                { "$([MSBuild]::Modulo(85, 2))", Result(85 % 2) },
                { "$([MSBuild]::Modulo(2345.5, 43))", Result(2345.5 % 43) },
            };

            static string Result(double d)
                => d.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    ///  Expand intrinsic property functions that call a bit operator.
    /// </summary>
    [Theory]
    [MemberData(nameof(StaticMethodIntrinsicBitOperationsData))]
    public void PropertyFunctionStaticMethodIntrinsicBitOperations(string expression, string expected)
        => ExpandProperties(expression).ShouldBe(expected);

    public static TheoryData<string, string> StaticMethodIntrinsicBitOperationsData
    {
        get
        {
            return new()
            {
                { @"$([MSBuild]::BitwiseOr(40, 2))", Result(40 | 2) },
                { @"$([MSBuild]::BitwiseAnd(42, 2))", Result(42 & 2) },
                { @"$([MSBuild]::BitwiseXor(213, 255))", Result(213 ^ 255) },
                { @"$([MSBuild]::BitwiseNot(-43))", Result(~-43) },
                { @"$([MSBuild]::LeftShift(1, 2))", Result(1 << 2) },
                { @"$([MSBuild]::RightShift(-8, 2))", Result(-8 >> 2) },
                { @"$([MSBuild]::RightShiftUnsigned(-8, 2))", Result(-8 >>> 2) },
            };

            static string Result(int i)
                => i.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    ///  Expand a property reference that has whitespace around the property name (should result in empty)
    /// </summary>
    [Fact]
    public void PropertySimpleSpaced()
        => ExpandProperties(@"$( SomeStuff )", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBeEmpty();

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValue()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue("Value", $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(
                $@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\{keyPath}', '$(SomeProperty)'))",
                Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValueDefault()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue(string.Empty, $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(
                $@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\{keyPath}', null))",
                Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValueFromView1()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);

            key.SetValue(string.Empty, "%" + envVar + "%", RegistryValueKind.ExpandString);
            ExpandProperties(
                $@"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\{keyPath}', null, null, RegistryView.Default, RegistryView.Default))",
                Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValueFromView2()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);

            key.SetValue(string.Empty, "%" + envVar + "%", RegistryValueKind.ExpandString);
            ExpandProperties(
                $@"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\{keyPath}', null, null, Microsoft.Win32.RegistryView.Default))",
                Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    /// <summary>
    ///  Expand a property function that references item metadata
    /// </summary>
    [Fact]
    public void PropertyFunctionConsumingItemMetadata()
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var properties = Properties("SomePath", Path.Combine(s_rootPathPrefix, "some", "path"));
        var metadata = Metadata(("Compile.Identity", "fOo.Cs"));
        var items = Items();
        items.ImportItems([new ProjectItemInstance(project, "Compile", "fOo.Cs", project.FullPath)]);

        var expander = ExpanderFactory.Create(properties, items, metadata);

        string result = expander.ExpandIntoStringLeaveEscaped(
            @"$([System.IO.Path]::Combine($(SomePath),%(Compile.Identity)))",
            ExpanderOptions.ExpandAll,
            MockElementLocation.Instance);

        result.ShouldBe(Path.Combine(s_rootPathPrefix, "some", "path", "fOo.Cs"));
    }

    /// <summary>
    ///  Expand a property function which is a string constructor referencing item metadata.
    /// </summary>
    /// <remarks>
    ///  Note that referencing a non-existent metadatum results in binding to a parameter-less String constructor. This constructor
    ///  does not exist in BCL but it is special-cased in the expander logic and handled to return an empty string.
    /// </remarks>
    [Theory]
    [InlineData("language", "english")]
    [InlineData("nonexistent", "")]
    public void PropertyStringConstructorConsumingItemMetadata(string metadatumName, string metadatumValue)
    {
        var expander = CreateItemFunctionExpander();

        string result = expander.ExpandIntoStringLeaveEscaped(
            $"$([System.String]::new(%({metadatumName})))",
            ExpanderOptions.ExpandAll,
            MockElementLocation.Instance);

        result.ShouldBe(metadatumValue);
    }

    public static IEnumerable<object[]> GetHashAlgoTypes()
        => Enum.GetNames(typeof(IntrinsicFunctions.StringHashingAlgorithm))
            .Append(null)
            .Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(GetHashAlgoTypes))]
    public void PropertyFunctionHashCodeSameOnlyIfStringSame(string hashType)
    {
        string[] stringsToHash = [
            "cat1s",
            "cat1z",
            "bat1s",
            "cut1s",
            "cat1so",
            "cats1",
            "acat1s",
            "cat12s",
            "cat1s"
        ];
        string hashTypeString = hashType is null ? "" : $", '{hashType}'";
        string[] hashes = stringsToHash.Select(toHash =>
            ExpandProperties($"$([MSBuild]::StableStringHash('{toHash}'{hashTypeString}))"))
            .ToArray();
        for (int a = 0; a < hashes.Length; a++)
        {
            for (int b = a; b < hashes.Length; b++)
            {
                if (stringsToHash[a].Equals(stringsToHash[b]))
                {
                    hashes[a].ShouldBe(hashes[b], "Identical strings should hash to the same value.");
                }
                else
                {
                    hashes[a].ShouldNotBe(hashes[b], "Different strings should not hash to the same value.");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetHashAlgoTypes))]
    public void PropertyFunctionHashCodeReturnsExpectedType(string hashType)
    {
        TypeCode expectedTypeCode = hashType switch
        {
            null => TypeCode.Int32,
            "Legacy" => TypeCode.Int32,
            "Fnv1a32bit" => TypeCode.Int32,
            "Fnv1a32bitFast" => TypeCode.Int32,
            "Fnv1a64bit" => TypeCode.Int64,
            "Fnv1a64bitFast" => TypeCode.Int64,
            "Sha256" => TypeCode.String,
            _ => throw new ArgumentOutOfRangeException(nameof(hashType))
        };

        string hashTypeString = hashType is null ? "" : $", '{hashType}'";
        ExpandProperties(
            $"$([System.Convert]::GetTypeCode($([MSBuild]::StableStringHash('FooBar'{hashTypeString}))))")
            .ShouldBe(expectedTypeCode.ToString());
    }

    [Theory]
    [InlineData("easycase")]
    [InlineData("")]
    [InlineData("\"\n()\tsdfIR$%#*;==")]
    public void TestBase64Conversion(string testCase)
    {
        string intermediate = ExpandProperties($"$([MSBuild]::ConvertToBase64('{testCase}'))");
        intermediate.Trim('=').All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/').ShouldBeTrue();
        ExpandProperties($"$([MSBuild]::ConvertFromBase64('{intermediate}'))")
            .ShouldBe(testCase);
    }

    [Theory]
    [InlineData("easycase", "ZWFzeWNhc2U=")]
    [InlineData("", "")]
    [InlineData("\"\n()\tsdfIR$%#*;==", "IgooKQlzZGZJUiQlIyo7PT0=")]
    public void TestExplicitToBase64Conversion(string plaintext, string base64)
        => ExpandProperties($"$([MSBuild]::ConvertToBase64('{plaintext}'))")
            .ShouldBe(base64);

    [Theory]
    [InlineData("easycase", "ZWFzeWNhc2U=")]
    [InlineData("", "")]
    [InlineData("\"\n()\tsdfIR$%#*;==", "IgooKQlzZGZJUiQlIyo7PT0=")]
    public void TestExplicitFromBase64Conversion(string plaintext, string base64)
        => ExpandProperties($"$([MSBuild]::ConvertFromBase64('{base64}'))")
            .ShouldBe(plaintext);

    private static PropertyDictionary<ProjectPropertyInstance> CreatePropertyFunctionSyntaxProperties()
    {
        AvailableStaticMethods.Reset_ForUnitTestsOnly();

        return Properties(
            ("File", @"foo\file.txt"),
            ("a", "no"),
            ("b", "true"),
            ("c", "1"),
            ("position", "4"),
            ("d", "xxx"),
            ("e", "xxx"),
            ("and", "and"),
            ("a_semi_b", "a;b"),
            ("a_apos_b", "a'b"),
            ("foo_apos_foo", "foo'foo"),
            ("a_escapedsemi_b", "a%3bb"),
            ("a_escapedapos_b", "a%27b"),
            ("has_trailing_slash", @"foo\"),
            ("emptystring", @""),
            ("space", @" "),
            ("listofthings", @"a;b;c;d;e;f;g;h;i;j;k;l"),
            ("input", @"EXPORT a"),
            ("propertycontainingnullasastring", @"null"));
    }

    [Theory]
    [MemberData(nameof(ValidPropertyFunctionExpressions))]
    public void PropertyFunctionSyntaxExpands(string expression, string expected)
        => ExpandProperties(expression, CreatePropertyFunctionSyntaxProperties())
            .ShouldBe(expected);

    public static TheoryData<string, string> ValidPropertyFunctionExpressions
    {
        get
        {
            TheoryData<string, string> data = new()
            {
                { "$(input.ToString()[1])", "X" },
                { "$(input[1])", "X" },
                { "$(listofthings.Split(';')[$(position)])","e" },
                { @"$([System.Text.RegularExpressions.Regex]::Match($(Input), `EXPORT\s+(.+)`).Groups[1].Value)","a" },
                { "$([MSBuild]::Add(1,2).CompareTo(3))", "0" },
                { "$([MSBuild]::Add(1,2).CompareTo(3.0))", "0" },
                { "$([MSBuild]::Add(1,2.0).CompareTo(3.0))", "0" },
                { "$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).CompareTo(3.0))", "0" },
                { "$([MSBuild]::Add(1,2).CompareTo('3'))", "0" },
                { "$([MSBuild]::Add(1,2).CompareTo(3.1))", "-1" },
                { "$([MSBuild]::Add(1,2.0).CompareTo(3.1))", "-1" },
                { "$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).CompareTo(3.1))", "-1" },
                { "$([MSBuild]::Add(1,2).CompareTo(2))", "1" },
                { "$([MSBuild]::Add(1,2).Equals(3))", "True" },
                { "$([MSBuild]::Add(1,2).Equals(3.0))", "True" },
                { "$([MSBuild]::Add(1,2.0).Equals(3.0))", "True" },
                { "$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).Equals(3.0))", "True" },
                { "$([MSBuild]::Add(1,2).Equals('3'))", "True" },
                { "$([MSBuild]::Add(1,2).Equals(3.1))", "False" },
                { "$([MSBuild]::Add(1,2.0).Equals(3.1))", "False" },
                { "$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).Equals(3.1))", "False" },
                { "$(a.Insert(0,'%28'))", "%28no" },
                { "$(a.Insert(0,'\"'))", "\"no" },
                { "$(a.Insert(0,'(('))", "%28%28no" },
                { "$(a.Insert(0,'))'))", "%29%29no" },
                { "A$(Reg:A)A", "AA" },
                { "A$(Reg:AA)", "A" },
                { "$(Reg:AA)", "" },
                { "$(Reg:AAAA)", "" },
                { "$(Reg:AAA)", "" },
                { "$([MSBuild]::Add(2,$([System.Convert]::ToInt64('28', 16))))", "42" },
                { "$([MSBuild]::Add(2,$([System.Convert]::ToInt64('28', $([System.Convert]::ToInt32(16))))))", "42" },
                { "$(e.Length.ToString())", "3" },
                { "$(e.get_Length().ToString())", "3" },
                { "$(emptystring.Length)", "0" },
                { "$(space.Length)", "1" },
                { "$([System.TimeSpan]::Equals(null, null))", "True" }, // constant, unquoted null is a special value
                { "$([MSBuild]::Add(40,null))", "40" },
                { "$([MSBuild]::Add( 40 , null ))", "40" },
                { "$([MSBuild]::Add(null,40))", "40" },
                { "$([MSBuild]::Escape(';'))", "%3b" },
                { "$([MSBuild]::UnEscape('%3b'))", ";" },
                { "$(e.Substring($(e.Length)))", "" },
                { "$([System.Int32]::MaxValue)", System.Int32.MaxValue.ToString() },
                { "x$()", "x" },
                // Following two are comparison between non-numeric and numeric properties. More details: #10583
                { "$(a.Equals($(c)))","False" },
                { "$(a.CompareTo($(c)))","1" },
            };

            if (!NativeMethodsShared.IsWindows)
            {
                data.Add("$(Registry:X)", "");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidPropertyFunctionExpressions))]
    public void InvalidPropertyFunctionSyntaxThrowsOrRemainsUnexpanded(string expression)
    {
        var properties = CreatePropertyFunctionSyntaxProperties();
        string result = null;

        Exception exception = Record.Exception(() =>
        {
            result = ExpandProperties(expression, properties);
        });

        if (exception is null)
        {
            result.ShouldBe(expression);
        }
        else
        {
            exception.ShouldBeOfType<InvalidProjectFileException>();
            _output.WriteLine(exception.Message);
        }
    }

    public static TheoryData<string> InvalidPropertyFunctionExpressions
    {
        get
        {
            TheoryData<string> data = new()
            {
                "$(input[)",
                "$(input.ToString()])",
                "$(input.ToString()[)",
                "$(input.ToString()[12])",
                "$(input[])",
                "$(input[-1])",
                "$(listofthings.Split(';')[)",
                "$(listofthings.Split(';')['goo'])",
                "$(listofthings.Split(';')[])",
                "$(listofthings.Split(';')[-1])",
                "$([]::())",
                @"

$(

$(

[System.IO]::Path.GetDirectory('c:\foo\bar\baz.txt')

).Substring(

'$([System.IO]::Path.GetPathRoot(

'$([System.IO]::Path.GetDirectory('c:\foo\bar\baz.txt'))'

).Length)'



)

",
                "$([Microsoft.VisualBasic.FileIO.FileSystem]::CurrentDirectory)", // not allowed
                "$(e.Length..ToString())",
                "$(SomeStuff.get_Length(null))",
                "$(SomeStuff.Substring((1)))",
                "$(b.Substring(-10, $(c)))",
                "$(b.Substring(-10, $(emptystring)))",
                "$(b.Substring(-10, $(space)))",
                "$([MSBuild]::Add.Sub(null,40))",
                "$([MSBuild]::Add( ,40))", // empty parameter is empty string
                "$([MSBuild]::Add('',40))", // empty quoted parameter is empty string
                "$([MSBuild]::Add(40,,,))",
                "$([MSBuild]::Add(40, ,,))",
                "$([MSBuild]::Add(40,)",
                "$([MSBuild]::Add(40,X)",
                "$([MSBuild]::Add(40,",
                "$([MSBuild]::Add(40",
                "$([MSBuild]::Add(,))", // gives "Late bound operations cannot be performed on types or methods for which ContainsGenericParameters is true."
                "$([System.TimeSpan]::Equals(,))", // empty parameter is interpreted as empty string
                "$([System.TimeSpan]::Equals($(space),$(emptystring)))", // empty parameter is interpreted as empty string
                "$([System.TimeSpan]::Equals($(emptystring),$(emptystring)))", // empty parameter is interpreted as empty string
                "$([MSBuild]::Add($(PropertyContainingNullAsAString),40))", // a property containing the word null is a string "null"
                "$([MSBuild]::Add('null',40))", // the word null is a string "null"
                "$(SomeStuff.Substring(-10))",
                "$(.Length)",
                "$(.Substring(1))",
                "$(.get_Length())",
                "$(e.)",
                "$(e..)",
                "$(e..Length)",
                "$(e$(d).Length)",
                "$($(d).Length)",
                "$(e`.Length)",
                "$([System.IO.Path]Combine::Combine(`a`,`b`))",
                "$([System.IO.Path]::Combine((`a`,`b`))",
                "$([System.IO.Path]Combine(::Combine(`a`,`b`))",
                "$([System.IO.Path]Combine(`::Combine(`a`,`b`)`, `b`)`)",
                "$([System.IO.Path]::`Combine(`a`, `b`)`)",
                "$([System.IO.Path]::(`Combine(`a`, `b`)`))",
                "$([System.DateTime]foofoo::Now)",
                "$([System.DateTime].Now)",
                "$([].Now)",
                "$([ ].Now)",
                "$([ .Now)",
                "$([])",
                "$([ )",
                "$([ ])",
                "$([System.Diagnostics.Process]::Start(`NOTEPAD.EXE`))",
                "$([[]]::Start(`NOTEPAD.EXE`))",
                "$([(::Start(`NOTEPAD.EXE`))",
                "$([Goop]::Start(`NOTEPAD.EXE`))",
                "$([System.Threading.Thread]::CurrentThread)",
                "$",
                "$(",
                "$((",
                "@",
            "@(",
            "@()",
            "%",
            "%(",
            "%()",
            "exists",
            "exists(",
            "exists()",
            "exists( )",
            "exists(,)",
            "@(x->'",
            "@(x->''",
            "@(x-",
            "@(x->'x','",
            "@(x->'x',''",
            "@(x->'x','')",
            "-1>x",
            "\n",
            "\t",
            "+-1",
            "$(SomeStuff.)",
            "$(SomeStuff.!)",
            "$(SomeStuff.`)",
            "$(SomeStuff.GetType)",
            "$(goop.baz`)",
            "$(SomeStuff.Substring(HELLO!))",
            "$(SomeStuff.ToLowerInvariant()_goop)",
            "$(SomeStuff($(System.DateTime.Now)))",
            "$(System.Foo.Bar.Lgg)",
            "$(SomeStuff.Lgg)",
            "$(SomeStuff($(Value)))",
            "$(e.$(e.Length))",
            "$(e.Substring($(e.Substring(,)))",
            "$(e.Substring($(e.Substring(a)))",
            "$(e.Substring($([System.IO.Path]::Combine(`a`, `b`))))",
            "$([]::())",
            "$((((",
            "$($())",
            "$",
            "()"
            };

#if !RUNTIME_TYPE_NETCORE
            if (NativeMethodsShared.IsWindows)
            {
                // '|' is only an invalid path character on Windows under .NET Framework.
                data.Add("$([System.IO.Path]::Combine(`|`,`b`))");
            }
#endif

            if (NativeMethodsShared.IsWindows)
            {
                data.Add("$(Registry:X)");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EnsureTrailingSlashExpressions))]
    public void PropertyFunctionEnsureTrailingSlash(string expression)
    {
        string path = Path.Combine("foo", "bar");

        ExpandProperties(expression, Properties("SomeProperty", path))
            .ShouldBe(path + Path.DirectorySeparatorChar);
    }

    public static TheoryData<string> EnsureTrailingSlashExpressions => new()
    {
        $"$([MSBuild]::EnsureTrailingSlash('{Path.Combine("foo", "bar")}'))",
        "$([MSBuild]::EnsureTrailingSlash($(SomeProperty)))",
    };

    [Fact]
    public void PropertyFunctionWithNewLines()
    {
        const string propertyFunction = @"$(SomeProperty
 .Substring(0, 10)
  .ToString()
   .Substring(0, 5)
     .ToString())";

        ExpandProperties(propertyFunction, Properties("SomeProperty", "6C8546D5297C424F962201B0E0E9F142"))
            .ShouldBe("6C854");
    }

    [Fact]
    public void PropertyFunctionStringIndexOfAny()
        => ExpandProperties("$(prop.IndexOfAny('y'))", Properties("prop", "x-y-z"))
            .ShouldBe("2");

    [Theory]
    [InlineData("$(prop.LastIndexOf('y'))", "prop", "x-x-y-y-y-z", "8")]
    [InlineData("$(prop.LastIndexOf('y', 7))", "prop", "x-x-y-y-y-z", "6")]
    public void PropertyFunctionStringLastIndexOf(string expression, string propertyName, string propertyValue, string expected)
        => ExpandProperties(expression, Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionStringLastIndexOfAny()
        => ExpandProperties("$(prop.LastIndexOfAny('xy'))", Properties("prop", "x-x-y-y-y-z"))
            .ShouldBe("8");

    [Fact]
    public void PropertyFunctionStringCopy()
    {
        const string Expression = """
            $([System.String]::Copy($(X)).LastIndexOf(
                '.designer.cs',
                System.StringComparison.OrdinalIgnoreCase))
            """;

        ExpandProperties(Expression, Properties("X", "test.designer.cs"))
            .ShouldBe("4");
    }

    [Fact]
    public void PropertyFunctionVersionParse()
        => ExpandProperties(@"$([System.Version]::Parse('$(X)').ToString(1))", Properties("X", "4.0"))
            .ShouldBe("4");

    [Fact]
    public void PropertyFunctionGuidNewGuid()
    {
        string result = ExpandProperties("$([System.Guid]::NewGuid())");

        Guid.TryParse(result, out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("NonExistingFeature", "Undefined")]
    [InlineData("EvaluationContext_SharedSDKCachePolicy", "Available")]
    public void PropertyFunctionCheckFeatureAvailability(string featureName, string availability)
        => ExpandProperties($"$([MSBuild]::CheckFeatureAvailability({featureName}))")
            .ShouldBe(availability);

    [Theory]
    [InlineData("\u0074\u0068\u0069\u0073\u002a\u3407\ud840\udc60\ud86a\ude30\ud86e\udc0a\ud86e\udda0\ud879\udeae\u2fd5\u0023", 2, 10, "is________")]
    [InlineData("\ud83d\udc68\u200d\ud83d\udc68\u200d\ud83d\udc66\u200d\ud83d\udc66\ud83d\udc68\u200d\ud83d\udc68\u200d\ud83d\udc66\u200d\ud83d\udc66\u002e\u0070\u0072\u006f\u006a", 0, 8, "________")]
    public void SubstringByAsciiChars(string featureName, int start, int length, string expected)
        => ExpandProperties($"$([MSBuild]::SubstringByAsciiChars({featureName}, {start}, {length}))")
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetCurrentToolsDirectory()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetCurrentToolsDirectory())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetCurrentToolsDirectory()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetToolsDirectory32()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetToolsDirectory32())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetToolsDirectory32()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetToolsDirectory64()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetToolsDirectory64())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetToolsDirectory64()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetMSBuildSDKsPath()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetMSBuildSDKsPath())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetMSBuildSDKsPath()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetVsInstallRoot()
    {
        string vsInstallRoot = EscapingUtilities.Escape(IntrinsicFunctions.GetVsInstallRoot());

        vsInstallRoot ??= "";

        ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetVsInstallRoot())")
            .ShouldBe(vsInstallRoot);
    }

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetMSBuildExtensionsPath()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetMSBuildExtensionsPath())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetMSBuildExtensionsPath()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetProgramFiles32()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetProgramFiles32())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetProgramFiles32()));

    [Fact]
    public void PropertyFunctionStringArrayIndexerGetter()
        => ExpandProperties("$(prop.Split('-')[0])", Properties("prop", "x-y-z"))
            .ShouldBe("x");

    [Theory]
    [InlineData("$(prop.Substring(2))", "prop", "abcdef", "cdef")]
    [InlineData("$(prop.Substring(2, 3))", "prop", "abcdef", "cde")]
    public void PropertyFunctionSubstring(string expression, string propertyName, string propertyValue, string expected)
        => ExpandProperties(expression, Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionStringGetChars()
        => ExpandProperties("$(prop[0])", Properties("prop", "461"))
            .ShouldBe("4");

    [Fact]
    public void PropertyFunctionStringGetCharsError()
    {
        Should.Throw<InvalidProjectFileException>(() =>
        {
            ExpandProperties("$(prop[5])", Properties("prop", "461"))
                .ShouldBe("4");
        });
    }

    [Theory]
    [InlineData("$(prop.PadLeft(2))", "prop", "x", " x")]
    [InlineData("$(prop.PadLeft(2, '0'))", "prop", "x", "0x")]
    [InlineData("$(prop.PadLeft($([MSBuild]::Multiply(1, 2)), '0'))", "prop", "x", "0x")]
    [InlineData("$(VersionSuffixBuildOfTheDay.PadLeft(3, $([System.Convert]::ToChar(`0`))))", "VersionSuffixBuildOfTheDay", "4", "004")]
    public void PropertyFunctionStringPadLeft(string expression, string propertyName, string propertyValue, string expected)
        => ExpandProperties(expression, Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Theory]
    [InlineData("$(prop.PadRight(2))", "prop", "x", "x ")]
    [InlineData("$(prop.PadRight(2, '0'))", "prop", "x", "x0")]
    public void PropertyFunctionStringPadRight(string expression, string propertyName, string propertyValue, string expected)
        => ExpandProperties(expression, Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionStringTrimEndCharArray()
        => ExpandProperties("$(prop.TrimEnd('.0123456789'))", Properties("prop", "net461"))
            .ShouldBe("net");

    [Theory]
    [InlineData("$(X.TrimStart('vV'))", "X", "v40", "40")]
    [InlineData("$(X.TrimStart(vV))", "X", "v40", "40")]
    public void PropertyFunctionStringTrimStart(string expression, string propertyName, string propertyValue, string expected)
        => ExpandProperties(expression, Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionStringTrimEnd1()
        => ExpandProperties("$(prop.TrimEnd('a'))", Properties("prop", "netaa"))
            .ShouldBe("net");

    // https://github.com/dotnet/msbuild/issues/2882
    [Fact]
    public void PropertyFunctionMathMaxOverflow()
        => ExpandProperties("$([System.Math]::Max($(X), 0))", Properties("X", "-2010"))
            .ShouldBe("0");

    [Fact]
    public void PropertyFunctionStringTrimEnd2()
        => Should.Throw<InvalidProjectFileException>(() =>
        {
            ExpandProperties("$(prop.TrimEnd('a', 'b'))", Properties("prop", "stringab"))
                .ShouldBe("string");
        });

    [Fact]
    public void PropertyFunctionMathMin()
        => ExpandProperties("$([System.Math]::Min($(X), 20))", Properties("X", "30"))
            .ShouldBe("20");

    [Fact]
    public void PropertyFunctionMSBuildAddIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Add($(X), 5))", Properties("X", "7"))
            .ShouldBe("12");

    [Fact]
    public void PropertyFunctionMSBuildAddRealLiteral()
        => ExpandProperties("$([MSBuild]::Add($(X), 0.5))", Properties("X", "7"))
            .ShouldBe("7.5");

    [Fact]
    public void PropertyFunctionMSBuildAddIntegerOverflow()
    {
        // Overflow wrapping - result exceeds size of long
        ExpandProperties("$([MSBuild]::Add($(X), 1))", Properties("X", long.MaxValue.ToString()))
            .ShouldBe("-9223372036854775808");
    }

    [Fact]
    [UseInvariantCulture]
    public void PropertyFunctionMSBuildAddRealArgument()
    {
        // string argument is an integer that exceeds the size of long.
        double value = long.MaxValue + 1.0;
        double expected = value + 1.0;
        ExpandProperties("$([MSBuild]::Add($(X), 1))", Properties("X", value.ToString()))
            .ShouldBe(expected.ToString());
    }

    [Fact]
    public void PropertyFunctionMSBuildAddComplex()
        => ExpandProperties("$([MSBuild]::Add($(X), $([MSBuild]::Add(2, 3))))", Properties("X", "7"))
            .ShouldBe("12");

    [Fact]
    public void PropertyFunctionMSBuildSubtractIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Subtract($(X), 20100000))", Properties("X", "20100042"))
            .ShouldBe("42");

    [Fact]
    public void PropertyFunctionMSBuildSubtractRealLiteral()
        => ExpandProperties("$([MSBuild]::Subtract($(X), 20100000.0))", Properties("X", "20100042"))
            .ShouldBe("42");

    [Fact]
    public void PropertyFunctionMSBuildSubtractIntegerMaxValue()
    {
        // If the double overload is used, there will be a rounding error.
        ExpandProperties("$([MSBuild]::Subtract($(X), 9223372036854775806))", Properties("X", long.MaxValue.ToString()))
            .ShouldBe("1");
    }

    [Fact]
    public void PropertyFunctionMSBuildMultiplyIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Multiply($(X), 8800))", Properties("X", "2"))
            .ShouldBe("17600");

    [Fact]
    public void PropertyFunctionMSBuildMultiplyRealLiteral()
        => ExpandProperties("$([MSBuild]::Multiply($(X), 1.5))", Properties("X", "2"))
            .ShouldBe("3");

    [Fact]
    public void PropertyFunctionMSBuildMultiplyIntegerOverflow()
    {
        // Overflow - result exceeds size of long
        ExpandProperties("$([MSBuild]::Multiply($(X), 2))", Properties("X", long.MaxValue.ToString()))
            .ShouldBe("-2");
    }

    [Fact]
    public void PropertyFunctionMSBuildMultiplyComplex()
        => ExpandProperties("$([MSBuild]::Multiply($(X), $([MSBuild]::Multiply(1, 8800))))", Properties("X", "2"))
            .ShouldBe("17600");

    [Fact]
    public void PropertyFunctionMSBuildDivideIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Divide($(X), 10000))", Properties("X", "65536"))
            .ShouldBe("6");

    [Fact]
    public void PropertyFunctionMSBuildDivideRealLiteral()
        => ExpandProperties("$([MSBuild]::Divide($(X), 10000.0))", Properties("X", "65536"))
            .ShouldBe("6.5536");

    [Fact]
    public void PropertyFunctionMSBuildModuloIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Modulo($(X), 3))", Properties("X", "10"))
            .ShouldBe("1");

    [Fact]
    public void PropertyFunctionMSBuildModuloRealLiteral()
        => ExpandProperties("$([MSBuild]::Modulo($(X), 3.0))", Properties("X", "10"))
            .ShouldBe("1");

    [Fact]
    public void PropertyFunctionConvertToString()
        => ExpandProperties("$([System.Convert]::ToString(`.`))")
            .ShouldBe(".");

    [Fact]
    public void PropertyFunctionConvertToInt32()
        => ExpandProperties("$([System.Convert]::ToInt32(42))")
            .ShouldBe("42");

    [Fact]
    public void PropertyFunctionToCharArray()
        => ExpandProperties("$([System.Convert]::ToString(`.`).ToCharArray())")
            .ShouldBe(".");

    [Fact]
    public void PropertyFunctionStringArrayGetValue()
        => ExpandProperties("$(X.Split($([System.Convert]::ToString(`.`).ToCharArray())).GetValue($([System.Convert]::ToInt32(0))))", Properties("X", "ab.cd"))
            .ShouldBe("ab");

    /// <summary>
    ///  Test that Char.IsDigit fast-path works correctly
    /// </summary>
    [Theory]

    // Test with digit characters - single char version
    [InlineData("$([System.Char]::IsDigit('0'))", "True")]
    [InlineData("$([System.Char]::IsDigit('5'))", "True")]
    [InlineData("$([System.Char]::IsDigit('9'))", "True")]

    // Test with non-digit characters - single char version
    [InlineData("$([System.Char]::IsDigit('a'))", "False")]
    [InlineData("$([System.Char]::IsDigit(' '))", "False")]
    [InlineData("$([System.Char]::IsDigit('/'))", "False")]
    [InlineData("$([System.Char]::IsDigit(':'))", "False")]

    // Test with string and index version
    [InlineData("$([System.Char]::IsDigit('abc123def', 3))", "True")]
    [InlineData("$([System.Char]::IsDigit('abc123def', 4))", "True")]
    [InlineData("$([System.Char]::IsDigit('abc123def', 5))", "True")]
    [InlineData("$([System.Char]::IsDigit('abc123def', 0))", "False")]
    [InlineData("$([System.Char]::IsDigit('abc123def', 2))", "False")]
    [InlineData("$([System.Char]::IsDigit('hello789', 5))", "True")]
    public void PropertyFunctionCharIsDigit(string expression, string expected)
        => ExpandProperties(expression).ShouldBe(expected);

    /// <summary>
    ///  Regression test for https://github.com/dotnet/msbuild/issues/12923.
    /// </summary>
    [Fact]
    public void PropertyFunction_ReplaceDoesNotCallRegexReplace()
        => Should.Throw<InvalidProjectFileException>(() =>
        {
            ExpandProperties("$([System.TimeSpan]::Replace('abc_123_ghi', '\\d+', 'def'))")
                .ShouldNotBe("abc_def_ghi");
        });

    [Theory]
    [InlineData("net6.0", "netstandard2.0", "")]
    [InlineData("net6.0-windows", "netstandard2.0", "")]
    [InlineData("net6.0-windows", "net6.0", "net6.0-windows")]
    [InlineData("netstandard2.0;net6.0", "net6.0", "net6.0")]
    [InlineData("netstandard2.0;net6.0-windows", "net6.0", "net6.0-windows")]
    [InlineData("netstandard2.0;net6.0-windows", "net6.0;netstandard2.0;net472", "netstandard2.0%3bnet6.0-windows")]
    [InlineData("netstandard2.0;net472", "net6.0;netstandard2.0;net472", "netstandard2.0%3bnet472")]
    public void PropertyFunctionFilterTargetFrameworks(string incoming, string filter, string expected)
        => ExpandProperties($"$([MSBuild]::FilterTargetFrameworks('{incoming}', '{filter}'))")
            .ShouldBe(expected);

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

    [Fact]
    public void ExpandItem_ConvertToStringUsingInvariantCultureForNumberData()
    {
        var currentThread = Thread.CurrentThread;
        var originalCulture = currentThread.CurrentCulture;
        var originalUICulture = currentThread.CurrentUICulture;

        try
        {
            var svSECultureInfo = new CultureInfo("sv-SE");
            using var env = TestEnvironment.Create(_output);
            currentThread.CurrentCulture = svSECultureInfo;
            currentThread.CurrentUICulture = svSECultureInfo;
            var root = env.CreateFolder();

            const string Content = """
                <Project>
                    <PropertyGroup>
                        <_value>$([MSBuild]::Subtract(0, 1))</_value>
                        <_otherValue Condition="'$(_value)' &gt;= -1">test-value</_otherValue>
                    </PropertyGroup>
                    <Target Name="Build" />
                </Project>
                """;

            var projectFile = env.CreateFile(root, ".proj", Content);
            ProjectInstance projectInstance = new(projectFile.Path);
            projectInstance.GetPropertyValue("_value").ShouldBe("-1");
            projectInstance.GetPropertyValue("_otherValue").ShouldBe("test-value");
        }
        finally
        {
            currentThread.CurrentCulture = originalCulture;
            currentThread.CurrentUICulture = originalUICulture;
        }
    }

    [Theory]
    [InlineData("getType")]
    [InlineData("GetType")]
    [InlineData("gettype")]
    public void GetTypeMethod_ShouldNotBeAllowed(string methodName)
    {
        var currentThread = Thread.CurrentThread;
        var originalCulture = currentThread.CurrentCulture;
        var originalUICulture = currentThread.CurrentUICulture;
        var enCultureInfo = new CultureInfo("en");

        try
        {
            currentThread.CurrentCulture = enCultureInfo;
            currentThread.CurrentUICulture = enCultureInfo;

            using var env = TestEnvironment.Create(_output);
            var root = env.CreateFolder();

            string content = $"""
                <Project>
                    <PropertyGroup>
                        <foo>aa</foo>
                        <typeval>$(foo.{methodName}().FullName)</typeval>
                    </PropertyGroup>
                </Project>
                """;

            var projectFile = env.CreateFile(root, ".proj", content);
            var exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                new ProjectInstance(projectFile.Path);
            });
            exception.BaseMessage.ShouldContain($"The function \"{methodName}\" on type \"System.String\" is not available for execution as an MSBuild property function.");
        }
        finally
        {
            currentThread.CurrentCulture = originalCulture;
            currentThread.CurrentUICulture = originalUICulture;
        }
    }

    [Theory]
    [InlineData("getType")]
    [InlineData("GetType")]
    [InlineData("gettype")]
    public void GetTypeMethod_ShouldBeAllowed_EnabledByEnvVariable(string methodName)
    {
        using var env = TestEnvironment.Create(_output);
        // This is the one test that vets the environment-variable opt-in actually flows through
        // to the feature check. Mimic a prior test having set the AppContext switch (which cannot
        // be returned to "unset" via the public API), then clear it reflectively so FeatureSwitches
        // falls back to the variable. Doing both makes the test deterministic regardless of test
        // ordering and self-validates the reflective unset on every runtime (.NET Core and .NET
        // Framework store the switch in different internal fields).
        AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", false);
        UnsetAppContextSwitch("Microsoft.Build.EnableAllPropertyFunctions");
        env.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");
        var root = env.CreateFolder();

        string content = $"""
            <Project>
                <PropertyGroup>
                    <foo>aa</foo>
                    <typeval>$(foo.{methodName}().FullName)</typeval>
                </PropertyGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", content);
        Should.NotThrow(() =>
        {
            new ProjectInstance(projectFile.Path);
        });
    }

    [Theory]
    [InlineData("$([System.Version]::Parse('17.12.11.10').ToString(2))")]
    [InlineData("$([System.Text.RegularExpressions.Regex]::Replace('abc123def', 'abc', ''))")]
    [InlineData("$([System.String]::new('Hi').Equals('Hello'))")]
    [InlineData("$([System.IO.Path]::GetFileNameWithoutExtension('C:\\folder\\file.txt'))")]
    [InlineData("$([System.Int32]::new(123).ToString('mm')")]
    [InlineData("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::NormalizeDirectory('C:/folder1/./folder2/'))")]
    [InlineData("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::IsOSPlatform('Windows'))")]
    public void FastPathValidationTest(string expression)
    {
        using var env = TestEnvironment.Create(_output);
        env.SetCurrentDirectory(env.CreateFolder().Path);

        // Setting this env variable allows to track if expander was using reflection for a function invocation.
        env.SetEnvironmentVariable("MSBuildLogPropertyFunctionsRequiringReflection", "1");

        var (_, loggingContext) = CreateLoggingContext();

        _ = ExpandProperties(expression, loggingContext);

        string reflectionInfoPath = Path.Combine(Directory.GetCurrentDirectory(), "PropertyFunctionsRequiringReflection");

        // the fast path was successfully resolved without reflection.
        File.Exists(reflectionInfoPath).ShouldBeFalse();
    }

    private (MockLogger Logger, MockLoggingContext Context) CreateLoggingContext()
    {
        var logger = new MockLogger(_output);
        ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.RegisterLogger(logger);
        var loggingContext = new MockLoggingContext(
            loggingService,
            new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));

        return (logger, loggingContext);
    }

    [Fact]
    public void PropertyFunctionRegisterBuildCheck()
    {
        using var env = TestEnvironment.Create(_output);
        var (logger, loggingContext) = CreateLoggingContext();
        var dummyAssemblyFile = env.CreateFile(env.CreateFolder(), "test.dll");

        ExpandProperties($"$([MSBuild]::RegisterBuildCheck({dummyAssemblyFile.Path}))", loggingContext)
            .ShouldBe(bool.TrueString);

        logger.AllBuildEvents.ShouldHaveSingleItem().ShouldBeOfType<BuildCheckAcquisitionEventArgs>();
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

    #region System.IO.File/Directory relative path resolution in -mt mode

    /// <summary>
    ///  TransientTestState that saves/restores <see cref="FileUtilities.CurrentThreadWorkingDirectory"/>.
    /// </summary>
    private sealed class TransientThreadWorkingDirectory : TransientTestState
    {
        private readonly string _originalValue;

        public TransientThreadWorkingDirectory(string newWorkingDirectory)
        {
            _originalValue = FileUtilities.CurrentThreadWorkingDirectory;
            FileUtilities.CurrentThreadWorkingDirectory = newWorkingDirectory;
        }

        public override void Revert()
        {
            FileUtilities.CurrentThreadWorkingDirectory = _originalValue;
        }
    }

    /// <summary>
    ///  TransientTestState that flips the EnableAllPropertyFunctions AppContext switch on and restores
    ///  its original value on revert (deterministic; does not stick across tests).
    /// </summary>
    private sealed class TransientEnableAllPropertyFunctions : TransientTestState
    {
        private readonly bool _original;

        public TransientEnableAllPropertyFunctions()
        {
            AppContext.TryGetSwitch("Microsoft.Build.EnableAllPropertyFunctions", out _original);
            AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", true);
        }

        public override void Revert() => AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", _original);
    }

    /// <summary>
    ///  Returns an AppContext switch to the "unset" state so that the FeatureSwitches check falls
    ///  back to the environment variable. AppContext can only set a switch true or false (never
    ///  unset), so the entry is removed reflectively from the runtime's private switch table. The
    ///  backing field differs by runtime (.NET Core uses `s_switches`, .NET Framework uses
    ///  `s_switchMap`), so this scans the non-public static dictionaries and clears the key from
    ///  whichever one holds it rather than hard-coding a field name.
    /// </summary>
    private static void UnsetAppContextSwitch(string switchName)
    {
        foreach (FieldInfo field in typeof(AppContext).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (field.GetValue(null) is IDictionary switches)
            {
                lock (switches)
                {
                    if (switches.Contains(switchName))
                    {
                        switches.Remove(switchName);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    ///  Helper: expand a property function expression with CurrentThreadWorkingDirectory set,
    ///  simulating -mt mode where Environment.CurrentDirectory may point elsewhere.
    /// </summary>
    private static string ExpandWithThreadWorkingDirectory(TestEnvironment env, string expression, string workingDir, string wrongDir = null)
    {
        env.WithTransientTestState(new TransientThreadWorkingDirectory(workingDir));
        if (wrongDir is not null)
        {
            env.SetCurrentDirectory(wrongDir);
        }

        return ExpandProperties(expression);
    }

    /// <summary>
    ///  Helper: set the process current directory and return it as the OS reports it. On macOS the test
    ///  temp folder is reached through a symlink (/var -> /private/var), and only the reported form matches
    ///  what resolution against the process current directory produces, so tests that compare -mt output
    ///  against non-mt output must build both sides from this rather than from the TestEnvironment path.
    /// </summary>
    private static string SetCurrentDirectoryCanonical(TestEnvironment env, string path)
    {
        env.SetCurrentDirectory(path);
        return Directory.GetCurrentDirectory();
    }

    // =====================================================================
    // Category A: -mt mode tests for default-allowed File methods
    // =====================================================================

    [Fact]
    public void NormalizePath_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::NormalizePath('obj', 'file.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_ParentSegment_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::NormalizePath('obj', '..', 'file.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "file.txt"));
    }

    [UnixOnlyFact]
    public void NormalizePath_BackslashRootedPath_MatchesNonMultithreadedResult()
    {
        using var env = TestEnvironment.Create(_output);
        var projectDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        // Baseline: non-mt mode, where the process current directory is the project directory.
        string projectDirPath = SetCurrentDirectoryCanonical(env, projectDir.Path);
        string expected = IntrinsicFunctions.NormalizePath(@"\tmp\file.txt");

        // -mt mode must agree: on Unix a backslash is an ordinary filename character, so resolution
        // must not normalize separators or it would silently point at a different file.
        string result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::NormalizePath('\tmp\file.txt'))",
            projectDirPath,
            wrongDir.Path);

        result.ShouldBe(expected);
    }

    [Fact]
    public void NormalizePath_BackslashSeparatedPath_MatchesNonMultithreadedResult()
    {
        using var env = TestEnvironment.Create(_output);
        var projectDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string projectDirPath = SetCurrentDirectoryCanonical(env, projectDir.Path);
        string expected = IntrinsicFunctions.NormalizePath(@"obj\..\file.txt");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::NormalizePath('obj\..\file.txt'))",
            projectDirPath,
            wrongDir.Path);

        result.ShouldBe(expected);
    }

    [Fact]
    public void MSBuildFileExists_BackslashSeparatedPath_MatchesNonMultithreadedResult()
    {
        using var env = TestEnvironment.Create(_output);
        var projectDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(projectDir.Path, "sub"));
        File.WriteAllText(Path.Combine(projectDir.Path, "sub", "marker.txt"), "x");

        // FileExistsNoThrow normalizes separators internally, so both non-mt and -mt must agree.
        env.SetCurrentDirectory(projectDir.Path);
        string expected = IntrinsicFunctions.FileExists(@"sub\marker.txt").ToString();

        string result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::FileExists('sub\marker.txt'))",
            projectDir.Path,
            wrongDir.Path);

        result.ShouldBe(expected);
    }

    [WindowsOnlyFact]
    public void NormalizePath_DriveRelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);
        string drive = Path.GetPathRoot(correctDir.Path).Substring(0, 2);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([MSBuild]::NormalizePath('{drive}obj', 'file.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_AbsolutePath_IgnoresThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var absoluteDir = env.CreateFolder(createFolder: true);

        string absolutePath = Path.Combine(absoluteDir.Path, "file.txt");
        string result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([MSBuild]::NormalizePath('{absolutePath}'))",
            correctDir.Path);

        result.ShouldBe(absolutePath);
    }

    [Fact]
    public void NormalizePath_WithoutThreadWorkingDirectory_UsesProcessWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var processDir = env.CreateFolder(createFolder: true);
        string processDirPath = SetCurrentDirectoryCanonical(env, processDir.Path);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::NormalizePath('obj', 'file.txt'))",
            workingDir: null);

        result.ShouldBe(Path.Combine(processDirPath, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_EmptyPath_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => IntrinsicFunctions.NormalizePath([]));

    [Fact]
    public void NormalizePath_NullPathArray_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() => IntrinsicFunctions.NormalizePath((string[])null));

    [Fact]
    public void NormalizePath_IllegalPath_ThrowsArgumentException()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        env.WithTransientTestState(new TransientThreadWorkingDirectory(correctDir.Path));

        // Resolution against the thread working directory intentionally swallows the invalid-path
        // exception (so that non-throwing intrinsics such as FileExists keep working); NormalizePath
        // is still expected to surface it, matching non-mt behavior.
        Should.Throw<ArgumentException>(() => IntrinsicFunctions.NormalizePath("bad\0path"));
    }

    [Fact]
    public void NormalizeDirectory_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::NormalizeDirectory('obj'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj") + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void MSBuildFileExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::FileExists('marker.txt'))",
            correctDir.Path,
            wrongDir.Path)
            .ShouldBe("True");
        ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::FileExists('absent.txt'))",
            correctDir.Path,
            wrongDir.Path)
            .ShouldBe("False");
    }

    [UnixOnlyFact]
    public void MSBuildFileExists_BackslashRootedPath_MatchesNonMultithreadedResult()
    {
        using var env = TestEnvironment.Create(_output);
        var projectDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        // FileExistsNoThrow normalizes separators internally, so on Unix '\tmp\x' is rooted by the
        // time the probe happens. Resolution must not treat it as relative to the project directory.
        Directory.CreateDirectory(Path.Combine(projectDir.Path, "tmp"));
        File.WriteAllText(Path.Combine(projectDir.Path, "tmp", "decoy.txt"), "x");

        env.SetCurrentDirectory(projectDir.Path);
        string expected = IntrinsicFunctions.FileExists(@"\tmp\decoy.txt").ToString();

        string result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::FileExists('\tmp\decoy.txt'))",
            projectDir.Path,
            wrongDir.Path);

        result.ShouldBe(expected);
    }

    [Fact]
    public void MSBuildDirectoryExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "obj"));

        ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::DirectoryExists('obj'))",
            correctDir.Path,
            wrongDir.Path)
            .ShouldBe("True");
        ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::DirectoryExists('absent'))",
            correctDir.Path,
            wrongDir.Path)
            .ShouldBe("False");
    }

    [Fact]
    public void GetDirectoryNameOfFileAbove_RelativeStartingDirectory_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::GetDirectoryNameOfFileAbove('.', 'marker.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(correctDir.Path);
    }

    [Fact]
    public void GetPathOfFileAbove_RelativeStartingDirectory_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::GetPathOfFileAbove('marker.txt', '.'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "marker.txt"));
    }

    [Fact]
    public void RegisterBuildCheck_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);
        File.WriteAllText(Path.Combine(correctDir.Path, "check.dll"), string.Empty);

        var (logger, loggingContext) = CreateLoggingContext();

        env.WithTransientTestState(new TransientThreadWorkingDirectory(correctDir.Path));
        env.SetCurrentDirectory(wrongDir.Path);

        ExpandProperties("$([MSBuild]::RegisterBuildCheck('check.dll'))", loggingContext)
            .ShouldBe(bool.TrueString);
        var acquisition = logger.AllBuildEvents.ShouldHaveSingleItem().ShouldBeOfType<BuildCheckAcquisitionEventArgs>();

        acquisition.AcquisitionPath.ShouldBe(Path.Combine(correctDir.Path, "check.dll"));
    }

    [Fact]
    public void FileReadAllText_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "notes.txt"), "correct content");
        File.WriteAllText(Path.Combine(wrongDir.Path, "notes.txt"), "wrong content");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::ReadAllText('notes.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe("correct content");
    }

    [Fact]
    public void FileExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "exists.txt"), "data");
        // Do NOT create the file in wrongDir

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::Exists('exists.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe("True");
    }

    [WindowsOnlyFact]
    public void FileGetAttributes_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "attrs.txt");
        File.WriteAllText(filePath, "data");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::BitwiseAnd(32,$([System.IO.File]::GetAttributes('attrs.txt'))))",
            correctDir.Path,
            wrongDir.Path);

        // FileAttributes.Archive = 32 — Windows-specific attribute
        result.ShouldBe("32");
    }

    [Fact]
    public void FileGetCreationTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "time.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetCreationTime(filePath);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetCreationTime('time.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FileGetLastWriteTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "time.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetLastWriteTime(filePath);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetLastWriteTime('time.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FileGetLastAccessTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "time.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetLastAccessTime(filePath);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetLastAccessTime('time.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    // =====================================================================
    // Category A: -mt mode tests for default-allowed Directory methods
    // =====================================================================

    [Fact]
    public void DirectoryExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "subdir"));
        // Do NOT create subdir in wrongDir

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::Exists('subdir'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe("True");
    }

    [Fact]
    public void DirectoryGetParent_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "parent", "child"));

        string result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([System.IO.Directory]::GetParent('parent\child'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldNotBeNullOrEmpty();
        Path.GetFileName(result).ShouldBe("parent");
    }

    [Fact]
    public void DirectoryGetFiles_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(correctDir.Path, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "a.txt"), "data");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::GetFiles('sub'))",
            correctDir.Path,
            wrongDir.Path);

        // GetFiles returns string[], which MSBuild converts to a semicolon-separated string
        result.ShouldContain("a.txt");
    }

    [Fact]
    public void DirectoryGetDirectories_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "parent", "child"));

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::GetDirectories('parent'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldContain("child");
    }

    [Fact]
    public void DirectoryGetLastWriteTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(correctDir.Path, "sub");
        Directory.CreateDirectory(subDir);
        DateTime expected = Directory.GetLastWriteTime(subDir);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::GetLastWriteTime('sub'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DirectoryGetLastAccessTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(correctDir.Path, "sub");
        Directory.CreateDirectory(subDir);
        DateTime expected = Directory.GetLastAccessTime(subDir);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::GetLastAccessTime('sub'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    // =====================================================================
    // Category A+: Extended File methods (MSBUILDENABLEALLPROPERTYFUNCTIONS)
    // =====================================================================

    [Fact]
    public void FileReadAllLines_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "lines.txt"), "line1\nline2");
        File.WriteAllText(Path.Combine(wrongDir.Path, "lines.txt"), "wrong");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::ReadAllLines('lines.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldContain("line1");
    }

    [Fact]
    public void FileReadAllBytes_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllBytes(Path.Combine(correctDir.Path, "data.bin"), [0x42]);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::ReadAllBytes('data.bin'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void FileWriteAllText_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::WriteAllText('output.txt', 'hello'))",
            correctDir.Path,
            wrongDir.Path);

        File.Exists(Path.Combine(correctDir.Path, "output.txt")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(correctDir.Path, "output.txt")).ShouldBe("hello");
        File.Exists(Path.Combine(wrongDir.Path, "output.txt")).ShouldBeFalse();
    }

    [Fact]
    public void FileAppendAllText_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "append.txt"), "base");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::AppendAllText('append.txt', ' added'))",
            correctDir.Path,
            wrongDir.Path);

        File.ReadAllText(Path.Combine(correctDir.Path, "append.txt")).ShouldBe("base added");
    }

    [Fact]
    public void FileDelete_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string correctFile = Path.Combine(correctDir.Path, "todelete.txt");
        string wrongFile = Path.Combine(wrongDir.Path, "todelete.txt");
        File.WriteAllText(correctFile, "data");
        File.WriteAllText(wrongFile, "data");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::Delete('todelete.txt'))",
            correctDir.Path,
            wrongDir.Path);

        File.Exists(correctFile).ShouldBeFalse();
        File.Exists(wrongFile).ShouldBeTrue();
    }

    [Fact]
    public void FileGetCreationTimeUtc_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "utc.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetCreationTimeUtc(filePath);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetCreationTimeUtc('utc.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FileGetLastWriteTimeUtc_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "utc.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetLastWriteTimeUtc(filePath);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetLastWriteTimeUtc('utc.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FileGetLastAccessTimeUtc_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "utc.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetLastAccessTimeUtc(filePath);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetLastAccessTimeUtc('utc.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    // =====================================================================
    // Category A+: Extended Directory methods (MSBUILDENABLEALLPROPERTYFUNCTIONS)
    // =====================================================================

    [Fact]
    public void DirectoryCreateDirectory_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::CreateDirectory('newdir'))",
            correctDir.Path,
            wrongDir.Path);

        Directory.Exists(Path.Combine(correctDir.Path, "newdir")).ShouldBeTrue();
        Directory.Exists(Path.Combine(wrongDir.Path, "newdir")).ShouldBeFalse();
    }

    [Fact]
    public void DirectoryDelete_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "todel"));
        Directory.CreateDirectory(Path.Combine(wrongDir.Path, "todel"));

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::Delete('todel'))",
            correctDir.Path,
            wrongDir.Path);

        Directory.Exists(Path.Combine(correctDir.Path, "todel")).ShouldBeFalse();
        Directory.Exists(Path.Combine(wrongDir.Path, "todel")).ShouldBeTrue();
    }

    // =====================================================================
    // Category B: Regular mode (CurrentThreadWorkingDirectory = null)
    // =====================================================================

    [Fact]
    public void FileReadAllText_AbsolutePath_WorksWithoutThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var dir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(dir.Path, "abs.txt");
        File.WriteAllText(filePath, "absolute content");

        // CurrentThreadWorkingDirectory is null (regular mode)
        string result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.File]::ReadAllText('{filePath}'))",
            workingDir: null);

        result.ShouldBe("absolute content");
    }

    [Fact]
    public void FileExists_AbsolutePath_WorksWithoutThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var dir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(dir.Path, "abs.txt");
        File.WriteAllText(filePath, "data");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.File]::Exists('{filePath}'))",
            workingDir: null);

        result.ShouldBe("True");
    }

    [Fact]
    public void DirectoryExists_AbsolutePath_WorksWithoutThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var dir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(dir.Path, "subdir");
        Directory.CreateDirectory(subDir);

        string result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.Directory]::Exists('{subDir}'))",
            workingDir: null);

        result.ShouldBe("True");
    }

    // =====================================================================
    // Category C: Absolute path passthrough (not mangled by resolution)
    // =====================================================================

    [Fact]
    public void FileReadAllText_AbsolutePath_NotMangledByThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var otherDir = env.CreateFolder(createFolder: true);

        string absFile = Path.Combine(otherDir.Path, "abs.txt");
        File.WriteAllText(absFile, "absolute content");

        // Even though CurrentThreadWorkingDirectory is set, absolute paths should pass through unchanged
        string result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.File]::ReadAllText('{absFile}'))",
            correctDir.Path);

        result.ShouldBe("absolute content");
    }

    [Fact]
    public void FileExists_AbsolutePath_NotMangledByThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var otherDir = env.CreateFolder(createFolder: true);

        string absFile = Path.Combine(otherDir.Path, "abs.txt");
        File.WriteAllText(absFile, "data");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.File]::Exists('{absFile}'))",
            correctDir.Path);

        result.ShouldBe("True");
    }

    // =====================================================================
    // Category D: Multi-path method tests (Copy, Move with two relative paths)
    // =====================================================================

    [Fact]
    public void FileCopy_TwoRelativePaths_BothResolveFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "source.txt"), "copy me");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::Copy('source.txt', 'dest.txt'))",
            correctDir.Path,
            wrongDir.Path);

        File.Exists(Path.Combine(correctDir.Path, "dest.txt")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(correctDir.Path, "dest.txt")).ShouldBe("copy me");
        File.Exists(Path.Combine(wrongDir.Path, "dest.txt")).ShouldBeFalse();
    }

    [Fact]
    public void FileMove_TwoRelativePaths_BothResolveFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "movesrc.txt"), "move me");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::Move('movesrc.txt', 'movedst.txt'))",
            correctDir.Path,
            wrongDir.Path);

        File.Exists(Path.Combine(correctDir.Path, "movesrc.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(correctDir.Path, "movedst.txt")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(correctDir.Path, "movedst.txt")).ShouldBe("move me");
    }

    [Fact]
    public void DirectoryMove_TwoRelativePaths_BothResolveFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "dirsrc"));

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::Move('dirsrc', 'dirdst'))",
            correctDir.Path,
            wrongDir.Path);

        Directory.Exists(Path.Combine(correctDir.Path, "dirsrc")).ShouldBeFalse();
        Directory.Exists(Path.Combine(correctDir.Path, "dirdst")).ShouldBeTrue();
    }

    // =====================================================================
    // Category E: Parent traversal test (../ relative paths)
    // =====================================================================

    [Fact]
    public void FileReadAllText_ParentTraversal_ResolvesCorrectly()
    {
        using var env = TestEnvironment.Create(_output);
        var rootDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        // Create structure: rootDir/sibling/file.txt, and set working dir to rootDir/subdir
        string siblingDir = Path.Combine(rootDir.Path, "sibling");
        string subDir = Path.Combine(rootDir.Path, "subdir");
        Directory.CreateDirectory(siblingDir);
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(siblingDir, "file.txt"), "traversal works");

        string result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([System.IO.File]::ReadAllText('../sibling/file.txt'))",
            subDir,
            wrongDir.Path);

        result.ShouldBe("traversal works");
    }

    // =====================================================================
    // Category F: FixFilePath ordering test (backslash parent traversal)
    // =====================================================================

    [Fact]
    public void FileReadAllText_BackslashParentTraversal_ResolvesCorrectly()
    {
        using var env = TestEnvironment.Create(_output);
        var rootDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string siblingDir = Path.Combine(rootDir.Path, "sibling");
        string subDir = Path.Combine(rootDir.Path, "subdir");
        Directory.CreateDirectory(siblingDir);
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(siblingDir, "file.txt"), "backslash traversal works");

        // Use backslash separators (Windows-style) — FixFilePath must normalize before resolution
        string result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([System.IO.File]::ReadAllText('..\\sibling\\file.txt'))",
            subDir,
            wrongDir.Path);

        result.ShouldBe("backslash traversal works");
    }

    #endregion
}
