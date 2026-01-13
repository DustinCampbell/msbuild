// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation.Parsing;

/// <summary>
/// Base class for all expression AST nodes.
/// </summary>
internal abstract class ExpressionNode
{
    protected ExpressionNode(ReadOnlyMemory<char> text, int position)
    {
        Text = text;
        Position = position;
    }

    /// <summary>
    /// The full text of this node from the source expression.
    /// </summary>
    public ReadOnlyMemory<char> Text { get; }

    /// <summary>
    /// The starting position of this node in the original expression.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// The length of this node's text.
    /// </summary>
    public int Length => Text.Length;

    /// <summary>
    /// The ending position of this node in the original expression.
    /// </summary>
    public int End => Position + Length;
}

/// <summary>
/// Represents a literal text node (text that doesn't contain $, %, or @).
/// </summary>
internal sealed class LiteralNode : ExpressionNode
{
    public LiteralNode(ReadOnlyMemory<char> text, int position)
        : base(text, position)
    {
    }
}

/// <summary>
/// Represents a property reference: $(PropertyBody)
/// </summary>
internal sealed class PropertyNode : ExpressionNode
{
    public PropertyNode(PropertyBodyNode body, ReadOnlyMemory<char> text, int position)
        : base(text, position)
    {
        Body = body;
    }

    public PropertyBodyNode Body { get; }
}

/// <summary>
/// Base class for property body nodes (the content inside $(...)).
/// </summary>
internal abstract class PropertyBodyNode : ExpressionNode
{
    protected PropertyBodyNode(ReadOnlyMemory<char> text, int position)
        : base(text, position)
    {
    }
}

/// <summary>
/// Represents a simple property reference with optional member access chain.
/// Example: PropertyName.Method().Property[0]
/// </summary>
internal sealed class SimplePropertyNode : PropertyBodyNode
{
    public SimplePropertyNode(
        ReadOnlyMemory<char> name,
        ImmutableArray<MemberAccessNode> memberAccesses,
        ReadOnlyMemory<char> text,
        int position)
        : base(text, position)
    {
        Name = name;
        MemberAccesses = memberAccesses;
    }

    public ReadOnlyMemory<char> Name { get; }
    public ImmutableArray<MemberAccessNode> MemberAccesses { get; }
}

/// <summary>
/// Represents a static function call: [TypeName]::MethodName(args)
/// Example: $([System.String]::Concat('a', 'b'))
/// </summary>
internal sealed class StaticFunctionNode : PropertyBodyNode
{
    public StaticFunctionNode(
        ReadOnlyMemory<char> typeName,
        ReadOnlyMemory<char> methodName,
        ImmutableArray<ExpressionNode> arguments,
        ImmutableArray<MemberAccessNode> memberAccesses,
        ReadOnlyMemory<char> text,
        int position)
        : base(text, position)
    {
        TypeName = typeName;
        MethodName = methodName;
        Arguments = arguments;
        MemberAccesses = memberAccesses;
    }

    public ReadOnlyMemory<char> TypeName { get; }
    public ReadOnlyMemory<char> MethodName { get; }
    public ImmutableArray<ExpressionNode> Arguments { get; }
    public ImmutableArray<MemberAccessNode> MemberAccesses { get; }
}

/// <summary>
/// Represents a registry lookup: Registry:HKEY_...\Path@ValueName
/// Example: $(Registry:HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework@InstallRoot)
/// </summary>
internal sealed class RegistryNode : PropertyBodyNode
{
    public RegistryNode(
        ReadOnlyMemory<char> keyPath,
        ReadOnlyMemory<char> valueName,
        ReadOnlyMemory<char> text,
        int position)
        : base(text, position)
    {
        KeyPath = keyPath;
        ValueName = valueName;
    }

    public ReadOnlyMemory<char> KeyPath { get; }
    public ReadOnlyMemory<char> ValueName { get; }
}

/// <summary>
/// Base class for member access nodes (.Method(), .Property, [indexer]).
/// </summary>
internal abstract class MemberAccessNode : ExpressionNode
{
    protected MemberAccessNode(ReadOnlyMemory<char> text, int position)
        : base(text, position)
    {
    }
}

/// <summary>
/// Represents a method call or property access: .Name() or .Name
/// Example: .ToUpper() or .Length
/// </summary>
internal sealed class MethodCallNode : MemberAccessNode
{
    public MethodCallNode(
        ReadOnlyMemory<char> name,
        ImmutableArray<ExpressionNode> arguments,
        ReadOnlyMemory<char> text,
        int position)
        : base(text, position)
    {
        Name = name;
        Arguments = arguments;
    }

    public ReadOnlyMemory<char> Name { get; }
    public ImmutableArray<ExpressionNode> Arguments { get; }
}

/// <summary>
/// Represents an indexer access: [expression]
/// Example: [0] or [$(Index)]
/// </summary>
internal sealed class IndexerNode : MemberAccessNode
{
    public IndexerNode(ExpressionNode index, ReadOnlyMemory<char> text, int position)
        : base(text, position)
    {
        Index = index;
    }

    public ExpressionNode Index { get; }
}

/// <summary>
/// Represents a metadata reference: %(Name) or %(ItemType.Name)
/// Example: %(Filename) or %(Compile.DependsOn)
/// </summary>
internal sealed class MetadataNode : ExpressionNode
{
    public MetadataNode(
        ReadOnlyMemory<char> itemType,
        ReadOnlyMemory<char> name,
        ReadOnlyMemory<char> text,
        int position)
        : base(text, position)
    {
        ItemType = itemType;
        Name = name;
    }

    public ReadOnlyMemory<char> ItemType { get; }
    public ReadOnlyMemory<char> Name { get; }
}

/// <summary>
/// Represents an item list reference: @(ItemType) with optional transforms and separator.
/// Example: @(Compile->'%(Filename)', ';')
/// </summary>
internal sealed class ItemNode : ExpressionNode
{
    public ItemNode(
        ReadOnlyMemory<char> itemType,
        ImmutableArray<TransformNode> transforms,
        ExpressionNode? separator,
        ReadOnlyMemory<char> text,
        int position)
        : base(text, position)
    {
        ItemType = itemType;
        Transforms = transforms;
        Separator = separator;
    }

    public ReadOnlyMemory<char> ItemType { get; }
    public ImmutableArray<TransformNode> Transforms { get; }
    public ExpressionNode? Separator { get; }
}

/// <summary>
/// Represents an item transform or function.
/// Example: ->'%(Filename)' or ->Distinct()
/// </summary>
internal sealed class TransformNode : ExpressionNode
{
    public TransformNode(ExpressionNode expression, ReadOnlyMemory<char> text, int position)
        : base(text, position)
    {
        Expression = expression;
    }

    public ExpressionNode Expression { get; }
}
