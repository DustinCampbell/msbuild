// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Provides additional Shouldly assertions for MSBuild unit tests.
/// </summary>
public static class ShouldlyExtensions
{
    /// <summary>
    ///  Verifies that a dictionary contains the specified key and applies the assertion to its value.
    /// </summary>
    /// <typeparam name="TKey">The type of key in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of value in the dictionary.</typeparam>
    /// <param name="actual">The dictionary to verify.</param>
    /// <param name="key">The key whose value to verify.</param>
    /// <param name="valueAssertion">The assertion to apply to the value.</param>
    public static void ShouldContain<TKey, TValue>(this IDictionary<TKey, TValue> actual, TKey key, Action<TValue> valueAssertion)
        where TKey : notnull
    {
        actual.ShouldNotBeNull();
        valueAssertion.ShouldNotBeNull();

        actual.ShouldContainKey(key);
        valueAssertion(actual[key]);
    }

    /// <summary>
    ///  Verifies that an enumerable contains an item at the specified index and applies the assertion to that item.
    /// </summary>
    /// <typeparam name="T">The type of item in the enumerable.</typeparam>
    /// <param name="actual">The enumerable to verify.</param>
    /// <param name="index">The zero-based index of the item to verify.</param>
    /// <param name="itemAssertion">The assertion to apply to the item.</param>
    public static void ShouldHaveItemAt<T>(this IEnumerable<T> actual, int index, Action<T> itemAssertion)
    {
        actual.ShouldNotBeNull();
        itemAssertion.ShouldNotBeNull();
        index.ShouldBeGreaterThanOrEqualTo(0);

        T item = actual.ElementAt(index);
        itemAssertion(item);
    }

    /// <summary>
    ///  Verifies that an enumerable contains exactly one item for each assertion and applies the assertions in order.
    /// </summary>
    /// <typeparam name="T">The type of item in the enumerable.</typeparam>
    /// <param name="actual">The enumerable to verify.</param>
    /// <param name="itemAssertions">The assertions to apply to the corresponding items.</param>
    public static void ShouldHaveItems<T>(this IEnumerable<T> actual, params Action<T>[] itemAssertions)
    {
        actual.ShouldNotBeNull();
        itemAssertions.ShouldNotBeNull().ShouldNotBeEmpty();

        int count = 0;
        foreach (T item in actual)
        {
            count.ShouldBeLessThan(
                itemAssertions.Length,
                $"Expected exactly {itemAssertions.Length} items, but found an additional item at index {count}.");

            Action<T> itemAssertion = itemAssertions[count].ShouldNotBeNull();
            itemAssertion(item);
            count++;
        }

        count.ShouldBe(itemAssertions.Length);
    }

    /// <summary>
    ///  Verifies that an enumerable contains exactly one item and applies the assertion to that item.
    /// </summary>
    /// <typeparam name="T">The type of item in the enumerable.</typeparam>
    /// <param name="actual">The enumerable to verify.</param>
    /// <param name="itemAssertion">The assertion to apply to the item.</param>
    public static void ShouldHaveSingleItem<T>(this IEnumerable<T> actual, Action<T> itemAssertion)
    {
        actual.ShouldNotBeNull();
        itemAssertion.ShouldNotBeNull();

        T item = actual.ShouldHaveSingleItem();
        itemAssertion(item);
    }

    /// <summary>
    ///  Verifies that a dictionary contains exactly one entry with the specified key and applies the assertion to its value.
    /// </summary>
    /// <typeparam name="TKey">The type of key in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of value in the dictionary.</typeparam>
    /// <param name="actual">The dictionary to verify.</param>
    /// <param name="key">The expected key of the single entry.</param>
    /// <param name="valueAssertion">The assertion to apply to the value.</param>
    public static void ShouldHaveSingleItem<TKey, TValue>(this IDictionary<TKey, TValue> actual, TKey key, Action<TValue> valueAssertion)
        where TKey : notnull
    {
        actual.ShouldNotBeNull();
        valueAssertion.ShouldNotBeNull();

        actual.Count.ShouldBe(1);
        actual.ShouldContain(key, valueAssertion);
    }
}
