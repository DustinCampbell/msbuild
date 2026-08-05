// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public class PropertyDictionary_Tests
{
    [Fact]
    public void SupportsConstrainedPropertyLookup()
    {
        MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;
        PropertyDictionary<ProjectPropertyInstance> dictionary = new(comparer);
        ProjectPropertyInstance property = ProjectPropertyInstance.Create("foo", "bar");
        dictionary.Set(property);

        const string expression = "$(foo)";
        ProjectPropertyInstance result = dictionary.GetProperty(expression, 2, 4);

        result.ShouldBeSameAs(property);
        comparer.GetHashCode(expression, 2, 3).ShouldBe(comparer.GetHashCode("foo"));
    }
}
