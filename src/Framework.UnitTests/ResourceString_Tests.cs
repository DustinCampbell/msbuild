// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Resources;
using Microsoft.Build.Framework.Resources;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ResourceStringTests
{
    [Fact]
    public void ResolveCachesParsedResourceByCulture()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
        var resourceManager = new TestResourceManager(
            static (_, requestedCulture) => requestedCulture.Name switch
            {
                "en-US" => "MSB1234: Hello",
                "fr-FR" => "MSB1234: Bonjour",
                _ => throw new InvalidOperationException(),
            });
        var resource = new ResourceString(resourceManager, () => culture, "Dotted.Resource.Key", "MSBuild.");

        ResolvedResourceString first = resource.Resolve();
        ResolvedResourceString second = resource.Resolve();

        first.Text.ShouldBe("Hello");
        first.Code.ShouldBe("MSB1234");
        first.HelpKeyword.ShouldBe("MSBuild.Dotted.Resource.Key");
        second.Text.ShouldBe(first.Text);
        resourceManager.GetStringCallCount.ShouldBe(1);

        culture = CultureInfo.GetCultureInfo("fr-FR");
        ResolvedResourceString french = resource.Resolve();

        french.Text.ShouldBe("Bonjour");
        french.Code.ShouldBe("MSB1234");
        french.HelpKeyword.ShouldBe("MSBuild.Dotted.Resource.Key");
        resourceManager.GetStringCallCount.ShouldBe(2);
    }

    [Fact]
    public void ResolvePreservesTextWithoutDiagnosticCode()
    {
        var resource = new ResourceString(
            new TestResourceManager(static (_, _) => "Plain text"),
            getCulture: null,
            "Resource_Name",
            "Custom.");

        ResolvedResourceString resolved = resource.Resolve();

        resolved.Text.ShouldBe("Plain text");
        resolved.Code.ShouldBeNull();
        resolved.HelpKeyword.ShouldBe("Custom.Resource_Name");
    }

    [Fact]
    public void WithArgumentsDefersFormattingUntilResolve()
    {
        var resourceManager = new TestResourceManager(static (_, _) => "MSB4321: {0} {1} {2} {3}");
        var resource = new ResourceString(resourceManager, getCulture: null, "Formatted.Resource", "MSBuild.");

        FormattedResourceString<string, int, string, int> formatted = resource.WithArguments("one", 2, "three", 4);

        resourceManager.GetStringCallCount.ShouldBe(0);
        ResolvedResourceString resolved = formatted.Resolve();

        resolved.Text.ShouldBe("one 2 three 4");
        resolved.Code.ShouldBe("MSB4321");
        resolved.HelpKeyword.ShouldBe("MSBuild.Formatted.Resource");
        resourceManager.GetStringCallCount.ShouldBe(1);
    }

    [Fact]
    public void FormattedResourceStringsAreValueTypes()
    {
        typeof(FormattedResourceString<string>).IsValueType.ShouldBeTrue();
        typeof(FormattedResourceString<string, string, string, string, string, string, string>).IsValueType.ShouldBeTrue();
        typeof(FormattedResourceString<string>).GetInterfaces().ShouldContain(typeof(IResourceString));
    }

    private sealed class TestResourceManager(Func<string, CultureInfo, string?> getString) : ResourceManager
    {
        public int GetStringCallCount { get; private set; }

        public override string? GetString(string name, CultureInfo? culture)
        {
            GetStringCallCount++;
            return getString(name, culture ?? CultureInfo.CurrentUICulture);
        }
    }
}
