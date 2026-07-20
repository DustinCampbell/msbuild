// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Framework.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ResourceString_Tests
{
    private const string Name = "Sample";

    private static ResourceString CreateResource(string text)
        => CreateResource(text, out _);

    private static ResourceString CreateResource(string text, CultureInfo? culture)
        => CreateResource(text, culture, out _);

    private static ResourceString CreateResource(string text, out TestResourceManager manager)
        => CreateResource(text, culture: null, out manager);

    private static ResourceString CreateResource(string text, CultureInfo? culture, out TestResourceManager manager)
    {
        manager = TestResourceManager.Create(Name, text);
        var provider = new ResourceProvider(manager);

        return new(provider, Name, culture ?? CultureInfo.InvariantCulture);
    }

    /// <inheritdoc cref="CreateCultureAwareResource(CultureInfo?, out TestResourceManager)"/>
    private static ResourceString CreateCultureAwareResource()
        => CreateCultureAwareResource(culture: null, out _);

    /// <inheritdoc cref="CreateCultureAwareResource(CultureInfo?, out TestResourceManager)"/>
    private static ResourceString CreateCultureAwareResource(out TestResourceManager manager)
        => CreateCultureAwareResource(culture: null, out manager);

    /// <summary>
    ///  Creates a resource backed by a manager whose value is the requested culture's name, so a change in the
    ///  resolved culture yields an observably different value. The resource's culture is passed through as-is
    ///  (a <see langword="null"/> culture means "resolve against the ambient CurrentUICulture").
    /// </summary>
    private static ResourceString CreateCultureAwareResource(CultureInfo? culture, out TestResourceManager manager)
    {
        manager = TestResourceManager.CreateForCultures(
            Name,
            static c => c is { Name.Length: > 0 } ? c.Name : "invariant");
        var provider = new ResourceProvider(manager);

        return new(provider, Name, culture);
    }

    [Fact]
    public void Name_ReturnsResourceName()
    {
        ResourceString resource = CreateResource("anything");

        resource.Name.ShouldBe(Name);
    }

    [Fact]
    public void HelpKeyword_IsPrefixedResourceName()
    {
        ResourceString resource = CreateResource("anything");

        resource.HelpKeyword.ShouldBe($"MSBuild.{Name}");
    }

    [Fact]
    public void Text_LoadsFromProvider()
    {
        ResourceString resource = CreateResource("Loaded text");

        resource.Text.ShouldBe("Loaded text");
    }

    [Fact]
    public void Text_IsLoadedOnceAndCached()
    {
        ResourceString resource = CreateResource("Loaded text", out TestResourceManager manager);

        string first = resource.Text;
        string second = resource.Text;

        first.ShouldBeSameAs(second);
        manager.GetStringCallCount.ShouldBe(1);
    }

    [Fact]
    public void ToString_ReturnsText()
    {
        ResourceString resource = CreateResource("MSB1234: The message");

        resource.ToString().ShouldBe("MSB1234: The message");
    }

    [Fact]
    public void TextWithoutCode_StripsMessageCode()
    {
        ResourceString resource = CreateResource("MSB1234: The message");

        resource.TextWithoutCode.ShouldBe("The message");
    }

    [Fact]
    public void Code_ReturnsMessageCode()
    {
        ResourceString resource = CreateResource("MSB1234: The message");

        resource.Code.ShouldBe("MSB1234");
    }

    [Fact]
    public void TextWithoutCode_IsSameInstanceAsTextWhenNoCode()
    {
        ResourceString resource = CreateResource("No code here");

        resource.TextWithoutCode.ShouldBeSameAs(resource.Text);
    }

    [Fact]
    public void Code_IsNullWhenNoCode()
    {
        ResourceString resource = CreateResource("No code here");

        resource.Code.ShouldBeNull();
    }

    [Theory]
    [InlineData("MSB1234: The message", "MSB1234", "The message")]
    [InlineData("   MSB0001: Leading whitespace", "MSB0001", "Leading whitespace")]
    [InlineData("MSB1234:No space after colon", "MSB1234", "No space after colon")]
    [InlineData("MSB123: Too few digits", null, "MSB123: Too few digits")]
    [InlineData("MSB12345: Too many digits", null, "MSB12345: Too many digits")]
    [InlineData("XYZ1234: Wrong prefix", null, "XYZ1234: Wrong prefix")]
    [InlineData("MSB1234 No colon", null, "MSB1234 No colon")]
    public void ExtractsCodeAndText(string text, string? expectedCode, string expectedTextWithoutCode)
    {
        ResourceString resource = CreateResource(text);

        resource.Code.ShouldBe(expectedCode);
        resource.TextWithoutCode.ShouldBe(expectedTextWithoutCode);
    }

    [Fact]
    public void ParsedText_IsComputedOnceAndCached()
    {
        ResourceString resource = CreateResource("MSB1234: The message");

        resource.TextWithoutCode.ShouldBeSameAs(resource.TextWithoutCode);
    }

    [Fact]
    public void Format_KeepsCodeAndSubstitutesArgument()
    {
        ResourceString resource = CreateResource("MSB1234: Hello {0}");

        resource.Format("World").ShouldBe("MSB1234: Hello World");
    }

    [Fact]
    public void Format_SubstitutesTwoArguments()
    {
        ResourceString resource = CreateResource("{0} and {1}");

        resource.Format("a", "b").ShouldBe("a and b");
    }

    [Fact]
    public void Format_SubstitutesThreeArguments()
    {
        ResourceString resource = CreateResource("{0}, {1}, {2}");

        resource.Format("a", "b", "c").ShouldBe("a, b, c");
    }

    [Fact]
    public void Format_WithParamsArray_SubstitutesArguments()
    {
        ResourceString resource = CreateResource("{0}-{1}-{2}-{3}");

        resource.Format(["a", "b", "c", "d"]).ShouldBe("a-b-c-d");
    }

    [Fact]
    public void Format_WithEmptyParamsArray_ReturnsText()
    {
        ResourceString resource = CreateResource("MSB1234: unchanged");

        resource.Format([]).ShouldBe("MSB1234: unchanged");
    }

    [Fact]
    public void Format_UsesResourceCulture()
    {
        ResourceString resource = CreateResource("{0}", new CultureInfo("de-DE"));

        // German uses a comma as the decimal separator.
        resource.Format(1.5).ShouldBe("1,5");
    }

    [Fact]
    public void Format_WithExplicitCulture_UsesThatCulture()
    {
        ResourceString resource = CreateResource("{0}", CultureInfo.InvariantCulture);

        resource.Format(new CultureInfo("de-DE"), 1.5).ShouldBe("1,5");
    }

    [Fact]
    public void Format_WithExplicitCulture_SubstitutesTwoArguments()
    {
        ResourceString resource = CreateResource("{0} {1}");

        resource.Format(CultureInfo.InvariantCulture, "a", "b").ShouldBe("a b");
    }

    [Fact]
    public void Format_WithExplicitCulture_SubstitutesThreeArguments()
    {
        ResourceString resource = CreateResource("{0} {1} {2}");

        resource.Format(CultureInfo.InvariantCulture, "a", "b", "c").ShouldBe("a b c");
    }

    [Fact]
    public void Format_WithExplicitCultureAndParamsArray_SubstitutesArguments()
    {
        ResourceString resource = CreateResource("{0}-{1}-{2}-{3}");

        resource.Format(CultureInfo.InvariantCulture, ["a", "b", "c", "d"]).ShouldBe("a-b-c-d");
    }

    [Fact]
    public void Format_WithExplicitCultureAndEmptyParamsArray_ReturnsText()
    {
        ResourceString resource = CreateResource("MSB1234: unchanged");

        resource.Format(CultureInfo.InvariantCulture, []).ShouldBe("MSB1234: unchanged");
    }

    [Fact]
    public void FormatStripCode_RemovesCodeAndSubstitutesArgument()
    {
        ResourceString resource = CreateResource("MSB1234: Hello {0}");

        resource.FormatStripCode("World").ShouldBe("Hello World");
    }

    [Fact]
    public void FormatStripCode_SubstitutesTwoArguments()
    {
        ResourceString resource = CreateResource("MSB1234: {0} and {1}");

        resource.FormatStripCode("a", "b").ShouldBe("a and b");
    }

    [Fact]
    public void FormatStripCode_SubstitutesThreeArguments()
    {
        ResourceString resource = CreateResource("MSB1234: {0}, {1}, {2}");

        resource.FormatStripCode("a", "b", "c").ShouldBe("a, b, c");
    }

    [Fact]
    public void FormatStripCode_WithParamsArray_SubstitutesArguments()
    {
        ResourceString resource = CreateResource("MSB1234: {0}-{1}-{2}-{3}");

        resource.FormatStripCode(["a", "b", "c", "d"]).ShouldBe("a-b-c-d");
    }

    [Fact]
    public void FormatStripCode_WithEmptyParamsArray_ReturnsTextWithoutCode()
    {
        ResourceString resource = CreateResource("MSB1234: unchanged");

        resource.FormatStripCode([]).ShouldBe("unchanged");
    }

    [Fact]
    public void FormatStripCode_WithNoCode_SubstitutesArgument()
    {
        ResourceString resource = CreateResource("Hello {0}");

        resource.FormatStripCode("World").ShouldBe("Hello World");
    }

    [Fact]
    public void Text_WithAmbientCulture_ReResolvesWhenUICultureChanges()
    {
        ResourceString resource = CreateCultureAwareResource();

        using (new UICultureScope("fr-FR"))
        {
            resource.Text.ShouldBe("fr-FR");
        }

        using (new UICultureScope("de-DE"))
        {
            resource.Text.ShouldBe("de-DE");
        }
    }

    [Fact]
    public void Text_WithAmbientCulture_CachesWithinSameUICulture()
    {
        ResourceString resource = CreateCultureAwareResource(out TestResourceManager manager);

        using (new UICultureScope("fr-FR"))
        {
            string first = resource.Text;
            string second = resource.Text;

            first.ShouldBeSameAs(second);
            manager.GetStringCallCount.ShouldBe(1);
        }
    }

    [Fact]
    public void Text_WithExplicitCulture_IgnoresAmbientUICulture()
    {
        ResourceString resource = CreateCultureAwareResource(new CultureInfo("fr-FR"), out TestResourceManager manager);

        using (new UICultureScope("de-DE"))
        {
            resource.Text.ShouldBe("fr-FR");
            resource.Text.ShouldBe("fr-FR");
            manager.GetStringCallCount.ShouldBe(1);
        }
    }

    [Fact]
    public void ParsedText_WithAmbientCulture_ReResolvesWhenUICultureChanges()
    {
        var manager = TestResourceManager.CreateForCultures(
            Name,
            static c => c?.Name == "fr-FR" ? "MSB1000: Bonjour" : "MSB2000: Hallo");
        var resource = new ResourceString(new ResourceProvider(manager), Name, culture: null);

        using (new UICultureScope("fr-FR"))
        {
            resource.Code.ShouldBe("MSB1000");
            resource.TextWithoutCode.ShouldBe("Bonjour");
        }

        using (new UICultureScope("de-DE"))
        {
            resource.Code.ShouldBe("MSB2000");
            resource.TextWithoutCode.ShouldBe("Hallo");
        }
    }

    [Fact]
    public void Format_WithAmbientCulture_UsesAmbientUICultureForTemplate()
    {
        var manager = TestResourceManager.CreateForCultures(
            Name,
            static c => c?.Name == "fr-FR" ? "Bonjour {0}" : "Hallo {0}");
        var resource = new ResourceString(new ResourceProvider(manager), Name, culture: null);

        using (new UICultureScope("fr-FR"))
        {
            resource.Format("World").ShouldBe("Bonjour World");
        }

        using (new UICultureScope("de-DE"))
        {
            resource.Format("World").ShouldBe("Hallo World");
        }
    }

    /// <summary>
    ///  Temporarily sets the ambient UI culture, restoring the previous culture on dispose so tests can observe
    ///  culture-sensitive resolution without leaving process-global state changed.
    /// </summary>
    private readonly ref struct UICultureScope
    {
        private readonly CultureInfo _original = CultureInfo.CurrentUICulture;

        public UICultureScope(string cultureName)
            => CultureInfo.CurrentUICulture = new CultureInfo(cultureName);

        public readonly void Dispose()
            => CultureInfo.CurrentUICulture = _original;
    }
}
