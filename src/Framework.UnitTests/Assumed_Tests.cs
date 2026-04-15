// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class Assumed_Tests
{
    [Fact]
    public void Null_DoesNotThrow_WhenValueIsNull()
    {
        Assumed.Null<object>(null);
    }

    [Fact]
    public void Null_Throws_WhenValueIsNotNull()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Null("not null"));
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Null_Throws_WithCustomMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Null("not null", "custom message"));
        ex.Message.ShouldContain("custom message");
    }

    [Fact]
    public void Null_Throws_WithInterpolatedMessage()
    {
        int id = 42;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Null("not null", $"value was {id}"));
        ex.Message.ShouldContain("value was 42");
    }

    [Fact]
    public void Null_DoesNotFormat_WhenAssertionHolds()
    {
        // This should not throw. The interpolated string handler should skip formatting.
        Assumed.Null<object>(null, $"should not be formatted {ThrowIfFormatted()}");
    }

    [Fact]
    public void NotNull_DoesNotThrow_WhenValueIsNotNull()
    {
        Assumed.NotNull("not null");
    }

    [Fact]
    public void NotNull_Throws_WhenValueIsNull()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull<object>(null));
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void NotNull_Throws_WithCustomMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull<object>(null, "custom message"));
        ex.Message.ShouldContain("custom message");
    }

    [Fact]
    public void NotNull_Throws_WithInterpolatedMessage()
    {
        int id = 42;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull<object>(null, $"value was {id}"));
        ex.Message.ShouldContain("value was 42");
    }

    [Fact]
    public void NotNull_DoesNotFormat_WhenAssertionHolds()
    {
        Assumed.NotNull("not null", $"should not be formatted {ThrowIfFormatted()}");
    }

    [Fact]
    public void True_DoesNotThrow_WhenConditionIsTrue()
    {
        Assumed.True(true);
    }

    [Fact]
    public void True_Throws_WhenConditionIsFalse()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false));
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void True_Throws_WithCustomMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false, "custom message"));
        ex.Message.ShouldContain("custom message");
    }

    [Fact]
    public void True_Throws_WithInterpolatedMessage()
    {
        int id = 42;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false, $"value was {id}"));
        ex.Message.ShouldContain("value was 42");
    }

    [Fact]
    public void True_DoesNotFormat_WhenAssertionHolds()
    {
        Assumed.True(true, $"should not be formatted {ThrowIfFormatted()}");
    }

    [Fact]
    public void False_DoesNotThrow_WhenConditionIsFalse()
    {
        Assumed.False(false);
    }

    [Fact]
    public void False_Throws_WhenConditionIsTrue()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.False(true));
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void False_Throws_WithCustomMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.False(true, "custom message"));
        ex.Message.ShouldContain("custom message");
    }

    [Fact]
    public void False_Throws_WithInterpolatedMessage()
    {
        int id = 42;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.False(true, $"value was {id}"));
        ex.Message.ShouldContain("value was 42");
    }

    [Fact]
    public void False_DoesNotFormat_WhenAssertionHolds()
    {
        Assumed.False(false, $"should not be formatted {ThrowIfFormatted()}");
    }

    [Fact]
    public void Unreachable_AlwaysThrows()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable());
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Unreachable_Throws_WithCustomMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable("custom message"));
        ex.Message.ShouldContain("custom message");
    }

    [Fact]
    public void Unreachable_Throws_WithInterpolatedMessage()
    {
        int id = 42;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable($"value was {id}"));
        ex.Message.ShouldContain("value was 42");
    }

    [Fact]
    public void UnreachableT_AlwaysThrows()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable<int>());
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void UnreachableT_Throws_WithCustomMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable<int>("custom message"));
        ex.Message.ShouldContain("custom message");
    }

    [Fact]
    public void UnreachableT_Throws_WithInterpolatedMessage()
    {
        int id = 42;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable<int>($"value was {id}"));
        ex.Message.ShouldContain("value was 42");
    }

    [Fact]
    public void ExceptionMessage_ContainsFileAndLine()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false, "test"));
        ex.Message.ShouldContain("Assumed_Tests.cs");
    }

    /// <summary>
    ///  Helper that throws if it is ever called. Used to verify that interpolated string
    ///  handlers skip formatting when the assertion holds.
    /// </summary>
    private static string ThrowIfFormatted()
        => throw new InvalidOperationException("Interpolated string should not have been formatted.");
}
