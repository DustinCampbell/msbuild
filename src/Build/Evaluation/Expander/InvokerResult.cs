// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Represents whether a function was handled and the value it returned.
/// </summary>
internal readonly struct InvokerResult
{
    /// <summary>
    ///  Gets whether the call was handled.
    /// </summary>
    public InvokerResultStatus Status { get; }

    /// <summary>
    ///  Gets the returned value, which may be <see langword="null"/>.
    /// </summary>
    public object? Value { get; }

    private InvokerResult(InvokerResultStatus status, object? value)
    {
        Status = status;
        Value = value;
    }

    /// <summary>
    ///  Gets a result for an unhandled function call.
    /// </summary>
    /// <value>
    ///  A result with <see cref="InvokerResultStatus.Unhandled"/> status and a <see langword="null"/> value.
    /// </value>
    public static InvokerResult Unhandled
        => new(InvokerResultStatus.Unhandled, value: null);

    /// <summary>
    ///  Creates a result for a handled function call.
    /// </summary>
    /// <param name="value">The returned value, including <see langword="null"/> for a null or void result.</param>
    /// <returns>
    ///  A result with <see cref="InvokerResultStatus.Handled"/> status and the supplied <paramref name="value"/>.
    /// </returns>
    public static InvokerResult Handled(object? value)
        => new(InvokerResultStatus.Handled, value);
}
