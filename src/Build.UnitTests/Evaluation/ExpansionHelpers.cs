// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using static Microsoft.Build.Execution.ProjectItemInstance;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Provides declarative helpers for constructing and exercising <see cref="Expander{P, I}"/> instances in tests.
/// </summary>
internal static class ExpansionHelpers
{
    /// <summary>
    ///  Expands text into task items using the specified properties.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <returns>
    ///  The expanded task items.
    /// </returns>
    public static IList<TaskItem> ExpandAsTaskItems(
        string text,
        ExpanderOptions options,
        IPropertyProvider<ProjectPropertyInstance> properties)
        => ExpandAsTaskItems(text, options, location: null, properties);

    /// <summary>
    ///  Expands text into task items using the specified items.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <returns>
    ///  The expanded task items.
    /// </returns>
    public static IList<TaskItem> ExpandAsTaskItems(
        string text,
        ExpanderOptions options,
        IItemProvider<ProjectItemInstance> items)
        => ExpandAsTaskItems(text, options, location: null, properties: null, items);

    /// <summary>
    ///  Expands text into task items using the specified properties and items.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <returns>
    ///  The expanded task items.
    /// </returns>
    public static IList<TaskItem> ExpandAsTaskItems(
        string text,
        ExpanderOptions options,
        IPropertyProvider<ProjectPropertyInstance> properties,
        IItemProvider<ProjectItemInstance> items)
        => ExpandAsTaskItems(text, options, location: null, properties, items);

    /// <summary>
    ///  Expands text into task items using the specified expansion inputs and services.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="location">The element location, or <see langword="null"/> to use a mock location.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <param name="fileSystem">The file system used by property functions.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  The expanded task items.
    /// </returns>
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

    /// <summary>
    ///  Expands text into items using the specified factory and properties.
    /// </summary>
    /// <typeparam name="T">The type of item to create.</typeparam>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="itemFactory">The factory used to create expanded items.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <returns>
    ///  The expanded items.
    /// </returns>
    public static IList<T> ExpandAsItems<T>(
        string text,
        ExpanderOptions options,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        IPropertyProvider<ProjectPropertyInstance> properties)
        where T : class, IItem
        => ExpandAsItems(text, options, itemFactory, location: null, properties: properties);

    /// <summary>
    ///  Expands text into items using the specified factory and source items.
    /// </summary>
    /// <typeparam name="T">The type of item to create.</typeparam>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="itemFactory">The factory used to create expanded items.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <returns>
    ///  The expanded items.
    /// </returns>
    public static IList<T> ExpandAsItems<T>(
        string text,
        ExpanderOptions options,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        IItemProvider<ProjectItemInstance> items)
        where T : class, IItem
        => ExpandAsItems(text, options, itemFactory, location: null, properties: null, items: items);

    /// <summary>
    ///  Expands text into items using the specified factory, properties, and source items.
    /// </summary>
    /// <typeparam name="T">The type of item to create.</typeparam>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="itemFactory">The factory used to create expanded items.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <returns>
    ///  The expanded items.
    /// </returns>
    public static IList<T> ExpandAsItems<T>(
        string text,
        ExpanderOptions options,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        IPropertyProvider<ProjectPropertyInstance> properties,
        IItemProvider<ProjectItemInstance> items)
        where T : class, IItem
        => ExpandAsItems(text, options, itemFactory, location: null, properties: properties, items: items);

    /// <summary>
    ///  Expands text into items using the specified expansion inputs and services.
    /// </summary>
    /// <typeparam name="T">The type of item to create.</typeparam>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="itemFactory">The factory used to create expanded items.</param>
    /// <param name="location">The element location, or <see langword="null"/> to use a mock location.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <param name="fileSystem">The file system used by property functions.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  The expanded items.
    /// </returns>
    public static IList<T> ExpandAsItems<T>(
        string text,
        ExpanderOptions options,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        IElementLocation? location = null,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null,
        IFileSystem? fileSystem = null,
        LoggingContext? loggingContext = null)
        where T : class, IItem
        => CreateExpander(options, properties, items, metadata, fileSystem, loggingContext)
            .ExpandIntoItemsLeaveEscaped(text, itemFactory, options, location ?? MockElementLocation.Instance);

    /// <summary>
    ///  Expands one item-vector expression into items and reports whether the expression is a transform.
    /// </summary>
    /// <typeparam name="T">The type of item to create.</typeparam>
    /// <param name="text">The item-vector expression to expand.</param>
    /// <param name="itemFactory">The factory used to create expanded items.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <returns>
    ///  The expanded items and whether <paramref name="text"/> is a transform expression.
    /// </returns>
    public static (IList<T> Items, bool IsTransformExpression) ExpandSingleItemVectorAsItems<T>(
        string text,
        IItemFactory<ProjectItemInstance, T> itemFactory,
        IItemProvider<ProjectItemInstance>? items = null)
        where T : class, IItem
    {
        IList<T> expandedItems = CreateExpander(ExpanderOptions.ExpandItems, items: items)
            .ExpandSingleItemVectorExpressionIntoItems(
                text,
                itemFactory,
                ExpanderOptions.ExpandItems,
                includeNullItems: false,
                out bool isTransformExpression,
                MockElementLocation.Instance);

        return (expandedItems, isTransformExpression);
    }

    /// <summary>
    ///  Expands item-vector expressions in text using an empty item provider.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandItems(string text)
        => ExpandAsString(text, ExpanderOptions.ExpandItems, location: null);

    /// <summary>
    ///  Expands item-vector expressions in text using an empty item provider.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="location">The element location used for diagnostics.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandItems(string text, IElementLocation location)
        => ExpandAsString(text, ExpanderOptions.ExpandItems, location);

    /// <summary>
    ///  Expands item-vector expressions in text using the specified items.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandItems(string text, IItemProvider<ProjectItemInstance> items)
        => ExpandAsString(text, ExpanderOptions.ExpandItems, location: null, properties: null, items);

    /// <summary>
    ///  Expands item-vector expressions in text using the specified location and items.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="location">The element location used for diagnostics.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandItems(
        string text,
        IElementLocation location,
        IItemProvider<ProjectItemInstance> items)
        => ExpandAsString(text, ExpanderOptions.ExpandItems, location, properties: null, items);

    /// <summary>
    ///  Expands metadata expressions in text using an empty metadata table.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandMetadata(string text)
        => ExpandAsString(text, ExpanderOptions.ExpandMetadata, location: null);

    /// <summary>
    ///  Expands metadata expressions in text using an empty metadata table.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="location">The element location used for diagnostics.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandMetadata(string text, IElementLocation location)
        => ExpandAsString(text, ExpanderOptions.ExpandMetadata, location);

    /// <summary>
    ///  Expands metadata expressions in text using the specified metadata.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandMetadata(string text, IMetadataTable metadata)
        => ExpandAsString(text, ExpanderOptions.ExpandMetadata, location: null, properties: null, items: null, metadata);

    /// <summary>
    ///  Expands metadata expressions in text using the specified options and metadata.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The metadata expansion options.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandMetadata(
        string text,
        ExpanderOptions options,
        IMetadataTable metadata)
        => ExpandAsString(text, options, location: null, properties: null, items: null, metadata);

    /// <summary>
    ///  Expands metadata expressions in text using the specified location and metadata.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="location">The element location used for diagnostics.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandMetadata(
        string text,
        IElementLocation location,
        IMetadataTable metadata)
        => ExpandAsString(text, ExpanderOptions.ExpandMetadata, location, properties: null, items: null, metadata);

    /// <summary>
    ///  Expands property expressions in text using an empty property provider.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandProperties(string text)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, location: null);

    /// <summary>
    ///  Expands property expressions in text using an empty property provider.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="location">The element location used for diagnostics.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandProperties(string text, IElementLocation location)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, location);

    /// <summary>
    ///  Expands property expressions in text using the specified properties.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandProperties(string text, IPropertyProvider<ProjectPropertyInstance> properties)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, location: null, properties);

    /// <summary>
    ///  Expands property expressions in text using the specified logging context.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandProperties(string text, LoggingContext loggingContext)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, loggingContext: loggingContext);

    /// <summary>
    ///  Expands property expressions in text using the specified location and properties.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="location">The element location used for diagnostics.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandProperties(
        string text,
        IElementLocation location,
        IPropertyProvider<ProjectPropertyInstance> properties)
        => ExpandAsString(text, ExpanderOptions.ExpandProperties, location, properties);

    /// <summary>
    ///  Expands property, item-vector, and metadata expressions in text.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
    public static string ExpandAll(
        string text,
        IPropertyProvider<ProjectPropertyInstance>? properties = null,
        IItemProvider<ProjectItemInstance>? items = null,
        IMetadataTable? metadata = null)
        => ExpandAsString(text, ExpanderOptions.ExpandAll, location: null, properties, items, metadata);

    /// <summary>
    ///  Expands text into a string using the specified expansion inputs and services.
    /// </summary>
    /// <param name="text">The text to expand.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="location">The element location, or <see langword="null"/> to use a mock location.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <param name="fileSystem">The file system used by property functions.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  The expanded string, with escaping preserved.
    /// </returns>
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

    /// <summary>
    ///  Creates an expander using the specified providers and services without supplying default providers.
    /// </summary>
    /// <typeparam name="TProperty">The property type used by the expander.</typeparam>
    /// <typeparam name="TItem">The item type used by the expander.</typeparam>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <param name="fileSystem">The file system used by property functions.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  The new expander.
    /// </returns>
    public static Expander<TProperty, TItem> CreateExpander<TProperty, TItem>(
        IPropertyProvider<TProperty>? properties = null,
        IItemProvider<TItem>? items = null,
        IMetadataTable? metadata = null,
        IFileSystem? fileSystem = null,
        LoggingContext? loggingContext = null)
        where TProperty : class, IProperty
        where TItem : class, IItem
        => CreateExpander(
            options: 0,
            properties,
            items,
            metadata,
            fileSystem,
            loggingContext);

    /// <summary>
    ///  Creates a project-instance expander using the specified options, providers, and services.
    /// </summary>
    /// <param name="options">The expansion options used to determine which missing providers are supplied as empty.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <param name="fileSystem">The file system used by property functions.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  The new expander.
    /// </returns>
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

    /// <summary>
    ///  Creates an expander using the specified options, providers, and services.
    /// </summary>
    /// <typeparam name="TProperty">The property type used by the expander.</typeparam>
    /// <typeparam name="TItem">The item type used by the expander.</typeparam>
    /// <param name="options">The expansion options used to determine which missing providers are supplied as empty.</param>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <param name="fileSystem">The file system used by property functions.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  The new expander.
    /// </returns>
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
            properties ??= EmptyProperties<TProperty>();
        }

        if ((options & ExpanderOptions.ExpandItems) != 0)
        {
            items ??= EmptyItems<TItem>();
        }

        if ((options & ExpanderOptions.ExpandMetadata) != 0)
        {
            metadata ??= EmptyMetadata;
        }

        return new(properties, items, metadata, fileSystem ?? FileSystems.Default, loggingContext);
    }

    /// <summary>
    ///  Gets an empty property provider.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <returns>
    ///  The empty property provider.
    /// </returns>
    public static IPropertyProvider<T> EmptyProperties<T>()
        where T : class, IProperty
        => EmptyPropertyProvider<T>.Instance;

    /// <summary>
    ///  Gets an empty item provider.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <returns>
    ///  The empty item provider.
    /// </returns>
    public static IItemProvider<T> EmptyItems<T>()
        where T : class, IItem
        => EmptyItemProvider<T>.Instance;

    /// <summary>
    ///  Gets an empty metadata table.
    /// </summary>
    /// <value>
    ///  The empty metadata table.
    /// </value>
    public static IMetadataTable EmptyMetadata
        => EmptyMetadataTable.Instance;

    /// <summary>
    ///  Provides no properties.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    private sealed class EmptyPropertyProvider<T> : IPropertyProvider<T>
        where T : class, IProperty
    {
        /// <summary>
        ///  Gets the singleton instance.
        /// </summary>
        public static readonly EmptyPropertyProvider<T> Instance = new();

        /// <summary>
        ///  Initializes the singleton instance.
        /// </summary>
        private EmptyPropertyProvider()
        {
        }

        /// <inheritdoc/>
        public T? GetProperty(string name)
            => null;

        /// <inheritdoc/>
        public T? GetProperty(string name, int startIndex, int length)
            => null;
    }

    /// <summary>
    ///  Provides no items.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    private sealed class EmptyItemProvider<T> : IItemProvider<T>
        where T : IItem
    {
        /// <summary>
        ///  Gets the singleton instance.
        /// </summary>
        public static readonly EmptyItemProvider<T> Instance = new();

        /// <summary>
        ///  Initializes the singleton instance.
        /// </summary>
        private EmptyItemProvider()
        {
        }

        /// <inheritdoc/>
        public ICollection<T> GetItems(string itemType)
            => Array.Empty<T>();
    }

    /// <summary>
    ///  Provides no metadata.
    /// </summary>
    private sealed class EmptyMetadataTable : IMetadataTable
    {
        /// <summary>
        ///  Gets the singleton instance.
        /// </summary>
        public static readonly EmptyMetadataTable Instance = new();

        /// <summary>
        ///  Initializes the singleton instance.
        /// </summary>
        private EmptyMetadataTable()
        {
        }

        /// <inheritdoc/>
        public string GetEscapedValue(string name)
            => string.Empty;

        /// <inheritdoc/>
        public string GetEscapedValue(string? itemType, string name)
            => string.Empty;

        /// <inheritdoc/>
        public string? GetEscapedValueIfPresent(string? itemType, string name)
            => null;
    }
}
