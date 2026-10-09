// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using EditableEvaluator = Microsoft.Build.Evaluation.LazyItemEvaluator<Microsoft.Build.Evaluation.ProjectProperty, Microsoft.Build.Evaluation.ProjectItem, Microsoft.Build.Evaluation.ProjectMetadata, Microsoft.Build.Evaluation.ProjectItemDefinition>;
using InstanceEvaluator = Microsoft.Build.Evaluation.LazyItemEvaluator<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance, Microsoft.Build.Execution.ProjectMetadataInstance, Microsoft.Build.Execution.ProjectItemDefinitionInstance>;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Drives the lazy item evaluator directly, without evaluating the scenario through a project.
/// </summary>
/// <remarks>
///  Only the empty outer project and optional item definitions use ordinary project evaluation.
///  Scenario XML belongs to a separate root and is passed directly to the evaluator. False-condition
///  elements are deliberately recorded so both item models can exercise the evaluator's filtering.
///  The supplied <see cref="TestEnvironment"/> owns the project collection and temporary directory.
/// </remarks>
internal sealed class LazyItemEvaluatorTestFixture
{
    /// <summary>
    ///  The collection that owns the outer project and scenario XML.
    /// </summary>
    private readonly ProjectCollection _collection;

    /// <summary>
    ///  The editable-model evaluator, when that model was selected.
    /// </summary>
    private readonly EditableEvaluator? _editableEvaluator;

    /// <summary>
    ///  The instance-model evaluator, when that model was selected.
    /// </summary>
    private readonly InstanceEvaluator? _instanceEvaluator;

    /// <summary>
    ///  The editable outer project, when that model was selected.
    /// </summary>
    private readonly Project? _project;

    /// <summary>
    ///  The instance-model outer project, when that model was selected.
    /// </summary>
    private readonly ProjectInstance? _projectInstance;

    /// <summary>
    ///  Initializes a direct evaluator with the selected item model.
    /// </summary>
    /// <param name="environment">The environment that owns transient test state.</param>
    /// <param name="instanceModel">Whether to use instance-model rather than editable-model items.</param>
    /// <param name="outerBody">Properties and item definitions evaluated before the scenario.</param>
    /// <param name="fileSystem">An optional filesystem for observing glob evaluation.</param>
    /// <param name="profile">Whether to collect evaluation profiling data.</param>
    public LazyItemEvaluatorTestFixture(
        TestEnvironment environment,
        bool instanceModel,
        string outerBody = "",
        MSBuildFileSystemBase? fileSystem = null,
        bool profile = false)
    {
        Directory = environment.CreateFolder();
        _collection = environment.CreateProjectCollection().Collection;
        ProjectRootElement root = CreateRoot(outerBody, Path.Combine(DirectoryPath, "outer.proj"));
        var loggingContext = new EvaluationLoggingContext(_collection.LoggingService, BuildEventContext.Invalid, root.FullPath);
        Profiler = new EvaluationProfiler(profile);
        EvaluationContext context = fileSystem is null
            ? EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated)
            : EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared, fileSystem);

        if (instanceModel)
        {
            _projectInstance = new ProjectInstance(root, globalProperties: null, toolsVersion: null, _collection);
            var factory = new CountingItemFactory<ProjectItemInstance>(
                new ProjectItemInstance.TaskItem.ProjectItemInstanceFactory(_projectInstance), CountCreation, CountClone);
            _instanceEvaluator = new InstanceEvaluator(_projectInstance, factory, loggingContext, Profiler, context);
        }
        else
        {
            _project = new Project(root, globalProperties: null, toolsVersion: null, _collection);
            var factory = new CountingItemFactory<ProjectItem>(
                new ProjectItem.ProjectItemFactory(_project), CountCreation, CountClone);
            _editableEvaluator = new EditableEvaluator(_project.TestOnlyGetPrivateData, factory, loggingContext, Profiler, context);
        }
    }

    /// <summary>
    ///  Gets the transient directory owned by the test environment.
    /// </summary>
    public TransientTestFolder Directory { get; }

    /// <summary>
    ///  Gets the root directory used for item matching and condition evaluation.
    /// </summary>
    public string DirectoryPath => Directory.Path;

    /// <summary>
    ///  Gets the number of items created by the evaluator's factory.
    /// </summary>
    public int CreatedItems { get; private set; }

    /// <summary>
    ///  Gets the number of those creations that clone an existing item.
    /// </summary>
    public int ClonedItems { get; private set; }

    /// <summary>
    ///  Gets the profiler used by the evaluator.
    /// </summary>
    public EvaluationProfiler Profiler { get; }

    /// <summary>
    ///  Records scenario item groups, evaluating their conditions against the current item state.
    /// </summary>
    /// <param name="body">The item groups to record.</param>
    /// <param name="definingProject">An optional full path for the scenario's defining project.</param>
    /// <returns>
    ///  The scenario root, allowing assertions about originating XML and metadata.
    /// </returns>
    public ProjectRootElement Record(string body, string? definingProject = null)
    {
        ProjectRootElement root = CreateRoot(body, definingProject ?? Path.Combine(DirectoryPath, "scenario.proj"));
        foreach (ProjectItemGroupElement group in root.ItemGroups)
        {
            bool groupResult = EvaluateCondition(group);
            foreach (ProjectItemElement element in group.Items)
            {
                bool conditionResult = EvaluateCondition(element) && groupResult;
                if (_instanceEvaluator is { } instanceEvaluator)
                {
                    instanceEvaluator.ProcessItemElement(DirectoryPath, element, conditionResult);
                }
                else
                {
                    _editableEvaluator!.ProcessItemElement(DirectoryPath, element, conditionResult);
                }
            }
        }

        return root;
    }

    /// <summary>
    ///  Evaluates a condition without appending an item operation.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <returns>
    ///  The condition's result at the current end of each item history.
    /// </returns>
    public bool EvaluateCondition(string condition)
    {
        ProjectRootElement root = ProjectRootElement.Create(_collection);
        root.FullPath = Path.Combine(DirectoryPath, "condition.proj");
        ProjectItemGroupElement group = root.AddItemGroup();
        group.Condition = condition;
        return EvaluateCondition(group);
    }

    /// <summary>
    ///  Admits one operation through the evaluator's condition and design-time policy entry point.
    /// </summary>
    /// <param name="elementXml">The single item element to admit.</param>
    /// <param name="groupCondition">The already evaluated parent group condition.</param>
    /// <returns>
    ///  Whether the operation's group and item conditions were both <see langword="true"/>.
    /// </returns>
    public bool Admit(string elementXml, bool groupCondition = true)
    {
        ProjectRootElement root = CreateRoot($"<ItemGroup>{elementXml}</ItemGroup>", Path.Combine(DirectoryPath, "admission.proj"));
        ProjectItemElement element = root.Items.Single();
        return _instanceEvaluator is { } instance
            ? instance.EvaluateItemElement(DirectoryPath, element, groupCondition)
            : _editableEvaluator!.EvaluateItemElement(DirectoryPath, element, groupCondition);
    }

    /// <summary>
    ///  Gets the evaluator's deferred results, preserving condition and origin information.
    /// </summary>
    /// <returns>
    ///  An enumerable that materializes the current item histories when enumerated.
    /// </returns>
    public IEnumerable<ItemRecord> GetItems()
    {
        if (_instanceEvaluator is { } instanceEvaluator)
        {
            foreach (InstanceEvaluator.ItemData data in instanceEvaluator.GetAllItemsDeferred())
            {
                yield return new ItemRecord(data.Item, data.OriginatingItemElement, data.ElementOrder, data.ConditionResult);
            }
        }
        else
        {
            foreach (EditableEvaluator.ItemData data in _editableEvaluator!.GetAllItemsDeferred())
            {
                yield return new ItemRecord(data.Item, data.OriginatingItemElement, data.ElementOrder, data.ConditionResult);
            }
        }
    }

    /// <summary>
    ///  Writes a property into the outer data used by subsequent operation construction.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property's unevaluated value.</param>
    public void SetProperty(string name, string value)
    {
        if (_projectInstance is { } projectInstance)
        {
            projectInstance.SetProperty(name, value);
        }
        else
        {
            _project!.SetProperty(name, value);
        }
    }

    /// <summary>
    ///  Parses XML without evaluating its item groups.
    /// </summary>
    /// <param name="body">The contents of the project root.</param>
    /// <param name="path">The full path assigned to the root.</param>
    /// <returns>
    ///  The parsed root element.
    /// </returns>
    private ProjectRootElement CreateRoot(string body, string path)
    {
        using var reader = XmlReader.Create(new StringReader($"""
            <Project>
            {body}
            </Project>
            """));
        ProjectRootElement root = ProjectRootElement.Create(reader, _collection);
        root.FullPath = path;
        return root;
    }

    /// <summary>
    ///  Evaluates a group's or item's condition through the evaluator's current-state provider.
    /// </summary>
    /// <param name="element">The element containing the condition.</param>
    /// <returns>
    ///  The evaluated condition result.
    /// </returns>
    private bool EvaluateCondition(ProjectElement element)
    {
        if (_instanceEvaluator is { } instanceEvaluator)
        {
            return instanceEvaluator.EvaluateConditionWithCurrentState(
                element, ExpanderOptions.ExpandPropertiesAndItems, ParserOptions.AllowPropertiesAndItemLists);
        }

        return _editableEvaluator!.EvaluateConditionWithCurrentState(
            element, ExpanderOptions.ExpandPropertiesAndItems, ParserOptions.AllowPropertiesAndItemLists);
    }

    /// <summary>
    ///  Records a factory creation.
    /// </summary>
    private void CountCreation() => CreatedItems++;

    /// <summary>
    ///  Records a factory clone.
    /// </summary>
    private void CountClone() => ClonedItems++;

    /// <summary>
    ///  Describes an evaluated item together with its recording provenance.
    /// </summary>
    /// <param name="Item">The evaluated item.</param>
    /// <param name="OriginatingItemElement">The Include element that originally created the item.</param>
    /// <param name="ElementOrder">The global order of that Include element.</param>
    /// <param name="ConditionResult">Whether the originating operation's conditions were <see langword="true"/>.</param>
    public readonly record struct ItemRecord(
        IItem Item, ProjectItemElement OriginatingItemElement, int ElementOrder, bool ConditionResult);

    /// <summary>
    ///  Counts creations without replacing the real item model's cloning or metadata behavior.
    /// </summary>
    /// <typeparam name="T">The item model used by the wrapped factory.</typeparam>
    private sealed class CountingItemFactory<T> : IItemFactory<T, T> where T : class, IItem
    {
        /// <summary>
        ///  The real model-specific factory.
        /// </summary>
        private readonly IItemFactory<T, T> _factory;

        /// <summary>
        ///  The callback for every item creation.
        /// </summary>
        private readonly Action _created;

        /// <summary>
        ///  The callback for creations that copy an existing item.
        /// </summary>
        private readonly Action _cloned;

        /// <summary>
        ///  Initializes a counting decorator.
        /// </summary>
        /// <param name="factory">The real item factory.</param>
        /// <param name="created">The creation callback.</param>
        /// <param name="cloned">The cloning callback.</param>
        public CountingItemFactory(IItemFactory<T, T> factory, Action created, Action cloned)
        {
            _factory = factory;
            _created = created;
            _cloned = cloned;
        }

        /// <summary>
        ///  Gets or sets the wrapped factory's item type.
        /// </summary>
        public string ItemType
        {
            get => _factory.ItemType;
            set => _factory.ItemType = value;
        }

        /// <summary>
        ///  Sets the wrapped factory's originating XML element.
        /// </summary>
        public ProjectItemElement ItemElement
        {
            set => _factory.ItemElement = value;
        }

        /// <summary>
        ///  Counts and forwards literal item creation.
        /// </summary>
        /// <param name="include">The escaped item include.</param>
        /// <param name="definingProject">The defining project path.</param>
        /// <returns>
        ///  The created item.
        /// </returns>
        public T CreateItem(string include, string definingProject)
        {
            _created();
            return _factory.CreateItem(include, definingProject);
        }

        /// <summary>
        ///  Counts and forwards cloning of an existing item.
        /// </summary>
        /// <param name="source">The source item.</param>
        /// <param name="definingProject">The defining project path.</param>
        /// <returns>
        ///  The cloned item.
        /// </returns>
        public T CreateItem(T source, string definingProject)
        {
            _created();
            _cloned();
            return _factory.CreateItem(source, definingProject);
        }

        /// <summary>
        ///  Counts and forwards transformed item creation.
        /// </summary>
        /// <param name="include">The transformed escaped include.</param>
        /// <param name="baseItem">The source item whose metadata is copied.</param>
        /// <param name="definingProject">The defining project path.</param>
        /// <returns>
        ///  The created item.
        /// </returns>
        public T CreateItem(string include, T baseItem, string definingProject)
        {
            _created();
            _cloned();
            return _factory.CreateItem(include, baseItem, definingProject);
        }

        /// <summary>
        ///  Counts and forwards literal or glob-expanded item creation.
        /// </summary>
        /// <param name="include">The escaped evaluated include.</param>
        /// <param name="includeBeforeWildcardExpansion">The original escaped include.</param>
        /// <param name="definingProject">The defining project path.</param>
        /// <returns>
        ///  The created item.
        /// </returns>
        public T CreateItem(string include, string includeBeforeWildcardExpansion, string definingProject)
        {
            _created();
            return _factory.CreateItem(include, includeBeforeWildcardExpansion, definingProject);
        }

        /// <summary>
        ///  Preserves the real factory's bulk metadata behavior.
        /// </summary>
        /// <param name="metadata">The evaluated metadata to apply.</param>
        /// <param name="destinationItems">The items receiving the metadata.</param>
        public void SetMetadata(IEnumerable<KeyValuePair<ProjectMetadataElement, string>> metadata, IEnumerable<T> destinationItems)
            => _factory.SetMetadata(metadata, destinationItems);
    }
}
