// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public class OneOrMany_Tests
{
    [Fact]
    public void DefaultValueIsEmpty()
    {
        OneOrMany<int> values = default;

        values.IsEmpty.ShouldBeTrue();
        values.Count.ShouldBe(0);
        GetValues(values).ShouldBeEmpty();
    }

    [Fact]
    public void StoresOneDefaultValue()
    {
        OneOrMany<string?> values = [null];

        values.IsEmpty.ShouldBeFalse();
        values.Count.ShouldBe(1);
        values[0].ShouldBeNull();
        GetValues(values).ShouldBe([null]);
    }

    [Fact]
    public void StoresAndIndexesManyValues()
    {
        OneOrMany<int> values = [10, 20, 30];

        values.IsEmpty.ShouldBeFalse();
        values.Count.ShouldBe(3);
        values[0].ShouldBe(10);
        values[1].ShouldBe(20);
        values[2].ShouldBe(30);
        GetValues(values).ShouldBe([10, 20, 30]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void OneValueRejectsOutOfRangeIndex(int index)
    {
        OneOrMany<int> values = new(42);

        Should.Throw<ArgumentOutOfRangeException>(() => _ = values[index]);
    }

    [Fact]
    public void EmptyAndSingleElementArraysUseInlineForms()
    {
        OneOrMany<int> empty = [];
        OneOrMany<int> one = [42];

        empty.IsEmpty.ShouldBeTrue();
        empty.Count.ShouldBe(0);
        one.Count.ShouldBe(1);
        one[0].ShouldBe(42);
    }

    [Fact]
    public void BuilderStoresZeroOneAndManyValues()
    {
        using OneOrMany<int>.Builder builder = default;

        builder.IsEmpty.ShouldBeTrue();
        builder.ToOneOrMany().IsEmpty.ShouldBeTrue();

        builder.Add(10);
        builder.Count.ShouldBe(1);
        builder.ToOneOrMany()[0].ShouldBe(10);

        builder.Add(20);
        builder.Add(30);

        OneOrMany<int> values = builder.ToOneOrMany();
        values.Count.ShouldBe(3);
        values[0].ShouldBe(10);
        values[1].ShouldBe(20);
        values[2].ShouldBe(30);
    }

    [Fact]
    public void BuilderPreservesOneDefaultValue()
    {
        using OneOrMany<string?>.Builder builder = default;

        builder.Add(null);

        OneOrMany<string?> values = builder.ToOneOrMany();
        values.Count.ShouldBe(1);
        values[0].ShouldBeNull();
    }

    [Fact]
    public void ImmutableArrayConstructorPreservesManyValues()
    {
        OneOrMany<int> values = new(ImmutableArray.Create(10, 20, 30));

        values.Count.ShouldBe(3);
        GetValues(values).ShouldBe([10, 20, 30]);
    }

    [Fact]
    public void ItemRefUncheckedReturnsInlineAndAdditionalValues()
    {
        string first = new(['f', 'i', 'r', 's', 't']);
        string second = new(['s', 'e', 'c', 'o', 'n', 'd']);
        OneOrMany<string> values = [first, second];

        ref readonly string firstReference = ref OneOrMany<string>.ItemRefUnchecked(in values, 0);
        ref readonly string secondReference = ref OneOrMany<string>.ItemRefUnchecked(in values, 1);

        firstReference.ShouldBeSameAs(first);
        secondReference.ShouldBeSameAs(second);
    }

    private static List<T> GetValues<T>(OneOrMany<T> values)
    {
        List<T> result = [];

        foreach (T value in values)
        {
            result.Add(value);
        }

        return result;
    }
}
