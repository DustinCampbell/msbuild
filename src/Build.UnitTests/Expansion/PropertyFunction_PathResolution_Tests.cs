// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class PropertyFunction_PathResolution_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    ///  TransientTestState that saves/restores <see cref="FileUtilities.CurrentThreadWorkingDirectory"/>.
    /// </summary>
    private sealed class TransientThreadWorkingDirectory : TransientTestState
    {
        private readonly string? _originalValue;

        public TransientThreadWorkingDirectory(string? newWorkingDirectory)
        {
            _originalValue = FileUtilities.CurrentThreadWorkingDirectory;
            FileUtilities.CurrentThreadWorkingDirectory = newWorkingDirectory;
        }

        public override void Revert()
        {
            FileUtilities.CurrentThreadWorkingDirectory = _originalValue;
        }
    }

    /// <summary>
    ///  Helper: expand a property function expression with CurrentThreadWorkingDirectory set,
    ///  simulating -mt mode where Environment.CurrentDirectory may point elsewhere.
    /// </summary>
    private static string ExpandWithThreadWorkingDirectory(TestEnvironment env, string expression, string? workingDir, string? wrongDir = null)
    {
        env.WithTransientTestState(new TransientThreadWorkingDirectory(workingDir));
        if (wrongDir is not null)
        {
            env.SetCurrentDirectory(wrongDir);
        }

        return ExpandProperties(expression).ShouldNotBeNull();
    }

    /// <summary>
    ///  Helper: set the process current directory and return it as the OS reports it. On macOS the test
    ///  temp folder is reached through a symlink (/var -> /private/var), and only the reported form matches
    ///  what resolution against the process current directory produces, so tests that compare -mt output
    ///  against non-mt output must build both sides from this rather than from the TestEnvironment path.
    /// </summary>
    private static string SetCurrentDirectoryCanonical(TestEnvironment env, string path)
    {
        env.SetCurrentDirectory(path);
        return Directory.GetCurrentDirectory();
    }

    // =====================================================================
    // Category A: -mt mode tests for default-allowed File methods
    // =====================================================================

    [Fact]
    public void NormalizePath_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::NormalizePath('obj', 'file.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_ParentSegment_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::NormalizePath('obj', '..', 'file.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "file.txt"));
    }

    [UnixOnlyFact]
    public void NormalizePath_BackslashRootedPath_MatchesNonMultithreadedResult()
    {
        using var env = TestEnvironment.Create(_output);
        var projectDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        // Baseline: non-mt mode, where the process current directory is the project directory.
        string projectDirPath = SetCurrentDirectoryCanonical(env, projectDir.Path);
        string expected = IntrinsicFunctions.NormalizePath(@"\tmp\file.txt");

        // -mt mode must agree: on Unix a backslash is an ordinary filename character, so resolution
        // must not normalize separators or it would silently point at a different file.
        string? result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::NormalizePath('\tmp\file.txt'))",
            projectDirPath,
            wrongDir.Path);

        result.ShouldBe(expected);
    }

    [Fact]
    public void NormalizePath_BackslashSeparatedPath_MatchesNonMultithreadedResult()
    {
        using var env = TestEnvironment.Create(_output);
        var projectDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string projectDirPath = SetCurrentDirectoryCanonical(env, projectDir.Path);
        string expected = IntrinsicFunctions.NormalizePath(@"obj\..\file.txt");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::NormalizePath('obj\..\file.txt'))",
            projectDirPath,
            wrongDir.Path);

        result.ShouldBe(expected);
    }

    [Fact]
    public void MSBuildFileExists_BackslashSeparatedPath_MatchesNonMultithreadedResult()
    {
        using var env = TestEnvironment.Create(_output);
        var projectDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(projectDir.Path, "sub"));
        File.WriteAllText(Path.Combine(projectDir.Path, "sub", "marker.txt"), "x");

        // FileExistsNoThrow normalizes separators internally, so both non-mt and -mt must agree.
        env.SetCurrentDirectory(projectDir.Path);
        string expected = IntrinsicFunctions.FileExists(@"sub\marker.txt").ToString();

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::FileExists('sub\marker.txt'))",
            projectDir.Path,
            wrongDir.Path);

        result.ShouldBe(expected);
    }

    [WindowsOnlyFact]
    public void NormalizePath_DriveRelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);
        string drive = Path.GetPathRoot(correctDir.Path)!.Substring(0, 2);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([MSBuild]::NormalizePath('{drive}obj', 'file.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_AbsolutePath_IgnoresThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var absoluteDir = env.CreateFolder(createFolder: true);

        string absolutePath = Path.Combine(absoluteDir.Path, "file.txt");
        string? result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([MSBuild]::NormalizePath('{absolutePath}'))",
            correctDir.Path);

        result.ShouldBe(absolutePath);
    }

    [Fact]
    public void NormalizePath_WithoutThreadWorkingDirectory_UsesProcessWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var processDir = env.CreateFolder(createFolder: true);
        string processDirPath = SetCurrentDirectoryCanonical(env, processDir.Path);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::NormalizePath('obj', 'file.txt'))",
            workingDir: null);

        result.ShouldBe(Path.Combine(processDirPath, "obj", "file.txt"));
    }

    [Fact]
    public void NormalizePath_EmptyPath_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() =>
            IntrinsicFunctions.NormalizePath([]));

    [Fact]
    public void NormalizePath_NullPathArray_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() =>
            IntrinsicFunctions.NormalizePath((string[]?)null));

    [Fact]
    public void NormalizePath_IllegalPath_ThrowsArgumentException()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        env.WithTransientTestState(new TransientThreadWorkingDirectory(correctDir.Path));

        // Resolution against the thread working directory intentionally swallows the invalid-path
        // exception (so that non-throwing intrinsics such as FileExists keep working); NormalizePath
        // is still expected to surface it, matching non-mt behavior.
        Should.Throw<ArgumentException>(() =>
            IntrinsicFunctions.NormalizePath("bad\0path"));
    }

    [Fact]
    public void NormalizeDirectory_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::NormalizeDirectory('obj'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "obj") + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void MSBuildFileExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::FileExists('marker.txt'))",
            correctDir.Path,
            wrongDir.Path)
            .ShouldBe("True");
        ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::FileExists('absent.txt'))",
            correctDir.Path,
            wrongDir.Path)
            .ShouldBe("False");
    }

    [UnixOnlyFact]
    public void MSBuildFileExists_BackslashRootedPath_MatchesNonMultithreadedResult()
    {
        using var env = TestEnvironment.Create(_output);
        var projectDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        // FileExistsNoThrow normalizes separators internally, so on Unix '\tmp\x' is rooted by the
        // time the probe happens. Resolution must not treat it as relative to the project directory.
        Directory.CreateDirectory(Path.Combine(projectDir.Path, "tmp"));
        File.WriteAllText(Path.Combine(projectDir.Path, "tmp", "decoy.txt"), "x");

        env.SetCurrentDirectory(projectDir.Path);
        string expected = IntrinsicFunctions.FileExists(@"\tmp\decoy.txt").ToString();

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([MSBuild]::FileExists('\tmp\decoy.txt'))",
            projectDir.Path,
            wrongDir.Path);

        result.ShouldBe(expected);
    }

    [Fact]
    public void MSBuildDirectoryExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "obj"));

        ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::DirectoryExists('obj'))",
            correctDir.Path,
            wrongDir.Path)
            .ShouldBe("True");
        ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::DirectoryExists('absent'))",
            correctDir.Path,
            wrongDir.Path)
            .ShouldBe("False");
    }

    [Fact]
    public void GetDirectoryNameOfFileAbove_RelativeStartingDirectory_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::GetDirectoryNameOfFileAbove('.', 'marker.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(correctDir.Path);
    }

    [Fact]
    public void GetPathOfFileAbove_RelativeStartingDirectory_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "marker.txt"), "x");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::GetPathOfFileAbove('marker.txt', '.'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe(Path.Combine(correctDir.Path, "marker.txt"));
    }

    [Fact]
    public void RegisterBuildCheck_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);
        File.WriteAllText(Path.Combine(correctDir.Path, "check.dll"), string.Empty);

        var (logger, loggingContext) = CreateLoggingContext(_output);

        env.WithTransientTestState(new TransientThreadWorkingDirectory(correctDir.Path));
        env.SetCurrentDirectory(wrongDir.Path);

        ExpandProperties("$([MSBuild]::RegisterBuildCheck('check.dll'))", loggingContext)
            .ShouldBe(bool.TrueString);
        var acquisition = logger.AllBuildEvents.ShouldHaveSingleItem().ShouldBeOfType<BuildCheckAcquisitionEventArgs>();

        acquisition.AcquisitionPath.ShouldBe(Path.Combine(correctDir.Path, "check.dll"));
    }

    [Fact]
    public void FileReadAllText_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "notes.txt"), "correct content");
        File.WriteAllText(Path.Combine(wrongDir.Path, "notes.txt"), "wrong content");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::ReadAllText('notes.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe("correct content");
    }

    [Fact]
    public void FileExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "exists.txt"), "data");
        // Do NOT create the file in wrongDir

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::Exists('exists.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe("True");
    }

    [WindowsOnlyFact]
    public void FileGetAttributes_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "attrs.txt");
        File.WriteAllText(filePath, "data");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([MSBuild]::BitwiseAnd(32,$([System.IO.File]::GetAttributes('attrs.txt'))))",
            correctDir.Path,
            wrongDir.Path);

        // FileAttributes.Archive = 32 — Windows-specific attribute
        result.ShouldBe("32");
    }

    [Fact]
    public void FileGetCreationTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "time.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetCreationTime(filePath);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetCreationTime('time.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FileGetLastWriteTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "time.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetLastWriteTime(filePath);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetLastWriteTime('time.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FileGetLastAccessTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "time.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetLastAccessTime(filePath);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetLastAccessTime('time.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    // =====================================================================
    // Category A: -mt mode tests for default-allowed Directory methods
    // =====================================================================

    [Fact]
    public void DirectoryExists_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "subdir"));
        // Do NOT create subdir in wrongDir

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::Exists('subdir'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldBe("True");
    }

    [Fact]
    public void DirectoryGetParent_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "parent", "child"));

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([System.IO.Directory]::GetParent('parent\child'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldNotBeNullOrEmpty();
        Path.GetFileName(result).ShouldBe("parent");
    }

    [Fact]
    public void DirectoryGetFiles_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(correctDir.Path, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "a.txt"), "data");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::GetFiles('sub'))",
            correctDir.Path,
            wrongDir.Path);

        // GetFiles returns string[], which MSBuild converts to a semicolon-separated string
        result.ShouldContain("a.txt");
    }

    [Fact]
    public void DirectoryGetDirectories_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "parent", "child"));

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::GetDirectories('parent'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldContain("child");
    }

    [Fact]
    public void DirectoryGetLastWriteTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(correctDir.Path, "sub");
        Directory.CreateDirectory(subDir);
        DateTime expected = Directory.GetLastWriteTime(subDir);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::GetLastWriteTime('sub'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DirectoryGetLastAccessTime_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(correctDir.Path, "sub");
        Directory.CreateDirectory(subDir);
        DateTime expected = Directory.GetLastAccessTime(subDir);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::GetLastAccessTime('sub'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    // =====================================================================
    // Category A+: Extended File methods (MSBUILDENABLEALLPROPERTYFUNCTIONS)
    // =====================================================================

    [Fact]
    public void FileReadAllLines_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "lines.txt"), "line1\nline2");
        File.WriteAllText(Path.Combine(wrongDir.Path, "lines.txt"), "wrong");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::ReadAllLines('lines.txt'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldContain("line1");
    }

    [Fact]
    public void FileReadAllBytes_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllBytes(Path.Combine(correctDir.Path, "data.bin"), [0x42]);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::ReadAllBytes('data.bin'))",
            correctDir.Path,
            wrongDir.Path);

        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void FileWriteAllText_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::WriteAllText('output.txt', 'hello'))",
            correctDir.Path,
            wrongDir.Path);

        File.Exists(Path.Combine(correctDir.Path, "output.txt")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(correctDir.Path, "output.txt")).ShouldBe("hello");
        File.Exists(Path.Combine(wrongDir.Path, "output.txt")).ShouldBeFalse();
    }

    [Fact]
    public void FileAppendAllText_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "append.txt"), "base");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::AppendAllText('append.txt', ' added'))",
            correctDir.Path,
            wrongDir.Path);

        File.ReadAllText(Path.Combine(correctDir.Path, "append.txt")).ShouldBe("base added");
    }

    [Fact]
    public void FileDelete_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string correctFile = Path.Combine(correctDir.Path, "todelete.txt");
        string wrongFile = Path.Combine(wrongDir.Path, "todelete.txt");
        File.WriteAllText(correctFile, "data");
        File.WriteAllText(wrongFile, "data");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::Delete('todelete.txt'))",
            correctDir.Path,
            wrongDir.Path);

        File.Exists(correctFile).ShouldBeFalse();
        File.Exists(wrongFile).ShouldBeTrue();
    }

    [Fact]
    public void FileGetCreationTimeUtc_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "utc.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetCreationTimeUtc(filePath);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetCreationTimeUtc('utc.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FileGetLastWriteTimeUtc_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "utc.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetLastWriteTimeUtc(filePath);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetLastWriteTimeUtc('utc.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FileGetLastAccessTimeUtc_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(correctDir.Path, "utc.txt");
        File.WriteAllText(filePath, "data");
        DateTime expected = File.GetLastAccessTimeUtc(filePath);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::GetLastAccessTimeUtc('utc.txt'))",
            correctDir.Path,
            wrongDir.Path);

        DateTime.Parse(result).ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    // =====================================================================
    // Category A+: Extended Directory methods (MSBUILDENABLEALLPROPERTYFUNCTIONS)
    // =====================================================================

    [Fact]
    public void DirectoryCreateDirectory_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::CreateDirectory('newdir'))",
            correctDir.Path,
            wrongDir.Path);

        Directory.Exists(Path.Combine(correctDir.Path, "newdir")).ShouldBeTrue();
        Directory.Exists(Path.Combine(wrongDir.Path, "newdir")).ShouldBeFalse();
    }

    [Fact]
    public void DirectoryDelete_RelativePath_ResolvesFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "todel"));
        Directory.CreateDirectory(Path.Combine(wrongDir.Path, "todel"));

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::Delete('todel'))",
            correctDir.Path,
            wrongDir.Path);

        Directory.Exists(Path.Combine(correctDir.Path, "todel")).ShouldBeFalse();
        Directory.Exists(Path.Combine(wrongDir.Path, "todel")).ShouldBeTrue();
    }

    // =====================================================================
    // Category B: Regular mode (CurrentThreadWorkingDirectory = null)
    // =====================================================================

    [Fact]
    public void FileReadAllText_AbsolutePath_WorksWithoutThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var dir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(dir.Path, "abs.txt");
        File.WriteAllText(filePath, "absolute content");

        // CurrentThreadWorkingDirectory is null (regular mode)
        string? result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.File]::ReadAllText('{filePath}'))",
            workingDir: null);

        result.ShouldBe("absolute content");
    }

    [Fact]
    public void FileExists_AbsolutePath_WorksWithoutThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var dir = env.CreateFolder(createFolder: true);

        string filePath = Path.Combine(dir.Path, "abs.txt");
        File.WriteAllText(filePath, "data");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.File]::Exists('{filePath}'))",
            workingDir: null);

        result.ShouldBe("True");
    }

    [Fact]
    public void DirectoryExists_AbsolutePath_WorksWithoutThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var dir = env.CreateFolder(createFolder: true);

        string subDir = Path.Combine(dir.Path, "subdir");
        Directory.CreateDirectory(subDir);

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.Directory]::Exists('{subDir}'))",
            workingDir: null);

        result.ShouldBe("True");
    }

    // =====================================================================
    // Category C: Absolute path passthrough (not mangled by resolution)
    // =====================================================================

    [Fact]
    public void FileReadAllText_AbsolutePath_NotMangledByThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var otherDir = env.CreateFolder(createFolder: true);

        string absFile = Path.Combine(otherDir.Path, "abs.txt");
        File.WriteAllText(absFile, "absolute content");

        // Even though CurrentThreadWorkingDirectory is set, absolute paths should pass through unchanged
        string? result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.File]::ReadAllText('{absFile}'))",
            correctDir.Path);

        result.ShouldBe("absolute content");
    }

    [Fact]
    public void FileExists_AbsolutePath_NotMangledByThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        var correctDir = env.CreateFolder(createFolder: true);
        var otherDir = env.CreateFolder(createFolder: true);

        string absFile = Path.Combine(otherDir.Path, "abs.txt");
        File.WriteAllText(absFile, "data");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            $"$([System.IO.File]::Exists('{absFile}'))",
            correctDir.Path);

        result.ShouldBe("True");
    }

    // =====================================================================
    // Category D: Multi-path method tests (Copy, Move with two relative paths)
    // =====================================================================

    [Fact]
    public void FileCopy_TwoRelativePaths_BothResolveFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "source.txt"), "copy me");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::Copy('source.txt', 'dest.txt'))",
            correctDir.Path,
            wrongDir.Path);

        File.Exists(Path.Combine(correctDir.Path, "dest.txt")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(correctDir.Path, "dest.txt")).ShouldBe("copy me");
        File.Exists(Path.Combine(wrongDir.Path, "dest.txt")).ShouldBeFalse();
    }

    [Fact]
    public void FileMove_TwoRelativePaths_BothResolveFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        File.WriteAllText(Path.Combine(correctDir.Path, "movesrc.txt"), "move me");

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::Move('movesrc.txt', 'movedst.txt'))",
            correctDir.Path,
            wrongDir.Path);

        File.Exists(Path.Combine(correctDir.Path, "movesrc.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(correctDir.Path, "movedst.txt")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(correctDir.Path, "movedst.txt")).ShouldBe("move me");
    }

    [Fact]
    public void DirectoryMove_TwoRelativePaths_BothResolveFromThreadWorkingDirectory()
    {
        using var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);
        var correctDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        Directory.CreateDirectory(Path.Combine(correctDir.Path, "dirsrc"));

        ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.Directory]::Move('dirsrc', 'dirdst'))",
            correctDir.Path,
            wrongDir.Path);

        Directory.Exists(Path.Combine(correctDir.Path, "dirsrc")).ShouldBeFalse();
        Directory.Exists(Path.Combine(correctDir.Path, "dirdst")).ShouldBeTrue();
    }

    // =====================================================================
    // Category E: Parent traversal test (../ relative paths)
    // =====================================================================

    [Fact]
    public void FileReadAllText_ParentTraversal_ResolvesCorrectly()
    {
        using var env = TestEnvironment.Create(_output);
        var rootDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        // Create structure: rootDir/sibling/file.txt, and set working dir to rootDir/subdir
        string siblingDir = Path.Combine(rootDir.Path, "sibling");
        string subDir = Path.Combine(rootDir.Path, "subdir");
        Directory.CreateDirectory(siblingDir);
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(siblingDir, "file.txt"), "traversal works");

        string? result = ExpandWithThreadWorkingDirectory(
            env,
            "$([System.IO.File]::ReadAllText('../sibling/file.txt'))",
            subDir,
            wrongDir.Path);

        result.ShouldBe("traversal works");
    }

    // =====================================================================
    // Category F: FixFilePath ordering test (backslash parent traversal)
    // =====================================================================

    [Fact]
    public void FileReadAllText_BackslashParentTraversal_ResolvesCorrectly()
    {
        using var env = TestEnvironment.Create(_output);
        var rootDir = env.CreateFolder(createFolder: true);
        var wrongDir = env.CreateFolder(createFolder: true);

        string siblingDir = Path.Combine(rootDir.Path, "sibling");
        string subDir = Path.Combine(rootDir.Path, "subdir");
        Directory.CreateDirectory(siblingDir);
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(siblingDir, "file.txt"), "backslash traversal works");

        // Use backslash separators (Windows-style) — FixFilePath must normalize before resolution
        string? result = ExpandWithThreadWorkingDirectory(
            env,
            @"$([System.IO.File]::ReadAllText('..\\sibling\\file.txt'))",
            subDir,
            wrongDir.Path);

        result.ShouldBe("backslash traversal works");
    }
}
