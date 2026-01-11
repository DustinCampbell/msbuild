// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal abstract class BinaryOperatorNode(GenericExpressionNode left, GenericExpressionNode right) : OperatorExpressionNode
{
    public GenericExpressionNode Left { get; } = left;
    public GenericExpressionNode Right { get; } = right;

    /// <inheritdoc cref="GenericExpressionNode"/>
    internal override bool IsUnexpandedValueEmpty()
        => Left.IsUnexpandedValueEmpty() && Right.IsUnexpandedValueEmpty();

    /// <summary>
    /// If any expression nodes cache any state for the duration of evaluation,
    /// now's the time to clean it up
    /// </summary>
    internal override void ResetState()
    {
        Left.ResetState();
        Right.ResetState();
    }

    #region REMOVE_COMPAT_WARNING

    internal override bool DetectAnd()
    {
        // Read the state of the current node
        bool detectedAnd = PossibleAndCollision;

        // Reset the flags on the current node
        PossibleAndCollision = false;

        // Process the node children
        bool detectAndRChild = Right.DetectAnd();
        bool detectAndLChild = Left.DetectAnd();

        return detectedAnd || detectAndRChild || detectAndLChild;
    }

    internal override bool DetectOr()
    {
        // Read the state of the current node
        bool detectedOr = PossibleOrCollision;

        // Reset the flags on the current node
        PossibleOrCollision = false;

        // Process the node children
        bool detectOrRChild = Right.DetectOr();
        bool detectOrLChild = Left.DetectOr();

        return detectedOr || detectOrRChild || detectOrLChild;
    }

    #endregion
}
