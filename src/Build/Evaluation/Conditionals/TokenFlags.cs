// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation
{
    [Flags]
    internal enum TokenFlags
    {
        None = 0,

        IsExpandable = 1 << 0,
    }
}
