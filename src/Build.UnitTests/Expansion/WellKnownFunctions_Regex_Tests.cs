// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Regex_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Regex), output)
{
    [Fact]
    public void Regex_Constructors()
    {
        Constructor.Invoke(["[a-z]+"]).ShouldBeOfType<Regex>();
        Constructor.Invoke(["[a-z]+", "RegexOptions.IgnoreCase"]).ShouldBeOfType<Regex>().Options.ShouldBe(RegexOptions.IgnoreCase);
        Constructor.Invoke(["[a-z]+", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)]).ShouldBeOfType<Regex>();
    }

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
    public void Regex_Replace_RegexOptions()
        => StaticMember(nameof(Regex.Replace))
            .Invoke(["ABC", "abc", "x", nameof(RegexOptions.IgnoreCase)])
            .ShouldBe("x");

    [Fact]
    public void Regex_StaticMembers()
    {
        StaticMember(nameof(Regex.Escape)).Invoke(["a.b"]).ShouldBe("a\\.b");
        StaticMember(nameof(Regex.Unescape)).Invoke(["a\\.b"]).ShouldBe("a.b");
        StaticMember(nameof(Regex.IsMatch)).Invoke(["ABC", "abc"]).ShouldBe(false);
        StaticMember(nameof(Regex.IsMatch)).Invoke(["ABC", "abc", "IgnoreCase"]).ShouldBe(true);
        StaticMember(nameof(Regex.Match)).Invoke(["abc123", "\\d+"]).ShouldBeOfType<Match>().Value.ShouldBe("123");
        StaticMember(nameof(Regex.Matches)).Invoke(["a1b2", "\\d"]).ShouldBeOfType<MatchCollection>().Count.ShouldBe(2);
        StaticMember(nameof(Regex.Split)).Invoke(["a1b2", "\\d"]).ShouldBeOfType<string[]>().ShouldBe(["a", "b", ""]);
    }

    [Fact]
    public void Regex_InstanceMembers()
    {
        Regex regex = new("(?<word>[a-z]+)", RegexOptions.IgnoreCase);
        Match match = regex.Match("123abc456");
        MatchCollection matches = regex.Matches("abc def");
        Group group = match.Groups["word"];

        InstanceMember(nameof(Regex.IsMatch)).Invoke(regex, ["123abc"]).ShouldBe(true);
        InstanceMember(nameof(Regex.Match)).Invoke(regex, ["123abc"]).ShouldBeOfType<Match>().Success.ShouldBeTrue();
        InstanceMember(nameof(Regex.Matches)).Invoke(regex, ["abc def"]).ShouldBeOfType<MatchCollection>().Count.ShouldBe(2);
        InstanceMember(nameof(Regex.Replace)).Invoke(regex, ["abc123", "#"]).ShouldBe("#123");
        InstanceMember(nameof(Regex.Split)).Invoke(regex, ["123abc456"]).ShouldBeOfType<string[]>();
        InstanceMember(nameof(Regex.GetGroupNames)).Invoke(regex).ShouldBeOfType<string[]>();
        InstanceMember(nameof(Regex.GetGroupNumbers)).Invoke(regex).ShouldBeOfType<int[]>();
        InstanceMember(nameof(Regex.GroupNameFromNumber)).Invoke(regex, ["1"]).ShouldBe("word");
        InstanceMember(nameof(Regex.GroupNumberFromName)).Invoke(regex, ["word"]).ShouldBe(1);
        InstanceMember(nameof(Regex.MatchTimeout)).Invoke(regex).ShouldBe(regex.MatchTimeout);
        InstanceMember(nameof(Regex.Options)).Invoke(regex).ShouldBe(regex.Options);
        InstanceMember(nameof(Regex.RightToLeft)).Invoke(regex).ShouldBe(regex.RightToLeft);
        InstanceMember(nameof(Regex.ToString)).Invoke(regex).ShouldBe(regex.ToString());

        InstanceMember(nameof(Match.Groups)).Invoke(match).ShouldBeSameAs(match.Groups);
        InstanceMember(nameof(Match.NextMatch)).Invoke(match).ShouldBeOfType<Match>();
        InstanceMember(nameof(Match.Result)).Invoke(match, ["${word}"]).ShouldBe("abc");
        InstanceMember(nameof(Group.Captures)).Invoke(group).ShouldBeSameAs(group.Captures);
        InstanceMember(nameof(Group.Success)).Invoke(group).ShouldBe(true);
        InstanceMember(nameof(Capture.Index)).Invoke(group).ShouldBe(group.Index);
        InstanceMember(nameof(Capture.Length)).Invoke(group).ShouldBe(group.Length);
        InstanceMember(nameof(Capture.Value)).Invoke(group).ShouldBe(group.Value);
        InstanceMember(nameof(Capture.ToString)).Invoke(group).ShouldBe(group.ToString());
        InstanceMember("get_Item").Invoke(match.Groups, ["word"]).ShouldBeSameAs(group);
        InstanceMember("get_Item").Invoke(match.Groups, ["1"]).ShouldBeSameAs(group);
        InstanceMember(nameof(GroupCollection.Count)).Invoke(match.Groups).ShouldBe(match.Groups.Count);
        InstanceMember("get_Item").Invoke(group.Captures, ["0"]).ShouldBeSameAs(group.Captures[0]);
        InstanceMember(nameof(CaptureCollection.Count)).Invoke(group.Captures).ShouldBe(group.Captures.Count);
        InstanceMember("get_Item").Invoke(matches, ["1"]).ShouldBeSameAs(matches[1]);
        InstanceMember(nameof(MatchCollection.Count)).Invoke(matches).ShouldBe(matches.Count);
    }

    [Fact]
    public void Regex_Escape_Invalid_NotHandled()
        => StaticMember(nameof(Regex.Escape))
            .NotHandled([1]);
}
