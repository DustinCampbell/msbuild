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
    public void StringArray_Length_NotHandled()
        => InstanceMember(nameof(Array.Length))
            .NotHandled(new[] { "first", "second" });
}
