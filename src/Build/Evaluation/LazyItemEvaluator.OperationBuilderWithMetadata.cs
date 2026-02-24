// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem<M>, IMetadataTable
        where M : class, IMetadatum
        where D : class, IItemDefinition<M>
    {
        private abstract class OperationBuilderWithMetadata : OperationBuilder
        {
            public readonly ImmutableArray<ProjectMetadataElement>.Builder Metadata = ImmutableArray.CreateBuilder<ProjectMetadataElement>();

            protected OperationBuilderWithMetadata(ProjectItemElement itemElement, Expander<P, I> expander, string rootDirectory, bool conditionResult)
                : base(itemElement, expander, rootDirectory, conditionResult)
            {
            }
        }
    }
}
