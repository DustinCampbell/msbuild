// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Shouldly;
using Xunit;

#if NET
using OperatingSystem = System.OperatingSystem;
#else
using OperatingSystem = Microsoft.Build.Framework.OperatingSystem;
#endif

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_OperatingSystem_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(OperatingSystem), output)
{
    [Fact]
    public void OperatingSystem_CommonMethods()
    {
        StaticMember(nameof(OperatingSystem.IsOSPlatform)).Invoke(["windows"]).ShouldBe(OperatingSystem.IsOSPlatform("windows"));
        StaticMember(nameof(OperatingSystem.IsOSPlatformVersionAtLeast))
            .Invoke(["windows", "0", 0, 0L, 0d])
            .ShouldBe(OperatingSystem.IsOSPlatformVersionAtLeast("windows", 0, 0, 0, 0));
        StaticMember(nameof(OperatingSystem.IsFreeBSD)).Invoke().ShouldBe(OperatingSystem.IsFreeBSD());
        StaticMember(nameof(OperatingSystem.IsFreeBSDVersionAtLeast)).Invoke([0, 0, 0, 0]).ShouldBe(OperatingSystem.IsFreeBSDVersionAtLeast(0, 0, 0, 0));
        StaticMember(nameof(OperatingSystem.IsLinux)).Invoke().ShouldBe(OperatingSystem.IsLinux());
        StaticMember(nameof(OperatingSystem.IsMacOS)).Invoke().ShouldBe(OperatingSystem.IsMacOS());
        StaticMember(nameof(OperatingSystem.IsMacOSVersionAtLeast)).Invoke([0, 0, 0]).ShouldBe(OperatingSystem.IsMacOSVersionAtLeast(0, 0, 0));
        StaticMember(nameof(OperatingSystem.IsWindows)).Invoke().ShouldBe(OperatingSystem.IsWindows());
        StaticMember(nameof(OperatingSystem.IsWindowsVersionAtLeast)).Invoke([0, 0, 0, 0]).ShouldBe(OperatingSystem.IsWindowsVersionAtLeast(0, 0, 0, 0));
    }

#if NET
    [Fact]
    public void OperatingSystem_NetMethods()
    {
        StaticMember(nameof(OperatingSystem.IsAndroid)).Invoke().ShouldBe(OperatingSystem.IsAndroid());
        StaticMember(nameof(OperatingSystem.IsAndroidVersionAtLeast)).Invoke([0, 0, 0, 0]).ShouldBe(OperatingSystem.IsAndroidVersionAtLeast(0, 0, 0, 0));
        StaticMember(nameof(OperatingSystem.IsBrowser)).Invoke().ShouldBe(OperatingSystem.IsBrowser());
        StaticMember(nameof(OperatingSystem.IsIOS)).Invoke().ShouldBe(OperatingSystem.IsIOS());
        StaticMember(nameof(OperatingSystem.IsIOSVersionAtLeast)).Invoke([0, 0, 0]).ShouldBe(OperatingSystem.IsIOSVersionAtLeast(0, 0, 0));
        StaticMember(nameof(OperatingSystem.IsMacCatalyst)).Invoke().ShouldBe(OperatingSystem.IsMacCatalyst());
        StaticMember(nameof(OperatingSystem.IsMacCatalystVersionAtLeast)).Invoke([0, 0, 0]).ShouldBe(OperatingSystem.IsMacCatalystVersionAtLeast(0, 0, 0));
        StaticMember(nameof(OperatingSystem.IsTvOS)).Invoke().ShouldBe(OperatingSystem.IsTvOS());
        StaticMember(nameof(OperatingSystem.IsTvOSVersionAtLeast)).Invoke([0, 0, 0]).ShouldBe(OperatingSystem.IsTvOSVersionAtLeast(0, 0, 0));
        StaticMember(nameof(OperatingSystem.IsWasi)).Invoke().ShouldBe(OperatingSystem.IsWasi());
        StaticMember(nameof(OperatingSystem.IsWatchOS)).Invoke().ShouldBe(OperatingSystem.IsWatchOS());
        StaticMember(nameof(OperatingSystem.IsWatchOSVersionAtLeast)).Invoke([0, 0, 0]).ShouldBe(OperatingSystem.IsWatchOSVersionAtLeast(0, 0, 0));
    }
#endif

    [Fact]
    public void OperatingSystem_InvalidArguments_NotHandled()
    {
        StaticMember(nameof(OperatingSystem.IsWindows)).NotHandled([0]);
        StaticMember(nameof(OperatingSystem.IsWindowsVersionAtLeast)).NotHandled([0]);
        StaticMember(nameof(OperatingSystem.IsOSPlatformVersionAtLeast)).NotHandled(["windows", 0]);
    }
}

public class WellKnownFunctions_OSPlatform_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(OSPlatform), output)
{
    [Fact]
    public void OSPlatform_StaticMembers()
    {
        StaticMember(nameof(OSPlatform.Create)).Invoke(["custom"]).ShouldBe(OSPlatform.Create("custom"));
#if NET
        StaticMember(nameof(OSPlatform.FreeBSD)).Invoke().ShouldBe(OSPlatform.FreeBSD);
#endif
        StaticMember(nameof(OSPlatform.Linux)).Invoke().ShouldBe(OSPlatform.Linux);
        StaticMember(nameof(OSPlatform.OSX)).Invoke().ShouldBe(OSPlatform.OSX);
        StaticMember(nameof(OSPlatform.Windows)).Invoke().ShouldBe(OSPlatform.Windows);
        StaticMember("op_Equality").Invoke([OSPlatform.Windows, OSPlatform.Windows]).ShouldBe(true);
        StaticMember("op_Inequality").Invoke([OSPlatform.Windows, OSPlatform.Linux]).ShouldBe(true);
    }

    [Fact]
    public void OSPlatform_InstanceMembers()
    {
        InstanceMember(nameof(OSPlatform.Equals)).Invoke(OSPlatform.Windows, [OSPlatform.Windows]).ShouldBe(true);
        InstanceMember(nameof(OSPlatform.Equals)).Invoke(OSPlatform.Windows, [(object)OSPlatform.Linux]).ShouldBe(false);
        InstanceMember(nameof(OSPlatform.GetHashCode)).Invoke(OSPlatform.Windows).ShouldBe(OSPlatform.Windows.GetHashCode());
        InstanceMember(nameof(OSPlatform.ToString)).Invoke(OSPlatform.Windows).ShouldBe(OSPlatform.Windows.ToString());
    }
}

public class WellKnownFunctions_RuntimeInformation_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(RuntimeInformation), output)
{
    [Fact]
    public void RuntimeInformation_Members()
    {
        StaticMember(nameof(RuntimeInformation.FrameworkDescription)).Invoke().ShouldBe(RuntimeInformation.FrameworkDescription);
        StaticMember(nameof(RuntimeInformation.OSArchitecture)).Invoke().ShouldBe(RuntimeInformation.OSArchitecture);
        StaticMember(nameof(RuntimeInformation.OSDescription)).Invoke().ShouldBe(RuntimeInformation.OSDescription);
        StaticMember(nameof(RuntimeInformation.ProcessArchitecture)).Invoke().ShouldBe(RuntimeInformation.ProcessArchitecture);
        StaticMember(nameof(RuntimeInformation.IsOSPlatform)).Invoke([OSPlatform.Windows]).ShouldBe(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
#if NET
        StaticMember(nameof(RuntimeInformation.RuntimeIdentifier)).Invoke().ShouldBe(RuntimeInformation.RuntimeIdentifier);
#endif
    }
}
