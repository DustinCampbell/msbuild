// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NETFRAMEWORK
using System.IO;
using System.Text;
#endif

#nullable disable

namespace Microsoft.Build.Shared
{
    internal static class StringExtensions
    {
#if NETFRAMEWORK
        /// <summary>
        /// Trivial implementation of CommonPrefixLength on spans of characters.
        /// </summary>
        public static int CommonPrefixLength(this ReadOnlySpan<char> span, ReadOnlySpan<char> other)
        {
            int commonPrefixLength = 0;
            int length = Math.Min(span.Length, other.Length);

            while (commonPrefixLength < length && span[commonPrefixLength] == other[commonPrefixLength])
            {
                commonPrefixLength++;
            }
            return commonPrefixLength;
        }

        /// <summary>
        /// Adds the missing span-taking overload to .NET Framework version of <see cref="StringBuilder"/>.
        /// </summary>
        public static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
        {
            return sb.Append(value.ToString());
        }

        /// <summary>
        /// Adds the missing span-taking overload to .NET Framework version of <see cref="TextWriter"/>.
        /// </summary>
        public static void Write(this TextWriter writer, ReadOnlySpan<char> buffer)
        {
            writer.Write(buffer.ToString());
        }

        /// <summary>
        /// Adds the missing span-taking overload to .NET Framework version of <see cref="TextWriter"/>.
        /// </summary>
        public static void WriteLine(this TextWriter writer, ReadOnlySpan<char> buffer)
        {
            writer.WriteLine(buffer.ToString());
        }
#endif

        /// <summary>
        /// Converts a string to a bool.  We consider "true/false", "on/off", and
        /// "yes/no" to be valid boolean representations in the XML. The '!' prefix for negation is allowed as well.
        /// Unrecognized values lead to exception
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when given argument is unrecognized MSBuild boolean string.</exception>
        public static bool IsMSBuildTrueString(this string msbuildString) =>
            ConversionUtilities.ConvertStringToBool(msbuildString, nullOrWhitespaceIsFalse: true);

        /// <summary>
        /// Converts a string to a bool.  We consider "true/false", "on/off", and
        /// "yes/no" to be valid boolean representations in the XML. The '!' prefix for negation is allowed as well.
        /// Unrecognized values lead to exception
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when given argument is unrecognized MSBuild boolean string.</exception>
        public static bool IsMSBuildFalseString(this string msbuildString) => !IsMSBuildTrueString(msbuildString);
    }
}
