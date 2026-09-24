// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_TimeSpan_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(TimeSpan), output)
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("01:00:00", "01:00:00", true)]
    [InlineData("01:00:00", "02:00:00", false)]
    public void TimeSpan_Equals(object? left, object? right, bool expected)
        => StaticMember(nameof(TimeSpan.Equals)).Invoke([left, right]).ShouldBe(expected);
}
