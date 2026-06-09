// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  The kind of relational comparison performed by a <see cref="RelationalOperatorNode"/>.
/// </summary>
internal enum RelationalOperationKind : byte
{
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
}
