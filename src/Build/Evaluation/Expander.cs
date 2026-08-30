// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

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
internal partial class Expander<P, I> : IExpander<P, I>, IMetadataScopeOwner
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
    /// Properties to draw on for expansion.
    /// </summary>
    private readonly IPropertyProvider<P> _properties;

    /// <summary>
    /// Items to draw on for expansion.
    /// </summary>
    private readonly IItemProvider<I> _items;

    /// <summary>
    /// Metadata to draw on for expansion.
    /// </summary>
    private IMetadataTable _metadata;

    private int _metadataScopeDepth;

    /// <summary>
    /// Set of properties which are null during expansion.
    /// </summary>
    private readonly PropertiesUseTracker _propertiesUseTracker;

    private readonly IFileSystem _fileSystem;

    private readonly LoggingContext _loggingContext;

    /// <summary>
    /// Non-null if the expander was constructed for evaluation.
    /// </summary>
    public EvaluationContext EvaluationContext { get; }

    public Expander(
        IPropertyProvider<P> properties,
        IItemProvider<I> items,
        IMetadataTable metadata,
        IFileSystem fileSystem,
        LoggingContext loggingContext,
        EvaluationContext evaluationContext)
    {
        _properties = properties;
        _items = items;
        _metadata = metadata;
        _propertiesUseTracker = new PropertiesUseTracker(loggingContext);
        _fileSystem = fileSystem ?? evaluationContext?.FileSystem ?? FileSystems.Default;
        _loggingContext = loggingContext;
        EvaluationContext = evaluationContext;
    }

    public IMetadataTable CurrentMetadata => _metadata;

    public MetadataScope EnterMetadataScope(IMetadataTable metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        IMetadataTable previousMetadata = _metadata;
        int depth = ++_metadataScopeDepth;
        _metadata = metadata;

        return new MetadataScope(this, previousMetadata, depth);
    }

    public PropertiesUseTracker PropertiesUseTracker => _propertiesUseTracker;

    void IMetadataScopeOwner.LeaveMetadataScope(int depth, IMetadataTable previousMetadata)
    {
        Assumed.True(depth == _metadataScopeDepth, "Metadata scopes must be disposed in reverse order.");

        _metadata = previousMetadata;
        _metadataScopeDepth--;
    }

    public string ExpandIntoStringAndUnescape(string expression, ExpanderOptions options, IElementLocation location)
    {
        string result = ExpandIntoStringLeaveEscaped(expression, options, location);

        return result != null
            ? EscapingUtilities.UnescapeAll(result)
            : null;
    }

    public string ExpandIntoStringLeaveEscaped(string expression, ExpanderOptions options, IElementLocation location)
    {
        if (expression.Length == 0)
        {
            return string.Empty;
        }

        Assumed.NotNull(location);

        ExpansionContext context = new(this, options, location);

        string result = MetadataExpander.ExpandMetadataLeaveEscaped(expression, context);
        result = PropertyExpander.ExpandPropertiesLeaveEscaped(result, context);
        result = ItemExpander.ExpandItemVectorsIntoString(result, context);
        result = FileUtilities.MaybeAdjustFilePath(result);

        return result;
    }

    public SemiColonTokenizer ExpandIntoStringListLeaveEscaped(string expression, ExpanderOptions options, IElementLocation location)
    {
        Assumed.True((options & ExpanderOptions.BreakOnNotEmpty) == 0, "not supported");

        return ExpressionShredder.SplitSemiColonSeparatedList(ExpandIntoStringLeaveEscaped(expression, options, location));
    }

    public IList<T> ExpandIntoItemsLeaveEscaped<T>(
        string expression,
        IItemFactory<I, T> itemFactory,
        ExpanderOptions options,
        IElementLocation location)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            return Array.Empty<T>();
        }

        Assumed.NotNull(location);

        ExpansionContext context = new(this, options, location);

#if !NET
        int expansionOptions = (int)(options & ExpanderOptions.ExpandAll);
        bool expandLiteralDirectly = false;

        if (expansionOptions > (int)ExpanderOptions.ExpandProperties &&
            expansionOptions != (int)ExpanderOptions.ExpandItems)
        {
            bool containsSelectedMarker;
            if ((expansionOptions & (int)ExpanderOptions.ExpandProperties) != 0)
            {
                if ((expansionOptions & (int)ExpanderOptions.ExpandItems) != 0)
                {
                    containsSelectedMarker = (expansionOptions & (int)ExpanderOptions.ExpandMetadata) != 0
                        ? ExpressionShredder.ContainsAnyExpansionMarker(expression)
                        : ExpressionShredder.ContainsPropertyOrItemVectorMarker(expression);
                }
                else
                {
                    containsSelectedMarker = ExpressionShredder.ContainsPropertyOrMetadataMarker(expression);
                }
            }
            else
            {
                containsSelectedMarker = ExpressionShredder.ContainsItemVectorOrMetadataMarker(expression);
            }

            expandLiteralDirectly = !containsSelectedMarker;
        }

        if (expandLiteralDirectly)
        {
            if ((options & ExpanderOptions.ExpandMetadata) != 0)
            {
                Assumed.NotNull(context.Metadata, "Cannot expand metadata without providing metadata");
            }

            if ((options & ExpanderOptions.ExpandProperties) != 0)
            {
                Assumed.NotNull(context.Properties, "Cannot expand properties without providing properties");
            }
        }
        else
        {
#endif
            expression = MetadataExpander.ExpandMetadataLeaveEscaped(expression, context);
            expression = PropertyExpander.ExpandPropertiesLeaveEscaped(expression, context);
#if !NET
        }
#endif
        expression = FileUtilities.MaybeAdjustFilePath(expression);

        List<T> result = [];

        if (expression.Length == 0)
        {
            return result;
        }

        foreach (string split in ExpressionShredder.SplitSemiColonSeparatedList(expression))
        {
#if !NET
            IList<T> itemsToAdd = expandLiteralDirectly
                ? null
                : ItemExpander.ExpandSingleItemVectorExpressionIntoItems(
                    split,
                    itemFactory,
                    includeNullEntries: false,
                    out _,
                    context);
#else
            IList<T> itemsToAdd = ItemExpander.ExpandSingleItemVectorExpressionIntoItems(
                split,
                itemFactory,
                includeNullEntries: false,
                out _,
                context);
#endif

            if ((itemsToAdd == null /* broke out early non empty */ || (itemsToAdd.Count > 0)) && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
            {
                return null;
            }

            if (itemsToAdd != null)
            {
                result.AddRange(itemsToAdd);
            }
            else
            {
                // The expression is not of the form @(itemName).  Therefore, just
                // treat it as a string, and create a new item from that string.
                T itemToAdd = itemFactory.CreateItem(split, location.File);

                result.Add(itemToAdd);
            }
        }

        return result;
    }

    public IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(
        string expression,
        IItemFactory<I, T> itemFactory,
        ExpanderOptions options,
        bool includeNullItems,
        out bool isTransformExpression,
        IElementLocation location)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            isTransformExpression = false;
            return Array.Empty<T>();
        }

        Assumed.NotNull(location);

        return ItemExpander.ExpandSingleItemVectorExpressionIntoItems(
            expression,
            itemFactory,
            includeNullItems,
            out isTransformExpression,
            new ExpansionContext(this, options, location));
    }

    public bool TryExpandSingleItemVectorExpression(
        string expression,
        ExpanderOptions options,
        IElementLocation elementLocation,
        out ExpressionShredder.ItemExpressionCapture itemVector)
        => ItemExpander.TryExpandSingleItemVectorExpression(
            expression,
            options,
            new ErrorReporter(elementLocation),
            out itemVector);

    public IList<T> ExpandItemVectorIntoItems<T>(
        ExpressionShredder.ItemExpressionCapture itemVector,
        IItemProvider<I> items,
        IItemFactory<I, T> itemFactory,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        IElementLocation location)
        where T : class, IItem
    {
        ExpansionContext context = new(this, options, location);
        return ItemExpander.ExpandItemVectorIntoItems(
            itemVector,
            items,
            itemFactory,
            includeNullEntries,
            out isTransformExpression,
            context);
    }

    public bool ExpandItemVector(
        ExpressionShredder.ItemExpressionCapture itemVector,
        IElementLocation location,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        out List<TransformEntry<I>> entries)
        => ItemExpander.ExpandItemVector(
            itemVector,
            _items,
            includeNullEntries,
            out isTransformExpression,
            out entries,
            new ExpansionContext(this, options, location));

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
                Span<char> destination = new(truncatedMetadataPointer, truncatedMetadataValue.Length);
                "...".AsSpan().CopyTo(destination.Slice(CharacterLimitPerExpansion - 3));
                metadataValue = truncatedMetadataValue;
            }
        }
#endif
        return metadataValue;
    }

    /// <summary>
    /// Returns true if ExpanderOptions.Truncate is set and EscapeHatches.DoNotTruncateConditions is not set.
    /// </summary>
    private static bool IsTruncationEnabled(ExpanderOptions options)
        => (options & ExpanderOptions.Truncate) != 0 && !Traits.Instance.EscapeHatches.DoNotTruncateConditions;
}
