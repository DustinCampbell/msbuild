// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;
using Microsoft.NET.StringTools;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures the allocation pressure that string interning hides in <see cref="ExpressionShredder"/>.
/// </summary>
/// <remarks>
///  The shredder interns the fragments it produces (item names, metadata names, split list
///  elements) via <see cref="Strings.WeakIntern(System.ReadOnlySpan{char})"/>. With a warm cache
///  those interns are hits, so a benchmark that splits the same expression repeatedly reports
///  near-zero allocation even though the shredder is conceptually producing many substrings. That
///  makes the "warm" numbers a poor proxy for the allocations the StringSegment work aims to remove.
/// <para>
///  To get a stable measure of the underlying allocation, each benchmark runs twice via
///  <see cref="BypassInterning"/>: with interning enabled (the production steady state) and with it
///  bypassed so every intern allocates a fresh string and never touches the cache. The bypassed
///  column is deterministic and independent of cache warmth, so it is the meaningful before/after
///  baseline for the shredder refactor.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class ExpressionShredderAllocationBenchmark
{
    private const string ItemExpressionsExpression = "@(Compile->'%(Filename)'->Distinct()->Reverse())";

    private const string SplitExpression =
        "@(Compile->'%(FullPath)', ';');$(A);$(B);value1;value2;" +
        "@(Reference);%(Culture);value3;@(Content->'%(Filename)', ';');value4";

    /// <summary>
    ///  When true, interning is bypassed so every fragment allocates a fresh string; when false,
    ///  the production interning path (warm cache) is used.
    /// </summary>
    [Params(false, true)]
    public bool BypassInterning { get; set; }

    [GlobalSetup]
    public void GlobalSetup() => Strings.DisableInterning = BypassInterning;

    [GlobalCleanup]
    public void GlobalCleanup() => Strings.DisableInterning = false;

    [Benchmark]
    public int ItemExpressions()
    {
        int count = 0;
        ExpressionShredder.ReferencedItemExpressionsEnumerator enumerator = ExpressionShredder.GetReferencedItemExpressions(ItemExpressionsExpression);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int Split()
    {
        int count = 0;
        foreach (string _ in ExpressionShredder.SplitSemiColonSeparatedList(SplitExpression))
        {
            count++;
        }

        return count;
    }
}
