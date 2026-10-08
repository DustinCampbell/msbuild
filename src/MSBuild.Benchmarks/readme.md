# MSBuild Benchmarks

This project contains performance benchmarks for MSBuild using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

### Run Benchmarks Across Supported TFMs

On Windows, `Run-Benchmarks.ps1` runs each selected benchmark on both `net472` and `net11.0`.
Artifacts are kept separate under `artifacts\BenchmarkDotNet\<TFM>`.

```powershell
cd src/MSBuild.Benchmarks
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*"
```

Use `-Set` to run named benchmark sets without writing filter patterns:

```powershell
.\Run-Benchmarks.ps1 -Set Expansion
.\Run-Benchmarks.ps1 -Set PropertyExpansion
.\Run-Benchmarks.ps1 -Set PropertyFunctions
```

Multiple sets are combined with OR. Some sets are umbrellas for narrower sets:

| Umbrella set | Included sets |
| --- | --- |
| `Expansion` | `PropertyExpansion`, `PropertyExpansionScaling`, `PropertyFunctions`, `ItemExpansion`, `ItemFunctions`, `MetadataExpansion`, `MetadataExpansionScaling`, `MixedExpansion` |
| `PropertyExpansion` | Regular property expansion and `PropertyFunctions` |
| `PropertyExpansionScaling` | Property-reference scaling and `PropertyBagCardinality` |
| `ItemExpansion` | Regular item expansion and `ItemFunctions` |
| `Conditions` | `ConditionParsing`, `ConditionEvaluation` |
| `ExpressionShredder` | `ExpressionShredderThroughput` |
| `Items` | `ItemEvaluation` |

Scaling sets remain separate. For example, `-Set MetadataExpansion` excludes
`MetadataExpansionScaling`. The cross-cutting `Scaling` set contains both property- and
metadata-expansion scaling benchmarks. `PropertyBagCardinality` varies the number of unreferenced
properties while keeping the referenced properties and expression shapes fixed.

`ExpressionShredderAllocations` is an opt-in cold-cache diagnostic for allocation-focused
shredder work and is not included in the broad `ExpressionShredder` set.

Common BenchmarkDotNet options are exposed directly:

```powershell
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -Job short -DisableNGen
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -LaunchCount 3
```

Use `-CollectEtw`, `-DisableInlining`, or `-EnforcePowerPlan` for the other custom options.
Less common BenchmarkDotNet arguments can still be passed with `-BenchmarkDotNetArguments`.

Use `-All` to explicitly run every benchmark, or `-Framework` to override the target frameworks:

```powershell
.\Run-Benchmarks.ps1 -All
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" -Framework net11.0
```

### Choose a Run Mode

Use `-Job dry` or `-Job short` only to confirm that benchmarks build and execute. These jobs do
not collect enough data for performance conclusions.

For exploratory measurements, omit `-Job` and `-LaunchCount`. BenchmarkDotNet will adapt its
warmup and measurement iterations within one process launch. For a final comparison, use three
independent launches on the target framework:

```powershell
.\Run-Benchmarks.ps1 -Filter "*MetadataExpansionBenchmark*" `
    -Framework net11.0 -LaunchCount 3
```

Each launch performs a complete benchmark run, so this approximately triples execution time.

The runner leaves the current OS power plan unchanged by default. Configure dedicated benchmark
machines with a stable performance-oriented power plan. Alternatively, use `-EnforcePowerPlan` to
allow BenchmarkDotNet to temporarily select the High Performance plan on Windows and restore the
previous plan when the run completes. If the process terminates abruptly, the plan may need to be
restored manually.

SDK selection honors the repository's `global.json`, including compatible patch roll-forward.
The runner reports the resolved SDK directory and pins benchmark child hosts to that installation.

Compare results only when the target framework, architecture, runtime, and machine environment
match. In particular, absolute `net472` and `net11.0` results are not directly comparable.

### Lazy Item Evaluator

`LazyItemEvaluatorBenchmark` constructs the evaluator directly using preconstructed item XML and
outer project data. Each invocation records and materializes fresh evaluator state, then consumes
ordered item identities and metadata. Setup validates identities, metadata, order, and provenance.
XML loading, outer project evaluation, and file creation are not timed.

The related classes isolate recording, 10,000-operation scaling, glob/removal behavior, and Exclude
cardinality. Earlier-state workloads include sparse reads, reads of every growing prefix, repeated
reads of one state, and reads after updates. The glob fixture deliberately shares a warmed
evaluation-context file cache. `LazyItemEvaluationBenchmark` remains the full, cold-project
evaluation control, including collection construction and XML loading.

Use the same selection and launch count before and after an evaluator change:

```powershell
.\Run-Benchmarks.ps1 -Filter '*LazyItem*' -LaunchCount 3 `
    -ArtifactsPath ..\..\artifacts\BenchmarkDotNet\LazyItems\baseline

.\Run-Benchmarks.ps1 -Filter '*LazyItem*' -LaunchCount 3 `
    -ArtifactsPath ..\..\artifacts\BenchmarkDotNet\LazyItems\candidate
```

Compare the same benchmark and parameters across revisions, not unrelated workload ratios within
one run. Preserve each revision's artifacts and record the commit and runtime. Check allocations
alongside time, especially for earlier-state reads: reducing tree overhead must not introduce
quadratic copying or excessive retained memory. Allocation totals alone do not measure peak
retention. Keep the runner's SDK, inlining, and power-plan settings identical between comparisons.

### Run Benchmarks on a Specific TFM

```
cd src/MSBuild.Benchmarks
dotnet run -c Release -f net472
dotnet run -c Release -f net11.0
```

### Filter to a Specific Benchmark Class

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark*"
```

### Filter to a Single Benchmark Method

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark.IncludeOnly"
```
## Command-Line Options

### Custom Options

- `--collect-etw` - Enable ETW (Event Tracing for Windows) profiling diagnostics
- `--disable-ngen` - Disable NGEN/ReadyToRun to measure pure JIT performance
- `--disable-inlining` - Disable JIT inlining for more accurate method-level profiling
- `--enforce-power-plan` - Allow BenchmarkDotNet to select High Performance on Windows

These custom options can be combined with any BenchmarkDotNet options:

```
dotnet run -c Release -f net11.0 -- --filter "*ItemSpecModifiersBenchmark*" --job short --disable-ngen
```
