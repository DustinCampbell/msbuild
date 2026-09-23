// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Math_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Math), output)
{
    [Fact]
    public void Math_Max_Double()
        => StaticMember(nameof(Math.Max))
            .Invoke([1.5d, 2.5d])
            .ShouldBe(2.5d);

    [Theory]
    [InlineData(1L, 2L, 2d)]
    [InlineData("1.5", "2.5", 2.5d)]
    public void Math_Max_ConvertibleToDouble(object left, object right, double expected)
        => StaticMember(nameof(Math.Max))
            .Invoke([left, right])
            .ShouldBe(expected);

    [Fact]
    public void Math_Max_Decimal_NotHandled()
        => StaticMember(nameof(Math.Max))
            .NotHandled([1m, 2m]);

    [Fact]
    public void Math_Max_InvalidArgument_NotHandled()
        => StaticMember(nameof(Math.Max))
            .NotHandled(["invalid", 2d]);

    [Fact]
    public void Math_Max_OneArgument_NotHandled()
        => StaticMember(nameof(Math.Max))
            .NotHandled([1d]);

    [Fact]
    public void Math_Min_Double()
        => StaticMember(nameof(Math.Min))
            .Invoke([1.5d, 2.5d])
            .ShouldBe(1.5d);

    [Fact]
    public void Math_Min_InvalidArgument_NotHandled()
        => StaticMember(nameof(Math.Min))
            .NotHandled([1d, "invalid"]);

    [Fact]
    public void Math_Abs_NotHandled()
        => StaticMember(nameof(Math.Abs))
            .NotHandled([-1]);

    [Fact]
    public void Math_Round_NotHandled()
        => StaticMember(nameof(Math.Round))
            .NotHandled([1.5d]);
}
