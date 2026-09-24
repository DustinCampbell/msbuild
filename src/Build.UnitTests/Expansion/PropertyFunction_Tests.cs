// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class PropertyFunction_Tests(ITestOutputHelper output)
{
    private const string ToolLocationHelperType = "Microsoft.Build.Utilities.ToolLocationHelper";
    private const string MissingSdkRoots = "$([System.Text.RegularExpressions.Regex]::Split('__MissingSdkRoot__', ';'))";
    private const string VersionOne = "$([System.Version]::new('1.0'))";

    private static readonly string s_rootPathPrefix = NativeMethodsShared.IsWindows
        ? @"C:\"
        : Path.VolumeSeparatorChar.ToString();

    private readonly ITestOutputHelper _output = output;

    private readonly string _dateToParse = new DateTime(2010, 12, 25).ToString(CultureInfo.CurrentCulture);

    /// <summary>
    ///  Verify when there is an error due to an attempt to use a static method that we report the method name.
    /// </summary>
    [Fact]
    public void StaticMethodErrorMessageHaveMethodName()
    {
        var ex = Should.Throw<InvalidProjectFileException>(() =>
        {
            const string Content = """
                <Project DefaultTargets='Build'>
                    <PropertyGroup>
                        <Function>$([System.IO.Path]::Combine(null,''))</Function>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <Message Text='[ $(Function) ]' />
                    </Target>
                </Project>
                """;

            Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);
        });

        ex.Message.ShouldContain("[System.IO.Path]::Combine(null, '')", Case.Insensitive);
    }

    /// <summary>
    ///  Verify when there is an error due to an attempt to use a static method that we report the method name.
    /// </summary>
    [Fact]
    public void StaticMethodErrorMessageHaveMethodName1()
    {
        var ex = Should.Throw<InvalidProjectFileException>(() =>
        {
            const string Content = """
                <Project DefaultTargets='Build'>
                    <PropertyGroup>
                        <Function>$(System.IO.Path::Combine('a','b'))</Function>
                    </PropertyGroup>
                    <Target Name='Build'>
                        <Message Text='[ $(Function) ]' />
                    </Target>
                </Project>
                """;

            Helpers.BuildProjectWithNewOMExpectFailure(Content, allowTaskCrash: false);
        });

        ex.Message.ShouldContain("System.IO.Path::Combine('a','b')", Case.Insensitive);
    }

    [Fact]
    public void StaticMethodWithThrowawayParameterSupported()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.Int32]::TryParse("3", out _))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """);

        log.FullLog.ShouldContain("Value is True");
    }

    [Fact]
    public void StaticMethodWithThrowawayParameterSupported2()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.Int32]::TryParse("notANumber", out _))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """);

        log.FullLog.ShouldContain("Value is False");
    }

    [Fact]
    public void StaticMethodWithUnderscoreNotConfusedWithThrowaway()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <PropertyGroup>
                    <MyProperty>Value is $([System.String]::Join('_', 'asdf', 'jkl'))</MyProperty>
                </PropertyGroup>
                <Target Name='Build'>
                    <Message Text='$(MyProperty)' />
                </Target>
            </Project>
            """);

        log.FullLog.ShouldContain("Value is asdf_jkl");
    }

    public static bool HasNetFramework48ReferenceAssemblies
        => ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version48) is not null;

    [Fact(Skip = "Requires .NET Framework 4.8 reference assemblies.", SkipUnless = nameof(HasNetFramework48ReferenceAssemblies))]
    public void TestGetPathToReferenceAssembliesAsFunction()
    {
        const string Content = $"""
            <Project ToolsVersion="msbuilddefaulttoolsversion">

                <PropertyGroup>
                    <TargetFrameworkIdentifier>.NETFramework</TargetFrameworkIdentifier>
                    <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
                    <TargetFrameworkProfile></TargetFrameworkProfile>
                    <TargetFrameworkMoniker>$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)</TargetFrameworkMoniker>
                </PropertyGroup>

                <Target Name="Build">
                    <GetReferenceAssemblyPaths
                        Condition=" '$(TargetFrameworkDirectory)' == '' and '$(TargetFrameworkMoniker)' !=''"
                        TargetFrameworkMoniker="$(TargetFrameworkMoniker)"
                        RootPath="$(TargetFrameworkRootPath)"
                    >
                        <Output TaskParameter="ReferenceAssemblyPaths" PropertyName="ReferenceAssemblyPathsFromTask"/>
                    </GetReferenceAssemblyPaths>

                    <PropertyGroup>
                        <ReferenceAssemblyPathsFromFunction>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToStandardLibraries($(TargetFrameworkIdentifier), $(TargetFrameworkVersion), $(TargetFrameworkProfile)))\</ReferenceAssemblyPathsFromFunction>
                    </PropertyGroup>

                    <Message Text="Task:     $(ReferenceAssemblyPathsFromTask)" Importance="High" />
                    <Message Text="Function: $(ReferenceAssemblyPathsFromFunction)" Importance="High" />

                    <Warning Text="Reference assembly paths do not match!" Condition="'$(ReferenceAssemblyPathsFromFunction)' != '$(ReferenceAssemblyPathsFromTask)'" />
                </Target>

            </Project>
            """;

        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess(Content);

        log.AssertLogDoesntContain("Reference assembly paths do not match");
    }

    /// <summary>
    ///  Expand property function that takes a null argument.
    /// </summary>
    [Fact]
    public void PropertyFunctionNullArgument()
        => ExpandProperties("$([System.Convert]::ChangeType('null',$(SomeStuff.GetTypeCode())))")
            .ShouldBe("null");

    /// <summary>
    ///  Expand a null-returning property function alone or concatenated with literal text.
    /// </summary>
    [Theory]
    [InlineData("$([System.Environment]::GetEnvironmentVariable(`_NonExistentVar`))", "")]
    [InlineData("prefix_$([System.Environment]::GetEnvironmentVariable(`_NonExistentVar`))", "prefix_")]
    public void NullReturningPropertyFunctionExpandsToEmptyString(string expression, string expected)
    {
        using var env = TestEnvironment.Create(_output);
        env.SetEnvironmentVariable("_NonExistentVar", null);

        ExpandProperties(expression)
            .ShouldBe(expected);
    }

    /// <summary>
    ///  Expand property function that takes no arguments and returns a string.
    /// </summary>
    [Fact]
    public void PropertyFunctionNoArguments()
        => ExpandProperties("$(SomeStuff.ToUpperInvariant())", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("THIS IS SOME STUFF");

    /// <summary>
    ///  Expand property function that takes no arguments and returns a string (trimmed).
    /// </summary>
    [Fact]
    public void PropertyFunctionNoArgumentsTrim()
        => ExpandProperties("$(FileName.Trim())", Properties("FileName", "    foo.ext   "))
            .ShouldBe("foo.ext");

    /// <summary>
    ///  Expand property function that is a get property accessor.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyGet()
        => ExpandProperties("$(SomeStuff.Length)", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("18");

    /// <summary>
    ///  Expand property function which is a manual get property accessor.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyManualGet()
        => ExpandProperties("$(SomeStuff.get_Length())", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("18");

    /// <summary>
    ///  Expand a property function followed by literal text.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyNoArgumentsConcat()
        => ExpandProperties("$(SomeStuff.ToLowerInvariant())_goop", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("this is some stuff_goop");

    /// <summary>
    ///  Expand property function with a constant argument.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgument()
        => ExpandProperties("$(SomeStuff.SubString(13))", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("STUff");

    /// <summary>
    ///  Expand a property function whose returned substring contains spaces.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentWithSpaces()
        => ExpandProperties("$(SomeStuff.SubString(8))", Properties("SomeStuff", "This IS SOME STUff"))
            .ShouldBe("SOME STUff");

    /// <summary>
    ///  Expand property function with a constant argument.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyPathRootSubtraction()
        => ExpandProperties(
            "$(MyPath.SubString($(RootPath.Length)))",
            Properties(
                ("RootPath", Path.Combine(s_rootPathPrefix, "this", "is", "the", "root")),
                ("MyPath", Path.Combine(s_rootPathPrefix, "this", "is", "the", "root", "my", "project", "is", "here.proj"))))
            .ShouldBe(Path.Combine(Path.DirectorySeparatorChar.ToString(), "my", "project", "is", "here.proj"));

    /// <summary>
    ///  Expand property function with an argument that is a property.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentExpandedProperty()
        => ExpandProperties(
            "$(SomeStuff.SubString(1$(Value)))",
            Properties(
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("STUff");

    /// <summary>
    ///  Expand property function that has a boolean return value.
    /// </summary>
    [Theory]
    [InlineData("PathRoot2", true)]
    [InlineData("PathRoot", false)]
    public void PropertyFunctionPropertyWithArgumentBooleanReturn(string propertyName, bool expected)
        => ExpandProperties(
            $"$({propertyName}.Endswith({Path.DirectorySeparatorChar}))",
            Properties(
                ("PathRoot", Path.Combine(s_rootPathPrefix, "goo")),
                ("PathRoot2", $"{Path.Combine(s_rootPathPrefix, "goop")}{Path.DirectorySeparatorChar}")))
            .ShouldBe(expected.ToResultString());

    /// <summary>
    ///  Expand property function with an argument that is expanded, and a chaining of other functions.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentNestedAndChainedFunction()
        => ExpandProperties(
            "$(SomeStuff.SubString(1$(Value)).ToLowerInvariant().SubString($(Value)))",
            Properties(
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("ff");

    /// <summary>
    ///  Expand property function with chained functions on its results.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentChained()
        => ExpandProperties(
            "$(SomeStuff.ToUpperInvariant().ToLowerInvariant())",
            Properties(
                ("Value", "3"),
                ("SomeStuff", "This IS SOME STUff")))
            .ShouldBe("this is some stuff");

    /// <summary>
    ///  Expand property function with an argument that is a function.
    /// </summary>
    [Fact]
    public void PropertyFunctionPropertyWithArgumentNested()
        => ExpandProperties(
            "$(SomeStuff.SubString($(Value.get_Length())))",
            Properties(
                ("Value", "12345"),
                ("SomeStuff", "1234567890")))
            .ShouldBe("67890");

    /// <summary>
    ///  Expand property function that returns a generic list.
    /// </summary>
    [Fact]
    public void PropertyFunctionGenericListReturn()
        => ExpandProperties("$([MSBuild]::__GetListTest())")
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Expand property function that returns an array.
    /// </summary>
    [Fact]
    public void PropertyFunctionArrayReturn()
        => ExpandProperties("$(List.Split(-))", Properties("List", "A-B-C-D"))
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Expand property function that returns a Dictionary.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionDictionaryReturn()
        => ExpandProperties("$([System.Environment]::GetEnvironmentVariables())")
            .ShouldNotBeNull()
            .ShouldContain($"OS={Environment.GetEnvironmentVariable("OS")}", Case.Insensitive);

    /// <summary>
    ///  Expand property function that returns an array.
    /// </summary>
    [Fact]
    public void PropertyFunctionArrayReturnManualSplitter()
        => ExpandProperties(
            "$(List.Split($(Splitter.ToCharArray())))",
            Properties(
                ("List", "A-B-C-D"),
                ("Splitter", "-")))
            .ShouldBe("A;B;C;D");

    /// <summary>
    ///  Evaluate conditions containing boolean-returning property functions.
    /// </summary>
    [Theory]
    [MemberData(nameof(PropertyFunctionConditions))]
    public void PropertyFunctionInCondition(string expression)
    {
        var expander = ExpanderFactory.Create(Properties(
            ("PathRoot", Path.Combine(s_rootPathPrefix, "goo")),
            ("PathRoot2", $"{Path.Combine(s_rootPathPrefix, "goop")}{Path.DirectorySeparatorChar}")));

        bool result = ConditionEvaluator.EvaluateCondition(
            expression,
            ParserOptions.AllowAll,
            expander,
            ExpanderOptions.ExpandProperties,
            Directory.GetCurrentDirectory(),
            MockElementLocation.Instance,
            FileSystems.Default,
            new TestLoggingContext(null, new BuildEventContext(1, 2, 3, 4)));

        result.ShouldBeTrue();
    }

    public static TheoryData<string> PropertyFunctionConditions => new()
    {
        $"'$(PathRoot2.Endswith(`{Path.DirectorySeparatorChar}`))' == 'true'",
        $"'$(PathRoot.EndsWith({Path.DirectorySeparatorChar}))' == 'false'",
    };

    /// <summary>
    ///  Reject property references used as functions or with unsupported members.
    /// </summary>
    [Theory]
    [InlineData("[$(SomeStuff($(Value)))]")]
    [InlineData("[$(SomeStuff.Lgg)]")]
    [InlineData("$(SomeStuff.ToUpperInvariant().Foo)")]
    [InlineData("[$(SomeStuff($(System.DateTime.Now)))]")]
    public void InvalidPropertyMemberExpressionsThrow(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                expression,
                Properties(
                    ("Value", "3"),
                    ("SomeStuff", "This IS SOME STUff"))));

    /// <summary>
    ///  Expand property function - invalid expression.
    /// </summary>
    [Fact]
    public void PropertyFunctionWithTextInsideCallThrows()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(SomeStuff.ToLowerInvariant()_goop)", Properties("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    ///  Reject substring arguments that cannot be converted or are out of range.
    /// </summary>
    [Theory]
    [InlineData("[$(SomeStuff.Substring(HELLO!))]")]
    [InlineData("[$(SomeStuff.Substring(-10))]")]
    public void PropertyFunctionWithInvalidSubstringArgumentThrows(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(expression, Properties("SomeStuff", "This IS SOME STUff")));

    /// <summary>
    ///  Expand property function that calls a static method with quoted arguments.
    /// </summary>
    [Fact]
    public void ParenthesizedStaticPropertyFunctionThrows()
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("""$(([System.DateTime]::Now).ToString("MM.dd.yyyy"))"""));

    /// <summary>
    ///  Expand property function - we don't handle metadata functions.
    /// </summary>
    [Fact]
    public void PropertyFunctionInvalidNoMetadataFunctions()
        => ExpandProperties("[%(LowerLetterList.Identity.ToUpper())]")
            .ShouldBe("[%(LowerLetterList.Identity.ToUpper())]");

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethod1()
        => ExpandProperties(
            "$([System.IO.Path]::Combine($(Drive), `$(File)`))",
            Properties(
                ("Drive", s_rootPathPrefix),
                ("File", Path.Combine("foo", "file.txt"))))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Expand property function that creates an instance of a type.
    /// </summary>
    [Fact]
    public void PropertyFunctionConstructor1()
        => ExpandProperties("$([System.Version]::new($(ver1)).ToString())", Properties("ver1", "1.2.3.4"))
            .ShouldBe("1.2.3.4");

    /// <summary>
    ///  Expand property function that creates an instance of a type.
    /// </summary>
    [Fact]
    public void PropertyFunctionConstructor2()
        => ExpandProperties(
            "$([System.Version]::new($(ver1)).CompareTo($([System.Version]::new($(ver2)))))",
            Properties(
                ("ver1", "1.2.3.4"),
                ("ver2", "2.2.3.4")))
            .ShouldBe("-1");

    /// <summary>
    ///  Expand property function that is only available when MSBUILDENABLEALLPROPERTYFUNCTIONS=1.
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: "https://github.com/dotnet/coreclr/issues/15662")]
    public void PropertyStaticFunctionAllEnabled()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);

        ExpandProperties("$([System.Type]::GetType(`System.Type`))", allowReflection: true)
            .ShouldBe("System.Type");
    }

    /// <summary>
    ///  Expand property function that is defined (on CoreFX) in an assembly named after its full namespace.
    /// </summary>
    [Fact]
    public void PropertyStaticFunctionLocatedFromAssemblyWithNamespaceName()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);

        string? result = ExpandProperties("$([System.Diagnostics.Process]::GetCurrentProcess().Id)", allowReflection: true);

        int.TryParse(result, out int pid).ShouldBeTrue();
        pid.ShouldBe(EnvironmentUtilities.CurrentProcessId);
    }

    /// <summary>
    ///  Expand property function that is only available when MSBUILDENABLEALLPROPERTYFUNCTIONS=1, but cannot be found.
    /// </summary>
    [Theory]
    [InlineData("$([Microsoft.FOO.FileIO.FileSystem]::CurrentDirectory)")]
    [InlineData("$([Foo.Baz]::new())")]
    [InlineData("$([Foo]::new())")]
    [InlineData("$([Foo.]::new())")]
    [InlineData("$([.Foo]::new())")]
    [InlineData("$([.]::new())")]
    [InlineData("$([]::new())")]
    public void PropertyStaticFunctionUsingNamespaceNotFound(string expression)
    {
        using var env = TestEnvironment.Create(_output);
        env.RemoveAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions);
        env.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

        FeatureSwitches.EnableAllPropertyFunctions.ShouldBeTrue();

        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(expression));
    }

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void PropertyFunctionStaticMethodQuoted1()
        => ExpandProperties(
            $"""$([System.IO.Path]::Combine(`{s_rootPathPrefix}`, `$(File)`))""",
            Properties("File", Path.Combine("foo", "file.txt")))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Preserve spaces inside quoted path arguments, including trailing spaces.
    /// </summary>
    [Theory]
    [InlineData("foo goo", "foo goo", "file.txt")]
    [InlineData("foo baz", "foo bar", "baz.txt")]
    [InlineData("foo baz ", "foo bar", "baz.txt")]
    public void QuotedPathArgumentsPreserveSpaces(string parentDirectory, string childDirectory, string fileName)
    {
        string parentPath = Path.Combine(s_rootPathPrefix, parentDirectory);

        ExpandProperties(
            $"$([System.IO.Path]::Combine(`{parentPath}`, `$(File)`))",
            Properties("File", Path.Combine(childDirectory, fileName)))
            .ShouldBe(Path.Combine(parentPath, childDirectory, fileName));
    }

    /// <summary>
    ///  Expand property function that calls a static method with quoted arguments.
    /// </summary>
    [Theory]
    [InlineData("yyyy/MM/dd HH:mm:ss")]
    [InlineData("MM.dd.yyyy")]
    public void PropertyFunctionDateTimeParseWithQuotedFormat(string format)
        => ExpandProperties($"""$([System.DateTime]::Parse('{_dateToParse}').ToString("{format}"))""")
            .ShouldBe(DateTime.Parse(_dateToParse).ToString(format));

    /// <summary>
    ///  Expand property function that calls a static method with quoted arguments.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodQuoted4()
        => ExpandProperties("""$([System.DateTime]::Now.ToString("MM.dd.yyyy"))""")
            .ShouldBe(DateTime.Now.ToString("MM.dd.yyyy"));

    /// <summary>
    ///  Expand property function that calls a static method.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodNested()
        => ExpandProperties(
            $"$([System.IO.Path]::Combine(`{s_rootPathPrefix}`, $([System.IO.Path]::Combine(`foo`,`file.txt`))))",
            Properties("File", $"foo{Path.DirectorySeparatorChar}file.txt"))
            .ShouldBe(Path.Combine(s_rootPathPrefix, "foo", "file.txt"));

    /// <summary>
    ///  Expand property function that calls a static method regex.
    /// </summary>
    [Theory]
    [MemberData(nameof(RegexIsMatchExpressions))]
    public void PropertyFunctionRegexIsMatch(string expression, bool expected)
        => ExpandProperties(expression)
            .ShouldBe(expected.ToResultString());

    public static TheoryData<string, bool> RegexIsMatchExpressions => new()
    {
        { """$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, `RegexOptions.IgnoreCase,RegexOptions.Singleline`))""", true },
        { """$([System.Text.RegularExpressions.Regex]::IsMatch(`-42`, `^-?\d+(\.\d{2})?$`, System.Text.RegularExpressions.RegexOptions.IgnoreCase|RegexOptions.Singleline))""", true },
        { """$([System.Text.RegularExpressions.Regex]::IsMatch(`100 GBP`, `^-?\d+(\.\d{2})?$`))""", false },
    };

    [Theory]
    [MemberData(nameof(RegexExpressions))]
    public void PropertyFunctionRegex(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    public static TheoryData<string, string> RegexExpressions => new()
    {
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').ToString())""", "[a-z]+" },
        { """$([System.Text.RegularExpressions.Regex]::Escape('a.b'))""", @"a\.b" },
        { """$([System.Text.RegularExpressions.Regex]::Unescape('a\.b'))""", "a.b" },
        { """$([System.Text.RegularExpressions.Regex]::IsMatch('ABC', 'abc'))""", bool.FalseString },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '\d+').Value)""", "123" },
        { """$([System.Text.RegularExpressions.Regex]::Matches('a1b2', '\d').Count)""", "2" },
        { """$([System.Text.RegularExpressions.Regex]::Replace('ABC', 'abc', 'x'))""", "ABC" },
        { """$([System.Text.RegularExpressions.Regex]::Split('a1b2', '\d')[1])""", "b" },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').IsMatch('123abc'))""", bool.TrueString },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').IsMatch('123abc', '3'))""", bool.TrueString },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').Match('123abc').Value)""", "abc" },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').Match('abc123def', '3').Value)""", "def" },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').Match('123abc456', '3', '3').Value)""", "abc" },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').Matches('abc def').Count)""", "2" },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').Matches('abc def', '3').Count)""", "1" },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').Replace('abc123def', '#'))""", "#123#" },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').Replace('abc123def', '#', '1'))""", "#123def" },
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+').Replace('abc123def', '#', '1', '3'))""", "abc123#" },
        { """$([System.Text.RegularExpressions.Regex]::new('\d').Split('a1b2')[1])""", "b" },
        { """$([System.Text.RegularExpressions.Regex]::new('\d').Split('a1b2', '2')[1])""", "b2" },
        { """$([System.Text.RegularExpressions.Regex]::new('\d').Split('a1b2', '2', '2')[1])""", "" },
        { """$([System.Text.RegularExpressions.Regex]::new('(?<word>[a-z]+)').GetGroupNames()[1])""", "word" },
        { """$([System.Text.RegularExpressions.Regex]::new('(?<word>[a-z]+)').GetGroupNumbers()[1])""", "1" },
        { """$([System.Text.RegularExpressions.Regex]::new('(?<word>[a-z]+)').GroupNameFromNumber('1'))""", "word" },
        { """$([System.Text.RegularExpressions.Regex]::new('(?<word>[a-z]+)').GroupNumberFromName('word'))""", "1" },
        { """$([System.Text.RegularExpressions.Regex]::new('x').RightToLeft)""", bool.FalseString },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '(?<word>[a-z]+)').Groups.Count)""", "2" },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '[a-z]+').NextMatch().Value)""", "" },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '(?<word>[a-z]+)').Result('${word}'))""", "abc" },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '[a-z]+').Index)""", "0" },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '[a-z]+').Length)""", "3" },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '[a-z]+').ToString())""", "abc" },
        { """$([System.Text.RegularExpressions.Regex]::Matches('abc def', '[a-z]+')[1].Value)""", "def" },
    };

    [ModernExpanderOnlyTheory]
    [MemberData(nameof(ModernRegexExpressions))]
    public void ModernPropertyFunctionRegex(string expression, string expected)
        => ExpandProperties(expression).ShouldBe(expected);

    public static TheoryData<string, string> ModernRegexExpressions => new()
    {
        { """$([System.Text.RegularExpressions.Regex]::new('[a-z]+', 'IgnoreCase').Options)""", "IgnoreCase" },
        {
            """$([System.Text.RegularExpressions.Regex]::new('[a-z]+', 'None', $([System.Text.RegularExpressions.Regex]::new('x').MatchTimeout)).ToString())""",
            "[a-z]+"
        },
        { """$([System.Text.RegularExpressions.Regex]::IsMatch('ABC', 'abc', 'IgnoreCase'))""", bool.TrueString },
        {
            """$([System.Text.RegularExpressions.Regex]::IsMatch('ABC', 'abc', 'IgnoreCase', $([System.Text.RegularExpressions.Regex]::new('x').MatchTimeout)))""",
            bool.TrueString
        },
        { """$([System.Text.RegularExpressions.Regex]::Match('ABC', 'abc', 'IgnoreCase').Success)""", bool.TrueString },
        {
            """$([System.Text.RegularExpressions.Regex]::Match('ABC', 'abc', 'IgnoreCase', $([System.Text.RegularExpressions.Regex]::new('x').MatchTimeout)).Success)""",
            bool.TrueString
        },
        { """$([System.Text.RegularExpressions.Regex]::Matches('A a', 'a', 'IgnoreCase').Count)""", "2" },
        {
            """$([System.Text.RegularExpressions.Regex]::Matches('A a', 'a', 'IgnoreCase', $([System.Text.RegularExpressions.Regex]::new('x').MatchTimeout)).Count)""",
            "2"
        },
        { """$([System.Text.RegularExpressions.Regex]::Replace('ABC', 'abc', 'x', 'IgnoreCase'))""", "x" },
        {
            """$([System.Text.RegularExpressions.Regex]::Replace('ABC', 'abc', 'x', 'IgnoreCase', $([System.Text.RegularExpressions.Regex]::new('x').MatchTimeout)))""",
            "x"
        },
        { """$([System.Text.RegularExpressions.Regex]::Split('Axa', 'a', 'IgnoreCase')[1])""", "x" },
        {
            """$([System.Text.RegularExpressions.Regex]::Split('Axa', 'a', 'IgnoreCase', $([System.Text.RegularExpressions.Regex]::new('x').MatchTimeout))[1])""",
            "x"
        },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '(?<word>[a-z]+)').Groups[1].Success)""", bool.TrueString },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '(?<word>[a-z]+)').Groups[1].Captures.Count)""", "1" },
        { """$([System.Text.RegularExpressions.Regex]::Match('abc123', '(?<word>[a-z]+)').Groups[1].Captures[0].Value)""", "abc" },
    };

    [Fact]
    public void PropertyFunctionFileSystemMembers()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var parent = env.CreateFolder();
        var child = env.CreateFolder(Path.Combine(parent.Path, "child"), createFolder: true);
        var file = env.CreateFile(child, "data.txt", "content");
        PropertyDictionary<ProjectPropertyInstance> properties = Properties(
            ("Parent", parent.Path),
            ("Child", child.Path),
            ("File", file.Path));

        ExpandProperties("$([System.IO.Directory]::Exists($(Child)))", properties).ShouldBe(bool.TrueString);
        ExpandProperties("$([System.IO.Directory]::GetDirectories($(Parent)).Length)", properties).ShouldBe("1");
        ExpandProperties("$([System.IO.Directory]::GetDirectories($(Parent), 'ch*').Length)", properties).ShouldBe("1");
        ExpandProperties("$([System.IO.Directory]::GetFiles($(Child)).Length)", properties).ShouldBe("1");
        DateTime.Parse(ExpandProperties("$([System.IO.Directory]::GetLastAccessTime($(Child)))", properties)!)
            .ShouldBe(Directory.GetLastAccessTime(child.Path), TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.Directory]::GetLastWriteTime($(Child)))", properties)!)
            .ShouldBe(Directory.GetLastWriteTime(child.Path), TimeSpan.FromSeconds(1));
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).FullName)", properties).ShouldBe(child.Path);

        ExpandProperties("$([System.IO.File]::Exists($(File)))", properties).ShouldBe(bool.TrueString);
        ExpandProperties("$([System.IO.File]::GetAttributes($(File)))", properties).ShouldBe(File.GetAttributes(file.Path).ToString());
        DateTime.Parse(ExpandProperties("$([System.IO.File]::GetCreationTime($(File)))", properties)!)
            .ShouldBe(File.GetCreationTime(file.Path), TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.File]::GetCreationTimeUtc($(File)))", properties)!)
            .ShouldBe(File.GetCreationTimeUtc(file.Path), TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.File]::GetLastAccessTime($(File)))", properties)!)
            .ShouldBe(File.GetLastAccessTime(file.Path), TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.File]::GetLastWriteTime($(File)))", properties)!)
            .ShouldBe(File.GetLastWriteTime(file.Path), TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.File]::GetLastWriteTimeUtc($(File)))", properties)!)
            .ShouldBe(File.GetLastWriteTimeUtc(file.Path), TimeSpan.FromSeconds(1));
        ExpandProperties("$([System.IO.File]::ReadAllBytes($(File)).Length)", properties).ShouldBe("7");
        ExpandProperties("$([System.IO.File]::ReadAllText($(File)))", properties).ShouldBe("content");

        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Attributes)", properties)
            .ShouldBe(new DirectoryInfo(child.Path).Attributes.ToString());
        DateTime.Parse(ExpandProperties("$([System.IO.Directory]::GetParent($(File)).CreationTime)", properties)!)
            .ShouldBe(new DirectoryInfo(child.Path).CreationTime, TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.Directory]::GetParent($(File)).CreationTimeUtc)", properties)!)
            .ShouldBe(new DirectoryInfo(child.Path).CreationTimeUtc, TimeSpan.FromSeconds(1));
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Exists)", properties).ShouldBe(bool.TrueString);
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Extension)", properties).ShouldBe("");
        DateTime.Parse(ExpandProperties("$([System.IO.Directory]::GetParent($(File)).LastAccessTime)", properties)!)
            .ShouldBe(new DirectoryInfo(child.Path).LastAccessTime, TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.Directory]::GetParent($(File)).LastAccessTimeUtc)", properties)!)
            .ShouldBe(new DirectoryInfo(child.Path).LastAccessTimeUtc, TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.Directory]::GetParent($(File)).LastWriteTime)", properties)!)
            .ShouldBe(new DirectoryInfo(child.Path).LastWriteTime, TimeSpan.FromSeconds(1));
        DateTime.Parse(ExpandProperties("$([System.IO.Directory]::GetParent($(File)).LastWriteTimeUtc)", properties)!)
            .ShouldBe(new DirectoryInfo(child.Path).LastWriteTimeUtc, TimeSpan.FromSeconds(1));
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Name)", properties).ShouldBe("child");
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).ToString())", properties).ShouldBe(child.Path);
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Parent.FullName)", properties).ShouldBe(parent.Path);
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Root.FullName)", properties)
            .ShouldBe(new DirectoryInfo(child.Path).Root.FullName);
#if NET
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).LinkTarget)", properties).ShouldBe("");
#endif
    }

    [Theory]
    [InlineData("$([Microsoft.Build.Utilities.ToolLocationHelper]::CurrentToolsVersion)", MSBuildConstants.CurrentToolsVersion)]
    [InlineData("$([Microsoft.Build.Utilities.ToolLocationHelper]::PathToSystem)", null)]
    public void PropertyFunctionToolLocationHelperProperties(string expression, string? expected)
        => ExpandProperties(expression).ShouldBe(expected ?? ToolLocationHelper.PathToSystem);

    [ModernExpanderOnlyFact]
    public void ModernPropertyFunctionAssemblyFoldersFromConfig()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var root = env.CreateFolder();
        var config = env.CreateFile(root, "AssemblyFolders.config", "<AssemblyFoldersConfig><AssemblyFolders /></AssemblyFoldersConfig>");

        ExpandProperties(
            $"""$([{ToolLocationHelperType}]::GetAssemblyFoldersFromConfigInfo('{config.Path}', 'v4.0', 'None'))""")
            .ShouldNotBeNull();
    }

    [ModernExpanderOnlyTheory]
    [MemberData(nameof(ModernToolLocationSdkExpressions))]
    public void ModernPropertyFunctionToolLocationSdkMethods(string expression)
        => ExpandProperties(expression).ShouldNotBeNull();

    public static TheoryData<string> ModernToolLocationSdkExpressions
    {
        get
        {
            TheoryData<string> expressions =
            [
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocations('Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocations({MissingSdkRoots}, '', 'Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocations({MissingSdkRoots}, {MissingSdkRoots}, '', 'Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocationsAndVersions('Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocationsAndVersions({MissingSdkRoots}, '', 'Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocationsAndVersions({MissingSdkRoots}, {MissingSdkRoots}, '', 'Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocation('Missing, Version=1.0', 'Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocation('Missing, Version=1.0', 'Missing', {VersionOne}, {MissingSdkRoots}, ''))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocation('Missing, Version=1.0', 'Missing', {VersionOne}, {MissingSdkRoots}, {MissingSdkRoots}, ''))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocation('Missing, Version=1.0', 'Missing', '1.0'))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocation('Missing, Version=1.0', 'Missing', '1.0', '__MissingSdkRoot__', ''))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocation('Missing, Version=1.0', 'Missing', '1.0', '__MissingSdkRoot__', '__MissingSdkRoot__', ''))""",
                $"""$([{ToolLocationHelperType}]::FilterPlatformExtensionSDKs({VersionOne}, $([{ToolLocationHelperType}]::GetPlatformExtensionSDKLocations('Missing', {VersionOne}))))""",
                $"""$([{ToolLocationHelperType}]::GetSDKReferenceFolders('__MissingSdkRoot__'))""",
                $"""$([{ToolLocationHelperType}]::GetSDKReferenceFolders('__MissingSdkRoot__', 'Retail', 'Neutral'))""",
                $"""$([{ToolLocationHelperType}]::GetSDKRedistFolders('__MissingSdkRoot__'))""",
                $"""$([{ToolLocationHelperType}]::GetSDKRedistFolders('__MissingSdkRoot__', 'Retail', 'Neutral'))""",
                $"""$([{ToolLocationHelperType}]::GetSDKDesignTimeFolders('__MissingSdkRoot__'))""",
                $"""$([{ToolLocationHelperType}]::GetSDKDesignTimeFolders('__MissingSdkRoot__', 'Retail', 'Neutral'))""",
                $"""$([{ToolLocationHelperType}]::GetTargetPlatformSdks())""",
                $"""$([{ToolLocationHelperType}]::GetTargetPlatformSdks({MissingSdkRoots}, ''))""",
                $"""$([{ToolLocationHelperType}]::FilterTargetPlatformSdks($([{ToolLocationHelperType}]::GetTargetPlatformSdks({MissingSdkRoots}, '')), {VersionOne}, {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformSDKPropsFileLocation('Missing', '1.0', 'Missing', '1.0', '1.0'))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformSDKPropsFileLocation('Missing', '1.0', 'Missing', '1.0', '1.0', '__MissingSdkRoot__', ''))""",
                $"""$([{ToolLocationHelperType}]::GetTargetPlatformReferences('Missing', '1.0', 'Missing', '1.0', '1.0'))""",
                $"""$([{ToolLocationHelperType}]::GetTargetPlatformReferences('Missing', '1.0', 'Missing', '1.0', '1.0', '__MissingSdkRoot__', ''))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformOrFrameworkExtensionSdkReferences('Missing, Version=1.0', 'Missing', '1.0', '__MissingSdkRoot__', '__MissingSdkRoot__', ''))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformOrFrameworkExtensionSdkReferences('Missing, Version=1.0', 'Missing', '1.0', '__MissingSdkRoot__', '__MissingSdkRoot__', '', 'Missing', '1.0'))""",
                $"""$([{ToolLocationHelperType}]::GetSDKContentFolderPath('Missing', '1.0', 'Missing', '1.0', '1.0', 'Folder', '__MissingSdkRoot__'))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformSDKLocation('Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformSDKLocation('Missing', {VersionOne}, {MissingSdkRoots}, ''))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformSDKLocation('Missing', '1.0'))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformSDKLocation('Missing', '1.0', '__MissingSdkRoot__', ''))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformSDKDisplayName('Missing', '1.0'))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformSDKDisplayName('Missing', '1.0', '__MissingSdkRoot__', ''))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformsForSDK('Missing', {VersionOne}))""",
                $"""$([{ToolLocationHelperType}]::GetPlatformsForSDK('Missing', {VersionOne}, {MissingSdkRoots}, ''))""",
                $"""$([{ToolLocationHelperType}]::GetLatestSDKTargetPlatformVersion('Missing', '1.0'))""",
                $"""$([{ToolLocationHelperType}]::GetLatestSDKTargetPlatformVersion('Missing', '1.0', {MissingSdkRoots}))""",
                $"""$([{ToolLocationHelperType}]::GetFoldersInVSInstalls({VersionOne}, {VersionOne}, 'Subfolder'))""",
                $"""$([{ToolLocationHelperType}]::GetFoldersInVSInstallsAsString('1.0', '1.0', 'Subfolder'))""",
                $"""$([{ToolLocationHelperType}]::FindRootFolderWhereAllFilesExist('__MissingSdkRoot__', 'missing.txt'))""",
            ];

            if (NativeMethodsShared.IsWindows)
            {
                expressions.Add(
                    $"""$([{ToolLocationHelperType}]::GetAssemblyFoldersExInfo('SOFTWARE\__Missing__', 'v4.0', 'AssemblyFoldersEx', '', '', 'None'))""");
            }

            return expressions;
        }
    }

    [Theory]
    [MemberData(nameof(ToolLocationStringFrameworkExpressions))]
    public void PropertyFunctionToolLocationStringFrameworkMethods(string expression)
        => ExpandProperties(expression).ShouldNotBeNull();

    public static TheoryData<string> ToolLocationStringFrameworkExpressions =>
    [
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkSdk())""",
        $"""$([{ToolLocationHelperType}]::GetPathToStandardLibraries('Missing', '1.0', ''))""",
        $"""$([{ToolLocationHelperType}]::GetPathToStandardLibraries('Missing', '1.0', '', 'AnyCPU'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToStandardLibraries('Missing', '1.0', '', 'AnyCPU', '__MissingFrameworkRoot__'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToStandardLibraries('Missing', '1.0', '', 'AnyCPU', '__MissingFrameworkRoot__', '__MissingFrameworkRoot__'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToReferenceAssemblies('Missing', '1.0', ''))""",
        $"""$([{ToolLocationHelperType}]::GetPathToReferenceAssemblies('Missing', '1.0', '', '__MissingFrameworkRoot__'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToReferenceAssemblies('Missing', '1.0', '', '__MissingFrameworkRoot__', '__MissingFrameworkRoot__'))""",
        $"""$([{ToolLocationHelperType}]::ClearSDKStaticCache())""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkSdkFile('Missing.exe'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToBuildTools('Missing'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToBuildToolsFile('Missing.exe', 'Missing'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToSystemFile('Missing.dll'))""",
        $"""$([{ToolLocationHelperType}]::GetSupportedTargetFrameworks())""",
        $"""$([{ToolLocationHelperType}]::HighestVersionOfTargetFrameworkIdentifier('__MissingFrameworkRoot__', 'Missing'))""",
        $"""$([{ToolLocationHelperType}]::GetProgramFilesReferenceAssemblyRoot())""",
    ];

    [ModernExpanderOnlyTheory]
    [MemberData(nameof(ModernToolLocationFrameworkExpressions))]
    public void ModernPropertyFunctionToolLocationFrameworkMethods(string expression)
        => ExpandProperties(expression).ShouldNotBeNull();

    public static TheoryData<string> ModernToolLocationFrameworkExpressions =>
    [
        $"""$([{ToolLocationHelperType}]::GetDotNetFrameworkVersionFolderPrefix('Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetDotNetFrameworkRootRegistryKey('Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetDotNetFrameworkSdkRootRegistryKey('Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetDotNetFrameworkSdkRootRegistryKey('Version40', 'Version100'))""",
        $"""$([{ToolLocationHelperType}]::GetDotNetFrameworkSdkInstallKeyValue('Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetDotNetFrameworkSdkInstallKeyValue('Version40', 'Version100'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFramework('Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFramework('Version40', 'Current'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkSdk('Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkSdk('Version40', 'Version100'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkReferenceAssemblies('Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkSdkFile('Missing.exe', 'Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkSdkFile('Missing.exe', 'Version40', 'Version100'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkSdkFile('Missing.exe', 'Version40', 'Current'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkSdkFile('Missing.exe', 'Version40', 'Version100', 'Current'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToWindowsSdk('Version45', 'Version110'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToWindowsSdkFile('Missing.exe', 'Version45', 'Version110'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToWindowsSdkFile('Missing.exe', 'Version45', 'Version110', 'Current'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToBuildTools('Missing', 'Current'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToBuildToolsFile('Missing.exe', 'Missing', 'Current'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkFile('Missing.dll', 'Version40'))""",
        $"""$([{ToolLocationHelperType}]::GetPathToDotNetFrameworkFile('Missing.dll', 'Version40', 'Current'))""",
    ];

    [ModernExpanderOnlyFact]
    public void ModernPropertyFunctionToolLocationFrameworkNameMethods()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var root = env.CreateFolder();
        var identifier = env.CreateFolder(Path.Combine(root.Path, "Missing"), createFolder: true);
        env.CreateFolder(Path.Combine(identifier.Path, "v1.0"), createFolder: true);
        PropertyDictionary<ProjectPropertyInstance> properties = Properties("Root", root.Path);

        string frameworkName =
            $"""$([{ToolLocationHelperType}]::HighestVersionOfTargetFrameworkIdentifier('$(Root)', 'Missing'))""";

        ExpandProperties($"""$([{ToolLocationHelperType}]::GetPathToReferenceAssemblies({frameworkName}))""", properties).ShouldNotBeNull();
        ExpandProperties($"""$([{ToolLocationHelperType}]::GetPathToReferenceAssemblies('$(Root)', {frameworkName}))""", properties).ShouldNotBeNull();
        ExpandProperties(
            $"""$([{ToolLocationHelperType}]::GetPathToReferenceAssemblies('$(Root)', '$(Root)', {frameworkName}))""",
            properties).ShouldNotBeNull();
        ExpandProperties(
            $"""$([{ToolLocationHelperType}]::GetDisplayNameForTargetFrameworkDirectory('$(Root)', {frameworkName}))""",
            properties).ShouldNotBeNull();
    }

    [ModernExpanderOnlyFact]
    public void ModernPropertyFunctionGetDirectoriesWithSearchOption()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var parent = env.CreateFolder();
        var child = env.CreateFolder(Path.Combine(parent.Path, "child"), createFolder: true);
        env.CreateFile(child, "data.txt", "content");
        PropertyDictionary<ProjectPropertyInstance> properties = Properties("Parent", parent.Path);

        ExpandProperties(
            "$([System.IO.Directory]::GetDirectories($(Parent), 'ch*', 'TopDirectoryOnly').Length)",
            properties).ShouldBe("1");
        ExpandProperties(
            "$([System.IO.Directory]::GetFiles($(Parent), '*.txt', 'AllDirectories').Length)",
            properties).ShouldBe("1");
    }

    [Theory]
    [MemberData(nameof(UriExpressions))]
    public void PropertyFunctionUri(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    public static TheoryData<string, string> UriExpressions => new()
    {
        { """$([System.Uri]::new('https://example.com/path').Host)""", "example.com" },
        {
            """$([System.Uri]::new($([System.Uri]::new('https://example.com/root/')), 'path').AbsoluteUri)""",
            "https://example.com/root/path"
        },
        { """$([System.Uri]::CheckHostName('example.com'))""", "Dns" },
        { """$([System.Uri]::CheckSchemeName('https'))""", bool.TrueString },
        { """$([System.Uri]::EscapeDataString('a b'))""", "a%2520b" },
        { """$([System.Uri]::EscapeUriString('https://example.com/a b'))""", "https://example.com/a%2520b" },
        { """$([System.Uri]::HexEscape('A'))""", "%2541" },
        { """$([System.Uri]::IsHexDigit('A'))""", bool.TrueString },
        { """$([System.Uri]::IsHexEncoding('%2541', '0'))""", bool.TrueString },
        { """$([System.Uri]::SchemeDelimiter)""", Uri.SchemeDelimiter },
        { """$([System.Uri]::UnescapeDataString('a%20b'))""", "a b" },
        { """$([System.Uri]::new('https://user:password@example.com:8080/root/file?q=v#f').AbsolutePath)""", "/root/file" },
        { """$([System.Uri]::new('https://example.com/path').AbsoluteUri)""", "https://example.com/path" },
        { """$([System.Uri]::new('https://example.com:8080/path').Authority)""", "example.com:8080" },
        { """$([System.Uri]::new('https://example.com/path').DnsSafeHost)""", "example.com" },
        { """$([System.Uri]::new('https://example.com/path#fragment').Fragment)""", "#fragment" },
        { """$([System.Uri]::new('https://example.com/path').HostNameType)""", "Dns" },
#if NET
        { """$([System.Uri]::new('https://example.com/path').IdnHost)""", "example.com" },
#endif
        { """$([System.Uri]::new('https://example.com/path').IsDefaultPort)""", bool.TrueString },
        { """$([System.Uri]::new('file:///C:/path').IsFile)""", bool.TrueString },
        { """$([System.Uri]::new('https://localhost/path').IsLoopback)""", bool.TrueString },
        { """$([System.Uri]::new('https://example.com/path').IsUnc)""", bool.FalseString },
        { """$([System.Uri]::new(`https://example.com/path?q=v`).OriginalString)""", "https://example.com/path%3fq=v" },
        { """$([System.Uri]::new(`https://example.com/path?q=v`).PathAndQuery)""", "/path%3fq=v" },
        { """$([System.Uri]::new('https://example.com:8080/path').Port)""", "8080" },
        { """$([System.Uri]::new(`https://example.com/path?q=v`).Query)""", "%3fq=v" },
        { """$([System.Uri]::new('https://example.com/path').Scheme)""", "https" },
        { """$([System.Uri]::new('https://example.com/root/path').Segments.GetValue('1'))""", "root/" },
        { """$([System.Uri]::new('https://example.com/path').UserEscaped)""", bool.FalseString },
        { """$([System.Uri]::new('https://user:password@example.com/path').UserInfo)""", "user:password" },
        {
            """$([System.Uri]::new('https://example.com/path').Equals($([System.Uri]::new('https://example.com/path'))))""",
            bool.TrueString
        },
        {
            """$([System.Uri]::new('https://example.com/root/').IsBaseOf($([System.Uri]::new('https://example.com/root/path'))))""",
            bool.TrueString
        },
        {
            """$([System.Uri]::new('https://example.com/root/').MakeRelativeUri($([System.Uri]::new('https://example.com/root/path'))))""",
            "path"
        },
        { """$([System.Uri]::new('https://example.com/path').ToString())""", "https://example.com/path" },
        { """$([System.UriBuilder]::new().Scheme)""", "http" },
        { """$([System.UriBuilder]::new('https://example.com/path').Host)""", "example.com" },
        { """$([System.UriBuilder]::new('https', 'example.com').Uri.AbsoluteUri)""", "https://example.com/" },
        { """$([System.UriBuilder]::new('https', 'example.com', '8080').Port)""", "8080" },
        { """$([System.UriBuilder]::new('https', 'example.com', '8080', 'path').Path)""", "path" },
        { """$([System.UriBuilder]::new('https', 'example.com', '8080', 'path', `?q=v`).Query)""", "%3fq=v" },
        { """$([System.UriBuilder]::new('https://user:password@example.com/path#fragment').Fragment)""", "#fragment" },
        { """$([System.UriBuilder]::new('https://user:password@example.com/path').Password)""", "password" },
        { """$([System.UriBuilder]::new('https://user:password@example.com/path').UserName)""", "user" },
        { """$([System.UriBuilder]::new('https://example.com/path').ToString())""", "https://example.com:443/path" },
    };

    [ModernExpanderOnlyTheory]
    [MemberData(nameof(ModernUriExpressions))]
    public void ModernPropertyFunctionUri(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    public static TheoryData<string, string> ModernUriExpressions => new()
    {
        { """$([System.Uri]::new('/path', 'Relative').IsAbsoluteUri)""", bool.FalseString },
        {
            """$([System.Uri]::new($([System.Uri]::new('https://example.com/root/')), $([System.Uri]::new('path', 'Relative'))).AbsoluteUri)""",
            "https://example.com/root/path"
        },
        {
            """$([System.Uri]::Compare($([System.Uri]::new('https://example.com/a')), $([System.Uri]::new('https://example.com/a')), 'AbsoluteUri', 'SafeUnescaped', 'Ordinal'))""",
            "0"
        },
        { """$([System.Uri]::new('https://example.com:8080/path').GetComponents('HostAndPort', 'SafeUnescaped'))""", "example.com:8080" },
        { """$([System.UriBuilder]::new($([System.Uri]::new('https://example.com/path'))).Path)""", "/path" },
    };

    /// <summary>
    ///  Expand property function that calls a static method  with an instance method chained.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodChained()
        => ExpandProperties($"$([System.DateTime]::Parse('{_dateToParse}').ToString(`yyyy/MM/dd HH:mm:ss`))")
            .ShouldBe(DateTime.Parse(_dateToParse).ToString("yyyy/MM/dd HH:mm:ss"));

    /// <summary>
    ///  Expand property function that calls a static method available only on net46 (Environment.GetFolderPath).
    /// </summary>
    [Fact]
    public void PropertyFunctionGetFolderPath()
        => ExpandProperties("$([System.Environment]::GetFolderPath(SpecialFolder.System))")
            .ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.System));

    /// <summary>
    ///  The test exercises: RuntimeInformation / OSPlatform usage, static method invocation, static property
    ///  invocation, method invocation expression as argument, call chain expression as argument.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuntimeInformationExpressions))]
    public void PropertyFunctionRuntimeInformation(string propertyFunction, string expectedExpansion)
        => ExpandProperties(propertyFunction)
            .ShouldBe(expectedExpansion);

    public static TheoryData<string, string> RuntimeInformationExpressions
    {
        get
        {
            string platform = Helpers.GetOSPlatformAsString();

            return new()
            {
                {
                    $"$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::Create($([System.Runtime.InteropServices.OSPlatform]::{platform}.ToString())))))",
                    true.ToResultString()
                },
                {
                    $"$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::{platform})))",
                    true.ToResultString()
                },
                {
                    "$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)",
                    RuntimeInformation.OSArchitecture.ToString()
                },
                { $"$([MSBuild]::IsOSPlatform({platform}))", true.ToResultString() },
            };
        }
    }

    [Theory]
    [InlineData("$([System.Version]::new().ToString())", "0.0")]
    [InlineData("$([System.Version]::new('1.2.3.4').Major)", "1")]
    [InlineData("$([System.Version]::new(1, 2).Minor)", "2")]
    [InlineData("$([System.Version]::new(1, 2, 3).Build)", "3")]
    [InlineData("$([System.Version]::new(1, 2, 3, 4).Revision)", "4")]
    public void PropertyFunctionVersionMembers(string expression, string expected)
        => ExpandProperties(expression).ShouldBe(expected);

    [Theory]
    [MemberData(nameof(DateTimeMemberExpressions))]
    [UseInvariantCulture]
    public void PropertyFunctionDateTimeMembers(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> DateTimeMemberExpressions
    {
        get
        {
            TheoryData<string, string> data = new();
            DateTime value = new(2026, 9, 23, 1, 2, 3, 4, DateTimeKind.Utc);
            DateTime other = value.AddDays(1);
            TimeSpan oneDay = TimeSpan.FromDays(1);
            const string culture = "$([System.Globalization.CultureInfo]::CurrentUICulture)";
            const string oneDayExpression = "$([System.TimeSpan]::FromDays('1'))";
            const string valueExpression = "[System.DateTime]::new('2026', '9', '23', '1', '2', '3', '4', 'DateTimeKind.Utc')";
            const string valueArgument = "$([System.DateTime]::new('2026', '9', '23', '1', '2', '3', '4', 'DateTimeKind.Utc'))";
            const string otherExpression = "$([System.DateTime]::new('2026', '9', '24', '1', '2', '3', '4', 'DateTimeKind.Utc'))";

            AddDateTime("[System.DateTime]::new('639047845230040000')", new DateTime(639047845230040000));
            AddDateTime(
                "[System.DateTime]::new('639047845230040000', 'DateTimeKind.Utc')",
                new DateTime(639047845230040000, DateTimeKind.Utc));
            AddDateTime("[System.DateTime]::new('2026', '9', '23')", new DateTime(2026, 9, 23));
            AddDateTime("[System.DateTime]::new('2026', '9', '23', '1', '2', '3')", new DateTime(2026, 9, 23, 1, 2, 3));
            AddDateTime(
                "[System.DateTime]::new('2026', '9', '23', '1', '2', '3', 'DateTimeKind.Utc')",
                new DateTime(2026, 9, 23, 1, 2, 3, DateTimeKind.Utc));
            AddDateTime("[System.DateTime]::new('2026', '9', '23', '1', '2', '3', '4')", new DateTime(2026, 9, 23, 1, 2, 3, 4));
            AddDateTime(valueExpression, value);
#if NET
            AddDateTime(
                "[System.DateTime]::new('2026', '9', '23', '1', '2', '3', '4', '5')",
                new DateTime(2026, 9, 23, 1, 2, 3, 4, 5));
            AddDateTime(
                "[System.DateTime]::new('2026', '9', '23', '1', '2', '3', '4', '5', 'DateTimeKind.Utc')",
                new DateTime(2026, 9, 23, 1, 2, 3, 4, 5, DateTimeKind.Utc));
#endif

            data.Add($"$([System.DateTime]::Compare({valueArgument}, {otherExpression}))", "-1");
            data.Add("$([System.DateTime]::DaysInMonth('2024', '2'))", "29");
            data.Add($"$([System.DateTime]::Equals({valueArgument}, {valueArgument}))", "True");
            AddDateTime($"[System.DateTime]::FromBinary('{value.ToBinary()}')", DateTime.FromBinary(value.ToBinary()));
            AddDateTime($"[System.DateTime]::FromFileTime('{value.ToFileTimeUtc()}')", DateTime.FromFileTime(value.ToFileTimeUtc()));
            AddDateTime($"[System.DateTime]::FromFileTimeUtc('{value.ToFileTimeUtc()}')", DateTime.FromFileTimeUtc(value.ToFileTimeUtc()));
            AddDateTime($"[System.DateTime]::FromOADate('{value.ToOADate().ToString(CultureInfo.InvariantCulture)}')", DateTime.FromOADate(value.ToOADate()));
            data.Add("$([System.DateTime]::IsLeapYear('2024'))", "True");
            data.Add("$([System.DateTime]::MaxValue.Year)", "9999");
            data.Add("$([System.DateTime]::MinValue.Year)", "1");
            data.Add("$([System.DateTime]::Now.Kind)", DateTimeKind.Local.ToResultString()!);
            data.Add("$([System.DateTime]::Today.TimeOfDay.Ticks)", "0");
            data.Add("$([System.DateTime]::UtcNow.Kind)", DateTimeKind.Utc.ToResultString()!);
#if NET
            AddDateTime("[System.DateTime]::UnixEpoch", DateTime.UnixEpoch);
#endif
            AddDateTime("[System.DateTime]::Parse('2026-09-23')", new DateTime(2026, 9, 23));
            AddDateTime($"[System.DateTime]::Parse('2026-09-23', {culture})", new DateTime(2026, 9, 23));
            AddDateTime(
                "[System.DateTime]::Parse('2026-09-23', null, 'DateTimeStyles.AssumeUniversal')",
                DateTime.Parse("2026-09-23", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
            AddDateTime(
                $"[System.DateTime]::ParseExact('2026-09-23', 'yyyy-MM-dd', {culture})",
                new DateTime(2026, 9, 23));
            AddDateTime(
                "[System.DateTime]::ParseExact('2026-09-23', 'yyyy-MM-dd', null, 'DateTimeStyles.AssumeUniversal')",
                DateTime.ParseExact("2026-09-23", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
            AddDateTime(
                $"[System.DateTime]::SpecifyKind($([System.DateTime]::Parse('2026-09-23')), 'DateTimeKind.Utc')",
                new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc));

            AddDateTime($"[System.DateTime]::op_Addition({valueArgument}, {oneDayExpression})", value + oneDay);
            data.Add($"$([System.DateTime]::op_Equality({valueArgument}, {valueArgument}))", "True");
            data.Add($"$([System.DateTime]::op_GreaterThan({otherExpression}, {valueArgument}))", "True");
            data.Add($"$([System.DateTime]::op_GreaterThanOrEqual({otherExpression}, {valueArgument}))", "True");
            data.Add($"$([System.DateTime]::op_Inequality({valueArgument}, {otherExpression}))", "True");
            data.Add($"$([System.DateTime]::op_LessThan({valueArgument}, {otherExpression}))", "True");
            data.Add($"$([System.DateTime]::op_LessThanOrEqual({valueArgument}, {otherExpression}))", "True");
            AddDateTime($"[System.DateTime]::op_Subtraction({otherExpression}, {oneDayExpression})", value);
            AddTimeSpan($"[System.DateTime]::op_Subtraction({otherExpression}, {valueArgument})", oneDay);

            AddDateTime($"{valueExpression}.Date", value.Date);
            AddProperty(nameof(DateTime.Day), value.Day);
            AddProperty(nameof(DateTime.DayOfWeek), value.DayOfWeek);
            AddProperty(nameof(DateTime.DayOfYear), value.DayOfYear);
            AddProperty(nameof(DateTime.Hour), value.Hour);
            AddProperty(nameof(DateTime.Kind), value.Kind);
            AddProperty(nameof(DateTime.Millisecond), value.Millisecond);
            AddProperty(nameof(DateTime.Minute), value.Minute);
            AddProperty(nameof(DateTime.Month), value.Month);
            AddProperty(nameof(DateTime.Second), value.Second);
            AddProperty(nameof(DateTime.Ticks), value.Ticks);
            AddTimeSpan($"{valueExpression}.TimeOfDay", value.TimeOfDay);
            AddProperty(nameof(DateTime.Year), value.Year);
#if NET
            AddProperty(nameof(DateTime.Microsecond), value.Microsecond);
            AddProperty(nameof(DateTime.Nanosecond), value.Nanosecond);
#endif

            AddDateTime($"{valueExpression}.Add({oneDayExpression})", value.Add(oneDay));
            AddDateTime($"{valueExpression}.AddDays('1')", value.AddDays(1));
            AddDateTime($"{valueExpression}.AddHours('1')", value.AddHours(1));
#if NET
            AddDateTime($"{valueExpression}.AddMicroseconds('1')", value.AddMicroseconds(1));
#endif
            AddDateTime($"{valueExpression}.AddMilliseconds('1')", value.AddMilliseconds(1));
            AddDateTime($"{valueExpression}.AddMinutes('1')", value.AddMinutes(1));
            AddDateTime($"{valueExpression}.AddMonths('1')", value.AddMonths(1));
            AddDateTime($"{valueExpression}.AddSeconds('1')", value.AddSeconds(1));
            AddDateTime($"{valueExpression}.AddTicks('1')", value.AddTicks(1));
            AddDateTime($"{valueExpression}.AddYears('1')", value.AddYears(1));
            data.Add($"$({valueExpression}.CompareTo({otherExpression}))", value.CompareTo(other).ToResultString());
            data.Add($"$({valueExpression}.Equals({otherExpression}))", value.Equals(other).ToResultString());
            data.Add(
                $"$({valueExpression}.GetDateTimeFormats().GetValue('0'))",
                value.GetDateTimeFormats(CultureInfo.InvariantCulture)[0]);
            data.Add($"$({valueExpression}.GetDateTimeFormats('O').GetValue('0'))", value.GetDateTimeFormats('O')[0]);
            data.Add(
                $"$({valueExpression}.GetDateTimeFormats({culture}).GetValue('0'))",
                value.GetDateTimeFormats(CultureInfo.InvariantCulture)[0]);
            data.Add(
                $"$({valueExpression}.GetDateTimeFormats($([System.Convert]::ToChar('O')), {culture}).GetValue('0'))",
                value.GetDateTimeFormats('O', CultureInfo.InvariantCulture)[0]);
            data.Add($"$({valueExpression}.GetHashCode())", value.GetHashCode().ToResultString());
            data.Add($"$({valueExpression}.GetTypeCode())", TypeCode.DateTime.ToResultString()!);
            data.Add($"$({valueExpression}.IsDaylightSavingTime())", value.IsDaylightSavingTime().ToResultString());
            AddDateTime($"{valueExpression}.Subtract({oneDayExpression})", value.Subtract(oneDay));
            AddTimeSpan($"{valueExpression}.Subtract({otherExpression})", value.Subtract(other));
            data.Add($"$({valueExpression}.ToBinary())", value.ToBinary().ToResultString());
            data.Add($"$({valueExpression}.ToFileTime())", value.ToFileTime().ToResultString());
            data.Add($"$({valueExpression}.ToFileTimeUtc())", value.ToFileTimeUtc().ToResultString());
            AddDateTime($"{valueExpression}.ToLocalTime()", value.ToLocalTime());
            data.Add($"$({valueExpression}.ToLongDateString())", value.ToString("D", CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToLongTimeString())", value.ToString("T", CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToOADate())", value.ToOADate().ToResultString());
            data.Add($"$({valueExpression}.ToShortDateString())", value.ToString("d", CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToShortTimeString())", value.ToString("t", CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToString())", value.ToString(CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToString('O'))", value.ToString("O"));
            data.Add($"$({valueExpression}.ToString({culture}))", value.ToString(CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToString('O', {culture}))", value.ToString("O", CultureInfo.InvariantCulture));
            AddDateTime($"{valueExpression}.ToUniversalTime()", value.ToUniversalTime());

            return data;

            void AddDateTime(string invocation, DateTime expected)
                => data.Add($"$({invocation}.ToBinary())", expected.ToBinary().ToResultString());

            void AddTimeSpan(string invocation, TimeSpan expected)
                => data.Add($"$({invocation}.Ticks)", expected.Ticks.ToResultString());

            void AddProperty(string propertyName, object expected)
                => data.Add($"$({valueExpression}.{propertyName})", expected.ToResultString()!);
        }
    }

    [Theory]
    [MemberData(nameof(DateTimeOffsetMemberExpressions))]
    [UseInvariantCulture]
    public void PropertyFunctionDateTimeOffsetMembers(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> DateTimeOffsetMemberExpressions
    {
        get
        {
            TheoryData<string, string> data = new();
            DateTimeOffset value = new(2026, 9, 23, 1, 2, 3, 4, TimeSpan.FromHours(2));
            DateTimeOffset other = value.AddDays(1);
            DateTimeOffset sameInstant = value.ToOffset(TimeSpan.FromHours(1));
            TimeSpan oneDay = TimeSpan.FromDays(1);
            const string culture = "$([System.Globalization.CultureInfo]::CurrentUICulture)";
            const string oneDayExpression = "$([System.TimeSpan]::FromDays('1'))";
            const string twoHoursExpression = "$([System.TimeSpan]::FromHours('2'))";
            const string valueExpression =
                "[System.DateTimeOffset]::new('2026', '9', '23', '1', '2', '3', '4', $([System.TimeSpan]::FromHours('2')))";
            const string valueArgument =
                "$([System.DateTimeOffset]::new('2026', '9', '23', '1', '2', '3', '4', $([System.TimeSpan]::FromHours('2'))))";
            const string otherExpression =
                "$([System.DateTimeOffset]::new('2026', '9', '24', '1', '2', '3', '4', $([System.TimeSpan]::FromHours('2'))))";
            string sameInstantExpression =
                $"$([System.DateTimeOffset]::new('{sameInstant.Ticks}', $([System.TimeSpan]::FromHours('1'))))";

            AddDateTimeOffset(
                "[System.DateTimeOffset]::new($([System.DateTime]::new('2026', '9', '23', '1', '2', '3', 'DateTimeKind.Utc')))",
                new DateTimeOffset(new DateTime(2026, 9, 23, 1, 2, 3, DateTimeKind.Utc)));
            AddDateTimeOffset(
                $"[System.DateTimeOffset]::new($([System.DateTime]::new('2026', '9', '23', '1', '2', '3')), {twoHoursExpression})",
                new DateTimeOffset(new DateTime(2026, 9, 23, 1, 2, 3), TimeSpan.FromHours(2)));
            AddDateTimeOffset(
                $"[System.DateTimeOffset]::new('{value.Ticks}', {twoHoursExpression})",
                new DateTimeOffset(value.Ticks, TimeSpan.FromHours(2)));
            AddDateTimeOffset(
                $"[System.DateTimeOffset]::new('2026', '9', '23', '1', '2', '3', {twoHoursExpression})",
                new DateTimeOffset(2026, 9, 23, 1, 2, 3, TimeSpan.FromHours(2)));
            AddDateTimeOffset(valueExpression, value);
#if NET
            AddDateTimeOffset(
                $"[System.DateTimeOffset]::new('2026', '9', '23', '1', '2', '3', '4', '5', {twoHoursExpression})",
                new DateTimeOffset(2026, 9, 23, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)));
#endif

            data.Add($"$([System.DateTimeOffset]::Compare({valueArgument}, {otherExpression}))", "-1");
            data.Add($"$([System.DateTimeOffset]::Equals({valueArgument}, {valueArgument}))", "True");
            AddDateTimeOffset(
                $"[System.DateTimeOffset]::FromFileTime('{value.ToFileTime()}')",
                DateTimeOffset.FromFileTime(value.ToFileTime()));
            AddDateTimeOffset("[System.DateTimeOffset]::FromUnixTimeMilliseconds('1')", DateTimeOffset.FromUnixTimeMilliseconds(1));
            AddDateTimeOffset("[System.DateTimeOffset]::FromUnixTimeSeconds('1')", DateTimeOffset.FromUnixTimeSeconds(1));
            AddDateTimeOffset("[System.DateTimeOffset]::MaxValue", DateTimeOffset.MaxValue);
            AddDateTimeOffset("[System.DateTimeOffset]::MinValue", DateTimeOffset.MinValue);
            data.Add(
                "$([System.DateTimeOffset]::Now.Offset.Ticks)",
                TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Ticks.ToResultString());
            data.Add("$([System.DateTimeOffset]::UtcNow.Offset.Ticks)", "0");
#if NET
            AddDateTimeOffset("[System.DateTimeOffset]::UnixEpoch", DateTimeOffset.UnixEpoch);
#endif
            AddDateTimeOffset(
                "[System.DateTimeOffset]::Parse('2026-09-23 +02:00')",
                DateTimeOffset.Parse("2026-09-23 +02:00", CultureInfo.InvariantCulture));
            AddDateTimeOffset(
                $"[System.DateTimeOffset]::Parse('2026-09-23 +02:00', {culture})",
                DateTimeOffset.Parse("2026-09-23 +02:00", CultureInfo.InvariantCulture));
            AddDateTimeOffset(
                "[System.DateTimeOffset]::Parse('2026-09-23 +02:00', null, 'DateTimeStyles.None')",
                DateTimeOffset.Parse("2026-09-23 +02:00", CultureInfo.InvariantCulture, DateTimeStyles.None));
            AddDateTimeOffset(
                $"[System.DateTimeOffset]::ParseExact('2026-09-23 +02:00', 'yyyy-MM-dd zzz', {culture})",
                DateTimeOffset.ParseExact("2026-09-23 +02:00", "yyyy-MM-dd zzz", CultureInfo.InvariantCulture));
            AddDateTimeOffset(
                "[System.DateTimeOffset]::ParseExact('2026-09-23 +02:00', 'yyyy-MM-dd zzz', null, 'DateTimeStyles.None')",
                DateTimeOffset.ParseExact(
                    "2026-09-23 +02:00",
                    "yyyy-MM-dd zzz",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None));

            AddDateTimeOffset($"[System.DateTimeOffset]::op_Addition({valueArgument}, {oneDayExpression})", value + oneDay);
            data.Add($"$([System.DateTimeOffset]::op_Equality({valueArgument}, {valueArgument}))", "True");
            data.Add($"$([System.DateTimeOffset]::op_GreaterThan({otherExpression}, {valueArgument}))", "True");
            data.Add($"$([System.DateTimeOffset]::op_GreaterThanOrEqual({otherExpression}, {valueArgument}))", "True");
            AddDateTimeOffset(
                "[System.DateTimeOffset]::op_Implicit($([System.DateTime]::new('2026', '9', '23', '0', '0', '0', 'DateTimeKind.Utc')))",
                new DateTimeOffset(new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc)));
            data.Add($"$([System.DateTimeOffset]::op_Inequality({valueArgument}, {otherExpression}))", "True");
            data.Add($"$([System.DateTimeOffset]::op_LessThan({valueArgument}, {otherExpression}))", "True");
            data.Add($"$([System.DateTimeOffset]::op_LessThanOrEqual({valueArgument}, {otherExpression}))", "True");
            AddDateTimeOffset($"[System.DateTimeOffset]::op_Subtraction({otherExpression}, {oneDayExpression})", value);
            AddTimeSpan($"[System.DateTimeOffset]::op_Subtraction({otherExpression}, {valueArgument})", oneDay);

            AddDateTime($"{valueExpression}.Date", value.Date);
            AddDateTime($"{valueExpression}.DateTime", value.DateTime);
            AddProperty(nameof(DateTimeOffset.Day), value.Day);
            AddProperty(nameof(DateTimeOffset.DayOfWeek), value.DayOfWeek);
            AddProperty(nameof(DateTimeOffset.DayOfYear), value.DayOfYear);
            AddProperty(nameof(DateTimeOffset.Hour), value.Hour);
            AddDateTime($"{valueExpression}.LocalDateTime", value.LocalDateTime);
            AddProperty(nameof(DateTimeOffset.Millisecond), value.Millisecond);
            AddProperty(nameof(DateTimeOffset.Minute), value.Minute);
            AddProperty(nameof(DateTimeOffset.Month), value.Month);
            AddTimeSpan($"{valueExpression}.Offset", value.Offset);
            AddProperty(nameof(DateTimeOffset.Second), value.Second);
            AddProperty(nameof(DateTimeOffset.Ticks), value.Ticks);
            AddTimeSpan($"{valueExpression}.TimeOfDay", value.TimeOfDay);
            AddDateTime($"{valueExpression}.UtcDateTime", value.UtcDateTime);
            AddProperty(nameof(DateTimeOffset.Year), value.Year);
#if NET
            AddProperty(nameof(DateTimeOffset.Microsecond), value.Microsecond);
            AddProperty(nameof(DateTimeOffset.Nanosecond), value.Nanosecond);
            AddProperty(nameof(DateTimeOffset.TotalOffsetMinutes), value.TotalOffsetMinutes);
            AddProperty(nameof(DateTimeOffset.UtcTicks), value.UtcTicks);
#endif

            AddDateTimeOffset($"{valueExpression}.Add({oneDayExpression})", value.Add(oneDay));
            AddDateTimeOffset($"{valueExpression}.AddDays('1')", value.AddDays(1));
            AddDateTimeOffset($"{valueExpression}.AddHours('1')", value.AddHours(1));
#if NET
            AddDateTimeOffset($"{valueExpression}.AddMicroseconds('1')", value.AddMicroseconds(1));
#endif
            AddDateTimeOffset($"{valueExpression}.AddMilliseconds('1')", value.AddMilliseconds(1));
            AddDateTimeOffset($"{valueExpression}.AddMinutes('1')", value.AddMinutes(1));
            AddDateTimeOffset($"{valueExpression}.AddMonths('1')", value.AddMonths(1));
            AddDateTimeOffset($"{valueExpression}.AddSeconds('1')", value.AddSeconds(1));
            AddDateTimeOffset($"{valueExpression}.AddTicks('1')", value.AddTicks(1));
            AddDateTimeOffset($"{valueExpression}.AddYears('1')", value.AddYears(1));
            data.Add($"$({valueExpression}.CompareTo({otherExpression}))", value.CompareTo(other).ToResultString());
            data.Add($"$({valueExpression}.Equals({otherExpression}))", value.Equals(other).ToResultString());
            data.Add($"$({valueExpression}.EqualsExact({sameInstantExpression}))", "False");
            data.Add($"$({valueExpression}.GetHashCode())", value.GetHashCode().ToResultString());
            AddDateTimeOffset($"{valueExpression}.Subtract({oneDayExpression})", value.Subtract(oneDay));
            AddTimeSpan($"{valueExpression}.Subtract({otherExpression})", value.Subtract(other));
            data.Add($"$({valueExpression}.ToFileTime())", value.ToFileTime().ToResultString());
            AddDateTimeOffset($"{valueExpression}.ToLocalTime()", value.ToLocalTime());
            AddDateTimeOffset($"{valueExpression}.ToOffset($([System.TimeSpan]::FromHours('3')))", value.ToOffset(TimeSpan.FromHours(3)));
            data.Add($"$({valueExpression}.ToString())", value.ToString(CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToString('O'))", value.ToString("O", CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToString({culture}))", value.ToString(CultureInfo.InvariantCulture));
            data.Add($"$({valueExpression}.ToString('O', {culture}))", value.ToString("O", CultureInfo.InvariantCulture));
            AddDateTimeOffset($"{valueExpression}.ToUniversalTime()", value.ToUniversalTime());
            data.Add($"$({valueExpression}.ToUnixTimeMilliseconds())", value.ToUnixTimeMilliseconds().ToResultString());
            data.Add($"$({valueExpression}.ToUnixTimeSeconds())", value.ToUnixTimeSeconds().ToResultString());

            return data;

            void AddDateTimeOffset(string invocation, DateTimeOffset expected)
                => data.Add($"$({invocation}.ToString('O', {culture}))", expected.ToString("O", CultureInfo.InvariantCulture));

            void AddDateTime(string invocation, DateTime expected)
                => data.Add($"$({invocation}.ToBinary())", expected.ToBinary().ToResultString());

            void AddTimeSpan(string invocation, TimeSpan expected)
                => data.Add($"$({invocation}.Ticks)", expected.Ticks.ToResultString());

            void AddProperty(string propertyName, object expected)
                => data.Add($"$({valueExpression}.{propertyName})", expected.ToResultString()!);
        }
    }

    [Theory]
    [MemberData(nameof(CultureInfoMemberExpressions))]
    public void PropertyFunctionCultureInfoMembers(string expression, string? expected)
    {
        string? result = ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander);

        if (expected is null)
        {
            result.ShouldNotBeNull();
        }
        else
        {
            result.ShouldBe(expected);
        }
    }

    public static TheoryData<string, string?> CultureInfoMemberExpressions
    {
        get
        {
            const string culture = "$([System.Globalization.CultureInfo]::new('en-US')";

            return new()
            {
                { "$([System.Globalization.CultureInfo]::CurrentUICulture)", null },
                { "$([System.Globalization.CultureInfo]::GetCultureInfo($([System.Convert]::ToInt32(1033))).Name)", "en-US" },
                { "$([System.Globalization.CultureInfo]::GetCultureInfo('en-US', 'fr-FR').Name)", "en-US" },
#if NET
                {
                    "$([System.Globalization.CultureInfo]::GetCultureInfo('en-US', $([System.Convert]::ToBoolean('true'))).Name)",
                    "en-US"
                },
#endif
                { $"{culture}.Calendar)", null },
                { $"{culture}.CompareInfo)", null },
                { $"{culture}.CultureTypes)", null },
                { $"{culture}.DateTimeFormat)", null },
                { $"{culture}.DisplayName)", null },
                { $"{culture}.EnglishName)", null },
                { $"{culture}.IetfLanguageTag)", "en-US" },
                { $"{culture}.IsNeutralCulture)", "False" },
                { $"{culture}.IsReadOnly)", "False" },
                { $"{culture}.KeyboardLayoutId)", "1033" },
                { $"{culture}.LCID)", "1033" },
                { $"{culture}.Name)", "en-US" },
                { $"{culture}.NativeName)", null },
                { $"{culture}.NumberFormat)", null },
                { $"{culture}.OptionalCalendars)", null },
                { $"{culture}.Parent.Name)", "en" },
                { $"{culture}.TextInfo)", null },
                { $"{culture}.ThreeLetterISOLanguageName)", "eng" },
                { $"{culture}.ThreeLetterWindowsLanguageName)", "ENU" },
                { $"{culture}.TwoLetterISOLanguageName)", "en" },
                { $"{culture}.UseUserOverride)", "True" },
                { $"{culture}.ClearCachedData())", string.Empty },
                { $"{culture}.Clone().Name)", "en-US" },
                { $"{culture}.Equals($([System.Globalization.CultureInfo]::new('en-US'))))", "True" },
                { $"{culture}.GetConsoleFallbackUICulture().Name)", "en-US" },
                { $"{culture}.GetHashCode())", null },
                {
                    $"{culture}.set_DateTimeFormat($([System.Globalization.CultureInfo]::new('fr-FR').DateTimeFormat)))",
                    string.Empty
                },
                {
                    $"{culture}.set_NumberFormat($([System.Globalization.CultureInfo]::new('fr-FR').NumberFormat)))",
                    string.Empty
                },
                { $"{culture}.ToString())", "en-US" },
            };
        }
    }

    [Theory]
    [MemberData(nameof(TimeSpanMemberExpressions))]
    [UseInvariantCulture]
    public void PropertyFunctionTimeSpanMembers(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> TimeSpanMemberExpressions
    {
        get
        {
            TheoryData<string, string> data = new();
            TimeSpan value = new(1, 2, 3, 4, 5);
            TimeSpan oneHour = TimeSpan.FromHours(1);
            TimeSpan twoHours = TimeSpan.FromHours(2);
            const string culture = "$([System.Globalization.CultureInfo]::CurrentUICulture)";
            const string oneHourExpression = "$([System.TimeSpan]::FromHours('1'))";
            const string twoHoursExpression = "$([System.TimeSpan]::FromHours('2'))";
            const string valueExpression = "[System.TimeSpan]::new('1', '2', '3', '4', '5')";

            AddTimeSpan("[System.TimeSpan]::new($([System.Convert]::ToInt64('36000000000')))", oneHour);
            AddTimeSpan("[System.TimeSpan]::new('1', '2', '3')", new TimeSpan(1, 2, 3));
            AddTimeSpan("[System.TimeSpan]::new('1', '2', '3', '4')", new TimeSpan(1, 2, 3, 4));
            AddTimeSpan("[System.TimeSpan]::new('1', '2', '3', '4', '5')", value);
#if NET
            AddTimeSpan("[System.TimeSpan]::new('1', '2', '3', '4', '5', '6')", new TimeSpan(1, 2, 3, 4, 5, 6));
#endif

            data.Add($"$([System.TimeSpan]::Compare({oneHourExpression}, {twoHoursExpression}))", "-1");
            data.Add($"$([System.TimeSpan]::Equals({oneHourExpression}, {oneHourExpression}))", "True");
            AddTimeSpan("[System.TimeSpan]::FromDays('1')", TimeSpan.FromDays(1));
            AddTimeSpan("[System.TimeSpan]::FromHours('1')", oneHour);
            AddTimeSpan("[System.TimeSpan]::FromMilliseconds('1')", TimeSpan.FromMilliseconds(1));
            AddTimeSpan("[System.TimeSpan]::FromMinutes('1')", TimeSpan.FromMinutes(1));
            AddTimeSpan("[System.TimeSpan]::FromSeconds('1')", TimeSpan.FromSeconds(1));
            AddTimeSpan("[System.TimeSpan]::FromTicks('1')", TimeSpan.FromTicks(1));
            AddTimeSpan("[System.TimeSpan]::Parse('01:02:03')", TimeSpan.Parse("01:02:03"));
            AddTimeSpan(
                $"[System.TimeSpan]::Parse('01:02:03', {culture})",
                TimeSpan.Parse("01:02:03", CultureInfo.CurrentUICulture));
            AddTimeSpan(
                $"[System.TimeSpan]::ParseExact('01:02:03', 'c', {culture})",
                TimeSpan.ParseExact("01:02:03", "c", CultureInfo.CurrentUICulture));
            AddTimeSpan("[System.TimeSpan]::MaxValue", TimeSpan.MaxValue);
            AddTimeSpan("[System.TimeSpan]::MinValue", TimeSpan.MinValue);
            AddTimeSpan("[System.TimeSpan]::Zero", TimeSpan.Zero);

            AddTimeSpan($"[System.TimeSpan]::op_Addition({oneHourExpression}, {twoHoursExpression})", oneHour + twoHours);
#if NET
            data.Add($"$([System.TimeSpan]::op_Division({twoHoursExpression}, {oneHourExpression}))", "2");
            AddTimeSpan(
                $"[System.TimeSpan]::op_Division({twoHoursExpression}, $([System.Convert]::ToDouble('2')))",
                twoHours / 2);
#endif
            data.Add($"$([System.TimeSpan]::op_Equality({oneHourExpression}, {oneHourExpression}))", "True");
            data.Add($"$([System.TimeSpan]::op_GreaterThan({twoHoursExpression}, {oneHourExpression}))", "True");
            data.Add($"$([System.TimeSpan]::op_GreaterThanOrEqual({twoHoursExpression}, {oneHourExpression}))", "True");
            data.Add($"$([System.TimeSpan]::op_Inequality({oneHourExpression}, {twoHoursExpression}))", "True");
            data.Add($"$([System.TimeSpan]::op_LessThan({oneHourExpression}, {twoHoursExpression}))", "True");
            data.Add($"$([System.TimeSpan]::op_LessThanOrEqual({oneHourExpression}, {twoHoursExpression}))", "True");
#if NET
            AddTimeSpan(
                $"[System.TimeSpan]::op_Multiply({twoHoursExpression}, $([System.Convert]::ToDouble('2')))",
                twoHours * 2);
            AddTimeSpan(
                $"[System.TimeSpan]::op_Multiply($([System.Convert]::ToDouble('2')), {twoHoursExpression})",
                2 * twoHours);
#endif
            AddTimeSpan($"[System.TimeSpan]::op_Subtraction({twoHoursExpression}, {oneHourExpression})", oneHour);
            AddTimeSpan($"[System.TimeSpan]::op_UnaryNegation({oneHourExpression})", -oneHour);
            AddTimeSpan($"[System.TimeSpan]::op_UnaryPlus({oneHourExpression})", oneHour);

            AddProperty(nameof(TimeSpan.Days), value.Days);
            AddProperty(nameof(TimeSpan.Hours), value.Hours);
            AddProperty(nameof(TimeSpan.Milliseconds), value.Milliseconds);
            AddProperty(nameof(TimeSpan.Minutes), value.Minutes);
            AddProperty(nameof(TimeSpan.Seconds), value.Seconds);
            AddProperty(nameof(TimeSpan.Ticks), value.Ticks);
            AddProperty(nameof(TimeSpan.TotalDays), value.TotalDays);
            AddProperty(nameof(TimeSpan.TotalHours), value.TotalHours);
            AddProperty(nameof(TimeSpan.TotalMilliseconds), value.TotalMilliseconds);
            AddProperty(nameof(TimeSpan.TotalMinutes), value.TotalMinutes);
            AddProperty(nameof(TimeSpan.TotalSeconds), value.TotalSeconds);
#if NET
            AddProperty(nameof(TimeSpan.Microseconds), value.Microseconds);
            AddProperty(nameof(TimeSpan.Nanoseconds), value.Nanoseconds);
            AddProperty(nameof(TimeSpan.TotalMicroseconds), value.TotalMicroseconds);
            AddProperty(nameof(TimeSpan.TotalNanoseconds), value.TotalNanoseconds);

            AddTimeSpan("[System.TimeSpan]::FromDays($([System.Convert]::ToInt32('1')))", TimeSpan.FromDays(1));
            AddTimeSpan("[System.TimeSpan]::FromDays('1', '2', '3', '4', '5', '6')", TimeSpan.FromDays(1, 2, 3, 4, 5, 6));
            AddTimeSpan("[System.TimeSpan]::FromHours($([System.Convert]::ToInt32('1')))", TimeSpan.FromHours(1));
            AddTimeSpan("[System.TimeSpan]::FromHours('1', '2', '3', '4', '5')", TimeSpan.FromHours(1, 2, 3, 4, 5));
            AddTimeSpan("[System.TimeSpan]::FromMicroseconds($([System.Convert]::ToInt64('1')))", TimeSpan.FromMicroseconds(1L));
            AddTimeSpan("[System.TimeSpan]::FromMicroseconds($([System.Convert]::ToDouble('1.5')))", TimeSpan.FromMicroseconds(1.5));
            AddTimeSpan("[System.TimeSpan]::FromMilliseconds($([System.Convert]::ToInt64('1')))", TimeSpan.FromMilliseconds(1L));
            AddTimeSpan("[System.TimeSpan]::FromMilliseconds('1', '2')", TimeSpan.FromMilliseconds(1, 2));
            AddTimeSpan("[System.TimeSpan]::FromMinutes($([System.Convert]::ToInt64('1')))", TimeSpan.FromMinutes(1L));
            AddTimeSpan("[System.TimeSpan]::FromMinutes('1', '2', '3', '4')", TimeSpan.FromMinutes(1, 2, 3, 4));
            AddTimeSpan("[System.TimeSpan]::FromSeconds($([System.Convert]::ToInt64('1')))", TimeSpan.FromSeconds(1L));
            AddTimeSpan("[System.TimeSpan]::FromSeconds('1', '2', '3')", TimeSpan.FromSeconds(1, 2, 3));

            AddStaticField(nameof(TimeSpan.HoursPerDay), TimeSpan.HoursPerDay);
            AddStaticField(nameof(TimeSpan.MicrosecondsPerDay), TimeSpan.MicrosecondsPerDay);
            AddStaticField(nameof(TimeSpan.MicrosecondsPerHour), TimeSpan.MicrosecondsPerHour);
            AddStaticField(nameof(TimeSpan.MicrosecondsPerMillisecond), TimeSpan.MicrosecondsPerMillisecond);
            AddStaticField(nameof(TimeSpan.MicrosecondsPerMinute), TimeSpan.MicrosecondsPerMinute);
            AddStaticField(nameof(TimeSpan.MicrosecondsPerSecond), TimeSpan.MicrosecondsPerSecond);
            AddStaticField(nameof(TimeSpan.MillisecondsPerDay), TimeSpan.MillisecondsPerDay);
            AddStaticField(nameof(TimeSpan.MillisecondsPerHour), TimeSpan.MillisecondsPerHour);
            AddStaticField(nameof(TimeSpan.MillisecondsPerMinute), TimeSpan.MillisecondsPerMinute);
            AddStaticField(nameof(TimeSpan.MillisecondsPerSecond), TimeSpan.MillisecondsPerSecond);
            AddStaticField(nameof(TimeSpan.MinutesPerDay), TimeSpan.MinutesPerDay);
            AddStaticField(nameof(TimeSpan.MinutesPerHour), TimeSpan.MinutesPerHour);
            AddStaticField(nameof(TimeSpan.NanosecondsPerTick), TimeSpan.NanosecondsPerTick);
            AddStaticField(nameof(TimeSpan.SecondsPerDay), TimeSpan.SecondsPerDay);
            AddStaticField(nameof(TimeSpan.SecondsPerHour), TimeSpan.SecondsPerHour);
            AddStaticField(nameof(TimeSpan.SecondsPerMinute), TimeSpan.SecondsPerMinute);
            AddStaticField(nameof(TimeSpan.TicksPerDay), TimeSpan.TicksPerDay);
            AddStaticField(nameof(TimeSpan.TicksPerHour), TimeSpan.TicksPerHour);
            AddStaticField(nameof(TimeSpan.TicksPerMicrosecond), TimeSpan.TicksPerMicrosecond);
            AddStaticField(nameof(TimeSpan.TicksPerMillisecond), TimeSpan.TicksPerMillisecond);
            AddStaticField(nameof(TimeSpan.TicksPerMinute), TimeSpan.TicksPerMinute);
            AddStaticField(nameof(TimeSpan.TicksPerSecond), TimeSpan.TicksPerSecond);
#endif

            AddTimeSpan($"{valueExpression}.Add({oneHourExpression})", value.Add(oneHour));
            data.Add($"$({valueExpression}.CompareTo({oneHourExpression}))", value.CompareTo(oneHour).ToResultString());
#if NET
            data.Add($"$({valueExpression}.Divide({oneHourExpression}))", value.Divide(oneHour).ToResultString());
            AddTimeSpan($"{valueExpression}.Divide($([System.Convert]::ToDouble('2')))", value.Divide(2));
#endif
            AddTimeSpan($"{valueExpression}.Duration()", value.Duration());
            data.Add($"$({valueExpression}.Equals({oneHourExpression}))", value.Equals(oneHour).ToResultString());
            data.Add($"$({valueExpression}.GetHashCode())", value.GetHashCode().ToResultString());
#if NET
            AddTimeSpan($"{valueExpression}.Multiply($([System.Convert]::ToDouble('2')))", value.Multiply(2));
#endif
            AddTimeSpan($"{valueExpression}.Negate()", value.Negate());
            AddTimeSpan($"{valueExpression}.Subtract({oneHourExpression})", value.Subtract(oneHour));
            data.Add($"$({valueExpression}.ToString())", value.ToString());
            data.Add($"$({valueExpression}.ToString('c'))", value.ToString("c"));
            data.Add($"$({valueExpression}.ToString('c', {culture}))", value.ToString("c", CultureInfo.CurrentUICulture));

            return data;

            void AddTimeSpan(string invocation, TimeSpan expected)
                => data.Add($"$({invocation}.Ticks)", expected.Ticks.ToResultString());

            void AddProperty(string propertyName, object expected)
                => data.Add($"$({valueExpression}.{propertyName})", expected.ToResultString()!);

#if NET
            void AddStaticField(string fieldName, object expected)
                => data.Add($"$([System.TimeSpan]::{fieldName})", expected.ToResultString()!);
#endif
        }
    }

    [Theory]
    [MemberData(nameof(EnvironmentMemberExpressions))]
    public void PropertyFunctionEnvironmentMembers(string expression)
        => ExpandProperties(expression).ShouldNotBeNull();

    public static TheoryData<string> EnvironmentMemberExpressions => new()
    {
            "$([System.Environment]::ExpandEnvironmentVariables('%PATH%'))",
            "$([System.Environment]::GetEnvironmentVariable('MSBUILD_WELLKNOWNFUNCTIONS_MISSING'))",
            "$([System.Environment]::GetEnvironmentVariable('MSBUILD_WELLKNOWNFUNCTIONS_MISSING', 'EnvironmentVariableTarget.Process'))",
            "$([System.Environment]::GetEnvironmentVariables())",
            "$([System.Environment]::GetEnvironmentVariables('EnvironmentVariableTarget.Process'))",
            "$([System.Environment]::GetFolderPath('SpecialFolder.System'))",
            "$([System.Environment]::GetFolderPath('SpecialFolder.System', 'SpecialFolderOption.None'))",
            "$([System.Environment]::GetLogicalDrives())",
            "$([System.Environment]::CommandLine)",
            "$([System.Environment]::Is64BitOperatingSystem)",
            "$([System.Environment]::Is64BitProcess)",
            "$([System.Environment]::MachineName)",
            "$([System.Environment]::NewLine)",
            "$([System.Environment]::OSVersion)",
            "$([System.Environment]::ProcessorCount)",
            "$([System.Environment]::StackTrace)",
            "$([System.Environment]::SystemDirectory)",
            "$([System.Environment]::SystemPageSize)",
            "$([System.Environment]::TickCount)",
            "$([System.Environment]::UserDomainName)",
            "$([System.Environment]::UserInteractive)",
            "$([System.Environment]::UserName)",
            "$([System.Environment]::Version)",
            "$([System.Environment]::WorkingSet)",
    };

    [Theory]
    [MemberData(nameof(RuntimeMemberExpressions))]
    public void PropertyFunctionRuntimeMembers(string expression)
        => ExpandProperties(expression).ShouldNotBeNull();

    public static TheoryData<string> RuntimeMemberExpressions
    {
        get
        {
            string platform = Helpers.GetOSPlatformAsString();

            TheoryData<string> data = new()
            {
                "$([System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription)",
                "$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)",
                "$([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)",
                "$([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)",
                $"$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::{platform})))",
                "$([System.Runtime.InteropServices.OSPlatform]::Create('custom').ToString())",
                "$([System.Runtime.InteropServices.OSPlatform]::Linux)",
                "$([System.Runtime.InteropServices.OSPlatform]::OSX)",
                "$([System.Runtime.InteropServices.OSPlatform]::Windows)",
                "$([System.Runtime.InteropServices.OSPlatform]::op_Equality($([System.Runtime.InteropServices.OSPlatform]::Windows), $([System.Runtime.InteropServices.OSPlatform]::Windows)))",
                "$([System.Runtime.InteropServices.OSPlatform]::op_Inequality($([System.Runtime.InteropServices.OSPlatform]::Windows), $([System.Runtime.InteropServices.OSPlatform]::Linux)))",
                "$([System.Runtime.InteropServices.OSPlatform]::Windows.Equals($([System.Runtime.InteropServices.OSPlatform]::Windows)))",
                "$([System.Runtime.InteropServices.OSPlatform]::Windows.GetHashCode())",
            };

#if NET
            data.Add("$([System.Runtime.InteropServices.OSPlatform]::FreeBSD)");
            data.Add("$([System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier)");
            data.Add("$([System.OperatingSystem]::IsAndroid())");
            data.Add("$([System.OperatingSystem]::IsAndroidVersionAtLeast(0, 0, 0, 0))");
            data.Add("$([System.OperatingSystem]::IsBrowser())");
            data.Add("$([System.OperatingSystem]::IsIOS())");
            data.Add("$([System.OperatingSystem]::IsIOSVersionAtLeast(0, 0, 0))");
            data.Add("$([System.OperatingSystem]::IsMacCatalyst())");
            data.Add("$([System.OperatingSystem]::IsMacCatalystVersionAtLeast(0, 0, 0))");
            data.Add("$([System.OperatingSystem]::IsTvOS())");
            data.Add("$([System.OperatingSystem]::IsTvOSVersionAtLeast(0, 0, 0))");
            data.Add("$([System.OperatingSystem]::IsWasi())");
            data.Add("$([System.OperatingSystem]::IsWatchOS())");
            data.Add("$([System.OperatingSystem]::IsWatchOSVersionAtLeast(0, 0, 0))");
#endif

            data.Add("$([System.OperatingSystem]::IsFreeBSD())");
            data.Add("$([System.OperatingSystem]::IsFreeBSDVersionAtLeast(0, 0, 0, 0))");
            data.Add("$([System.OperatingSystem]::IsLinux())");
            data.Add("$([System.OperatingSystem]::IsMacOS())");
            data.Add("$([System.OperatingSystem]::IsMacOSVersionAtLeast(0, 0, 0))");
            data.Add("$([System.OperatingSystem]::IsOSPlatform('windows'))");
            data.Add("$([System.OperatingSystem]::IsOSPlatformVersionAtLeast('windows', 0, 0, 0, 0))");
            data.Add("$([System.OperatingSystem]::IsWindows())");
            data.Add("$([System.OperatingSystem]::IsWindowsVersionAtLeast(0, 0, 0, 0))");

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PrimitiveMemberExpressions))]
    public void PropertyFunctionPrimitiveMembers(string expression)
        => ExpandProperties(expression).ShouldNotBeNull();

    public static TheoryData<string> PrimitiveMemberExpressions
    {
        get
        {
            TheoryData<string> data = new()
            {
                "$([System.Convert]::ToInt32('42').CompareTo($([System.Convert]::ToInt32('41'))))",
                "$([System.Convert]::ToInt32('42').Equals($([System.Convert]::ToInt32('42'))))",
                "$([System.Convert]::ToDouble('42').CompareTo($([System.Convert]::ToInt32('41'))))",
                "$([System.Convert]::ToDouble('42').Equals($([System.Convert]::ToInt32('42'))))",
                "$([System.Convert]::ToInt32('42').GetHashCode())",
                "$([System.Convert]::ToInt32('42').GetTypeCode())",
                "$([System.Convert]::ToInt32('42').ToString())",
                "$([System.Convert]::ToInt32('42').ToString('D4'))",
                "$([System.Convert]::ToInt32('42').ToString($([System.Globalization.CultureInfo]::CurrentUICulture)))",
                "$([System.Convert]::ToInt32('42').ToString('D4', $([System.Globalization.CultureInfo]::CurrentUICulture)))",
                "$([System.Convert]::ToBoolean('true').ToString())",
                "$([System.Convert]::ToChar('x').ToString())",
                "$([System.Byte]::MinValue)",
                "$([System.Byte]::MaxValue)",
                "$([System.SByte]::MinValue)",
                "$([System.SByte]::MaxValue)",
                "$([System.Int16]::MinValue)",
                "$([System.Int16]::MaxValue)",
                "$([System.UInt16]::MinValue)",
                "$([System.UInt16]::MaxValue)",
                "$([System.Int32]::MinValue)",
                "$([System.Int32]::MaxValue)",
                "$([System.UInt32]::MinValue)",
                "$([System.UInt32]::MaxValue)",
                "$([System.Int64]::MinValue)",
                "$([System.Int64]::MaxValue)",
                "$([System.UInt64]::MinValue)",
                "$([System.UInt64]::MaxValue)",
                "$([System.Single]::MinValue)",
                "$([System.Single]::MaxValue)",
                "$([System.Single]::Epsilon)",
                "$([System.Single]::NaN)",
                "$([System.Single]::NegativeInfinity)",
                "$([System.Single]::PositiveInfinity)",
                "$([System.Double]::MinValue)",
                "$([System.Double]::MaxValue)",
                "$([System.Double]::Epsilon)",
                "$([System.Double]::NaN)",
                "$([System.Double]::NegativeInfinity)",
                "$([System.Double]::PositiveInfinity)",
                "$([System.Decimal]::MinValue)",
                "$([System.Decimal]::MaxValue)",
                "$([System.Decimal]::MinusOne)",
                "$([System.Decimal]::One)",
                "$([System.Decimal]::Zero)",
                "$([System.Char]::MinValue.GetHashCode())",
                "$([System.Char]::MaxValue.GetHashCode())",
            };

#if NET
            data.Add("$([System.Single]::E)");
            data.Add("$([System.Single]::NegativeZero)");
            data.Add("$([System.Single]::Pi)");
            data.Add("$([System.Single]::Tau)");
            data.Add("$([System.Double]::E)");
            data.Add("$([System.Double]::NegativeZero)");
            data.Add("$([System.Double]::Pi)");
            data.Add("$([System.Double]::Tau)");
#endif

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PrimitiveParseExpressions))]
    public void PropertyFunctionPrimitiveParse(string expression)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("42");

    public static TheoryData<string> PrimitiveParseExpressions
    {
        get
        {
            TheoryData<string> data = new();
            const string culture = "$([System.Globalization.CultureInfo]::CurrentUICulture)";
            const string styles = "System.Globalization.NumberStyles.Integer";
            string[] typeNames = ["Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64", "Single", "Double", "Decimal"];

            foreach (string typeName in typeNames)
            {
                data.Add($"$([System.{typeName}]::Parse('42'))");
                data.Add($"$([System.{typeName}]::Parse('42', {culture}))");
                data.Add($"$([System.{typeName}]::Parse('42', '{styles}'))");
            }

            return data;
        }
    }

#if NET
    [Theory]
    [MemberData(nameof(IntegerMemberExpressions))]
    public void PropertyFunctionIntegerMembers(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> IntegerMemberExpressions
    {
        get
        {
            TheoryData<string, string> data = new();
            (string TypeName, int Bits, bool Signed)[] types =
            [
                ("Byte", 8, false),
                ("SByte", 8, true),
                ("Int16", 16, true),
                ("UInt16", 16, false),
                ("Int32", 32, true),
                ("UInt32", 32, false),
                ("Int64", 64, true),
                ("UInt64", 64, false),
            ];

            foreach ((string typeName, int bits, bool signed) in types)
            {
                data.Add($"$([System.{typeName}]::Clamp('4', '1', '3'))", "3");
                data.Add($"$([System.{typeName}]::DivRem('5', '2'))", "(2, 1)");
                data.Add($"$([System.{typeName}]::IsEvenInteger('4'))", "True");
                data.Add($"$([System.{typeName}]::IsOddInteger('3'))", "True");
                data.Add($"$([System.{typeName}]::IsPow2('4'))", "True");
                data.Add($"$([System.{typeName}]::LeadingZeroCount('1'))", (bits - 1).ToResultString());
                data.Add($"$([System.{typeName}]::Log10('100'))", "2");
                data.Add($"$([System.{typeName}]::Log2('8'))", "3");
                data.Add($"$([System.{typeName}]::Max('4', '2'))", "4");
                data.Add($"$([System.{typeName}]::Min('4', '2'))", "2");
                data.Add($"$([System.{typeName}]::PopCount('7'))", "3");
                data.Add($"$([System.{typeName}]::RotateLeft('1', '1'))", "2");
                data.Add($"$([System.{typeName}]::RotateRight('2', '1'))", "1");
                data.Add($"$([System.{typeName}]::Sign('4'))", "1");
                data.Add($"$([System.{typeName}]::TrailingZeroCount('8'))", "3");

                if (signed)
                {
                    data.Add($"$([System.{typeName}]::Abs('-4'))", "4");
                    data.Add($"$([System.{typeName}]::CopySign('4', '-2'))", "-4");
                    data.Add($"$([System.{typeName}]::IsNegative('-4'))", "True");
                    data.Add($"$([System.{typeName}]::IsPositive('4'))", "True");
                    data.Add($"$([System.{typeName}]::MaxMagnitude('-4', '2'))", "-4");
                    data.Add($"$([System.{typeName}]::MinMagnitude('-4', '2'))", "2");
                }
            }

            data.Add("$([System.Int32]::BigMul('4', '2'))", "8");
            data.Add("$([System.UInt32]::BigMul('4', '2'))", "8");
            data.Add("$([System.Int64]::BigMul('4', '2'))", "8");
            data.Add("$([System.UInt64]::BigMul('4', '2'))", "8");

            return data;
        }
    }
#endif

#if NET
    [Theory]
    [MemberData(nameof(FloatingPointMemberExpressions))]
    [UseInvariantCulture]
    public void PropertyFunctionFloatingPointMembers(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> FloatingPointMemberExpressions
    {
        get
        {
            TheoryData<string, string> data = new();

            AddCases<float>(data, "Single");
            AddCases<double>(data, "Double");

            return data;

            static void AddCases<T>(TheoryData<string, string> data, string typeName)
                where T : unmanaged, System.Numerics.IFloatingPointIeee754<T>, System.Numerics.IMinMaxValue<T>
            {
                T negativeOne = T.CreateChecked(-1);
                T zero = T.Zero;
                T quarter = T.CreateChecked(0.25);
                T half = T.CreateChecked(0.5);
                T one = T.One;
                T two = T.CreateChecked(2);
                T three = T.CreateChecked(3);
                T four = T.CreateChecked(4);
                T eight = T.CreateChecked(8);

                Add("Abs", "'-1'", T.Abs(negativeOne));
                Add("Acos", "'0.5'", T.Acos(half));
                Add("Acosh", "'2'", T.Acosh(two));
                Add("AcosPi", "'0.5'", T.AcosPi(half));
                Add("Asin", "'0.5'", T.Asin(half));
                Add("Asinh", "'0.5'", T.Asinh(half));
                Add("AsinPi", "'0.5'", T.AsinPi(half));
                Add("Atan", "'1'", T.Atan(one));
                Add("Atan2", "'1', '2'", T.Atan2(one, two));
                Add("Atan2Pi", "'1', '2'", T.Atan2Pi(one, two));
                Add("Atanh", "'0.5'", T.Atanh(half));
                Add("AtanPi", "'1'", T.AtanPi(one));
                Add("BitDecrement", "'1'", T.BitDecrement(one));
                Add("BitIncrement", "'1'", T.BitIncrement(one));
                Add("Cbrt", "'8'", T.Cbrt(eight));
                Add("Ceiling", "'0.5'", T.Ceiling(half));
                Add("Clamp", "'2', '0', '1'", T.Clamp(two, zero, one));
                Add("ClampNative", "'2', '0', '1'", T.ClampNative(two, zero, one));
                Add("CopySign", "'1', '-1'", T.CopySign(one, negativeOne));
                Add("Cos", "'1'", T.Cos(one));
                Add("Cosh", "'1'", T.Cosh(one));
                Add("CosPi", "'1'", T.CosPi(one));
                Add("DegreesToRadians", "'1'", T.DegreesToRadians(one));
                Add("Exp", "'1'", T.Exp(one));
                Add("Exp10", "'1'", T.Exp10(one));
                Add("Exp10M1", "'1'", T.Exp10M1(one));
                Add("Exp2", "'1'", T.Exp2(one));
                Add("Exp2M1", "'1'", T.Exp2M1(one));
                Add("ExpM1", "'1'", T.ExpM1(one));
                Add("Floor", "'0.5'", T.Floor(half));
                Add("FusedMultiplyAdd", "'2', '3', '4'", T.FusedMultiplyAdd(two, three, four));
                Add("Hypot", "'3', '4'", T.Hypot(three, four));
                Add("Ieee754Remainder", "'3', '2'", T.Ieee754Remainder(three, two));
                Add("ILogB", "'4'", T.ILogB(four));
                Add("IsEvenInteger", "'4'", T.IsEvenInteger(four));
                Add("IsFinite", "'1'", T.IsFinite(one));
                Add("IsInfinity", $"$([System.{typeName}]::PositiveInfinity)", T.IsInfinity(T.PositiveInfinity));
                Add("IsInteger", "'1'", T.IsInteger(one));
                Add("IsNaN", $"$([System.{typeName}]::NaN)", T.IsNaN(T.NaN));
                Add("IsNegative", "'-1'", T.IsNegative(negativeOne));
                Add("IsNegativeInfinity", $"$([System.{typeName}]::NegativeInfinity)", T.IsNegativeInfinity(T.NegativeInfinity));
                Add("IsNormal", "'1'", T.IsNormal(one));
                Add("IsOddInteger", "'3'", T.IsOddInteger(three));
                Add("IsPositive", "'1'", T.IsPositive(one));
                Add("IsPositiveInfinity", $"$([System.{typeName}]::PositiveInfinity)", T.IsPositiveInfinity(T.PositiveInfinity));
                Add("IsPow2", "'4'", true);
                Add("IsRealNumber", "'1'", T.IsRealNumber(one));
                Add("IsSubnormal", $"$([System.{typeName}]::Epsilon)", T.IsSubnormal(T.Epsilon));
                Add("Lerp", "'1', '3', '0.5'", T.Lerp(one, three, half));
                Add("Log", "'2'", T.Log(two));
                Add("Log", "'4', '2'", T.Log(four, two));
                Add("Log10", "'2'", T.Log10(two));
                Add("Log10P1", "'1'", T.Log10P1(one));
                Add("Log2", "'2'", T.Log2(two));
                Add("Log2P1", "'1'", T.Log2P1(one));
                Add("LogP1", "'1'", T.LogP1(one));
                Add("Max", "'1', '2'", T.Max(one, two));
                Add("MaxMagnitude", "'-1', '2'", T.MaxMagnitude(negativeOne, two));
                Add("MaxMagnitudeNumber", "'-1', '2'", T.MaxMagnitudeNumber(negativeOne, two));
                Add("MaxNative", "'1', '2'", T.MaxNative(one, two));
                Add("MaxNumber", "'1', '2'", T.MaxNumber(one, two));
                Add("Min", "'1', '2'", T.Min(one, two));
                Add("MinMagnitude", "'-1', '2'", T.MinMagnitude(negativeOne, two));
                Add("MinMagnitudeNumber", "'-1', '2'", T.MinMagnitudeNumber(negativeOne, two));
                Add("MinNative", "'1', '2'", T.MinNative(one, two));
                Add("MinNumber", "'1', '2'", T.MinNumber(one, two));
                Add("MultiplyAddEstimate", "'2', '3', '4'", T.MultiplyAddEstimate(two, three, four));
                Add("Pow", "'2', '3'", T.Pow(two, three));
                Add("RadiansToDegrees", "'1'", T.RadiansToDegrees(one));
                Add("ReciprocalEstimate", "'2'", T.ReciprocalEstimate(two));
                Add("ReciprocalSqrtEstimate", "'4'", T.ReciprocalSqrtEstimate(four));
                Add("RootN", "'4', '2'", T.RootN(four, 2));
                Add("Round", "'0.5'", T.Round(half));
                Add("Round", "'0.25', '1'", T.Round(quarter, 1));
                Add("Round", "'0.5', 'System.MidpointRounding.AwayFromZero'", T.Round(half, MidpointRounding.AwayFromZero));
                Add(
                    "Round",
                    "'0.25', '1', 'System.MidpointRounding.AwayFromZero'",
                    T.Round(quarter, 1, MidpointRounding.AwayFromZero));
                Add("ScaleB", "'1', '2'", T.ScaleB(one, 2));
                Add("Sign", "'-1'", T.Sign(negativeOne));
                Add("Sin", "'1'", T.Sin(one));
                AddTuple("SinCos", "'1'", T.SinCos(one));
                AddTuple("SinCosPi", "'1'", T.SinCosPi(one));
                Add("Sinh", "'1'", T.Sinh(one));
                Add("SinPi", "'0.5'", T.SinPi(half));
                Add("Sqrt", "'4'", T.Sqrt(four));
                Add("Tan", "'1'", T.Tan(one));
                Add("Tanh", "'1'", T.Tanh(one));
                Add("TanPi", "'0.25'", T.TanPi(quarter));
                Add("Truncate", "'0.5'", T.Truncate(half));

                void Add(string methodName, string arguments, object expected)
                    => data.Add($"$([System.{typeName}]::{methodName}({arguments}))", expected.ToResultString()!);

                void AddTuple(string methodName, string arguments, (T Sin, T Cos) expected)
                    => data.Add(
                        $"$([System.{typeName}]::{methodName}({arguments}))",
                        $"({expected.Sin.ToResultString()}, {expected.Cos.ToResultString()})");
            }
        }
    }

    [Theory]
    [MemberData(nameof(FloatingPointCoercionExpressions))]
    public void PropertyFunctionFloatingPointCoercion(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> FloatingPointCoercionExpressions
    {
        get
        {
            TheoryData<string, string> data = new();
            (string Expression, string Expected)[] values =
            [
                ("$([System.Convert]::ToByte('4'))", "4"),
                ("$([System.Convert]::ToSByte('4'))", "4"),
                ("$([System.Convert]::ToInt16('4'))", "4"),
                ("$([System.Convert]::ToUInt16('4'))", "4"),
                ("$([System.Convert]::ToInt32('4'))", "4"),
                ("$([System.Convert]::ToUInt32('4'))", "4"),
                ("$([System.Convert]::ToInt64('4'))", "4"),
                ("$([System.Convert]::ToUInt64('4'))", "4"),
                ("$([System.Convert]::ToSingle('4'))", "4"),
                ("$([System.Convert]::ToDouble('4'))", "4"),
                ("$([System.Convert]::ToDecimal('4'))", "4"),
                ("$([System.Convert]::ToBoolean('true'))", "1"),
            ];

            foreach (string typeName in new[] { "Single", "Double" })
            {
                foreach ((string expression, string expected) in values)
                {
                    data.Add($"$([System.{typeName}]::Max({expression}, '0'))", expected);
                }
            }

            return data;
        }
    }

    [Theory]
    [InlineData("Single")]
    [InlineData("Double")]
    public void PropertyFunctionFloatingPointDoesNotCoerceChar(string typeName)
        => Should.Throw<InvalidProjectFileException>(
            () => ExpandProperties(
                $"$([System.{typeName}]::Max($([System.Convert]::ToChar('A')), '0'))",
                allowReflection: ExpanderFactory.UseLegacyExpander));
#endif

    [Theory]
    [MemberData(nameof(DecimalMemberExpressions))]
    public void PropertyFunctionDecimalMembers(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> DecimalMemberExpressions
    {
        get
        {
            TheoryData<string, string> data = new();
            const string two = "$([System.Decimal]::Parse('2'))";
            const string three = "$([System.Decimal]::Parse('3'))";
            const string four = "$([System.Decimal]::Parse('4'))";
            const string fortyTwo = "$([System.Decimal]::Parse('42'))";

            data.Add("$([System.Decimal]::new($([System.Convert]::ToInt32('42'))))", "42");
            data.Add("$([System.Decimal]::new($([System.Convert]::ToUInt32('42'))))", "42");
            data.Add("$([System.Decimal]::new($([System.Convert]::ToInt64('42'))))", "42");
            data.Add("$([System.Decimal]::new($([System.Convert]::ToUInt64('42'))))", "42");
            data.Add("$([System.Decimal]::new($([System.Convert]::ToSingle('42'))))", "42");
            data.Add("$([System.Decimal]::new($([System.Convert]::ToDouble('42'))))", "42");
            data.Add("$([System.Decimal]::new('42'))", "42");
            data.Add($"$([System.Decimal]::new($([System.Decimal]::GetBits({fortyTwo}))))", "42");
            data.Add(
                "$([System.Decimal]::new($([System.Convert]::ToInt32('42')), $([System.Convert]::ToInt32('0')), "
                    + "$([System.Convert]::ToInt32('0')), $([System.Convert]::ToBoolean('false')), $([System.Convert]::ToByte('1'))))",
                "4.2");

            data.Add($"$([System.Decimal]::Ceiling($([System.Decimal]::Parse('1.2'))))", "2");
            data.Add($"$([System.Decimal]::Floor($([System.Decimal]::Parse('1.8'))))", "1");
            data.Add($"$([System.Decimal]::GetBits({fortyTwo}).GetValue('0'))", "42");
            data.Add($"$([System.Decimal]::Negate({fortyTwo}))", "-42");
            data.Add($"$([System.Decimal]::ToByte({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToDouble({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToInt16({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToInt32({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToInt64({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToOACurrency($([System.Decimal]::Parse('4.2'))))", "42000");
            data.Add($"$([System.Decimal]::ToSByte({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToSingle({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToUInt16({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToUInt32({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::ToUInt64({fortyTwo}))", "42");
            data.Add($"$([System.Decimal]::Truncate($([System.Decimal]::Parse('1.8'))))", "1");
            data.Add("$([System.Decimal]::FromOACurrency($([System.Convert]::ToInt64('42000'))))", "4.2");

            data.Add($"$([System.Decimal]::Add({four}, {two}))", "6");
            data.Add($"$([System.Decimal]::Compare({four}, {two}))", "1");
            data.Add($"$([System.Decimal]::Divide({four}, {two}))", "2");
            data.Add($"$([System.Decimal]::Equals({four}, {four}))", "True");
            data.Add($"$([System.Decimal]::Multiply({four}, {two}))", "8");
            data.Add($"$([System.Decimal]::Remainder({three}, {two}))", "1");
            data.Add($"$([System.Decimal]::Subtract({four}, {two}))", "2");
            data.Add("$([System.Decimal]::Add('4', '2'))", "6");
            data.Add($"$([System.Decimal]::Round($([System.Decimal]::Parse('1.6'))))", "2");
            data.Add($"$([System.Decimal]::Round($([System.Decimal]::Parse('1.26')), '1'))", "1.3");

            data.Add($"$([System.Decimal]::op_Addition({four}, {two}))", "6");
            data.Add($"$([System.Decimal]::op_Decrement({four}))", "3");
            data.Add($"$([System.Decimal]::op_Division({four}, {two}))", "2");
            data.Add($"$([System.Decimal]::op_Equality({four}, {four}))", "True");
            data.Add($"$([System.Decimal]::op_GreaterThan({four}, {two}))", "True");
            data.Add($"$([System.Decimal]::op_GreaterThanOrEqual({four}, {four}))", "True");
            data.Add("$([System.Decimal]::op_Implicit($([System.Convert]::ToInt32('42'))))", "42");
            data.Add("$([System.Decimal]::op_Implicit($([System.Convert]::ToChar('*'))))", "42");
            data.Add("$([System.Decimal]::op_Explicit($([System.Convert]::ToDouble('42'))))", "42");
            data.Add($"$([System.Decimal]::op_Increment({four}))", "5");
            data.Add($"$([System.Decimal]::op_Inequality({four}, {two}))", "True");
            data.Add($"$([System.Decimal]::op_LessThan({two}, {four}))", "True");
            data.Add($"$([System.Decimal]::op_LessThanOrEqual({four}, {four}))", "True");
            data.Add($"$([System.Decimal]::op_Modulus({three}, {two}))", "1");
            data.Add($"$([System.Decimal]::op_Multiply({four}, {two}))", "8");
            data.Add($"$([System.Decimal]::op_Subtraction({four}, {two}))", "2");
            data.Add($"$([System.Decimal]::op_UnaryNegation({four}))", "-4");
            data.Add($"$([System.Decimal]::op_UnaryPlus({four}))", "4");

#if NET
            const string one = "$([System.Decimal]::Parse('1'))";
            const string negativeFour = "$([System.Decimal]::Parse('-4'))";

            data.Add($"$([System.Decimal]::Abs({negativeFour}))", "4");
            data.Add($"$([System.Decimal]::Clamp({four}, {one}, {three}))", "3");
            data.Add($"$([System.Decimal]::CopySign({four}, {negativeFour}))", "-4");
            data.Add($"$([System.Decimal]::IsCanonical({four}))", "True");
            data.Add($"$([System.Decimal]::IsEvenInteger({four}))", "True");
            data.Add($"$([System.Decimal]::IsInteger({four}))", "True");
            data.Add($"$([System.Decimal]::IsNegative({negativeFour}))", "True");
            data.Add($"$([System.Decimal]::IsOddInteger({three}))", "True");
            data.Add($"$([System.Decimal]::IsPositive({four}))", "True");
            data.Add($"$([System.Decimal]::Max({four}, {two}))", "4");
            data.Add($"$([System.Decimal]::MaxMagnitude({negativeFour}, {two}))", "-4");
            data.Add($"$([System.Decimal]::Min({four}, {two}))", "2");
            data.Add($"$([System.Decimal]::MinMagnitude({negativeFour}, {two}))", "2");
            data.Add($"$([System.Decimal]::Sign({negativeFour}))", "-1");
            data.Add("$([System.Decimal]::Parse('42').Scale)", "0");
#endif

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(StringIndexOfExpressions))]
    public void StringIndexOfTests(string expression, int expected)
        => ExpandProperties(expression, Properties("AString", "x12x456789x11"))
            .ShouldBe(expected.ToResultString());

    public static TheoryData<string, int> StringIndexOfExpressions => new()
    {
        { "$(AString.IndexOf('x', 1))", 3 },
        { "$(AString.IndexOf('x45', 1))", 3 },
        { "$(AString.IndexOf('x', 1, 4))", 3 },

        // 9 is not a valid StringComparison enum value
        { "$(AString.IndexOf('x', 9))", 10 },
        { "$(AString.IndexOf('X', 'StringComparison.Ordinal'))", -1 },
        { "$(AString.IndexOf('X', 'StringComparison.OrdinalIgnoreCase'))", 0 },
        { "$(AString.IndexOf('X4', 'StringComparison.OrdinalIgnoreCase'))", 3 },
        { "$(AString.IndexOf('X4', 1, 'StringComparison.OrdinalIgnoreCase'))", 3 },
        { "$(AString.IndexOf('X', 1, 3, 'StringComparison.OrdinalIgnoreCase'))", 3 },
    };

    [Theory]
    [MemberData(nameof(StringEndsWithExpressions))]
    public void StringEndsWithTests(string expression, bool expected, string propertyValue)
        => ExpandProperties(expression, Properties("AString", propertyValue))
            .ShouldBe(expected.ToResultString());

    public static TheoryData<string, bool, string> StringEndsWithExpressions => new()
    {
        { "$(AString.EndsWith('World'))", true, "HelloWorld" },
        { "$(AString.EndsWith('world'))", false, "HelloWorld" },
        { "$(AString.EndsWith('WORLD', 'StringComparison.Ordinal'))", false, "HelloWorld" },
        { "$(AString.EndsWith('WORLD', 'StringComparison.OrdinalIgnoreCase'))", true, "HelloWorld" },
        { "$(AString.EndsWith('world', 'StringComparison.OrdinalIgnoreCase'))", true, "HelloWorld" },
        { "$(AString.EndsWith('Hello', 'StringComparison.Ordinal'))", false, "HelloWorld" },
        { "$(AString.EndsWith('', 'StringComparison.Ordinal'))", true, "HelloWorld" },
        { "$(AString.EndsWith('.TXT', 'StringComparison.OrdinalIgnoreCase'))", true, @"C:\Path\File.txt" },
        { "$(AString.EndsWith('.TXT', 'StringComparison.Ordinal'))", false, @"C:\Path\File.txt" },
    };

    [Theory]
    [MemberData(nameof(StringEqualsWithStringComparisonExpressions))]
    public void StringEqualsWithStringComparisonTests(string expression, bool expected, string propertyValue)
        => ExpandProperties(expression, Properties("AString", propertyValue))
            .ShouldBe(expected.ToResultString());

    public static TheoryData<string, bool, string> StringEqualsWithStringComparisonExpressions => new()
    {
        { "$(AString.Equals($(AString.ToLower()), 'StringComparison.InvariantCulture'))", true, "linux" },
        { "$(AString.Equals($(AString.ToLower()), 'StringComparison.InvariantCulture'))", false, "Linux" },
        { "$(AString.Equals('hello', 'StringComparison.OrdinalIgnoreCase'))", true, "hello" },
        { "$(AString.Equals('HELLO', 'StringComparison.OrdinalIgnoreCase'))", true, "hello" },
        { "$(AString.Equals('HELLO', 'StringComparison.Ordinal'))", false, "hello" },
    };

    [Theory]
    [MemberData(nameof(StringMemberExpressions))]
    public void StringMemberTests(string expression, string expected)
        => ExpandProperties(
            expression,
            Properties(
                ("X", "abcabc"),
                ("Combining", "e\u0301"),
                ("Delimited", "a,,b"),
                ("Padded", " abc "),
                ("Trimmed", "xxabcxx"),
                ("Multiline", "a\r\nb")),
            allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> StringMemberExpressions
    {
        get
        {
            const string InvariantCulture = "$([System.Globalization.CultureInfo]::GetCultureInfo(''))";
            const string CharA = "$([System.Convert]::ToChar('a'))";
            const string CharB = "$([System.Convert]::ToChar('b'))";
#if NET
            const string CharC = "$([System.Convert]::ToChar('c'))";
#endif
            const string CharX = "$([System.Convert]::ToChar('x'))";
            const string CharArray = "$([System.String]::Copy('abc').ToCharArray())";

            TheoryData<string, string> data = new()
            {
                { $"$([System.String]::new({CharA}, '3'))", "aaa" },
                { "$([System.String]::Compare('a', 'b'))", "-1" },
                { "$([System.String]::Compare('a', 'A', 'true'))", "0" },
                { "$([System.String]::Compare('a', 'A', 'StringComparison.OrdinalIgnoreCase'))", "0" },
                { $"$([System.String]::Compare('a', 'A', 'true', {InvariantCulture}))", "0" },
                { $"$([System.String]::Compare('a', 'A', {InvariantCulture}, 'CompareOptions.IgnoreCase'))", "0" },
                { "$([System.String]::Compare('xa', '1', 'yb', '1', '1'))", "-1" },
                { "$([System.String]::Compare('xa', '1', 'yA', '1', '1', 'true'))", "0" },
                { "$([System.String]::Compare('xa', '1', 'yA', '1', '1', 'StringComparison.OrdinalIgnoreCase'))", "0" },
                { $"$([System.String]::Compare('xa', '1', 'yA', '1', '1', 'true', {InvariantCulture}))", "0" },
                {
                    $"$([System.String]::Compare('xa', '1', 'yA', '1', '1', {InvariantCulture}, 'CompareOptions.IgnoreCase'))",
                    "0"
                },
                { "$([System.String]::CompareOrdinal('a', 'b'))", "-1" },
                { "$([System.String]::CompareOrdinal('xa', '1', 'yb', '1', '1'))", "-1" },
                { "$([System.String]::Concat('a'))", "a" },
                { "$([System.String]::Concat('a', 'b'))", "ab" },
                { "$([System.String]::Concat('a', 'b', 'c'))", "abc" },
                { "$([System.String]::Concat('a', 'b', 'c', 'd'))", "abcd" },
                { "$([System.String]::Concat($([System.String]::Copy('a,b').Split(','))))", "ab" },
                { "$([System.String]::Copy('abc'))", "abc" },
                { "$([System.String]::Format('{0}', 'a'))", "a" },
                { "$([System.String]::Format('{0}{1}', 'a', 'b'))", "ab" },
                { "$([System.String]::Format('{0}{1}{2}', 'a', 'b', 'c'))", "abc" },
                { "$([System.String]::Format('{0}{1}{2}{3}', 'a', 'b', 'c', 'd'))", "abcd" },
                { $"$([System.String]::Format({InvariantCulture}, '{{0}}', 'a'))", "a" },
                { $"$([System.String]::Format({InvariantCulture}, '{{0}}{{1}}', 'a', 'b'))", "ab" },
                { $"$([System.String]::Format({InvariantCulture}, '{{0}}{{1}}{{2}}', 'a', 'b', 'c'))", "abc" },
                { "$([System.String]::Join(',', 'a', 'b'))", "a,b" },
                { "$([System.String]::Join(',', 'a', $([System.Convert]::ToInt32('1'))))", "a,1" },
                { "$([System.String]::Join('-', $([System.String]::Copy('a,b,c').Split(','))))", "a-b-c" },
                { "$([System.String]::Intern('abc'))", "abc" },
                { "$([System.String]::IsInterned('abc'))", "abc" },
                { "$([System.String]::op_Equality('abc', 'abc'))", bool.TrueString },
                { "$([System.String]::op_Inequality('abc', 'def'))", bool.TrueString },
                { "$(X.Clone())", "abcabc" },
                { "$(X.CompareTo('abd'))", "-1" },
                { $"$(X.CopyTo('1', {CharArray}, '0', '2'))", string.Empty },
                { $"$(X.EndsWith('BC', 'true', {InvariantCulture}))", bool.TrueString },
                { "$(X.IndexOf('b', '1'))", "1" },
                { "$(X.IndexOf('b', '0', '2'))", "1" },
                { "$(X.IndexOf('B', '0', 'StringComparison.OrdinalIgnoreCase'))", "1" },
                { "$(X.IndexOf('B', '0', '2', 'StringComparison.OrdinalIgnoreCase'))", "1" },
                { $"$(X.IndexOf({CharB}))", "1" },
                { $"$(X.IndexOf({CharB}, '2'))", "4" },
                { $"$(X.IndexOf({CharB}, '2', '3'))", "4" },
                { "$(X.IndexOfAny('bx'))", "1" },
                { "$(X.IndexOfAny('bx', '2'))", "4" },
                { "$(X.IndexOfAny('bx', '2', '2'))", "-1" },
                { "$(X.IsNormalized())", bool.TrueString },
                { "$(X.IsNormalized('NormalizationForm.FormC'))", bool.TrueString },
                { "$(X.LastIndexOf('b', '5', '3'))", "4" },
                { "$(X.LastIndexOf('B', '5', 'StringComparison.OrdinalIgnoreCase'))", "4" },
                { "$(X.LastIndexOf('B', '5', '3', 'StringComparison.OrdinalIgnoreCase'))", "4" },
                { $"$(X.LastIndexOf({CharB}))", "4" },
                { $"$(X.LastIndexOf({CharB}, '3'))", "1" },
                { $"$(X.LastIndexOf({CharB}, '5', '3'))", "4" },
                { "$(X.LastIndexOfAny('bx', '3'))", "1" },
                { "$(X.LastIndexOfAny('bx', '5', '3'))", "4" },
                { "$(Combining.Normalize())", "\u00E9" },
                { "$(Combining.Normalize('NormalizationForm.FormD'))", "e\u0301" },
                { "$(X.Remove('2'))", "ab" },
                { "$(X.Remove('1', '2'))", "aabc" },
                { $"$(X.Replace({CharB}, {CharX}))", "axcaxc" },
                { "$(Delimited.Split()[0])", "a,,b" },
                { "$(Delimited.Split(',', '2')[1])", ",b" },
                { "$(Delimited.Split(',', 'StringSplitOptions.RemoveEmptyEntries')[1])", "b" },
                { "$(Delimited.Split(',', '2', 'StringSplitOptions.RemoveEmptyEntries')[1])", "b" },
                { $"$(X.StartsWith('AB', 'true', {InvariantCulture}))", bool.TrueString },
                { $"$(X.ToLower({InvariantCulture}))", "abcabc" },
                { $"$(X.ToUpper({InvariantCulture}))", "ABCABC" },
                { $"$(X.ToString({InvariantCulture}))", "abcabc" },
                { "$(Padded.TrimStart())", "abc " },
                { "$(Padded.TrimEnd())", " abc" },
                { $"$(Trimmed.Trim({CharX}))", "abc" },
                { $"$(Trimmed.TrimStart({CharX}))", "abcxx" },
                { $"$(Trimmed.TrimEnd({CharX}))", "xxabc" },
            };

#if NET
            data.Add($"$(X.Contains({CharB}))", bool.TrueString);
            data.Add("$(X.Contains('B', 'StringComparison.OrdinalIgnoreCase'))", bool.TrueString);
            data.Add($"$(X.Contains($([System.Convert]::ToChar('B')), 'StringComparison.OrdinalIgnoreCase'))", bool.TrueString);
            data.Add($"$(X.EndsWith({CharC}))", bool.TrueString);
            data.Add($"$(X.IndexOf($([System.Convert]::ToChar('B')), 'StringComparison.OrdinalIgnoreCase'))", "1");
            data.Add(
                "$(X.GetHashCode('StringComparison.Ordinal'))",
                "abcabc".GetHashCode(StringComparison.Ordinal).ToString(CultureInfo.InvariantCulture));
            data.Add("$(X.Replace('B', 'x', 'StringComparison.OrdinalIgnoreCase'))", "axcaxc");
            data.Add($"$(X.Replace('B', 'x', 'true', {InvariantCulture}))", "axcaxc");
            data.Add("$(Delimited.Split(',', 'StringSplitOptions.RemoveEmptyEntries')[1])", "b");
            data.Add("$(Delimited.Split(',', '2', 'StringSplitOptions.RemoveEmptyEntries')[1])", "b");
            data.Add($"$(Delimited.Split({CharC}, 'StringSplitOptions.RemoveEmptyEntries')[0])", "a,,b");
            data.Add($"$(Delimited.Split({CharC}, '2', 'StringSplitOptions.RemoveEmptyEntries')[0])", "a,,b");
            data.Add($"$(X.StartsWith({CharA}))", bool.TrueString);
            data.Add("$(Multiline.ReplaceLineEndings('|'))", "a|b");
            data.Add("$(Multiline.ReplaceLineEndings())", $"a{Environment.NewLine}b");
#endif

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ArrayMemberExpressions))]
    public void ArrayMemberTests(string expression, string expected)
        => ExpandProperties(
            expression,
            Properties(
                ("ArraySource", "a,b"),
                ("ArrayDestination", "x,y")),
            allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> ArrayMemberExpressions => new()
    {
        { "$(ArraySource.Split(',').Clone().Length)", "2" },
        { "$(ArraySource.Split(',').Equals($(ArrayDestination.Split(','))))", bool.FalseString },
        { "$(ArraySource.Split(',').GetEnumerator())", Array.Empty<string>().GetEnumerator().ToString()! },
        { "$(ArraySource.Split(',').GetLength('0'))", "2" },
        { "$(ArraySource.Split(',').GetLongLength('0'))", "2" },
        { "$(ArraySource.Split(',').GetLowerBound('0'))", "0" },
        { "$(ArraySource.Split(',').GetUpperBound('0'))", "1" },
        { "$(ArraySource.Split(',').GetValue('1'))", "b" },
        { "$(ArraySource.Split(',').GetValue($([System.Convert]::ToInt64('1'))))", "b" },
        { "$(ArraySource.Split(',').Initialize())", string.Empty },
        { "$(ArraySource.Split(',').IsFixedSize)", bool.TrueString },
        { "$(ArraySource.Split(',').IsReadOnly)", bool.FalseString },
        { "$(ArraySource.Split(',').IsSynchronized)", bool.FalseString },
        { "$(ArraySource.Split(',').Length)", "2" },
        { "$(ArraySource.Split(',').LongLength)", "2" },
        { "$(ArraySource.Split(',').Rank)", "1" },
        { "$(ArraySource.Split(',').SetValue('c', '0'))", string.Empty },
        { "$(ArraySource.Split(',').SetValue('c', $([System.Convert]::ToInt64('0'))))", string.Empty },
        { "$(ArraySource.Split(',').SyncRoot.Length)", "2" },
        { "$(ArraySource.Split(',').ToString())", "System.String[]" },
    };

    [ModernExpanderOnlyTheory]
    [InlineData("'0'")]
    [InlineData("$([System.Convert]::ToInt64('0'))")]
    public void ArrayCopyTo(string index)
        => ExpandProperties(
            $"$(ArraySource.Split(',').CopyTo($(ArrayDestination.Split(',')), {index}))",
            Properties(
                ("ArraySource", "a,b"),
                ("ArrayDestination", "x,y")),
            allowReflection: false).ShouldBeEmpty();

    [Fact]
    public void ArrayGetHashCode()
        => ExpandProperties(
            "$(ArraySource.Split(',').GetHashCode())",
            Properties("ArraySource", "a,b"),
            allowReflection: ExpanderFactory.UseLegacyExpander).ShouldNotBeNullOrEmpty();

    [Theory]
    [MemberData(nameof(EnumMemberExpressions))]
    public void EnumMemberTests(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> EnumMemberExpressions
    {
        get
        {
            const string Monday = "[System.DateTime]::Parse('2024-01-01').DayOfWeek";
            const string MondayArgument = "$([System.DateTime]::Parse('2024-01-01').DayOfWeek)";
            const string TuesdayArgument = "$([System.DateTime]::Parse('2024-01-02').DayOfWeek)";
            const string InvariantCulture = "$([System.Globalization.CultureInfo]::GetCultureInfo(''))";

            return new TheoryData<string, string>
            {
                { $"$({Monday}.CompareTo({TuesdayArgument}))", "-1" },
                { $"$({Monday}.Equals({MondayArgument}))", bool.TrueString },
                { $"$({Monday}.GetHashCode())", "1" },
                { $"$({Monday}.GetTypeCode())", nameof(TypeCode.Int32) },
                { $"$({Monday}.HasFlag({MondayArgument}))", bool.TrueString },
                { $"$({Monday}.ToString())", nameof(DayOfWeek.Monday) },
                { $"$({Monday}.ToString('D'))", "1" },
                { $"$({Monday}.ToString({InvariantCulture}))", nameof(DayOfWeek.Monday) },
                { $"$({Monday}.ToString('D', {InvariantCulture}))", "1" },
            };
        }
    }

    [Theory]
    [MemberData(nameof(RemainingIntrinsicFunctionExpressions))]
    public void RemainingIntrinsicFunctionTests(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe(expected);

    public static TheoryData<string, string> RemainingIntrinsicFunctionExpressions
    {
        get
        {
            string basePath = Path.Combine(s_rootPathPrefix, "base") + Path.DirectorySeparatorChar;
            string childPath = Path.Combine(basePath, "child");

            return new TheoryData<string, string>
            {
                {
                    "$([MSBuild]::DoesTaskHostExist('CurrentRuntime', 'CurrentArchitecture'))",
                    IntrinsicFunctions.DoesTaskHostExist("CurrentRuntime", "CurrentArchitecture").ToString()
                },
                {
                    "$([MSBuild]::FilterTargetFrameworks('net8.0;net9.0', 'net9.0'))",
                    IntrinsicFunctions.FilterTargetFrameworks("net8.0;net9.0", "net9.0")
                },
                {
                    @"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuildUnitTests\Missing', 'Missing'))",
                    string.Empty
                },
                {
                    @"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuildUnitTests\Missing', 'Missing', 'fallback'))",
                    Convert.ToString(
                        IntrinsicFunctions.GetRegistryValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\MSBuildUnitTests\Missing",
                            "Missing",
                            "fallback"),
                        CultureInfo.InvariantCulture) ?? string.Empty
                },
                {
                    @"$([MSBuild]::GetRegistryValueFromView('HKEY_CURRENT_USER\Software\Microsoft\MSBuildUnitTests\Missing', 'Missing', 'fallback'))",
                    "fallback"
                },
                { "$([MSBuild]::IsOsBsdLike())", IntrinsicFunctions.IsOsBsdLike().ToString() },
                { "$([MSBuild]::IsOsUnixLike())", IntrinsicFunctions.IsOsUnixLike().ToString() },
                { $"$([MSBuild]::MakeRelative('{basePath}', '{childPath}'))", "child" },
                { "$([MSBuild]::NormalizeDirectory('a', 'b'))", IntrinsicFunctions.NormalizeDirectory("a", "b") },
            };
        }
    }

    /// <summary>
    ///  Expand property function that calls a method with an enum parameter.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodEnumArgument()
        => ExpandProperties("$([System.String]::Equals(`a`, `A`, StringComparison.OrdinalIgnoreCase))")
            .ShouldBe(true.ToString());

    /// <summary>
    ///  Expand property function that calls GetCultureInfo.
    /// </summary>
    [Fact]
    public void PropertyFunctionStaticMethodGetCultureInfo()
        => ExpandProperties("$([System.Globalization.CultureInfo]::GetCultureInfo(`en-US`).ToString())")
            .ShouldBe(new CultureInfo("en-US").ToString());

    [Theory]
    [MemberData(nameof(ValidPropertyFunctionExpressions))]
    public void PropertyFunctionSyntaxExpands(string expression, string expected)
        => ExpandProperties(expression, CreatePropertyFunctionSyntaxProperties())
            .ShouldBe(expected);

    public static TheoryData<string, string> ValidPropertyFunctionExpressions
    {
        get
        {
            TheoryData<string, string> data = new()
            {
                { "$(input.ToString()[1])", "X" },
                { "$(input[1])", "X" },
                { "$(listofthings.Split(';')[$(position)])","e" },
                { """$([System.Text.RegularExpressions.Regex]::Match($(Input), `EXPORT\s+(.+)`).Groups[1].Value)""","a" },
                { "$([MSBuild]::Add(1,2).CompareTo(3))", "0" },
                { "$([MSBuild]::Add(1,2).CompareTo(3.0))", "0" },
                { "$([MSBuild]::Add(1,2.0).CompareTo(3.0))", "0" },
                { "$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).CompareTo(3.0))", "0" },
                { "$([MSBuild]::Add(1,2).CompareTo('3'))", "0" },
                { "$([MSBuild]::Add(1,2).CompareTo(3.1))", "-1" },
                { "$([MSBuild]::Add(1,2.0).CompareTo(3.1))", "-1" },
                { "$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).CompareTo(3.1))", "-1" },
                { "$([MSBuild]::Add(1,2).CompareTo(2))", "1" },
                { "$([MSBuild]::Add(1,2).Equals(3))", true.ToResultString() },
                { "$([MSBuild]::Add(1,2).Equals(3.0))", true.ToResultString() },
                { "$([MSBuild]::Add(1,2.0).Equals(3.0))", true.ToResultString() },
                { "$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).Equals(3.0))", true.ToResultString() },
                { "$([MSBuild]::Add(1,2).Equals('3'))", true.ToResultString() },
                { "$([MSBuild]::Add(1,2).Equals(3.1))", false.ToResultString() },
                { "$([MSBuild]::Add(1,2.0).Equals(3.1))", false.ToResultString() },
                { "$([System.Convert]::ToDouble($([MSBuild]::Add(1,2))).Equals(3.1))", false.ToResultString() },
                { "$(a.Insert(0,'%28'))", "%28no" },
                { "$(a.Insert(0,'\"'))", "\"no" },
                { "$(a.Insert(0,'(('))", "%28%28no" },
                { "$(a.Insert(0,'))'))", "%29%29no" },
                { "A$(Reg:A)A", "AA" },
                { "A$(Reg:AA)", "A" },
                { "$(Reg:AA)", "" },
                { "$(Reg:AAAA)", "" },
                { "$(Reg:AAA)", "" },
                { "$([MSBuild]::Add(2,$([System.Convert]::ToInt64('28', 16))))", "42" },
                { "$([MSBuild]::Add(2,$([System.Convert]::ToInt64('28', $([System.Convert]::ToInt32(16))))))", "42" },
                { "$(e.Length.ToString())", "3" },
                { "$(e.get_Length().ToString())", "3" },
                { "$(emptystring.Length)", "0" },
                { "$(space.Length)", "1" },
                { "$([System.TimeSpan]::Equals(null, null))", true.ToResultString() }, // constant, unquoted null is a special value
                { "$([MSBuild]::Add(40,null))", "40" },
                { "$([MSBuild]::Add( 40 , null ))", "40" },
                { "$([MSBuild]::Add(null,40))", "40" },
                { "$([MSBuild]::Escape(';'))", "%3b" },
                { "$([MSBuild]::UnEscape('%3b'))", ";" },
                { "$(e.Substring($(e.Length)))", "" },
                { "$([System.Int32]::MaxValue)", int.MaxValue.ToResultString() },
                { "x$()", "x" },

                // Following two are comparison between non-numeric and numeric properties. More details: #10583
                { "$(a.Equals($(c)))", false.ToResultString() },
                { "$(a.CompareTo($(c)))", "1" },
            };

            if (!NativeMethodsShared.IsWindows)
            {
                data.Add("$(Registry:X)", "");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidPropertyFunctionExpressions))]
    public void InvalidPropertyFunctionSyntaxThrows(string expression)
        => Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(expression, CreatePropertyFunctionSyntaxProperties()));

    public static TheoryData<string> InvalidPropertyFunctionExpressions
    {
        get
        {
            TheoryData<string> data =
            [
                "$(input[)",
                "$(input.ToString()])",
                "$(input.ToString()[)",
                "$(input.ToString()[12])",
                "$(input[])",
                "$(input[-1])",
                "$(listofthings.Split(';')[)",
                "$(listofthings.Split(';')['goo'])",
                "$(listofthings.Split(';')[])",
                "$(listofthings.Split(';')[-1])",
                "$([]::())",
                "$([Microsoft.VisualBasic.FileIO.FileSystem]::CurrentDirectory)", // not allowed
                "$(e.Length..ToString())",
                "$(SomeStuff.get_Length(null))",
                "$(SomeStuff.Substring((1)))",
                "$(b.Substring(-10, $(c)))",
                "$(b.Substring(-10, $(emptystring)))",
                "$(b.Substring(-10, $(space)))",
                "$([MSBuild]::Add.Sub(null,40))",
                "$([MSBuild]::Add( ,40))", // empty parameter is empty string
                "$([MSBuild]::Add('',40))", // empty quoted parameter is empty string
                "$([MSBuild]::Add(40,,,))",
                "$([MSBuild]::Add(40, ,,))",
                "$([MSBuild]::Add(,))", // gives "Late bound operations cannot be performed on types or methods for which ContainsGenericParameters is true."
                "$([System.TimeSpan]::Equals(,))", // empty parameter is interpreted as empty string
                "$([System.TimeSpan]::Equals($(space),$(emptystring)))", // empty parameter is interpreted as empty string
                "$([System.TimeSpan]::Equals($(emptystring),$(emptystring)))", // empty parameter is interpreted as empty string
                "$([MSBuild]::Add($(PropertyContainingNullAsAString),40))", // a property containing the word null is a string "null"
                "$([MSBuild]::Add('null',40))", // the word null is a string "null"
                "$(SomeStuff.Substring(-10))",
                "$(.Length)",
                "$(.Substring(1))",
                "$(.get_Length())",
                "$(e.)",
                "$(e..)",
                "$(e..Length)",
                "$(e$(d).Length)",
                "$($(d).Length)",
                "$([System.IO.Path]Combine::Combine(`a`,`b`))",
                "$([System.IO.Path]::`Combine(`a`, `b`)`)",
                "$([System.IO.Path]::(`Combine(`a`, `b`)`))",
                "$([System.DateTime]foofoo::Now)",
                "$([System.DateTime].Now)",
                "$([].Now)",
                "$([ ].Now)",
                "$([ .Now)",
                "$([])",
                "$([ )",
                "$([ ])",
                "$([System.Diagnostics.Process]::Start(`NOTEPAD.EXE`))",
                "$([[]]::Start(`NOTEPAD.EXE`))",
                "$([Goop]::Start(`NOTEPAD.EXE`))",
                "$([System.Threading.Thread]::CurrentThread)",
                "$(SomeStuff.)",
                "$(SomeStuff.!)",
                "$(SomeStuff.GetType)",
                "$(SomeStuff.Substring(HELLO!))",
                "$(SomeStuff.ToLowerInvariant()_goop)",
                "$(SomeStuff($(System.DateTime.Now)))",
                "$(System.Foo.Bar.Lgg)",
                "$(SomeStuff.Lgg)",
                "$(SomeStuff($(Value)))",
                "$(e.$(e.Length))",
                "$(e.Substring($([System.IO.Path]::Combine(`a`, `b`))))",
                "$([]::())",
                "$($())",
            ];

#if !RUNTIME_TYPE_NETCORE
            if (NativeMethodsShared.IsWindows)
            {
                // '|' is only an invalid path character on Windows under .NET Framework.
                data.Add("$([System.IO.Path]::Combine(`|`,`b`))");
            }
#endif

            if (NativeMethodsShared.IsWindows)
            {
                data.Add("$(Registry:X)");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(UnrecognizedPropertyFunctionExpressions))]
    public void UnrecognizedPropertyFunctionSyntaxRemainsUnexpanded(string expression)
        => ExpandProperties(expression, CreatePropertyFunctionSyntaxProperties())
            .ShouldBe(expression);

    public static TheoryData<string> UnrecognizedPropertyFunctionExpressions =>
    [
        """
        $(

        $(

        [System.IO]::Path.GetDirectory('c:\foo\bar\baz.txt')

        ).Substring(

        '$([System.IO]::Path.GetPathRoot(

        '$([System.IO]::Path.GetDirectory('c:\foo\bar\baz.txt'))'

        ).Length)'



        )
        """,
        "$([MSBuild]::Add(40,)",
        "$([MSBuild]::Add(40,X)",
        "$([MSBuild]::Add(40,",
        "$([MSBuild]::Add(40",
        "$(e`.Length)",
        "$([System.IO.Path]::Combine((`a`,`b`))",
        "$([System.IO.Path]Combine(::Combine(`a`,`b`))",
        "$([System.IO.Path]Combine(`::Combine(`a`,`b`)`, `b`)`)",
        "$([(::Start(`NOTEPAD.EXE`))",
        "$",
        "$(",
        "$((",
        "@",
        "@(",
        "@()",
        "%",
        "%(",
        "%()",
        "exists",
        "exists(",
        "exists()",
        "exists( )",
        "exists(,)",
        "@(x->'",
        "@(x->''",
        "@(x-",
        "@(x->'x','",
        "@(x->'x',''",
        "@(x->'x','')",
        "-1>x",
        "\n",
        "\t",
        "+-1",
        "$(SomeStuff.`)",
        "$(goop.baz`)",
        "$(e.Substring($(e.Substring(,)))",
        "$(e.Substring($(e.Substring(a)))",
        "$((((",
        "$",
        "()",
    ];

    private static PropertyDictionary<ProjectPropertyInstance> CreatePropertyFunctionSyntaxProperties()
        => Properties(
            ("File", @"foo\file.txt"),
            ("a", "no"),
            ("b", "true"),
            ("c", "1"),
            ("position", "4"),
            ("d", "xxx"),
            ("e", "xxx"),
            ("and", "and"),
            ("a_semi_b", "a;b"),
            ("a_apos_b", "a'b"),
            ("foo_apos_foo", "foo'foo"),
            ("a_escapedsemi_b", "a%3bb"),
            ("a_escapedapos_b", "a%27b"),
            ("has_trailing_slash", @"foo\"),
            ("emptystring", ""),
            ("space", " "),
            ("listofthings", "a;b;c;d;e;f;g;h;i;j;k;l"),
            ("input", "EXPORT a"),
            ("propertycontainingnullasastring", "null"));

    [Fact]
    public void PropertyFunctionWithNewLines()
    {
        const string Expression = """
            $(SomeProperty
             .Substring(0, 10)
              .ToString()
               .Substring(0, 5)
                 .ToString())
            """;

        ExpandProperties(Expression, Properties("SomeProperty", "6C8546D5297C424F962201B0E0E9F142"))
            .ShouldBe("6C854");
    }

    [Fact]
    public void PropertyFunctionStringIndexOfAny()
        => ExpandProperties("$(prop.IndexOfAny('y'))", Properties("prop", "x-y-z"))
            .ShouldBe("2");

    [Theory]
    [InlineData("$(prop.LastIndexOf('y'))", "8")]
    [InlineData("$(prop.LastIndexOf('y', 7))", "6")]
    public void PropertyFunctionStringLastIndexOf(string expression, string expected)
        => ExpandProperties(expression, Properties("prop", "x-x-y-y-y-z"))
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionStringLastIndexOfAny()
        => ExpandProperties("$(prop.LastIndexOfAny('xy'))", Properties("prop", "x-x-y-y-y-z"))
            .ShouldBe("8");

    [Fact]
    public void PropertyFunctionStringCopy()
    {
        const string Expression = """
            $([System.String]::Copy($(X)).LastIndexOf(
                '.designer.cs',
                System.StringComparison.OrdinalIgnoreCase))
            """;

        ExpandProperties(Expression, Properties("X", "test.designer.cs"))
            .ShouldBe("4");
    }

    [Fact]
    public void PropertyFunctionVersionParse()
        => ExpandProperties("$([System.Version]::Parse('$(X)').ToString(1))", Properties("X", "4.0"))
            .ShouldBe("4");

    [Fact]
    public void PropertyFunctionGuidNewGuid()
    {
        string? result = ExpandProperties("$([System.Guid]::NewGuid())");

        Guid.TryParse(result, out _).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(GuidAndVersionExpressions))]
    public void PropertyFunctionGuidAndVersionMembers(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    public static TheoryData<string, string> GuidAndVersionExpressions => new()
    {
        { """$([System.Guid]::Empty)""", Guid.Empty.ToString() },
        { """$([System.Guid]::new('00000000-0000-0000-0000-000000000000'))""", Guid.Empty.ToString() },
        {
            """$([System.Guid]::new('0', '0', '0', $([System.Convert]::FromBase64String('AAAAAAAAAAA='))))""",
            Guid.Empty.ToString()
        },
        { """$([System.Guid]::new('0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0'))""", Guid.Empty.ToString() },
        { """$([System.Guid]::Parse('00000000-0000-0000-0000-000000000000'))""", Guid.Empty.ToString() },
#if NET
        {
            """$([System.Guid]::Parse('00000000-0000-0000-0000-000000000000', $([System.Globalization.CultureInfo]::GetCultureInfo('en-US'))))""",
            Guid.Empty.ToString()
        },
#endif
        { """$([System.Guid]::ParseExact('00000000000000000000000000000000', 'N'))""", Guid.Empty.ToString() },
        {
            """$([System.Guid]::Parse('12345678-1234-5678-9012-345678901234').CompareTo($([System.Guid]::Parse('12345678-1234-5678-9012-345678901234'))))""",
            "0"
        },
        {
            """$([System.Guid]::Parse('12345678-1234-5678-9012-345678901234').Equals($([System.Guid]::Parse('12345678-1234-5678-9012-345678901234'))))""",
            bool.TrueString
        },
        { """$([System.Guid]::Parse('12345678-1234-5678-9012-345678901234').ToByteArray().Length)""", "16" },
        { """$([System.Guid]::Parse('12345678-1234-5678-9012-345678901234').ToString())""", "12345678-1234-5678-9012-345678901234" },
        { """$([System.Guid]::Parse('12345678-1234-5678-9012-345678901234').ToString('N'))""", "12345678123456789012345678901234" },
        {
            """$([System.Guid]::Parse('12345678-1234-5678-9012-345678901234').ToString('D', $([System.Globalization.CultureInfo]::GetCultureInfo('en-US'))))""",
            "12345678-1234-5678-9012-345678901234"
        },
        {
            """$([System.Version]::op_Equality($([System.Version]::new('1.2')), $([System.Version]::new('1.2'))))""",
            bool.TrueString
        },
        {
            """$([System.Version]::op_Inequality($([System.Version]::new('1.2')), $([System.Version]::new('2.0'))))""",
            bool.TrueString
        },
        {
            """$([System.Version]::op_LessThan($([System.Version]::new('1.2')), $([System.Version]::new('2.0'))))""",
            bool.TrueString
        },
        {
            """$([System.Version]::op_LessThanOrEqual($([System.Version]::new('1.2')), $([System.Version]::new('2.0'))))""",
            bool.TrueString
        },
        {
            """$([System.Version]::op_GreaterThan($([System.Version]::new('2.0')), $([System.Version]::new('1.2'))))""",
            bool.TrueString
        },
        {
            """$([System.Version]::op_GreaterThanOrEqual($([System.Version]::new('2.0')), $([System.Version]::new('1.2'))))""",
            bool.TrueString
        },
    };

    [ModernExpanderOnlyFact]
    public void ModernPropertyFunctionGuidByteArrayConstructor()
        => ExpandProperties("""$([System.Guid]::new($([System.Convert]::FromBase64String('AAAAAAAAAAAAAAAAAAAAAA=='))))""")
            .ShouldBe(Guid.Empty.ToString());

    [Theory]
    [MemberData(nameof(StringComparerExpressions))]
    public void PropertyFunctionStringComparerMembers(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    public static TheoryData<string, string> StringComparerExpressions => new()
    {
        { """$([System.StringComparer]::CurrentCulture.Compare('a', 'a'))""", "0" },
        { """$([System.StringComparer]::CurrentCultureIgnoreCase.Compare('a', 'A'))""", "0" },
        { """$([System.StringComparer]::InvariantCulture.Compare('a', 'a'))""", "0" },
        { """$([System.StringComparer]::InvariantCultureIgnoreCase.Compare('a', 'A'))""", "0" },
        { """$([System.StringComparer]::Ordinal.Compare('a', 'a'))""", "0" },
        { """$([System.StringComparer]::OrdinalIgnoreCase.Compare('a', 'A'))""", "0" },
        {
            """$([System.StringComparer]::Create($([System.Globalization.CultureInfo]::GetCultureInfo('en-US')), 'true').Equals('a', 'A'))""",
            bool.TrueString
        },
        { """$([System.StringComparer]::OrdinalIgnoreCase.Equals('a', 'A'))""", bool.TrueString },
        {
            """$([System.StringComparer]::OrdinalIgnoreCase.Equals($([System.StringComparer]::OrdinalIgnoreCase)))""",
            bool.TrueString
        },
        {
            """$([System.StringComparer]::OrdinalIgnoreCase.GetHashCode('a'))""",
            StringComparer.OrdinalIgnoreCase.GetHashCode("a").ToResultString()
        },
        {
            """$([System.StringComparer]::OrdinalIgnoreCase.GetHashCode())""",
            StringComparer.OrdinalIgnoreCase.GetHashCode().ToResultString()
        },
        { """$([System.StringComparer]::OrdinalIgnoreCase.ToString())""", StringComparer.OrdinalIgnoreCase.ToString()! },
    };

#if NET
    [ModernExpanderOnlyFact]
    public void ModernPropertyFunctionStringComparerFromComparison()
        => ExpandProperties("""$([System.StringComparer]::FromComparison('OrdinalIgnoreCase').Equals('a', 'A'))""")
            .ShouldBe(bool.TrueString);
#endif

    [Theory]
    [MemberData(nameof(PathExpressions))]
    public void PropertyFunctionPathMembers(string expression, string expected)
        => ExpandProperties(expression)
            .ShouldBe(expected);

    public static TheoryData<string, string> PathExpressions => new()
    {
        { """$([System.IO.Path]::AltDirectorySeparatorChar)""", Path.AltDirectorySeparatorChar.ToString() },
        { """$([System.IO.Path]::DirectorySeparatorChar)""", Path.DirectorySeparatorChar.ToString() },
        { """$([System.IO.Path]::PathSeparator)""", Path.PathSeparator.ToString() },
        { """$([System.IO.Path]::VolumeSeparatorChar)""", Path.VolumeSeparatorChar.ToString() },
        { """$([System.IO.Path]::ChangeExtension('file.txt', '.bin'))""", "file.bin" },
        { """$([System.IO.Path]::Combine('one'))""", "one" },
        { """$([System.IO.Path]::Combine('one', 'two', 'three'))""", Path.Combine("one", "two", "three") },
        { """$([System.IO.Path]::Combine('one', 'two', 'three', 'four'))""", Path.Combine("one", "two", "three", "four") },
        { """$([System.IO.Path]::Combine('one', 'two', 'three', 'four', 'five'))""", Path.Combine("one", "two", "three", "four", "five") },
        { """$([System.IO.Path]::GetDirectoryName('one/two.txt'))""", "one" },
        { """$([System.IO.Path]::GetExtension('one/two.txt'))""", ".txt" },
        { """$([System.IO.Path]::GetFileName('one/two.txt'))""", "two.txt" },
        { """$([System.IO.Path]::GetFileNameWithoutExtension('one/two.txt'))""", "two" },
        { """$([System.IO.Path]::GetInvalidFileNameChars().Length)""", Path.GetInvalidFileNameChars().Length.ToResultString() },
        { """$([System.IO.Path]::GetInvalidPathChars().Length)""", Path.GetInvalidPathChars().Length.ToResultString() },
        { """$([System.IO.Path]::GetPathRoot($([System.IO.Path]::GetFullPath('file.txt'))))""", Path.GetPathRoot(Path.GetFullPath("file.txt"))! },
        { """$([System.IO.Path]::GetRandomFileName().Length)""", "12" },
        { """$([System.IO.Path]::GetTempPath())""", Path.GetTempPath() },
        { """$([System.IO.Path]::HasExtension('file.txt'))""", bool.TrueString },
        { """$([System.IO.Path]::IsPathRooted('relative'))""", bool.FalseString },
#if NET
        { """$([System.IO.Path]::EndsInDirectorySeparator($([System.IO.Path]::GetTempPath())))""", bool.TrueString },
        {
            """$([System.IO.Path]::GetFullPath('file.txt', $([System.IO.Path]::GetTempPath())))""",
            Path.GetFullPath("file.txt", Path.GetTempPath())
        },
        {
            """$([System.IO.Path]::GetRelativePath($([System.IO.Path]::GetTempPath()), $([System.IO.Path]::Combine($([System.IO.Path]::GetTempPath()), 'file.txt'))))""",
            "file.txt"
        },
        { """$([System.IO.Path]::IsPathFullyQualified($([System.IO.Path]::GetTempPath())))""", bool.TrueString },
        { """$([System.IO.Path]::Join('one', 'two'))""", Path.Join("one", "two") },
        { """$([System.IO.Path]::Join('one', 'two', 'three'))""", Path.Join("one", "two", "three") },
        { """$([System.IO.Path]::Join('one', 'two', 'three', 'four'))""", Path.Join("one", "two", "three", "four") },
        { """$([System.IO.Path]::Join('one', 'two', 'three', 'four', 'five'))""", Path.Join("one", "two", "three", "four", "five") },
        {
            """$([System.IO.Path]::TrimEndingDirectorySeparator($([System.IO.Path]::GetTempPath())))""",
            Path.TrimEndingDirectorySeparator(Path.GetTempPath())
        },
#endif
    };

    [Fact]
    public void PropertyFunctionPathGetTempFileName()
    {
        string path = ExpandProperties("$([System.IO.Path]::GetTempFileName())")!;

        File.Exists(path).ShouldBeTrue();
        File.Delete(path);
    }

    [Fact]
    public void PropertyFunctionStringArrayIndexerGetter()
        => ExpandProperties("$(prop.Split('-')[0])", Properties("prop", "x-y-z"))
            .ShouldBe("x");

    [Theory]
    [InlineData("$(prop.Substring(2))", "cdef")]
    [InlineData("$(prop.Substring(2, 3))", "cde")]
    public void PropertyFunctionSubstring(string expression, string expected)
        => ExpandProperties(expression, Properties("prop", "abcdef"))
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionStringGetChars()
        => ExpandProperties("$(prop[0])", Properties("prop", "461"))
            .ShouldBe("4");

    [Fact]
    public void PropertyFunctionStringGetCharsError()
        => Should.Throw<InvalidProjectFileException>(() =>
        {
            ExpandProperties("$(prop[5])", Properties("prop", "461"))
                .ShouldBe("4");
        });

    [Theory]
    [InlineData("$(prop.PadLeft(2))", " x", "prop", "x")]
    [InlineData("$(prop.PadLeft(2, '0'))", "0x", "prop", "x")]
    [InlineData("$(prop.PadLeft($([MSBuild]::Multiply(1, 2)), '0'))", "0x", "prop", "x")]
    [InlineData("$(VersionSuffixBuildOfTheDay.PadLeft(3, $([System.Convert]::ToChar(`0`))))", "004", "VersionSuffixBuildOfTheDay", "4")]
    public void PropertyFunctionStringPadLeft(string expression, string expected, string propertyName, string propertyValue)
        => ExpandProperties(expression, Properties(propertyName, propertyValue))
            .ShouldBe(expected);

    [Theory]
    [InlineData("$(prop.PadRight(2))", "x ")]
    [InlineData("$(prop.PadRight(2, '0'))", "x0")]
    public void PropertyFunctionStringPadRight(string expression, string expected)
        => ExpandProperties(expression, Properties("prop", "x"))
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionStringTrimEndCharArray()
        => ExpandProperties("$(prop.TrimEnd('.0123456789'))", Properties("prop", "net461"))
            .ShouldBe("net");

    [Theory]
    [InlineData("$(X.TrimStart('vV'))", "40")]
    [InlineData("$(X.TrimStart(vV))", "40")]
    public void PropertyFunctionStringTrimStart(string expression, string expected)
        => ExpandProperties(expression, Properties("X", "v40"))
            .ShouldBe(expected);

    [Fact]
    public void PropertyFunctionStringTrimEnd1()
        => ExpandProperties("$(prop.TrimEnd('a'))", Properties("prop", "netaa"))
            .ShouldBe("net");

    // https://github.com/dotnet/msbuild/issues/2882
    [Fact]
    public void PropertyFunctionMathMaxOverflow()
        => ExpandProperties("$([System.Math]::Max($(X), 0))", Properties("X", "-2010"))
            .ShouldBe("0");

    [Fact]
    public void PropertyFunctionStringTrimEnd2()
        => Should.Throw<InvalidProjectFileException>(static () =>
            ExpandProperties("$(prop.TrimEnd('a', 'b'))", Properties("prop", "stringab")));

    [Fact]
    public void PropertyFunctionMathMin()
        => ExpandProperties("$([System.Math]::Min($(X), 20))", Properties("X", "30"))
            .ShouldBe("20");

    [Theory]
    [MemberData(nameof(MathExpressions))]
    public void PropertyFunctionMathMembers(string expression, string expected)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander)
            .ShouldBe(expected);

    public static TheoryData<string, string> MathExpressions => new()
    {
        { """$([System.Math]::E)""", Math.E.ToResultString() },
        { """$([System.Math]::PI)""", Math.PI.ToResultString() },
        { """$([System.Math]::Abs('-1'))""", "1" },
        { """$([System.Math]::Abs($([System.Convert]::ToDecimal('-1.5'))))""", "1.5" },
        { """$([System.Math]::Acos('1'))""", "0" },
        { """$([System.Math]::Asin('0'))""", "0" },
        { """$([System.Math]::Atan('0'))""", "0" },
        { """$([System.Math]::Atan2('0', '1'))""", "0" },
        { """$([System.Math]::BigMul('3', '4'))""", "12" },
        { """$([System.Math]::Ceiling($([System.Convert]::ToDecimal('1.2'))))""", "2" },
        { """$([System.Math]::Ceiling('1.2'))""", "2" },
        { """$([System.Math]::Cos('0'))""", "1" },
        { """$([System.Math]::Cosh('0'))""", "1" },
        { """$([System.Math]::Exp('0'))""", "1" },
        { """$([System.Math]::Floor($([System.Convert]::ToDecimal('1.8'))))""", "1" },
        { """$([System.Math]::Floor('1.8'))""", "1" },
        { """$([System.Math]::IEEERemainder('5', '2'))""", "1" },
        { """$([System.Math]::Log('1'))""", "0" },
        { """$([System.Math]::Log('8', '2'))""", "3" },
        { """$([System.Math]::Log10('100'))""", "2" },
        { """$([System.Math]::Max($([System.Convert]::ToDecimal('1')), $([System.Convert]::ToDecimal('2'))))""", "2" },
        { """$([System.Math]::Min($([System.Convert]::ToInt32('1')), $([System.Convert]::ToInt32('2'))))""", "1" },
        { """$([System.Math]::Pow('2', '3'))""", "8" },
        { """$([System.Math]::Round('1.5'))""", "2" },
        { """$([System.Math]::Round('1.25', '1'))""", "1.2" },
        { """$([System.Math]::Round($([System.Convert]::ToDecimal('1.5'))))""", "2" },
        { """$([System.Math]::Round($([System.Convert]::ToDecimal('1.25')), '1'))""", "1.2" },
        { """$([System.Math]::Sign('-1'))""", "-1" },
        { """$([System.Math]::Sin('0'))""", "0" },
        { """$([System.Math]::Sinh('0'))""", "0" },
        { """$([System.Math]::Sqrt('4'))""", "2" },
        { """$([System.Math]::Tan('0'))""", "0" },
        { """$([System.Math]::Tanh('0'))""", "0" },
        { """$([System.Math]::Truncate($([System.Convert]::ToDecimal('1.8'))))""", "1" },
        { """$([System.Math]::Truncate('1.8'))""", "1" },
#if NET
        { """$([System.Math]::Tau)""", Math.Tau.ToResultString() },
        { """$([System.Math]::Acosh('1'))""", "0" },
        { """$([System.Math]::Asinh('0'))""", "0" },
        { """$([System.Math]::Atanh('0'))""", "0" },
        { """$([System.Math]::BitDecrement('2'))""", Math.BitDecrement(2).ToResultString() },
        { """$([System.Math]::BitIncrement('2'))""", Math.BitIncrement(2).ToResultString() },
        { """$([System.Math]::Cbrt('8'))""", "2" },
        { """$([System.Math]::Clamp('5', '0', '3'))""", "3" },
        { """$([System.Math]::CopySign('2', '-1'))""", "-2" },
        { """$([System.Math]::FusedMultiplyAdd('2', '3', '4'))""", "10" },
        { """$([System.Math]::ILogB('8'))""", "3" },
        { """$([System.Math]::Log2('8'))""", "3" },
        { """$([System.Math]::MaxMagnitude('-3', '2'))""", "-3" },
        { """$([System.Math]::MinMagnitude('-3', '2'))""", "2" },
        { """$([System.Math]::ReciprocalEstimate('4'))""", Math.ReciprocalEstimate(4).ToResultString() },
        { """$([System.Math]::ReciprocalSqrtEstimate('4'))""", Math.ReciprocalSqrtEstimate(4).ToResultString() },
        { """$([System.Math]::ScaleB('2', '3'))""", "16" },
        { """$([System.Math]::SinCos('0'))""", "(0, 1)" },
#endif
    };

    [Theory]
    [InlineData(nameof(Convert.ToSByte))]
    [InlineData(nameof(Convert.ToInt16))]
    [InlineData(nameof(Convert.ToInt32))]
    [InlineData(nameof(Convert.ToInt64))]
    [InlineData(nameof(Convert.ToSingle))]
    [InlineData(nameof(Convert.ToDouble))]
    [InlineData(nameof(Convert.ToDecimal))]
    public void PropertyFunctionMathSignedNumericOverloads(string conversion)
    {
        string value = $"$([System.Convert]::{conversion}('-2'))";

        ExpandProperties($"$([System.Math]::Abs({value}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("2");
        ExpandProperties($"$([System.Math]::Sign({value}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("-1");
    }

    [Theory]
    [InlineData(nameof(Convert.ToByte))]
    [InlineData(nameof(Convert.ToUInt16))]
    [InlineData(nameof(Convert.ToUInt32))]
    [InlineData(nameof(Convert.ToUInt64))]
    public void PropertyFunctionMathUnsignedNumericWidening(string conversion)
    {
        string value = $"$([System.Convert]::{conversion}('2'))";

        ExpandProperties($"$([System.Math]::Abs({value}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("2");
        ExpandProperties($"$([System.Math]::Sign({value}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("1");
    }

    [Theory]
    [InlineData(nameof(Convert.ToSByte))]
    [InlineData(nameof(Convert.ToByte))]
    [InlineData(nameof(Convert.ToInt16))]
    [InlineData(nameof(Convert.ToUInt16))]
    [InlineData(nameof(Convert.ToInt32))]
    [InlineData(nameof(Convert.ToUInt32))]
    [InlineData(nameof(Convert.ToInt64))]
    [InlineData(nameof(Convert.ToUInt64))]
    [InlineData(nameof(Convert.ToSingle))]
    [InlineData(nameof(Convert.ToDouble))]
    [InlineData(nameof(Convert.ToDecimal))]
    public void PropertyFunctionMathMinMaxOverloads(string conversion)
    {
        string one = $"$([System.Convert]::{conversion}('1'))";
        string two = $"$([System.Convert]::{conversion}('2'))";

        ExpandProperties($"$([System.Math]::Min({one}, {two}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("1");
        ExpandProperties($"$([System.Math]::Max({one}, {two}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("2");
    }

    [Theory]
    [InlineData(nameof(Convert.ToInt16), nameof(Convert.ToInt32))]
    [InlineData(nameof(Convert.ToUInt32), nameof(Convert.ToInt64))]
    [InlineData(nameof(Convert.ToInt64), nameof(Convert.ToUInt64))]
    [InlineData(nameof(Convert.ToSingle), nameof(Convert.ToDouble))]
    public void PropertyFunctionMathMixedNumericWidening(string leftConversion, string rightConversion)
    {
        string one = $"$([System.Convert]::{leftConversion}('1'))";
        string two = $"$([System.Convert]::{rightConversion}('2'))";

        ExpandProperties($"$([System.Math]::Max({one}, {two}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("2");
    }

    [Fact]
    public void PropertyFunctionMathWidensBigMulArguments()
        => ExpandProperties(
            "$([System.Math]::BigMul($([System.Convert]::ToInt16('3')), $([System.Convert]::ToInt32('4'))))",
            allowReflection: ExpanderFactory.UseLegacyExpander)
            .ShouldBe("12");

    [Fact]
    public void PropertyFunctionMathWidensCharArguments()
    {
        const string a = "$([System.Convert]::ToChar('A'))";
        const string b = "$([System.Convert]::ToChar('B'))";

        ExpandProperties($"$([System.Math]::Max({a}, {b}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("66");
    }

#if NET
    [Theory]
    [InlineData(nameof(Convert.ToSByte))]
    [InlineData(nameof(Convert.ToByte))]
    [InlineData(nameof(Convert.ToInt16))]
    [InlineData(nameof(Convert.ToUInt16))]
    [InlineData(nameof(Convert.ToInt32))]
    [InlineData(nameof(Convert.ToUInt32))]
    [InlineData(nameof(Convert.ToInt64))]
    [InlineData(nameof(Convert.ToUInt64))]
    [InlineData(nameof(Convert.ToSingle))]
    [InlineData(nameof(Convert.ToDouble))]
    [InlineData(nameof(Convert.ToDecimal))]
    public void PropertyFunctionMathClampOverloads(string conversion)
    {
        string zero = $"$([System.Convert]::{conversion}('0'))";
        string two = $"$([System.Convert]::{conversion}('2'))";
        string three = $"$([System.Convert]::{conversion}('3'))";

        ExpandProperties($"$([System.Math]::Clamp({three}, {zero}, {two}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("2");
    }

    [Theory]
    [InlineData(nameof(Convert.ToSByte))]
    [InlineData(nameof(Convert.ToByte))]
    [InlineData(nameof(Convert.ToInt16))]
    [InlineData(nameof(Convert.ToUInt16))]
    [InlineData(nameof(Convert.ToInt32))]
    [InlineData(nameof(Convert.ToUInt32))]
    [InlineData(nameof(Convert.ToInt64))]
    [InlineData(nameof(Convert.ToUInt64))]
    public void PropertyFunctionMathDivRemOverloads(string conversion)
    {
        string seven = $"$([System.Convert]::{conversion}('7'))";
        string two = $"$([System.Convert]::{conversion}('2'))";

        ExpandProperties($"$([System.Math]::DivRem({seven}, {two}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("(3, 1)");
    }

    [Theory]
    [InlineData(nameof(Convert.ToInt32))]
    [InlineData(nameof(Convert.ToUInt32))]
    [InlineData(nameof(Convert.ToInt64))]
    [InlineData(nameof(Convert.ToUInt64))]
    public void PropertyFunctionMathBigMulOverloads(string conversion)
    {
        string three = $"$([System.Convert]::{conversion}('3'))";
        string four = $"$([System.Convert]::{conversion}('4'))";

        ExpandProperties($"$([System.Math]::BigMul({three}, {four}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("12");
    }

    [Fact]
    public void PropertyFunctionMathWidensClampAndDivRemArguments()
    {
        ExpandProperties(
            "$([System.Math]::Clamp($([System.Convert]::ToInt16('3')), $([System.Convert]::ToInt32('0')), $([System.Convert]::ToInt32('2'))))",
            allowReflection: ExpanderFactory.UseLegacyExpander)
            .ShouldBe("2");
        ExpandProperties(
            "$([System.Math]::DivRem($([System.Convert]::ToInt16('7')), $([System.Convert]::ToInt32('2'))))",
            allowReflection: ExpanderFactory.UseLegacyExpander)
            .ShouldBe("(3, 1)");

        const string a = "$([System.Convert]::ToChar('A'))";
        const string b = "$([System.Convert]::ToChar('B'))";
        const string c = "$([System.Convert]::ToChar('C'))";

        ExpandProperties($"$([System.Math]::Clamp({c}, {a}, {b}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("66");
        ExpandProperties($"$([System.Math]::DivRem({c}, {b}))", allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("(1, 1)");
    }
#endif

    /// <summary>
    ///  Verifies that arithmetic intrinsics preserve reflection behavior by treating <see langword="null"/> as
    ///  the numeric value-type default, zero.
    /// </summary>
    /// <param name="methodName">The arithmetic intrinsic to invoke.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="expected">The expected arithmetic result.</param>
    [Theory]
    [InlineData("Add", "null", "40", "40")]
    [InlineData("Add", "null", "1.5", "1.5")]
    [InlineData("Add", "40", "null", "40")]
    [InlineData("Add", "1.5", "null", "1.5")]
    [InlineData("Subtract", "null", "40", "-40")]
    [InlineData("Subtract", "null", "1.5", "-1.5")]
    [InlineData("Subtract", "40", "null", "40")]
    [InlineData("Subtract", "1.5", "null", "1.5")]
    [InlineData("Multiply", "null", "40", "0")]
    [InlineData("Multiply", "null", "1.5", "0")]
    [InlineData("Multiply", "40", "null", "0")]
    [InlineData("Multiply", "1.5", "null", "0")]
    [InlineData("Divide", "null", "40", "0")]
    [InlineData("Divide", "null", "1.5", "0")]
    [InlineData("Modulo", "null", "40", "0")]
    [InlineData("Modulo", "null", "1.5", "0")]
    public void PropertyFunctionMSBuildArithmeticTreatsNullAsZero(string methodName, string left, string right, string expected)
        => ExpandProperties($"$([MSBuild]::{methodName}({left}, {right}))")
            .ShouldBe(expected);

    /// <summary>
    ///  Verifies arithmetic coercion errors for zero divisors and nonnumeric typed values.
    /// </summary>
    /// <param name="expression">The complete property-function expression expected to fail.</param>
    [Theory]
    [InlineData("$([MSBuild]::Divide(40, null))")]
    [InlineData("$([MSBuild]::Modulo(40, null))")]
    [InlineData("$([MSBuild]::Divide(null, null))")]
    [InlineData("$([MSBuild]::Modulo(null, null))")]
    [InlineData("$([MSBuild]::Add($([System.DateTime]::Parse('2024-01-01')), 2))")]
    [InlineData("$([MSBuild]::Add(2, $([System.DateTime]::Parse('2024-01-01'))))")]
    public void PropertyFunctionMSBuildArithmeticPreservesCoercionErrors(string expression)
        => Should.Throw<InvalidProjectFileException>(() => ExpandProperties(expression));

    /// <summary>
    ///  Verifies that recognized arithmetic intrinsics with uncoercible arguments report the existing static
    ///  function diagnostic without falling back to reflection.
    /// </summary>
    /// <param name="methodName">The arithmetic intrinsic to invoke.</param>
    [Theory]
    [InlineData("Add")]
    [InlineData("Subtract")]
    [InlineData("Multiply")]
    [InlineData("Divide")]
    [InlineData("Modulo")]
    public void PropertyFunctionMSBuildArithmeticInvalidArgumentsUseStaticFunctionError(string methodName)
    {
        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => ExpandProperties($"$([MSBuild]::{methodName}('not a number', 2))"));

        exception.ErrorCode.ShouldBe("MSB4186");
    }

    /// <summary>
    ///  Enumerates every ordered pair of supported raw and typed arithmetic operand forms, including
    ///  <see langword="null"/>, numeric values, <see cref="bool"/>, <see cref="char"/>, and enums.
    /// </summary>
    /// <value>
    ///  The operand expressions, result, and runtime result type for each ordered pair.
    /// </value>
    public static TheoryData<string, string?> ArithmeticCoercionCases
    {
        get
        {
            var operands = new[]
            {
                (Left: "null", Right: "null", LeftDouble: 0D, RightDouble: 0D, WellKnownLong: false,
                    WellKnownDouble: false, BindsToLong: true, BindsToDouble: true,
                    CoercesToLong: true, CoercesToDouble: true),
                (Left: "1", Right: "2", LeftDouble: 1D, RightDouble: 2D, WellKnownLong: true,
                    WellKnownDouble: true, BindsToLong: false, BindsToDouble: false,
                    CoercesToLong: true, CoercesToDouble: true),
                (Left: "1.25", Right: "2.25", LeftDouble: 1.25D, RightDouble: 2.25D, WellKnownLong: false,
                    WellKnownDouble: true, BindsToLong: false, BindsToDouble: false,
                    CoercesToLong: false, CoercesToDouble: true),
                (Left: "'1-'", Right: "'2-'", LeftDouble: -1D, RightDouble: -2D, WellKnownLong: false,
                    WellKnownDouble: true, BindsToLong: false, BindsToDouble: false,
                    CoercesToLong: false, CoercesToDouble: false),
                (Left: "$([System.Convert]::ToSByte('1'))", Right: "$([System.Convert]::ToSByte('2'))",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToByte('1'))", Right: "$([System.Convert]::ToByte('2'))",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToInt16('1'))", Right: "$([System.Convert]::ToInt16('2'))",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToUInt16('1'))", Right: "$([System.Convert]::ToUInt16('2'))",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToInt32('1'))", Right: "$([System.Convert]::ToInt32('2'))",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: true, WellKnownDouble: true,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToUInt32('1'))", Right: "$([System.Convert]::ToUInt32('2'))",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToInt64('1'))", Right: "$([System.Convert]::ToInt64('2'))",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: true, WellKnownDouble: true,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToUInt64('1'))", Right: "$([System.Convert]::ToUInt64('2'))",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: false, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToSingle('1.25'))", Right: "$([System.Convert]::ToSingle('2.25'))",
                    LeftDouble: 1.25D, RightDouble: 2.25D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: false, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToDouble('1.25'))", Right: "$([System.Convert]::ToDouble('2.25'))",
                    LeftDouble: 1.25D, RightDouble: 2.25D, WellKnownLong: false, WellKnownDouble: true,
                    BindsToLong: false, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToDecimal('1.25'))", Right: "$([System.Convert]::ToDecimal('2.25'))",
                    LeftDouble: 1.25D, RightDouble: 2.25D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: false, BindsToDouble: false, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToBoolean('true'))", Right: "$([System.Convert]::ToBoolean('false'))",
                    LeftDouble: 1D, RightDouble: 0D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: false, BindsToDouble: false, CoercesToLong: true, CoercesToDouble: true),
                (Left: "$([System.Convert]::ToChar('A'))", Right: "$([System.Convert]::ToChar('B'))",
                    LeftDouble: 65D, RightDouble: 66D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: false),
                (Left: "$([System.DateTime]::Parse('2024-01-01').DayOfWeek)",
                    Right: "$([System.DateTime]::Parse('2024-01-02').DayOfWeek)",
                    LeftDouble: 1D, RightDouble: 2D, WellKnownLong: false, WellKnownDouble: false,
                    BindsToLong: true, BindsToDouble: true, CoercesToLong: true, CoercesToDouble: true),
            };

            TheoryData<string, string?> data = new();

            foreach (var left in operands)
            {
                foreach (var right in operands)
                {
                    bool usesLong = left.WellKnownLong && right.WellKnownLong;
                    bool usesDouble = !usesLong && left.WellKnownDouble && right.WellKnownDouble;

                    if (!usesLong && !usesDouble)
                    {
                        usesLong = left.BindsToLong && right.BindsToLong;
                        usesDouble = !usesLong && left.BindsToDouble && right.BindsToDouble;

                        if (!usesLong && !usesDouble)
                        {
                            usesLong = left.CoercesToLong && right.CoercesToLong;
                            usesDouble = !usesLong && left.CoercesToDouble && right.CoercesToDouble;
                        }
                    }

                    string invocation = $"[MSBuild]::Add({left.Left}, {right.Right})";

                    if (usesLong)
                    {
                        long result = Convert.ToInt64(left.LeftDouble) + Convert.ToInt64(right.RightDouble);
                        AddInt64Case(data, invocation, result);
                    }
                    else if (usesDouble)
                    {
                        AddDoubleCase(data, invocation, left.LeftDouble + right.RightDouble);
                    }
                    else
                    {
                        data.Add($"$({invocation})", null);
                    }
                }
            }

            AddInt64Case(data, "[MSBuild]::Subtract(null, null)", 0);
            AddInt64Case(data, "[MSBuild]::Multiply(null, null)", 0);

            AddInt64Case(data, "[MSBuild]::Add($([System.Convert]::ToSingle('1.5')), 0)", 2);
            AddInt64Case(data, "[MSBuild]::Add($([System.Convert]::ToSingle('2.5')), 0)", 2);
            AddInt64Case(data, "[MSBuild]::Add($([System.Convert]::ToSingle('-1.5')), 0)", -2);
            AddInt64Case(data, "[MSBuild]::Add($([System.Convert]::ToSingle('-2.5')), 0)", -2);
            AddInt64Case(data, "[MSBuild]::Add($([System.Convert]::ToDecimal('1.5')), 0)", 2);
            AddInt64Case(data, "[MSBuild]::Add($([System.Convert]::ToDecimal('2.5')), 0)", 2);
            AddInt64Case(data, "[MSBuild]::Add($([System.Convert]::ToDecimal('-1.5')), 0)", -2);
            AddInt64Case(data, "[MSBuild]::Add($([System.Convert]::ToDecimal('-2.5')), 0)", -2);

            AddDoubleCase(data, "[MSBuild]::Add($([System.Double]::NaN), 2)", double.NaN);
            AddDoubleCase(data, "[MSBuild]::Add(2, $([System.Double]::NaN))", double.NaN);
            AddDoubleCase(data, "[MSBuild]::Add($([System.Double]::PositiveInfinity), 2)", double.PositiveInfinity);
            AddDoubleCase(data, "[MSBuild]::Add(2, $([System.Double]::PositiveInfinity))", double.PositiveInfinity);
            AddDoubleCase(data, "[MSBuild]::Add($([System.Double]::NegativeInfinity), 2)", double.NegativeInfinity);
            AddDoubleCase(data, "[MSBuild]::Add(2, $([System.Double]::NegativeInfinity))", double.NegativeInfinity);

            return data;

            static void AddInt64Case(TheoryData<string, string?> data, string invocation, long value)
                => AddCase(data, invocation, value.ToString(CultureInfo.InvariantCulture), TypeCode.Int64);

            static void AddDoubleCase(TheoryData<string, string?> data, string invocation, double value)
                => AddCase(data, invocation, value.ToString(CultureInfo.InvariantCulture), TypeCode.Double);

            static void AddCase(TheoryData<string, string?> data, string invocation, string expected, TypeCode expectedTypeCode)
            {
                data.Add($"$({invocation})", expected);
                data.Add($"$({invocation}.GetTypeCode())", expectedTypeCode.ToString());
            }
        }
    }

    /// <summary>
    ///  Characterizes overload selection for every ordered pair of supported arithmetic operand forms. The
    ///  well-known path preserves the historical precedence of direct numeric conversions, primitive reflection
    ///  binding, and the final <see cref="Convert.ChangeType"/> fallback.
    /// </summary>
    /// <param name="expression">The complete property-function expression.</param>
    /// <param name="expected">
    ///  The expected expression result, or <see langword="null"/> when evaluation should fail.
    /// </param>
    [Theory]
    [MemberData(nameof(ArithmeticCoercionCases))]
    [UseInvariantCulture]
    public void PropertyFunctionMSBuildArithmeticCoercion(string expression, string? expected)
    {
        if (expected is null)
        {
            Should.Throw<InvalidProjectFileException>(() => ExpandProperties(expression));
        }
        else
        {
            ExpandProperties(expression).ShouldBe(expected);
        }
    }

    /// <summary>
    ///  Verifies that typed numeric values whose conversion to <see cref="long"/> overflows select the
    ///  <see cref="double"/> overload.
    /// </summary>
    /// <param name="conversionMethod">The <see cref="Convert"/> method that produces the typed operand.</param>
    /// <param name="value">The value passed to <paramref name="conversionMethod"/>.</param>
    [Theory]
    [InlineData("ToUInt64", "9223372036854775808")]
    [InlineData("ToSingle", "1E+20")]
    [InlineData("ToDouble", "1E+20")]
    [InlineData("ToDecimal", "9223372036854775808")]
    [UseInvariantCulture]
    public void PropertyFunctionMSBuildArithmeticTypedNumericArgumentsCanOverflowLongCoercion(string conversionMethod, string value)
    {
        string invocation = $"[MSBuild]::Add($([System.Convert]::{conversionMethod}('{value}')), 0)";

        ExpandProperties($"$({invocation}.GetTypeCode())")
            .ShouldBe(nameof(TypeCode.Double));
    }

    [Fact]
    public void PropertyFunctionConvertToString()
        => ExpandProperties("$([System.Convert]::ToString(`.`))")
            .ShouldBe(".");

    [Fact]
    public void PropertyFunctionConvertToInt32()
        => ExpandProperties("$([System.Convert]::ToInt32(42))")
            .ShouldBe("42");

    [Theory]
    [InlineData("$([System.Convert]::ToBoolean('true'))", "True")]
    [InlineData("$([System.Convert]::ToByte('42'))", "42")]
    [InlineData("$([System.Convert]::ToChar('x'))", "x")]
    [InlineData("$([System.Convert]::ToDateTime('2026-09-23').ToString('yyyy-MM-dd'))", "2026-09-23")]
    [InlineData("$([System.Convert]::ToDecimal('1.25'))", "1.25")]
    [InlineData("$([System.Convert]::ToDouble('1.25'))", "1.25")]
    [InlineData("$([System.Convert]::ToInt16('42'))", "42")]
    [InlineData("$([System.Convert]::ToInt32('FF', 16))", "255")]
    [InlineData("$([System.Convert]::ToInt64('42'))", "42")]
    [InlineData("$([System.Convert]::ToSByte('-42'))", "-42")]
    [InlineData("$([System.Convert]::ToSingle('1.25'))", "1.25")]
    [InlineData("$([System.Convert]::ToString(42))", "42")]
    [InlineData("$([System.Convert]::ToUInt16('42'))", "42")]
    [InlineData("$([System.Convert]::ToUInt32('42'))", "42")]
    [InlineData("$([System.Convert]::ToUInt64('42'))", "42")]
    [InlineData("$([System.Convert]::ChangeType('42', 'TypeCode.Int32'))", "42")]
    [InlineData("$([System.Convert]::GetTypeCode(42))", "String")]
    [UseInvariantCulture]
    public void PropertyFunctionConvert(string expression, string expected)
        => ExpandProperties(expression).ShouldBe(expected);

    [Fact]
    public void PropertyFunctionConvertBase64()
        => ExpandProperties("$([System.Convert]::ToBase64String($([System.Convert]::FromBase64String('AQID'))))")
            .ShouldBe("AQID");

#if NET
    [Theory]
    [InlineData("$([System.Convert]::ToHexString($([System.Convert]::FromHexString('ABCD'))))", "ABCD")]
    [InlineData("$([System.Convert]::ToHexStringLower($([System.Convert]::FromHexString('ABCD'))))", "abcd")]
    public void PropertyFunctionConvertHex(string expression, string expected)
        => ExpandProperties(expression).ShouldBe(expected);
#endif

    [Fact]
    public void PropertyFunctionToCharArray()
        => ExpandProperties("$([System.Convert]::ToString(`.`).ToCharArray())")
            .ShouldBe(".");

    [Fact]
    public void PropertyFunctionStringArrayGetValue()
        => ExpandProperties(
            "$(X.Split($([System.Convert]::ToString(`.`).ToCharArray())).GetValue($([System.Convert]::ToInt32(0))))",
            Properties("X", "ab.cd"))
            .ShouldBe("ab");

    /// <summary>
    ///  Test that Char.IsDigit fast-path works correctly.
    /// </summary>
    [Theory]
    [MemberData(nameof(CharIsDigitExpressions))]
    public void PropertyFunctionCharIsDigit(string expression, bool expected)
        => ExpandProperties(expression).ShouldBe(expected.ToResultString());

    public static TheoryData<string, bool> CharIsDigitExpressions => new()
    {
        // Test with digit characters - single char version
        { "$([System.Char]::IsDigit('0'))", true },
        { "$([System.Char]::IsDigit('5'))", true },
        { "$([System.Char]::IsDigit('9'))", true },

        // Test with non-digit characters - single char version
        { "$([System.Char]::IsDigit('a'))", false },
        { "$([System.Char]::IsDigit(' '))", false },
        { "$([System.Char]::IsDigit('/'))", false },
        { "$([System.Char]::IsDigit(':'))", false },

        // Test with string and index version
        { "$([System.Char]::IsDigit('abc123def', 3))", true },
        { "$([System.Char]::IsDigit('abc123def', 4))", true },
        { "$([System.Char]::IsDigit('abc123def', 5))", true },
        { "$([System.Char]::IsDigit('abc123def', 0))", false },
        { "$([System.Char]::IsDigit('abc123def', 2))", false },
        { "$([System.Char]::IsDigit('hello789', 5))", true },
    };

    [Fact]
    public void PropertyFunctionCharMembers()
    {
        bool allowReflection = ExpanderFactory.UseLegacyExpander;

        ExpandProperties("$([System.Char]::ConvertFromUtf32('65'))", allowReflection).ShouldBe("A");
        ExpandProperties("$([System.Char]::ConvertToUtf32('AB', '1'))", allowReflection).ShouldBe("66");
        ExpandProperties("$([System.Char]::GetNumericValue('8'))", allowReflection).ShouldBe("8");
        ExpandProperties("$([System.Char]::GetNumericValue('A8', '1'))", allowReflection).ShouldBe("8");
        ExpandProperties("$([System.Char]::GetUnicodeCategory('A'))", allowReflection).ShouldBe("UppercaseLetter");
        ExpandProperties("$([System.Char]::GetUnicodeCategory('AB', '1'))", allowReflection).ShouldBe("UppercaseLetter");
        ExpandProperties("$([System.Char]::IsDigit('1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsLetter('a'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsLetterOrDigit('1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsLower('a'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsNumber('8'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsPunctuation('!'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsSeparator(' '))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsSymbol('+'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsUpper('A'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsWhiteSpace(' '))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsLetter('Ab', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsLetterOrDigit('A1', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsLower('Aa', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsNumber('A8', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsPunctuation('A!', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsSeparator('A ', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsSymbol('A+', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsUpper('aA', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsWhiteSpace('A ', '1'))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::Parse('a'))", allowReflection).ShouldBe("a");
        ExpandProperties("$([System.Char]::ToLower('A'))", allowReflection).ShouldBe("a");
        ExpandProperties("$([System.Char]::ToLowerInvariant('A'))", allowReflection).ShouldBe("a");
        ExpandProperties("$([System.Char]::ToString('a'))", allowReflection).ShouldBe("a");
        ExpandProperties("$([System.Char]::ToUpper('a'))", allowReflection).ShouldBe("A");
        ExpandProperties("$([System.Char]::ToUpperInvariant('a'))", allowReflection).ShouldBe("A");
    }

    [Fact]
    public void PropertyFunctionCharSurrogateAndControlMembers()
    {
        bool allowReflection = ExpanderFactory.UseLegacyExpander;
        PropertyDictionary<ProjectPropertyInstance> properties = Properties(
            ("Control", "\n"),
            ("SurrogatePair", "\U0001F600"));
        const string high = "$([System.Convert]::ToChar($([System.Convert]::ToInt32('55357'))))";
        const string low = "$([System.Convert]::ToChar($([System.Convert]::ToInt32('56832'))))";

        ExpandProperties("$([System.Char]::IsControl($(Control)))", properties, allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsControl($(Control), '0'))", properties, allowReflection).ShouldBe("True");
        ExpandProperties($"$([System.Char]::IsHighSurrogate({high}))", allowReflection).ShouldBe("True");
        ExpandProperties($"$([System.Char]::IsLowSurrogate({low}))", allowReflection).ShouldBe("True");
        ExpandProperties($"$([System.Char]::IsSurrogate({high}))", allowReflection).ShouldBe("True");
        ExpandProperties($"$([System.Char]::IsSurrogatePair({high}, {low}))", allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsHighSurrogate($(SurrogatePair), '0'))", properties, allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsLowSurrogate($(SurrogatePair), '1'))", properties, allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsSurrogate($(SurrogatePair), '0'))", properties, allowReflection).ShouldBe("True");
        ExpandProperties("$([System.Char]::IsSurrogatePair($(SurrogatePair), '0'))", properties, allowReflection).ShouldBe("True");
    }

    [Fact]
    public void PropertyFunctionCharCasingWithCulture()
    {
        const string culture = "$([System.Globalization.CultureInfo]::new('tr-TR'))";
        bool allowReflection = ExpanderFactory.UseLegacyExpander;

        ExpandProperties($"$([System.Char]::ToLower('I', {culture}))", allowReflection).ShouldBe("\u0131");
        ExpandProperties($"$([System.Char]::ToUpper('i', {culture}))", allowReflection).ShouldBe("\u0130");
    }

#if NET
    [Theory]
    [InlineData("$([System.Char]::IsAscii('a'))")]
    [InlineData("$([System.Char]::IsAsciiDigit('1'))")]
    [InlineData("$([System.Char]::IsAsciiHexDigit('f'))")]
    [InlineData("$([System.Char]::IsAsciiHexDigitLower('f'))")]
    [InlineData("$([System.Char]::IsAsciiHexDigitUpper('F'))")]
    [InlineData("$([System.Char]::IsAsciiLetter('a'))")]
    [InlineData("$([System.Char]::IsAsciiLetterLower('a'))")]
    [InlineData("$([System.Char]::IsAsciiLetterOrDigit('1'))")]
    [InlineData("$([System.Char]::IsAsciiLetterUpper('A'))")]
    [InlineData("$([System.Char]::IsBetween('b', 'a', 'c'))")]
    public void PropertyFunctionCharNetMembers(string expression)
        => ExpandProperties(expression, allowReflection: ExpanderFactory.UseLegacyExpander).ShouldBe("True");
#endif

    /// <summary>
    ///  Regression test for https://github.com/dotnet/msbuild/issues/12923.
    /// </summary>
    [Fact]
    public void PropertyFunction_ReplaceDoesNotCallRegexReplace()
        => Should.Throw<InvalidProjectFileException>(static () =>
            ExpandProperties("""$([System.TimeSpan]::Replace('abc_123_ghi', '\d+', 'def'))""")
                .ShouldNotBe("abc_def_ghi"));

    [Theory]
    [InlineData("getType")]
    [InlineData("GetType")]
    [InlineData("gettype")]
    public void GetTypeMethod_ShouldNotBeAllowed(string methodName)
    {
        var currentThread = Thread.CurrentThread;
        var originalCulture = currentThread.CurrentCulture;
        var originalUICulture = currentThread.CurrentUICulture;
        var enCultureInfo = new CultureInfo("en");

        try
        {
            currentThread.CurrentCulture = enCultureInfo;
            currentThread.CurrentUICulture = enCultureInfo;

            using var env = TestEnvironment.Create(_output);
            var root = env.CreateFolder();

            string content = $"""
                <Project>
                    <PropertyGroup>
                        <foo>aa</foo>
                        <typeval>$(foo.{methodName}().FullName)</typeval>
                    </PropertyGroup>
                </Project>
                """;

            var projectFile = env.CreateFile(root, ".proj", content);
            var exception = Should.Throw<InvalidProjectFileException>(() =>
            {
                new ProjectInstance(projectFile.Path);
            });

            exception.BaseMessage.ShouldContain($"The function \"{methodName}\" on type \"System.String\" is not available for execution as an MSBuild property function.");
        }
        finally
        {
            currentThread.CurrentCulture = originalCulture;
            currentThread.CurrentUICulture = originalUICulture;
        }
    }

    [Theory]
    [InlineData("getType")]
    [InlineData("GetType")]
    [InlineData("gettype")]
    public void GetTypeMethod_ShouldBeAllowed_EnabledByEnvVariable(string methodName)
    {
        using var env = TestEnvironment.Create(_output);
        env.RemoveAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions);
        env.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

        FeatureSwitches.EnableAllPropertyFunctions.ShouldBeTrue();

        var root = env.CreateFolder();

        string content = $"""
            <Project>
                <PropertyGroup>
                    <foo>aa</foo>
                    <typeval>$(foo.{methodName}().FullName)</typeval>
                </PropertyGroup>
            </Project>
            """;

        var projectFile = env.CreateFile(root, ".proj", content);
        Should.NotThrow(() =>
        {
            new ProjectInstance(projectFile.Path);
        });
    }

    [Theory]
    [MemberData(nameof(FastPathValidationExpressions))]
    public void FastPathValidationTest(string expression)
        => ExpandProperties(expression, allowReflection: false);

    public static TheoryData<string> FastPathValidationExpressions => new()
    {
        "$([System.Version]::Parse('17.12.11.10').ToString(2))",
        "$([System.Text.RegularExpressions.Regex]::Replace('abc123def', 'abc', ''))",
        "$([System.String]::new('Hi').Equals('Hello'))",
        """$([System.IO.Path]::GetFileNameWithoutExtension('C:\folder\file.txt'))""",
        "$([System.String]::new('Hello World').Length.ToString('mm'))",
        "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::NormalizeDirectory('C:/folder1/./folder2/'))",
        "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::IsOSPlatform('Windows'))",
        "$([Microsoft.Build.Evaluation.IntrinsicFunctions]::RegisterBuildCheck('check.dll'))",
    };

    [ModernExpanderOnlyTheory]
    [MemberData(nameof(ReflectionFallbackExpressions))]
    public void ReflectionFallback_IsLogged(string expression, string expectedLog)
        => Should.Throw<ShouldAssertException>(() => ExpandProperties(expression, allowReflection: false))
            .Message.ShouldContain(expectedLog);

    public static TheoryData<string, string> ReflectionFallbackExpressions => new()
    {
        { "$([MSBuild]::__ReflectionTest())", "ReceiverType=Microsoft.Build.Evaluation.IntrinsicFunctions; ObjectInstanceType=; MethodName=__ReflectionTest(" },
    };

    [Fact]
    public void ExpandItem_ConvertToStringUsingInvariantCultureForNumberData()
    {
        var currentThread = Thread.CurrentThread;
        var originalCulture = currentThread.CurrentCulture;
        var originalUICulture = currentThread.CurrentUICulture;

        try
        {
            var svSECultureInfo = new CultureInfo("sv-SE");
            using var env = TestEnvironment.Create(_output);
            currentThread.CurrentCulture = svSECultureInfo;
            currentThread.CurrentUICulture = svSECultureInfo;
            var root = env.CreateFolder();

            const string Content = """
                <Project>
                    <PropertyGroup>
                        <_value>$([MSBuild]::Subtract(0, 1))</_value>
                        <_otherValue Condition="'$(_value)' &gt;= -1">test-value</_otherValue>
                    </PropertyGroup>
                    <Target Name="Build" />
                </Project>
                """;

            var projectFile = env.CreateFile(root, ".proj", Content);
            ProjectInstance projectInstance = new(projectFile.Path);
            projectInstance.GetPropertyValue("_value").ShouldBe("-1");
            projectInstance.GetPropertyValue("_otherValue").ShouldBe("test-value");
        }
        finally
        {
            currentThread.CurrentCulture = originalCulture;
            currentThread.CurrentUICulture = originalUICulture;
        }
    }
}
