// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class AvailableStaticMembers
{
    /// <summary>
    ///  Identifies either every static member on a type or one named static member.
    /// </summary>
    /// <param name="TypeName">The full type name.</param>
    /// <param name="MemberName">
    ///  The simple member name, or <see langword="null"/> when every static member is available.
    /// </param>
    private readonly record struct MemberKey(string TypeName, string? MemberName = null)
    {
        public bool Equals(MemberKey other)
            => StringComparer.OrdinalIgnoreCase.Equals(TypeName, other.TypeName)
            && StringComparer.OrdinalIgnoreCase.Equals(MemberName, other.MemberName);

        public override int GetHashCode()
        {
            int typeHashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(TypeName);
            int memberHashCode = MemberName is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(MemberName);
            return unchecked((typeHashCode * 397) ^ memberHashCode);
        }
    }
}
