// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Utilities;

internal sealed partial class ResourceString
{
    private sealed class TextAndCode(string text, string code)
    {
        private readonly string _text = text;
        private readonly string _code = code;

        public void Deconstruct(out string text, out string code)
        {
            text = _text;
            code = _code;
        }

        /// <summary>
        ///  Attempts to extract the MSBuild code prefixed to <paramref name="text"/>. MSBuild codes match
        ///  <c>^\s*(?&lt;CODE&gt;MSB\d\d\d\d):\s*(?&lt;MESSAGE&gt;.*)$</c>.
        /// </summary>
        /// <param name="text">The resource text to parse.</param>
        /// <returns>
        ///  The stripped text and the extracted code, or <see langword="null"/> if <paramref name="text"/>
        ///  has no code prefix.
        /// </returns>
        public static TextAndCode? TryParse(string text)
        {
            int i = 0;

            SkipWhiteSpace(text, ref i);

            if (text.Length < i + 8 ||
                text[i] is not 'M' ||
                text[i + 1] is not 'S' ||
                text[i + 2] is not 'B' ||
                text[i + 3] is < '0' or > '9' ||
                text[i + 4] is < '0' or > '9' ||
                text[i + 5] is < '0' or > '9' ||
                text[i + 6] is < '0' or > '9' ||
                text[i + 7] is not ':')
            {
                return null;
            }

            string code = text.Substring(i, 7);
            i += 8;

            SkipWhiteSpace(text, ref i);

            if (i < text.Length)
            {
                text = text.Substring(i);
            }

            return new TextAndCode(text, code);

            static void SkipWhiteSpace(string text, ref int i)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
            }
        }
    }
}
