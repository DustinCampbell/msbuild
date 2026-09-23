// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Regex_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Regex), output)
{
    [Fact]
    public void Constructor_String_NotHandled()
        => Constructor.NotHandled(["[a-z]+"]);

    [Theory]
    [InlineData("abc123", "[0-9]+", "#", "abc#")]
    [InlineData("abc", "[0-9]+", "#", "abc")]
    public void Regex_Replace_StringStringString(string input, string pattern, string replacement, string expected)
        => StaticMember(nameof(Regex.Replace))
            .Invoke([input, pattern, replacement])
            .ShouldBe(expected);

    [Fact]
    public void Regex_Replace_InvalidArgument_NotHandled()
        => StaticMember(nameof(Regex.Replace))
            .NotHandled(["abc", "[a-z]+", 1]);

    [Fact]
    public void Regex_Replace_TwoArguments_NotHandled()
        => StaticMember(nameof(Regex.Replace))
            .NotHandled(["abc", "b"]);

    [Fact]
    public void Regex_Replace_RegexOptions_NotHandled()
        => StaticMember(nameof(Regex.Replace))
            .NotHandled(["ABC", "abc", "x", nameof(RegexOptions.IgnoreCase)]);

    [Fact]
    public void Regex_Escape_NotHandled()
        => StaticMember(nameof(Regex.Escape))
            .NotHandled(["a.b"]);

    [Fact]
    public void Regex_IsMatch_NotHandled()
        => InstanceMember(nameof(Regex.IsMatch))
            .NotHandled(new Regex("[a-z]+"), ["abc"]);
}
