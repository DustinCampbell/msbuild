// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
/// Tests mainly for project evaluation.
/// </summary>
public class ItemEvaluation_Tests : IDisposable
{
    public ItemEvaluation_Tests()
    {
        ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        GC.Collect();
    }

    public void Dispose()
    {
        ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        GC.Collect();
    }

    [Fact]
    public void IncludeShouldPreserveIntermediaryReferences()
    {
        const string Content = """
            <i2 Include='a;b;c'>
                <m1>m1_contents</m1>
                <m2>m2_contents</m2>
            </i2>

            <i Include='@(i2)'/>

            <i2 Include='d;e;f;@(i2)'>
                <m1>m1_updated</m1>
                <m2>m2_updated</m2>
            </i2>
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(Content, allItems: true);

        Metadata mI2_1 = [
            ("m1", "m1_contents"),
            ("m2", "m2_contents")
        ];

        Metadata mI2_2 = [
            ("m1", "m1_updated"),
            ("m2", "m2_updated")
        ];

        var itemsForI = items.Where(i => i.ItemType == "i").ToList();
        ObjectModelHelpers.AssertItems(expectedItems: ["a", "b", "c"], itemsForI, mI2_1);

        var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
        ObjectModelHelpers.AssertItems(
            expectedItems: ["a", "b", "c", "d", "e", "f", "a", "b", "c"],
            itemsForI2,
            expectedDirectMetadataPerItem: [mI2_1, mI2_1, mI2_1, mI2_2, mI2_2, mI2_2, mI2_2, mI2_2, mI2_2]);
    }

    [Theory]
    // remove the items by referencing each one
    [InlineData("""
        <i2 Include='a;b;c'>
            <m1>m1_contents</m1>
            <m2>m2_contents</m2>
        </i2>

        <i Include='@(i2)'/>

        <i2 Remove='a;b;c'/>
        """)]
    // remove the items via a glob
    [InlineData("""
        <i2 Include='a;b;c'>
            <m1>m1_contents</m1>
            <m2>m2_contents</m2>
        </i2>

        <i Include='@(i2)'/>

        <i2 Remove='*'/>
        """)]
    public void RemoveShouldPreserveIntermediaryReferences(string content)
    {
        IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

        var itemsForI = items.Where(i => i.ItemType == "i").ToList();
        ObjectModelHelpers.AssertItems(
            expectedItems: ["a", "b", "c"],
            itemsForI,
            expectedDirectMetadata: [("m1", "m1_contents"), ("m2", "m2_contents")]);

        var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
        ObjectModelHelpers.AssertItems(expectedItems: [], itemsForI2);
    }

    [Fact]
    public void RemoveRespectsItemTransform()
    {
        const string Content = """
            <i Include='a;b;c' />

            <i Remove='@(i->WithMetadataValue(`Identity`, `b`))' />
            <i Remove='@(i->`%(Extension)`)' /> <!-- should do nothing -->
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(Content, allItems: true);

        ObjectModelHelpers.AssertItems(expectedItems: ["a", "c"], items);
    }

    [Fact]
    public void UpdateRespectsItemTransform()
    {
        const string content = """
            <i Include='a;b;c' />

            <i Update='@(i->WithMetadataValue(`Identity`, `b`))'>
                <m1>m1_updated</m1>
            </i>
            <i Update=`@(i->'%(Extension)')`> <!-- should do nothing -->
                <m2>m2_updated</m2>
            </i>
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

        ObjectModelHelpers.AssertItems(
            expectedItems: ["a", "b", "c"],
            items,
            expectedDirectMetadataPerItem: [[], [("m1", "m1_updated")], []]);
    }

    [Fact]
    public void UpdateShouldPreserveIntermediaryReferences()
    {
        const string Content = """
            <i2 Include='a;b;c'>
                <m1>m1_contents</m1>
                <m2>%(Identity)</m2>
            </i2>

            <i Include='@(i2)'>
                <m3>@(i2 -> '%(m2)')</m3>
                <m4 Condition="'@(i2 -> '%(m2)')' == 'a;b;c'">m4_contents</m4>
            </i>

            <i2 Update='a;b;c'>
                <m1>m1_updated</m1>
                <m2>m2_updated</m2>
                <m3>m3_updated</m3>
                <m4>m4_updated</m4>
            </i2>
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(Content, allItems: true);

        var itemsForI = items.Where(i => i.ItemType == "i").ToList();

        ObjectModelHelpers.AssertItems(
            expectedItems: ["a", "b", "c"],
            itemsForI,
            expectedDirectMetadataPerItem: [
                [("m1", "m1_contents"), ("m2", "a"), ("m3", "a;b;c"), ("m4", "m4_contents")],
                [("m1", "m1_contents"), ("m2", "b"), ("m3", "a;b;c"), ("m4", "m4_contents")],
                [("m1", "m1_contents"), ("m2", "c"), ("m3", "a;b;c"), ("m4", "m4_contents")]]);

        var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
        ObjectModelHelpers.AssertItems(
            expectedItems: ["a", "b", "c"],
            itemsForI2,
            expectedDirectMetadata:
                [("m1", "m1_updated"), ("m2", "m2_updated"), ("m3", "m3_updated"), ("m4", "m4_updated")]);
    }

    public static TheoryData<string, string[], Metadata> IndirectItemReferencesTestData
        => new()
        {
            // indirect item reference via properties in metadata
            ("""
                <Project>
                    <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                    </ItemGroup>
                    <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                    </PropertyGroup>

                    <ItemGroup>
                        <i Include=`val`>
                            <m1>$(P1)</m1>
                            <m2>$(P2)</m2>
                        </i>
                    </ItemGroup>
                </Project>
                """,
                ["val"],
                [("m1", "1;2"), ("m2", "1;2")]),

            // indirect item reference via properties in metadata condition
            ("""
                <Project>
                    <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                    </ItemGroup>
                    <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                    </PropertyGroup>

                    <ItemGroup>
                        <i Include=`val`>
                            <m1 Condition=`'$(P1)' == '1;2'`>val1</m1>
                            <m2 Condition=`'$(P2)' == '1;2'`>val2</m2>
                        </i>
                    </ItemGroup>
                </Project>
                """,
                ["val"],
                [("m1", "val1"), ("m2", "val2")]),

            // indirect item reference via properties in include
            (
                """
                <Project>
                    <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                    </ItemGroup>
                    <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                    </PropertyGroup>

                    <ItemGroup>
                        <i Include=`$(P1)`/>
                        <i Include=`$(P2)`/>
                    </ItemGroup>
                </Project>
                """,
                ["1", "2", "1", "2"],
                []),

            // indirect item reference via properties in condition
            ("""
                <Project>
                    <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                    </ItemGroup>
                    <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                    </PropertyGroup>

                    <ItemGroup>
                        <i Condition=`'$(P1)' == '1;2'` Include=`val1`/>
                        <i Condition=`'$(P2)' == '1;2'` Include=`val2`/>
                    </ItemGroup>
                </Project>
                """,
                ["val1", "val2"],
                []),

            // indirect item reference via metadata reference in conditions and metadata
            ("""
                <Project>
                    <ItemGroup>
                        <IndirectItem Include=`1` />
                        <IndirectItem Include=`2` />
                    </ItemGroup>
                    <PropertyGroup>
                        <P1>@(IndirectItem)</P1>
                        <P2>$(P1)</P2>
                    </PropertyGroup>

                    <ItemGroup>
                        <i Include=`val`>
                            <m1 Condition=`'$(P2)' == '1;2'`>$(P2)</m1>
                            <m2 Condition=`'%(m1)' == '1;2'`>%(m1)</m2>
                        </i>
                    </ItemGroup>
                </Project>
                """,
                ["val"],
                [("m1", "1;2"), ("m2", "1;2")]),
        };

    [Theory]
    [MemberData(nameof(IndirectItemReferencesTestData))]
    public void ItemOperationsShouldExpandIndirectItemReferences(
        string projectContent,
        string[] expectedItemValues,
        Metadata expectedItemMetadata)
    {
        IList<ProjectItem> items = ObjectModelHelpers.GetItems(projectContent);

        ObjectModelHelpers.AssertItems(expectedItemValues, items, expectedItemMetadata);
    }

    [Fact]
    public void OnlyPropertyReferencesGetExpandedInPropertyFunctionArgumentsInsideIncludeAttributes()
    {
        const string ProjectContent = """
            <Project>
                <ItemGroup>
                    <A Include=`1`/>
                    <B Include=`$([System.String]::new('@(A)'))`/>
                    <C Include=`$([System.String]::new('$(P)'))`/>
                </ItemGroup>

                <PropertyGroup>
                    <P>@(A)</P>
                </PropertyGroup>
            </Project>
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItems(ProjectContent, allItems: true);

        ObjectModelHelpers.AssertItems(["1", "@(A)", "@(A)"], items);
    }

    [Fact]
    public void MetadataAndPropertyReferencesGetExpandedInPropertyFunctionArgumentsInsideMetadataElements()
    {
        const string ProjectContent = """
            <Project>
                <ItemGroup>
                    <A Include=`1` />
                    <B Include=`B`>
                        <M>$([System.String]::new(`%(Identity)`))</M>
                        <M2>$([System.String]::new(`%(M)`))</M2>
                    </B>
                    <C Include=`C`>
                        <M>$([System.String]::new(`$(P)`))</M>
                        <M2>$([System.String]::new(`%(M)`))</M2>
                    </C>
                    <D Include=`D`>
                        <M>$([System.String]::new(`@(A)`))</M>
                        <M2>$([System.String]::new(`%(M)`))</M2>
                    </D>
                </ItemGroup>

                <PropertyGroup>
                    <P>@(A)</P>
                </PropertyGroup>
            </Project>
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItems(ProjectContent, allItems: true);

        ObjectModelHelpers.AssertItems(
            expectedItems: ["1", "B", "C", "D"],
            items,
            expectedDirectMetadataPerItem: [
                [],
                [("M", "B"), ("M2", "B")],
                [("M", "@(A)"), ("M2", "@(A)")],
                [("M", "@(A)"), ("M2", "@(A)")]]);
    }

    [Fact]
    public void ExcludeSeesIntermediaryState()
    {
        const string ProjectContent = """
            <Project>
                <ItemGroup>
                    <a Include=`1` />
                    <i Include=`1;2` Exclude=`@(a)` />
                    <a Include=`2` />
                    <a Condition=`'@(a)' == '1;2'` Include=`3` />
                </ItemGroup>
                    <Target Name=`Build`>
                    <Message Text=`Done!` />
                </Target>
            </Project>
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItems(ProjectContent);

        ObjectModelHelpers.AssertItems(expectedItems: ["2"], items);
    }

    [Fact]
    public void MultipleInterItemDependenciesOnSameItemOperation()
    {
        const string Content = """
            <i1 Include='i1_1;i1_2;i1_3;i1_4;i1_5'/>
            <i1 Update='*'>
                <m>i1</m>
            </i1>
            <i1 Remove='*i1_5'/>

            <i_cond Condition='@(i1->Count()) == 4' Include='i1 has 4 items'/>

            <i2 Include='@(i1);i2_4'/>
            <i2 Remove='i?_4'/>
            <i2 Update='i?_1'>
                <m>i2</m>
            </i2>

            <i3 Include='@(i1);i3_3'/>
            <i3 Remove='*i?_3'/>

            <i1 Remove='*i1_2'/>
            <i1 Include='i1_6'/>
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(Content, allItems: true);

        Metadata i1BaseMetadata = [("m", "i1")];

        // i1 items: i1_1; i1_3; i1_4; i1_6
        Metadata[] i1Metadata = [i1BaseMetadata, i1BaseMetadata, i1BaseMetadata, []];

        var i1Items = items.Where(i => i.ItemType == "i1").ToList();
        ObjectModelHelpers.AssertItems(["i1_1", "i1_3", "i1_4", "i1_6"], i1Items, i1Metadata);

        // i2 items: i1_1; i1_2; i1_3
        Metadata[] i2Metadata = [[("m", "i2")], i1BaseMetadata, i1BaseMetadata];

        var i2Items = items.Where(i => i.ItemType == "i2").ToList();
        ObjectModelHelpers.AssertItems(["i1_1", "i1_2", "i1_3"], i2Items, i2Metadata);

        // i3 items: i1_1; i1_2; i1_4
        var i3Items = items.Where(i => i.ItemType == "i3").ToList();
        ObjectModelHelpers.AssertItems(["i1_1", "i1_2", "i1_4"], i3Items, i1BaseMetadata);

        var i_condItems = items.Where(i => i.ItemType == "i_cond").ToList();
        ObjectModelHelpers.AssertItems(["i1 has 4 items"], i_condItems);
    }

    [Fact]
    public void LongIncludeChain()
    {
        const int INCLUDE_COUNT = 10000;

        // This was about the minimum count needed to repro a StackOverflowException
        // const int INCLUDE_COUNT = 4000;

        var builder = new StringBuilder();
        for (int i = 0; i < INCLUDE_COUNT; i++)
        {
            _ = builder.Append("<i Include='ItemValue").Append(i).AppendLine("' />");
        }

        IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(builder.ToString());

        Assert.Equal(INCLUDE_COUNT, items.Count);
    }

    // see https://github.com/dotnet/msbuild/issues/2069
    [Fact]
    public void ImmutableListBuilderBug()
    {
        const string content = """
            <i Include="0;x1;x2;x3;x4;x5;6;7;8;9"/>
            <i Remove="x*"/>
            """;

        IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

        Assert.Equal("0;6;7;8;9", string.Join(";", items.Select(i => i.EvaluatedInclude)));
    }

    [Fact]
    public void LazyWildcardExpansionDoesNotEvaluateWildCardsIfNotReferenced()
    {
        string content = """
            <Project>
               <Import Project=`foo/*.props`/>
               <ItemGroup>
                  <i Include=`**/foo/**/*.cs` />
                  <i2 Include=`**/bar/**/*.cs` />
                  <i3 Include=`**/yyy/**/*.cs` Exclude=`mock-value` />
               </ItemGroup>

               <ItemGroup>
                  <ItemReference Include=`@(i)`/>
                  <FullPath Include=`@(i->'%(FullPath)')`/>
                  <Identity Include=`@(i->'%(Identity)')`/>
                  <RecursiveDir Include=`@(i->'%(RecursiveDir)')`/>
               </ItemGroup>
            </Project>
            """.Cleanup();

        string import = """
            <Project>
               <PropertyGroup>
                  <FromImport>true</FromImport>
               </PropertyGroup>
            </Project>
            """.Cleanup();

        using var env = TestEnvironment.Create();

        TransientTestProjectWithFiles projectFiles = env.CreateTestProjectWithFiles(
            content,
            files: ["foo/extra.props", "foo/a.cs", "foo/b.cs", "bar/c.cs", "bar/d.cs", "yyy/d.cs"]);

        File.WriteAllText(projectFiles.CreatedFiles[0], import);

        _ = env.SetEnvironmentVariable("MsBuildSkipEagerWildCardEvaluationRegexes", ".*foo.*;.*yyy*.");

        EngineFileUtilities.CaptureLazyWildcardRegexes();

        var project = new Project(projectFiles.ProjectFile);

        Assert.Equal("true", project.GetPropertyValue("FromImport"));
        Assert.Equal("**/foo/**/*.cs", project.GetConcatenatedItemsOfType("i"));
        Assert.Equal("**/yyy/**/*.cs", project.GetConcatenatedItemsOfType("i3"));

        string expectedItems = @"bar\c.cs;bar\d.cs";

        if (!NativeMethodsShared.IsWindows)
        {
            expectedItems = expectedItems.ToSlash();
        }

        Assert.Equal(expectedItems, project.GetConcatenatedItemsOfType("i2"));

        string fullPathItems = project.GetConcatenatedItemsOfType("FullPath");
        Assert.Contains("a.cs", fullPathItems);
        Assert.Contains("b.cs", fullPathItems);

        string identityItems = project.GetConcatenatedItemsOfType("Identity");
        Assert.Contains("a.cs", identityItems);
        Assert.Contains("b.cs", identityItems);

        // direct item references do not expand the lazy wildcard
        Assert.Equal("**/foo/**/*.cs", project.GetConcatenatedItemsOfType("ItemReference"));

        // recursive dir does not work with lazy wildcards
        Assert.Equal(string.Empty, project.GetConcatenatedItemsOfType("RecursiveDir"));
    }

    [Fact]
    public void DoesNotCrashWhenUnEvaluatedWildCardLooksLikeUNC()
    {
        string content = """
            <Project>
              <PropertyGroup>
                <A>$(B)\</A>
              </PropertyGroup>

              <ItemGroup>
                <None Include="$(A)\csc.*" />
                <None Update="2.txt">
                  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                </None>
              </ItemGroup>

              <Target Name="a" />
            </Project>
            """.Cleanup();

        using var env = TestEnvironment.Create();

        TransientTestProjectWithFiles projectFiles = env.CreateTestProjectWithFiles(content);

        _ = env.SetEnvironmentVariable("MsBuildSkipEagerWildCardEvaluationRegexes", ".*");

        EngineFileUtilities.CaptureLazyWildcardRegexes();

        Project project = Should.NotThrow(() => new Project(projectFiles.ProjectFile));

        project.GetConcatenatedItemsOfType("None").ShouldContain("csc.*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DifferentExcludesOnSameWildcardProduceDifferentResults(bool cacheFileEnumerations)
    {
        string projectContents = """
            <Project>
               <ItemGroup>
                  <i Include=`**/*.cs`/>
                  <i Include=`**/*.cs` Exclude=`*a.cs`/>
                  <i Include=`**/*.cs` Exclude=`a.cs;c.cs`/>
               </ItemGroup>
            </Project>
            """.Cleanup();

        try
        {
            using var env = TestEnvironment.Create();

            if (cacheFileEnumerations)
            {
                _ = env.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            }

            ObjectModelHelpers.AssertItemEvaluationFromProject(
                projectContents,
                inputFiles: ["a.cs", "b.cs", "c.cs"],
                expectedInclude: ["a.cs", "b.cs", "c.cs", "b.cs", "c.cs", "b.cs"]);
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    // see https://github.com/dotnet/msbuild/issues/3460
    [Fact]
    public void MetadataPropertyFunctionBug()
    {
        const string prefix = "SomeLongPrefix-"; // Needs to be longer than "%(FileName)"

        string projectContent = $"""
            <Project>
              <ItemGroup>
                <Bar Include=`{prefix}foo`>
                  <Baz>$([System.String]::new(%(FileName)).Substring({prefix.Length}))</Baz>
                </Bar>
              </ItemGroup>
            </Project>
            """.Cleanup();

        IList<ProjectItem> items = ObjectModelHelpers.GetItems(projectContent, allItems: true);

        ObjectModelHelpers.AssertItems(
            expectedItems: [$"{prefix}foo"],
            items,
            expectedDirectMetadata: [("Baz", "foo")]);
    }
}
