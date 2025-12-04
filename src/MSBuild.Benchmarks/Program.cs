// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using MSBuild.Benchmarks;

var argList = new List<string>(args);

ParseAndRemoveBooleanParameter(argList, "--etw", out var useEtwProfiler);
ParseAndRemoveBooleanParameter(argList, "--disable-ngen", out var disableNgen);
ParseAndRemoveBooleanParameter(argList, "--disable-inlining", out var disableJitInlining);

return BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, GetConfig(useEtwProfiler, disableNgen, disableJitInlining))
    .ToExitCode();

IConfig GetConfig(bool useEtwProfiler, bool disableNgen, bool disableJitInlining)
{
    if (Debugger.IsAttached)
    {
        return new DebugInProcessConfig();
    }

    var config = DefaultConfig.Instance;

    if (useEtwProfiler)
    {
        config = config.AddDiagnoser(new EtwProfiler());
    }

    var updateEnvironmentVars = disableNgen || disableJitInlining;

    if (updateEnvironmentVars)
    {
        var job = Job.Default;

        if (disableNgen)
        {
            job = job
                .WithEnvironmentVariable("COMPlus_ZapDisable", "1")
                .WithEnvironmentVariable("COMPlus_ReadyToRun", "0")
                .WithEnvironmentVariable("DOTNET_ReadyToRun", "0");
        }

        if (disableJitInlining)
        {
            job = job
                .WithEnvironmentVariable("COMPlus_JitNoInline", "1")
                .WithEnvironmentVariable("DOTNET_JitNoInline", "0");
        }

        config = config.AddJob(job);
    }

    return config;
}

void ParseAndRemoveBooleanParameter(List<string> argsList, string parameter, out bool parameterValue)
{
    var parameterIndex = argsList.IndexOf(parameter);

    if (parameterIndex != -1)
    {
        argsList.RemoveAt(parameterIndex);

        parameterValue = true;
    }
    else
    {
        parameterValue = false;
    }
}
