// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks covering the <see cref="LazyItemEvaluator{P, I, M, D}"/> code paths
///  exercised during project evaluation. Each benchmark creates a synthetic project
///  with a specific pattern of Include / Remove / Update item elements, then evaluates it.
///  This exercises the full chain: LazyItemList, MemoizedOperation, OrderedItemDataCollection,
///  IncludeOperation, RemoveOperation, and UpdateOperation.
/// </summary>
[MemoryDiagnoser]
public class LazyItemEvaluatorBenchmark
{
    private string _tempDir = null!;
    private string _srcDir = null!;

    /// <summary>
    ///  Scales the number of items. "Small" ≈ real component project, "Large" ≈ monorepo/generated code.
    /// </summary>
    [Params(100, 1000, 5000)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks", Guid.NewGuid().ToString("N"));
        _srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(_srcDir);

        // Create dummy source files for glob-based benchmarks.
        for (int i = 0; i < ItemCount; i++)
        {
            File.WriteAllText(Path.Combine(_srcDir, $"File{i}.cs"), string.Empty);
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>
    ///  Creates a <see cref="ProjectItemElement"/> with a Remove attribute and appends it to the given item group.
    ///  The Remove value must be set before appending because MSBuild validates that an item outside of a Target
    ///  has Include, Update, or Remove set.
    /// </summary>
    private static ProjectItemElement AddRemoveItem(ProjectItemGroupElement itemGroup, string itemType, string remove)
    {
        var item = itemGroup.ContainingProject.CreateItemElement(itemType);
        item.Remove = remove;
        itemGroup.AppendChild(item);
        return item;
    }

    /// <summary>
    ///  Creates a <see cref="ProjectItemElement"/> with an Update attribute and appends it to the given item group.
    ///  The Update value must be set before appending because MSBuild validates that an item outside of a Target
    ///  has Include, Update, or Remove set.
    /// </summary>
    private static ProjectItemElement AddUpdateItem(ProjectItemGroupElement itemGroup, string itemType, string update)
    {
        var item = itemGroup.ContainingProject.CreateItemElement(itemType);
        item.Update = update;
        itemGroup.AppendChild(item);
        return item;
    }

    private string CreateFileList(int start, int count)
        => string.Join(";", Enumerable.Range(start, count).Select(i => Path.Combine(_srcDir, $"File{i}.cs")));

    /// <summary>
    ///  Baseline: A single Include with many semicolon-separated items.
    ///  Exercises IncludeOperation with ValueFragments, no excludes, no metadata.
    /// </summary>
    [Benchmark(Description = "Include (value list)")]
    public Project Include_ValueList()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeValueList.csproj");

        var itemGroup = root.AddItemGroup();
        itemGroup.AddItem("Compile", include: CreateFileList(0, ItemCount));

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  A single glob Include (e.g. src\**\*.cs).
    ///  Exercises IncludeOperation with a GlobFragment hitting the file system.
    /// </summary>
    [Benchmark(Description = "Include (glob)")]
    public Project Include_Glob()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeGlob.csproj");

        var itemGroup = root.AddItemGroup();
        itemGroup.AddItem("Compile", include: @"src\**\*.cs");

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Glob Include with Exclude patterns.
    ///  Exercises IncludeOperation's exclude matching (FileSpecMatcherTester) and
    ///  BuildExcludePatternsForGlobs.
    /// </summary>
    [Benchmark(Description = "Include (glob + exclude)")]
    public Project Include_GlobWithExclude()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeGlobExclude.csproj");

        var itemGroup = root.AddItemGroup();
        var item = itemGroup.AddItem("Compile", include: @"src\**\*.cs");
        item.Exclude = @"src\**\File0.cs;src\**\File1.cs;src\**\File2.cs";

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Include followed by Remove="@(Compile)" (self-referencing remove).
    ///  Exercises RemoveOperation's fast path: ItemspecContainsASingleBareItemReference → Clear().
    /// </summary>
    [Benchmark(Description = "Include + Remove(@self)")]
    public Project Include_ThenRemoveSelf()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeRemoveSelf.csproj");

        var itemGroup1 = root.AddItemGroup();
        itemGroup1.AddItem("Compile", include: CreateFileList(0, ItemCount));

        var itemGroup2 = root.AddItemGroup();
        var remove = AddRemoveItem(itemGroup2, "Compile", "@(Compile)");

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Include followed by Remove with specific items (not self-referencing).
    ///  Exercises the dictionary-based bulk remove path when item count ≥ DictionaryBasedItemRemoveThreshold,
    ///  and the linear matching path when below the threshold.
    /// </summary>
    [Benchmark(Description = "Include + Remove (partial)")]
    public Project Include_ThenRemovePartial()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeRemovePartial.csproj");

        var itemGroup1 = root.AddItemGroup();
        itemGroup1.AddItem("Compile", include: CreateFileList(0, ItemCount));

        // Remove ~10% of items by explicit value.
        int removeCount = Math.Max(1, ItemCount / 10);
        var itemGroup2 = root.AddItemGroup();
        var remove = AddRemoveItem(itemGroup2, "Compile", CreateFileList(0, removeCount));

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Include followed by Update="@(Compile)" (self-referencing update, all items match).
    ///  Exercises UpdateOperation's fast path and metadata decoration on all items.
    /// </summary>
    [Benchmark(Description = "Include + Update(@self) with metadata")]
    public Project Include_ThenUpdateSelfWithMetadata()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeUpdateSelf.csproj");

        var itemGroup1 = root.AddItemGroup();
        itemGroup1.AddItem("Compile", include: CreateFileList(0, ItemCount));

        var itemGroup2 = root.AddItemGroup();
        var update = AddUpdateItem(itemGroup2, "Compile", "@(Compile)");
        update.AddMetadata("CustomMeta", "ConstantValue");

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Include followed by many individual Update elements (the batched update optimization path).
    ///  Each Update targets a single item by path — exercises ProcessNonWildCardItemUpdates
    ///  and the dictionary-based batching in ComputeItems.
    /// </summary>
    [Benchmark(Description = "Include + many single-item Updates")]
    public Project Include_ThenManyIndividualUpdates()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeManyUpdates.csproj");

        var itemGroup1 = root.AddItemGroup();
        itemGroup1.AddItem("Compile", include: CreateFileList(0, ItemCount));

        // Add individual updates for ~10% of items, each setting metadata.
        int updateCount = Math.Max(1, ItemCount / 10);
        var itemGroup2 = root.AddItemGroup();
        for (int i = 0; i < updateCount; i++)
        {
            var update = AddUpdateItem(itemGroup2, "Compile", Path.Combine(_srcDir, $"File{i}.cs"));
            update.AddMetadata("Label", $"Updated{i}");
        }

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Include with per-item metadata using built-in metadata expressions (%(Filename)).
    ///  Exercises the needToExpandMetadataForEachItem=true path in DecorateItemsWithMetadata.
    /// </summary>
    [Benchmark(Description = "Include + per-item metadata (%(Filename))")]
    public Project Include_WithPerItemMetadata()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludePerItemMeta.csproj");

        var itemGroup = root.AddItemGroup();
        var item = itemGroup.AddItem("Compile", include: CreateFileList(0, ItemCount));
        item.AddMetadata("Link", @"Generated\%(Filename)%(Extension)");

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Include with constant (non-varying) metadata.
    ///  Exercises the needToExpandMetadataForEachItem=false path — metadata is evaluated once
    ///  and applied to all items via SetMetadata.
    /// </summary>
    [Benchmark(Description = "Include + constant metadata")]
    public Project Include_WithConstantMetadata()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeConstMeta.csproj");

        var itemGroup = root.AddItemGroup();
        var item = itemGroup.AddItem("Compile", include: CreateFileList(0, ItemCount));
        item.AddMetadata("Visible", "false");
        item.AddMetadata("AutoGen", "true");

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Cross-item-type reference: Include="@(Content)" on Compile.
    ///  Exercises the item reference path (ItemExpressionFragment), referencedItemLists,
    ///  and the MemoizedOperation caching when one item type references another.
    /// </summary>
    [Benchmark(Description = "Include @(OtherType) reference")]
    public Project Include_CrossItemTypeReference()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeCrossRef.csproj");

        var itemGroup1 = root.AddItemGroup();
        itemGroup1.AddItem("Content", include: CreateFileList(0, ItemCount));

        var itemGroup2 = root.AddItemGroup();
        itemGroup2.AddItem("Compile", include: "@(Content)");

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Multiple references to the same item type from different item groups.
    ///  Exercises LazyItemList chain walking and MemoizedOperation cache hits:
    ///  the second @(Compile) reference should hit the cache populated by the first.
    /// </summary>
    [Benchmark(Description = "Multiple @() references (cache exercise)")]
    public Project MultipleReferences_CacheExercise()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "MultiRef.csproj");

        var itemGroup1 = root.AddItemGroup();
        itemGroup1.AddItem("Compile", include: CreateFileList(0, ItemCount));

        // First reference: Content copies from Compile.
        var itemGroup2 = root.AddItemGroup();
        itemGroup2.AddItem("Content", include: "@(Compile)");

        // Second reference: Resource also copies from Compile — should benefit from cache.
        var itemGroup3 = root.AddItemGroup();
        itemGroup3.AddItem("Resource", include: "@(Compile)");

        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Chained operations: Include → Remove (partial) → Update (remaining).
    ///  Exercises the full ComputeItems stack walk, globsToIgnore propagation,
    ///  and interleaved operation types.
    /// </summary>
    [Benchmark(Description = "Include + Remove + Update chain")]
    public Project Include_Remove_Update_Chain()
    {
        using var pc = new ProjectCollection();
        var root = ProjectRootElement.Create(pc);
        root.FullPath = Path.Combine(_tempDir, "Chain.csproj");

        var itemGroup1 = root.AddItemGroup();
        itemGroup1.AddItem("Compile", include: CreateFileList(0, ItemCount));

        // Remove first 10%.
        int removeCount = Math.Max(1, ItemCount / 10);
        var itemGroup2 = root.AddItemGroup();
        AddRemoveItem(itemGroup2, "Compile", CreateFileList(0, removeCount));

        // Update remaining with metadata.
        var itemGroup3 = root.AddItemGroup();
        var update = AddUpdateItem(itemGroup3, "Compile", "@(Compile)");
        update.AddMetadata("Processed", "true");
        return new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            pc,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);
    }

    /// <summary>
    ///  Evaluation with a shared <see cref="EvaluationContext"/>.
    ///  Exercises any caching benefits from context reuse (glob caches, file system caches)
    ///  combined with LazyItemEvaluator paths.
    /// </summary>
    [Benchmark(Description = "Include (glob) with shared EvaluationContext")]
    public Project Include_Glob_SharedContext()
    {
        using var collection = new ProjectCollection();
        var context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "IncludeGlobShared.csproj");

        var itemGroup = root.AddItemGroup();
        itemGroup.AddItem("Compile", include: @"src\**\*.cs");

        return Project.FromProjectRootElement(root, new ProjectOptions
        {
            ProjectCollection = collection,
            EvaluationContext = context,
            LoadSettings = ProjectLoadSettings.RecordDuplicateButNotCircularImports,
        });
    }

    /// <summary>
    ///  GetAllItemsDeferred: exercises the LINQ SelectMany + OrderBy path over all item lists.
    /// </summary>
    [Benchmark(Description = "GetAllItems via evaluation (multi-type)")]
    public int GetAllItems_MultiType()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.FullPath = Path.Combine(_tempDir, "MultiType.csproj");

        int perType = ItemCount / 3;
        var itemGroup = root.AddItemGroup();
        itemGroup.AddItem("Compile", include: CreateFileList(0, perType));
        itemGroup.AddItem("Content", include: CreateFileList(perType, perType));
        itemGroup.AddItem("None", include: CreateFileList(perType * 2, ItemCount - perType * 2));

        var project = new Project(
            root,
            globalProperties: null,
            toolsVersion: null,
            collection,
            ProjectLoadSettings.RecordDuplicateButNotCircularImports);

        // Force all items to be read — exercises the final materialization from LazyItemEvaluator.
        int count = 0;
        foreach (var item in project.AllEvaluatedItems)
        {
            count++;
        }

        return count;
    }
}
