// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class AvailableStaticMembers
{
    /// <summary>
    ///  Identifies either every static member on a type or one named static member.
    /// </summary>
    /// <param name="TypeName">The full type name.</param>
    /// <param name="MemberName">
    ///  The simple member name, or a null segment when every static member is available.
    /// </param>
    private readonly record struct MemberKey(StringSegment TypeName, StringSegment MemberName = default)
    {
        public bool Equals(MemberKey other)
            => TypeName.Equals(other.TypeName, StringComparison.OrdinalIgnoreCase)
            && MemberName.Equals(other.MemberName, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode()
        {
            int typeHashCode = TypeName.GetHashCode(StringComparison.OrdinalIgnoreCase);
            int memberHashCode = MemberName.HasValue
                ? MemberName.GetHashCode(StringComparison.OrdinalIgnoreCase)
                : 0;
            return unchecked((typeHashCode * 397) ^ memberHashCode);
        }
    }
}
