// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
///  Represents the location of an XML node in a project file.
/// </summary>
/// <remarks>
///  <para>
///   Instances of this type are immutable and may be freely shared across threads.
///  </para>
///  <para>
///   Editing the project XML through the MSBuild API will invalidate existing locations
///   until the XML is reloaded.
///  </para>
/// </remarks>
[Serializable]
public abstract class ElementLocation : IElementLocation, IEquatable<ElementLocation>, ITranslatable, IImmutable
{
    /// <summary>
    ///  Gets a shared instance representing an unknown or unnamed location.
    /// </summary>
    /// <value>
    ///  An <see cref="ElementLocation"/> with an empty file path and zero line/column.
    /// </value>
    /// <remarks>
    ///  Use this for projects that have not been given a name. When an element's location
    ///  is truly absent, use <see langword="null"/> instead.
    /// </remarks>
    public static ElementLocation EmptyLocation => EmptyElementLocation.Instance;

    /// <summary>
    ///  Gets the file from which this element originated.
    /// </summary>
    /// <value>
    ///  The file path, or an empty string if not known. This may differ from the project file
    ///  if the element was part of an import or originated in a targets file.
    /// </value>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public abstract string File { get; }

    /// <summary>
    ///  Gets the line number where this element exists in its file.
    /// </summary>
    /// <value>
    ///  The 1-based line number, or zero if unknown.
    /// </value>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public abstract int Line { get; }

    /// <summary>
    ///  Gets the column number where this element exists in its file.
    /// </summary>
    /// <value>
    ///  The 1-based column number, or zero if unknown.
    /// </value>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public abstract int Column { get; }

    /// <summary>
    ///  Gets the location formatted for display in a diagnostic message.
    /// </summary>
    /// <value>
    ///  A string such as <c>c:\foo\bar.csproj (12,34)</c>. A new string is allocated on each access.
    /// </value>
    public string LocationString
    {
        get
        {
            return GetLocationString(File, Line, Column);

            static string GetLocationString(string file, int line, int column)
                => line != 0
                    ? column != 0
                        ? ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("FileLocation", file, line, column)
                        : $"{file} ({line})"
                    : file;
        }
    }

    public override int GetHashCode()
        => Line.GetHashCode() ^ Column.GetHashCode(); // Line and column are good enough

    public override bool Equals(object? obj)
        => obj is IElementLocation other && Equals(other);

    public bool Equals(ElementLocation? other)
        => other is not null &&
           Line == other.Line &&
           Column == other.Column &&
           string.Equals(File, other.File, StringComparison.OrdinalIgnoreCase);

    public override string ToString()
        => LocationString;

    /// <summary>
    ///  Serializes this location to the given translator.
    /// </summary>
    /// <param name="translator">The translator to write to.</param>
    /// <remarks>
    ///  Values are always serialized as <see cref="int"/>, even when stored internally as
    ///  <see cref="ushort"/>, to avoid needing a discriminator byte for negligible savings.
    /// </remarks>
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
    ///  Deserializes an <see cref="ElementLocation"/> from the given translator.
    /// </summary>
    /// <param name="translator">The translator to read from.</param>
    /// <returns>
    ///  An <see cref="ElementLocation"/> reconstructed from the serialized data.
    /// </returns>
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

    /// <summary>
    ///  Creates an <see cref="ElementLocation"/> with only a file path and no line or column information.
    /// </summary>
    /// <param name="file">The file path, or <see langword="null"/> if not known.</param>
    /// <returns>
    ///  An <see cref="ElementLocation"/> for the specified file.
    /// </returns>
    internal static ElementLocation Create(string? file)
        => file.IsNullOrEmpty()
            ? EmptyLocation
            : new FileOnlyElementLocation(file);

    /// <summary>
    ///  Creates an <see cref="ElementLocation"/> with the specified file, line, and column.
    /// </summary>
    /// <param name="file">
    ///  The file path, or <see langword="null"/> if the file was not loaded from disk.
    /// </param>
    /// <param name="line">The 1-based line number, or 0 if unknown.</param>
    /// <param name="column">The 1-based column number, or 0 if unknown.</param>
    /// <returns>
    ///  An <see cref="ElementLocation"/> representing the specified location.
    /// </returns>
    public static ElementLocation Create(string? file, int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        if (line == 0 && column == 0)
        {
            return Create(file);
        }

        return line <= ushort.MaxValue && column <= ushort.MaxValue
            ? new SmallElementLocation(file ?? string.Empty, (ushort)line, (ushort)column)
            : new RegularElementLocation(file ?? string.Empty, line, column);
    }

    private sealed class EmptyElementLocation : ElementLocation
    {
        public static readonly ElementLocation Instance = new EmptyElementLocation();

        private EmptyElementLocation()
        {
        }

        public override string File => string.Empty;

        public override int Line => 0;

        public override int Column => 0;
    }

    private sealed class FileOnlyElementLocation(string file) : ElementLocation
    {
        public override string File => file;

        public override int Line => 0;

        public override int Column => 0;
    }

    // Stores line and column as ushort to save 4 bytes per instance.
    // The vast majority of locations fit in this range (files rarely exceed 65535 lines).
    private sealed class SmallElementLocation(string file, ushort line, ushort column) : ElementLocation
    {
        private readonly ushort _line = line;
        private readonly ushort _column = column;

        public override string File { get; } = file;

        public override int Line => _line;

        public override int Column => _column;
    }

    // Stores line and column as int when values exceed ushort.MaxValue.
    private sealed class RegularElementLocation(string file, int line, int column) : ElementLocation
    {
        public override string File { get; } = file;

        public override int Line { get; } = line;

        public override int Column { get; } = column;
    }
}
