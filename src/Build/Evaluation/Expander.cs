// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;

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
    /// The CultureInfo from the invariant culture. Used to avoid allocations for
    /// performing IndexOf etc.
    /// </summary>
    private static readonly CompareInfo s_invariantCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

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
        _fileSystem = fileSystem;
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

        string result = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, location, _loggingContext);
        result = PropertyExpander.ExpandPropertiesLeaveEscaped(result, _properties, options, location, _propertiesUseTracker, _fileSystem);
        result = ItemExpander.ExpandItemVectorsIntoString(this, result, _items, options, location);
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

        expression = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, location);
        expression = PropertyExpander.ExpandPropertiesLeaveEscaped(expression, _properties, options, location, _propertiesUseTracker, _fileSystem);
        expression = FileUtilities.MaybeAdjustFilePath(expression);

        List<T> result = [];

        if (expression.Length == 0)
        {
            return result;
        }

        foreach (string split in ExpressionShredder.SplitSemiColonSeparatedList(expression))
        {
            IList<T> itemsToAdd = ItemExpander.ExpandSingleItemVectorExpressionIntoItems(
                expander: this,
                split,
                _items,
                itemFactory,
                options,
                includeNullEntries: false,
                out _,
                location);

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
            expander: this,
            expression,
            _items,
            itemFactory,
            options,
            includeNullItems,
            out isTransformExpression,
            location);
    }

    public bool TryExpandSingleItemVectorExpression(
        string expression,
        ExpanderOptions options,
        IElementLocation elementLocation,
        out ExpressionShredder.ItemExpressionCapture itemVector)
        => ItemExpander.TryExpandSingleItemVectorExpression(expression, options, elementLocation, out itemVector);

    public IList<T> ExpandItemVectorIntoItems<T>(
        ExpressionShredder.ItemExpressionCapture itemVector,
        IItemProvider<I> items,
        IItemFactory<I, T> itemFactory,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        IElementLocation location)
        where T : class, IItem
        => ItemExpander.ExpandItemVectorIntoItems(
            itemVector,
            expander: this,
            items,
            itemFactory,
            options,
            includeNullEntries,
            out isTransformExpression,
            location);

    public bool ExpandItemVector(
        ExpressionShredder.ItemExpressionCapture itemVector,
        IElementLocation location,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        out List<TransformEntry<I>> entries)
        => ItemExpander.ExpandItemVector(
            expander: this,
            itemVector,
            _items,
            location,
            options,
            includeNullEntries,
            out isTransformExpression,
            out entries);

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
        => (options & ExpanderOptions.Truncate) != 0 && !Traits.Instance.EscapeHatches.DoNotTruncateConditions;

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
            if (n < argumentsContentLength - 1 && argumentsSpan[n] == '$' && argumentsSpan[n + 1] == '(')
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
                argumentBuilder.Append(argumentsMemory.Slice(nestedPropertyStart, n - nestedPropertyStart + 1));
            }
            else if (argumentsSpan[n] is '`' or '"' or '\'')
            {
                int quoteStart = n;
                n++; // skip over the opening quote

                n = ScanForClosingQuote(argumentsSpan[quoteStart], argumentsSpan, n);

                if (n == -1)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedQuote"));
                }

                FlushCurrentArgumentToArgumentBuilder(argumentEndIndex: quoteStart);
                argumentBuilder.Append(argumentsMemory.Slice(quoteStart, n - quoteStart + 1));
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

                    arguments = [with(argumentCount)];
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

            return [.. arguments];
        }
    }
}
