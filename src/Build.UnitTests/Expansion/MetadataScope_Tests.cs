// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public sealed class MetadataScope_Tests
{
    [Fact]
    public void RestoresPreviousMetadata()
    {
        IMetadataTable originalMetadata = CreateMetadata("original");
        IMetadataTable scopedMetadata = CreateMetadata("scoped");
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander =
            ExpanderFactory.Create<ProjectPropertyInstance, ProjectItemInstance>(originalMetadata);

        using (expander.EnterMetadataScope(scopedMetadata))
        {
            expander.CurrentMetadata.ShouldBeSameAs(scopedMetadata);
            ExpandMetadata(expander).ShouldBe("scoped");
        }

        expander.CurrentMetadata.ShouldBeSameAs(originalMetadata);
        ExpandMetadata(expander).ShouldBe("original");
    }

    [Fact]
    public void NestedScopesRestoreInReverseOrder()
    {
        IMetadataTable outerMetadata = CreateMetadata("outer");
        IMetadataTable innerMetadata = CreateMetadata("inner");
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander =
            ExpanderFactory.Create(new PropertyDictionary<ProjectPropertyInstance>());

        using (expander.EnterMetadataScope(outerMetadata))
        {
            expander.CurrentMetadata.ShouldBeSameAs(outerMetadata);

            using (expander.EnterMetadataScope(innerMetadata))
            {
                expander.CurrentMetadata.ShouldBeSameAs(innerMetadata);
            }

            expander.CurrentMetadata.ShouldBeSameAs(outerMetadata);
        }

        expander.CurrentMetadata.ShouldBeNull();
    }

    [Fact]
    public void RestoresPreviousMetadataWhenExpansionThrows()
    {
        IMetadataTable originalMetadata = CreateMetadata("original");
        IMetadataTable scopedMetadata = CreateMetadata("scoped");
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander =
            ExpanderFactory.Create<ProjectPropertyInstance, ProjectItemInstance>(originalMetadata);

        Should.Throw<InvalidOperationException>(() => ThrowWithinMetadataScope(expander, scopedMetadata));

        expander.CurrentMetadata.ShouldBeSameAs(originalMetadata);
    }

    [Fact]
    public void ScopesMustBeDisposedInReverseOrder()
    {
        IMetadataTable outerMetadata = CreateMetadata("outer");
        IMetadataTable innerMetadata = CreateMetadata("inner");
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander =
            ExpanderFactory.Create(new PropertyDictionary<ProjectPropertyInstance>());
        MetadataScope outerScope = expander.EnterMetadataScope(outerMetadata);
        MetadataScope innerScope = expander.EnterMetadataScope(innerMetadata);

        InternalErrorException? exception = null;
        try
        {
            outerScope.Dispose();
        }
        catch (InternalErrorException ex)
        {
            exception = ex;
        }

        exception.ShouldNotBeNull();
        expander.CurrentMetadata.ShouldBeSameAs(innerMetadata);

        innerScope.Dispose();
        outerScope.Dispose();
        expander.CurrentMetadata.ShouldBeNull();
    }

    private static IMetadataTable CreateMetadata(string value)
        => new StringMetadataTable(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Value"] = value,
            });

    private static string ExpandMetadata(IExpander<ProjectPropertyInstance, ProjectItemInstance> expander)
        => expander.ExpandIntoStringLeaveEscaped(
            "%(Value)",
            ExpanderOptions.ExpandCustomMetadata,
            MockElementLocation.Instance)!;

    private static void ThrowWithinMetadataScope(
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander,
        IMetadataTable metadata)
    {
        using MetadataScope metadataScope = expander.EnterMetadataScope(metadata);
        throw new InvalidOperationException();
    }
}
