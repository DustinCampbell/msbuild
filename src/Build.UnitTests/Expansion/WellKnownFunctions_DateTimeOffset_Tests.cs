// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_DateTimeOffset_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(DateTimeOffset), output)
{
    [Fact]
    public void DateTimeOffset_Constructors()
    {
        DateTime dateTime = new(2026, 9, 23, 1, 2, 3, DateTimeKind.Unspecified);
        TimeSpan offset = TimeSpan.FromHours(2);

        Constructor.Invoke([dateTime]).ShouldBe(new DateTimeOffset(dateTime));
        Constructor.Invoke([dateTime, offset]).ShouldBe(new DateTimeOffset(dateTime, offset));
        Constructor.Invoke([dateTime.Ticks, offset]).ShouldBe(new DateTimeOffset(dateTime.Ticks, offset));
        Constructor.Invoke([2026, 9, 23, 1, 2, 3, offset]).ShouldBe(new DateTimeOffset(2026, 9, 23, 1, 2, 3, offset));
        Constructor.Invoke([2026, 9, 23, 1, 2, 3, 4, offset]).ShouldBe(new DateTimeOffset(2026, 9, 23, 1, 2, 3, 4, offset));
#if NET
        Constructor.Invoke([2026, 9, 23, 1, 2, 3, 4, 5, offset])
            .ShouldBe(new DateTimeOffset(2026, 9, 23, 1, 2, 3, 4, 5, offset));
#endif
    }

    [Fact]
    public void DateTimeOffset_StaticMembers()
    {
        DateTimeOffset earlier = new(2026, 9, 23, 1, 2, 3, TimeSpan.Zero);
        DateTimeOffset later = earlier.AddDays(1);

        StaticMember(nameof(DateTimeOffset.Compare)).Invoke([earlier, later]).ShouldBe(-1);
        StaticMember(nameof(DateTimeOffset.Equals)).Invoke([earlier, earlier]).ShouldBe(true);
        StaticMember(nameof(DateTimeOffset.FromFileTime)).Invoke([earlier.ToFileTime()])
            .ShouldBe(DateTimeOffset.FromFileTime(earlier.ToFileTime()));
        StaticMember(nameof(DateTimeOffset.FromUnixTimeMilliseconds)).Invoke([1L])
            .ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1));
        StaticMember(nameof(DateTimeOffset.FromUnixTimeSeconds)).Invoke([1L]).ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1));
        StaticMember(nameof(DateTimeOffset.MaxValue)).Invoke().ShouldBe(DateTimeOffset.MaxValue);
        StaticMember(nameof(DateTimeOffset.MinValue)).Invoke().ShouldBe(DateTimeOffset.MinValue);
        StaticMember(nameof(DateTimeOffset.Now)).Invoke().ShouldBeOfType<DateTimeOffset>();
        StaticMember(nameof(DateTimeOffset.UtcNow)).Invoke().ShouldBeOfType<DateTimeOffset>().Offset.ShouldBe(TimeSpan.Zero);
#if NET
        StaticMember(nameof(DateTimeOffset.UnixEpoch)).Invoke().ShouldBe(DateTimeOffset.UnixEpoch);
#endif
        StaticMember(nameof(DateTimeOffset.Parse)).Invoke(["2026-09-23 +02:00"])
            .ShouldBe(DateTimeOffset.Parse("2026-09-23 +02:00"));
        StaticMember(nameof(DateTimeOffset.Parse)).Invoke(["2026-09-23 +02:00", CultureInfo.InvariantCulture])
            .ShouldBe(DateTimeOffset.Parse("2026-09-23 +02:00", CultureInfo.InvariantCulture));
        StaticMember(nameof(DateTimeOffset.Parse))
            .Invoke(["2026-09-23 +02:00", CultureInfo.InvariantCulture, DateTimeStyles.None])
            .ShouldBe(DateTimeOffset.Parse("2026-09-23 +02:00", CultureInfo.InvariantCulture, DateTimeStyles.None));
        StaticMember(nameof(DateTimeOffset.ParseExact))
            .Invoke(["2026-09-23 +02:00", "yyyy-MM-dd zzz", CultureInfo.InvariantCulture])
            .ShouldBe(DateTimeOffset.ParseExact("2026-09-23 +02:00", "yyyy-MM-dd zzz", CultureInfo.InvariantCulture));
        StaticMember(nameof(DateTimeOffset.ParseExact))
            .Invoke(["2026-09-23 +02:00", "yyyy-MM-dd zzz", CultureInfo.InvariantCulture, DateTimeStyles.None])
            .ShouldBe(DateTimeOffset.ParseExact(
                "2026-09-23 +02:00",
                "yyyy-MM-dd zzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None));
        StaticMember(nameof(DateTimeOffset.ParseExact))
            .Invoke(["2026-09-23 +02:00", new[] { "O", "yyyy-MM-dd zzz" }, CultureInfo.InvariantCulture, DateTimeStyles.None])
            .ShouldBe(DateTimeOffset.ParseExact(
                "2026-09-23 +02:00",
                ["O", "yyyy-MM-dd zzz"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None));
    }

    [Fact]
    public void DateTimeOffset_Operators()
    {
        DateTime dateTime = new(2026, 9, 23, 1, 2, 3, DateTimeKind.Utc);
        DateTimeOffset earlier = new(dateTime);
        DateTimeOffset later = earlier.AddDays(1);
        TimeSpan oneDay = TimeSpan.FromDays(1);

        StaticMember("op_Addition").Invoke([earlier, oneDay]).ShouldBe(later);
        StaticMember("op_Equality").Invoke([earlier, earlier]).ShouldBe(true);
        StaticMember("op_GreaterThan").Invoke([later, earlier]).ShouldBe(true);
        StaticMember("op_GreaterThanOrEqual").Invoke([later, earlier]).ShouldBe(true);
        StaticMember("op_Implicit").Invoke([dateTime]).ShouldBe((DateTimeOffset)dateTime);
        StaticMember("op_Inequality").Invoke([earlier, later]).ShouldBe(true);
        StaticMember("op_LessThan").Invoke([earlier, later]).ShouldBe(true);
        StaticMember("op_LessThanOrEqual").Invoke([earlier, later]).ShouldBe(true);
        StaticMember("op_Subtraction").Invoke([later, oneDay]).ShouldBe(earlier);
        StaticMember("op_Subtraction").Invoke([later, earlier]).ShouldBe(oneDay);
    }

    [Fact]
    public void DateTimeOffset_InstanceMembers()
    {
        DateTimeOffset value = new(2026, 9, 23, 1, 2, 3, 4, TimeSpan.FromHours(2));
        DateTimeOffset other = value.AddDays(1);
        DateTimeOffset sameInstant = value.ToOffset(TimeSpan.FromHours(1));
        TimeSpan oneDay = TimeSpan.FromDays(1);

        InstanceMember(nameof(DateTimeOffset.Date)).Invoke(value).ShouldBe(value.Date);
        InstanceMember(nameof(DateTimeOffset.DateTime)).Invoke(value).ShouldBe(value.DateTime);
        InstanceMember(nameof(DateTimeOffset.Day)).Invoke(value).ShouldBe(value.Day);
        InstanceMember(nameof(DateTimeOffset.DayOfWeek)).Invoke(value).ShouldBe(value.DayOfWeek);
        InstanceMember(nameof(DateTimeOffset.DayOfYear)).Invoke(value).ShouldBe(value.DayOfYear);
        InstanceMember(nameof(DateTimeOffset.Hour)).Invoke(value).ShouldBe(value.Hour);
        InstanceMember(nameof(DateTimeOffset.LocalDateTime)).Invoke(value).ShouldBe(value.LocalDateTime);
        InstanceMember(nameof(DateTimeOffset.Millisecond)).Invoke(value).ShouldBe(value.Millisecond);
        InstanceMember(nameof(DateTimeOffset.Minute)).Invoke(value).ShouldBe(value.Minute);
        InstanceMember(nameof(DateTimeOffset.Month)).Invoke(value).ShouldBe(value.Month);
        InstanceMember(nameof(DateTimeOffset.Offset)).Invoke(value).ShouldBe(value.Offset);
        InstanceMember(nameof(DateTimeOffset.Second)).Invoke(value).ShouldBe(value.Second);
        InstanceMember(nameof(DateTimeOffset.Ticks)).Invoke(value).ShouldBe(value.Ticks);
        InstanceMember(nameof(DateTimeOffset.TimeOfDay)).Invoke(value).ShouldBe(value.TimeOfDay);
        InstanceMember(nameof(DateTimeOffset.UtcDateTime)).Invoke(value).ShouldBe(value.UtcDateTime);
        InstanceMember(nameof(DateTimeOffset.Year)).Invoke(value).ShouldBe(value.Year);
#if NET
        InstanceMember(nameof(DateTimeOffset.Microsecond)).Invoke(value).ShouldBe(value.Microsecond);
        InstanceMember(nameof(DateTimeOffset.Nanosecond)).Invoke(value).ShouldBe(value.Nanosecond);
        InstanceMember(nameof(DateTimeOffset.TotalOffsetMinutes)).Invoke(value).ShouldBe(value.TotalOffsetMinutes);
        InstanceMember(nameof(DateTimeOffset.UtcTicks)).Invoke(value).ShouldBe(value.UtcTicks);
#endif

        InstanceMember(nameof(DateTimeOffset.Add)).Invoke(value, [oneDay]).ShouldBe(value.Add(oneDay));
        InstanceMember(nameof(DateTimeOffset.AddDays)).Invoke(value, [1d]).ShouldBe(value.AddDays(1d));
        InstanceMember(nameof(DateTimeOffset.AddHours)).Invoke(value, [1d]).ShouldBe(value.AddHours(1d));
#if NET
        InstanceMember(nameof(DateTimeOffset.AddMicroseconds)).Invoke(value, [1d]).ShouldBe(value.AddMicroseconds(1d));
#endif
        InstanceMember(nameof(DateTimeOffset.AddMilliseconds)).Invoke(value, [1d]).ShouldBe(value.AddMilliseconds(1d));
        InstanceMember(nameof(DateTimeOffset.AddMinutes)).Invoke(value, [1d]).ShouldBe(value.AddMinutes(1d));
        InstanceMember(nameof(DateTimeOffset.AddMonths)).Invoke(value, [1]).ShouldBe(value.AddMonths(1));
        InstanceMember(nameof(DateTimeOffset.AddSeconds)).Invoke(value, [1d]).ShouldBe(value.AddSeconds(1d));
        InstanceMember(nameof(DateTimeOffset.AddTicks)).Invoke(value, [1L]).ShouldBe(value.AddTicks(1));
        InstanceMember(nameof(DateTimeOffset.AddYears)).Invoke(value, [1]).ShouldBe(value.AddYears(1));
        InstanceMember(nameof(DateTimeOffset.CompareTo)).Invoke(value, [other]).ShouldBe(value.CompareTo(other));
        InstanceMember(nameof(DateTimeOffset.Equals)).Invoke(value, [other]).ShouldBe(value.Equals(other));
        InstanceMember(nameof(DateTimeOffset.Equals)).Invoke(value, ["not a date"]).ShouldBe(false);
        InstanceMember(nameof(DateTimeOffset.EqualsExact)).Invoke(value, [sameInstant]).ShouldBe(false);
        InstanceMember(nameof(DateTimeOffset.GetHashCode)).Invoke(value).ShouldBe(value.GetHashCode());
        InstanceMember(nameof(DateTimeOffset.Subtract)).Invoke(value, [oneDay]).ShouldBe(value.Subtract(oneDay));
        InstanceMember(nameof(DateTimeOffset.Subtract)).Invoke(value, [other]).ShouldBe(value.Subtract(other));
        InstanceMember(nameof(DateTimeOffset.ToFileTime)).Invoke(value).ShouldBe(value.ToFileTime());
        InstanceMember(nameof(DateTimeOffset.ToLocalTime)).Invoke(value).ShouldBe(value.ToLocalTime());
        InstanceMember(nameof(DateTimeOffset.ToOffset)).Invoke(value, [TimeSpan.FromHours(3)])
            .ShouldBe(value.ToOffset(TimeSpan.FromHours(3)));
        InstanceMember(nameof(DateTimeOffset.ToString)).Invoke(value).ShouldBe(value.ToString());
        InstanceMember(nameof(DateTimeOffset.ToString)).Invoke(value, ["O"]).ShouldBe(value.ToString("O"));
        InstanceMember(nameof(DateTimeOffset.ToString)).Invoke(value, [CultureInfo.InvariantCulture])
            .ShouldBe(value.ToString(CultureInfo.InvariantCulture));
        InstanceMember(nameof(DateTimeOffset.ToString)).Invoke(value, ["O", CultureInfo.InvariantCulture])
            .ShouldBe(value.ToString("O", CultureInfo.InvariantCulture));
        InstanceMember(nameof(DateTimeOffset.ToUniversalTime)).Invoke(value).ShouldBe(value.ToUniversalTime());
        InstanceMember(nameof(DateTimeOffset.ToUnixTimeMilliseconds)).Invoke(value).ShouldBe(value.ToUnixTimeMilliseconds());
        InstanceMember(nameof(DateTimeOffset.ToUnixTimeSeconds)).Invoke(value).ShouldBe(value.ToUnixTimeSeconds());
    }
}
