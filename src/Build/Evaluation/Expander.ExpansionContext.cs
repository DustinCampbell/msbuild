// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Carries the stable dependencies for one expansion operation.
    /// </summary>
    private readonly struct ExpansionContext
    {
        private readonly Expander<P, I> _expander;

        /// <summary>
        ///  Initializes a context for one expansion operation.
        /// </summary>
        /// <param name="expander">The owning expander.</param>
        /// <param name="options">The requested expansion options.</param>
        /// <param name="location">The project element location associated with the operation.</param>
        public ExpansionContext(Expander<P, I> expander, ExpanderOptions options, IElementLocation location)
            : this(expander, options, new ErrorReporter(location))
        {
        }

        private ExpansionContext(Expander<P, I> expander, ExpanderOptions options, ErrorReporter errors)
        {
            _expander = expander;
            Options = options;
            Errors = errors;
        }

        /// <summary>
        ///  Gets the diagnostics reporter for the operation.
        /// </summary>
        public ErrorReporter Errors { get; }

        /// <summary>
        ///  Gets the file system used during expansion.
        /// </summary>
        public IFileSystem FileSystem => _expander._fileSystem;

        /// <summary>
        ///  Gets the item provider used during expansion.
        /// </summary>
        public IItemProvider<I> Items => _expander._items;

        /// <summary>
        ///  Gets the project element location associated with the operation.
        /// </summary>
        public IElementLocation Location => Errors.Location;

        /// <summary>
        ///  Gets the logging context used during expansion.
        /// </summary>
        public LoggingContext? LoggingContext => _expander._loggingContext;

        /// <summary>
        ///  Gets the metadata table used during expansion.
        /// </summary>
        public IMetadataTable Metadata => _expander._metadata;

        /// <summary>
        ///  Gets the requested expansion options.
        /// </summary>
        public ExpanderOptions Options { get; }

        /// <summary>
        ///  Gets the property provider used during expansion.
        /// </summary>
        public IPropertyProvider<P> Properties => _expander._properties;

        /// <summary>
        ///  Gets the logging context associated with property tracking.
        /// </summary>
        public LoggingContext? PropertyLoggingContext => _expander._propertiesUseTracker.LoggingContext;

        /// <summary>
        ///  Gets the property-use tracker used during expansion.
        /// </summary>
        public PropertiesUseTracker PropertiesUseTracker => _expander._propertiesUseTracker;

        /// <summary>
        ///  Creates a context for the same expansion operation with different options.
        /// </summary>
        /// <param name="options">The replacement expansion options.</param>
        /// <returns>
        ///  A context that shares the current operation's dependencies and uses <paramref name="options"/>.
        /// </returns>
        public ExpansionContext WithOptions(ExpanderOptions options)
            => options == Options
                ? this
                : new(_expander, options, Errors);
    }
}
