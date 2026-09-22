// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Shouldly;
using static Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.UnitTests.Expansion;

internal static class ExpansionHelpers
{
    public static string? ExpandMetadata(string expression, IMetadataTable metadata)
        => ExpandMetadata(expression, metadata, ExpanderOptions.ExpandMetadata);

    public static string? ExpandMetadata(string expression, IMetadataTable metadata, ExpanderOptions options)
    {
        const ExpanderOptions SupportedOptions = ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate | ExpanderOptions.BreakOnNotEmpty;
        (options & ~SupportedOptions).ShouldBe(ExpanderOptions.Invalid, "Metadata expansion received unsupported options.");

        var expander = ExpanderFactory.Create(Properties(), Items(), metadata);

        return expander.ExpandIntoStringLeaveEscaped(expression, options, MockElementLocation.Instance);
    }

    public static string? ExpandProperties(string expression)
        => ExpandProperties(expression, Properties(), loggingContext: null);

    public static string? ExpandProperties(string expression, PropertyDictionary<ProjectPropertyInstance> properties)
        => ExpandProperties(expression, properties, loggingContext: null);

    public static string? ExpandProperties(string expression, LoggingContext? loggingContext)
        => ExpandProperties(expression, Properties(), loggingContext);

    public static string? ExpandProperties(string expression, PropertyDictionary<ProjectPropertyInstance> properties, LoggingContext? loggingContext)
    {
        var expander = loggingContext is not null
            ? ExpanderFactory.Create(properties, loggingContext)
            : ExpanderFactory.Create(properties);

        return expander.ExpandIntoStringLeaveEscaped(expression, ExpanderOptions.ExpandProperties, MockElementLocation.Instance);
    }

    public static IItemFactory<ProjectItemInstance, ProjectItemInstance> ItemFactory(ProjectInstance project)
        => new ProjectItemInstanceFactory(project);

    public static IItemFactory<ProjectItemInstance, ProjectItemInstance> ItemFactory(ProjectInstance project, string? itemType)
        => new ProjectItemInstanceFactory(project, itemType);

    public static ItemDictionary<ProjectItemInstance> Items()
    {
        var result = new ItemDictionary<ProjectItemInstance>();

        return result;
    }

    public static ItemDictionary<ProjectItemInstance> GenerateItems(int count, ProjectInstance project, Func<ProjectInstance, int, ProjectItemInstance> generator)
    {
        var result = new ItemDictionary<ProjectItemInstance>();

        for (int i = 0; i < count; i++)
        {
            result.Add(generator(project, i));
        }

        return result;
    }

    public static StringMetadataTable Metadata(string name, string value)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        map.Add(name, value);

        return new StringMetadataTable(map);
    }

    public static StringMetadataTable Metadata(params IEnumerable<(string Name, string Value)> metadata)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in metadata)
        {
            map.Add(name, value);
        }

        return new StringMetadataTable(map);
    }

    public static PropertyDictionary<ProjectPropertyInstance> Properties(string name, string value)
    {
        var result = new PropertyDictionary<ProjectPropertyInstance>();

        result.Set(ProjectPropertyInstance.Create(name, value));

        return result;
    }

    public static PropertyDictionary<ProjectPropertyInstance> Properties(params IEnumerable<(string Name, string Value)> properties)
    {
        var result = new PropertyDictionary<ProjectPropertyInstance>();

        foreach (var (name, value) in properties)
        {
            result.Set(ProjectPropertyInstance.Create(name, value));
        }

        return result;
    }
}
