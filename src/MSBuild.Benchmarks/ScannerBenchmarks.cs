// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class ScannerBenchmarks
{
    private const ParserOptions DefaultOptions = ParserOptions.AllowAll;

    // Simple expressions
    private const string SimpleComparison = "'Debug' == 'Debug'";
    private const string SimpleProperty = "$(Configuration) == 'Debug'";
    private const string SimpleNumeric = "1 > 0";

    // Complex expressions
    private const string ComplexCondition = "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU' and '$(TargetFramework)' != ''";
    private const string NestedProperties = "$(Prop1.StartsWith('$(Prop2)')) and '$(Prop3)' == '$(Prop4)'";
    private const string ItemMetadata = "%(Identity) != '' and %(FullPath) == '%(RootDir)%(Directory)%(Filename)%(Extension)'";
    private const string FunctionCall = "Exists('$(MSBuildProjectFile)') and HasTrailingSlash('$(OutputPath)')";

    // Real-world examples
    private const string TypicalSdkCondition = "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU' or '$(Configuration)|$(Platform)' == 'Release|AnyCPU'";
    private const string VersionCheck = "'$(TargetFrameworkVersion)' >= '4.7.2' and '$(LangVersion)' != 'latest'";

    [Benchmark]
    public int SimpleComparison_Scan()
    {
        var scanner = new Scanner(SimpleComparison, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark]
    public int SimpleProperty_Scan()
    {
        var scanner = new Scanner(SimpleProperty, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark]
    public int SimpleNumeric_Scan()
    {
        var scanner = new Scanner(SimpleNumeric, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark]
    public int ComplexCondition_Scan()
    {
        var scanner = new Scanner(ComplexCondition, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark]
    public int NestedProperties_Scan()
    {
        var scanner = new Scanner(NestedProperties, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark]
    public int ItemMetadata_Scan()
    {
        var scanner = new Scanner(ItemMetadata, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark]
    public int FunctionCall_Scan()
    {
        var scanner = new Scanner(FunctionCall, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark]
    public int TypicalSdkCondition_Scan()
    {
        var scanner = new Scanner(TypicalSdkCondition, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark]
    public int VersionCheck_Scan()
    {
        var scanner = new Scanner(VersionCheck, DefaultOptions);
        return Advance(scanner);
    }

    [Benchmark(Description = "Scan with property validation")]
    public int PropertyValidation_Scan()
    {
        var scanner = new Scanner(NestedProperties, ParserOptions.AllowProperties);
        return Advance(scanner);
    }

    [Benchmark(Description = "Scan with all features disabled except properties")]
    public int RestrictedOptions_Scan()
    {
        var scanner = new Scanner(SimpleProperty, ParserOptions.AllowProperties);
        return Advance(scanner);
    }

    private static int Advance(Scanner scanner)
    {
        int tokenCount = 0;

        while (scanner.Advance() && !scanner.IsNext(TokenKind.EndOfInput))
        {
            tokenCount++;
        }

        return tokenCount;
    }
}
