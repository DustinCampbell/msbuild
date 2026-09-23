// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Represents the result of attempting to invoke a well-known function without reflection.
/// </summary>
internal readonly struct WellKnownFunctionResult(WellKnownFunctionStatus status, object? result)
{
    /// <summary>
    ///  Gets the invocation status.
    /// </summary>
    public WellKnownFunctionStatus Status => status;

    /// <summary>
    ///  Gets the value returned by the invoked function.
    /// </summary>
    public object? Result => result;

    /// <summary>
    ///  Gets a result indicating that the function was not handled.
    /// </summary>
    public static WellKnownFunctionResult NotHandled
        => new(WellKnownFunctionStatus.NotHandled, result: null);

    /// <summary>
    ///  Creates a result indicating that the function was invoked.
    /// </summary>
    /// <param name="result">The value returned by the function.</param>
    /// <returns>
    ///  The invocation result.
    /// </returns>
    public static WellKnownFunctionResult Invoked(object? result)
        => new(WellKnownFunctionStatus.Invoked, result);
}
