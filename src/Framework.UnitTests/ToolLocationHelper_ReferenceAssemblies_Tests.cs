// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    /// <summary>
    ///  Verifies that reference-assembly paths include the framework identifier, version, and profile.
    /// </summary>
    [Fact]
    public void GenerateReferencAssemblyPathAllElements()
    {
        string targetFrameworkRootPath = NativeMethodsShared.IsWindows
            ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
            : "/usr/lib";
        string targetFrameworkIdentifier = "Compact Framework";
        Version targetFrameworkVersion = new("1.0");
        string targetFrameworkProfile = "PocketPC";

        FrameworkName frameworkName = new(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

        string expectedPath = Path.Combine(targetFrameworkRootPath, targetFrameworkIdentifier);
        expectedPath = Path.Combine(expectedPath, "v" + targetFrameworkVersion);
        expectedPath = Path.Combine(expectedPath, "Profile");
        expectedPath = Path.Combine(expectedPath, targetFrameworkProfile);

        string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        path.ShouldBe(expectedPath, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that a reference-assembly path omits the profile directory when no profile is specified.
    /// </summary>
    [Fact]
    public void GenerateReferencAssemblyPathNoProfile()
    {
        string targetFrameworkRootPath = NativeMethodsShared.IsWindows
            ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
            : "/usr/lib";
        string targetFrameworkIdentifier = "Compact Framework";
        Version targetFrameworkVersion = new("1.0");
        FrameworkName frameworkName = new(targetFrameworkIdentifier, targetFrameworkVersion, string.Empty);
        string expectedPath = Path.Combine(targetFrameworkRootPath, targetFrameworkIdentifier);
        expectedPath = Path.Combine(expectedPath, "v" + targetFrameworkVersion);

        string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        path.ShouldBe(expectedPath, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that invalid profile-name characters cause <see cref="InvalidOperationException"/> during path generation.
    /// </summary>
    [Fact]
    public void GenerateReferencAssemblyInvalidProfile()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string targetFrameworkRootPath = NativeMethodsShared.IsWindows
                ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
                : "/usr/lib";
            string targetFrameworkIdentifier = "Compact Framework";
            Version targetFrameworkVersion = new("1.0");
            string targetFrameworkProfile = "PocketPC" + new string(Path.GetInvalidFileNameChars());

            FrameworkName frameworkName = new(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

            _ = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        });

    /// <summary>
    ///  Verifies that invalid framework-identifier characters cause <see cref="InvalidOperationException"/> during path generation.
    /// </summary>
    [Fact]
    public void GenerateReferencAssemblyInvalidIdentifier()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string targetFrameworkRootPath = NativeMethodsShared.IsWindows
                ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
                : "/usr/lib";
            string targetFrameworkIdentifier = "Compact Framework" + new string(Path.GetInvalidFileNameChars());
            Version targetFrameworkVersion = new("1.0");
            string targetFrameworkProfile = "PocketPC";

            FrameworkName frameworkName = new(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

            _ = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        });

    /// <summary>
    ///  Verifies that an excessively long reference-assembly path causes <see cref="InvalidOperationException"/>.
    /// </summary>
    [WindowsFullFrameworkOnlyFact]
    public void GenerateReferencAssemblyPathTooLong()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string pathTooLong = new('a', 500);

            string targetFrameworkRootPath = NativeMethodsShared.IsWindows
                ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
                : "/usr/lib";
            string targetFrameworkIdentifier = "Compact Framework" + pathTooLong;
            Version targetFrameworkVersion = new("1.0");
            string targetFrameworkProfile = "PocketPC";

            FrameworkName frameworkName = new(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

            _ = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        });

    /// <summary>
    ///  Verifies that a missing redist list returns <see langword="null"/> because there is no framework to chain to.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsNoRedistList()
    {
        string path = ToolLocationHelper.ChainReferenceAssemblyPath(@"PathDoesNotExistSoICannotChain");
        path.ShouldBeNull(); // " Expected the path to be null when the path to the FrameworkList.xml does not exist"
    }

    /// <summary>
    ///  Verifies that a redist list resolves its included framework directory.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsCircularRefernce()
    {
        string redistString41 = """
            <FileList Redist='Random' IncludeFramework='v4.0'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        string redistString40 = """
            <FileList Redist='Random'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        TransientTestFolder root = Environment.CreateFolder();
        string tempDirectory = root.Path;

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        string redist40Directory = Path.Combine(tempDirectory, "v4.0", "RedistList") + Path.DirectorySeparatorChar;
        TransientTestFolder redist41Folder = Environment.CreateFolder(redist41Directory);
        TransientTestFolder redist40Folder = Environment.CreateFolder(redist40Directory);
        Environment.CreateFile(redist40Folder, "FrameworkList.xml", redistString40);
        Environment.CreateFile(redist41Folder, "FrameworkList.xml", redistString41);

        ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"))
            .ShouldBe(Path.Combine(tempDirectory, "v4.0"), StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that a redist list without <c>IncludeFramework</c> ends the chain with an empty result.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsNoInclude()
    {
        string redistString41 = """
            <FileList Redist='Random'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        TransientTestFolder root = Environment.CreateFolder();
        string tempDirectory = root.Path;

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        TransientTestFolder redist41Folder = Environment.CreateFolder(redist41Directory);
        Environment.CreateFile(redist41Folder, "FrameworkList.xml", redistString41);
        ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"))
            .ShouldBeEmpty(); // "Expected the path to be empty"
    }

    /// <summary>
    ///  Verifies that an empty <c>IncludeFramework</c> value ends the chain without throwing.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsEmptyInclude()
    {
        string redistString41 = """
            <FileList Redist='Random' IncludeFramework=''>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        TransientTestFolder root = Environment.CreateFolder();
        string tempDirectory = root.Path;

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        TransientTestFolder redist41Folder = Environment.CreateFolder(redist41Directory);
        Environment.CreateFile(redist41Folder, "FrameworkList.xml", redistString41);
        ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"))
            .ShouldBeEmpty(); // "Expected the path to be empty"
    }

    /// <summary>
    ///  Verifies that valid XML without a <c>FileList</c> element ends the chain without throwing.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsNoFileList()
    {
        string redistString41 = """
            <FileListNOT Redist='Random'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileListNOT >
            """;

        TransientTestFolder root = Environment.CreateFolder();
        string tempDirectory = root.Path;

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        TransientTestFolder redist41Folder = Environment.CreateFolder(redist41Directory);
        Environment.CreateFile(redist41Folder, "FrameworkList.xml", redistString41);
        ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"))
            .ShouldBeEmpty(); // "Expected the path to be empty"
    }

    /// <summary>
    ///  Verifies that malformed redist XML causes <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsBadFile()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string redistString40 = "GARBAGE";
            TransientTestFolder root = Environment.CreateFolder();
            string tempDirectory = root.Path;

            string redist40Directory = Path.Combine(tempDirectory, "v4.0", "RedistList") + Path.DirectorySeparatorChar;
            TransientTestFolder redist40Folder = Environment.CreateFolder(redist40Directory);
            Environment.CreateFile(redist40Folder, "FrameworkList.xml", redistString40);

            ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.0"))
                .ShouldBeNull(); // "Expected the path to be null"
        });

    /// <summary>
    ///  Verifies that a missing included framework directory produces <see langword="null"/>.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistPointsToInvalidInclude()
    {
        string redistString41 = """
            <FileList Redist='Random' IncludeFramework='IDontExist'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList>
            """;

        TransientTestFolder root = Environment.CreateFolder();
        string tempDirectory = root.Path;

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
        TransientTestFolder redist41Folder = Environment.CreateFolder(redist41Directory);
        Environment.CreateFile(redist41Folder, "FrameworkList.xml", redistString41);

        ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath)
            .ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that invalid included-framework path characters cause <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistInvalidPathChars()
        => Should.Throw<InvalidOperationException>(() =>
        {
            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

            string redistString41 = $"""
                <FileList Redist='Random' IncludeFramework='{new string(invalidFileNameChars)}'>
                    <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
                </FileList>
                """;

            TransientTestFolder root = Environment.CreateFolder();
            string tempDirectory = root.Path;

            string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
            string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
            TransientTestFolder redist41Folder = Environment.CreateFolder(redist41Directory);
            Environment.CreateFile(redist41Folder, "FrameworkList.xml", redistString41);

            _ = ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath);
        });

    /// <summary>
    ///  Verifies that an excessively long included-framework path causes <see cref="InvalidOperationException"/>.
    /// </summary>
    [WindowsFullFrameworkOnlyFact]
    public void ChainReferenceAssembliesRedistPathTooLong()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string tooLong = new('a', 500);
            string redistString41 = $"""
                <FileList Redist='Random' IncludeFramework='{tooLong}'>
                    <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
                </FileList>
                """;

            TransientTestFolder root = Environment.CreateFolder();
            string tempDirectory = root.Path;

            string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
            string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
            TransientTestFolder redist41Folder = Environment.CreateFolder(redist41Directory);
            Environment.CreateFile(redist41Folder, "FrameworkList.xml", redistString41);

            _ = ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath);
        });

    /// <summary>
    ///  Verifies that chained reference-assembly directories are returned in framework inclusion order.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithRootGoodWithChain()
    {
        string redistString41 = """
            <FileList Redist='Random' IncludeFramework='v4.0'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        string redistString40 = """
            <FileList Redist='Random' IncludeFramework='v3.9'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        string redistString39 = """
            <FileList Redist='Random'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        TransientTestFolder root = Environment.CreateFolder();
        string tempDirectory = root.Path;

        string framework41Directory = Path.Combine(tempDirectory, "MyFramework", "v4.1") + Path.DirectorySeparatorChar;
        string framework41redistDirectory = Path.Combine(framework41Directory, "RedistList");

        string framework40Directory = Path.Combine(tempDirectory, "MyFramework", "v4.0") + Path.DirectorySeparatorChar;
        string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");

        string framework39Directory = Path.Combine(tempDirectory, "MyFramework", "v3.9") + Path.DirectorySeparatorChar;
        string framework39redistDirectory = Path.Combine(framework39Directory, "RedistList");

        TransientTestFolder framework41RedistFolder = Environment.CreateFolder(framework41redistDirectory);
        TransientTestFolder framework40RedistFolder = Environment.CreateFolder(framework40redistDirectory);
        TransientTestFolder framework39RedistFolder = Environment.CreateFolder(framework39redistDirectory);

        Environment.CreateFile(framework39RedistFolder, "FrameworkList.xml", redistString39);
        Environment.CreateFile(framework40RedistFolder, "FrameworkList.xml", redistString40);
        Environment.CreateFile(framework41RedistFolder, "FrameworkList.xml", redistString41);

        FrameworkName frameworkName = new("MyFramework", new Version("4.1"));
        IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(tempDirectory, frameworkName);

        // "Expected the method to return three paths."
        directories.ShouldHaveItems(
            directory => directory.ShouldBe(framework41Directory, StringCompareShould.IgnoreCase),
            directory => directory.ShouldBe(framework40Directory, StringCompareShould.IgnoreCase),
            directory => directory.ShouldBe(framework39Directory, StringCompareShould.IgnoreCase));
    }

    /// <summary>
    ///  Verifies that display names use the redist list's name or fall back to the framework name, version, and profile.
    /// </summary>
    [Fact]
    public void DisplayNameGeneration()
    {
        string redistString40 = """
            <FileList Redist='Random' Name='MyFramework 4.0' >
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        string redistString39 = """
            <FileList Redist='Random'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        TransientTestFolder root = Environment.CreateFolder();
        string tempDirectory = root.Path;

        string framework40Directory = $"{Path.Combine(tempDirectory, "MyFramework", "v4.0")}{Path.DirectorySeparatorChar}";
        string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");

        string framework39Directory = Path.Combine(tempDirectory, "MyFramework", "v3.9", "Profile", "Client");
        string framework39redistDirectory = Path.Combine(framework39Directory, "RedistList");

        TransientTestFolder framework40RedistFolder = Environment.CreateFolder(framework40redistDirectory);
        TransientTestFolder framework39RedistFolder = Environment.CreateFolder(framework39redistDirectory);

        Environment.CreateFile(framework39RedistFolder, "FrameworkList.xml", redistString39);
        Environment.CreateFile(framework40RedistFolder, "FrameworkList.xml", redistString40);

        ToolLocationHelper.GetDisplayNameForTargetFrameworkDirectory(framework40Directory, new("MyFramework", new Version("4.0")))
            .ShouldBe("MyFramework 4.0", StringCompareShould.IgnoreCase);
        ToolLocationHelper.GetDisplayNameForTargetFrameworkDirectory(framework39Directory, new("MyFramework", new Version("3.9"), "Client"))
            .ShouldBe("MyFramework v3.9 Client", StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that a circular redist-list chain terminates and includes each reference-assembly directory only once.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithRootCircularReference()
    {
        string redistString41 = """
            <FileList Redist='Random' IncludeFramework='v4.0'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        string redistString40 = """
            <FileList Redist='Random' IncludeFramework='v4.1'>
                <File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />
            </FileList >
            """;

        TransientTestFolder root = Environment.CreateFolder();
        string tempDirectory = root.Path;

        string framework41Directory = $"{Path.Combine(tempDirectory, "MyFramework", "v4.1")}{Path.DirectorySeparatorChar}";
        string framework41redistDirectory = Path.Combine(framework41Directory, "RedistList");

        string framework40Directory = $"{Path.Combine(tempDirectory, "MyFramework", "v4.0")}{Path.DirectorySeparatorChar}";
        string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");

        TransientTestFolder framework41RedistFolder = Environment.CreateFolder(framework41redistDirectory);
        TransientTestFolder framework40RedistFolder = Environment.CreateFolder(framework40redistDirectory);

        Environment.CreateFile(framework40RedistFolder, "FrameworkList.xml", redistString40);
        Environment.CreateFile(framework41RedistFolder, "FrameworkList.xml", redistString41);

        FrameworkName frameworkName = new("MyFramework", new Version("4.1"));
        IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(tempDirectory, frameworkName);

        // "Expected the method to return two paths."
        directories.ShouldHaveItems(
            directory => directory.ShouldBe(framework41Directory, StringCompareShould.IgnoreCase),
            directory => directory.ShouldBe(framework40Directory, StringCompareShould.IgnoreCase));
    }

    /// <summary>
    ///  Verifies that a <see langword="null"/> framework name is rejected when a reference-assembly root is supplied.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullFrameworkName()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies("Not Null String", frameworkName: null));

    /// <summary>
    ///  Verifies that <see langword="null"/> root and framework-name arguments are rejected.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullArgumentNameandFrameworkName()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(targetFrameworkRootPath: null, frameworkName: null));

    /// <summary>
    ///  Verifies that a <see langword="null"/> root is rejected when a framework name is supplied.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameNullRoot()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(targetFrameworkRootPath: null, new FrameworkName("Ident", new Version("2.0"))));

    /// <summary>
    ///  Verifies that an empty root is rejected when a framework name is supplied.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameEmptyRoot()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(string.Empty, new FrameworkName("Ident", new Version("2.0"))));

    /// <summary>
    ///  Verifies that an empty root cannot be used to generate a reference-assembly path.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameEmptyRoot2()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(string.Empty, new FrameworkName("Ident", new Version("2.0"))));

    /// <summary>
    ///  Verifies that default-location lookup rejects a <see langword="null"/> framework name.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesDefaultLocationNullFrameworkName()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName: null));

    /// <summary>
    ///  Verifies that .NET Framework 4.8 reference-assembly lookup matches the framework-location helper when available.
    /// </summary>
    /// <remarks>
    ///  Later .NET Framework releases update the runtime in place. A runtime directory beginning with
    ///  <c>v4.0</c> therefore does not establish which reference-assembly versions are installed.
    /// </remarks>
    [Fact]
    public void GetPathToReferenceAssembliesDefaultLocation48()
    {
        string? referenceAssemblyPath = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version48);
        if (referenceAssemblyPath is not null)
        {
            FrameworkName frameworkName = new(".NETFramework", new Version("4.8"));
            IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName);

            // "Expected the method to return one path."
            directories.ShouldHaveSingleItem(directory => directory.ShouldBe(referenceAssemblyPath, StringCompareShould.IgnoreCase));
        }

        // else
        // "Ignored because v4.8 did not seem to be installed"
    }

    /// <summary>
    ///  Verifies that an unavailable framework version produces no reference-assembly directories.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesDefaultLocation99()
    {
        string targetFrameworkIdentifier = ".Net Framework";
        Version targetFrameworkVersion = new("99.99");

        FrameworkName frameworkName = new(targetFrameworkIdentifier, targetFrameworkVersion, string.Empty);

        IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName);
        directories.ShouldBeEmpty(); // "Expected the method to return no paths."
    }

    /// <summary>
    ///  Verifies that the 32-bit Program Files directory is selected according to the operating system's architecture.
    /// </summary>
    [WindowsOnlyFact]
    public void TestGenerateProgramFiles32()
    {
        Environment.SpecialFolder folder = System.Environment.Is64BitOperatingSystem
            ? System.Environment.SpecialFolder.ProgramFilesX86
            : System.Environment.SpecialFolder.ProgramFiles;
        string programFilesX86 = System.Environment.GetFolderPath(folder);
        string result = FrameworkLocationHelper.GenerateProgramFiles32();

        // 32-bit OS: "Expected to use program files"
        // 64-bit OS: "Expected to use program files x86"
        result.ShouldBe(programFilesX86, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that the reference-assembly root is derived from the 32-bit Program Files directory.
    /// </summary>
    [WindowsOnlyFact(additionalMessage: "No ProgramFiles known location outside Windows.")]
    public void TestGeneratedReferenceAssemblyPath()
    {
        string programFiles32 = FrameworkLocationHelper.GenerateProgramFiles32();
        string referenceAssemblyRoot = FrameworkLocationHelper.GenerateProgramFilesReferenceAssemblyRoot();
        string pathToCombineWith = @"Reference Assemblies\Microsoft\Framework";
        string combinedPath = Path.Combine(programFiles32, pathToCombineWith);
        string fullPath = Path.GetFullPath(combinedPath);

        referenceAssemblyRoot.ShouldBe(fullPath, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that a simulated .NET Framework 2.0 installation returns its runtime directory.
    /// </summary>
    [Fact]
    public void LegacyFramework20Good()
    {
        FrameworkName frameworkName = new("Anything", new Version("2.0"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNet20Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldHaveSingleItem(directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet20FrameworkPath));
    }

    /// <summary>
    ///  Verifies that a missing .NET Framework 2.0 installation produces no legacy assembly paths.
    /// </summary>
    [Fact]
    public void LegacyFramework20NotInstalled()
    {
        FrameworkName frameworkName = new("Anything", new Version("2.0"));
        LegacyFrameworkTestHelper legacyHelper = new();

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.0 lookup includes its reference and runtime directories followed by the 2.0 runtime.
    /// </summary>
    [Fact]
    public void LegacyFramework30Good()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.0"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNetReferenceAssemblies30Installed = true,
            DotNet30Installed = true,
            DotNet20Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldHaveItems(
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet30FrameworkPath),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet20FrameworkPath));
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.0 lookup produces no paths when its runtime directory is missing.
    /// </summary>
    [Fact]
    public void LegacyFramework30NotInstalled()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.0"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNetReferenceAssemblies30Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.0 lookup produces no paths when its reference assemblies are missing.
    /// </summary>
    [Fact]
    public void LegacyFramework30ReferenceAssembliesNotInstalled()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.0"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNet30Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.0 lookup omits the 2.0 runtime when it is not installed.
    /// </summary>
    [Fact]
    public void LegacyFramework30WithNo20Installed()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.0"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNet30Installed = true,
            DotNetReferenceAssemblies30Installed = true,
        };

        // Note no 2.0 installed
        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldHaveItems(
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath, StringCompareShould.IgnoreCase),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet30FrameworkPath, StringCompareShould.IgnoreCase));
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.5 lookup includes its reference and runtime directories and the 3.0 and 2.0 chain.
    /// </summary>
    [Fact]
    public void LegacyFramework35Good()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNetReferenceAssemblies35Installed = true,
            DotNetReferenceAssemblies30Installed = true,
            DotNet30Installed = true,
            DotNet35Installed = true,
            DotNet20Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldHaveItems(
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet35FrameworkPath),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet30FrameworkPath),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet20FrameworkPath));
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.5 lookup produces no paths when its runtime directory is missing.
    /// </summary>
    [Fact]
    public void LegacyFramework35NotInstalled()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNetReferenceAssemblies35Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.5 lookup produces no paths when its reference assemblies are missing.
    /// </summary>
    [Fact]
    public void LegacyFramework35ReferenceAssembliesNotInstalled()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNet35Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies architecture-specific .NET Framework 3.5 standard-library paths on a 64-bit operating system.
    /// </summary>
    /// <remarks>
    ///  <c>x86</c> selects the 32-bit .NET Framework 2.0 runtime directory; <c>x64</c> and <c>itanium</c>
    ///  select the 64-bit directory. Unrecognized platforms use the current process's architecture.
    /// </remarks>
    [Fact]
    public void GetPathToStandardLibraries64Bit35()
    {
        string frameworkDirectory2032bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture.Bitness32);
        string frameworkDirectory2064bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture.Bitness64);
        string frameworkDirectory20Current = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture.Current);

        if (!System.Environment.Is64BitOperatingSystem)
        {
            // "Not 64 bit OS "
            return;
        }

        if (string.IsNullOrEmpty(frameworkDirectory2032bit) || string.IsNullOrEmpty(frameworkDirectory2064bit) || string.IsNullOrEmpty(frameworkDirectory20Current))
        {
            // ".Net 2.0 not installed: checked current {0} :: 64 bit :: {1} :: 32 bit {2}", frameworkDirectory20Current, frameworkDirectory2064bit, frameworkDirectory2032bit
            return;
        }

        string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "x86");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "x64");
        pathToFramework.ShouldBe(frameworkDirectory2064bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "itanium");
        pathToFramework.ShouldBe(frameworkDirectory2064bit, StringCompareShould.IgnoreCase);

        if (!System.Environment.Is64BitProcess)
        {
            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "RandomPlatform");
            pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);
        }
        else
        {
            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "RandomPlatform");
            pathToFramework.ShouldBe(frameworkDirectory2064bit, StringCompareShould.IgnoreCase);
        }
    }

    /// <summary>
    ///  Verifies that .NET Framework 4.0 standard-library paths are architecture-independent on a 64-bit operating system.
    /// </summary>
    /// <remarks>
    ///  Every tested platform resolves to the directory returned by
    ///  <see cref="ToolLocationHelper.GetPathToReferenceAssemblies(FrameworkName)"/>.
    /// </remarks>
    [Fact]
    public void GetPathToStandardLibraries64Bit40()
    {
        IList<string> referencePaths = ToolLocationHelper.GetPathToReferenceAssemblies(new FrameworkName(".NETFramework", new Version("4.0")));

        if (!System.Environment.Is64BitOperatingSystem)
        {
            // "Not 64 bit OS "
            return;
        }

        if (referencePaths.Count == 0)
        {
            // ".Net 4.0 not installed"
            return;
        }

        string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "x86");
        string dotNet40Path = FileUtilities.EnsureNoTrailingSlash(referencePaths[0]);
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "x64");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "itanium");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "RandomPlatform");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.5 standard-library lookup uses the 32-bit runtime on a 32-bit operating system.
    /// </summary>
    /// <remarks>
    ///  <c>x86</c>, <c>x64</c>, <c>itanium</c>, and unrecognized platforms all select the 32-bit .NET Framework 2.0 directory.
    /// </remarks>
    [Fact]
    public void GetPathToStandardLibraries32Bit35()
    {
        string frameworkDirectory2032bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture.Bitness32);
        string frameworkDirectory20Current = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture.Current);

        if (System.Environment.Is64BitOperatingSystem)
        {
            // "Is a 64 bit OS "
            return;
        }

        if (string.IsNullOrEmpty(frameworkDirectory2032bit) || string.IsNullOrEmpty(frameworkDirectory20Current))
        {
            // ".Net 2.0 not installed: checked current {0} :: 32 bit {2}", frameworkDirectory20Current, frameworkDirectory2032bit
            return;
        }

        string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "x86");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "x64");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "itanium");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "RandomPlatform");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that .NET Framework 4.0 standard-library paths are architecture-independent on a 32-bit operating system.
    /// </summary>
    /// <remarks>
    ///  Every tested platform resolves to the directory returned by
    ///  <see cref="ToolLocationHelper.GetPathToReferenceAssemblies(FrameworkName)"/>.
    /// </remarks>
    [Fact]
    public void GetPathToStandardLibraries32Bit40()
    {
        IList<string> referencePaths = ToolLocationHelper.GetPathToReferenceAssemblies(new FrameworkName(".NETFramework", new Version("4.0")));

        if (System.Environment.Is64BitOperatingSystem)
        {
            // "Is 64 bit OS "
            return;
        }

        if (referencePaths.Count == 0)
        {
            // ".Net 4.0 not installed"
            return;
        }

        string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "x86");
        string dotNet40Path = FileUtilities.EnsureNoTrailingSlash(referencePaths[0]);
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "x64");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "itanium");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "RandomPlatform");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that .NET Framework 3.5 lookup retains the 3.0 chain but omits an unavailable 2.0 runtime.
    /// </summary>
    [Fact]
    public void LegacyFramework35WithNo20Installed()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNetReferenceAssemblies35Installed = true,
            DotNetReferenceAssemblies30Installed = true,
            DotNet35Installed = true,
            DotNet30Installed = true,
        };

        // Note no 2.0 installed
        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldHaveItems(
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath, StringCompareShould.IgnoreCase),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet35FrameworkPath, StringCompareShould.IgnoreCase),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath, StringCompareShould.IgnoreCase),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet30FrameworkPath, StringCompareShould.IgnoreCase));
    }

    /// <summary>
    ///  Verifies that an incomplete .NET Framework 3.0 installation stops the 3.5 chain before both 3.0 and 2.0.
    /// </summary>
    [Fact]
    public void LegacyFramework35WithNo30Installed()
    {
        FrameworkName frameworkName = new("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNetReferenceAssemblies35Installed = true,
            DotNet35Installed = true,
            DotNet20Installed = true,
        };

        // Note no 3.0

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldHaveItems(
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath, StringCompareShould.IgnoreCase),
            directory => directory.ShouldBe(LegacyFrameworkTestHelper.DotNet35FrameworkPath, StringCompareShould.IgnoreCase));
    }

    /// <summary>
    ///  Verifies that a missing .NET Framework 4.0 installation produces no legacy assembly paths.
    /// </summary>
    [Fact]
    public void LegacyFramework40NotInstalled()
    {
        FrameworkName frameworkName = new("Anything", new Version("4.0"));
        LegacyFrameworkTestHelper legacyHelper = new();

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that .NET Framework 4.0 lookup produces no paths when its runtime directory is missing.
    /// </summary>
    [Fact]
    public void LegacyFramework40DotNetFrameworkDirectoryNotInstalled()
    {
        FrameworkName frameworkName = new("Anything", new Version("4.0"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNetReferenceAssemblies40Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that .NET Framework 4.0 lookup produces no paths when its reference assemblies are missing.
    /// </summary>
    [Fact]
    public void LegacyFramework40DotNetReferenceAssemblyDirectoryNotInstalled()
    {
        FrameworkName frameworkName = new("Anything", new Version("4.0"));
        LegacyFrameworkTestHelper legacyHelper = new()
        {
            DotNet40Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that standard-library lookup finds a framework under a custom root.
    /// </summary>
    [Fact]
    public void GetPathToStandardLibrariesWithCustomTargetFrameworkRoot()
    {
        string frameworkName = "Foo Framework";
        string frameworkVersion = "v0.1";
        string rootDir = Path.Combine(Environment.DefaultTestDirectory.Path, "framework-root");

        string asmPath = CreateNewFrameworkAndGetAssembliesPath(frameworkName, frameworkVersion, rootDir);

        string stdLibPath = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null, rootDir);
        stdLibPath.ShouldBe(asmPath);
    }

    /// <summary>
    ///  Verifies that a <see langword="null"/> platform argument preserves the default standard-library path.
    /// </summary>
    [Fact]
    public void GetPathToStandardLibrariesWithNullTargetFrameworkRootPath()
    {
        string frameworkName = ".NETFramework";
        string frameworkVersion = "v4.5";

        string v45Path = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty);

        // This look up should fall back the default path with the .NET frameworks
        string v45PathWithNullRoot = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null);

        v45PathWithNullRoot.ShouldBe(v45Path);
    }

    /// <summary>
    ///  Verifies that standard-library lookup searches fallback roots when no custom root is specified.
    /// </summary>
    [Fact]
    public void GetPathToStandardLibrariesWithCustomTargetFrameworkInFallbackSearchPathAndNullRoot()
    {
        string frameworkName = "Foo Framework";
        string frameworkVersion = "v0.1";
        string customFrameworkRootPath = Path.Combine(Environment.DefaultTestDirectory.Path, "framework-root");

        string asmPath = CreateNewFrameworkAndGetAssembliesPath(frameworkName, frameworkVersion, customFrameworkRootPath);
        string fallbackSearchPaths = $"/foo/bar;{customFrameworkRootPath};/a/b";

        string stdLibPath = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null, null, fallbackSearchPaths);
        stdLibPath.ShouldBe(asmPath);
    }

    /// <summary>
    ///  Verifies that a custom standard-library root takes precedence over fallback search paths.
    /// </summary>
    [Fact]
    public void GetPathToStandardLibrariesWithCustomRootAndCustomTargetFrameworkInFallbackSearchPath()
    {
        // We are creating the same framework in the root path and in a second location, used as a
        // fallback search path. When trying to find the framework, we should always resolve to
        // the framework in the root path, because the search order is:
        //  1. rootPath or default path, if null
        //  2. fallback search paths
        string frameworkName = "Foo Framework";
        string frameworkVersion = "v0.1";
        string rootDir = Path.Combine(Environment.CreateFolder().Path, "framework-root");
        string fallbackPath = Path.Combine(Environment.CreateFolder().Path, "framework-root");

        string asmPathForRoot = CreateNewFrameworkAndGetAssembliesPath(frameworkName, frameworkVersion, rootDir);
        CreateNewFrameworkAndGetAssembliesPath(frameworkName, frameworkVersion, fallbackPath);
        string fallbackSearchPaths = $"/foo/bar;{fallbackPath};/a/b";

        string stdLibPath = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null, rootDir, fallbackSearchPaths);

        // the path should be for the framework in the root path
        stdLibPath.ShouldBe(asmPathForRoot);
    }

    /// <summary>
    ///  Verifies that <see langword="null"/> platform and root arguments preserve the default standard-library path.
    /// </summary>
    [Fact]
    public void GetPathToStandardLibrariesWithNullTargetFrameworkFallbackSearchPaths()
    {
        string frameworkName = ".NETFramework";
        string frameworkVersion = "v4.5";

        string v45Path = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty);

        // This look up should fall back the default path with the .NET frameworks
        string v45PathWithNullRoot = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null, null);

        v45PathWithNullRoot.ShouldBe(v45Path);
    }

    /// <summary>
    ///  Verifies that reference-assembly lookup finds a framework under a custom root.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkInRoot()
    {
        string customFrameworkDirToUse = Path.Combine(Environment.CreateFolder().Path, "framework-root");

        CheckGetPathToReferenceAssemblies(
            customFrameworkDirToUse,
            null,
            (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string? fallbackSearchPaths)
                => ToolLocationHelper.GetPathToReferenceAssemblies(
                    frameworkName, $"v{frameworkVersion}", frameworkProfile, customFrameworkDir));
    }

    /// <summary>
    ///  Verifies that reference-assembly lookup searches fallback paths when a custom root has no matching framework.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkInFallbackPath()
    {
        string customFrameworkDirToUse = Path.Combine(Environment.CreateFolder().Path, "framework-root");
        string searchPaths = $"/foo/bar;{customFrameworkDirToUse}";
        string rootDir = Environment.CreateFolder().Path;

        CheckGetPathToReferenceAssemblies(
            customFrameworkDirToUse,
            searchPaths,
            (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string? fallbackSearchPaths)
                => ToolLocationHelper.GetPathToReferenceAssemblies(
                    frameworkName, $"v{frameworkVersion}", frameworkProfile, rootDir, fallbackSearchPaths));
    }

    /// <summary>
    ///  Verifies that reference-assembly lookup searches fallback paths when the custom root is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkInFallbackPathAndNullRoot()
    {
        string customFrameworkDirToUse = Path.Combine(Environment.CreateFolder().Path, "framework-root");
        string searchPaths = $"/foo/bar;{customFrameworkDirToUse}";

        CheckGetPathToReferenceAssemblies(
            customFrameworkDirToUse,
            searchPaths,
            (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string? fallbackSearchPaths)
                => ToolLocationHelper.GetPathToReferenceAssemblies(
                    frameworkName,
                    $"v{frameworkVersion}",
                    frameworkProfile,
                    targetFrameworkRootPath: null,
                    targetFrameworkFallbackSearchPaths: fallbackSearchPaths));
    }

    /// <summary>
    ///  Verifies that the <see cref="FrameworkName"/> overload finds reference assemblies under a custom root.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkInRoot2()
    {
        string customFrameworkDirToUse = Path.Combine(Environment.CreateFolder().Path, "framework-root");
        CheckGetPathToReferenceAssemblies(
            customFrameworkDirToUse,
            null,
            (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string? fallbackSearchPaths)
                => ToolLocationHelper.GetPathToReferenceAssemblies(
                    customFrameworkDir,
                    fallbackSearchPaths,
                    new FrameworkName(frameworkName, new Version(frameworkVersion), frameworkProfile)));
    }

    /// <summary>
    ///  Verifies that the <see cref="FrameworkName"/> overload searches fallback paths after an unmatched custom root.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkRootInFallbackPath2()
    {
        string customFrameworkDirToUse = Path.Combine(Environment.CreateFolder().Path, "framework-root");
        string searchPaths = $"{customFrameworkDirToUse};/a/b";
        string rootDir = Environment.CreateFolder().Path;

        CheckGetPathToReferenceAssemblies(
            customFrameworkDirToUse,
            searchPaths,
            (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string? fallbackSearchPaths)
                => ToolLocationHelper.GetPathToReferenceAssemblies(
                    rootDir,
                    fallbackSearchPaths,
                    new FrameworkName(frameworkName, new Version(frameworkVersion), frameworkProfile)));
    }

    /// <summary>
    ///  Verifies that the <see cref="FrameworkName"/> overload searches fallback paths with a <see langword="null"/> root.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkRootInFallbackPathAndNullRoot2()
    {
        string customFrameworkDirToUse = Path.Combine(Environment.CreateFolder().Path, "framework-root");
        string searchPaths = $"{customFrameworkDirToUse};/a/b";
        CheckGetPathToReferenceAssemblies(
            customFrameworkDirToUse,
            searchPaths,
            (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string? fallbackSearchPaths)
                => ToolLocationHelper.GetPathToReferenceAssemblies(
                    targetFrameworkRootPath: null,
                    fallbackSearchPaths,
                    new FrameworkName(frameworkName, new Version(frameworkVersion), frameworkProfile)));
    }

    /// <summary>
    ///  Verifies that a lookup delegate finds a framework created under a custom root.
    /// </summary>
    /// <param name="customFrameworkDir">The root under which to create the framework.</param>
    /// <param name="fallbackSearchPaths">The fallback search paths, or <see langword="null"/> to omit them.</param>
    /// <param name="getPathToReferenceAssemblies">The reference-assembly lookup to verify.</param>
    private void CheckGetPathToReferenceAssemblies(
        string customFrameworkDir,
        string? fallbackSearchPaths,
        Func<string, string, string, string, string?, IList<string>> getPathToReferenceAssemblies)
    {
        string frameworkName = "Foo Framework";
        string frameworkVersion = "0.1";
        string frameworkVersionWithV = $"v{frameworkVersion}";
        string frameworkProfile = string.Empty;

        CreateNewFrameworkAndGetAssembliesPath(frameworkName, frameworkVersionWithV, customFrameworkDir);

        IList<string> stdLibPaths = getPathToReferenceAssemblies(frameworkName, frameworkVersion, frameworkProfile, customFrameworkDir, fallbackSearchPaths);
        string expectedPath = $"{Path.Combine(customFrameworkDir, frameworkName, frameworkVersionWithV)}{Path.DirectorySeparatorChar}";
        stdLibPaths.ShouldHaveSingleItem(directory => directory.ShouldBe(expectedPath, directory));
    }

    /// <summary>
    ///  Verifies that a <see langword="null"/> reference-assembly root preserves default framework lookup.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithNullTargetFrameworkRootPath()
    {
        string frameworkName = ".NETFramework";
        string frameworkVersion = "v4.5";

        var v45Paths = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, frameworkVersion, string.Empty);

        // This look up should fall back the default path with the .NET frameworks
        var v45PathsWithNullRoot = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, frameworkVersion, string.Empty, null);

        v45PathsWithNullRoot.ShouldBe(v45Paths);
    }

    /// <summary>
    ///  Verifies that <see langword="null"/> root and fallback arguments preserve default reference-assembly lookup.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithNullTargetFrameworkFallbackSearchPaths()
    {
        string frameworkName = ".NETFramework";
        string frameworkVersion = "v4.5";

        var v45Paths = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, frameworkVersion, string.Empty);

        // This look up should fall back the default path with the .NET frameworks
        var v45PathsWithNullRoot = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, frameworkVersion, string.Empty, null, null);

        v45PathsWithNullRoot.ShouldBe(v45Paths);
    }

    /// <summary>
    ///  Creates reference assemblies and a redist list for a framework under the specified root.
    /// </summary>
    /// <param name="frameworkName">The framework identifier.</param>
    /// <param name="frameworkVersion">The version directory name, including any leading <c>v</c>.</param>
    /// <param name="rootDir">The directory under which to create the framework.</param>
    /// <returns>
    ///  The directory containing the test reference assemblies.
    /// </returns>
    private string CreateNewFrameworkAndGetAssembliesPath(string frameworkName, string frameworkVersion, string rootDir)
    {
        string frameworkListXml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <FileList  Name="{frameworkName}"/>
            """;

        string asmPath = Path.Combine(rootDir, frameworkName, frameworkVersion);
        TransientTestFolder frameworkFolder = Environment.CreateFolder(asmPath);
        TransientTestFolder redistFolder = frameworkFolder.CreateDirectory("RedistList");

        Environment.CreateFile(redistFolder, "FrameworkList.xml", frameworkListXml);
        Environment.CreateFile(frameworkFolder, "mscorlib.dll", string.Empty);

        return asmPath;
    }
}
