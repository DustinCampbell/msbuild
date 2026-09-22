// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Char_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(char), output)
{
    [Fact]
    public void Char_IsDigit_CaseInsensitive()
        => StaticMember("isdigit")
            .Invoke(["5"])
            .ShouldBe(true);

    [Fact]
    public void Char_IsDigit_EmptyString_NotHandled()
        => StaticMember(nameof(char.IsDigit))
            .NotHandled([""]);

    [Fact]
    public void Char_IsDigit_InvalidIndex_NotHandled()
        => StaticMember(nameof(char.IsDigit))
            .NotHandled(["a5", "invalid"]);

    [Theory]
    [InlineData("5", true)]
    [InlineData("x", false)]
    [InlineData('5', true)]
    [InlineData('x', false)]
    public void Char_IsDigit_Char(object value, bool expected)
        => StaticMember(nameof(char.IsDigit))
            .Invoke([value])
            .ShouldBe(expected);

    [Theory]
    [InlineData("a5", "1", true)]
    [InlineData("a5", "0", false)]
    [InlineData("a5", 1, true)]
    [InlineData("a5", 0, false)]
    public void Char_IsDigit_StringIndex(string value, object index, bool expected)
        => StaticMember(nameof(char.IsDigit))
            .Invoke([value, index])
            .ShouldBe(expected);

    [Fact]
    public void Char_IsLetter_NotHandled()
        => StaticMember(nameof(char.IsLetter))
            .NotHandled(["a"]);

    [Fact]
    public void Char_ToString_NotHandled()
        => InstanceMember(nameof(char.ToString))
            .NotHandled('a');
}
