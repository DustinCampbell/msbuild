// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem<M>, IMetadataTable
        where M : class, IMetadatum
        where D : class, IItemDefinition<M>
    {
        private abstract class OperationBuilder
        {
            // WORKAROUND: Unnecessary boxed allocation: https://github.com/dotnet/corefx/issues/24563
            private static readonly ImmutableDictionary<string, LazyItemList> s_emptyIgnoreCase = ImmutableDictionary.Create<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

            public ProjectItemElement ItemElement { get; }
            public string ItemType { get; }
            public ItemSpec<P, I> ItemSpec { get; }
            public bool ConditionResult { get; }

            public ImmutableDictionary<string, LazyItemList>.Builder ReferencedItemLists { get; } = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
                ImmutableDictionary.CreateBuilder<string, LazyItemList>() :
                s_emptyIgnoreCase.ToBuilder();

            protected abstract ItemSpec<P, I> CreateItemSpec(ProjectItemElement itemElement, Expander<P, I> expander, string rootDirectory);

            protected OperationBuilder(ProjectItemElement itemElement, Expander<P, I> expander, string rootDirectory, bool conditionResult)
            {
                ItemElement = itemElement;
                ItemType = itemElement.ItemType;
                ItemSpec = CreateItemSpec(itemElement, expander, rootDirectory);
                ConditionResult = conditionResult;
            }
        }
    }
}
