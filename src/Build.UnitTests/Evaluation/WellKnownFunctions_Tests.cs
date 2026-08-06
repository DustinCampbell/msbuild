// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.Evaluation;

public class WellKnownFunctions_Tests
{
    [Theory]
    [InlineData("StartsWith", "abca", "a", null, "True")]
    [InlineData("Replace", "abca", "a", "x", "xbcx")]
    [InlineData("Contains", "abca", "bc", null, "True")]
    [InlineData("ToUpperInvariant", "abca", null, null, "ABCA")]
    [InlineData("ToLowerInvariant", "ABCA", null, null, "abca")]
    [InlineData("EndsWith", "abca", "a", null, "True")]
    [InlineData("EndsWith", "abca", "A", "OrdinalIgnoreCase", "True")]
    [InlineData("ToLower", "ABCA", null, null, "abca")]
    [InlineData("IndexOf", "abca", "BC", "OrdinalIgnoreCase", "1")]
    [InlineData("IndexOfAny", "abca", "cx", null, "2")]
    [InlineData("LastIndexOf", "abca", "a", null, "3")]
    [InlineData("LastIndexOf", "abca", "a", "2", "0")]
    [InlineData("LastIndexOf", "abca", "A", "OrdinalIgnoreCase", "3")]
    [InlineData("LastIndexOfAny", "abca", "ax", null, "3")]
    [InlineData("Length", "abca", null, null, "4")]
    [InlineData("Substring", "abca", "1", null, "bca")]
    [InlineData("Substring", "abca", "1", "2", "bc")]
    [InlineData("Split", "a,b", ",", null, "a|b")]
    [InlineData("PadLeft", "4", "3", null, "  4")]
    [InlineData("PadLeft", "4", "3", "0", "004")]
    [InlineData("PadRight", "4", "3", null, "4  ")]
    [InlineData("PadRight", "4", "3", "0", "400")]
    [InlineData("TrimStart", "aabca", "a", null, "bca")]
    [InlineData("TrimEnd", "abcaa", "a", null, "abc")]
    [InlineData("get_Chars", "abca", "1", null, "b")]
    [InlineData("Equals", "abca", "abca", null, "True")]
    public void ExecutesEveryWellKnownStringFunction(
        string methodName,
        string instance,
        string? argument0,
        string? argument1,
        string expected)
    {
        Format(Execute(typeof(string), methodName, instance, Arguments(argument0, argument1))).ShouldBe(expected);
    }

    [Fact]
    public void ExecutesEveryOtherRuntimeWellKnownFunction()
    {
        Format(Execute(typeof(string), nameof(string.IsNullOrWhiteSpace), null, [" "])).ShouldBe("True");
        Format(Execute(typeof(string), nameof(string.IsNullOrEmpty), null, [string.Empty])).ShouldBe("True");
        Format(Execute(typeof(string), nameof(string.Copy), null, ["copy"])).ShouldBe("copy");
        Format(Execute(typeof(string), nameof(string.Concat), null, ["left", "right"])).ShouldBe("leftright");

        Format(Execute(typeof(Math), nameof(Math.Max), null, ["123", "456"])).ShouldBe("456");
        Format(Execute(typeof(Math), nameof(Math.Min), null, ["123", "456"])).ShouldBe("123");

        Format(Execute(typeof(Version), nameof(Version.Parse), null, ["1.2.3"])).ShouldBe("1.2.3");
        Format(Execute(typeof(Version), nameof(Version.ToString), new Version(1, 2, 3), ["2"])).ShouldBe("1.2");
        Execute(typeof(Guid), nameof(Guid.NewGuid), null, []).ShouldBeOfType<Guid>();
        Format(Execute(typeof(char), nameof(char.IsDigit), null, ["4"])).ShouldBe("True");
        Format(Execute(typeof(char), nameof(char.IsDigit), null, ["a4", "1"])).ShouldBe("True");
        Format(Execute(typeof(Regex), nameof(Regex.Replace), null, ["abc123", "[0-9]+", ""])).ShouldBe("abc");
        Format(Execute(typeof(int), nameof(int.ToString), 42, ["D4"])).ShouldBe("0042");
        Format(Execute(typeof(string[]), "GetValue", new[] { "zero", "one" }, ["1"])).ShouldBe("one");

        Format(ExecuteConstructor(typeof(string), [])).ShouldBe(string.Empty);
        Format(ExecuteConstructor(typeof(string), ["value"])).ShouldBe("value");
    }

    [Fact]
    public void ExecutesEveryWellKnownPathFunction()
    {
        Format(Execute(typeof(Path), nameof(Path.Combine), null, ["a"])).ShouldBe(Path.Combine("a"));
        Format(Execute(typeof(Path), nameof(Path.Combine), null, ["a", "b"])).ShouldBe(Path.Combine("a", "b"));
        Format(Execute(typeof(Path), nameof(Path.Combine), null, ["a", "b", "c"])).ShouldBe(Path.Combine("a", "b", "c"));
        Format(Execute(typeof(Path), nameof(Path.Combine), null, ["a", "b", "c", "d"])).ShouldBe(Path.Combine("a", "b", "c", "d"));
        Format(Execute(typeof(Path), nameof(Path.Combine), null, ["a", "b", "c", "d", "e"]))
            .ShouldBe(Path.Combine("a", "b", "c", "d", "e"));
        Format(Execute(typeof(Path), nameof(Path.DirectorySeparatorChar), null, [])).ShouldBe(Path.DirectorySeparatorChar.ToString());
        Format(Execute(typeof(Path), nameof(Path.GetFullPath), null, ["."])).ShouldBe(Path.GetFullPath("."));
        Format(Execute(typeof(Path), nameof(Path.IsPathRooted), null, [Path.GetPathRoot(Environment.CurrentDirectory)!])).ShouldBe("True");
        Format(Execute(typeof(Path), nameof(Path.GetTempPath), null, [])).ShouldBe(Path.GetTempPath());
        Format(Execute(typeof(Path), nameof(Path.GetFileName), null, [Path.Combine("folder", "file.txt")])).ShouldBe("file.txt");
        Format(Execute(typeof(Path), nameof(Path.GetDirectoryName), null, [Path.Combine("folder", "file.txt")])).ShouldBe("folder");
        Format(Execute(typeof(Path), nameof(Path.GetFileNameWithoutExtension), null, [Path.Combine("folder", "file.txt")])).ShouldBe("file");
    }

    [Fact]
    public void ExecutesEveryWellKnownIntrinsicFunction()
    {
        IFileSystem fileSystem = FileSystems.Default;
        string currentDirectory = Directory.GetCurrentDirectory();
        const string missingName = "__well_known_function_missing__";

        ExecuteIntrinsic(nameof(IntrinsicFunctions.EnsureTrailingSlash), ["folder"])
            .ShouldBe(IntrinsicFunctions.EnsureTrailingSlash("folder"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.ValueOrDefault), ["", "fallback"]).ShouldBe("fallback");
        ExecuteIntrinsic(nameof(IntrinsicFunctions.NormalizePath), ["."])
            .ShouldBe(IntrinsicFunctions.NormalizePath("."));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), [currentDirectory, missingName])
            .ShouldBe(IntrinsicFunctions.GetDirectoryNameOfFileAbove(currentDirectory, missingName, fileSystem));
        ExecuteIntrinsic(
                nameof(IntrinsicFunctions.GetRegistryValueFromView),
                [@"HKEY_CURRENT_USER\Software\MSBuild_Nonexistent", "Missing", "fallback", "Default"])
            .ShouldBe("fallback");
        ExecuteIntrinsic(nameof(IntrinsicFunctions.IsRunningFromVisualStudio), [])
            .ShouldBe(IntrinsicFunctions.IsRunningFromVisualStudio());
        ExecuteIntrinsic(nameof(IntrinsicFunctions.Escape), ["a;b"]).ShouldBe(IntrinsicFunctions.Escape("a;b"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.Unescape), ["a%3bb"]).ShouldBe("a;b");
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetPathOfFileAbove), [missingName, currentDirectory])
            .ShouldBe(IntrinsicFunctions.GetPathOfFileAbove(missingName, currentDirectory, fileSystem));

        ExecuteIntrinsic(nameof(IntrinsicFunctions.Add), ["40", "2"]).ShouldBe(42L);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.Subtract), ["44", "2"]).ShouldBe(42L);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.Multiply), ["21", "2"]).ShouldBe(42L);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.Divide), ["84", "2"]).ShouldBe(42L);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.Modulo), ["44", "2"]).ShouldBe(0L);

        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetCurrentToolsDirectory), [])
            .ShouldBe(IntrinsicFunctions.GetCurrentToolsDirectory());
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetToolsDirectory32), [])
            .ShouldBe(IntrinsicFunctions.GetToolsDirectory32());
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetToolsDirectory64), [])
            .ShouldBe(IntrinsicFunctions.GetToolsDirectory64());
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetMSBuildSDKsPath), [])
            .ShouldBe(IntrinsicFunctions.GetMSBuildSDKsPath());
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetVsInstallRoot), [])
            .ShouldBe(IntrinsicFunctions.GetVsInstallRoot());
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), [])
            .ShouldBe(IntrinsicFunctions.GetMSBuildExtensionsPath());
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetProgramFiles32), [])
            .ShouldBe(IntrinsicFunctions.GetProgramFiles32());

        ExecuteIntrinsic(nameof(IntrinsicFunctions.VersionEquals), ["1.2", "1.2"]).ShouldBe(true);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.VersionNotEquals), ["1.2", "1.3"]).ShouldBe(true);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.VersionGreaterThan), ["1.3", "1.2"]).ShouldBe(true);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), ["1.2", "1.2"]).ShouldBe(true);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.VersionLessThan), ["1.2", "1.3"]).ShouldBe(true);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.VersionLessThanOrEquals), ["1.2", "1.2"]).ShouldBe(true);

        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), ["net8.0"])
            .ShouldBe(IntrinsicFunctions.GetTargetFrameworkIdentifier("net8.0"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), ["net8.0"])
            .ShouldBe(IntrinsicFunctions.GetTargetFrameworkVersion("net8.0"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetTargetFrameworkVersion), ["net8.0", "3"])
            .ShouldBe(IntrinsicFunctions.GetTargetFrameworkVersion("net8.0", 3));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), ["net8.0", "net7.0"])
            .ShouldBe(IntrinsicFunctions.IsTargetFrameworkCompatible("net8.0", "net7.0"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), ["net8.0-windows10.0.19041.0"])
            .ShouldBe(IntrinsicFunctions.GetTargetPlatformIdentifier("net8.0-windows10.0.19041.0"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetTargetPlatformVersion), ["net8.0-windows10.0.19041.0"])
            .ShouldBe(IntrinsicFunctions.GetTargetPlatformVersion("net8.0-windows10.0.19041.0"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.GetTargetPlatformVersion), ["net8.0-windows10.0.19041.0", "3"])
            .ShouldBe(IntrinsicFunctions.GetTargetPlatformVersion("net8.0-windows10.0.19041.0", 3));

        ExecuteIntrinsic(nameof(IntrinsicFunctions.ConvertToBase64), ["hello"])
            .ShouldBe(IntrinsicFunctions.ConvertToBase64("hello"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.ConvertFromBase64), ["aGVsbG8="]).ShouldBe("hello");
        ExecuteIntrinsic(nameof(IntrinsicFunctions.StableStringHash), ["text"])
            .ShouldBe(IntrinsicFunctions.StableStringHash("text"));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.StableStringHash), ["text", "FNV1A32Bit"])
            .ShouldBe(IntrinsicFunctions.StableStringHash("text", IntrinsicFunctions.StringHashingAlgorithm.Fnv1a32bit));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.AreFeaturesEnabled), ["0.0"])
            .ShouldBe(IntrinsicFunctions.AreFeaturesEnabled(new Version(0, 0)));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.SubstringByAsciiChars), ["abcdef", "1", "3"]).ShouldBe("bcd");
        ExecuteIntrinsic(nameof(IntrinsicFunctions.CheckFeatureAvailability), ["UnknownFeature"])
            .ShouldBe(IntrinsicFunctions.CheckFeatureAvailability("UnknownFeature"));

        ExecuteIntrinsic(nameof(IntrinsicFunctions.BitwiseOr), ["40", "2"]).ShouldBe(42);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.BitwiseAnd), ["43", "42"]).ShouldBe(42);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.BitwiseXor), ["40", "2"]).ShouldBe(42);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.BitwiseNot), ["0"]).ShouldBe(-1);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.LeftShift), ["21", "1"]).ShouldBe(42);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.RightShift), ["84", "1"]).ShouldBe(42);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.RightShiftUnsigned), ["84", "1"]).ShouldBe(42);

        ExecuteIntrinsic(nameof(IntrinsicFunctions.NormalizeDirectory), ["."])
            .ShouldBe(IntrinsicFunctions.NormalizeDirectory("."));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.IsOSPlatform), [Environment.OSVersion.Platform.ToString()])
            .ShouldBe(IntrinsicFunctions.IsOSPlatform(Environment.OSVersion.Platform.ToString()));
        ExecuteIntrinsic(nameof(IntrinsicFunctions.FileExists), [Path.Combine(currentDirectory, missingName)]).ShouldBe(false);
        ExecuteIntrinsic(nameof(IntrinsicFunctions.DirectoryExists), [Path.Combine(currentDirectory, missingName)]).ShouldBe(false);
    }

    [Fact]
    public void RejectsInvalidArgumentsWithoutMaterializingForReflection()
    {
        TryExecute(typeof(string), nameof(string.StartsWith), "text", [], out _).ShouldBeFalse();
        TryExecute(typeof(string), nameof(string.Substring), "text", ["not-an-integer"], out _).ShouldBeFalse();
        TryExecute(typeof(string), nameof(string.EndsWith), "text", ["t", "4"], out _).ShouldBeFalse();
        TryExecute(typeof(string), nameof(string.Split), "text", ["too long"], out _).ShouldBeFalse();
        TryExecute(typeof(Path), nameof(Path.Combine), null, [], out _).ShouldBeFalse();
        TryExecute(typeof(Regex), nameof(Regex.Replace), null, ["input", "pattern"], out _).ShouldBeFalse();
        TryExecute(typeof(IntrinsicFunctions), nameof(IntrinsicFunctions.Add), null, ["1"], out _).ShouldBeFalse();
        TryExecute(typeof(string), "Unknown", null, [], out _).ShouldBeFalse();

        FunctionArguments constructorArguments = CreateArguments(typeof(string), "new", ["one", "two"]);
        WellKnownFunctions.TryExecuteWellKnownConstructorNoThrow(
            typeof(string),
            out _,
            ref constructorArguments).ShouldBeFalse();
    }

    [Fact]
    public void SupportsEscapedNestedTypedAndAppendedArguments()
    {
        FunctionArguments concatArguments = CreateArguments(typeof(string), nameof(string.Concat), ["unused", "suffix"]);
        concatArguments.SetExpandedValue(0, "left%3bright");
        Execute(typeof(string), nameof(string.Concat), null, ref concatArguments).ShouldBe("left;rightsuffix");

        FunctionArguments padArguments = CreateArguments(typeof(string), nameof(string.PadLeft), ["3", "unused"]);
        padArguments.SetExpandedValue(1, '0');
        Execute(typeof(string), nameof(string.PadLeft), "4", ref padArguments).ShouldBe("004");

        FunctionArguments appendedArguments = CreateArguments(typeof(IntrinsicFunctions), nameof(IntrinsicFunctions.GetPathOfFileAbove), ["missing"]);
        appendedArguments.AppendExpandedValue(Directory.GetCurrentDirectory());
        Execute(
            typeof(IntrinsicFunctions),
            nameof(IntrinsicFunctions.GetPathOfFileAbove),
            null,
            ref appendedArguments).ShouldBe(string.Empty);
    }

    private static object? ExecuteIntrinsic(string methodName, string[] arguments)
        => Execute(typeof(IntrinsicFunctions), methodName, null, arguments);

    private static object? Execute(Type receiverType, string methodName, object? instance, string[] arguments)
    {
        FunctionArguments functionArguments = CreateArguments(receiverType, methodName, arguments);
        return Execute(receiverType, methodName, instance, ref functionArguments);
    }

    private static object? Execute(
        Type receiverType,
        string methodName,
        object? instance,
        ref FunctionArguments arguments)
    {
        WellKnownFunctions.TryExecuteWellKnownFunction(
            methodName,
            receiverType,
            FileSystems.Default,
            out object? result,
            instance,
            ref arguments).ShouldBeTrue();

        return result;
    }

    private static bool TryExecute(
        Type receiverType,
        string methodName,
        object? instance,
        string[] arguments,
        out object? result)
    {
        FunctionArguments functionArguments = CreateArguments(receiverType, methodName, arguments);
        return WellKnownFunctions.TryExecuteWellKnownFunction(
            methodName,
            receiverType,
            FileSystems.Default,
            out result,
            instance,
            ref functionArguments);
    }

    private static object? ExecuteConstructor(Type receiverType, string[] arguments)
    {
        FunctionArguments functionArguments = CreateArguments(receiverType, "new", arguments);
        WellKnownFunctions.TryExecuteWellKnownConstructorNoThrow(
            receiverType,
            out object? result,
            ref functionArguments).ShouldBeTrue();
        return result;
    }

    private static FunctionArguments CreateArguments(Type receiverType, string methodName, string[] arguments)
    {
        ImmutableArray<StringSegment> expressions = [.. arguments];
        return new FunctionArguments(expressions, receiverType, methodName);
    }

    private static string[] Arguments(string? argument0, string? argument1)
    {
        if (argument0 is null)
        {
            return [];
        }

        return argument1 is null ? [argument0] : [argument0, argument1];
    }

    private static string Format(object? value)
        => value switch
        {
            null => "<null>",
            string[] values => string.Join("|", values),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()!,
        };
}
