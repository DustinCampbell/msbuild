// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public class CompactDictionary_Tests
{
    [Fact]
    public void Empty_CountIsZero()
    {
        CompactDictionary<string, int>.Empty.Count.ShouldBe(0);
    }

    [Fact]
    public void Empty_ContainsKey_ReturnsFalse()
    {
        CompactDictionary<string, int>.Empty.ContainsKey("key").ShouldBeFalse();
    }

    [Fact]
    public void Empty_TryGetValue_ReturnsFalse()
    {
        CompactDictionary<string, int>.Empty.TryGetValue("key", out _).ShouldBeFalse();
    }

    [Fact]
    public void Empty_Indexer_ThrowsKeyNotFoundException()
    {
        Should.Throw<KeyNotFoundException>(() => _ = CompactDictionary<string, int>.Empty["key"]);
    }

    [Fact]
    public void Empty_Keys_IsEmpty()
    {
        CompactDictionary<string, int>.Empty.Keys.Length.ShouldBe(0);
    }

    [Fact]
    public void Empty_Values_IsEmpty()
    {
        CompactDictionary<string, int>.Empty.Values.Length.ShouldBe(0);
    }

    [Fact]
    public void Empty_Enumerator_HasNoElements()
    {
        var enumerator = CompactDictionary<string, int>.Empty.GetEnumerator();
        enumerator.MoveNext().ShouldBeFalse();
    }

    [Fact]
    public void SingleEntry_CountIsOne()
    {
        var dict = BuildDictionary(("key", "value"));
        dict.Count.ShouldBe(1);
    }

    [Fact]
    public void SingleEntry_ContainsKey_ReturnsTrue()
    {
        var dict = BuildDictionary(("key", "value"));
        dict.ContainsKey("key").ShouldBeTrue();
    }

    [Fact]
    public void SingleEntry_ContainsKey_ReturnsFalseForMissingKey()
    {
        var dict = BuildDictionary(("key", "value"));
        dict.ContainsKey("other").ShouldBeFalse();
    }

    [Fact]
    public void SingleEntry_TryGetValue_ReturnsValueForExistingKey()
    {
        var dict = BuildDictionary(("key", "value"));
        dict.TryGetValue("key", out var value).ShouldBeTrue();
        value.ShouldBe("value");
    }

    [Fact]
    public void SingleEntry_TryGetValue_ReturnsFalseForMissingKey()
    {
        var dict = BuildDictionary(("key", "value"));
        dict.TryGetValue("other", out _).ShouldBeFalse();
    }

    [Fact]
    public void SingleEntry_Indexer_ReturnsValue()
    {
        var dict = BuildDictionary(("key", "value"));
        dict["key"].ShouldBe("value");
    }

    [Fact]
    public void SingleEntry_Indexer_ThrowsForMissingKey()
    {
        var dict = BuildDictionary(("key", "value"));
        Should.Throw<KeyNotFoundException>(() => _ = dict["other"]);
    }

    [Fact]
    public void SingleEntry_Keys_ContainsKey()
    {
        var dict = BuildDictionary(("key", "value"));
        dict.Keys.Length.ShouldBe(1);
        dict.Keys[0].ShouldBe("key");
    }

    [Fact]
    public void SingleEntry_Values_ContainsValue()
    {
        var dict = BuildDictionary(("key", "value"));
        dict.Values.Length.ShouldBe(1);
        dict.Values[0].ShouldBe("value");
    }

    [Fact]
    public void SingleEntry_Enumerator_YieldsEntry()
    {
        var dict = BuildDictionary(("key", "value"));
        EnumerateAll(dict).ShouldBe([new KeyValuePair<string, string>("key", "value")]);
    }

    [Fact]
    public void TwoEntries_CountIsTwo()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict.Count.ShouldBe(2);
    }

    [Fact]
    public void TwoEntries_ContainsKey_ReturnsTrueForBothKeys()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict.ContainsKey("a").ShouldBeTrue();
        dict.ContainsKey("b").ShouldBeTrue();
    }

    [Fact]
    public void TwoEntries_ContainsKey_ReturnsFalseForMissingKey()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict.ContainsKey("c").ShouldBeFalse();
    }

    [Fact]
    public void TwoEntries_TryGetValue_ReturnsValuesForBothKeys()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));

        dict.TryGetValue("a", out var v1).ShouldBeTrue();
        v1.ShouldBe("1");

        dict.TryGetValue("b", out var v2).ShouldBeTrue();
        v2.ShouldBe("2");
    }

    [Fact]
    public void TwoEntries_TryGetValue_ReturnsFalseForMissingKey()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict.TryGetValue("c", out _).ShouldBeFalse();
    }

    [Fact]
    public void TwoEntries_Indexer_ReturnsBothValues()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict["a"].ShouldBe("1");
        dict["b"].ShouldBe("2");
    }

    [Fact]
    public void TwoEntries_Indexer_ThrowsForMissingKey()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        Should.Throw<KeyNotFoundException>(() => _ = dict["c"]);
    }

    [Fact]
    public void TwoEntries_Keys_ContainsBothKeys()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict.Keys.ToArray().ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public void TwoEntries_Values_ContainsBothValues()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict.Values.ToArray().ShouldBe(["1", "2"], ignoreOrder: true);
    }

    [Fact]
    public void TwoEntries_Enumerator_YieldsBothEntries()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"));
        var items = EnumerateAll(dict);
        items.Count.ShouldBe(2);
        items.ShouldContain(new KeyValuePair<string, string>("a", "1"));
        items.ShouldContain(new KeyValuePair<string, string>("b", "2"));
    }

    [Fact]
    public void ThreeEntries_CountIsThree()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"), ("c", "3"));
        dict.Count.ShouldBe(3);
    }

    [Fact]
    public void ThreeEntries_ContainsKey_ReturnsTrueForAllKeys()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"), ("c", "3"));
        dict.ContainsKey("a").ShouldBeTrue();
        dict.ContainsKey("b").ShouldBeTrue();
        dict.ContainsKey("c").ShouldBeTrue();
    }

    [Fact]
    public void ThreeEntries_ContainsKey_ReturnsFalseForMissingKey()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"), ("c", "3"));
        dict.ContainsKey("d").ShouldBeFalse();
    }

    [Fact]
    public void ThreeEntries_TryGetValue_ReturnsValuesForAllKeys()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"), ("c", "3"));

        dict.TryGetValue("a", out var v1).ShouldBeTrue();
        v1.ShouldBe("1");

        dict.TryGetValue("b", out var v2).ShouldBeTrue();
        v2.ShouldBe("2");

        dict.TryGetValue("c", out var v3).ShouldBeTrue();
        v3.ShouldBe("3");
    }

    [Fact]
    public void ThreeEntries_Indexer_ReturnsAllValues()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"), ("c", "3"));
        dict["a"].ShouldBe("1");
        dict["b"].ShouldBe("2");
        dict["c"].ShouldBe("3");
    }

    [Fact]
    public void FiveEntries_AllOperationsWork()
    {
        var dict = BuildDictionary(("a", "1"), ("b", "2"), ("c", "3"), ("d", "4"), ("e", "5"));

        dict.Count.ShouldBe(5);
        dict["a"].ShouldBe("1");
        dict["e"].ShouldBe("5");
        dict.ContainsKey("c").ShouldBeTrue();
        dict.ContainsKey("z").ShouldBeFalse();
    }

    [Fact]
    public void CaseInsensitive_SingleEntry_FindsKeyIgnoringCase()
    {
        var dict = BuildDictionary(StringComparer.OrdinalIgnoreCase, ("Key", "value"));

        dict.ContainsKey("KEY").ShouldBeTrue();
        dict.ContainsKey("key").ShouldBeTrue();
        dict["kEy"].ShouldBe("value");
    }

    [Fact]
    public void CaseInsensitive_TwoEntries_FindsKeysIgnoringCase()
    {
        var dict = BuildDictionary(StringComparer.OrdinalIgnoreCase, ("Foo", "1"), ("Bar", "2"));

        dict["FOO"].ShouldBe("1");
        dict["bar"].ShouldBe("2");
        dict.ContainsKey("BAR").ShouldBeTrue();
    }

    [Fact]
    public void CaseInsensitive_ThreeEntries_FindsKeysIgnoringCase()
    {
        var dict = BuildDictionary(StringComparer.OrdinalIgnoreCase, ("A", "1"), ("B", "2"), ("C", "3"));

        dict["a"].ShouldBe("1");
        dict["b"].ShouldBe("2");
        dict["c"].ShouldBe("3");
    }

    [Fact]
    public void Builder_Add_ZeroItems_ReturnsEmptySingleton()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Build().ShouldBeSameAs(CompactDictionary<string, string>.Empty);
    }

    [Fact]
    public void Builder_Add_OneItem_ReturnsSingleEntry()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("key", "value");

        var dict = builder.Build();
        dict.Count.ShouldBe(1);
        dict["key"].ShouldBe("value");
    }

    [Fact]
    public void Builder_Add_TwoItems_ReturnsTwoEntries()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("a", "1");
        builder.Add("b", "2");

        var dict = builder.Build();
        dict.Count.ShouldBe(2);
        dict["a"].ShouldBe("1");
        dict["b"].ShouldBe("2");
    }

    [Fact]
    public void Builder_Add_ThreeItems_SpillsToDictionary()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("a", "1");
        builder.Add("b", "2");
        builder.Add("c", "3");

        var dict = builder.Build();
        dict.Count.ShouldBe(3);
        dict["a"].ShouldBe("1");
        dict["b"].ShouldBe("2");
        dict["c"].ShouldBe("3");
    }

    [Fact]
    public void Builder_Add_FourItems_ContinuesInDictionary()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("a", "1");
        builder.Add("b", "2");
        builder.Add("c", "3");
        builder.Add("d", "4");

        var dict = builder.Build();
        dict.Count.ShouldBe(4);
        dict["d"].ShouldBe("4");
    }

    [Fact]
    public void Builder_Set_IntoEmpty_AddsEntry()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Set("key", "value");

        var dict = builder.Build();
        dict.Count.ShouldBe(1);
        dict["key"].ShouldBe("value");
    }

    [Fact]
    public void Builder_Set_ExistingFirstKey_ReplacesValue()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("key", "old");
        builder.Set("key", "new");

        var dict = builder.Build();
        dict.Count.ShouldBe(1);
        dict["key"].ShouldBe("new");
    }

    [Fact]
    public void Builder_Set_ExistingSecondKey_ReplacesValue()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("a", "1");
        builder.Add("b", "old");
        builder.Set("b", "new");

        var dict = builder.Build();
        dict.Count.ShouldBe(2);
        dict["a"].ShouldBe("1");
        dict["b"].ShouldBe("new");
    }

    [Fact]
    public void Builder_Set_ExistingFirstKey_WithTwoEntries_ReplacesValue()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("a", "old");
        builder.Add("b", "2");
        builder.Set("a", "new");

        var dict = builder.Build();
        dict.Count.ShouldBe(2);
        dict["a"].ShouldBe("new");
        dict["b"].ShouldBe("2");
    }

    [Fact]
    public void Builder_Set_ThirdKey_WithTwoEntries_SpillsToDictionary()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("a", "1");
        builder.Add("b", "2");
        builder.Set("c", "3");

        var dict = builder.Build();
        dict.Count.ShouldBe(3);
        dict["a"].ShouldBe("1");
        dict["b"].ShouldBe("2");
        dict["c"].ShouldBe("3");
    }

    [Fact]
    public void Builder_Set_ExistingKey_InDictionary_ReplacesValue()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.Ordinal);
        builder.Add("a", "1");
        builder.Add("b", "2");
        builder.Add("c", "old");
        builder.Set("c", "new");

        var dict = builder.Build();
        dict.Count.ShouldBe(3);
        dict["c"].ShouldBe("new");
    }

    [Fact]
    public void Builder_Set_CaseInsensitive_ReplacesMatchingKey()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.OrdinalIgnoreCase);
        builder.Add("Key", "old");
        builder.Set("KEY", "new");

        var dict = builder.Build();
        dict.Count.ShouldBe(1);
        dict["key"].ShouldBe("new");
    }

    [Fact]
    public void Builder_Set_CaseInsensitive_TwoEntries_ReplacesMatchingKey()
    {
        var builder = new CompactDictionary<string, string>.Builder(StringComparer.OrdinalIgnoreCase);
        builder.Add("Foo", "1");
        builder.Add("Bar", "old");
        builder.Set("BAR", "new");

        var dict = builder.Build();
        dict.Count.ShouldBe(2);
        dict["bar"].ShouldBe("new");
    }

    [Fact]
    public void IReadOnlyDictionary_TryGetValue_WorksThroughInterface()
    {
        IReadOnlyDictionary<string, string> dict = BuildDictionary(("key", "value"));
        dict.TryGetValue("key", out var value).ShouldBeTrue();
        value.ShouldBe("value");
    }

    [Fact]
    public void IReadOnlyDictionary_Keys_ReturnsEnumerable()
    {
        IReadOnlyDictionary<string, string> dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict.Keys.ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public void IReadOnlyDictionary_Values_ReturnsEnumerable()
    {
        IReadOnlyDictionary<string, string> dict = BuildDictionary(("a", "1"), ("b", "2"));
        dict.Values.ShouldBe(["1", "2"], ignoreOrder: true);
    }

    [Fact]
    public void IReadOnlyDictionary_Enumeration_WorksWithLinq()
    {
        IReadOnlyDictionary<string, string> dict = BuildDictionary(("a", "1"), ("b", "2"), ("c", "3"));
        dict.Count.ShouldBe(3);
        dict.Where(kvp => kvp.Key == "b").Single().Value.ShouldBe("2");
    }

    [Fact]
    public void Enumerator_Current_ThrowsBeforeMoveNext()
    {
        var dict = BuildDictionary(("a", "1"));
        var enumerator = dict.GetEnumerator();
        Should.Throw<InvalidOperationException>(() => _ = enumerator.Current);
    }

    [Fact]
    public void Enumerator_Current_ThrowsAfterExhausted()
    {
        var dict = BuildDictionary(("a", "1"));
        var enumerator = dict.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        Should.Throw<InvalidOperationException>(() => _ = enumerator.Current);
    }

    [Fact]
    public void Enumerator_MoveNext_ReturnsFalseRepeatedly()
    {
        var dict = BuildDictionary(("a", "1"));
        var enumerator = dict.GetEnumerator();

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.MoveNext().ShouldBeFalse();
        enumerator.MoveNext().ShouldBeFalse();
    }

    private static CompactDictionary<string, string> BuildDictionary(params (string key, string value)[] items)
        => BuildDictionary(StringComparer.Ordinal, items);

    private static CompactDictionary<string, string> BuildDictionary(IEqualityComparer<string> comparer, params (string key, string value)[] items)
    {
        var builder = new CompactDictionary<string, string>.Builder(comparer);

        foreach (var (key, value) in items)
        {
            builder.Add(key, value);
        }

        return builder.Build();
    }

    private static List<KeyValuePair<TKey, TValue>> EnumerateAll<TKey, TValue>(CompactDictionary<TKey, TValue> dict)
        where TKey : notnull
    {
        var list = new List<KeyValuePair<TKey, TValue>>();
        var enumerator = dict.GetEnumerator();

        while (enumerator.MoveNext())
        {
            list.Add(enumerator.Current);
        }

        return list;
    }
}
