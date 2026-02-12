// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security;

namespace Microsoft.Build.Shared;

/// <summary>
/// Utility methods for classifying and handling exceptions.
/// </summary>
internal static class FrameworkExceptionHandling
{
    /// <summary>
    ///  Determine whether the exception is file-IO related.
    /// </summary>
    /// <param name="exception">
    ///  The exception to test.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="exception"/> is IO related; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   These all derive from IOException:
    ///  </para>
    ///
    ///  <list type="bullet">
    ///   <item>DirectoryNotFoundException</item>
    ///   <item>DriveNotFoundException</item>
    ///   <item>EndOfStreamException</item>
    ///   <item>FileLoadException</item>
    ///   <item>FileNotFoundException</item>
    ///   <item>PathTooLongException</item>
    ///   <item>PipeException</item>
    ///  </list>
    /// </remarks>
    public static bool IsIoRelatedException(Exception exception)
        => exception is UnauthorizedAccessException
                     or NotSupportedException
                     or (ArgumentException and not ArgumentNullException)
                     or SecurityException
                     or IOException;
}
