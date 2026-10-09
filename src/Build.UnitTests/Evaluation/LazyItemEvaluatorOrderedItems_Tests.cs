// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;
using ItemEvaluator = Microsoft.Build.Evaluation.LazyItemEvaluator<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance, Microsoft.Build.Execution.ProjectMetadataInstance, Microsoft.Build.Execution.ProjectItemDefinitionInstance>;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Directly tests saved ordered item views, ownership boundaries, and normalized-value indexing.
/// </summary>
public sealed class LazyItemEvaluatorOrderedItems_Tests
{
    /// <summary>
    ///  The output used by transient test infrastructure.
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    ///  Initializes the test class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public LazyItemEvaluatorOrderedItems_Tests(ITestOutputHelper output) => _output = output;

    /// <summary>
    ///  A saved view has a fixed prefix and counts only condition-visible entries.
    /// </summary>
    [Fact]
    public void SavedViewIsBoundedAndConditionFiltered()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel: true);
        fixture.Record("""<ItemGroup><A Include="a;b;c" /></ItemGroup>""");
        ItemEvaluator.ItemData[] data = Entries(fixture);
        var builder = ItemEvaluator.OrderedItemDataCollection.CreateBuilder();
        builder.Add(data[0]);
        builder.Add(new(data[1].Item, data[1].OriginatingItemElement, 0, conditionResult: false));
        ItemEvaluator.OrderedItemDataCollection saved = builder.ToImmutable();
        builder.Add(data[2]);

        saved.Count.ShouldBe(1);
        saved.Select(i => i.EvaluatedInclude).ShouldBe(["a"]);
        saved.Contains(data[0].Item).ShouldBeTrue();
        saved.Contains(data[1].Item).ShouldBeFalse();
        saved.IsReadOnly.ShouldBeTrue();
        ProjectItemInstance[] copied = new ProjectItemInstance[2];
        saved.CopyTo(copied, 1);
        copied[1].ShouldBeSameAs(data[0].Item);
        Should.Throw<NotSupportedException>(() => ((ICollection<ProjectItemInstance>)saved).Add(data[2].Item));
        saved.ToBuilder().Count.ShouldBe(2);
    }

    /// <summary>
    ///  Appending to a shorter saved prefix detaches rather than revealing or overwriting a later tail.
    /// </summary>
    [Fact]
    public void BranchingFromAnEarlierPrefixPreservesBothTails()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel: true);
        fixture.Record("""<ItemGroup><A Include="a;b;c" /></ItemGroup>""");
        ItemEvaluator.ItemData[] data = Entries(fixture);
        var builder = ItemEvaluator.OrderedItemDataCollection.CreateBuilder();
        builder.Add(data[0]);
        ItemEvaluator.OrderedItemDataCollection prefix = builder.ToImmutable();
        builder.Add(data[1]);
        var branch = prefix.ToBuilder();
        branch.Add(data[2]);

        prefix.Select(i => i.EvaluatedInclude).ShouldBe(["a"]);
        builder.Select(i => i.Item.EvaluatedInclude).ShouldBe(["a", "b"]);
        branch.Select(i => i.Item.EvaluatedInclude).ShouldBe(["a", "c"]);
        Should.Throw<ArgumentOutOfRangeException>(() => prefix.ToBuilder()[1]);
    }

    /// <summary>
    ///  Replacing an item and compacting the working prefix cannot change an earlier saved view.
    /// </summary>
    [Fact]
    public void ReplacementAndRemovalDetachSavedEntries()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel: true);
        fixture.Record("""<ItemGroup><A Include="a;b" M="old" /></ItemGroup>""");
        ItemEvaluator.ItemData[] original = Entries(fixture);
        fixture.Record("""<ItemGroup><A Update="a" M="new" /></ItemGroup>""");
        ItemEvaluator.ItemData updated = Entries(fixture)[0];
        var builder = ItemEvaluator.OrderedItemDataCollection.CreateBuilder();
        builder.Add(original[0]);
        builder.Add(original[1]);
        ItemEvaluator.OrderedItemDataCollection saved = builder.ToImmutable();
        builder[0] = updated;
        builder.RemoveAll(new HashSet<ProjectItemInstance> { original[1].Item });

        saved.Select(i => $"{i.EvaluatedInclude}:{i.GetMetadataValue("M")}").ShouldBe(["a:old", "b:old"]);
        builder.Select(i => $"{i.Item.EvaluatedInclude}:{i.Item.GetMetadataValue("M")}").ShouldBe(["a:new"]);
    }

    /// <summary>
    ///  An indexed replacement can move to a previously absent normalized key and preserve duplicates.
    /// </summary>
    [Fact]
    public void NormalizedIndexTracksReplacementAndDuplicateRemoval()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel: true);
        fixture.Record("""<ItemGroup><A Include="a;a;b" /></ItemGroup>""");
        ItemEvaluator.ItemData[] data = Entries(fixture);
        var builder = ItemEvaluator.OrderedItemDataCollection.CreateBuilder();
        builder.Add(data[0]);
        builder.Add(data[1]);
        Dictionary<string, ItemDataCollectionValue<ProjectItemInstance>> index = builder.Dictionary;
        index.Count.ShouldBe(1);
        builder[0] = data[2];
        builder.Dictionary.Count.ShouldBe(2);
        builder.RemoveAll(new[] { data[0].NormalizedItemValue });

        builder.Select(i => i.Item.EvaluatedInclude).ShouldBe(["b"]);
        builder.Dictionary.ShouldBeSameAs(index);
        builder.Dictionary.Count.ShouldBe(1);
        index.ContainsKey(data[0].NormalizedItemValue).ShouldBeFalse();
        List<ProjectItemInstance> indexed = [];
        foreach (ProjectItemInstance item in builder.Dictionary[data[2].NormalizedItemValue])
        {
            indexed.Add(item);
        }
        indexed.ShouldBe([data[2].Item]);
    }

    /// <summary>
    ///  Clearing a working view does not clear shared saved data or its visible count.
    /// </summary>
    [Fact]
    public void ClearingDoesNotMutateSavedView()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel: true);
        fixture.Record("""<ItemGroup><A Include="a;b" /></ItemGroup>""");
        var builder = ItemEvaluator.OrderedItemDataCollection.CreateBuilder();
        foreach (ItemEvaluator.ItemData data in Entries(fixture))
        {
            builder.Add(data);
        }
        ItemEvaluator.OrderedItemDataCollection saved = builder.ToImmutable();
        builder.Clear();

        builder.Count.ShouldBe(0);
        builder.ToImmutable().Count.ShouldBe(0);
        saved.Count.ShouldBe(2);
        saved.Select(i => i.EvaluatedInclude).ShouldBe(["a", "b"]);
    }

    /// <summary>
    ///  Converts fixture results to the instance model's ordered entry representation.
    /// </summary>
    /// <param name="fixture">The direct evaluator fixture.</param>
    /// <returns>
    ///  The ordered entries, retaining origin, order, and condition data.
    /// </returns>
    private static ItemEvaluator.ItemData[] Entries(LazyItemEvaluatorTestFixture fixture)
        => fixture.GetItems().Select(data => new ItemEvaluator.ItemData(
            data.Item.ShouldBeOfType<ProjectItemInstance>(), data.OriginatingItemElement, data.ElementOrder, data.ConditionResult)).ToArray();
}
