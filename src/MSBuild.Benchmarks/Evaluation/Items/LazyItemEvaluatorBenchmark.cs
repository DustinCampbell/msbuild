// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

/// <summary>
///  Measures recording and materialization without XML loading or outer project evaluation.
/// </summary>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemEvaluatorBenchmark
{
    /// <summary>
    ///  The preconstructed scenario and its outer evaluation data.
    /// </summary>
    private LazyItemEvaluatorBenchmarkFixture _fixture = null!;

    /// <summary>
    ///  Gets or sets the independently measured CPU workload.
    /// </summary>
    [Params(
        LazyItemWorkload.LiteralIncludes,
        LazyItemWorkload.SemicolonInclude,
        LazyItemWorkload.ManyItemTypes,
        LazyItemWorkload.SparseReads,
        LazyItemWorkload.EveryStepRead,
        LazyItemWorkload.RepeatedRead,
        LazyItemWorkload.SnapshotUpdates,
        LazyItemWorkload.LiteralUpdates,
        LazyItemWorkload.OverlappingUpdates,
        LazyItemWorkload.MetadataWildcardUpdate,
        LazyItemWorkload.QualifiedUpdate,
        LazyItemWorkload.LiteralRemove,
        LazyItemWorkload.DuplicateRemove,
        LazyItemWorkload.MetadataRemove)]
    public LazyItemWorkload Workload { get; set; }

    /// <summary>
    ///  Gets or sets the number of input item identities.
    /// </summary>
    [Params(100, 1000)]
    public int ItemCount { get; set; }

    /// <summary>
    ///  Constructs and validates XML and expected output outside the measured operation.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => _fixture = new(Workload, ItemCount);

    /// <summary>
    ///  Removes the scenario's owned project and temporary files.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => _fixture.Dispose();

    /// <summary>
    ///  Records and materializes a fresh evaluator.
    /// </summary>
    /// <returns>
    ///  The checksum of its ordered identities and metadata.
    /// </returns>
    [Benchmark]
    public ulong Evaluate() => _fixture.Evaluate();
}

/// <summary>
///  Measures large histories separately from intentionally expensive earlier-state workloads.
/// </summary>
[BenchmarkCategory("Items", "ItemEvaluation", "Scaling")]
[MemoryDiagnoser]
public class LazyItemEvaluatorScalingBenchmark
{
    /// <summary>
    ///  The single-item Include scenario.
    /// </summary>
    private LazyItemEvaluatorBenchmarkFixture _includes = null!;

    /// <summary>
    ///  The disjoint literal Update scenario.
    /// </summary>
    private LazyItemEvaluatorBenchmarkFixture _updates = null!;

    /// <summary>
    ///  Gets or sets the large history size.
    /// </summary>
    [Params(10000)]
    public int ItemCount { get; set; }

    /// <summary>
    ///  Constructs and validates both large scenarios outside measurement.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _includes = new(LazyItemWorkload.LiteralIncludes, ItemCount);
        _updates = new(LazyItemWorkload.LiteralUpdates, ItemCount);
    }

    /// <summary>
    ///  Disposes the owned scenario data.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _includes.Dispose();
        _updates.Dispose();
    }

    /// <summary>
    ///  Measures a large append-only operation history.
    /// </summary>
    /// <returns>
    ///  The result checksum.
    /// </returns>
    [Benchmark]
    public ulong Includes() => _includes.Evaluate();

    /// <summary>
    ///  Measures disjoint updates against a large item list.
    /// </summary>
    /// <returns>
    ///  The result checksum.
    /// </returns>
    [Benchmark]
    public ulong Updates() => _updates.Evaluate();
}

/// <summary>
///  Measures recording cost separately from final item materialization.
/// </summary>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemEvaluatorRecordingBenchmark
{
    /// <summary>
    ///  The append-only scenario to record.
    /// </summary>
    private LazyItemEvaluatorBenchmarkFixture _fixture = null!;

    /// <summary>
    ///  Gets or sets the operation count.
    /// </summary>
    [Params(100, 10000)]
    public int OperationCount { get; set; }

    /// <summary>
    ///  Constructs and validates the scenario before recording is measured.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => _fixture = new(LazyItemWorkload.LiteralIncludes, OperationCount);

    /// <summary>
    ///  Disposes the scenario.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => _fixture.Dispose();

    /// <summary>
    ///  Constructs an evaluator and records its operations without demanding final results.
    /// </summary>
    /// <returns>
    ///  The evaluator, retained by BenchmarkDotNet's result consumer.
    /// </returns>
    [Benchmark]
    public object Record() => _fixture.Record();
}

/// <summary>
///  Measures evaluator glob handling with a bounded tree and a warmed file-entry cache.
/// </summary>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemEvaluatorGlobBenchmark
{
    /// <summary>
    ///  The scenario containing the precreated file tree.
    /// </summary>
    private LazyItemEvaluatorBenchmarkFixture _fixture = null!;

    /// <summary>
    ///  Gets or sets the glob/removal workload.
    /// </summary>
    [Params(
        LazyItemWorkload.GlobInclude,
        LazyItemWorkload.GlobRemove,
        LazyItemWorkload.GlobSubtreeRemove,
        LazyItemWorkload.ManyGlobRemoves,
        LazyItemWorkload.GlobReinclude,
        LazyItemWorkload.GlobEarlyRead)]
    public LazyItemWorkload Workload { get; set; }

    /// <summary>
    ///  Gets or sets the bounded file count.
    /// </summary>
    [Params(100, 500)]
    public int FileCount { get; set; }

    /// <summary>
    ///  Creates files and validates expected results outside measurement.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => _fixture = new(Workload, FileCount);

    /// <summary>
    ///  Disposes the collection and deletes the owned temporary file tree.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => _fixture.Dispose();

    /// <summary>
    ///  Records and evaluates the glob scenario with fresh evaluator state.
    /// </summary>
    /// <returns>
    ///  The result checksum.
    /// </returns>
    [Benchmark]
    public ulong Evaluate() => _fixture.Evaluate();
}

/// <summary>
///  Measures exclusion cardinality independently of item count.
/// </summary>
[BenchmarkCategory("Items", "ItemEvaluation")]
[MemoryDiagnoser]
public class LazyItemEvaluatorExcludeBenchmark
{
    /// <summary>
    ///  The literal Include and its nonmatching exclusions.
    /// </summary>
    private LazyItemEvaluatorBenchmarkFixture _fixture = null!;

    /// <summary>
    ///  Gets or sets the number of stored exclusion patterns.
    /// </summary>
    [Params(0, 4, 128, 16384)]
    public int ExcludeCount { get; set; }

    /// <summary>
    ///  Constructs and validates the fixed-size input before measurement.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup() => _fixture = new(LazyItemWorkload.Excludes, itemCount: 100, ExcludeCount);

    /// <summary>
    ///  Disposes the owned scenario.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup() => _fixture.Dispose();

    /// <summary>
    ///  Measures the stored and expanded Exclude handling.
    /// </summary>
    /// <returns>
    ///  The result checksum.
    /// </returns>
    [Benchmark]
    public ulong Evaluate() => _fixture.Evaluate();
}
