// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
#if NET
using System.IO;
#else
using Microsoft.IO;
#endif
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using Microsoft.Win32;
using AvailableStaticMethods = Microsoft.Build.Internal.AvailableStaticMethods;
using ItemSpecModifiers = Microsoft.Build.Shared.FileUtilities.ItemSpecModifiers;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using TaskItemFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.TaskItemFactory;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Indicates to the expander what exactly it should expand.
    /// </summary>
    [Flags]
    internal enum ExpanderOptions
    {
        /// <summary>
        /// Invalid
        /// </summary>
        Invalid = 0x0,

        /// <summary>
        /// Expand bare custom metadata, like %(foo), but not built-in
        /// metadata, such as %(filename) or %(identity)
        /// </summary>
        ExpandCustomMetadata = 0x1,

        /// <summary>
        /// Expand bare built-in metadata, such as %(filename) or %(identity)
        /// </summary>
        ExpandBuiltInMetadata = 0x2,

        /// <summary>
        /// Expand all bare metadata
        /// </summary>
        ExpandMetadata = ExpandCustomMetadata | ExpandBuiltInMetadata,

        /// <summary>
        /// Expand only properties
        /// </summary>
        ExpandProperties = 0x4,

        /// <summary>
        /// Expand only item list expressions
        /// </summary>
        ExpandItems = 0x8,

        /// <summary>
        /// If the expression is going to not be an empty string, break
        /// out early
        /// </summary>
        BreakOnNotEmpty = 0x10,

        /// <summary>
        /// When an error occurs expanding a property, just leave it unexpanded.
        /// </summary>
        /// <remarks>
        /// This should only be used in cases where property evaluation isn't critical, such as when attempting to log a
        /// message with a best effort expansion of a string, or when discovering partial information during lazy evaluation.
        /// </remarks>
        LeavePropertiesUnexpandedOnError = 0x20,

        /// <summary>
        /// When an expansion occurs, truncate it to Expander.DefaultTruncationCharacterLimit or Expander.DefaultTruncationItemLimit.
        /// </summary>
        Truncate = 0x40,

        /// <summary>
        /// Issues build message if item references unqualified or qualified metadata odf self - as this can lead to unintended expansion and
        ///  cross-combination of other items.
        /// More info: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-batching#item-batching-on-self-referencing-metadata
        /// </summary>
        LogOnItemMetadataSelfReference = 0x80,

        /// <summary>
        /// Expand only properties and then item lists
        /// </summary>
        ExpandPropertiesAndItems = ExpandProperties | ExpandItems,

        /// <summary>
        /// Expand only bare metadata and then properties
        /// </summary>
        ExpandPropertiesAndMetadata = ExpandMetadata | ExpandProperties,

        /// <summary>
        /// Expand only bare custom metadata and then properties
        /// </summary>
        ExpandPropertiesAndCustomMetadata = ExpandCustomMetadata | ExpandProperties,

        /// <summary>
        /// Expand bare metadata, then properties, then item expressions
        /// </summary>
        ExpandAll = ExpandMetadata | ExpandProperties | ExpandItems
    }

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
        /// A helper struct wrapping a <see cref="SpanBasedStringBuilder"/> and providing file path conversion
        /// as used in e.g. property expansion.
        /// </summary>
        /// <remarks>
        /// If exactly one value is added and no concatenation takes places, this value is returned without
        /// conversion. In other cases values are stringified and attempted to be interpreted as file paths
        /// before concatenation.
        /// </remarks>
        private struct SpanBasedConcatenator : IDisposable
        {
            /// <summary>
            /// The backing <see cref="SpanBasedStringBuilder"/>, null until the second value is added.
            /// </summary>
            private SpanBasedStringBuilder _builder;

            /// <summary>
            /// The first value added to the concatenator. Tracked in its own field so it can be returned
            /// without conversion if no concatenation takes place.
            /// </summary>
            private object _firstObject;

            /// <summary>
            /// The first value added to the concatenator if it is a span. Tracked in its own field so the
            /// <see cref="SpanBasedStringBuilder"/> functionality doesn't have to be invoked if no concatenation
            /// takes place.
            /// </summary>
            private ReadOnlyMemory<char> _firstSpan;

            /// <summary>
            /// True if this instance is already disposed.
            /// </summary>
            private bool _disposed;

            /// <summary>
            /// Adds an object to be concatenated.
            /// </summary>
            public void Add(object obj)
            {
                CheckDisposed();
                FlushFirstValueIfNeeded();
                if (_builder != null)
                {
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(obj.ToString()));
                }
                else
                {
                    _firstObject = obj;
                }
            }

            /// <summary>
            /// Adds a span to be concatenated.
            /// </summary>
            public void Add(ReadOnlyMemory<char> span)
            {
                CheckDisposed();
                FlushFirstValueIfNeeded();
                if (_builder != null)
                {
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(span));
                }
                else
                {
                    _firstSpan = span;
                }
            }

            /// <summary>
            /// Returns the result of the concatenation.
            /// </summary>
            /// <returns>
            /// If only one value has been added and it is not a string, it is returned unchanged.
            /// In all other cases (no value, one string value, multiple values) the result is a
            /// concatenation of the string representation of the values, each additionally subjected
            /// to file path adjustment.
            /// </returns>
            public readonly object GetResult()
            {
                CheckDisposed();
                if (_firstObject != null)
                {
                    return (_firstObject is string stringValue) ? FileUtilities.MaybeAdjustFilePath(stringValue) : _firstObject;
                }
                return _firstSpan.IsEmpty
                    ? _builder?.ToString() ?? string.Empty
                    : FileUtilities.MaybeAdjustFilePath(_firstSpan).ToString();
            }

            /// <summary>
            /// Disposes of the struct by delegating the call to the underlying <see cref="SpanBasedStringBuilder"/>.
            /// </summary>
            public void Dispose()
            {
                CheckDisposed();
                _builder?.Dispose();
                _disposed = true;
            }

            /// <summary>
            /// Throws <see cref="ObjectDisposedException"/> if this concatenator is already disposed.
            /// </summary>
            private readonly void CheckDisposed() =>
                ErrorUtilities.VerifyThrowObjectDisposed(!_disposed, nameof(SpanBasedConcatenator));

            /// <summary>
            /// Lazily initializes <see cref="_builder"/> and populates it with the first value
            /// when the second value is being added.
            /// </summary>
            private void FlushFirstValueIfNeeded()
            {
                if (_firstObject != null)
                {
                    _builder = Strings.GetSpanBasedStringBuilder();
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstObject.ToString()));
                    _firstObject = null;
                }
                else if (!_firstSpan.IsEmpty)
                {
                    _builder = Strings.GetSpanBasedStringBuilder();
#if FEATURE_SPAN
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstSpan));
#else
                    _builder.Append(FileUtilities.MaybeAdjustFilePath(_firstSpan.ToString()));
#endif
                    _firstSpan = new ReadOnlyMemory<char>();
                }
            }
        }

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

            ErrorUtilities.VerifyThrowInternalNull(elementLocation);

            string result = MetadataExpander.ExpandMetadataLeaveEscaped(expression, _metadata, options, elementLocation, _loggingContext);
            result = PropertyExpander.ExpandPropertiesLeaveEscaped(result, _properties, options, elementLocation, _propertiesUseTracker, _fileSystem);
            result = ItemExpander.ExpandItemVectorsIntoString<I>(this, result, _items, options, elementLocation);
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

            ErrorUtilities.VerifyThrowInternalNull(elementLocation);

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
            ErrorUtilities.VerifyThrow((options & ExpanderOptions.BreakOnNotEmpty) == 0, "not supported");

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
                IList<T> itemsToAdd = ItemExpander.ExpandSingleItemVectorExpressionIntoItems<I, T>(this, split, _items, itemFactory, options, false /* do not include null items */, out isTransformExpression, elementLocation);

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

            ErrorUtilities.VerifyThrowInternalNull(elementLocation);

            return ItemExpander.ExpandSingleItemVectorExpressionIntoItems(this, expression, _items, itemFactory, options, includeNullItems, out isTransformExpression, elementLocation);
        }

        internal static ExpressionShredder.ItemExpressionCapture? ExpandSingleItemVectorExpressionIntoExpressionCapture(
                string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            return ItemExpander.ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, options, elementLocation);
        }

        internal IList<T> ExpandExpressionCaptureIntoItems<S, T>(
                ExpressionShredder.ItemExpressionCapture expressionCapture, IItemProvider<S> items, IItemFactory<S, T> itemFactory,
                ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
            where S : class, IItem
            where T : class, IItem
        {
            return ItemExpander.ExpandExpressionCaptureIntoItems<S, T>(expressionCapture, this, items, itemFactory, options,
                includeNullEntries, out isTransformExpression, elementLocation);
        }

        internal bool ExpandExpressionCapture(
            ExpressionShredder.ItemExpressionCapture expressionCapture,
            IElementLocation elementLocation,
            ExpanderOptions options,
            bool includeNullEntries,
            out bool isTransformExpression,
            out List<KeyValuePair<string, I>> itemsFromCapture)
        {
            return ItemExpander.ExpandExpressionCapture(this, expressionCapture, _items, elementLocation, options, includeNullEntries, out isTransformExpression, out itemsFromCapture);
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
        /// Also returns flags to indicate if a propertyfunction or registry property is likely
        /// to be found in the expression.
        /// </summary>
        private static int ScanForClosingParenthesis(ReadOnlySpan<char> expression, int index, out bool potentialPropertyFunction, out bool potentialRegistryFunction)
        {
            int nestLevel = 1;
            int length = expression.Length;

            potentialPropertyFunction = false;
            potentialRegistryFunction = false;

            // Scan for our closing ')'
            while (index < length && nestLevel > 0)
            {
                char character = expression[index];
                switch (character)
                {
                    case '\'':
                    case '`':
                    case '"':
                        {
                            index++;
                            index = ScanForClosingQuote(character, expression, index);

                            if (index < 0)
                            {
                                return -1;
                            }
                            break;
                        }
                    case '(':
                        {
                            nestLevel++;
                            break;
                        }
                    case ')':
                        {
                            nestLevel--;
                            break;
                        }
                    case '.':
                    case '[':
                    case '$':
                        {
                            potentialPropertyFunction = true;
                            break;
                        }
                    case ':':
                        {
                            potentialRegistryFunction = true;
                            break;
                        }
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
                    n = ScanForClosingParenthesis(argumentsSpan, n, out _, out _);

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

        /// <summary>
        /// Expands bare metadata expressions, like %(Compile.WarningLevel), or unqualified, like %(Compile).
        /// </summary>
        /// <remarks>
        /// This is a private nested class, exposed only through the Expander class.
        /// That allows it to hide its private methods even from Expander.
        /// </remarks>
        private static class MetadataExpander
        {
            /// <summary>
            /// Expands all embedded item metadata in the given string, using the bucketed items.
            /// Metadata may be qualified, like %(Compile.WarningLevel), or unqualified, like %(Compile).
            /// </summary>
            /// <param name="expression">The expression containing item metadata references.</param>
            /// <param name="metadata">The metadata to be expanded.</param>
            /// <param name="options">Used to specify what to expand.</param>
            /// <param name="elementLocation">The location information for error reporting purposes.</param>
            /// <param name="loggingContext">The logging context for this operation.</param>
            /// <returns>The string with item metadata expanded in-place, escaped.</returns>
            internal static string ExpandMetadataLeaveEscaped(string expression, IMetadataTable metadata, ExpanderOptions options, IElementLocation elementLocation, LoggingContext loggingContext = null)
            {
                try
                {
                    if ((options & ExpanderOptions.ExpandMetadata) == 0)
                    {
                        return expression;
                    }

                    ErrorUtilities.VerifyThrow(metadata != null, "Cannot expand metadata without providing metadata");

                    // PERF NOTE: Regex matching is expensive, so if the string doesn't contain any item metadata references, just bail
                    // out -- pre-scanning the string is actually cheaper than running the Regex, even when there are no matches!
                    if (s_invariantCompareInfo.IndexOf(expression, "%(", CompareOptions.Ordinal) == -1)
                    {
                        return expression;
                    }

                    string result = null;

                    if (s_invariantCompareInfo.IndexOf(expression, "@(", CompareOptions.Ordinal) == -1)
                    {
                        // if there are no item vectors in the string
                        // run a simpler Regex to find item metadata references
                        MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);

                        using SpanBasedStringBuilder finalResultBuilder = Strings.GetSpanBasedStringBuilder();
                        RegularExpressions.ReplaceAndAppend(expression, MetadataMatchEvaluator.ExpandSingleMetadata, matchEvaluator, finalResultBuilder, RegularExpressions.ItemMetadataRegex);

                        // Don't create more strings
                        if (finalResultBuilder.Equals(expression.AsSpan()))
                        {
                            // If the final result is the same as the original expression, then just return the original expression
                            result = expression;
                        }
                        else
                        {
                            // Otherwise, convert the final result to a string
                            // and return that.
                            result = finalResultBuilder.ToString();
                        }
                    }
                    else
                    {
                        ExpressionShredder.ReferencedItemExpressionsEnumerator itemVectorExpressionsEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

                        // otherwise, run the more complex Regex to find item metadata references not contained in transforms
                        using SpanBasedStringBuilder finalResultBuilder = Strings.GetSpanBasedStringBuilder();

                        int start = 0;

                        if (itemVectorExpressionsEnumerator.MoveNext())
                        {
                            MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);
                            ExpressionShredder.ItemExpressionCapture firstItemExpressionCapture = itemVectorExpressionsEnumerator.Current;

                            if (itemVectorExpressionsEnumerator.MoveNext())
                            {
                                // we're in the uncommon case with a partially enumerated enumerator. We need to process the first two items we enumerated and the remaining ones.
                                // Move over the expression, skipping those that have been recognized as an item vector expression
                                // Anything other than an item vector expression we want to expand bare metadata in.
                                start = ProcessItemExpressionCapture(expression, finalResultBuilder, matchEvaluator, start, firstItemExpressionCapture);
                                start = ProcessItemExpressionCapture(expression, finalResultBuilder, matchEvaluator, start, itemVectorExpressionsEnumerator.Current);

                                while (itemVectorExpressionsEnumerator.MoveNext())
                                {
                                    start = ProcessItemExpressionCapture(expression, finalResultBuilder, matchEvaluator, start, itemVectorExpressionsEnumerator.Current);
                                }
                            }
                            else
                            {
                                // There is only one item. Check to see if we're in the common case.
                                if (firstItemExpressionCapture.Value == expression && firstItemExpressionCapture.Separator == null)
                                {
                                    // The most common case is where the transform is the whole expression
                                    // Also if there were no valid item vector expressions found, then go ahead and do the replacement on
                                    // the whole expression (which is what Orcas did).
                                    return expression;
                                }
                                else
                                {
                                    start = ProcessItemExpressionCapture(expression, finalResultBuilder, matchEvaluator, start, firstItemExpressionCapture);
                                }
                            }
                        }

                        // If there's anything left after the last item vector expression
                        // then we need to metadata replace and then append that
                        if (start < expression.Length)
                        {
                            MetadataMatchEvaluator matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);
                            string subExpressionToReplaceIn = expression.Substring(start);

                            RegularExpressions.ReplaceAndAppend(subExpressionToReplaceIn, MetadataMatchEvaluator.ExpandSingleMetadata, matchEvaluator, finalResultBuilder, RegularExpressions.NonTransformItemMetadataRegex);
                        }

                        if (finalResultBuilder.Equals(expression.AsSpan()))
                        {
                            // If the final result is the same as the original expression, then just return the original expression
                            result = expression;
                        }
                        else
                        {
                            // Otherwise, convert the final result to a string
                            // and return that.
                            result = finalResultBuilder.ToString();
                        }
                    }

                    return result;
                }
                catch (InvalidOperationException ex)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotExpandItemMetadata", expression, ex.Message);
                }

                return null;

                static int ProcessItemExpressionCapture(string expression, SpanBasedStringBuilder finalResultBuilder, MetadataMatchEvaluator matchEvaluator, int start, ExpressionShredder.ItemExpressionCapture itemExpressionCapture)
                {
                    // Extract the part of the expression that appears before the item vector expression
                    // e.g. the ABC in ABC@(foo->'%(FullPath)')
                    string subExpressionToReplaceIn = expression.Substring(start, itemExpressionCapture.Index - start);

                    RegularExpressions.ReplaceAndAppend(subExpressionToReplaceIn, MetadataMatchEvaluator.ExpandSingleMetadata, matchEvaluator, finalResultBuilder, RegularExpressions.NonTransformItemMetadataRegex);

                    // Expand any metadata that appears in the item vector expression's separator
                    if (itemExpressionCapture.Separator != null)
                    {
                        RegularExpressions.ReplaceAndAppend(itemExpressionCapture.Value, MetadataMatchEvaluator.ExpandSingleMetadata, matchEvaluator, -1, itemExpressionCapture.SeparatorStart, finalResultBuilder, RegularExpressions.NonTransformItemMetadataRegex);
                    }
                    else
                    {
                        // Append the item vector expression as is
                        // e.g. the @(foo->'%(FullPath)') in ABC@(foo->'%(FullPath)')
                        finalResultBuilder.Append(itemExpressionCapture.Value);
                    }

                    // Move onto the next part of the expression that isn't an item vector expression
                    start = (itemExpressionCapture.Index + itemExpressionCapture.Length);
                    return start;
                }
            }
        }

        /// <summary>
        /// A functor that returns the value of the metadata in the match
        /// that is contained in the metadata dictionary it was created with.
        /// </summary>
        private struct MetadataMatchEvaluator
        {
            /// <summary>
            /// Source of the metadata.
            /// </summary>
            private IMetadataTable _metadata;

            /// <summary>
            /// Whether to expand built-in metadata, custom metadata, or both kinds.
            /// </summary>
            private ExpanderOptions _options;

            private IElementLocation _elementLocation;

            private LoggingContext _loggingContext;

            /// <summary>
            /// Constructor taking a source of metadata.
            /// </summary>
            internal MetadataMatchEvaluator(
                IMetadataTable metadata,
                ExpanderOptions options,
                IElementLocation elementLocation,
                LoggingContext loggingContext)
            {
                _metadata = metadata;
                _options = options & (ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate | ExpanderOptions.LogOnItemMetadataSelfReference);
                _elementLocation = elementLocation;
                _loggingContext = loggingContext;

                ErrorUtilities.VerifyThrow(options != ExpanderOptions.Invalid, "Must be expanding metadata of some kind");
            }

            /// <summary>
            /// Expands a single item metadata, which may be qualified with an item type.
            /// </summary>
            internal static string ExpandSingleMetadata(Match itemMetadataMatch, MetadataMatchEvaluator evaluator)
            {
                ErrorUtilities.VerifyThrow(itemMetadataMatch.Success, "Need a valid item metadata.");

                string metadataName = itemMetadataMatch.Groups[RegularExpressions.NameGroup].Value;

                string metadataValue = null;

                bool isBuiltInMetadata = FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName);

                if (
                    (isBuiltInMetadata && ((evaluator._options & ExpanderOptions.ExpandBuiltInMetadata) != 0)) ||
                   (!isBuiltInMetadata && ((evaluator._options & ExpanderOptions.ExpandCustomMetadata) != 0)))
                {
                    string itemType = null;

                    // check if the metadata is qualified with the item type
                    if (itemMetadataMatch.Groups[RegularExpressions.ItemSpecificationGroup].Length > 0)
                    {
                        itemType = itemMetadataMatch.Groups[RegularExpressions.ItemTypeGroup].Value;
                    }

                    metadataValue = evaluator._metadata.GetEscapedValue(itemType, metadataName);

                    if ((evaluator._options & ExpanderOptions.LogOnItemMetadataSelfReference) != 0 &&
                        evaluator._loggingContext != null &&
                        !string.IsNullOrEmpty(metadataName) &&
                        evaluator._metadata is IItemTypeDefinition itemMetadata &&
                        (string.IsNullOrEmpty(itemType) || string.Equals(itemType, itemMetadata.ItemType, StringComparison.Ordinal)))
                    {
                        evaluator._loggingContext.LogComment(MessageImportance.Low, new BuildEventFileInfo(evaluator._elementLocation),
                            "ItemReferencingSelfInTarget", itemMetadata.ItemType, metadataName);
                    }

                    if (IsTruncationEnabled(evaluator._options) && metadataValue.Length > CharacterLimitPerExpansion)
                    {
                        metadataValue = TruncateString(metadataValue);
                    }
                }
                else
                {
                    // look up the metadata - we may not have a value for it
                    metadataValue = itemMetadataMatch.Value;
                }

                return metadataValue;
            }
        }

        /// <summary>
        /// Regular expressions used by the expander.
        /// The expander currently uses regular expressions rather than a parser to do its work.
        /// </summary>
        private static partial class RegularExpressions
        {
            /**************************************************************************************************************************
            * WARNING: The regular expressions below MUST be kept in sync with the expressions in the ProjectWriter class -- if the
            * description of an item vector changes, the expressions must be updated in both places.
            *************************************************************************************************************************/

#if NET
            [GeneratedRegex(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture)]
            internal static partial Regex ItemMetadataRegex { get; }
#else
            /// <summary>
            /// Regular expression used to match item metadata references embedded in strings.
            /// For example, %(Compile.DependsOn) or %(DependsOn).
            /// </summary>
            internal static Regex ItemMetadataRegex => s_itemMetadataRegex ??=
                new Regex(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

            internal static Regex s_itemMetadataRegex;
#endif

            /// <summary>
            /// Name of the group matching the "name" of a metadatum.
            /// </summary>
            internal const string NameGroup = "NAME";

            /// <summary>
            /// Name of the group matching the prefix on a metadata expression, for example "Compile." in "%(Compile.Object)".
            /// </summary>
            internal const string ItemSpecificationGroup = "ITEM_SPECIFICATION";

            /// <summary>
            /// Name of the group matching the item type in an item expression or metadata expression.
            /// </summary>
            internal const string ItemTypeGroup = "ITEM_TYPE";

            internal const string NonTransformItemMetadataSpecification = @"((?<=" + ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?!" +
                                                                ItemVectorWithTransformRHS + @")) | ((?<!" + ItemVectorWithTransformLHS + @")" +
                                                                ItemMetadataSpecification + @"(?=" + ItemVectorWithTransformRHS + @")) | ((?<!" +
                                                                ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?!" +
                                                                ItemVectorWithTransformRHS + @"))";

#if NET
            [GeneratedRegex(NonTransformItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture)]
            internal static partial Regex NonTransformItemMetadataRegex { get; }
#else
            /// <summary>
            /// regular expression used to match item metadata references outside of item vector transforms.
            /// </summary>
            /// <remarks>PERF WARNING: this Regex is complex and tends to run slowly.</remarks>
            private static Regex s_nonTransformItemMetadataPattern;

            internal static Regex NonTransformItemMetadataRegex => s_nonTransformItemMetadataPattern ??=
                new Regex(NonTransformItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
#endif

            /// <summary>
            /// Complete description of an item metadata reference, including the optional qualifying item type.
            /// For example, %(Compile.DependsOn) or %(DependsOn).
            /// </summary>
            private const string ItemMetadataSpecification = @"%\(\s* (?<ITEM_SPECIFICATION>(?<ITEM_TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")\s*\.\s*)? (?<NAME>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @") \s*\)";

            /// <summary>
            /// description of an item vector with a transform, left hand side.
            /// </summary>
            private const string ItemVectorWithTransformLHS = @"@\(\s*" + ProjectWriter.itemTypeOrMetadataNameSpecification + @"\s*->\s*'[^']*";

            /// <summary>
            /// description of an item vector with a transform, right hand side.
            /// </summary>
            private const string ItemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

            /**************************************************************************************************************************
             * WARNING: The regular expressions above MUST be kept in sync with the expressions in the ProjectWriter class.
             *************************************************************************************************************************/

            /// <summary>
            /// Copied from <see cref="Regex.Replace(string, MatchEvaluator, int, int)"/> and modified to use a <see cref="SpanBasedStringBuilder"/> rather than repeatedly allocating a <see cref="System.Text.StringBuilder"/>. This
            /// allows us to avoid intermediate string allocations when repeatedly doing replacements. 
            /// </summary>
            /// <param name="input">The string to operate on.</param>
            /// <param name="evaluator">A function to transform any matches found.</param>
            /// <param name="metadataMatchEvaluator">State used in the transform function.</param>
            /// <param name="stringBuilder">The <see cref="SpanBasedStringBuilder"/> that will accumulate the results.</param>
            /// <param name="regex">The <see cref="Regex"/> that will perform the matching.</param>
            public static void ReplaceAndAppend(string input, Func<Match, MetadataMatchEvaluator, string> evaluator, MetadataMatchEvaluator metadataMatchEvaluator, SpanBasedStringBuilder stringBuilder, Regex regex)
            {
                ReplaceAndAppend(input, evaluator, metadataMatchEvaluator, -1, regex.RightToLeft ? input.Length : 0, stringBuilder, regex);
            }

            /// <summary>
            /// Copied from <see cref="Regex.Replace(string, MatchEvaluator, int, int)"/> and modified to use a <see cref="SpanBasedStringBuilder"/> rather than repeatedly allocating a <see cref="System.Text.StringBuilder"/>. This
            /// allows us to avoid intermediate string allocations when repeatedly doing replacements.
            /// </summary>
            /// <param name="input">The string to operate on.</param>
            /// <param name="evaluator">A function to transform any matches found.</param>
            /// <param name="matchEvaluatorState">State used in the transform function.</param>
            /// <param name="count">The number of replacements.</param>
            /// <param name="startat">Index to start when doing replacements.</param>
            /// <param name="stringBuilder">The <see cref="SpanBasedStringBuilder"/> that will accumulate the results.</param>
            /// <param name="regex">The <see cref="Regex"/> that will perform the matching.</param>
            public static void ReplaceAndAppend(string input, Func<Match, MetadataMatchEvaluator, string> evaluator, MetadataMatchEvaluator matchEvaluatorState, int count, int startat, SpanBasedStringBuilder stringBuilder, Regex regex)
            {
                if (evaluator is null)
                {
                    throw new ArgumentNullException(nameof(evaluator));
                }

                if (stringBuilder is null)
                {
                    throw new ArgumentNullException(nameof(stringBuilder));
                }

                if (count < -1)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                if (startat < 0 || startat > input.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(startat));
                }

                if (regex is null)
                {
                    throw new ArgumentNullException(nameof(regex));
                }

                if (count == 0)
                {
                    stringBuilder.Append(input);

                    return;
                }

                Match match = regex.Match(input, startat);
                if (!match.Success)
                {
                    stringBuilder.Append(input);

                    return;
                }

                if (!regex.RightToLeft)
                {
                    int prevat = 0;
                    do
                    {
                        if (match.Index != prevat)
                        {
                            stringBuilder.Append(input, prevat, match.Index - prevat);
                        }

                        prevat = match.Index + match.Length;
                        stringBuilder.Append(evaluator(match, matchEvaluatorState));
                        if (--count == 0)
                        {
                            break;
                        }

                        match = match.NextMatch();
                    }
                    while (match.Success);
                    if (prevat < input.Length)
                    {
                        stringBuilder.Append(input, prevat, input.Length - prevat);
                    }
                }
                else
                {
                    List<ReadOnlyMemory<char>> list = new List<ReadOnlyMemory<char>>();
                    int prevat = input.Length;
                    do
                    {
                        if (match.Index + match.Length != prevat)
                        {
                            list.Add(input.AsMemory().Slice(match.Index + match.Length, prevat - match.Index - match.Length));
                        }

                        prevat = match.Index;
                        list.Add(evaluator(match, matchEvaluatorState).AsMemory());
                        if (--count == 0)
                        {
                            break;
                        }

                        match = match.NextMatch();
                    }
                    while (match.Success);

                    if (prevat > 0)
                    {
                        stringBuilder.Append(input, 0, prevat);
                    }

                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        stringBuilder.Append(list[i]);
                    }
                }
            }
        }

        private struct FunctionBuilder<T>
            where T : class, IProperty
        {
            /// <summary>
            /// The type of this function's receiver.
            /// </summary>
            public Type ReceiverType { get; set; }

            /// <summary>
            /// The name of the function.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The arguments for the function.
            /// </summary>
            public string[] Arguments { get; set; }

            /// <summary>
            /// The expression that this function is part of.
            /// </summary>
            public string Expression { get; set; }

            /// <summary>
            /// The property name that this function is applied on.
            /// </summary>
            public string Receiver { get; set; }

            /// <summary>
            /// The binding flags that will be used during invocation of this function.
            /// </summary>
            public BindingFlags BindingFlags { get; set; }

            /// <summary>
            /// The remainder of the body once the function and arguments have been extracted.
            /// </summary>
            public string Remainder { get; set; }

            public IFileSystem FileSystem { get; set; }

            public LoggingContext LoggingContext { get; set; }

            /// <summary>
            /// List of properties which have been used but have not been initialized yet.
            /// </summary>
            public PropertiesUseTracker PropertiesUseTracker { get; set; }

            internal readonly Function<T> Build()
            {
                return new Function<T>(
                    ReceiverType,
                    Expression,
                    Receiver,
                    Name,
                    Arguments,
                    BindingFlags,
                    Remainder,
                    PropertiesUseTracker,
                    FileSystem,
                    LoggingContext);
            }
        }

        /// <summary>
        /// This class represents the function as extracted from an expression
        /// It is also responsible for executing the function.
        /// </summary>
        /// <typeparam name="T">Type of the properties used to expand the expression.</typeparam>
        internal class Function<T>
            where T : class, IProperty
        {
            /// <summary>
            /// The type of this function's receiver.
            /// </summary>
            private Type _receiverType;

            /// <summary>
            /// The name of the function.
            /// </summary>
            private readonly string _methodMethodName;

            /// <summary>
            /// The arguments for the function.
            /// </summary>
            private readonly string[] _arguments;

            /// <summary>
            /// The expression that this function is part of.
            /// </summary>
            private readonly string _expression;

            /// <summary>
            /// The property name that this function is applied on.
            /// </summary>
            private readonly string _receiver;

            /// <summary>
            /// The binding flags that will be used during invocation of this function.
            /// </summary>
            private BindingFlags _bindingFlags;

            /// <summary>
            /// The remainder of the body once the function and arguments have been extracted.
            /// </summary>
            private readonly string _remainder;

            /// <summary>
            /// List of properties which have been used but have not been initialized yet.
            /// </summary>
            private PropertiesUseTracker _propertiesUseTracker;

            private readonly IFileSystem _fileSystem;

            private readonly LoggingContext _loggingContext;

            /// <summary>
            /// Construct a function that will be executed during property evaluation.
            /// </summary>
            internal Function(
                Type receiverType,
                string expression,
                string receiver,
                string methodName,
                string[] arguments,
                BindingFlags bindingFlags,
                string remainder,
                PropertiesUseTracker propertiesUseTracker,
                IFileSystem fileSystem,
                LoggingContext loggingContext)
            {
                _methodMethodName = methodName;
                if (arguments == null)
                {
                    _arguments = [];
                }
                else
                {
                    _arguments = arguments;
                }

                _receiver = receiver;
                _expression = expression;
                _receiverType = receiverType;
                _bindingFlags = bindingFlags;
                _remainder = remainder;
                _propertiesUseTracker = propertiesUseTracker;
                _fileSystem = fileSystem;
                _loggingContext = loggingContext;
            }

            /// <summary>
            /// Part of the extraction may result in the name of the property
            /// This accessor is used by the Expander
            /// Examples of expression root:
            ///     [System.Diagnostics.Process]::Start
            ///     SomeMSBuildProperty.
            /// </summary>
            internal string Receiver
            {
                get { return _receiver; }
            }

            /// <summary>
            /// Extract the function details from the given property function expression.
            /// </summary>
            internal static Function<T> ExtractPropertyFunction(
                string expressionFunction,
                IElementLocation elementLocation,
                object propertyValue,
                PropertiesUseTracker propertiesUseTracker,
                IFileSystem fileSystem,
                LoggingContext loggingContext)
            {
                // Used to aggregate all the components needed for a Function
                FunctionBuilder<T> functionBuilder = new FunctionBuilder<T> { FileSystem = fileSystem, LoggingContext = loggingContext };

                // By default the expression root is the whole function expression
                ReadOnlySpan<char> expressionRoot = expressionFunction == null ? ReadOnlySpan<char>.Empty : expressionFunction.AsSpan();

                // The arguments for this function start at the first '('
                // If there are no arguments, then we're a property getter
                var argumentStartIndex = expressionFunction.IndexOf('(');

                // If we have arguments, then we only want the content up to but not including the '('
                if (argumentStartIndex > -1)
                {
                    expressionRoot = expressionRoot.Slice(0, argumentStartIndex);
                }

                // In case we ended up with something we don't understand
                ProjectErrorUtilities.VerifyThrowInvalidProject(!expressionRoot.IsEmpty, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                functionBuilder.Expression = expressionFunction;
                functionBuilder.PropertiesUseTracker = propertiesUseTracker;

                // This is a static method call
                // A static method is the content that follows the last "::", the rest being the type
                if (propertyValue == null && expressionRoot[0] == '[')
                {
                    var typeEndIndex = expressionRoot.IndexOf(']');

                    if (typeEndIndex < 1)
                    {
                        // We ended up with something other than a function expression
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", expressionFunction, String.Empty);
                    }

                    var typeName = Strings.WeakIntern(expressionRoot.Slice(1, typeEndIndex - 1));
                    var methodStartIndex = typeEndIndex + 1;

                    if (expressionRoot.Length > methodStartIndex + 2 && expressionRoot[methodStartIndex] == ':' && expressionRoot[methodStartIndex + 1] == ':')
                    {
                        // skip over the "::"
                        methodStartIndex += 2;
                    }
                    else
                    {
                        // We ended up with something other than a static function expression
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", expressionFunction, String.Empty);
                    }

                    ConstructFunction(elementLocation, expressionFunction, argumentStartIndex, methodStartIndex, ref functionBuilder);

                    // Locate a type that matches the body of the expression.
                    var receiverType = GetTypeForStaticMethod(typeName, functionBuilder.Name);

                    if (receiverType == null)
                    {
                        // We ended up with something other than a type
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionTypeUnavailable", expressionFunction, typeName);
                    }

                    functionBuilder.ReceiverType = receiverType;
                }
                else if (expressionFunction[0] == '[') // We have an indexer
                {
                    var indexerEndIndex = expressionFunction.IndexOf(']', 1);
                    if (indexerEndIndex < 1)
                    {
                        // We ended up with something other than a function expression
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                    }

                    var methodStartIndex = indexerEndIndex + 1;

                    functionBuilder.ReceiverType = propertyValue.GetType();

                    ConstructIndexerFunction(expressionFunction, elementLocation, propertyValue, methodStartIndex, indexerEndIndex, ref functionBuilder);
                }
                else // This could be a property reference, or a chain of function calls
                {
                    // Look for an instance function call next, such as in SomeStuff.ToLower()
                    var methodStartIndex = expressionRoot.IndexOf('.');
                    if (methodStartIndex == -1)
                    {
                        // We don't have a function invocation in the expression root, return null
                        return null;
                    }

                    // skip over the '.';
                    methodStartIndex++;

                    var rootEndIndex = expressionRoot.IndexOf('.');

                    // If this is an instance function rather than a static, then we'll capture the name of the property referenced
                    var functionReceiver = Strings.WeakIntern(expressionRoot.Slice(0, rootEndIndex).Trim());

                    // If propertyValue is null (we're not recursing), then we're expecting a valid property name
                    if (propertyValue == null && !IsValidPropertyName(functionReceiver))
                    {
                        // We extracted something that wasn't a valid property name, fail.
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                    }

                    // If we are recursively acting on a type that has been already produced then pass that type inwards (e.g. we are interpreting a function call chain)
                    // Otherwise, the receiver of the function is a string
                    var receiverType = propertyValue?.GetType() ?? typeof(string);

                    functionBuilder.Receiver = functionReceiver;
                    functionBuilder.ReceiverType = receiverType;

                    ConstructFunction(elementLocation, expressionFunction, argumentStartIndex, methodStartIndex, ref functionBuilder);
                }

                return functionBuilder.Build();
            }

            /// <summary>
            /// Execute the function on the given instance.
            /// </summary>
            internal object Execute(object objectInstance, IPropertyProvider<P> properties, ExpanderOptions options, IElementLocation elementLocation)
            {
                object functionResult = String.Empty;
                object[] args = null;

                try
                {
                    // If there is no object instance, then the method invocation will be a static
                    if (objectInstance == null)
                    {
                        // Check that the function that we're going to call is valid to call
                        if (!IsStaticMethodAvailable(_receiverType, _methodMethodName))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _methodMethodName, _receiverType.FullName);
                        }

                        _bindingFlags |= BindingFlags.Static;

                        // For our intrinsic function we need to support calling of internal methods
                        // since we don't want them to be public
                        if (_receiverType == typeof(IntrinsicFunctions))
                        {
                            _bindingFlags |= BindingFlags.NonPublic;
                        }
                    }
                    else
                    {
                        // Check that the function that we're going to call is valid to call
                        if (!IsInstanceMethodAvailable(_methodMethodName))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _methodMethodName, _receiverType.FullName);
                        }

                        _bindingFlags |= BindingFlags.Instance;

                        // The object that we're about to call methods on may have escaped characters
                        // in it, we want to operate on the unescaped string in the function, just as we
                        // want to pass arguments that are unescaped (see below)
                        if (objectInstance is string objectInstanceString)
                        {
                            objectInstance = EscapingUtilities.UnescapeAll(objectInstanceString);
                        }
                    }

                    // We have a methodinfo match, need to plug in the arguments
                    args = new object[_arguments.Length];

                    // Assemble our arguments ready for passing to our method
                    for (int n = 0; n < _arguments.Length; n++)
                    {
                        object argument = PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(
                            _arguments[n],
                            properties,
                            options,
                            elementLocation,
                            _propertiesUseTracker,
                            _fileSystem);

                        if (argument is string argumentValue)
                        {
                            // Unescape the value since we're about to send it out of the engine and into
                            // the function being called. If a file or a directory function, fix the path
                            if (_receiverType == typeof(File) || _receiverType == typeof(Directory)
                                || _receiverType == typeof(Path))
                            {
                                argumentValue = FileUtilities.FixFilePath(argumentValue);
                            }

                            args[n] = EscapingUtilities.UnescapeAll(argumentValue);
                        }
                        else
                        {
                            args[n] = argument;
                        }
                    }

                    // Handle special cases where the object type needs to affect the choice of method
                    // The default binder and method invoke, often chooses the incorrect Equals and CompareTo and
                    // fails the comparison, because what we have on the right is generally a string.
                    // This special casing is to realize that its a comparison that is taking place and handle the
                    // argument type coercion accordingly; effectively pre-preparing the argument type so
                    // that it matches the left hand side ready for the default binder’s method invoke.
                    if (objectInstance != null && args.Length == 1 && (String.Equals("Equals", _methodMethodName, StringComparison.OrdinalIgnoreCase) || String.Equals("CompareTo", _methodMethodName, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Support comparison when the lhs is an integer
                        if (ParseArgs.IsFloatingPointRepresentation(args[0]))
                        {
                            if (double.TryParse(objectInstance.ToString(), NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double result))
                            {
                                objectInstance = result;
                                _receiverType = objectInstance.GetType();
                            }
                        }

                        // change the type of the final unescaped string into the destination
                        args[0] = Convert.ChangeType(args[0], objectInstance.GetType(), CultureInfo.InvariantCulture);
                    }

                    if (_receiverType == typeof(IntrinsicFunctions))
                    {
                        // Special case a few methods that take extra parameters that can't be passed in by the user
                        if (_methodMethodName.Equals("GetPathOfFileAbove") && args.Length == 1)
                        {
                            // Append the IElementLocation as a parameter to GetPathOfFileAbove if the user only
                            // specified the file name.  This is syntactic sugar so they don't have to always
                            // include $(MSBuildThisFileDirectory) as a parameter.
                            string startingDirectory = String.IsNullOrWhiteSpace(elementLocation.File) ? String.Empty : Path.GetDirectoryName(elementLocation.File);

                            args = [args[0], startingDirectory];
                        }
                    }

                    // If we've been asked to construct an instance, then we
                    // need to locate an appropriate constructor and invoke it
                    if (String.Equals("new", _methodMethodName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!WellKnownFunctions.TryExecuteWellKnownConstructorNoThrow(_receiverType, out functionResult, args))
                        {
                            functionResult = LateBindExecute(null /* no previous exception */, BindingFlags.Public | BindingFlags.Instance, null /* no instance for a constructor */, args, true /* is constructor */);
                        }
                    }
                    else
                    {
                        bool wellKnownFunctionSuccess = false;

                        try
                        {
                            // First attempt to recognize some well-known functions to avoid binding
                            // and potential first-chance MissingMethodExceptions.
                            wellKnownFunctionSuccess = WellKnownFunctions.TryExecuteWellKnownFunction(_methodMethodName, _receiverType, _fileSystem, out functionResult, objectInstance, args);

                            if (!wellKnownFunctionSuccess)
                            {
                                // Some well-known functions need evaluated value from properties.
                                wellKnownFunctionSuccess = WellKnownFunctions.TryExecuteWellKnownFunctionWithPropertiesParam(_methodMethodName, _receiverType, _loggingContext, properties, out functionResult, objectInstance, args);
                            }
                        }
                        // we need to preserve the same behavior on exceptions as the actual binder
                        catch (Exception ex)
                        {
                            string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                            if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                            {
                                return partiallyEvaluated;
                            }

                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message.Replace("\r\n", " "));
                        }

                        if (!wellKnownFunctionSuccess)
                        {
                            // Execute the function given converted arguments
                            // The only exception that we should catch to try a late bind here is missing method
                            // otherwise there is the potential of running a function twice!
                            try
                            {
                                // If there are any out parameters, try to figure out their type and create defaults for them as appropriate before calling the method.
                                if (args.Any(a => "out _".Equals(a)))
                                {
                                    IEnumerable<MethodInfo> methods = _receiverType.GetMethods(_bindingFlags).Where(m => m.Name.Equals(_methodMethodName) && m.GetParameters().Length == args.Length);
                                    functionResult = GetMethodResult(objectInstance, methods, args, 0);
                                }
                                else
                                {
                                    // If there are no out parameters, use InvokeMember using the standard binder - this will match and coerce as needed
                                    functionResult = _receiverType.InvokeMember(_methodMethodName, _bindingFlags, Type.DefaultBinder, objectInstance, args, CultureInfo.InvariantCulture);
                                }
                            }
                            // If we're invoking a method, then there are deeper attempts that can be made to invoke the method.
                            // If not, we were asked to get a property or field but found that we cannot locate it. No further argument coercion is possible, so throw.
                            catch (MissingMethodException ex) when ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                            {
                                // The standard binder failed, so do our best to coerce types into the arguments for the function
                                // This may happen if the types need coercion, but it may also happen if the object represents a type that contains open type parameters, that is, ContainsGenericParameters returns true.
                                functionResult = LateBindExecute(ex, _bindingFlags, objectInstance, args, false /* is not constructor */);
                            }
                        }
                    }

                    // If the result of the function call is a string, then we need to escape the result
                    // so that we maintain the "engine contains escaped data" state.
                    // The exception is that the user is explicitly calling MSBuild::Unescape, MSBuild::Escape, or ConvertFromBase64
                    if (functionResult is string functionResultString &&
                        !String.Equals("Unescape", _methodMethodName, StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals("Escape", _methodMethodName, StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals("ConvertFromBase64", _methodMethodName, StringComparison.OrdinalIgnoreCase))
                    {
                        functionResult = EscapingUtilities.Escape(functionResultString);
                    }

                    // We have nothing left to parse, so we'll return what we have
                    if (String.IsNullOrEmpty(_remainder))
                    {
                        return functionResult;
                    }

                    // Recursively expand the remaining property body after execution
                    return PropertyExpander.ExpandPropertyBody(
                        _remainder,
                        functionResult,
                        properties,
                        options,
                        elementLocation,
                        _propertiesUseTracker,
                        _fileSystem);
                }

                // Exceptions coming from the actual function called are wrapped in a TargetInvocationException
                catch (TargetInvocationException ex)
                {
                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                    if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                    {
                        // If the caller wants to ignore errors (in a log statement for example), just return the partially evaluated value
                        return partiallyEvaluated;
                    }
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.InnerException.Message.Replace("\r\n", " "));
                    return null;
                }

                // Any other exception was thrown by trying to call it
                catch (Exception ex) when (!ExceptionHandling.NotExpectedFunctionException(ex))
                {
                    // If there's a :: in the expression, they were probably trying for a static function
                    // invocation. Give them some more relevant info in that case
                    if (s_invariantCompareInfo.IndexOf(_expression, "::", CompareOptions.OrdinalIgnoreCase) > -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", _expression, ex.Message.Replace("Microsoft.Build.Evaluation.IntrinsicFunctions.", "[MSBuild]::"));
                    }
                    else
                    {
                        // We ended up with something other than a function expression
                        string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message);
                    }

                    return null;
                }
            }

            private object GetMethodResult(object objectInstance, IEnumerable<MethodInfo> methods, object[] args, int index)
            {
                for (int i = index; i < args.Length; i++)
                {
                    if (args[i].Equals("out _"))
                    {
                        object toReturn = null;
                        foreach (MethodInfo method in methods)
                        {
                            Type t = method.GetParameters()[i].ParameterType;
                            args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
                            object currentReturnValue = GetMethodResult(objectInstance, methods, args, i + 1);
                            if (currentReturnValue is not null)
                            {
                                if (toReturn is null)
                                {
                                    toReturn = currentReturnValue;
                                }
                                else if (!toReturn.Equals(currentReturnValue))
                                {
                                    // There were multiple methods that seemed viable and gave different results. We can't differentiate between them so throw.
                                    ErrorUtilities.ThrowArgument("CouldNotDifferentiateBetweenCompatibleMethods", _methodMethodName, args.Length);
                                    return null;
                                }
                            }
                        }

                        return toReturn;
                    }
                }

                try
                {
                    return _receiverType.InvokeMember(_methodMethodName, _bindingFlags, Type.DefaultBinder, objectInstance, args, CultureInfo.InvariantCulture) ?? "null";
                }
                catch (Exception)
                {
                    // This isn't a viable option, but perhaps another set of parameters will work.
                    return null;
                }
            }

            /// <summary>
            /// Given a type name and method name, try to resolve the type.
            /// </summary>
            /// <param name="typeName">May be full name or assembly qualified name.</param>
            /// <param name="simpleMethodName">simple name of the method.</param>
            /// <returns></returns>
            private static Type GetTypeForStaticMethod(string typeName, string simpleMethodName)
            {
                Type receiverType;
                Tuple<string, Type> cachedTypeInformation;

                // If we don't have a type name, we already know that we won't be able to find a type.
                // Go ahead and return here -- otherwise the Type.GetType() calls below will throw.
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return null;
                }

                // Check if the type is in the allowlist cache. If it is, use it or load it.
                cachedTypeInformation = AvailableStaticMethods.GetTypeInformationFromTypeCache(typeName, simpleMethodName);
                if (cachedTypeInformation != null)
                {
                    // We need at least one of these set
                    ErrorUtilities.VerifyThrow(cachedTypeInformation.Item1 != null || cachedTypeInformation.Item2 != null, "Function type information needs either string or type represented.");

                    // If we have the type information in Type form, then just return that
                    if (cachedTypeInformation.Item2 != null)
                    {
                        return cachedTypeInformation.Item2;
                    }
                    else if (cachedTypeInformation.Item1 != null)
                    {
                        // This is a case where the Type is not available at compile time, so
                        // we are forced to bind by name instead
                        var assemblyQualifiedTypeName = cachedTypeInformation.Item1;

                        // Get the type from the assembly qualified type name from AvailableStaticMethods
                        receiverType = Type.GetType(assemblyQualifiedTypeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                        // If the type information from the cache is not loadable, it means the cache information got corrupted somehow
                        // Throw here to prevent adding null types in the cache
                        ErrorUtilities.VerifyThrowInternalNull(receiverType, $"Type information for {typeName} was present in the allowlist cache as {assemblyQualifiedTypeName} but the type could not be loaded.");

                        // If we've used it once, chances are that we'll be using it again
                        // We can record the type here since we know it's available for calling from the fact that is was in the AvailableStaticMethods table
                        AvailableStaticMethods.TryAdd(typeName, simpleMethodName, new Tuple<string, Type>(assemblyQualifiedTypeName, receiverType));

                        return receiverType;
                    }
                }

                // Get the type from mscorlib (or the currently running assembly)
                receiverType = Type.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                if (receiverType != null)
                {
                    // DO NOT CACHE THE TYPE HERE!
                    // We don't add the resolved type here in the AvailableStaticMethods table. This is because that table is used
                    // during function parse, but only later during execution do we check for the ability to call specific methods on specific types.
                    // Caching it here would load any type into the allow list.
                    return receiverType;
                }

                // Note the following code path is only entered when MSBUILDENABLEALLPROPERTYFUNCTIONS == 1.
                // This environment variable must not be cached - it should be dynamically settable while the application is executing.
                if (Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1")
                {
                    // We didn't find the type, so go probing. First in System
                    receiverType = GetTypeFromAssembly(typeName, "System");

                    // Next in System.Core
                    if (receiverType == null)
                    {
                        receiverType = GetTypeFromAssembly(typeName, "System.Core");
                    }

                    // We didn't find the type, so try to find it using the namespace
                    if (receiverType == null)
                    {
                        receiverType = GetTypeFromAssemblyUsingNamespace(typeName);
                    }

                    if (receiverType != null)
                    {
                        // If we've used it once, chances are that we'll be using it again
                        // We can cache the type here, since all functions are enabled
                        AvailableStaticMethods.TryAdd(typeName, new Tuple<string, Type>(typeName, receiverType));
                    }
                }

                return receiverType;
            }

            /// <summary>
            /// Gets the specified type using the namespace to guess the assembly that its in.
            /// </summary>
            private static Type GetTypeFromAssemblyUsingNamespace(string typeName)
            {
                string baseName = typeName;
                int assemblyNameEnd = baseName.Length;

                // If the string has no dot, or is nothing but a dot, we have no
                // namespace to look for, so we can't help.
                if (assemblyNameEnd <= 0)
                {
                    return null;
                }

                // We will work our way up the namespace looking for an assembly that matches
                while (assemblyNameEnd > 0)
                {
                    string candidateAssemblyName = baseName.Substring(0, assemblyNameEnd);

                    // Try to load the assembly with the computed name
                    Type foundType = GetTypeFromAssembly(typeName, candidateAssemblyName);

                    if (foundType != null)
                    {
                        // We have a match, so get the type from that assembly
                        return foundType;
                    }
                    else
                    {
                        // Keep looking as we haven't found a match yet
                        baseName = candidateAssemblyName;
                        assemblyNameEnd = baseName.LastIndexOf('.');
                    }
                }

                // We didn't find it, so we need to give up
                return null;
            }

            /// <summary>
            /// Get the specified type from the assembly partial name supplied.
            /// </summary>
            [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName", Justification = "Necessary since we don't have the full assembly name. ")]
            private static Type GetTypeFromAssembly(string typeName, string candidateAssemblyName)
            {
                Type objectType = null;

                // Try to load the assembly with the computed name
#if FEATURE_GAC
#pragma warning disable 618, 612
                // Unfortunately Assembly.Load is not an alternative to LoadWithPartialName, since
                // Assembly.Load requires the full assembly name to be passed to it.
                // Therefore we must ignore the deprecated warning.
                Assembly candidateAssembly = Assembly.LoadWithPartialName(candidateAssemblyName);
#pragma warning restore 618, 612
#else
                Assembly candidateAssembly = null;
                try
                {
                    candidateAssembly = Assembly.Load(new AssemblyName(candidateAssemblyName));
                }
                catch (FileNotFoundException)
                {
                    // Swallow the error; LoadWithPartialName returned null when the partial name
                    // was not found but Load throws.  Either way we'll provide a nice "couldn't
                    // resolve this" error later.
                }
#endif

                if (candidateAssembly != null)
                {
                    objectType = candidateAssembly.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);
                }

                return objectType;
            }

            /// <summary>
            /// Extracts the name, arguments, binding flags, and invocation type for an indexer
            /// Also extracts the remainder of the expression that is not part of this indexer.
            /// </summary>
            private static void ConstructIndexerFunction(string expressionFunction, IElementLocation elementLocation, object propertyValue, int methodStartIndex, int indexerEndIndex, ref FunctionBuilder<T> functionBuilder)
            {
                ReadOnlyMemory<char> argumentsContent = expressionFunction.AsMemory().Slice(1, indexerEndIndex - 1);
                string[] functionArguments;

                // If there are no arguments, then just create an empty array
                if (argumentsContent.IsEmpty)
                {
                    functionArguments = [];
                }
                else
                {
                    // We will keep empty entries so that we can treat them as null
                    functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                }

                // choose the name of the function based on the type of the object that we
                // are using.
                string functionName;
                if (propertyValue is Array)
                {
                    functionName = "GetValue";
                }
                else if (propertyValue is string)
                {
                    functionName = "get_Chars";
                }
                else // a regular indexer
                {
                    functionName = "get_Item";
                }

                functionBuilder.Name = functionName;
                functionBuilder.Arguments = functionArguments;
                functionBuilder.BindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod;
                functionBuilder.Remainder = expressionFunction.Substring(methodStartIndex);
            }

            /// <summary>
            /// Extracts the name, arguments, binding flags, and invocation type for a static or instance function.
            /// Also extracts the remainder of the expression that is not part of this function.
            /// </summary>
            private static void ConstructFunction(IElementLocation elementLocation, string expressionFunction, int argumentStartIndex, int methodStartIndex, ref FunctionBuilder<T> functionBuilder)
            {
                // The unevaluated and unexpanded arguments for this function
                string[] functionArguments;

                // The name of the function that will be invoked
                ReadOnlySpan<char> functionName;

                // What's left of the expression once the function has been constructed
                ReadOnlySpan<char> remainder = ReadOnlySpan<char>.Empty;

                // The binding flags that we will use for this function's execution
                BindingFlags defaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

                ReadOnlySpan<char> expressionFunctionAsSpan = expressionFunction.AsSpan();

                ReadOnlySpan<char> expressionSubstringAsSpan = argumentStartIndex > -1 ? expressionFunctionAsSpan.Slice(methodStartIndex, argumentStartIndex - methodStartIndex) : ReadOnlySpan<char>.Empty;

                // There are arguments that need to be passed to the function
                if (argumentStartIndex > -1 && !expressionSubstringAsSpan.Contains(".".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    // separate the function and the arguments
                    functionName = expressionSubstringAsSpan.Trim();

                    // Skip the '('
                    argumentStartIndex++;

                    // Scan for the matching closing bracket, skipping any nested ones
                    int argumentsEndIndex = ScanForClosingParenthesis(expressionFunctionAsSpan, argumentStartIndex, out _, out _);

                    if (argumentsEndIndex == -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                    }

                    // We have been asked for a method invocation
                    defaultBindingFlags |= BindingFlags.InvokeMethod;

                    // It may be that there are '()' but no actual arguments content
                    if (argumentStartIndex == expressionFunction.Length - 1)
                    {
                        functionArguments = [];
                    }
                    else
                    {
                        // we have content within the '()' so let's extract and deal with it
                        ReadOnlyMemory<char> argumentsContent = expressionFunction.AsMemory().Slice(argumentStartIndex, argumentsEndIndex - argumentStartIndex);

                        // If there are no arguments, then just create an empty array
                        if (argumentsContent.IsEmpty)
                        {
                            functionArguments = [];
                        }
                        else
                        {
                            // We will keep empty entries so that we can treat them as null
                            functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                        }

                        remainder = expressionFunctionAsSpan.Slice(argumentsEndIndex + 1).Trim();
                    }
                }
                else
                {
                    int nextMethodIndex = expressionFunction.IndexOf('.', methodStartIndex);
                    int methodLength = expressionFunction.Length - methodStartIndex;
                    int indexerIndex = expressionFunction.IndexOf('[', methodStartIndex);

                    // We don't want to consume the indexer
                    if (indexerIndex >= 0 && indexerIndex < nextMethodIndex)
                    {
                        nextMethodIndex = indexerIndex;
                    }

                    functionArguments = [];

                    if (nextMethodIndex > 0)
                    {
                        methodLength = nextMethodIndex - methodStartIndex;
                        remainder = expressionFunctionAsSpan.Slice(nextMethodIndex).Trim();
                    }

                    ReadOnlySpan<char> netPropertyName = expressionFunctionAsSpan.Slice(methodStartIndex, methodLength).Trim();

                    ProjectErrorUtilities.VerifyThrowInvalidProject(netPropertyName.Length > 0, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                    // We have been asked for a property or a field
                    defaultBindingFlags |= (BindingFlags.GetProperty | BindingFlags.GetField);

                    functionName = netPropertyName;
                }

                // either there are no functions left or what we have is another function or an indexer
                if (remainder.IsEmpty || remainder[0] == '.' || remainder[0] == '[')
                {
                    functionBuilder.Name = functionName.ToString();
                    functionBuilder.Arguments = functionArguments;
                    functionBuilder.BindingFlags = defaultBindingFlags;
                    functionBuilder.Remainder = remainder.ToString();
                }
                else
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                }
            }

            /// <summary>
            /// Coerce the arguments according to the parameter types
            /// Will only return null if the coercion didn't work due to an InvalidCastException.
            /// </summary>
            private static object[] CoerceArguments(object[] args, ParameterInfo[] parameters)
            {
                object[] coercedArguments = new object[args.Length];

                try
                {
                    // Do our best to coerce types into the arguments for the function
                    for (int n = 0; n < parameters.Length; n++)
                    {
                        if (args[n] == null)
                        {
                            // We can't coerce (object)null -- that's as general
                            // as it can get!
                            continue;
                        }

                        // Here we have special case conversions on a type basis
                        if (parameters[n].ParameterType == typeof(char[]))
                        {
                            coercedArguments[n] = args[n].ToString().ToCharArray();
                        }
                        else if (parameters[n].ParameterType.GetTypeInfo().IsEnum && args[n] is string v && v.Contains('.'))
                        {
                            Type enumType = parameters[n].ParameterType;
                            string typeLeafName = $"{enumType.Name}.";
                            string typeFullName = $"{enumType.FullName}.";

                            // Enum.parse expects commas between enum components
                            // We'll support the C# type | syntax too
                            // We'll also allow the user to specify the leaf or full type name on the enum
                            string argument = args[n].ToString().Replace('|', ',').Replace(typeFullName, "").Replace(typeLeafName, "");

                            // Parse the string representation of the argument into the destination enum
                            coercedArguments[n] = Enum.Parse(enumType, argument);
                        }
                        else
                        {
                            // change the type of the final unescaped string into the destination
                            coercedArguments[n] = Convert.ChangeType(args[n], parameters[n].ParameterType, CultureInfo.InvariantCulture);
                        }
                    }
                }
                // The coercion failed therefore we return null
                catch (InvalidCastException)
                {
                    return null;
                }
                catch (FormatException)
                {
                    return null;
                }
                catch (OverflowException)
                {
                    // https://github.com/dotnet/msbuild/issues/2882
                    // test: PropertyFunctionMathMaxOverflow
                    return null;
                }

                return coercedArguments;
            }

            /// <summary>
            /// Make an attempt to create a string showing what we were trying to execute when we failed.
            /// This will show any intermediate evaluation which may help the user figure out what happened.
            /// </summary>
            private string GenerateStringOfMethodExecuted(string expression, object objectInstance, string name, object[] args)
            {
                string parameters = String.Empty;
                if (args != null)
                {
                    foreach (object arg in args)
                    {
                        if (arg == null)
                        {
                            parameters += "null";
                        }
                        else
                        {
                            string argString = arg.ToString();
                            if (arg is string && argString.Length == 0)
                            {
                                parameters += "''";
                            }
                            else
                            {
                                parameters += arg.ToString();
                            }
                        }

                        parameters += ", ";
                    }

                    if (parameters.Length > 2)
                    {
                        parameters = parameters.Substring(0, parameters.Length - 2);
                    }
                }

                if (objectInstance == null)
                {
                    string typeName = _receiverType.FullName;

                    // We don't want to expose the real type name of our intrinsics
                    // so we'll replace it with "MSBuild"
                    if (_receiverType == typeof(IntrinsicFunctions))
                    {
                        typeName = "MSBuild";
                    }
                    if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return $"[{typeName}]::{name}({parameters})";
                    }
                    else
                    {
                        return $"[{typeName}]::{name}";
                    }
                }
                else
                {
                    string propertyValue = $"\"{objectInstance as string}\"";

                    if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return $"{propertyValue}.{name}({parameters})";
                    }
                    else
                    {
                        return $"{propertyValue}.{name}";
                    }
                }
            }

            /// <summary>
            /// Check the property function allowlist whether this method is available.
            /// </summary>
            private static bool IsStaticMethodAvailable(Type receiverType, string methodName)
            {
                if (receiverType == typeof(IntrinsicFunctions))
                {
                    // These are our intrinsic functions, so we're OK with those
                    return true;
                }

                if (Traits.Instance.EnableAllPropertyFunctions)
                {
                    // anything goes
                    return true;
                }

                return AvailableStaticMethods.GetTypeInformationFromTypeCache(receiverType.FullName, methodName) != null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsInstanceMethodAvailable(string methodName)
            {
                if (Traits.Instance.EnableAllPropertyFunctions)
                {
                    // anything goes
                    return true;
                }

                // This could be expanded to an allow / deny list.
                return !string.Equals("GetType", methodName, StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Construct and instance of objectType based on the constructor or method arguments provided.
            /// Arguments must never be null.
            /// </summary>
            private object LateBindExecute(Exception ex, BindingFlags bindingFlags, object objectInstance /* null unless instance method */, object[] args, bool isConstructor)
            {
                // First let's try for a method where all arguments are strings..
                Type[] types = new Type[_arguments.Length];
                for (int n = 0; n < _arguments.Length; n++)
                {
                    types[n] = typeof(string);
                }

                MethodBase memberInfo;
                if (isConstructor)
                {
                    memberInfo = _receiverType.GetConstructor(bindingFlags, null, types, null);
                }
                else
                {
                    memberInfo = _receiverType.GetMethod(_methodMethodName, bindingFlags, null, types, null);
                }

                // If we didn't get a match on all string arguments,
                // search for a method with the right number of arguments
                if (memberInfo == null)
                {
                    // Gather all methods that may match
                    IEnumerable<MethodBase> members;
                    if (isConstructor)
                    {
                        members = _receiverType.GetConstructors(bindingFlags);
                    }
                    else if (_receiverType == typeof(IntrinsicFunctions) && IntrinsicFunctionOverload.IsKnownOverloadMethodName(_methodMethodName))
                    {
                        MemberInfo[] foundMembers = _receiverType.FindMembers(
                            MemberTypes.Method,
                            bindingFlags,
                            (info, criteria) => string.Equals(info.Name, (string)criteria, StringComparison.OrdinalIgnoreCase),
                            _methodMethodName);
                        Array.Sort(foundMembers, IntrinsicFunctionOverload.IntrinsicFunctionOverloadMethodComparer);
                        members = foundMembers.Cast<MethodBase>();
                    }
                    else
                    {
                        members = _receiverType.GetMethods(bindingFlags).Where(m => string.Equals(m.Name, _methodMethodName, StringComparison.OrdinalIgnoreCase));
                    }

                    foreach (MethodBase member in members)
                    {
                        ParameterInfo[] parameters = member.GetParameters();

                        // Simple match on name and number of params, we will be case insensitive
                        if (parameters.Length == _arguments.Length)
                        {
                            // Try to find a method with the right name, number of arguments and
                            // compatible argument types
                            // we have a match on the name and argument number
                            // now let's try to coerce the arguments we have
                            // into the arguments on the matching method
                            object[] coercedArguments = CoerceArguments(args, parameters);

                            if (coercedArguments != null)
                            {
                                // We have a complete match
                                memberInfo = member;
                                args = coercedArguments;
                                break;
                            }
                        }
                    }
                }

                object functionResult = null;

                // We have a match and coerced arguments, let's construct..
                if (memberInfo != null && args != null)
                {
                    if (isConstructor)
                    {
                        functionResult = ((ConstructorInfo)memberInfo).Invoke(args);
                    }
                    else
                    {
                        functionResult = ((MethodInfo)memberInfo).Invoke(objectInstance /* null if static method */, args);
                    }
                }
                else if (!isConstructor)
                {
                    throw ex;
                }

                if (functionResult == null && isConstructor)
                {
                    throw new TargetInvocationException(new MissingMethodException());
                }

                return functionResult;
            }
        }
    }

#nullable enable

    internal static class IntrinsicFunctionOverload
    {
        private static readonly string[] s_knownOverloadName = { "Add", "Subtract", "Multiply", "Divide", "Modulo", };

        // Order by the TypeCode of the first parameter.
        // When change wave is enabled, order long before double.
        // Otherwise preserve prior behavior of double before long.
        // For reuse, the comparer is cached in a non-generic type.
        // Both comparer instances can be cached to support change wave testing.
        private static IComparer<MemberInfo>? s_comparerLongBeforeDouble;

        internal static IComparer<MemberInfo> IntrinsicFunctionOverloadMethodComparer => LongBeforeDoubleComparer;

        private static IComparer<MemberInfo> LongBeforeDoubleComparer => s_comparerLongBeforeDouble ??= Comparer<MemberInfo>.Create((key0, key1) => SelectTypeOfFirstParameter(key0).CompareTo(SelectTypeOfFirstParameter(key1)));

        internal static bool IsKnownOverloadMethodName(string methodName) => s_knownOverloadName.Any(name => string.Equals(name, methodName, StringComparison.OrdinalIgnoreCase));

        private static TypeCode SelectTypeOfFirstParameter(MemberInfo member)
        {
            MethodBase? method = member as MethodBase;
            if (method == null)
            {
                return TypeCode.Empty;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length > 0
                ? Type.GetTypeCode(parameters[0].ParameterType)
                : TypeCode.Empty;
        }
    }
}
