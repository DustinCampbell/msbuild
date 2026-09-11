// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal interface IFunctionArgumentMaterializer
{
    object? Materialize(StringSegment source, int index);
}
