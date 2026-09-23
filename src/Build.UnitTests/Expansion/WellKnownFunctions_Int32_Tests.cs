// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Int32_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(int), output)
{
    [Theory]
    [InlineData(42, "D4", "0042")]
    [InlineData(255, "X", "FF")]
    public void Int32_ToString_Format(int value, string format, string expected)
        => InstanceMember(nameof(int.ToString))
            .Invoke(value, [format])
            .ShouldBe(expected);

    [Fact]
    public void Int32_ToString_InvalidFormatType_NotHandled()
        => InstanceMember(nameof(int.ToString))
            .NotHandled(42, [1]);

    [Fact]
    public void Int32_ToString_NoArguments_NotHandled()
        => InstanceMember(nameof(int.ToString))
            .NotHandled(42);

    [Fact]
    public void Int32_CompareTo_Int32_NotHandled()
        => InstanceMember(nameof(int.CompareTo))
            .NotHandled(42, [1]);

    [Fact]
    public void Int32_Parse_String_NotHandled()
        => StaticMember(nameof(int.Parse))
            .NotHandled(["42"]);
}
