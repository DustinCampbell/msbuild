// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class ExpanderMetadata_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpdateMetadataRestoresPreviousTable(bool hasInitialMetadata)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        IMetadataTable? initial = hasInitialMetadata ? CreateMetadata("initial") : null;
        var expander = CreateExpander(initial);
        IMetadataTable temporary = CreateMetadata("temporary");

        using (expander.UpdateMetadata(temporary))
        {
            expander.Metadata.ShouldBeSameAs(temporary);
            ExpandMetadata(expander).ShouldBe("temporary");
        }

        if (initial is null)
        {
            expander.Metadata.ShouldBeNull();
        }
        else
        {
            expander.Metadata.ShouldBeSameAs(initial);
            ExpandMetadata(expander).ShouldBe("initial");
        }
    }

    [Fact]
    public void NestedScopesRestoreMetadataInOrder()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        IMetadataTable initial = CreateMetadata("initial");
        var expander = CreateExpander(initial);

        using (expander.UpdateMetadata(CreateMetadata("outer")))
        {
            ExpandMetadata(expander).ShouldBe("outer");
            using (expander.UpdateMetadata(CreateMetadata("inner")))
            {
                ExpandMetadata(expander).ShouldBe("inner");
            }

            ExpandMetadata(expander).ShouldBe("outer");
        }

        expander.Metadata.ShouldBeSameAs(initial);
        ExpandMetadata(expander).ShouldBe("initial");
    }

    [Fact]
    public void ScopeCanTemporarilyClearMetadata()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        IMetadataTable initial = CreateMetadata("initial");
        var expander = CreateExpander(initial);

        using (expander.UpdateMetadata(null))
        {
            expander.Metadata.ShouldBeNull();
        }

        expander.Metadata.ShouldBeSameAs(initial);
        ExpandMetadata(expander).ShouldBe("initial");
    }

    [Fact]
    public void FailedExpansionRestoresMetadata()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        IMetadataTable initial = CreateMetadata("initial");
        var expander = CreateExpander(initial);

        Should.Throw<InvalidProjectFileException>(() =>
        {
            using var metadataScope = expander.UpdateMetadata(CreateMetadata("temporary"));
            ExpandMetadata(expander).ShouldBe("temporary");
            expander.ExpandIntoStringLeaveEscaped(
                "$([System.Int32]::Parse('not an integer'))",
                ExpanderOptions.ExpandProperties,
                MockElementLocation.Instance);
        });

        expander.Metadata.ShouldBeSameAs(initial);
        ExpandMetadata(expander).ShouldBe("initial");
    }

    private static Expander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(IMetadataTable? metadata)
        => new(new PropertyDictionary<ProjectPropertyInstance>(), items: null, metadata, FileSystems.Default);

    private static IMetadataTable CreateMetadata(string value)
        => new StringMetadataTable(new Dictionary<string, string>(MSBuildNameIgnoreCaseComparer.Default) { ["Value"] = value });

    private static string ExpandMetadata(Expander<ProjectPropertyInstance, ProjectItemInstance> expander)
        => expander.ExpandIntoStringLeaveEscaped("%(Value)", ExpanderOptions.ExpandMetadata, MockElementLocation.Instance);
}
