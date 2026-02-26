// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private interface IItemOperation
    {
        void Apply(OrderedItemDataCollection.Builder listBuilder, GlobSet globsToIgnore);
    }
}
