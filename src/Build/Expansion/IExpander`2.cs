// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Expansion;

/// <summary>
///  Defines the state and operations required to expand MSBuild expressions.
/// </summary>
/// <typeparam name="TProperty">The type of properties available during expansion.</typeparam>
/// <typeparam name="TItem">The type of items available during expansion.</typeparam>
/// <remarks>
///  Callers should obtain instances from <see cref="ExpanderFactory"/> so implementation selection remains
///  centralized.
/// </remarks>
internal interface IExpander<TProperty, TItem>
    where TProperty : class, IProperty
    where TItem : class, IItem
{
    /// <summary>
    ///  Gets the evaluation context, or <see langword="null"/> when the expander was not created for evaluation.
    /// </summary>
    EvaluationContext? EvaluationContext { get; }

    /// <summary>
    ///  Gets the metadata available in the current metadata scope.
    /// </summary>
    IMetadataTable? CurrentMetadata { get; }

    /// <summary>
    ///  Temporarily uses the specified metadata table during expansion.
    /// </summary>
    /// <param name="metadata">The metadata table to use until the returned scope is disposed.</param>
    /// <returns>
    ///  A scope that restores the previously active metadata table when disposed.
    /// </returns>
    /// <remarks>
    ///  Scopes may be nested and must be disposed in reverse order. The expander must not be used concurrently
    ///  while a metadata scope is active.
    /// </remarks>
    MetadataScope EnterMetadataScope(IMetadataTable metadata);

    /// <summary>
    ///  Gets the tracker that records property reads during expansion.
    /// </summary>
    PropertiesUseTracker PropertiesUseTracker { get; }

    /// <summary>
    ///  Expands the selected metadata, property, and item expressions, then unescapes the result.
    /// </summary>
    /// <param name="expression">The expression to expand.</param>
    /// <param name="options">The kinds of expressions to expand and any expansion behavior modifiers.</param>
    /// <param name="location">The project location associated with the expression.</param>
    /// <returns>
    ///  The expanded and unescaped string, or <see langword="null"/> when
    ///  <see cref="ExpanderOptions.BreakOnNotEmpty"/> causes expansion to stop early.
    /// </returns>
    string? ExpandIntoStringAndUnescape(string expression, ExpanderOptions options, IElementLocation location);

    /// <summary>
    ///  Expands the selected metadata, property, and item expressions while leaving the result escaped.
    /// </summary>
    /// <param name="expression">The expression to expand.</param>
    /// <param name="options">The kinds of expressions to expand and any expansion behavior modifiers.</param>
    /// <param name="location">The project location associated with the expression.</param>
    /// <returns>
    ///  The expanded escaped string, or <see langword="null"/> when
    ///  <see cref="ExpanderOptions.BreakOnNotEmpty"/> causes expansion to stop early.
    /// </returns>
    string? ExpandIntoStringLeaveEscaped(string expression, ExpanderOptions options, IElementLocation location);

    /// <summary>
    ///  Expands metadata and property expressions while preserving the runtime type of a property-function result.
    /// </summary>
    /// <param name="expression">The expression to expand.</param>
    /// <param name="options">The kinds of expressions to expand and any expansion behavior modifiers.</param>
    /// <param name="location">The project location associated with the expression.</param>
    /// <returns>
    ///  The expanded escaped value.
    /// </returns>
    object ExpandPropertiesLeaveTypedAndEscaped(string expression, ExpanderOptions options, IElementLocation location);

    /// <summary>
    ///  Expands the selected expressions while leaving the result escaped, then tokenizes the result on semicolons.
    /// </summary>
    /// <param name="expression">The expression to expand and tokenize.</param>
    /// <param name="options">The kinds of expressions to expand and any expansion behavior modifiers.</param>
    /// <param name="location">The project location associated with the expression.</param>
    /// <returns>
    ///  A tokenizer over the expanded escaped values.
    /// </returns>
    SemiColonTokenizer ExpandIntoStringListLeaveEscaped(string expression, ExpanderOptions options, IElementLocation location);

    /// <summary>
    ///  Expands the selected expressions into escaped items.
    /// </summary>
    /// <typeparam name="T">The type of items to return.</typeparam>
    /// <param name="expression">The expression to expand.</param>
    /// <param name="itemFactory">The factory used to create returned items.</param>
    /// <param name="options">The kinds of expressions to expand and any expansion behavior modifiers.</param>
    /// <param name="location">The project location associated with the expression.</param>
    /// <returns>
    ///  The expanded items, or <see langword="null"/> when <see cref="ExpanderOptions.BreakOnNotEmpty"/> causes
    ///  expansion to stop early.
    /// </returns>
    IList<T>? ExpandIntoItemsLeaveEscaped<T>(
        string expression,
        IItemFactory<TItem, T> itemFactory,
        ExpanderOptions options,
        IElementLocation location)
        where T : class, IItem;

    /// <summary>
    ///  Expands an expression consisting of one complete item vector into escaped items.
    /// </summary>
    /// <typeparam name="T">The type of items to return.</typeparam>
    /// <param name="expression">The item-vector expression to expand.</param>
    /// <param name="itemFactory">The factory used to create returned items.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="includeNullItems">
    ///  <see langword="true"/> to retain <see langword="null"/> transform results; otherwise,
    ///  <see langword="false"/>.
    /// </param>
    /// <param name="isTransformExpression">
    ///  Receives <see langword="true"/> when <paramref name="expression"/> is a transform; otherwise,
    ///  <see langword="false"/>.
    /// </param>
    /// <param name="location">The project location associated with the item-vector expression.</param>
    /// <returns>
    ///  The expanded items; an empty list when the item vector produces no items; or <see langword="null"/> when
    ///  item expansion is disabled, the expression is not an item vector, or expansion stops early.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">
    ///  <paramref name="expression"/> contains an item vector with surrounding text or more than one item vector.
    /// </exception>
    IList<T>? ExpandSingleItemVectorExpressionIntoItems<T>(
        string expression,
        IItemFactory<TItem, T> itemFactory,
        ExpanderOptions options,
        bool includeNullItems,
        out bool isTransformExpression,
        IElementLocation location)
        where T : class, IItem;

    /// <summary>
    ///  Tries to parse an expression consisting of one complete item vector.
    /// </summary>
    /// <param name="expression">The expression to parse.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="location">The project location associated with the expression.</param>
    /// <param name="itemVector">
    ///  Receives the parsed item-vector expression when the method returns <see langword="true"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> when item expansion is enabled and an item vector is found; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">
    ///  <paramref name="expression"/> contains an item vector with surrounding text or more than one item vector.
    /// </exception>
    bool TryExpandSingleItemVectorExpression(
        string expression,
        ExpanderOptions options,
        IElementLocation location,
        out ExpressionShredder.ItemExpressionCapture itemVector);

    /// <summary>
    ///  Expands a parsed item-vector expression into escaped items.
    /// </summary>
    /// <typeparam name="T">The type of items to return.</typeparam>
    /// <param name="itemVector">The parsed item-vector expression to expand.</param>
    /// <param name="items">The item provider from which referenced items are retrieved.</param>
    /// <param name="itemFactory">The factory used to create returned items.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="includeNullEntries">
    ///  <see langword="true"/> to retain <see langword="null"/> transform results; otherwise,
    ///  <see langword="false"/>.
    /// </param>
    /// <param name="isTransformExpression">
    ///  Receives <see langword="true"/> when <paramref name="itemVector"/> represents a transform; otherwise,
    ///  <see langword="false"/>.
    /// </param>
    /// <param name="location">The project location associated with the item-vector expression.</param>
    /// <returns>
    ///  The expanded items, or <see langword="null"/> when <see cref="ExpanderOptions.BreakOnNotEmpty"/> causes
    ///  expansion to stop early.
    /// </returns>
    IList<T>? ExpandItemVectorIntoItems<T>(
        ExpressionShredder.ItemExpressionCapture itemVector,
        IItemProvider<TItem> items,
        IItemFactory<TItem, T> itemFactory,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        IElementLocation location)
        where T : class, IItem;

    /// <summary>
    ///  Expands a parsed item-vector expression into transform entries.
    /// </summary>
    /// <param name="itemVector">The parsed item-vector expression to expand.</param>
    /// <param name="location">The project location associated with the item-vector expression.</param>
    /// <param name="options">The expansion options.</param>
    /// <param name="includeNullEntries">
    ///  <see langword="true"/> to retain <see langword="null"/> transform results; otherwise,
    ///  <see langword="false"/>.
    /// </param>
    /// <param name="isTransformExpression">
    ///  Receives <see langword="true"/> when <paramref name="itemVector"/> represents a transform; otherwise,
    ///  <see langword="false"/>.
    /// </param>
    /// <param name="entries">
    ///  Receives the expanded transform entries, or <see langword="null"/> when the item vector produces no entries.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> when <see cref="ExpanderOptions.BreakOnNotEmpty"/> causes expansion to stop early;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    bool ExpandItemVector(
        ExpressionShredder.ItemExpressionCapture itemVector,
        IElementLocation location,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        out List<TransformEntry<TItem>>? entries);
}
