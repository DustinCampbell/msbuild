// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class PropertyExpansion_Tests
{
    /// <summary>
    ///  Expand property expressions into ProjectPropertyInstance items.
    /// </summary>
    [Fact]
    public void ExpandPropertiesIntoProjectPropertyInstances()
    {
        var expander = ExpanderFactory.Create(Properties(
            ("a", "aaa"),
            ("b", "bbb"),
            ("c", "cc;dd")));

        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var itemFactory = ItemFactory(project, itemType: "i");

        IList<ProjectItemInstance> items = expander.ExpandIntoItemsLeaveEscaped(
            "foo$(a);$(b);$(c);$(d",
            itemFactory,
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance).ShouldNotBeNull();

        items.Count.ShouldBe(5);
    }

    [Fact]
    public void ExpandEmptyPropertyExpressionToEmpty()
        => ExpandProperties("$()")
            .ShouldBeEmpty();

    // Compat hack: WebProjects may have an import with a condition like:
    //       Condition=" '$(Solutions.VSVersion)' == '8.0'"
    // These would have been '' in prior versions of msbuild but would be treated as a possible string function in current versions.
    // Be compatible by returning an empty string here.
    [Fact]
    public void Regress692569()
        => ExpandProperties("$(Solutions.VSVersion)")
            .ShouldBeEmpty();

    /// <summary>
    ///  Expand property function - properties won't get confused with a type or namespace.
    /// </summary>
    [Fact]
    public void PropertyFunctionNoCollisionsOnType()
        => ExpandProperties("$(System)", Properties("System", "The System Namespace"))
            .ShouldBe("The System Namespace");

    /// <summary>
    ///  Expand a property reference that has whitespace around the property name (should result in empty)
    /// </summary>
    [Fact]
    public void PropertySimpleSpaced()
        => ExpandProperties("$( SomeStuff )", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBeEmpty();
}
