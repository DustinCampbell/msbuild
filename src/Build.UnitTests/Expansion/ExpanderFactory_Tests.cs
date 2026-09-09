// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Expansion.Legacy;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public sealed class ExpanderFactory_Tests(ITestOutputHelper output) : IDisposable
{
    private const string DisableFeaturesFromVersion = "MSBUILDDISABLEFEATURESFROMVERSION";
    private const string UseLegacyExpander = "MSBUILDUSELEGACYEXPANDER";

    public void Dispose()
    {
        ChangeWaves.ResetStateForTests();
    }

    [Fact]
    public void CreatesModernExpanderByDefault()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        SetSelectionEnvironment(env, disabledWave: null, useLegacyExpander: null);

        CreateExpander().ShouldBeOfType<Expander<ProjectPropertyInstance, ProjectItemInstance>>();
    }

    [Fact]
    public void EscapeHatchCreatesLegacyExpander()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        SetSelectionEnvironment(env, disabledWave: null, useLegacyExpander: "1");

        CreateExpander().ShouldBeOfType<LegacyExpander<ProjectPropertyInstance, ProjectItemInstance>>();
    }

    [Fact]
    public void DisabledChangeWaveCreatesLegacyExpander()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        SetSelectionEnvironment(env, ChangeWaves.Wave18_12.ToString(), useLegacyExpander: null);

        CreateExpander().ShouldBeOfType<LegacyExpander<ProjectPropertyInstance, ProjectItemInstance>>();
    }

    private static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander()
        => ExpanderFactory.Create(new PropertyDictionary<ProjectPropertyInstance>());

    private static void SetSelectionEnvironment(TestEnvironment env, string? disabledWave, string? useLegacyExpander)
    {
        env.SetEnvironmentVariable(DisableFeaturesFromVersion, disabledWave);
        env.SetEnvironmentVariable(UseLegacyExpander, useLegacyExpander);
        ChangeWaves.ResetStateForTests();
    }
}
