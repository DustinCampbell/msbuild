// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
/// Base class for expressions that can be used as receivers.
/// </summary>
internal abstract class ReceiverExpressionNode(SourceSpan source) : ExpressionNode(source)
{
}

/// <summary>
/// Represents a simple identifier: Configuration, ToUpper, Contains.
/// Used for property names, function names, variable names, etc.
/// </summary>
internal sealed class IdentifierNode(Token name) : ReceiverExpressionNode(name.Source)
{
    /// <summary>
    /// The identifier token.
    /// </summary>
    public Token Name => name;
}

/// <summary>
/// Represents a type name: String, System.String, System.Collections.Generic.List.
/// </summary>
internal sealed class TypeNameNode : ReceiverExpressionNode
{
    private readonly SourceSpan _namespace;
    private readonly Token _name;

    /// <summary>
    /// Creates a simple type name like "String".
    /// </summary>
    public TypeNameNode(Token name)
        : base(name.Source)
    {
        _name = name;
    }

    /// <summary>
    /// Creates a qualified type name like "System.String".
    /// </summary>
    public TypeNameNode(SourceSpan @namespace, Token name, SourceSpan source)
        : base(source)
    {
        _namespace = @namespace;
        _name = name;
    }

    /// <summary>
    /// The type name (last part). "String" for System.String.
    /// </summary>
    public Token Name => _name;

    /// <summary>
    /// The namespace part. "System.Collections.Generic" for System.Collections.Generic.List.
    /// Empty for unqualified names like "String".
    /// </summary>
    public SourceSpan Namespace => _namespace;

    /// <summary>
    /// True if the type name is qualified (has a namespace).
    /// </summary>
    public bool IsQualified => _namespace.Length > 0;
}

/// <summary>
/// Represents member access for property function calls: Configuration.ToUpper, Obj.Property.
/// </summary>
internal sealed class MemberAccessNode(
    ReceiverExpressionNode target,
    Token memberName,
    SourceSpan source) : ReceiverExpressionNode(source)
{
    /// <summary>
    /// The target expression (can be nested for multi-level access).
    /// </summary>
    public ReceiverExpressionNode Target => target;

    /// <summary>
    /// The member name token.
    /// </summary>
    public Token MemberName => memberName;
}

/// <summary>
/// Represents static member access: [TypeName]::MethodName.
/// </summary>
internal sealed class StaticMemberAccessNode(
    TypeNameNode typeName,
    Token memberName,
    SourceSpan source) : ReceiverExpressionNode(source)
{
    /// <summary>
    /// The type name (can be simple like "String" or qualified like "System.String").
    /// </summary>
    public TypeNameNode TypeName => typeName;

    /// <summary>
    /// The member name token.
    /// </summary>
    public Token MemberName => memberName;
}

/// <summary>
/// Represents a function call: ToUpper() or Count().
/// </summary>
internal sealed class FunctionCallNode(
    ReceiverExpressionNode receiver,
    ImmutableArray<ExpressionNode> arguments,
    SourceSpan source) : ReceiverExpressionNode(source)
{
    /// <summary>
    /// The receiver expression (identifier, member access, or static member access).
    /// </summary>
    public ReceiverExpressionNode Receiver => receiver;

    /// <summary>
    /// The function arguments.
    /// </summary>
    public ImmutableArray<ExpressionNode> Arguments => arguments;
}
