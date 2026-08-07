// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Collects string segments, adjusts each segment that may contain a file path, and weakly interns their
    ///  concatenation.
    /// </summary>
    /// <remarks>
    ///  This is purpose-built for property expansion to adjust segments that are actually file paths.
    /// </remarks>
    private ref struct StringSegmentBuilder
    {
        private SpanBasedStringBuilder? _builder;

        /// <summary>
        ///  Adjusts a segment that may contain a file path and adds it to be concatenated.
        /// </summary>
        public void Append(StringSegment segment)
        {
            if (segment.IsNullOrEmpty)
            {
                return;
            }

            segment = NativeMethodsShared.IsWindows
                ? segment
                : FileUtilities.MaybeAdjustFilePath(segment);

            (_builder ??= Strings.GetSpanBasedStringBuilder()).Append(segment.AsMemory());
        }

        /// <summary>
        ///  Adjusts a string that may contain a file path and adds it to be concatenated.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string value)
        {
            if (value.Length == 0)
            {
                return;
            }

            if (NativeMethodsShared.IsWindows)
            {
                (_builder ??= Strings.GetSpanBasedStringBuilder()).Append(value);
            }
            else
            {
                Append(FileUtilities.MaybeAdjustFilePath((StringSegment)value));
            }
        }

        /// <summary>
        ///  Adjusts a string region that may contain a file path and adds it to be concatenated.
        /// </summary>
        public void Append(string value, int start, int length)
        {
            if (length == 0)
            {
                return;
            }

            if (NativeMethodsShared.IsWindows)
            {
                (_builder ??= Strings.GetSpanBasedStringBuilder()).Append(value, start, length);
            }
            else
            {
                Append(FileUtilities.MaybeAdjustFilePath(new StringSegment(value, start, length)));
            }
        }

        /// <summary>
        ///  Returns the result of the concatenation.
        /// </summary>
        public readonly string GetResult()
            => _builder?.ToString() ?? string.Empty;

        /// <summary>
        ///  Returns the span-based builder to its pool.
        /// </summary>
        public void Dispose()
            => _builder?.Dispose();
    }
}
