// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation.Expander;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class InvokerResult_Tests
{
    [Fact]
    public void UnhandledResultHasNoValue()
    {
        InvokerResult result = InvokerResult.Unhandled;

        result.Status.ShouldBe(InvokerResultStatus.Unhandled);
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(0)]
    [InlineData("")]
    public void HandledResultPreservesValue(object? value)
    {
        InvokerResult result = InvokerResult.Handled(value);

        result.Status.ShouldBe(InvokerResultStatus.Handled);
        result.Value.ShouldBe(value);
    }

    [Fact]
    public void NullArrayElementIsHandled()
    {
        string[] receiver = [null!];

        InvokerResult result = WellKnownFunctions.TryInvokeInstanceMember(receiver, "GetValue", [0]);

        result.Status.ShouldBe(InvokerResultStatus.Handled);
        result.Value.ShouldBeNull();
    }
}
