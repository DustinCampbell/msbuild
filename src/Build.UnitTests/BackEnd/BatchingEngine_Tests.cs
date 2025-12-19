// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Xunit;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ProjectItemInstanceFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.ProjectItemInstanceFactory;

namespace Microsoft.Build.UnitTests.BackEnd;

public class BatchingEngine_Tests
{
    [Fact]
    public void GetBuckets()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();

        var itemsByType = new ItemDictionary<ProjectItemInstance>();

        itemsByType.ImportItems([
            new ProjectItemInstance(project, "File", "a.foo", project.FullPath),
            new ProjectItemInstance(project, "File", "b.foo", project.FullPath),
            new ProjectItemInstance(project, "File", "c.foo", project.FullPath),
            new ProjectItemInstance(project, "File", "d.foo", project.FullPath),
            new ProjectItemInstance(project, "File", "e.foo", project.FullPath),
        ]);

        itemsByType.ImportItems([
            new ProjectItemInstance(project, "Doc", "a.doc", project.FullPath),
            new ProjectItemInstance(project, "Doc", "b.doc", project.FullPath),
            new ProjectItemInstance(project, "Doc", "c.doc", project.FullPath),
            new ProjectItemInstance(project, "Doc", "d.doc", project.FullPath),
            new ProjectItemInstance(project, "Doc", "e.doc", project.FullPath)
        ]);

        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("UnitTests", "unittests.foo"));
        properties.Set(ProjectPropertyInstance.Create("OBJ", "obj"));

        List<ItemBucket> buckets = BatchingEngine.PrepareBatchingBuckets(
            [
                "@(File);$(unittests)",
                @"$(obj)\%(Filename).ext",
                "@(File->'%(extension)')",  // attributes in transforms don't affect batching
            ],
            CreateLookup(itemsByType, properties),
            MockElementLocation.Instance,
            new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)));

        Assert.Equal(5, buckets.Count);

        foreach (ItemBucket bucket in buckets)
        {
            // non-batching data -- same for all buckets
            XmlAttribute tempXmlAttribute = new XmlDocument().CreateAttribute("attrib");
            tempXmlAttribute.Value = "'$(Obj)'=='obj'";

            Assert.True(ConditionEvaluator.EvaluateCondition(
                tempXmlAttribute.Value,
                ParserOptions.AllowAll,
                bucket.Expander,
                ExpanderOptions.ExpandAll,
                Directory.GetCurrentDirectory(),
                MockElementLocation.Instance,
                FileSystems.Default,
                new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4))));

            Assert.Equal(
                "a.doc;b.doc;c.doc;d.doc;e.doc",
                bucket.Expander.ExpandIntoStringAndUnescape(
                    "@(doc)", ExpanderOptions.ExpandItems, MockElementLocation.Instance));
            Assert.Equal(
                "unittests.foo",
                bucket.Expander.ExpandIntoStringAndUnescape(
                    "$(bogus)$(UNITTESTS)", ExpanderOptions.ExpandPropertiesAndMetadata, MockElementLocation.Instance));
        }

        Assert.Equal(
            "a.foo",
            buckets[0].Expander.ExpandIntoStringAndUnescape(
                "@(File)", ExpanderOptions.ExpandItems, MockElementLocation.Instance));
        Assert.Equal(
            ".foo",
            buckets[0].Expander.ExpandIntoStringAndUnescape(
                "@(File->'%(Extension)')", ExpanderOptions.ExpandItems, MockElementLocation.Instance));
        Assert.Equal(
            "obj\\a.ext",
            buckets[0].Expander.ExpandIntoStringAndUnescape(
                @"$(obj)\%(Filename).ext", ExpanderOptions.ExpandPropertiesAndMetadata, MockElementLocation.Instance));

        // we weren't batching on this attribute, so it has no value
        Assert.Equal(
            string.Empty,
            buckets[0].Expander.ExpandIntoStringAndUnescape(
                "%(Extension)", ExpanderOptions.ExpandAll, MockElementLocation.Instance));

        var factory = new ProjectItemInstanceFactory(project, "i");
        IList<ProjectItemInstance> items = buckets[0].Expander.ExpandIntoItemsLeaveEscaped(
            "@(file)",
            factory,
            ExpanderOptions.ExpandItems,
            MockElementLocation.Instance);

        Assert.NotNull(items);
        _ = Assert.Single(items);

        int invalidProjectFileExceptions = 0;
        try
        {
            // This should throw because we don't allow item lists to be concatenated
            // with other strings.
            items = buckets[0].Expander.ExpandSingleItemVectorExpressionIntoItems(
                "@(file)$(unitests)",
                factory,
                ExpanderOptions.ExpandItems,
                includeNullItems: false,
                out _,
                MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException ex)
        {
            // check we don't lose error codes from IPFE's during build
            Assert.Equal("MSB4012", ex.ErrorCode);
            invalidProjectFileExceptions++;
        }

        // We do allow separators in item vectors, this results in an item group with a single flattened item
        items = buckets[0].Expander.ExpandIntoItemsLeaveEscaped(
            "@(file, ',')",
            factory,
            ExpanderOptions.ExpandItems,
            MockElementLocation.Instance);

        Assert.NotNull(items);
        _ = Assert.Single(items);
        Assert.Equal("a.foo", items[0].EvaluatedInclude);

        Assert.Equal(1, invalidProjectFileExceptions);
    }

    /// <summary>
    /// Tests the real simple case of using an unqualified metadata reference %(Culture),
    /// where there are only two items and both of them have a value for Culture, but they
    /// have different values.
    /// </summary>
    [Fact]
    public void ValidUnqualifiedMetadataReference()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();

        var a = new ProjectItemInstance(project, "File", "a.foo", project.FullPath);
        var b = new ProjectItemInstance(project, "File", "b.foo", project.FullPath);
        _ = a.SetMetadata("Culture", "fr-fr");
        _ = b.SetMetadata("Culture", "en-en");

        var itemsByType = new ItemDictionary<ProjectItemInstance>();
        itemsByType.ImportItems([a, b]);

        var properties = new PropertyDictionary<ProjectPropertyInstance>();

        List<ItemBucket> buckets = BatchingEngine.PrepareBatchingBuckets(
            ["@(File)", "%(Culture)"],
            CreateLookup(itemsByType, properties),
            null,
            new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)));

        Assert.Equal(2, buckets.Count);
    }

    /// <summary>
    /// Tests the case where an unqualified metadata reference is used illegally.
    /// It's illegal because not all of the items consumed contain a value for
    /// that metadata.
    /// </summary>
    [Fact]
    public void InvalidUnqualifiedMetadataReference()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();

        var a = new ProjectItemInstance(project, "File", "a.foo", project.FullPath);
        _ = a.SetMetadata("Culture", "fr-fr");

        var itemsByType = new ItemDictionary<ProjectItemInstance>();
        itemsByType.ImportItems([a, new ProjectItemInstance(project, "File", "b.foo", project.FullPath)]);

        var properties = new PropertyDictionary<ProjectPropertyInstance>();

        _ = Assert.Throws<InvalidProjectFileException>(() =>
        {
            // This is expected to throw because not all items contain a value for metadata "Culture".
            // Only a.foo has a Culture metadata.  b.foo does not.
            _ = BatchingEngine.PrepareBatchingBuckets(
                ["@(File)", "%(Culture)"],
                CreateLookup(itemsByType, properties),
                MockElementLocation.Instance,
                new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)));
        });
    }

    /// <summary>
    /// Tests the case where an unqualified metadata reference is used illegally.
    /// It's illegal because not all of the items consumed contain a value for
    /// that metadata.
    /// </summary>
    [Fact]
    public void NoItemsConsumed()
    {
        var itemsByType = new ItemDictionary<ProjectItemInstance>();
        var properties = new PropertyDictionary<ProjectPropertyInstance>();

        _ = Assert.Throws<InvalidProjectFileException>(() =>
        {
            // This is expected to throw because we have no idea what item list %(Culture) refers to.
            _ = BatchingEngine.PrepareBatchingBuckets(
                ["$(File)", "%(Culture)"],
                CreateLookup(itemsByType, properties),
                MockElementLocation.Instance,
                new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)));
        });
    }

    /// <summary>
    /// Missing unittest found by mutation testing.
    /// REASON TEST WASN'T ORIGINALLY PRESENT: Missed test.
    ///
    /// This test ensures that two items with duplicate attributes end up in exactly one batching
    /// bucket.
    /// </summary>
    [Fact]
    public void Regress_Mutation_DuplicateBatchingBucketsAreFoldedTogether()
    {
        ProjectInstance project = ProjectHelpers.CreateEmptyProjectInstance();

        var itemsByType = new ItemDictionary<ProjectItemInstance>();

        itemsByType.ImportItems([
            new ProjectItemInstance(project, "File", "a.foo", project.FullPath),
            new ProjectItemInstance(project, "File", "b.foo", project.FullPath), // Need at least two items for this test case to ensure multiple buckets might be possible
        ]);

        var properties = new PropertyDictionary<ProjectPropertyInstance>();

        List<ItemBucket> buckets = BatchingEngine.PrepareBatchingBuckets(
            ["%(File.Culture)"],
            CreateLookup(itemsByType, properties),
            null,
            new TestLoggingContext(loggingService: null, new BuildEventContext(1, 2, 3, 4)));

        // If duplicate buckets have been folded correctly, then there will be exactly one bucket here
        // containing both a.foo and b.foo.
        _ = Assert.Single(buckets);
    }

    [Fact]
    public void Simple()
    {
        const string Content = """
            <Project ToolsVersion="msbuilddefaulttoolsversion">

                <ItemGroup>
                    <AToB Include="a;b"/>
                </ItemGroup>

                <Target Name="Build">
                    <CreateItem Include="%(AToB.Identity)">
                        <Output ItemName="AToBBatched" TaskParameter="Include"/>
                    </CreateItem>
                    <Message Text="[AToBBatched: @(AToBBatched)]"/>
                </Target>

            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("[AToBBatched: a;b]");
    }

    /// <summary>
    /// When removing an item in a target which is batched and called by call target there was an exception thrown
    /// due to us adding the same item instance to the remove item lists when merging the lookups between the two batches.
    /// The fix was to not add the item to the remove list if it already exists.
    /// </summary>
    [Fact]
    public void Regress72803()
    {
        const string Content = """
            <Project DefaultTargets="ReleaseBuild">
                <ItemGroup>
                    <Environments Include="dev" />
                    <Environments Include="prod" />
                    <ItemsToZip Include="1" />
                    </ItemGroup>
                <Target Name="ReleaseBuild">
                <CallTarget Targets="MakeAppPackage;MakeDbPackage"/>
                </Target>
                <Target Name="MakeAppPackage" Outputs="%(Environments.Identity)">
                    <ItemGroup>
                        <ItemsToZip Include="%(Environments.Identity).msi" />
                    </ItemGroup>
                </Target>
                <Target Name="MakeDbPackage"  Outputs="%(Environments.Identity)">
                    <Message Text="Item Before:%(Environments.Identity) @(ItemsToZip)" />
                    <ItemGroup>
                            <ItemsToZip Remove="@(ItemsToZip)" />
                    </ItemGroup>
                    <Message Text="Item After:%(Environments.Identity) @(ItemsToZip)" Condition="'@(ItemsToZip)' != ''" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("Item Before:dev 1");
        log.AssertLogContains("Item Before:prod 1");
        log.AssertLogDoesntContain("Item After:dev 1");
        log.AssertLogDoesntContain("Item After:prod 1");
    }

    /// <summary>
    /// Regress a bug where batching over an item list seemed to have
    /// items for that list even in buckets where there should be none, because
    /// it was batching over metadata that only other list/s had.
    /// </summary>
    [Fact]
    public void BucketsWithEmptyListForBatchedItemList()
    {
        const string Content = """
             <Project ToolsVersion="msbuilddefaulttoolsversion">
              <ItemGroup>
                <i Include="b"/>
                <j Include="a">
                    <k>x</k>
                </j>
              </ItemGroup>

              <Target Name="t">
                <ItemGroup>
                  <Obj Condition="'%(j.k)'==''" Include="@(j->'%(Filename).obj');%(i.foo)"/>
                </ItemGroup>
                <Message Text="@(Obj)" />
              </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogDoesntContain("a.obj");
    }

    /// <summary>
    /// Bug for Targets instead of Tasks.
    /// </summary>
    [Fact]
    public void BucketsWithEmptyListForTargetBatchedItemList()
    {
        string content = """
            <Project ToolsVersion="msbuilddefaulttoolsversion">
                <ItemGroup>
                    <a Include="a1"/>
                    <b Include="b1"/>
                </ItemGroup>
                <Target Name="t"  Outputs="%(a.Identity)%(b.identity)">
                    <Message Text="[a=@(a) b=@(b)]" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(content);

        log.AssertLogContains("[a=a1 b=]");
        log.AssertLogContains("[a= b=b1]");
    }

    /// <summary>
    /// A batching target that has no outputs should still run.
    /// This is how we shipped before, although Jay pointed out it's odd.
    /// </summary>
    [Fact]
    public void BatchOnEmptyOutput()
    {
        const string Content = """
            <Project ToolsVersion="msbuilddefaulttoolsversion">
                <ItemGroup>
                    <File Include="$(foo)" />
                </ItemGroup>
                <!-- Should not run as the single batch has no outputs -->
                <Target Name="b" Outputs="%(File.Identity)"><Message Text="[@(File)]" /></Target>
                <Target Name="a" DependsOnTargets="b">
                    <Message Text="[a]" />
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("[]");
    }

    /// <summary>
    /// Every batch should get its own new task object.
    /// We verify this by using the Warning class. If the same object is being reused,
    /// the second warning would have the code from the first use of the task.
    /// </summary>
    [Fact]
    public void EachBatchGetsASeparateTaskObject()
    {
        const string Content = """
            <Project ToolsVersion="msbuilddefaulttoolsversion">
                <ItemGroup>
                    <i Include="i1">
                        <Code>high</Code>
                    </i>
                    <i Include="i2"/>
                </ItemGroup>

                <Target Name="t">
                    <Warning Text="@(i)" Code="%(i.Code)"/>
                </Target>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        Assert.Equal("high", log.Warnings[0].Code);
        Assert.Null(log.Warnings[1].Code);
    }

    /// <summary>
    /// It is important that the batching engine invokes the different batches in the same
    /// order as the items are declared in the project, especially when batching is simply
    /// being used as a "for loop".
    /// </summary>
    [Fact]
    public void BatcherPreservesItemOrderWithinASingleItemList()
    {
        const string Content = """
            <Project ToolsVersion="msbuilddefaulttoolsversion">

                <ItemGroup>
                    <AToZ Include="a;b;c;d;e;f;g;h;i;j;k;l;m;n;o;p;q;r;s;t;u;v;w;x;y;z"/>
                    <ZToA Include="z;y;x;w;v;u;t;s;r;q;p;o;n;m;l;k;j;i;h;g;f;e;d;c;b;a"/>
                </ItemGroup>

                <Target Name="Build">
                    <CreateItem Include="%(AToZ.Identity)">
                        <Output ItemName="AToZBatched" TaskParameter="Include"/>
                    </CreateItem>
                    <CreateItem Include="%(ZToA.Identity)">
                        <Output ItemName="ZToABatched" TaskParameter="Include"/>
                    </CreateItem>
                    <Message Text="AToZBatched: @(AToZBatched)"/>
                    <Message Text="ZToABatched: @(ZToABatched)"/>
                </Target>

            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogContains("AToZBatched: a;b;c;d;e;f;g;h;i;j;k;l;m;n;o;p;q;r;s;t;u;v;w;x;y;z");
        log.AssertLogContains("ZToABatched: z;y;x;w;v;u;t;s;r;q;p;o;n;m;l;k;j;i;h;g;f;e;d;c;b;a");
    }

    /// <summary>
    /// Undefined and empty metadata values should not be distinguished when bucketing.
    /// This is the same as previously shipped.
    /// </summary>
    [Fact]
    public void UndefinedAndEmptyMetadataValues()
    {
        const string Content = """
            <Project ToolsVersion='msbuilddefaulttoolsversion'>
                <ItemGroup>
                    <i Include='i1'/>
                    <i Include='i2'>
                        <m></m>
                    </i>
                    <i Include='i3'>
                        <m>m1</m>
                    </i>
                </ItemGroup>

                <Target Name='Build'>
                    <Message Text='[@(i) %(i.m)]'/>
                </Target>

            </Project>
            """;

        using var projectFromString = new ProjectFromString(ObjectModelHelpers.CleanupFileContents(Content));
        Project project = projectFromString.Project;
        var logger = new MockLogger();
        _ = project.Build(logger);

        logger.AssertLogContains("[i1;i2 ]", "[i3 m1]");
    }

    /// <summary>
    /// This is a regression test for https://github.com/dotnet/msbuild/issues/10180.
    /// </summary>
    [Fact]
    public void HandlesEarlyExitFromTargetBatching()
    {
        const string Content = """
            <Project>
                <ItemGroup>
                    <Example Include='Item1'>
                        <Color>Blue</Color>
                    </Example>
                    <Example Include='Item2'>
                        <Color>Red</Color>
                    </Example>
                </ItemGroup>

                <Target Name='Build'
                    Inputs='@(Example)'
                    Outputs='%(Color)\MyFile.txt'>
                    <NonExistentTask
                        Text = '@(Example)'
                        Output = '%(Color)\MyFile.txt'/>
                </Target>
            </Project>
            """;

        using var projectFromString = new ProjectFromString(Content);
        Project project = projectFromString.Project;
        var logger = new MockLogger();
        _ = project.Build(logger);

        // Build should fail with error MSB4036: The "NonExistentTask" task was not found.
        logger.AssertLogContains("MSB4036");
    }

    private static Lookup CreateLookup(
        ItemDictionary<ProjectItemInstance> itemsByType,
        PropertyDictionary<ProjectPropertyInstance> properties)
        => new(itemsByType, properties);
}
