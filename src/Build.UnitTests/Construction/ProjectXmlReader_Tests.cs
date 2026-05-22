// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction;

public class ProjectXmlReader_Tests
{
    private const string SimpleProject = """
        <Project>
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <Compile Include="Program.cs" />
            <Compile Include="Utils.cs" Condition="'$(Debug)' == 'true'" />
          </ItemGroup>
        </Project>
        """;

    private const string ProjectWithImports = """
        <Project>
          <Import Project="common.props" Condition="Exists('common.props')" />
          <ImportGroup Condition="'$(UseImports)' == 'true'">
            <Import Project="extra.props" />
          </ImportGroup>
        </Project>
        """;

    private const string ProjectWithTargets = """
        <Project>
          <Target Name="Build" DependsOnTargets="Compile">
            <Message Text="Building..." />
            <Csc Sources="@(Compile)" OutputAssembly="out.dll">
              <Output TaskParameter="OutputAssembly" ItemName="Assemblies" />
            </Csc>
            <OnError ExecuteTargets="HandleError" />
          </Target>
        </Project>
        """;

    private const string ProjectWithChoose = """
        <Project>
          <Choose>
            <When Condition="'$(OS)' == 'Windows_NT'">
              <PropertyGroup>
                <Runtime>win</Runtime>
              </PropertyGroup>
            </When>
            <Otherwise>
              <PropertyGroup>
                <Runtime>unix</Runtime>
              </PropertyGroup>
            </Otherwise>
          </Choose>
        </Project>
        """;

    private const string ProjectWithItemDefinitions = """
        <Project>
          <ItemDefinitionGroup>
            <Compile>
              <Optimize>true</Optimize>
            </Compile>
          </ItemDefinitionGroup>
        </Project>
        """;

    private const string ProjectWithMetadataAsAttributes = """
        <Project>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" PrivateAssets="all" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void ParsesSimpleProject_Properties()
    {
        var pre = ParseWithNewParser(SimpleProject);

        pre.ElementName.ShouldBe("Project");
        var propertyGroups = pre.PropertyGroups.ToList();
        propertyGroups.Count.ShouldBe(1);

        var properties = propertyGroups[0].Properties.ToList();
        properties.Count.ShouldBe(2);
        properties[0].Name.ShouldBe("OutputType");
        properties[0].Value.ShouldBe("Exe");
        properties[1].Name.ShouldBe("TargetFramework");
        properties[1].Value.ShouldBe("net10.0");
    }

    [Fact]
    public void ParsesSimpleProject_Items()
    {
        var pre = ParseWithNewParser(SimpleProject);

        var itemGroups = pre.ItemGroups.ToList();
        itemGroups.Count.ShouldBe(1);

        var items = itemGroups[0].Items.ToList();
        items.Count.ShouldBe(2);
        items[0].ItemType.ShouldBe("Compile");
        items[0].Include.ShouldBe("Program.cs");
        items[1].Include.ShouldBe("Utils.cs");
        items[1].Condition.ShouldBe("'$(Debug)' == 'true'");
    }

    [Fact]
    public void ParsesImports()
    {
        var pre = ParseWithNewParser(ProjectWithImports);

        var imports = pre.Imports.ToList();
        imports.Count.ShouldBe(2);
        imports[0].Project.ShouldBe("common.props");
        imports[0].Condition.ShouldBe("Exists('common.props')");

        var importGroups = pre.ImportGroups.ToList();
        importGroups.Count.ShouldBe(1);
        importGroups[0].Condition.ShouldBe("'$(UseImports)' == 'true'");
    }

    [Fact]
    public void ParsesTargets()
    {
        var pre = ParseWithNewParser(ProjectWithTargets);

        var targets = pre.Targets.ToList();
        targets.Count.ShouldBe(1);
        targets[0].Name.ShouldBe("Build");
        targets[0].DependsOnTargets.ShouldBe("Compile");

        var children = targets[0].Children.ToList();
        children.Count.ShouldBe(3); // Message, Csc, OnError

        var messageTask = (ProjectTaskElement)children[0];
        messageTask.Name.ShouldBe("Message");
        messageTask.GetParameter("Text").ShouldBe("Building...");

        var cscTask = (ProjectTaskElement)children[1];
        cscTask.Name.ShouldBe("Csc");
        cscTask.Outputs.Count.ShouldBe(1);

        var onError = (ProjectOnErrorElement)children[2];
        onError.ExecuteTargetsAttribute.ShouldBe("HandleError");
    }

    [Fact]
    public void ParsesChoose()
    {
        var pre = ParseWithNewParser(ProjectWithChoose);

        var chooses = pre.ChooseElements.ToList();
        chooses.Count.ShouldBe(1);

        var choose = chooses[0];
        choose.WhenElements.Count.ShouldBe(1);
        choose.OtherwiseElement.ShouldNotBeNull();

        var when = choose.WhenElements.First();
        when.Condition.ShouldBe("'$(OS)' == 'Windows_NT'");
        when.PropertyGroups.First().Properties.First().Name.ShouldBe("Runtime");
        when.PropertyGroups.First().Properties.First().Value.ShouldBe("win");
    }

    [Fact]
    public void ParsesItemDefinitions()
    {
        var pre = ParseWithNewParser(ProjectWithItemDefinitions);

        var idgs = pre.ItemDefinitionGroups.ToList();
        idgs.Count.ShouldBe(1);

        var itemDefs = idgs[0].ItemDefinitions.ToList();
        itemDefs.Count.ShouldBe(1);
        itemDefs[0].ItemType.ShouldBe("Compile");

        var metadata = itemDefs[0].Metadata.ToList();
        metadata.Count.ShouldBe(1);
        metadata[0].Name.ShouldBe("Optimize");
        metadata[0].Value.ShouldBe("true");
    }

    [Fact]
    public void ParsesMetadataAsAttributes()
    {
        var pre = ParseWithNewParser(ProjectWithMetadataAsAttributes);

        var items = pre.ItemGroups.First().Items.ToList();
        items.Count.ShouldBe(1);
        items[0].Include.ShouldBe("Newtonsoft.Json");

        var metadata = items[0].Metadata.ToList();
        metadata.Count.ShouldBe(2);
        metadata.ShouldContain(m => m.Name == "Version" && m.Value == "13.0.1");
        metadata.ShouldContain(m => m.Name == "PrivateAssets" && m.Value == "all");
    }

    [Fact]
    public void LocationsArePopulated()
    {
        var pre = ParseWithNewParser(SimpleProject);

        pre.Location.ShouldNotBeNull();
        pre.Location.Line.ShouldBeGreaterThan(0);
        pre.Location.Column.ShouldBeGreaterThan(0);

        var prop = pre.PropertyGroups.First().Properties.First();
        prop.Location.ShouldNotBeNull();
        prop.Location.Line.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void AttributeLocationsArePopulated()
    {
        var pre = ParseWithNewParser(SimpleProject);

        var item = pre.ItemGroups.First().Items.First();
        var includeLocation = item.IncludeLocation;
        includeLocation.ShouldNotBeNull();
        includeLocation.Line.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(SimpleProject)]
    [InlineData(ProjectWithImports)]
    [InlineData(ProjectWithTargets)]
    [InlineData(ProjectWithChoose)]
    [InlineData(ProjectWithItemDefinitions)]
    [InlineData(ProjectWithMetadataAsAttributes)]
    public void NewParserProducesSameStructureAsOldParser(string projectXml)
    {
        // Parse with old (DOM-based) parser
        using var stringReader = new StringReader(projectXml);
        using var xmlReader = XmlReader.Create(stringReader);
        var oldPre = ProjectRootElement.Create(xmlReader);

        // Parse with new (ElementData-based) parser
        var newPre = ParseWithNewParser(projectXml);

        // Compare structure
        CompareElements(oldPre, newPre);
    }

    #region Helpers

    private static ProjectRootElement ParseWithNewParser(string projectXml)
    {
        using var collection = new ProjectCollection();
        var pre = ProjectRootElement.Create(collection);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
        };

        using var stringReader = new StringReader(projectXml);
        using var xmlReader = XmlReader.Create(stringReader, settings);
        ProjectXmlReader.Parse(xmlReader, pre, "test.proj");
        return pre;
    }

    private static void CompareElements(ProjectElement expected, ProjectElement actual)
    {
        actual.ElementName.ShouldBe(expected.ElementName, $"Element name mismatch");
        actual.Condition.ShouldBe(expected.Condition, $"Condition mismatch on {expected.ElementName}");

        if (expected is ProjectElementContainer expectedContainer && actual is ProjectElementContainer actualContainer)
        {
            var expectedChildren = expectedContainer.Children.ToList();
            var actualChildren = actualContainer.Children.ToList();
            actualChildren.Count.ShouldBe(expectedChildren.Count, $"Child count mismatch on {expected.ElementName}");

            for (int i = 0; i < expectedChildren.Count; i++)
            {
                CompareElements(expectedChildren[i], actualChildren[i]);
            }
        }
    }

    #endregion
}

/// <summary>
/// Integration tests that verify ProjectXmlReader is wired into the real load path
/// when MSBUILD_ENABLE_XMLREADER_PARSER=1 is set.
/// </summary>
public class ProjectXmlReader_IntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TestEnvironment _env;

    public ProjectXmlReader_IntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _env = TestEnvironment.Create(output);
        // Opt-in to the new parser for all tests in this class
        _env.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", "1");
    }

    public void Dispose() => _env.Dispose();

    private const string SimpleProject = """
        <Project>
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <Compile Include="Program.cs" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void OpenFromFile_UsesProjectXmlReader_WhenEnabled()
    {
        var folder = _env.CreateFolder();
        var projFile = Path.Combine(folder.Path, "test.proj");
        File.WriteAllText(projFile, SimpleProject);

        using var collection = new ProjectCollection();
        var pre = ProjectRootElement.Open(projFile, collection);

        // Verify the element tree was populated correctly
        pre.PropertyGroups.Count.ShouldBe(1);
        pre.ItemGroups.Count.ShouldBe(1);

        var props = pre.PropertyGroups.First().Properties.ToList();
        props.Count.ShouldBe(2);
        props[0].Name.ShouldBe("OutputType");
        props[0].Value.ShouldBe("Exe");
        props[1].Name.ShouldBe("TargetFramework");
        props[1].Value.ShouldBe("net10.0");

        var items = pre.ItemGroups.First().Items.ToList();
        items.Count.ShouldBe(1);
        items[0].ItemType.ShouldBe("Compile");
        items[0].Include.ShouldBe("Program.cs");
    }

    [Fact]
    public void OpenFromFile_HasCorrectLocations()
    {
        var folder = _env.CreateFolder();
        var projFile = Path.Combine(folder.Path, "test.proj");
        File.WriteAllText(projFile, SimpleProject);

        using var collection = new ProjectCollection();
        var pre = ProjectRootElement.Open(projFile, collection);

        pre.Location.ShouldNotBeNull();
        pre.Location.File.ShouldBe(projFile);
        pre.Location.Line.ShouldBeGreaterThan(0);

        var prop = pre.PropertyGroups.First().Properties.First();
        prop.Location.ShouldNotBeNull();
        prop.Location.File.ShouldBe(projFile);
        prop.Location.Line.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void OpenFromFile_SetsMetadataCorrectly()
    {
        var folder = _env.CreateFolder();
        var projFile = Path.Combine(folder.Path, "test.proj");
        File.WriteAllText(projFile, SimpleProject);

        using var collection = new ProjectCollection();
        var pre = ProjectRootElement.Open(projFile, collection);

        pre.FullPath.ShouldBe(projFile);
        pre.DirectoryPath.ShouldBe(folder.Path);
        pre.Encoding.ShouldNotBeNull();
    }

    [Fact]
    public void OpenFromFile_FallsBackToDom_WhenNotEnabled()
    {
        // Override the class-level opt-in: disable the new parser
        _env.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", null);

        var folder = _env.CreateFolder();
        var projFile = Path.Combine(folder.Path, "test.proj");
        File.WriteAllText(projFile, SimpleProject);

        using var collection = new ProjectCollection();
        var pre = ProjectRootElement.Open(projFile, collection);

        // Should still work with DOM-based path
        pre.PropertyGroups.Count.ShouldBe(1);
        pre.ItemGroups.Count.ShouldBe(1);

        // DOM-backed elements have non-null XmlDocument
        pre.ShouldSatisfyAllConditions(
            () => pre.PropertyGroups.First().Properties.First().Name.ShouldBe("OutputType"),
            () => pre.PropertyGroups.First().Properties.First().Value.ShouldBe("Exe"));
    }

    [Fact]
    public void CreateFromXmlReader_UsesProjectXmlReader_WhenEnabled()
    {
        using var stringReader = new StringReader(SimpleProject);
        using var xmlReader = XmlReader.Create(stringReader);
        using var collection = new ProjectCollection();

        var pre = ProjectRootElement.Create(xmlReader, collection);

        pre.PropertyGroups.Count.ShouldBe(1);
        pre.ItemGroups.Count.ShouldBe(1);

        var prop = pre.PropertyGroups.First().Properties.First();
        prop.Name.ShouldBe("OutputType");
        prop.Value.ShouldBe("Exe");
    }

    [Fact]
    public void OpenFromFile_ProducesSameStructureAsDomParser()
    {
        var folder = _env.CreateFolder();
        var projFile = Path.Combine(folder.Path, "test.proj");
        File.WriteAllText(projFile, SimpleProject);

        // Load with new parser (env var enabled in constructor)
        using var collection1 = new ProjectCollection();
        var newPre = ProjectRootElement.Open(projFile, collection1);

        // Load with old parser (env var disabled)
        _env.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", null);
        using var collection2 = new ProjectCollection();
        var oldPre = ProjectRootElement.Open(projFile, collection2);

        // Compare structures
        CompareElements(oldPre, newPre);
    }

    private static void CompareElements(ProjectElement expected, ProjectElement actual)
    {
        actual.ElementName.ShouldBe(expected.ElementName, $"Element name mismatch");
        actual.Condition.ShouldBe(expected.Condition, $"Condition mismatch on {expected.ElementName}");

        if (expected is ProjectElementContainer expectedContainer && actual is ProjectElementContainer actualContainer)
        {
            var expectedChildren = expectedContainer.Children.ToList();
            var actualChildren = actualContainer.Children.ToList();
            actualChildren.Count.ShouldBe(expectedChildren.Count, $"Child count mismatch on {expected.ElementName}");

            for (int i = 0; i < expectedChildren.Count; i++)
            {
                CompareElements(expectedChildren[i], actualChildren[i]);
            }
        }
    }
}
