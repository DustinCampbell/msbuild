// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using IProjectInstanceExpander =
    Microsoft.Build.Expansion.IExpander<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;
using ProjectInstanceItemSpec =
    Microsoft.Build.Evaluation.ItemSpec<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Evaluation
{
    [Trait("Category", "expansion")]
    public class ItemSpec_Tests
    {
        [Fact]
        public void EachFragmentTypeShouldContributeToItemSpecGlob()
        {
            var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> { { "foo", new[] { "d", "e" } } }));

            var itemSpecGlob = itemSpec.ToMSBuildGlob();

            Assert.True(itemSpecGlob.IsMatch("a"));
            Assert.True(itemSpecGlob.IsMatch("bar"));
            Assert.True(itemSpecGlob.IsMatch("car"));
            Assert.True(itemSpecGlob.IsMatch("d"));
            Assert.True(itemSpecGlob.IsMatch("e"));
        }

        [Fact]
        public void AbsolutePathsShouldMatch()
        {
            var absoluteRootPath = NativeMethodsShared.IsWindows
                ? @"c:\a\b"
                : "/a/b";

            var projectFile = Path.Combine(absoluteRootPath, "build.proj");
            var absoluteSpec = Path.Combine(absoluteRootPath, "s.cs");

            var itemSpecFromAbsolute = CreateItemSpecFrom(absoluteSpec, CreateExpander(new Dictionary<string, string[]>()), new MockElementLocation(projectFile));
            var itemSpecFromRelative = CreateItemSpecFrom("s.cs", CreateExpander(new Dictionary<string, string[]>()), new MockElementLocation(projectFile));

            itemSpecFromRelative.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
            itemSpecFromRelative.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();

            itemSpecFromAbsolute.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
            itemSpecFromAbsolute.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();
        }

        [Fact]
        public void FragmentGlobsWorkAfterStateIsPartiallyInitializedByOtherOperations()
        {
            var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> { { "foo", new[] { "d", "e" } } }));

            int matches;
            // cause partial Lazy state to initialize in the ItemExpressionFragment
            itemSpec.FragmentsMatchingItem("e", out matches);

            Assert.Equal(1, matches);

            var itemSpecGlob = itemSpec.ToMSBuildGlob();

            Assert.True(itemSpecGlob.IsMatch("a"));
            Assert.True(itemSpecGlob.IsMatch("bar"));
            Assert.True(itemSpecGlob.IsMatch("car"));
            Assert.True(itemSpecGlob.IsMatch("d"));
            Assert.True(itemSpecGlob.IsMatch("e"));
        }

        private ProjectInstanceItemSpec CreateItemSpecFrom(string itemSpec, IProjectInstanceExpander expander, IElementLocation location = null)
        {
            location ??= MockElementLocation.Instance;

            return new ProjectInstanceItemSpec(itemSpec, expander, location, Path.GetDirectoryName(location.File));
        }

        private IProjectInstanceExpander CreateExpander(Dictionary<string, string[]> items)
        {
            var itemDictionary = ToItemDictionary(items);

            return ExpanderFactory.Create(
                new PropertyDictionary<ProjectPropertyInstance>(),
                itemDictionary,
                new TestLoggingContext(null, new BuildEventContext(1, 2, 3, 4)));
        }

        private static ItemDictionary<ProjectItemInstance> ToItemDictionary(Dictionary<string, string[]> itemTypes)
        {
            var itemDictionary = new ItemDictionary<ProjectItemInstance>();

            var dummyProject = ProjectHelpers.CreateEmptyProjectInstance();

            foreach (var itemType in itemTypes)
            {
                foreach (var item in itemType.Value)
                {
                    itemDictionary.Add(new ProjectItemInstance(dummyProject, itemType.Key, item, dummyProject.FullPath));
                }
            }

            return itemDictionary;
        }
    }
}
