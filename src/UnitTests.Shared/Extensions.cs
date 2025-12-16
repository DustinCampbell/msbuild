// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests.Shared;

public static class Extensions
{
    public static T ChooseRandomItem<T>(this IReadOnlyList<T> list, Random random)
        => list[random.Next(list.Count)];
}
