// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Directory_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Directory), output)
{
    [Fact]
    public void Directory_StaticMembers()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        var parent = env.CreateFolder();
        var child = env.CreateFolder(Path.Combine(parent.Path, "child"), createFolder: true);
        var file = env.CreateFile(child, "data.txt", "content");

        StaticMember(nameof(Directory.Exists)).Invoke([child.Path]).ShouldBe(true);
        StaticMember(nameof(Directory.GetDirectories)).Invoke([parent.Path]).ShouldBeOfType<string[]>().ShouldBe([child.Path]);
        StaticMember(nameof(Directory.GetDirectories)).Invoke([parent.Path, "ch*"]).ShouldBeOfType<string[]>().ShouldBe([child.Path]);
        StaticMember(nameof(Directory.GetDirectories)).Invoke([parent.Path, "ch*", "TopDirectoryOnly"]).ShouldBeOfType<string[]>().ShouldBe([child.Path]);
        StaticMember(nameof(Directory.GetFiles)).Invoke([child.Path]).ShouldBeOfType<string[]>().ShouldBe([file.Path]);
        StaticMember(nameof(Directory.GetFiles)).Invoke([parent.Path, "*.txt", SearchOption.AllDirectories]).ShouldBeOfType<string[]>().ShouldBe([file.Path]);
        StaticMember(nameof(Directory.GetLastAccessTime)).Invoke([child.Path]).ShouldBeOfType<DateTime>();
        StaticMember(nameof(Directory.GetLastWriteTime)).Invoke([child.Path]).ShouldBeOfType<DateTime>();
        StaticMember(nameof(Directory.GetParent)).Invoke([file.Path]).ShouldBeOfType<DirectoryInfo>().FullName.ShouldBe(child.Path);
    }

    [Fact]
    public void FileSystemInfo_InstanceMembers()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        var parent = env.CreateFolder();
        var child = env.CreateFolder(Path.Combine(parent.Path, "child"), createFolder: true);
        var file = env.CreateFile(child, "data.txt", "content");
        var directoryInfo = new DirectoryInfo(child.Path);
        var fileInfo = new FileInfo(file.Path);

        InstanceMember(nameof(FileSystemInfo.Attributes)).Invoke(directoryInfo).ShouldBe(directoryInfo.Attributes);
        InstanceMember(nameof(FileSystemInfo.CreationTime)).Invoke(directoryInfo).ShouldBe(directoryInfo.CreationTime);
        InstanceMember(nameof(FileSystemInfo.CreationTimeUtc)).Invoke(directoryInfo).ShouldBe(directoryInfo.CreationTimeUtc);
        InstanceMember(nameof(FileSystemInfo.Exists)).Invoke(directoryInfo).ShouldBe(true);
        InstanceMember(nameof(FileSystemInfo.Extension)).Invoke(fileInfo).ShouldBe(".txt");
        InstanceMember(nameof(FileSystemInfo.FullName)).Invoke(directoryInfo).ShouldBe(child.Path);
        InstanceMember(nameof(FileSystemInfo.LastAccessTime)).Invoke(directoryInfo).ShouldBe(directoryInfo.LastAccessTime);
        InstanceMember(nameof(FileSystemInfo.LastAccessTimeUtc)).Invoke(directoryInfo).ShouldBe(directoryInfo.LastAccessTimeUtc);
        InstanceMember(nameof(FileSystemInfo.LastWriteTime)).Invoke(directoryInfo).ShouldBe(directoryInfo.LastWriteTime);
        InstanceMember(nameof(FileSystemInfo.LastWriteTimeUtc)).Invoke(directoryInfo).ShouldBe(directoryInfo.LastWriteTimeUtc);
        InstanceMember(nameof(FileSystemInfo.Name)).Invoke(directoryInfo).ShouldBe("child");
        InstanceMember(nameof(FileSystemInfo.ToString)).Invoke(directoryInfo).ShouldBe(directoryInfo.ToString());
#if NET
        InstanceMember(nameof(FileSystemInfo.LinkTarget)).Invoke(directoryInfo).ShouldBeNull();
#endif
        InstanceMember(nameof(DirectoryInfo.Parent)).Invoke(directoryInfo).ShouldBeOfType<DirectoryInfo>().FullName.ShouldBe(parent.Path);
        InstanceMember(nameof(DirectoryInfo.Root)).Invoke(directoryInfo).ShouldBeOfType<DirectoryInfo>().FullName.ShouldBe(directoryInfo.Root.FullName);
        InstanceMember(nameof(FileInfo.Directory)).Invoke(fileInfo).ShouldBeOfType<DirectoryInfo>().FullName.ShouldBe(child.Path);
        InstanceMember(nameof(FileInfo.DirectoryName)).Invoke(fileInfo).ShouldBe(child.Path);
        InstanceMember(nameof(FileInfo.IsReadOnly)).Invoke(fileInfo).ShouldBe(fileInfo.IsReadOnly);
        InstanceMember(nameof(FileInfo.Length)).Invoke(fileInfo).ShouldBe(7L);
    }
}

public class WellKnownFunctions_File_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(File), output)
{
    [Fact]
    public void File_StaticMembers()
    {
        using TestEnvironment env = TestEnvironment.Create(Output);
        var folder = env.CreateFolder();
        var file = env.CreateFile(folder, "data.txt", "content");

        StaticMember(nameof(File.Exists)).Invoke([file.Path]).ShouldBe(true);
        StaticMember(nameof(File.GetAttributes)).Invoke([file.Path]).ShouldBe(File.GetAttributes(file.Path));
        StaticMember(nameof(File.GetCreationTime)).Invoke([file.Path]).ShouldBeOfType<DateTime>();
        StaticMember(nameof(File.GetCreationTimeUtc)).Invoke([file.Path]).ShouldBeOfType<DateTime>();
        StaticMember(nameof(File.GetLastAccessTime)).Invoke([file.Path]).ShouldBeOfType<DateTime>();
        StaticMember(nameof(File.GetLastWriteTime)).Invoke([file.Path]).ShouldBeOfType<DateTime>();
        StaticMember(nameof(File.GetLastWriteTimeUtc)).Invoke([file.Path]).ShouldBeOfType<DateTime>();
        StaticMember(nameof(File.ReadAllBytes)).Invoke([file.Path]).ShouldBeOfType<byte[]>().ShouldBe(File.ReadAllBytes(file.Path));
        StaticMember(nameof(File.ReadAllText)).Invoke([file.Path]).ShouldBe("content");
    }
}
