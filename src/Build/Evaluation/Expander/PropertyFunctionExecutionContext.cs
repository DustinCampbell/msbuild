// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#else
using System.IO;
#endif
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides environmental state required while executing a property function.
/// </summary>
internal readonly struct PropertyFunctionExecutionContext<T>(
    IPropertyProvider<T> properties,
    IFileSystem fileSystem,
    LoggingContext loggingContext,
    IElementLocation location)
    where T : class, IProperty
{
    public IPropertyProvider<T> Properties { get; } = properties;

    public IFileSystem FileSystem { get; } = fileSystem;

    public LoggingContext LoggingContext { get; } = loggingContext;

    public IElementLocation Location { get; } = location;

    public string? StartingDirectory
        => string.IsNullOrWhiteSpace(Location.File)
            ? string.Empty
            : Path.GetDirectoryName(Location.File);
}
