// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
namespace Microsoft.Build;

internal static class CharExtensions
{
    extension(char)
    {
        /// <summary>
        ///  Indicates whether a character is categorized as an ASCII letter.
        /// </summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>
        ///  <see langword="true"/> if <paramref name="c"/> is an ASCII letter; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  This determines whether the character is in the range 'A' through 'Z', inclusive,
        ///  or 'a' through 'z', inclusive.
        /// </remarks>
        public static bool IsAsciiLetter(char c)
            => (uint)((c | 0x20) - 'a') <= 'z' - 'a';
    }
}
#endif
