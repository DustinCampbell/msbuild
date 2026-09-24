// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    public void Constructor_Overloads()
    {
        Constructor.Invoke([new[] { 'a', 'b', 'c' }]).ShouldBe("abc");
        Constructor.Invoke([new[] { 'a', 'b', 'c' }, "1", "2"]).ShouldBe("bc");
        Constructor.Invoke(["a", "3"]).ShouldBe("aaa");
    }

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

#if NET
    [Fact]
    public void String_Contains_ModernOverloads()
    {
        InstanceMember(nameof(string.Contains)).Invoke("abc", ['b']).ShouldBe(true);
        InstanceMember(nameof(string.Contains)).Invoke("abc", ["B", StringComparison.OrdinalIgnoreCase]).ShouldBe(true);
        InstanceMember(nameof(string.Contains)).Invoke("abc", ['B', StringComparison.OrdinalIgnoreCase]).ShouldBe(true);
    }
#endif

    [Fact]
    public void String_Compare_Overloads()
    {
        StaticMember(nameof(string.Compare)).Invoke(["a", "b"]).ShouldBeOfType<int>().ShouldBeLessThan(0);
        StaticMember(nameof(string.Compare)).Invoke(["a", "A", "true"]).ShouldBe(0);
        StaticMember(nameof(string.Compare)).Invoke(["a", "A", StringComparison.OrdinalIgnoreCase]).ShouldBe(0);
        StaticMember(nameof(string.Compare)).Invoke(["a", "A", true, CultureInfo.InvariantCulture]).ShouldBe(0);
        StaticMember(nameof(string.Compare)).Invoke(["a", "A", CultureInfo.InvariantCulture, CompareOptions.IgnoreCase]).ShouldBe(0);
        StaticMember(nameof(string.Compare)).Invoke(["xa", "1", "yb", "1", "1"]).ShouldBeOfType<int>().ShouldBeLessThan(0);
        StaticMember(nameof(string.Compare)).Invoke(["xa", "1", "yA", "1", "1", "true"]).ShouldBe(0);
        StaticMember(nameof(string.Compare)).Invoke(["xa", "1", "yA", "1", "1", StringComparison.OrdinalIgnoreCase]).ShouldBe(0);
        StaticMember(nameof(string.Compare)).Invoke(["xa", "1", "yA", "1", "1", true, CultureInfo.InvariantCulture]).ShouldBe(0);
        StaticMember(nameof(string.Compare))
            .Invoke(["xa", "1", "yA", "1", "1", CultureInfo.InvariantCulture, CompareOptions.IgnoreCase])
            .ShouldBe(0);
    }

    [Fact]
    public void String_Concat_Overloads()
    {
        StaticMember(nameof(string.Concat)).Invoke(["a"]).ShouldBe("a");
        StaticMember(nameof(string.Concat)).Invoke(["a", "b"]).ShouldBe("ab");
        StaticMember(nameof(string.Concat)).Invoke(["a", 1]).ShouldBe("a1");
        StaticMember(nameof(string.Concat)).Invoke(["a", "b", "c"]).ShouldBe("abc");
        StaticMember(nameof(string.Concat)).Invoke(["a", 1, true]).ShouldBe("a1True");
        StaticMember(nameof(string.Concat)).Invoke(["a", "b", "c", "d"]).ShouldBe("abcd");
        StaticMember(nameof(string.Concat)).Invoke([new[] { "a", "b" }]).ShouldBe("ab");
        StaticMember(nameof(string.Concat)).Invoke([new object[] { "a", 1 }]).ShouldBe("a1");
        StaticMember(nameof(string.Concat)).Invoke([new List<string> { "a", "b" }]).ShouldBe("ab");
    }

    [Fact]
    public void String_CompareOrdinal_Overloads()
    {
        StaticMember(nameof(string.CompareOrdinal)).Invoke(["a", "b"]).ShouldBeOfType<int>().ShouldBeLessThan(0);
        StaticMember(nameof(string.CompareOrdinal)).Invoke(["xa", "1", "yb", "1", "1"]).ShouldBeOfType<int>().ShouldBeLessThan(0);
    }

    [Fact]
    public void String_Format_Overloads()
    {
        StaticMember(nameof(string.Format)).Invoke(["{0}", 1]).ShouldBe("1");
        StaticMember(nameof(string.Format)).Invoke(["{0}{1}", 1, 2]).ShouldBe("12");
        StaticMember(nameof(string.Format)).Invoke(["{0}{1}{2}", 1, 2, 3]).ShouldBe("123");
        StaticMember(nameof(string.Format)).Invoke(["{0}{1}{2}{3}", 1, 2, 3, 4]).ShouldBe("1234");
        StaticMember(nameof(string.Format)).Invoke(["{0}{1}", new object[] { 1, 2 }]).ShouldBe("12");
        StaticMember(nameof(string.Format)).Invoke([CultureInfo.InvariantCulture, "{0:F1}", 1.24]).ShouldBe("1.2");
        StaticMember(nameof(string.Format)).Invoke([CultureInfo.InvariantCulture, "{0}{1}", 1, 2]).ShouldBe("12");
        StaticMember(nameof(string.Format)).Invoke([CultureInfo.InvariantCulture, "{0}{1}{2}", 1, 2, 3]).ShouldBe("123");
        StaticMember(nameof(string.Format)).Invoke([CultureInfo.InvariantCulture, "{0}{1}", new object[] { 1, 2 }]).ShouldBe("12");
    }

    [Fact]
    public void String_Join_Overloads()
    {
        StaticMember(nameof(string.Join)).Invoke([",", new[] { "a", "b" }]).ShouldBe("a,b");
        StaticMember(nameof(string.Join)).Invoke([",", new[] { "a", "b", "c" }, 1, 2]).ShouldBe("b,c");
        StaticMember(nameof(string.Join)).Invoke([",", new object[] { "a", 1 }]).ShouldBe("a,1");
        StaticMember(nameof(string.Join)).Invoke([",", new List<string> { "a", "b" }]).ShouldBe("a,b");
        StaticMember(nameof(string.Join)).Invoke([",", "a", "b"]).ShouldBe("a,b");
        StaticMember(nameof(string.Join)).Invoke([",", "a", 1]).ShouldBe("a,1");
#if NET
        StaticMember(nameof(string.Join)).Invoke([',', new[] { "a", "b" }]).ShouldBe("a,b");
        StaticMember(nameof(string.Join)).Invoke([',', new[] { "a", "b", "c" }, 1, 2]).ShouldBe("b,c");
        StaticMember(nameof(string.Join)).Invoke([',', new object[] { "a", 1 }]).ShouldBe("a,1");
        StaticMember(nameof(string.Join)).Invoke([',', "a", 1]).ShouldBe("a,1");
#endif
    }

    [Fact]
    public void String_InterningAndOperators()
    {
        string value = new(['a', 'b', 'c']);

        StaticMember(nameof(string.Intern)).Invoke([value]).ShouldBeSameAs(string.Intern(value));
        StaticMember(nameof(string.IsInterned)).Invoke([value]).ShouldBe(string.IsInterned(value));
        StaticMember("op_Equality").Invoke(["abc", "abc"]).ShouldBe(true);
        StaticMember("op_Inequality").Invoke(["abc", "def"]).ShouldBe(true);
    }

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

    [Theory]
    [InlineData("OrdinalIgnoreCase")]
    [InlineData(StringComparison.OrdinalIgnoreCase)]
    public void String_EndsWith_StringComparison(object comparison)
        => InstanceMember(nameof(string.EndsWith))
            .Invoke("abc", ["BC", comparison])
            .ShouldBe(true);

    [Fact]
    public void String_EndsWith_InvalidComparison_NotHandled()
        => InstanceMember(nameof(string.EndsWith))
            .NotHandled("abc", ["bc", "InvalidComparison"]);

    [Fact]
    public void String_EndsWith_NoArguments_NotHandled()
        => InstanceMember(nameof(string.EndsWith))
            .NotHandled("abc");

    [Fact]
    public void String_EndsWith_CultureOverload()
        => InstanceMember(nameof(string.EndsWith))
            .Invoke("abc", ["BC", true, CultureInfo.InvariantCulture])
            .ShouldBe(true);

#if NET
    [Fact]
    public void String_EndsWith_Char()
        => InstanceMember(nameof(string.EndsWith))
            .Invoke("abc", ['c'])
            .ShouldBe(true);
#endif

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "ABC", false)]
    public void String_Equals_String(string instance, string value, bool expected)
        => InstanceMember(nameof(string.Equals))
            .Invoke(instance, [value])
            .ShouldBe(expected);

    [Fact]
    public void String_Equals_StringComparison()
        => InstanceMember(nameof(string.Equals))
            .Invoke("abc", ["ABC", nameof(StringComparison.OrdinalIgnoreCase)])
            .ShouldBe(true);

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
    public void String_IndexOf_String()
        => InstanceMember(nameof(string.IndexOf))
            .Invoke("abc", ["b"])
            .ShouldBe(1);

    [Theory]
    [InlineData("OrdinalIgnoreCase")]
    [InlineData(StringComparison.OrdinalIgnoreCase)]
    public void String_IndexOf_StringComparison(object comparison)
        => InstanceMember(nameof(string.IndexOf))
            .Invoke("abc", ["B", comparison])
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

    [Fact]
    public void String_IndexOfAny_Overloads()
    {
        InstanceMember(nameof(string.IndexOfAny)).Invoke("abcabc", [new[] { 'b', 'x' }]).ShouldBe(1);
        InstanceMember(nameof(string.IndexOfAny)).Invoke("abcabc", ["bx", 2]).ShouldBe(4);
        InstanceMember(nameof(string.IndexOfAny)).Invoke("abcabc", ["bx", 2, 2]).ShouldBe(-1);
    }

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
            .Invoke([null])
            .ShouldBe(true);

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
            .Invoke([null])
            .ShouldBe(true);

    [Fact]
    public void String_IsNullOrWhiteSpace_NoArguments_NotHandled()
        => StaticMember(nameof(string.IsNullOrWhiteSpace))
            .NotHandled();

    [Fact]
    public void String_LastIndexOf_String()
        => InstanceMember(nameof(string.LastIndexOf))
            .Invoke("abcb", ["b"])
            .ShouldBe("abcb".LastIndexOf("b", StringComparison.CurrentCulture));

    [Theory]
    [InlineData("2")]
    [InlineData(2)]
    public void String_LastIndexOf_StringInt32(object startIndex)
        => InstanceMember(nameof(string.LastIndexOf))
            .Invoke("abcb", ["b", startIndex])
            .ShouldBe("abcb".LastIndexOf("b", 2, StringComparison.CurrentCulture));

    [Theory]
    [InlineData("OrdinalIgnoreCase")]
    [InlineData(StringComparison.OrdinalIgnoreCase)]
    public void String_LastIndexOf_StringComparison(object comparison)
        => InstanceMember(nameof(string.LastIndexOf))
            .Invoke("abcb", ["B", comparison])
            .ShouldBe(3);

    [Fact]
    public void String_LastIndexOf_StringInt32Int32()
        => InstanceMember(nameof(string.LastIndexOf))
            .Invoke("abcb", ["b", 3, 2])
            .ShouldBe(3);

    [Fact]
    public void String_LastIndexOfAny_String()
        => InstanceMember(nameof(string.LastIndexOfAny))
            .Invoke("abcb", ["xb"])
            .ShouldBe(3);

    [Fact]
    public void String_LastIndexOf_AdditionalOverloads()
    {
        InstanceMember(nameof(string.LastIndexOf)).Invoke("abcabc", ['b']).ShouldBe(4);
        InstanceMember(nameof(string.LastIndexOf)).Invoke("abcabc", ['b', 3]).ShouldBe(1);
        InstanceMember(nameof(string.LastIndexOf)).Invoke("abcabc", ['b', 5, 3]).ShouldBe(4);
        InstanceMember(nameof(string.LastIndexOf)).Invoke("abcabc", ["B", 5, StringComparison.OrdinalIgnoreCase]).ShouldBe(4);
        InstanceMember(nameof(string.LastIndexOf)).Invoke("abcabc", ["B", 5, 3, StringComparison.OrdinalIgnoreCase]).ShouldBe(4);
    }

    [Fact]
    public void String_LastIndexOfAny_Overloads()
    {
        InstanceMember(nameof(string.LastIndexOfAny)).Invoke("abcabc", [new[] { 'b', 'x' }]).ShouldBe(4);
        InstanceMember(nameof(string.LastIndexOfAny)).Invoke("abcabc", ["bx", 3]).ShouldBe(1);
        InstanceMember(nameof(string.LastIndexOfAny)).Invoke("abcabc", ["bx", 5, 3]).ShouldBe(4);
    }

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
    public void String_Replace_CharChar()
        => InstanceMember(nameof(string.Replace))
            .Invoke("abc", ['b', 'x'])
            .ShouldBe("axc");

#if NET
    [Fact]
    public void String_Replace_ModernOverloads()
    {
        InstanceMember(nameof(string.Replace)).Invoke("aBc", ["b", "x", StringComparison.OrdinalIgnoreCase]).ShouldBe("axc");
        InstanceMember(nameof(string.Replace)).Invoke("aBc", ["b", "x", true, CultureInfo.InvariantCulture]).ShouldBe("axc");
    }
#endif

    [Fact]
    public void String_Remove_Overloads()
    {
        InstanceMember(nameof(string.Remove)).Invoke("abcd", [2]).ShouldBe("ab");
        InstanceMember(nameof(string.Remove)).Invoke("abcd", [1, 2]).ShouldBe("ad");
    }

    [Theory]
    [InlineData(";")]
    [InlineData(';')]
    public void String_Split_Char(object separator)
        => InstanceMember(nameof(string.Split))
            .Invoke("a;b", [separator])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);

    [Fact]
    public void String_Split_MultipleCharacters()
        => InstanceMember(nameof(string.Split))
            .Invoke("a::b", ["::"])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "", "b"]);

    [Fact]
    public void String_Split_ArrayOverloads()
    {
        char[] separators = [',', ';'];
        string[] stringSeparators = [",", ";"];

        InstanceMember(nameof(string.Split)).Invoke("a b").ShouldBeOfType<string[]>().ShouldBe(["a", "b"]);
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [separators, 2]).ShouldBeOfType<string[]>().ShouldBe(["a", ",b"]);
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [separators, StringSplitOptions.RemoveEmptyEntries])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [separators, 2, StringSplitOptions.RemoveEmptyEntries])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [stringSeparators, StringSplitOptions.RemoveEmptyEntries])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [stringSeparators, 2, StringSplitOptions.RemoveEmptyEntries])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);
    }

#if NET
    [Fact]
    public void String_Split_ModernOverloads()
    {
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [",", StringSplitOptions.RemoveEmptyEntries])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [",", 2, StringSplitOptions.RemoveEmptyEntries])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [',', StringSplitOptions.RemoveEmptyEntries])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);
        InstanceMember(nameof(string.Split)).Invoke("a,,b", [',', 2, StringSplitOptions.RemoveEmptyEntries])
            .ShouldBeOfType<string[]>()
            .ShouldBe(["a", "b"]);
    }
#endif

    [Theory]
    [InlineData("abc", "ab", true)]
    [InlineData("abc", "bc", false)]
    public void String_StartsWith_String(string instance, string value, bool expected)
        => InstanceMember(nameof(string.StartsWith))
            .Invoke(instance, [value])
            .ShouldBe(expected);

    [Fact]
    public void String_StartsWith_StringComparison()
        => InstanceMember(nameof(string.StartsWith))
            .Invoke("abc", ["AB", nameof(StringComparison.OrdinalIgnoreCase)])
            .ShouldBe(true);

    [Fact]
    public void String_StartsWith_CultureOverload()
        => InstanceMember(nameof(string.StartsWith))
            .Invoke("abc", ["AB", true, CultureInfo.InvariantCulture])
            .ShouldBe(true);

#if NET
    [Fact]
    public void String_StartsWith_Char()
        => InstanceMember(nameof(string.StartsWith))
            .Invoke("abc", ['a'])
            .ShouldBe(true);
#endif

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
    public void String_CultureAwareCasing()
    {
        InstanceMember(nameof(string.ToLower)).Invoke("ABC", [CultureInfo.InvariantCulture]).ShouldBe("abc");
        InstanceMember(nameof(string.ToUpper)).Invoke("abc", [CultureInfo.InvariantCulture]).ShouldBe("ABC");
    }

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
    public void String_ToUpper()
        => InstanceMember(nameof(string.ToUpper))
            .Invoke("abc")
            .ShouldBe("ABC");

    [Fact]
    public void String_Trim()
        => InstanceMember(nameof(string.Trim))
            .Invoke(" abc ")
            .ShouldBe("abc");

    [Fact]
    public void String_UtilityMembers()
    {
        InstanceMember(nameof(string.Clone)).Invoke("abc").ShouldBe("abc");
        InstanceMember(nameof(string.GetHashCode)).Invoke("abc").ShouldBe("abc".GetHashCode());
#if NET
        InstanceMember(nameof(string.GetHashCode)).Invoke("abc", [StringComparison.Ordinal]).ShouldBe("abc".GetHashCode(StringComparison.Ordinal));
#endif
        InstanceMember(nameof(string.IsNormalized)).Invoke("abc").ShouldBe(true);
        InstanceMember(nameof(string.IsNormalized)).Invoke("abc", [NormalizationForm.FormC]).ShouldBe(true);
        InstanceMember(nameof(string.Normalize)).Invoke("e\u0301").ShouldBe("\u00E9");
        InstanceMember(nameof(string.Normalize)).Invoke("e\u0301", [NormalizationForm.FormD]).ShouldBe("e\u0301");
        InstanceMember(nameof(string.GetEnumerator)).Invoke("abc").ShouldBeOfType<CharEnumerator>();
        InstanceMember(nameof(string.ToString)).Invoke("abc", [CultureInfo.InvariantCulture]).ShouldBe("abc");
    }

    [Fact]
    public void String_CopyTo()
    {
        char[] destination = ['.', '.', '.', '.'];

        InstanceMember(nameof(string.CopyTo)).Invoke("abc", [1, destination, 1, 2]).ShouldBeNull();

        destination.ShouldBe(['.', 'b', 'c', '.']);
    }

#if NET
    [Fact]
    public void String_ReplaceLineEndings_Overloads()
    {
        InstanceMember("ReplaceLineEndings").Invoke("a\r\nb").ShouldBe($"a{Environment.NewLine}b");
        InstanceMember("ReplaceLineEndings").Invoke("a\r\nb", ["|"]).ShouldBe("a|b");
    }
#endif

    [Fact]
    public void String_Trim_Overloads()
    {
        InstanceMember(nameof(string.Trim)).Invoke("xxabcxx", [new[] { 'x' }]).ShouldBe("abc");
        InstanceMember(nameof(string.TrimStart)).Invoke(" abc ").ShouldBe("abc ");
        InstanceMember(nameof(string.TrimStart)).Invoke("xxabc", [new[] { 'x' }]).ShouldBe("abc");
        InstanceMember(nameof(string.TrimEnd)).Invoke(" abc ").ShouldBe(" abc");
        InstanceMember(nameof(string.TrimEnd)).Invoke("abcxx", [new[] { 'x' }]).ShouldBe("abc");
        InstanceMember(nameof(string.Trim)).Invoke("xxabcxx", ['x']).ShouldBe("abc");
        InstanceMember(nameof(string.TrimStart)).Invoke("xxabc", ['x']).ShouldBe("abc");
        InstanceMember(nameof(string.TrimEnd)).Invoke("abcxx", ['x']).ShouldBe("abc");
    }

    [Fact]
    public void String_AdditionalMembers()
    {
        StaticMember(nameof(string.Equals)).Invoke(["abc", "ABC", "OrdinalIgnoreCase"]).ShouldBe(true);
        InstanceMember(nameof(string.CompareTo)).Invoke("abc", ["abd"]).ShouldBeOfType<int>().ShouldBeLessThan(0);
        InstanceMember(nameof(string.GetTypeCode)).Invoke("abc").ShouldBe(TypeCode.String);
        InstanceMember(nameof(string.Insert)).Invoke("bc", ["0", "a"]).ShouldBe("abc");
        InstanceMember(nameof(string.Length)).Invoke("abc").ShouldBe(3);
        InstanceMember("get_Length").Invoke("abc").ShouldBe(3);
        InstanceMember(nameof(string.Split)).Invoke("a;b", [new[] { ';' }]).ShouldBeOfType<string[]>().ShouldBe(["a", "b"]);
        InstanceMember(nameof(string.ToCharArray)).Invoke("abc").ShouldBeOfType<char[]>().ShouldBe(['a', 'b', 'c']);
        InstanceMember(nameof(string.ToCharArray)).Invoke("abc", ["1", "2"]).ShouldBeOfType<char[]>().ShouldBe(['b', 'c']);
        InstanceMember(nameof(string.ToString)).Invoke("abc").ShouldBe("abc");
        InstanceMember(nameof(string.Trim)).Invoke("xxabcxx", [new[] { 'x' }]).ShouldBe("abc");
    }

    [Fact]
    public void String_IndexOf_Overloads()
    {
        InstanceMember(nameof(string.IndexOf)).Invoke("abc", ["b"]).ShouldBe(1);
        InstanceMember(nameof(string.IndexOf)).Invoke("abc", ["b", "1"]).ShouldBe(1);
        InstanceMember(nameof(string.IndexOf)).Invoke("abc", ["B", "OrdinalIgnoreCase"]).ShouldBe(1);
        InstanceMember(nameof(string.IndexOf)).Invoke("abc", ["b", "0", "2"]).ShouldBe(1);
        InstanceMember(nameof(string.IndexOf)).Invoke("abc", ["B", "0", "OrdinalIgnoreCase"]).ShouldBe(1);
        InstanceMember(nameof(string.IndexOf)).Invoke("abc", ["B", "0", "2", "OrdinalIgnoreCase"]).ShouldBe(1);
    }

    [Fact]
    public void String_IndexOf_CharOverloads()
    {
        InstanceMember(nameof(string.IndexOf)).Invoke("abcabc", ['b']).ShouldBe(1);
        InstanceMember(nameof(string.IndexOf)).Invoke("abcabc", ['b', 2]).ShouldBe(4);
        InstanceMember(nameof(string.IndexOf)).Invoke("abcabc", ['b', 2, 3]).ShouldBe(4);
#if NET
        InstanceMember(nameof(string.IndexOf)).Invoke("abcabc", ['B', StringComparison.OrdinalIgnoreCase]).ShouldBe(1);
#endif
    }

    [Fact]
    public void String_TrimEnd_String()
        => InstanceMember(nameof(string.TrimEnd))
            .Invoke("abcxx", ["x"])
            .ShouldBe("abc");

    [Fact]
    public void String_TrimEnd_EmptyString()
        => InstanceMember(nameof(string.TrimEnd))
            .Invoke(" abc ", [""])
            .ShouldBe(" abc");

    [Fact]
    public void String_TrimStart_String()
        => InstanceMember(nameof(string.TrimStart))
            .Invoke("xxabc", ["x"])
            .ShouldBe("abc");

    [Fact]
    public void String_TrimStart_EmptyString()
        => InstanceMember(nameof(string.TrimStart))
            .Invoke(" abc ", [""])
            .ShouldBe("abc ");
}
