// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public sealed class WellKnownFunctions_ToolLocationHelper_Tests
{
    [Fact]
    public void Properties()
    {
        AssertInvoked(nameof(ToolLocationHelper.CurrentToolsVersion), [], ToolLocationHelper.CurrentToolsVersion);
        AssertInvoked(nameof(ToolLocationHelper.PathToSystem), [], ToolLocationHelper.PathToSystem);
    }

    private static void AssertInvoked(string methodName, object?[] arguments, object? expected)
    {
        WellKnownFunctionResult result =
            WellKnownFunctions.TryInvokeStatic(typeof(ToolLocationHelper), methodName, arguments, FileSystems.Default);

        result.Status.ShouldBe(WellKnownFunctionStatus.Invoked);
        result.Result.ShouldBe(expected);
    }
}
