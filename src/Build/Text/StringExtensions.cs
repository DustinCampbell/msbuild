// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System;

internal static partial class StringExtensions
{
    extension(string)
    {
        /// <summary>
        ///  Allocates a string of the specified length filled with null characters.
        /// </summary>
        internal static string FastAllocateString(int length) =>
            // This calls FastAllocateString in the runtime, with extra checks.
            new string('\0', length);
    }
}
