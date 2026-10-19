// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build;

namespace MSBuild.Benchmarks;

/// <summary>
///  Generates deterministic in-memory declarations and models their expected results outside benchmark timing.
/// </summary>
internal sealed class LazyItemBenchmarkScenario
{
    private const int OperationCount = 32;
    private const string InitialTrace = "r";
    private readonly StringBuilder _xml = new("<Project>");
    private readonly List<(string Include, string Trace, int Order)> _source = [];
    private readonly List<(string ItemType, string Include, string Trace, int Order)> _otherItems = [];
    private int _nextOrder;

    private LazyItemBenchmarkScenario()
    {
    }

    public string ProjectXml { get; private set; } = null!;

    public int SourceCount => _source.Count;

    public List<string> Files { get; } = [];

    public List<(string ItemType, string Include, string Trace)> ExpectedItems { get; } = [];

    public static LazyItemBenchmarkScenario CreateOperations(LazyItemOperationBenchmark.WorkloadKind workload, int itemCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, OperationCount * 10);

        LazyItemBenchmarkScenario scenario = new();

        if (workload == LazyItemOperationBenchmark.WorkloadKind.MetadataRemove)
        {
            scenario.AppendItem("RemoveKey", "key", "value0");
            for (int item = 0; item < itemCount; item++)
            {
                scenario.AppendItem("Source", ItemName(item), $"value{item % 4}");
            }

            scenario._xml.Append(
                """<ItemGroup><Source Remove="@(RemoveKey)" MatchOnMetadata="Trace" /></ItemGroup>""");

            scenario._source.RemoveAll(static item => item.Trace == "value0");
            return scenario.Complete();
        }

        bool duplicates = workload is LazyItemOperationBenchmark.WorkloadKind.IndexedDuplicatePathRemoves
                                   or LazyItemOperationBenchmark.WorkloadKind.BulkDuplicatePathRemoves;
        bool individualIncludes = workload is LazyItemOperationBenchmark.WorkloadKind.IndividualIncludes;
        scenario.AppendItems(0, itemCount, duplicates, individualIncludes);

        switch (workload)
        {
            case LazyItemOperationBenchmark.WorkloadKind.MetadataWildcardUpdate:
                scenario._xml.Append("""<ItemGroup><Source Update="*"><Trace>%(Filename)</Trace></Source></ItemGroup>""");
                for (int index = 0; index < scenario._source.Count; index++)
                {
                    var item = scenario._source[index];
                    scenario._source[index] = (item.Include, Path.GetFileNameWithoutExtension(item.Include), item.Order);
                }

                return scenario.Complete();

            case LazyItemOperationBenchmark.WorkloadKind.BulkLiteralRemoves:
            case LazyItemOperationBenchmark.WorkloadKind.BulkDuplicatePathRemoves:
                HashSet<string> removed = [with(StringComparer.Ordinal)];
                for (int item = 0; item < itemCount; item += 2)
                {
                    removed.Add(ItemName(item));
                }

                scenario.AppendRemove(string.Join(";", removed), include => removed.Contains(include.StartsWith("./", StringComparison.Ordinal)
                    ? include.Substring(2)
                    : include));

                return scenario.Complete();
        }

        for (int operation = 0; operation < OperationCount; operation++)
        {
            switch (workload)
            {
                case LazyItemOperationBenchmark.WorkloadKind.IncludeOnly:
                case LazyItemOperationBenchmark.WorkloadKind.IndividualIncludes:
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.DisjointLiteralUpdates:
                    scenario.AppendUpdate(ItemName(operation), index => index == operation);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.OverlappingLiteralUpdates:
                    scenario.AppendUpdate(ItemName(0), static index => index == 0);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.SparseWildcardUpdates:
                    scenario.AppendUpdate("*.cs", static index => index % 10 == 0);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.DenseWildcardUpdates:
                    scenario.AppendUpdate("*", static _ => true);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.IndexedLiteralRemoves:
                    string literal = ItemName(operation);
                    scenario.AppendRemove(literal, item => item == literal);
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.IndexedWildcardRemoves:
                    string prefix = $"item{operation:D5}";
                    scenario.AppendRemove($"{prefix}?.*", item => item.StartsWith(prefix, StringComparison.Ordinal));
                    break;

                case LazyItemOperationBenchmark.WorkloadKind.IndexedDuplicatePathRemoves:
                    string path = ItemName(operation);
                    scenario.AppendRemove($"{path};./{path};{path}", item => item == path || item == $"./{path}");
                    break;

                default:
                    Assumed.Unreachable($"Unexpected workload: {workload}");
                    break;
            }
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateHistory(LazyItemHistoryBenchmark.WorkloadKind workload, int operationCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(operationCount, 1);

        LazyItemBenchmarkScenario scenario = new();

        switch (workload)
        {
            case LazyItemHistoryBenchmark.WorkloadKind.DisjointLiteralUpdates:
            case LazyItemHistoryBenchmark.WorkloadKind.OverlappingLiteralUpdates:
                {
                    scenario.AppendItems(0, operationCount);
                    for (int operation = 0; operation < operationCount; operation++)
                    {
                        int target = workload is LazyItemHistoryBenchmark.WorkloadKind.DisjointLiteralUpdates
                            ? operation
                            : operation % Math.Min(8, operationCount);

                        scenario.AppendUpdate(ItemName(target), index => index == target);
                    }

                    break;
                }

            case LazyItemHistoryBenchmark.WorkloadKind.IndividualIncludes:
            case LazyItemHistoryBenchmark.WorkloadKind.InterleavedItemTypes:
                {
                    for (int operation = 0; operation < operationCount; operation++)
                    {
                        string itemType = workload is LazyItemHistoryBenchmark.WorkloadKind.InterleavedItemTypes
                            ? $"Type{operation % 32}"
                            : "Source";

                        scenario.AppendItem(itemType, ItemName(operation), InitialTrace);
                    }

                    break;
                }

            default:
                Assumed.Unreachable($"Unexpected workload: {workload}");
                break;
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateCheckpoints(LazyItemCheckpointBenchmark.WorkloadKind workload, int itemCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, 1);

        LazyItemBenchmarkScenario scenario = new();

        switch (workload)
        {
            case LazyItemCheckpointBenchmark.WorkloadKind.SparseGrowingReads:
            case LazyItemCheckpointBenchmark.WorkloadKind.EveryGrowingRead:
                for (int item = 0; item < itemCount; item++)
                {
                    scenario.AppendItem("Source", ItemName(item), InitialTrace);
                    if (workload == LazyItemCheckpointBenchmark.WorkloadKind.EveryGrowingRead || (item + 1) % 100 == 0)
                    {
                        scenario.AppendReadCount();
                    }
                }

                break;

            case LazyItemCheckpointBenchmark.WorkloadKind.ReadAfterEveryUpdate:
                scenario.AppendItems(0, itemCount);
                for (int item = 0; item < itemCount; item++)
                {
                    scenario.AppendUpdate(ItemName(item), index => index == item);
                    scenario.AppendReadCount();
                }

                break;

            case LazyItemCheckpointBenchmark.WorkloadKind.RepeatedCurrentReads:
                scenario.AppendItems(0, itemCount);
                for (int read = 0; read < 100; read++)
                {
                    scenario.AppendReadCount();
                }

                break;

            default:
                Assumed.Unreachable($"Unexpected workload: {workload}");
                break;
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateQualifiedUpdate(int sourceCount, int targetCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetCount, 1);

        LazyItemBenchmarkScenario scenario = new();

        for (int source = 0; source < sourceCount; source++)
        {
            scenario.AppendItem("Lookup", ItemName(source), $"lookup{source}");
        }

        scenario.AppendItems(0, targetCount);
        scenario._xml.Append("""<ItemGroup><Source Update="@(Lookup)"><Trace>%(Lookup.Trace)</Trace></Source></ItemGroup>""");

        for (int target = 0; target < Math.Min(sourceCount, targetCount); target++)
        {
            (string? include, string? trace, int order) = scenario._source[target];
            scenario._source[target] = (include, $"lookup{target}", order);
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateExcludes(int itemCount, int excludeCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(excludeCount);

        LazyItemBenchmarkScenario scenario = new();

        string[] includes = new string[itemCount];
        for (int item = 0; item < itemCount; item++)
        {
            includes[item] = ItemName(item);
            scenario._source.Add((includes[item], InitialTrace, scenario._nextOrder++));
        }

        string[] excludes = new string[excludeCount];
        for (int exclude = 0; exclude < excludeCount; exclude++)
        {
            excludes[exclude] = $"excluded{exclude:D5}.txt";
        }

        scenario._xml.Append(
            $"""<ItemGroup><Source Include="{string.Join(";", includes)}" Exclude="{string.Join(";", excludes)}"><Trace>{InitialTrace}</Trace></Source></ItemGroup>""");

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateGlobs(LazyItemGlobBenchmark.WorkloadKind workload, int fileCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fileCount, 1);

        LazyItemBenchmarkScenario scenario = new();

        for (int file = 0; file < fileCount; file++)
        {
            scenario.Files.Add(Path.Combine("src", $"dir{file % 10}", $"file{file:D5}.cs"));
        }

        scenario.Files.Sort(StringComparer.OrdinalIgnoreCase);
        string glob = Path.Combine("src", "**", "*.cs");
        scenario.AppendGlob(glob, InitialTrace);

        switch (workload)
        {
            case LazyItemGlobBenchmark.WorkloadKind.EarlyRead:
            case LazyItemGlobBenchmark.WorkloadKind.ManyRemovesAfterRead:
                scenario.AppendReadCount();
                break;

            case LazyItemGlobBenchmark.WorkloadKind.ManyRemovesAfterCapture:
                scenario._xml.Append("""<ItemGroup><Captured Include="@(Source)" /></ItemGroup>""");

                foreach ((string include, string trace, int order) in scenario._source)
                {
                    scenario._otherItems.Add(("Captured", include, trace, scenario._nextOrder++));
                }

                break;
        }

        switch (workload)
        {
            case LazyItemGlobBenchmark.WorkloadKind.IncludeOnly:
                break;

            case LazyItemGlobBenchmark.WorkloadKind.RemoveAll:
            case LazyItemGlobBenchmark.WorkloadKind.Reinclude:
            case LazyItemGlobBenchmark.WorkloadKind.EarlyRead:
                scenario.AppendRemove(glob, static _ => true);
                break;

            case LazyItemGlobBenchmark.WorkloadKind.SubtreeRemove:
                string subtree = Path.Combine("src", "dir0");
                scenario.AppendRemove(Path.Combine(subtree, "**", "*.cs"), include => Path.GetDirectoryName(include) == subtree);
                break;

            case LazyItemGlobBenchmark.WorkloadKind.ManyRemoves:
            case LazyItemGlobBenchmark.WorkloadKind.ManyRemovesAfterRead:
            case LazyItemGlobBenchmark.WorkloadKind.ManyRemovesAfterCapture:
                for (int file = 0; file < fileCount; file++)
                {
                    string name = $"file{file:D5}.cs";
                    scenario.AppendRemove(Path.Combine("src", "**", name), include => Path.GetFileName(include) == name);
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(workload));
        }

        if (workload is LazyItemGlobBenchmark.WorkloadKind.Reinclude)
        {
            scenario.AppendGlob(glob, "reinserted");
        }

        return scenario.Complete();
    }

    public static LazyItemBenchmarkScenario CreateSnapshots(LazyItemSnapshotBenchmark.WorkloadKind workload, int itemCount, int referenceCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, OperationCount);
        ArgumentOutOfRangeException.ThrowIfNegative(referenceCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(referenceCount, OperationCount);

        if (referenceCount != 0 && OperationCount % referenceCount != 0)
        {
            throw new ArgumentException("Reference count must divide the operation count.", nameof(referenceCount));
        }

        LazyItemBenchmarkScenario scenario = new();
        if (workload is not LazyItemSnapshotBenchmark.WorkloadKind.Append)
        {
            scenario.AppendItems(0, itemCount);
        }

        for (int operation = 0; operation < OperationCount; operation++)
        {
            switch (workload)
            {
                case LazyItemSnapshotBenchmark.WorkloadKind.Append:
                    int start = operation * itemCount / OperationCount;
                    int end = (operation + 1) * itemCount / OperationCount;
                    scenario.AppendItems(start, end);
                    break;

                case LazyItemSnapshotBenchmark.WorkloadKind.SparseUpdates:
                    scenario.AppendUpdate(ItemName(0), static index => index == 0);
                    break;

                case LazyItemSnapshotBenchmark.WorkloadKind.DenseUpdates:
                    scenario.AppendUpdate("*", static _ => true);
                    break;

                default:
                    Assumed.Unreachable($"Unexpected workload: {workload}");
                    break;
            }

            if (referenceCount > 0 && (operation + 1) % (OperationCount / referenceCount) == 0)
            {
                scenario.AppendCapture(operation);
            }
        }

        return scenario.Complete();
    }

    private void AppendItems(int start, int end, bool duplicates = false, bool individualIncludes = false)
    {
        _xml.Append("<ItemGroup>");

        StringBuilder includes = new();
        for (int item = start; item < end; item++)
        {
            string include = ItemName(item);

            if (item == 0)
            {
                _xml.Append($"<Source Include=\"{include}\"><Trace>{InitialTrace}</Trace><Track>true</Track></Source>");
            }
            else if (individualIncludes)
            {
                _xml.Append($"<Source Include=\"{include}\"><Trace>{InitialTrace}</Trace></Source>");
            }
            else
            {
                includes.Append($"{include};");
            }

            _source.Add((include, InitialTrace, _nextOrder++));

            if (duplicates)
            {
                includes.Append($"./{include};");
                _source.Add(($"./{include}", InitialTrace, _nextOrder++));
            }
        }

        if (includes.Length > 0)
        {
            _xml.Append($"<Source Include=\"{includes}\"><Trace>{InitialTrace}</Trace></Source>");
        }

        _xml.Append("</ItemGroup>");
    }

    private void AppendUpdate(string itemSpec, Predicate<int> match)
    {
        _xml.Append($"<ItemGroup><Source Update=\"{itemSpec}\"><Trace>%(Trace)x</Trace></Source></ItemGroup>");

        for (int index = 0; index < _source.Count; index++)
        {
            if (match(index))
            {
                (string? include, string? trace, int order) = _source[index];
                _source[index] = (include, trace + "x", order);
            }
        }
    }

    private void AppendItem(string itemType, string include, string trace)
    {
        _xml.Append($"""<ItemGroup><{itemType} Include="{include}"><Trace>{trace}</Trace></{itemType}></ItemGroup>""");

        if (itemType == "Source")
        {
            _source.Add((include, trace, _nextOrder++));
        }
        else
        {
            _otherItems.Add((itemType, include, trace, _nextOrder++));
        }
    }

    private void AppendGlob(string glob, string trace)
    {
        _xml.Append($"""<ItemGroup><Source Include="{glob}"><Trace>{trace}</Trace></Source></ItemGroup>""");

        foreach (string file in Files)
        {
            _source.Add((file, trace, _nextOrder++));
        }
    }

    private void AppendReadCount()
        // A wrong condition read must produce unexpected output instead of silently skipping an empty probe.
        => _xml.Append(
            $"""<ItemGroup Condition="'@(Source->Count())' != '{_source.Count}'"><ReadMismatch Include="incorrect-count" /></ItemGroup>""");

    private void AppendRemove(string itemSpec, Predicate<string> match)
    {
        _xml.Append($"<ItemGroup><Source Remove=\"{itemSpec}\" /></ItemGroup>");
        _source.RemoveAll(item => match(item.Include));
    }

    private void AppendCapture(int operation)
    {
        string itemType = $"Snapshot{operation}";
        _xml.Append($"<ItemGroup><{itemType} Include=\"@(Source->WithMetadataValue('Track', 'true'))\" /></ItemGroup>");
        (string? include, string? trace, _) = _source[0];
        _otherItems.Add((itemType, include, trace, _nextOrder++));
    }

    private LazyItemBenchmarkScenario Complete()
    {
        _xml.Append("</Project>");
        ProjectXml = _xml.ToString();

        // GetAllItemsDeferred orders by originating Include declaration, not by item type or snapshot publication.
        List<(string ItemType, string Include, string Trace, int Order)> ordered = [];
        foreach ((string? include, string? trace, int order) in _source)
        {
            ordered.Add(("Source", include, trace, order));
        }

        ordered.AddRange(_otherItems);
        ordered.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        foreach ((string? itemType, string? include, string? trace, int order) in ordered)
        {
            ExpectedItems.Add((itemType, include, trace));
        }

        return this;
    }

    private static string ItemName(int index)
        => $"item{index:D6}.{(index % 10 == 0 ? "cs" : "txt")}";
}
