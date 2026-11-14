// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using static Microsoft.Build.Execution.ProjectItemInstance;

namespace Microsoft.Build.UnitTests.Evaluation;

internal static class ExpansionHelpers
{
    public static IList<TaskItem> ExpandAsTaskItems(
        string text,
        ExpanderOptions options,
        IPropertyProvider<ProjectPropertyInstance> properties)
        => ExpandAsTaskItems(text, options, location: null, properties);

    public static IList<TaskItem> ExpandAsTaskItems(
        string text,
        ExpanderOptions options,
        IItemProvider<ProjectItemInstance> items)
        => ExpandAsTaskItems(text, options, location: null, properties: null, items);

    public static IList<TaskItem> ExpandAsTaskItems(
        string text,
        ExpanderOptions options,
        IPropertyProvider<ProjectPropertyInstance> properties,
        IItemProvider<ProjectItemInstance> items)
        => ExpandAsTaskItems(text, options, location: null, properties, items);

    public static IList<TaskItem> ExpandAsTaskItems(
        string text,
        ExpanderOptions options,
        IElementLocation? location = null,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null,
        IFileSystem? fileSystem = null,
        LoggingContext? loggingContext = null)
        => CreateExpander(options, properties, items, metadata, fileSystem, loggingContext)
            .ExpandIntoTaskItemsLeaveEscaped(text, options, location ?? MockElementLocation.Instance);

    public static IList<T> ExpandAsItems<T>(
        string text,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        ExpanderOptions options,
        IPropertyProvider<ProjectPropertyInstance> properties)
        where T : class, IItem
        => ExpandAsItems(text, itemFactory, options, location: null, properties);

    public static IList<T> ExpandAsItems<T>(
        string text,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        ExpanderOptions options,
        IItemProvider<ProjectItemInstance> items)
        where T : class, IItem
        => ExpandAsItems(text, itemFactory, options, location: null, properties: null, items);

    public static IList<T> ExpandAsItems<T>(
        string text,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        ExpanderOptions options,
        IPropertyProvider<ProjectPropertyInstance> properties,
        IItemProvider<ProjectItemInstance> items)
        where T : class, IItem
        => ExpandAsItems(text, itemFactory, options, location: null, properties, items);

    public static IList<T> ExpandAsItems<T>(
        string text,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        ExpanderOptions options,
        IElementLocation? location = null,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null,
        IFileSystem? fileSystem = null,
        LoggingContext? loggingContext = null)
        where T : class, IItem
        => CreateExpander(options, properties, items, metadata, fileSystem, loggingContext)
            .ExpandIntoItemsLeaveEscaped(text, itemFactory, options, location ?? MockElementLocation.Instance);

    public static string ExpandItems(string text)
        => ExpandAsString(text, ExpanderOptions.ExpandItems, location: null);

    public static string ExpandItems(string text, IElementLocation location)
        => ExpandAsString(text, ExpanderOptions.ExpandItems, location);

    public static string ExpandItems(string text, IItemProvider<ProjectItemInstance> items)
        => ExpandAsString(text, ExpanderOptions.ExpandItems, location: null, properties: null, items);

    public static string ExpandItems(
        string text,
        IElementLocation location,
        IItemProvider<ProjectItemInstance> items)
        => ExpandAsString(text, ExpanderOptions.ExpandItems, location, properties: null, items);

    public static string ExpandMetadata(string text)
        => ExpandAsString(text, ExpanderOptions.ExpandMetadata, location: null);

    public static string ExpandMetadata(string text, IElementLocation location)
        => ExpandAsString(text, ExpanderOptions.ExpandMetadata, location);

    public static string ExpandMetadata(string text, IMetadataTable metadata)
        => ExpandAsString(text, ExpanderOptions.ExpandMetadata, location: null, properties: null, items: null, metadata);

    public static string ExpandMetadata(
        string text,
        ExpanderOptions options,
        IMetadataTable metadata)
        => ExpandAsString(text, options, location: null, properties: null, items: null, metadata);

    public static string ExpandMetadata(
        string text,
        IElementLocation location,
        IMetadataTable metadata)
        => ExpandAsString(text, ExpanderOptions.ExpandMetadata, location, properties: null, items: null, metadata);

    public static string ExpandProperties(string text)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, location: null);

    public static string ExpandProperties(string text, IElementLocation location)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, location);

    public static string ExpandProperties(string text, IPropertyProvider<ProjectPropertyInstance> properties)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, location: null, properties);

    public static string ExpandProperties(string text, LoggingContext loggingContext)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, loggingContext: loggingContext);

    public static string ExpandProperties(
        string text,
        IElementLocation location,
        IPropertyProvider<ProjectPropertyInstance> properties)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, location, properties);

    public static string ExpandAll(
        string text,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null)
        => ExpandAsString(text, ExpanderOptions.ExpandAll, location: null, properties, items, metadata);

    public static string ExpandAsString(
        string text,
        ExpanderOptions options,
        IElementLocation? location = null,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null,
        IFileSystem? fileSystem = null,
        LoggingContext? loggingContext = null)
        => CreateExpander(options, properties, items, metadata, fileSystem, loggingContext)
            .ExpandIntoStringLeaveEscaped(text, options, location ?? MockElementLocation.Instance);

    public static Expander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(
        ExpanderOptions options,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null,
        IFileSystem? fileSystem = null,
        LoggingContext? loggingContext = null)
        => CreateExpander<ProjectPropertyInstance, ProjectItemInstance>(
            options,
            properties,
            items,
            metadata,
            fileSystem,
            loggingContext);

    public static Expander<TProperty, TItem> CreateExpander<TProperty, TItem>(
        ExpanderOptions options,
        IPropertyProvider<TProperty>? properties = null,
        IItemProvider<TItem>? items = null,
        IMetadataTable? metadata = null,
        IFileSystem? fileSystem = null,
        LoggingContext? loggingContext = null)
        where TProperty : class, IProperty
        where TItem : class, IItem
    {
        if ((options & ExpanderOptions.ExpandProperties) != 0)
        {
            properties ??= EmptyPropertyProvider<TProperty>.Instance;
        }

        if ((options & ExpanderOptions.ExpandItems) != 0)
        {
            items ??= EmptyItemProvider<TItem>.Instance;
        }

        if ((options & ExpanderOptions.ExpandMetadata) != 0)
        {
            metadata ??= NoMetadata;
        }

        return new(properties, items, metadata, fileSystem ?? FileSystems.Default, loggingContext);
    }

    public static IPropertyProvider<ProjectPropertyInstance> NoProperties
        => EmptyPropertyProvider<ProjectPropertyInstance>.Instance;

    public static IPropertyProvider<ProjectPropertyInstance> Properties(params ReadOnlySpan<ProjectPropertyInstance> properties)
        => Properties<ProjectPropertyInstance>(properties);

    public static IPropertyProvider<T> Properties<T>(params ReadOnlySpan<T> properties)
        where T : class, IKeyed, IValued, IEquatable<T>
        => properties.Length > 0
            ? PropertyDictionary.Create(properties)
            : EmptyPropertyProvider<T>.Instance;

    public static IPropertyProvider<ProjectPropertyInstance> Property(string name, string value)
        => Properties(ProjectPropertyInstance.Create(name, value));

    public static IPropertyProvider<ProjectPropertyInstance> Property(ProjectPropertyInstance property)
        => Property<ProjectPropertyInstance>(property);

    public static IPropertyProvider<T> Property<T>(T property)
        where T : class, IKeyed, IValued, IEquatable<T>
        => Properties<T>(property);

    public static IPropertyProvider<ProjectPropertyInstance> Properties(params ImmutableArray<(string Name, string Value)> properties)
        => properties.Length > 0
            ? PropertyDictionary.Create([.. properties.Select(p => ProjectPropertyInstance.Create(p.Name, p.Value))])
            : NoProperties;

    public static IItemProvider<ProjectItemInstance> NoItems
        => EmptyItemProvider<ProjectItemInstance>.Instance;

    public static IItemProvider<ProjectItemInstance> Item(ProjectItemInstance item)
        => Item<ProjectItemInstance>(item);

    public static IItemProvider<T> Item<T>(T item)
        where T : class, IKeyed, IItem
        => Items<T>(item);

    public static IItemProvider<ProjectItemInstance> Items(params ReadOnlySpan<ProjectItemInstance> items)
        => Items<ProjectItemInstance>(items);

    public static IItemProvider<T> Items<T>(params ReadOnlySpan<T> items)
        where T : class, IKeyed, IItem
        => items.Length > 0
            ? ItemDictionary.Create(items)
            : EmptyItemProvider<T>.Instance;

    public static IMetadataTable NoMetadata
        => EmptyMetadataTable.Instance;

    public static IMetadataTable Metadata(string name, string value)
        => Metadata((name, value));

    public static IMetadataTable Metadata(params ReadOnlySpan<(string Name, string Value)> metadata)
        => metadata.Length > 0
            ? CreateMetadataTable(metadata)
            : NoMetadata;

    private static StringMetadataTable CreateMetadataTable(ReadOnlySpan<(string Name, string Value)> metadata)
    {
        Dictionary<string, string> map = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (var (name, value) in metadata)
        {
            map[name] = value;
        }

        return new StringMetadataTable(map);
    }

    private sealed class EmptyPropertyProvider<T> : IPropertyProvider<T>
        where T : class
    {
        public static readonly EmptyPropertyProvider<T> Instance = new();

        private EmptyPropertyProvider()
        {
        }

        public T? GetProperty(string name)
            => null;

        public T? GetProperty(string name, int startIndex, int length)
            => null;
    }

    private sealed class EmptyItemProvider<T> : IItemProvider<T>
        where T : IItem
    {
        public static readonly EmptyItemProvider<T> Instance = new();

        private EmptyItemProvider()
        {
        }

        public ICollection<T> GetItems(string itemType)
            => Array.Empty<T>();
    }

    private sealed class EmptyMetadataTable : IMetadataTable
    {
        public static readonly EmptyMetadataTable Instance = new();

        private EmptyMetadataTable()
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
