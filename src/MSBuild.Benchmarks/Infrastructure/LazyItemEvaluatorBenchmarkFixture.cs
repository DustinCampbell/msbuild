// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using ItemEvaluator = Microsoft.Build.Evaluation.LazyItemEvaluator<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance, Microsoft.Build.Execution.ProjectMetadataInstance, Microsoft.Build.Execution.ProjectItemDefinitionInstance>;

namespace MSBuild.Benchmarks;

/// <summary>
///  Identifies independently measured lazy item workloads.
/// </summary>
public enum LazyItemWorkload
{
    /// <summary>
    ///  Records many single-item Includes.
    /// </summary>
    LiteralIncludes,

    /// <summary>
    ///  Records one Include containing a semicolon-separated list.
    /// </summary>
    SemicolonInclude,

    /// <summary>
    ///  Interleaves Includes for many item types.
    /// </summary>
    ManyItemTypes,

    /// <summary>
    ///  Reads the growing item state at occasional conditions.
    /// </summary>
    SparseReads,

    /// <summary>
    ///  Reads every growing item prefix.
    /// </summary>
    EveryStepRead,

    /// <summary>
    ///  Reads the same item state repeatedly.
    /// </summary>
    RepeatedRead,

    /// <summary>
    ///  Reads the item state after every literal Update.
    /// </summary>
    SnapshotUpdates,

    /// <summary>
    ///  Applies disjoint literal Updates.
    /// </summary>
    LiteralUpdates,

    /// <summary>
    ///  Applies overlapping literal Updates.
    /// </summary>
    OverlappingUpdates,

    /// <summary>
    ///  Applies metadata that varies by item through a wildcard Update.
    /// </summary>
    MetadataWildcardUpdate,

    /// <summary>
    ///  Captures qualified metadata from matching source items.
    /// </summary>
    QualifiedUpdate,

    /// <summary>
    ///  Removes half the item identities using literal fragments.
    /// </summary>
    LiteralRemove,

    /// <summary>
    ///  Removes literal identities from a duplicate-heavy item list.
    /// </summary>
    DuplicateRemove,

    /// <summary>
    ///  Removes items by matching metadata rather than identity.
    /// </summary>
    MetadataRemove,

    /// <summary>
    ///  Expands a recursive file glob.
    /// </summary>
    GlobInclude,

    /// <summary>
    ///  Removes the same glob that was included.
    /// </summary>
    GlobRemove,

    /// <summary>
    ///  Removes one subtree from a recursive Include.
    /// </summary>
    GlobSubtreeRemove,

    /// <summary>
    ///  Applies many separate glob removals.
    /// </summary>
    ManyGlobRemoves,

    /// <summary>
    ///  Reinserts a glob after removing it.
    /// </summary>
    GlobReinclude,

    /// <summary>
    ///  Demands the included files before a later glob removal.
    /// </summary>
    GlobEarlyRead,

    /// <summary>
    ///  Matches literal items against an independently sized Exclude list.
    /// </summary>
    Excludes,
}

/// <summary>
///  Owns preconstructed XML and outer evaluation data for an isolated evaluator workload.
/// </summary>
/// <remarks>
///  Each measured invocation creates a new evaluator, records its operations, and consumes its
///  results. XML construction, project evaluation, and file creation are outside the measured path.
///  Glob workloads intentionally reuse a warmed evaluation-context file cache; the existing
///  end-to-end benchmark supplies the complementary isolated-project evaluation measurement.
/// </remarks>
internal sealed class LazyItemEvaluatorBenchmarkFixture : IDisposable
{
    /// <summary>
    ///  The project collection and XML root owned by this fixture.
    /// </summary>
    private readonly BenchmarkProject _benchmarkProject;

    /// <summary>
    ///  The unchanged outer data used by each evaluator invocation.
    /// </summary>
    private readonly ProjectInstance _project;

    /// <summary>
    ///  The preconstructed operations in declaration order.
    /// </summary>
    private readonly ProjectItemElement[] _operations;

    /// <summary>
    ///  The shared expression and warmed file-entry caches.
    /// </summary>
    private readonly EvaluationContext _context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

    /// <summary>
    ///  The logging context supplied to expression and glob evaluation.
    /// </summary>
    private readonly EvaluationLoggingContext _loggingContext;

    /// <summary>
    ///  A disabled profiler, keeping profiling overhead out of ordinary measurements.
    /// </summary>
    private readonly EvaluationProfiler _profiler = new(shouldTrackElements: false);

    /// <summary>
    ///  The temporary directory holding fixture files.
    /// </summary>
    private readonly TemporaryDirectory _directory;

    /// <summary>
    ///  The expected ordered output, including metadata, for setup-time verification.
    /// </summary>
    private readonly List<ExpectedItem> _expected = [];

    /// <summary>
    ///  Initializes and validates one workload.
    /// </summary>
    /// <param name="workload">The workload to construct.</param>
    /// <param name="itemCount">The number of input item identities.</param>
    /// <param name="excludeCount">The independently sized Exclude list.</param>
    public LazyItemEvaluatorBenchmarkFixture(LazyItemWorkload workload, int itemCount, int excludeCount = 0)
    {
        _directory = new TemporaryDirectory(nameof(LazyItemEvaluatorBenchmarkFixture));
        _benchmarkProject = new BenchmarkProject(_directory.GetPath("benchmark.proj"));
        _project = _benchmarkProject.CreateProjectInstance();
        _loggingContext = new EvaluationLoggingContext(
            _benchmarkProject.ProjectCollection.LoggingService, BuildEventContext.Invalid, _project.FullPath);

        ProjectItemGroupElement group = _benchmarkProject.RootElement.AddItemGroup();
        BuildScenario(group, workload, itemCount, excludeCount);
        _operations = [.. group.Items];
        Validate();
    }

    /// <summary>
    ///  Records a fresh evaluator, including any state demanded by item conditions.
    /// </summary>
    /// <returns>
    ///  The evaluator whose final state has not yet been requested.
    /// </returns>
    public ItemEvaluator Record()
    {
        var evaluator = new ItemEvaluator(
            _project,
            new ProjectItemInstance.TaskItem.ProjectItemInstanceFactory(_project),
            _loggingContext,
            _profiler,
            _context);

        foreach (ProjectItemElement operation in _operations)
        {
            bool result = evaluator.EvaluateConditionWithCurrentState(
                operation, ExpanderOptions.ExpandPropertiesAndItems, ParserOptions.AllowPropertiesAndItemLists);
            if (!result)
            {
                throw new InvalidOperationException($"Lazy item benchmark condition unexpectedly failed: {operation.Condition}");
            }
            evaluator.ProcessItemElement(_directory.DirectoryPath, operation, result);
        }

        return evaluator;
    }

    /// <summary>
    ///  Records and materializes a fresh evaluator, consuming identity and metadata output.
    /// </summary>
    /// <returns>
    ///  A deterministic checksum of the ordered item types, identities, and metadata values.
    /// </returns>
    public ulong Evaluate()
    {
        ItemEvaluator evaluator = Record();
        ulong checksum = 14695981039346656037UL;
        foreach (ItemEvaluator.ItemData data in evaluator.GetAllItemsDeferred())
        {
            checksum = AddChecksum(checksum, data.Item.ItemType);
            checksum = AddChecksum(checksum, data.Item.EvaluatedInclude);
            checksum = AddChecksum(checksum, data.Item.GetMetadataValue("M"));
        }

        return checksum;
    }

    /// <summary>
    ///  Disposes the project collection and removes its owned temporary files.
    /// </summary>
    public void Dispose()
    {
        _benchmarkProject.Dispose();
        _directory.Dispose();
    }

    /// <summary>
    ///  Constructs pre-parsed XML and explicit expected output for the selected workload.
    /// </summary>
    /// <param name="group">The group receiving operations.</param>
    /// <param name="workload">The selected workload.</param>
    /// <param name="count">The number of item identities.</param>
    /// <param name="excludeCount">The number of nonmatching exclusion patterns.</param>
    private void BuildScenario(ProjectItemGroupElement group, LazyItemWorkload workload, int count, int excludeCount)
    {
        string[] names = new string[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = $"item{i:D5}.txt";
        }

        switch (workload)
        {
            case LazyItemWorkload.LiteralIncludes:
            case LazyItemWorkload.ManyItemTypes:
            case LazyItemWorkload.SparseReads:
            case LazyItemWorkload.EveryStepRead:
                for (int i = 0; i < count; i++)
                {
                    string type = workload == LazyItemWorkload.ManyItemTypes ? $"Type{i % 32}" : "A";
                    Include(group, type, names[i], "initial");
                    _expected.Add(new ExpectedItem(type, names[i], "initial"));
                    if (workload == LazyItemWorkload.EveryStepRead
                        || (workload == LazyItemWorkload.SparseReads && (i + 1) % 100 == 0))
                    {
                        ReadCount(group, i + 1);
                    }
                }
                break;

            case LazyItemWorkload.SemicolonInclude:
            case LazyItemWorkload.RepeatedRead:
            case LazyItemWorkload.Excludes:
                ProjectItemElement include = Include(group, "A", string.Join(";", names), "initial");
                if (workload == LazyItemWorkload.Excludes && excludeCount > 0)
                {
                    string[] excludes = new string[excludeCount];
                    for (int i = 0; i < excludes.Length; i++)
                    {
                        excludes[i] = $"excluded{i:D5}.txt";
                    }
                    include.Exclude = string.Join(";", excludes);
                }
                if (workload == LazyItemWorkload.RepeatedRead)
                {
                    for (int i = 0; i < 100; i++)
                    {
                        ReadCount(group, count);
                    }
                }
                AddExpected(names, "initial");
                break;

            case LazyItemWorkload.LiteralUpdates:
            case LazyItemWorkload.OverlappingUpdates:
            case LazyItemWorkload.SnapshotUpdates:
                Include(group, "A", string.Join(";", names), "initial");
                for (int i = 0; i < count; i++)
                {
                    string name = names[workload == LazyItemWorkload.OverlappingUpdates ? i % Math.Min(8, count) : i];
                    Update(group, name, "updated");
                    if (workload == LazyItemWorkload.SnapshotUpdates)
                    {
                        ReadCount(group, count);
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    string metadata = workload != LazyItemWorkload.OverlappingUpdates || i < 8 ? "updated" : "initial";
                    _expected.Add(new ExpectedItem("A", names[i], metadata));
                }
                break;

            case LazyItemWorkload.MetadataWildcardUpdate:
                Include(group, "A", string.Join(";", names), "initial");
                Update(group, "*.txt", "%(Filename)");
                foreach (string name in names)
                {
                    _expected.Add(new ExpectedItem("A", name, Path.GetFileNameWithoutExtension(name)));
                }
                break;

            case LazyItemWorkload.QualifiedUpdate:
                for (int i = 0; i < count; i++)
                {
                    Include(group, "Source", names[i], $"source{i}");
                    _expected.Add(new ExpectedItem("Source", names[i], $"source{i}"));
                }
                Include(group, "A", string.Join(";", names), "initial");
                Update(group, "@(Source)", "%(Source.M)");
                for (int i = 0; i < count; i++)
                {
                    _expected.Add(new ExpectedItem("A", names[i], $"source{i}"));
                }
                break;

            case LazyItemWorkload.LiteralRemove:
            case LazyItemWorkload.DuplicateRemove:
                var toInclude = new List<string>();
                var toRemove = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    toInclude.Add(names[i]);
                    if (workload == LazyItemWorkload.DuplicateRemove)
                    {
                        toInclude.Add(names[i]);
                    }
                    if (i % 2 == 0)
                    {
                        toRemove.Add(names[i]);
                    }
                    else
                    {
                        _expected.Add(new ExpectedItem("A", names[i], "initial"));
                        if (workload == LazyItemWorkload.DuplicateRemove)
                        {
                            _expected.Add(new ExpectedItem("A", names[i], "initial"));
                        }
                    }
                }
                Include(group, "A", string.Join(";", toInclude), "initial");
                Remove(group, string.Join(";", toRemove));
                break;

            case LazyItemWorkload.MetadataRemove:
                Include(group, "Source", "source", "value0");
                _expected.Add(new ExpectedItem("Source", "source", "value0"));
                for (int i = 0; i < count; i++)
                {
                    Include(group, "A", names[i], $"value{i % 4}");
                    if (i % 4 != 0)
                    {
                        _expected.Add(new ExpectedItem("A", names[i], $"value{i % 4}"));
                    }
                }
                // Parse this attribute rather than use the construction API setter, which currently
                // checks RemoveMetadata instead of Remove and rejects otherwise valid scenario XML.
                using (var reader = XmlReader.Create(new StringReader("""
                    <Project><ItemGroup><A Remove="@(Source)" MatchOnMetadata="M" /></ItemGroup></Project>
                    """)))
                {
                    ProjectRootElement parsed = ProjectRootElement.Create(reader, _benchmarkProject.ProjectCollection);
                    ProjectItemElement remove = group.ContainingProject.CreateItemElement("A");
                    remove.CopyFrom(parsed.Items.Single());
                    group.AppendChild(remove);
                }
                break;

            case LazyItemWorkload.GlobInclude:
            case LazyItemWorkload.GlobRemove:
            case LazyItemWorkload.GlobSubtreeRemove:
            case LazyItemWorkload.ManyGlobRemoves:
            case LazyItemWorkload.GlobReinclude:
            case LazyItemWorkload.GlobEarlyRead:
                BuildGlobScenario(group, workload, count);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(workload), workload, "Unknown lazy item workload.");
        }
    }

    /// <summary>
    ///  Creates a bounded file tree and constructs a glob/removal scenario.
    /// </summary>
    /// <param name="group">The group receiving operations.</param>
    /// <param name="workload">The selected glob workload.</param>
    /// <param name="count">The number of files to create.</param>
    private void BuildGlobScenario(ProjectItemGroupElement group, LazyItemWorkload workload, int count)
    {
        var expected = new List<string>();
        for (int i = 0; i < count; i++)
        {
            string path = Path.Combine("src", $"dir{i % 10}", $"file{i:D5}.cs");
            _directory.WriteFile(path, string.Empty);
            if (workload != LazyItemWorkload.GlobSubtreeRemove || i % 10 != 0)
            {
                expected.Add(path);
            }
        }

        string glob = Path.Combine("src", "**", "*.cs");
        Include(group, "A", glob, "initial");
        if (workload == LazyItemWorkload.GlobEarlyRead)
        {
            ReadCount(group, count);
        }

        if (workload is LazyItemWorkload.GlobRemove or LazyItemWorkload.GlobReinclude or LazyItemWorkload.GlobEarlyRead)
        {
            Remove(group, glob);
        }
        else if (workload == LazyItemWorkload.GlobSubtreeRemove)
        {
            Remove(group, Path.Combine("src", "dir0", "**", "*.cs"));
        }
        else if (workload == LazyItemWorkload.ManyGlobRemoves)
        {
            for (int i = 0; i < count; i++)
            {
                Remove(group, Path.Combine("src", "**", $"file{i:D5}.cs"));
            }
        }

        if (workload == LazyItemWorkload.GlobReinclude)
        {
            Include(group, "A", glob, "updated");
        }

        if (workload is LazyItemWorkload.GlobInclude or LazyItemWorkload.GlobSubtreeRemove or LazyItemWorkload.GlobReinclude)
        {
            expected.Sort(StringComparer.OrdinalIgnoreCase);
            AddExpected(expected, workload == LazyItemWorkload.GlobReinclude ? "updated" : "initial");
        }
    }

    /// <summary>
    ///  Adds expected items of the primary item type.
    /// </summary>
    /// <param name="names">The ordered identities.</param>
    /// <param name="metadata">Their expected metadata value.</param>
    private void AddExpected(IEnumerable<string> names, string metadata)
    {
        foreach (string name in names)
        {
            _expected.Add(new ExpectedItem("A", name, metadata));
        }
    }

    /// <summary>
    ///  Creates an Include with constant metadata.
    /// </summary>
    /// <param name="group">The containing group.</param>
    /// <param name="type">The item type.</param>
    /// <param name="include">The item specification.</param>
    /// <param name="metadata">The metadata value.</param>
    /// <returns>
    ///  The Include element.
    /// </returns>
    private static ProjectItemElement Include(ProjectItemGroupElement group, string type, string include, string metadata)
    {
        // AddItem sorts by type and include, which would change the operation history being measured.
        ProjectItemElement element = group.ContainingProject.CreateItemElement(type, include);
        group.AppendChild(element);
        element.AddMetadata("M", metadata);
        return element;
    }

    /// <summary>
    ///  Creates an Update on the primary item type.
    /// </summary>
    /// <param name="group">The containing group.</param>
    /// <param name="spec">The Update specification.</param>
    /// <param name="metadata">The metadata value.</param>
    private static void Update(ProjectItemGroupElement group, string spec, string metadata)
    {
        ProjectItemElement element = group.ContainingProject.CreateItemElement("A");
        element.Update = spec;
        group.AppendChild(element);
        element.AddMetadata("M", metadata);
    }

    /// <summary>
    ///  Creates a Remove on the primary item type.
    /// </summary>
    /// <param name="group">The containing group.</param>
    /// <param name="spec">The Remove specification.</param>
    /// <returns>
    ///  The Remove element.
    /// </returns>
    private static ProjectItemElement Remove(ProjectItemGroupElement group, string spec)
    {
        ProjectItemElement element = group.ContainingProject.CreateItemElement("A");
        element.Remove = spec;
        group.AppendChild(element);
        return element;
    }

    /// <summary>
    ///  Adds an empty Include whose condition demands the current primary item state.
    /// </summary>
    /// <param name="group">The containing group.</param>
    /// <param name="expectedCount">The expected count at this point in the history.</param>
    private static void ReadCount(ProjectItemGroupElement group, int expectedCount)
    {
        ProjectItemElement probe = group.ContainingProject.CreateItemElement("Probe", "$(Missing)");
        probe.Condition = $"'@(A->Count())' == '{expectedCount}'";
        group.AppendChild(probe);
    }

    /// <summary>
    ///  Validates ordered identities, metadata, and provenance before timing begins.
    /// </summary>
    private void Validate()
    {
        ItemEvaluator.ItemData[] actual = [.. Record().GetAllItemsDeferred()];
        if (actual.Length != _expected.Count)
        {
            throw new InvalidOperationException($"Lazy item benchmark expected {_expected.Count} items but produced {actual.Length}.");
        }

        for (int i = 0; i < actual.Length; i++)
        {
            ExpectedItem expected = _expected[i];
            ProjectItemInstance item = actual[i].Item;
            if (item.ItemType != expected.Type
                || item.EvaluatedInclude != expected.Include
                || item.GetMetadataValue("M") != expected.Metadata
                || !actual[i].ConditionResult
                || actual[i].OriginatingItemElement.ContainingProject != _benchmarkProject.RootElement
                || item.GetMetadataValue("DefiningProjectFullPath") != _project.FullPath)
            {
                throw new InvalidOperationException(
                    $"Lazy item benchmark output differs at item {i}: expected {expected}, " +
                    $"actual {item.ItemType}:{item.EvaluatedInclude}:{item.GetMetadataValue("M")}.");
            }
        }
    }

    /// <summary>
    ///  Adds a string and a field separator to a process-independent checksum.
    /// </summary>
    /// <param name="checksum">The current checksum.</param>
    /// <param name="value">The next field.</param>
    /// <returns>
    ///  The updated checksum.
    /// </returns>
    private static ulong AddChecksum(ulong checksum, string value)
    {
        unchecked
        {
            foreach (char character in value)
            {
                checksum = (checksum ^ character) * 1099511628211UL;
            }
            return (checksum ^ 0xFFFF) * 1099511628211UL;
        }
    }

    /// <summary>
    ///  Describes one expected item for setup-time validation.
    /// </summary>
    /// <param name="Type">The item type.</param>
    /// <param name="Include">The evaluated include.</param>
    /// <param name="Metadata">The evaluated metadata value.</param>
    private readonly record struct ExpectedItem(string Type, string Include, string Metadata);
}
