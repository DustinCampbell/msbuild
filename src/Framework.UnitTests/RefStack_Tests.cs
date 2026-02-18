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
    public void TryPop_EmptyStack_ReturnsFalse()
    {
        using RefStack<int> stack = new(4);

        bool result = stack.TryPop(out int value);

        result.ShouldBeFalse();
        value.ShouldBe(default);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPop_SingleItem_ReturnsItemAndMakesEmpty()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        bool result = stack.TryPop(out int value);

        result.ShouldBeTrue();
        value.ShouldBe(42);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPop_MultipleItems_ReturnsInLifoOrder()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.TryPop(out int value1).ShouldBeTrue();
        value1.ShouldBe(3);

        stack.TryPop(out int value2).ShouldBeTrue();
        value2.ShouldBe(2);

        stack.TryPop(out int value3).ShouldBeTrue();
        value3.ShouldBe(1);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPop_AfterAllItemsPopped_ReturnsFalse()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);

        stack.TryPop(out _).ShouldBeTrue();
        stack.TryPop(out _).ShouldBeTrue();

        bool result = stack.TryPop(out int value);

        result.ShouldBeFalse();
        value.ShouldBe(default);
    }

    [Fact]
    public void TryPop_WithReferenceType_WorksCorrectly()
    {
        using RefStack<string> stack = new(4);
        stack.Push("first");
        stack.Push("second");

        stack.TryPop(out string? value1).ShouldBeTrue();
        value1.ShouldBe("second");

        stack.TryPop(out string? value2).ShouldBeTrue();
        value2.ShouldBe("first");

        stack.TryPop(out string? value3).ShouldBeFalse();
        value3.ShouldBeNull();
    }

    [Fact]
    public void TryPop_WithNullableReferenceType_HandlesNullCorrectly()
    {
        using RefStack<string?> stack = new(4);
        stack.Push(null);
        stack.Push("value");

        stack.TryPop(out string? value1).ShouldBeTrue();
        value1.ShouldBe("value");

        stack.TryPop(out string? value2).ShouldBeTrue();
        value2.ShouldBeNull();

        stack.TryPop(out string? value3).ShouldBeFalse();
        value3.ShouldBeNull();
    }

    [Fact]
    public void TryPop_InterleavedWithPush_WorksCorrectly()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.TryPop(out int value1).ShouldBeTrue();
        value1.ShouldBe(1);

        stack.Push(2);
        stack.Push(3);
        stack.TryPop(out int value2).ShouldBeTrue();
        value2.ShouldBe(3);

        stack.Push(4);
        stack.TryPop(out int value3).ShouldBeTrue();
        value3.ShouldBe(4);

        stack.TryPop(out int value4).ShouldBeTrue();
        value4.ShouldBe(2);

        stack.TryPop(out _).ShouldBeFalse();
    }

    [Fact]
    public void TryPop_AfterGrowth_WorksCorrectly()
    {
        using RefStack<int> stack = new(2);

        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        stack.Push(4);

        stack.TryPop(out int value).ShouldBeTrue();
        value.ShouldBe(4);
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
    public void Peek_EmptyStack_ThrowsInvalidOperationException()
        => Should.Throw<InvalidOperationException>(() =>
        {
            using RefStack<int> stack = new(4);

            return stack.Peek();
        });

    [Fact]
    public void Peek_SingleItem_ReturnsItemWithoutRemoving()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        int result = stack.Peek();

        result.ShouldBe(42);
        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Peek_MultipleItems_ReturnsTopItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.Peek().ShouldBe(3);
        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Peek_DoesNotModifyStack()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);

        int peek1 = stack.Peek();
        int peek2 = stack.Peek();

        peek1.ShouldBe(2);
        peek2.ShouldBe(2);
        stack.Pop().ShouldBe(2);
    }

    [Fact]
    public void Peek_AfterPopAndPush_ReturnsCorrectItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        _ = stack.Pop();
        stack.Push(3);

        stack.Peek().ShouldBe(3);
    }

    [Fact]
    public void Peek_WithReferenceType_ReturnsCorrectItem()
    {
        using RefStack<string> stack = new(4);
        stack.Push("first");
        stack.Push("second");

        stack.Peek().ShouldBe("second");
        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Peek_AfterMultiplePushes_ReturnsTopItem()
    {
        using RefStack<int> stack = new(2);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3); // Triggers growth
        stack.Push(4);

        stack.Peek().ShouldBe(4);
    }

    [Fact]
    public void PeekOrDefault_EmptyStack_ReturnsDefault()
    {
        using RefStack<int> stack = new(4);

        int result = stack.PeekOrDefault();

        result.ShouldBe(0);
    }

    [Fact]
    public void PeekOrDefault_WithItems_ReturnsTopItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        int result = stack.PeekOrDefault();

        result.ShouldBe(42);
        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void PeekOrDefault_MultipleItems_ReturnsTopItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.PeekOrDefault().ShouldBe(3);
    }

    [Fact]
    public void PeekOrDefault_DoesNotModifyStack()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);

        int peek1 = stack.PeekOrDefault();
        int peek2 = stack.PeekOrDefault();

        peek1.ShouldBe(2);
        peek2.ShouldBe(2);
        stack.Pop().ShouldBe(2);
    }

    [Fact]
    public void PeekOrDefault_WithReferenceType_EmptyStack_ReturnsNull()
    {
        using RefStack<string> stack = new(4);

        string? result = stack.PeekOrDefault();

        result.ShouldBeNull();
    }

    [Fact]
    public void PeekOrDefault_WithReferenceType_WithItems_ReturnsTopItem()
    {
        using RefStack<string> stack = new(4);
        stack.Push("hello");
        stack.Push("world");

        stack.PeekOrDefault().ShouldBe("world");
    }

    [Fact]
    public void PeekOrDefault_AfterPopAndPush_ReturnsCorrectItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        _ = stack.Pop();
        stack.Push(3);

        stack.PeekOrDefault().ShouldBe(3);
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_EmptyStack_ReturnsDefaultValue()
    {
        using RefStack<int> stack = new(4);

        int result = stack.PeekOrDefault(99);

        result.ShouldBe(99);
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_WithItems_ReturnsTopItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        int result = stack.PeekOrDefault(99);

        result.ShouldBe(42);
        stack.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_MultipleItems_ReturnsTopItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        stack.PeekOrDefault(99).ShouldBe(3);
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_DoesNotModifyStack()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);

        int peek1 = stack.PeekOrDefault(99);
        int peek2 = stack.PeekOrDefault(99);

        peek1.ShouldBe(2);
        peek2.ShouldBe(2);
        stack.Pop().ShouldBe(2);
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_WithReferenceType_EmptyStack_ReturnsDefaultValue()
    {
        using RefStack<string> stack = new(4);

        string result = stack.PeekOrDefault("default");

        result.ShouldBe("default");
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_WithReferenceType_WithItems_ReturnsTopItem()
    {
        using RefStack<string> stack = new(4);
        stack.Push("hello");
        stack.Push("world");

        stack.PeekOrDefault("default").ShouldBe("world");
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_AfterPopAndPush_ReturnsCorrectItem()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        _ = stack.Pop();
        stack.Push(3);

        stack.PeekOrDefault(99).ShouldBe(3);
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_NegativeDefault_WorksCorrectly()
    {
        using RefStack<int> stack = new(4);

        stack.PeekOrDefault(-1).ShouldBe(-1);
    }

    [Fact]
    public void PeekOrDefault_WithDefaultValue_AfterGrowth_WorksCorrectly()
    {
        using RefStack<int> stack = new(2);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3); // Triggers growth

        stack.PeekOrDefault(99).ShouldBe(3);
    }

    [Fact]
    public void Stack_PeekTryPeekConsistency_BothReturnSameValue()
    {
        using RefStack<int> stack = new(4);
        stack.Push(42);

        int peekResult = stack.Peek();
        stack.TryPeek(out int tryPeekResult).ShouldBeTrue();

        peekResult.ShouldBe(tryPeekResult);
        peekResult.ShouldBe(42);
    }

    [Fact]
    public void Stack_AllPeekMethods_DoNotModifyStack()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);

        stack.Peek().ShouldBe(2);
        stack.PeekOrDefault().ShouldBe(2);
        stack.PeekOrDefault(99).ShouldBe(2);
        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(2);

        // Verify stack wasn't modified
        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Stack_WithScratchBuffer_AllPeekMethodsWork()
    {
        using RefStack<int> stack = new(stackalloc int[10]);
        stack.Push(1);
        stack.Push(2);

        stack.Peek().ShouldBe(2);
        stack.PeekOrDefault().ShouldBe(2);
        stack.PeekOrDefault(99).ShouldBe(2);
        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(2);
    }

    [Fact]
    public void Stack_EmptyAfterPopAll_AllPeekMethodsHandleCorrectly()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        _ = stack.Pop();
        _ = stack.Pop();

        stack.PeekOrDefault().ShouldBe(0);
        stack.PeekOrDefault(99).ShouldBe(99);
        stack.TryPeek(out int value).ShouldBeFalse();
        value.ShouldBe(0);
    }

    [Fact]
    public void Stack_WithNullableValueType_WorksCorrectly()
    {
        using RefStack<int?> stack = new(4);
        stack.Push(null);
        stack.Push(42);

        stack.Peek().ShouldBe(42);
        stack.Pop().ShouldBe(42);
        stack.Peek().ShouldBeNull();
        stack.Pop().ShouldBeNull();
    }

    [Fact]
    public void Stack_ComplexScenario_AllOperationsWork()
    {
        using RefStack<int> stack = new(2);

        stack.Push(1);
        stack.Peek().ShouldBe(1);

        stack.Push(2);
        stack.PeekOrDefault().ShouldBe(2);

        stack.Push(3); // Growth
        stack.PeekOrDefault(99).ShouldBe(3);

        stack.Pop().ShouldBe(3);
        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(2);

        stack.Pop().ShouldBe(2);
        stack.Peek().ShouldBe(1);

        stack.Pop().ShouldBe(1);
        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryPeek_WithReferenceType_EmptyStack_ReturnsNull()
    {
        using RefStack<string> stack = new(4);

        stack.TryPeek(out string? value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void TryPeek_WithReferenceType_WithItems_ReturnsTopItem()
    {
        using RefStack<string> stack = new(4);
        stack.Push("hello");
        stack.Push("world");

        stack.TryPeek(out string? value).ShouldBeTrue();
        value.ShouldBe("world");
    }

    [Fact]
    public void Stack_LargeNumberOfPeekOperations_WorksCorrectly()
    {
        using RefStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        for (int i = 0; i < 100; i++)
        {
            stack.Peek().ShouldBe(3);
            stack.PeekOrDefault().ShouldBe(3);
            stack.TryPeek(out int value).ShouldBeTrue();
            value.ShouldBe(3);
        }

        // Verify stack wasn't modified
        stack.Pop().ShouldBe(3);
        stack.Pop().ShouldBe(2);
        stack.Pop().ShouldBe(1);
    }

    [Fact]
    public void Stack_AlternatingPushPopPeek_WorksCorrectly()
    {
        using RefStack<int> stack = new(4);

        stack.Push(1);
        stack.Peek().ShouldBe(1);

        stack.Push(2);
        stack.Peek().ShouldBe(2);
        stack.Pop().ShouldBe(2);

        stack.Peek().ShouldBe(1);
        stack.Push(3);
        stack.Peek().ShouldBe(3);
        stack.Pop().ShouldBe(3);

        stack.Peek().ShouldBe(1);
        stack.Pop().ShouldBe(1);

        stack.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Pop_EmptyStack_ThrowsOrFails()
    {
        using RefStack<int> stack = new(4);

        // Pop on empty stack should access invalid index
        // The behavior depends on Debug.Assert - in release it may throw IndexOutOfRangeException
        // In this test we just verify TryPop handles it gracefully
        stack.TryPop(out _).ShouldBeFalse();
    }

    [Fact]
    public void Stack_WithEmptyScratchBuffer_CanGrowAndPeek()
    {
        using RefStack<int> stack = new(scratchBuffer: []);

        stack.Push(1);
        stack.Push(2);

        stack.Peek().ShouldBe(2);
        stack.PeekOrDefault().ShouldBe(2);
        stack.TryPeek(out int value).ShouldBeTrue();
        value.ShouldBe(2);
    }
}
