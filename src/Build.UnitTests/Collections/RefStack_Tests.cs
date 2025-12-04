// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public class RefStack_Tests
{
    [Fact]
    public void Constructor_WithCapacity_InitializesEmpty()
    {
        using RefStack<int> stack = new(10);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithScratchBuffer_InitializesEmpty()
    {
        using RefStack<int> stack = new(stackalloc int[10]);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyScratchBuffer_InitializesEmpty()
    {
        using RefStack<int> stack = new(scratchBuffer: []);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Push_SingleItem_AddsToStack()
    {
        using RefStack<int> stack = new(4);

        stack.Push(42);

        stack.IsEmpty.ShouldBeFalse();
        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(42);
    }

    [Fact]
    public void Push_MultipleItems_MaintainsLifoOrder()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(3);
    }

    [Fact]
    public void Push_ExceedsInitialCapacity_GrowsAutomatically()
    {
        using RefStack<int> stack = new(2);

        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        stack.Push(4);

        stack.Pop().ShouldBe(4);
        stack.Pop().ShouldBe(3);
        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
    }

    [Fact]
    public void Push_WithScratchBuffer_WorksCorrectly()
    {
        using RefStack<int> stack = new(stackalloc int[10]);

        stack.Push(1);
        stack.Push(2);

        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
    }

    [Fact]
    public void Push_WithScratchBuffer_CanGrowBeyondInitialCapacity()
    {
        using RefStack<int> stack = new(stackalloc int[2]);

        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.Pop().ShouldBe(3);
        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
    }

    [Fact]
    public void Pop_SingleItem_ReturnsItemAndMakesEmpty()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        int result = stack.Pop();

        result.ShouldBe(42);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_MultipleItems_ReturnsInLifoOrder()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.Pop().ShouldBe(3);
        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_AfterPush_MaintainsCorrectState()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.Push(2);
        stack.Pop().ShouldBe(2);
        stack.Push(3);

        stack.Pop().ShouldBe(3);
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPeek_EmptyStack_ReturnsFalse()
    {
        using RefStack<int> stack = new(4);

        bool result = stack.TryPeek(out int value);

        result.ShouldBeFalse();
        value.ShouldBe(default);
    }

    [Fact]
    public void TryPeek_NonEmptyStack_ReturnsTopItemWithoutRemoving()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        bool result = stack.TryPeek(out int value);

        result.ShouldBeTrue();
        value.ShouldBe(42);
        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void TryPeek_MultipleItems_ReturnsTopItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(3);

        stack.Pop().ShouldBe(3);
        stack.TryPeek(out value).ShouldBeTrue();
        value.ShouldBe(2);
    }

    [Fact]
    public void TryPeek_DoesNotModifyStack()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);

        stack.TryPeek(out int value1).ShouldBeTrue();
        stack.TryPeek(out int value2).ShouldBeTrue();

        value1.ShouldBe(2);
        value2.ShouldBe(2);
        stack.Pop().ShouldBe(2);
    }

    [Fact]
    public void IsEmpty_NewStack_ReturnsTrue()
    {
        using RefStack<int> stack = new(4);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void IsEmpty_AfterPush_ReturnsFalse()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);

        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void IsEmpty_AfterPushAndPop_ReturnsTrue()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        _ = stack.Pop();

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void IsEmpty_MultipleOperations_ReflectsCorrectState()
    {
        using RefStack<int> stack = new(4);

        stack.IsEmpty.ShouldBeTrue();

        stack.Push(1);
        stack.IsEmpty.ShouldBeFalse();

        stack.Push(2);
        stack.IsEmpty.ShouldBeFalse();

        _ = stack.Pop();
        stack.IsEmpty.ShouldBeFalse();

        _ = stack.Pop();
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        RefStack<int> stack = new(4);
        stack.Push(1);

        stack.Dispose();
        stack.Dispose(); // Should not throw
    }

    [Fact]
    public void Stack_WithReferenceType_WorksCorrectly()
    {
        using RefStack<string> stack = new(4);

        stack.Push("first");
        stack.Push("second");
        stack.Push("third");

        stack.Pop().ShouldBe("third");
        stack.TryPeek(out string? value).ShouldBeTrue();
        value.ShouldBe("second");
        stack.Pop().ShouldBe("second");
        stack.Pop().ShouldBe("first");
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Stack_WithNullableReferenceType_HandlesNullCorrectly()
    {
        using RefStack<string?> stack = new(4);

        stack.Push(null);
        stack.Push("value");

        stack.Pop().ShouldBe("value");
        stack.Pop().ShouldBeNull();
    }

    [Fact]
    public void Stack_WithStruct_WorksCorrectly()
    {
        using RefStack<(int X, int Y)> stack = new(4);

        stack.Push((1, 2));
        stack.Push((3, 4));

        stack.Pop().ShouldBe((3, 4));
        stack.Pop().ShouldBe((1, 2));
    }

    [Fact]
    public void Stack_LargeNumberOfOperations_WorksCorrectly()
    {
        using RefStack<int> stack = new(4);

        for (int i = 0; i < 100; i++)
        {
            stack.Push(i);
        }

        for (int i = 99; i >= 0; i--)
        {
            stack.Pop().ShouldBe(i);
        }

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Stack_InterleavedPushPop_WorksCorrectly()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.Push(2);
        stack.Pop().ShouldBe(2);

        stack.Push(3);
        stack.Push(4);
        stack.Pop().ShouldBe(4);
        stack.Pop().ShouldBe(3);

        stack.Push(5);
        stack.Pop().ShouldBe(5);
        stack.Pop().ShouldBe(1);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPeek_AfterMultipleOperations_ReturnsCorrectValue()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.Push(2);
        _ = stack.Pop();
        stack.Push(3);

        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(3);
    }

    [Fact]
    public void Push_InParameter_WorksCorrectly()
    {
        using RefStack<int> stack = new(4);
        int value = 42;

        stack.Push(in value);

        stack.Pop().ShouldBe(42);
    }
}
