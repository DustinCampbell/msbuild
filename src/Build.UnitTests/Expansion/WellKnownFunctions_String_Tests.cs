// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_String_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(string), output)
{
    [Fact]
    public void Constructor_NoArguments()
        => Constructor.Invoke().ShouldBe(string.Empty);

    [Fact]
    public void Constructor_String()
        => Constructor.Invoke(["copy"]).ShouldBe("copy");

    [Fact]
    public void Constructor_Null()
        => Constructor.NotHandled([null]);

    [Fact]
    public void Constructor_NonString_NotHandled()
        => Constructor.NotHandled([1]);

    [Fact]
    public void Constructor_CharInt32_NotHandled()
        => Constructor.NotHandled(["a", "3"]);

    [Theory]
    [InlineData("abc", "b", true)]
    [InlineData("abc", "x", false)]
    public void String_Contains_String(string instance, string value, bool expected)
        => InstanceMember(nameof(string.Contains))
            .Invoke(instance, [value])
            .ShouldBe(expected);

    [Fact]
    public void String_Contains_NoArguments_NotHandled()
        => InstanceMember(nameof(string.Contains))
            .NotHandled("abc");

    [Fact]
    public void String_Compare_NotHandled()
        => StaticMember(nameof(string.Compare))
            .NotHandled(["a", "b"]);

    [Fact]
    public void String_Concat_NotHandled()
        => StaticMember(nameof(string.Concat))
            .NotHandled(["a", "b"]);

    [Fact]
    public void String_Copy_String()
        => StaticMember(nameof(string.Copy))
            .Invoke(["abc"])
            .ShouldBe("abc");

    [Fact]
    public void String_Copy_Null_NotHandled()
        => StaticMember(nameof(string.Copy))
            .NotHandled([null]);

    [Theory]
    [InlineData("abc", "bc", true)]
    [InlineData("abc", "ab", false)]
    public void String_EndsWith_String(string instance, string value, bool expected)
        => InstanceMember(nameof(string.EndsWith))
            .Invoke(instance, [value])
            .ShouldBe(expected);

    [Fact]
    public void String_EndsWith_StringComparison()
        => InstanceMember(nameof(string.EndsWith))
            .Invoke("abc", ["BC", "OrdinalIgnoreCase"])
            .ShouldBe(true);

    [Fact]
    public void String_EndsWith_InvalidComparison_NotHandled()
        => InstanceMember(nameof(string.EndsWith))
            .NotHandled("abc", ["bc", "InvalidComparison"]);

    [Fact]
    public void String_EndsWith_NoArguments_NotHandled()
        => InstanceMember(nameof(string.EndsWith))
            .NotHandled("abc");

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "ABC", false)]
    public void String_Equals_String(string instance, string value, bool expected)
        => InstanceMember(nameof(string.Equals))
            .Invoke(instance, [value])
            .ShouldBe(expected);

    [Fact]
    public void String_Equals_StringComparison_NotHandled()
        => InstanceMember(nameof(string.Equals))
            .NotHandled("abc", ["ABC", nameof(StringComparison.OrdinalIgnoreCase)]);

    [Fact]
    public void String_GetChars_Int32()
        => InstanceMember("get_Chars")
            .Invoke("abc", [1])
            .ShouldBe('b');

    [Fact]
    public void String_GetChars_InvalidIndex_NotHandled()
        => InstanceMember("get_Chars")
            .NotHandled("abc", ["invalid"]);

    [Fact]
    public void String_IndexOf_String_NotHandled()
        => InstanceMember(nameof(string.IndexOf))
            .NotHandled("abc", ["b"]);

    [Fact]
    public void String_IndexOf_StringComparison()
        => InstanceMember(nameof(string.IndexOf))
            .Invoke("abc", ["B", "OrdinalIgnoreCase"])
            .ShouldBe(1);

    [Fact]
    public void String_IndexOf_InvalidComparison_NotHandled()
        => InstanceMember(nameof(string.IndexOf))
            .NotHandled("abc", ["b", "InvalidComparison"]);

    [Fact]
    public void String_IndexOfAny_String()
        => InstanceMember(nameof(string.IndexOfAny))
            .Invoke("abc", ["xb"])
            .ShouldBe(1);

    [Theory]
    [InlineData("", true)]
    [InlineData("abc", false)]
    public void String_IsNullOrEmpty_String(string? value, bool expected)
        => StaticMember(nameof(string.IsNullOrEmpty))
            .Invoke([value])
            .ShouldBe(expected);

    [Fact]
    public void String_IsNullOrEmpty_Null()
        => StaticMember(nameof(string.IsNullOrEmpty))
            .NotHandled([null]);

    [Fact]
    public void String_IsNullOrEmpty_NoArguments_NotHandled()
        => StaticMember(nameof(string.IsNullOrEmpty))
            .NotHandled();

    [Theory]
    [InlineData("  ", true)]
    [InlineData("abc", false)]
    public void String_IsNullOrWhiteSpace_String(string? value, bool expected)
        => StaticMember(nameof(string.IsNullOrWhiteSpace))
            .Invoke([value])
            .ShouldBe(expected);

    [Fact]
    public void String_IsNullOrWhiteSpace_Null()
        => StaticMember(nameof(string.IsNullOrWhiteSpace))
            .NotHandled([null]);

    [Fact]
    public void String_IsNullOrWhiteSpace_NoArguments_NotHandled()
        => StaticMember(nameof(string.IsNullOrWhiteSpace))
            .NotHandled();

    [Fact]
    public void String_LastIndexOf_String()
        => InstanceMember(nameof(string.LastIndexOf))
            .Invoke("abcb", ["b"])
            .ShouldBe("abcb".LastIndexOf("b", StringComparison.CurrentCulture));

    [Fact]
    public void String_LastIndexOf_StringInt32()
        => InstanceMember(nameof(string.LastIndexOf))
            .Invoke("abcb", ["b", "2"])
            .ShouldBe("abcb".LastIndexOf("b", 2, StringComparison.CurrentCulture));

    [Fact]
    public void String_LastIndexOf_StringComparison()
        => InstanceMember(nameof(string.LastIndexOf))
            .Invoke("abcb", ["B", "OrdinalIgnoreCase"])
            .ShouldBe(3);

    [Fact]
    public void String_LastIndexOf_StringInt32Int32_NotHandled()
        => InstanceMember(nameof(string.LastIndexOf))
            .NotHandled("abcb", ["b", 3, 2]);

    [Fact]
    public void String_LastIndexOfAny_String()
        => InstanceMember(nameof(string.LastIndexOfAny))
            .Invoke("abcb", ["xb"])
            .ShouldBe(3);

    [Fact]
    public void String_Length()
        => InstanceMember(nameof(string.Length))
            .Invoke("abc")
            .ShouldBe(3);

    [Fact]
    public void String_Length_Arguments_NotHandled()
        => InstanceMember(nameof(string.Length))
            .NotHandled("abc", [1]);

    [Fact]
    public void String_PadLeft_Int32()
        => InstanceMember(nameof(string.PadLeft))
            .Invoke("abc", [5])
            .ShouldBe("  abc");

    [Fact]
    public void String_PadLeft_Int32Char()
        => InstanceMember(nameof(string.PadLeft))
            .Invoke("abc", [5, '.'])
            .ShouldBe("..abc");

    [Fact]
    public void String_PadLeft_InvalidWidth_NotHandled()
        => InstanceMember(nameof(string.PadLeft))
            .NotHandled("abc", ["invalid"]);

    [Fact]
    public void String_PadLeft_InvalidPaddingCharacter_NotHandled()
        => InstanceMember(nameof(string.PadLeft))
            .NotHandled("abc", [5, ".."]);

    [Fact]
    public void String_PadRight_Int32()
        => InstanceMember(nameof(string.PadRight))
            .Invoke("abc", [5])
            .ShouldBe("abc  ");

    [Fact]
    public void String_PadRight_Int32Char()
        => InstanceMember(nameof(string.PadRight))
            .Invoke("abc", [5, '.'])
            .ShouldBe("abc..");

    [Fact]
    public void String_PadRight_InvalidPaddingCharacter_NotHandled()
        => InstanceMember(nameof(string.PadRight))
            .NotHandled("abc", [5, ".."]);

    [Fact]
    public void String_PadRight_InvalidWidth_NotHandled()
        => InstanceMember(nameof(string.PadRight))
            .NotHandled("abc", ["invalid"]);

    [Fact]
    public void String_Replace_StringString()
        => InstanceMember(nameof(string.Replace))
            .Invoke("abc", ["b", "x"])
            .ShouldBe("axc");

    [Fact]
    public void String_Replace_CharChar_NotHandled()
        => InstanceMember(nameof(string.Replace))
            .NotHandled("abc", ['b', 'x']);

    [Fact]
    public void String_Split_Char()
        => InstanceMember(nameof(string.Split))
            .Invoke("a;b", [";"])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);

    [Fact]
    public void String_Split_MultipleCharacters_NotHandled()
        => InstanceMember(nameof(string.Split))
            .NotHandled("a::b", ["::"]);

    [Theory]
    [InlineData("abc", "ab", true)]
    [InlineData("abc", "bc", false)]
    public void String_StartsWith_String(string instance, string value, bool expected)
        => InstanceMember(nameof(string.StartsWith))
            .Invoke(instance, [value])
            .ShouldBe(expected);

    [Fact]
    public void String_StartsWith_StringComparison_NotHandled()
        => InstanceMember(nameof(string.StartsWith))
            .NotHandled("abc", ["AB", nameof(StringComparison.OrdinalIgnoreCase)]);

    [Fact]
    public void String_Substring_Int32()
        => InstanceMember(nameof(string.Substring))
            .Invoke("abc", [1])
            .ShouldBe("bc");

    [Fact]
    public void String_Substring_Int32Int32()
        => InstanceMember(nameof(string.Substring))
            .Invoke("abc", [1, 1])
            .ShouldBe("b");

    [Fact]
    public void String_Substring_InvalidStartIndex_NotHandled()
        => InstanceMember(nameof(string.Substring))
            .NotHandled("abc", ["invalid"]);

    [Fact]
    public void String_ToLower()
        => InstanceMember(nameof(string.ToLower))
            .Invoke("ABC")
            .ShouldBe("abc".ToLower());

    [Fact]
    public void String_ToLowerInvariant()
        => InstanceMember(nameof(string.ToLowerInvariant))
            .Invoke("ABC")
            .ShouldBe("abc");

    [Fact]
    public void String_ToUpperInvariant()
        => InstanceMember(nameof(string.ToUpperInvariant))
            .Invoke("abc")
            .ShouldBe("ABC");

    [Fact]
    public void String_ToUpperInvariant_CaseInsensitive()
        => InstanceMember("toupperinvariant")
            .Invoke("abc")
            .ShouldBe("ABC");

    [Fact]
    public void String_ToUpper_NotHandled()
        => InstanceMember(nameof(string.ToUpper))
            .NotHandled("abc");

    [Fact]
    public void String_Trim_NotHandled()
        => InstanceMember(nameof(string.Trim))
            .NotHandled(" abc ");

    [Fact]
    public void String_TrimEnd_String()
        => InstanceMember(nameof(string.TrimEnd))
            .Invoke("abcxx", ["x"])
            .ShouldBe("abc");

    [Fact]
    public void String_TrimEnd_EmptyString_NotHandled()
        => InstanceMember(nameof(string.TrimEnd))
            .NotHandled("abc", [""]);

    [Fact]
    public void String_TrimStart_String()
        => InstanceMember(nameof(string.TrimStart))
            .Invoke("xxabc", ["x"])
            .ShouldBe("abc");

    [Fact]
    public void String_TrimStart_EmptyString_NotHandled()
        => InstanceMember(nameof(string.TrimStart))
            .NotHandled("abc", [""]);
}
