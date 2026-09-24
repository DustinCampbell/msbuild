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
    public void Path_Fields()
    {
        StaticMember(nameof(Path.AltDirectorySeparatorChar)).Invoke().ShouldBe(Path.AltDirectorySeparatorChar);
        StaticMember(nameof(Path.DirectorySeparatorChar)).Invoke().ShouldBe(Path.DirectorySeparatorChar);
        StaticMember(nameof(Path.PathSeparator)).Invoke().ShouldBe(Path.PathSeparator);
        StaticMember(nameof(Path.VolumeSeparatorChar)).Invoke().ShouldBe(Path.VolumeSeparatorChar);
    }

    [Fact]
    public void Path_RemainingMethods()
    {
        StaticMember(nameof(Path.ChangeExtension)).Invoke(["file.txt", ".bin"]).ShouldBe(Path.ChangeExtension("file.txt", ".bin"));
        StaticMember(nameof(Path.GetExtension)).Invoke(["file.txt"]).ShouldBe(".txt");
        StaticMember(nameof(Path.GetInvalidFileNameChars)).Invoke().ShouldBeOfType<char[]>().ShouldBe(Path.GetInvalidFileNameChars());
        StaticMember(nameof(Path.GetInvalidPathChars)).Invoke().ShouldBeOfType<char[]>().ShouldBe(Path.GetInvalidPathChars());
        StaticMember(nameof(Path.GetPathRoot)).Invoke([Path.GetFullPath("file.txt")]).ShouldBe(Path.GetPathRoot(Path.GetFullPath("file.txt")));
        StaticMember(nameof(Path.GetRandomFileName)).Invoke().ShouldBeOfType<string>().Length.ShouldBe(12);
        StaticMember(nameof(Path.HasExtension)).Invoke(["file.txt"]).ShouldBe(true);
#if NET
        StaticMember(nameof(Path.EndsInDirectorySeparator)).Invoke([Path.GetTempPath()]).ShouldBe(true);
        StaticMember(nameof(Path.GetFullPath)).Invoke(["file.txt", Path.GetTempPath()]).ShouldBe(Path.GetFullPath("file.txt", Path.GetTempPath()));
        StaticMember(nameof(Path.GetRelativePath)).Invoke([Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "file.txt")]).ShouldBe("file.txt");
        StaticMember(nameof(Path.IsPathFullyQualified)).Invoke([Path.GetFullPath("file.txt")]).ShouldBe(true);
        StaticMember(nameof(Path.Join)).Invoke(["one", "two"]).ShouldBe(Path.Join("one", "two"));
        StaticMember(nameof(Path.Join)).Invoke(["one", "two", "three"]).ShouldBe(Path.Join("one", "two", "three"));
        StaticMember(nameof(Path.Join)).Invoke(["one", "two", "three", "four"]).ShouldBe(Path.Join("one", "two", "three", "four"));
        StaticMember(nameof(Path.Join)).Invoke(["one", "two", "three", "four", "five"]).ShouldBe(Path.Join(["one", "two", "three", "four", "five"]));
        StaticMember(nameof(Path.TrimEndingDirectorySeparator)).Invoke([Path.GetTempPath()]).ShouldBe(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
#endif
    }

    [Fact]
    public void Path_GetTempFileName()
    {
        string path = StaticMember(nameof(Path.GetTempFileName)).Invoke().ShouldBeOfType<string>();

        File.Exists(path).ShouldBeTrue();
        File.Delete(path);
    }

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
