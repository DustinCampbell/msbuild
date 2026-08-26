// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Describes one parsed property-function argument.
/// </summary>
internal readonly struct PropertyFunctionArgument(StringSegmentRange range, bool requiresExpansion)
{
    public StringSegmentRange Range { get; } = range;

    public bool RequiresExpansion { get; } = requiresExpansion;
}
