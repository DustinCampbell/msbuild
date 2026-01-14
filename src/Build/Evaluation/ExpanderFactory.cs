// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation;

internal static class ExpanderFactory
{
    private const bool UseNewExpander = true;

    public static IExpander<P, I> Create<P, I>(
        IPropertyProvider<P> properties,
        IFileSystem fileSystem)
        where P : class, IProperty
        where I : class, IItem
        => Create<P, I>(properties, items: null, metadata: null, fileSystem, evaluationContext: null, loggingContext: null);

    public static IExpander<P, I> Create<P, I>(
        IPropertyProvider<P> properties,
        IFileSystem fileSystem,
        LoggingContext loggingContext)
        where P : class, IProperty
        where I : class, IItem
        => Create<P, I>(properties, items: null, metadata: null, fileSystem, evaluationContext: null, loggingContext);

    public static IExpander<P, I> Create<P, I>(
        IMetadataTable metadata,
        IFileSystem fileSystem)
        where P : class, IProperty
        where I : class, IItem
        => Create<P, I>(properties: null, items: null, metadata, fileSystem, evaluationContext: null, loggingContext: null);

    public static IExpander<P, I> Create<P, I>(
        IPropertyProvider<P> properties,
        IItemProvider<I> items,
        IFileSystem fileSystem,
        LoggingContext loggingContext)
        where P : class, IProperty
        where I : class, IItem
        => Create(properties, items, metadata: null, fileSystem, evaluationContext: null, loggingContext);

    public static IExpander<P, I> Create<P, I>(
        IPropertyProvider<P> properties,
        IItemProvider<I> items,
        EvaluationContext evaluationContext,
        LoggingContext loggingContext)
        where P : class, IProperty
        where I : class, IItem
        => Create(properties, items, metadata: null, evaluationContext.FileSystem, evaluationContext, loggingContext);

    public static IExpander<P, I> Create<P, I>(
        IPropertyProvider<P> properties,
        IItemProvider<I> items,
        IMetadataTable metadata,
        IFileSystem fileSystem,
        LoggingContext loggingContext)
        where P : class, IProperty
        where I : class, IItem
        => Create(properties, items, metadata, fileSystem, evaluationContext: null, loggingContext);

    public static IExpander<P, I> Create<P, I>(
        IPropertyProvider<P>? properties,
        IItemProvider<I>? items,
        IMetadataTable? metadata,
        IFileSystem fileSystem,
        EvaluationContext? evaluationContext,
        LoggingContext? loggingContext)
        where P : class, IProperty
        where I : class, IItem
        => UseNewExpander
            ? new Expander2<P, I>(properties, items, metadata, fileSystem, evaluationContext, loggingContext)
            : new Expander<P, I>(properties, items, metadata, fileSystem, evaluationContext, loggingContext);
}
