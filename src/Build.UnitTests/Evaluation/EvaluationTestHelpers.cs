// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Creates property, item, and metadata providers for evaluation tests.
/// </summary>
internal static class EvaluationTestHelpers
{
    /// <summary>
    ///  Creates an empty project instance named <c>foo.proj</c> in the test assembly directory.
    /// </summary>
    /// <returns>
    ///  The project instance, without creating a project file on disk.
    /// </returns>
    public static ProjectInstance CreateProject()
    {
        ProjectRootElement xml = ProjectRootElement.Create();
        xml.FullPath = Path.Combine(AppContext.BaseDirectory, "foo.proj");

        return new(xml);
    }

    /// <summary>
    ///  Creates a provider containing one property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns>
    ///  The property provider.
    /// </returns>
    public static IPropertyProvider<ProjectPropertyInstance> Properties(string name, string value)
        => PropertyDictionary.Create([ProjectPropertyInstance.Create(name, value)]);

    /// <summary>
    ///  Creates a provider from property names and values.
    /// </summary>
    /// <param name="properties">The properties to create.</param>
    /// <returns>
    ///  The property provider, or <see cref="NoProperties"/> when no properties are supplied.
    /// </returns>
    public static IPropertyProvider<ProjectPropertyInstance> Properties(params ImmutableArray<(string Name, string Value)> properties)
        => properties.Length > 0
            ? Properties([.. properties.Select(p => ProjectPropertyInstance.Create(p.Name, p.Value))])
            : NoProperties;

    /// <summary>
    ///  Creates a provider containing the supplied property instances.
    /// </summary>
    /// <param name="properties">The properties to include.</param>
    /// <returns>
    ///  The property provider, or <see cref="NoProperties"/> when no properties are supplied.
    /// </returns>
    public static IPropertyProvider<ProjectPropertyInstance> Properties(params ReadOnlySpan<ProjectPropertyInstance> properties)
        => properties.Length > 0
            ? PropertyDictionary.Create(properties)
            : NoProperties;

    /// <summary>
    ///  Gets an empty property provider.
    /// </summary>
    public static IPropertyProvider<ProjectPropertyInstance> NoProperties
        => EmptyPropertyProvider.Instance;

    /// <summary>
    ///  Creates a provider containing one item owned by a project named <c>foo.proj</c>.
    /// </summary>
    /// <param name="itemType">The item type.</param>
    /// <param name="include">The item include.</param>
    /// <returns>
    ///  The item provider.
    /// </returns>
    public static IItemProvider<ProjectItemInstance> Items(string itemType, string include)
    {
        ProjectInstance project = CreateProject();
        return ItemDictionary.Create([new ProjectItemInstance(project, itemType, include, project.FullPath)]);
    }

    /// <summary>
    ///  Creates a provider from item types and includes, preserving their order and sharing one owning project.
    /// </summary>
    /// <param name="items">The items to create.</param>
    /// <returns>
    ///  The item provider, or <see cref="NoItems"/> when no items are supplied.
    /// </returns>
    public static IItemProvider<ProjectItemInstance> Items(params ImmutableArray<(string ItemType, string Include)> items)
    {
        if (items.Length == 0)
        {
            return NoItems;
        }

        ProjectInstance project = CreateProject();
        return Items([.. items.Select(i => new ProjectItemInstance(project, i.ItemType, i.Include, project.FullPath))]);
    }

    /// <summary>
    ///  Creates a provider containing the supplied item instances.
    /// </summary>
    /// <param name="items">The items to include.</param>
    /// <returns>
    ///  The item provider, or <see cref="NoItems"/> when no items are supplied.
    /// </returns>
    public static IItemProvider<ProjectItemInstance> Items(params ReadOnlySpan<ProjectItemInstance> items)
        => items.Length > 0
            ? ItemDictionary.Create(items)
            : NoItems;

    /// <summary>
    ///  Gets an empty item provider.
    /// </summary>
    public static IItemProvider<ProjectItemInstance> NoItems
        => EmptyItemProvider.Instance;

    /// <summary>
    ///  Creates a table containing one metadata value.
    /// </summary>
    /// <param name="name">The metadata name.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>
    ///  The metadata table.
    /// </returns>
    public static IMetadataTable Metadata(string name, string value)
        => Metadata((name, value));

    /// <summary>
    ///  Creates a case-insensitive table from metadata names and values.
    /// </summary>
    /// <param name="metadata">The metadata to include.</param>
    /// <returns>
    ///  The metadata table, or <see cref="NoMetadata"/> when no metadata is supplied.
    /// </returns>
    public static IMetadataTable Metadata(params ReadOnlySpan<(string Name, string Value)> metadata)
    {
        if (metadata.Length == 0)
        {
            return NoMetadata;
        }

        Dictionary<string, string> map = new(metadata.Length, StringComparer.OrdinalIgnoreCase);

        foreach ((string name, string value) in metadata)
        {
            map[name] = value;
        }

        return new StringMetadataTable(map);
    }

    /// <summary>
    ///  Gets an empty metadata table.
    /// </summary>
    public static IMetadataTable NoMetadata
        => EmptyMetadataProvider.Instance;

    private sealed class EmptyPropertyProvider : IPropertyProvider<ProjectPropertyInstance>
    {
        public static readonly EmptyPropertyProvider Instance = new();

        private EmptyPropertyProvider()
        {
        }

        public ProjectPropertyInstance? GetProperty(string name)
            => null;

        public ProjectPropertyInstance? GetProperty(string name, int startIndex, int length)
            => null;
    }

    private sealed class EmptyItemProvider : IItemProvider<ProjectItemInstance>
    {
        public static readonly EmptyItemProvider Instance = new();

        private EmptyItemProvider()
        {
        }

        public ICollection<ProjectItemInstance> GetItems(string itemType)
            => [];
    }

    private sealed class EmptyMetadataProvider : IMetadataTable
    {
        public static readonly EmptyMetadataProvider Instance = new();

        private EmptyMetadataProvider()
        {
        }

        public string GetEscapedValue(string name)
            => string.Empty;

        public string GetEscapedValue(string? itemType, string name)
            => string.Empty;

        public string? GetEscapedValueIfPresent(string? itemType, string name)
            => null;
    }
}
