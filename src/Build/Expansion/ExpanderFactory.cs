// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion.Legacy;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Expansion;

/// <summary>
///  Creates expansion engines and centralizes selection of their concrete implementation.
/// </summary>
internal static class ExpanderFactory
{
    /// <summary>
    ///  Gets whether expansion should use the legacy implementation.
    /// </summary>
    public static bool UseLegacyExpander
        => !ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_12) || Traits.Instance.UseLegacyExpander;

    private static IExpander<TProperty, TItem> CreateCore<TProperty, TItem>(
        IPropertyProvider<TProperty>? properties,
        IItemProvider<TItem>? items,
        IMetadataTable? metadata,
        IFileSystem? fileSystem,
        LoggingContext? loggingContext,
        EvaluationContext? evaluationContext)
        where TProperty : class, IProperty
        where TItem : class, IItem
        => !UseLegacyExpander
            ? new Expander<TProperty, TItem>(properties, items, metadata, fileSystem, loggingContext, evaluationContext)
            : new LegacyExpander<TProperty, TItem>(properties, items, metadata, fileSystem, loggingContext, evaluationContext);

    private static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateCore(
        IPropertyProvider<ProjectPropertyInstance>? properties,
        IMetadataTable? metadata,
        IFileSystem? fileSystem,
        LoggingContext? loggingContext,
        EvaluationContext? evaluationContext)
        => CreateCore<ProjectPropertyInstance, ProjectItemInstance>(properties, items: null, metadata, fileSystem, loggingContext, evaluationContext);

    private static IExpander<ProjectProperty, ProjectItem> CreateCore(
        IPropertyProvider<ProjectProperty>? properties,
        IMetadataTable? metadata,
        IFileSystem? fileSystem,
        LoggingContext? loggingContext,
        EvaluationContext? evaluationContext)
        => CreateCore<ProjectProperty, ProjectItem>(properties, items: null, metadata, fileSystem, loggingContext, evaluationContext);

    /// <summary>
    ///  Creates an expander backed by project-instance properties.
    /// </summary>
    /// <param name="properties">The properties available during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<ProjectPropertyInstance, ProjectItemInstance> Create(
        IPropertyProvider<ProjectPropertyInstance> properties)
        => CreateCore(properties, metadata: null, fileSystem: null, loggingContext: null, evaluationContext: null);

    /// <summary>
    ///  Creates an expander backed by project-instance properties and a logging context.
    /// </summary>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<ProjectPropertyInstance, ProjectItemInstance> Create(
        IPropertyProvider<ProjectPropertyInstance> properties,
        LoggingContext loggingContext)
        => CreateCore(
            properties,
            metadata: null,
            fileSystem: null,
            loggingContext,
            evaluationContext: null);

    /// <summary>
    ///  Creates an expander backed by evaluated project properties.
    /// </summary>
    /// <param name="properties">The properties available during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<ProjectProperty, ProjectItem> Create(
        IPropertyProvider<ProjectProperty> properties)
        => CreateCore(properties, metadata: null, fileSystem: null, loggingContext: null, evaluationContext: null);

    /// <summary>
    ///  Creates an expander backed by the specified properties and items.
    /// </summary>
    /// <typeparam name="TProperty">The type of properties available during expansion.</typeparam>
    /// <typeparam name="TItem">The type of items available during expansion.</typeparam>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<TProperty, TItem> Create<TProperty, TItem>(
        IPropertyProvider<TProperty> properties,
        IItemProvider<TItem> items)
        where TProperty : class, IProperty
        where TItem : class, IItem
        => CreateCore(properties, items, metadata: null, fileSystem: null, loggingContext: null, evaluationContext: null);

    /// <summary>
    ///  Creates an expander backed by the specified properties, items, evaluation context, and logging context.
    /// </summary>
    /// <typeparam name="TProperty">The type of properties available during expansion.</typeparam>
    /// <typeparam name="TItem">The type of items available during expansion.</typeparam>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="evaluationContext">The evaluation context whose file system is used during expansion.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<TProperty, TItem> Create<TProperty, TItem>(
        IPropertyProvider<TProperty> properties,
        IItemProvider<TItem> items,
        EvaluationContext evaluationContext,
        LoggingContext loggingContext)
        where TProperty : class, IProperty
        where TItem : class, IItem
        => CreateCore(properties, items, metadata: null, fileSystem: null, loggingContext, evaluationContext);

    /// <summary>
    ///  Creates an expander backed by the specified properties, items, and logging context.
    /// </summary>
    /// <typeparam name="TProperty">The type of properties available during expansion.</typeparam>
    /// <typeparam name="TItem">The type of items available during expansion.</typeparam>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<TProperty, TItem> Create<TProperty, TItem>(
        IPropertyProvider<TProperty> properties,
        IItemProvider<TItem> items,
        LoggingContext loggingContext)
        where TProperty : class, IProperty
        where TItem : class, IItem
        => CreateCore(properties, items, metadata: null, fileSystem: null, loggingContext, evaluationContext: null);

    /// <summary>
    ///  Creates an expander backed by the specified properties, items, and metadata.
    /// </summary>
    /// <typeparam name="TProperty">The type of properties available during expansion.</typeparam>
    /// <typeparam name="TItem">The type of items available during expansion.</typeparam>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<TProperty, TItem> Create<TProperty, TItem>(
        IPropertyProvider<TProperty> properties,
        IItemProvider<TItem> items,
        IMetadataTable metadata)
        where TProperty : class, IProperty
        where TItem : class, IItem
        => CreateCore(properties, items, metadata, fileSystem: null, loggingContext: null, evaluationContext: null);

    /// <summary>
    ///  Creates an expander backed only by the specified metadata.
    /// </summary>
    /// <typeparam name="TProperty">The property type expected by callers of the expander.</typeparam>
    /// <typeparam name="TItem">The item type expected by callers of the expander.</typeparam>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<TProperty, TItem> Create<TProperty, TItem>(
        IMetadataTable metadata)
        where TProperty : class, IProperty
        where TItem : class, IItem
        => CreateCore<TProperty, TItem>(properties: null, items: null, metadata, fileSystem: null, loggingContext: null, evaluationContext: null);

    /// <summary>
    ///  Creates an expander backed by the specified properties, items, metadata, and logging context.
    /// </summary>
    /// <typeparam name="TProperty">The type of properties available during expansion.</typeparam>
    /// <typeparam name="TItem">The type of items available during expansion.</typeparam>
    /// <param name="properties">The properties available during expansion.</param>
    /// <param name="items">The items available during expansion.</param>
    /// <param name="metadata">The metadata available during expansion.</param>
    /// <param name="loggingContext">The logging context used during expansion.</param>
    /// <returns>
    ///  A configured expander.
    /// </returns>
    public static IExpander<TProperty, TItem> Create<TProperty, TItem>(
        IPropertyProvider<TProperty> properties,
        IItemProvider<TItem> items,
        IMetadataTable metadata,
        LoggingContext loggingContext)
        where TProperty : class, IProperty
        where TItem : class, IItem
        => CreateCore(properties, items, metadata, fileSystem: null, loggingContext, evaluationContext: null);
}
