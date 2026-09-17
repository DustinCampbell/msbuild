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
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ProjectHelpers = Microsoft.Build.UnitTests.BackEnd.ProjectHelpers;
using ProjectItemInstanceFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.ProjectItemInstanceFactory;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation;

[Trait("Category", "expansion")]
public class Expander_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private string _dateToParse = new DateTime(2010, 12, 25).ToString(CultureInfo.CurrentCulture);
    private static readonly string s_rootPathPrefix = NativeMethodsShared.IsWindows ? "C:\\" : Path.VolumeSeparatorChar.ToString();

    [Fact]
    public void ExpandAllIntoTaskItems0()
    {
        PropertyDictionary<ProjectPropertyInstance> pg = new();
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg);

        IList<TaskItem> itemsOut = expander.ExpandIntoTaskItemsLeaveEscaped("", ExpanderOptions.ExpandProperties, null);

        ObjectModelHelpers.AssertItemsMatch("", GetTaskArrayFromItemList(itemsOut));
    }

    [Fact]
    public void ExpandAllIntoTaskItems1()
    {
        PropertyDictionary<ProjectPropertyInstance> pg = new();
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg);

        IList<TaskItem> itemsOut = expander.ExpandIntoTaskItemsLeaveEscaped("foo", ExpanderOptions.ExpandProperties, MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch(@"foo", GetTaskArrayFromItemList(itemsOut));
    }

    [Fact]
    public void ExpandAllIntoTaskItems2()
    {
        PropertyDictionary<ProjectPropertyInstance> pg = new();
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg);

        IList<TaskItem> itemsOut = expander.ExpandIntoTaskItemsLeaveEscaped("foo;bar", ExpanderOptions.ExpandProperties, MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch("""
            foo
            bar
            """, GetTaskArrayFromItemList(itemsOut));
    }

    [Fact]
    public void ExpandAllIntoTaskItems3()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        PropertyDictionary<ProjectPropertyInstance> pg = new();

        List<ProjectItemInstance> ig =
        [
            new ProjectItemInstance(project, "Compile", "foo.cs", project.FullPath),
            new ProjectItemInstance(project, "Compile", "bar.cs", project.FullPath),
        ];

        List<ProjectItemInstance> ig2 = [new ProjectItemInstance(project, "Resource", "bing.resx", project.FullPath)];

        ItemDictionary<ProjectItemInstance> itemsByType = [];
        itemsByType.ImportItems(ig);
        itemsByType.ImportItems(ig2);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(
            pg,
            itemsByType,
            new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)));

        IList<TaskItem> itemsOut = expander.ExpandIntoTaskItemsLeaveEscaped("foo;bar;@(compile);@(resource)", ExpanderOptions.ExpandPropertiesAndItems, MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch("""
            foo
            bar
            foo.cs
            bar.cs
            bing.resx
            """,
            GetTaskArrayFromItemList(itemsOut));
    }

    [Fact]
    public void ExpandAllIntoTaskItems4()
    {
        PropertyDictionary<ProjectPropertyInstance> pg = new();
        pg.Set(ProjectPropertyInstance.Create("a", "aaa"));
        pg.Set(ProjectPropertyInstance.Create("b", "bbb"));
        pg.Set(ProjectPropertyInstance.Create("c", "cc;dd"));

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg);

        IList<TaskItem> itemsOut = expander.ExpandIntoTaskItemsLeaveEscaped("foo$(a);$(b);$(c)", ExpanderOptions.ExpandProperties, MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch("""
            fooaaa
            bbb
            cc
            dd
            """,
            GetTaskArrayFromItemList(itemsOut));
    }

    /// <summary>
    ///  Expand property expressions into ProjectPropertyInstance items.
    /// </summary>
    [Fact]
    public void ExpandPropertiesIntoProjectPropertyInstances()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        PropertyDictionary<ProjectPropertyInstance> pg = new();
        pg.Set(ProjectPropertyInstance.Create("a", "aaa"));
        pg.Set(ProjectPropertyInstance.Create("b", "bbb"));
        pg.Set(ProjectPropertyInstance.Create("c", "cc;dd"));

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg);

        ProjectItemInstanceFactory itemFactory = new(project, "i");
        IList<ProjectItemInstance> itemsOut = expander.ExpandIntoItemsLeaveEscaped("foo$(a);$(b);$(c);$(d", itemFactory, ExpanderOptions.ExpandProperties, MockElementLocation.Instance);

        itemsOut.Count.ShouldBe(5);
    }

    /// <summary>
    ///  Expand an item vector into items of the specified type.
    /// </summary>
    [Fact]
    public void ExpandItemVectorsIntoProjectItemInstancesSpecifyingItemType()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateExpander();

        ProjectItemInstanceFactory itemFactory = new(project, "j");

        IList<ProjectItemInstance> items = expander.ExpandIntoItemsLeaveEscaped("@(i)", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        items.Count.ShouldBe(2);
        items[0].ItemType.ShouldBe("j");
        items[1].ItemType.ShouldBe("j");
        items[0].EvaluatedInclude.ShouldBe("i0");
        items[1].EvaluatedInclude.ShouldBe("i1");
    }

    /// <summary>
    ///  Expand an item vector into items of the type of the item vector.
    /// </summary>
    [Fact]
    public void ExpandItemVectorsIntoProjectItemInstancesWithoutSpecifyingItemType()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateExpander();

        ProjectItemInstanceFactory itemFactory = new(project);

        IList<ProjectItemInstance> items = expander.ExpandIntoItemsLeaveEscaped("@(i)", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        items.Count.ShouldBe(2);
        items[0].ItemType.ShouldBe("i");
        items[1].ItemType.ShouldBe("i");
        items[0].EvaluatedInclude.ShouldBe("i0");
        items[1].EvaluatedInclude.ShouldBe("i1");
    }

    /// <summary>
    ///  Expand an item vector function AnyHaveMetadataValue.
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsAnyHaveMetadataValue()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander();

        ProjectItemInstanceFactory itemFactory = new(project, "i");

        IList<ProjectItemInstance> itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->AnyHaveMetadataValue('Even', 'true'))", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.ShouldHaveSingleItem();
        itemsTrue[0].ItemType.ShouldBe("i");
        itemsTrue[0].EvaluatedInclude.ShouldBe("true");

        IList<ProjectItemInstance> itemsFalse = expander.ExpandIntoItemsLeaveEscaped("@(i->AnyHaveMetadataValue('Even', 'goop'))", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsFalse.ShouldHaveSingleItem();
        itemsFalse[0].ItemType.ShouldBe("i");
        itemsFalse[0].EvaluatedInclude.ShouldBe("false");
    }

    [Fact]
    public void ExpandEmptyItemVectorFunctionWithAnyHaveMetadataValue()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateItemFunctionExpander();
        ProjectItemInstanceFactory itemFactory = new(project, "i");

        IList<ProjectItemInstance> itemsEmpty = expander.ExpandIntoItemsLeaveEscaped("@(unsetItem->AnyHaveMetadataValue('Metadatum', 'value'))", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);
        ProjectItemInstance pii = itemsEmpty.ShouldHaveSingleItem();
        pii.EvaluatedInclude.ShouldBe("false");
    }

    [Theory]
    [InlineData("@(unsetItem)", false)]
    [InlineData("@(unsetItem->Distinct())", true)]
    public void EmptyItemVectorReportsWhetherExpressionIsTransform(string expression, bool expected)
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateItemFunctionExpander();
        ProjectItemInstanceFactory itemFactory = new(project, "i");

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
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsGetDirectoryNameOfMetadataValueDistinct()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander();

        ProjectItemInstanceFactory itemFactory = new(project, "i");

        IList<ProjectItemInstance> itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->Metadata('Meta0')->DirectoryName()->Distinct())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.ShouldHaveSingleItem();
        itemsTrue[0].ItemType.ShouldBe("i");
        itemsTrue[0].EvaluatedInclude.ShouldBe(Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory"));

        IList<ProjectItemInstance> itemsDir = expander.ExpandIntoItemsLeaveEscaped("@(i->Metadata('Meta9')->DirectoryName()->Distinct())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsDir.ShouldHaveSingleItem();
        itemsDir[0].ItemType.ShouldBe("i");
        itemsDir[0].EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), @"seconddirectory"));
    }

    /// <summary>
    /// /// Expand an item vector function that is an itemspec modifier
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsItemSpecModifier()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander();

        ProjectItemInstanceFactory itemFactory = new(project, "i");

        IList<ProjectItemInstance> itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->Metadata('Meta0')->Directory())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.Count.ShouldBe(10);
        itemsTrue[5].ItemType.ShouldBe("i");
        itemsTrue[5].EvaluatedInclude.ShouldBe(Path.Combine("firstdirectory", "seconddirectory") + Path.DirectorySeparatorChar);

        itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->Metadata('Meta0')->Filename())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.Count.ShouldBe(10);
        itemsTrue[5].ItemType.ShouldBe("i");
        itemsTrue[5].EvaluatedInclude.ShouldBe("file0");

        itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->Metadata('Meta0')->Extension())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.Count.ShouldBe(10);
        itemsTrue[5].ItemType.ShouldBe("i");
        itemsTrue[5].EvaluatedInclude.ShouldBe(".ext");
    }

    /// <summary>
    /// Expand an item expression (that isn't a real expression) but includes a property reference nested within a metadata reference
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsInvalid1()
    {
        var expander = CreateItemFunctionExpander();

        string result = expander.ExpandIntoStringLeaveEscaped("[@(type-&gt;'%($(a)), '%'')]", ExpanderOptions.ExpandAll, MockElementLocation.Instance);

        result.ShouldBe(@"[@(type-&gt;'%(filename), '%'')]");
    }

    /// <summary>
    /// Expand an item expression (that isn't a real expression) but includes a metadata reference that till needs to be expanded
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsInvalid2()
    {
        var expander = CreateItemFunctionExpander();

        string result = expander.ExpandIntoStringLeaveEscaped("[@(i->'%(Meta9))']", ExpanderOptions.ExpandAll, MockElementLocation.Instance);

        result.ShouldBe(@"[@(i->')']");
    }

    /// <summary>
    /// Expand an item vector function that is chained into a string
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsChained1()
    {
        var expander = CreateItemFunctionExpander();

        string result = expander.ExpandIntoStringLeaveEscaped("@(i->'%(Meta0)'->'%(Directory)'->Distinct())", ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        result.ShouldBe(Path.Combine("firstdirectory", "seconddirectory") + Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Expand an item vector function that is chained and has constants into a string
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsChained2()
    {
        var expander = CreateItemFunctionExpander();

        string result = expander.ExpandIntoStringLeaveEscaped("[@(i->'%(Meta0)'->'%(Directory)'->Distinct())]", ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        result.ShouldBe(@"[firstdirectory\seconddirectory\]");
    }

    /// <summary>
    /// Expand an item vector function that is chained and has constants into a string
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsChained3()
    {
        var expander = CreateItemFunctionExpander();

        string result = expander.ExpandIntoStringLeaveEscaped("@(i->'%(MetaBlank)'->'%(Directory)'->Distinct())", ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsChainedProject1()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
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
            """);

        logger.AssertLogContains(@"DirChain0: Value1\;Value2\");
        logger.AssertLogContains(@"DirChain1: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||Value2\");
        logger.AssertLogContains(@"DirChain2: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||##Value2\");
        logger.AssertLogContains(@"DirChain3: Value1\||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##Value2\");
        logger.AssertLogContains(@"DirChain4: Value1\$||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##Value2\");
        logger.AssertLogContains(@"DirChain5: Value1\$||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||$##$Value2\");
    }

    /// <summary>
    /// Test that chained item functions work with whitespace before the second arrow operator
    /// </summary>
    [Fact]
    public void ItemFunctionChainingWithWhitespaceBeforeArrow()
    {
        string content = @"
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
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        // Both should produce the same result: B and C
        log.AssertLogContains("Test1: [B;C]");
        log.AssertLogContains("Test2: [B;C]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsCount1()
    {
        string content = @"
 <Project DefaultTargets=`t`>

        <Target Name=`t`>
            <ItemGroup>
                <I Include=`foo;bar`/>
                <J Include=`;`/>
            </ItemGroup>

            <Message Text=`[@(I->Count())][@(J->Count())]` />
        </Target>
</Project>
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        log.AssertLogContains("[2][0]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsCount2()
    {
        string content = @"
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
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        log.AssertLogContains("2;0");
    }

    [Fact]
    public void ExpandItemVectorFunctionsCountOperatingOnEmptyResult1()
    {
        string content = @"
 <Project DefaultTargets=`t`>

        <Target Name=`t`>
            <ItemGroup>
                <I Include=`foo;bar`/>
                <J Include=`;`/>
            </ItemGroup>

            <Message Text=`[@(I->Metadata('foo')->Count())][@(J->Metadata('foo')->Count())]` />
        </Target>
</Project>
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        log.AssertLogContains("[0][0]");
    }

    [Fact]
    public void ExpandItemVectorFunctionsCountOperatingOnEmptyResult2()
    {
        string content = @"
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
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        log.AssertLogContains("0;0");
    }

    [Fact]
    public void ExpandItemVectorFunctionsBuiltIn1()
    {
        string content = @"
 <Project DefaultTargets=`t`>

        <Target Name=`t`>
            <ItemGroup>
                <I Include=`foo;bar`/>
            </ItemGroup>

            <Message Text=`[@(I->FullPath())]` />
        </Target>
</Project>
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        var current = Directory.GetCurrentDirectory();
        log.AssertLogContains(String.Format(@"[{0}foo;{0}bar]", current + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ExpandItemVectorFunctionsBuiltIn2()
    {
        string content = @"
 <Project DefaultTargets=`t`>

        <Target Name=`t`>
            <ItemGroup>
                <I Include=`foo;bar`/>
            </ItemGroup>

            <Message Text=`[@(I->FullPath()->Distinct())]` />
        </Target>
</Project>
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        var current = Directory.GetCurrentDirectory();
        log.AssertLogContains(String.Format(@"[{0}foo;{0}bar]", current + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ExpandItemVectorFunctionsBuiltIn3()
    {
        string content = @"
 <Project DefaultTargets=`t`>

        <Target Name=`t`>
            <ItemGroup>
                <I Include=`foo;bar;foo;bar;foo`/>
            </ItemGroup>

            <Message Text=`[@(I->FullPath()->Distinct())]` />
        </Target>
</Project>
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        var current = Directory.GetCurrentDirectory();
        log.AssertLogContains(String.Format(@"[{0}foo;{0}bar]", current + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ExpandItemVectorFunctionsBuiltIn4()
    {
        string content = @"
 <Project DefaultTargets=`t`>

        <Target Name=`t`>
            <ItemGroup>
                <I Include=`foo;bar;foo;bar;foo`/>
            </ItemGroup>

            <Message Text=`[@(I->Identity()->Distinct())]` />
        </Target>
</Project>
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        log.AssertLogContains("[foo;bar]");
    }

    [LongPathSupportDisabledFact(fullFrameworkOnly: true, additionalMessage: "https://github.com/dotnet/msbuild/issues/4363")]
    public void ExpandItemVectorFunctionsBuiltIn_PathTooLongError()
    {
        string content = @"
 <Project DefaultTargets=`t`>

        <Target Name=`t`>
            <ItemGroup>
                <I Include=`fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo`/>
            </ItemGroup>

            <Message Text=`[@(I->FullPath())]` />
        </Target>
</Project>
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(content, false /* no crashes */);
        log.AssertLogContains("MSB4198");
    }

    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486. Cannot have invalid characters in file name on Unix.")]
    public void ExpandItemVectorFunctionsBuiltIn_InvalidCharsError()
    {
        string content = @"
 <Project DefaultTargets=`t`>

        <Target Name=`t`>
            <ItemGroup>
                <I Include=`aaa|||bbb\ccc.txt`/>
            </ItemGroup>

            <Message Text=`[@(I->Directory())]` />
        </Target>
</Project>
                ";

        MockLogger log = Helpers.BuildProjectWithNewOMExpectFailure(content, false /* no crashes */);
        log.AssertLogContains("MSB4198");
    }

    /// <summary>
    /// /// Expand an item vector function that is an itemspec modifier
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsItemSpecModifier2()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander();

        ProjectItemInstanceFactory itemFactory = new ProjectItemInstanceFactory(project, "i");

        IList<ProjectItemInstance> itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->'%(Meta0)'->'%(Directory)')", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.Count.ShouldBe(10);
        itemsTrue[5].ItemType.ShouldBe("i");
        itemsTrue[5].EvaluatedInclude.ShouldBe(Path.Combine("firstdirectory", "seconddirectory") + Path.DirectorySeparatorChar);

        itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->'%(Meta0)'->'%(Filename)')", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.Count.ShouldBe(10);
        itemsTrue[5].ItemType.ShouldBe("i");
        itemsTrue[5].EvaluatedInclude.ShouldBe("file0");

        itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->'%(Meta0)'->'%(Extension)'->Distinct())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.ShouldHaveSingleItem();
        itemsTrue[0].ItemType.ShouldBe("i");
        itemsTrue[0].EvaluatedInclude.ShouldBe(".ext");

        itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->'%(Meta0)'->'%(Filename)'->Substring($(Val)))", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.Count.ShouldBe(10);
        itemsTrue[5].ItemType.ShouldBe("i");
        itemsTrue[5].EvaluatedInclude.ShouldBe("le0");
    }

    /// <summary>
    /// Expand an item vector function Metadata()->DirectoryName()
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsGetDirectoryNameOfMetadataValue()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander();

        ProjectItemInstanceFactory itemFactory = new ProjectItemInstanceFactory(project, "i");

        IList<ProjectItemInstance> itemsTrue = expander.ExpandIntoItemsLeaveEscaped("@(i->Metadata('Meta0')->DirectoryName())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        itemsTrue.Count.ShouldBe(10);
        itemsTrue[5].ItemType.ShouldBe("i");
        itemsTrue[5].EvaluatedInclude.ShouldBe(Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory"));
    }

    /// <summary>
    /// Expand an item vector function Metadata() that contains semi-colon delimited sub-items
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExpandItemVectorFunctionsMetadataValueMultiItem()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander();

        ProjectItemInstanceFactory itemFactory = new ProjectItemInstanceFactory(project, "i");

        IList<ProjectItemInstance> items = expander.ExpandIntoItemsLeaveEscaped("@(i->Metadata('Meta10')->DirectoryName())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        items.Count.ShouldBe(20);
        items[5].ItemType.ShouldBe("i");
        items[6].ItemType.ShouldBe("i");
        items[5].EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), @"secondd;rectory"));
        items[6].EvaluatedInclude.ShouldBe(Path.Combine(Directory.GetCurrentDirectory(), @"someo;herplace"));
    }

    /// <summary>
    /// Expand an item vector function Items->ClearMetadata()
    /// </summary>
    [Fact]
    public void ExpandItemVectorFunctionsClearMetadata()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var expander = CreateItemFunctionExpander();

        ProjectItemInstanceFactory itemFactory = new(project, "i");

        IList<ProjectItemInstance> items = expander.ExpandIntoItemsLeaveEscaped("@(i->ClearMetadata())", itemFactory, ExpanderOptions.ExpandItems, MockElementLocation.Instance);

        items.Count.ShouldBe(10);
        items[5].ItemType.ShouldBe("i");
        items[5].Metadata.ShouldBeEmpty();
    }

    /// <summary>
    /// Creates an expander populated with some ProjectPropertyInstances and ProjectPropertyItems.
    /// </summary>
    /// <returns></returns>
    private IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateItemFunctionExpander()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        PropertyDictionary<ProjectPropertyInstance> pg = new PropertyDictionary<ProjectPropertyInstance>();
        pg.Set(ProjectPropertyInstance.Create("p", "v0"));
        pg.Set(ProjectPropertyInstance.Create("p", "v1"));
        pg.Set(ProjectPropertyInstance.Create("Val", "2"));
        pg.Set(ProjectPropertyInstance.Create("a", "filename"));

        ItemDictionary<ProjectItemInstance> ig = new ItemDictionary<ProjectItemInstance>();

        for (int n = 0; n < 10; n++)
        {
            ProjectItemInstance pi = new ProjectItemInstance(project, "i", "i" + n.ToString(), project.FullPath);
            for (int m = 0; m < 5; m++)
            {
                pi.SetMetadata("Meta" + m.ToString(), Path.Combine(s_rootPathPrefix, "firstdirectory", "seconddirectory", "file") + m.ToString() + ".ext");
            }
            pi.SetMetadata("Meta9", Path.Combine("seconddirectory", "file.ext"));
            pi.SetMetadata("Meta10", String.Format(";{0};{1};", Path.Combine("someo%3bherplace", "foo.txt"), Path.Combine("secondd%3brectory", "file.ext")));
            pi.SetMetadata("MetaBlank", @"");

            if (n % 2 > 0)
            {
                pi.SetMetadata("Even", "true");
                pi.SetMetadata("Odd", "false");
            }
            else
            {
                pi.SetMetadata("Even", "false");
                pi.SetMetadata("Odd", "true");
            }
            ig.Add(pi);
        }

        Dictionary<string, string> itemMetadataTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        itemMetadataTable["Culture"] = "abc%253bdef;$(Gee_Aych_Ayee)";
        itemMetadataTable["Language"] = "english";
        IMetadataTable itemMetadata = new StringMetadataTable(itemMetadataTable);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg, ig, itemMetadata);

        return expander;
    }

    /// <summary>
    /// Creates an expander populated with some ProjectPropertyInstances and ProjectPropertyItems.
    /// </summary>
    /// <returns></returns>
    private IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        PropertyDictionary<ProjectPropertyInstance> pg = new PropertyDictionary<ProjectPropertyInstance>();
        pg.Set(ProjectPropertyInstance.Create("p", "v0"));
        pg.Set(ProjectPropertyInstance.Create("p", "v1"));

        ItemDictionary<ProjectItemInstance> ig = new ItemDictionary<ProjectItemInstance>();
        ProjectItemInstance i0 = new ProjectItemInstance(project, "i", "i0", project.FullPath);
        ProjectItemInstance i1 = new ProjectItemInstance(project, "i", "i1", project.FullPath);
        ig.Add(i0);
        ig.Add(i1);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(
            pg,
            ig,
            new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)));

        return expander;
    }

    /// <summary>
    /// Regression test for bug when there are literally zero items declared
    /// in the project, we should continue to expand item list references to empty-string
    /// rather than not expand them at all.
    /// </summary>
    [Fact]
    public void ZeroItemsInProjectExpandsToEmpty()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <Target Name=`Build` Condition=`'@(foo)'!=''` >
                        <Message Text=`This target should NOT run.`/>
                    </Target>

                </Project>
                ");

        logger.AssertLogDoesntContain("This target should NOT run.");

        logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>

                    <ItemGroup>
                        <foo Include=`abc` Condition=` '@(foo)' == '' ` />
                    </ItemGroup>

                    <Target Name=`Build`>
                        <Message Text=`Item list foo contains @(foo)`/>
                    </Target>

                </Project>
                ");

        logger.AssertLogContains("Item list foo contains abc");
    }

    [Fact]
    public void ItemIncludeContainsMultipleItemReferences()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"
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
                ");

        logger.AssertLogContains("Property OutputType=Library");
        logger.AssertLogContains("Item ObjFiles=foo.obj;bar.obj");
        logger.AssertLogContains("Item CleanFiles=foo.obj;bar.obj");
    }

    /// <summary>
    /// Bad path with illegal windows chars when getting metadata through ->Metadata function
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathAndMetadataItemFunctionInvalidWindowsPathChars()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(@"
                <Project DefaultTargets='Build'>
                    <ItemGroup>
                        <x Include='" + ":|?*" + @"'/>
                    </ItemGroup>
                    <Target Name='Build'>
                        <Message Text=""@(x->Metadata('FullPath'))"" />
                    </Target>
                </Project>", false);

        logger.AssertLogContains("MSB4023");
    }

    /// <summary>
    /// Asking for blank metadata
    /// </summary>
    [Fact]
    public void InvalidMetadataName()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(@"
                <Project DefaultTargets='Build'>
                    <ItemGroup>
                        <x Include='x'/>
                    </ItemGroup>
                    <Target Name='Build'>
                        <Message Text=""@(x->Metadata(''))"" />
                    </Target>
                </Project>", false);

        logger.AssertLogContains("MSB4023");
    }

    /// <summary>
    /// Bad path with illegal windows chars when getting metadata through ->WithMetadataValue function
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathAndMetadataItemFunctionInvalidWindowsPathChars2()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(@"
                <Project DefaultTargets='Build'>
                    <ItemGroup>
                        <x Include='" + ":|?*" + @"'/>
                    </ItemGroup>
                    <Target Name='Build'>
                        <Message Text=""@(x->WithMetadataValue('FullPath', 'x'))"" />
                    </Target>
                </Project>", false);

        logger.AssertLogContains("MSB4023");
    }

    /// <summary>
    /// Asking for blank metadata with ->WithMetadataValue
    /// </summary>
    [Fact]
    public void InvalidMetadataName2()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(@"
                <Project DefaultTargets='Build'>
                    <ItemGroup>
                        <x Include='x'/>
                    </ItemGroup>
                    <Target Name='Build'>
                        <Message Text=""@(x->WithMetadataValue('', 'x'))"" />
                    </Target>
                </Project>", false);

        logger.AssertLogContains("MSB4023");
    }

    /// <summary>
    /// Bad path with illegal windows chars when getting metadata through ->AnyHaveMetadataValue function
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathAndMetadataItemInvalidWindowsPathChars3()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(@"
                <Project DefaultTargets='Build'>
                    <ItemGroup>
                        <x Include='" + ":|?*" + @"'/>
                    </ItemGroup>
                    <Target Name='Build'>
                        <Message Text=""@(x->AnyHaveMetadataValue('FullPath', 'x'))"" />
                    </Target>
                </Project>", false);

        logger.AssertLogContains("MSB4023");
    }

    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathInDirectMetadata()
    {
        var logger = Helpers.BuildProjectContentUsingBuildManagerExpectResult(
            @"<Project DefaultTargets='Build'>
                    <ItemGroup>
                        <x Include=':|?*'>
                            <m>%(FullPath)</m>
                        </x>
                    </ItemGroup>
                </Project>",
            BuildResultCode.Failure);

        logger.AssertLogContains("MSB4248");
    }

    [LongPathSupportDisabledFact(fullFrameworkOnly: true, additionalMessage: "new enough dotnet.exe transparently opts into long paths")]
    public void PathTooLongInDirectMetadata()
    {
        var logger = Helpers.BuildProjectContentUsingBuildManagerExpectResult(
            @"<Project DefaultTargets='Build'>
                    <ItemGroup>
                        <x Include='" + new string('x', 250) + @"'>
                            <m>%(FullPath)</m>
                        </x>
                    </ItemGroup>
                </Project>",
            BuildResultCode.Failure);

        logger.AssertLogContains("MSB4248");
    }

    /// <summary>
    /// Asking for blank metadata with ->AnyHaveMetadataValue
    /// </summary>
    [Fact]
    public void InvalidMetadataName3()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectFailure(@"
                <Project DefaultTargets='Build'>
                    <ItemGroup>
                        <x Include='x'/>
                    </ItemGroup>
                    <Target Name='Build'>
                        <Message Text=""@(x->AnyHaveMetadataValue('', 'x'))"" />
                    </Target>
                </Project>", false);

        logger.AssertLogContains("MSB4023");
    }

    /// <summary>
    /// Filter by metadata presence
    /// </summary>
    [Fact]
    public void HasMetadata()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"
<Project ToolsVersion=""msbuilddefaulttoolsversion"">

  <ItemGroup>
    <_Item Include=""One"">
      <A>aa</A>
      <B>bb</B>
      <C>cc</C>
    </_Item>
    <_Item Include=""Two"">
      <B>bb</B>
      <C>cc</C>
    </_Item>
    <_Item Include=""Three"">
      <A>aa</A>
      <C>cc</C>
    </_Item>
    <_Item Include=""Four"">
      <A>aa</A>
      <B>bb</B>
      <C>cc</C>
    </_Item>
    <_Item Include=""Five"">
      <A></A>
    </_Item>
  </ItemGroup>

  <Target Name=""AfterBuild"">
    <Message Text=""[@(_Item->HasMetadata('a'), '|')]""/>
  </Target>


</Project>");

        logger.AssertLogContains("[One|Three|Four]");
    }


    /// <summary>
    /// Filter items by WithoutMetadataValue function
    /// </summary>
    [Fact]
    public void WithoutMetadataValue()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
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

        logger.AssertLogContains("[Two|Three|Four]");
    }
    [Fact]
    public void DirectItemMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess(@"
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
                ");

        logger.AssertLogContains("QualifiedNotMatchCase Foo=X");
        logger.AssertLogContains("QualifiedMatchCase Foo=X");
        logger.AssertLogContains("UnqualifiedNotMatchCase Foo=X");
        logger.AssertLogContains("UnqualifiedMatchCase Foo=X");
    }

    [Fact]
    public void ItemDefinitionGroupMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
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

        logger.AssertLogContains("QualifiedNotMatchCase Foo=X");
        logger.AssertLogContains("QualifiedMatchCase Foo=X");
        logger.AssertLogContains("UnqualifiedNotMatchCase Foo=X");
        logger.AssertLogContains("UnqualifiedMatchCase Foo=X");
    }

    [Fact]
    public void WellKnownMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
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

        logger.AssertLogContains("QualifiedNotMatchCase Foo=Foo");
        logger.AssertLogContains("QualifiedMatchCase Foo=Foo");
        logger.AssertLogContains("UnqualifiedNotMatchCase Foo=Foo");
        logger.AssertLogContains("UnqualifiedMatchCase Foo=Foo");
    }

    /// <summary>
    /// Verify when there is an error due to an attempt to use a static method that we report the method name.
    /// </summary>
    [Fact]
    public void StaticMethodErrorMessageHaveMethodName()
    {
        InvalidProjectFileException ex = Should.Throw<InvalidProjectFileException>(() =>
            Helpers.BuildProjectWithNewOMExpectFailure("""
                <Project DefaultTargets='Build'>
                    <PropertyGroup>
                        <Function>$([System.IO.Path]::Combine(null,''))</Function>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <Message Text='[ $(Function) ]' />
                    </Target>
                </Project>
                """,
                allowTaskCrash: false));

        ex.Message.ShouldContain("[System.IO.Path]::Combine(null, '')", Case.Insensitive);
    }

    /// <summary>
    /// Verify when there is an error due to an attempt to use a static method that we report the method name
    /// </summary>
    [Fact]
    public void StaticMethodErrorMessageHaveMethodName1()
    {
        InvalidProjectFileException ex = Should.Throw<InvalidProjectFileException>(() =>
            Helpers.BuildProjectWithNewOMExpectFailure("""
                <Project DefaultTargets='Build'>
                    <PropertyGroup>
                        <Function>$(System.IO.Path::Combine('a','b'))</Function>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <Message Text='[ $(Function) ]' />
                    </Target>
                </Project>
                """,
                allowTaskCrash: false));

        ex.Message.ShouldContain("System.IO.Path::Combine('a','b')", Case.Insensitive);
    }

    [Fact]
    public void StaticMethodWithThrowawayParameterSupported()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
              <PropertyGroup>
                <MyProperty>Value is $([System.Int32]::TryParse("3", out _))</MyProperty>
              </PropertyGroup>
              <Target Name='Build'>
                <Message Text='$(MyProperty)' />
              </Target>
            </Project>
            """);

        logger.FullLog.ShouldContain("Value is True");
    }

    [Fact]
    public void StaticMethodWithThrowawayParameterSupported2()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
              <PropertyGroup>
                <MyProperty>Value is $([System.Int32]::TryParse("notANumber", out _))</MyProperty>
              </PropertyGroup>
              <Target Name='Build'>
                <Message Text='$(MyProperty)' />
              </Target>
            </Project>
            """);

        logger.FullLog.ShouldContain("Value is False");
    }

    [Fact]
    public void ExpandEmptyPropertyExpressionToEmpty()
        => ExpandProperties("$()")
            .ShouldBe(string.Empty);

    /// <summary>
    ///  Modern-only: LegacyExpander invokes the compatible method repeatedly while probing overloads.
    /// </summary>
    [ModernExpanderOnlyFact]
    public void InstanceMethodWithThrowawayParameterInvokedOnce()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::new().Invoke('value', out _))", allowReflection: true)
            .ShouldBe("1");
    }

    /// <summary>
    ///  Modern-only: LegacyExpander swallows method-body exceptions while probing overloads.
    /// </summary>
    [ModernExpanderOnlyFact]
    public void InstanceMethodWithThrowawayParameterPropagatesInvocationFailure()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        InvalidProjectFileException ex = Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties($"$([{typeName}]::new().Throw('value', out _))", allowReflection: true));

        ex.Message.ShouldContain("out invocation failed");
    }

    /// <summary>
    ///  Modern-only: LegacyExpander treats any parameter at an <c>out _</c> position as a potential out parameter.
    /// </summary>
    [ModernExpanderOnlyFact]
    public void InstanceMethodWithThrowawayParameterDoesNotBindNormalParameter()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::new().NotOut('value', out _))", allowReflection: true)
            .ShouldBe(string.Empty);
    }

    /// <summary>
    ///  Parity: Non-out arguments must disambiguate overloads when the out argument has no input value.
    /// </summary>
    [Fact]
    public void InstanceMethodWithThrowawayParameterSelectsCompatibleOverload()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::new().Select('value', out _))", allowReflection: true)
            .ShouldBe("string");
    }

    /// <summary>
    ///  Parity: Multiple discarded out arguments must bind and execute normally.
    /// </summary>
    [Fact]
    public void InstanceMethodWithMultipleThrowawayParametersSupported()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::new().Multiple('value', out _, out _))", allowReflection: true)
            .ShouldBe("value");
    }

    /// <summary>
    ///  Legacy-only: Preserves the compatibility behavior in which overload probing leaks its <c>"null"</c>
    ///  sentinel into the expansion result.
    /// </summary>
    [LegacyExpanderOnlyFact]
    public void LegacyInstanceMethodWithThrowawayParameterExpandsNullResultAsLiteralNull()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::new().ReturnNull('value', out _))", allowReflection: true)
            .ShouldBe("null");
    }

    /// <summary>
    ///  Modern-only: A successfully invoked method returning <see langword="null"/> expands to an empty string.
    /// </summary>
    [ModernExpanderOnlyFact]
    public void InstanceMethodWithThrowawayParameterExpandsNullResultAsEmpty()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::new().ReturnNull('value', out _))", allowReflection: true)
            .ShouldBe(string.Empty);
    }

    /// <summary>
    ///  Legacy-only: Preserves the compatibility behavior in which a nested property function passes the leaked
    ///  <c>"null"</c> sentinel to an outer function as a string.
    /// </summary>
    [LegacyExpanderOnlyFact]
    public void LegacyNestedMethodWithThrowawayParameterPassesNullResultAsString()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::ClassifyNullResult($([{typeName}]::new().ReturnNull('value', out _))))", allowReflection: true)
            .ShouldBe("null string");
    }

    /// <summary>
    ///  Modern-only: Property expansion omits a nested property function's <see langword="null"/> result, so
    ///  the outer function receives an empty string rather than the legacy <c>"null"</c> sentinel.
    /// </summary>
    [ModernExpanderOnlyFact]
    public void NestedMethodWithThrowawayParameterPassesNullResultAsEmptyString()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::ClassifyNullResult($([{typeName}]::new().ReturnNull('value', out _))))", allowReflection: true)
            .ShouldBe("empty string");
    }

    /// <summary>
    ///  Parity: Overloads that differ only by discarded out-parameter type remain unbindable.
    /// </summary>
    [Fact]
    public void InstanceMethodWithThrowawayParameterDoesNotBindAmbiguousOverloads()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        env.WithTransientTestState(new TransientAvailableStaticMembersCache());

        string typeName = typeof(OutArgumentProbe).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::new().Ambiguous('value', out _))", allowReflection: true)
            .ShouldBe(string.Empty);
    }

    public sealed class OutArgumentProbe
    {
        private int _invocationCount;

        public int Invoke(string value, out int result)
        {
            result = default;
            return ++_invocationCount;
        }

        public int Invoke(int value, out string result)
        {
            result = string.Empty;
            return ++_invocationCount;
        }

        public string Throw(string value, out int result)
        {
            result = default;
            throw new InvalidOperationException("out invocation failed");
        }

        public string Throw(int value, out string result)
        {
            result = string.Empty;
            throw new InvalidOperationException("out invocation failed");
        }

        public string NotOut(string value, string argument)
            => value + argument;

        public string Select(string value, out int result)
        {
            result = default;
            return "string";
        }

        public string Select(int value, out string result)
        {
            result = string.Empty;
            return "int";
        }

        public string Multiple(string value, out int number, out string text)
        {
            number = default;
            text = string.Empty;
            return value;
        }

        public string ReturnNull(string value, out string result)
        {
            result = null;
            return null;
        }

        public static string ClassifyNullResult(object value)
            => value switch
            {
                null => "null reference",
                "" => "empty string",
                "null" => "null string",
                _ => "other"
            };

        public string Ambiguous(string value, out int result)
        {
            result = default;
            return "int";
        }

        public string Ambiguous(string value, out DateTime result)
        {
            result = default;
            return "DateTime";
        }
    }

    private sealed class TransientAvailableStaticMembersCache : TransientTestState
    {
        public TransientAvailableStaticMembersCache()
            => AvailableStaticMembers.Reset_ForUnitTestsOnly();

        public override void Revert()
            => AvailableStaticMembers.Reset_ForUnitTestsOnly();
    }

    [Fact]
    public void StaticMethodWithUnderscoreNotConfusedWithThrowaway()
    {
        MockLogger logger = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
              <PropertyGroup>
                <MyProperty>Value is $([System.String]::Join('_', 'asdf', 'jkl'))</MyProperty>
              </PropertyGroup>
              <Target Name='Build'>
                <Message Text='$(MyProperty)' />
              </Target>
            </Project>
            """);

        logger.FullLog.ShouldContain("Value is asdf_jkl");
    }

    /// <summary>
    ///  Creates a set of complicated item metadata and properties, and items to exercise
    ///  the Expander class.  The data here contains escaped characters, metadata that
    ///  references properties, properties that reference items, and other complex scenarios.
    /// </summary>
    /// <param name="readOnlyLookup"></param>
    /// <param name="itemMetadata"></param>
    private void CreateComplexPropertiesItemsMetadata(out Lookup readOnlyLookup, out StringMetadataTable itemMetadata)
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        Dictionary<string, string> itemMetadataTable = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Culture"] = "abc%253bdef;$(Gee_Aych_Ayee)",
            ["Language"] = "english"
        };

        itemMetadata = new StringMetadataTable(itemMetadataTable);

        PropertyDictionary<ProjectPropertyInstance> pg = new();
        pg.Set(ProjectPropertyInstance.Create("Gee_Aych_Ayee", "ghi"));
        pg.Set(ProjectPropertyInstance.Create("OutputPath", @"\jk ; l\mno%253bpqr\stu"));
        pg.Set(ProjectPropertyInstance.Create("TargetPath", "@(IntermediateAssembly->'%(RelativeDir)')"));

        List<ProjectItemInstance> intermediateAssemblyItemGroup = [];
        ProjectItemInstance i1 = new ProjectItemInstance(
            project,
            "IntermediateAssembly",
            NativeMethodsShared.IsWindows ? @"subdir1\engine.dll" : "subdir1/engine.dll",
            project.FullPath);
        intermediateAssemblyItemGroup.Add(i1);
        i1.SetMetadata("aaa", "111");
        ProjectItemInstance i2 = new(
            project,
            "IntermediateAssembly",
            NativeMethodsShared.IsWindows ? @"subdir2\tasks.dll" : "subdir2/tasks.dll",
            project.FullPath);
        intermediateAssemblyItemGroup.Add(i2);
        i2.SetMetadata("bbb", "222");

        List<ProjectItemInstance> contentItemGroup = [];
        ProjectItemInstance i3 = new(project, "Content", "splash.bmp", project.FullPath);
        contentItemGroup.Add(i3);
        i3.SetMetadata("ccc", "333");

        List<ProjectItemInstance> resourceItemGroup = [];
        ProjectItemInstance i4 = new(project, "Resource", "string$(p).resx", project.FullPath);
        resourceItemGroup.Add(i4);
        i4.SetMetadata("ddd", "444");
        ProjectItemInstance i5 = new(project, "Resource", "dialogs%253b.resx", project.FullPath);
        resourceItemGroup.Add(i5);
        i5.SetMetadata("eee", "555");

        List<ProjectItemInstance> contentItemGroup2 = [];
        ProjectItemInstance i6 = new(project, "Content", "about.bmp", project.FullPath);
        contentItemGroup2.Add(i6);
        i6.SetMetadata("fff", "666");

        ItemDictionary<ProjectItemInstance> secondaryItemsByName = [];
        secondaryItemsByName.ImportItems(resourceItemGroup);
        secondaryItemsByName.ImportItems(contentItemGroup2);

        Lookup lookup = new(secondaryItemsByName, pg);

        // Add primary items
        lookup.EnterScope("x");
        lookup.PopulateWithItems("IntermediateAssembly", intermediateAssemblyItemGroup);
        lookup.PopulateWithItems("Content", contentItemGroup);

        readOnlyLookup = lookup;
    }

    /// <summary>
    ///  Exercises ExpandAllIntoTaskItems with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoTaskItemsComplex()
    {
        CreateComplexPropertiesItemsMetadata(out Lookup lookup, out StringMetadataTable itemMetadata);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        IList<TaskItem> taskItems = expander.ExpandIntoTaskItemsLeaveEscaped(
            "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)",
             ExpanderOptions.ExpandAll,
             MockElementLocation.Instance);

        // the following items are passed to the TaskItem constructor, and thus their ItemSpecs should be
        // in escaped form.
        ObjectModelHelpers.AssertItemsMatch($"""
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

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data but in a piecemeal fashion.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringComplexPiecemeal()
    {
        CreateComplexPropertiesItemsMetadata(out Lookup lookup, out StringMetadataTable itemMetadata);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        string stringToExpand = "@(Resource->'%(Filename)') ;";
        expander.ExpandIntoStringAndUnescape(stringToExpand, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(@"string$(p);dialogs%3b ;");

        stringToExpand = "@(Content)";
        expander.ExpandIntoStringAndUnescape(stringToExpand, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe("splash.bmp");

        stringToExpand = "@(NonExistent)";
        expander.ExpandIntoStringAndUnescape(stringToExpand, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(string.Empty);

        stringToExpand = "$(NonExistent)";
        expander.ExpandIntoStringAndUnescape(stringToExpand, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(string.Empty);

        stringToExpand = "%(NonExistent)";
        expander.ExpandIntoStringAndUnescape(stringToExpand, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(string.Empty);

        stringToExpand = "$(OutputPath)";
        expander.ExpandIntoStringAndUnescape(stringToExpand, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(@"\jk ; l\mno%3bpqr\stu");

        stringToExpand = "$(TargetPath)";
        expander.ExpandIntoStringAndUnescape(stringToExpand, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe("subdir1" + Path.DirectorySeparatorChar + ";subdir2" + Path.DirectorySeparatorChar);

        stringToExpand = "%(Language)_%(Culture)";
        expander.ExpandIntoStringAndUnescape(stringToExpand, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe("english_abc%3bdef;ghi");
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with an item list using a transform that is empty.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringEmpty()
    {
        CreateComplexPropertiesItemsMetadata(out Lookup lookup, out StringMetadataTable itemMetadata);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        XmlAttribute xmlattribute = (new XmlDocument()).CreateAttribute("dummy");
        xmlattribute.Value = "@(IntermediateAssembly->'')";

        expander.ExpandIntoStringAndUnescape(xmlattribute.Value, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(";");

        xmlattribute.Value = "@(IntermediateAssembly->'%(goop)')";

        expander.ExpandIntoStringAndUnescape(xmlattribute.Value, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
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
    public void QuotedTransformDerivableItemSpecModifierUsesRequiredContext(
        string modifier,
        bool usesProjectDirectory,
        bool usesDefiningProject)
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        string itemSpec = Path.Combine("src", "directory", "File.cs");
        string definingProject = Path.Combine(project.Directory, "Imported.targets");
        var item = new ContextTrackingItem("Compile", itemSpec, project.Directory, definingProject);
        var items = new ItemDictionary<ContextTrackingItem> { item };
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var expander = ExpanderFactory.Create(
            properties,
            items,
            new TestLoggingContext(null, new BuildEventContext(1, 2, 3, 4)));

        string expected = ItemSpecModifiers.GetItemSpecModifier(itemSpec, modifier, project.Directory, definingProject);
        string actual = expander.ExpandIntoStringLeaveEscaped(
            $"@(Compile->'%({modifier})')",
            ExpanderOptions.ExpandItems,
            MockElementLocation.Instance);

        actual.ShouldBe(expected);
        item.ProjectDirectoryAccessCount.ShouldBe(usesProjectDirectory ? 1 : 0);
        item.DefiningProjectAccessCount.ShouldBe(usesDefiningProject ? 1 : 0);
    }

    private sealed class ContextTrackingItem : IItem
    {
        private readonly string _itemSpec;
        private readonly string _projectDirectory;
        private readonly string _definingProject;

        public ContextTrackingItem(string itemType, string itemSpec, string projectDirectory, string definingProject)
        {
            Key = itemType;
            _itemSpec = itemSpec;
            _projectDirectory = projectDirectory;
            _definingProject = definingProject;
        }

        public string Key { get; }

        public string EvaluatedInclude => _itemSpec;

        public string EvaluatedIncludeEscaped => _itemSpec;

        public string ProjectDirectory
        {
            get
            {
                ProjectDirectoryAccessCount++;
                return _projectDirectory;
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
                return _definingProject;
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
        CreateComplexPropertiesItemsMetadata(out Lookup lookup, out StringMetadataTable itemMetadata);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        XmlAttribute xmlattribute = (new XmlDocument()).CreateAttribute("dummy");
        xmlattribute.Value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        expander.ExpandIntoStringAndUnescape(xmlattribute.Value, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(
                @"string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1" +
                Path.DirectorySeparatorChar +
                ";subdir2" +
                Path.DirectorySeparatorChar +
                " ; english_abc%3bdef;ghi");
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringLeaveEscapedComplex()
    {
        CreateComplexPropertiesItemsMetadata(out Lookup lookup, out StringMetadataTable itemMetadata);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        XmlAttribute xmlattribute = (new XmlDocument()).CreateAttribute("dummy");
        xmlattribute.Value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        expander.ExpandIntoStringLeaveEscaped(xmlattribute.Value, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(
                @"string$(p);dialogs%253b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%253bpqr\stu ; subdir1" +
                Path.DirectorySeparatorChar +
                ";subdir2" +
                Path.DirectorySeparatorChar +
                " ; english_abc%253bdef;ghi");
    }

    /// <summary>
    /// Exercises ExpandIntoStringAndUnescape and ExpanderOptions.Truncate
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringTruncated()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        var manySpaces = "".PadLeft(2000);
        var pg = new PropertyDictionary<ProjectPropertyInstance>();
        pg.Set(ProjectPropertyInstance.Create("ManySpacesProperty", manySpaces));
        var itemMetadataTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ManySpacesMetadata", manySpaces },
        };

        var itemMetadata = new StringMetadataTable(itemMetadataTable);
        var projectItemGroups = new ItemDictionary<ProjectItemInstance>();
        var itemGroup = new List<ProjectItemInstance>();
        for (int i = 0; i < 50; i++)
        {
            var item = new ProjectItemInstance(project, "ManyItems", $"ThisIsAFairlyLongFileName_{i}.bmp", project.FullPath);
            item.SetMetadata("Foo", $"ThisIsAFairlyLongMetadataValue_{i}");
            itemGroup.Add(item);
        }

        var lookup = new Lookup(projectItemGroups, pg);
        lookup.EnterScope("x");
        lookup.PopulateWithItems("ManySpacesItem",
        [
            new ProjectItemInstance(project, "ManySpacesItem", "Foo", project.FullPath),
            new ProjectItemInstance(project, "ManySpacesItem", manySpaces, project.FullPath),
            new ProjectItemInstance(project, "ManySpacesItem", "Bar", project.FullPath),
        ]);

        lookup.PopulateWithItems("Exactly1024",
        [
            new ProjectItemInstance(project, "Exactly1024", "".PadLeft(1024), project.FullPath),
            new ProjectItemInstance(project, "Exactly1024", "Foo", project.FullPath),
        ]);

        lookup.PopulateWithItems("ManyItems", itemGroup);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        XmlAttribute xmlattribute = new XmlDocument().CreateAttribute("dummy");
        xmlattribute.Value = "'%(ManySpacesMetadata)' != '' and '$(ManySpacesProperty)' != '' and '@(ManySpacesItem)' != '' and '@(Exactly1024)' != '' and '@(ManyItems)' != '' and '@(ManyItems->'%(Foo)')' != '' and '@(ManyItems->'%(Nonexistent)')' != ''";

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
        expander.ExpandIntoStringAndUnescape(xmlattribute.Value, ExpanderOptions.ExpandAll | ExpanderOptions.Truncate, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a string that does not need expanding.
    ///  In this case the expanded string should be reference identical to the passed in string.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringExpectIdenticalReference()
    {
        CreateComplexPropertiesItemsMetadata(out Lookup lookup, out StringMetadataTable itemMetadata);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        XmlAttribute xmlattribute = new XmlDocument().CreateAttribute("dummy");

        // Create a *non-literal* string. If we used a literal string, the CLR might (would) intern
        // it, which would mean that Expander would inevitably return a reference to the same string.
        // In real builds, the strings will never be literals, and we want to test the behavior in
        // that situation.
        xmlattribute.Value = "abc123" + new Random().Next();
        string expandedString = expander.ExpandIntoStringLeaveEscaped(xmlattribute.Value, ExpanderOptions.ExpandAll, MockElementLocation.Instance);

        // Verify neither string got interned, so that this test is meaningful
        string.IsInterned(xmlattribute.Value).ShouldBeNull();
        string.IsInterned(expandedString).ShouldBeNull();

        // Finally verify Expander indeed didn't create a new string.
        expandedString.ShouldBeSameAs(xmlattribute.Value);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data and various expander options.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringExpanderOptions()
    {
        CreateComplexPropertiesItemsMetadata(out Lookup lookup, out StringMetadataTable itemMetadata);

        string value = @"@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        expander.ExpandIntoStringAndUnescape(value, ExpanderOptions.ExpandProperties, MockElementLocation.Instance)
            .ShouldBe(@"@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ; %(NonExistent) ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; %(Language)_%(Culture)");

        expander.ExpandIntoStringAndUnescape(value, ExpanderOptions.ExpandPropertiesAndMetadata, MockElementLocation.Instance)
            .ShouldBe(@"@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ;  ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; english_abc%3bdef;ghi");

        expander.ExpandIntoStringAndUnescape(value, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(@$"string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%3bdef;ghi");

        expander.ExpandIntoStringAndUnescape(value, ExpanderOptions.ExpandItems, MockElementLocation.Instance)
            .ShouldBe(@"string$(p);dialogs%3b ; splash.bmp ;  ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)");
    }

    /// <summary>
    ///  Builds an <see cref="IExpander{P, I}"/> backed by a fixed metadata table for exercising the
    ///  hand-written metadata scanner. Metadata values intentionally contain no path separators so
    ///  that <c>MaybeAdjustFilePath</c> does not perturb the asserted results.
    /// </summary>
    private static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateMetadataExpander()
    {
        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Culture"] = "en-US",
            ["Foo"] = "Bar",
            ["Compile.Link"] = "Link.cs",
            ["Filename"] = "App",
        };

        return ExpanderFactory.Create(
            new PropertyDictionary<ProjectPropertyInstance>(),
            new ItemDictionary<ProjectItemInstance>(),
            new StringMetadataTable(metadata));
    }

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
    {
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateMetadataExpander();

        expander.ExpandIntoStringLeaveEscaped(input, ExpanderOptions.ExpandMetadata, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData("%(", ExpanderOptions.ExpandMetadata)]
    [InlineData("%(Culture)", ExpanderOptions.ExpandBuiltInMetadata)]
    [InlineData("%(Filename)", ExpanderOptions.ExpandCustomMetadata)]
    internal void ExpandMetadata_NoExpansionReturnsOriginalString(string expression, ExpanderOptions options)
    {
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateMetadataExpander();

        expander.ExpandIntoStringLeaveEscaped(expression, options, MockElementLocation.Instance)
            .ShouldBeSameAs(expression);
    }

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
    {
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateMetadataExpander();

        expander.ExpandIntoStringLeaveEscaped(input, ExpanderOptions.ExpandMetadata, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

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
    {
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateMetadataExpander();

        expander.ExpandIntoStringLeaveEscaped(input, options, MockElementLocation.Instance)
            .ShouldBe(expected);
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
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = CreateItemFunctionExpander();

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            expander.ExpandIntoStringLeaveEscaped(input, ExpanderOptions.ExpandItems, MockElementLocation.Instance));

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
        CreateComplexPropertiesItemsMetadata(out Lookup lookup, out StringMetadataTable itemMetadata);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(lookup, lookup, itemMetadata);

        string value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        IList<string> expanded = expander.ExpandIntoStringListLeaveEscaped(value, ExpanderOptions.ExpandAll, MockElementLocation.Instance).ToList();

        expanded.Count.ShouldBe(9);
        expanded[0].ShouldBe(@"string$(p)");
        expanded[1].ShouldBe(@"dialogs%253b");
        expanded[2].ShouldBe("splash.bmp");
        expanded[3].ShouldBe(@"\jk");
        expanded[4].ShouldBe(@"l\mno%253bpqr\stu");
        expanded[5].ShouldBe("subdir1" + Path.DirectorySeparatorChar);
        expanded[6].ShouldBe("subdir2" + Path.DirectorySeparatorChar);
        expanded[7].ShouldBe(@"english_abc%253bdef");
        expanded[8].ShouldBe("ghi");
    }

    internal ITaskItem[] GetTaskArrayFromItemList(IList<TaskItem> list)
    {
        ITaskItem[] items = new ITaskItem[list.Count];
        for (int i = 0; i < list.Count; ++i)
        {
            items[i] = list[i];
        }

        return items;
    }

    /// <summary>
    /// v10.0\TeamData\Microsoft.Data.Schema.Common.targets shipped with bad syntax:
    /// $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory)
    /// this was evaluating to blank before, now it errors; we have to special case it to
    /// evaluate to blank.
    /// Note that this still works whether or not the key exists and has a value.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixSpecialCase()
        => ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory)")
            .ShouldBe(string.Empty);

    // Compat hack: WebProjects may have an import with a condition like:
    //       Condition=" '$(Solutions.VSVersion)' == '8.0'"
    // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
    // Be compatible by returning an empty string here.
    [Fact]
    public void Regress692569()
        => ExpandProperties(@"$(Solutions.VSVersion)")
            .ShouldBe(string.Empty);

    /// <summary>
    /// In the general case, we should still error for properties that incorrectly miss the Registry: prefix.
    /// Note that this still fails whether or not the key exists.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixError()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@XXXXDBDirectory)"));

    /// <summary>
    /// In the general case, we should still error for properties that incorrectly miss the Registry: prefix, like
    /// the special case, but with extra char on the end.
    /// Note that this still fails whether or not the key exists.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixError2()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectoryX)"));

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyString()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");
            key.SetValue("Value", "String", RegistryValueKind.String);

            ExpandProperties(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)")
                .ShouldBe("String");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyBinary()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            byte[] utfText = Encoding.UTF8.GetBytes("String".ToCharArray());
            key.SetValue("Value", utfText, RegistryValueKind.Binary);

            ExpandProperties(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)")
                .ShouldBe("83;116;114;105;110;103");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyDWord()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");
            key.SetValue("Value", 123456, RegistryValueKind.DWord);

            ExpandProperties(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)")
                .ShouldBe("123456");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyExpandString()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue("Value", $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)")
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyQWord()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");
            key.SetValue("Value", 123456789123456789L, RegistryValueKind.QWord);

            ExpandProperties(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)")
                .ShouldBe("123456789123456789");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyMultiString()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");
            key.SetValue("Value", new[] { "A", "B", "C", "D" }, RegistryValueKind.MultiString);

            ExpandProperties(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test@Value)")
                .ShouldBe("A;B;C;D");
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
    [Fact]
    public void PropertyFunctionNullReturn()
    {
        // The null-returning function is the only thing in the expression.
        ExpandProperties("$([System.Environment]::GetEnvironmentVariable(`_NonExistentVar`))")
            .ShouldBe(string.Empty);

        // The result of the null-returning function is concatenated with a non-empty string.
        ExpandProperties("prefix_$([System.Environment]::GetEnvironmentVariable(`_NonExistentVar`))")
            .ShouldBe("prefix_");
    }

    /// <summary>
    ///  Expand property function that takes no arguments and returns a string.
    /// </summary>
    [Fact]
    public void PropertyFunctionNoArguments()
        => ExpandProperties("$(SomeStuff.ToUpperInvariant())", ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("THIS IS SOME STUFF");

    /// <summary>
    ///  Expand property function that takes no arguments and returns a string (trimmed).
    /// </summary>
    [Fact]
    public void PropertyFunctionNoArgumentsTrim()
        => ExpandProperties("$(FileName.Trim())", ("FileName", "    foo.ext   "))
            .ShouldBe("foo.ext");

    /// <summary>
    ///  Expand property function that is a get property accessor.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyGet()
        => ExpandProperties("$(SomeStuff.Length)", ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("18");

    /// <summary>
    ///  Expand property function which is a manual get property accessor.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyManualGet()
        => ExpandProperties("$(SomeStuff.get_Length())", ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("18");

    /// <summary>
    ///  Expand property function which is a manual get property accessor and a concatenation of a constant.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyNoArgumentsConcat()
        => ExpandProperties("$(SomeStuff.ToLowerInvariant())_goop", ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("this is some stuff_goop");

    /// <summary>
    /// Expand property function with a constant argument
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgument()
        => ExpandProperties("$(SomeStuff.SubString(13))", ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("STUff");

    /// <summary>
    /// Expand property function with a constant argument that contains spaces
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentWithSpaces()
        => ExpandProperties("$(SomeStuff.SubString(8))", ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("SOME STUff");

    /// <summary>
    /// Expand property function with a constant argument
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyPathRootSubtraction()
        => ExpandProperties(
                "$(MyPath.SubString($(RootPath.Length)))",
                ("RootPath", Path.Combine(s_rootPathPrefix, "this", "is", "the", "root")),
                ("MyPath", Path.Combine(s_rootPathPrefix, "this", "is", "the", "root", "my", "project", "is", "here.proj")))
            .ShouldBe(Path.Combine(Path.DirectorySeparatorChar.ToString(), "my", "project", "is", "here.proj"));

    /// <summary>
    /// Expand property function with an argument that is a property
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentExpandedProperty()
        => ExpandProperties(
                "$(SomeStuff.SubString(1$(Value)))",
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("STUff");

    /// <summary>
    /// Expand property function that has a boolean return value
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentBooleanReturn()
    {
        ExpandProperties(
                @"$(PathRoot2.Endswith(" + Path.DirectorySeparatorChar + "))",
                ("PathRoot2", Path.Combine(s_rootPathPrefix, "goop") + Path.DirectorySeparatorChar))
            .ShouldBe("True");
        ExpandProperties(
                @"$(PathRoot.Endswith(" + Path.DirectorySeparatorChar + "))",
                ("PathRoot", Path.Combine(s_rootPathPrefix, "goo")))
            .ShouldBe("False");
    }

    /// <summary>
    /// Expand property function with an argument that is expanded, and a chaining of other functions.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentNestedAndChainedFunction()
        => ExpandProperties(
                "$(SomeStuff.SubString(1$(Value)).ToLowerInvariant().SubString($(Value)))",
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("ff");


    /// <summary>
    /// Expand property function with chained functions on its results
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentChained()
        => ExpandProperties(
                "$(SomeStuff.ToUpperInvariant().ToLowerInvariant())",
                ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("this is some stuff");

    /// <summary>
    /// Expand property function with an argument that is a function
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentNested()
        => ExpandProperties(
                "$(SomeStuff.SubString($(Value.get_Length())))",
                ("Value", "12345"),
                ("SomeStuff", "1234567890"))
            .ShouldBe("67890");

    /// <summary>
    /// Expand property function that returns an generic list
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

        string typeName = typeof(PropertyValueConversionTestData).AssemblyQualifiedName;

        ExpandProperties($"$([{typeName}]::GetValue(`{scenario}`))")
            .ShouldBe(expected);
    }

    /// <summary>
    /// Expand property function that returns an array
    /// </summary>
    [Fact]
    public void PropertyFunctionArrayReturn()
        => ExpandProperties("$(List.Split(-))", ("List", "A-B-C-D"))
            .ShouldBe("A;B;C;D");

    /// <summary>
    /// Expand property function that returns a Dictionary
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionDictionaryReturn()
    {
        string expected = ("OS=" + Environment.GetEnvironmentVariable("OS")).ToUpperInvariant();

        ExpandProperties("$([System.Environment]::GetEnvironmentVariables())")
            .ToUpperInvariant()
            .ShouldContain(expected);
    }

    /// <summary>
    /// Expand property function that returns an array
    /// </summary>
    [Fact]
    public void PropertyFunctionArrayReturnManualSplitter()
        => ExpandProperties(
                "$(List.Split($(Splitter.ToCharArray())))",
                ("List", "A-B-C-D"),
                ("Splitter", "-"))
            .ShouldBe("A;B;C;D");

    /// <summary>
    /// Expand property function that returns an array
    /// </summary>
    [Fact]
    public void PropertyFunctionInCondition()
    {
        PropertyDictionary<ProjectPropertyInstance> pg = new PropertyDictionary<ProjectPropertyInstance>();
        pg.Set(ProjectPropertyInstance.Create("PathRoot", Path.Combine(s_rootPathPrefix, "goo")));
        pg.Set(ProjectPropertyInstance.Create("PathRoot2", Path.Combine(s_rootPathPrefix, "goop") + Path.DirectorySeparatorChar));

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg);

        ConditionEvaluator.EvaluateCondition(
                @"'$(PathRoot2.Endswith(`" + Path.DirectorySeparatorChar + "`))' == 'true'",
                ParserOptions.AllowAll,
                expander,
                ExpanderOptions.ExpandProperties,
                Directory.GetCurrentDirectory(),
                MockElementLocation.Instance,
                FileSystems.Default,
                new TestLoggingContext(null!, new BuildEventContext(1, 2, 3, 4)))
            .ShouldBeTrue();
        ConditionEvaluator.EvaluateCondition(
                @"'$(PathRoot.EndsWith(" + Path.DirectorySeparatorChar + "))' == 'false'",
                ParserOptions.AllowAll,
                expander,
                ExpanderOptions.ExpandProperties,
                Directory.GetCurrentDirectory(),
                MockElementLocation.Instance,
                FileSystems.Default,
                new TestLoggingContext(null!, new BuildEventContext(1, 2, 3, 4)))
            .ShouldBeTrue();
    }

    /// <summary>
    ///  Verifies that a property indexer's closing bracket must follow its opening bracket.
    /// </summary>
    [ModernExpanderOnlyFact]
    public void PropertyIndexerRejectsClosingBracketBeforeOpeningBracket()
    {
        const string expression = "$(SomeStuff][0])";

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ExpandProperties(expression));

        exception.BaseMessage.ShouldContain("SomeStuff][0]");
        exception.BaseMessage.ShouldContain(AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
    }

    /// <summary>
    ///  Verifies the legacy behavior for a property indexer whose closing bracket precedes its opening bracket.
    /// </summary>
    [LegacyExpanderOnlyFact]
    public void PropertyIndexerClosingBracketBeforeOpeningBracketPreservesLegacyError()
    {
        const string expression = "$(SomeStuff][0])";

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ExpandProperties(expression));

        exception.BaseMessage.ShouldContain("\"\".get_Chars(0)");
        exception.BaseMessage.ShouldNotContain(AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
    }

    /// <summary>
    /// Expand property function that is invalid - properties don't take arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalid1()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                "[$(SomeStuff($(Value)))]",
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    /// Expand property function - invalid since properties don't have properties
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalid2()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("[$(SomeStuff.Lgg)]", ("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    /// Expand property function - invalid since properties don't have properties and don't support '.' in them
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalid3()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(SomeStuff.ToUpperInvariant().Foo)", ("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    /// Expand property function - properties don't take arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalid4()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                "[$(SomeStuff($(System.DateTime.Now)))]",
                ("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    /// Expand property function - invalid expression
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalid5()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(SomeStuff.ToLowerInvariant()_goop)", ("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    /// Expand property function - functions with invalid arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalid6()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("[$(SomeStuff.Substring(HELLO!))]", ("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    /// Expand property function - functions with invalid arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalid7()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("[$(SomeStuff.Substring(-10))]", ("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    /// Expand property function that calls a static method with quoted arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalid8()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(([System.DateTime]::Now).ToString(\"MM.dd.yyyy\"))"));

    /// <summary>
    /// Expand property function - we don't handle metadata functions
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalidNoMetadataFunctions()
        => ExpandProperties("[%(LowerLetterList.Identity.ToUpper())]")
            .ShouldBe("[%(LowerLetterList.Identity.ToUpper())]");

    /// <summary>
    /// Expand property function - properties won't get confused with a type or namespace
    /// </summary>
    [Fact]
    public void PropertyFunctionNoCollisionsOnType()
        => ExpandProperties("$(System)", ("System", "The System Namespace"))
            .ShouldBe("The System Namespace");

    /// <summary>
    /// Expand property function that calls a static method
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodMakeRelative()
        => ExpandProperties(
                @"$([MSBuild]::MakeRelative($(ParentPath), `$(FilePath)`))",
                ("ParentPath", Path.Combine(s_rootPathPrefix, "abc", "def")),
                ("FilePath", Path.Combine(s_rootPathPrefix, "abc", "def", "foo.cpp")))
            .ShouldBe("foo.cpp");

    /// <summary>
    /// Expand property function that calls a static method
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethod1()
        => ExpandProperties(
                @"$([System.IO.Path]::Combine($(Drive), `$(File)`))",
                ("Drive", s_rootPathPrefix),
                ("File", Path.Combine("foo", "file.txt")))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    /// Expand property function that creates an instance of a type
    /// </summary>
    [Fact]
    public void PropertyFunctionConstructor1()
        => ExpandProperties(
                @"$([System.Version]::new($(ver1)).ToString())",
                ("ver1", "1.2.3.4"))
            .ShouldBe("1.2.3.4");

    /// <summary>
    /// Expand property function that creates an instance of a type
    /// </summary>
    [Fact]
    public void PropertyFunctionConstructor2()
        => ExpandProperties(
                @"$([System.Version]::new($(ver1)).CompareTo($([System.Version]::new($(ver2)))))",
                ("ver1", "1.2.3.4"),
                ("ver2", "2.2.3.4"))
            .ShouldBe("-1");

    /// <summary>
    /// Expand property function that is only available when MSBUILDENABLEALLPROPERTYFUNCTIONS=1
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: "https://github.com/dotnet/coreclr/issues/15662")]
    public void PropertyStaticFunctionAllEnabled()
    {
        using (var env = TestEnvironment.Create())
        {
            env.WithTransientTestState(new TransientEnableAllPropertyFunctions());

            try
            {
                ExpandProperties("$([System.Type]::GetType(`System.Type`))")
                    .ShouldBe("System.Type");
            }
            finally
            {
                AvailableStaticMembers.Reset_ForUnitTestsOnly();
            }
        }
    }

    /// <summary>
    /// Expand property function that is defined (on CoreFX) in an assembly named after its full namespace.
    /// </summary>
    [Fact]
    public void PropertyStaticFunctionLocatedFromAssemblyWithNamespaceName()
    {
        AppContext.TryGetSwitch("Microsoft.Build.EnableAllPropertyFunctions", out bool originalSwitch);

        try
        {
            AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", true);

            ExpandProperties("$([System.Diagnostics.Process]::GetCurrentProcess().Id)")
                .ShouldBe(System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
        }
        finally
        {
            AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", originalSwitch);
            AvailableStaticMembers.Reset_ForUnitTestsOnly();
        }
    }

    /// <summary>
    /// Expand property function that is only available when MSBUILDENABLEALLPROPERTYFUNCTIONS=1, but cannot be found
    /// </summary>
    [Fact]
    public void PropertyStaticFunctionUsingNamespaceNotFound()
    {
        string env = Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS");

        try
        {
            Environment.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

            Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([Microsoft.FOO.FileIO.FileSystem]::CurrentDirectory)"));
            Should.Throw<InvalidProjectFileException>(() => ExpandProperties("$([Foo.Baz]::new())"));
            Should.Throw<InvalidProjectFileException>(() => ExpandProperties("$([Foo]::new())"));
            Should.Throw<InvalidProjectFileException>(() => ExpandProperties("$([Foo.]::new())"));
            Should.Throw<InvalidProjectFileException>(() => ExpandProperties("$([.Foo]::new())"));
            Should.Throw<InvalidProjectFileException>(() => ExpandProperties("$([.]::new())"));
            Should.Throw<InvalidProjectFileException>(() => ExpandProperties("$([]::new())"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", env);
            AvailableStaticMembers.Reset_ForUnitTestsOnly();
        }
    }

    /// <summary>
    /// Expand property function that calls a static method
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodQuoted1()
        => ExpandProperties(
                @"$([System.IO.Path]::Combine(`" + s_rootPathPrefix + "`, `$(File)`))",
                ("File", Path.Combine("foo", "file.txt")))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    /// Expand property function that calls a static method
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted1Spaces()
        => ExpandProperties(
                @"$([System.IO.Path]::Combine(`" + Path.Combine(s_rootPathPrefix, "foo goo") + "`, `$(File)`))",
                ("File", "foo goo" + Path.DirectorySeparatorChar + "file.txt"))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo goo", "foo goo", "file.txt"));

    /// <summary>
    /// Expand property function that calls a static method
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted1Spaces2()
        => ExpandProperties(
                @"$([System.IO.Path]::Combine(`" + Path.Combine(s_rootPathPrefix, "foo baz") + @"`, `$(File)`))",
                ("File", Path.Combine("foo bar", "baz.txt")))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo baz", "foo bar", "baz.txt"));

    /// <summary>
    /// Expand property function that calls a static method
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted1Spaces3()
        => ExpandProperties(
                @"$([System.IO.Path]::Combine(`" + Path.Combine(s_rootPathPrefix, "foo baz") + @" `, `$(File)`))",
                ("File", Path.Combine("foo bar", "baz.txt")))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo baz ", "foo bar", "baz.txt"));

    /// <summary>
    /// Expand property function that calls a static method with quoted arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted2()
    {
        string dateTime = "'" + _dateToParse + "'";

        ExpandProperties("$([System.DateTime]::Parse(" + dateTime + ").ToString(\"yyyy/MM/dd HH:mm:ss\"))")
            .ShouldBe(DateTime.Parse(_dateToParse).ToString("yyyy/MM/dd HH:mm:ss"));
    }

    /// <summary>
    /// Expand property function that calls a static method with quoted arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted3()
    {
        string dateTime = "'" + _dateToParse + "'";

        ExpandProperties("$([System.DateTime]::Parse(" + dateTime + ").ToString(\"MM.dd.yyyy\"))")
            .ShouldBe(DateTime.Parse(_dateToParse).ToString("MM.dd.yyyy"));
    }

    /// <summary>
    /// Expand property function that calls a static method with quoted arguments
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted4()
        => ExpandProperties("$([System.DateTime]::Now.ToString(\"MM.dd.yyyy\"))")
            .ShouldBe(DateTime.Now.ToString("MM.dd.yyyy"));

    /// <summary>
    /// Expand property function that calls a static method
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodNested()
        => ExpandProperties(
                @"$([System.IO.Path]::Combine(`" +
                s_rootPathPrefix +
                @"`, $([System.IO.Path]::Combine(`foo`,`file.txt`))))")
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    /// Expand property function that calls a static method regex
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodRegex1()
    {
        // Support enum combines as Enum.Parse expects them
        ExpandProperties(
                @"$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, `RegexOptions.IgnoreCase,RegexOptions.Singleline`))")
            .ShouldBe("True");

        // We support the C# style enum combining syntax too
        ExpandProperties(
                @"$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, System.Text.RegularExpressions.RegexOptions.IgnoreCase|RegexOptions.Singleline))")
            .ShouldBe("True");

        ExpandProperties(@"$([System.Text.RegularExpressions.Regex]::IsMatch(`100 GBP`, `^-?\d+(\.\d{2})?$`))")
            .ShouldBe("False");
    }

    /// <summary>
    /// Expand property function that calls a static method  with an instance method chained
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodChained()
    {
        string dateTime = "'" + _dateToParse + "'";

        ExpandProperties(@"$([System.DateTime]::Parse(" + dateTime + ").ToString(`yyyy/MM/dd HH:mm:ss`))")
            .ShouldBe(DateTime.Parse(_dateToParse).ToString("yyyy/MM/dd HH:mm:ss"));
    }

    /// <summary>
    /// Expand property function that calls a static method available only on net46 (Environment.GetFolderPath)
    /// </summary>
    [Fact]
    public void PropertyFunctionGetFolderPath()
        => ExpandProperties(@"$([System.Environment]::GetFolderPath(SpecialFolder.System))")
            .ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.System));

    /// <summary>
    /// The test exercises: RuntimeInformation / OSPlatform usage, static method invocation, static property invocation, method invocation expression as argument, call chain expression as argument
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
        string propertyFunction = $"$([System.OperatingSystem]::IsOSPlatform('{platform}'))";
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsOSPlatform(platform);
#else
        result = Framework.OperatingSystem.IsOSPlatform(platform);
#endif
        string expected = result ? "True" : "False";
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
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsOSPlatformVersionAtLeast(platform, major, minor, build, revision);
#else
        result = Framework.OperatingSystem.IsOSPlatformVersionAtLeast(platform, major, minor, build, revision);
#endif
        string expected = result ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsLinux()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsLinux())";
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsLinux();
#else
        result = Framework.OperatingSystem.IsLinux();
#endif
        string expected = result ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsFreeBSD()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsFreeBSD())";
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsFreeBSD();
#else
        result = Framework.OperatingSystem.IsFreeBSD();
#endif
        string expected = result ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsFreeBSDVersionAtLeast(int major, int minor, int build, int revision)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsFreeBSDVersionAtLeast({major}, {minor}, {build}, {revision}))";
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsFreeBSDVersionAtLeast(major, minor, build, revision);
#else
        result = Framework.OperatingSystem.IsFreeBSDVersionAtLeast(major, minor, build, revision);
#endif
        string expected = result ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsMacOS()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsMacOS())";
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsMacOS();
#else
        result = Framework.OperatingSystem.IsMacOS();
#endif
        string expected = result ? "True" : "False";
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
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsMacOSVersionAtLeast(major, minor, build);
#else
        result = Framework.OperatingSystem.IsMacOSVersionAtLeast(major, minor, build);
#endif
        string expected = result ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsWindows()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsWindows())";
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsWindows();
#else
        result = Framework.OperatingSystem.IsWindows();
#endif
        string expected = result ? "True" : "False";
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
        bool result = false;
#if NET5_0_OR_GREATER
        result = OperatingSystem.IsWindowsVersionAtLeast(major, minor, build, revision);
#else
        result = Framework.OperatingSystem.IsWindowsVersionAtLeast(major, minor, build, revision);
#endif
        string expected = result ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

#if NET5_0_OR_GREATER

    [Fact]
    public void IsAndroid()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsAndroid())";

        string expected = OperatingSystem.IsAndroid() ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsAndroidVersionAtLeast(int major, int minor, int build, int revision)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsAndroidVersionAtLeast({major}, {minor}, {build}, {revision}))";
        string expected = OperatingSystem.IsAndroidVersionAtLeast(major, minor, build, revision) ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsIOS()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsIOS())";

        string expected = OperatingSystem.IsIOS() ? "True" : "False";
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
        string expected = OperatingSystem.IsIOSVersionAtLeast(major, minor, build) ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsMacCatalyst()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsMacCatalyst())";

        string expected = OperatingSystem.IsMacCatalyst() ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(999, 0, 0)]
    public void IsMacCatalystVersionAtLeast(int major, int minor, int build)
    {
        string propertyFunction = $"$([System.OperatingSystem]::IsMacCatalystVersionAtLeast({major}, {minor}, {build}))";
        string expected = OperatingSystem.IsMacCatalystVersionAtLeast(major, minor, build) ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsTvOS()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsTvOS())";

        string expected = OperatingSystem.IsTvOS() ? "True" : "False";
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
        string expected = OperatingSystem.IsTvOSVersionAtLeast(major, minor, build) ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsWatchOS()
    {
        const string propertyFunction = "$([System.OperatingSystem]::IsWatchOS())";

        string expected = OperatingSystem.IsWatchOS() ? "True" : "False";
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
        string expected = OperatingSystem.IsWatchOSVersionAtLeast(major, minor, build) ? "True" : "False";
        ExpandProperties(propertyFunction)
            .ShouldBe(expected);
    }

#endif

    [Theory]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('x', 1))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('x45', 1))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('x', 1, 4))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('x', 9))", "10")] // 9 is not a valid StringComparison enum value
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X', 'StringComparison.Ordinal'))", "-1")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X', 'StringComparison.OrdinalIgnoreCase'))", "0")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X4', 'StringComparison.OrdinalIgnoreCase'))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X4', 1, 'StringComparison.OrdinalIgnoreCase'))", "3")]
    [InlineData("AString", "x12x456789x11", "$(AString.IndexOf('X', 1, 3, 'StringComparison.OrdinalIgnoreCase'))", "3")]
    public void StringIndexOfTests(string propertyName, string properyValue, string propertyFunction, string expectedExpansion)
        => ExpandProperties(propertyFunction, (propertyName, properyValue))
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
        => ExpandProperties(propertyFunction, (propertyName, propertyValue))
            .ShouldBe(expectedExpansion);

    [Theory]
    [InlineData("AString", "linux", "$(AString.Equals($(AString.ToLower()), 'StringComparison.InvariantCulture'))", "True")]
    [InlineData("AString", "Linux", "$(AString.Equals($(AString.ToLower()), 'StringComparison.InvariantCulture'))", "False")]
    [InlineData("AString", "hello", "$(AString.Equals('hello', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "hello", "$(AString.Equals('HELLO', 'StringComparison.OrdinalIgnoreCase'))", "True")]
    [InlineData("AString", "hello", "$(AString.Equals('HELLO', 'StringComparison.Ordinal'))", "False")]
    public void StringEqualsWithStringComparisonTests(string propertyName, string propertyValue, string propertyFunction, string expectedExpansion)
        => ExpandProperties(propertyFunction, (propertyName, propertyValue))
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

    private void AssertThrows(string expression, string expectedMessage)
    {
        InvalidProjectFileException ex = Should.Throw<InvalidProjectFileException>(
            () => ExpandProperties(expression));

        ex.Message.ShouldContain(expectedMessage);
    }

    private void AssertSuccess(object expected, string expression)
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
        string tempPath = FileUtilities.TempFileDirectory;
        string tempFile = Path.GetFileName(FileUtilities.GetTemporaryFile());

        try
        {
            string directoryStart = Path.Combine(tempPath, "one\\two\\three\\four\\five");

            PropertyDictionary<ProjectPropertyInstance> pg = new PropertyDictionary<ProjectPropertyInstance>();
            pg.Set(ProjectPropertyInstance.Create("StartingDirectory", directoryStart));
            pg.Set(ProjectPropertyInstance.Create("FileToFind", tempFile));

            IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg);

            string result = expander.ExpandIntoStringAndUnescape(@"$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), $(FileToFind)))", ExpanderOptions.ExpandProperties, MockElementLocation.Instance);

            FileUtilities.EnsureTrailingSlash(result).ShouldBe(FileUtilities.EnsureTrailingSlash(tempPath));

            result = expander.ExpandIntoStringAndUnescape(@"$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), Hobbits))", ExpanderOptions.ExpandProperties, MockElementLocation.Instance);

            result.ShouldBe(string.Empty);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies that <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> returns the correct path if a file exists
    /// or an empty string if it doesn't.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAbove()
    {
        // Set element location to a file deep in the directory structure.  This is where the function should start looking
        MockElementLocation mockElementLocation = new(Path.Combine(ObjectModelHelpers.TempProjectDir, "one", "two", "three", "four", "five", Path.GetRandomFileName()));

        string fileToFind = FileUtilities.GetTemporaryFile(ObjectModelHelpers.TempProjectDir, null, ".tmp");

        try
        {
            PropertyDictionary<ProjectPropertyInstance> pg = new PropertyDictionary<ProjectPropertyInstance>();
            pg.Set(ProjectPropertyInstance.Create("FileToFind", Path.GetFileName(fileToFind)));

            IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg);

            string result = expander.ExpandIntoStringAndUnescape(@"$([MSBuild]::GetPathOfFileAbove($(FileToFind)))", ExpanderOptions.ExpandProperties, mockElementLocation);

            result.ShouldBe(fileToFind);

            result = expander.ExpandIntoStringAndUnescape(@"$([MSBuild]::GetPathOfFileAbove('Hobbits'))", ExpanderOptions.ExpandProperties, mockElementLocation);

            result.ShouldBe(string.Empty);
        }
        finally
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }
    }

    /// <summary>
    /// Verifies that the usage of GetPathOfFileAbove() within an in-memory project throws an exception.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAboveInMemoryProject()
    {
        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            ObjectModelHelpers.CreateInMemoryProject(
                "<Project><PropertyGroup><foo>$([MSBuild]::GetPathOfFileAbove('foo'))</foo></PropertyGroup></Project>"));

        exception.Message.ShouldStartWith(
            "The expression \"[MSBuild]::GetPathOfFileAbove(foo, \'\')\" cannot be evaluated.");
    }

    /// <summary>
    /// Verifies that <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> only accepts a file name.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAboveFileNameOnly()
    {
        string fileWithPath = Path.Combine("foo", "bar", "file.txt");

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                @"$([MSBuild]::GetPathOfFileAbove($(FileWithPath)))",
                ("FileWithPath", fileWithPath)));

        exception.Message.ShouldContain(
            ResourceUtilities.FormatResourceStringStripCodeAndKeyword("InvalidGetPathOfFileAboveParameter", fileWithPath));
    }

    /// <summary>
    /// Expand property function that calls GetCultureInfo
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetCultureInfo()
        => ExpandProperties(@"$([System.Globalization.CultureInfo]::GetCultureInfo(`en-US`).ToString())")
            .ShouldBe(new CultureInfo("en-US").ToString());

    /// <summary>
    /// Expand property function that calls a static arithmetic method
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodArithmeticAddInt32()
        => ExpandProperties(@"$([MSBuild]::Add(40, 2))")
            .ShouldBe((40 + 2).ToString());

    /// <summary>
    /// Expand property function that calls a static arithmetic method
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodArithmeticAddDouble()
        => ExpandProperties(@"$([MSBuild]::Add(39.9, 2.1))")
            .ShouldBe((39.9 + 2.1).ToString());

    /// <summary>
    /// Expand property function choosing either the value (if not empty) or the default specified
    /// </summary>
    [Fact]
    public void PropertyFunctionValueOrDefault()
    {
        ExpandProperties(@"$([MSBuild]::ValueOrDefault('', '42'))")
            .ShouldBe("42");
        ExpandProperties(@"$([MSBuild]::ValueOrDefault('42', '43'))")
            .ShouldBe("42");
    }

    /// <summary>
    /// Expand property function choosing either the value (from the environment) or the default specified
    /// </summary>
    [Fact]
    public void PropertyFunctionValueOrDefaultFromEnvironment()
    {
        ExpandProperties(
                @"$([MSBuild]::ValueOrDefault('$(DifferentTargetsPath)', '42'))",
                ("DifferentTargetsPath", "Different"))
            .ShouldBe("Different");
        ExpandProperties(
                @"$([MSBuild]::ValueOrDefault('$(DifferentTargetsPath)', '43'))",
                ("DifferentTargetsPath", string.Empty))
            .ShouldBe("43");
    }

#if FEATURE_APPDOMAIN
    /// <summary>
    /// Expand property function that tests for existence of the task host
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist()
        => ExpandProperties(@"$([MSBuild]::DoesTaskHostExist('CurrentRuntime', 'CurrentArchitecture'))")
            .ShouldBe(bool.TrueString);

    /// <summary>
    /// Expand property function that tests for existence of the task host
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Whitespace()
        => ExpandProperties(@"$([MSBuild]::DoesTaskHostExist('   CurrentRuntime    ', 'CurrentArchitecture'))")
            .ShouldBe(bool.TrueString);
#endif

    [Fact]
    public void PropertyFunctionNormalizeDirectory()
    {
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(
            new PropertyDictionary<ProjectPropertyInstance>(
            [
                ProjectPropertyInstance.Create("MyPath", "one"),
                ProjectPropertyInstance.Create("MySecondPath", "two"),
            ]));

        expander.ExpandIntoStringAndUnescape(
                @"$([MSBuild]::NormalizeDirectory($(MyPath)))",
                ExpanderOptions.ExpandProperties,
                MockElementLocation.Instance)
            .ShouldBe($"{Path.GetFullPath("one")}{Path.DirectorySeparatorChar}");

        expander.ExpandIntoStringAndUnescape(
                @"$([MSBuild]::NormalizeDirectory($(MyPath), $(MySecondPath)))",
                ExpanderOptions.ExpandProperties,
                MockElementLocation.Instance)
            .ShouldBe($"{Path.GetFullPath(Path.Combine("one", "two"))}{Path.DirectorySeparatorChar}");
    }

    /// <summary>
    /// Expand property function that tests for existence of the task host
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Error()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(@"$([MSBuild]::DoesTaskHostExist('ASDF', 'CurrentArchitecture'))"));

#if FEATURE_APPDOMAIN
    /// <summary>
    /// Expand property function that tests for existence of the task host
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Evaluated()
        => ExpandProperties(
                @"$([MSBuild]::DoesTaskHostExist('$(Runtime)', '$(Architecture)'))",
                ("Runtime", "CurrentRuntime"),
                ("Architecture", "CurrentArchitecture"))
            .ShouldBe(bool.TrueString);
#endif

#if FEATURE_APPDOMAIN
    /// <summary>
    /// Expand property function that tests for existence of the task host
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
            ExpandProperties(@"$([MSBuild]::DoesTaskHostExist('CLR2', 'CurrentArchitecture'))")
                .ShouldBe(bool.FalseString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME", taskHostName);
            NodeProviderOutOfProcTaskHost.ClearCachedTaskHostPaths();
        }
    }
#endif

    /// <summary>
    /// Expand property function that calls a static bitwise method to retrieve file attribute
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

            ExpandProperties(@"$([MSBuild]::BitwiseAnd(32,$([System.IO.File]::GetAttributes(" + tempFile + "))))")
                .ShouldBe("32");
        }
        finally
        {
            File.SetAttributes(tempFile, FileAttributes.Normal);
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Expand intrinsic property function calls a static arithmetic method
    /// </summary>
    [Fact]
    [UseInvariantCulture]
    public void PropertyFunctionStaticMethodIntrinsicMaths()
    {
        ExpandProperties(@"$([MSBuild]::Add(39.9, 2.1))").ShouldBe((39.9 + 2.1).ToString());
        ExpandProperties(@"$([MSBuild]::Add(40, 2))").ShouldBe((40 + 2).ToString());
        ExpandProperties(@"$([MSBuild]::Subtract(44, 2))").ShouldBe((44 - 2).ToString());
        ExpandProperties(@"$([MSBuild]::Subtract(42.9, 0.9))").ShouldBe((42.9 - 0.9).ToString());
        ExpandProperties(@"$([MSBuild]::Multiply(21, 2))").ShouldBe((21 * 2).ToString());
        ExpandProperties(@"$([MSBuild]::Multiply(84.0, 0.5))").ShouldBe((84.0 * 0.5).ToString());
        ExpandProperties(@"$([MSBuild]::Divide(84, 2))").ShouldBe((84 / 2).ToString());
        ExpandProperties(@"$([MSBuild]::Divide(84.4, 2.0))").ShouldBe((84.4 / 2.0).ToString());
        ExpandProperties(@"$([MSBuild]::Modulo(85, 2))").ShouldBe((85 % 2).ToString());
        ExpandProperties(@"$([MSBuild]::Modulo(2345.5, 43))").ShouldBe((2345.5 % 43).ToString());
    }

    /// <summary>
    /// Expand intrinsic property functions that call a bit operator
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodIntrinsicBitOperations()
    {
        ExpandProperties(@"$([MSBuild]::BitwiseOr(40, 2))").ShouldBe((40 | 2).ToString());
        ExpandProperties(@"$([MSBuild]::BitwiseAnd(42, 2))").ShouldBe((42 & 2).ToString());
        ExpandProperties(@"$([MSBuild]::BitwiseXor(213, 255))").ShouldBe((213 ^ 255).ToString());
        ExpandProperties(@"$([MSBuild]::BitwiseNot(-43))").ShouldBe((~-43).ToString());
        ExpandProperties(@"$([MSBuild]::LeftShift(1, 2))").ShouldBe((1 << 2).ToString());
        ExpandProperties(@"$([MSBuild]::RightShift(-8, 2))").ShouldBe((-8 >> 2).ToString());
        ExpandProperties(@"$([MSBuild]::RightShiftUnsigned(-8, 2))").ShouldBe((-8 >>> 2).ToString());
    }

    /// <summary>
    /// Expand a property reference that has whitespace around the property name (should result in empty)
    /// </summary>
    [Fact]
    public void PropertySimpleSpaced()
        => ExpandProperties(@"$( SomeStuff )", ("SomeStuff", "This IS SOME STUff"))
            .ShouldBe(string.Empty);

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegitryValue()
    {
        try
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            key.SetValue("Value", "%" + envVar + "%", RegistryValueKind.ExpandString);
            ExpandProperties(
                    @"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', '$(SomeProperty)'))",
                    ("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegitryValueDefault()
    {
        try
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            key.SetValue(String.Empty, "%" + envVar + "%", RegistryValueKind.ExpandString);
            ExpandProperties(
                    @"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', null))")
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValueFromView1()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue(string.Empty, $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(
                    @"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', null, null, RegistryView.Default, RegistryView.Default))")
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test");
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValueFromView2()
    {
        try
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test");

            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue(string.Empty, $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(
                    @"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test', null, null, Microsoft.Win32.RegistryView.Default))")
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
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
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();
        PropertyDictionary<ProjectPropertyInstance> pg = new();
        Dictionary<string, string> itemMetadataTable = [with(StringComparer.OrdinalIgnoreCase)];
        itemMetadataTable["Compile.Identity"] = "fOo.Cs";
        StringMetadataTable itemMetadata = new(itemMetadataTable);

        List<ProjectItemInstance> ig = [];
        pg.Set(ProjectPropertyInstance.Create("SomePath", Path.Combine(s_rootPathPrefix, "some", "path")));
        ig.Add(new ProjectItemInstance(project, "Compile", "fOo.Cs", project.FullPath));

        ItemDictionary<ProjectItemInstance> itemsByType = [];
        itemsByType.ImportItems(ig);

        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander = ExpanderFactory.Create(pg, itemsByType, itemMetadata);

        expander.ExpandIntoStringLeaveEscaped(@"$([System.IO.Path]::Combine($(SomePath),%(Compile.Identity)))", ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(Path.Combine(s_rootPathPrefix, "some", "path", "fOo.Cs"));
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
    public void PropertyStringConstructorConsumingItemMetadata(string name, string value)
    {
        var expander = CreateItemFunctionExpander();

        expander.ExpandIntoStringLeaveEscaped($"$([System.String]::new(%({name})))", ExpanderOptions.ExpandAll, MockElementLocation.Instance)
           .ShouldBe(value);
    }

    public static TheoryData<string> GetHashAlgoTypes()
    {
        var data = new TheoryData<string>();

        foreach (var name in Enum.GetNames(typeof(IntrinsicFunctions.StringHashingAlgorithm)))
        {
            data.Add(name);
        }

        data.Add(null);

        return data;
    }

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
            "cat1s",
        ];

        string hashTypeString = hashType == null ? "" : $", '{hashType}'";
        string[] hashes = Array.ConvertAll(stringsToHash, toHash =>
            ExpandProperties($"$([MSBuild]::StableStringHash('{toHash}'{hashTypeString}))"));

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

        string hashTypeString = hashType == null ? "" : $", '{hashType}'";
        ExpandProperties($"$([System.Convert]::GetTypeCode($([MSBuild]::StableStringHash('FooBar'{hashTypeString}))))")
            .ShouldBe(expectedTypeCode.ToString());
    }

    [Theory]
    [InlineData("easycase")]
    [InlineData("")]
    [InlineData("\"\n()\tsdfIR$%#*;==")]
    public void TestBase64Conversion(string testCase)
    {
        string intermediate = ExpandProperties($"$([MSBuild]::ConvertToBase64('{testCase}'))");
        intermediate.Trim('=').All(c => char.IsLetterOrDigit(c) || c is '+' or '/').ShouldBeTrue();
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

    /// <summary>
    ///  A whole bunch error check tests.
    /// </summary>
    [Fact]
    public void Medley()
    {
        // Make absolutely sure that the static method cache hasn't been polluted by the other tests.
        AvailableStaticMembers.Reset_ForUnitTestsOnly();

        PropertyDictionary<ProjectPropertyInstance> properties = new();
        properties.Set(ProjectPropertyInstance.Create("File", @"foo\file.txt"));

        properties.Set(ProjectPropertyInstance.Create("a", "no"));
        properties.Set(ProjectPropertyInstance.Create("b", "true"));
        properties.Set(ProjectPropertyInstance.Create("c", "1"));
        properties.Set(ProjectPropertyInstance.Create("position", "4"));
        properties.Set(ProjectPropertyInstance.Create("d", "xxx"));
        properties.Set(ProjectPropertyInstance.Create("e", "xxx"));
        properties.Set(ProjectPropertyInstance.Create("and", "and"));
        properties.Set(ProjectPropertyInstance.Create("a_semi_b", "a;b"));
        properties.Set(ProjectPropertyInstance.Create("a_apos_b", "a'b"));
        properties.Set(ProjectPropertyInstance.Create("foo_apos_foo", "foo'foo"));
        properties.Set(ProjectPropertyInstance.Create("a_escapedsemi_b", "a%3bb"));
        properties.Set(ProjectPropertyInstance.Create("a_escapedapos_b", "a%27b"));
        properties.Set(ProjectPropertyInstance.Create("has_trailing_slash", @"foo\"));
        properties.Set(ProjectPropertyInstance.Create("emptystring", @""));
        properties.Set(ProjectPropertyInstance.Create("space", @" "));
        properties.Set(ProjectPropertyInstance.Create("listofthings", @"a;b;c;d;e;f;g;h;i;j;k;l"));
        properties.Set(ProjectPropertyInstance.Create("input", @"EXPORT a"));
        properties.Set(ProjectPropertyInstance.Create("propertycontainingnullasastring", @"null"));

        List<(string input, string result)> validTests = [
            ("$(input.ToString()[1])", "X"),
            ("$(input[1])", "X"),
            ("$(listofthings.Split(';')[$(position)])","e"),
            (@"$([System.Text.RegularExpressions.Regex]::Match($(Input), `EXPORT\s+(.+)`).Groups[1].Value)","a"),
            ("$([MSBuild]::Add(1,2).CompareTo(3))", "0"),
            ("$([MSBuild]::Add(1,2).CompareTo(3))", "0"),
            ("$([MSBuild]::Add(1,2).CompareTo(3.0))", "0"),
            ("$([MSBuild]::Add(1,2.0).CompareTo(3.0))", "0"),
            ("$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).CompareTo(3.0))", "0"),
            ("$([MSBuild]::Add(1,2).CompareTo('3'))", "0"),
            ("$([MSBuild]::Add(1,2).CompareTo(3.1))", "-1"),
            ("$([MSBuild]::Add(1,2.0).CompareTo(3.1))", "-1"),
            ("$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).CompareTo(3.1))", "-1"),
            ("$([MSBuild]::Add(1,2).CompareTo(2))", "1"),
            ("$([MSBuild]::Add(1,2).Equals(3))", "True"),
            ("$([MSBuild]::Add(1,2).Equals(3.0))", "True"),
            ("$([MSBuild]::Add(1,2.0).Equals(3.0))", "True"),
            ("$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).Equals(3.0))", "True"),
            ("$([MSBuild]::Add(1,2).Equals('3'))", "True"),
            ("$([MSBuild]::Add(1,2).Equals(3.1))", "False"),
            ("$([MSBuild]::Add(1,2.0).Equals(3.1))", "False"),
            ("$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).Equals(3.1))", "False"),
            ("$(a.Insert(0,'%28'))", "%28no"),
            ("$(a.Insert(0,'\"'))", "\"no"),
            ("$(a.Insert(0,'(('))", "%28%28no"),
            ("$(a.Insert(0,'))'))", "%29%29no"),
            ("A$(Reg:A)A", "AA"),
            ("A$(Reg:AA)", "A"),
            ("$(Reg:AA)", ""),
            ("$(Reg:AAAA)", ""),
            ("$(Reg:AAA)", ""),
            ("$([MSBuild]::Add(2,$([System.Convert]::ToInt64('28', 16))))", "42"),
            ("$([MSBuild]::Add(2,$([System.Convert]::ToInt64('28', $([System.Convert]::ToInt32(16))))))", "42"),
            ("$(e.Length.ToString())", "3"),
            ("$(e.get_Length().ToString())", "3"),
            ("$(emptystring.Length)", "0"),
            ("$(space.Length)", "1"),
            ("$([System.TimeSpan]::Equals(null, null))", "True"), // constant, unquoted null is a special value
            ("$([MSBuild]::Add(40,null))", "40"),
            ("$([MSBuild]::Add( 40 , null ))", "40"),
            ("$([MSBuild]::Add(null,40))", "40"),
            ("$([MSBuild]::Escape(';'))", "%3b"),
            ("$([MSBuild]::UnEscape('%3b'))", ";"),
            ("$(e.Substring($(e.Length)))", ""),
            ("$([System.Int32]::MaxValue)", int.MaxValue.ToString()),
            ("x$()", "x"),
            ("A$(Reg:A)A", "AA"),
            ("A$(Reg:AA)", "A"),
            ("$(Reg:AA)", ""),
            ("$(Reg:AAAA)", ""),
            ("$(Reg:AAA)", ""),

            // Following two are comparison between non-numeric and numeric properties. More details: #10583
            ("$(a.Equals($(c)))","False"),
            ("$(a.CompareTo($(c)))","1"),
        ];

        List<string> errorTests = [
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
            "()",
        ];

#if !RUNTIME_TYPE_NETCORE
        if (NativeMethodsShared.IsWindows)
        {
            // '|' is only an invalid character in Windows filesystems
            errorTests.Add("$([System.IO.Path]::Combine(`|`,`b`))");
        }
#endif

        if (NativeMethodsShared.IsWindows)
        {
            errorTests.Add("$(Registry:X)");
        }

        if (!NativeMethodsShared.IsWindows)
        {
            // If no registry or not running on windows, this gets expanded to the empty string
            // example: xplat build running on OSX
            validTests.Add(("$(Registry:X)", ""));
        }

        foreach (var (input, result) in validTests)
        {
            ExpandProperties(input, properties)
                .ShouldBe(result, $"FAILURE: {input} expanded to '{ExpandProperties(input, properties)}' instead of '{result}'");
        }

        for (int i = 0; i < errorTests.Count; i++)
        {
            // If an expression is invalid,
            //      - Expansion may throw InvalidProjectFileException, or
            //      - return the original unexpanded expression
            bool success = true;
            bool caughtException = false;
            string result = string.Empty;
            try
            {
                result = ExpandProperties(errorTests[i], properties);
                if (result == errorTests[i])
                {
                    Console.WriteLine($"{errorTests[i]} did not expand.");
                    success = false;
                }
            }
            catch (InvalidProjectFileException ex)
            {
                Console.WriteLine($"{errorTests[i]} caused '{ex.Message}'");
                caughtException = true;
            }

            (!success || caughtException).ShouldBeTrue(
                $"FAILURE: Expected '{errorTests[i]}' to not parse or not be evaluated but it evaluated to '{result}'");
        }
    }

    [Fact]
    public void PropertyFunctionEnsureTrailingSlash()
    {
        string path = Path.Combine("foo", "bar");

        // Verify a constant expands properly
        ExpandProperties($"$([MSBuild]::EnsureTrailingSlash('{path}'))")
            .ShouldBe(path + Path.DirectorySeparatorChar);

        // Verify that a property expands properly
        ExpandProperties("$([MSBuild]::EnsureTrailingSlash($(SomeProperty)))", ("SomeProperty", path))
            .ShouldBe(path + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void PropertyFunctionWithNewLines()
    {
        const string Expression = """
            $(SomeProperty
             .Substring(0, 10)
              .ToString()
               .Substring(0, 5)
                 .ToString())
            """;

        ExpandProperties(Expression, ("SomeProperty", "6C8546D5297C424F962201B0E0E9F142"))
            .ShouldBe("6C854");
    }

    [Fact]
    public void PropertyFunctionStringIndexOfAny()
        => ExpandProperties("$(prop.IndexOfAny('y'))", ("prop", "x-y-z"))
            .ShouldBe("2");

    [Fact]
    public void PropertyFunctionStringLastIndexOf()
    {
        ExpandProperties("$(prop.LastIndexOf('y'))", ("prop", "x-x-y-y-y-z"))
            .ShouldBe("8");
        ExpandProperties("$(prop.LastIndexOf('y', 7))", ("prop", "x-x-y-y-y-z"))
            .ShouldBe("6");
    }

    [Fact]
    public void PropertyFunctionStringLastIndexOfAny()
        => ExpandProperties("$(prop.LastIndexOfAny('xy'))", ("prop", "x-x-y-y-y-z"))
            .ShouldBe("8");

    [Fact]
    public void PropertyFunctionStringCopy()
    {
        const string Expression = """
            $([System.String]::Copy($(X)).LastIndexOf(
                '.designer.cs',
                System.StringComparison.OrdinalIgnoreCase))
            """;

        ExpandProperties(Expression, ("X", "test.designer.cs"))
            .ShouldBe("4");
    }

    [Fact]
    public void PropertyFunctionVersionParse()
        => ExpandProperties(@"$([System.Version]::Parse('$(X)').ToString(1))", ("X", "4.0"))
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
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetVsInstallRoot())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetVsInstallRoot() ?? string.Empty));

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
        => ExpandProperties("$(prop.Split('-')[0])", ("prop", "x-y-z"))
            .ShouldBe("x");

    [Fact]
    public void PropertyFunctionSubstring1()
        => ExpandProperties("$(prop.Substring(2))", ("prop", "abcdef"))
            .ShouldBe("cdef");

    [Fact]
    public void PropertyFunctionSubstring2()
        => ExpandProperties("$(prop.Substring(2, 3))", ("prop", "abcdef"))
            .ShouldBe("cde");

    [Fact]
    public void PropertyFunctionStringGetChars()
        => ExpandProperties("$(prop[0])", ("prop", "461"))
            .ShouldBe("4");

    [Fact]
    public void PropertyFunctionStringGetCharsError()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(prop[5])", ("prop", "461")));

    [Fact]
    public void PropertyFunctionStringPadLeft1()
        => ExpandProperties("$(prop.PadLeft(2))", ("prop", "x"))
            .ShouldBe(" x");

    [Fact]
    public void PropertyFunctionStringPadLeft2()
        => ExpandProperties("$(prop.PadLeft(2, '0'))", ("prop", "x"))
            .ShouldBe("0x");

    [Fact]
    public void PropertyFunctionStringPadLeftComplex()
        => ExpandProperties("$(prop.PadLeft($([MSBuild]::Multiply(1, 2)), '0'))", ("prop", "x"))
            .ShouldBe("0x");

    [Fact]
    public void PropertyFunctionStringPadLeftChar()
        => ExpandProperties("$(VersionSuffixBuildOfTheDay.PadLeft(3, $([System.Convert]::ToChar(`0`))))", ("VersionSuffixBuildOfTheDay", "4"))
            .ShouldBe("004");

    [Fact]
    public void PropertyFunctionStringPadRight1()
        => ExpandProperties("$(prop.PadRight(2))", ("prop", "x"))
            .ShouldBe("x ");

    [Fact]
    public void PropertyFunctionStringPadRight2()
        => ExpandProperties("$(prop.PadRight(2, '0'))", ("prop", "x"))
            .ShouldBe("x0");

    [Fact]
    public void PropertyFunctionStringTrimEndCharArray()
        => ExpandProperties("$(prop.TrimEnd('.0123456789'))", ("prop", "net461"))
            .ShouldBe("net");

    [Fact]
    public void PropertyFunctionStringTrimStart()
        => ExpandProperties("$(X.TrimStart('vV'))", ("X", "v40"))
            .ShouldBe("40");

    [Fact]
    public void PropertyFunctionStringTrimStartNoQuotes()
        => ExpandProperties("$(X.TrimStart(vV))", ("X", "v40"))
            .ShouldBe("40");

    [Fact]
    public void PropertyFunctionStringTrimEnd1()
        => ExpandProperties("$(prop.TrimEnd('a'))", ("prop", "netaa"))
            .ShouldBe("net");

    // https://github.com/dotnet/msbuild/issues/2882
    [Fact]
    public void PropertyFunctionMathMaxOverflow()
        => ExpandProperties("$([System.Math]::Max($(X), 0))", ("X", "-2010"))
            .ShouldBe("0");

    [Fact]
    public void PropertyFunctionStringTrimEnd2()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(prop.TrimEnd('a', 'b'))", ("prop", "stringab")));

    [Fact]
    public void PropertyFunctionMathMin()
        => ExpandProperties("$([System.Math]::Min($(X), 20))", ("X", "30"))
            .ShouldBe("20");

    [Fact]
    public void PropertyFunctionMSBuildAddIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Add($(X), 5))", ("X", "7"))
            .ShouldBe("12");

    [Fact]
    public void PropertyFunctionMSBuildAddRealLiteral()
        => ExpandProperties("$([MSBuild]::Add($(X), 0.5))", ("X", "7"))
            .ShouldBe("7.5");

    [Fact]
    public void PropertyFunctionMSBuildAddIntegerOverflow()
        // Overflow wrapping - result exceeds size of long
        => ExpandProperties("$([MSBuild]::Add($(X), 1))", ("X", long.MaxValue.ToString()))
            .ShouldBe("-9223372036854775808");

    [Fact]
    [UseInvariantCulture]
    public void PropertyFunctionMSBuildAddRealArgument()
    {
        // string argument is an integer that exceeds the size of long.
        double value = long.MaxValue + 1.0;
        double expected = value + 1.0;
        ExpandProperties("$([MSBuild]::Add($(X), 1))", ("X", value.ToString()))
            .ShouldBe(expected.ToString());
    }

    [Fact]
    public void PropertyFunctionMSBuildAddComplex()
        => ExpandProperties("$([MSBuild]::Add($(X), $([MSBuild]::Add(2, 3))))", ("X", "7"))
            .ShouldBe("12");

    [Fact]
    public void PropertyFunctionMSBuildSubtractIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Subtract($(X), 20100000))", ("X", "20100042"))
            .ShouldBe("42");

    [Fact]
    public void PropertyFunctionMSBuildSubtractRealLiteral()
        => ExpandProperties("$([MSBuild]::Subtract($(X), 20100000.0))", ("X", "20100042"))
            .ShouldBe("42");

    [Fact]
    public void PropertyFunctionMSBuildSubtractIntegerMaxValue()
        // If the double overload is used, there will be a rounding error.
        => ExpandProperties("$([MSBuild]::Subtract($(X), 9223372036854775806))", ("X", long.MaxValue.ToString()))
            .ShouldBe("1");

    [Fact]
    public void PropertyFunctionMSBuildMultiplyIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Multiply($(X), 8800))", ("X", "2"))
            .ShouldBe("17600");

    [Fact]
    public void PropertyFunctionMSBuildMultiplyRealLiteral()
        => ExpandProperties("$([MSBuild]::Multiply($(X), 1.5))", ("X", "2"))
            .ShouldBe("3");

    [Fact]
    public void PropertyFunctionMSBuildMultiplyIntegerOverflow()
        // Overflow - result exceeds size of long
        => ExpandProperties("$([MSBuild]::Multiply($(X), 2))", ("X", long.MaxValue.ToString()))
            .ShouldBe("-2");

    [Fact]
    public void PropertyFunctionMSBuildMultiplyComplex()
        => ExpandProperties("$([MSBuild]::Multiply($(X), $([MSBuild]::Multiply(1, 8800))))", ("X", "2"))
            .ShouldBe("17600");

    [Fact]
    public void PropertyFunctionMSBuildDivideIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Divide($(X), 10000))", ("X", "65536"))
            .ShouldBe("6");

    [Fact]
    public void PropertyFunctionMSBuildDivideRealLiteral()
        => ExpandProperties("$([MSBuild]::Divide($(X), 10000.0))", ("X", "65536"))
            .ShouldBe("6.5536");

    [Fact]
    public void PropertyFunctionMSBuildModuloIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Modulo($(X), 3))", ("X", "10"))
            .ShouldBe("1");

    [Fact]
    public void PropertyFunctionMSBuildModuloRealLiteral()
        => ExpandProperties("$([MSBuild]::Modulo($(X), 3.0))", ("X", "10"))
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
        => ExpandProperties("$(X.Split($([System.Convert]::ToString(`.`).ToCharArray())).GetValue($([System.Convert]::ToInt32(0))))", ("X", "ab.cd"))
            .ShouldBe("ab");

    /// <summary>
    ///  Test that Char.IsDigit fast-path works correctly.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="expected">Expected result.</param>
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
        => ExpandProperties(expression)
            .ShouldBe(expected);

    /// <summary>
    ///  Regression test for https://github.com/dotnet/msbuild/issues/12923.
    /// </summary>
    [Fact]
    public void PropertyFunction_ReplaceDoesNotCallRegexReplace()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$([System.TimeSpan]::Replace('abc_123_ghi', '\\d+', 'def'))").ShouldNotBe("abc_def_ghi"));

    private string ExpandProperties(string expression)
        => ExpandProperties(expression, allowReflection: true, propertyProvider: null);

    private string ExpandProperties(string expression, params (string Name, string Value)[] properties)
        => ExpandProperties(expression, allowReflection: true, properties);

    private string ExpandProperties(string expression, bool allowReflection, params (string Name, string Value)[] properties)
    {
        var propertyDict = new PropertyDictionary<ProjectPropertyInstance>();

        foreach ((string name, string value) in properties)
        {
            propertyDict.Set(ProjectPropertyInstance.Create(name, value));
        }

        return ExpandProperties(expression, allowReflection, propertyDict);
    }

    private string ExpandProperties(string expression, bool allowReflection)
        => ExpandProperties(expression, allowReflection, propertyProvider: null);

    private string ExpandProperties(string expression, LoggingContext loggingContext)
        => ExpandProperties(expression, allowReflection: true, propertyProvider: null, loggingContext);

    private string ExpandProperties(string expression, IPropertyProvider<ProjectPropertyInstance> propertyProvider)
        => ExpandProperties(expression, allowReflection: true, propertyProvider);

    private string ExpandProperties(
        string expression,
        bool allowReflection,
        IPropertyProvider<ProjectPropertyInstance> propertyProvider,
        LoggingContext loggingContext = null)
    {
        using var env = TestEnvironment.Create();

        // Setting this env variable allows to track if expander was using reflection for a function invocation.
        env.SetEnvironmentVariable("MSBuildLogPropertyFunctionsRequiringReflection", "1");

        if (loggingContext is null)
        {
            var logger = new MockLogger();
            ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            loggingService.RegisterLogger(logger).ShouldBeTrue();
            loggingContext = new MockLoggingContext(
                loggingService,
                new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));
        }

        propertyProvider ??= new PropertyDictionary<ProjectPropertyInstance>();

        var expander = ExpanderFactory.Create(propertyProvider, loggingContext);
        string reflectionInfoPath = Path.Combine(Directory.GetCurrentDirectory(), "PropertyFunctionsRequiringReflection");

        try
        {
            string result = expander.ExpandIntoStringLeaveEscaped(expression, ExpanderOptions.ExpandProperties, MockElementLocation.Instance);

            bool exists = File.Exists(reflectionInfoPath);

            if (exists)
            {
                string[] lines = File.ReadAllLines(reflectionInfoPath);

                if (lines.Length > 0)
                {
                    _output.WriteLine("Property functions invoked with reflection:");

                    foreach (string line in lines)
                    {
                        _output.WriteLine(line);
                    }

                    allowReflection.ShouldBeTrue(
                        $"{lines[0]} {(lines.Length > 1 ? $"(and {lines.Length - 1} other property functions) were" : "was")} invoked with reflection.");
                }
            }

            return result;
        }
        finally
        {
            if (File.Exists(reflectionInfoPath))
            {
                File.Delete(reflectionInfoPath);
            }
        }
    }

    [Theory]
    [InlineData("net6.0", "netstandard2.0", "")]
    [InlineData("net6.0-windows", "netstandard2.0", "")]
    [InlineData("net6.0-windows", "net6.0", "net6.0-windows")]
    [InlineData("netstandard2.0;net6.0", "net6.0", "net6.0")]
    [InlineData("netstandard2.0;net6.0-windows", "net6.0", "net6.0-windows")]
    [InlineData("netstandard2.0;net6.0-windows", "net6.0;netstandard2.0;net472", "netstandard2.0%3bnet6.0-windows")]
    [InlineData("netstandard2.0;net472", "net6.0;netstandard2.0;net472", "netstandard2.0%3bnet472")]
    public void PropertyFunctionFilterTargetFrameworks(string incoming, string filter, string expected)
        => ExpandProperties($"$([MSBuild]::FilterTargetFrameworks('{incoming}', '{filter}'))").ShouldBe(expected);

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
        using (var env = TestEnvironment.Create())
        {
            var root = env.CreateFolder();

            var alpha = root.CreateDirectory("alpha");
            var projectFile = env.CreateFile(alpha, ".proj", """
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
                """);

            var beta = alpha.CreateDirectory("beta");
            var gamma = alpha.CreateDirectory("gamma");

            ProjectInstance projectInstance = new ProjectInstance(projectFile.Path);
            ICollection<ProjectItemInstance> myDirectories = projectInstance.GetItems("MyDirectories");

            var includes = myDirectories.Select(i => i.EvaluatedInclude);
            includes.ShouldBeUnique();
            includes.ShouldContain(root.Path);
            includes.ShouldContain(alpha.Path);
            includes.ShouldContain(beta.Path);
            includes.ShouldNotContain(gamma.Path);
        }
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
        using (var env = TestEnvironment.Create())
        {
            var root = env.CreateFolder();

            var alpha = root.CreateDirectory("alpha");
            var projectFile = env.CreateFile(alpha, ".proj", """
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
                """);

            var beta = alpha.CreateDirectory("beta");
            var gamma = root.CreateDirectory("gamma");

            ProjectInstance projectInstance = new ProjectInstance(projectFile.Path);
            ICollection<ProjectItemInstance> myDirectories = projectInstance.GetItems("MyDirectories");

            var includes = myDirectories.Select(i => i.EvaluatedInclude);
            includes.ShouldBeUnique();
            includes.ShouldContain(root.Path);
            includes.ShouldContain(alpha.Path);
            includes.ShouldContain(beta.Path);
            includes.ShouldContain(gamma.Path);
        }
    }

    [Fact]
    public void ExpandItemVectorFunctions_Combine()
    {
        using (var env = TestEnvironment.Create())
        {
            var root = env.CreateFolder();

            var projectFile = env.CreateFile(root, ".proj", """
                <Project>
                  <ItemGroup>
                    <MyDirectory Include="Alpha;Beta;Alpha\Gamma" />
                  </ItemGroup>
                  <ItemGroup>
                    <Squiggle Include="@(MyDirectory->Combine('.squiggle'))" />
                  </ItemGroup>
                </Project>
                """);

            ProjectInstance projectInstance = new ProjectInstance(projectFile.Path);
            ICollection<ProjectItemInstance> squiggles = projectInstance.GetItems("Squiggle");

            var expectedAlphaSquigglePath = Path.Combine("Alpha", ".squiggle");
            var expectedBetaSquigglePath = Path.Combine("Beta", ".squiggle");
            var expectedAlphaGammaSquigglePath = Path.Combine("Alpha", "Gamma", ".squiggle");
            squiggles.Select(i => i.EvaluatedInclude).ShouldBe(
            [
                expectedAlphaSquigglePath,
                expectedBetaSquigglePath,
                expectedAlphaGammaSquigglePath
            ],
            Case.Insensitive);
        }
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
        using (var env = TestEnvironment.Create())
        {
            var root = env.CreateFolder();

            var projectFile = env.CreateFile(root, ".proj", """
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
                """);

            var alpha = root.CreateDirectory("alpha");
            var one = alpha.CreateFile("One.cs");
            var three = alpha.CreateFile("Three.cs");

            ProjectInstance projectInstance = new ProjectInstance(projectFile.Path);
            ICollection<ProjectItemInstance> squiggleItems = projectInstance.GetItems("Compile");

            var alphaOnePath = Path.Combine("alpha", "One.cs");
            var alphaThreePath = Path.Combine("alpha", "Three.cs");
            squiggleItems.Select(i => i.EvaluatedInclude).ShouldBe(new[] { alphaOnePath, alphaThreePath }, Case.Insensitive);
        }
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
        using (var env = TestEnvironment.Create())
        {
            var root = env.CreateFolder();

            var projectFile = env.CreateFile(root, ".proj", """
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
                """);

            var alpha = root.CreateDirectory("alpha");
            var beta = alpha.CreateDirectory("beta");
            var delta = alpha.CreateDirectory("delta");

            ProjectInstance projectInstance = new ProjectInstance(projectFile.Path);
            ICollection<ProjectItemInstance> squiggleItems = projectInstance.GetItems("MyDirectory");

            var alphaBetaPath = Path.Combine("alpha", "beta");
            var alphaDeltaPath = Path.Combine("alpha", "delta");
            squiggleItems.Select(i => i.EvaluatedInclude).ShouldBe([alphaBetaPath, alphaDeltaPath], Case.Insensitive);
        }
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
            using (var env = TestEnvironment.Create())
            {
                currentThread.CurrentCulture = svSECultureInfo;
                currentThread.CurrentUICulture = svSECultureInfo;
                var root = env.CreateFolder();

                var projectFile = env.CreateFile(root, ".proj", """
                    <Project>
                      <PropertyGroup>
                        <_value>$([MSBuild]::Subtract(0, 1))</_value>
                        <_otherValue Condition="'$(_value)' &gt;= -1">test-value</_otherValue>
                      </PropertyGroup>
                      <Target Name="Build" />
                    </Project>
                    """);
                ProjectInstance projectInstance = new ProjectInstance(projectFile.Path);
                projectInstance.GetPropertyValue("_value").ShouldBe("-1");
                projectInstance.GetPropertyValue("_otherValue").ShouldBe("test-value");
            }
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

            using (var env = TestEnvironment.Create())
            {
                var root = env.CreateFolder();

                var projectFile = env.CreateFile(root, ".proj", $"""
                    <Project>
                        <PropertyGroup>
                            <foo>aa</foo>
                            <typeval>$(foo.{methodName}().FullName)</typeval>
                        </PropertyGroup>
                    </Project>
                    """);

                var exception = Should.Throw<InvalidProjectFileException>(() =>
                {
                    new ProjectInstance(projectFile.Path);
                });

                exception.BaseMessage.ShouldContain($"The function \"{methodName}\" on type \"System.String\" is not available for execution as an MSBuild property function.");
            }
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
        using (var env = TestEnvironment.Create())
        {
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

            var projectFile = env.CreateFile(root, ".proj", $"""
                <Project>
                    <PropertyGroup>
                        <foo>aa</foo>
                        <typeval>$(foo.{methodName}().FullName)</typeval>
                    </PropertyGroup>
                </Project>
                """);

            Should.NotThrow(() =>
            {
                new ProjectInstance(projectFile.Path);
            });
        }
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
        => ExpandProperties(expression, allowReflection: false);

    [Fact]
    public void PropertyFunctionRegisterBuildCheck()
    {
        using (var env = TestEnvironment.Create())
        {
            var logger = new MockLogger();
            ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
            loggingService.RegisterLogger(logger);
            var loggingContext = new MockLoggingContext(
                loggingService,
                new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));
            var dummyAssemblyFile = env.CreateFile(env.CreateFolder(), "test.dll");

            ExpandProperties($"$([MSBuild]::RegisterBuildCheck({dummyAssemblyFile.Path}))", loggingContext)
                .ShouldBe(bool.TrueString);

            foreach (var buildEvent in logger.AllBuildEvents)
            {
                buildEvent.ShouldBeOfType<BuildCheckAcquisitionEventArgs>();
            }

            logger.AllBuildEvents.Count.ShouldBe(1);
        }
    }

    /// <summary>
    /// Test for issue where chained item functions with empty results incorrectly evaluate as non-empty in conditions
    /// </summary>
    [Fact]
    public void ChainedItemFunctionEmptyResultInCondition()
    {
        string content = """
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

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        // The chained WithMetadataValue should return empty, so the condition should be true and EmptyResult should be set
        log.AssertLogContains("EmptyResult=TRUE");
    }

    #region System.IO.File/Directory relative path resolution in -mt mode

    /// <summary>
    /// TransientTestState that saves/restores <see cref="FileUtilities.CurrentThreadWorkingDirectory"/>.
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

        public override void Revert() => AppContext.SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", _original);
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
    private string ExpandWithThreadWorkingDirectory(TestEnvironment env, string expression, string workingDir, string wrongDir = null)
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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::NormalizePath('obj', 'file.txt'))", correctDir.Path, wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_ParentSegment_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::NormalizePath('obj', '..', 'file.txt'))", correctDir.Path, wrongDir.Path);

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
        string result = ExpandWithThreadWorkingDirectory(env,
            @"$([MSBuild]::NormalizePath('\tmp\file.txt'))", projectDirPath, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            @"$([MSBuild]::NormalizePath('obj\..\file.txt'))", projectDirPath, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            @"$([MSBuild]::FileExists('sub\marker.txt'))", projectDir.Path, wrongDir.Path);

        result.ShouldBe(expected);
    }

    [WindowsOnlyFact]
    public void NormalizePath_DriveRelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);
        string drive = Path.GetPathRoot(correctDir.Path).Substring(0, 2);

        string result = ExpandWithThreadWorkingDirectory(env,
            $"$([MSBuild]::NormalizePath('{drive}obj', 'file.txt'))", correctDir.Path, wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_AbsolutePath_IgnoresThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var absoluteDir = env.CreateFolder(createFolder: true);

        string absolutePath = Path.Combine(absoluteDir.Path, "file.txt");
        string result = ExpandWithThreadWorkingDirectory(env,
            $"$([MSBuild]::NormalizePath('{absolutePath}'))", correctDir.Path);

        result.ShouldBe(absolutePath);
    }

    [Fact]
    public void NormalizePath_WithoutThreadWorkingDirectory_UsesProcessWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var processDir = env.CreateFolder(createFolder: true);
        string processDirPath = SetCurrentDirectoryCanonical(env, processDir.Path);

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::NormalizePath('obj', 'file.txt'))", null);

        result.ShouldBe(Path.Combine(processDirPath, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_EmptyPath_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => IntrinsicFunctions.NormalizePath([]));
    }

    [Fact]
    public void NormalizePath_NullPathArray_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => IntrinsicFunctions.NormalizePath((string[])null));
    }

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::NormalizeDirectory('obj'))", correctDir.Path, wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj") + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void MSBuildFileExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::FileExists('marker.txt'))", correctDir.Path, wrongDir.Path)
            .ShouldBe("True");
        ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::FileExists('absent.txt'))", correctDir.Path, wrongDir.Path)
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

        string result = ExpandWithThreadWorkingDirectory(env,
            @"$([MSBuild]::FileExists('\tmp\decoy.txt'))", projectDir.Path, wrongDir.Path);

        result.ShouldBe(expected);
    }

    [Fact]
    public void MSBuildDirectoryExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "obj"));

        ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::DirectoryExists('obj'))", correctDir.Path, wrongDir.Path)
            .ShouldBe("True");
        ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::DirectoryExists('absent'))", correctDir.Path, wrongDir.Path)
            .ShouldBe("False");
    }

    [Fact]
    public void GetDirectoryNameOfFileAbove_RelativeStartingDirectory_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::GetDirectoryNameOfFileAbove('.', 'marker.txt'))", correctDir.Path, wrongDir.Path);

        result.ShouldBe(correctDir.Path);
    }

    [Fact]
    public void GetPathOfFileAbove_RelativeStartingDirectory_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([MSBuild]::GetPathOfFileAbove('marker.txt', '.'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::ReadAllText('notes.txt'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            @"$([MSBuild]::BitwiseAnd(32,$([System.IO.File]::GetAttributes('attrs.txt'))))",
            correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::GetCreationTime('time.txt'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::GetLastWriteTime('time.txt'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::GetLastAccessTime('time.txt'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.Directory]::Exists('subdir'))", correctDir.Path, wrongDir.Path);

        result.ShouldBe("True");
    }

    [Fact]
    public void DirectoryGetParent_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "parent", "child"));

        string result = ExpandWithThreadWorkingDirectory(env,
            @"$([System.IO.Directory]::GetParent('parent\child'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.Directory]::GetFiles('sub'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.Directory]::GetDirectories('parent'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.Directory]::GetLastWriteTime('sub'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.Directory]::GetLastAccessTime('sub'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::ReadAllLines('lines.txt'))", correctDir.Path, wrongDir.Path);

        result.ShouldContain("line1");
    }

    [Fact]
    public void FileReadAllBytes_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllBytes(Path.Combine(correctDir.Path, "data.bin"), [0x42]);

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::ReadAllBytes('data.bin'))", correctDir.Path, wrongDir.Path);

        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void FileWriteAllText_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.WithTransientTestState(new TransientEnableAllPropertyFunctions());
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::WriteAllText('output.txt', 'hello'))", correctDir.Path, wrongDir.Path);

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

        ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::AppendAllText('append.txt', ' added'))", correctDir.Path, wrongDir.Path);

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

        ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::Delete('todelete.txt'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::GetCreationTimeUtc('utc.txt'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::GetLastWriteTimeUtc('utc.txt'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::GetLastAccessTimeUtc('utc.txt'))", correctDir.Path, wrongDir.Path);

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

        ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.Directory]::CreateDirectory('newdir'))", correctDir.Path, wrongDir.Path);

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

        ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.Directory]::Delete('todel'))", correctDir.Path, wrongDir.Path);

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
        string result = ExpandWithThreadWorkingDirectory(env,
            $"$([System.IO.File]::ReadAllText('{filePath}'))", null);

        result.ShouldBe("absolute content");
    }

    [Fact]
    public void FileExists_AbsolutePath_WorksWithoutThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var dir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(dir.Path, "abs.txt");
        File.WriteAllText(filePath, "data");

        string result = ExpandWithThreadWorkingDirectory(env,
            $"$([System.IO.File]::Exists('{filePath}'))", null);

        result.ShouldBe("True");
    }

    [Fact]
    public void DirectoryExists_AbsolutePath_WorksWithoutThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var dir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(dir.Path, "subdir");
        Directory.CreateDirectory(subDir);

        string result = ExpandWithThreadWorkingDirectory(env,
            $"$([System.IO.Directory]::Exists('{subDir}'))", null);

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
        string result = ExpandWithThreadWorkingDirectory(env,
            $"$([System.IO.File]::ReadAllText('{absFile}'))", correctDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            $"$([System.IO.File]::Exists('{absFile}'))", correctDir.Path);

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

        ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::Copy('source.txt', 'dest.txt'))", correctDir.Path, wrongDir.Path);

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

        ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.File]::Move('movesrc.txt', 'movedst.txt'))", correctDir.Path, wrongDir.Path);

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

        ExpandWithThreadWorkingDirectory(env,
            "$([System.IO.Directory]::Move('dirsrc', 'dirdst'))", correctDir.Path, wrongDir.Path);

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

        string result = ExpandWithThreadWorkingDirectory(env,
            @"$([System.IO.File]::ReadAllText('../sibling/file.txt'))", subDir, wrongDir.Path);

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
        string result = ExpandWithThreadWorkingDirectory(env,
            @"$([System.IO.File]::ReadAllText('..\\sibling\\file.txt'))", subDir, wrongDir.Path);

        result.ShouldBe("backslash traversal works");
    }

    #endregion
}
