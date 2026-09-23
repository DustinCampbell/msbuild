// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Path_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Path), output)
{
    [Fact]
    public void Path_Combine_NoArguments()
        => StaticMember(nameof(Path.Combine))
            .NotHandled();

    [Fact]
    public void Path_Combine_NullArgument_NotHandled()
        => StaticMember(nameof(Path.Combine))
            .NotHandled(["one", null]);

    [Fact]
    public void Path_Combine_OneArgument()
        => StaticMember(nameof(Path.Combine))
            .Invoke(["one"])
            .ShouldBe(Path.Combine("one"));

    [Fact]
    public void Path_Combine_TwoArguments()
        => StaticMember(nameof(Path.Combine))
            .Invoke(["one", "two"])
            .ShouldBe(Path.Combine("one", "two"));

    [Fact]
    public void Path_Combine_ThreeArguments()
        => StaticMember(nameof(Path.Combine))
            .Invoke(["one", "two", "three"])
            .ShouldBe(Path.Combine("one", "two", "three"));

    [Fact]
    public void Path_Combine_FourArguments()
        => StaticMember(nameof(Path.Combine))
            .Invoke(["one", "two", "three", "four"])
            .ShouldBe(Path.Combine("one", "two", "three", "four"));

    [Fact]
    public void Path_Combine_ParamsArray()
        => StaticMember(nameof(Path.Combine))
            .Invoke(["one", "two", "three", "four", "five"])
            .ShouldBe(Path.Combine(["one", "two", "three", "four", "five"]));

    [Fact]
    public void Path_DirectorySeparatorChar()
        => StaticMember(nameof(Path.DirectorySeparatorChar))
            .Invoke()
            .ShouldBe(Path.DirectorySeparatorChar);

    [Fact]
    public void Path_DirectorySeparatorChar_Arguments_NotHandled()
        => StaticMember(nameof(Path.DirectorySeparatorChar))
            .NotHandled(["argument"]);

    [Fact]
    public void Path_GetDirectoryName()
        => StaticMember(nameof(Path.GetDirectoryName))
            .Invoke([Path.Combine("one", "two")])
            .ShouldBe(Path.GetDirectoryName(Path.Combine("one", "two")));

    [Fact]
    public void Path_GetFileName()
        => StaticMember(nameof(Path.GetFileName))
            .Invoke([Path.Combine("one", "two.txt")])
            .ShouldBe("two.txt");

    [Fact]
    public void Path_GetFileNameWithoutExtension()
        => StaticMember(nameof(Path.GetFileNameWithoutExtension))
            .Invoke([Path.Combine("one", "two.txt")])
            .ShouldBe("two");

    [Fact]
    public void Path_GetFullPath()
    {
        string path = Path.Combine("one", "two.txt");

        StaticMember(nameof(Path.GetFullPath))
            .Invoke([path])
            .ShouldBe(CurrentDirectoryRootedPath(path));
    }

    [Fact]
    public void Path_GetFullPath_InvalidArgument_NotHandled()
        => StaticMember(nameof(Path.GetFullPath))
            .NotHandled([1]);

    [Fact]
    public void Path_GetTempPath()
        => StaticMember(nameof(Path.GetTempPath))
            .Invoke()
            .ShouldBe(Path.GetTempPath());

    [Fact]
    public void Path_GetTempPath_Arguments_NotHandled()
        => StaticMember(nameof(Path.GetTempPath))
            .NotHandled(["argument"]);

    [Theory]
    [InlineData("relative", false)]
    public void Path_IsPathRooted(string path, bool expected)
        => StaticMember(nameof(Path.IsPathRooted))
            .Invoke([path])
            .ShouldBe(expected);

    [Fact]
    public void Path_IsPathRooted_RootedPath()
    {
        string path = Path.GetPathRoot(Directory.GetCurrentDirectory())!;

        StaticMember(nameof(Path.IsPathRooted))
            .Invoke([path])
            .ShouldBe(true);
    }

    [Fact]
    public void Path_AltDirectorySeparatorChar_NotHandled()
        => StaticMember(nameof(Path.AltDirectorySeparatorChar))
            .NotHandled();

    [Fact]
    public void Path_ChangeExtension_NotHandled()
        => StaticMember(nameof(Path.ChangeExtension))
            .NotHandled(["file.txt", ".bin"]);

    [Fact]
    public void Path_GetExtension_NotHandled()
        => StaticMember(nameof(Path.GetExtension))
            .NotHandled(["file.txt"]);

    [Fact]
    public void Path_GetInvalidFileNameChars_NotHandled()
        => StaticMember(nameof(Path.GetInvalidFileNameChars))
            .NotHandled();

    [Theory]
    [InlineData(nameof(Path.GetDirectoryName))]
    [InlineData(nameof(Path.GetFileName))]
    [InlineData(nameof(Path.GetFileNameWithoutExtension))]
    [InlineData(nameof(Path.IsPathRooted))]
    public void Path_StringMember_NonString_NotHandled(string memberName)
        => StaticMember(memberName)
            .NotHandled([1]);

    private static string CurrentDirectoryRootedPath(string path)
        => !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
            ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, path))
            : Path.GetFullPath(path);
}
