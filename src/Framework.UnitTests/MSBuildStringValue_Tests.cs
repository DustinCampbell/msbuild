// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public sealed class MSBuildStringValue_Tests
{
    [Theory]
    [InlineData("foo")] // no special chars — no-op case
    [InlineData("path\\to\\file")] // backslashes are not special
    [InlineData("")] // empty
    public void FromEscaped_Escaped_ReturnsSameInstance(string value)
    {
        var s = MSBuildStringValue.FromEscaped(value);
        s.Escaped.ShouldBeSameAs(value);
    }

    [Theory]
    [InlineData("foo", "foo")] // no special chars — unescaped == escaped
    [InlineData("", "")]
    [InlineData("foo%3bbar", "foo;bar")] // %3b → ';'
    [InlineData("%28paren%29", "(paren)")] // %28 → '(', %29 → ')'
    [InlineData("%25", "%")] // %25 → '%'
    public void FromEscaped_Unescaped_ReturnsUnescapedValue(string escaped, string expectedUnescaped)
    {
        var s = MSBuildStringValue.FromEscaped(escaped);
        s.Unescaped.ShouldBe(expectedUnescaped);
    }

    [Theory]
    [InlineData("foo")] // no-op: Escape returns same reference
    [InlineData("")]
    public void FromEscaped_NoSpecialChars_BothFormsAreSameInstance(string value)
    {
        var s = MSBuildStringValue.FromEscaped(value);
        _ = s.Unescaped; // trigger lazy computation

        s.Escaped.ShouldBeSameAs(s.Unescaped);
        s.AreBothFormsKnownEqual.ShouldBeTrue();
    }

    [Theory]
    [InlineData("foo%3bbar", "foo;bar")] // actual escaping — forms differ
    [InlineData("%28%29", "()")]
    public void FromEscaped_WithSpecialChars_BothFormsAreDistinct(string escaped, string expectedUnescaped)
    {
        var s = MSBuildStringValue.FromEscaped(escaped);

        s.Escaped.ShouldBe(escaped);
        s.Unescaped.ShouldBe(expectedUnescaped);
        s.Escaped.ShouldNotBeSameAs(s.Unescaped);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("")]
    public void FromUnescaped_Unescaped_ReturnsSameInstance(string value)
    {
        var s = MSBuildStringValue.FromUnescaped(value);
        s.Unescaped.ShouldBeSameAs(value);
    }

    [Theory]
    [InlineData("foo", "foo")] // no special chars — escaped == unescaped
    [InlineData("", "")]
    [InlineData("foo;bar", "foo%3bbar")] // ';' → %3b
    [InlineData("(paren)", "%28paren%29")] // '(' → %28, ')' → %29
    [InlineData("%", "%25")] // '%' → %25
    public void FromUnescaped_Escaped_ReturnsEscapedValue(string unescaped, string expectedEscaped)
    {
        var s = MSBuildStringValue.FromUnescaped(unescaped);
        s.Escaped.ShouldBe(expectedEscaped);
    }

    [Theory]
    [InlineData("foo")]  // no-op: Escape returns same reference
    [InlineData("")]
    public void FromUnescaped_NoSpecialChars_BothFormsAreSameInstance(string value)
    {
        var s = MSBuildStringValue.FromUnescaped(value);
        _ = s.Escaped; // trigger lazy computation

        s.Escaped.ShouldBeSameAs(s.Unescaped);
        s.AreBothFormsKnownEqual.ShouldBeTrue();
    }

    [Theory]
    [InlineData("foo;bar", "foo%3bbar")]
    [InlineData("()", "%28%29")]
    public void FromUnescaped_WithSpecialChars_BothFormsAreDistinct(string unescaped, string expectedEscaped)
    {
        var s = MSBuildStringValue.FromUnescaped(unescaped);

        s.Unescaped.ShouldBe(unescaped);
        s.Escaped.ShouldBe(expectedEscaped);
        s.Escaped.ShouldNotBeSameAs(s.Unescaped);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("")]
    [InlineData("path\\to\\file")]
    public void From_PlainString_BothFormsAreIdentical(string value)
    {
        var s = MSBuildStringValue.From(value);
        s.Escaped.ShouldBe(value);
        s.Unescaped.ShouldBe(value);
        s.Unescaped.ShouldBeSameAs(s.Escaped);
    }

    [Theory]
    [InlineData("foo%3bbar", "foo;bar")]
    [InlineData("%28paren%29", "(paren)")]
    [InlineData("%25", "%")]
    [InlineData("%2a%3f", "*?")]
    public void From_WithEscapeSequences_TreatedAsEscaped(string value, string expectedUnescaped)
    {
        var s = MSBuildStringValue.From(value);
        s.Escaped.ShouldBe(value);
        s.Unescaped.ShouldBe(expectedUnescaped);
    }

    [Theory]
    [InlineData("foo;bar", "foo%3bbar")]
    [InlineData("(paren)", "%28paren%29")]
    [InlineData("*?", "%2a%3f")]
    public void From_WithSpecialCharsButNoEscapeSequences_TreatedAsUnescaped(string value, string expectedEscaped)
    {
        var s = MSBuildStringValue.From(value);
        s.Unescaped.ShouldBe(value);
        s.Escaped.ShouldBe(expectedEscaped);
    }

    [Theory]
    [InlineData("100%")]
    [InlineData("100% done")]
    [InlineData("%ZZ")]
    [InlineData("foo%")]
    public void From_WithPercentButNoValidEscapeSequence_TreatedAsUnescaped(string value)
    {
        var s = MSBuildStringValue.From(value);
        s.Unescaped.ShouldBe(value);
    }

    [Theory]
    [InlineData("foo%3bbar")]
    [InlineData("%28paren%29")]
    public void From_WithEscapeSequences_EscapedReturnsSameInstance(string value)
    {
        var s = MSBuildStringValue.From(value);
        s.Escaped.ShouldBeSameAs(value);
    }

    [Theory]
    [InlineData("foo;bar")]
    [InlineData("plain")]
    public void From_WithoutEscapeSequences_UnescapedReturnsSameInstance(string value)
    {
        var s = MSBuildStringValue.From(value);
        s.Unescaped.ShouldBeSameAs(value);
    }

    [Theory]
    [InlineData("foo%3bbar")]
    [InlineData("%28paren%29")]
    [InlineData("plain")]
    [InlineData("")]
    public void From_RoundTrip_EscapeUnescapedEqualsEscaped(string value)
    {
        var s = MSBuildStringValue.From(value);
        EscapingUtilities.Escape(s.Unescaped).ShouldBe(s.Escaped);
    }

    [Fact]
    public void From_MatchesFromEscaped_WhenValueContainsEscapeSequences()
    {
        const string value = "foo%3bbar";
        var fromMethod = MSBuildStringValue.From(value);
        var fromEscaped = MSBuildStringValue.FromEscaped(value);

        fromMethod.Escaped.ShouldBe(fromEscaped.Escaped);
        fromMethod.Unescaped.ShouldBe(fromEscaped.Unescaped);
    }

    [Fact]
    public void From_MatchesFromUnescaped_WhenValueHasNoEscapeSequences()
    {
        const string value = "foo;bar";
        var fromMethod = MSBuildStringValue.From(value);
        var fromUnescaped = MSBuildStringValue.FromUnescaped(value);

        fromMethod.Escaped.ShouldBe(fromUnescaped.Escaped);
        fromMethod.Unescaped.ShouldBe(fromUnescaped.Unescaped);
    }

    [Fact]
    public void AreBothFormsKnownEqual_BeforeEitherFormIsAccessed_IsFalse()
    {
        // Only _escaped is set; _unescaped is null — not yet known to be equal.
        var s = MSBuildStringValue.FromEscaped("foo");
        s.AreBothFormsKnownEqual.ShouldBeFalse();
    }

    [Fact]
    public void AreBothFormsKnownEqual_AfterAccessingUnescaped_NoSpecialChars_IsTrue()
    {
        var s = MSBuildStringValue.FromEscaped("foo");
        _ = s.Unescaped;
        s.AreBothFormsKnownEqual.ShouldBeTrue();
    }

    [Fact]
    public void AreBothFormsKnownEqual_AfterAccessingEscaped_NoSpecialChars_IsTrue()
    {
        var s = MSBuildStringValue.FromUnescaped("foo");
        _ = s.Escaped;
        s.AreBothFormsKnownEqual.ShouldBeTrue();
    }

    [Fact]
    public void AreBothFormsKnownEqual_WithSpecialChars_IsFalse()
    {
        var s = MSBuildStringValue.FromEscaped("foo%3bbar");
        _ = s.Unescaped;
        s.AreBothFormsKnownEqual.ShouldBeFalse();
    }

    [Fact]
    public void Escaped_AccessedTwice_ReturnsSameInstance()
    {
        var s = MSBuildStringValue.FromUnescaped("foo;bar");
        string first = s.Escaped;
        string second = s.Escaped;
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Unescaped_AccessedTwice_ReturnsSameInstance()
    {
        var s = MSBuildStringValue.FromEscaped("foo%3bbar");
        string first = s.Unescaped;
        string second = s.Unescaped;
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Equals_SameEscapedValue_ReturnsTrue()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        var b = MSBuildStringValue.FromUnescaped("foo;bar");
        a.Equals(b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentEscapedValues_ReturnsFalse()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        var b = MSBuildStringValue.FromEscaped("other");
        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var a = MSBuildStringValue.FromEscaped("foo");
        a.Equals((MSBuildStringValue?)null).ShouldBeFalse();
        a.Equals((string?)null).ShouldBeFalse();
        a.Equals((object?)null).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_SameEscapedValue_ProducesSameHash()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        var b = MSBuildStringValue.FromUnescaped("foo;bar");
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Theory]
    [InlineData("foo%3bbar")]
    [InlineData("plain")]
    public void ToString_ReturnsEscapedForm(string escaped)
    {
        var s = MSBuildStringValue.FromEscaped(escaped);
        s.ToString().ShouldBe(escaped);
    }

    [Theory]
    [InlineData("foo;bar")]
    [InlineData("(paren)")]
    [InlineData("plain")]
    [InlineData("")]
    [InlineData("path\\to\\dir\\")]
    public void FromUnescaped_RoundTrip_UnescapeEscapedEqualsOriginal(string unescaped)
    {
        var s = MSBuildStringValue.FromUnescaped(unescaped);
        EscapingUtilities.UnescapeAll(s.Escaped).ShouldBe(unescaped);
    }

    [Theory]
    [InlineData("foo%3bbar")]
    [InlineData("%28paren%29")]
    [InlineData("plain")]
    [InlineData("")]
    public void FromEscaped_RoundTrip_EscapeUnescapedEqualsOriginal(string escaped)
    {
        var s = MSBuildStringValue.FromEscaped(escaped);
        EscapingUtilities.Escape(s.Unescaped).ShouldBe(escaped);
    }

    [Fact]
    public void Equals_String_MatchingEscapedValue_ReturnsTrue()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        a.Equals("foo%3bbar").ShouldBeTrue();
    }

    [Fact]
    public void Equals_String_NonMatchingValue_ReturnsFalse()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        a.Equals("foo;bar").ShouldBeFalse(); // unescaped form is not equal to escaped
        a.Equals("other").ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        MSBuildStringValue? a = null;
        MSBuildStringValue? b = null;
        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_LeftNull_ReturnsFalse()
    {
        MSBuildStringValue? a = null;
        var b = MSBuildStringValue.FromEscaped("foo");
        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_RightNull_ReturnsFalse()
    {
        var a = MSBuildStringValue.FromEscaped("foo");
        MSBuildStringValue? b = null;
        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_SameEscapedValue_ReturnsTrue()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        var b = MSBuildStringValue.FromUnescaped("foo;bar");
        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_DifferentEscapedValues_ReturnsFalse()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        var b = MSBuildStringValue.FromEscaped("other");
        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_MSBuildStringValue_String_MatchingEscapedValue_ReturnsTrue()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        (a == "foo%3bbar").ShouldBeTrue();
        (a != "foo%3bbar").ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_MSBuildStringValue_String_NonMatchingValue_ReturnsFalse()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        (a == "foo;bar").ShouldBeFalse(); // unescaped form does not compare equal
        (a != "foo;bar").ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_MSBuildStringValue_NullString_ReturnsFalse()
    {
        var a = MSBuildStringValue.FromEscaped("foo");
        (a == (string?)null).ShouldBeFalse();
        (a != (string?)null).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_NullMSBuildStringValue_NullString_ReturnsTrue()
    {
        MSBuildStringValue? a = null;
        (a == (string?)null).ShouldBeTrue();
        (a != (string?)null).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_String_MSBuildStringValue_MatchingEscapedValue_ReturnsTrue()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        ("foo%3bbar" == a).ShouldBeTrue();
        ("foo%3bbar" != a).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_String_MSBuildStringValue_NonMatchingValue_ReturnsFalse()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        ("other" == a).ShouldBeFalse();
        ("other" != a).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_NullString_NullMSBuildStringValue_ReturnsTrue()
    {
        MSBuildStringValue? a = null;
        ((string?)null == a).ShouldBeTrue();
        ((string?)null != a).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_String_MSBuildStringValue_IsSymmetric()
    {
        var a = MSBuildStringValue.FromEscaped("foo%3bbar");
        (a == "foo%3bbar").ShouldBe("foo%3bbar" == a);
        (a == "other").ShouldBe("other" == a);
    }
}
