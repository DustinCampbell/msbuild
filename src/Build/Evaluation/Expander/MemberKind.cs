// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Identifies the syntax used to access a member.
/// </summary>
internal enum MemberKind
{
    /// <summary>
    ///  The member is invoked as a method.
    /// </summary>
    Method,

    /// <summary>
    ///  The member is read as a property or field.
    /// </summary>
    PropertyOrField,

    /// <summary>
    ///  The receiver is indexed.
    /// </summary>
    Indexer,
}
