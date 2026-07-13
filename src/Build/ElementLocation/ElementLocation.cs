// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
///  Represents the source location of an XML node in a project file.
/// </summary>
/// <remarks>
///  Instances are immutable. Editing project XML through the MSBuild APIs can make existing locations stale until the XML is reloaded.
/// </remarks>
[Serializable]
public abstract class ElementLocation : IElementLocation, IEquatable<ElementLocation>, ITranslatable, IImmutable
{
    /// <summary>
    ///  Gets an element location with no file, line, or column information.
    /// </summary>
    /// <remarks>
    ///  Use a <see langword="null"/> location to represent a missing location. Use this value when a location exists but cannot be identified.
    /// </remarks>
    public static ElementLocation EmptyLocation => EmptyElementLocation.Instance;

    /// <summary>
    ///  Gets the file from which this element originated.
    /// </summary>
    /// <remarks>
    ///  This value may differ from the project file when the element originated in an imported project or targets file.
    ///  Returns an empty string when the file is unknown.
    /// </remarks>
    public abstract string File { get; }

    /// <summary>
    ///  Gets the line number where this element appears in its file.
    /// </summary>
    /// <remarks>
    ///  Lines are 1-based. A value of 0 indicates that the line is unknown.
    /// </remarks>
    public abstract int Line { get; }

    /// <summary>
    ///  Gets the column number where this element appears in its file.
    /// </summary>
    /// <remarks>
    ///  Columns are 1-based. A value of 0 indicates that the column is unknown.
    /// </remarks>
    public abstract int Column { get; }

    /// <summary>
    ///  Gets this location formatted for display in a message.
    /// </summary>
    /// <remarks>
    ///  The returned string uses the form <c>file</c>, <c>file (line)</c>, or <c>file (line,column)</c>, depending on the available information.
    /// </remarks>
    public string LocationString
        => GetLocationString(File, Line, Column);

    /// <summary>
    ///  Returns the hash code for this location.
    /// </summary>
    /// <returns>
    ///  The hash code for this location.
    /// </returns>
    public override int GetHashCode()
        => Line.GetHashCode() ^ Column.GetHashCode(); // Line and column are good enough

    /// <summary>
    ///  Determines whether the specified object is equal to this location.
    /// </summary>
    /// <param name="obj">The object to compare with this location.</param>
    /// <returns>
    ///  <see langword="true"/> if the specified object is equal to this location; otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj)
        => obj is ElementLocation other && Equals(other);

    /// <summary>
    ///  Determines whether the specified location is equal to this location.
    /// </summary>
    /// <param name="other">The location to compare with this location.</param>
    /// <returns>
    ///  <see langword="true"/> if the specified location has the same file, line, and column; otherwise, <see langword="false"/>.
    /// </returns>
    public virtual bool Equals(ElementLocation? other)
        => other is not null
        && Line == other.Line
        && Column == other.Column
        && string.Equals(File, other.File, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns this location formatted for display.
    /// </summary>
    /// <returns>
    ///  This location formatted for display.
    /// </returns>
    public override string ToString()
        => LocationString;

    /// <summary>
    ///  Creates an element location with file, line, and column information.
    /// </summary>
    /// <param name="file">The file from which the element originated, or <see langword="null"/> if the file is unknown.</param>
    /// <param name="line">The 1-based line number, or 0 if the line is unknown.</param>
    /// <param name="column">The 1-based column number, or 0 if the column is unknown.</param>
    /// <returns>
    ///  An element location for the specified file, line, and column.
    /// </returns>
    public static ElementLocation Create(string? file, int line, int column)
    {
        if (line == 0 && column == 0)
        {
            return Create(file);
        }

        file ??= string.Empty;

        Assumed.PositiveOrZero(line, "Use zero for unknown line");
        Assumed.PositiveOrZero(column, "Use zero for unknown column");

        return line <= 65535 && column <= 65535
            ? new SmallElementLocation(file, (ushort)line, (ushort)column)
            : new RegularElementLocation(file, line, column);
    }

    /// <summary>
    ///  Creates an element location with file information only.
    /// </summary>
    /// <param name="file">The file from which the element originated, or <see langword="null"/> if the file is unknown.</param>
    internal static ElementLocation Create(string? file)
        => file.IsNullOrEmpty()
            ? EmptyLocation
            : new FileOnlyElementLocation(file);

    /// <summary>
    ///  Writes this location to the serializer.
    /// </summary>
    void ITranslatable.Translate(ITranslator translator)
    {
        Assumed.Equal(translator.Mode, TranslationDirection.WriteToStream, "write only");

        string file = File;
        int line = Line;
        int column = Column;
        translator.Translate(ref file);
        translator.Translate(ref line);
        translator.Translate(ref column);
    }

    /// <summary>
    ///  Creates an element location during deserialization.
    /// </summary>
    internal static ElementLocation FactoryForDeserialization(ITranslator translator)
    {
        string? file = null;
        int line = 0;
        int column = 0;
        translator.Translate(ref file);
        translator.Translate(ref line);
        translator.Translate(ref column);

        return Create(file, line, column);
    }

    private static string GetLocationString(string file, int line, int column)
        => line != 0
            ? column != 0
                ? $"{file} ({line},{column})"
                : $"{file} ({line})"
            : file;

    /// <summary>
    ///  Represents a location with no file, line, or column information.
    /// </summary>
    private sealed class EmptyElementLocation : ElementLocation
    {
        public static readonly EmptyElementLocation Instance = new();

        public override string File => string.Empty;

        public override int Line => 0;

        public override int Column => 0;

        private EmptyElementLocation()
        {
        }
    }

    /// <summary>
    ///  Represents a location with file information only.
    /// </summary>
    private sealed class FileOnlyElementLocation(string file) : ElementLocation
    {
        public override string File => file;

        public override int Line => 0;

        public override int Column => 0;
    }

    /// <summary>
    ///  Represents a location whose line or column does not fit in a ushort.
    /// </summary>
    private sealed class RegularElementLocation(string file, int line, int column) : ElementLocation
    {
        public override string File => file;

        public override int Line => line;

        public override int Column => column;
    }

    /// <summary>
    ///  Represents a location whose line and column each fit in a ushort.
    /// </summary>
    private sealed class SmallElementLocation(string file, ushort line, ushort column) : ElementLocation
    {
        public override string File => file;

        public override int Line => line;

        public override int Column => column;
    }
}
