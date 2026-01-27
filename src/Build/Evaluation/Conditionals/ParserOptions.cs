// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

[Flags]
internal enum ParserOptions
{
    None = 0x0,
    AllowProperties = 0x1,
    AllowItemLists = 0x2,
    AllowPropertiesAndItemLists = AllowProperties | AllowItemLists,
    AllowBuiltInMetadata = 0x4,
    AllowCustomMetadata = 0x8,
    AllowUnknownFunctions = 0x10,
    AllowItemMetadata = AllowBuiltInMetadata | AllowCustomMetadata,
    AllowPropertiesAndItemMetadata = AllowProperties | AllowItemMetadata,
    AllowPropertiesAndCustomMetadata = AllowProperties | AllowCustomMetadata,
    AllowAll = AllowProperties | AllowItemLists | AllowItemMetadata
};
