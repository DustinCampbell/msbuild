// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.BackEnd;
using static Microsoft.Build.UnitTests.Evaluation.ExpansionHelpers;
using ProjectItemInstanceFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.ProjectItemInstanceFactory;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Creates declarative property, item, and metadata inputs for expansion tests.
/// </summary>
internal sealed class ExpansionTestContext
{
    /// <summary>
    ///  Gets the project that owns items and item factories created by this context.
    /// </summary>
    public ProjectInstance Project { get; } = ProjectHelpers.CreateEmptyProjectInstance();

    /// <summary>
    ///  Creates an item factory associated with this context's project.
    /// </summary>
    public ProjectItemInstanceFactory ItemFactory(string? itemType = null)
        => itemType is null
            ? new(Project)
            : new(Project, itemType);

    /// <summary>
    ///  Creates an item associated with this context's project.
    /// </summary>
    public ProjectItemInstance Item(string itemType, string includeEscaped)
        => new(Project, itemType, includeEscaped, Project.FullPath);

    /// <summary>
    ///  Creates an item provider containing one item.
    /// </summary>
    public IItemProvider<ProjectItemInstance> Items(string itemType, string includeEscaped)
        => Items((itemType, includeEscaped));

    /// <summary>
    ///  Creates an item provider from item types and escaped includes.
    /// </summary>
    public IItemProvider<ProjectItemInstance> Items(params ImmutableArray<(string ItemType, string IncludeEscaped)> items)
        => items.Length > 0
            ? ItemDictionary.Create([.. items.Select(i => Item(i.ItemType, i.IncludeEscaped))])
            : EmptyItems<ProjectItemInstance>();

    /// <summary>
    ///  Creates an item provider from items associated with this context's project.
    /// </summary>
    public IItemProvider<ProjectItemInstance> Items(params ReadOnlySpan<ProjectItemInstance> items)
    {
        foreach (ProjectItemInstance item in items)
        {
            if (!ReferenceEquals(item.Project, Project))
            {
                throw new ArgumentException("All items must belong to this test project.", nameof(items));
            }
        }

        return Items<ProjectItemInstance>(items);
    }

    /// <summary>
    ///  Creates an item provider from the specified items.
    /// </summary>
    public IItemProvider<T> Items<T>(params ReadOnlySpan<T> items)
        where T : class, IKeyed, IItem
        => items.Length > 0
            ? ItemDictionary.Create(items)
            : EmptyItems<T>();

    /// <summary>
    ///  Creates a metadata table containing one metadatum.
    /// </summary>
    public IMetadataTable Metadata(string name, string value)
        => Metadata((name, value));

    /// <summary>
    ///  Creates a metadata table from metadata names and values.
    /// </summary>
    public IMetadataTable Metadata(params ReadOnlySpan<(string Name, string Value)> metadata)
    {
        if (metadata.Length == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> map = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (var (name, value) in metadata)
        {
            map[name] = value;
        }

        return new StringMetadataTable(map);
    }

    /// <summary>
    ///  Creates a property provider containing one property.
    /// </summary>
    public IPropertyProvider<ProjectPropertyInstance> Properties(string name, string value)
        => PropertyDictionary.Create([ProjectPropertyInstance.Create(name, value)]);

    /// <summary>
    ///  Creates a property provider from property names and values.
    /// </summary>
    public IPropertyProvider<ProjectPropertyInstance> Properties(params ImmutableArray<(string Name, string Value)> properties)
        => properties.Length > 0
            ? PropertyDictionary.Create([.. properties.Select(p => ProjectPropertyInstance.Create(p.Name, p.Value))])
            : EmptyProperties<ProjectPropertyInstance>();
}
