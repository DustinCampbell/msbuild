// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace Microsoft.Build.Shared;

/// <summary>
///  This class encapsulates information about a file that is associated with a build event.
/// </summary>
internal sealed class BuildEventFileInfo
{
    internal static BuildEventFileInfo Empty { get; } = new(file: null, line: 0, column: 0);

    /// <summary>
    ///  Gets the filename/path to be associated with some build event.
    /// </summary>
    /// <value>
    ///  The filename/path string.
    /// </value>
    internal string File { get; }

    /// <summary>
    ///  Gets the line number of interest in the file.
    /// </summary>
    /// <value>
    ///  Line number, or zero if not available.
    /// </value>
    internal int Line { get; }

    /// <summary>
    ///  Gets the column number of interest in the file.
    /// </summary>
    /// <value>
    ///  Column number, or zero if not available.
    /// </value>
    internal int Column { get; }

    private BuildEventFileInfo(string? file, int line, int column)
    {
        // Projects that don't have a filename when the are built should use an empty string instead.
        File = file ?? string.Empty;
        Line = line;
        Column = column;
    }

    public static BuildEventFileInfo Create(string? file, int line = 0, int column = 0)
        => !file.IsNullOrEmpty() || line != 0 || column != 0
            ? new(file, line, column)
            : Empty;

    public static BuildEventFileInfo Create(IElementLocation location)
        => Create(location.File, location.Line, location.Column);

    public static BuildEventFileInfo Create(XmlException exception)
    {
        ErrorUtilities.VerifyThrow(exception != null, "Need exception context.");

        return Create(
            file: exception.SourceUri.IsNullOrEmpty() ? string.Empty : new Uri(exception.SourceUri).LocalPath,
            line: exception.LineNumber,
            column: exception.LinePosition);
    }

    public static BuildEventFileInfo Create(string file, XmlException exception)
    {
        ErrorUtilities.VerifyThrow(exception != null, "Need exception context.");

        return Create(
            file,
            line: exception.LineNumber,
            column: exception.LinePosition);
    }
}
