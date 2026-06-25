// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.Build.Text;

internal static class StringBuilderExtensions
{
    /// <summary>
    ///  Appends the contents of a <see cref="StringSegment"/> to a <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="segment">The segment whose contents should be appended.</param>
    /// <returns>
    ///  The <paramref name="builder"/> instance after the append operation.
    /// </returns>
    public static StringBuilder AppendSegment(this StringBuilder builder, StringSegment segment)
        => builder.Append(segment.Buffer, segment.Offset, segment.Length);
}
