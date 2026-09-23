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
    public void Constructor_String_NotHandled()
        => Constructor.NotHandled(["00000000-0000-0000-0000-000000000000"]);

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
    public void Guid_Parse_String_NotHandled()
        => StaticMember(nameof(Guid.Parse))
            .NotHandled(["00000000-0000-0000-0000-000000000000"]);

    [Fact]
    public void Guid_ToString_NotHandled()
        => InstanceMember(nameof(Guid.ToString))
            .NotHandled(Guid.Empty);
}
