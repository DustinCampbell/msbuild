// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
#if !NET
using Microsoft.Build;
#endif
using Microsoft.Build.Collections;

namespace MSBuild.Benchmarks;

/// <summary>
///  Compares the previous and current hash-code algorithms for MSBuild names.
/// </summary>
[MemoryDiagnoser]
public class MSBuildNameIgnoreCaseComparerGetHashCodeBenchmark
{
    private const string Name = "aBcDeFgHiJkLmNoPqRsTuVwXyZaBcDeFgHiJkLmNoPqRsTuVwXyZaBcDeFgHiJkL";
    private const int SegmentOffset = 2;

    private string _nameBuffer = null!;

    [Params(1, 2, 3, 4, 5, 6, 7, 8, 16, 32, 64)]
    public int Length { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _nameBuffer = $"zz{Name.Substring(0, Length)}zz";

#if !NET
        Assumed.Equal(Previous(), Current(), $"The previous and current hash-code implementations produced different results for length {Length}.");
#endif
    }

    [Benchmark(Baseline = true)]
    public int Previous()
        => LegacyMSBuildNameIgnoreCaseComparer.Default.GetHashCode(_nameBuffer, SegmentOffset, Length);

    [Benchmark]
    public int Current()
        => MSBuildNameIgnoreCaseComparer.Default.GetHashCode(_nameBuffer, SegmentOffset, Length);
}
