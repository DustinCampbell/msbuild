// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a quoted-expression or function item transform.
/// </summary>
internal readonly struct ItemTransform
{
    private readonly StringSegment _text;
    private readonly int _functionNameLength;
    private readonly Range _functionArgumentsRange;
    private readonly bool _hasFunctionArguments;

    /// <summary>
    ///  Initializes an item transform.
    /// </summary>
    /// <param name="text">The complete transform text.</param>
    /// <param name="functionNameLength">
    ///  The function name's length, or <c>0</c> for a quoted-expression transform.
    /// </param>
    /// <param name="functionArgumentsRange">
    ///  The optional function arguments' range within <paramref name="text"/>.
    /// </param>
    private ItemTransform(
        StringSegment text,
        int functionNameLength,
        Range? functionArgumentsRange)
    {
        _text = text;
        _functionNameLength = functionNameLength;
        _functionArgumentsRange = functionArgumentsRange.GetValueOrDefault();
        _hasFunctionArguments = functionArgumentsRange.HasValue;
    }

    /// <summary>
    ///  Creates a quoted-expression transform.
    /// </summary>
    /// <param name="text">The quoted expression without its surrounding quotes.</param>
    /// <returns>
    ///  The quoted-expression transform.
    /// </returns>
    public static ItemTransform CreateQuotedExpressionTransform(StringSegment text)
        => new(text, functionNameLength: 0, functionArgumentsRange: null);

    /// <summary>
    ///  Creates a function transform.
    /// </summary>
    /// <param name="text">The complete function transform.</param>
    /// <param name="functionNameLength">The length of the function name at the start of <paramref name="text"/>.</param>
    /// <param name="functionArgumentsRange">
    ///  The optional function arguments' range within <paramref name="text"/>.
    /// </param>
    /// <returns>
    ///  The function transform.
    /// </returns>
    public static ItemTransform CreateFunctionTransform(
        StringSegment text,
        int functionNameLength,
        Range? functionArgumentsRange)
        => new(text, functionNameLength, functionArgumentsRange);

    /// <summary>
    ///  Gets the complete transform text.
    /// </summary>
    public StringSegment Text => _text;

    /// <summary>
    ///  Gets a value indicating whether this is a function transform.
    /// </summary>
    public bool IsFunction => _functionNameLength > 0;

    /// <summary>
    ///  Gets a value indicating whether this is a quoted-expression transform.
    /// </summary>
    public bool IsQuotedExpression => !IsFunction;

    /// <summary>
    ///  Gets the function name, or a <see langword="null"/> segment for a quoted-expression transform.
    /// </summary>
    public StringSegment FunctionName => IsFunction ? _text[.._functionNameLength] : default;

    /// <summary>
    ///  Gets the function arguments, or a <see langword="null"/> segment when none were specified.
    /// </summary>
    public StringSegment FunctionArguments => _hasFunctionArguments ? _text[_functionArgumentsRange] : default;

    /// <inheritdoc />
    public override string ToString()
        => _text.ValueOrEmpty;
}
