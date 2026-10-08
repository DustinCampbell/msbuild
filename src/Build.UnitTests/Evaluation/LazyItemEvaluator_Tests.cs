// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.FileSystem;
using Shouldly;
using Xunit;
using ItemRecord = Microsoft.Build.UnitTests.Evaluation.LazyItemEvaluatorTestFixture.ItemRecord;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Characterizes lazy item evaluation directly for editable and instance-model items.
/// </summary>
public sealed class LazyItemEvaluator_Tests
{
    /// <summary>
    ///  The output used by transient test infrastructure.
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    ///  Initializes the test class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public LazyItemEvaluator_Tests(ITestOutputHelper output) => _output = output;

    /// <summary>
    ///  An empty evaluator has no materialized items.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyEvaluator(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.GetItems().ShouldBeEmpty();
        fixture.CreatedItems.ShouldBe(0);
        fixture.EvaluateCondition("'@(Missing)' == ''").ShouldBeTrue();
    }

    /// <summary>
    ///  Recording and obtaining the enumerable do not create items before demand.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaterializationIsDeferredAndCached(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""<ItemGroup><A Include="a;b" /></ItemGroup>""");
        IEnumerable<ItemRecord> deferred = fixture.GetItems();
        fixture.CreatedItems.ShouldBe(0);

        Identities(deferred).ShouldBe(["a", "b"]);
        fixture.CreatedItems.ShouldBe(2);
        Identities(fixture.GetItems()).ShouldBe(["a", "b"]);
        fixture.CreatedItems.ShouldBe(2);
    }

    /// <summary>
    ///  Conditions demand the current state rather than the final state.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConditionsObserveCurrentState(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""<ItemGroup><A Include="a" /></ItemGroup>""");
        fixture.EvaluateCondition("'@(A)' == 'a'").ShouldBeTrue();
        fixture.CreatedItems.ShouldBe(1);

        fixture.Record("""<ItemGroup><A Include="b" /></ItemGroup>""");
        fixture.EvaluateCondition("'@(A)' == 'a;b'").ShouldBeTrue();
        fixture.CreatedItems.ShouldBe(2);
        fixture.EvaluateCondition("'@(A)' == 'a;b'").ShouldBeTrue();
        fixture.CreatedItems.ShouldBe(2);
    }

    /// <summary>
    ///  Literal fragments retain duplicates and unescape only the item values.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralFragmentsPreserveEscapingAndDuplicates(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""<ItemGroup><A Include="a;;a;semi%3Bcolon;star%2A.txt;question%3F.txt" /></ItemGroup>""");
        Identities(fixture.GetItems()).ShouldBe(["a", "a", "semi;colon", "star*.txt", "question?.txt"]);
    }

    /// <summary>
    ///  Missing item references remain empty instead of becoming forward references.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferencesCaptureOnlyPrecedingOperations(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <B Include="@(A)" />
              <A Include="a" />
              <A Include="@(A)" />
              <B Include="@(A)" />
              <A Include="b" />
            </ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Identities(OfType(items, "A")).ShouldBe(["a", "a", "b"]);
        Identities(OfType(items, "B")).ShouldBe(["a", "a"]);
    }

    /// <summary>
    ///  Updates and removals cannot mutate items captured by earlier references.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EarlierReferencesKeepItemsAndMetadata(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Include="a;b" M="original" />
              <B Include="@(A)" />
              <A Update="a" M="changed" />
              <A Remove="b" />
              <C Include="@(A)" />
            </ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Values(OfType(items, "A"), "M").ShouldBe(["a:changed"]);
        Values(OfType(items, "B"), "M").ShouldBe(["a:original", "b:original"]);
        Values(OfType(items, "C"), "M").ShouldBe(["a:changed"]);
    }

    /// <summary>
    ///  Reading one state does not seal the evaluator or mutate returned items on later updates.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaterializedItemsRemainUnchangedWhenRecordingContinues(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""<ItemGroup><A Include="a" M="old" /></ItemGroup>""");
        ItemRecord original = fixture.GetItems().Single();
        fixture.Record("""<ItemGroup><A Update="a" M="new" /><A Include="b" /></ItemGroup>""");
        Values(fixture.GetItems(), "M").ShouldBe(["a:new", "b:"]);
        original.Item.GetMetadataValue("M").ShouldBe("old");
        fixture.ClonedItems.ShouldBe(1);
    }

    /// <summary>
    ///  Deferred enumeration observes operations recorded before enumeration begins.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerableDoesNotCaptureTheEndBeforeEnumeration(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""<ItemGroup><A Include="a" /></ItemGroup>""");
        IEnumerable<ItemRecord> items = fixture.GetItems();
        fixture.Record("""<ItemGroup><A Include="b" /><B Include="c" /></ItemGroup>""");
        Identities(items).ShouldBe(["a", "b", "c"]);
    }

    /// <summary>
    ///  Publication restores include-element order across item types without disturbing duplicates.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicationPreservesGlobalAndWithinElementOrder(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Include="a;a" M="first" />
              <B Include="b" />
              <A Include="c" M="last" />
              <A Update="a" N="updated" />
              <B Include="d" />
            </ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Identities(items).ShouldBe(["a", "a", "b", "c", "d"]);
        items.Select(i => i.ElementOrder).ShouldBe([0, 0, 1, 2, 3]);
        Values(OfType(items, "A"), "M").ShouldBe(["a:first", "a:first", "c:last"]);
    }

    /// <summary>
    ///  Cloning for an Update retains the Include XML and defining project.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpdatePreservesOriginatingElement(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        string importedPath = Path.Combine(fixture.DirectoryPath, "imports", "items.proj");
        ProjectRootElement original = fixture.Record("""<ItemGroup><A Include="a" M="old" /></ItemGroup>""", importedPath);
        fixture.Record("""<ItemGroup><A Update="a" M="new" /></ItemGroup>""");
        ItemRecord item = fixture.GetItems().Single();
        item.OriginatingItemElement.ShouldBeSameAs(original.Items.Single());
        item.Item.GetMetadataValue("DefiningProjectFullPath").ShouldBe(importedPath);
        item.Item.GetMetadataValue("M").ShouldBe("new");
    }

    /// <summary>
    ///  Exclude and metadata expressions retain the item states captured at their declaration.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExcludeAndMetadataCaptureEarlierStates(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <Excluded Include="a" />
              <A Include="a;b" Exclude="@(Excluded)">
                <Seen>@(Excluded)</Seen>
                <Checked Condition="'@(Excluded)' == 'a'">yes</Checked>
              </A>
              <Excluded Include="b" />
            </ItemGroup>
            """);
        ItemRecord item = OfType(fixture.GetItems(), "A").Single();
        item.Item.EvaluatedInclude.ShouldBe("b");
        item.Item.GetMetadataValue("Seen").ShouldBe("a");
        item.Item.GetMetadataValue("Checked").ShouldBe("yes");
    }

    /// <summary>
    ///  Property expansion discovers indirect item references without expanding their items early.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropertiesCanIntroduceItemReferences(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.SetProperty("Indirect", "@(A)");
        fixture.Record("""
            <ItemGroup>
              <A Include="a" />
              <B Include="$(Indirect)" Seen="$(Indirect)" />
              <A Include="b" />
            </ItemGroup>
            """);
        Values(OfType(fixture.GetItems(), "B"), "Seen").ShouldBe(["a:a"]);
    }

    /// <summary>
    ///  Only the properties visible when a specification is recorded affect its fragments.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItemSpecificationsExpandPropertiesAtRecordingTime(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.SetProperty("Value", "old");
        fixture.Record("""<ItemGroup><A Include="$(Value)" /></ItemGroup>""");
        fixture.SetProperty("Value", "new");
        fixture.Record("""<ItemGroup><A Include="$(Value)" /></ItemGroup>""");
        Identities(fixture.GetItems()).ShouldBe(["old", "new"]);
    }

    /// <summary>
    ///  Item functions and transforms are not mistaken for a bare self-reference.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpdateAndRemoveRespectTransformedReferences(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Include="a;b;c" />
              <A Update="@(A->WithMetadataValue('Identity', 'b'))" M="updated" />
              <A Update="@(A->'%(Extension)')" N="not matched" />
              <A Remove="@(A->WithMetadataValue('Identity', 'c'))" />
              <A Remove="@(A->'%(Extension)')" />
            </ItemGroup>
            """);
        Values(fixture.GetItems(), "M").ShouldBe(["a:", "b:updated"]);
        fixture.GetItems().Select(i => i.Item.GetMetadataValue("N")).ShouldAllBe(value => value.Length == 0);
    }

    /// <summary>
    ///  Bare self-reference updates and removals operate on all preceding items, not later includes.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelfReferencesDoNotAffectLaterIncludes(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Update="@(A)" M="too early" />
              <A Remove="@(A)" />
              <A Include="a;b" />
              <A Update="@(A)" M="updated" />
              <B Include="@(A)" />
              <A Remove="@(A)" />
              <A Include="c" />
            </ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Values(OfType(items, "A"), "M").ShouldBe(["c:"]);
        Values(OfType(items, "B"), "M").ShouldBe(["a:updated", "b:updated"]);
    }

    /// <summary>
    ///  Metadata is evaluated sequentially against each item when it references metadata.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MetadataValuesAndConditionsUsePrecedingMetadata(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Include="one.txt;two.txt">
                <First>%(Filename)</First>
                <Second Condition="'%(First)' != ''">%(First).generated</Second>
                <Upper>$([System.String]::Copy('%(First)').ToUpperInvariant())</Upper>
              </A>
            </ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Values(items, "Second").ShouldBe(["one.txt:one.generated", "two.txt:two.generated"]);
        Values(items, "Upper").ShouldBe(["one.txt:ONE", "two.txt:TWO"]);
    }

    /// <summary>
    ///  Copying items preserves source definitions above the destination's definitions.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItemDefinitionsAreInheritedWithTheirPrecedence(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel, """
            <ItemDefinitionGroup>
              <A><M>source</M></A>
              <B><M>destination</M><N>destination-only</N></B>
            </ItemDefinitionGroup>
            """);
        fixture.Record("""<ItemGroup><A Include="a" /><B Include="@(A)" /></ItemGroup>""");
        ItemRecord destination = OfType(fixture.GetItems(), "B").Single();
        destination.Item.GetMetadataValue("M").ShouldBe("source");
        destination.Item.GetMetadataValue("N").ShouldBe("destination-only");
    }

    /// <summary>
    ///  Qualified metadata in Update uses the last matching source item for each referenced type.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdateMetadataUsesMatchingSourceItems(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <Source Include="a" M="first" />
              <Source Include="a" M="last" />
              <Source Include="b" M="other" />
              <A Include="a;b;c" />
              <A Update="@(Source)">
                <M>%(Source.M)</M>
                <IdentityCopy>%(A.Identity)</IdentityCopy>
              </A>
              <Source Update="*" M="later" />
            </ItemGroup>
            """);
        ItemRecord[] items = OfType(fixture.GetItems(), "A").ToArray();
        Values(items, "M").ShouldBe(["a:last", "b:other", "c:"]);
        Values(items, "IdentityCopy").ShouldBe(["a:a", "b:b", "c:"]);
    }

    /// <summary>
    ///  The qualified-metadata compatibility switch retains its empty-string behavior.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedMetadataEscapeHatchIsPreserved(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", "1");
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <Source Include="a" M="source" />
              <A Include="a" />
              <A Update="@(Source)" M="%(Source.M)" />
            </ItemGroup>
            """);
        OfType(fixture.GetItems(), "A").Single().Item.GetMetadataValue("M").ShouldBeEmpty();
    }

    /// <summary>
    ///  Indexed source capture keeps separate item types and the last source within each type.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdateIndexesMultipleSourceTypesAndLiteralFragments(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <S Include="a" M="first" />
              <T Include="a" M="other-type" />
              <S Include="a" M="last" />
              <A Include="a;b;c" />
              <A Update="@(S);@(T);c" M="%(S.M)|%(T.M)|%(A.Identity)" />
            </ItemGroup>
            """);
        Values(OfType(fixture.GetItems(), "A"), "M").ShouldBe(["a:last|other-type|a", "b:", "c:||c"]);
    }

    /// <summary>
    ///  Wildcard-valued transforms retain the specification matcher instead of literal indexing.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdateRetainsWildcardSourceMatching(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <S Include="source" M="captured" />
              <A Include="one.txt;two.txt;other.cs" />
              <A Update="@(S->'*.txt')" M="%(S.M)" />
            </ItemGroup>
            """);
        Values(OfType(fixture.GetItems(), "A"), "M").ShouldBe(["one.txt:captured", "two.txt:captured", "other.cs:"]);
    }

    /// <summary>
    ///  Escaped wildcard characters in literal sources do not become wildcard matches.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QualifiedUpdateIndexesEscapedLiteralIdentities(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", null);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <S Include="literal%2A.txt" M="captured" />
              <A Include="literal%2A.txt;literal-other.txt" />
              <A Update="@(S)" M="%(S.M)" />
            </ItemGroup>
            """);
        Values(OfType(fixture.GetItems(), "A"), "M").ShouldBe(["literal*.txt:captured", "literal-other.txt:"]);
    }

    /// <summary>
    ///  Item-name lookup honors both normal case-insensitive lookup and its compatibility switch.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    /// <param name="caseSensitive">Whether the compatibility switch is enabled.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ItemNameComparisonPreservesEscapeHatch(bool instanceModel, bool caseSensitive)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDUSECASESENSITIVEITEMNAMES", caseSensitive ? "1" : null);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""<ItemGroup><Source Include="a" /><A Include="@(source)" /></ItemGroup>""");
        Identities(OfType(fixture.GetItems(), "A")).ShouldBe(caseSensitive ? [] : ["a"]);
    }

    /// <summary>
    ///  False-condition includes remain available for design-time output but not item expressions.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FalseConditionsAreRetainedButNotReferenced(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Include="true" />
              <A Include="false" Condition="false" />
              <A Update="*" M="not applied" Condition="false" />
              <A Remove="*" Condition="false" />
              <B Include="@(A)" />
            </ItemGroup>
            <ItemGroup Condition="false"><A Include="group-false" /></ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Identities(OfType(items, "A")).ShouldBe(["true", "false", "group-false"]);
        OfType(items, "A").Select(i => i.ConditionResult).ShouldBe([true, false, false]);
        Identities(OfType(items, "B")).ShouldBe(["true"]);
        items.ShouldAllBe(i => i.Item.GetMetadataValue("M").Length == 0);
    }

    /// <summary>
    ///  Distinct literal updates retain metadata ordering at overlap and reference boundaries.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralUpdatesRespectOverlapsAndEarlierReferences(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Include="a;b;c" M="0" />
              <A Update="a" M="%(M)1" />
              <A Update="b" M="%(M)2" />
              <B Include="@(A)" />
              <A Update="a" M="%(M)3" />
              <A Update="*" N="%(M)" />
            </ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Values(OfType(items, "A"), "M").ShouldBe(["a:013", "b:02", "c:0"]);
        Values(OfType(items, "B"), "M").ShouldBe(["a:01", "b:02", "c:0"]);
        Values(OfType(items, "A"), "N").ShouldBe(["a:013", "b:02", "c:0"]);
    }

    /// <summary>
    ///  A rejected literal batch cannot apply fragments from its rejected operation twice.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    /// <param name="spec">The Update specification that rejects batching after its first fragment.</param>
    /// <param name="expectedB">The expected metadata on the second item.</param>
    [Theory]
    [InlineData(false, "a;a", "0")]
    [InlineData(true, "a;a", "0")]
    [InlineData(false, "a;./a", "0")]
    [InlineData(true, "a;./a", "0")]
    [InlineData(false, "a;*", "0x")]
    [InlineData(true, "a;*", "0x")]
    public void RejectedLiteralBatchDoesNotLeakCandidateKeys(bool instanceModel, string spec, string expectedB)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record($"""
            <ItemGroup>
              <A Include="a;b" M="0" />
              <A Update="{spec}" M="%(M)x" />
            </ItemGroup>
            """);
        Values(fixture.GetItems(), "M").ShouldBe(["a:0x", $"b:{expectedB}"]);
    }

    /// <summary>
    ///  A condition-demanded literal update is cached rather than replayed on subsequent reads.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralUpdateCachesDemandedPrefixes(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""<ItemGroup><A Include="a;b" M="0" /><A Update="a" M="1" /></ItemGroup>""");
        fixture.EvaluateCondition("'@(A)' == 'a;b'").ShouldBeTrue();
        fixture.ClonedItems.ShouldBe(1);
        fixture.EvaluateCondition("'@(A)' == 'a;b'").ShouldBeTrue();
        fixture.ClonedItems.ShouldBe(1);

        fixture.Record("""<ItemGroup><A Update="b" M="2" /></ItemGroup>""");
        fixture.EvaluateCondition("'@(A)' == 'a;b'").ShouldBeTrue();
        fixture.ClonedItems.ShouldBe(2);
        Values(fixture.GetItems(), "M").ShouldBe(["a:1", "b:2"]);
        fixture.ClonedItems.ShouldBe(2);
    }

    /// <summary>
    ///  Scan and dictionary removals preserve duplicate handling and surviving order.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    /// <param name="threshold">The item count at which dictionary removal is enabled.</param>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 1000)]
    [InlineData(true, 1000)]
    public void RemoveIndexStaysConsistentAfterUpdatesAndAppends(bool instanceModel, int threshold)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", threshold.ToString());
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Include="a;b;b;c;d" />
              <A Remove="d" />
              <A Update="b" M="updated" />
              <A Include="b;e" />
              <A Remove="b" />
            </ItemGroup>
            """);
        Identities(fixture.GetItems()).ShouldBe(["a", "c", "e"]);
    }

    /// <summary>
    ///  Metadata-based removal matches the requested tuple rather than item identity.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    /// <param name="options">The metadata comparison mode.</param>
    /// <param name="remaining">The expected remaining item identities.</param>
    [Theory]
    [InlineData(false, "CaseSensitive", "second;third")]
    [InlineData(true, "CaseSensitive", "second;third")]
    [InlineData(false, "CaseInsensitive", "third")]
    [InlineData(true, "CaseInsensitive", "third")]
    public void MatchOnMetadataUsesAllNames(bool instanceModel, string options, string remaining)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record($"""
            <ItemGroup>
              <Source Include="unrelated" M="value" N="tuple" />
              <A Include="first" M="value" N="tuple" />
              <A Include="second" M="VALUE" N="TUPLE" />
              <A Include="third" M="value" N="other" />
              <A Remove="@(Source)" MatchOnMetadata="M;N" MatchOnMetadataOptions="{options}" />
            </ItemGroup>
            """);
        Identities(OfType(fixture.GetItems(), "A")).ShouldBe(remaining.Split(';'));
    }

    /// <summary>
    ///  Empty metadata does not match a metadata-based removal.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingMetadataDoesNotMatchRemoval(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <Source Include="source" />
              <A Include="a" />
              <A Remove="@(Source)" MatchOnMetadata="Missing" />
            </ItemGroup>
            """);
        Identities(OfType(fixture.GetItems(), "A")).ShouldBe(["a"]);
    }

    /// <summary>
    ///  Metadata-based Remove eagerly builds its match set from the captured source state.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchOnMetadataCapturesAndMaterializesItsSourceDuringRecording(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <Source Include="source" M="old" />
              <A Include="a" M="old" />
              <A Include="b" M="new" />
              <A Remove="@(Source)" MatchOnMetadata="M" />
              <Source Update="*" M="new" />
            </ItemGroup>
            """);
        fixture.CreatedItems.ShouldBe(1);
        Identities(OfType(fixture.GetItems(), "A")).ShouldBe(["b"]);
    }

    /// <summary>
    ///  Path-like metadata matches relative and absolute forms using the current directory.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchOnMetadataPathLikeNormalizesRelativeValues(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        env.SetCurrentDirectory(fixture.DirectoryPath);
        string absolute = Path.Combine(fixture.DirectoryPath, "nested", "file.txt");
        string relative = Path.Combine("nested", ".", "file.txt");
        fixture.Record($"""
            <ItemGroup>
              <Source Include="source" M="{absolute}" />
              <A Include="matched" M="{relative}" />
              <A Include="retained" M="other.txt" />
              <A Remove="@(Source)" MatchOnMetadata="M" MatchOnMetadataOptions="PathLike" />
            </ItemGroup>
            """);
        Identities(OfType(fixture.GetItems(), "A")).ShouldBe(["retained"]);
    }

    /// <summary>
    ///  Literal removal produces identical results around the default dictionary threshold.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    /// <param name="count">The item count on the running list.</param>
    [Theory]
    [InlineData(false, 99)]
    [InlineData(true, 99)]
    [InlineData(false, 100)]
    [InlineData(true, 100)]
    [InlineData(false, 101)]
    [InlineData(true, 101)]
    public void RemoveThresholdBoundaryPreservesResults(bool instanceModel, int count)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", "100");
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        string[] names = Enumerable.Range(0, count).Select(i => $"item{i}").ToArray();
        fixture.Record($"""
            <ItemGroup>
              <A Include="{string.Join(";", names)}" />
              <A Remove="item0;item50;missing" />
            </ItemGroup>
            """);
        Identities(fixture.GetItems()).ShouldBe(names.Where(n => n is not ("item0" or "item50")));
    }

    /// <summary>
    ///  Invalid metadata matching is rejected while recording, even without materialization.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidMatchOnMetadataFailsDuringRecording(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        var error = Should.Throw<InvalidProjectFileException>(() => fixture.Record("""
            <ItemGroup><A Remove="literal" MatchOnMetadata="M" /></ItemGroup>
            """));
        error.HelpKeyword.ShouldBe("MSBuild.OM_MatchOnMetadataIsRestrictedToReferencedItems");
    }

    /// <summary>
    ///  An empty selection still evaluates constant metadata at materialization time.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyIncludePreservesMetadataFailureTiming(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""
            <ItemGroup>
              <A Include="$(Missing)"><M>$([System.Int32]::Parse('invalid'))</M></A>
            </ItemGroup>
            """);
        fixture.CreatedItems.ShouldBe(0);
        Should.Throw<InvalidProjectFileException>(() => fixture.GetItems().ToArray());
    }

    /// <summary>
    ///  Literal exclusions also match non-filesystem items without performing a glob.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExcludePatternsApplyToLiteralItems(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        fixture.Record("""<ItemGroup><A Include="a.cs;b.txt;c.cs;semi%3Bcolon" Exclude="*.cs;semi%3Bcolon" /></ItemGroup>""");
        Identities(fixture.GetItems()).ShouldBe(["b.txt"]);
    }

    /// <summary>
    ///  Globs preserve deterministic ordering and recursive-directory metadata.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlobResultsPreserveOrderAndRecursiveDirectory(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        env.CreateFile(fixture.Directory, "z.cs", "");
        env.CreateFile(fixture.Directory, "a.cs", "");
        string nested = Path.Combine(fixture.DirectoryPath, "nested");
        TransientTestFolder nestedFolder = env.CreateFolder(nested);
        env.CreateFile(nestedFolder, "b.cs", "");
        string glob = Path.Combine("**", "*.cs");
        fixture.Record($"""<ItemGroup><A Include="{glob}" /></ItemGroup>""");
        ItemRecord[] items = fixture.GetItems().ToArray();
        Identities(items).ShouldBe(["a.cs", Path.Combine("nested", "b.cs"), "z.cs"]);
        items[1].Item.GetMetadataValue("RecursiveDir").ShouldBe("nested" + Path.DirectorySeparatorChar);
    }

    /// <summary>
    ///  A later identical glob removal avoids enumerating and creating discarded files.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdenticalGlobRemoveSuppressesEnumeration(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fileSystem = new RecordingFileSystem();
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel, fileSystem: fileSystem);
        env.CreateFile(fixture.Directory, "a.cs", "");
        fixture.Record("""<ItemGroup><A Include="*.cs" /><A Remove="*.cs" /></ItemGroup>""");
        fixture.GetItems().ShouldBeEmpty();
        fileSystem.Enumerations.ShouldBe(0);
        fixture.CreatedItems.ShouldBe(0);
    }

    /// <summary>
    ///  A reference before removal observes files even when the final state can suppress them.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlobSuppressionCannotContaminateEarlierReferences(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        env.CreateFile(fixture.Directory, "a.cs", "");
        env.CreateFile(fixture.Directory, "b.cs", "");
        fixture.Record("""
            <ItemGroup>
              <A Include="*.cs" M="old" />
              <B Include="@(A)" />
              <A Remove="*.cs" />
              <A Include="a.cs" M="new" />
            </ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Values(OfType(items, "B"), "M").ShouldBe(["a.cs:old", "b.cs:old"]);
        Values(OfType(items, "A"), "M").ShouldBe(["a.cs:new"]);
    }

    /// <summary>
    ///  Removal-range cache keys keep multiple historical and final glob states independent.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultipleGlobRemovalsRetainIndependentHistoricalStates(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        env.CreateFile(fixture.Directory, "a.cs", "");
        env.CreateFile(fixture.Directory, "b.cs", "");
        env.CreateFile(fixture.Directory, "c.txt", "");
        fixture.Record("""
            <ItemGroup>
              <A Include="*.cs" M="old" />
              <B Include="@(A)" />
              <A Remove="a*" />
              <A Update="b.cs" M="updated" />
              <C Include="@(A)" />
              <A Remove="*.cs" />
              <A Remove="*.txt" Condition="false" />
              <A Include="*.txt" M="final" />
            </ItemGroup>
            """);
        ItemRecord[] items = fixture.GetItems().ToArray();
        Values(OfType(items, "B"), "M").ShouldBe(["a.cs:old", "b.cs:old"]);
        Values(OfType(items, "C"), "M").ShouldBe(["b.cs:updated"]);
        Values(OfType(items, "A"), "M").ShouldBe(["c.txt:final"]);
        Identities(items).ShouldBe(["a.cs", "b.cs", "b.cs", "c.txt"]);
    }

    /// <summary>
    ///  An eager item-dependent condition prevents future removals from changing its answer.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConditionBeforeGlobRemovalStillSeesFiles(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        env.CreateFile(fixture.Directory, "a.cs", "");
        fixture.Record("""
            <ItemGroup>
              <A Include="*.cs" />
              <Marker Include="seen" Condition="'@(A)' != ''" />
              <A Remove="*.cs" />
            </ItemGroup>
            """);
        Identities(fixture.GetItems()).ShouldBe(["seen"]);
    }

    /// <summary>
    ///  Removal exclusions do not affect a later reinclude of the same glob.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LaterGlobReincludeIsNotSuppressed(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        env.CreateFile(fixture.Directory, "a.cs", "");
        fixture.Record("""
            <ItemGroup>
              <A Include="*.cs" />
              <A Remove="*.cs" />
              <A Include="*.cs" />
            </ItemGroup>
            """);
        Identities(fixture.GetItems()).ShouldBe(["a.cs"]);
    }

    /// <summary>
    ///  Different exclusions on the same glob cannot share an incorrect materialized result.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedGlobsRetainDistinctExclusions(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        env.CreateFile(fixture.Directory, "a.cs", "");
        env.CreateFile(fixture.Directory, "b.cs", "");
        fixture.Record("""
            <ItemGroup>
              <A Include="*.cs" Exclude="a.cs" M="first" />
              <A Include="*.cs" Exclude="b.cs" M="second" />
            </ItemGroup>
            """);
        Values(fixture.GetItems(), "M").ShouldBe(["b.cs:first", "a.cs:second"]);
    }

    /// <summary>
    ///  False-condition full-filesystem globs do not enumerate the real filesystem.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DangerousFalseConditionGlobIsSkipped(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildAlwaysEvaluateDangerousGlobs", null);
        var fileSystem = new RecordingFileSystem(useRealFileSystem: false);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel, fileSystem: fileSystem);
        string glob = Path.DirectorySeparatorChar + Path.Combine("**", "*.cs");
        fixture.Record($"""<ItemGroup><A Include="{glob}" Condition="false" /></ItemGroup>""");
        fixture.GetItems().ShouldBeEmpty();
        fileSystem.Enumerations.ShouldBe(0);
    }

    /// <summary>
    ///  The dangerous-glob compatibility switch still enumerates a controlled empty filesystem.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DangerousFalseConditionGlobEscapeHatchIsPreserved(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("MSBuildAlwaysEvaluateDangerousGlobs", "1");
        env.SetEnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD", null);
        var fileSystem = new RecordingFileSystem(useRealFileSystem: false);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel, fileSystem: fileSystem);
        string glob = Path.DirectorySeparatorChar + Path.Combine("**", "*.cs");
        fixture.Record($"""<ItemGroup><A Include="{glob}" Condition="false" /></ItemGroup>""");
        fixture.GetItems().ShouldBeEmpty();
        fileSystem.Enumerations.ShouldBeGreaterThan(0);
    }

    /// <summary>
    ///  A long same-type history is materialized without recursive linked-list traversal.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongHistoryRetainsAllItems(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel);
        StringBuilder body = new("<ItemGroup>");
        for (int i = 0; i < 10000; i++)
        {
            body.Append("<A Include=\"item").Append(i).Append("\" />");
        }

        body.Append("</ItemGroup>");
        fixture.Record(body.ToString());
        ItemRecord[] items = fixture.GetItems().ToArray();
        items.Length.ShouldBe(10000);
        items[0].Item.EvaluatedInclude.ShouldBe("item0");
        items[^1].Item.EvaluatedInclude.ShouldBe("item9999");
    }

    /// <summary>
    ///  Profiling scopes stay balanced across conditions, includes, updates, and removals.
    /// </summary>
    /// <param name="instanceModel">Whether to use instance-model items.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProfilingScopesRemainBalanced(bool instanceModel)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var fixture = new LazyItemEvaluatorTestFixture(env, instanceModel, profile: true);
        fixture.Record("""
            <ItemGroup>
              <A Include="a;b" />
              <A Update="*" M="updated" />
              <A Remove="b" />
              <B Include="@(A)" Condition="'@(A)' == 'a'" />
            </ItemGroup>
            """);
        Identities(fixture.GetItems()).ShouldBe(["a", "a"]);
        fixture.Profiler.IsEmpty().ShouldBeTrue();
        fixture.Profiler.ProfiledResult.ShouldNotBeNull();
        fixture.Profiler.ProfiledResult!.Value.ProfiledLocations.ShouldNotBeEmpty();
    }

    /// <summary>
    ///  Selects items of one MSBuild item type without discarding their provenance.
    /// </summary>
    /// <param name="items">The evaluator results.</param>
    /// <param name="itemType">The item type to select.</param>
    /// <returns>
    ///  The selected results.
    /// </returns>
    private static IEnumerable<ItemRecord> OfType(IEnumerable<ItemRecord> items, string itemType)
        => items.Where(i => i.Item.Key.Equals(itemType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///  Gets ordered identities for compact assertions.
    /// </summary>
    /// <param name="items">The evaluator results.</param>
    /// <returns>
    ///  Their ordered evaluated includes.
    /// </returns>
    private static string[] Identities(IEnumerable<ItemRecord> items)
        => items.Select(i => i.Item.EvaluatedInclude).ToArray();

    /// <summary>
    ///  Gets ordered identity/metadata pairs without ignoring empty metadata.
    /// </summary>
    /// <param name="items">The evaluator results.</param>
    /// <param name="metadata">The metadata name.</param>
    /// <returns>
    ///  Their ordered identity/metadata pairs.
    /// </returns>
    private static string[] Values(IEnumerable<ItemRecord> items, string metadata)
        => items.Select(i => $"{i.Item.EvaluatedInclude}:{i.Item.GetMetadataValue(metadata)}").ToArray();

    /// <summary>
    ///  Records filesystem enumeration while forwarding ordinary glob operations.
    /// </summary>
    private sealed class RecordingFileSystem : MSBuildFileSystemBase
    {
        /// <summary>
        ///  Whether enumeration may reach disk rather than the controlled empty filesystem.
        /// </summary>
        private readonly bool _useRealFileSystem;

        /// <summary>
        ///  Initializes a recording filesystem with an optional empty enumeration implementation.
        /// </summary>
        /// <param name="useRealFileSystem">Whether to forward enumeration to the real filesystem.</param>
        public RecordingFileSystem(bool useRealFileSystem = true) => _useRealFileSystem = useRealFileSystem;

        /// <summary>
        ///  Gets the number of filesystem enumerations requested.
        /// </summary>
        public int Enumerations { get; private set; }

        /// <summary>
        ///  Counts and forwards file enumeration.
        /// </summary>
        /// <param name="path">The directory to search.</param>
        /// <param name="searchPattern">The filename pattern.</param>
        /// <param name="searchOption">The recursion policy.</param>
        /// <returns>
        ///  The matching files.
        /// </returns>
        public override IEnumerable<string> EnumerateFiles(
            string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            Enumerations++;
            return _useRealFileSystem ? base.EnumerateFiles(path, searchPattern, searchOption) : [];
        }

        /// <summary>
        ///  Counts and forwards directory enumeration.
        /// </summary>
        /// <param name="path">The directory to search.</param>
        /// <param name="searchPattern">The directory-name pattern.</param>
        /// <param name="searchOption">The recursion policy.</param>
        /// <returns>
        ///  The matching directories.
        /// </returns>
        public override IEnumerable<string> EnumerateDirectories(
            string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            Enumerations++;
            return _useRealFileSystem ? base.EnumerateDirectories(path, searchPattern, searchOption) : [];
        }

        /// <summary>
        ///  Counts and forwards filesystem-entry enumeration.
        /// </summary>
        /// <param name="path">The directory to search.</param>
        /// <param name="searchPattern">The entry-name pattern.</param>
        /// <param name="searchOption">The recursion policy.</param>
        /// <returns>
        ///  The matching entries.
        /// </returns>
        public override IEnumerable<string> EnumerateFileSystemEntries(
            string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            Enumerations++;
            return _useRealFileSystem ? base.EnumerateFileSystemEntries(path, searchPattern, searchOption) : [];
        }
    }
}
