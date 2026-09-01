// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Stores the source range and processing flags for one function argument.
/// </summary>
/// <param name="range">The argument's range within its source text.</param>
/// <param name="flags">The processing required before the argument can be consumed.</param>
internal readonly struct Argument(StringSegmentRange range, ArgumentFlags flags)
{
    /// <summary>
    ///  Gets an argument representing <see langword="null"/>.
    /// </summary>
    public static Argument Null => new(StringSegmentRange.Null, ArgumentFlags.None);

    /// <summary>
    ///  Creates an empty argument at <paramref name="range"/>.
    /// </summary>
    /// <param name="range">The empty argument's range within its source text.</param>
    /// <returns>
    ///  The empty argument.
    /// </returns>
    public static Argument Empty(StringSegmentRange range)
        => new(range, ArgumentFlags.None);

    /// <summary>
    ///  Gets the argument's range within its source text.
    /// </summary>
    public StringSegmentRange Range { get; } = range;

    /// <summary>
    ///  Gets the processing flags for the argument.
    /// </summary>
    public ArgumentFlags Flags { get; } = flags;

    /// <summary>
    ///  Determines the processing flags for an argument source.
    /// </summary>
    /// <param name="argument">The normalized argument source.</param>
    /// <returns>
    ///  The processing flags for the argument.
    /// </returns>
    public static ArgumentFlags GetFlags(StringSegment argument)
    {
        ArgumentFlags flags = ArgumentFlags.None;

        if (argument.Contains("$("))
        {
            flags |= ArgumentFlags.ExpandProperties;
        }

        if (EscapingUtilities.ContainsEscapeSequence(argument))
        {
            flags |= ArgumentFlags.Unescape;
        }

        return flags;
    }
}
