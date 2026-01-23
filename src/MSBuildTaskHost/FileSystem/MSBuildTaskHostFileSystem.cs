// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared.FileSystem;

/// <summary>
/// Legacy implementation for MSBuildTaskHost which is stuck on net20 APIs.
/// </summary>
internal sealed class MSBuildTaskHostFileSystem
{
    public static readonly MSBuildTaskHostFileSystem Instance = new();

    public bool FileOrDirectoryExists(string path)
        => NativeMethods.FileOrDirectoryExists(path);

    public FileAttributes GetAttributes(string path)
        => File.GetAttributes(path);

    public DateTime GetLastWriteTimeUtc(string path)
        => File.GetLastWriteTimeUtc(path);

    public bool DirectoryExists(string path)
       => NativeMethods.DirectoryExists(path);

    public IEnumerable<string> EnumerateDirectories(
        string path,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.GetDirectories(path, searchPattern, searchOption);

    public TextReader ReadFile(string path)
        => new StreamReader(path);

    public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        => new FileStream(path, mode, access, share);

    public string ReadFileAllText(string path)
        => File.ReadAllText(path);

    public byte[] ReadFileAllBytes(string path)
        => File.ReadAllBytes(path);

    public IEnumerable<string> EnumerateFiles(
        string path,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.GetFiles(path, searchPattern, searchOption);

    public IEnumerable<string> EnumerateFileSystemEntries(
        string path,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        ErrorUtilities.VerifyThrow(searchOption == SearchOption.TopDirectoryOnly, $"In net20 {nameof(Directory.GetFileSystemEntries)} does not take a {nameof(SearchOption)} parameter");

        return Directory.GetFileSystemEntries(path, searchPattern);
    }

    public bool FileExists(string path)
        => NativeMethods.FileExists(path);
}
