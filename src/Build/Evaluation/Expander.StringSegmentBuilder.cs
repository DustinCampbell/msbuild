// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Collects string segments, adjusts each segment that may contain a file path, and concatenates them into
    ///  a single string.
    /// </summary>
    /// <remarks>
    ///  This is purpose-built for property expansion to adjust segments that are actually file paths.
    /// </remarks>
    private ref struct StringSegmentBuilder
    {
        private RefArrayBuilder<StringSegment> _segments;

        /// <summary>
        ///  Adjusts a segment that may contain a file path and adds it to be concatenated.
        /// </summary>
        public void Append(StringSegment segment)
        {
            if (segment.IsNullOrEmpty)
            {
                return;
            }

            if (NativeMethodsShared.IsWindows)
            {
                _segments.Add(segment);
            }
            else
            {
                _segments.Add(FileUtilities.MaybeAdjustFilePath(segment));
            }
        }

        /// <summary>
        ///  Returns the result of the concatenation.
        /// </summary>
        public readonly string GetResult()
            => StringSegment.Join(string.Empty, _segments.AsSpan());

        /// <summary>
        ///  Returns the rented segment array to the pool.
        /// </summary>
        public void Dispose()
            => _segments.Dispose();
    }
}
