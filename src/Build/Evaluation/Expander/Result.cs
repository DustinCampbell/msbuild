// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

internal readonly record struct Result(bool Success, object? Value)
{
    public static Result None => default;

    public static Result From(object? value)
        => new(true, value);
}
