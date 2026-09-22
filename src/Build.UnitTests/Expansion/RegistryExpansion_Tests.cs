// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Build.Exceptions;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class RegistryExpansion_Tests
{
    /// <summary>
    ///  v10.0\TeamData\Microsoft.Data.Schema.Common.targets shipped with bad syntax:
    ///  $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory)
    ///  this was evaluating to blank before, now it errors; we have to special case it to
    ///  evaluate to blank.
    ///  Note that this still works whether or not the key exists and has a value.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixSpecialCase()
        => ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory)")
            .ShouldBeEmpty();

    /// <summary>
    ///  In the general case, we should still error for properties that incorrectly miss the Registry: prefix.
    ///  Note that this still fails whether or not the key exists.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixError()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@XXXXDBDirectory)"));

    /// <summary>
    ///  In the general case, we should still error for properties that incorrectly miss the Registry: prefix, like
    ///  the special case, but with extra char on the end.
    ///  Note that this still fails whether or not the key exists.
    /// </summary>
    [Fact]
    public void RegistryPropertyInvalidPrefixError2()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(@"$(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectoryX)"));

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyString()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", "String", RegistryValueKind.String);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("String");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyBinary()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", Encoding.UTF8.GetBytes("String"), RegistryValueKind.Binary);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("83;116;114;105;110;103");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyDWord()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", 123456, RegistryValueKind.DWord);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("123456");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyExpandString()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue("Value", $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyQWord()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", 123456789123456789, RegistryValueKind.QWord);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("123456789123456789");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void RegistryPropertyMultiString()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", new[] { "A", "B", "C", "D" }, RegistryValueKind.MultiString);

            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("A;B;C;D");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValue()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue("Value", $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(
                $@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\{keyPath}', '$(SomeProperty)'))",
                Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValueDefault()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            key.SetValue(string.Empty, $"%{envVar}%", RegistryValueKind.ExpandString);

            ExpandProperties(
                $@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\{keyPath}', null))",
                Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValueFromView1()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);

            key.SetValue(string.Empty, $"%{envVar}%", RegistryValueKind.ExpandString);
            ExpandProperties(
                $@"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\{keyPath}', null, null, RegistryView.Default, RegistryView.Default))",
                Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void PropertyFunctionGetRegistryValueFromView2()
    {
        string keyPath = $@"Software\Microsoft\MSBuild_test_{Guid.NewGuid():N}";
        try
        {
            string envVar = NativeMethodsShared.IsWindows ? "TEMP" : "USER";
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);

            key.SetValue(string.Empty, $"%{envVar}%", RegistryValueKind.ExpandString);
            ExpandProperties(
                $@"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\{keyPath}', null, null, Microsoft.Win32.RegistryView.Default))",
                Properties("SomeProperty", "Value"))
                .ShouldBe(Environment.GetEnvironmentVariable(envVar));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }
}
