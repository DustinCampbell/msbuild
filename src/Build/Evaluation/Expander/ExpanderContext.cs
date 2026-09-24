// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides services used while expanding expressions.
/// </summary>
/// <param name="properties">
///  The properties available during expansion, or <see langword="null"/> if properties are unavailable.
/// </param>
/// <param name="loggingContext">
///  The logging context for the expansion, or <see langword="null"/> if logging is unavailable.
/// </param>
/// <param name="fileSystem">
///  The file system used during expansion, or <see langword="null"/> to use <see cref="FileSystems.Default"/>.
/// </param>
/// <param name="propertiesUseTracker">
///  The tracker for properties read during expansion, or <see langword="null"/> if property tracking is unavailable.
/// </param>
/// <param name="location">
///  The location of the element being expanded, or <see langword="null"/> if the location is unavailable.
/// </param>
internal readonly struct ExpanderContext(
    IPropertyProvider<IProperty>? properties = null,
    LoggingContext? loggingContext = null,
    IFileSystem? fileSystem = null,
    PropertiesUseTracker? propertiesUseTracker = null,
    IElementLocation? location = null)
{
    /// <summary>
    ///  Gets the properties available during expansion, or <see langword="null"/> if properties are unavailable.
    /// </summary>
    public IPropertyProvider<IProperty>? Properties => properties;

    /// <summary>
    ///  Gets the logging context for the expansion, or <see langword="null"/> if logging is unavailable.
    /// </summary>
    public LoggingContext? LoggingContext => loggingContext;

    /// <summary>
    ///  Gets the file system used during expansion.
    /// </summary>
    public IFileSystem FileSystem => fileSystem ?? FileSystems.Default;

    /// <summary>
    ///  Gets the tracker for properties read during expansion, or <see langword="null"/> if property tracking is unavailable.
    /// </summary>
    public PropertiesUseTracker? PropertiesUseTracker => propertiesUseTracker;

    /// <summary>
    ///  Gets the location of the element being expanded, or <see langword="null"/> if the location is unavailable.
    /// </summary>
    public IElementLocation? Location => location;
}
