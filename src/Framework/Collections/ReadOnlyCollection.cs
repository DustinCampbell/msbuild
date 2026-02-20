// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal static class ReadOnlyCollection
{
    public static ReadOnlyCollection<T> Create<T>(IEnumerable<T>? backing)
        => ReadOnlyCollection<T>.Create(backing);
}
