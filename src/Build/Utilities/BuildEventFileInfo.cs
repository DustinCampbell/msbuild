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
    /// <summary>
    ///  Gets the filename/path to be associated with some build event.
    /// </summary>
    /// <value>
    ///  The filename/path string.
    /// </value>
    public string File { get; }

    /// <summary>
    ///  Gets the line number of interest in the file.
    /// </summary>
    /// <value>
    ///  Line number, or zero if not available.
    /// </value>
    public int Line { get; }

    /// <summary>
    ///  Gets the column number of interest in the file.
    /// </summary>
    /// <value>
    ///  Column number, or zero if not available.
    /// </value>
    public int Column { get; }

    /// <summary>
    ///  Gets the last line number of a range of interesting lines in the file.
    /// </summary>
    /// <value>
    ///  Last line number, or zero if not available.
    /// </value>
    public int EndLine { get; }

    /// <summary>
    ///  Gets the last column number of a range of interesting columns in the file.
    /// </summary>
    /// <value>
    ///  Last column number, or zero if not available.
    /// </value>
    public int EndColumn { get; }

    private BuildEventFileInfo(string? filePath = null, int line = 0, int column = 0, int endLine = 0, int endColumn = 0)
    {
        File = filePath ?? string.Empty;
        Line = line;
        Column = column;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    public static BuildEventFileInfo Empty { get; } = new();

    public static BuildEventFileInfo From(IElementLocation location)
        => location is { File: null or [], Line: 0, Column: 0 }
            ? Empty
            : new(location.File, location.Line, location.Column);

    public static BuildEventFileInfo From(string filePath)
        => filePath is null or []
            ? Empty
            : new(filePath);

    public static BuildEventFileInfo From(string filePath, int line)
        => filePath is null or [] && line is 0
            ? Empty
            : new(filePath, line);

    public static BuildEventFileInfo From(string filePath, int line, int column)
        => filePath is null or [] && line is 0 && column is 0
            ? Empty
            : new(filePath, line, column);

    public static BuildEventFileInfo From(string filePath, int line, int column, int endLine, int endColumn)
        => filePath is null or [] && line is 0 && column is 0 && endLine is 0 && endColumn is 0
            ? Empty
            : new(filePath, line, column, endLine, endColumn);

    public static BuildEventFileInfo From(XmlException e)
        => new(
            filePath: e.SourceUri?.Length > 0 ? new Uri(e.SourceUri).LocalPath : string.Empty,
            e.LineNumber,
            e.LinePosition);

    public static BuildEventFileInfo From(string filePath, XmlException e)
        => From(filePath, e.LineNumber, e.LinePosition);
}
