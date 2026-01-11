// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

[Flags]
internal enum TokenFlags : byte
{
    None = 0,
    Expandable = 1 << 0,
    IsBooleanTrue = 1 << 1,
    IsBooleanFalse = 1 << 2
}
