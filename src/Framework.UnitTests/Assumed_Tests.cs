// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class Assumed_Tests
{
    [Fact]
    public void NotNull_ReferenceType_DoesNotThrow_WhenNotNull()
    {
        string value = "hello";
        Should.NotThrow(() => Assumed.NotNull(value));
    }

    [Fact]
    public void NotNull_ReferenceType_Throws_WhenNull()
    {
        string? value = null;
        Should.Throw<InternalErrorException>(() => Assumed.NotNull(value));
    }

    [Fact]
    public void NotNull_ReferenceType_ThrowsWithCustomMessage_WhenNull()
    {
        string? value = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull(value, "custom message"));
        ex.Message.ShouldContain("custom message");
    }

    [Fact]
    public void NotNull_ReferenceType_ThrowsWithDefaultMessage_WhenNull()
    {
        string? value = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull(value));
        // The default message should contain the expression text for the value parameter.
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void NotNull_ReferenceType_InterpolatedString_DoesNotThrow_WhenNotNull()
    {
        string value = "hello";
        Should.NotThrow(() => Assumed.NotNull(value, $"unexpected null"));
    }

    [Fact]
    public void NotNull_ReferenceType_InterpolatedString_Throws_WhenNull()
    {
        string? value = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull(value, $"value was null"));
        ex.Message.ShouldContain("value was null");
    }

    [Fact]
    public void NotNull_ValueType_DoesNotThrow_WhenNotNull()
    {
        int? value = 42;
        Should.NotThrow(() => Assumed.NotNull(value));
    }

    [Fact]
    public void NotNull_ValueType_Throws_WhenNull()
    {
        int? value = null;
        Should.Throw<InternalErrorException>(() => Assumed.NotNull(value));
    }

    [Fact]
    public void NotNull_ValueType_ThrowsWithCustomMessage_WhenNull()
    {
        int? value = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull(value, "expected a value"));
        ex.Message.ShouldContain("expected a value");
    }

    [Fact]
    public void NotNull_ValueType_InterpolatedString_DoesNotThrow_WhenNotNull()
    {
        int? value = 42;
        Should.NotThrow(() => Assumed.NotNull(value, $"unexpected null"));
    }

    [Fact]
    public void NotNull_ValueType_InterpolatedString_Throws_WhenNull()
    {
        int? value = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull(value, $"int was null"));
        ex.Message.ShouldContain("int was null");
    }

    [Fact]
    public void NotNullOrEmpty_DoesNotThrow_WhenNonEmpty()
    {
        string value = "hello";
        Should.NotThrow(() => Assumed.NotNullOrEmpty(value));
    }

    [Fact]
    public void NotNullOrEmpty_Throws_WhenNull()
    {
        string? value = null;
        Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(value));
    }

    [Fact]
    public void NotNullOrEmpty_Throws_WhenEmpty()
    {
        string value = "";
        Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(value));
    }

    [Fact]
    public void NotNullOrEmpty_ThrowsWithCustomMessage_WhenNull()
    {
        string? value = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(value, "bad null"));
        ex.Message.ShouldContain("bad null");
    }

    [Fact]
    public void NotNullOrEmpty_ThrowsWithCustomMessage_WhenEmpty()
    {
        string value = "";
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(value, "bad empty"));
        ex.Message.ShouldContain("bad empty");
    }

    [Fact]
    public void NotNullOrEmpty_InterpolatedString_DoesNotThrow_WhenNonEmpty()
    {
        string value = "hello";
        Should.NotThrow(() => Assumed.NotNullOrEmpty(value, $"unexpected"));
    }

    [Fact]
    public void NotNullOrEmpty_InterpolatedString_Throws_WhenNull()
    {
        string? value = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(value, $"was null"));
        ex.Message.ShouldContain("was null");
    }

    [Fact]
    public void NotNullOrEmpty_InterpolatedString_Throws_WhenEmpty()
    {
        string value = "";
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNullOrEmpty(value, $"was empty"));
        ex.Message.ShouldContain("was empty");
    }

    [Fact]
    public void False_DoesNotThrow_WhenFalse()
    {
        Should.NotThrow(() => Assumed.False(false));
    }

    [Fact]
    public void False_Throws_WhenTrue()
    {
        Should.Throw<InternalErrorException>(() => Assumed.False(true));
    }

    [Fact]
    public void False_ThrowsWithCustomMessage_WhenTrue()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.False(true, "condition was true"));
        ex.Message.ShouldContain("condition was true");
    }

    [Fact]
    public void False_ThrowsWithDefaultMessage_WhenTrue()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.False(true));
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void False_InterpolatedString_DoesNotThrow_WhenFalse()
    {
        Should.NotThrow(() => Assumed.False(false, $"should not format"));
    }

    [Fact]
    public void False_InterpolatedString_Throws_WhenTrue()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.False(true, $"condition was true"));
        ex.Message.ShouldContain("condition was true");
    }

    [Fact]
    public void True_DoesNotThrow_WhenTrue()
    {
        Should.NotThrow(() => Assumed.True(true));
    }

    [Fact]
    public void True_Throws_WhenFalse()
    {
        Should.Throw<InternalErrorException>(() => Assumed.True(false));
    }

    [Fact]
    public void True_ThrowsWithCustomMessage_WhenFalse()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false, "condition was false"));
        ex.Message.ShouldContain("condition was false");
    }

    [Fact]
    public void True_ThrowsWithDefaultMessage_WhenFalse()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false));
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void True_InterpolatedString_DoesNotThrow_WhenTrue()
    {
        Should.NotThrow(() => Assumed.True(true, $"should not format"));
    }

    [Fact]
    public void True_InterpolatedString_Throws_WhenFalse()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false, $"condition was false"));
        ex.Message.ShouldContain("condition was false");
    }

    [Fact]
    public void Unreachable_AlwaysThrows()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Unreachable());
    }

    [Fact]
    public void Unreachable_ThrowsWithCustomMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable("should not get here"));
        ex.Message.ShouldContain("should not get here");
    }

    [Fact]
    public void Unreachable_ThrowsWithDefaultMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable());
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Unreachable_InterpolatedString_AlwaysThrows()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable($"unreachable hit"));
        ex.Message.ShouldContain("unreachable hit");
    }

    [Fact]
    public void Unreachable_Generic_AlwaysThrows()
    {
        Should.Throw<InternalErrorException>(() => Assumed.Unreachable<int>());
    }

    [Fact]
    public void Unreachable_Generic_ThrowsWithCustomMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable<string>("not reachable"));
        ex.Message.ShouldContain("not reachable");
    }

    [Fact]
    public void Unreachable_Generic_ThrowsWithDefaultMessage()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable<int>());
        ex.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Unreachable_Generic_InterpolatedString_AlwaysThrows()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable<int>($"generic unreachable"));
        ex.Message.ShouldContain("generic unreachable");
    }

    [Fact]
    public void ExceptionMessage_ContainsFileInfo()
    {
        string? value = null;
        var ex = Should.Throw<InternalErrorException>(() => Assumed.NotNull(value));

        // ThrowInternalError appends file and line info from CallerFilePath/CallerLineNumber.
        ex.Message.ShouldContain("Assumed_Tests.cs");
    }

    [Fact]
    public void True_ExceptionMessage_ContainsFileInfo()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.True(false));
        ex.Message.ShouldContain("Assumed_Tests.cs");
    }

    [Fact]
    public void False_ExceptionMessage_ContainsFileInfo()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.False(true));
        ex.Message.ShouldContain("Assumed_Tests.cs");
    }

    [Fact]
    public void Unreachable_ExceptionMessage_ContainsFileInfo()
    {
        var ex = Should.Throw<InternalErrorException>(() => Assumed.Unreachable());
        ex.Message.ShouldContain("Assumed_Tests.cs");
    }

    [Fact]
    public void False_InterpolatedString_DoesNotFormatMessage_WhenConditionIsFalse()
    {
        int formatCount = 0;
        // When condition is false (happy path), the interpolated string handler should
        // short-circuit and never evaluate the interpolation holes.
        Assumed.False(false, $"value is {Increment(ref formatCount)}");
        formatCount.ShouldBe(0);
    }

    [Fact]
    public void True_InterpolatedString_DoesNotFormatMessage_WhenConditionIsTrue()
    {
        int formatCount = 0;
        Assumed.True(true, $"value is {Increment(ref formatCount)}");
        formatCount.ShouldBe(0);
    }

    [Fact]
    public void NotNull_ReferenceType_InterpolatedString_DoesNotFormatMessage_WhenNotNull()
    {
        int formatCount = 0;
        string value = "hello";
        Assumed.NotNull(value, $"value is {Increment(ref formatCount)}");
        formatCount.ShouldBe(0);
    }

    [Fact]
    public void NotNull_ValueType_InterpolatedString_DoesNotFormatMessage_WhenNotNull()
    {
        int formatCount = 0;
        int? value = 42;
        Assumed.NotNull(value, $"value is {Increment(ref formatCount)}");
        formatCount.ShouldBe(0);
    }

    [Fact]
    public void NotNullOrEmpty_InterpolatedString_DoesNotFormatMessage_WhenNonEmpty()
    {
        int formatCount = 0;
        string value = "hello";
        Assumed.NotNullOrEmpty(value, $"value is {Increment(ref formatCount)}");
        formatCount.ShouldBe(0);
    }

    private static string Increment(ref int counter)
    {
        counter++;
        return counter.ToString();
    }
}
