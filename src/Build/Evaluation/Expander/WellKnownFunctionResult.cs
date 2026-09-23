// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

internal readonly struct WellKnownFunctionResult(WellKnownFunctionStatus status, object? result)
{
    public WellKnownFunctionStatus Status => status;

    public object? Result => result;

    public static WellKnownFunctionResult NotHandled
        => new(WellKnownFunctionStatus.NotHandled, result: null);

    public static WellKnownFunctionResult Invoked(object? result)
        => new(WellKnownFunctionStatus.Invoked, result);
}
