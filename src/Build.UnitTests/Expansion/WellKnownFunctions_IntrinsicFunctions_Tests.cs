// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_IntrinsicFunctions_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(IntrinsicFunctions), output)
{
    [Theory]
    [MemberData(nameof(ArithmeticFunctionData))]
    public void IntrinsicFunctions_Arithmetic(string methodName, object? left, object? right, bool handled, bool divideByZero, object? expected)
    {
        if (!handled)
        {
            StaticMember(methodName).NotHandled([left, right]);
        }
        else if (divideByZero)
        {
            Should.Throw<DivideByZeroException>(() => StaticMember(methodName).Invoke([left, right]));
        }
        else
        {
            StaticMember(methodName)
                .Invoke([left, right])
                .ShouldBe(expected);
        }
    }

    public static TheoryData<string, object?, object?, bool, bool, object?> ArithmeticFunctionData
    {
        get
        {
            string[] methodNames =
            [
                nameof(IntrinsicFunctions.Add),
                nameof(IntrinsicFunctions.Subtract),
                nameof(IntrinsicFunctions.Multiply),
                nameof(IntrinsicFunctions.Divide),
                nameof(IntrinsicFunctions.Modulo),
            ];

            TheoryData<string, object?, object?, bool, bool, object?> data = new();

            foreach (string methodName in methodNames)
            {
                foreach (var (left, right, converted0, converted1) in ArgumentParser_Tests.GetArithmeticArgumentCases())
                {
                    if (converted0 is null)
                    {
                        data.Add(methodName, left, right, false, false, null);
                    }
                    else if (converted0 is long long0)
                    {
                        long long1 = (long)converted1!;
                        bool divideByZero = long1 == 0
                            && methodName is nameof(IntrinsicFunctions.Divide) or nameof(IntrinsicFunctions.Modulo);

                        data.Add(methodName, left, right, true, divideByZero, divideByZero ? null : InvokeLongs(methodName, long0, long1));
                    }
                    else
                    {
                        data.Add(methodName, left, right, true, false, InvokeDoubles(methodName, (double)converted0, (double)converted1!));
                    }
                }
            }

            return data;

            static long InvokeLongs(string methodName, long arg0, long arg1)
                => methodName switch
                {
                    nameof(IntrinsicFunctions.Add) => IntrinsicFunctions.Add(arg0, arg1),
                    nameof(IntrinsicFunctions.Subtract) => IntrinsicFunctions.Subtract(arg0, arg1),
                    nameof(IntrinsicFunctions.Multiply) => IntrinsicFunctions.Multiply(arg0, arg1),
                    nameof(IntrinsicFunctions.Divide) => IntrinsicFunctions.Divide(arg0, arg1),
                    nameof(IntrinsicFunctions.Modulo) => IntrinsicFunctions.Modulo(arg0, arg1),

                    _ => Assumed.Unreachable<long>(),
                };

            static double InvokeDoubles(string methodName, double arg0, double arg1)
                => methodName switch
                {
                    nameof(IntrinsicFunctions.Add) => IntrinsicFunctions.Add(arg0, arg1),
                    nameof(IntrinsicFunctions.Subtract) => IntrinsicFunctions.Subtract(arg0, arg1),
                    nameof(IntrinsicFunctions.Multiply) => IntrinsicFunctions.Multiply(arg0, arg1),
                    nameof(IntrinsicFunctions.Divide) => IntrinsicFunctions.Divide(arg0, arg1),
                    nameof(IntrinsicFunctions.Modulo) => IntrinsicFunctions.Modulo(arg0, arg1),

                    _ => Assumed.Unreachable<double>(),
                };
        }
    }

    [Theory]
    [InlineData(nameof(IntrinsicFunctions.Add))]
    [InlineData(nameof(IntrinsicFunctions.Divide))]
    [InlineData(nameof(IntrinsicFunctions.Modulo))]
    [InlineData(nameof(IntrinsicFunctions.Multiply))]
    [InlineData(nameof(IntrinsicFunctions.Subtract))]
    public void IntrinsicFunctions_Arithmetic_InvalidArgument_NotHandled(string memberName)
        => StaticMember(memberName)
            .NotHandled(["invalid", 1]);

    [Fact]
    public void IntrinsicFunctions_AreFeaturesEnabled()
    {
        Version wave = new(999, 0);

        StaticMember(nameof(IntrinsicFunctions.AreFeaturesEnabled))
            .Invoke([wave.ToString()])
            .ShouldBe(IntrinsicFunctions.AreFeaturesEnabled(wave));
    }

    [Fact]
    public void IntrinsicFunctions_AreFeaturesEnabled_Version()
    {
        Version wave = new(999, 0);

        StaticMember(nameof(IntrinsicFunctions.AreFeaturesEnabled))
            .Invoke([wave])
            .ShouldBe(IntrinsicFunctions.AreFeaturesEnabled(wave));
    }

    [Fact]
    public void IntrinsicFunctions_BitwiseAnd()
        => StaticMember(nameof(IntrinsicFunctions.BitwiseAnd))
            .Invoke([6, 3])
            .ShouldBe(2);

    [Fact]
    public void IntrinsicFunctions_BitwiseNot()
        => StaticMember(nameof(IntrinsicFunctions.BitwiseNot))
            .Invoke([6])
            .ShouldBe(~6);

    [Fact]
    public void IntrinsicFunctions_BitwiseOr()
        => StaticMember(nameof(IntrinsicFunctions.BitwiseOr))
            .Invoke([4, 2])
            .ShouldBe(6);

    [Fact]
    public void IntrinsicFunctions_BitwiseXor()
        => StaticMember(nameof(IntrinsicFunctions.BitwiseXor))
            .Invoke([6, 3])
            .ShouldBe(5);

    [Theory]
    [InlineData(nameof(IntrinsicFunctions.BitwiseAnd))]
    [InlineData(nameof(IntrinsicFunctions.BitwiseOr))]
    [InlineData(nameof(IntrinsicFunctions.BitwiseXor))]
    [InlineData(nameof(IntrinsicFunctions.LeftShift))]
    [InlineData(nameof(IntrinsicFunctions.RightShift))]
    [InlineData(nameof(IntrinsicFunctions.RightShiftUnsigned))]
    public void IntrinsicFunctions_BinaryInteger_InvalidArgument_NotHandled(string memberName)
        => StaticMember(memberName)
            .NotHandled(["invalid", 1]);

    [Fact]
    public void IntrinsicFunctions_BitwiseNot_InvalidArgument_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.BitwiseNot))
            .NotHandled(["invalid"]);

    [Fact]
    public void IntrinsicFunctions_CheckFeatureAvailability()
        => StaticMember(nameof(IntrinsicFunctions.CheckFeatureAvailability))
            .Invoke(["UnrecognizedFeature"])
            .ShouldBe(IntrinsicFunctions.CheckFeatureAvailability("UnrecognizedFeature"));

    [Fact]
    public void IntrinsicFunctions_ConvertFromBase64()
        => StaticMember(nameof(IntrinsicFunctions.ConvertFromBase64))
            .Invoke(["YWJj"])
            .ShouldBe("abc");

    [Fact]
    public void IntrinsicFunctions_ConvertToBase64()
        => StaticMember(nameof(IntrinsicFunctions.ConvertToBase64))
            .Invoke(["abc"])
            .ShouldBe("YWJj");

    [Fact]
    public void IntrinsicFunctions_DirectoryExists()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        TransientTestFolder folder = env.CreateFolder();

        StaticMember(nameof(IntrinsicFunctions.DirectoryExists))
            .Invoke([folder.Path])
            .ShouldBe(true);
    }

    [Fact]
    public void IntrinsicFunctions_DirectoryExists_MissingDirectory()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        TransientTestFolder folder = env.CreateFolder();
        string missingDirectory = Path.Combine(folder.Path, "missing");

        StaticMember(nameof(IntrinsicFunctions.DirectoryExists))
            .Invoke([missingDirectory])
            .ShouldBe(false);
    }

    [Fact]
    public void IntrinsicFunctions_DoesTaskHostExist_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.DoesTaskHostExist))
            .NotHandled(["CurrentRuntime", "CurrentArchitecture"]);

    [Fact]
    public void IntrinsicFunctions_EnsureTrailingSlash()
        => StaticMember(nameof(IntrinsicFunctions.EnsureTrailingSlash))
            .Invoke(["abc"])
            .ShouldBe(IntrinsicFunctions.EnsureTrailingSlash("abc"));

    [Fact]
    public void IntrinsicFunctions_Escape()
        => StaticMember(nameof(IntrinsicFunctions.Escape))
            .Invoke(["a;b"])
            .ShouldBe(IntrinsicFunctions.Escape("a;b"));

    [Fact]
    public void IntrinsicFunctions_FileExists()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        TransientTestFile file = env.CreateFile(env.CreateFolder(), "exists.txt");

        StaticMember(nameof(IntrinsicFunctions.FileExists))
            .Invoke([file.Path])
            .ShouldBe(true);
    }

    [Fact]
    public void IntrinsicFunctions_FileExists_MissingFile()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        TransientTestFolder folder = env.CreateFolder();
        string missingFile = Path.Combine(folder.Path, "missing.txt");

        StaticMember(nameof(IntrinsicFunctions.FileExists))
            .Invoke([missingFile])
            .ShouldBe(false);
    }

    [Fact]
    public void IntrinsicFunctions_FilterTargetFrameworks_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.FilterTargetFrameworks))
            .NotHandled(["net8.0;net9.0", "net9.0"]);

    [Fact]
    public void IntrinsicFunctions_GetCurrentToolsDirectory()
        => StaticMember(nameof(IntrinsicFunctions.GetCurrentToolsDirectory))
            .Invoke()
            .ShouldBe(IntrinsicFunctions.GetCurrentToolsDirectory());

    [Fact]
    public void IntrinsicFunctions_GetDirectoryNameOfFileAbove()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        TransientTestFolder root = env.CreateFolder();
        string child = Path.Combine(root.Path, "child");
        Directory.CreateDirectory(child);
        env.CreateFile(root, "marker.txt");

        StaticMember(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove))
            .Invoke([child, "marker.txt"])
            .ShouldBe(IntrinsicFunctions.GetDirectoryNameOfFileAbove(child, "marker.txt", FileSystems.Default));
    }

    [Fact]
    public void IntrinsicFunctions_GetMSBuildExtensionsPath()
        => StaticMember(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath))
            .Invoke()
            .ShouldBe(IntrinsicFunctions.GetMSBuildExtensionsPath());

    [Fact]
    public void IntrinsicFunctions_GetMSBuildSDKsPath()
        => StaticMember(nameof(IntrinsicFunctions.GetMSBuildSDKsPath))
            .Invoke()
            .ShouldBe(IntrinsicFunctions.GetMSBuildSDKsPath());

    [Fact]
    public void IntrinsicFunctions_GetPathOfFileAbove()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        TransientTestFolder root = env.CreateFolder();
        string child = Path.Combine(root.Path, "child");
        Directory.CreateDirectory(child);
        env.CreateFile(root, "marker.txt");

        StaticMember(nameof(IntrinsicFunctions.GetPathOfFileAbove))
            .Invoke(["marker.txt", child])
            .ShouldBe(IntrinsicFunctions.GetPathOfFileAbove("marker.txt", child, FileSystems.Default));
    }

    [Fact]
    public void IntrinsicFunctions_GetProgramFiles32()
        => StaticMember(nameof(IntrinsicFunctions.GetProgramFiles32))
            .Invoke()
            .ShouldBe(IntrinsicFunctions.GetProgramFiles32());

    [Fact]
    public void IntrinsicFunctions_GetRegistryValue_TwoArguments_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.GetRegistryValue))
            .NotHandled(
                [
                    @"HKEY_CURRENT_USER\Software\Microsoft\MSBuildUnitTests\Missing",
                    "Missing",
                ]);

    [Fact]
    public void IntrinsicFunctions_GetRegistryValue_ThreeArguments_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.GetRegistryValue))
            .NotHandled(
                [
                    @"HKEY_CURRENT_USER\Software\Microsoft\MSBuildUnitTests\Missing",
                    "Missing",
                    "fallback",
                ]);

    [Fact]
    public void IntrinsicFunctions_GetRegistryValueFromView()
        => StaticMember(nameof(IntrinsicFunctions.GetRegistryValueFromView))
            .Invoke(
                [
                    @"HKEY_CURRENT_USER\Software\Microsoft\MSBuildUnitTests\Missing",
                    "Missing",
                    "fallback",
                    "Default",
                ])
            .ShouldBe("fallback");

    [Fact]
    public void IntrinsicFunctions_GetTargetFrameworkIdentifier()
        => StaticMember(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier))
            .Invoke(["net8.0-windows10.0"])
            .ShouldBe(IntrinsicFunctions.GetTargetFrameworkIdentifier("net8.0-windows10.0"));

    [Fact]
    public void IntrinsicFunctions_GetTargetFrameworkVersion_OneArgument()
        => StaticMember(nameof(IntrinsicFunctions.GetTargetFrameworkVersion))
            .Invoke(["net8.0"])
            .ShouldBe(IntrinsicFunctions.GetTargetFrameworkVersion("net8.0"));

    [Fact]
    public void IntrinsicFunctions_GetTargetFrameworkVersion_TwoArguments()
        => StaticMember(nameof(IntrinsicFunctions.GetTargetFrameworkVersion))
            .Invoke(["net8.0", "1"])
            .ShouldBe(IntrinsicFunctions.GetTargetFrameworkVersion("net8.0", 1));

    [Fact]
    public void IntrinsicFunctions_GetTargetPlatformIdentifier()
        => StaticMember(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier))
            .Invoke(["net8.0-windows10.0"])
            .ShouldBe(IntrinsicFunctions.GetTargetPlatformIdentifier("net8.0-windows10.0"));

    [Fact]
    public void IntrinsicFunctions_GetTargetPlatformVersion_OneArgument()
        => StaticMember(nameof(IntrinsicFunctions.GetTargetPlatformVersion))
            .Invoke(["net8.0-windows10.0"])
            .ShouldBe(IntrinsicFunctions.GetTargetPlatformVersion("net8.0-windows10.0"));

    [Fact]
    public void IntrinsicFunctions_GetTargetPlatformVersion_TwoArguments()
        => StaticMember(nameof(IntrinsicFunctions.GetTargetPlatformVersion))
            .Invoke(["net8.0-windows10.0", "2"])
            .ShouldBe(IntrinsicFunctions.GetTargetPlatformVersion("net8.0-windows10.0", 2));

    [Fact]
    public void IntrinsicFunctions_GetToolsDirectory32()
        => StaticMember(nameof(IntrinsicFunctions.GetToolsDirectory32))
            .Invoke()
            .ShouldBe(IntrinsicFunctions.GetToolsDirectory32());

    [Fact]
    public void IntrinsicFunctions_GetToolsDirectory64()
        => StaticMember(nameof(IntrinsicFunctions.GetToolsDirectory64))
            .Invoke()
            .ShouldBe(IntrinsicFunctions.GetToolsDirectory64());

    [Fact]
    public void IntrinsicFunctions_GetVsInstallRoot()
        => StaticMember(nameof(IntrinsicFunctions.GetVsInstallRoot))
            .Invoke()
            .ShouldBe(IntrinsicFunctions.GetVsInstallRoot());

    [Fact]
    public void IntrinsicFunctions_IsOSPlatform()
        => StaticMember(nameof(IntrinsicFunctions.IsOSPlatform))
            .Invoke(["windows"])
            .ShouldBe(IntrinsicFunctions.IsOSPlatform("windows"));

    [Fact]
    public void IntrinsicFunctions_IsOsBsdLike_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.IsOsBsdLike))
            .NotHandled();

    [Fact]
    public void IntrinsicFunctions_IsOsUnixLike_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.IsOsUnixLike))
            .NotHandled();

    [Fact]
    public void IntrinsicFunctions_IsRunningFromVisualStudio()
        => StaticMember(nameof(IntrinsicFunctions.IsRunningFromVisualStudio))
            .Invoke()
            .ShouldBe(IntrinsicFunctions.IsRunningFromVisualStudio());

    [Fact]
    public void IntrinsicFunctions_IsTargetFrameworkCompatible()
        => StaticMember(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible))
            .Invoke(["net8.0", "net6.0"])
            .ShouldBe(IntrinsicFunctions.IsTargetFrameworkCompatible("net8.0", "net6.0"));

    [Fact]
    public void IntrinsicFunctions_LeftShift()
        => StaticMember(nameof(IntrinsicFunctions.LeftShift))
            .Invoke([1, 3])
            .ShouldBe(8);

    [Fact]
    public void IntrinsicFunctions_MakeRelative_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.MakeRelative))
            .NotHandled([Path.GetPathRoot(Directory.GetCurrentDirectory())!, "relative"]);

    [Fact]
    public void IntrinsicFunctions_NormalizeDirectory()
        => StaticMember(nameof(IntrinsicFunctions.NormalizeDirectory))
            .Invoke(["a"])
            .ShouldBe(IntrinsicFunctions.NormalizeDirectory("a"));

    [Fact]
    public void IntrinsicFunctions_NormalizeDirectory_NonString_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.NormalizeDirectory))
            .NotHandled(["a", 1]);

    [Fact]
    public void IntrinsicFunctions_NormalizePath()
        => StaticMember(nameof(IntrinsicFunctions.NormalizePath))
            .Invoke(["a", "b"])
            .ShouldBe(IntrinsicFunctions.NormalizePath("a", "b"));

    [Fact]
    public void IntrinsicFunctions_NormalizePath_NonString_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.NormalizePath))
            .NotHandled(["a", 1]);

    [Theory]
    [InlineData(nameof(IntrinsicFunctions.GetCurrentToolsDirectory))]
    [InlineData(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath))]
    [InlineData(nameof(IntrinsicFunctions.GetMSBuildSDKsPath))]
    [InlineData(nameof(IntrinsicFunctions.GetProgramFiles32))]
    [InlineData(nameof(IntrinsicFunctions.GetToolsDirectory32))]
    [InlineData(nameof(IntrinsicFunctions.GetToolsDirectory64))]
    [InlineData(nameof(IntrinsicFunctions.GetVsInstallRoot))]
    [InlineData(nameof(IntrinsicFunctions.IsRunningFromVisualStudio))]
    public void IntrinsicFunctions_ParameterlessMember_Arguments_NotHandled(string memberName)
        => StaticMember(memberName)
            .NotHandled(["argument"]);

    [Fact]
    public void IntrinsicFunctions_RegisterBuildCheck()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        var (logger, loggingContext) = ExpansionHelpers.CreateLoggingContext(Output);
        TransientTestFolder folder = env.CreateFolder();
        TransientTestFile assemblyFile = env.CreateFile(folder, "check.dll");
        TransientTestFile projectFile = env.CreateFile(folder, "project.proj", "<Project />");
        ProjectInstance project = new(projectFile.Path);

        StaticMember(nameof(IntrinsicFunctions.RegisterBuildCheck))
            .Invoke([assemblyFile.Path], project, loggingContext)
            .ShouldBe(true);

        logger.AllBuildEvents.ShouldHaveSingleItem().ShouldBeOfType<BuildCheckAcquisitionEventArgs>();
    }

    [Fact]
    public void IntrinsicFunctions_RightShift()
        => StaticMember(nameof(IntrinsicFunctions.RightShift))
            .Invoke([8, 1])
            .ShouldBe(4);

    [Fact]
    public void IntrinsicFunctions_RightShiftUnsigned()
        => StaticMember(nameof(IntrinsicFunctions.RightShiftUnsigned))
            .Invoke([-8, 1])
            .ShouldBe(-8 >>> 1);

    [Fact]
    public void IntrinsicFunctions_StableStringHash_OneArgument()
        => StaticMember(nameof(IntrinsicFunctions.StableStringHash))
            .Invoke(["abc"])
            .ShouldBe(IntrinsicFunctions.StableStringHash("abc"));

    [Fact]
    public void IntrinsicFunctions_StableStringHash_TwoArguments()
        => StaticMember(nameof(IntrinsicFunctions.StableStringHash))
            .Invoke(["abc", "Sha256"])
            .ShouldBe(IntrinsicFunctions.StableStringHash("abc", IntrinsicFunctions.StringHashingAlgorithm.Sha256));

    [Fact]
    public void IntrinsicFunctions_StableStringHash_InvalidAlgorithm_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.StableStringHash))
            .NotHandled(["abc", "InvalidAlgorithm"]);

    [Theory]
    [InlineData(nameof(IntrinsicFunctions.CheckFeatureAvailability))]
    [InlineData(nameof(IntrinsicFunctions.ConvertFromBase64))]
    [InlineData(nameof(IntrinsicFunctions.ConvertToBase64))]
    [InlineData(nameof(IntrinsicFunctions.DirectoryExists))]
    [InlineData(nameof(IntrinsicFunctions.EnsureTrailingSlash))]
    [InlineData(nameof(IntrinsicFunctions.Escape))]
    [InlineData(nameof(IntrinsicFunctions.FileExists))]
    [InlineData(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier))]
    [InlineData(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier))]
    [InlineData(nameof(IntrinsicFunctions.IsOSPlatform))]
    [InlineData(nameof(IntrinsicFunctions.NormalizeDirectory))]
    [InlineData(nameof(IntrinsicFunctions.NormalizePath))]
    [InlineData(nameof(IntrinsicFunctions.StableStringHash))]
    [InlineData(nameof(IntrinsicFunctions.Unescape))]
    public void IntrinsicFunctions_StringMember_NonString_NotHandled(string memberName)
        => StaticMember(memberName)
            .NotHandled([1]);

    [Fact]
    public void IntrinsicFunctions_SubstringByAsciiChars()
        => StaticMember(nameof(IntrinsicFunctions.SubstringByAsciiChars))
            .Invoke(["abc", "1", "2"])
            .ShouldBe("bc");

    [Fact]
    public void IntrinsicFunctions_SubstringByAsciiChars_InvalidIndex_NotHandled()
        => StaticMember(nameof(IntrinsicFunctions.SubstringByAsciiChars))
            .NotHandled(["abc", "invalid", "2"]);

    [Fact]
    public void IntrinsicFunctions_Unescape()
        => StaticMember(nameof(IntrinsicFunctions.Unescape))
            .Invoke(["%3b"])
            .ShouldBe(";");

    [Fact]
    public void IntrinsicFunctions_ValueOrDefault()
        => StaticMember(nameof(IntrinsicFunctions.ValueOrDefault))
            .Invoke(["", "default"])
            .ShouldBe("default");

    [Fact]
    public void IntrinsicFunctions_ValueOrDefault_NonEmptyValue()
        => StaticMember(nameof(IntrinsicFunctions.ValueOrDefault))
            .Invoke(["value", "default"])
            .ShouldBe("value");

    [Theory]
    [InlineData("1.0", "1.0.0", true)]
    [InlineData("1.0", "2.0", false)]
    public void IntrinsicFunctions_VersionEquals(string left, string right, bool expected)
        => StaticMember(nameof(IntrinsicFunctions.VersionEquals))
            .Invoke([left, right])
            .ShouldBe(expected);

    [Theory]
    [InlineData("2.0", "1.0", true)]
    [InlineData("1.0", "2.0", false)]
    public void IntrinsicFunctions_VersionGreaterThan(string left, string right, bool expected)
        => StaticMember(nameof(IntrinsicFunctions.VersionGreaterThan))
            .Invoke([left, right])
            .ShouldBe(expected);

    [Theory]
    [InlineData("1.0", "1.0", true)]
    [InlineData("1.0", "2.0", false)]
    public void IntrinsicFunctions_VersionGreaterThanOrEquals(string left, string right, bool expected)
        => StaticMember(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals))
            .Invoke([left, right])
            .ShouldBe(expected);

    [Theory]
    [InlineData("1.0", "2.0", true)]
    [InlineData("2.0", "1.0", false)]
    public void IntrinsicFunctions_VersionLessThan(string left, string right, bool expected)
        => StaticMember(nameof(IntrinsicFunctions.VersionLessThan))
            .Invoke([left, right])
            .ShouldBe(expected);

    [Theory]
    [InlineData("1.0", "1.0", true)]
    [InlineData("2.0", "1.0", false)]
    public void IntrinsicFunctions_VersionLessThanOrEquals(string left, string right, bool expected)
        => StaticMember(nameof(IntrinsicFunctions.VersionLessThanOrEquals))
            .Invoke([left, right])
            .ShouldBe(expected);

    [Theory]
    [InlineData("1.0", "2.0", true)]
    [InlineData("1.0", "1.0.0", false)]
    public void IntrinsicFunctions_VersionNotEquals(string left, string right, bool expected)
        => StaticMember(nameof(IntrinsicFunctions.VersionNotEquals))
            .Invoke([left, right])
            .ShouldBe(expected);
}
