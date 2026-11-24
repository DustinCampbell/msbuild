// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Specifies which reference forms are permitted in an MSBuild condition expression.
/// </summary>
/// <remarks>
///  <see cref="Parser"/> and <see cref="Scanner"/> require <see cref="AllowProperties"/>.
///  Restrictions also apply to reference forms recognized while scanning quoted strings.
/// </remarks>
[Flags]
internal enum ParserOptions
{
    /// <summary>
    ///  No reference forms are enabled. This value alone is not supported for condition parsing.
    /// </summary>
    None = 0x0,

    /// <summary>
    ///  Allows property references. Required for all condition parsing.
    /// </summary>
    AllowProperties = 0x1,

    /// <summary>
    ///  Allows item-list expressions.
    /// </summary>
    AllowItemLists = 0x2,

    /// <summary>
    ///  Allows property references and item-list expressions.
    /// </summary>
    AllowPropertiesAndItemLists = AllowProperties | AllowItemLists,

    /// <summary>
    ///  Allows built-in item metadata references, such as <c>%(Filename)</c>.
    /// </summary>
    AllowBuiltInMetadata = 0x4,

    /// <summary>
    ///  Allows custom item metadata references.
    /// </summary>
    AllowCustomMetadata = 0x8,

    /// <summary>
    ///  Allows both built-in and custom item metadata references.
    /// </summary>
    AllowItemMetadata = AllowBuiltInMetadata | AllowCustomMetadata,

    /// <summary>
    ///  Allows property references and both kinds of item metadata references.
    /// </summary>
    AllowPropertiesAndItemMetadata = AllowProperties | AllowItemMetadata,

    /// <summary>
    ///  Allows property references and custom item metadata references, but not built-in metadata.
    /// </summary>
    AllowPropertiesAndCustomMetadata = AllowProperties | AllowCustomMetadata,

    /// <summary>
    ///  Allows property references, item-list expressions, and both kinds of item metadata references.
    /// </summary>
    AllowAll = AllowProperties | AllowItemLists | AllowItemMetadata
};
