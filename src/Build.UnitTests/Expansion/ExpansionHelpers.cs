// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Shouldly;
using Xunit;
using static Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.UnitTests.Expansion;

internal static class ExpansionHelpers
{
    private const string PropertyFunctionsRequiringReflectionFileName = "PropertyFunctionsRequiringReflection";

    public static string ToResultString(this bool value)
        => value.ToString(CultureInfo.InvariantCulture);

    public static string ToResultString(this double value)
        => value.ToString(CultureInfo.InvariantCulture);

    public static string ToResultString(this int value)
        => value.ToString(CultureInfo.InvariantCulture);

    public static string ToResultString(this long value)
        => value.ToString(CultureInfo.InvariantCulture);

    public static string? ToResultString(this object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture);

    public static string? ExpandMetadata(string expression, IMetadataTable metadata)
        => ExpandMetadata(expression, metadata, ExpanderOptions.ExpandMetadata);

    public static string? ExpandMetadata(string expression, IMetadataTable metadata, ExpanderOptions options)
    {
        const ExpanderOptions SupportedOptions = ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate | ExpanderOptions.BreakOnNotEmpty;
        (options & ~SupportedOptions).ShouldBe(ExpanderOptions.Invalid, "Metadata expansion received unsupported options.");

        var expander = ExpanderFactory.Create(Properties(), Items(), metadata);

        return ExpandIntoStringLeaveEscaped(expander, expression, options, allowReflection: true);
    }

    public static string? ExpandProperties(string expression, bool allowReflection = true)
        => ExpandProperties(expression, Properties(), loggingContext: null, allowReflection);

    public static string? ExpandProperties(string expression, PropertyDictionary<ProjectPropertyInstance> properties, bool allowReflection = true)
        => ExpandProperties(expression, properties, loggingContext: null, allowReflection);

    public static string? ExpandProperties(
        string expression,
        PropertyDictionary<ProjectPropertyInstance> properties,
        ExpanderOptions options,
        bool allowReflection = true)
        => ExpandProperties(expression, properties, loggingContext: null, allowReflection, options);

    public static string? ExpandProperties(
        string expression,
        PropertyDictionary<ProjectPropertyInstance> properties,
        string filePath,
        bool allowReflection = true)
        => ExpandProperties(expression, properties, loggingContext: null, allowReflection, location: new(filePath));

    public static string? ExpandProperties(string expression, LoggingContext? loggingContext, bool allowReflection = true)
        => ExpandProperties(expression, Properties(), loggingContext, allowReflection);

    public static string? ExpandProperties(
        string expression,
        PropertyDictionary<ProjectPropertyInstance> properties,
        LoggingContext? loggingContext,
        bool allowReflection = true,
        ExpanderOptions options = ExpanderOptions.ExpandProperties,
        MockElementLocation? location = null)
    {
        options |= ExpanderOptions.ExpandProperties;

        if (loggingContext is null && TestContext.Current.TestOutputHelper is { } output)
        {
            (_, loggingContext) = CreateLoggingContext(output);
        }

        var expander = loggingContext is not null
            ? ExpanderFactory.Create(properties, loggingContext)
            : ExpanderFactory.Create(properties);

        return ExpandIntoStringLeaveEscaped(expander, expression, options, allowReflection, location);
    }

    public static string? ExpandPropertiesAndMetadata(string expression, IMetadataTable metadata, bool allowReflection = true)
        => ExpandPropertiesAndMetadata(expression, Properties(), metadata, allowReflection);

    public static string? ExpandPropertiesAndMetadata(
        string expression,
        PropertyDictionary<ProjectPropertyInstance> properties,
        IMetadataTable metadata,
        bool allowReflection = true)
    {
        var expander = ExpanderFactory.Create<ProjectPropertyInstance, ProjectItemInstance>(properties, items: null!, metadata);

        return ExpandIntoStringLeaveEscaped(expander, expression, ExpanderOptions.ExpandPropertiesAndMetadata, allowReflection);
    }

    private static string? ExpandIntoStringLeaveEscaped(
        IExpander<ProjectPropertyInstance, ProjectItemInstance> expander,
        string expression,
        ExpanderOptions options,
        bool allowReflection,
        MockElementLocation? location = null)
    {
        location ??= MockElementLocation.Instance;

        bool enableAllPropertyFunctions = FeatureSwitches.EnableAllPropertyFunctions;
        string reflectionInfoPath = Path.Combine(Directory.GetCurrentDirectory(), PropertyFunctionsRequiringReflectionFileName);

        ITestOutputHelper? output = TestContext.Current.TestOutputHelper;

        using TestEnvironment env = output is not null
            ? TestEnvironment.Create(output)
            : TestEnvironment.Create();

        env.SetEnvironmentVariable("MSBuildLogPropertyFunctionsRequiringReflection", "1");

        try
        {
            string? result = expander.ExpandIntoStringLeaveEscaped(expression, options, location);

            if (File.Exists(reflectionInfoPath))
            {
                string[] lines = File.ReadAllLines(reflectionInfoPath);

                if (output is not null)
                {
                    output.WriteLine("The following property functions were invoked with reflection.");

                    foreach (string line in lines)
                    {
                        output.WriteLine($"    {line}");
                    }
                }

                if (lines.Length > 0)
                {
                    allowReflection.ShouldBeTrue(
                        $"{lines[0]} {(lines.Length > 1 ? $"(and {lines.Length - 1} other property functions) were" : "was")} invoked with reflection.");
                }
            }

            return result;
        }
        finally
        {
            if (File.Exists(reflectionInfoPath))
            {
                File.Delete(reflectionInfoPath);
            }

            if (enableAllPropertyFunctions)
            {
                AvailableStaticMethods.Reset_ForUnitTestsOnly();
            }
        }
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

    public static ItemDictionary<ProjectItemInstance> Items(ProjectInstance project, params (string ItemType, string[] Includes)[] items)
    {
        var result = new ItemDictionary<ProjectItemInstance>();

        foreach (var (itemType, includes) in items)
        {
            foreach (string include in includes)
            {
                result.Add(new ProjectItemInstance(project, itemType, include, project.FullPath));
            }
        }

        return result;
    }

    public static ItemDictionary<ProjectItemInstance> GenerateItems(
        int count,
        ProjectInstance project,
        Func<ProjectInstance, int, ProjectItemInstance> generator)
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

    public static (MockLogger Logger, MockLoggingContext Context) CreateLoggingContext(ITestOutputHelper output)
    {
        var logger = new MockLogger(output);
        ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.RegisterLogger(logger);
        var loggingContext = new MockLoggingContext(
            loggingService,
            new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));

        return (logger, loggingContext);
    }
}
