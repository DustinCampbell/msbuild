// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Tests framework, SDK, and assembly location discovery.
/// </summary>
/// <param name="output">The output helper for the test.</param>
public sealed partial class ToolLocationHelper_Tests(ITestOutputHelper output) : TestBase(output)
{
    protected override ValueTask InitializeAsync()
    {
        ToolLocationHelper.ClearStaticCaches();
        return default;
    }
}
