// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
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
    public void Char_Classification()
    {
        StaticMember(nameof(char.GetNumericValue)).Invoke(['8']).ShouldBe(8d);
        StaticMember(nameof(char.GetUnicodeCategory)).Invoke(['A']).ShouldBe(UnicodeCategory.UppercaseLetter);
        StaticMember(nameof(char.IsControl)).Invoke(['\n']).ShouldBe(true);
        StaticMember(nameof(char.IsHighSurrogate)).Invoke(['\uD83D']).ShouldBe(true);
        StaticMember(nameof(char.IsLetter)).Invoke(['a']).ShouldBe(true);
        StaticMember(nameof(char.IsLetterOrDigit)).Invoke(['1']).ShouldBe(true);
        StaticMember(nameof(char.IsLower)).Invoke(['a']).ShouldBe(true);
        StaticMember(nameof(char.IsLowSurrogate)).Invoke(['\uDE00']).ShouldBe(true);
        StaticMember(nameof(char.IsNumber)).Invoke(['8']).ShouldBe(true);
        StaticMember(nameof(char.IsPunctuation)).Invoke(['!']).ShouldBe(true);
        StaticMember(nameof(char.IsSeparator)).Invoke([' ']).ShouldBe(true);
        StaticMember(nameof(char.IsSurrogate)).Invoke(['\uD83D']).ShouldBe(true);
        StaticMember(nameof(char.IsSymbol)).Invoke(['+']).ShouldBe(true);
        StaticMember(nameof(char.IsUpper)).Invoke(['A']).ShouldBe(true);
        StaticMember(nameof(char.IsWhiteSpace)).Invoke(['\t']).ShouldBe(true);
    }

    [Fact]
    public void Char_StringIndexClassification()
    {
        const string value = "A8!\t\U0001F600";

        StaticMember(nameof(char.GetNumericValue)).Invoke([value, 1]).ShouldBe(8d);
        StaticMember(nameof(char.GetUnicodeCategory)).Invoke([value, 0]).ShouldBe(UnicodeCategory.UppercaseLetter);
        StaticMember(nameof(char.IsControl)).Invoke([value, 3]).ShouldBe(true);
        StaticMember(nameof(char.IsDigit)).Invoke([value, 1]).ShouldBe(true);
        StaticMember(nameof(char.IsHighSurrogate)).Invoke([value, 4]).ShouldBe(true);
        StaticMember(nameof(char.IsLetter)).Invoke([value, 0]).ShouldBe(true);
        StaticMember(nameof(char.IsLetterOrDigit)).Invoke([value, 1]).ShouldBe(true);
        StaticMember(nameof(char.IsLower)).Invoke(["a", 0]).ShouldBe(true);
        StaticMember(nameof(char.IsLowSurrogate)).Invoke([value, 5]).ShouldBe(true);
        StaticMember(nameof(char.IsNumber)).Invoke([value, 1]).ShouldBe(true);
        StaticMember(nameof(char.IsPunctuation)).Invoke([value, 2]).ShouldBe(true);
        StaticMember(nameof(char.IsSeparator)).Invoke([" ", 0]).ShouldBe(true);
        StaticMember(nameof(char.IsSurrogate)).Invoke([value, 4]).ShouldBe(true);
        StaticMember(nameof(char.IsSurrogatePair)).Invoke([value, 4]).ShouldBe(true);
        StaticMember(nameof(char.IsSymbol)).Invoke(["+", 0]).ShouldBe(true);
        StaticMember(nameof(char.IsUpper)).Invoke([value, 0]).ShouldBe(true);
        StaticMember(nameof(char.IsWhiteSpace)).Invoke([value, 3]).ShouldBe(true);
    }

    [Fact]
    public void Char_ConversionAndCasing()
    {
        StaticMember(nameof(char.ConvertFromUtf32)).Invoke([0x1F600]).ShouldBe("\U0001F600");
        StaticMember(nameof(char.ConvertToUtf32)).Invoke(['\uD83D', '\uDE00']).ShouldBe(0x1F600);
        StaticMember(nameof(char.ConvertToUtf32)).Invoke(["\U0001F600", 0]).ShouldBe(0x1F600);
        StaticMember(nameof(char.Parse)).Invoke(["a"]).ShouldBe('a');
        StaticMember(nameof(char.ToLower)).Invoke(['A']).ShouldBe('a');
        StaticMember(nameof(char.ToLower)).Invoke(['I', CultureInfo.GetCultureInfo("tr-TR")]).ShouldBe('\u0131');
        StaticMember(nameof(char.ToLowerInvariant)).Invoke(['A']).ShouldBe('a');
        StaticMember(nameof(char.ToString)).Invoke(['a']).ShouldBe("a");
        StaticMember(nameof(char.ToUpper)).Invoke(['a']).ShouldBe('A');
        StaticMember(nameof(char.ToUpper)).Invoke(['i', CultureInfo.GetCultureInfo("tr-TR")]).ShouldBe('\u0130');
        StaticMember(nameof(char.ToUpperInvariant)).Invoke(['a']).ShouldBe('A');
    }

#if NET
    [Fact]
    public void Char_AsciiClassification()
    {
        StaticMember(nameof(char.IsAscii)).Invoke(['a']).ShouldBe(true);
        StaticMember(nameof(char.IsAsciiDigit)).Invoke(['1']).ShouldBe(true);
        StaticMember(nameof(char.IsAsciiHexDigit)).Invoke(['f']).ShouldBe(true);
        StaticMember(nameof(char.IsAsciiHexDigitLower)).Invoke(['f']).ShouldBe(true);
        StaticMember(nameof(char.IsAsciiHexDigitUpper)).Invoke(['F']).ShouldBe(true);
        StaticMember(nameof(char.IsAsciiLetter)).Invoke(['a']).ShouldBe(true);
        StaticMember(nameof(char.IsAsciiLetterLower)).Invoke(['a']).ShouldBe(true);
        StaticMember(nameof(char.IsAsciiLetterOrDigit)).Invoke(['1']).ShouldBe(true);
        StaticMember(nameof(char.IsAsciiLetterUpper)).Invoke(['A']).ShouldBe(true);
        StaticMember(nameof(char.IsBetween)).Invoke(['b', 'a', 'c']).ShouldBe(true);
    }
#endif

    [Fact]
    public void Char_InstanceToString()
        => InstanceMember(nameof(char.ToString))
            .Invoke('a')
            .ShouldBe("a");
}
