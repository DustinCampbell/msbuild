// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public partial class ComparePropertyFunctionsBenchmark
{
    private static readonly IFileSystem s_fileSystem = FileSystems.Default;

    // Common test data
    private const string TestString = "MyTestString";
    private static readonly Version s_testVersion = new(1, 2, 3, 4);
    private const int TestInt = 42;

    // Benchmarks that measure ONLY the dispatch overhead by calling no-op functions
    // This isolates the lookup performance from the actual function execution cost

    [Benchmark(Description = "Original dispatch - first in chain")]
    public bool Original_Dispatch_First()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "StartsWith",
            typeof(string),
            s_fileSystem,
            out _,
            TestString,
            ["My"]); // string.StartsWith(string value)
    }

    [Benchmark(Description = "New dispatch - first in chain")]
    public bool New_Dispatch_First()
    {
        return WellKnownFunctions.TryExecute(
            typeof(string),
            "StartsWith",
            TestString,
            ["My"]) // string.StartsWith(string value)
            is (true, _);
    }

    [Benchmark(Description = "Original dispatch - last in chain")]
    public bool Original_Dispatch_Last()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "Equals",
            typeof(string),
            s_fileSystem,
            out _,
            TestString,
            [TestString]); // string.Equals(string value)
    }

    [Benchmark(Description = "New dispatch - last in chain")]
    public bool New_Dispatch_Last()
    {
        return WellKnownFunctions.TryExecute(
            typeof(string),
            "Equals",
            TestString,
            [TestString]) // string.Equals(string value)
            is (true, _);
    }

    [Benchmark(Description = "Original dispatch - intrinsic late")]
    public bool Original_Dispatch_IntrinsicLate()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "RightShiftUnsigned",
            typeof(IntrinsicFunctions),
            s_fileSystem,
            out _,
            null,
            [16, 2]); // IntrinsicFunctions.RightShiftUnsigned(int left, int right)
    }

    [Benchmark(Description = "New dispatch - intrinsic late")]
    public bool New_Dispatch_IntrinsicLate()
    {
        return WellKnownFunctions.TryExecute(
            typeof(IntrinsicFunctions),
            "RightShiftUnsigned",
            null,
            [16, 2]) // IntrinsicFunctions.RightShiftUnsigned(int left, int right)
            is (true, _);
    }

    [Benchmark(Description = "Original dispatch - very late")]
    public bool Original_Dispatch_VeryLate()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "IsOSPlatform",
            typeof(IntrinsicFunctions),
            s_fileSystem,
            out _,
            null,
            ["Windows"]); // IntrinsicFunctions.IsOSPlatform(string platformString)
    }

    [Benchmark(Description = "New dispatch - very late")]
    public bool New_Dispatch_VeryLate()
    {
        return WellKnownFunctions.TryExecute(
            typeof(IntrinsicFunctions),
            "IsOSPlatform",
            null,
            ["Windows"]) // IntrinsicFunctions.IsOSPlatform(string platformString)
            is (true, _);
    }

    [Benchmark(Description = "Original dispatch - instance method (Version)")]
    public bool Original_Dispatch_InstanceVersion()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "ToString",
            typeof(Version),
            s_fileSystem,
            out _,
            s_testVersion,
            [2]); // Version.ToString(int fieldCount)
    }

    [Benchmark(Description = "New dispatch - instance method (Version)")]
    public bool New_Dispatch_InstanceVersion()
    {
        return WellKnownFunctions.TryExecute(
            typeof(Version),
            "ToString",
            s_testVersion,
            [2]) // Version.ToString(int fieldCount)
            is (true, _);
    }

    [Benchmark(Description = "Original dispatch - instance method (Int)")]
    public bool Original_Dispatch_InstanceInt()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "ToString",
            typeof(int),
            s_fileSystem,
            out _,
            TestInt,
            ["X4"]); // int.ToString(string format)
    }

    [Benchmark(Description = "New dispatch - instance method (Int)")]
    public bool New_Dispatch_InstanceInt()
    {
        return WellKnownFunctions.TryExecute(
            typeof(int),
            "ToString",
            TestInt,
            ["X4"]) // int.ToString(string format)
            is (true, _);
    }

    // Benchmarks that measure BOTH dispatch + execution (original behavior)
    // These show the overall performance improvement

    [Benchmark(Description = "Original full - String.Equals")]
    public bool Original_Full_String_Equals()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "Equals",
            typeof(string),
            s_fileSystem,
            out _,
            TestString,
            [TestString]);
    }

    [Benchmark(Description = "New full - String.Equals")]
    public bool New_Full_String_Equals()
    {
        return WellKnownFunctions.TryExecute(
            typeof(string),
            "Equals",
            TestString, [TestString])
            is (true, _);
    }

    [Benchmark(Description = "Original full - RightShiftUnsigned")]
    public bool Original_Full_RightShiftUnsigned()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "RightShiftUnsigned",
            typeof(IntrinsicFunctions),
            s_fileSystem,
            out _,
            null,
            [16, 2]);
    }

    [Benchmark(Description = "New full - RightShiftUnsigned")]
    public bool New_Full_RightShiftUnsigned()
    {
        return WellKnownFunctions.TryExecute(
            typeof(IntrinsicFunctions),
            "RightShiftUnsigned",
            null,
            [16, 2])
            is (true, _);
    }

    [Benchmark(Description = "Original full - IsOSPlatform")]
    public bool Original_Full_IsOSPlatform()
    {
        return OriginalImplementation.WellKnownFunctions.TryExecuteWellKnownFunction(
            "IsOSPlatform",
            typeof(IntrinsicFunctions),
            s_fileSystem,
            out _,
            null,
            ["Windows"]);
    }

    [Benchmark(Description = "New full - IsOSPlatform")]
    public bool New_Full_IsOSPlatform()
    {
        return WellKnownFunctions.TryExecute(
            typeof(IntrinsicFunctions),
            "IsOSPlatform",
            null,
            ["Windows"])
            is (true, _);
    }
}
