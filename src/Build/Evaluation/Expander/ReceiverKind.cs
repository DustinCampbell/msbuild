// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Identifies how a parsed function obtains its receiver.
/// </summary>
internal enum ReceiverKind
{
    /// <summary>
    ///  The receiver is the type named by a static property-function expression.
    /// </summary>
    Static,

    /// <summary>
    ///  The receiver is the value of an MSBuild property named by the expression.
    /// </summary>
    MSBuildProperty,

    /// <summary>
    ///  The receiver is the result of the preceding function in the expression.
    /// </summary>
    Chained,
}
