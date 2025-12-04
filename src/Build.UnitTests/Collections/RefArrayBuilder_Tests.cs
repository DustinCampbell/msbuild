// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public class RefArrayBuilder_Tests
{
    [Fact]
    public void Constructor_InitializesWithCapacity()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.Count.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithScratchBuffer_UsesScratchBuffer()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[10]);

        builder.Count.ShouldBe(0);
        builder.Add(42);

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithScratchBuffer_CanGrowBeyondInitialCapacity()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[2]);

        builder.Add(1);
        builder.Add(2);
        builder.Add(3); // Should trigger growth

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Constructor_WithEmptyScratchBuffer_CanStillAdd()
    {
        using RefArrayBuilder<int> builder = new(scratchBuffer: []);

        builder.Count.ShouldBe(0);
        builder.Add(42); // Should trigger growth

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithScratchBuffer_AddRangeWorks()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[10]);

        builder.AddRange([1, 2, 3]);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Add_SingleItem_IncreasesLength()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(42);

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(42);
    }

    [Fact]
    public void Add_MultipleItems_MaintainsOrder()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Add_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.Add(4);
        builder.Add(5);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void AddRange_SingleElement_AddsCorrectly()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.AddRange([42]);

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(42);
    }

    [Fact]
    public void AddRange_MultipleElements_AddsAllInOrder()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.AddRange([2, 3, 4]);

        builder.Count.ShouldBe(4);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
    }

    [Fact]
    public void AddRange_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.AddRange([2, 3, 4, 5]);

        builder.Count.ShouldBe(5);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void AddRange_EmptySpan_DoesNothing()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.AddRange([]);

        builder.Count.ShouldBe(1);
        builder[0].ShouldBe(1);
    }

    [Fact]
    public void Insert_AtBeginning_ShiftsExistingElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(2);
        builder.Add(3);
        builder.Insert(0, 1);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Insert_InMiddle_ShiftsSubsequentElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(3);
        builder.Insert(1, 2);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Insert_AtEnd_AppendsElement()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Insert(2, 3);

        builder.Count.ShouldBe(3);
        builder[2].ShouldBe(3);
    }

    [Fact]
    public void Insert_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(3);
        builder.Add(4);
        builder.Add(5);
        builder.Insert(1, 2);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_AtBeginning_ShiftsExistingElements()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.Add(4);
        builder.Add(5);
        builder.InsertRange(0, [1, 2, 3]);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_InMiddle_ShiftsSubsequentElements()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.Add(1);
        builder.Add(5);
        builder.InsertRange(1, [2, 3, 4]);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_AtEnd_AppendsElements()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.Add(1);
        builder.Add(2);
        builder.InsertRange(2, [3, 4, 5]);

        builder.Count.ShouldBe(5);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_ExceedsCapacity_GrowsAutomatically()
    {
        using RefArrayBuilder<int> builder = new(2);
        builder.Add(1);
        builder.Add(5);
        builder.InsertRange(1, [2, 3, 4]);

        builder.Count.ShouldBe(5);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(3);
        builder[3].ShouldBe(4);
        builder[4].ShouldBe(5);
    }

    [Fact]
    public void InsertRange_EmptySpan_DoesNothing()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.InsertRange(1, []);

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
    }

    [Fact]
    public void Count_CanBeSet()
    {
        var builder = new RefArrayBuilder<int>(4);
        try
        {
            builder.Add(1);
            builder.Add(2);
            builder.Add(3);
            builder.Count = 2;

            builder.Count.ShouldBe(2);
            builder[0].ShouldBe(1);
            builder[1].ShouldBe(2);
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Fact]
    public void Indexer_ReturnsReference()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        ref int value = ref builder[1];
        value = 42;

        builder[1].ShouldBe(42);
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSlice()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);

        ReadOnlySpan<int> span = builder.AsSpan();

        span.Length.ShouldBe(3);
        span[0].ShouldBe(1);
        span[1].ShouldBe(2); span[2].ShouldBe(3);
        span[2].ShouldBe(3);
    }

    [Fact]
    public void AsSpan_EmptyBuilder_ReturnsEmptySpan()
    {
        using RefArrayBuilder<int> builder = new(4);

        ReadOnlySpan<int> span = builder.AsSpan();

        span.Length.ShouldBe(0);
    }

    [Fact]
    public void AsSpan_ReflectsChanges()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        ReadOnlySpan<int> span1 = builder.AsSpan();
        span1.Length.ShouldBe(2);

        builder.Add(3);

        ReadOnlySpan<int> span2 = builder.AsSpan();
        span2.Length.ShouldBe(3);
        span2[2].ShouldBe(3);
    }

    [Fact]
    public void AsMemory_ReturnsCorrectSlice()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);

        Memory<int> memory = builder.AsMemory();

        memory.Length.ShouldBe(3);
        memory.Span[0].ShouldBe(1);
        memory.Span[1].ShouldBe(2);
        memory.Span[2].ShouldBe(3);
    }

    [Fact]
    public void AsMemory_EmptyBuilder_ReturnsEmptyMemory()
    {
        using RefArrayBuilder<int> builder = new(4);

        Memory<int> memory = builder.AsMemory();

        memory.Length.ShouldBe(0);
    }

    [Fact]
    public void AsMemory_ReflectsChanges()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        Memory<int> memory1 = builder.AsMemory();
        memory1.Length.ShouldBe(2);

        builder.Add(3);

        Memory<int> memory2 = builder.AsMemory();
        memory2.Length.ShouldBe(3);
        memory2.Span[2].ShouldBe(3);
    }

    [Fact]
    public void AsMemory_CanBeConvertedToArray()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);

        int[] array = builder.AsMemory().ToArray();

        array.Length.ShouldBe(3);
        array[0].ShouldBe(1);
        array[1].ShouldBe(2);
        array[2].ShouldBe(3);
    }

    [Fact]
    public void WithReferenceTypes_WorksCorrectly()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.Add("hello");
        builder.Add("world");

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe("hello");
        builder[1].ShouldBe("world");
    }

    [Fact]
    public void WithReferenceTypes_AddRange_WorksCorrectly()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.AddRange(["one", "two", "three"]);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe("one");
        builder[1].ShouldBe("two");
        builder[2].ShouldBe("three");
    }

    [Fact]
    public void WithReferenceTypes_Insert_WorksCorrectly()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.Add("first");
        builder.Add("third");
        builder.Insert(1, "second");

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe("first");
        builder[1].ShouldBe("second");
        builder[2].ShouldBe("third");
    }

    [Fact]
    public void MultipleOperations_ComplexScenario()
    {
        using RefArrayBuilder<int> builder = new(2);

        // Start small and grow
        builder.Add(1);
        builder.Add(2);
        builder.AddRange([3, 4, 5]);
        builder.Insert(0, 0);
        builder.InsertRange(6, [6, 7, 8]);

        builder.Count.ShouldBe(9);

        // Verify sequence
        for (int i = 0; i < 9; i++)
        {
            builder[i].ShouldBe(i);
        }
    }

    [Fact]
    public void LargeCapacity_HandlesGrowthCorrectly()
    {
        using RefArrayBuilder<int> builder = new(2);

        // Add many items to trigger multiple growth operations
        for (int i = 0; i < 100; i++)
        {
            builder.Add(i);
        }

        builder.Count.ShouldBe(100);
        builder[0].ShouldBe(0);
        builder[50].ShouldBe(50);
        builder[99].ShouldBe(99);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        builder.Dispose();
        builder.Dispose(); // Should not throw
    }

    [Fact]
    public void RemoveAt_FirstElement_ShiftsSubsequentElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.RemoveAt(0);

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe(2);
        builder[1].ShouldBe(3);
    }

    [Fact]
    public void RemoveAt_MiddleElement_ShiftsSubsequentElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.Add(4);
        builder.RemoveAt(1);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(3);
        builder[2].ShouldBe(4);
    }

    [Fact]
    public void RemoveAt_LastElement_RemovesCorrectly()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.RemoveAt(2);

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
    }

    [Fact]
    public void RemoveAt_SingleElement_BecomesEmpty()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(42);
        builder.RemoveAt(0);

        builder.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveAt_WithReferenceTypes_ClearsReference()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.Add("first");
        builder.Add("second");
        builder.Add("third");
        builder.RemoveAt(1);

        builder.Count.ShouldBe(2);
        builder[0].ShouldBe("first");
        builder[1].ShouldBe("third");
    }

    [Fact]
    public void RemoveAt_MultipleRemovals_WorksCorrectly()
    {
        using RefArrayBuilder<int> builder = new(10);
        builder.AddRange([1, 2, 3, 4, 5]);
        builder.RemoveAt(2); // Remove 3
        builder.RemoveAt(1); // Remove 2

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(4);
        builder[2].ShouldBe(5);
    }

    [Fact]
    public void RemoveAt_RemoveAllElements_LeavesEmpty()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.RemoveAt(2);
        builder.RemoveAt(1);
        builder.RemoveAt(0);

        builder.Count.ShouldBe(0);
    }

    [Fact]
    public void RemoveAt_AfterGrowth_WorksCorrectly()
    {
        using RefArrayBuilder<int> builder = new(2);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3); // Triggers growth
        builder.Add(4);
        builder.RemoveAt(1);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(3);
        builder[2].ShouldBe(4);
    }

    [Fact]
    public void RemoveAt_CanAddAfterRemoval()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);
        builder.RemoveAt(1);
        builder.Add(4);

        builder.Count.ShouldBe(3);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(3);
        builder[2].ShouldBe(4);
    }

    [Fact]
    public void RemoveAt_WithScratchBuffer_WorksCorrectly()
    {
        using RefArrayBuilder<int> builder = new(stackalloc int[10]);
        builder.AddRange([1, 2, 3, 4, 5]);
        builder.RemoveAt(2);

        builder.Count.ShouldBe(4);
        builder[0].ShouldBe(1);
        builder[1].ShouldBe(2);
        builder[2].ShouldBe(4);
        builder[3].ShouldBe(5);
    }

    [Fact]
    public void ToImmutable_EmptyBuilder_ReturnsEmptyImmutableArray()
    {
        using RefArrayBuilder<int> builder = new(4);

        var result = builder.ToImmutable();

        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void ToImmutable_WithItems_ReturnsImmutableArrayWithCorrectElements()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);
        builder.Add(3);

        var result = builder.ToImmutable();

        result.Length.ShouldBe(3);
        result[0].ShouldBe(1);
        result[1].ShouldBe(2);
        result[2].ShouldBe(3);
    }

    [Fact]
    public void ToImmutable_WithReferenceTypes_ReturnsImmutableArrayWithCorrectElements()
    {
        using RefArrayBuilder<string> builder = new(4);
        builder.Add("hello");
        builder.Add("world");

        var result = builder.ToImmutable();

        result.Length.ShouldBe(2);
        result[0].ShouldBe("hello");
        result[1].ShouldBe("world");
    }

    [Fact]
    public void ToImmutable_AfterMultipleOperations_ReturnsCorrectImmutableArray()
    {
        using RefArrayBuilder<int> builder = new(2);
        builder.Add(1);
        builder.AddRange([2, 3]);
        builder.Insert(0, 0);
        builder.InsertRange(4, [4, 5]);

        var result = builder.ToImmutable();

        result.Length.ShouldBe(6);
        for (int i = 0; i < 6; i++)
        {
            result[i].ShouldBe(i);
        }
    }

    [Fact]
    public void ToImmutable_DoesNotModifyBuilder()
    {
        using RefArrayBuilder<int> builder = new(4);
        builder.Add(1);
        builder.Add(2);

        var result1 = builder.ToImmutable();
        builder.Add(3);
        var result2 = builder.ToImmutable();

        result1.Length.ShouldBe(2);
        result1[0].ShouldBe(1);
        result1[1].ShouldBe(2);

        result2.Length.ShouldBe(3);
        result2[0].ShouldBe(1);
        result2[1].ShouldBe(2);
        result2[2].ShouldBe(3);
    }

    [Fact]
    public void ToImmutable_AfterCountSet_ReturnsCorrectImmutableArray()
    {
        var builder = new RefArrayBuilder<int>(4);
        try
        {
            builder.Add(1);
            builder.Add(2);
            builder.Add(3);
            builder.Count = 2;

            var result = builder.ToImmutable();

            result.Length.ShouldBe(2);
            result[0].ShouldBe(1);
            result[1].ShouldBe(2);
        }
        finally
        {
            builder.Dispose();
        }
    }
}
