// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Environment_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Environment), output)
{
    [Fact]
    public void Environment_Methods()
    {
        const string variable = "MSBUILD_WELLKNOWNFUNCTIONS_MISSING";

        StaticMember(nameof(Environment.ExpandEnvironmentVariables)).Invoke(["%PATH%"]).ShouldBe(Environment.ExpandEnvironmentVariables("%PATH%"));
        StaticMember(nameof(Environment.GetEnvironmentVariable)).Invoke([variable]).ShouldBeNull();
        StaticMember(nameof(Environment.GetEnvironmentVariable))
            .Invoke([variable, EnvironmentVariableTarget.Process])
            .ShouldBeNull();
        StaticMember(nameof(Environment.GetEnvironmentVariable))
            .Invoke([variable, nameof(EnvironmentVariableTarget.Process)])
            .ShouldBeNull();
        StaticMember(nameof(Environment.GetEnvironmentVariables)).Invoke().ShouldBeOfType<Hashtable>();
        StaticMember(nameof(Environment.GetEnvironmentVariables))
            .Invoke([EnvironmentVariableTarget.Process])
            .ShouldBeAssignableTo<IDictionary>();
        StaticMember(nameof(Environment.GetFolderPath))
            .Invoke([Environment.SpecialFolder.System])
            .ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.System));
        StaticMember(nameof(Environment.GetFolderPath))
            .Invoke(["SpecialFolder.System", "SpecialFolderOption.None"])
            .ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.System, Environment.SpecialFolderOption.None));
        StaticMember(nameof(Environment.GetLogicalDrives)).Invoke().ShouldBeOfType<string[]>();
    }

    [Fact]
    public void Environment_Properties()
    {
        StaticMember(nameof(Environment.CommandLine)).Invoke().ShouldBe(Environment.CommandLine);
        StaticMember(nameof(Environment.Is64BitOperatingSystem)).Invoke().ShouldBe(Environment.Is64BitOperatingSystem);
        StaticMember(nameof(Environment.Is64BitProcess)).Invoke().ShouldBe(Environment.Is64BitProcess);
        StaticMember(nameof(Environment.MachineName)).Invoke().ShouldBe(Environment.MachineName);
        StaticMember(nameof(Environment.NewLine)).Invoke().ShouldBe(Environment.NewLine);
        StaticMember(nameof(Environment.OSVersion)).Invoke().ShouldBe(Environment.OSVersion);
        StaticMember(nameof(Environment.ProcessorCount)).Invoke().ShouldBe(Environment.ProcessorCount);
        StaticMember(nameof(Environment.StackTrace)).Invoke().ShouldBeOfType<string>().ShouldNotBeEmpty();
        StaticMember(nameof(Environment.SystemDirectory)).Invoke().ShouldBe(Environment.SystemDirectory);
        StaticMember(nameof(Environment.SystemPageSize)).Invoke().ShouldBe(Environment.SystemPageSize);
        StaticMember(nameof(Environment.TickCount)).Invoke().ShouldBeOfType<int>();
        StaticMember(nameof(Environment.UserDomainName)).Invoke().ShouldBe(Environment.UserDomainName);
        StaticMember(nameof(Environment.UserInteractive)).Invoke().ShouldBe(Environment.UserInteractive);
        StaticMember(nameof(Environment.UserName)).Invoke().ShouldBe(Environment.UserName);
        StaticMember(nameof(Environment.Version)).Invoke().ShouldBe(Environment.Version);
        StaticMember(nameof(Environment.WorkingSet)).Invoke().ShouldBeOfType<long>();
    }

    [Fact]
    public void Environment_InvalidArguments_NotHandled()
    {
        StaticMember(nameof(Environment.CommandLine)).NotHandled(["extra"]);
        StaticMember(nameof(Environment.GetEnvironmentVariable)).NotHandled([42]);
        StaticMember(nameof(Environment.GetFolderPath)).NotHandled(["NotAFolder"]);
    }
}
