// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

namespace Microsoft.Build.UnitTests.Evaluation;

[Trait("Category", "expansion")]
public class ItemSpec_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void EachFragmentTypeShouldContributeToItemSpecGlob()
    {
        var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(("foo", ["d", "e"])));

        var itemSpecGlob = itemSpec.ToMSBuildGlob();

        itemSpecGlob.IsMatch("a").ShouldBeTrue();
        itemSpecGlob.IsMatch("bar").ShouldBeTrue();
        itemSpecGlob.IsMatch("car").ShouldBeTrue();
        itemSpecGlob.IsMatch("d").ShouldBeTrue();
        itemSpecGlob.IsMatch("e").ShouldBeTrue();
    }

    [Fact]
    public void AbsolutePathsShouldMatch()
    {
        var absoluteRootPath = NativeMethodsShared.IsWindows
            ? @"c:\a\b"
            : "/a/b";

        var projectFile = Path.Combine(absoluteRootPath, "build.proj");
        var absoluteSpec = Path.Combine(absoluteRootPath, "s.cs");

        var itemSpecFromAbsolute = CreateItemSpecFrom(absoluteSpec, CreateExpander(), new MockElementLocation(projectFile));
        var itemSpecFromRelative = CreateItemSpecFrom("s.cs", CreateExpander(), new MockElementLocation(projectFile));

        itemSpecFromRelative.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
        itemSpecFromRelative.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();

        itemSpecFromAbsolute.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
        itemSpecFromAbsolute.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();
    }

    [Fact]
    public void FragmentGlobsWorkAfterStateIsPartiallyInitializedByOtherOperations()
    {
        var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(("foo", ["d", "e"])));

        // Partially initialize the lazy state in ItemExpressionFragment before creating the glob.
        itemSpec.FragmentsMatchingItem("e", out int matches);

        matches.ShouldBe(1);

        var itemSpecGlob = itemSpec.ToMSBuildGlob();

        itemSpecGlob.IsMatch("a").ShouldBeTrue();
        itemSpecGlob.IsMatch("bar").ShouldBeTrue();
        itemSpecGlob.IsMatch("car").ShouldBeTrue();
        itemSpecGlob.IsMatch("d").ShouldBeTrue();
        itemSpecGlob.IsMatch("e").ShouldBeTrue();
    }

    private static ItemSpec<ProjectPropertyInstance, ProjectItemInstance> CreateItemSpecFrom(
        string itemSpec,
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander,
        IElementLocation? location = null)
    {
        location ??= MockElementLocation.Instance;

        return new(itemSpec, expander, location, Path.GetDirectoryName(location.File));
    }

    private IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(params (string ItemType, string[] Includes)[] items)
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var (_, loggingContext) = CreateLoggingContext(_output);

        return ExpanderFactory.Create(Properties(), Items(project, items), loggingContext);
    }
}
