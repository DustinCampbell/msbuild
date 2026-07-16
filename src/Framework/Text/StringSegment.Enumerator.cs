// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.Build.Text;

internal readonly partial struct StringSegment
{
    /// <summary>
    ///  Returns an enumerator that iterates over the characters of this segment. Enables <c>foreach</c>
    ///  iteration directly over a <see cref="StringSegment"/> without allocating.
    /// </summary>
    /// <returns>An <see cref="Enumerator"/> for this segment.</returns>
    public Enumerator GetEnumerator()
        => new(this);

    /// <summary>
    ///  Enumerates the characters of a <see cref="StringSegment"/>. Iteration is allocation-free and
    ///  operates directly over the underlying buffer.
    /// </summary>
    /// <remarks>
    ///  Initializes a new enumerator over the characters of the specified segment.
    /// </remarks>
    /// <param name="segment">The segment whose characters are enumerated.</param>
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref struct Enumerator(StringSegment segment)
    {
        private int _index = -1;

        /// <summary>
        ///  Gets the character at the current position of the enumerator.
        /// </summary>
        /// <remarks>
        ///  Reads directly from the underlying buffer. <see cref="MoveNext"/> has already validated that the
        ///  position is in range, so the segment indexer's bounds check is intentionally skipped here.
        /// </remarks>
        public readonly char Current
            => segment.Buffer![segment.Offset + _index];

        /// <summary>
        ///  Advances the enumerator to the next character of the segment.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if the enumerator advanced to another character; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int nextIndex = _index + 1;
            if (nextIndex < segment.Length)
            {
                _index = nextIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Resets the enumerator to its initial position, before the first character of the segment.
        /// </summary>
        public void Reset()
            => _index = -1;
    }
}
