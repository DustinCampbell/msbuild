// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.Build;

internal static class StringBuilderExtensions
{
    extension(StringBuilder builder)
    {
        /// <summary>
        ///  Appends a <see cref="ReadOnlySpan{T}"/> of characters to the end of the <see cref="StringBuilder"/>.
        /// </summary>
        public unsafe StringBuilder AppendSpan(ReadOnlySpan<char> value)
        {
            if (!value.IsEmpty)
            {
                fixed (char* pValue = value)
                {
                    // Use the StringBuilder's Append method that takes a char pointer and length for better performance.
                    builder.Append(pValue, value.Length);
                }
            }

            return builder;
        }

        /// <summary>
        ///  Appends a <see cref="Memory{T}"/> of characters to the end of the <see cref="StringBuilder"/>.
        /// </summary>
        public StringBuilder AppendSpan(Memory<char> value) => builder.AppendSpan(value.Span);
    }
}
