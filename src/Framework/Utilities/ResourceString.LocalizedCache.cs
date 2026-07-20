// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.Build.Framework.Utilities;

internal sealed partial class ResourceString
{
    private sealed class LocalizedCache(CultureInfo culture, string text, object parsedText)
    {
        /// <summary>
        ///  Gets the culture the resource was resolved against.
        /// </summary>
        public CultureInfo Culture => culture;

        /// <summary>
        ///  Gets the resolved resource text.
        /// </summary>
        public string Text => text;

        /// <summary>
        ///  Gets the parsed text/code. Encodes two states to avoid allocating in the common "no code" case:
        ///  a <see cref="string"/> means there is no code (the string is <c>TextWithoutCode</c>, reference-equal
        ///  to <see cref="Text"/>) and the code is <see langword="null"/>; a <see cref="TextAndCode"/> holds the
        ///  stripped text and code otherwise.
        /// </summary>
        public object ParsedText => parsedText;
    }
}
