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
    public void Version_Constructor_InvalidArguments_NotHandled()
    {
        Constructor.NotHandled([1]);
        Constructor.NotHandled(["invalid", "2"]);
        Constructor.NotHandled(["1", "2", "3", "4", "5"]);
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
    public void Version_ToString_NoArguments_NotHandled()
        => InstanceMember(nameof(Version.ToString))
            .NotHandled(new Version(1, 2));

    [Fact]
    public void Version_CompareTo_NotHandled()
        => InstanceMember(nameof(Version.CompareTo))
            .NotHandled(new Version(1, 2), [new Version(1, 1)]);

    [Fact]
    public void Version_Major_NotHandled()
        => InstanceMember(nameof(Version.Major))
            .NotHandled(new Version(1, 2));
}
