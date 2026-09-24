// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Guid_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Guid), output)
{
    [Fact]
    public void Guid_Constructors()
    {
        Constructor.Invoke(["00000000-0000-0000-0000-000000000000"]).ShouldBe(Guid.Empty);
        Constructor.Invoke([new byte[16]]).ShouldBe(Guid.Empty);
        Constructor.Invoke(["0", "0", "0", new byte[8]]).ShouldBe(Guid.Empty);
        Constructor.Invoke(["0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0"]).ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Guid_NewGuid()
        => StaticMember(nameof(Guid.NewGuid))
            .Invoke()
            .ShouldBeOfType<Guid>()
            .ShouldNotBe(Guid.Empty);

    [Fact]
    public void Guid_NewGuid_Arguments_NotHandled()
        => StaticMember(nameof(Guid.NewGuid))
            .NotHandled(["argument"]);

    [Fact]
    public void Guid_StaticMembers()
    {
        StaticMember(nameof(Guid.Empty)).Invoke().ShouldBe(Guid.Empty);
        StaticMember(nameof(Guid.Parse)).Invoke(["00000000-0000-0000-0000-000000000000"]).ShouldBe(Guid.Empty);
#if NET
        StaticMember(nameof(Guid.Parse)).Invoke(["00000000-0000-0000-0000-000000000000", System.Globalization.CultureInfo.InvariantCulture]).ShouldBe(Guid.Empty);
#endif
        StaticMember(nameof(Guid.ParseExact)).Invoke(["00000000000000000000000000000000", "N"]).ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Guid_InstanceMembers()
    {
        Guid guid = Guid.Parse("12345678-1234-5678-9012-345678901234");

        InstanceMember(nameof(Guid.CompareTo)).Invoke(guid, [guid]).ShouldBe(0);
        InstanceMember(nameof(Guid.Equals)).Invoke(guid, [guid]).ShouldBe(true);
        InstanceMember(nameof(Guid.GetHashCode)).Invoke(guid).ShouldBe(guid.GetHashCode());
        InstanceMember(nameof(Guid.ToByteArray)).Invoke(guid).ShouldBeOfType<byte[]>().ShouldBe(guid.ToByteArray());
        InstanceMember(nameof(Guid.ToString)).Invoke(guid).ShouldBe(guid.ToString());
        InstanceMember(nameof(Guid.ToString)).Invoke(guid, ["N"]).ShouldBe(guid.ToString("N"));
        InstanceMember(nameof(Guid.ToString)).Invoke(guid, ["D", System.Globalization.CultureInfo.InvariantCulture]).ShouldBe(guid.ToString("D"));
    }
}
