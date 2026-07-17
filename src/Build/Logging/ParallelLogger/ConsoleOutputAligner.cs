// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Align output to multiple lines so no logged test is lost due to limited <see cref="Console.BufferWidth"/>.
    /// During alignment optional prefix/indent is applied.
    /// </summary>
    /// <remarks>
    /// This class is not thread safe.
    /// </remarks>
    internal class ConsoleOutputAligner
    {
        internal const int ConsoleTabWidth = 8;

        private readonly int _bufferWidth;
        private readonly bool _alignMessages;
        private readonly IStringBuilderProvider _stringBuilderProvider;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="bufferWidth">Console buffer width. -1 if unknown/unlimited</param>
        /// <param name="alignMessages">Whether messages are aligned/wrapped into console buffer width</param>
        /// <param name="stringBuilderProvider"></param>
        public ConsoleOutputAligner(int bufferWidth, bool alignMessages, IStringBuilderProvider stringBuilderProvider)
        {
            _bufferWidth = bufferWidth;
            _alignMessages = alignMessages;
            _stringBuilderProvider = stringBuilderProvider;
        }

        /// <summary>
        /// Based on bufferWidth split message into multiple lines and indent if needed.
        /// TAB character are interpreted by standard Console logic.
        /// </summary>
        /// <param name="message">Input message. May contains tabs and new lines. Both \r\n and \n is supported but replaced into current environment new line.</param>
        /// <param name="prefixAlreadyWritten">true if message already contains prefix (message context, timestamp, etc...).</param>
        /// <param name="prefixWidth">Width of the prefix. Every line in result string will be indented by this number of spaces except 1st line with already written prefix.</param>
        /// <returns>Aligned message ready to be written to Console</returns>
        /// <remarks>
        /// For optimization purposes this method uses single <see cref="StringBuilder"/> instance. This makes this method non thread safe.
        /// Calling side is expected this usage is non-concurrent. This shall not be an issue as it is expected that writing into Console shall be serialized anyway.
        /// </remarks>
        public string AlignConsoleOutput(string message, bool prefixAlreadyWritten, int prefixWidth)
        {
            StringBuilder sb = _stringBuilderProvider.Acquire(EstimateCapacity(message.Length, prefixAlreadyWritten, prefixWidth));
            AppendAlignedOutput(sb, new StringCharSource(message), prefixAlreadyWritten, prefixWidth);
            return _stringBuilderProvider.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Same alignment as <see cref="AlignConsoleOutput(string, bool, int)"/> but streams the result
        /// directly to <paramref name="writer"/> in chunks, avoiding the allocation of the fully-formatted
        /// output string.
        /// </summary>
        /// <remarks>
        /// For optimization purposes this method uses a single <see cref="StringBuilder"/> instance, which
        /// makes it non thread safe (see <see cref="AlignConsoleOutput(string, bool, int)"/>).
        /// </remarks>
        public void AlignConsoleOutput(TextWriter writer, string message, bool prefixAlreadyWritten, int prefixWidth)
        {
            StringBuilder sb = _stringBuilderProvider.Acquire(EstimateCapacity(message.Length, prefixAlreadyWritten, prefixWidth));
            try
            {
                AppendAlignedOutput(sb, new StringCharSource(message), prefixAlreadyWritten, prefixWidth);
                WriteStringBuilder(writer, sb);
            }
            finally
            {
                _stringBuilderProvider.Release(sb);
            }
        }

        /// <summary>
        /// Same alignment as <see cref="AlignConsoleOutput(TextWriter, string, bool, int)"/> but reads the
        /// message straight from a <see cref="StringBuilder"/>, so a caller that has already assembled the
        /// message in a pooled builder never has to materialize it as a string.
        /// </summary>
        /// <remarks>
        /// The destination builder is taken from the global <see cref="Microsoft.Build.Framework.StringBuilderCache"/> rather than the
        /// injected provider, because <paramref name="message"/> typically already occupies the provider's
        /// single shared builder; this keeps the source and destination builders distinct.
        /// </remarks>
        public void AlignConsoleOutput(TextWriter writer, StringBuilder message, bool prefixAlreadyWritten, int prefixWidth)
        {
            // Copy the message into a pooled char buffer once, then align over the array: random access,
            // IndexOf and bulk Append are all fast on a char[] (and vectorized on modern runtimes), which a
            // StringBuilder's chunked indexer is not. The rented buffer keeps this allocation-free.
            int length = message.Length;
            char[] chars = ArrayPool<char>.Shared.Rent(Math.Max(length, 1));
            try
            {
                message.CopyTo(0, chars, 0, length);

                StringBuilder sb = StringBuilderCache.Acquire(EstimateCapacity(length, prefixAlreadyWritten, prefixWidth));
                try
                {
                    AppendAlignedOutput(sb, new CharArrayCharSource(chars, length), prefixAlreadyWritten, prefixWidth);
                    WriteStringBuilder(writer, sb);
                }
                finally
                {
                    StringBuilderCache.Release(sb);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(chars);
            }
        }

        // Empiric value of average line length in console output. Used to estimate number of lines in message for StringBuilder capacity.
        // Wrongly estimated capacity is not a problem as StringBuilder will grow as needed. It is just optimization to avoid multiple reallocations.
        private const int AverageLineLength = 40;

        /// <summary>
        /// Estimates the capacity needed for the aligned output, to avoid reallocating while building it.
        /// </summary>
        private static int EstimateCapacity(int messageLength, bool prefixAlreadyWritten, int prefixWidth)
            => messageLength + ((prefixAlreadyWritten ? 0 : prefixWidth) + Environment.NewLine.Length) * (messageLength / AverageLineLength + 1);

        /// <summary>
        /// Based on bufferWidth split message into multiple lines and indent if needed, appending the
        /// result into <paramref name="sb"/>. TAB characters are interpreted by standard Console logic.
        /// The message is read through <typeparamref name="TSource"/> so the same logic serves both a
        /// <see cref="string"/> and a <see cref="StringBuilder"/> source without boxing or a virtual call.
        /// </summary>
        private void AppendAlignedOutput<TSource>(StringBuilder sb, in TSource source, bool prefixAlreadyWritten, int prefixWidth)
            where TSource : struct, ICharSource
        {
            int length = source.Length;
            int i = 0;
            int j = source.IndexOfCrLf(0);

            // The string contains new lines, treat each new line as a different string to format and send to the console
            while (j >= 0)
            {
                AlignAndIndentLineOfMessage(sb, prefixAlreadyWritten, prefixWidth, in source, i, j - i);
                i = j + (source[j] == '\r' && (j + 1) < length && source[j + 1] == '\n' ? 2 : 1);
                j = source.IndexOfCrLf(i);
            }

            // Process rest of message
            AlignAndIndentLineOfMessage(sb, prefixAlreadyWritten, prefixWidth, in source, i, length - i);
        }

        /// <summary>
        /// Writes the contents of <paramref name="sb"/> to <paramref name="writer"/> without allocating an
        /// intermediate string. On modern runtimes the builder's chunks are written as spans; on .NET
        /// Framework (where the span/StringBuilder overloads are unavailable) the contents are copied into a
        /// pooled <see cref="char"/> buffer in blocks.
        /// </summary>
        internal static void WriteStringBuilder(TextWriter writer, StringBuilder sb)
        {
            int length = sb.Length;
            if (length == 0)
            {
                return;
            }

#if NET
            foreach (ReadOnlyMemory<char> chunk in sb.GetChunks())
            {
                writer.Write(chunk.Span);
            }
#else
            const int maxBlock = 4096;
            char[] buffer = ArrayPool<char>.Shared.Rent(Math.Min(length, maxBlock));
            try
            {
                int index = 0;
                while (index < length)
                {
                    int count = Math.Min(buffer.Length, length - index);
                    sb.CopyTo(index, buffer, 0, count);
                    writer.Write(buffer, 0, count);
                    index += count;
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
#endif
        }

        /// <summary>
        /// Append aligned and indented message lines into running <see cref="StringBuilder"/>.
        /// </summary>
        private void AlignAndIndentLineOfMessage<TSource>(StringBuilder sb, bool prefixAlreadyWritten, int prefixWidth, in TSource source, int start, int count)
            where TSource : struct, ICharSource
        {
            int bufferWidthMinusNewLine = _bufferWidth - 1;

            bool bufferIsLargerThanPrefix = bufferWidthMinusNewLine > prefixWidth;
            if (_alignMessages && bufferIsLargerThanPrefix && count > 0)
            {
                // If the buffer is larger then the prefix information (timestamp and key) then reformat the messages

                // Beginning index of string to be written
                int index = 0;
                // Loop until all the string has been sent to the console
                while (index < count)
                {
                    // Position of virtual console cursor.
                    // By simulating cursor position adjustment for tab characters '\t' we can compute
                    //   exact numbers of characters from source string to fit into Console.BufferWidth.
                    int cursor = 0;

                    // Write prefix if needed
                    if ((!prefixAlreadyWritten || index > 0 || start > 0) && prefixWidth > 0)
                    {
                        sb.Append(' ', prefixWidth);
                    }
                    // We have to adjust cursor position whether the prefix has been already written or we wrote/indented it ourselves
                    cursor += prefixWidth;

                    // End index of string to be written (behind last character)
                    int endIndex = index;
                    while (cursor < bufferWidthMinusNewLine)
                    {
                        int remainingCharsToEndOfBuffer = Math.Min(bufferWidthMinusNewLine - cursor, count - endIndex);
                        int nextTab = source.IndexOf('\t', start + endIndex, remainingCharsToEndOfBuffer);
                        if (nextTab >= 0)
                        {
                            // Position before tab
                            cursor += nextTab - (start + endIndex);
                            // Move to next tab position
                            cursor += ConsoleTabWidth - cursor % ConsoleTabWidth;
                            // Move end index after the '\t' in preparation for following IndexOf '\t'
                            endIndex += nextTab - (start + endIndex) + 1;
                        }
                        else
                        {
                            endIndex += remainingCharsToEndOfBuffer;
                            break;
                        }
                    }

                    source.AppendTo(sb, start + index, endIndex - index);
                    sb.AppendLine();

                    index = endIndex;
                }
            }
            else
            {
                // If there is not enough room just print the message out and let the console do the formatting
                if (!prefixAlreadyWritten && prefixWidth > 0)
                {
                    sb.Append(' ', prefixWidth);
                }

                source.AppendTo(sb, start, count);
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Abstraction over a read-only character source (a <see cref="string"/> or a <see cref="StringBuilder"/>)
        /// so the alignment logic can be shared. Implemented by <see langword="readonly"/> structs and consumed
        /// through a generic <c>struct</c> constraint, so calls are devirtualized with no boxing.
        /// </summary>
        private interface ICharSource
        {
            int Length { get; }

            char this[int index] { get; }

            /// <summary>Returns the index of the first '\r' or '\n' at or after <paramref name="start"/>, or -1.</summary>
            int IndexOfCrLf(int start);

            /// <summary>Returns the index of <paramref name="value"/> within [start, start + count), or -1.</summary>
            int IndexOf(char value, int start, int count);

            /// <summary>Appends <paramref name="count"/> characters starting at <paramref name="start"/> to <paramref name="dest"/>.</summary>
            void AppendTo(StringBuilder dest, int start, int count);
        }

        private readonly struct StringCharSource : ICharSource
        {
            private readonly string _value;

            public StringCharSource(string value) => _value = value;

            public int Length => _value.Length;

            public char this[int index] => _value[index];

            public int IndexOfCrLf(int start) => _value.IndexOfAny(MSBuildConstants.CrLf, start);

            public int IndexOf(char value, int start, int count) => _value.IndexOf(value, start, count);

            public void AppendTo(StringBuilder dest, int start, int count) => dest.Append(_value, start, count);
        }

        private readonly struct CharArrayCharSource : ICharSource
        {
            private readonly char[] _value;
            private readonly int _length;

            public CharArrayCharSource(char[] value, int length)
            {
                _value = value;
                _length = length;
            }

            public int Length => _length;

            public char this[int index] => _value[index];

            public int IndexOfCrLf(int start)
            {
                char[] value = _value;
                int length = _length;
                for (int i = start; i < length; i++)
                {
                    char c = value[i];
                    if (c == '\r' || c == '\n')
                    {
                        return i;
                    }
                }

                return -1;
            }

            public int IndexOf(char value, int start, int count) => Array.IndexOf(_value, value, start, count);

            public void AppendTo(StringBuilder dest, int start, int count) => dest.Append(_value, start, count);
        }
    }
}
