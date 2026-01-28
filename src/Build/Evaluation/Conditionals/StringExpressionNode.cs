// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Node representing a string
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal abstract partial class StringExpressionNode : OperandExpressionNode
{
    public static StringExpressionNode Create(string value, bool expandable = false)
        => !string.IsNullOrEmpty(value)
            ? new Default(value, expandable)
            : Empty.Instance;

    public abstract string Value { get; }

    internal override string DebuggerDisplay => $"\"{Value}\"";
}
