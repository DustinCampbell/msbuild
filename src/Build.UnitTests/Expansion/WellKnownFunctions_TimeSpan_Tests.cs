// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_TimeSpan_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(TimeSpan), output)
{
    [Fact]
    public void TimeSpan_Constructors()
    {
        Constructor.Invoke([TimeSpan.TicksPerHour]).ShouldBe(TimeSpan.FromHours(1));
        Constructor.Invoke([1, 2, 3]).ShouldBe(new TimeSpan(1, 2, 3));
        Constructor.Invoke([1, 2, 3, 4]).ShouldBe(new TimeSpan(1, 2, 3, 4));
        Constructor.Invoke([1, 2, 3, 4, 5]).ShouldBe(new TimeSpan(1, 2, 3, 4, 5));
#if NET
        Constructor.Invoke([1, 2, 3, 4, 5, 6]).ShouldBe(new TimeSpan(1, 2, 3, 4, 5, 6));
#endif
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("01:00:00", "01:00:00", true)]
    [InlineData("01:00:00", "02:00:00", false)]
    public void TimeSpan_Equals(object? left, object? right, bool expected)
        => StaticMember(nameof(TimeSpan.Equals)).Invoke([left, right]).ShouldBe(expected);

    [Fact]
    public void TimeSpan_StaticMembers()
    {
        TimeSpan oneHour = TimeSpan.FromHours(1);
        TimeSpan twoHours = TimeSpan.FromHours(2);

        StaticMember(nameof(TimeSpan.Compare)).Invoke([oneHour, twoHours]).ShouldBe(-1);
        StaticMember(nameof(TimeSpan.FromDays)).Invoke([1d]).ShouldBe(TimeSpan.FromDays(1d));
        StaticMember(nameof(TimeSpan.FromHours)).Invoke([1d]).ShouldBe(oneHour);
        StaticMember(nameof(TimeSpan.FromMilliseconds)).Invoke([1d]).ShouldBe(TimeSpan.FromMilliseconds(1d));
        StaticMember(nameof(TimeSpan.FromMinutes)).Invoke([1d]).ShouldBe(TimeSpan.FromMinutes(1d));
        StaticMember(nameof(TimeSpan.FromSeconds)).Invoke([1d]).ShouldBe(TimeSpan.FromSeconds(1d));
        StaticMember(nameof(TimeSpan.FromTicks)).Invoke([1L]).ShouldBe(TimeSpan.FromTicks(1));
        StaticMember(nameof(TimeSpan.MaxValue)).Invoke().ShouldBe(TimeSpan.MaxValue);
        StaticMember(nameof(TimeSpan.MinValue)).Invoke().ShouldBe(TimeSpan.MinValue);
        StaticMember(nameof(TimeSpan.Zero)).Invoke().ShouldBe(TimeSpan.Zero);
        StaticMember(nameof(TimeSpan.Parse)).Invoke(["01:02:03"]).ShouldBe(TimeSpan.Parse("01:02:03"));
        StaticMember(nameof(TimeSpan.Parse)).Invoke(["01:02:03", CultureInfo.InvariantCulture])
            .ShouldBe(TimeSpan.Parse("01:02:03", CultureInfo.InvariantCulture));
        StaticMember(nameof(TimeSpan.ParseExact)).Invoke(["01:02:03", "c", CultureInfo.InvariantCulture])
            .ShouldBe(TimeSpan.ParseExact("01:02:03", "c", CultureInfo.InvariantCulture));
        StaticMember(nameof(TimeSpan.ParseExact)).Invoke(["01:02:03", new[] { "c", "g" }, CultureInfo.InvariantCulture])
            .ShouldBe(TimeSpan.ParseExact("01:02:03", ["c", "g"], CultureInfo.InvariantCulture));
        StaticMember(nameof(TimeSpan.ParseExact))
            .Invoke(["01:02:03", "c", CultureInfo.InvariantCulture, TimeSpanStyles.None])
            .ShouldBe(TimeSpan.ParseExact("01:02:03", "c", CultureInfo.InvariantCulture, TimeSpanStyles.None));
        StaticMember(nameof(TimeSpan.ParseExact))
            .Invoke(["01:02:03", new[] { "c", "g" }, CultureInfo.InvariantCulture, TimeSpanStyles.None])
            .ShouldBe(TimeSpan.ParseExact("01:02:03", ["c", "g"], CultureInfo.InvariantCulture, TimeSpanStyles.None));
    }

#if NET
    [Fact]
    public void TimeSpan_ModernStaticMembers()
    {
        StaticMember(nameof(TimeSpan.FromDays)).Invoke([1]).ShouldBe(TimeSpan.FromDays(1));
        StaticMember(nameof(TimeSpan.FromDays)).Invoke([1, 2, 3L, 4L, 5L, 6L]).ShouldBe(TimeSpan.FromDays(1, 2, 3, 4, 5, 6));
        StaticMember(nameof(TimeSpan.FromHours)).Invoke([1]).ShouldBe(TimeSpan.FromHours(1));
        StaticMember(nameof(TimeSpan.FromHours)).Invoke([1, 2L, 3L, 4L, 5L]).ShouldBe(TimeSpan.FromHours(1, 2, 3, 4, 5));
        StaticMember(nameof(TimeSpan.FromMicroseconds)).Invoke([1L]).ShouldBe(TimeSpan.FromMicroseconds(1L));
        StaticMember(nameof(TimeSpan.FromMicroseconds)).Invoke([1.5d]).ShouldBe(TimeSpan.FromMicroseconds(1.5d));
        StaticMember(nameof(TimeSpan.FromMilliseconds)).Invoke([1L]).ShouldBe(TimeSpan.FromMilliseconds(1L));
        StaticMember(nameof(TimeSpan.FromMilliseconds)).Invoke([1L, 2L]).ShouldBe(TimeSpan.FromMilliseconds(1L, 2L));
        StaticMember(nameof(TimeSpan.FromMinutes)).Invoke([1L]).ShouldBe(TimeSpan.FromMinutes(1L));
        StaticMember(nameof(TimeSpan.FromMinutes)).Invoke([1L, 2L, 3L, 4L]).ShouldBe(TimeSpan.FromMinutes(1L, 2L, 3L, 4L));
        StaticMember(nameof(TimeSpan.FromSeconds)).Invoke([1L]).ShouldBe(TimeSpan.FromSeconds(1L));
        StaticMember(nameof(TimeSpan.FromSeconds)).Invoke([1L, 2L, 3L]).ShouldBe(TimeSpan.FromSeconds(1L, 2L, 3L));

        (string Name, object Value)[] fields =
        [
            (nameof(TimeSpan.HoursPerDay), TimeSpan.HoursPerDay),
            (nameof(TimeSpan.MicrosecondsPerDay), TimeSpan.MicrosecondsPerDay),
            (nameof(TimeSpan.MicrosecondsPerHour), TimeSpan.MicrosecondsPerHour),
            (nameof(TimeSpan.MicrosecondsPerMillisecond), TimeSpan.MicrosecondsPerMillisecond),
            (nameof(TimeSpan.MicrosecondsPerMinute), TimeSpan.MicrosecondsPerMinute),
            (nameof(TimeSpan.MicrosecondsPerSecond), TimeSpan.MicrosecondsPerSecond),
            (nameof(TimeSpan.MillisecondsPerDay), TimeSpan.MillisecondsPerDay),
            (nameof(TimeSpan.MillisecondsPerHour), TimeSpan.MillisecondsPerHour),
            (nameof(TimeSpan.MillisecondsPerMinute), TimeSpan.MillisecondsPerMinute),
            (nameof(TimeSpan.MillisecondsPerSecond), TimeSpan.MillisecondsPerSecond),
            (nameof(TimeSpan.MinutesPerDay), TimeSpan.MinutesPerDay),
            (nameof(TimeSpan.MinutesPerHour), TimeSpan.MinutesPerHour),
            (nameof(TimeSpan.NanosecondsPerTick), TimeSpan.NanosecondsPerTick),
            (nameof(TimeSpan.SecondsPerDay), TimeSpan.SecondsPerDay),
            (nameof(TimeSpan.SecondsPerHour), TimeSpan.SecondsPerHour),
            (nameof(TimeSpan.SecondsPerMinute), TimeSpan.SecondsPerMinute),
            (nameof(TimeSpan.TicksPerDay), TimeSpan.TicksPerDay),
            (nameof(TimeSpan.TicksPerHour), TimeSpan.TicksPerHour),
            (nameof(TimeSpan.TicksPerMicrosecond), TimeSpan.TicksPerMicrosecond),
            (nameof(TimeSpan.TicksPerMillisecond), TimeSpan.TicksPerMillisecond),
            (nameof(TimeSpan.TicksPerMinute), TimeSpan.TicksPerMinute),
            (nameof(TimeSpan.TicksPerSecond), TimeSpan.TicksPerSecond),
        ];

        foreach ((string name, object value) in fields)
        {
            StaticMember(name).Invoke().ShouldBe(value);
        }
    }
#endif

    [Fact]
    public void TimeSpan_Operators()
    {
        TimeSpan oneHour = TimeSpan.FromHours(1);
        TimeSpan twoHours = TimeSpan.FromHours(2);

        StaticMember("op_Addition").Invoke([oneHour, twoHours]).ShouldBe(oneHour + twoHours);
#if NET
        StaticMember("op_Division").Invoke([twoHours, oneHour]).ShouldBe(twoHours / oneHour);
        StaticMember("op_Division").Invoke([twoHours, 2d]).ShouldBe(twoHours / 2d);
#endif
        StaticMember("op_Equality").Invoke([oneHour, oneHour]).ShouldBe(true);
        StaticMember("op_GreaterThan").Invoke([twoHours, oneHour]).ShouldBe(true);
        StaticMember("op_GreaterThanOrEqual").Invoke([twoHours, oneHour]).ShouldBe(true);
        StaticMember("op_Inequality").Invoke([oneHour, twoHours]).ShouldBe(true);
        StaticMember("op_LessThan").Invoke([oneHour, twoHours]).ShouldBe(true);
        StaticMember("op_LessThanOrEqual").Invoke([oneHour, twoHours]).ShouldBe(true);
#if NET
        StaticMember("op_Multiply").Invoke([twoHours, 2d]).ShouldBe(twoHours * 2d);
        StaticMember("op_Multiply").Invoke([2d, twoHours]).ShouldBe(2d * twoHours);
#endif
        StaticMember("op_Subtraction").Invoke([twoHours, oneHour]).ShouldBe(twoHours - oneHour);
        StaticMember("op_UnaryNegation").Invoke([oneHour]).ShouldBe(-oneHour);
        StaticMember("op_UnaryPlus").Invoke([oneHour]).ShouldBe(+oneHour);
    }

    [Fact]
    public void TimeSpan_InstanceMembers()
    {
        TimeSpan value = new(1, 2, 3, 4, 5);
        TimeSpan other = TimeSpan.FromHours(1);

        InstanceMember(nameof(TimeSpan.Days)).Invoke(value).ShouldBe(value.Days);
        InstanceMember(nameof(TimeSpan.Hours)).Invoke(value).ShouldBe(value.Hours);
        InstanceMember(nameof(TimeSpan.Milliseconds)).Invoke(value).ShouldBe(value.Milliseconds);
        InstanceMember(nameof(TimeSpan.Minutes)).Invoke(value).ShouldBe(value.Minutes);
        InstanceMember(nameof(TimeSpan.Seconds)).Invoke(value).ShouldBe(value.Seconds);
        InstanceMember(nameof(TimeSpan.Ticks)).Invoke(value).ShouldBe(value.Ticks);
        InstanceMember(nameof(TimeSpan.TotalDays)).Invoke(value).ShouldBe(value.TotalDays);
        InstanceMember(nameof(TimeSpan.TotalHours)).Invoke(value).ShouldBe(value.TotalHours);
        InstanceMember(nameof(TimeSpan.TotalMilliseconds)).Invoke(value).ShouldBe(value.TotalMilliseconds);
        InstanceMember(nameof(TimeSpan.TotalMinutes)).Invoke(value).ShouldBe(value.TotalMinutes);
        InstanceMember(nameof(TimeSpan.TotalSeconds)).Invoke(value).ShouldBe(value.TotalSeconds);
#if NET
        InstanceMember(nameof(TimeSpan.Microseconds)).Invoke(value).ShouldBe(value.Microseconds);
        InstanceMember(nameof(TimeSpan.Nanoseconds)).Invoke(value).ShouldBe(value.Nanoseconds);
        InstanceMember(nameof(TimeSpan.TotalMicroseconds)).Invoke(value).ShouldBe(value.TotalMicroseconds);
        InstanceMember(nameof(TimeSpan.TotalNanoseconds)).Invoke(value).ShouldBe(value.TotalNanoseconds);
#endif
        InstanceMember(nameof(TimeSpan.Add)).Invoke(value, [other]).ShouldBe(value.Add(other));
        InstanceMember(nameof(TimeSpan.CompareTo)).Invoke(value, [other]).ShouldBe(value.CompareTo(other));
#if NET
        InstanceMember(nameof(TimeSpan.Divide)).Invoke(value, [other]).ShouldBe(value.Divide(other));
        InstanceMember(nameof(TimeSpan.Divide)).Invoke(value, [2d]).ShouldBe(value.Divide(2d));
#endif
        InstanceMember(nameof(TimeSpan.Duration)).Invoke(value).ShouldBe(value.Duration());
        InstanceMember(nameof(TimeSpan.Equals)).Invoke(value, [other]).ShouldBe(value.Equals(other));
        InstanceMember(nameof(TimeSpan.GetHashCode)).Invoke(value).ShouldBe(value.GetHashCode());
#if NET
        InstanceMember(nameof(TimeSpan.Multiply)).Invoke(value, [2d]).ShouldBe(value.Multiply(2d));
#endif
        InstanceMember(nameof(TimeSpan.Negate)).Invoke(value).ShouldBe(value.Negate());
        InstanceMember(nameof(TimeSpan.Subtract)).Invoke(value, [other]).ShouldBe(value.Subtract(other));
        InstanceMember(nameof(TimeSpan.ToString)).Invoke(value).ShouldBe(value.ToString());
        InstanceMember(nameof(TimeSpan.ToString)).Invoke(value, ["c"]).ShouldBe(value.ToString("c"));
        InstanceMember(nameof(TimeSpan.ToString)).Invoke(value, ["c", CultureInfo.InvariantCulture])
            .ShouldBe(value.ToString("c", CultureInfo.InvariantCulture));
    }
}
