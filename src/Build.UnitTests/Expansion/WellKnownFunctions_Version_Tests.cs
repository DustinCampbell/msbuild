// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Version_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Version), output)
{
    [Fact]
    public void Version_Constructors()
    {
        Constructor.Invoke().ShouldBe(new Version());
        Constructor.Invoke(["1.2.3.4"]).ShouldBe(new Version(1, 2, 3, 4));
        Constructor.Invoke(["1", "2"]).ShouldBe(new Version(1, 2));
        Constructor.Invoke(["1", "2", "3"]).ShouldBe(new Version(1, 2, 3));
        Constructor.Invoke(["1", "2", "3", "4"]).ShouldBe(new Version(1, 2, 3, 4));
    }

    [Fact]
    public void Version_Parse_String()
        => StaticMember(nameof(Version.Parse))
            .Invoke(["1.2.3.4"])
            .ShouldBe(new Version(1, 2, 3, 4));

    [Fact]
    public void Version_Parse_InvalidArgument_NotHandled()
        => StaticMember(nameof(Version.Parse))
            .NotHandled([1]);

    [Fact]
    public void Version_Parse_NoArguments_NotHandled()
        => StaticMember(nameof(Version.Parse))
            .NotHandled();

    [Theory]
    [InlineData(0, "")]
    [InlineData(2, "1.2")]
    [InlineData(3, "1.2.3")]
    [InlineData(4, "1.2.3.4")]
    public void Version_ToString_FieldCount(int fieldCount, string expected)
        => InstanceMember(nameof(Version.ToString))
            .Invoke(new Version(1, 2, 3, 4), [fieldCount])
            .ShouldBe(expected);

    [Fact]
    public void Version_ToString_InvalidFieldCount_NotHandled()
        => InstanceMember(nameof(Version.ToString))
            .NotHandled(new Version(1, 2), ["invalid"]);

    [Fact]
    public void Version_ToString_NoArguments()
        => InstanceMember(nameof(Version.ToString))
            .Invoke(new Version(1, 2))
            .ShouldBe("1.2");

    [Fact]
    public void Version_Members()
    {
        Version version = new(1, 2, 3, 4);

        InstanceMember(nameof(Version.CompareTo)).Invoke(version, [new Version(1, 2)]).ShouldBe(1);
        InstanceMember(nameof(Version.Equals)).Invoke(version, [new Version(1, 2, 3, 4)]).ShouldBe(true);
        InstanceMember(nameof(Version.GetHashCode)).Invoke(version).ShouldBe(version.GetHashCode());
        InstanceMember(nameof(Version.Major)).Invoke(version).ShouldBe(1);
        InstanceMember(nameof(Version.Minor)).Invoke(version).ShouldBe(2);
        InstanceMember(nameof(Version.Build)).Invoke(version).ShouldBe(3);
        InstanceMember(nameof(Version.Revision)).Invoke(version).ShouldBe(4);
        InstanceMember(nameof(Version.MajorRevision)).Invoke(version).ShouldBe(version.MajorRevision);
        InstanceMember(nameof(Version.MinorRevision)).Invoke(version).ShouldBe(version.MinorRevision);
    }

    [Fact]
    public void Version_CompareTo_Invalid_NotHandled()
        => InstanceMember(nameof(Version.CompareTo))
            .NotHandled(new Version(1, 2), ["1.1"]);

    [Fact]
    public void Version_Major_WithArgument_NotHandled()
        => InstanceMember(nameof(Version.Major))
            .NotHandled(new Version(1, 2), [1]);
}
