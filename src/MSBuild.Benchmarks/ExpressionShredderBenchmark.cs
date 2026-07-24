// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks for <see cref="ExpressionShredder"/> covering the three entry points used
///  during evaluation: extracting referenced item names and metadata, enumerating item
///  expressions (transforms), and splitting semicolon-separated lists.
/// </summary>
/// <remarks>
///  Each entry point is a single parameterized benchmark driven by an <see cref="ArgumentsSourceAttribute"/>
///  (which is method-scoped, so the scenarios do not multiply across methods). The scenario argument is a
///  short label; the expression and its pre-built single-element list are resolved from dictionaries in
///  <see cref="GlobalSetup"/> so the measured region allocates nothing beyond what the shredder itself does.
///  Transform shapes are only exercised in depth under
///  <see cref="ExpressionShredder.GetReferencedItemExpressions(string)"/>, which builds the capture list;
///  <see cref="ExpressionShredder.GetReferencedItemNamesAndMetadata"/> walks transforms but builds no
///  captures from them, so a single representative transform is enough there.
/// </remarks>
[MemoryDiagnoser]
public class ExpressionShredderBenchmark
{
    // Scenarios: label -> expression. The label is what appears in the results table.
    private static readonly (string Label, string Expression)[] s_scenarios =
    [
        // Plain string with no expansion tokens: exercises the fast-path bail-out.
        ("Plain", "This is a plain string with no expansion tokens at all."),

        // Item expression shapes.
        ("SingleItem", "@(Compile)"),
        ("QuotedTransform", "@(Compile->'%(Filename).obj')"),
        ("FunctionTransform", "@(Compile->Distinct())"),
        ("FunctionTransformWithArguments", "@(Compile->Substring(0, 4))"),
        ("FunctionTransformWithQuotedArguments", "@(Compile->'%(Filename)'->Substring('()', $(Val), ')('))"),
        ("MultipleTransforms", "@(Compile->'%(Filename)'->Distinct()->Reverse())"),
        ("TransformWithSeparator", "@(Compile, ';')"),
        ("ChainedFunctionsWithWhitespace", "@(Compile->Distinct() -> Reverse() ->Count())"),

        // Metadata shapes.
        ("UnqualifiedMetadata", "%(Culture)"),
        ("QualifiedMetadata", "%(Compile.Culture)"),
        ("MultipleMetadata", "%(Culture)_%(Generator)"),

        // Mixed item + metadata + property.
        ("Mixed", @"$(OutputPath)\%(Culture)\@(Compile->'%(Filename)')"),

        // A realistic multi-reference expression drawn from the shredder tests.
        ("Realistic",
            "@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);" +
            "@(Compile);@(ManifestResourceWithNoCulture);$(ApplicationIcon);$(AssemblyOriginatorKeyFile);" +
            "@(ManifestNonResxWithNoCultureOnDisk);@(ReferencePath);@(CompiledLicenseFile);" +
            "@(EmbeddedDocumentation);$(Win32Resource);$(Win32Manifest);@(CustomAdditionalCompileInputs)"),

        // Long semicolon-separated list with semicolons embedded inside item separators
        // (which must NOT be treated as list delimiters).
        ("SemicolonList",
            "@(Compile->'%(FullPath)', ';');$(A);$(B);value1;value2;" +
            "@(Reference);%(Culture);value3;@(Content->'%(Filename)', ';');value4"),
    ];

    private Dictionary<string, string> _expressions = null!;
    private Dictionary<string, string[]> _lists = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _expressions = new(s_scenarios.Length);
        _lists = new(s_scenarios.Length);
        foreach ((string label, string expression) in s_scenarios)
        {
            _expressions[label] = expression;
            _lists[label] = [expression];
        }
    }

    // =========================================================================
    // GetReferencedItemNamesAndMetadata
    // Transforms are traversed but produce no captures here, so one representative
    // transform (MultipleTransforms) stands in for all transform shapes.
    // =========================================================================

    public static IEnumerable<object> NamesAndMetadataCases()
    {
        yield return "Plain";
        yield return "SingleItem";
        yield return "MultipleTransforms";
        yield return "TransformWithSeparator";
        yield return "UnqualifiedMetadata";
        yield return "QualifiedMetadata";
        yield return "MultipleMetadata";
        yield return "Mixed";
        yield return "Realistic";
    }

    [Benchmark]
    [ArgumentsSource(nameof(NamesAndMetadataCases))]
    public int NamesAndMetadata(string scenario)
    {
        ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata(_lists[scenario]);
        return (pair.Items?.Count ?? 0) + (pair.Metadata?.Count ?? 0);
    }

    // =========================================================================
    // GetReferencedItemExpressions (the transform/capture path; full shape matrix)
    // =========================================================================

    public static IEnumerable<object> ItemExpressionsCases()
    {
        yield return "SingleItem";
        yield return "QuotedTransform";
        yield return "FunctionTransform";
        yield return "FunctionTransformWithArguments";
        yield return "FunctionTransformWithQuotedArguments";
        yield return "MultipleTransforms";
        yield return "ChainedFunctionsWithWhitespace";
        yield return "Realistic";
    }

    [Benchmark]
    [ArgumentsSource(nameof(ItemExpressionsCases))]
    public int ItemExpressions(string scenario)
    {
        int count = 0;
        ExpressionShredder.ReferencedItemExpressionsEnumerator enumerator = ExpressionShredder.GetReferencedItemExpressions(_expressions[scenario]);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    // =========================================================================
    // SplitSemiColonSeparatedList
    // =========================================================================

    public static IEnumerable<object> SplitCases()
    {
        yield return "Realistic";
        yield return "SemicolonList";
    }

    [Benchmark]
    [ArgumentsSource(nameof(SplitCases))]
    public int Split(string scenario)
    {
        int count = 0;
        foreach (string _ in ExpressionShredder.SplitSemiColonSeparatedList(_expressions[scenario]))
        {
            count++;
        }

        return count;
    }
}
