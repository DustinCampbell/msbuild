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
internal partial class Expander2<P, I> : IExpander<P, I>
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

#nullable enable

    /// <summary>
    /// Properties to draw on for expansion.
    /// </summary>
    private IPropertyProvider<P>? _properties;

    /// <summary>
    /// Items to draw on for expansion.
    /// </summary>
    private IItemProvider<I>? _items;

    /// <summary>
    /// Metadata to draw on for expansion.
    /// </summary>
    private IMetadataTable? _metadata;

    /// <summary>
    /// Set of properties which are null during expansion.
    /// </summary>
    private readonly PropertiesUseTracker _propertiesUseTracker;

    private readonly IFileSystem _fileSystem;

    private readonly LoggingContext? _loggingContext;

    /// <summary>
    /// Non-null if the expander was constructed for evaluation.
    /// </summary>
    public EvaluationContext? EvaluationContext { get; }

    public Expander2(
        IPropertyProvider<P>? properties,
        IItemProvider<I>? items,
        IMetadataTable? metadata,
        IFileSystem fileSystem,
        EvaluationContext? evaluationContext,
        LoggingContext? loggingContext)
    {
        _properties = properties;
        _items = items;
        _metadata = metadata;
        _fileSystem = fileSystem;
        EvaluationContext = evaluationContext;
        _loggingContext = loggingContext;

        _propertiesUseTracker = new PropertiesUseTracker(loggingContext);
    }

#nullable disable

    /// <summary>
    /// Accessor for the metadata.
    /// Set temporarily during item metadata evaluation.
    /// </summary>
    public IMetadataTable Metadata
    {
        get { return _metadata; }
        set { _metadata = value; }
    }

    /// <summary>
    /// If a property is expanded but evaluates to null then it is considered to be un-initialized.
    /// We want to keep track of these properties so that we can warn if the property gets set later on.
    /// </summary>
    public PropertiesUseTracker PropertiesUseTracker => _propertiesUseTracker;

    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options.
    /// This is the standard form. Before using the expanded value, it must be unescaped, and this does that for you.
    ///
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    /// </summary>
    public string ExpandIntoStringAndUnescape(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        string result = ExpandIntoStringLeaveEscaped(expression, options, elementLocation);

        return (result == null) ? null : EscapingUtilities.UnescapeAll(result);
    }

    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options.
    /// Use this form when the result is going to be processed further, for example by matching against the file system,
    /// so literals must be distinguished, and you promise to unescape after that.
    ///
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    /// </summary>
    public string ExpandIntoStringLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        if (expression.Length == 0)
        {
            return String.Empty;
        }

        ErrorUtilities.VerifyThrowInternalNull(elementLocation);

        string result = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation, _loggingContext);
        result = PropertyExpander.ExpandPropertiesLeaveEscaped(result, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
        result = ItemExpander.ExpandItemVectorsIntoString(this, result, _items, options, elementLocation);
        result = FileUtilities.MaybeAdjustFilePath(result);

        return result;
    }

    public object ExpandPropertiesLeaveTypedAndEscaped(
        string expression,
        ExpanderOptions options,
        IElementLocation elementLocation)
    {
        if (expression.Length == 0)
        {
            return string.Empty;
        }

        ErrorUtilities.VerifyThrowInternalNull(elementLocation);

        string metaExpanded = MetadataExpander.ExpandMetadataLeaveEscaped(
            expression,
            _metadata,
            options,
            elementLocation);

        return PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(
            metaExpanded,
            _properties,
            options,
            elementLocation,
            _propertiesUseTracker,
            _fileSystem);
    }

    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options,
    /// then splits on semi-colons into a list of strings.
    /// Use this form when the result is going to be processed further, for example by matching against the file system,
    /// so literals must be distinguished, and you promise to unescape after that.
    /// </summary>
    public SemiColonTokenizer ExpandIntoStringListLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        ErrorUtilities.VerifyThrow((options & ExpanderOptions.BreakOnNotEmpty) == 0, "not supported");

        return ExpressionShredder.SplitSemiColonSeparatedList(ExpandIntoStringLeaveEscaped(expression, options, elementLocation));
    }

    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options
    /// and produces a list of items of the type for which it was specialized.
    /// If the expression is empty, returns an empty list.
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    ///
    /// Use this form when the result is going to be processed further, for example by matching against the file system,
    /// so literals must be distinguished, and you promise to unescape after that.
    /// </summary>
    /// <typeparam name="T">Type of items to return.</typeparam>
    public IList<T> ExpandIntoItemsLeaveEscaped<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, IElementLocation elementLocation)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            return Array.Empty<T>();
        }

        ErrorUtilities.VerifyThrowInternalNull(elementLocation);

        expression = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation);
        expression = PropertyExpander.ExpandPropertiesLeaveEscaped(expression, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
        expression = FileUtilities.MaybeAdjustFilePath(expression);

        List<T> result = new List<T>();

        if (expression.Length == 0)
        {
            return result;
        }

        var splits = ExpressionShredder.SplitSemiColonSeparatedList(expression);
        foreach (string split in splits)
        {
            bool isTransformExpression;
            IList<T> itemsToAdd = ItemExpander.ExpandSingleItemVectorExpressionIntoItems(this, split, _items, itemFactory, options, includeNullEntries: false, out isTransformExpression, elementLocation);

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
                T itemToAdd = itemFactory.CreateItem(split, elementLocation.File);

                result.Add(itemToAdd);
            }
        }

        return result;
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
    public IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, bool includeNullItems, out bool isTransformExpression, IElementLocation elementLocation)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            isTransformExpression = false;
            return Array.Empty<T>();
        }

        ErrorUtilities.VerifyThrowInternalNull(elementLocation);

        return ItemExpander.ExpandSingleItemVectorExpressionIntoItems(this, expression, _items, itemFactory, options, includeNullItems, out isTransformExpression, elementLocation);
    }

    public IList<T> ExpandExpressionCaptureIntoItems<T>(
        ExpressionShredder.ItemExpressionCapture expressionCapture,
        IItemProvider<I> items,
        IItemFactory<I, T> itemFactory,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        IElementLocation elementLocation)
        where T : class, IItem
        => ItemExpander.ExpandExpressionCaptureIntoItems(expressionCapture, this, items, itemFactory, options,
            includeNullEntries, out isTransformExpression, elementLocation);

    public bool ExpandExpressionCapture(
        ExpressionShredder.ItemExpressionCapture expressionCapture,
        IElementLocation elementLocation,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        out List<KeyValuePair<string, I>> itemsFromCapture)
        => ItemExpander.ExpandExpressionCapture(this, expressionCapture, _items, elementLocation, options, includeNullEntries, out isTransformExpression, out itemsFromCapture);

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
}
