// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Carries dependencies used while executing property functions.
/// </summary>
/// <param name="properties">The properties available during expansion.</param>
/// <param name="fileSystem">The file system used during expansion.</param>
/// <param name="loggingContext">The logging context used during expansion.</param>
internal readonly struct ExecutionContext(
    IPropertyProvider<IProperty> properties,
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
    ///  Gets the properties available during expansion.
    /// </summary>
    public IPropertyProvider<IProperty> Properties { get; } = properties;
}
