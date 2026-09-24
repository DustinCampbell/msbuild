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
    public void DateTime_Constructors()
    {
        Constructor.Invoke([new DateTime(2026, 9, 23).Ticks]).ShouldBe(new DateTime(2026, 9, 23));
        Constructor.Invoke([new DateTime(2026, 9, 23).Ticks, DateTimeKind.Utc])
            .ShouldBe(new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc));
        Constructor.Invoke([2026, 9, 23]).ShouldBe(new DateTime(2026, 9, 23));
        Constructor.Invoke([2026, 9, 23, 1, 2, 3]).ShouldBe(new DateTime(2026, 9, 23, 1, 2, 3));
        Constructor.Invoke([2026, 9, 23, 1, 2, 3, DateTimeKind.Utc]).ShouldBe(new DateTime(2026, 9, 23, 1, 2, 3, DateTimeKind.Utc));
        Constructor.Invoke([2026, 9, 23, 1, 2, 3, 4]).ShouldBe(new DateTime(2026, 9, 23, 1, 2, 3, 4));
        Constructor.Invoke([2026, 9, 23, 1, 2, 3, 4, DateTimeKind.Utc])
            .ShouldBe(new DateTime(2026, 9, 23, 1, 2, 3, 4, DateTimeKind.Utc));
#if NET
        Constructor.Invoke([2026, 9, 23, 1, 2, 3, 4, 5]).ShouldBe(new DateTime(2026, 9, 23, 1, 2, 3, 4, 5));
        Constructor.Invoke([2026, 9, 23, 1, 2, 3, 4, 5, DateTimeKind.Utc])
            .ShouldBe(new DateTime(2026, 9, 23, 1, 2, 3, 4, 5, DateTimeKind.Utc));
#endif
    }

    [Fact]
    public void DateTime_StaticMembers()
    {
        DateTime earlier = new(2026, 9, 23, 1, 2, 3, DateTimeKind.Utc);
        DateTime later = earlier.AddDays(1);
        long fileTime = earlier.ToFileTimeUtc();

        StaticMember(nameof(DateTime.Compare)).Invoke([earlier, later]).ShouldBe(-1);
        StaticMember(nameof(DateTime.DaysInMonth)).Invoke([2024, 2]).ShouldBe(29);
        StaticMember(nameof(DateTime.Equals)).Invoke([earlier, earlier]).ShouldBe(true);
        StaticMember(nameof(DateTime.FromBinary)).Invoke([earlier.ToBinary()]).ShouldBe(DateTime.FromBinary(earlier.ToBinary()));
        StaticMember(nameof(DateTime.FromFileTime)).Invoke([fileTime]).ShouldBe(DateTime.FromFileTime(fileTime));
        StaticMember(nameof(DateTime.FromFileTimeUtc)).Invoke([fileTime]).ShouldBe(DateTime.FromFileTimeUtc(fileTime));
        StaticMember(nameof(DateTime.FromOADate)).Invoke([earlier.ToOADate()]).ShouldBe(DateTime.FromOADate(earlier.ToOADate()));
        StaticMember(nameof(DateTime.IsLeapYear)).Invoke([2024]).ShouldBe(true);
        StaticMember(nameof(DateTime.MaxValue)).Invoke().ShouldBe(DateTime.MaxValue);
        StaticMember(nameof(DateTime.MinValue)).Invoke().ShouldBe(DateTime.MinValue);
        StaticMember(nameof(DateTime.Now)).Invoke().ShouldBeOfType<DateTime>().Kind.ShouldBe(DateTimeKind.Local);
        StaticMember(nameof(DateTime.Today)).Invoke().ShouldBe(DateTime.Today);
        StaticMember(nameof(DateTime.UtcNow)).Invoke().ShouldBeOfType<DateTime>().Kind.ShouldBe(DateTimeKind.Utc);
#if NET
        StaticMember(nameof(DateTime.UnixEpoch)).Invoke().ShouldBe(DateTime.UnixEpoch);
#endif
        StaticMember(nameof(DateTime.Parse)).Invoke(["2026-09-23"]).ShouldBe(new DateTime(2026, 9, 23));
        StaticMember(nameof(DateTime.Parse)).Invoke(["2026-09-23", CultureInfo.InvariantCulture]).ShouldBe(new DateTime(2026, 9, 23));
        StaticMember(nameof(DateTime.Parse)).Invoke(["2026-09-23", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal])
            .ShouldBe(DateTime.Parse("2026-09-23", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
        StaticMember(nameof(DateTime.ParseExact)).Invoke(["2026-09-23", "yyyy-MM-dd", CultureInfo.InvariantCulture])
            .ShouldBe(earlier.Date);
        StaticMember(nameof(DateTime.ParseExact))
            .Invoke(["2026-09-23", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal])
            .ShouldBe(DateTime.ParseExact("2026-09-23", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
        StaticMember(nameof(DateTime.ParseExact))
            .Invoke(["2026-09-23", new[] { "O", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal])
            .ShouldBe(DateTime.ParseExact(
                "2026-09-23",
                ["O", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal));
        StaticMember(nameof(DateTime.SpecifyKind)).Invoke([earlier.Date, DateTimeKind.Utc])
            .ShouldBe(DateTime.SpecifyKind(earlier.Date, DateTimeKind.Utc));
    }

    [Fact]
    public void DateTime_Operators()
    {
        DateTime earlier = new(2026, 9, 23);
        DateTime later = earlier.AddDays(1);
        TimeSpan oneDay = TimeSpan.FromDays(1);

        StaticMember("op_Addition").Invoke([earlier, oneDay]).ShouldBe(later);
        StaticMember("op_Equality").Invoke([earlier, earlier]).ShouldBe(true);
        StaticMember("op_GreaterThan").Invoke([later, earlier]).ShouldBe(true);
        StaticMember("op_GreaterThanOrEqual").Invoke([later, earlier]).ShouldBe(true);
        StaticMember("op_Inequality").Invoke([earlier, later]).ShouldBe(true);
        StaticMember("op_LessThan").Invoke([earlier, later]).ShouldBe(true);
        StaticMember("op_LessThanOrEqual").Invoke([earlier, later]).ShouldBe(true);
        StaticMember("op_Subtraction").Invoke([later, oneDay]).ShouldBe(earlier);
        StaticMember("op_Subtraction").Invoke([later, earlier]).ShouldBe(oneDay);
    }

    [Fact]
    public void DateTime_InstanceMembers()
    {
        DateTime value = new(2026, 9, 23, 1, 2, 3, 4, DateTimeKind.Utc);
        DateTime other = value.AddDays(1);
        TimeSpan oneDay = TimeSpan.FromDays(1);

        InstanceMember(nameof(DateTime.Date)).Invoke(value).ShouldBe(value.Date);
        InstanceMember(nameof(DateTime.Day)).Invoke(value).ShouldBe(value.Day);
        InstanceMember(nameof(DateTime.DayOfWeek)).Invoke(value).ShouldBe(value.DayOfWeek);
        InstanceMember(nameof(DateTime.DayOfYear)).Invoke(value).ShouldBe(value.DayOfYear);
        InstanceMember(nameof(DateTime.Hour)).Invoke(value).ShouldBe(value.Hour);
        InstanceMember(nameof(DateTime.Kind)).Invoke(value).ShouldBe(value.Kind);
        InstanceMember(nameof(DateTime.Millisecond)).Invoke(value).ShouldBe(value.Millisecond);
        InstanceMember(nameof(DateTime.Minute)).Invoke(value).ShouldBe(value.Minute);
        InstanceMember(nameof(DateTime.Month)).Invoke(value).ShouldBe(value.Month);
        InstanceMember(nameof(DateTime.Second)).Invoke(value).ShouldBe(value.Second);
        InstanceMember(nameof(DateTime.Ticks)).Invoke(value).ShouldBe(value.Ticks);
        InstanceMember(nameof(DateTime.TimeOfDay)).Invoke(value).ShouldBe(value.TimeOfDay);
        InstanceMember(nameof(DateTime.Year)).Invoke(value).ShouldBe(value.Year);
#if NET
        InstanceMember(nameof(DateTime.Microsecond)).Invoke(value).ShouldBe(value.Microsecond);
        InstanceMember(nameof(DateTime.Nanosecond)).Invoke(value).ShouldBe(value.Nanosecond);
#endif

        InstanceMember(nameof(DateTime.Add)).Invoke(value, [oneDay]).ShouldBe(value.Add(oneDay));
        InstanceMember(nameof(DateTime.AddDays)).Invoke(value, [1d]).ShouldBe(value.AddDays(1d));
        InstanceMember(nameof(DateTime.AddHours)).Invoke(value, [1d]).ShouldBe(value.AddHours(1d));
#if NET
        InstanceMember(nameof(DateTime.AddMicroseconds)).Invoke(value, [1d]).ShouldBe(value.AddMicroseconds(1d));
#endif
        InstanceMember(nameof(DateTime.AddMilliseconds)).Invoke(value, [1d]).ShouldBe(value.AddMilliseconds(1d));
        InstanceMember(nameof(DateTime.AddMinutes)).Invoke(value, [1d]).ShouldBe(value.AddMinutes(1d));
        InstanceMember(nameof(DateTime.AddMonths)).Invoke(value, [1]).ShouldBe(value.AddMonths(1));
        InstanceMember(nameof(DateTime.AddSeconds)).Invoke(value, [1d]).ShouldBe(value.AddSeconds(1d));
        InstanceMember(nameof(DateTime.AddTicks)).Invoke(value, [1L]).ShouldBe(value.AddTicks(1));
        InstanceMember(nameof(DateTime.AddYears)).Invoke(value, [1]).ShouldBe(value.AddYears(1));
        InstanceMember(nameof(DateTime.CompareTo)).Invoke(value, [other]).ShouldBe(value.CompareTo(other));
        InstanceMember(nameof(DateTime.Equals)).Invoke(value, [other]).ShouldBe(value.Equals(other));
        InstanceMember(nameof(DateTime.Equals)).Invoke(value, ["not a date"]).ShouldBe(false);
        InstanceMember(nameof(DateTime.GetDateTimeFormats)).Invoke(value).ShouldBe(value.GetDateTimeFormats());
        InstanceMember(nameof(DateTime.GetDateTimeFormats)).Invoke(value, ['O']).ShouldBe(value.GetDateTimeFormats('O'));
        InstanceMember(nameof(DateTime.GetDateTimeFormats)).Invoke(value, [CultureInfo.InvariantCulture])
            .ShouldBe(value.GetDateTimeFormats(CultureInfo.InvariantCulture));
        InstanceMember(nameof(DateTime.GetDateTimeFormats)).Invoke(value, ['O', CultureInfo.InvariantCulture])
            .ShouldBe(value.GetDateTimeFormats('O', CultureInfo.InvariantCulture));
        InstanceMember(nameof(DateTime.GetHashCode)).Invoke(value).ShouldBe(value.GetHashCode());
        InstanceMember(nameof(DateTime.GetTypeCode)).Invoke(value).ShouldBe(TypeCode.DateTime);
        InstanceMember(nameof(DateTime.IsDaylightSavingTime)).Invoke(value).ShouldBe(value.IsDaylightSavingTime());
        InstanceMember(nameof(DateTime.Subtract)).Invoke(value, [oneDay]).ShouldBe(value.Subtract(oneDay));
        InstanceMember(nameof(DateTime.Subtract)).Invoke(value, [other]).ShouldBe(value.Subtract(other));
        InstanceMember(nameof(DateTime.ToBinary)).Invoke(value).ShouldBe(value.ToBinary());
        InstanceMember(nameof(DateTime.ToFileTime)).Invoke(value).ShouldBe(value.ToFileTime());
        InstanceMember(nameof(DateTime.ToFileTimeUtc)).Invoke(value).ShouldBe(value.ToFileTimeUtc());
        InstanceMember(nameof(DateTime.ToLocalTime)).Invoke(value).ShouldBe(value.ToLocalTime());
        InstanceMember(nameof(DateTime.ToLongDateString)).Invoke(value).ShouldBe(value.ToLongDateString());
        InstanceMember(nameof(DateTime.ToLongTimeString)).Invoke(value).ShouldBe(value.ToLongTimeString());
        InstanceMember(nameof(DateTime.ToOADate)).Invoke(value).ShouldBe(value.ToOADate());
        InstanceMember(nameof(DateTime.ToShortDateString)).Invoke(value).ShouldBe(value.ToShortDateString());
        InstanceMember(nameof(DateTime.ToShortTimeString)).Invoke(value).ShouldBe(value.ToShortTimeString());
        InstanceMember(nameof(DateTime.ToString)).Invoke(value).ShouldBe(value.ToString());
        InstanceMember(nameof(DateTime.ToString)).Invoke(value, ["yyyy-MM-dd"]).ShouldBe("2026-09-23");
        InstanceMember(nameof(DateTime.ToString)).Invoke(value, [CultureInfo.InvariantCulture])
            .ShouldBe(value.ToString(CultureInfo.InvariantCulture));
        InstanceMember(nameof(DateTime.ToString)).Invoke(value, ["yyyy-MM-dd", CultureInfo.InvariantCulture]).ShouldBe("2026-09-23");
        InstanceMember(nameof(DateTime.ToUniversalTime)).Invoke(value).ShouldBe(value.ToUniversalTime());
    }
}
