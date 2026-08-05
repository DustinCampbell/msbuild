// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Text;
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
    {
        ExpressionShredder.ReferencedItemExpressionsEnumerator transformsEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

        return transformsEnumerator.MoveNext();
    }

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
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options.
    /// Use this form when the result is going to be processed further, for example by matching against the file system,
    /// so literals must be distinguished, and you promise to unescape after that.
    ///
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    /// </summary>
    internal string ExpandIntoStringLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        if (expression.Length == 0)
        {
            return String.Empty;
        }

        Assumed.NotNull(elementLocation);

        string result = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation, _loggingContext);
        result = PropertyExpander.ExpandPropertiesLeaveEscaped(result, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
        result = ItemExpander.ExpandItemVectorsIntoString(this, result, _items, options, elementLocation);
        result = FileUtilities.MaybeAdjustFilePath(result);

        return result;
    }

    /// <summary>
    /// Used only for unit tests. Expands the property expression (including any metadata expressions) and returns
    /// the result typed (i.e. not converted into a string if the result is a function return).
    /// </summary>
    internal object ExpandPropertiesLeaveTypedAndEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        if (expression.Length == 0)
        {
            return String.Empty;
        }

        Assumed.NotNull(elementLocation);

        string metaExpanded = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation);
        return PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(metaExpanded, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
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
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options
    /// and produces a list of items of the type for which it was specialized.
    /// If the expression is empty, returns an empty list.
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    ///
    /// Use this form when the result is going to be processed further, for example by matching against the file system,
    /// so literals must be distinguished, and you promise to unescape after that.
    /// </summary>
    /// <typeparam name="T">Type of items to return.</typeparam>
    internal IList<T> ExpandIntoItemsLeaveEscaped<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, IElementLocation elementLocation)
        where T : class, IItem
    {
        if (expression.Length == 0)
        {
            return Array.Empty<T>();
        }

        Assumed.NotNull(elementLocation);

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
            IList<T> itemsToAdd = ItemExpander.ExpandSingleItemVectorExpressionIntoItems(this, split, _items, itemFactory, options, false /* do not include null items */, out isTransformExpression, elementLocation);

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

    internal static ExpressionShredder.ItemExpressionCapture? ExpandSingleItemVectorExpressionIntoExpressionCapture(
            string expression, ExpanderOptions options, IElementLocation elementLocation)
    {
        return ItemExpander.ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, options, elementLocation);
    }

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
        return ItemExpander.ExpandExpressionCapture(this, expressionCapture, _items, elementLocation, options, includeNullEntries, out isTransformExpression, out entries);
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
    ///  Returns <see langword="true"/> if the supplied segment contains a valid property name.
    /// </summary>
    private static bool IsValidPropertyName(StringSegment propertyName)
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
    ///  Scans for the closing parenthesis that matches an opening parenthesis immediately preceding
    ///  <paramref name="expression"/>.
    /// </summary>
    /// <param name="expression">The expression beginning immediately after the opening parenthesis.</param>
    /// <param name="potentialPropertyFunction">
    ///  Receives a value indicating whether the expression may contain a property function.
    /// </param>
    /// <param name="potentialRegistryFunction">
    ///  Receives a value indicating whether the expression may contain a registry function.
    /// </param>
    /// <returns>
    ///  The index of the matching closing parenthesis relative to <paramref name="expression"/>, or
    ///  <c>-1</c> if no matching parenthesis is found.
    /// </returns>
    private static int ScanForClosingParenthesis(StringSegment expression, out bool potentialPropertyFunction, out bool potentialRegistryFunction)
    {
        int nestLevel = 1;
        int index = 0;
        int length = expression.Length;

        potentialPropertyFunction = false;
        potentialRegistryFunction = false;

        // Scan for the closing parenthesis.
        while (index < length && nestLevel > 0)
        {
            char character = expression[index];

            switch (character)
            {
                case '\'' or '`' or '"':
                    int closingQuoteIndex = expression.IndexOf(character, index + 1);
                    if (closingQuoteIndex < 0)
                    {
                        return -1;
                    }

                    index = closingQuoteIndex;
                    break;

                case '(':
                    nestLevel++;
                    break;

                case ')':
                    nestLevel--;
                    break;

                case '.':
                case '[':
                case '$':
                    potentialPropertyFunction = true;
                    break;

                case ':':
                    potentialRegistryFunction = true;
                    break;
            }

            index++;
        }

        // The scan advances past the last character inspected, so step back to its index.
        index--;

        return nestLevel == 0 ? index : -1;
    }

    /// <summary>
    ///  Extracts the top-level function arguments, splitting on commas outside nested property expressions
    ///  and quoted strings.
    /// </summary>
    /// <param name="expressionFunction">The complete function expression, used when reporting parse errors.</param>
    /// <param name="arguments">The non-empty, unexpanded function argument list.</param>
    /// <param name="elementLocation">The location of the function expression.</param>
    /// <returns>
    ///  An immutable array of unexpanded argument segments that view the original argument list.
    /// </returns>
    private static ImmutableArray<StringSegment> ExtractFunctionArguments(StringSegment expressionFunction, StringSegment arguments, IElementLocation elementLocation)
    {
        // Extract each top-level argument in a single pass.
        int index = 0;
        int argStart = 0;
        int length = arguments.Length;

        using RefArrayBuilder<StringSegment> builder = default;

        while (index < length)
        {
            char character = arguments[index];

            switch (character)
            {
                // Skip the entire nested property expression.
                case '$' when index < length - 1 && arguments[index + 1] == '(':
                    index += 2; // Skip the opening "$(".

                    // Find the matching closing parenthesis, accounting for nesting.
                    int closingParenIndex = ScanForClosingParenthesis(arguments[index..], out _, out _);

                    if (closingParenIndex == -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                    }

                    index += closingParenIndex;
                    break;

                case '`' or '"' or '\'':
                    index++; // Skip the opening quote.

                    int closingQuoteIndex = arguments.IndexOf(character, index);

                    if (closingQuoteIndex == -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedQuote"));
                    }

                    index = closingQuoteIndex;
                    break;

                case ',':
                    // Add the argument ending at this top-level comma.
                    builder.Add(ExtractArgument(arguments[argStart..index]));
                    argStart = index + 1;
                    break;
            }

            index++;
        }

        // Extract the only argument, or the final argument after the last comma.
        StringSegment finalArg = ExtractArgument(arguments[argStart..]);
        if (builder.IsEmpty)
        {
            return [finalArg];
        }

        builder.Add(finalArg);

        return builder.ToImmutable();

        // Trim whitespace and matching quote delimiters while preserving null argument semantics.
        static StringSegment ExtractArgument(StringSegment argument)
        {
            argument = argument.Trim();

            // The unquoted literal null represents a null argument.
            if (argument.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return default;
            }

            if (argument is [var quote, .., var closingQuote]
                && quote is '\'' or '`' or '"'
                && closingQuote == quote)
            {
                argument = argument.Trim(quote);
            }

            return argument;
        }
    }
}
