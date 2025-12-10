// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class WellKnownFunctionsMethodDispatchBenchmark
{
    // Simulate common string method names from WellKnownFunctions
    private static readonly string[] s_commonMethodNames =
    [
        // String methods
        "Contains",
        "StartsWith",
        "EndsWith",
        "Substring",
        "IndexOf",
        "LastIndexOf",
        "ToLower",
        "ToUpper",
        "Trim",
        "TrimStart",
        "TrimEnd",
        "Replace",
        "Split",
        "Join",
        "Format",
        "IsNullOrEmpty",
        "IsNullOrWhiteSpace",
        "Concat",
        "Compare",
        "Equals",

        // Intrinsic function methods
        "EnsureTrailingSlash",
        "ValueOrDefault",
        "NormalizePath",
        "GetDirectoryNameOfFileAbove",
        "GetRegistryValueFromView",
        "IsRunningFromVisualStudio",
        "Escape",
        "Unescape",
        "GetPathOfFileAbove",
        "Add",
        "Subtract",
        "Multiply",
        "Divide",
        "Modulo",
        "GetCurrentToolsDirectory",
        "GetToolsDirectory32",
        "GetToolsDirectory64",
        "GetMSBuildSDKsPath",
        "GetVsInstallRoot",
        "GetMSBuildExtensionsPath",
        "GetProgramFiles32",
        "VersionEquals",
        "VersionNotEquals",
        "VersionGreaterThan",
        "VersionGreaterThanOrEquals",
        "VersionLessThan",
        "VersionLessThanOrEquals",
        "GetTargetFrameworkIdentifier",
        "GetTargetFrameworkVersion",
        "IsTargetFrameworkCompatible",
        "GetTargetPlatformIdentifier",
        "GetTargetPlatformVersion",
        "ConvertToBase64",
        "ConvertFromBase64",
        "StableStringHash",
        "AreFeaturesEnabled",
        "SubstringByAsciiChars",
        "CheckFeatureAvailability",
        "BitwiseOr",
        "BitwiseAnd",
        "BitwiseXor",
        "BitwiseNot",
        "LeftShift",
        "RightShift",
        "RightShiftUnsigned",
        "NormalizeDirectory",
        "IsOSPlatform",
        "FileExists",
        "DirectoryExists",
        "RegisterBuildCheck",
    ];

    private static readonly FrozenDictionary<string, int> s_methodLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        // String methods
        { "Contains", 1 },
        { "StartsWith", 2 },
        { "EndsWith", 3 },
        { "Substring", 4 },
        { "IndexOf", 5 },
        { "LastIndexOf", 6 },
        { "ToLower", 7 },
        { "ToUpper", 8 },
        { "Trim", 9 },
        { "TrimStart", 10 },
        { "TrimEnd", 11 },
        { "Replace", 12 },
        { "Split", 13 },
        { "Join", 14 },
        { "Format", 15 },
        { "IsNullOrEmpty", 16 },
        { "IsNullOrWhiteSpace", 17 },
        { "Concat", 18 },
        { "Compare", 19 },
        { "Equals", 20 },

        // Intrinsic function methods
        { "EnsureTrailingSlash", 21 },
        { "ValueOrDefault", 22 },
        { "NormalizePath", 23 },
        { "GetDirectoryNameOfFileAbove", 24 },
        { "GetRegistryValueFromView", 25 },
        { "IsRunningFromVisualStudio", 26 },
        { "Escape", 27 },
        { "Unescape", 28 },
        { "GetPathOfFileAbove", 29 },
        { "Add", 30 },
        { "Subtract", 31 },
        { "Multiply", 32 },
        { "Divide", 33 },
        { "Modulo", 34 },
        { "GetCurrentToolsDirectory", 35 },
        { "GetToolsDirectory32", 36 },
        { "GetToolsDirectory64", 37 },
        { "GetMSBuildSDKsPath", 38 },
        { "GetVsInstallRoot", 39 },
        { "GetMSBuildExtensionsPath", 40 },
        { "GetProgramFiles32", 41 },
        { "VersionEquals", 42 },
        { "VersionNotEquals", 43 },
        { "VersionGreaterThan", 44 },
        { "VersionGreaterThanOrEquals", 45 },
        { "VersionLessThan", 46 },
        { "VersionLessThanOrEquals", 47 },
        { "GetTargetFrameworkIdentifier", 48 },
        { "GetTargetFrameworkVersion", 49 },
        { "IsTargetFrameworkCompatible", 50 },
        { "GetTargetPlatformIdentifier", 51 },
        { "GetTargetPlatformVersion", 52 },
        { "ConvertToBase64", 53 },
        { "ConvertFromBase64", 54 },
        { "StableStringHash", 55 },
        { "AreFeaturesEnabled", 56 },
        { "SubstringByAsciiChars", 57 },
        { "CheckFeatureAvailability", 58 },
        { "BitwiseOr", 59 },
        { "BitwiseAnd", 60 },
        { "BitwiseXor", 61 },
        { "BitwiseNot", 62 },
        { "LeftShift", 63 },
        { "RightShift", 64 },
        { "RightShiftUnsigned", 65 },
        { "NormalizeDirectory", 66 },
        { "IsOSPlatform", 67 },
        { "FileExists", 68 },
        { "DirectoryExists", 69 },
        { "RegisterBuildCheck", 70 },
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly Random _random = new(42);

    private string[] _methodNames = [];

    [Params(0, 5)]
    public int UnknownEvery { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _methodNames = new string[2000];

        for (int i = 0; i < _methodNames.Length; i++)
        {
            bool emitUnknown = UnknownEvery > 0 && (i + 1) % UnknownEvery == 0;

            if (emitUnknown)
            {
                _methodNames[i] = GetUnknownMethodName();
            }
            else
            {
                // Use the method name with random casing to test OrdinalIgnoreCase behavior
                string methodName = s_commonMethodNames[i % s_commonMethodNames.Length];
                _methodNames[i] = RandomizeCase(methodName);
            }
        }
    }

    [Benchmark(Baseline = true)]
    public int FrozenDictionaryLookup()
    {
        int hits = 0;

        foreach (string methodName in _methodNames)
        {
            if (s_methodLookup.TryGetValue(methodName, out int methodId))
            {
                hits += methodId;
            }
        }

        return hits;
    }

    [Benchmark]
    public int DirectStringChecks()
    {
        int hits = 0;

        foreach (string methodName in _methodNames)
        {
            if (TryGetMethodId(methodName, out int methodId))
            {
                hits += methodId;
            }
        }

        return hits;
    }

    private static bool TryGetMethodId(string methodName, out int methodId)
    {
        // String methods
        if (string.Equals(methodName, "Contains", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 1;
            return true;
        }

        if (string.Equals(methodName, "StartsWith", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 2;
            return true;
        }

        if (string.Equals(methodName, "EndsWith", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 3;
            return true;
        }

        if (string.Equals(methodName, "Substring", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 4;
            return true;
        }

        if (string.Equals(methodName, "IndexOf", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 5;
            return true;
        }

        if (string.Equals(methodName, "LastIndexOf", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 6;
            return true;
        }

        if (string.Equals(methodName, "ToLower", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 7;
            return true;
        }

        if (string.Equals(methodName, "ToUpper", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 8;
            return true;
        }

        if (string.Equals(methodName, "Trim", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 9;
            return true;
        }

        if (string.Equals(methodName, "TrimStart", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 10;
            return true;
        }

        if (string.Equals(methodName, "TrimEnd", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 11;
            return true;
        }

        if (string.Equals(methodName, "Replace", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 12;
            return true;
        }

        if (string.Equals(methodName, "Split", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 13;
            return true;
        }

        if (string.Equals(methodName, "Join", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 14;
            return true;
        }

        if (string.Equals(methodName, "Format", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 15;
            return true;
        }

        if (string.Equals(methodName, "IsNullOrEmpty", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 16;
            return true;
        }

        if (string.Equals(methodName, "IsNullOrWhiteSpace", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 17;
            return true;
        }

        if (string.Equals(methodName, "Concat", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 18;
            return true;
        }

        if (string.Equals(methodName, "Compare", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 19;
            return true;
        }

        if (string.Equals(methodName, "Equals", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 20;
            return true;
        }

        // Intrinsic function methods
        if (string.Equals(methodName, "EnsureTrailingSlash", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 21;
            return true;
        }

        if (string.Equals(methodName, "ValueOrDefault", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 22;
            return true;
        }

        if (string.Equals(methodName, "NormalizePath", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 23;
            return true;
        }

        if (string.Equals(methodName, "GetDirectoryNameOfFileAbove", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 24;
            return true;
        }

        if (string.Equals(methodName, "GetRegistryValueFromView", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 25;
            return true;
        }

        if (string.Equals(methodName, "IsRunningFromVisualStudio", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 26;
            return true;
        }

        if (string.Equals(methodName, "Escape", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 27;
            return true;
        }

        if (string.Equals(methodName, "Unescape", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 28;
            return true;
        }

        if (string.Equals(methodName, "GetPathOfFileAbove", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 29;
            return true;
        }

        if (string.Equals(methodName, "Add", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 30;
            return true;
        }

        if (string.Equals(methodName, "Subtract", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 31;
            return true;
        }

        if (string.Equals(methodName, "Multiply", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 32;
            return true;
        }

        if (string.Equals(methodName, "Divide", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 33;
            return true;
        }

        if (string.Equals(methodName, "Modulo", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 34;
            return true;
        }

        if (string.Equals(methodName, "GetCurrentToolsDirectory", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 35;
            return true;
        }

        if (string.Equals(methodName, "GetToolsDirectory32", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 36;
            return true;
        }

        if (string.Equals(methodName, "GetToolsDirectory64", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 37;
            return true;
        }

        if (string.Equals(methodName, "GetMSBuildSDKsPath", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 38;
            return true;
        }

        if (string.Equals(methodName, "GetVsInstallRoot", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 39;
            return true;
        }

        if (string.Equals(methodName, "GetMSBuildExtensionsPath", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 40;
            return true;
        }

        if (string.Equals(methodName, "GetProgramFiles32", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 41;
            return true;
        }

        if (string.Equals(methodName, "VersionEquals", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 42;
            return true;
        }

        if (string.Equals(methodName, "VersionNotEquals", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 43;
            return true;
        }

        if (string.Equals(methodName, "VersionGreaterThan", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 44;
            return true;
        }

        if (string.Equals(methodName, "VersionGreaterThanOrEquals", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 45;
            return true;
        }

        if (string.Equals(methodName, "VersionLessThan", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 46;
            return true;
        }

        if (string.Equals(methodName, "VersionLessThanOrEquals", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 47;
            return true;
        }

        if (string.Equals(methodName, "GetTargetFrameworkIdentifier", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 48;
            return true;
        }

        if (string.Equals(methodName, "GetTargetFrameworkVersion", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 49;
            return true;
        }

        if (string.Equals(methodName, "IsTargetFrameworkCompatible", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 50;
            return true;
        }

        if (string.Equals(methodName, "GetTargetPlatformIdentifier", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 51;
            return true;
        }

        if (string.Equals(methodName, "GetTargetPlatformVersion", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 52;
            return true;
        }

        if (string.Equals(methodName, "ConvertToBase64", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 53;
            return true;
        }

        if (string.Equals(methodName, "ConvertFromBase64", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 54;
            return true;
        }

        if (string.Equals(methodName, "StableStringHash", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 55;
            return true;
        }

        if (string.Equals(methodName, "AreFeaturesEnabled", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 56;
            return true;
        }

        if (string.Equals(methodName, "SubstringByAsciiChars", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 57;
            return true;
        }

        if (string.Equals(methodName, "CheckFeatureAvailability", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 58;
            return true;
        }

        if (string.Equals(methodName, "BitwiseOr", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 59;
            return true;
        }

        if (string.Equals(methodName, "BitwiseAnd", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 60;
            return true;
        }

        if (string.Equals(methodName, "BitwiseXor", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 61;
            return true;
        }

        if (string.Equals(methodName, "BitwiseNot", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 62;
            return true;
        }

        if (string.Equals(methodName, "LeftShift", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 63;
            return true;
        }

        if (string.Equals(methodName, "RightShift", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 64;
            return true;
        }

        if (string.Equals(methodName, "RightShiftUnsigned", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 65;
            return true;
        }

        if (string.Equals(methodName, "NormalizeDirectory", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 66;
            return true;
        }

        if (string.Equals(methodName, "IsOSPlatform", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 67;
            return true;
        }

        if (string.Equals(methodName, "FileExists", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 68;
            return true;
        }

        if (string.Equals(methodName, "DirectoryExists", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 69;
            return true;
        }

        if (string.Equals(methodName, "RegisterBuildCheck", StringComparison.OrdinalIgnoreCase))
        {
            methodId = 70;
            return true;
        }

        methodId = default;
        return false;
    }

    private string RandomizeCase(string methodName)
    {
        // Randomly change casing to ensure OrdinalIgnoreCase is being tested
        int caseVariant = _random.Next(0, 4);

        return caseVariant switch
        {
            0 => methodName, // Original casing
            1 => methodName.ToLowerInvariant(), // all lowercase
            2 => methodName.ToUpperInvariant(), // ALL UPPERCASE
            _ => RandomizeCasePerChar(methodName), // rAnDoM cAsInG
        };
    }

    private string RandomizeCasePerChar(string input)
    {
        char[] chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = _random.Next(0, 2) == 0
                ? char.ToLowerInvariant(chars[i])
                : char.ToUpperInvariant(chars[i]);
        }

        return new string(chars);
    }

    private string GetUnknownMethodName()
    {
        // Mix a few different unknown method names to avoid biasing caches on a single missing key.
        int pick = _random.Next(0, 3);

        return pick switch
        {
            0 => "PadLeft",
            1 => "PadRight",
            _ => "Remove",
        };
    }
}
