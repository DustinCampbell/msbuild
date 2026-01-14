// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
using System.Buffers;
using System.IO;

namespace Microsoft.Build;

internal static class TextWriterExtensions
{
    extension(TextWriter writer)
    {
        /// <summary>
        ///  Allows writing a <see cref="ReadOnlySpan{Char}"/> to a <see cref="TextWriter"/>.
        /// </summary>
        public void Write(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
            {
                return;
            }

            if (writer is StringWriter stringWriter)
            {
                stringWriter.GetStringBuilder().AppendSpan(value);
                return;
            }

            // Fall back to renting a buffer
            char[] buffer = ArrayPool<char>.Shared.Rent(value.Length);
            value.CopyTo(buffer);
            writer.Write(buffer, 0, value.Length);
            ArrayPool<char>.Shared.Return(buffer);
        }

        /// <summary>
        ///  Allows writing a <see cref="ReadOnlySpan{Char}"/> to a <see cref="TextWriter"/>.
        /// </summary>
        public void WriteLine(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
            {
                writer.WriteLine();
                return;
            }

            if (writer is StringWriter stringWriter)
            {
                stringWriter.GetStringBuilder().AppendSpan(value);
                writer.WriteLine();
                return;
            }

            char[] buffer = ArrayPool<char>.Shared.Rent(value.Length);
            value.CopyTo(buffer);
            writer.Write(buffer, 0, value.Length);
            ArrayPool<char>.Shared.Return(buffer);

            writer.WriteLine();
        }
    }
}
#endif
