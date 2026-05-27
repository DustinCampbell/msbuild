// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

/// <summary>
///  Common condition patterns found in real-world MSBuild projects,
///  shared across parsing and evaluation benchmarks.
/// </summary>
internal static class ConditionStrings
{
    // Simple comparisons
    public const string SimpleEquality = "'$(Configuration)' == 'Debug'";
    public const string EmptyCheck = "'$(TargetFramework)' == ''";
    public const string NonEmptyCheck = "'$(TargetFramework)' != ''";
    public const string NumericComparison = "$(BuildNumber) >= 100";
    public const string NumericLessThan = "$(ErrorCount) < 5";

    // Boolean operators
    public const string BooleanAnd = "'$(Configuration)' == 'Debug' And '$(Platform)' == 'AnyCPU'";
    public const string BooleanOr = "'$(Configuration)' == 'Debug' Or '$(Configuration)' == 'Release'";
    public const string Negation = "!Exists('$(OutputPath)')";
    public const string NegatedEquality = "!('$(Configuration)' == 'Debug')";

    // Complex / nested expressions
    public const string Complex = "'$(Configuration)' == 'Debug' And ('$(Platform)' == 'x64' Or '$(Platform)' == 'AnyCPU')";
    public const string DeepNesting = "((('$(Configuration)' == 'Debug')))";
    public const string MultipleAnds = "'$(Configuration)' == 'Debug' And '$(Platform)' == 'AnyCPU' And '$(TargetFramework)' == 'net10.0'";
    public const string MixedAndOr = "'$(A)' == '1' And ('$(B)' == '2' Or '$(C)' == '3') And '$(D)' == '4'";

    // Function calls
    public const string ExistsCheck = "Exists('$(MSBuildProjectDirectory)')";
    public const string HasTrailingSlashCheck = "HasTrailingSlash('$(OutputPath)')";
    public const string ExistsWithConcatenation = "Exists('$(MSBuildProjectDirectory)\\$(OutputPath)')";

    // Property concatenation
    public const string ConcatenatedComparison = "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'";
    public const string MultipleProperties = "'$(RootNamespace).$(AssemblyName)' == 'MyApp.MyApp'";

    // Boolean literals
    public const string BooleanLiteralTrue = "'$(IsPackable)' == 'true'";
    public const string BooleanLiteralFalse = "'$(GenerateDocumentationFile)' == 'false'";
    public const string BareBoolean = "$(IsPackable)";

    // Item and metadata expressions
    public const string ItemListCondition = "'@(Compile)' != ''";
    public const string MetadataCondition = "'%(Extension)' == '.cs'";

    // Long / realistic conditions from SDK targets
    public const string RealisticSdkCondition =
        "'$(TargetFrameworkIdentifier)' == '.NETCoreApp' And '$(TargetFrameworkVersion)' >= '5.0' And '$(UseWindowsForms)' == 'true'";

    public const string RealisticMultiTargeting =
        "'$(TargetFramework)' == 'net10.0' Or '$(TargetFramework)' == 'net9.0' Or '$(TargetFramework)' == 'net8.0' Or '$(TargetFramework)' == 'net472'";
}
