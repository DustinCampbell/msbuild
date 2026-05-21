// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
///  The location of an XML node in a file.
///  Any editing of the project XML through the MSBuild API's will invalidate locations in that XML until the XML is reloaded.
/// </summary>
/// <remarks>
///  This object is IMMUTABLE, so that it can be passed around arbitrarily.
///  DO NOT make these objects any larger. There are huge numbers of them and they are transmitted between nodes.
/// </remarks>
[Serializable]
public abstract class ElementLocation : IElementLocation, ITranslatable, IImmutable
{
    /// <summary>
    ///  Gets the empty element location.
    ///  This is not to be used when something is "missing": that should have a null location.
    ///  It is to be used for the project location when the project has not been given a name.
    ///  In that case, it exists, but can't have a specific location.
    /// </summary>
    public static ElementLocation EmptyLocation { get; } = new SmallElementLocation(file: null, line: 0, column: 0);

    /// <summary>
    ///  The file from which this particular element originated.  It may
    ///  differ from the ProjectFile if, for instance, it was part of
    ///  an import or originated in a targets file.
    ///  If not known, returns empty string.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public abstract string File { get; }

    /// <summary>
    ///  The line number where this element exists in its file.
    ///  The first line is numbered 1.
    ///  Zero indicates "unknown location".
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public abstract int Line { get; }

    /// <summary>
    ///  The column number where this element exists in its file.
    ///  The first column is numbered 1.
    ///  Zero indicates "unknown location".
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public abstract int Column { get; }

    /// <summary>
    ///  The location in a form suitable for replacement
    ///  into a message.
    ///  Example: "c:\foo\bar.csproj (12,34)"
    ///  Calling this creates and formats a new string.
    ///  PREFER TO PUT THE LOCATION INFORMATION AT THE START OF THE MESSAGE INSTEAD.
    ///  Only in rare cases should the location go within the message itself.
    /// </summary>
    public string LocationString => GetLocationString(File, Line, Column);

    /// <summary>
    ///  Get reasonable hash code.
    /// </summary>
    public override int GetHashCode()
        => Line.GetHashCode() ^ Column.GetHashCode(); // Line and column are good enough

    /// <summary>
    ///  Override Equals so that identical fields imply equal objects.
    /// </summary>
    public override bool Equals(object? obj)
        => obj is IElementLocation other &&
            Line == other.Line &&
            Column == other.Column &&
            string.Equals(File, other.File, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Location of element.
    /// </summary>
    public override string ToString()
        => LocationString;

    /// <summary>
    ///  Writes the packet to the serializer.
    ///  Always send as ints, even if ushorts are being used: otherwise it'd
    ///  need a byte to discriminate and the savings would be microscopic.
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
    ///  Factory for serialization.
    ///  Custom factory is needed because this class is abstract and uses a factory pattern.
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

    /// <summary>
    ///  Constructor for when we only know the file and nothing else.
    ///  This is the case when we are creating a new item, for example, and it has
    ///  not been evaluated from some XML.
    /// </summary>
    internal static ElementLocation Create(string file)
        => Create(file, line: 0, column: 0);

    /// <summary>
    ///  Constructor for the case where we have most or all information.
    ///  Numerical values must be 1-based, non-negative; 0 indicates unknown
    ///  File may be null, indicating the file was not loaded from disk.
    /// </summary>
    /// <remarks>
    ///  In AG there are 600 locations that have a file but zero line and column.
    ///  In theory yet another derived class could be made for these to save 4 bytes each.
    /// </remarks>
    public static ElementLocation Create(string? file, int line, int column)
    {
        if (file.IsNullOrEmpty() && line == 0 && column == 0)
        {
            return EmptyLocation;
        }

        return line <= 65535 && column <= 65535
            ? new SmallElementLocation(file, line, column)
            : new RegularElementLocation(file, line, column);
    }

    /// <summary>
    ///  The location in a form suitable for replacement into a message.
    ///  Example: "c:\foo\bar.csproj (12,34)"
    ///  Calling this creates and formats a new string.
    ///  PREFER TO PUT THE LOCATION INFORMATION AT THE START OF THE MESSAGE INSTEAD.
    ///  Only in rare cases should the location go within the message itself.
    /// </summary>
    private static string GetLocationString(string file, int line, int column)
        => line != 0
            ? column != 0
                ? ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("FileLocation", file, line, column)
                : $"{file} ({line})"
            : file;

    /// <summary>
    ///  Rarer variation for when the line and column won't each fit in a ushort.
    /// </summary>
    private sealed class RegularElementLocation : ElementLocation
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public override string File { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public override int Line { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public override int Column { get; }

        /// <summary>
        ///  Constructor for the case where we have most or all information.
        ///  Numerical values must be 1-based, non-negative; 0 indicates unknown
        ///  File may be null, indicating the file was not loaded from disk.
        /// </summary>
        internal RegularElementLocation(string? file, int line, int column)
        {
            ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(file, nameof(file));
            Assumed.PositiveOrZero(line, "Use zero for unknown");
            Assumed.PositiveOrZero(column, "Use zero for unknown");

            File = file ?? string.Empty;
            Line = line;
            Column = column;
        }
    }

    /// <summary>
    ///  For when the line and column each fit in a short - under 65536
    ///  (almost always will: microsoft.common.targets is less than 5000 lines long)
    ///  When loading Australian Government, for example, there are over 31,000 ElementLocation
    ///  objects so this saves 4 bytes each = 123KB
    ///  
    ///  A "very small" variation that used two bytes (or halves of a short) would fit about half of them
    ///  and save 4 more bytes each, but the CLR packs each field to 4 bytes, so it isn't actually any smaller.
    /// </summary>
    private sealed class SmallElementLocation : ElementLocation
    {
        private readonly string _file;
        private readonly ushort _line;
        private readonly ushort _column;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public override string File => _file;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public override int Line => _line;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public override int Column => _column;

        /// <summary>
        ///  Constructor for the case where we have most or all information.
        ///  Numerical values must be 1-based, non-negative; 0 indicates unknown
        ///  File may be null or empty, indicating the file was not loaded from disk.
        /// </summary>
        internal SmallElementLocation(string? file, int line, int column)
        {
            Assumed.PositiveOrZero(line, "Use zero for unknown");
            Assumed.PositiveOrZero(column, "Use zero for unknown");
            Assumed.LessThanOrEqual(line, 65535, "Use ElementLocation instead");
            Assumed.LessThanOrEqual(column, 65535, "Use ElementLocation instead");

            _file = file ?? string.Empty;
            _line = (ushort)line;
            _column = (ushort)column;
        }
    }
}
