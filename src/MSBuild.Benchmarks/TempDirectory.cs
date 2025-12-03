// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

/// <summary>
///  Helper class for managing temporary directories in benchmarks.
///  Creates a temporary directory on construction and deletes it on disposal.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    private readonly DirectoryInfo _directory;
    private bool _disposed;

    /// <summary>
    ///  Gets the full path to the temporary directory.
    /// </summary>
    public string FullName => _directory.FullName;

    /// <summary>
    ///  Creates a new temporary directory with a random name.
    /// </summary>
    public TempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), path2: $"MSBuildBenchmarks_{Path.GetRandomFileName()}");
        _directory = Directory.CreateDirectory(tempPath);
    }

    /// <summary>
    ///  Deletes the temporary directory and all its contents.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_directory.Exists)
        {
            _directory.Delete(recursive: true);
        }
    }

    private DirectoryInfo GetSubdirectory(string name)
    {
        var path = Path.Combine(_directory.FullName, name);
        var result = new DirectoryInfo(path);

        if (!result.Exists)
        {
            result.Create();
        }

        return result;
    }

    /// <summary>
    /// Writes text to a file within the temporary directory.
    /// </summary>
    /// <param name="folderName">
    ///  The optional subfolder name within the temporary directory. If specified, the folder will be created if it doesn't exist.
    /// </param>
    /// <param name="fileName">The file name or relative path.</param>
    /// <param name="contents">The text contents to write.</param>
    /// <returns>The full path to the created file.</returns>
    public string WriteFile(string? folderName, string fileName, string contents)
    {
        var directory = folderName != null
            ? GetSubdirectory(folderName)
            : _directory;

        var filePath = Path.Combine(directory.FullName, fileName);
        File.WriteAllText(filePath, contents);

        return filePath;
    }

    /// <summary>
    ///  Writes text to a file within the temporary directory.
    /// </summary>
    /// <param name="fileName">The file name or relative path.</param>
    /// <param name="contents">The text contents to write.</param>
    /// <returns>The full path to the created file.</returns>
    public string WriteFile(string fileName, string contents)
    {
        return WriteFile(folderName: null, fileName, contents);
    }
}
