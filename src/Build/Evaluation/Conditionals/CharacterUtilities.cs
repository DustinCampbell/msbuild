// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation
{
    internal static class CharacterUtilities
    {
        public static bool IsNumberStart(char candidate)
            => candidate is '+' or '-' or '.' || char.IsDigit(candidate);

        public static bool IsIdentifierStart(char candidate)
            => candidate == '_' || char.IsLetter(candidate);

        public static bool IsIdentifier(char candidate)
            => IsIdentifierStart(candidate) || char.IsDigit(candidate);

        public static bool IsHexDigit(char candidate)
            => char.IsDigit(candidate) || ((uint)((candidate | 0x20) - 'a') <= 'f' - 'a');
    }
}
