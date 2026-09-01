// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides allocation-free access to packed function arguments.
/// </summary>
/// <param name="buffer">The source text containing the argument ranges.</param>
/// <param name="arguments">The packed arguments.</param>
internal readonly struct ArgumentList(string? buffer, OneOrMany<Argument> arguments)
{
    /// <summary>
    ///  Gets an empty argument list.
    /// </summary>
    public static ArgumentList Empty => default;

    private readonly string? _buffer = buffer;
    private readonly OneOrMany<Argument> _arguments = arguments;

    /// <summary>
    ///  Gets the number of arguments.
    /// </summary>
    public int Count
        => _arguments.Count;

    /// <summary>
    ///  Gets the argument at the specified index.
    /// </summary>
    public StringSegment this[int index]
        => _arguments[index].Range.ToSegment(_buffer);

    /// <summary>
    ///  Gets the processing flags for the argument at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <returns>
    ///  The argument's processing flags.
    /// </returns>
    public ArgumentFlags GetFlags(int index)
        => _arguments[index].Flags;

    /// <summary>
    ///  Returns an allocation-free enumerator over the arguments.
    /// </summary>
    public Enumerator GetEnumerator()
        => new(_buffer, _arguments);

    /// <summary>
    ///  Enumerates the arguments.
    /// </summary>
    public struct Enumerator(string? buffer, OneOrMany<Argument> arguments)
    {
        private OneOrMany<Argument>.Enumerator _arguments = arguments.GetEnumerator();

        /// <summary>
        ///  Gets the current argument.
        /// </summary>
        public readonly StringSegment Current
            => _arguments.Current.Range.ToSegment(buffer);

        /// <summary>
        ///  Advances to the next argument.
        /// </summary>
        public bool MoveNext()
            => _arguments.MoveNext();
    }
}
