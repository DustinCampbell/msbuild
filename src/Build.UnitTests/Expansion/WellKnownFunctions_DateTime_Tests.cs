// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_DateTime_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(DateTime), output)
{
    [Fact]
    public void DateTime_StaticMembers()
    {
        StaticMember(nameof(DateTime.MinValue)).Invoke().ShouldBe(DateTime.MinValue);
        StaticMember(nameof(DateTime.MaxValue)).Invoke().ShouldBe(DateTime.MaxValue);
        StaticMember(nameof(DateTime.Now)).Invoke().ShouldBeOfType<DateTime>().Kind.ShouldBe(DateTimeKind.Local);
        StaticMember(nameof(DateTime.Today)).Invoke().ShouldBe(DateTime.Today);
        StaticMember(nameof(DateTime.UtcNow)).Invoke().ShouldBeOfType<DateTime>().Kind.ShouldBe(DateTimeKind.Utc);
#if NET
        StaticMember(nameof(DateTime.UnixEpoch)).Invoke().ShouldBe(DateTime.UnixEpoch);
#endif
        StaticMember(nameof(DateTime.Parse)).Invoke(["2026-09-23"]).ShouldBe(new DateTime(2026, 9, 23));
        StaticMember(nameof(DateTime.Parse)).Invoke(["2026-09-23", CultureInfo.InvariantCulture]).ShouldBe(new DateTime(2026, 9, 23));
        StaticMember(nameof(DateTime.Parse))
            .Invoke(["2026-09-23", CultureInfo.InvariantCulture, "DateTimeStyles.AssumeUniversal"])
            .ShouldBeOfType<DateTime>();
    }

    [Fact]
    public void DateTime_InstanceMembers()
    {
        DateTime value = new(2026, 9, 23);

        InstanceMember(nameof(DateTime.DayOfWeek)).Invoke(value).ShouldBe(value.DayOfWeek);
        InstanceMember(nameof(DateTime.GetHashCode)).Invoke(value).ShouldBe(value.GetHashCode());
        InstanceMember(nameof(DateTime.GetTypeCode)).Invoke(value).ShouldBe(TypeCode.DateTime);
        InstanceMember(nameof(DateTime.ToString)).Invoke(value).ShouldBe(value.ToString());
        InstanceMember(nameof(DateTime.ToString)).Invoke(value, ["yyyy-MM-dd"]).ShouldBe("2026-09-23");
        InstanceMember(nameof(DateTime.ToString)).Invoke(value, [CultureInfo.InvariantCulture]).ShouldBe(value.ToString(CultureInfo.InvariantCulture));
        InstanceMember(nameof(DateTime.ToString)).Invoke(value, ["yyyy-MM-dd", CultureInfo.InvariantCulture]).ShouldBe("2026-09-23");
    }
}
