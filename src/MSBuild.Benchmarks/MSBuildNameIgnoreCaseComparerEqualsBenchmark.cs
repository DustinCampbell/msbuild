// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build;
using Microsoft.Build.Collections;

namespace MSBuild.Benchmarks;

/// <summary>
///  Compares the previous and current case-insensitive equality algorithms for MSBuild names.
/// </summary>
[MemoryDiagnoser]
public class MSBuildNameIgnoreCaseComparerEqualsBenchmark
{
    private const int SegmentOffset = 2;

    private string _name = null!;
    private string _nameBuffer = null!;

    public enum ComparisonScenario
    {
        Equal,
        FirstCharacterMismatch,
        LastCharacterMismatch,
    }

    [Params(1, 4, 8, 16, 32, 64)]
    public int Length { get; set; }

    [ParamsAllValues]
    public ComparisonScenario Scenario { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _name = new string('a', Length);

        char[] constrainedName = new string('A', Length).ToCharArray();
        switch (Scenario)
        {
            case ComparisonScenario.FirstCharacterMismatch:
                constrainedName[0] = 'B';
                break;

            case ComparisonScenario.LastCharacterMismatch:
                constrainedName[Length - 1] = 'B';
                break;
        }

        _nameBuffer = $"zz{new string(constrainedName)}zz";

        Assumed.Equal(Previous(), Current(), $"The previous and current equality implementations produced different results for scenario {Scenario} with length {Length}.");
    }

    [Benchmark(Baseline = true)]
    public bool Previous()
        => LegacyMSBuildNameIgnoreCaseComparer.Default.Equals(_name, _nameBuffer, SegmentOffset, Length);

    [Benchmark]
    public bool Current()
        => MSBuildNameIgnoreCaseComparer.Default.Equals(_name, _nameBuffer, SegmentOffset, Length);
}
