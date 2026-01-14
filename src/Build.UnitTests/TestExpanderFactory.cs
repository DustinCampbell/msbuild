// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Engine.UnitTests;

internal static class TestExpanderFactory
{
    public static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(
        LoggingContext? loggingContext = null)
        => CreateExpander<ProjectPropertyInstance, ProjectItemInstance>(properties: null, items: null, metadata: null, loggingContext);

    public static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(
        IPropertyProvider<ProjectPropertyInstance> properties,
        LoggingContext? loggingContext = null)
        => CreateExpander<ProjectPropertyInstance, ProjectItemInstance>(properties, items: null, metadata: null, loggingContext);

    public static IExpander<ProjectProperty, ProjectItem> CreateExpander(
        IPropertyProvider<ProjectProperty> properties,
        LoggingContext? loggingContext = null)
        => CreateExpander<ProjectProperty, ProjectItem>(properties, items: null, metadata: null, loggingContext);

    public static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander(
        IItemProvider<ProjectItemInstance> items,
        LoggingContext? loggingContext = null)
        => CreateExpander<ProjectPropertyInstance, ProjectItemInstance>(properties: null, items, metadata: null, loggingContext);

    public static IExpander<ProjectProperty, ProjectItem> CreateExpander(
        IItemProvider<ProjectItem> items,
        LoggingContext? loggingContext = null)
        => CreateExpander<ProjectProperty, ProjectItem>(properties: null, items, metadata: null, loggingContext);

    public static IExpander<P, I> CreateExpander<P, I>(
        IPropertyProvider<P> properties,
        IItemProvider<I> items,
        LoggingContext? loggingContext = null)
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem
        => CreateExpander(properties, items, metadata: null, loggingContext);

    public static IExpander<P, I> CreateExpander<P, I>(
        IPropertyProvider<P>? properties,
        IItemProvider<I>? items,
        IMetadataTable? metadata,
        LoggingContext? loggingContext = null)
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem
        => ExpanderFactory.Create(
            properties: properties ?? new PropertyDictionary<P>(),
            items: items ?? new ItemDictionary<I>(),
            metadata: metadata ?? new StringMetadataTable(null),
            FileSystems.Default,
            evaluationContext: null,
            loggingContext);
}
