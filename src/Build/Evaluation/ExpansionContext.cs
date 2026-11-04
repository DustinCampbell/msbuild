// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#else
using Path = System.IO.Path;
#endif

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Provides the properties, options, and services used during expansion.
/// </summary>
/// <remarks>
///  Initializes the inputs used during expansion.
/// </remarks>
/// <param name="properties">The property provider, or <see langword="null"/> when not needed.</param>
/// <param name="options">The expansion options.</param>
/// <param name="location">The location used for diagnostics and source-relative functions.</param>
/// <param name="propertiesUseTracker">The tracker for property reads and source of the logging context.</param>
/// <param name="fileSystem">The filesystem used during expansion.</param>
internal readonly struct ExpansionContext(
    IPropertyProvider<IProperty>? properties,
    ExpanderOptions options,
    IElementLocation location,
    PropertiesUseTracker propertiesUseTracker,
    IFileSystem fileSystem)
{
    /// <summary>
    ///  Gets the property provider used to expand function arguments, if needed.
    /// </summary>
    public IPropertyProvider<IProperty>? Properties { get; } = properties;

    /// <summary>
    ///  Gets the expansion options.
    /// </summary>
    public ExpanderOptions Options { get; } = options;

    /// <summary>
    ///  Gets the location used for diagnostics and source-relative functions.
    /// </summary>
    public IElementLocation Location { get; } = location;

    /// <summary>
    ///  Gets the tracker notified when properties are read.
    /// </summary>
    public PropertiesUseTracker PropertiesUseTracker { get; } = propertiesUseTracker;

    /// <summary>
    ///  Gets the filesystem used during expansion.
    /// </summary>
    public IFileSystem FileSystem { get; } = fileSystem;

    /// <summary>
    ///  Gets the source file's directory for source-relative functions.
    /// </summary>
    /// <value>
    ///  The containing directory, an empty string when the source file is unspecified or whitespace,
    ///  or <see langword="null"/> when the source path is a filesystem root.
    /// </value>
    public string? StartingDirectory
        => string.IsNullOrWhiteSpace(Location.File)
            ? string.Empty
            : Path.GetDirectoryName(Location.File);

    /// <summary>
    ///  Gets the logging context supplied by the property-use tracker.
    /// </summary>
    /// <value>
    ///  The tracker's logging context, or <see langword="null"/> when logging is unavailable.
    /// </value>
    public LoggingContext? LoggingContext => PropertiesUseTracker.LoggingContext;
}
