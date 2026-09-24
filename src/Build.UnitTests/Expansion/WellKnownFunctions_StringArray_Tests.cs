// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_StringArray_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(string[]), output)
{
    [Fact]
    public void StringArray_GetValue_Int32()
        => InstanceMember(nameof(Array.GetValue))
            .Invoke(new[] { "first", "second" }, [1])
            .ShouldBe("second");

    [Fact]
    public void StringArray_GetValue_Int64()
        => InstanceMember(nameof(Array.GetValue))
            .Invoke(new[] { "first", "second" }, [1L])
            .ShouldBe("second");

    [Fact]
    public void StringArray_GetValue_String()
        => InstanceMember(nameof(Array.GetValue))
            .Invoke(new[] { "first", "second" }, ["1"])
            .ShouldBe("second");

    [Fact]
    public void StringArray_GetValue_InvalidIndex_NotHandled()
        => InstanceMember(nameof(Array.GetValue))
            .NotHandled(new[] { "first", "second" }, ["invalid"]);

    [Fact]
    public void StringArray_GetValue_NoArguments_NotHandled()
        => InstanceMember(nameof(Array.GetValue))
            .NotHandled(new[] { "first", "second" });

    [Fact]
    public void StringArray_GetLength_NotHandled()
        => InstanceMember(nameof(Array.GetLength))
            .NotHandled(new[] { "first", "second" }, [0]);

    [Fact]
    public void Array_Properties()
    {
        int[] array = [1, 2];

        InstanceMember(nameof(Array.Length)).Invoke(array).ShouldBe(2);
        InstanceMember(nameof(Array.LongLength)).Invoke(array).ShouldBe(2L);
        InstanceMember(nameof(Array.Rank)).Invoke(array).ShouldBe(1);
        InstanceMember(nameof(Array.GetValue)).Invoke(array, ["1"]).ShouldBe(2);
    }
}
