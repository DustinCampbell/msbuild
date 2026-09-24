// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

#if NET
using OperatingSystem = System.OperatingSystem;
#else
using OperatingSystem = Microsoft.Build.Framework.OperatingSystem;
#endif

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class PropertyFunction_Intrinsic_Tests(ITestOutputHelper output)
{
    private static readonly string s_rootPathPrefix = NativeMethodsShared.IsWindows ? @"C:\" : Path.VolumeSeparatorChar.ToString();

    private readonly ITestOutputHelper _output = output;

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodMakeRelative()
        => ExpandProperties(
            "$([MSBuild]::MakeRelative($(ParentPath), `$(FilePath)`))",
            Properties(
                ("ParentPath", Path.Combine(s_rootPathPrefix, "abc", "def")),
                ("FilePath", Path.Combine(s_rootPathPrefix, "abc", "def", "foo.cpp"))))
            .ShouldBe("foo.cpp");

    [Theory]
    [InlineData("windows")]
    [InlineData("linux")]
    [InlineData("macos")]
    [InlineData("osx")]
    public void IsOSPlatform(string platform)
    {
        string expected = OperatingSystem.IsOSPlatform(platform) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsOSPlatform('{platform}'))")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData("windows", 4, 0, 0, 0)]
    [InlineData("windows", 999, 0, 0, 0)]
    [InlineData("linux", 0, 0, 0, 0)]
    [InlineData("macos", 10, 15, 0, 0)]
    [InlineData("macos", 999, 0, 0, 0)]
    [InlineData("osx", 0, 0, 0, 0)]
    public void IsOSPlatformVersionAtLeast(string platform, int major, int minor, int build, int revision)
    {
        string expected = OperatingSystem.IsOSPlatformVersionAtLeast(platform, major, minor, build, revision) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsOSPlatformVersionAtLeast('{platform}', {major}, {minor}, {build}, {revision}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsLinux()
    {
        string expected = OperatingSystem.IsLinux() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsLinux())")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsFreeBSD()
    {
        string expected = OperatingSystem.IsFreeBSD() ? "True" : "False";

        ExpandProperties("$([System.OperatingSystem]::IsFreeBSD())")
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsFreeBSDVersionAtLeast(int major, int minor, int build, int revision)
    {
        string expected = OperatingSystem.IsFreeBSDVersionAtLeast(major, minor, build, revision) ? "True" : "False";

        ExpandProperties($"$([System.OperatingSystem]::IsFreeBSDVersionAtLeast({major}, {minor}, {build}, {revision}))")
            .ShouldBe(expected);
    }

    [Fact]
    public void IsMacOS()
        => ExpandProperties("$([System.OperatingSystem]::IsMacOS())")
            .ShouldBe(OperatingSystem.IsMacOS().ToResultString());

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 15, 0)]
    [InlineData(999, 0, 0)]
    public void IsMacOSVersionAtLeast(int major, int minor, int build)
        => ExpandProperties($"$([System.OperatingSystem]::IsMacOSVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(OperatingSystem.IsMacOSVersionAtLeast(major, minor, build).ToResultString());

    [Fact]
    public void IsWindows()
        => ExpandProperties("$([System.OperatingSystem]::IsWindows())")
            .ShouldBe(OperatingSystem.IsWindows().ToResultString());

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(4, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsWindowsVersionAtLeast(int major, int minor, int build, int revision)
        => ExpandProperties($"$([System.OperatingSystem]::IsWindowsVersionAtLeast({major}, {minor}, {build}, {revision}))")
            .ShouldBe(OperatingSystem.IsWindowsVersionAtLeast(major, minor, build, revision).ToResultString());

#if NET
    [Fact]
    public void IsAndroid()
        => ExpandProperties("$([System.OperatingSystem]::IsAndroid())")
            .ShouldBe(OperatingSystem.IsAndroid().ToResultString());

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(999, 0, 0, 0)]
    public void IsAndroidVersionAtLeast(int major, int minor, int build, int revision)
        => ExpandProperties($"$([System.OperatingSystem]::IsAndroidVersionAtLeast({major}, {minor}, {build}, {revision}))")
            .ShouldBe(OperatingSystem.IsAndroidVersionAtLeast(major, minor, build, revision).ToResultString());

    [Fact]
    public void IsIOS()
        => ExpandProperties("$([System.OperatingSystem]::IsIOS())")
            .ShouldBe(OperatingSystem.IsIOS().ToResultString());

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(16, 5, 1)]
    [InlineData(999, 0, 0)]
    public void IsIOSVersionAtLeast(int major, int minor, int build)
        => ExpandProperties($"$([System.OperatingSystem]::IsIOSVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(OperatingSystem.IsIOSVersionAtLeast(major, minor, build).ToResultString());

    [Fact]
    public void IsMacCatalyst()
        => ExpandProperties("$([System.OperatingSystem]::IsMacCatalyst())")
            .ShouldBe(OperatingSystem.IsMacCatalyst().ToResultString());

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(999, 0, 0)]
    public void IsMacCatalystVersionAtLeast(int major, int minor, int build)
        => ExpandProperties($"$([System.OperatingSystem]::IsMacCatalystVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(OperatingSystem.IsMacCatalystVersionAtLeast(major, minor, build).ToResultString());

    [Fact]
    public void IsTvOS()
        => ExpandProperties("$([System.OperatingSystem]::IsTvOS())")
            .ShouldBe(OperatingSystem.IsTvOS().ToResultString());

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(16, 5, 0)]
    [InlineData(999, 0, 0)]
    public void IsTvOSVersionAtLeast(int major, int minor, int build)
        => ExpandProperties($"$([System.OperatingSystem]::IsTvOSVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(OperatingSystem.IsTvOSVersionAtLeast(major, minor, build).ToResultString());

    [Fact]
    public void IsWatchOS()
        => ExpandProperties("$([System.OperatingSystem]::IsWatchOS())")
            .ShouldBe(OperatingSystem.IsWatchOS().ToResultString());

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(9, 5, 2)]
    [InlineData(999, 0, 0)]
    public void IsWatchOSVersionAtLeast(int major, int minor, int build)
        => ExpandProperties($"$([System.OperatingSystem]::IsWatchOSVersionAtLeast({major}, {minor}, {build}))")
            .ShouldBe(OperatingSystem.IsWatchOSVersionAtLeast(major, minor, build).ToResultString());
#endif

    [Fact]
    public void IsOsPlatformShouldBeCaseInsensitiveToParameter()
        => ExpandProperties($"$([MSBuild]::IsOsPlatform({Helpers.GetOSPlatformAsString().ToLower()}))")
            .ShouldBe(true.ToResultString());

    [Theory]
    [InlineData("NotAVersion")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1,2,3,4")]
    public void PropertyFunctionVersionComparisonsFailsWithInvalidArguments(string badVersion)
    {
        string expectedMessage = ResourceUtilities.GetResourceString("InvalidVersionFormat");

        AssertThrows($"$([MSBuild]::VersionGreaterThan('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionGreaterThan('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionGreaterThanOrEquals('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionGreaterThanOrEquals('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionLessThan('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionLessThan('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionLessThanOrEquals('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionLessThanOrEquals('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionEquals('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionEquals('1.0.0', '{badVersion}'))", expectedMessage);

        AssertThrows($"$([MSBuild]::VersionNotEquals('{badVersion}', '1.0.0'))", expectedMessage);
        AssertThrows($"$([MSBuild]::VersionNotEquals('1.0.0', '{badVersion}'))", expectedMessage);
    }

    [Theory]
    [InlineData("v1.0", "2.1", -1)]
    [InlineData("3.2", "3.14-pre", -1)]
    [InlineData("3+metadata", "3.0", 0)]
    [InlineData("2.1", "2.1.0", 0)]
    [InlineData("v1.2.3-pre+metadata", "1.2.3.0", 0)]
    [InlineData("3.14", "3.2", 1)]
    [InlineData("42.43.44.45", "42.43.44.5", 1)]
    public void PropertyFunctionVersionComparisons(string a, string b, int expectedSign)
    {
        AssertSuccess($"$([MSBuild]::VersionGreaterThan('{a}', '{b}'))", expectedSign > 0);
        AssertSuccess($"$([MSBuild]::VersionGreaterThanOrEquals('{a}', '{b}'))", expectedSign >= 0);
        AssertSuccess($"$([MSBuild]::VersionLessThan('{a}', '{b}'))", expectedSign < 0);
        AssertSuccess($"$([MSBuild]::VersionLessThanOrEquals('{a}', '{b}'))", expectedSign <= 0);
        AssertSuccess($"$([MSBuild]::VersionEquals('{a}', '{b}'))", expectedSign == 0);
        AssertSuccess($"$([MSBuild]::VersionNotEquals('{a}', '{b}'))", expectedSign != 0);
    }

    [Theory]
    [InlineData("net45", ".NETFramework", "4.5")]
    [InlineData("netcoreapp3.1", ".NETCoreApp", "3.1")]
    [InlineData("netstandard2.1", ".NETStandard", "2.1")]
    [InlineData("net5.0-ios12.0", ".NETCoreApp", "5.0")]
    [InlineData("foo", "Unsupported", "0.0")]
    public void PropertyFunctionTargetFrameworkParsing(string tfm, string expectedIdentifier, string expectedVersion)
    {
        AssertSuccess($"$([MSBuild]::GetTargetFrameworkIdentifier('{tfm}'))", expectedIdentifier);
        AssertSuccess($"$([MSBuild]::GetTargetFrameworkVersion('{tfm}'))", expectedVersion);
    }

    [Theory]
    [InlineData("net45", 2, "4.5")]
    [InlineData("net45", 3, "4.5.0")]
    [InlineData("net472", 3, "4.7.2")]
    [InlineData("net472", 2, "4.7.2")]
    public void PropertyFunctionTargetFrameworkVersionMultipartParsing(string tfm, int versionPartCount, string expectedVersion)
        => AssertSuccess($"$([MSBuild]::GetTargetFrameworkVersion('{tfm}', {versionPartCount}))", expectedVersion);

    [Theory]
    [InlineData("net5.0-windows10.1.2.3", 4, "10.1.2.3")]
    [InlineData("net5.0-windows10.1.2.3", 2, "10.1.2.3")]
    [InlineData("net5.0-windows10.0.0.3", 2, "10.0.0.3")]
    [InlineData("net5.0-windows0.0.0.3", 2, "0.0.0.3")]
    public void PropertyFunctionTargetPlatformVersionMultipartParsing(string tfm, int versionPartCount, string expectedVersion)
        => AssertSuccess($"$([MSBuild]::GetTargetPlatformVersion('{tfm}', {versionPartCount}))", expectedVersion);

    [Theory]
    [InlineData("net5.0-ios12.0", "ios", "12.0")]
    [InlineData("net5.1-android1.1", "android", "1.1")]
    [InlineData("net6.0-windows99.99", "windows", "99.99")]
    [InlineData("net5.0-ios", "ios", "0.0")]
    [InlineData("foo", "", "0.0")]
    public void PropertyFunctionTargetPlatformParsing(string tfm, string expectedIdentifier, string expectedVersion)
    {
        AssertSuccess($"$([MSBuild]::GetTargetPlatformIdentifier('{tfm}'))", expectedIdentifier);
        AssertSuccess($"$([MSBuild]::GetTargetPlatformVersion('{tfm}'))", expectedVersion);
    }

    [Theory]
    [InlineData("net5.0", "net5.0", true)]
    [InlineData("net5.0-windows10.0", "net5.0-windows10.0", true)]
    [InlineData("net5.0-ios", "net5.0-andriod", false)]
    [InlineData("net5.0-ios12.0", "net5.0-ios11.0", true)]
    [InlineData("net5.0-ios11.0", "net5.0-ios12.0", false)]
    [InlineData("net45", "net46", false)]
    [InlineData("net46", "net45", true)]
    [InlineData("netcoreapp3.1", "netcoreapp1.0", true)]
    [InlineData("netstandard1.6", "netstandard2.1", false)]
    [InlineData("netcoreapp3.0", "netstandard2.1", true)]
    [InlineData("net461", "netstandard1.0", true)]
    [InlineData("foo", "netstandard1.0", false)]
    public void PropertyFunctionTargetFrameworkComparisons(string tfm1, string tfm2, bool expected)
        => ExpandProperties($"$([MSBuild]::IsTargetFrameworkCompatible('{tfm1}', '{tfm2}'))")
            .ShouldBe(expected.ToResultString());

    private static void AssertThrows(string expression, string expectedMessage)
    {
        InvalidProjectFileException ex = Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(expression));

        ex.Message.ShouldContain(expectedMessage);
    }

    private static void AssertSuccess(string expression, object expected)
        => ExpandProperties(expression).ShouldBe(expected.ToResultString());

    /// <summary>
    ///  Expand intrinsic property function to locate the directory of a file above.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodDirectoryNameOfFileAbove()
    {
        using var env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        var file = env.CreateFile(folder, "marker.tmp");
        string directoryStart = Path.Combine(folder.Path, "one", "two", "three", "four", "five");

        var properties = Properties(
            ("StartingDirectory", directoryStart),
            ("FileToFind", Path.GetFileName(file.Path)));

        string? result = ExpandProperties("$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), $(FileToFind)))", properties);

        FileUtilities.EnsureTrailingSlash(result.ShouldNotBeNull())
            .ShouldBe(FileUtilities.EnsureTrailingSlash(folder.Path));

        ExpandProperties("$([MSBuild]::GetDirectoryNameOfFileAbove($(StartingDirectory), Hobbits))", properties)
            .ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> returns the correct path if a file exists
    ///  or an empty string if it doesn't.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAbove()
    {
        using var env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        var file = env.CreateFile(folder, "marker.tmp");
        string projectFile = Path.Combine(folder.Path, "one", "two", "three", "four", "five", "test.proj");

        var properties = Properties("FileToFind", Path.GetFileName(file.Path));

        ExpandProperties("$([MSBuild]::GetPathOfFileAbove($(FileToFind)))", properties, projectFile)
            .ShouldBe(file.Path);

        ExpandProperties("$([MSBuild]::GetPathOfFileAbove('Hobbits'))", properties, projectFile)
            .ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that the legacy pseudo-overload for <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> requires
    ///  the method name to use its declared casing.
    /// </summary>
    [LegacyExpanderOnlyFact]
    public void LegacyGetPathOfFileAboveRequiresExactCasing()
    {
        using var env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        var file = env.CreateFile(folder, "marker.tmp");
        string projectFile = Path.Combine(folder.Path, "one", "two", "three", "test.proj");

        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                "$([MSBuild]::getpathoffileabove($(FileToFind)))",
                Properties("FileToFind", Path.GetFileName(file.Path)),
                projectFile));
    }

    /// <summary>
    ///  Verifies that the modern pseudo-overload for <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> handles
    ///  the method name case-insensitively.
    /// </summary>
    [ModernExpanderOnlyFact]
    public void ModernGetPathOfFileAboveIgnoresCasing()
    {
        using var env = TestEnvironment.Create(_output);
        var folder = env.CreateFolder();
        var file = env.CreateFile(folder, "marker.tmp");
        string projectFile = Path.Combine(folder.Path, "one", "two", "three", "test.proj");

        ExpandProperties(
            "$([MSBuild]::getpathoffileabove($(FileToFind)))",
            Properties("FileToFind", Path.GetFileName(file.Path)),
            projectFile)
            .ShouldBe(file.Path);
    }

    /// <summary>
    ///  Verifies that the usage of GetPathOfFileAbove() within an in-memory project throws an exception.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAboveInMemoryProject()
    {
        const string Content = """
            <Project>
                <PropertyGroup>
                    <foo>$([MSBuild]::GetPathOfFileAbove('foo'))</foo>
                </PropertyGroup>
            </Project>
            """;

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
        {
            ObjectModelHelpers.CreateInMemoryProject(Content);
        });

        exception.Message.ShouldStartWith("""The expression "[MSBuild]::GetPathOfFileAbove(foo, '')" cannot be evaluated.""");
    }

    /// <summary>
    ///  Verifies that <see cref="IntrinsicFunctions.GetPathOfFileAbove"/> only accepts a file name.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetPathOfFileAboveFileNameOnly()
    {
        string fileWithPath = Path.Combine("foo", "bar", "file.txt");

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(() =>
        {
            ExpandProperties("$([MSBuild]::GetPathOfFileAbove($(FileWithPath)))", Properties("FileWithPath", fileWithPath));
        });

        exception.Message.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("InvalidGetPathOfFileAboveParameter", fileWithPath));
    }

    /// <summary>
    ///  Expand property function that calls a static arithmetic method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodArithmeticAddInt32()
        => ExpandProperties("$([MSBuild]::Add(40, 2))")
            .ShouldBe((40 + 2).ToResultString());

    /// <summary>
    ///  Expand property function that calls a static arithmetic method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodArithmeticAddDouble()
        => ExpandProperties("$([MSBuild]::Add(39.9, 2.1))")
            .ShouldBe((39.9 + 2.1).ToResultString());

    /// <summary>
    ///  Expand property function choosing either the value (if not empty) or the default specified.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValueOrDefaultData))]
    public void PropertyFunctionValueOrDefault(string expression, int expected)
        => ExpandProperties(expression).ShouldBe(expected.ToResultString());

    public static TheoryData<string, int> ValueOrDefaultData => new()
    {
        { "$([MSBuild]::ValueOrDefault('', '42'))", 42 },
        { "$([MSBuild]::ValueOrDefault('42', '43'))", 42 },
    };

    /// <summary>
    ///  Observe property value changes when choosing between the value and a default.
    /// </summary>
    [Fact]
    public void PropertyFunctionValueOrDefaultFromEnvironment()
    {
        var properties = Properties("DifferentTargetsPath", "Different");

        ExpandProperties("$([MSBuild]::ValueOrDefault('$(DifferentTargetsPath)', '42'))", properties)
            .ShouldBe("Different");

        properties.Set(ProjectPropertyInstance.Create("DifferentTargetsPath", string.Empty));

        ExpandProperties("$([MSBuild]::ValueOrDefault('$(DifferentTargetsPath)', '43'))", properties)
            .ShouldBe("43");
    }

#if FEATURE_APPDOMAIN
    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist()
        => ExpandProperties("$([MSBuild]::DoesTaskHostExist('CurrentRuntime', 'CurrentArchitecture'))")
            .ShouldBe(true.ToResultString());

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Whitespace()
        => ExpandProperties("$([MSBuild]::DoesTaskHostExist('   CurrentRuntime    ', 'CurrentArchitecture'))")
            .ShouldBe(true.ToResultString());
#endif

    [Theory]
    [MemberData(nameof(NormalizeDirectoryExpressions))]
    public void PropertyFunctionNormalizeDirectory(string expression, string expectedRelativePath)
    {
        var expander = ExpanderFactory.Create(Properties(
            ("MyPath", "one"),
            ("MySecondPath", "two")));

        expander.ExpandIntoStringAndUnescape(expression, ExpanderOptions.ExpandProperties, MockElementLocation.Instance)
            .ShouldBe($"{Path.GetFullPath(expectedRelativePath)}{Path.DirectorySeparatorChar}");
    }

    public static TheoryData<string, string> NormalizeDirectoryExpressions => new()
    {
        { "$([MSBuild]::NormalizeDirectory($(MyPath)))", "one" },
        { "$([MSBuild]::NormalizeDirectory($(MyPath), $(MySecondPath)))", Path.Combine("one", "two") },
    };

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Error()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$([MSBuild]::DoesTaskHostExist('ASDF', 'CurrentArchitecture'))"));

#if FEATURE_APPDOMAIN
    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_Evaluated()
        => ExpandProperties(
            "$([MSBuild]::DoesTaskHostExist('$(Runtime)', '$(Architecture)'))",
            Properties(
                ("Runtime", "CurrentRuntime"),
                ("Architecture", "CurrentArchitecture")))
            .ShouldBe(true.ToResultString());

    /// <summary>
    ///  Expand property function that tests for existence of the task host.
    /// </summary>
    [Fact]
    public void PropertyFunctionDoesTaskHostExist_NonexistentTaskHost()
    {
        try
        {
            using var env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDTASKHOST_EXE_NAME", "asdfghjkl.exe");
            NodeProviderOutOfProcTaskHost.ClearCachedTaskHostPaths();

            // CLR has been forced to pretend not to exist, whether it actually does or not
            ExpandProperties("$([MSBuild]::DoesTaskHostExist('CLR2', 'CurrentArchitecture'))")
                .ShouldBe(false.ToResultString());
        }
        finally
        {
            NodeProviderOutOfProcTaskHost.ClearCachedTaskHostPaths();
        }
    }
#endif

    /// <summary>
    ///  Expand property function that calls a static bitwise method to retrieve file attribute.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodFileAttributes()
    {
        using var env = TestEnvironment.Create(_output);
        var file = env.CreateFile();

        try
        {
            File.SetAttributes(file.Path, FileAttributes.ReadOnly | FileAttributes.Archive);

            ExpandProperties($"$([MSBuild]::BitwiseAnd(32,$([System.IO.File]::GetAttributes({file.Path}))))")
                .ShouldBe(32.ToResultString());
        }
        finally
        {
            File.SetAttributes(file.Path, FileAttributes.Normal);
        }
    }

    /// <summary>
    ///  Expand intrinsic property function calls a static arithmetic method.
    /// </summary>
    [Theory]
    [UseInvariantCulture]
    [MemberData(nameof(StaticMethodIntrinsicMathsData))]
    public void PropertyFunctionStaticMethodIntrinsicMaths(string expression, double expected)
        => ExpandProperties(expression).ShouldBe(expected.ToResultString());

    public static TheoryData<string, double> StaticMethodIntrinsicMathsData => new()
    {
        { "$([MSBuild]::Add(39.9, 2.1))", 39.9 + 2.1 },
        { "$([MSBuild]::Add(40, 2))", 40 + 2 },
        { "$([MSBuild]::Subtract(44, 2))", 44 - 2 },
        { "$([MSBuild]::Subtract(42.9, 0.9))", 42.9 - 0.9d },
        { "$([MSBuild]::Multiply(21, 2))", 21d * 2d },
        { "$([MSBuild]::Multiply(84.0, 0.5))", 84.0d * 0.5d },
        { "$([MSBuild]::Divide(84, 2))", 84d / 2d },
        { "$([MSBuild]::Divide(84.4, 2.0))", 84.4 / 2.0 },
        { "$([MSBuild]::Modulo(85, 2))", 85 % 2 },
        { "$([MSBuild]::Modulo(2345.5, 43))", 2345.5 % 43 },
    };

    /// <summary>
    ///  Expand intrinsic property functions that call a bit operator.
    /// </summary>
    [Theory]
    [MemberData(nameof(StaticMethodIntrinsicBitOperationsData))]
    public void PropertyFunctionStaticMethodIntrinsicBitOperations(string expression, int expected)
        => ExpandProperties(expression).ShouldBe(expected.ToResultString());

    public static TheoryData<string, int> StaticMethodIntrinsicBitOperationsData => new()
    {
        { "$([MSBuild]::BitwiseOr(40, 2))", 40 | 2 },
        { "$([MSBuild]::BitwiseAnd(42, 2))", 42 & 2 },
        { "$([MSBuild]::BitwiseXor(213, 255))", 213 ^ 255 },
        { "$([MSBuild]::BitwiseNot(-43))", ~ -43 },
        { "$([MSBuild]::LeftShift(1, 2))", 1 << 2 },
        { "$([MSBuild]::RightShift(-8, 2))", -8 >> 2 },
        { "$([MSBuild]::RightShiftUnsigned(-8, 2))", -8 >>> 2 },
    };

    public static IEnumerable<object?[]> GetHashAlgoTypes()
        => Enum.GetNames(typeof(IntrinsicFunctions.StringHashingAlgorithm))
            .Append(null)
            .Select(t => new object?[] { t });

    [Theory]
    [MemberData(nameof(GetHashAlgoTypes))]
    public void PropertyFunctionHashCodeSameOnlyIfStringSame(string? hashType)
    {
        string[] stringsToHash = [
            "cat1s",
            "cat1z",
            "bat1s",
            "cut1s",
            "cat1so",
            "cats1",
            "acat1s",
            "cat12s",
            "cat1s"
        ];
        string hashTypeString = hashType is null ? "" : $", '{hashType}'";
        string?[] hashes = stringsToHash.Select(toHash =>
            ExpandProperties($"$([MSBuild]::StableStringHash('{toHash}'{hashTypeString}))"))
            .ToArray();
        for (int a = 0; a < hashes.Length; a++)
        {
            for (int b = a; b < hashes.Length; b++)
            {
                if (stringsToHash[a].Equals(stringsToHash[b]))
                {
                    hashes[a].ShouldBe(hashes[b], "Identical strings should hash to the same value.");
                }
                else
                {
                    hashes[a].ShouldNotBe(hashes[b], "Different strings should not hash to the same value.");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetHashAlgoTypes))]
    public void PropertyFunctionHashCodeReturnsExpectedType(string? hashType)
    {
        TypeCode expectedTypeCode = hashType switch
        {
            null => TypeCode.Int32,
            "Legacy" => TypeCode.Int32,
            "Fnv1a32bit" => TypeCode.Int32,
            "Fnv1a32bitFast" => TypeCode.Int32,
            "Fnv1a64bit" => TypeCode.Int64,
            "Fnv1a64bitFast" => TypeCode.Int64,
            "Sha256" => TypeCode.String,
            _ => throw new ArgumentOutOfRangeException(nameof(hashType)),
        };

        string hashTypeString = hashType is null ? "" : $", '{hashType}'";
        ExpandProperties(
            $"$([System.Convert]::GetTypeCode($([MSBuild]::StableStringHash('FooBar'{hashTypeString}))))")
            .ShouldBe(expectedTypeCode.ToString());
    }

    [Theory]
    [InlineData("easycase")]
    [InlineData("")]
    [InlineData("\"\n()\tsdfIR$%#*;==")]
    public void TestBase64Conversion(string testCase)
    {
        string intermediate = ExpandProperties($"$([MSBuild]::ConvertToBase64('{testCase}'))").ShouldNotBeNull();
        intermediate.Trim('=').All(c => char.IsLetterOrDigit(c) || c is '+' or '/').ShouldBeTrue();
        ExpandProperties($"$([MSBuild]::ConvertFromBase64('{intermediate}'))")
            .ShouldBe(testCase);
    }

    [Theory]
    [InlineData("easycase", "ZWFzeWNhc2U=")]
    [InlineData("", "")]
    [InlineData("\"\n()\tsdfIR$%#*;==", "IgooKQlzZGZJUiQlIyo7PT0=")]
    public void TestExplicitToBase64Conversion(string plaintext, string base64)
        => ExpandProperties($"$([MSBuild]::ConvertToBase64('{plaintext}'))")
            .ShouldBe(base64);

    [Theory]
    [InlineData("easycase", "ZWFzeWNhc2U=")]
    [InlineData("", "")]
    [InlineData("\"\n()\tsdfIR$%#*;==", "IgooKQlzZGZJUiQlIyo7PT0=")]
    public void TestExplicitFromBase64Conversion(string plaintext, string base64)
        => ExpandProperties($"$([MSBuild]::ConvertFromBase64('{base64}'))")
            .ShouldBe(plaintext);

    [Theory]
    [MemberData(nameof(EnsureTrailingSlashExpressions))]
    public void PropertyFunctionEnsureTrailingSlash(string expression)
    {
        string path = Path.Combine("foo", "bar");

        ExpandProperties(expression, Properties("SomeProperty", path))
            .ShouldBe($"{path}{Path.DirectorySeparatorChar}");
    }

    public static TheoryData<string> EnsureTrailingSlashExpressions => new()
    {
        $"$([MSBuild]::EnsureTrailingSlash('{Path.Combine("foo", "bar")}'))",
        "$([MSBuild]::EnsureTrailingSlash($(SomeProperty)))",
    };

    [Theory]
    [InlineData("NonExistingFeature", "Undefined")]
    [InlineData("EvaluationContext_SharedSDKCachePolicy", "Available")]
    public void PropertyFunctionCheckFeatureAvailability(string featureName, string availability)
        => ExpandProperties($"$([MSBuild]::CheckFeatureAvailability({featureName}))")
            .ShouldBe(availability);

    [Theory]
    [InlineData("\u0074\u0068\u0069\u0073\u002a\u3407\ud840\udc60\ud86a\ude30\ud86e\udc0a\ud86e\udda0\ud879\udeae\u2fd5\u0023", 2, 10, "is________")]
    [InlineData("\ud83d\udc68\u200d\ud83d\udc68\u200d\ud83d\udc66\u200d\ud83d\udc66\ud83d\udc68\u200d\ud83d\udc68\u200d\ud83d\udc66\u200d\ud83d\udc66\u002e\u0070\u0072\u006f\u006a", 0, 8, "________")]
    public void SubstringByAsciiChars(string featureName, int start, int length, string expected)
        => ExpandProperties($"$([MSBuild]::SubstringByAsciiChars({featureName}, {start}, {length}))")
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetCurrentToolsDirectory()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetCurrentToolsDirectory())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetCurrentToolsDirectory()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetToolsDirectory32()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetToolsDirectory32())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetToolsDirectory32()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetToolsDirectory64()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetToolsDirectory64())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetToolsDirectory64()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetMSBuildSDKsPath()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetMSBuildSDKsPath())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetMSBuildSDKsPath()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetVsInstallRoot()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetVsInstallRoot())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetVsInstallRoot()) ?? string.Empty);

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetMSBuildExtensionsPath()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetMSBuildExtensionsPath())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetMSBuildExtensionsPath()));

    [Fact]
    public void PropertyFunctionIntrinsicFunctionGetProgramFiles32()
        => ExpandProperties("$([Microsoft.Build.Evaluation.IntrinsicFunctions]::GetProgramFiles32())")
            .ShouldBe(EscapingUtilities.Escape(IntrinsicFunctions.GetProgramFiles32()));

    [Fact]
    public void PropertyFunctionMSBuildAddIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Add($(X), 5))", Properties("X", "7"))
            .ShouldBe(12.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildAddRealLiteral()
        => ExpandProperties("$([MSBuild]::Add($(X), 0.5))", Properties("X", "7"))
            .ShouldBe(7.5d.ToResultString());

    /// <summary>
    ///  Overflow wrapping - result exceeds size of long.
    /// </summary>
    [Fact]
    public void PropertyFunctionMSBuildAddIntegerOverflow()
        => ExpandProperties("$([MSBuild]::Add($(X), 1))", Properties("X", long.MaxValue.ToString()))
            .ShouldBe((-9223372036854775808).ToResultString());

    [Fact]
    [UseInvariantCulture]
    public void PropertyFunctionMSBuildAddRealArgument()
    {
        // string argument is an integer that exceeds the size of long.
        double value = long.MaxValue + 1.0;
        double expected = value + 1.0;
        ExpandProperties("$([MSBuild]::Add($(X), 1))", Properties("X", value.ToString()))
            .ShouldBe(expected.ToResultString());
    }

    [Fact]
    public void PropertyFunctionMSBuildAddComplex()
        => ExpandProperties("$([MSBuild]::Add($(X), $([MSBuild]::Add(2, 3))))", Properties("X", "7"))
            .ShouldBe(12.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildSubtractIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Subtract($(X), 20100000))", Properties("X", "20100042"))
            .ShouldBe(42.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildSubtractRealLiteral()
        => ExpandProperties("$([MSBuild]::Subtract($(X), 20100000.0))", Properties("X", "20100042"))
            .ShouldBe(42.ToResultString());

    /// <summary>
    ///  If the double overload is used, there will be a rounding error.
    /// </summary>
    [Fact]
    public void PropertyFunctionMSBuildSubtractIntegerMaxValue()
        => ExpandProperties("$([MSBuild]::Subtract($(X), 9223372036854775806))", Properties("X", long.MaxValue.ToString()))
            .ShouldBe(1.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildMultiplyIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Multiply($(X), 8800))", Properties("X", "2"))
            .ShouldBe(17600.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildMultiplyRealLiteral()
        => ExpandProperties("$([MSBuild]::Multiply($(X), 1.5))", Properties("X", "2"))
            .ShouldBe(3.ToResultString());

    /// <summary>
    ///  Overflow - result exceeds size of long.
    /// </summary>
    [Fact]
    public void PropertyFunctionMSBuildMultiplyIntegerOverflow()
        => ExpandProperties("$([MSBuild]::Multiply($(X), 2))", Properties("X", long.MaxValue.ToString()))
            .ShouldBe((-2).ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildMultiplyComplex()
        => ExpandProperties("$([MSBuild]::Multiply($(X), $([MSBuild]::Multiply(1, 8800))))", Properties("X", "2"))
            .ShouldBe(17600.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildDivideIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Divide($(X), 10000))", Properties("X", "65536"))
            .ShouldBe(6.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildDivideRealLiteral()
        => ExpandProperties("$([MSBuild]::Divide($(X), 10000.0))", Properties("X", "65536"))
            .ShouldBe(6.5536.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildModuloIntegerLiteral()
        => ExpandProperties("$([MSBuild]::Modulo($(X), 3))", Properties("X", "10"))
            .ShouldBe(1.ToResultString());

    [Fact]
    public void PropertyFunctionMSBuildModuloRealLiteral()
        => ExpandProperties("$([MSBuild]::Modulo($(X), 3.0))", Properties("X", "10"))
            .ShouldBe(1.ToResultString());

    [Theory]
    [InlineData("net6.0", "netstandard2.0", "")]
    [InlineData("net6.0-windows", "netstandard2.0", "")]
    [InlineData("net6.0-windows", "net6.0", "net6.0-windows")]
    [InlineData("netstandard2.0;net6.0", "net6.0", "net6.0")]
    [InlineData("netstandard2.0;net6.0-windows", "net6.0", "net6.0-windows")]
    [InlineData("netstandard2.0;net6.0-windows", "net6.0;netstandard2.0;net472", "netstandard2.0%3bnet6.0-windows")]
    [InlineData("netstandard2.0;net472", "net6.0;netstandard2.0;net472", "netstandard2.0%3bnet472")]
    public void PropertyFunctionFilterTargetFrameworks(string incoming, string filter, string expected)
        => ExpandProperties($"$([MSBuild]::FilterTargetFrameworks('{incoming}', '{filter}'))")
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionRegisterBuildCheck()
    {
        using var env = TestEnvironment.Create(_output);
        var (logger, loggingContext) = CreateLoggingContext(_output);
        var dummyAssemblyFile = env.CreateFile(env.CreateFolder(), "test.dll");

        ExpandProperties($"$([MSBuild]::RegisterBuildCheck({dummyAssemblyFile.Path}))", loggingContext)
            .ShouldBe(true.ToResultString());

        logger.AllBuildEvents.ShouldHaveSingleItem().ShouldBeOfType<BuildCheckAcquisitionEventArgs>();
    }
}
