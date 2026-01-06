// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
/// Represents a property reference: $(Configuration) or $(Property.ToUpper()).
/// </summary>
internal sealed class PropertyReferenceNode(ExpressionNode expression, SourceSpan source) : ExpressionNode(source)
{
    /// <summary>
    /// The inner expression between $( and ).
    /// Could be a simple identifier or a complex expression with method calls.
    /// </summary>
    public ExpressionNode Expression => expression;
}

/// <summary>
/// Represents an item vector reference: @(Compile) or @(Compile->'%(FullPath)', ';').
/// </summary>
internal sealed class ItemVectorNode(
    Token itemType,
    ImmutableArray<TransformNode> transforms,
    ExpressionNode? separator,
    SourceSpan source) : ExpressionNode(source)
{
    /// <summary>
    /// The item type name token.
    /// </summary>
    public Token ItemType => itemType;

    /// <summary>
    /// Transform expressions (the part after ->).
    /// </summary>
    public ImmutableArray<TransformNode> Transforms => transforms;

    /// <summary>
    /// Separator expression (the part after the comma).
    /// </summary>
    public ExpressionNode? Separator => separator;
}

/// <summary>
/// Represents a transform expression: ->'%(FullPath)' or ->ToUpper().
/// </summary>
internal sealed class TransformNode(ExpressionNode expression, SourceSpan source) : ExpressionNode(source)
{
    /// <summary>
    /// The transform expression (what comes after the arrow).
    /// </summary>
    public ExpressionNode Expression => expression;
}

/// <summary>
/// Represents a metadata reference: %(FullPath) or %(Compile.Object).
/// </summary>
internal sealed class MetadataReferenceNode(Token itemType, Token metadataName, SourceSpan source) : ExpressionNode(source)
{
    /// <summary>
    /// Optional item type qualifier token (default for unqualified metadata).
    /// </summary>
    public Token ItemType => itemType;

    /// <summary>
    /// The metadata name token.
    /// </summary>
    public Token MetadataName => metadataName;

    public MetadataReferenceNode(Token metadataName, SourceSpan source)
        : this(itemType: default, metadataName, source)
    {
    }
}
