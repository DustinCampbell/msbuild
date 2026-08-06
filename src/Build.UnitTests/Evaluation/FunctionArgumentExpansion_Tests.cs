// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public class FunctionArgumentExpansion_Tests
{
    [Theory]
    [InlineData("$([System.Math]::Max(123, 456))", "456")]
    [InlineData("$([System.Math]::Min(123, 456))", "123")]
    [InlineData("$([System.Math]::Max(-1, 2.5))", "2.5")]
    [InlineData("$([System.String]::Concat('prefix', 'suffix'))", "prefixsuffix")]
    [InlineData("$([System.String]::Concat($([System.String]::Concat('prefix', '-')), 'suffix'))", "prefix-suffix")]
    [InlineData("$([MSBuild]::ValueOrDefault('', 'fallback'))", "fallback")]
    [InlineData("$([System.Convert]::ToInt32('42'))", "42")]
    [InlineData("$([System.String]::Concat(null, 'suffix'))", "suffix")]
    public void ExpandsLazyAndFallbackFunctionArguments(string expression, string expected)
    {
        Expand(expression).ShouldBe(expected);
    }

    [Fact]
    public void ExpandsPropertyBackedAndChainedArguments()
    {
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("Prefix", "prefix-"));
        properties.Set(ProjectPropertyInstance.Create("Suffix", "suffix"));
        properties.Set(ProjectPropertyInstance.Create("Text", "prefix-value"));

        Expand("$([System.String]::Concat('$(Prefix)', '$(Suffix)'))", properties).ShouldBe("prefix-suffix");
        Expand("$(Text.Substring(7).ToUpperInvariant())", properties).ShouldBe("VALUE");
        Expand("$(Text.Substring(0, 6))", properties).ShouldBe("prefix");
    }

    [Fact]
    public void UnescapesArgumentsAndEscapesFunctionResult()
    {
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("Prefix", "left%3bright"));

        string result = Expand("$([System.String]::Concat('$(Prefix)', '%3bsuffix'))", properties);

        result.ShouldBe(EscapingUtilities.Escape("left;right;suffix"));
    }

    private static string Expand(
        string expression,
        PropertyDictionary<ProjectPropertyInstance>? properties = null)
    {
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties ?? new PropertyDictionary<ProjectPropertyInstance>(),
            FileSystems.Default);

        return expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandProperties,
            ElementLocation.EmptyLocation);
    }
}
