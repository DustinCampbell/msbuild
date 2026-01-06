// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
/// Represents a binary operator expression: left op right.
/// </summary>
internal sealed class BinaryOperatorNode(
    ExpressionNode left,
    Token @operator,
    ExpressionNode right,
    SourceSpan source) : ExpressionNode(source)
{
    /// <summary>
    /// The operator token (==, !=, &lt;, &lt;=, &gt;, &gt;=, and, or).
    /// </summary>
    public Token Operator => @operator;

    /// <summary>
    /// The left operand.
    /// </summary>
    public ExpressionNode Left => left;

    /// <summary>
    /// The right operand.
    /// </summary>
    public ExpressionNode Right => right;
}

/// <summary>
/// Represents a unary operator expression (currently only negation: !operand).
/// </summary>
internal sealed class UnaryOperatorNode(
    Token @operator,
    ExpressionNode operand,
    SourceSpan source) : ExpressionNode(source)
{
    /// <summary>
    /// The operator token (!).
    /// </summary>
    public Token Operator => @operator;

    /// <summary>
    /// The operand expression.
    /// </summary>
    public ExpressionNode Operand => operand;
}
