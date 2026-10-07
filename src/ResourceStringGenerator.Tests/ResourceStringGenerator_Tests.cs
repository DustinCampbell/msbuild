// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.ResourceStringGenerator.Tests;

public class ResourceStringGeneratorTests
{
    [Fact]
    public void GeneratesExplicitCatalogFromExactResourceKeys()
    {
        TestAdditionalText resources = CreateResources(
            "Strings.resx",
            """
            <root>
              <data name="Simple"><value>Simple text</value></data>
              <data name="Dotted.Resource"><value>Dotted text</value></data>
              <data name="class"><value>Keyword text</value></data>
            </root>
            """,
            new Dictionary<string, string>
            {
                ["ResourceCatalogNamespace"] = "Example.Resources",
                ["ResourceCatalogClass"] = "ExampleStrings",
                ["ResourceCatalogResourceAccessorType"] = "global::Example.Resources.SR",
                ["ResourceCatalogHelpKeywordPrefix"] = "Example.",
            });

        GeneratorDriverRunResult result = RunGenerator(resources);

        result.Diagnostics.ShouldBeEmpty();
        GeneratedSourceResult generatedSource = result.Results.Single().GeneratedSources.Single();
        string source = generatedSource.SourceText.ToString();
        source.ShouldContain("namespace Example.Resources;");
        source.ShouldContain("internal static partial class ExampleStrings");
        source.ShouldContain("global::Example.Resources.SR.ResourceManager");
        source.ShouldContain("static () => global::Example.Resources.SR.Culture");
        source.ShouldContain("ResourceString Dotted_Resource");
        source.ShouldContain("\"Dotted.Resource\"");
        source.ShouldContain("ResourceString @class");
        source.ShouldContain("\"Example.\"");
        source.ShouldContain("\"Dotted.Resource\" => Dotted_Resource");
        CSharpSyntaxTree.ParseText(source).GetDiagnostics().ShouldBeEmpty();
    }

    [Fact]
    public void CombinesMultipleResourceFilesIntoOneCatalog()
    {
        TestAdditionalText primaryResources = CreateResources(
            "Primary.resx",
            """
            <root>
              <data name="Primary"><value>Primary text</value></data>
            </root>
            """,
            CreateDirectConfiguration(
                resourceManagerExpression: "global::Example.PrimaryResources",
                cultureExpression: "global::Example.PrimaryCulture"));
        TestAdditionalText secondaryResources = CreateResources(
            "Secondary.resx",
            """
            <root>
              <data name="Secondary"><value>Secondary text</value></data>
            </root>
            """,
            CreateDirectConfiguration(
                resourceManagerExpression: "global::Example.SecondaryResources",
                cultureExpression: null));

        GeneratorDriverRunResult result = RunGenerator(primaryResources, secondaryResources);

        result.Diagnostics.ShouldBeEmpty();
        GeneratedSourceResult generatedSource = result.Results.Single().GeneratedSources.Single();
        string source = generatedSource.SourceText.ToString();
        source.ShouldContain("ResourceString Primary");
        source.ShouldContain("ResourceString Secondary");
        source.ShouldContain("global::Example.PrimaryResources");
        source.ShouldContain("global::Example.SecondaryResources");
    }

    [Fact]
    public void ReportsDuplicateResourceKeys()
    {
        TestAdditionalText first = CreateResources(
            "First.resx",
            """
            <root>
              <data name="Duplicate"><value>First</value></data>
            </root>
            """,
            CreateDirectConfiguration("global::Example.First", cultureExpression: null));
        TestAdditionalText second = CreateResources(
            "Second.resx",
            """
            <root>
              <data name="Duplicate"><value>Second</value></data>
            </root>
            """,
            CreateDirectConfiguration("global::Example.Second", cultureExpression: null));

        GeneratorDriverRunResult result = RunGenerator(first, second);

        result.Diagnostics.Select(static diagnostic => diagnostic.Id).ShouldContain("MSBRSG003");
    }

    [Fact]
    public void ReportsGeneratedIdentifierCollisions()
    {
        TestAdditionalText resources = CreateResources(
            "Strings.resx",
            """
            <root>
              <data name="Dotted.Resource"><value>Dotted</value></data>
              <data name="Dotted_Resource"><value>Underscore</value></data>
            </root>
            """,
            CreateDirectConfiguration("global::Example.Resources", cultureExpression: null));

        GeneratorDriverRunResult result = RunGenerator(resources);

        Diagnostic diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "MSBRSG004");
        diagnostic.GetMessage().ShouldContain("Dotted.Resource");
        diagnostic.GetMessage().ShouldContain("Dotted_Resource");
    }

    [Fact]
    public void ReportsGeneratedCatalogMemberCollisions()
    {
        TestAdditionalText resources = CreateResources(
            "Strings.resx",
            """
            <root>
              <data name="GetResourceString"><value>Lookup</value></data>
            </root>
            """,
            CreateDirectConfiguration("global::Example.Resources", cultureExpression: null));

        GeneratorDriverRunResult result = RunGenerator(resources);

        Diagnostic diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "MSBRSG006");
        diagnostic.GetMessage().ShouldContain("GetResourceString");
    }

    [Fact]
    public void IgnoresAdditionalFilesWithoutCatalogMetadata()
    {
        TestAdditionalText resources = CreateResources(
            "Unconfigured.resx",
            """
            <root>
              <data name="Ignored"><value>Ignored</value></data>
            </root>
            """,
            []);

        GeneratorDriverRunResult result = RunGenerator(resources);

        result.Diagnostics.ShouldBeEmpty();
        result.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    private static Dictionary<string, string> CreateDirectConfiguration(
        string resourceManagerExpression,
        string? cultureExpression)
    {
        var configuration = new Dictionary<string, string>
        {
            ["ResourceCatalogNamespace"] = "Example.Resources",
            ["ResourceCatalogClass"] = "ExampleStrings",
            ["ResourceCatalogResourceManagerExpression"] = resourceManagerExpression,
            ["ResourceCatalogHelpKeywordPrefix"] = "MSBuild.",
        };

        if (cultureExpression is not null)
        {
            configuration["ResourceCatalogCultureExpression"] = cultureExpression;
        }

        return configuration;
    }

    private static TestAdditionalText CreateResources(
        string path,
        string content,
        Dictionary<string, string> metadata)
        => new(path, content, metadata);

    private static GeneratorDriverRunResult RunGenerator(params TestAdditionalText[] resources)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText("internal sealed class Placeholder { }")],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ISourceGenerator generator = new ResourceStringGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator],
            resources.Cast<AdditionalText>().ToImmutableArray(),
            new CSharpParseOptions(LanguageVersion.Preview),
            new TestAnalyzerConfigOptionsProvider(resources));

        return driver.RunGenerators(compilation).GetRunResult();
    }

    private sealed class TestAdditionalText(
        string path,
        string content,
        Dictionary<string, string> metadata) : AdditionalText
    {
        private readonly SourceText _text = SourceText.From(content);

        public Dictionary<string, string> Metadata { get; } = metadata;

        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default)
            => _text;
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions s_emptyOptions = new TestAnalyzerConfigOptions([]);
        private readonly Dictionary<string, AnalyzerConfigOptions> _optionsByPath;

        public TestAnalyzerConfigOptionsProvider(IEnumerable<TestAdditionalText> resources)
        {
            _optionsByPath = resources.ToDictionary(
                static resource => resource.Path,
                static resource => (AnalyzerConfigOptions)new TestAnalyzerConfigOptions(resource.Metadata));
        }

        public override AnalyzerConfigOptions GlobalOptions => s_emptyOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
            => s_emptyOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => _optionsByPath[textFile.Path];
    }

    private sealed class TestAnalyzerConfigOptions(Dictionary<string, string> metadata) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            const string MetadataPrefix = "build_metadata.AdditionalFiles.";

            if (key.StartsWith(MetadataPrefix, StringComparison.Ordinal))
            {
                return metadata.TryGetValue(key.Substring(MetadataPrefix.Length), out value);
            }

            value = null;
            return false;
        }
    }
}
