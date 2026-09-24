// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Enum_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Enum), output)
{
    [Fact]
    public void Enum_InstanceMembers()
    {
        InstanceMember(nameof(Enum.CompareTo)).Invoke(DayOfWeek.Monday, [DayOfWeek.Tuesday]).ShouldBe(-1);
        InstanceMember(nameof(Enum.Equals)).Invoke(DayOfWeek.Monday, [DayOfWeek.Monday]).ShouldBe(true);
        InstanceMember(nameof(Enum.GetHashCode)).Invoke(DayOfWeek.Monday).ShouldBe(DayOfWeek.Monday.GetHashCode());
        InstanceMember(nameof(Enum.GetTypeCode)).Invoke(DayOfWeek.Monday).ShouldBe(TypeCode.Int32);
        InstanceMember(nameof(Enum.HasFlag)).Invoke(DayOfWeek.Monday, [DayOfWeek.Monday]).ShouldBe(true);
        InstanceMember(nameof(Enum.ToString)).Invoke(DayOfWeek.Monday).ShouldBe(nameof(DayOfWeek.Monday));
        InstanceMember(nameof(Enum.ToString)).Invoke(DayOfWeek.Monday, ["D"]).ShouldBe("1");
        InstanceMember(nameof(Enum.ToString)).Invoke(DayOfWeek.Monday, [CultureInfo.InvariantCulture]).ShouldBe(nameof(DayOfWeek.Monday));
        InstanceMember(nameof(Enum.ToString))
            .Invoke(DayOfWeek.Monday, ["D", CultureInfo.InvariantCulture])
            .ShouldBe("1");
    }
}
