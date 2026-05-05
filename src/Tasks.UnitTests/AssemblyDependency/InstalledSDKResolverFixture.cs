// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

/// <summary>
/// Unit tests for the InstalledSDKResolver task.
/// </summary>
public sealed class InstalledSDKResolverFixture(ITestOutputHelper output) : ResolveAssemblyReferenceTestFixture(output)
{
    /// <summary>
    /// Verify that we do not find the winmd file even if it on the search path if the sdkname
    /// does not match something passed into the ResolvedSDKs property.
    /// </summary>
    [Fact]
    public void SDKNameNotInResolvedSDKListButOnSearchPath()
    {
        // Create the engine.
        MockEngine engine = new(_output);
        TaskItem taskItem = new(@"SDKWinMD");
        taskItem.SetMetadata("SDKName", "NotInstalled, Version=1.0");

        // Now, pass feed resolved primary references into ResolveAssemblyReference.
        ResolveAssemblyReference task = new()
        {
            BuildEngine = engine,
            Assemblies = [taskItem],
            SearchPaths = [@"C:\FakeSDK\References"],
        };

        bool succeeded = Execute(task);

        Assert.True(succeeded);
        Assert.Empty(task.ResolvedFiles);

        Assert.Equal(0, engine.Errors);
        Assert.Equal(1, engine.Warnings);
    }

    /// <summary>
    /// Verify when we are trying to match a name which is the reference assembly directory.
    /// </summary>
    [Theory]
    [InlineData("DebugX86SDKWinMD", @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd")]
    [InlineData("DebugNeutralSDKWinMD", @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd")]
    [InlineData("x86SDKWinMD", @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd")]
    [InlineData("NeutralSDKWinMD", @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd")]
    [InlineData("SDKReference", @"C:\FakeSDK\References\Debug\X86\SDKReference.dll")]
    [InlineData("DebugX86SDKRA", @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll")]
    [InlineData("DebugNeutralSDKRA", @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll")]
    [InlineData("x86SDKRA", @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll")]
    [InlineData("NeutralSDKRA", @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll")]
    public void SDKNameMatchInRADirectory(string referenceName, string expectedPath)
    {
        MockEngine engine = new(_output);
        TaskItem taskItem = new(referenceName);
        taskItem.SetMetadata("SDKName", "FakeSDK, Version=1.0");

        TaskItem resolvedSDK = new(@"C:\FakeSDK");
        resolvedSDK.SetMetadata("SDKName", "FakeSDK, Version=1.0");
        resolvedSDK.SetMetadata("TargetedSDKConfiguration", "Debug");
        resolvedSDK.SetMetadata("TargetedSDKArchitecture", "X86");

        ResolveAssemblyReference task = new()
        {
            BuildEngine = engine,
            Assemblies = [taskItem],
            ResolvedSDKReferences = [resolvedSDK],
            SearchPaths = [@"C:\SomeOtherPlace"],
        };

        bool succeeded = Execute(task);

        Assert.True(succeeded);
        ITaskItem resolvedFile = Assert.Single(task.ResolvedFiles);
        Assert.Equal(0, engine.Errors);
        Assert.Equal(0, engine.Warnings);
        Assert.Equal(expectedPath, resolvedFile.ItemSpec, ignoreCase: true);
    }
}
