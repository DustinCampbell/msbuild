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

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Carries dependencies used while executing property functions.
/// </summary>
/// <param name="properties">The properties available during expansion.</param>
/// <param name="location">The project element location associated with the invocation.</param>
/// <param name="fileSystem">The file system used during expansion.</param>
/// <param name="loggingContext">The logging context used during expansion.</param>
internal readonly struct ExecutionContext(
    IPropertyProvider<IProperty> properties,
    IElementLocation location,
    IFileSystem fileSystem,
    LoggingContext? loggingContext)
{
    /// <summary>
    ///  Gets the file system used during expansion.
    /// </summary>
    public IFileSystem FileSystem { get; } = fileSystem;

    /// <summary>
    ///  Gets the logging context used during expansion.
    /// </summary>
    public LoggingContext? LoggingContext { get; } = loggingContext;

    /// <summary>
    ///  Gets the project element location associated with the invocation.
    /// </summary>
    public IElementLocation Location { get; } = location;

    /// <summary>
    ///  Gets the properties available during expansion.
    /// </summary>
    public IPropertyProvider<IProperty> Properties { get; } = properties;

    /// <summary>
    ///  Gets the directory containing the project element associated with the invocation.
    /// </summary>
    /// <returns>
    ///  The containing directory, <see cref="string.Empty"/> when no file is associated with the invocation,
    ///  or <see langword="null"/> when the file has no directory component.
    /// </returns>
    public string? GetStartingDirectory()
    {
        string file = Location.File;

        return !file.IsNullOrWhiteSpace()
            ? Path.GetDirectoryName(file)
            : string.Empty;
    }
}
