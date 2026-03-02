// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security;

namespace Microsoft.Build.Framework;

internal static class FrameworkExceptionHandling
{
    /// <summary>
    /// Determine whether the exception is file-IO related.
    /// </summary>
    /// <param name="e">The exception to check.</param>
    /// <returns>True if exception is IO related.</returns>
    internal static bool IsIoRelatedException(Exception e)
    {
        // These all derive from IOException
        //     DirectoryNotFoundException
        //     DriveNotFoundException
        //     EndOfStreamException
        //     FileLoadException
        //     FileNotFoundException
        //     PathTooLongException
        //     PipeException
        return e is UnauthorizedAccessException
               || e is NotSupportedException
               || (e is ArgumentException && !(e is ArgumentNullException))
               || e is SecurityException
               || e is IOException;
    }
}
