// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents the result of parsing a condition expression.
///  Contains either a successfully parsed expression node or error information.
/// </summary>
internal readonly struct ParseResult
{
    public ExpressionNode? Node { get; }
    public string? ErrorResource { get; }
    public object?[]? ErrorArgs { get; }
    internal int ErrorPosition { get; }

    private readonly ElementLocation? _elementLocation;

    [MemberNotNullWhen(false, nameof(Node))]
    [MemberNotNullWhen(true, nameof(ErrorResource))]
    [MemberNotNullWhen(true, nameof(ErrorArgs))]
    [MemberNotNullWhen(true, nameof(_elementLocation))]
    public bool IsError => ErrorResource is not null;

    private ParseResult(
        ExpressionNode? node,
        string? errorResource,
        object?[]? errorArgs,
        int errorPosition,
        ElementLocation? elementLocation)
    {
        Node = node;
        ErrorResource = errorResource;
        ErrorArgs = errorArgs;
        ErrorPosition = errorPosition;
        _elementLocation = elementLocation;
    }

    public static ParseResult Success(ExpressionNode node)
    {
        Assumed.NotNull(node);

        return new(node, errorResource: null, errorArgs: null, errorPosition: 0, elementLocation: null);
    }

    public static ParseResult Error(string resource, object?[] args, int position, ElementLocation elementLocation)
    {
        Assumed.NotNull(resource);
        Assumed.NotNull(args);
        Assumed.Positive(position);
        Assumed.NotNull(elementLocation);

        return new(node: null, resource, args, position, elementLocation);
    }

    /// <summary>
    ///  Throws an <see cref="Exceptions.InvalidProjectFileException"/> if this result represents a parse error.
    /// </summary>
    public void ThrowIfError()
    {
        if (IsError)
        {
            ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, ErrorResource, ErrorArgs);
        }
    }
}
