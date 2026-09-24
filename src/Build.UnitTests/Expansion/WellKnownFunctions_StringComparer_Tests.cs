// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_StringComparer_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(StringComparer), output)
{
    [Fact]
    public void StringComparer_StaticMembers()
    {
        StaticMember(nameof(StringComparer.CurrentCulture)).Invoke().ShouldBe(StringComparer.CurrentCulture);
        StaticMember(nameof(StringComparer.CurrentCultureIgnoreCase)).Invoke().ShouldBe(StringComparer.CurrentCultureIgnoreCase);
        StaticMember(nameof(StringComparer.InvariantCulture)).Invoke().ShouldBeSameAs(StringComparer.InvariantCulture);
        StaticMember(nameof(StringComparer.InvariantCultureIgnoreCase)).Invoke().ShouldBeSameAs(StringComparer.InvariantCultureIgnoreCase);
        StaticMember(nameof(StringComparer.Ordinal)).Invoke().ShouldBeSameAs(StringComparer.Ordinal);
        StaticMember(nameof(StringComparer.OrdinalIgnoreCase)).Invoke().ShouldBeSameAs(StringComparer.OrdinalIgnoreCase);
        StaticMember(nameof(StringComparer.Create)).Invoke([CultureInfo.InvariantCulture, "true"]).ShouldBeAssignableTo<StringComparer>();
#if NET
        StaticMember(nameof(StringComparer.FromComparison)).Invoke(["OrdinalIgnoreCase"]).ShouldBeSameAs(StringComparer.OrdinalIgnoreCase);
#endif
    }

    [Fact]
    public void StringComparer_InstanceMembers()
    {
        StringComparer comparer = StringComparer.OrdinalIgnoreCase;

        InstanceMember(nameof(StringComparer.Compare)).Invoke(comparer, ["a", "A"]).ShouldBe(0);
        InstanceMember(nameof(StringComparer.Equals)).Invoke(comparer, ["a", "A"]).ShouldBe(true);
        InstanceMember(nameof(StringComparer.Equals)).Invoke(comparer, [StringComparer.OrdinalIgnoreCase]).ShouldBe(true);
        InstanceMember(nameof(StringComparer.GetHashCode)).Invoke(comparer, ["a"]).ShouldBe(comparer.GetHashCode("a"));
        InstanceMember(nameof(StringComparer.GetHashCode)).Invoke(comparer).ShouldBe(comparer.GetHashCode());
        InstanceMember(nameof(StringComparer.ToString)).Invoke(comparer).ShouldBe(comparer.ToString());
    }
}
