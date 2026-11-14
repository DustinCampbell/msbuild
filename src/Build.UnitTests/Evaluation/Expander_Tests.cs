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
using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Engine.UnitTests.Evaluation;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
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
using static Microsoft.Build.UnitTests.Evaluation.ExpansionHelpers;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#if NET5_0_OR_GREATER
using TestOperatingSystem = System.OperatingSystem;
#else
using TestOperatingSystem = Microsoft.Build.Framework.OperatingSystem;
#endif

namespace Microsoft.Build.UnitTests.Evaluation;

public class Expander_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly string _dateToParse = new DateTime(2010, 12, 25).ToString(CultureInfo.CurrentCulture);
    private static readonly string s_rootPathPrefix = NativeMethodsShared.IsWindows ? @"C:\" : Path.VolumeSeparatorChar.ToString();

    private ExpansionTestContext Data => field ??= new();

    [Fact]
    public void ExpandAllIntoTaskItems0()
    {
        IList<TaskItem> items = ExpandAsTaskItems(string.Empty, ExpanderOptions.ExpandProperties);

        ObjectModelHelpers.AssertItemsMatch(string.Empty, GetTaskArrayFromItemList(items));
    }

    [Fact]
    public void ExpandAllIntoTaskItems1()
    {
        IList<TaskItem> items = ExpandAsTaskItems("foo", ExpanderOptions.ExpandProperties);

        ObjectModelHelpers.AssertItemsMatch("foo", GetTaskArrayFromItemList(items));
    }

    [Fact]
    public void ExpandAllIntoTaskItems2()
    {
        IList<TaskItem> items = ExpandAsTaskItems("foo;bar", ExpanderOptions.ExpandProperties);

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
        IList<TaskItem> items = ExpandAsTaskItems(
            "foo;bar;@(compile);@(resource)",
            ExpanderOptions.ExpandPropertiesAndItems,
            Data.Items(
                ("Compile", "foo.cs"),
                ("Compile", "bar.cs"),
                ("Resource", "bing.resx")));

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
        IList<TaskItem> items = ExpandAsTaskItems(
            "foo$(a);$(b);$(c)",
            ExpanderOptions.ExpandProperties,
            Data.Properties(
                ("a", "aaa"),
                ("b", "bbb"),
                ("c", "cc;dd")));

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
        IList<ProjectItemInstance> items = ExpandAsItems(
            "foo$(a);$(b);$(c);$(d",
            ExpanderOptions.ExpandProperties,
            Data.ItemFactory("i"),
            Data.Properties(("a", "aaa"), ("b", "bbb"), ("c", "cc;dd")));

        items.Count.ShouldBe(5);
    }

    /// <summary>
    ///  Expand property expressions into ProjectPropertyInstance items.
    /// </summary>
    [Fact]
    public void ExpandEmptyPropertyExpressionToEmpty()
        => ExpandProperties("$()")
            .ShouldBeEmpty();

    /// <summary>
    ///  Expand an item vector into items of the specified type.
    /// </summary>
    [Fact]
    public void ExpandItemVectorsIntoProjectItemInstancesSpecifyingItemType()
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            "@(i)",
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("j"),
            Data.Items(("i", "i0"), ("i", "i1")));

        items.ShouldHaveItems(
            firstItem =>
            {
                firstItem.ItemType.ShouldBe("j");
                firstItem.EvaluatedInclude.ShouldBe("i0");
            },
            secondItem =>
            {
                secondItem.ItemType.ShouldBe("j");
                secondItem.EvaluatedInclude.ShouldBe("i1");
            });
    }

    /// <summary>
    ///  Expand an item vector into items of the type of the item vector.
    /// </summary>
    [Fact]
    public void ExpandItemVectorsIntoProjectItemInstancesWithoutSpecifyingItemType()
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            "@(i)",
            ExpanderOptions.ExpandItems,
            Data.ItemFactory(),
            Data.Items(("i", "i0"), ("i", "i1")));

        items.ShouldHaveItems(
            firstItem =>
            {
                firstItem.ItemType.ShouldBe("i");
                firstItem.EvaluatedInclude.ShouldBe("i0");
            },
            secondItem =>
            {
                secondItem.ItemType.ShouldBe("i");
                secondItem.EvaluatedInclude.ShouldBe("i1");
            });
    }

    /// <summary>
    ///  Expand an item vector function AnyHaveMetadataValue.
    /// </summary>
    [Theory]
    [InlineData("true", "true")]
    [InlineData("goop", "false")]
    public void ExpandItemVectorFunctionsAnyHaveMetadataValue(string metadataValue, string expected)
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            $"@(i->AnyHaveMetadataValue('Even', '{metadataValue}'))",
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("i"),
            Data.Items(
                Data.Item("i", "i0").WithMetadata("Even", "false"),
                Data.Item("i", "i1").WithMetadata("Even", "true")));

        items.ShouldHaveSingleItem(item =>
        {
            item.ItemType.ShouldBe("i");
            item.EvaluatedInclude.ShouldBe(expected);
        });
    }

    [Fact]
    public void ExpandEmptyItemVectorFunctionWithAnyHaveMetadataValue()
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            "@(unsetItem->AnyHaveMetadataValue('Metadatum', 'value'))",
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("i"));

        items.ShouldHaveSingleItem(item => item.EvaluatedInclude.ShouldBe("false"));
    }

    [Theory]
    [InlineData("@(unsetItem)", false)]
    [InlineData("@(unsetItem->Distinct())", true)]
    public void EmptyItemVectorReportsWhetherExpressionIsTransform(string expression, bool expected)
    {
        var (items, isTransformExpression) = ExpandSingleItemVectorAsItems(
            expression,
            Data.ItemFactory("i"));

        items.ShouldBeEmpty();
        isTransformExpression.ShouldBe(expected);
    }

    /// <summary>
    ///  Expand an item vector function Metadata()->DirectoryName()->Distinct().
    /// </summary>
    [Theory]
    [InlineData("Meta0")]
    [InlineData("Meta9")]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsGetDirectoryNameOfMetadataValueDistinct(string metadataName)
    {
        string metadataValue;
        string expected;

        if (metadataName == "Meta0")
        {
            metadataValue = Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory", "file0.ext");
            expected = Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory");
        }
        else
        {
            metadataValue = Path.Combine("seconddirectory", "file.ext");
            expected = Path.Combine(Directory.GetCurrentDirectory(), "seconddirectory");
        }

        IList<ProjectItemInstance> items = ExpandAsItems(
            $"@(i->Metadata('{metadataName}')->DirectoryName()->Distinct())",
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("i"),
            Data.Items(
                Data.Item("i", "i0").WithMetadata(metadataName, metadataValue)));

        items.ShouldHaveSingleItem(item =>
        {
            item.ItemType.ShouldBe("i");
            item.EvaluatedInclude.ShouldBe(expected);
        });
    }

    public static TheoryData<string, string> ExpandItemVectorFunctionsItemSpecModifierAfterMetadataFunctionData { get; } = new()
    {
        { "@(i->Metadata('Meta0')->Directory())", $"{Path.Combine("firstdirectory", "seconddirectory")}{Path.DirectorySeparatorChar}" },
        { "@(i->Metadata('Meta0')->Filename())", "file0" },
        { "@(i->Metadata('Meta0')->Extension())", ".ext" },
    };

    /// <summary>
    ///  Expand an item vector function that is an itemspec modifier.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpandItemVectorFunctionsItemSpecModifierAfterMetadataFunctionData))]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsItemSpecModifierAfterMetadataFunction(string expression, string expected)
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            expression,
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("i"),
            Data.Items(
                Data.Item("i", "i0")
                    .WithMetadata("Meta0", Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory", "file0.ext"))));

        items.ShouldHaveSingleItem(item =>
        {
            item.ItemType.ShouldBe("i");
            item.EvaluatedInclude.ShouldBe(expected);
        });
    }

    /// <summary>
    ///  Expand an item expression (that isn't a real expression) but includes a property reference nested within a metadata reference.
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsInvalid1()
        => ExpandAll("[@(type-&gt;'%($(a)), '%'')]", Data.Properties("a", "filename"))
            .ShouldBe(@"[@(type-&gt;'%(filename), '%'')]");

    /// <summary>
    ///  Expand an item expression (that isn't a real expression) but includes a metadata reference that still needs to be expanded.
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsInvalid2()
        => ExpandAll("[@(i->'%(Meta9))']")
            .ShouldBe(@"[@(i->')']");

    public static TheoryData<string, string> ExpandItemVectorFunctionsChainedData { get; } = new()
    {
        {
            "@(i->'%(Meta0)'->'%(Directory)'->Distinct())",
            $"{Path.Combine("firstdirectory", "seconddirectory")}{Path.DirectorySeparatorChar}"
        },
        {
            "[@(i->'%(Meta0)'->'%(Directory)'->Distinct())]",
            @"[firstdirectory\seconddirectory\]"
        },
    };

    /// <summary>
    ///  Expand an item vector function that is chained into a string.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpandItemVectorFunctionsChainedData))]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsChained(string expression, string expected)
        => ExpandItems(
            expression,
            Data.Items(
                Data.Item("i", "i0")
                    .WithMetadata("Meta0", Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory", "file0.ext")),
                Data.Item("i", "i1")
                    .WithMetadata("Meta0", Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory", "file0.ext"))))
            .ShouldBe(expected);

    /// <summary>
    ///  Expand an item vector function that is chained and has constants into a string.
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsChainedEmptyResult()
        => ExpandItems(
            "@(i->'%(MetaBlank)'->'%(Directory)'->Distinct())",
            Data.Items(
                Data.Item("i", "i0").WithMetadata("MetaBlank", string.Empty)))
            .ShouldBeEmpty();

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

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.AssertLogContains(@"DirChain0: Value1\;Value2\");
        logger.AssertLogContains(@"DirChain1: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||Value2\");
        logger.AssertLogContains(@"DirChain2: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||##Value2\");
        logger.AssertLogContains(@"DirChain3: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##Value2\");
        logger.AssertLogContains(@"DirChain4: Value1\$||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##Value2\");
        logger.AssertLogContains(@"DirChain5: Value1\$||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##$Value2\");
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

        string current = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
        log.AssertLogContains($"[{current}foo;{current}bar]");
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

        string current = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
        log.AssertLogContains($"[{current}foo;{current}bar]");
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

        string current = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
        log.AssertLogContains($"[{current}foo;{current}bar]");
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

    public static TheoryData<string, string> ExpandItemVectorFunctionsItemSpecModifierAfterQuotedTransformData { get; } = new()
    {
        { "@(i->'%(Meta0)'->'%(Directory)')", Path.Combine("firstdirectory", "seconddirectory") + Path.DirectorySeparatorChar },
        { "@(i->'%(Meta0)'->'%(Filename)')", "file0" },
        { "@(i->'%(Meta0)'->'%(Extension)'->Distinct())", ".ext" },
        { "@(i->'%(Meta0)'->'%(Filename)'->Substring($(Val)))", "le0" },
    };

    /// <summary>
    ///  Expand an item vector function that is an itemspec modifier.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpandItemVectorFunctionsItemSpecModifierAfterQuotedTransformData))]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsItemSpecModifierAfterQuotedTransform(string expression, string expected)
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            expression,
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("i"),
            Data.Properties("Val", "2"),
            Data.Items(
                Data.Item("i", "i0")
                    .WithMetadata("Meta0", Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory", "file0.ext"))));

        items.ShouldHaveSingleItem(item =>
        {
            item.ItemType.ShouldBe("i");
            item.EvaluatedInclude.ShouldBe(expected);
        });
    }

    /// <summary>
    ///  Expand an item vector function Metadata()->DirectoryName().
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsGetDirectoryNameOfMetadataValue()
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            "@(i->Metadata('Meta0')->DirectoryName())",
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("i"),
            Data.Items(
                Data.Item("i", "i0")
                    .WithMetadata("Meta0", Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory", "file0.ext"))));

        items.ShouldHaveSingleItem(item =>
        {
            item.ItemType.ShouldBe("i");
            item.EvaluatedInclude.ShouldBe(Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory"));
        });
    }

    /// <summary>
    ///  Expand an item vector function Metadata() that contains semi-colon delimited sub-items.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsMetadataValueMultiItem()
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            "@(i->Metadata('Meta10')->DirectoryName())",
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("i"),
            Data.Items(
                Data.Item("i", "i0")
                    .WithMetadata(
                        "Meta10",
                        $";{Path.Combine("someo%3bherplace", "foo.txt")};{Path.Combine("secondd%3brectory", "file.ext")};")));

        items.ShouldHaveItems(
            firstItem =>
            {
                firstItem.ItemType.ShouldBe("i");
                firstItem.EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), @"someo;herplace"));
            },
            secondItem =>
            {
                secondItem.ItemType.ShouldBe("i");
                secondItem.EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), @"secondd;rectory"));
            });
    }

    /// <summary>
    ///  Expand an item vector function Items->ClearMetadata().
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsClearMetadata()
    {
        IList<ProjectItemInstance> items = ExpandAsItems(
            "@(i->ClearMetadata())",
            ExpanderOptions.ExpandItems,
            Data.ItemFactory("i"),
            Data.Items(
                Data.Item("i", "i0").WithMetadata("Foo", "Bar")));

        items.ShouldHaveSingleItem(item =>
        {
            item.ItemType.ShouldBe("i");
            item.Metadata.ShouldBeEmpty();
        });
    }

    /// <summary>
    ///  Regression test for bug when there are literally zero items declared
    ///  in the project, we should continue to expand item list references to empty-string
    ///  rather than not expand them at all.
    /// </summary>
    [Fact]
    public void ZeroItemsInProjectExpandsToEmpty()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                <Target Name=`Build` Condition=`'@(foo)'!=''` >
                    <Message Text=`This target should NOT run.`/>
                </Target>
            </Project>
            """);

        logger.AssertLogDoesntContain("This target should NOT run.");

        logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                <ItemGroup>
                    <foo Include=`abc` Condition=` '@(foo)' == '' ` />
                </ItemGroup>
                <Target Name=`Build`>
                    <Message Text=`Item list foo contains @(foo)`/>
                </Target>
            </Project>
            """);

        logger.AssertLogContains("Item list foo contains abc");
    }

    [Fact]
    public void ItemIncludeContainsMultipleItemReferences()
    {
        const string Content = """
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
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.AssertLogContains("Property OutputType=Library");
        logger.AssertLogContains("Item ObjFiles=foo.obj;bar.obj");
        logger.AssertLogContains("Item CleanFiles=foo.obj;bar.obj");
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

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        logger.AssertLogContains("MSB4023");
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

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        logger.AssertLogContains("MSB4023");
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

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        logger.AssertLogContains("MSB4023");
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

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        logger.AssertLogContains("MSB4023");
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

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        logger.AssertLogContains("MSB4023");
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

        MockLogger logger = Helpers.BuildProjectContentUsingBuildManagerExpectResult(Content, BuildResultCode.Failure);

        logger.AssertLogContains("MSB4248");
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

        MockLogger logger = Helpers.BuildProjectContentUsingBuildManagerExpectResult(content, BuildResultCode.Failure);

        logger.AssertLogContains("MSB4248");
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

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);

        logger.AssertLogContains("MSB4023");
    }

    /// <summary>
    ///  Filter by metadata presence.
    /// </summary>
    [Fact]
    public void HasMetadata()
    {
        const string Content = """
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
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.AssertLogContains("[One|Three|Four]");
    }

    /// <summary>
    ///  Filter items by WithoutMetadataValue function.
    /// </summary>
    [Fact]
    public void WithoutMetadataValue()
    {
        const string Content = """
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
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.AssertLogContains("[Two|Three|Four]");
    }

    [Fact]
    public void DirectItemMetadataReferenceShouldBeCaseInsensitive()
    {
        const string Content = """
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
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.AssertLogContains("QualifiedNotMatchCase Foo=X");
        logger.AssertLogContains("QualifiedMatchCase Foo=X");
        logger.AssertLogContains("UnqualifiedNotMatchCase Foo=X");
        logger.AssertLogContains("UnqualifiedMatchCase Foo=X");
    }

    [Fact]
    public void ItemDefinitionGroupMetadataReferenceShouldBeCaseInsensitive()
    {
        const string Content = """
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
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.AssertLogContains("QualifiedNotMatchCase Foo=X");
        logger.AssertLogContains("QualifiedMatchCase Foo=X");
        logger.AssertLogContains("UnqualifiedNotMatchCase Foo=X");
        logger.AssertLogContains("UnqualifiedMatchCase Foo=X");
    }

    [Fact]
    public void WellKnownMetadataReferenceShouldBeCaseInsensitive()
    {
        const string Content = """
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
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.AssertLogContains("QualifiedNotMatchCase Foo=Foo");
        logger.AssertLogContains("QualifiedMatchCase Foo=Foo");
        logger.AssertLogContains("UnqualifiedNotMatchCase Foo=Foo");
        logger.AssertLogContains("UnqualifiedMatchCase Foo=Foo");
    }

    /// <summary>
    ///  Verify when there is an error due to an attempt to use a static method that we report the method name.
    /// </summary>
    [Fact]
    public void StaticMethodErrorMessageHaveMethodName()
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

        InvalidProjectFileException e = Should.Throw<InvalidProjectFileException>(() =>
            Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false));

        e.Message.ShouldContain("[System.IO.Path]::Combine(null, '')", Case.Insensitive);
    }

    /// <summary>
    ///  Verify when there is an error due to an attempt to use a static method that we report the method name.
    /// </summary>
    [Fact]
    public void StaticMethodErrorMessageHaveMethodName1()
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

        InvalidProjectFileException e = Should.Throw<InvalidProjectFileException>(() =>
            Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false));

        e.Message.ShouldContain("System.IO.Path::Combine('a','b')", Case.Insensitive);
    }

    [Fact]
    public void StaticMethodWithThrowawayParameterSupported()
    {
        const string Content = """
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.Int32]::TryParse("3", out _))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.FullLog.ShouldContain("Value is True");
    }

    [Fact]
    public void StaticMethodWithThrowawayParameterSupported2()
    {
        const string Content = """
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.Int32]::TryParse("notANumber", out _))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.FullLog.ShouldContain("Value is False");
    }

    [Fact]
    public void StaticMethodWithUnderscoreNotConfusedWithThrowaway()
    {
        const string Content = """
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.String]::Join('_', 'asdf', 'jkl'))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """;

        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        logger.FullLog.ShouldContain("Value is asdf_jkl");
    }

    /// <summary>
    ///  Creates an expander with complicated item metadata, properties, and items.
    ///  The data contains escaped characters, metadata that references properties,
    ///  properties that reference items, and other complex scenarios.
    /// </summary>
    /// <returns>
    ///  The populated expander.
    /// </returns>
    private Expander<ProjectPropertyInstance, ProjectItemInstance> CreateComplexExpander()
    {
        PropertyDictionary<ProjectPropertyInstance> properties = [
            ProjectPropertyInstance.Create("Gee_Aych_Ayee", "ghi"),
            ProjectPropertyInstance.Create("OutputPath", @"\jk ; l\mno%253bpqr\stu"),
            ProjectPropertyInstance.Create("TargetPath", "@(IntermediateAssembly->'%(RelativeDir)')")
        ];

        ItemDictionary<ProjectItemInstance> items = [
            Data.Item("Resource", "string$(p).resx").WithMetadata("ddd", "444"),
            Data.Item("Resource", "dialogs%253b.resx").WithMetadata("eee", "555"),
            Data.Item("Content", "about.bmp").WithMetadata("fff", "666")
        ];

        Lookup lookup = new(items, properties);

        // Add primary items
        lookup.EnterScope("x");

        string include1 = NativeMethodsShared.IsWindows ? @"subdir1\engine.dll" : "subdir1/engine.dll";
        string include2 = NativeMethodsShared.IsWindows ? @"subdir2\tasks.dll" : "subdir2/tasks.dll";

        lookup.PopulateWithItems("IntermediateAssembly", [
            Data.Item("IntermediateAssembly", include1).WithMetadata("aaa", "111"),
            Data.Item("IntermediateAssembly", include2).WithMetadata("bbb", "222")
        ]);

        lookup.PopulateWithItems("Content", [
            Data.Item("Content", "splash.bmp").WithMetadata("ccc", "333")
        ]);

        IMetadataTable metadata = Data.Metadata(
            ("Culture", "abc%253bdef;$(Gee_Aych_Ayee)"),
            ("Language", "english"));

        return CreateExpander(lookup, lookup, metadata);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoTaskItems with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoTaskItemsComplex()
    {
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateComplexExpander();

        IList<TaskItem> taskItems = expander.ExpandIntoTaskItemsLeaveEscaped(
            "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)",
            ExpanderOptions.ExpandAll,
            MockElementLocation.Instance);

        // the following items are passed to the TaskItem constructor, and thus their ItemSpecs should be
        // in escaped form.
        ObjectModelHelpers.AssertItemsMatch(
            $"""
            string$(p): ddd=444
            dialogs%253b: eee=555
            splash.bmp: ccc=333
            \jk
            l\mno%253bpqr\stu
            subdir1{Path.DirectorySeparatorChar}: aaa=111
            subdir2{Path.DirectorySeparatorChar}: bbb=222
            english_abc%253bdef
            ghi
            """,
            GetTaskArrayFromItemList(taskItems));
    }

    [Fact]
    public void ExpandAllIntoStringExpandsTransformedItemFilenames()
        => EscapingUtilities.UnescapeAll(
            ExpandAll(
                "@(Resource->'%(Filename)') ;",
                items: Data.Items(
                    ("Resource", "string$(p).resx"),
                    ("Resource", "dialogs%253b.resx"))))
            .ShouldBe("string$(p);dialogs%3b ;");

    [Fact]
    public void ExpandAllIntoStringExpandsItemVector()
        => ExpandAll("@(Content)", items: Data.Items("Content", "splash.bmp"))
            .ShouldBe("splash.bmp");

    [Theory]
    [InlineData("@(NonExistent)")]
    [InlineData("$(NonExistent)")]
    [InlineData("%(NonExistent)")]
    public void ExpandAllIntoStringExpandsMissingReferencesToEmpty(string value)
        => ExpandAll(value)
            .ShouldBeEmpty();

    [Fact]
    public void ExpandAllIntoStringExpandsEscapedProperty()
        => EscapingUtilities.UnescapeAll(
            ExpandAll("$(OutputPath)", Data.Properties("OutputPath", @"\jk ; l\mno%253bpqr\stu")))
            .ShouldBe(@"\jk ; l\mno%3bpqr\stu");

    [Fact]
    public void ExpandAllIntoStringExpandsPropertyContainingItemTransform()
        => ExpandAll(
            "$(TargetPath)",
            Data.Properties("TargetPath", "@(IntermediateAssembly->'%(RelativeDir)')"),
            Data.Items(
                ("IntermediateAssembly", Path.Combine("subdir1", "engine.dll")),
                ("IntermediateAssembly", Path.Combine("subdir2", "tasks.dll"))))
            .ShouldBe($"subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar}");

    [Fact]
    public void ExpandAllIntoStringExpandsMetadataContainingProperty()
        => EscapingUtilities.UnescapeAll(
            ExpandAll(
                "%(Language)_%(Culture)",
                Data.Properties("Gee_Aych_Ayee", "ghi"),
                metadata: Data.Metadata(
                    ("Culture", "abc%253bdef;$(Gee_Aych_Ayee)"),
                    ("Language", "english"))))
            .ShouldBe("english_abc%3bdef;ghi");

    /// <summary>
    ///  Exercises ExpandAllIntoString with an item list using a transform that is empty.
    /// </summary>
    /// <param name="value">The value to expand.</param>
    [Theory]
    [InlineData("@(IntermediateAssembly->'')")]
    [InlineData("@(IntermediateAssembly->'%(goop)')")]
    public void ExpandAllIntoStringEmpty(string value)
        => ExpandAll(value, items: Data.Items(("IntermediateAssembly", "a.dll"), ("IntermediateAssembly", "b.dll")))
            .ShouldBe(";");

    public static TheoryData<string, bool, bool> QuotedTransformDerivableItemSpecModifierUsesRequiredContextData { get; } = new()
    {
        // These modifiers do not require project context.
        { ItemSpecModifiers.Filename, false, false },
        { ItemSpecModifiers.Extension, false, false },
        { ItemSpecModifiers.RelativeDir, false, false },
        { ItemSpecModifiers.Identity, false, false },
        { ItemSpecModifiers.ModifiedTime, false, false },
        { ItemSpecModifiers.CreatedTime, false, false },
        { ItemSpecModifiers.AccessedTime, false, false },

        // These modifiers require the project directory.
        { ItemSpecModifiers.FullPath, true, false },
        { ItemSpecModifiers.RootDir, true, false },
        { ItemSpecModifiers.Directory, true, false },

        // These modifiers require both the project directory and defining-project metadata.
        { ItemSpecModifiers.DefiningProjectFullPath, true, true },
        { ItemSpecModifiers.DefiningProjectDirectory, true, true },
        { ItemSpecModifiers.DefiningProjectName, true, true },
        { ItemSpecModifiers.DefiningProjectExtension, true, true },
    };

    [Theory]
    [MemberData(nameof(QuotedTransformDerivableItemSpecModifierUsesRequiredContextData))]
    public void QuotedTransformDerivableItemSpecModifierUsesRequiredContext(
        string modifier,
        bool usesProjectDirectory,
        bool usesDefiningProject)
    {
        string itemSpec = Path.Combine("src", "directory", "File.cs");
        string definingProject = Path.Combine(Data.Project.Directory, "Imported.targets");
        var item = new ContextTrackingItem("Compile", itemSpec, Data.Project.Directory, definingProject);
        var expander = CreateExpander<ProjectPropertyInstance, ContextTrackingItem>(ExpanderOptions.ExpandItems, items: Data.Items(item));

        string expected = ItemSpecModifiers.GetItemSpecModifier(itemSpec, modifier, Data.Project.Directory, definingProject);
        string actual = expander.ExpandIntoStringLeaveEscaped(
            $"@(Compile->'%({modifier})')",
            ExpanderOptions.ExpandItems,
            MockElementLocation.Instance);

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
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateComplexExpander();

        const string Value = """
            @(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)
            """;

        string actual = expander.ExpandIntoStringAndUnescape(Value, ExpanderOptions.ExpandAll, MockElementLocation.Instance);

        actual.ShouldBe(
            $"""
            string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%3bdef;ghi
            """);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringLeaveEscapedComplex()
    {
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateComplexExpander();

        const string Value = """
            @(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)
            """;

        string actual = expander.ExpandIntoStringLeaveEscaped(Value, ExpanderOptions.ExpandAll, MockElementLocation.Instance);

        actual.ShouldBe(
            $"""
            string$(p);dialogs%253b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%253bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%253bdef;ghi
            """);
    }

    [Fact]
    public void ExpandMetadataTruncatesValuesLongerThanCharacterLimit()
    {
        const int CharacterLimit = 1024;
        const string Ellipsis = "...";
        string value = new(' ', CharacterLimit + 1);
        string expected = $"{new string(' ', CharacterLimit - Ellipsis.Length)}{Ellipsis}";

        ExpandMetadata("%(Value)", ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate, Data.Metadata("Value", value))
            .ShouldBe(expected);
    }

    [Fact]
    public void ExpandPropertiesTruncatesValuesLongerThanCharacterLimit()
    {
        const int CharacterLimit = 1024;
        const string Ellipsis = "...";
        string value = new(' ', CharacterLimit + 1);
        string expected = $"{new string(' ', CharacterLimit - Ellipsis.Length)}{Ellipsis}";

        ExpandAsString("$(Value)", ExpanderOptions.ExpandProperties | ExpanderOptions.Truncate, properties: Data.Properties("Value", value))
            .ShouldBe(expected);
    }

    [Fact]
    public void ExpandItemsTruncatesAtCharacterLimit()
    {
        const int CharacterLimit = 1024;
        const string Prefix = "Foo;";
        const string Ellipsis = "...";
        string value = new(' ', CharacterLimit + 1);
        string expected = $"{Prefix}{new string(' ', CharacterLimit - Prefix.Length - Ellipsis.Length)}{Ellipsis}";

        ExpandAsString(
            "@(Item)",
            ExpanderOptions.ExpandItems | ExpanderOptions.Truncate,
            items: Data.Items(("Item", "Foo"), ("Item", value), ("Item", "Bar")))
            .ShouldBe(expected);
    }

    [Fact]
    public void ExpandItemsPreservesValueAtCharacterLimit()
    {
        const int CharacterLimit = 1024;
        string value = new(' ', CharacterLimit);

        ExpandAsString(
            "@(Item)",
            ExpanderOptions.ExpandItems | ExpanderOptions.Truncate,
            items: Data.Items(("Item", value), ("Item", "Foo")))
            .ShouldBe($"{value};...");
    }

    [Theory]
    [InlineData("@(Item)", "Item0;Item1;Item2;...")]
    [InlineData("@(Item->'%(Value)')", "Value0;Value1;Value2;...")]
    [InlineData("@(Item->'%(Nonexistent)')", ";;;...")]
    public void ExpandItemsTruncatesAfterItemLimit(string expression, string expected)
        => ExpandAsString(
            expression,
            ExpanderOptions.ExpandItems | ExpanderOptions.Truncate,
            items: Data.Items(
                Data.Item("Item", "Item0").WithMetadata("Value", "Value0"),
                Data.Item("Item", "Item1").WithMetadata("Value", "Value1"),
                Data.Item("Item", "Item2").WithMetadata("Value", "Value2"),
                Data.Item("Item", "Item3").WithMetadata("Value", "Value3")))
            .ShouldBe(expected);

    /// <summary>
    ///  Exercises ExpandAllIntoString with a string that does not need expanding.
    ///  In this case the expanded string should be reference identical to the passed in string.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringExpectIdenticalReference()
    {
        const string Value = "abc123";
        string actual = ExpandAll(Value);

        actual.ShouldBeSameAs(Value);
    }

    public static TheoryData<object, string> ExpandAllIntoStringExpanderOptionsData { get; } = new()
    {
        {
            ExpanderOptions.ExpandProperties,
            """@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ; %(NonExistent) ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; %(Language)_%(Culture)"""
        },
        {
            ExpanderOptions.ExpandPropertiesAndMetadata,
            """@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ;  ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; english_abc%3bdef;ghi"""
        },
        {
            ExpanderOptions.ExpandAll,
            $"""string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%3bdef;ghi"""
        },
        {
            ExpanderOptions.ExpandItems,
            """string$(p);dialogs%3b ; splash.bmp ;  ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)"""
        },
    };

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data and various expander options.
    /// </summary>
    /// <param name="options">The boxed expansion options to use.</param>
    /// <param name="expected">The expected expanded and unescaped value.</param>
    [Theory]
    [MemberData(nameof(ExpandAllIntoStringExpanderOptionsData))]
    public void ExpandAllIntoStringExpanderOptions(object options, string expected)
    {
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateComplexExpander();

        const string Value = """
            @(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)
            """;

        string actual = expander.ExpandIntoStringAndUnescape(Value, (ExpanderOptions)options, MockElementLocation.Instance);

        actual.ShouldBe(expected);
    }

    /// <summary>
    ///  Creates the fixed metadata table used to exercise the hand-written metadata scanner.
    ///  Metadata values intentionally contain no path separators so that <c>MaybeAdjustFilePath</c>
    ///  does not perturb the asserted results.
    /// </summary>
    /// <returns>
    ///  The metadata table.
    /// </returns>
    private IMetadataTable CreateScannerMetadata()
        => Data.Metadata(
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

    // Simple expansion, unqualified and qualified.
    [InlineData("%(Culture)", "en-US")]
    [InlineData("%(Foo)", "Bar")]
    [InlineData("%(Compile.Link)", "Link.cs")]

    // Whitespace around the parentheses and the dot separator is allowed.
    [InlineData("%( Culture )", "en-US")]
    [InlineData("%( Compile . Link )", "Link.cs")]

    // Missing metadata expands to empty; a missing qualifier does not fall back to the unqualified key.
    [InlineData("%(DoesNotExist)", "")]
    [InlineData("%(Other.Foo)", "")]

    // Malformed references are emitted verbatim.
    [InlineData("%(", "%(")]
    [InlineData("%()", "%()")]
    [InlineData("%( )", "%( )")]
    [InlineData("%(.x)", "%(.x)")]

    // The outer reference is not closed by ')', so only the inner reference expands.
    [InlineData("%(Culture%(Foo))", "%(CultureBar)")]

    // Mixed with surrounding literal text and adjacent references.
    [InlineData("prefix_%(Culture)_suffix", "prefix_en-US_suffix")]
    [InlineData("%(Culture)%(Foo)", "en-USBar")]
    public void ExpandMetadata_ScannerEdgeCases(string input, string expected)
        => ExpandMetadata(input, CreateScannerMetadata())
            .ShouldBe(expected);

    [Theory]
    [InlineData("%(", ExpanderOptions.ExpandMetadata)]
    [InlineData("%(Culture)", ExpanderOptions.ExpandBuiltInMetadata)]
    [InlineData("%(Filename)", ExpanderOptions.ExpandCustomMetadata)]
    internal void ExpandMetadata_NoExpansionReturnsOriginalString(string input, ExpanderOptions options)
        => ExpandMetadata(input, options, CreateScannerMetadata())
            .ShouldBeSameAs(input);

    /// <summary>
    ///  Parity tests for metadata expansion in the gaps between (and within the separators of) item
    ///  vector expressions. Items are intentionally left unexpanded (ExpandMetadata only) so the
    ///  assertions isolate the gap/separator boundary handling in ScanAndExpandMetadataInGaps,
    ///  including the case where "@(" appears but does not form a well-formed item vector.
    /// </summary>
    [Theory]

    // Metadata after, before, and between item vectors.
    [InlineData("@(Compile)%(Culture)", "@(Compile)en-US")]
    [InlineData("%(Culture)@(Compile)", "en-US@(Compile)")]
    [InlineData("@(A)%(Culture)@(B)", "@(A)en-US@(B)")]

    // A lone item vector has no gaps and is returned unchanged, even with embedded metadata in a transform.
    [InlineData("@(Compile)", "@(Compile)")]
    [InlineData("@(Compile->'%(Filename)')", "@(Compile->'%(Filename)')")]

    // Metadata embedded in an item vector's separator is expanded in place.
    [InlineData("@(Compile, '%(Culture)')", "@(Compile, 'en-US')")]

    // "@(" that does not form a valid item vector still has its surrounding metadata expanded.
    [InlineData("%(Culture)@(", "en-US@(")]
    public void ExpandMetadata_ItemVectorGapsAndSeparators(string input, string expected)
        => ExpandMetadata(input, CreateScannerMetadata())
            .ShouldBe(expected);

    /// <summary>
    ///  Verifies the built-in vs. custom metadata gating in the scanner: a reference is expanded only
    ///  when the matching <see cref="ExpanderOptions"/> flag is set; otherwise it is emitted verbatim.
    /// </summary>
    /// <remarks>
    ///  Declared <c>internal</c> because <see cref="ExpanderOptions"/> is internal; this assembly is
    ///  configured to discover non-public test methods.
    /// </remarks>
    [Theory]

    // Custom metadata (Culture) only expands with ExpandCustomMetadata.
    [InlineData("%(Culture)", ExpanderOptions.ExpandCustomMetadata, "en-US")]
    [InlineData("%(Culture)", ExpanderOptions.ExpandBuiltInMetadata, "%(Culture)")]

    // Built-in metadata (Filename) only expands with ExpandBuiltInMetadata.
    [InlineData("%(Filename)", ExpanderOptions.ExpandBuiltInMetadata, "App")]
    [InlineData("%(Filename)", ExpanderOptions.ExpandCustomMetadata, "%(Filename)")]
    internal void ExpandMetadata_BuiltInVsCustomGating(string input, ExpanderOptions options, string expected)
        => ExpandMetadata(input, options, CreateScannerMetadata())
            .ShouldBe(expected);

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
            ExpandItems(input, Data.Items("i", "i0")));

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
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateComplexExpander();

        const string Value = """
            @(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)
            """;

        List<string> expanded = [.. expander.ExpandIntoStringListLeaveEscaped(Value, ExpanderOptions.ExpandAll, MockElementLocation.Instance)];

        expanded.ShouldBe([
            "string$(p)",
            "dialogs%253b",
            "splash.bmp",
            """\jk""",
            """l\mno%253bpqr\stu""",
            "subdir1" + Path.DirectorySeparatorChar,
            "subdir2" + Path.DirectorySeparatorChar,
            "english_abc%253bdef",
            "ghi"
        ]);
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
        => ExpandProperties("""$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory)""")
            .ShouldBeEmpty();

    // Compat hack: WebProjects may have an import with a condition like:
    //       Condition=" '$(Solutions.VSVersion)' == '8.0'"
    // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
    // Be compatible by returning an empty string here.
    [Fact]
    public void Regress692569()
        => ExpandProperties("$(Solutions.VSVersion)")
            .ShouldBeEmpty();

    /// <summary>
    ///  Verifies that properties incorrectly missing the Registry: prefix produce an error.
    /// </summary>
    /// <param name="expression">The invalid registry property expression.</param>
    [Theory]
    [InlineData("""$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@XXXXDBDirectory)""")]
    [InlineData("""$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectoryX)""")]
    public void RegistryPropertyInvalidPrefixError(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(expression));

    [SupportedOSPlatform("windows")]
    public static TheoryData<object, RegistryValueKind, string> RegistryPropertyData
    {
        get
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            return new()
            {
                { "String", RegistryValueKind.String, "String" },
                { Encoding.UTF8.GetBytes("String"), RegistryValueKind.Binary, "83;116;114;105;110;103" },
                { 123456, RegistryValueKind.DWord, "123456" },
                { $"%{envVar}%", RegistryValueKind.ExpandString, Environment.GetEnvironmentVariable(envVar)! },
                { 123456789123456789L, RegistryValueKind.QWord, "123456789123456789" },
                { new[] { "A", "B", "C", "D" }, RegistryValueKind.MultiString, "A;B;C;D" },
            };
        }
    }

    /// <summary>
    ///  Expands registry properties with each supported registry value kind.
    /// </summary>
    /// <param name="value">The registry value to set.</param>
    /// <param name="valueKind">The registry value kind.</param>
    /// <param name="expected">The expected expanded value.</param>
    [WindowsOnlyTheory]
    // The data depends on the execution-process environment and contains heterogeneous registry value types.
    [MemberData(nameof(RegistryPropertyData), DisableDiscoveryEnumeration = true)]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyValue(object value, RegistryValueKind valueKind, string expected)
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            key.SetValue("Value", value, valueKind);

            ExpandProperties("""$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)""")
                .ShouldBe(expected);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [Fact]
    public void TestItemSpecModiferEscaping()
    {
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

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogDoesntContain("%28");
        log.AssertLogDoesntContain("%29");
    }

    [Fact]
    public void TestGetPathToReferenceAssembliesAsFunction()
    {
        if (ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version48) == null)
        {
            // if there aren't any reference assemblies installed on the machine in the first place, of course
            // we're not going to find them. :)
            return;
        }

        string content = $"""
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

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

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
    ///  Expand property function that returns a null.
    /// </summary>
    /// <param name="expression">The expression containing the null-returning function.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("$([System.Environment]::GetEnvironmentVariable(`_NonExistentVar`))", "")]
    [InlineData("prefix_$([System.Environment]::GetEnvironmentVariable(`_NonExistentVar`))", "prefix_")]
    public void PropertyFunctionNullReturn(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    /// <summary>
    ///  Expand property function that takes no arguments and returns a string.
    /// </summary>
    [Fact]
    public void PropertyFunctionNoArguments()
        => ExpandProperties(
            "$(SomeStuff.ToUpperInvariant())",
            Data.Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("THIS IS SOME STUFF");

    /// <summary>
    ///  Expand property function that takes no arguments and returns a string (trimmed).
    /// </summary>
    [Fact]
    public void PropertyFunctionNoArgumentsTrim()
        => ExpandProperties(
            "$(FileName.Trim())",
            Data.Properties("FileName", "    foo.ext   "))
            .ShouldBe("foo.ext");

    /// <summary>
    ///  Expand property function that is a get property accessor.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyGet()
        => ExpandProperties(
            "$(SomeStuff.Length)",
            Data.Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("18");

    /// <summary>
    ///  Expand property function which is a manual get property accessor.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyManualGet()
        => ExpandProperties(
            "$(SomeStuff.get_Length())",
            Data.Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("18");

    /// <summary>
    ///  Expand property function which is a manual get property accessor and a concatenation of a constant.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyNoArgumentsConcat()
        => ExpandProperties(
            "$(SomeStuff.ToLowerInvariant())_goop",
            Data.Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("this is some stuff_goop");

    /// <summary>
    ///  Expand property function with a constant argument.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgument()
        => ExpandProperties(
            "$(SomeStuff.SubString(13))",
            Data.Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("STUff");

    /// <summary>
    ///  Expand property function with a constant argument that contains spaces.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentWithSpaces()
        => ExpandProperties(
            "$(SomeStuff.SubString(8))",
            Data.Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("SOME STUff");

    /// <summary>
    ///  Expand property function with a constant argument.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyPathRootSubtraction()
        => ExpandProperties(
            "$(MyPath.SubString($(RootPath.Length)))",
            Data.Properties(
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
            Data.Properties(("Value", "3"), ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("STUff");

    /// <summary>
    ///  Expand property function that has a boolean return value.
    /// </summary>
    /// <param name="propertyName">The property whose value is tested.</param>
    /// <param name="expected">The expected Boolean result.</param>
    [Theory]
    [InlineData("PathRoot2", "True")]
    [InlineData("PathRoot", "False")]
    public void PropertyFunctionPropertyWithArgumentBooleanReturn(string propertyName, string expected)
        => ExpandProperties(
            $"$({propertyName}.Endswith({Path.DirectorySeparatorChar}))",
            Data.Properties(
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
            Data.Properties(("Value", "3"), ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("ff");

    /// <summary>
    ///  Expand property function with chained functions on its results.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentChained()
        => ExpandProperties(
            "$(SomeStuff.ToUpperInvariant().ToLowerInvariant())",
            Data.Properties(("Value", "3"), ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("this is some stuff");

    /// <summary>
    ///  Expand property function with an argument that is a function.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentNested()
        => ExpandProperties(
            "$(SomeStuff.SubString($(Value.get_Length())))",
            Data.Properties(("Value", "12345"), ("SomeStuff", "1234567890")))
            .ShouldBe("67890");

    /// <summary>
    ///  Expand property function that returns an generic list.
    /// </summary>
    [Fact]
    public void PropertyFunctionGenericListReturn()
        => ExpandProperties("$([MSBuild]::__GetListTest())")
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Verifies conversion of property-function results across scalar, collection, and nested collection values.
    /// </summary>
    /// <param name="scenario">The property-value conversion scenario to exercise.</param>
    /// <param name="expected">The expected escaped string representation.</param>
    [Theory]
    [InlineData("Null", "")]
    [InlineData("EmptyString", "")]
    [InlineData("String", "a%3bb")]
    [InlineData("Scalar", "42")]
    [InlineData("EmptyDictionary", "")]
    [InlineData("EmptyArray", "")]
    [InlineData("EmptyEnumerable", "")]
    [InlineData("Dictionary", "a%3bb=c%25d")]
    [InlineData("EnumerableWithEmptyElements", "a;;b%3bc")]
    [InlineData("NonzeroLowerBoundArray", "a;b")]
    [InlineData("MultidimensionalArray", "a;b;c;d")]
    [InlineData("NestedEnumerable", "a%25253bb%253bc")]
    [InlineData("NestedDictionary", "key=a%253bb%3bc")]
    public void PropertyFunctionConvertsComplexValues(string scenario, string expected)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());

        string? typeName = typeof(PropertyValueConversionTestData).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::GetValue(`{scenario}`))")
            .ShouldBe(expected);
    }

    /// <summary>
    ///  Expand property function that returns an array.
    /// </summary>
    [Fact]
    public void PropertyFunctionArrayReturn()
        => ExpandProperties("$(List.Split(-))", Data.Properties("List", "A-B-C-D"))
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Expand property function that returns a Dictionary.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionDictionaryReturn()
        => ExpandProperties("$([System.Environment]::GetEnvironmentVariables())").ToUpperInvariant()
            .ShouldContain($"OS={Environment.GetEnvironmentVariable("OS")}".ToUpperInvariant());

    /// <summary>
    ///  Expand property function that returns an array.
    /// </summary>
    [Fact]
    public void PropertyFunctionArrayReturnManualSplitter()
        => ExpandProperties(
            "$(List.Split($(Splitter.ToCharArray())))",
            Data.Properties(("List", "A-B-C-D"), ("Splitter", "-")))
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Expand property function that returns an array.
    /// </summary>
    [Fact]
    public void PropertyFunctionInCondition()
    {
        PropertyDictionary<ProjectPropertyInstance> properties = [
            ProjectPropertyInstance.Create("PathRoot", Path.Combine(s_rootPathPrefix, "goo")),
            ProjectPropertyInstance.Create("PathRoot2", $"{Path.Combine(s_rootPathPrefix, "goop")}{Path.DirectorySeparatorChar}")
        ];

        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(properties, FileSystems.Default);

        ConditionEvaluator.EvaluateCondition(
            @"'$(PathRoot2.Endswith(`" + Path.DirectorySeparatorChar + "`))' == 'true'",
            ParserOptions.AllowAll,
            expander,
            ExpanderOptions.ExpandProperties,
            Directory.GetCurrentDirectory(),
            MockElementLocation.Instance,
            FileSystems.Default,
            new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)))
            .ShouldBeTrue();

        ConditionEvaluator.EvaluateCondition(
            @"'$(PathRoot.EndsWith(" + Path.DirectorySeparatorChar + "))' == 'false'",
            ParserOptions.AllowAll,
            expander,
            ExpanderOptions.ExpandProperties,
            Directory.GetCurrentDirectory(),
            MockElementLocation.Instance,
            FileSystems.Default,
            new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)))
            .ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies that invalid property-function expressions produce an error.
    /// </summary>
    /// <param name="expression">The invalid property-function expression.</param>
    [Theory]
    [InlineData("[$(SomeStuff($(Value)))]")]
    [InlineData("[$(SomeStuff.Lgg)]")]
    [InlineData("$(SomeStuff.ToUpperInvariant().Foo)")]
    [InlineData("[$(SomeStuff($(System.DateTime.Now)))]")]
    [InlineData("$(SomeStuff.ToLowerInvariant()_goop)")]
    [InlineData("[$(SomeStuff.Substring(HELLO!))]")]
    [InlineData("[$(SomeStuff.Substring(-10))]")]
    [InlineData("""$(([System.DateTime]::Now).ToString("MM.dd.yyyy"))""")]
    public void PropertyFunctionInvalid(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                expression,
                Data.Properties(("Value", "3"), ("SomeStuff", "This IS SOME STUff"))));

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
        => ExpandProperties("$(System)", Data.Properties("System", "The System Namespace"))
            .ShouldBe("The System Namespace");

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodMakeRelative()
        => ExpandProperties(
            "$([MSBuild]::MakeRelative($(ParentPath), `$(FilePath)`))",
            Data.Properties(
                ("ParentPath", Path.Combine(s_rootPathPrefix, "abc", "def")),
                ("FilePath", Path.Combine(s_rootPathPrefix, "abc", "def", "foo.cpp"))))
            .ShouldBe(@"foo.cpp");

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethod1()
        => ExpandProperties(
            "$([System.IO.Path]::Combine($(Drive), `$(File)`))",
            Data.Properties(("Drive", s_rootPathPrefix), ("File", Path.Combine("foo", "file.txt"))))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Expand a typed property-function result as an argument to another property function.
    /// </summary>
    [Fact]
    public void PropertyFunctionConstructorResultIsPreservedAsTypedArgument()
        => ExpandProperties(
            "$([System.Version]::new($(ver1)).Equals($([System.Version]::new($(ver1)))))",
            Data.Properties("ver1", "1.2.3.4"))
            .ShouldBe(bool.TrueString);

    /// <summary>
    ///  Expand property function that creates an instance of a type.
    /// </summary>
    [Fact]
    public void PropertyFunctionConstructor2()
        => ExpandProperties(
            "$([System.Version]::new($(ver1)).CompareTo($([System.Version]::new($(ver2)))))",
            Data.Properties(("ver1", "1.2.3.4"), ("ver2", "2.2.3.4")))
            .ShouldBe("-1");

    /// <summary>
    ///  Expand property function that is only available when MSBUILDENABLEALLPROPERTYFUNCTIONS=1.
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: "https://github.com/dotnet/coreclr/issues/15662")]
    public void PropertyStaticFunctionAllEnabled()
    {
        using var env = TestEnvironment.Create();
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
    [Fact]
    public void PropertyStaticFunctionUsingNamespaceNotFound()
    {
        string? env = Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS");

        try
        {
            Environment.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

            _ = Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([Microsoft.FOO.FileIO.FileSystem]::CurrentDirectory)"));
            _ = Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([Foo.Baz]::new())"));
            _ = Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([Foo]::new())"));
            _ = Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([Foo.]::new())"));
            _ = Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([.Foo]::new())"));
            _ = Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([.]::new())"));
            _ = Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([]::new())"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", env);
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
            $"$([System.IO.Path]::Combine(`{s_rootPathPrefix}`, `$(File)`))",
            Data.Properties("File", Path.Combine("foo", "file.txt")))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Expands quoted path arguments containing spaces.
    /// </summary>
    /// <param name="rootDirectory">The root directory passed directly to <c>Path.Combine</c>.</param>
    /// <param name="fileDirectory">The directory stored in the <c>File</c> property.</param>
    /// <param name="fileName">The file name stored in the <c>File</c> property.</param>
    [Theory]
    [InlineData("foo goo", "foo goo", "file.txt")]
    [InlineData("foo baz", "foo bar", "baz.txt")]
    [InlineData("foo baz ", "foo bar", "baz.txt")]
    public void PropertyFunctionStaticMethodQuotedPathWithSpaces(string rootDirectory, string fileDirectory, string fileName)
        => ExpandProperties(
            $"$([System.IO.Path]::Combine(`{Path.Combine(s_rootPathPrefix, rootDirectory)}`, `$(File)`))",
            Data.Properties("File", Path.Combine(fileDirectory, fileName)))
            .ShouldBe(Path.Combine(s_rootPathPrefix, rootDirectory, fileDirectory, fileName));

    /// <summary>
    ///  Expands a quoted date argument with the specified format.
    /// </summary>
    /// <param name="format">The date format to apply.</param>
    [Theory]
    [InlineData("yyyy/MM/dd HH:mm:ss")]
    [InlineData("MM.dd.yyyy")]
    public void PropertyFunctionStaticMethodQuotedDate(string format)
        => ExpandProperties($"""$([System.DateTime]::Parse('{_dateToParse}').ToString("{format}"))""")
            .ShouldBe(DateTime.Parse(_dateToParse).ToString(format));

    /// <summary>
    ///  Expand property function that calls a static method with quoted arguments.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted4()
        => ExpandProperties("""$([System.DateTime]::Now.ToString("MM.dd.yyyy"))""")
            .ShouldBe(DateTime.Now.ToString("MM.dd.yyyy"));

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodNested()
        => ExpandProperties(
            $"$([System.IO.Path]::Combine(`{s_rootPathPrefix}`, $([System.IO.Path]::Combine(`foo`,`file.txt`))))",
            Data.Properties("File", $"foo{Path.DirectorySeparatorChar}file.txt"))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Expand property function that calls a static method regex.
    /// </summary>
    /// <param name="expression">The regular-expression property function.</param>
    /// <param name="expected">The expected Boolean result.</param>
    [Theory]
    [InlineData(
        """$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, `RegexOptions.IgnoreCase,RegexOptions.Singleline`))""",
        "True")]
    // C#-style enum combination syntax.
    [InlineData(
        """$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, System.Text.RegularExpressions.RegexOptions.IgnoreCase|RegexOptions.Singleline))""",
        "True")]
    [InlineData("""$([System.Text.RegularExpressions.Regex]::IsMatch(`100 GBP`, `^-?\d+(\.\d{2})?$`))""", "False")]
    public void PropertyFunctionStaticMethodRegex(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    /// <summary>
    ///  Expand property function that calls a static method available only on net46 (Environment.GetFolderPath).
    /// </summary>
    [Fact]
    public void PropertyFunctionGetFolderPath()
        => ExpandProperties("$([System.Environment]::GetFolderPath(SpecialFolder.System))")
            .ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.System));

    /// <summary>
    ///  The test exercises: RuntimeInformation / OSPlatform usage, static method invocation, static property invocation,
    ///  method invocation expression as argument, call chain expression as argument.
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
        static string FormatString(string s, string platform, string architecture) => s
            .Replace("$$platform$$", platform)
            .Replace("$$architecture$$", architecture);

        string currentPlatformString = Helpers.GetOSPlatformAsString();
        string currentArchitectureString = RuntimeInformation.OSArchitecture.ToString();

        propertyFunction = FormatString(propertyFunction, currentPlatformString, currentArchitectureString);
        expectedExpansion = FormatString(expectedExpansion, currentPlatformString, currentArchitectureString);

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
        string propertyFunction = $"$([System.OperatingSystem]::IsOSPlatform('{platform}'))";
        string expected = TestOperatingSystem.IsOSPlatform(platform) ? "True" : "False";

        ExpandProperties(propertyFunction)
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
        string propertyFunction = $"$([System.OperatingSystem]::IsOSPlatformVersionAtLeast('{platform}', {major}, {minor}, {build}, {revision}))";
        string expected = TestOperatingSystem.IsOSPlatformVersionAtLeast(platform, major, minor, build, revision) ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsLinux()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsLinux())";
        string expected = TestOperatingSystem.IsLinux() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsFreeBSD()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsFreeBSD())";
        string expected = TestOperatingSystem.IsFreeBSD() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsFreeBSDVersionAtLeast(int major, int minor, int build, int revision)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsFreeBSDVersionAtLeast({major}, {minor}, {build}, {revision}))";
        string expected = TestOperatingSystem.IsFreeBSDVersionAtLeast(major, minor, build, revision) ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsMacOS()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsMacOS())";
        string expected = TestOperatingSystem.IsMacOS() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 15, 0)]
    [InlineData(999, 0, 0)]
    public void IsMacOSVersionAtLeast(int major, int minor, int build)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsMacOSVersionAtLeast({major}, {minor}, {build}))";
        string expected = TestOperatingSystem.IsMacOSVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsWindows()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsWindows())";
        string expected = TestOperatingSystem.IsWindows() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(4, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsWindowsVersionAtLeast(int major, int minor, int build, int revision)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsWindowsVersionAtLeast({major}, {minor}, {build}, {revision}))";
        string expected = TestOperatingSystem.IsWindowsVersionAtLeast(major, minor, build, revision) ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

#if NET5_0_OR_GREATER

    [Fact]
    public void IsAndroid()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsAndroid())";
        string expected = TestOperatingSystem.IsAndroid() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsAndroidVersionAtLeast(int major, int minor, int build, int revision)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsAndroidVersionAtLeast({major}, {minor}, {build}, {revision}))";
        string expected = TestOperatingSystem.IsAndroidVersionAtLeast(major, minor, build, revision) ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsIOS()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsIOS())";
        string expected = TestOperatingSystem.IsIOS() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(16, 5, 1)]
    [InlineData(999, 0, 0)]
    public void IsIOSVersionAtLeast(int major, int minor, int build)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsIOSVersionAtLeast({major}, {minor}, {build}))";
        string expected = TestOperatingSystem.IsIOSVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsMacCatalyst()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsMacCatalyst())";
        string expected = TestOperatingSystem.IsMacCatalyst() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(999, 0, 0)]
    public void IsMacCatalystVersionAtLeast(int major, int minor, int build)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsMacCatalystVersionAtLeast({major}, {minor}, {build}))";
        string expected = TestOperatingSystem.IsMacCatalystVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsTvOS()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsTvOS())";
        string expected = TestOperatingSystem.IsTvOS() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(16, 5, 0)]
    [InlineData(999, 0, 0)]
    public void IsTvOSVersionAtLeast(int major, int minor, int build)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsTvOSVersionAtLeast({major}, {minor}, {build}))";
        string expected = TestOperatingSystem.IsTvOSVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsWatchOS()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsWatchOS())";
        string expected = TestOperatingSystem.IsWatchOS() ? "True" : "False";

        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(9, 5, 2)]
    [InlineData(999, 0, 0)]
    public void IsWatchOSVersionAtLeast(int major, int minor, int build)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsWatchOSVersionAtLeast({major}, {minor}, {build}))";
        string expected = TestOperatingSystem.IsWatchOSVersionAtLeast(major, minor, build) ? "True" : "False";

        ExpandProperties(propertyFunction)
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
    public void StringIndexOfTests(string propertyName, string properyValue, string propertyFunction, string expectedExpansion)
        => ExpandProperties(propertyFunction, Data.Properties(propertyName, properyValue))
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
    public void StringEndsWithTests(string propertyName, string propertyValue, string propertyFunction, string expectedExpansion)
        => ExpandProperties(propertyFunction, Data.Properties(propertyName, propertyValue))
            .ShouldBe(expectedExpansion);

    [Theory]
    [InlineData("AString", "linux", "$(AString.Equals($(AString.ToLower()), 'StringComparison.InvariantCulture'))", "True")]
    [InlineData("AString", "Linux", "$(AString.Equals($(AString.ToLower()), 'StringComparison.InvariantCulture'))", "False")]
    [InlineData("AString", "hello", "$(AString.Equals('hello', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "hello", "$(AString.Equals('HELLO', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "hello", "$(AString.Equals('HELLO', 'StringComparison.Ordinal'))", "False")]
    public void StringEqualsWithStringComparisonTests(string propertyName, string propertyValue, string propertyFunction, string expectedExpansion)
        => ExpandProperties(propertyFunction, Data.Properties(propertyName, propertyValue))
            .ShouldBe(expectedExpansion);

    [Fact]
    public void IsOsPlatformShouldBeCaseInsensitiveToParameter()
    {
        string osPlatformLowerCase = Helpers.GetOSPlatformAsString().ToLower();

        ExpandProperties($"$([MSBuild]::IsOsPlatform({osPlatformLowerCase}))")
            .ShouldBe("True");
    }

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
        AssertSuccess(expectedSign > 0, $"$([MSBuild]::VersionGreaterThan('{a}', '{b}'))");
        AssertSuccess(expectedSign >= 0, $"$([MSBuild]::VersionGreaterThanOrEquals('{a}', '{b}'))");
        AssertSuccess(expectedSign < 0, $"$([MSBuild]::VersionLessThan('{a}', '{b}'))");
        AssertSuccess(expectedSign <= 0, $"$([MSBuild]::VersionLessThanOrEquals('{a}', '{b}'))");
        AssertSuccess(expectedSign == 0, $"$([MSBuild]::VersionEquals('{a}', '{b}'))");
        AssertSuccess(expectedSign != 0, $"$([MSBuild]::VersionNotEquals('{a}', '{b}'))");
    }

    [Theory]
    [InlineData("net45", ".NETFramework", "4.5")]
    [InlineData("netcoreapp3.1", ".NETCoreApp", "3.1")]
    [InlineData("netstandard2.1", ".NETStandard", "2.1")]
    [InlineData("net5.0-ios12.0", ".NETCoreApp", "5.0")]
    [InlineData("foo", "Unsupported", "0.0")]
    public void PropertyFunctionTargetFrameworkParsing(string tfm, string expectedIdentifier, string expectedVersion)
    {
        AssertSuccess(expectedIdentifier, $"$([MSBuild]::GetTargetFrameworkIdentifier('{tfm}'))");
        AssertSuccess(expectedVersion, $"$([MSBuild]::GetTargetFrameworkVersion('{tfm}'))");
    }

    [Theory]
    [InlineData("net45", 2, "4.5")]
    [InlineData("net45", 3, "4.5.0")]
    [InlineData("net472", 3, "4.7.2")]
    [InlineData("net472", 2, "4.7.2")]
    public void PropertyFunctionTargetFrameworkVersionMultipartParsing(string tfm, int versionPartCount, string expectedVersion)
        => AssertSuccess(expectedVersion, $"$([MSBuild]::GetTargetFrameworkVersion('{tfm}', {versionPartCount}))");

    [Theory]
    [InlineData("net5.0-windows10.1.2.3", 4, "10.1.2.3")]
    [InlineData("net5.0-windows10.1.2.3", 2, "10.1.2.3")]
    [InlineData("net5.0-windows10.0.0.3", 2, "10.0.0.3")]
    [InlineData("net5.0-windows0.0.0.3", 2, "0.0.0.3")]
    public void PropertyFunctionTargetPlatformVersionMultipartParsing(string tfm, int versionPartCount, string expectedVersion)
        => AssertSuccess(expectedVersion, $"$([MSBuild]::GetTargetPlatformVersion('{tfm}', {versionPartCount}))");

    [Theory]
    [InlineData("net5.0-ios12.0", "ios", "12.0")]
    [InlineData("net5.1-android1.1", "android", "1.1")]
    [InlineData("net6.0-windows99.99", "windows", "99.99")]
    [InlineData("net5.0-ios", "ios", "0.0")]
    [InlineData("foo", "", "0.0")]
    public void PropertyFunctionTargetPlatformParsing(string tfm, string expectedIdentifier, string expectedVersion)
    {
        AssertSuccess(expectedIdentifier, $"$([MSBuild]::GetTargetPlatformIdentifier('{tfm}'))");
        AssertSuccess(expectedVersion, $"$([MSBuild]::GetTargetPlatformVersion('{tfm}'))");
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
    public void PropertyFunctionTargetFrameworkComparisons(string tfm1, string tfm2, bool expectedFrameworkCompatible)
        => AssertSuccess(expectedFrameworkCompatible, $"$([MSBuild]::IsTargetFrameworkCompatible('{tfm1}', '{tfm2}'))");

    private static void AssertThrows(string expression, string expectedMessage)
    {
        InvalidProjectFileException ex = Should.Throw<InvalidProjectFileException>(
            () => ExpandProperties(expression));

        ex.Message.ShouldContain(expectedMessage);
    }

    private static void AssertSuccess(object expected, string expression)
        => ExpandProperties(expression)
            .ShouldBe(expected.ToString());

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
        string tempPath = FileUtilities.TempFileDirectory;
        string tempFile = Path.GetFileName(FileUtilities.GetTemporaryFile());

        try
        {
            string directoryStart = Path.Combine(tempPath, @"one\two\three\four\five");

            PropertyDictionary<ProjectPropertyInstance> properties = [
                ProjectPropertyInstance.Create("StartingDirectory", directoryStart),
                ProjectPropertyInstance.Create("FileToFind", tempFile)
            ];

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(properties, FileSystems.Default);

            string result = expander.ExpandIntoStringAndUnescape(
                "$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), $(FileToFind)))",
                ExpanderOptions.ExpandProperties,
                MockElementLocation.Instance);

            FileUtilities.EnsureTrailingSlash(result).ShouldBe(FileUtilities.EnsureTrailingSlash(tempPath));

            result = expander.ExpandIntoStringAndUnescape(
                "$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), Hobbits))",
                ExpanderOptions.ExpandProperties,
                MockElementLocation.Instance);

            result.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///  Verifies that <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> returns the correct path if a file exists
    ///  or an empty string if it doesn't.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAbove()
    {
        // Set element location to a file deep in the directory structure.  This is where the function should start looking
        var location = ElementLocation.Create(
            Path.Combine(ObjectModelHelpers.TempProjectDir, "one", "two", "three", "four", "five", Path.GetRandomFileName()));

        string fileToFind = FileUtilities.GetTemporaryFile(ObjectModelHelpers.TempProjectDir, fileName: null, ".tmp");

        try
        {
            PropertyDictionary<ProjectPropertyInstance> properties = [
                ProjectPropertyInstance.Create("FileToFind", Path.GetFileName(fileToFind))
            ];

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(properties, FileSystems.Default);

            string result = expander.ExpandIntoStringAndUnescape(
                "$([MSBuild]::GetPathOfFileAbove($(FileToFind)))",
                ExpanderOptions.ExpandProperties,
                location);

            result.ShouldBe(fileToFind);

            result = expander.ExpandIntoStringAndUnescape(
                "$([MSBuild]::GetPathOfFileAbove('Hobbits'))",
                ExpanderOptions.ExpandProperties,
                location);

            result.ShouldBeEmpty();
        }
        finally
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }
    }

    /// <summary>
    ///  Verifies that the usage of GetPathOfFileAbove() within an in-memory project throws an exception.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAboveInMemoryProject()
    {
        const string Xml = """
            <Project>
                <PropertyGroup>
                    <foo>$([MSBuild]::GetPathOfFileAbove('foo'))</foo>
                </PropertyGroup>
            </Project>
            """;

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            ObjectModelHelpers.CreateInMemoryProject(Xml));

        exception.Message.ShouldStartWith("""The expression "[MSBuild]::GetPathOfFileAbove(foo, '')" cannot be evaluated.""");
    }

    /// <summary>
    ///  Verifies that <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> only accepts a file name.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAboveFileNameOnly()
    {
        string fileWithPath = Path.Combine("foo", "bar", "file.txt");

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                @"$([MSBuild]::GetPathOfFileAbove($(FileWithPath)))",
                Data.Properties("FileWithPath", fileWithPath)));

        exception.Message.ShouldContain(
            ResourceUtilities.FormatResourceStringStripCodeAndKeyword("InvalidGetPathOfFileAboveParameter", fileWithPath));
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
    /// <param name="expression">The property-function expression.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("$([MSBuild]::ValueOrDefault('', '42'))", "42")]
    [InlineData("$([MSBuild]::ValueOrDefault('42', '43'))", "42")]
    public void PropertyFunctionValueOrDefault(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    /// <summary>
    ///  Expand property function choosing either the value (from the environment) or the default specified.
    /// </summary>
    [Fact]
    public void PropertyFunctionValueOrDefaultFromEnvironment()
    {
        PropertyDictionary<ProjectPropertyInstance> properties = [
            ProjectPropertyInstance.Create("DifferentTargetsPath", "Different")
        ];

        var expander = CreateExpander(ExpanderOptions.ExpandProperties, properties);

        expander.ExpandIntoStringLeaveEscaped(
            "$([MSBuild]::ValueOrDefault('$(DifferentTargetsPath)', '42'))",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance)
            .ShouldBe("Different");

        properties["DifferentTargetsPath"] = ProjectPropertyInstance.Create("DifferentTargetsPath", string.Empty);

        expander.ExpandIntoStringLeaveEscaped(
            "$([MSBuild]::ValueOrDefault('$(DifferentTargetsPath)', '43'))",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance)
            .ShouldBe("43");
    }

#if FEATURE_APPDOMAIN

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    /// <param name="runtime">The runtime argument.</param>
    [Theory]
    [InlineData("CurrentRuntime")]
    [InlineData("   CurrentRuntime    ")]
    public void PropertyFunctionDoesTaskHostExist(string runtime)
        => ExpandProperties($"$([MSBuild]::DoesTaskHostExist('{runtime}', 'CurrentArchitecture'))")
            .ShouldBe("true", StringCompareShould.IgnoreCase);

#endif

    /// <summary>
    ///  Expands normalized directories from one or more path segments.
    /// </summary>
    /// <param name="includeSecondPath">Whether to include the second path segment.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropertyFunctionNormalizeDirectory(bool includeSecondPath)
    {
        string expression = includeSecondPath
            ? "$([MSBuild]::NormalizeDirectory($(MyPath), $(MySecondPath)))"
            : "$([MSBuild]::NormalizeDirectory($(MyPath)))";
        string expectedPath = includeSecondPath
            ? Path.Combine("one", "two")
            : "one";

        ExpandProperties(
            expression,
            Data.Properties(("MyPath", "one"), ("MySecondPath", "two")))
            .ShouldBe($"{Path.GetFullPath(expectedPath)}{Path.DirectorySeparatorChar}");
    }

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Error()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$([MSBuild]::DoesTaskHostExist('ASDF', 'CurrentArchitecture'))"));

#if FEATURE_APPDOMAIN

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Evaluated()
        => ExpandProperties(
            "$([MSBuild]::DoesTaskHostExist('$(Runtime)', '$(Architecture)'))",
            Data.Properties(("Runtime", "CurrentRuntime"), ("Architecture", "CurrentArchitecture")))
            .ShouldBe("true", StringCompareShould.IgnoreCase);

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_NonexistentTaskHost()
    {
        string taskHostName = Environment.GetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME");
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME", "asdfghjkl.exe");
            NodeProviderOutOfProcTaskHost.ClearCachedTaskHostPaths();

            // CLR has been forced to pretend not to exist, whether it actually does or not
            ExpandProperties("$([MSBuild]::DoesTaskHostExist('CLR2', 'CurrentArchitecture'))")
                .ShouldBe("false", StringCompareShould.IgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME", taskHostName);
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
        string tempFile = FileUtilities.GetTemporaryFile();
        try
        {
            File.SetAttributes(tempFile, FileAttributes.ReadOnly | FileAttributes.Archive);

            ExpandProperties($"$([MSBuild]::BitwiseAnd(32,$([System.IO.File]::GetAttributes({tempFile}))))")
                .ShouldBe("32");
        }
        finally
        {
            File.SetAttributes(tempFile, FileAttributes.Normal);
            File.Delete(tempFile);
        }
    }

    /// <summary>
    ///  Expand intrinsic property function calls a static arithmetic method.
    /// </summary>
    /// <param name="expression">The arithmetic property-function expression.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("$([MSBuild]::Add(39.9, 2.1))", "42")]
    [InlineData("$([MSBuild]::Add(40, 2))", "42")]
    [InlineData("$([MSBuild]::Subtract(44, 2))", "42")]
    [InlineData("$([MSBuild]::Subtract(42.9, 0.9))", "42")]
    [InlineData("$([MSBuild]::Multiply(21, 2))", "42")]
    [InlineData("$([MSBuild]::Multiply(84.0, 0.5))", "42")]
    [InlineData("$([MSBuild]::Divide(84, 2))", "42")]
    [InlineData("$([MSBuild]::Divide(84.4, 2.0))", "42.2")]
    [InlineData("$([MSBuild]::Modulo(85, 2))", "1")]
    [InlineData("$([MSBuild]::Modulo(2345.5, 43))", "23.5")]
    [UseInvariantCulture]
    public void PropertyFunctionStaticMethodIntrinsicMaths(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    /// <summary>
    ///  Expand intrinsic property functions that call a bit operator.
    /// </summary>
    /// <param name="expression">The bitwise property-function expression.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("$([MSBuild]::BitwiseOr(40, 2))", "42")]
    [InlineData("$([MSBuild]::BitwiseAnd(42, 2))", "2")]
    [InlineData("$([MSBuild]::BitwiseXor(213, 255))", "42")]
    [InlineData("$([MSBuild]::BitwiseNot(-43))", "42")]
    [InlineData("$([MSBuild]::LeftShift(1, 2))", "4")]
    [InlineData("$([MSBuild]::RightShift(-8, 2))", "-2")]
    [InlineData("$([MSBuild]::RightShiftUnsigned(-8, 2))", "1073741822")]
    public void PropertyFunctionStaticMethodIntrinsicBitOperations(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    /// <summary>
    ///  Expand a property reference that has whitespace around the property name (should result in empty).
    /// </summary>
    [Fact]
    public void PropertySimpleSpaced()
        => ExpandProperties(@"$( SomeStuff )", Data.Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBeEmpty();

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegitryValue()
    {
        try
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            key.SetValue("Value", $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(
                """$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', '$(SomeProperty)'))""",
                Data.Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar)!);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    /// <summary>
    ///  Expands the default registry value through the supported intrinsic function forms.
    /// </summary>
    /// <param name="expression">The registry property-function expression.</param>
    [WindowsOnlyTheory]
    [InlineData("""$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', null))""")]
    [InlineData("""$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', null, null, RegistryView.Default, RegistryView.Default))""")]
    [InlineData("""$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', null, null, Microsoft.Win32.RegistryView.Default))""")]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryDefaultValue(string expression)
    {
        try
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            key.SetValue(string.Empty, $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(expression)
                .ShouldBe(Environment.GetEnvironmentVariable(envVar)!);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    /// <summary>
    ///  Expand a property function that references item metadata.
    /// </summary>
    [Fact]
    public void PropertyFunctionConsumingItemMetadata()
    {
        string result = ExpandAll(
            "$([System.IO.Path]::Combine($(SomePath),%(Compile.Identity)))",
            Data.Properties("SomePath", Path.Combine(s_rootPathPrefix, "some", "path")),
            Data.Items("Compile", "fOo.Cs"),
            Data.Metadata("Compile.Identity", "fOo.Cs"));

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
        => ExpandAll(
            $"$([System.String]::new(%({metadatumName})))",
            metadata: Data.Metadata("Language", "english"))
            .ShouldBe(metadatumValue);

    public static IEnumerable<object?[]> GetHashAlgoTypes()
        => Enum.GetNames(typeof(IntrinsicFunctions.StringHashingAlgorithm))
            .Append(null)
            .Select(t => new object?[] { t });

    [Theory]
    [MemberData(nameof(GetHashAlgoTypes))]
    public void PropertyFunctionHashCodeSameOnlyIfStringSame(string? hashType)
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
            "cat1s",
        ];

        string hashTypeString = hashType == null ? "" : $", '{hashType}'";
        string[] hashes = [.. stringsToHash.Select(toHash =>
            ExpandProperties($"$([MSBuild]::StableStringHash('{toHash}'{hashTypeString}))"))];

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
    public void StableStringHashReturnsExpectedType(string hashType)
    {
        Type expectedType = hashType switch
        {
            null => typeof(int),
            "Legacy" => typeof(int),
            "Fnv1a32bit" => typeof(int),
            "Fnv1a32bitFast" => typeof(int),
            "Fnv1a64bit" => typeof(long),
            "Fnv1a64bitFast" => typeof(long),
            "Sha256" => typeof(string),

            _ => Assumed.Unreachable<Type>(),
        };

        object hashValue = hashType is null
            ? IntrinsicFunctions.StableStringHash("FooBar")
            : IntrinsicFunctions.StableStringHash("FooBar", ParseHashType(hashType));

        hashValue.ShouldBeOfType(expectedType);

        static IntrinsicFunctions.StringHashingAlgorithm ParseHashType(string hashType)
            => (IntrinsicFunctions.StringHashingAlgorithm)Enum.Parse(typeof(IntrinsicFunctions.StringHashingAlgorithm), hashType);
    }

    [Theory]
    [InlineData("easycase")]
    [InlineData("")]
    [InlineData("\"\n()\tsdfIR$%#*;==")]
    public void TestBase64Conversion(string value)
    {
        string result = ExpandProperties($"$([MSBuild]::ConvertToBase64('{value}'))");

        result.Trim('=').ShouldAllBe(c => char.IsLetterOrDigit(c) || c == '+' || c == '/');

        ExpandProperties($"$([MSBuild]::ConvertFromBase64('{result}'))")
            .ShouldBe(value);
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

    public static TheoryData<string, string> PropertyFunctionValidExpressionData
    {
        get
        {
            TheoryData<string, string> data = new()
            {
                { "$(input.ToString()[1])", "X" },
                { "$(input[1])", "X" },
                { "$(listofthings.Split(';')[$(position)])", "e" },
                { @"$([System.Text.RegularExpressions.Regex]::Match($(Input), `EXPORT\s+(.+)`).Groups[1].Value)", "a" },
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
                { "$([System.TimeSpan]::Equals(null, null))", "True" },
                { "$([MSBuild]::Add(40,null))", "40" },
                { "$([MSBuild]::Add( 40 , null ))", "40" },
                { "$([MSBuild]::Add(null,40))", "40" },
                { "$([MSBuild]::Escape(';'))", "%3b" },
                { "$([MSBuild]::UnEscape('%3b'))", ";" },
                { "$(e.Substring($(e.Length)))", "" },
                { "$([System.Int32]::MaxValue)", int.MaxValue.ToString() },
                { "x$()", "x" },
                // Comparison between non-numeric and numeric properties. More details: #10583.
                { "$(a.Equals($(c)))", "False" },
                { "$(a.CompareTo($(c)))", "1" },
            };

            if (!NativeMethodsShared.IsWindows)
            {
                data.Add("$(Registry:X)", "");
            }

            return data;
        }
    }

    public static TheoryData<string> PropertyFunctionInvalidExpressionData
    {
        get
        {
            TheoryData<string> data =
            [
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
                """

                $(

                $(

                [System.IO]::Path.GetDirectory('c:\foo\bar\baz.txt')

                ).Substring(

                '$([System.IO]::Path.GetPathRoot(

                '$([System.IO]::Path.GetDirectory('c:\foo\bar\baz.txt'))'

                ).Length)'



                )

                """,
                "$([Microsoft.VisualBasic.FileIO.FileSystem]::CurrentDirectory)",
                "$(e.Length..ToString())",
                "$(SomeStuff.get_Length(null))",
                "$(SomeStuff.Substring((1)))",
                "$(b.Substring(-10, $(c)))",
                "$(b.Substring(-10, $(emptystring)))",
                "$(b.Substring(-10, $(space)))",
                "$([MSBuild]::Add.Sub(null,40))",
                "$([MSBuild]::Add( ,40))",
                "$([MSBuild]::Add('',40))",
                "$([MSBuild]::Add(40,,,))",
                "$([MSBuild]::Add(40, ,,))",
                "$([MSBuild]::Add(40,)",
                "$([MSBuild]::Add(40,X)",
                "$([MSBuild]::Add(40,",
                "$([MSBuild]::Add(40",
                "$([MSBuild]::Add(,))",
                "$([System.TimeSpan]::Equals(,))",
                "$([System.TimeSpan]::Equals($(space),$(emptystring)))",
                "$([System.TimeSpan]::Equals($(emptystring),$(emptystring)))",
                "$([MSBuild]::Add($(PropertyContainingNullAsAString),40))",
                "$([MSBuild]::Add('null',40))",
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
                "$((((",
                "$($())",
                "()",
            ];

#if !RUNTIME_TYPE_NETCORE
            if (NativeMethodsShared.IsWindows)
            {
                // '|' is only an invalid character in Windows filesystems.
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

    /// <summary>
    ///  Verifies valid property-function expressions.
    /// </summary>
    /// <param name="expression">The expression to expand.</param>
    /// <param name="expected">The expected expanded value.</param>
    [Theory]
    [MemberData(nameof(PropertyFunctionValidExpressionData))]
    public void PropertyFunctionValidExpression(string expression, string expected)
        => ExpandProperties(expression, CreatePropertyFunctionExpressionProperties())
            .ShouldBe(expected);

    /// <summary>
    ///  Verifies expressions that must not evaluate successfully.
    /// </summary>
    /// <param name="expression">The invalid or disallowed expression.</param>
    [Theory]
    [MemberData(nameof(PropertyFunctionInvalidExpressionData))]
    public void PropertyFunctionInvalidExpression(string expression)
    {
        try
        {
            ExpandProperties(expression, CreatePropertyFunctionExpressionProperties())
                .ShouldBe(expression, $"Expected '{expression}' to remain unexpanded or throw.");
        }
        catch (InvalidProjectFileException)
        {
        }
    }

    private IPropertyProvider<ProjectPropertyInstance> CreatePropertyFunctionExpressionProperties()
    {
        AvailableStaticMethods.Reset_ForUnitTestsOnly();

        return Data.Properties(
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
            ("emptystring", ""),
            ("space", " "),
            ("listofthings", "a;b;c;d;e;f;g;h;i;j;k;l"),
            ("input", "EXPORT a"),
            ("propertycontainingnullasastring", "null"));
    }

    /// <summary>
    ///  Ensures trailing slashes are added to constant and property arguments.
    /// </summary>
    /// <param name="useProperty">Whether the function argument is a property reference.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropertyFunctionEnsureTrailingSlash(bool useProperty)
    {
        string path = Path.Combine("foo", "bar");

        string expression = useProperty
            ? "$([MSBuild]::EnsureTrailingSlash($(SomeProperty)))"
            : $"$([MSBuild]::EnsureTrailingSlash('{path}'))";

        ExpandProperties(expression, Data.Properties("SomeProperty", path))
            .ShouldBe(path + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void PropertyFunctionWithNewLines()
    {
        const string propertyFunction = """
            $(SomeProperty
             .Substring(0, 10)
              .ToString()
               .Substring(0, 5)
                 .ToString())
            """;

        ExpandProperties(propertyFunction, Data.Properties("SomeProperty", "6C8546D5297C424F962201B0E0E9F142"))
            .ShouldBe("6C854");
    }

    /// <summary>
    ///  Exercises string index methods.
    /// </summary>
    /// <param name="expression">The property-function expression.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("$(prop.IndexOfAny('y'))", "x-y-z", "2")]
    [InlineData("$(prop.LastIndexOf('y'))", "x-x-y-y-y-z", "8")]
    [InlineData("$(prop.LastIndexOf('y', 7))", "x-x-y-y-y-z", "6")]
    [InlineData("$(prop.LastIndexOfAny('xy'))", "x-x-y-y-y-z", "8")]
    public void PropertyFunctionStringIndexMethods(string expression, string propertyValue, string expected)
        => TestPropertyFunction(expression, "prop", propertyValue, expected);

    [Fact]
    public void PropertyFunctionStringCopy()
    {
        string propertyFunction = """
            $([System.String]::Copy($(X)).LastIndexOf(
                '.designer.cs',
                System.StringComparison.OrdinalIgnoreCase))
            """;

        TestPropertyFunction(propertyFunction, "X", "test.designer.cs", "4");
    }

    [Fact]
    public void PropertyFunctionVersionParse()
        => TestPropertyFunction(@"$([System.Version]::Parse('$(X)').ToString(1))", "X", "4.0", "4");

    [Fact]
    public void PropertyFunctionGuidNewGuid()
        => Guid.TryParse(ExpandProperties("$([System.Guid]::NewGuid())"), out _)
            .ShouldBeTrue();

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

    public static TheoryData<string, string> PropertyFunctionIntrinsicFunctionDirectoryData { get; } = new()
    {
        {
            "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetCurrentToolsDirectory())",
            EscapingUtilities.Escape(IntrinsicFunctions.GetCurrentToolsDirectory()) ?? string.Empty
        },
        {
            "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetToolsDirectory32())",
            EscapingUtilities.Escape(IntrinsicFunctions.GetToolsDirectory32()) ?? string.Empty
        },
        {
            "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetToolsDirectory64())",
            EscapingUtilities.Escape(IntrinsicFunctions.GetToolsDirectory64()) ?? string.Empty
        },
        {
            "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetMSBuildSDKsPath())",
            EscapingUtilities.Escape(IntrinsicFunctions.GetMSBuildSDKsPath()) ?? string.Empty
        },
        {
            "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetVsInstallRoot())",
            EscapingUtilities.Escape(IntrinsicFunctions.GetVsInstallRoot()) ?? string.Empty
        },
        {
            "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetMSBuildExtensionsPath())",
            EscapingUtilities.Escape(IntrinsicFunctions.GetMSBuildExtensionsPath()) ?? string.Empty
        },
        {
            "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetProgramFiles32())",
            EscapingUtilities.Escape(IntrinsicFunctions.GetProgramFiles32()) ?? string.Empty
        },
    };

    /// <summary>
    ///  Exercises intrinsic directory functions.
    /// </summary>
    /// <param name="expression">The intrinsic property-function expression.</param>
    /// <param name="expected">The expected escaped value.</param>
    [Theory]
    // Expected paths depend on the execution-process environment, so enumerate the data when the test runs.
    [MemberData(nameof(PropertyFunctionIntrinsicFunctionDirectoryData), DisableDiscoveryEnumeration = true)]
    public void PropertyFunctionIntrinsicFunctionDirectory(string expression, string expected)
        => TestPropertyFunction(expression, "X", "_", expected);

    [Fact]
    public void PropertyFunctionStringArrayIndexerGetter()
        => TestPropertyFunction("$(prop.Split('-')[0])", "prop", "x-y-z", "x");

    /// <summary>
    ///  Exercises string substring overloads.
    /// </summary>
    /// <param name="expression">The property-function expression.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("$(prop.Substring(2))", "cdef")]
    [InlineData("$(prop.Substring(2, 3))", "cde")]
    public void PropertyFunctionSubstring(string expression, string expected)
        => TestPropertyFunction(expression, "prop", "abcdef", expected);

    [Fact]
    public void PropertyFunctionStringGetChars()
        => TestPropertyFunction("$(prop[0])", "prop", "461", "4");

    [Fact]
    public void PropertyFunctionStringGetCharsError()
        => Should.Throw<InvalidProjectFileException>(() =>
            TestPropertyFunction("$(prop[5])", "prop", "461", "4"));

    /// <summary>
    ///  Exercises string padding methods.
    /// </summary>
    /// <param name="expression">The property-function expression.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("$(prop.PadLeft(2))", "prop", "x", " x")]
    [InlineData("$(prop.PadLeft(2, '0'))", "prop", "x", "0x")]
    [InlineData("$(prop.PadLeft($([MSBuild]::Multiply(1, 2)), '0'))", "prop", "x", "0x")]
    [InlineData("$(VersionSuffixBuildOfTheDay.PadLeft(3, $([System.Convert]::ToChar(`0`))))", "VersionSuffixBuildOfTheDay", "4", "004")]
    [InlineData("$(prop.PadRight(2))", "prop", "x", "x ")]
    [InlineData("$(prop.PadRight(2, '0'))", "prop", "x", "x0")]
    public void PropertyFunctionStringPadding(string expression, string propertyName, string propertyValue, string expected)
        => TestPropertyFunction(expression, propertyName, propertyValue, expected);

    [Fact]
    public void PropertyFunctionStringTrimEndCharArray()
        => TestPropertyFunction("$(prop.TrimEnd('.0123456789'))", "prop", "net461", "net");

    /// <summary>
    ///  Exercises quoted and unquoted <c>TrimStart</c> arguments.
    /// </summary>
    /// <param name="expression">The property-function expression.</param>
    [Theory]
    [InlineData("$(X.TrimStart('vV'))")]
    [InlineData("$(X.TrimStart(vV))")]
    public void PropertyFunctionStringTrimStart(string expression)
        => TestPropertyFunction(expression, "X", "v40", "40");

    [Fact]
    public void PropertyFunctionStringTrimEnd1()
        => TestPropertyFunction("$(prop.TrimEnd('a'))", "prop", "netaa", "net");

    // https://github.com/dotnet/msbuild/issues/2882
    [Fact]
    public void PropertyFunctionMathMaxOverflow()
        => TestPropertyFunction("$([System.Math]::Max($(X), 0))", "X", "-2010", "0");

    [Fact]
    public void PropertyFunctionStringTrimEnd2()
        => Should.Throw<InvalidProjectFileException>(() =>
            TestPropertyFunction("$(prop.TrimEnd('a', 'b'))", "prop", "stringab", "string"));

    [Fact]
    public void PropertyFunctionMathMin()
        => TestPropertyFunction("$([System.Math]::Min($(X), 20))", "X", "30", "20");

    [Fact]
    public void PropertyFunctionMSBuildAddIntegerLiteral()
        => TestPropertyFunction("$([MSBuild]::Add($(X), 5))", "X", "7", "12");

    [Fact]
    public void PropertyFunctionMSBuildAddRealLiteral()
        => TestPropertyFunction("$([MSBuild]::Add($(X), 0.5))", "X", "7", "7.5");

    [Fact]
    public void PropertyFunctionMSBuildAddIntegerOverflow()
    {
        // Overflow wrapping - result exceeds size of long
        string expected = "-9223372036854775808";
        TestPropertyFunction("$([MSBuild]::Add($(X), 1))", "X", long.MaxValue.ToString(), expected);
    }

    [Fact]
    [UseInvariantCulture]
    public void PropertyFunctionMSBuildAddRealArgument()
    {
        // string argument is an integer that exceeds the size of long.
        double value = long.MaxValue + 1.0;
        double expected = value + 1.0;
        TestPropertyFunction("$([MSBuild]::Add($(X), 1))", "X", value.ToString(), expected.ToString());
    }

    [Fact]
    public void PropertyFunctionMSBuildAddComplex()
        => TestPropertyFunction("$([MSBuild]::Add($(X), $([MSBuild]::Add(2, 3))))", "X", "7", "12");

    [Fact]
    public void PropertyFunctionMSBuildSubtractIntegerLiteral()
        => TestPropertyFunction("$([MSBuild]::Subtract($(X), 20100000))", "X", "20100042", "42");

    [Fact]
    public void PropertyFunctionMSBuildSubtractRealLiteral()
        => TestPropertyFunction("$([MSBuild]::Subtract($(X), 20100000.0))", "X", "20100042", "42");

    [Fact]
    public void PropertyFunctionMSBuildSubtractIntegerMaxValue()
    {
        // If the double overload is used, there will be a rounding error.
        string expected = "1";
        TestPropertyFunction("$([MSBuild]::Subtract($(X), 9223372036854775806))", "X", long.MaxValue.ToString(), expected);
    }

    [Fact]
    public void PropertyFunctionMSBuildMultiplyIntegerLiteral()
        => TestPropertyFunction("$([MSBuild]::Multiply($(X), 8800))", "X", "2", "17600");

    [Fact]
    public void PropertyFunctionMSBuildMultiplyRealLiteral()
        => TestPropertyFunction("$([MSBuild]::Multiply($(X), 1.5))", "X", "2", "3");

    [Fact]
    public void PropertyFunctionMSBuildMultiplyIntegerOverflow()
    {
        // Overflow - result exceeds size of long
        string expected = "-2";
        TestPropertyFunction("$([MSBuild]::Multiply($(X), 2))", "X", long.MaxValue.ToString(), expected);
    }

    [Fact]
    public void PropertyFunctionMSBuildMultiplyComplex()
        => TestPropertyFunction("$([MSBuild]::Multiply($(X), $([MSBuild]::Multiply(1, 8800))))", "X", "2", "17600");

    [Fact]
    public void PropertyFunctionMSBuildDivideIntegerLiteral()
    {
        string expected = "6";
        TestPropertyFunction("$([MSBuild]::Divide($(X), 10000))", "X", "65536", expected);
    }

    [Fact]
    public void PropertyFunctionMSBuildDivideRealLiteral()
        => TestPropertyFunction("$([MSBuild]::Divide($(X), 10000.0))", "X", "65536", "6.5536");

    [Fact]
    public void PropertyFunctionMSBuildModuloIntegerLiteral()
        => TestPropertyFunction("$([MSBuild]::Modulo($(X), 3))", "X", "10", "1");

    [Fact]
    public void PropertyFunctionMSBuildModuloRealLiteral()
        => TestPropertyFunction("$([MSBuild]::Modulo($(X), 3.0))", "X", "10", "1");

    [Fact]
    public void PropertyFunctionConvertToString()
        => TestPropertyFunction("$([System.Convert]::ToString(`.`))", "_", "_", ".");

    [Fact]
    public void PropertyFunctionConvertToInt32()
        => TestPropertyFunction("$([System.Convert]::ToInt32(42))", "_", "_", "42");

    [Fact]
    public void PropertyFunctionToCharArray()
        => TestPropertyFunction("$([System.Convert]::ToString(`.`).ToCharArray())", "_", "_", ".");

    [Fact]
    public void PropertyFunctionStringArrayGetValue()
        => TestPropertyFunction("$(X.Split($([System.Convert]::ToString(`.`).ToCharArray())).GetValue($([System.Convert]::ToInt32(0))))", "X", "ab.cd", "ab");

    /// <summary>
    ///  Test that Char.IsDigit fast-path works correctly.
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
        => TestPropertyFunction(expression, "dummy", "", expected);

    [Fact]
    public void PropertyFunction_ReplaceDoesNotCallRegexReplace()
    {
        // Regression test for https://github.com/dotnet/msbuild/issues/12923
        PropertyDictionary<ProjectProperty> properties = [];
        var expander = new Expander<ProjectProperty, ProjectItem>(properties, FileSystems.Default);

        _ = Should.Throw<InvalidProjectFileException>(() =>
        {
            string result = expander.ExpandIntoStringLeaveEscaped(
                "$([System.TimeSpan]::Replace('abc_123_ghi', '\\d+', 'def'))",
                ExpanderOptions.ExpandProperties,
                MockElementLocation.Instance);

            result.ShouldNotBe("abc_def_ghi");
        });
    }

    private void TestPropertyFunction(string expression, string propertyName, string propertyValue, string expected)
        => ExpandProperties(expression, Data.Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Theory]
    [InlineData("net6.0", "netstandard2.0", "")]
    [InlineData("net6.0-windows", "netstandard2.0", "")]
    [InlineData("net6.0-windows", "net6.0", "net6.0-windows")]
    [InlineData("netstandard2.0;net6.0", "net6.0", "net6.0")]
    [InlineData("netstandard2.0;net6.0-windows", "net6.0", "net6.0-windows")]
    [InlineData("netstandard2.0;net6.0-windows", "net6.0;netstandard2.0;net472", "netstandard2.0%3bnet6.0-windows")]
    [InlineData("netstandard2.0;net472", "net6.0;netstandard2.0;net472", "netstandard2.0%3bnet472")]
    public void PropertyFunctionFilterTargetFrameworks(string incoming, string filter, string expected)
        => TestPropertyFunction($"$([MSBuild]::FilterTargetFrameworks('{incoming}', '{filter}'))", "_", "_", expected);

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
        using var env = TestEnvironment.Create();
        var root = env.CreateFolder();
        var alpha = root.CreateDirectory("alpha");

        const string Contents = """
            <Project>
              <ItemGroup>
                <Compile Include="One.cs" />
                <Compile Include="beta\Two.cs" />
                <Compile Include="beta\Three.cs" />
              </ItemGroup>
              <ItemGroup>
                <MyDirectories Include="@(Compile->GetPathsOfAllDirectoriesAbove())" />
              </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(alpha, ".proj", Contents);

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
        using var env = TestEnvironment.Create();
        var root = env.CreateFolder();
        var alpha = root.CreateDirectory("alpha");

        const string Contents = """
            <Project>
                <ItemGroup>
                <Compile Include="One.cs" />
                <Compile Include="beta\Two.cs" />
                <Compile Include="..\gamma\Three.cs" />
                </ItemGroup>
                <ItemGroup>
                <MyDirectories Include="@(Compile->GetPathsOfAllDirectoriesAbove())" />
                </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(alpha, ".proj", Contents);

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
        using var env = TestEnvironment.Create();
        var root = env.CreateFolder();

        const string Contents = """
            <Project>
              <ItemGroup>
                <MyDirectory Include="Alpha;Beta;Alpha\Gamma" />
              </ItemGroup>
              <ItemGroup>
                <Squiggle Include="@(MyDirectory->Combine('.squiggle'))" />
              </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", Contents);

        ProjectInstance projectInstance = new(projectFile.Path);
        ICollection<ProjectItemInstance> squiggles = projectInstance.GetItems("Squiggle");

        string expectedAlphaSquigglePath = Path.Combine("Alpha", ".squiggle");
        string expectedBetaSquigglePath = Path.Combine("Beta", ".squiggle");
        string expectedAlphaGammaSquigglePath = Path.Combine("Alpha", "Gamma", ".squiggle");

        squiggles
            .Select(i => i.EvaluatedInclude)
            .ShouldBe([expectedAlphaSquigglePath, expectedBetaSquigglePath, expectedAlphaGammaSquigglePath], Case.Insensitive);
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
        using var env = TestEnvironment.Create();
        var root = env.CreateFolder();

        const string Contents = """
            <Project>
              <ItemGroup>
                <PotentialCompile Include="alpha\One.cs" />
                <PotentialCompile Include="alpha\Two.cs" />
                <PotentialCompile Include="alpha\Three.cs" />
                <PotentialCompile Include="alpha\Four.cs" />
              </ItemGroup>
              <ItemGroup>
                <Compile Include="@(PotentialCompile->Exists())" />
              </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", Contents);

        var alpha = root.CreateDirectory("alpha");
        var one = alpha.CreateFile("One.cs");
        var three = alpha.CreateFile("Three.cs");

        ProjectInstance projectInstance = new(projectFile.Path);
        ICollection<ProjectItemInstance> compileItems = projectInstance.GetItems("Compile");

        var alphaOnePath = Path.Combine("alpha", "One.cs");
        var alphaThreePath = Path.Combine("alpha", "Three.cs");
        compileItems
            .Select(i => i.EvaluatedInclude)
            .ShouldBe([alphaOnePath, alphaThreePath], Case.Insensitive);
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
        using var env = TestEnvironment.Create();
        var root = env.CreateFolder();

        const string Contents = """
            <Project>
              <ItemGroup>
                <PotentialDirectory Include="alpha\beta" />
                <PotentialDirectory Include="alpha\gamma" />
                <PotentialDirectory Include="alpha\delta" />
                <PotentialDirectory Include="alpha\epsilon" />
              </ItemGroup>
              <ItemGroup>
                <MyDirectory Include="@(PotentialDirectory->Exists())" />
              </ItemGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", Contents);

        var alpha = root.CreateDirectory("alpha");
        var beta = alpha.CreateDirectory("beta");
        var delta = alpha.CreateDirectory("delta");

        ProjectInstance projectInstance = new ProjectInstance(projectFile.Path);
        ICollection<ProjectItemInstance> directories = projectInstance.GetItems("MyDirectory");

        var alphaBetaPath = Path.Combine("alpha", "beta");
        var alphaDeltaPath = Path.Combine("alpha", "delta");
        directories.Select(i => i.EvaluatedInclude).ShouldBe(new[] { alphaBetaPath, alphaDeltaPath }, Case.Insensitive);
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
            using var env = TestEnvironment.Create();
            currentThread.CurrentCulture = svSECultureInfo;
            currentThread.CurrentUICulture = svSECultureInfo;
            var root = env.CreateFolder();

            const string Contents = """
                <Project>
                  <PropertyGroup>
                    <_value>$([MSBuild]::Subtract(0, 1))</_value>
                    <_otherValue Condition="'$(_value)' &gt;= -1">test-value</_otherValue>
                  </PropertyGroup>
                  <Target Name="Build" />
                </Project>
                """;

            var projectFile = env.CreateFile(root, ".proj", Contents);
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

            using var env = TestEnvironment.Create();
            var root = env.CreateFolder();

            string contents = $"""
                <Project>
                    <PropertyGroup>
                        <foo>aa</foo>
                        <typeval>$(foo.{methodName}().FullName)</typeval>
                    </PropertyGroup>
                </Project>
                """;

            var projectFile = env.CreateFile(root, ".proj", contents);

            var exception = Should.Throw<InvalidProjectFileException>(() =>
                new ProjectInstance(projectFile.Path));

            exception.BaseMessage.ShouldContain(
                $"""The function "{methodName}" on type "System.String" is not available for execution as an MSBuild property function.""");
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
        using var env = TestEnvironment.Create();

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

        string contents = $"""
            <Project>
                <PropertyGroup>
                    <foo>aa</foo>
                    <typeval>$(foo.{methodName}().FullName)</typeval>
                </PropertyGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", contents);

        _ = Should.NotThrow(() =>
            new ProjectInstance(projectFile.Path));
    }

    [Theory]
    [InlineData("$([System.Version]::Parse('17.12.11.10').ToString(2))")]
    [InlineData("$([System.Text.RegularExpressions.Regex]::Replace('abc123def', 'abc', ''))")]
    [InlineData("$([System.String]::new('Hi').Equals('Hello'))")]
    [InlineData("$([System.IO.Path]::GetFileNameWithoutExtension('C:\\folder\\file.txt'))")]
    [InlineData("$([System.Int32]::new(123).ToString('mm')")]
    [InlineData("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::NormalizeDirectory('C:/folder1/./folder2/'))")]
    [InlineData("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::IsOSPlatform('Windows'))")]
    public void FastPathValidationTest(string methodInvocationMetadata)
    {
        using var env = TestEnvironment.Create();

        // Setting this env variable allows to track if expander was using reflection for a function invocation.
        env.SetEnvironmentVariable("MSBuildLogPropertyFunctionsRequiringReflection", "1");

        var logger = new MockLogger();
        ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.RegisterLogger(logger);
        var loggingContext = new MockLoggingContext(
            loggingService,
            new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));

        _ = ExpandProperties(methodInvocationMetadata, loggingContext);

        string reflectionInfoPath = Path.Combine(Directory.GetCurrentDirectory(), "PropertyFunctionsRequiringReflection");

        // the fast path was successfully resolved without reflection.
        File.Exists(reflectionInfoPath).ShouldBeFalse();
    }

    [Fact]
    public void PropertyFunctionRegisterBuildCheck()
    {
        using var env = TestEnvironment.Create();
        var logger = new MockLogger();
        ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.RegisterLogger(logger);
        var loggingContext = new MockLoggingContext(
            loggingService,
            new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));
        var dummyAssemblyFile = env.CreateFile(env.CreateFolder(), "test.dll");

        string result = ExpandProperties(
            $"$([MSBuild]::RegisterBuildCheck({dummyAssemblyFile.Path}))",
            loggingContext);

        result.ShouldBe(bool.TrueString);
        logger.AllBuildEvents.ShouldAllBe(be => be is BuildCheckAcquisitionEventArgs);
        logger.AllBuildEvents.Count.ShouldBe(1);
    }

    /// <summary>
    ///  Test for issue where chained item functions with empty results incorrectly evaluate as non-empty in conditions.
    /// </summary>
    [Fact]
    public void ChainedItemFunctionEmptyResultInCondition()
    {
        const string Content = """
            <Project>
              <Target Name='Test'>
                <ItemGroup>
                  <TestItem Include='Test1' Foo='Bar' />
                  <TestItem Include='Test2' />
                </ItemGroup>

                <!-- This should be empty because Test1 has Foo='Bar', not 'Baz' -->
                <PropertyGroup Condition="'@(TestItem->WithMetadataValue('Identity', 'Test1')->WithMetadataValue('Foo', 'Baz'))' == ''">
                  <EmptyResult>TRUE</EmptyResult>
                </PropertyGroup>

                <Message Text='EmptyResult=$(EmptyResult)' Importance='high' />
              </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        // The chained WithMetadataValue should return empty, so the condition should be true and EmptyResult should be set
        log.AssertLogContains("EmptyResult=TRUE");
    }

    #region System.IO.File/Directory relative path resolution in -mt mode

    /// <summary>
    ///  TransientTestState that saves/restores <see cref="FileUtilities.CurrentThreadWorkingDirectory"/>.
    /// </summary>
    private sealed class TransientThreadWorkingDirectory : TransientTestState
    {
        private readonly string? _originalValue;

        public TransientThreadWorkingDirectory(string? newWorkingDirectory)
        {
            _originalValue = FileUtilities.CurrentThreadWorkingDirectory;
            FileUtilities.CurrentThreadWorkingDirectory = newWorkingDirectory;
        }

        public override void Revert()
            => FileUtilities.CurrentThreadWorkingDirectory = _originalValue;
    }

    /// <summary>
    /// TransientTestState that flips the EnableAllPropertyFunctions AppContext switch on and restores
    /// its original value on revert (deterministic; does not stick across tests).
    /// </summary>
    private sealed class TransientEnableAllPropertyFunctions : TransientTestState
    {
        private readonly bool _original;

        public TransientEnableAllPropertyFunctions()
        {
            AppContext.TryGetSwitch("Microsoft.Build.EnableAllPropertyFunctions", out _original);
            AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", true);
        }

        public override void Revert()
            => AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", _original);
    }

    /// <summary>
    /// Returns an AppContext switch to the "unset" state so that the FeatureSwitches check falls
    /// back to the environment variable. AppContext can only set a switch true or false (never
    /// unset), so the entry is removed reflectively from the runtime's private switch table. The
    /// backing field differs by runtime (.NET Core uses `s_switches`, .NET Framework uses
    /// `s_switchMap`), so this scans the non-public static dictionaries and clears the key from
    /// whichever one holds it rather than hard-coding a field name.
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
    /// Helper: expand a property function expression with CurrentThreadWorkingDirectory set,
    /// simulating -mt mode where Environment.CurrentDirectory may point elsewhere.
    /// </summary>
    private string ExpandWithThreadWorkingDirectory(TestEnvironment env, string expression, string? workingDir, string? wrongDir = null)
    {
        env.WithTransientTestState(new TransientThreadWorkingDirectory(workingDir));
        if (wrongDir != null)
        {
            env.SetCurrentDirectory(wrongDir);
        }

        return ExpandProperties(expression);
    }

    /// <summary>
    /// Helper: set the process current directory and return it as the OS reports it. On macOS the test
    /// temp folder is reached through a symlink (/var -> /private/var), and only the reported form matches
    /// what resolution against the process current directory produces, so tests that compare -mt output
    /// against non-mt output must build both sides from this rather than from the TestEnvironment path.
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
        string drive = Path.GetPathRoot(correctDir.Path)!.Substring(0, 2);

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
            null);

        result.ShouldBe(Path.Combine(processDirPath, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_EmptyPath_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() =>
            IntrinsicFunctions.NormalizePath([]));

    [Fact]
    public void NormalizePath_NullPathArray_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() =>
            IntrinsicFunctions.NormalizePath((string[])null!));

    [Fact]
    public void NormalizePath_IllegalPath_ThrowsArgumentException()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        env.WithTransientTestState(new TransientThreadWorkingDirectory(correctDir.Path));

        // Resolution against the thread working directory intentionally swallows the invalid-path
        // exception (so that non-throwing intrinsics such as FileExists keep working); NormalizePath
        // is still expected to surface it, matching non-mt behavior.
        _ = Should.Throw<ArgumentException>(() =>
             IntrinsicFunctions.NormalizePath("bad\0path"));
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

        var logger = new MockLogger();
        ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.RegisterLogger(logger);
        var loggingContext = new MockLoggingContext(
            loggingService,
            new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));

        env.WithTransientTestState(new TransientThreadWorkingDirectory(correctDir.Path));
        env.SetCurrentDirectory(wrongDir.Path);

        string result = ExpandProperties(
            "$([MSBuild]::RegisterBuildCheck('check.dll'))",
            loggingContext);

        result.ShouldBe(bool.TrueString);
        logger.AllBuildEvents.ShouldHaveSingleItem(buildEvent =>
        {
            BuildCheckAcquisitionEventArgs acquisition = buildEvent.ShouldBeOfType<BuildCheckAcquisitionEventArgs>();
            acquisition.AcquisitionPath.ShouldBe(Path.Combine(correctDir.Path, "check.dll"));
        });
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

        // Note: Do NOT create the file in wrongDir
        File.WriteAllText(Path.Combine(correctDir.Path, "exists.txt"), "data");

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

        // Note: Do NOT create subdir in wrongDir
        Directory.CreateDirectory(Path.Combine(correctDir.Path, "subdir"));

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

        _ = ExpandWithThreadWorkingDirectory(
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

        _ = ExpandWithThreadWorkingDirectory(
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

        _ = ExpandWithThreadWorkingDirectory(
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

        _ = ExpandWithThreadWorkingDirectory(
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

        _ = ExpandWithThreadWorkingDirectory(
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
            null);

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
            null);

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
            null);

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
