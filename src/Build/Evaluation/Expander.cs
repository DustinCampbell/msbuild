// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using TaskItemFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.TaskItemFactory;

#nullable disable

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Expands item/property/metadata in expressions.
/// Encapsulates the data necessary for expansion.
/// </summary>
/// <remarks>
/// Requires the caller to explicitly state what they wish to expand at the point of expansion (explicitly does not have a field for ExpanderOptions).
/// Callers typically use a single expander in many locations, and this forces the caller to make explicit what they wish to expand at the point of expansion.
///
/// Requires the caller to have previously provided the necessary material for the expansion requested.
/// For example, if the caller requests ExpanderOptions.ExpandItems, the Expander will throw if it was not given items.
/// </remarks>
/// <typeparam name="P">Type of the properties used.</typeparam>
/// <typeparam name="I">Type of the items used.</typeparam>
internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// A limit for truncating string expansions within an evaluated Condition. Properties, item metadata, or item groups will be truncated to N characters such as 'N...'.
    /// Enabled by ExpanderOptions.Truncate.
    /// </summary>
    private const int CharacterLimitPerExpansion = 1024;
    /// <summary>
    /// A limit for truncating string expansions for item groups within an evaluated Condition. N items will be evaluated such as 'A;B;C;...'.
    /// Enabled by ExpanderOptions.Truncate.
    /// </summary>
    private const int ItemLimitPerExpansion = 3;

    /// <summary>
    /// The CultureInfo from the invariant culture. Used to avoid allocations for
    /// performing IndexOf etc.
    /// </summary>
    private static readonly CompareInfo s_invariantCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>
    /// Properties to draw on for expansion.
    /// </summary>
    private IPropertyProvider<P> _properties;

    /// <summary>
    /// Items to draw on for expansion.
    /// </summary>
    private IItemProvider<I> _items;

    /// <summary>
    /// Metadata to draw on for expansion.
    /// </summary>
    private IMetadataTable _metadata;

    /// <summary>
    /// Set of properties which are null during expansion.
    /// </summary>
    private PropertiesUseTracker _propertiesUseTracker;

    private readonly IFileSystem _fileSystem;

    private readonly LoggingContext _loggingContext;

    /// <summary>
    /// Non-null if the expander was constructed for evaluation.
    /// </summary>
    internal EvaluationContext EvaluationContext { get; }

    private Expander(IPropertyProvider<P> properties, LoggingContext loggingContext)
    {
        _properties = properties;
        _propertiesUseTracker = new PropertiesUseTracker(loggingContext);
        _loggingContext = loggingContext;
    }

    /// <summary>
    /// Creates an expander passing it some properties to use.
    /// Properties may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IFileSystem fileSystem, LoggingContext loggingContext)
        : this(properties, loggingContext)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Creates an expander passing it some properties to use.
    /// Properties may be null.
    ///
    /// Used for tests and for ToolsetReader - that operates agnostic on the project
    ///   - so no logging context is passed, and no BuildCheck check will be executed.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IFileSystem fileSystem)
    : this(properties, fileSystem, null)
    { }

    /// <summary>
    /// Creates an expander passing it some properties to use and the evaluation context.
    /// Properties may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, EvaluationContext evaluationContext,
        LoggingContext loggingContext)
        : this(properties, loggingContext)
    {
        _fileSystem = evaluationContext.FileSystem;
        EvaluationContext = evaluationContext;
    }

    /// <summary>
    /// Creates an expander passing it some properties and items to use.
    /// Either or both may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, IFileSystem fileSystem, LoggingContext loggingContext)
        : this(properties, fileSystem, loggingContext)
    {
        _items = items;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Expander{P, I}"/> class.
    /// Creates an expander passing it some properties and items to use, and the evaluation context.
    /// Either or both may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, EvaluationContext evaluationContext, LoggingContext loggingContext)
        : this(properties, evaluationContext, loggingContext)
    {
        _items = items;
    }

    /// <summary>
    /// Creates an expander passing it some properties, items, and/or metadata to use.
    /// Any or all may be null.
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, IMetadataTable metadata, IFileSystem fileSystem, LoggingContext loggingContext)
        : this(properties, items, fileSystem, loggingContext)
    {
        _metadata = metadata;
    }

    /// <summary>
    /// Creates an expander passing it some properties, items, and/or metadata to use.
    /// Any or all may be null.
    ///
    /// This is for the purpose of evaluations through API calls, that might not be able to pass the logging context
    ///  - BuildCheck checking won't be executed for those.
    /// (for one of the calls we can actually pass IDataConsumingContext - as we have logging service and project)
    ///
    /// </summary>
    internal Expander(IPropertyProvider<P> properties, IItemProvider<I> items, IMetadataTable metadata, IFileSystem fileSystem)
        : this(properties, items, fileSystem, null)
    {
        _metadata = metadata;
    }

    private Expander(
        IPropertyProvider<P> properties,
        IItemProvider<I> items,
        IMetadataTable metadata,
        IFileSystem fileSystem,
        EvaluationContext evaluationContext,
        LoggingContext loggingContext)
        : this(properties, items, metadata, fileSystem, loggingContext)
    {
        EvaluationContext = evaluationContext;
    }

    /// <summary>
    /// Recreates the expander with passed in logging context
    /// </summary>
    /// <param name="loggingContext"></param>
    /// <returns></returns>
    internal Expander<P, I> WithLoggingContext(LoggingContext loggingContext)
    {
        return new Expander<P, I>(_properties, _items, _metadata, _fileSystem, EvaluationContext, loggingContext);
    }

    /// <summary>
    /// Accessor for the metadata.
    /// Set temporarily during item metadata evaluation.
    /// </summary>
    internal IMetadataTable Metadata
    {
        get { return _metadata; }
        set { _metadata = value; }
    }

    /// <summary>
    /// If a property is expanded but evaluates to null then it is considered to be un-initialized.
    /// We want to keep track of these properties so that we can warn if the property gets set later on.
    /// </summary>
    internal PropertiesUseTracker PropertiesUseTracker
    {
        get { return _propertiesUseTracker; }
        set { _propertiesUseTracker = value; }
    }

    /// <summary>
    /// Tests to see if the expression may contain expandable expressions, i.e.
    /// contains $, % or @.
    /// </summary>
    internal static bool ExpressionMayContainExpandableExpressions(string expression)
    {
        return expression.AsSpan().IndexOfAny('$', '%', '@') >= 0;
    }

    /// <summary>
    /// Returns true if the expression contains an item vector pattern, else returns false.
    /// Used to flag use of item expressions where they are illegal.
    /// </summary>
    internal static bool ExpressionContainsItemVector(string expression)
        => ExpressionShredder.TryGetNextItemVectorExpression(expression, out _);

    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options.
    /// This is the standard form. Before using the expanded value, it must be unescaped, and this does that for you.
    ///
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    /// </summary>
    internal string ExpandIntoStringAndUnescape(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        string result = ExpandIntoStringLeaveEscaped(expression, options, elementLocation);

        return (result == null) ? null : EscapingUtilities.UnescapeAll(result);
    }

    /// <summary>
    ///  Expands the marker types selected by <paramref name="options"/> in metadata, property, then item-vector
    ///  order and leaves the result escaped.
    /// </summary>
    /// <param name="expression">The expression to expand.</param>
    /// <param name="options">The expansion pipelines and behavior to enable.</param>
    /// <param name="elementLocation">The location used to report expansion errors.</param>
    /// <returns>
    ///  The expanded, escaped string, or <see langword="null"/> when
    ///  <see cref="ExpanderOptions.BreakOnNotEmpty"/> stops expansion early.
    /// </returns>
    /// <remarks>
    ///  Only pipelines selected by <paramref name="options"/> are invoked. They run in a fixed order because
    ///  metadata expansion can produce property syntax, and metadata or property expansion can produce item-vector
    ///  syntax.
    ///  <para>
    ///   A non-empty <paramref name="expression"/> is scanned once for the first selected marker. Its index is reused
    ///   by each selected pipeline as either a known first marker or the starting point for further scanning. Property
    ///   expansion can adjust path-like prefixes and change their length, so item scanning restarts at the beginning
    ///   afterward.
    ///  </para>
    ///  <para>
    ///   When no selected marker is present in a non-empty <paramref name="expression"/>, the pipelines are skipped,
    ///   but their provider assumptions are still validated and file-path adjustment is still applied.
    ///  </para>
    ///  <para>
    ///   Use this form when the result will be processed further, such as when matching against the file system, so
    ///   escaped literals remain distinguishable. The caller is responsible for unescaping the result afterward.
    ///  </para>
    /// </remarks>
    internal string ExpandIntoStringLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        if (expression.Length == 0)
        {
            return string.Empty;
        }

        Assumed.NotNull(elementLocation);

        bool expandMetadata = (options & ExpanderOptions.ExpandMetadata) != 0;
        bool expandProperties = (options & ExpanderOptions.ExpandProperties) != 0;
        bool expandItems = (options & ExpanderOptions.ExpandItems) != 0;

        // Find the first selected marker once so the expansion pipelines can reuse its position.
        int markerIndex = GetFirstMarkerIndex(expression, expandProperties, expandItems, expandMetadata);
        if (markerIndex < 0)
        {
            // Selected pipelines normally validate their providers before scanning for their marker.
            VerifyExpansionProviders(options);
            return FileUtilities.MaybeAdjustFilePath(expression);
        }

        string result = expression;

        // Earlier pipelines can produce syntax consumed by later pipelines, so preserve this order.
        if (expandMetadata)
        {
            result = MetadataExpander.ExpandMetadataLeaveEscaped(result, markerIndex, _metadata, options, elementLocation, _loggingContext);
        }

        if (expandProperties)
        {
            result = PropertyExpander.ExpandPropertiesLeaveEscaped(result, markerIndex, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);

            // Property expansion may adjust path-like prefixes and change their length.
            // So, the marker index can't be trusted for the rest of the pipeline.
            markerIndex = 0;
        }

        if (expandItems)
        {
            result = ItemExpander.ExpandItemVectorsIntoString(this, result, markerIndex, _items, options, elementLocation);
        }

        result = FileUtilities.MaybeAdjustFilePath(result);

        return result;
    }

    private static int GetFirstMarkerIndex(string expression, bool expandProperties, bool expandItems, bool expandMetadata)
    {
        if (expandProperties)
        {
            if (expandItems)
            {
                return expandMetadata
                    ? ExpressionShredder.IndexOfAnyExpansionMarker(expression)
                    : ExpressionShredder.IndexOfPropertyOrItemVectorMarker(expression);
            }

            return expandMetadata
                ? ExpressionShredder.IndexOfPropertyOrMetadataMarker(expression)
                : ExpressionShredder.IndexOfPropertyMarker(expression);
        }

        if (expandItems)
        {
            return expandMetadata
                ? ExpressionShredder.IndexOfItemVectorOrMetadataMarker(expression)
                : ExpressionShredder.IndexOfItemVectorMarker(expression);
        }

        return expandMetadata
            ? ExpressionShredder.IndexOfMetadataMarker(expression)
            : -1;
    }

    private void VerifyExpansionProviders(ExpanderOptions options)
    {
        VerifyMetadataAndPropertyProviders(options);

        if ((options & ExpanderOptions.ExpandItems) != 0)
        {
            Assumed.NotNull(_items, "Cannot expand items without providing items");
        }
    }

    private void VerifyMetadataAndPropertyProviders(ExpanderOptions options)
    {
        if ((options & ExpanderOptions.ExpandMetadata) != 0)
        {
            Assumed.NotNull(_metadata, "Cannot expand metadata without providing metadata");
        }

        if ((options & ExpanderOptions.ExpandProperties) != 0)
        {
            Assumed.NotNull(_properties, "Cannot expand properties without providing properties");
        }
    }

    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options,
    /// then splits on semi-colons into a list of strings.
    /// Use this form when the result is going to be processed further, for example by matching against the file system,
    /// so literals must be distinguished, and you promise to unescape after that.
    /// </summary>
    internal SemiColonTokenizer ExpandIntoStringListLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        Assumed.True((options & ExpanderOptions.BreakOnNotEmpty) == 0, "not supported");

        return ExpressionShredder.SplitSemiColonSeparatedList(ExpandIntoStringLeaveEscaped(expression, options, elementLocation));
    }

    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options
    /// and produces a list of TaskItems.
    /// If the expression is empty, returns an empty list.
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    /// </summary>
    internal IList<TaskItem> ExpandIntoTaskItemsLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        return ExpandIntoItemsLeaveEscaped(expression, (IItemFactory<I, TaskItem>)TaskItemFactory.Instance, options, elementLocation);
    }

    /// <summary>
    ///  Expands the marker types selected by <paramref name="options"/> in metadata, property, then item-vector
    ///  order and creates items of type <typeparamref name="T"/> from the escaped result.
    /// </summary>
    /// <typeparam name="T">The type of items to return.</typeparam>
    /// <param name="expression">The expression to expand and split into items.</param>
    /// <param name="itemFactory">The factory used to create items from expanded item vectors and literals.</param>
    /// <param name="options">The expansion pipelines and behavior to enable.</param>
    /// <param name="elementLocation">The location used to report expansion errors and create literal items.</param>
    /// <returns>
    ///  The expanded items, an empty list when <paramref name="expression"/> produces no items, or
    ///  <see langword="null"/> when <see cref="ExpanderOptions.BreakOnNotEmpty"/> stops expansion early.
    /// </returns>
    /// <remarks>
    ///  Metadata and property expansion operate on the complete <paramref name="expression"/> before file-path
    ///  adjustment and semicolon splitting. Item vectors are then expanded from each non-empty split; splits that
    ///  are not item vectors are created as literal items.
    ///  <para>
    ///   The first selected marker is located once and its index is reused by the metadata and property pipelines.
    ///   When no selected marker is present, those pipelines are skipped and the adjusted expression is sent directly
    ///   to the literal-item path. The literal-item path is also used after metadata and property expansion when item
    ///   expansion is not selected.
    ///  </para>
    ///  <para>
    ///   Use this form when the items will be processed further, such as when matching against the file system, so
    ///   escaped literals remain distinguishable. The caller is responsible for unescaping item values afterward.
    ///  </para>
    /// </remarks>
    internal IList<T> ExpandIntoItemsLeaveEscaped<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, IElementLocation elementLocation)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            return Array.Empty<T>();
        }

        Assumed.NotNull(elementLocation);

        // The individual pipeline entry points assume that their corresponding option is enabled.
        bool expandMetadata = (options & ExpanderOptions.ExpandMetadata) != 0;
        bool expandProperties = (options & ExpanderOptions.ExpandProperties) != 0;
        bool expandItems = (options & ExpanderOptions.ExpandItems) != 0;

        // Find the first selected marker once so metadata and property expansion can reuse its position.
        int markerIndex = GetFirstMarkerIndex(expression, expandProperties, expandItems, expandMetadata);
        if (markerIndex < 0)
        {
            // No pipeline can introduce an item vector when the original expression has no selected marker.
            // Metadata and property providers still require validation, while an item provider is only required
            // after finding an actual item vector.
            VerifyMetadataAndPropertyProviders(options);
            expression = FileUtilities.MaybeAdjustFilePath(expression);
            return CreateLiteralItems(expression, itemFactory, options, elementLocation);
        }

        // Expand the complete expression before splitting. Metadata expansion precedes property expansion because
        // metadata values can contain property syntax.
        if (expandMetadata)
        {
            expression = MetadataExpander.ExpandMetadataLeaveEscaped(expression, markerIndex, _metadata, options, elementLocation);
        }

        if (expandProperties)
        {
            expression = PropertyExpander.ExpandPropertiesLeaveEscaped(expression, markerIndex, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
        }

        // Normalize path-like scalar results before they are split and materialized as items.
        expression = FileUtilities.MaybeAdjustFilePath(expression);

        if (expression.Length == 0)
        {
            return Array.Empty<T>();
        }

        // With item expansion disabled, item-vector syntax remains literal. Otherwise each split must be checked
        // because metadata or property expansion may have introduced an item vector.
        return !expandItems
            ? CreateLiteralItems(expression, itemFactory, options, elementLocation)
            : ExpandItems(this, expression, _items, itemFactory, options, elementLocation);

        static IList<T> CreateLiteralItems(
            string expression,
            IItemFactory<I, T> itemFactory,
            ExpanderOptions options,
            IElementLocation elementLocation)
        {
            var splitEnumerator = ExpressionShredder.SplitSemiColonSeparatedList(expression).GetEnumerator();

            // Empty splits are discarded, so a non-empty expression can still produce no items.
            if (!splitEnumerator.MoveNext())
            {
                return Array.Empty<T>();
            }

            // The first yielded split proves that this literal-only path would produce a non-empty result.
            if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
            {
                return null;
            }

            List<T> result = [];

            do
            {
                result.Add(itemFactory.CreateItem(splitEnumerator.Current, elementLocation.File));
            }
            while (splitEnumerator.MoveNext());

            return result;
        }

        static IList<T> ExpandItems(
            Expander<P, I> expander,
            string expression,
            IItemProvider<I> items,
            IItemFactory<I, T> itemFactory,
            ExpanderOptions options,
            IElementLocation elementLocation)
        {
            var splitEnumerator = ExpressionShredder.SplitSemiColonSeparatedList(expression).GetEnumerator();

            // Empty splits are discarded, so a non-empty expression can still produce no items.
            if (!splitEnumerator.MoveNext())
            {
                return Array.Empty<T>();
            }

            bool breakOnNotEmpty = (options & ExpanderOptions.BreakOnNotEmpty) != 0;
            List<T> result = [];

            do
            {
                string split = splitEnumerator.Current;
                IList<T> itemsToAdd = ItemExpander.ExpandSingleItemVectorExpressionIntoItems(expander, split, items, itemFactory, options, includeNullEntries: false, out _, elementLocation);

                // A null result means either that the split is a non-empty literal or that item expansion already
                // stopped for BreakOnNotEmpty. A non-empty item result also satisfies BreakOnNotEmpty.
                if (breakOnNotEmpty && itemsToAdd is null or { Count: > 0 })
                {
                    return null;
                }

                if (itemsToAdd != null)
                {
                    result.AddRange(itemsToAdd);
                }
                else
                {
                    // The expression is not of the form @(itemName). Therefore, treat it as a string
                    // and create a new item from that string.
                    result.Add(itemFactory.CreateItem(split, elementLocation.File));
                }
            }
            while (splitEnumerator.MoveNext());

            return result;
        }
    }

    /// <summary>
    /// This is a specialized method for the use of TargetUpToDateChecker and Evaluator.EvaluateItemXml only.
    ///
    /// Extracts the items in the given SINGLE item vector.
    /// For example, expands @(Compile->'%(foo)') to a set of items derived from the items in the "Compile" list.
    ///
    /// If there is in fact more than one vector in the expression, throws InvalidProjectFileException.
    ///
    /// If there are no item expressions in the expression (for example a literal "foo.cpp"), returns null.
    /// If expression expands to no items, returns an empty list.
    /// If item expansion is not allowed by the provided options, returns null.
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    ///
    /// If the expression is a transform, any transformations to an expression that evaluates to nothing (i.e., because
    /// an item has no value for a piece of metadata) are optionally indicated with a null entry in the list. This means
    /// that the length of the returned list is always the same as the length of the referenced item list in the input string.
    /// That's important for any correlation the caller wants to do.
    ///
    /// If expression was a transform, 'isTransformExpression' is true, otherwise false.
    ///
    /// Item type of the items returned is determined by the IItemFactory passed in; if the IItemFactory does not
    /// have an item type set on it, it will be given the item type of the item vector to use.
    /// </summary>
    /// <typeparam name="T">Type of the items that should be returned.</typeparam>
    internal IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, bool includeNullItems, out bool isTransformExpression, IElementLocation elementLocation)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            isTransformExpression = false;
            return Array.Empty<T>();
        }

        Assumed.NotNull(elementLocation);

        return ItemExpander.ExpandSingleItemVectorExpressionIntoItems(this, expression, _items, itemFactory, options, includeNullItems, out isTransformExpression, elementLocation);
    }

    internal static bool TryExpandSingleItemVectorExpression(
        string expression,
        ExpanderOptions options,
        IElementLocation elementLocation,
        out ExpressionShredder.ItemExpressionCapture itemVector)
        => ItemExpander.TryExpandSingleItemVectorExpression(expression, options, elementLocation, out itemVector);

    internal IList<T> ExpandExpressionCaptureIntoItems<T>(
        ExpressionShredder.ItemExpressionCapture expressionCapture, IItemProvider<I> items, IItemFactory<I, T> itemFactory,
        ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
        where T : class, IItem
    {
        return ItemExpander.ExpandExpressionCaptureIntoItems(expressionCapture, this, items, itemFactory, options,
            includeNullEntries, out isTransformExpression, elementLocation);
    }

    internal bool ExpandExpressionCapture(
        ExpressionShredder.ItemExpressionCapture expressionCapture,
        IElementLocation elementLocation,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        out List<TransformEntry> entries)
    {
        return ItemExpander.ExpandItemVector(this, expressionCapture, _items, elementLocation, options, includeNullEntries, out isTransformExpression, out entries);
    }

    private static string TruncateString(string metadataValue)
    {
#if NET
        metadataValue = string.Concat(metadataValue.AsSpan(0, CharacterLimitPerExpansion - 3), "...");
#else
        // PERF: We need the formatted, truncated string. Using something like a StringBuilder avoids
        // needing to use an unsafe block, but this is more efficient.
        string truncatedMetadataValue = metadataValue.Substring(0, CharacterLimitPerExpansion);
        unsafe
        {
            fixed (char* truncatedMetadataPointer = truncatedMetadataValue)
            {
                Span<char> destination = new Span<char>(truncatedMetadataPointer, truncatedMetadataValue.Length);
                "...".AsSpan().CopyTo(destination.Slice(CharacterLimitPerExpansion - 3));
                metadataValue = truncatedMetadataValue;
            }
        }
#endif
        return metadataValue;
    }

    /// <summary>
    /// Returns true if the supplied string contains a valid property name.
    /// </summary>
    private static bool IsValidPropertyName(string propertyName)
    {
        if (propertyName.Length == 0 || !XmlUtilities.IsValidInitialElementNameCharacter(propertyName[0]))
        {
            return false;
        }

        for (int n = 1; n < propertyName.Length; n++)
        {
            if (!XmlUtilities.IsValidSubsequentElementNameCharacter(propertyName[n]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns true if ExpanderOptions.Truncate is set and EscapeHatches.DoNotTruncateConditions is not set.
    /// </summary>
    private static bool IsTruncationEnabled(ExpanderOptions options)
    {
        return (options & ExpanderOptions.Truncate) != 0 && !Traits.Instance.EscapeHatches.DoNotTruncateConditions;
    }

    /// <summary>
    /// Scan for the closing bracket that matches the one we've already skipped;
    /// essentially, pushes and pops on a stack of parentheses to do this.
    /// Takes the expression and the index to start at.
    /// Returns the index of the matching parenthesis, or -1 if it was not found.
    /// </summary>
    private static int ScanForClosingParenthesis(ReadOnlySpan<char> expression, int index)
    {
        int nestLevel = 1;
        int length = expression.Length;

        // Scan for our closing ')'
        while (index < length && nestLevel > 0)
        {
            char character = expression[index];
            switch (character)
            {
                case '\'' or '`' or '"':
                    index++;
                    index = ScanForClosingQuote(character, expression, index);

                    if (index < 0)
                    {
                        return -1;
                    }

                    break;

                case '(':
                    nestLevel++;
                    break;

                case ')':
                    nestLevel--;
                    break;
            }

            index++;
        }

        // We will have parsed past the ')', so step back one character
        index--;

        return (nestLevel == 0) ? index : -1;
    }

    /// <summary>
    /// Skip all characters until we find the matching quote character.
    /// </summary>
    private static int ScanForClosingQuote(char quoteChar, ReadOnlySpan<char> expression, int index)
    {
        // Scan for our closing quoteChar
        int foundIndex = expression.Slice(index).IndexOf(quoteChar);
        return foundIndex < 0 ? -1 : foundIndex + index;
    }

    /// <summary>
    /// Extract the argument from the StringBuilder, handling nulls appropriately.
    /// </summary>
    private static string ExtractArgument(SpanBasedStringBuilder argumentBuilder)
    {
        // we reached the end of an argument, add the builder's final result
        // to our arguments.
        argumentBuilder.Trim();

        // We support passing of null through the argument constant value null
        if (argumentBuilder.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else
        {
            if (argumentBuilder.Length > 0)
            {
                if (argumentBuilder[0] == '\'' && argumentBuilder[argumentBuilder.Length - 1] == '\'')
                {
                    argumentBuilder.Trim('\'');
                }
                else if (argumentBuilder[0] == '`' && argumentBuilder[argumentBuilder.Length - 1] == '`')
                {
                    argumentBuilder.Trim('`');
                }
                else if (argumentBuilder[0] == '"' && argumentBuilder[argumentBuilder.Length - 1] == '"')
                {
                    argumentBuilder.Trim('"');
                }

                return argumentBuilder.ToString();
            }
            else
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Extract the first level of arguments from the content.
    /// Splits the content passed in at commas.
    /// Returns an array of unexpanded arguments.
    /// If there are no arguments, returns an empty array.
    /// </summary>
    private static string[] ExtractFunctionArguments(IElementLocation elementLocation, string expressionFunction, ReadOnlyMemory<char> argumentsMemory)
    {
        int argumentsContentLength = argumentsMemory.Length;
        ReadOnlySpan<char> argumentsSpan = argumentsMemory.Span;

        using SpanBasedStringBuilder argumentBuilder = Strings.GetSpanBasedStringBuilder();
        int? argumentStartIndex = null;

        // We iterate over the string in the for loop below. When we find an argument, instead of adding it to the argument
        // builder one-character-at-a-time, we remember the start index and then call this function when we find the end of
        // the argument. This appends the entire {start, end} span to the builder in one call.
        void FlushCurrentArgumentToArgumentBuilder(int argumentEndIndex)
        {
            if (argumentStartIndex.HasValue)
            {
                argumentBuilder.Append(argumentsMemory.Slice(argumentStartIndex.Value, argumentEndIndex - argumentStartIndex.Value));
                argumentStartIndex = null;
            }
        }

        // Iterate over the contents of the arguments extracting the
        // the individual arguments as we go
        List<string> arguments = null;
        for (int n = 0; n < argumentsContentLength; n++)
        {
            // We found a property expression.. skip over all of it.
            if ((n < argumentsContentLength - 1) && (argumentsSpan[n] == '$' && argumentsSpan[n + 1] == '('))
            {
                int nestedPropertyStart = n;
                n += 2; // skip over the opening '$('

                // Scan for the matching closing bracket, skipping any nested ones
                n = ScanForClosingParenthesis(argumentsSpan, n);

                if (n == -1)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                }

                FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: nestedPropertyStart);
                argumentBuilder.Append(argumentsMemory.Slice(nestedPropertyStart, (n - nestedPropertyStart) + 1));
            }
            else if (argumentsSpan[n] == '`' || argumentsSpan[n] == '"' || argumentsSpan[n] == '\'')
            {
                int quoteStart = n;
                n++; // skip over the opening quote

                n = ScanForClosingQuote(argumentsSpan[quoteStart], argumentsSpan, n);

                if (n == -1)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedQuote"));
                }

                FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: quoteStart);
                argumentBuilder.Append(argumentsMemory.Slice(quoteStart, (n - quoteStart) + 1));
            }
            else if (argumentsSpan[n] == ',')
            {
                FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: n);

                // We have reached the end of the current argument, go ahead and add it
                // to our list
                if (arguments is null)
                {
                    // get an upper limit for the size of the arguments list.
                    int argumentCount = 2;
                    for (int i = n + 1; i < argumentsContentLength; ++i)
                    {
                        if (argumentsSpan[i] == ',')
                        {
                            argumentCount++;
                        }
                    }

                    arguments = new List<string>(argumentCount);
                }

                arguments.Add(ExtractArgument(argumentBuilder));

                // Clear out the argument builder ready for the next argument
                argumentBuilder.Clear();
            }
            else
            {
                argumentStartIndex ??= n;
            }
        }

        // We reached the end of the string but we may have seen the start but not the end of the last (or only) argument so flush it now.
        FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: argumentsContentLength);

        // This will either be the one and only argument, or the last one
        // so add it to our list
        string finalArgument = ExtractArgument(argumentBuilder);
        if (arguments is null)
        {
            return [finalArgument];
        }
        else
        {
            arguments.Add(finalArgument);

            return arguments.ToArray();
        }
    }
}
