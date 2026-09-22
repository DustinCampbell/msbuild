// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

internal readonly struct WellKnownFunctionResult
{
    private WellKnownFunctionResult(WellKnownFunctionStatus status, object? result)
    {
        Status = status;
        Result = result;
    }

    public WellKnownFunctionStatus Status { get; }

    public object? Result { get; }

    public static WellKnownFunctionResult NotHandled => new(WellKnownFunctionStatus.NotHandled, result: null);

    public static WellKnownFunctionResult Invoked(object? result) => new(WellKnownFunctionStatus.Invoked, result);
}
