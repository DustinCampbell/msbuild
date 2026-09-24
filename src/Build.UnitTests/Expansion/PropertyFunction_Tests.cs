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
    ///  Verifies failed property-function expansion returns an invocation containing the evaluated receiver and arguments.
    /// </summary>
    /// <param name="expression">The property-function expression to expand.</param>
    /// <param name="expected">The expected invocation.</param>
    [Theory]
    [InlineData("$([System.IO.Path]::Combine(null,''))", "[System.IO.Path]::Combine(null, '')")]
    [InlineData("$(SomeStuff.Substring(-10))", "\"This IS SOME STUff\".Substring(-10)")]
    [InlineData("$([MSBuild]::GetPathOfFileAbove('foo'))", "[MSBuild]::GetPathOfFileAbove(foo, '')")]
    public void FailedPropertyFunctionReturnsEvaluatedInvocation(string expression, string expected)
        => ExpandProperties(expression, Properties("SomeStuff", "This IS SOME STUff"), ExpanderOptions.LeavePropertiesUnexpandedOnError)
            .ShouldBe(expected);

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

        ExpandProperties("$([System.Type]::GetType(`System.Type`))")
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

        string? result = ExpandProperties("$([System.Diagnostics.Process]::GetCurrentProcess().Id)");

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
        { "$([System.Math]::Abs(-1))", "ReceiverType=System.Math; ObjectInstanceType=; MethodName=Abs(" },
        { "$([System.String]::new(' abc ').Trim())", "ReceiverType=System.String; ObjectInstanceType=System.String; MethodName=Trim(" },
        { "$([System.Version]::new('1.2'))", "ReceiverType=System.Version; ObjectInstanceType=; MethodName=new(" },
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
