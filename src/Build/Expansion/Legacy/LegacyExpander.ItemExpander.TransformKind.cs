// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Expansion.Legacy;

internal partial class LegacyExpander<P, I>
{
    private static partial class ItemExpander
    {
        private enum TransformKind
        {
            ItemSpecModifierFunction,
            Count,
            Exists,
            Combine,
            GetPathsOfAllDirectoriesAbove,
            DirectoryName,
            Metadata,
            DistinctWithCase,
            Distinct,
            Reverse,
            ExpandQuotedExpressionFunction,
            ExecuteStringFunction,
            ClearMetadata,
            HasMetadata,
            WithMetadataValue,
            WithoutMetadataValue,
            AnyHaveMetadataValue,
        }
    }
}
