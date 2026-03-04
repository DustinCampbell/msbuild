// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#if NETFRAMEWORK
using Path = Microsoft.IO.Path;
#else
#endif

namespace Microsoft.Build.Framework
{
    // TODO: this should be unified with Shared\FileUtilities, but it is hard to untangle everything in one go.
    // Moved some of the methods here for now.

    /// <summary>
    /// This class contains utility methods for file IO.
    /// Functions from FileUtilities are transferred here as part of the effort to remove Shared files.
    /// </summary>
    internal static partial class FrameworkFileUtilities
    {
        private const int DefaultRetryCount = 2;
        private const int DefaultRetryTimeOut = 500;

        private const char UnixDirectorySeparator = '/';
        private const char WindowsDirectorySeparator = '\\';

        internal static readonly char[] Slashes = [UnixDirectorySeparator, WindowsDirectorySeparator];

        // ISO 8601 Universal time with sortable format
        public const string FileTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

        public static readonly bool IsFileSystemCaseSensitive = GetIsFileSystemCaseSensitive();

        public static readonly StringComparison PathComparison = IsFileSystemCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        public static readonly StringComparer PathComparer = IsFileSystemCaseSensitive
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// Determines whether the file system is case sensitive.
        /// Copied from https://github.com/dotnet/runtime/blob/73ba11f3015216b39cb866d9fb7d3d25e93489f2/src/libraries/Common/src/System/IO/PathInternal.CaseSensitivity.cs#L41-L59
        /// </summary>
        private static bool GetIsFileSystemCaseSensitive()
        {
            try
            {
                string pathWithUpperCase = Path.Combine(Path.GetTempPath(), $"CASESENSITIVETEST{Guid.NewGuid():N}");
                using (new FileStream(pathWithUpperCase, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 0x1000, FileOptions.DeleteOnClose))
                {
                    string lowerCased = pathWithUpperCase.ToLowerInvariant();
                    return !FileSystems.Default.FileExists(lowerCased);
                }
            }
            catch (Exception ex)
            {
                // In case something goes terribly wrong, we don't want to fail just because
                // of a casing test, so we assume case-insensitive-but-preserving.
                Debug.Fail($"Casing test failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// AsyncLocal working directory for use during property/item expansion in multithreaded mode.
        /// Set by MultiThreadedTaskEnvironmentDriver when building projects. null in multi-process mode.
        /// Using AsyncLocal ensures the value flows to child threads/tasks spawned during execution of tasks.
        /// </summary>
        private static readonly AsyncLocal<string?> s_currentThreadWorkingDirectory = new();

        internal static string? CurrentThreadWorkingDirectory
        {
            get => s_currentThreadWorkingDirectory.Value;
            set => s_currentThreadWorkingDirectory.Value = value;
        }

        /// <summary>
        /// The directory where MSBuild stores cache information used during the build.
        /// </summary>
        private static string? s_cacheDirectory = null;

        /// <summary>
        /// FOR UNIT TESTS ONLY
        /// Clear out the static variable used for the cache directory so that tests that
        /// modify it can validate their modifications.
        /// </summary>
        public static void ClearCacheDirectoryPath()
        {
            s_cacheDirectory = null;
        }

        /// <summary>
        /// Retrieves the MSBuild runtime cache directory.
        /// </summary>
        public static string GetCacheDirectory()
            => s_cacheDirectory ??= Path.Combine(TempFileDirectory, string.Format(CultureInfo.CurrentUICulture, "MSBuild{0}-{1}", EnvironmentUtilities.CurrentProcessId, AppDomain.CurrentDomain.Id));

        /// <summary>
        /// Clears the MSBuild runtime cache.
        /// </summary>
        internal static void ClearCacheDirectory()
        {
            string cacheDirectory = GetCacheDirectory();

            if (FileSystems.Default.DirectoryExists(cacheDirectory))
            {
                DeleteDirectoryNoThrow(cacheDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Indicates if the given character is a slash in current OS.
        /// </summary>
        /// <param name="c"></param>
        /// <returns>true, if slash</returns>
        internal static bool IsSlash(char c)
        {
            return (c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Indicates if the given file-spec ends with a slash.
        /// </summary>
        /// <param name="fileSpec">The file spec.</param>
        /// <returns>true, if file-spec has trailing slash</returns>
        internal static bool EndsWithSlash(string fileSpec)
        {
            return (fileSpec.Length > 0)
                ? IsSlash(fileSpec[fileSpec.Length - 1])
                : false;
        }

        /// <summary>
        /// Fixes backslashes to forward slashes on Unix. This allows to recognise windows style paths on Unix. 
        /// However, this leads to incorrect path on Linux if backslash was part of the file/directory name.
        /// </summary>  
        internal static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == WindowsDirectorySeparator ? path : path.Replace(WindowsDirectorySeparator, UnixDirectorySeparator);
        }

        /// <summary>
        /// If the given path doesn't have a trailing slash then add one.
        /// If the path is an empty string, does not modify it.
        /// </summary>
        /// <param name="fileSpec">The path to check.</param>
        /// <returns>A path with a slash.</returns>
        internal static string EnsureTrailingSlash(string fileSpec)
        {
            fileSpec = FixFilePath(fileSpec);
            if (fileSpec.Length > 0 && !IsSlash(fileSpec[fileSpec.Length - 1]))
            {
                fileSpec += Path.DirectorySeparatorChar;
            }

            return fileSpec;
        }

        /// <summary>
        /// Ensures the path does not have a trailing slash.
        /// </summary>
        internal static string EnsureNoTrailingSlash(string path)
        {
            path = FixFilePath(path);
            if (path.Length > 0 && IsSlash(path[path.Length - 1]))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }

        /// <summary>
        /// Gets the canonicalized full path of the provided path.
        /// Guidance for use: call this on all paths accepted through public entry
        /// points that need normalization. After that point, only verify the path
        /// is rooted, using ErrorUtilities.VerifyThrowPathRooted.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        public static string NormalizePath(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            string fullPath = GetFullPath(path);
            return FixFilePath(fullPath);
        }

        public static string NormalizePath(string directory, string file)
            => NormalizePath(Path.Combine(directory, file));

        public static string NormalizePath(params string[] paths)
            => NormalizePath(Path.Combine(paths));

        /// <summary>
        /// Determines the full path for the given file-spec.
        /// ASSUMES INPUT IS STILL ESCAPED
        /// </summary>
        /// <param name="fileSpec">The file spec to get the full path of.</param>
        /// <param name="currentDirectory"></param>
        /// <param name="escape">Whether to escape the path after getting the full path.</param>
        /// <returns>Full path to the file, escaped if not specified otherwise.</returns>
        public static string GetFullPath(string fileSpec, string currentDirectory, bool escape = true)
        {
            // Sending data out of the engine into the filesystem, so time to unescape.
            fileSpec = FixFilePath(EscapingUtilities.UnescapeAll(fileSpec));

            string fullPath = NormalizePath(Path.Combine(currentDirectory, fileSpec));
            // In some cases we might want to NOT escape in order to preserve symbols like @, %, $ etc.
            if (escape)
            {
                // Data coming back from the filesystem into the engine, so time to escape it back.
                fullPath = EscapingUtilities.Escape(fullPath);
            }

            if (NativeMethods.IsWindows && !EndsWithSlash(fullPath))
            {
                if (FileUtilitiesRegex.IsDrivePattern(fileSpec) ||
                    FileUtilitiesRegex.IsUncPattern(fullPath))
                {
                    // append trailing slash if Path.GetFullPath failed to (this happens with drive-specs and UNC shares)
                    fullPath += Path.DirectorySeparatorChar;
                }
            }

            return fullPath;
        }

        public static string GetFullPath(string path)
        {
#if FEATURE_LEGACY_GETFULLPATH
            if (NativeMethods.IsWindows)
            {
                string uncheckedFullPath = NativeMethods.GetFullPath(path);

                if (IsPathTooLong(uncheckedFullPath))
                {
                    throw new PathTooLongException(SR.FormatPathTooLong(path, NativeMethods.MaxPath));
                }

                // We really don't care about extensions here, but System.IO.Path.HasExtension
                // provides a great way to invoke the CLR's invalid path checks (these are independent of path length)
                System.IO.Path.HasExtension(uncheckedFullPath);

                // If we detect we are a UNC path then we need to use the regular get full path
                // in order to do the correct checks for UNC formatting and security checks for
                // strings like \\?\GlobalRoot
                return IsUNCPath(uncheckedFullPath)
                    ? System.IO.Path.GetFullPath(uncheckedFullPath)
                    : uncheckedFullPath;
            }
#endif
            return Path.GetFullPath(path);
        }

#if FEATURE_LEGACY_GETFULLPATH
        private static bool IsUNCPath(string path)
        {
            if (!NativeMethods.IsWindows || !path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return false;
            }
            bool isUNC = true;
            for (int i = 2; i < path.Length - 1; i++)
            {
                if (path[i] == '\\')
                {
                    isUNC = false;
                    break;
                }
            }

            /*
              From Path.cs in the CLR

              Throw an ArgumentException for paths like \\, \\server, \\server\
              This check can only be properly done after normalizing, so
              \\foo\.. will be properly rejected.  Also, reject \\?\GLOBALROOT\
              (an internal kernel path) because it provides aliases for drives.

              throw new ArgumentException(Environment.GetResourceString("Arg_PathIllegalUNC"));

               // Check for \\?\Globalroot, an internal mechanism to the kernel
               // that provides aliases for drives and other undocumented stuff.
               // The kernel team won't even describe the full set of what
               // is available here - we don't want managed apps mucking
               // with this for security reasons.
            */
            return isUNC || path.IndexOf(@"\\?\globalroot", StringComparison.OrdinalIgnoreCase) != -1;
        }
#endif // FEATURE_LEGACY_GETFULLPATH

        /// <summary>
        /// A variation of Path.GetFullPath that will return the input value
        /// instead of throwing any IO exception.
        /// Useful to get a better path for an error message, without the risk of throwing
        /// if the error message was itself caused by the path being invalid!
        /// </summary>
        public static string GetFullPathNoThrow(string path)
        {
            try
            {
                path = NormalizePath(path);
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
            }

            return path;
        }

        /// <summary>
        /// Extracts the directory from the given file-spec.
        /// </summary>
        /// <param name="fileSpec">The filespec.</param>
        /// <returns>directory path</returns>
        public static string GetDirectory(string fileSpec)
        {
            string? directory = Path.GetDirectoryName(FixFilePath(fileSpec));

            // if file-spec is a root directory e.g. c:, c:\, \, \\server\share
            // NOTE: Path.GetDirectoryName also treats invalid UNC file-specs as root directories e.g. \\, \\server
            if (directory == null)
            {
                // just use the file-spec as-is
                directory = fileSpec;
            }
            else if ((directory.Length > 0) && !EndsWithSlash(directory))
            {
                // restore trailing slash if Path.GetDirectoryName has removed it (this happens with non-root directories)
                directory += Path.DirectorySeparatorChar;
            }

            return directory;
        }

        public static bool IsPathTooLong(string path)
            => path.Length >= NativeMethods.MaxPath; // >= not > because MAX_PATH assumes a trailing null

        /// <summary>
        /// Checks if the path contains backslashes on Unix.
        /// </summary>
        private static bool HasWindowsDirectorySeparatorOnUnix(string path)
            => NativeMethods.IsUnixLike && path.IndexOf(WindowsDirectorySeparator) >= 0;

        /// <summary>
        /// Checks if the path contains forward slashes on Windows.
        /// </summary>
        private static bool HasUnixDirectorySeparatorOnWindows(string path)
            => NativeMethods.IsWindows && path.IndexOf(UnixDirectorySeparator) >= 0;

        /// <summary>
        /// Quickly checks if the path may contain relative segments like "." or "..".
        /// This is a non-precise detection that may have false positives but no false negatives.
        /// </summary>
        /// <remarks>
        /// Check for relative path segments "." and ".."
        /// In absolute path those segments can not appear in the beginning of the path, only after a path separator.
        /// This is not a precise full detection of relative segments. There are no false negatives as this might affect correctness, but it may have false positives:
        /// like when there is a hidden file or directory starting with a dot, or on linux the backslash and dot can be part of the file name.
        /// </remarks>
        private static bool MayHaveRelativeSegment(string path)
            => path.Contains("/.") || path.Contains("\\.");

        /// <summary>
        /// If the given path doesn't have a trailing slash then add one.
        /// </summary>
        /// <param name="path">The absolute path to check.</param>
        /// <returns>An absolute path with a trailing slash.</returns>
        /// <remarks>
        /// If the path does not require modification, returns the current instance to avoid unnecessary allocations.
        /// Preserves the OriginalValue of the current instance.
        /// </remarks>
        internal static AbsolutePath EnsureTrailingSlash(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                return path;
            }

            // Check if the path already has a trailing slash and no separator fixing is needed on Unix.
            // EnsureTrailingSlash should also fix the path separators on Unix.
            if (IsSlash(path.Value[path.Value.Length - 1]) && !HasWindowsDirectorySeparatorOnUnix(path.Value))
            {
                return path;
            }

            return new AbsolutePath(EnsureTrailingSlash(path.Value),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Ensures the absolute path does not have a trailing slash.
        /// </summary>
        /// <param name="path">The absolute path to check.</param>
        /// <returns>An absolute path without a trailing slash.</returns>
        /// <remarks>
        /// If the path does not require modification, returns the current instance to avoid unnecessary allocations.
        /// Preserves the OriginalValue of the current instance.
        /// </remarks>
        internal static AbsolutePath EnsureNoTrailingSlash(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                return path;
            }

            // Check if already has no trailing slash and no separator fixing needed on unix 
            // (EnsureNoTrailingSlash also should fix the paths on unix). 
            if (!IsSlash(path.Value[path.Value.Length - 1]) && !HasWindowsDirectorySeparatorOnUnix(path.Value))
            {
                return path;
            }

            return new AbsolutePath(EnsureNoTrailingSlash(path.Value),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Gets the canonicalized full path of the provided path.
        /// Resolves relative segments like "." and "..". Fixes directory separators.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        /// <remarks>
        /// If the path does not require modification, returns the current instance to avoid unnecessary allocations.
        /// Preserves the OriginalValue of the current instance.
        /// </remarks>
        internal static AbsolutePath NormalizePath(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                return path;
            }

            if (!MayHaveRelativeSegment(path.Value) &&
                !HasWindowsDirectorySeparatorOnUnix(path.Value) &&
                !HasUnixDirectorySeparatorOnWindows(path.Value))
            {
                return path;
            }

            return new AbsolutePath(FixFilePath(Path.GetFullPath(path.Value)),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Fixes file path separators for the current platform.
        /// </summary>
        internal static AbsolutePath FixFilePath(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value) || !HasWindowsDirectorySeparatorOnUnix(path.Value))
            {
                return path;
            }

            return new AbsolutePath(FixFilePath(path.Value),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Gets a file info object for the specified file path. If the file path
        /// is invalid, or is a directory, or cannot be accessed, or does not exist,
        /// it returns null rather than throwing or returning a FileInfo around a non-existent file.
        /// This allows it to be called where File.Exists() (which never throws, and returns false
        /// for directories) was called - but with the advantage that a FileInfo object is returned
        /// that can be queried (e.g., for LastWriteTime) without hitting the disk again.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>FileInfo around path if it is an existing /file/, else null</returns>
        public static FileInfo? GetFileInfoNoThrow(string filePath)
        {
            filePath = AttemptToShortenPath(filePath);

            FileInfo fileInfo;

            try
            {
                fileInfo = new FileInfo(filePath);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Invalid or inaccessible path: treat as if nonexistent file, just as File.Exists does
                return null;
            }

            if (fileInfo.Exists)
            {
                // It's an existing file
                return fileInfo;
            }
            else
            {
                // Nonexistent, or existing but a directory, just as File.Exists behaves
                return null;
            }
        }

        /// <summary>
        /// Normalizes the path if and only if it is longer than max path,
        /// or would be if rooted by the current directory.
        /// This may make it shorter by removing ".."'s.
        /// </summary>
        internal static string AttemptToShortenPath(string path)
        {
            if (IsPathTooLong(path) || IsPathTooLongIfRooted(path))
            {
                // Attempt to make it shorter -- perhaps there are some \..\ elements
                path = GetFullPathNoThrow(path);
            }

            return FixFilePath(path);
        }

        private static bool IsPathTooLongIfRooted(string path)
        {
            bool hasMaxPath = NativeMethods.HasMaxPath;
            int maxPath = NativeMethods.MaxPath;
            // >= not > because MAX_PATH assumes a trailing null
            return hasMaxPath && !IsRootedNoThrow(path) && NativeMethods.GetCurrentDirectory().Length + path.Length + 1 /* slash */ >= maxPath;
        }

        /// <summary>
        /// A variation of Path.IsRooted that not throw any IO exception.
        /// </summary>
        private static bool IsRootedNoThrow(string path)
        {
            try
            {
                return Path.IsPathRooted(FixFilePath(path));
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                return false;
            }
        }

        /// <summary>
        /// A variation on Directory.Delete that will throw ExceptionHandling.NotExpectedException exceptions.
        /// </summary>
        public static void DeleteDirectoryNoThrow(
            string path,
            bool recursive = false,
            int retryCount = DefaultRetryCount,
            int retryTimeOut = DefaultRetryTimeOut)
        {
            string? environmentRetryCount = Environment.GetEnvironmentVariable("MSBUILDDIRECTORYDELETERETRYCOUNT");

            if (environmentRetryCount is not null && !int.TryParse(environmentRetryCount, out retryCount))
            {
                retryCount = DefaultRetryCount;
            }

            string? environmentRetryTimeOut = Environment.GetEnvironmentVariable("MSBUILDDIRECTORYDELETRETRYTIMEOUT");

            if (environmentRetryTimeOut is not null && !int.TryParse(environmentRetryTimeOut, out retryTimeOut))
            {
                retryTimeOut = DefaultRetryTimeOut;
            }

            path = FixFilePath(path);

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    if (FileSystems.Default.DirectoryExists(path))
                    {
                        Directory.Delete(path, recursive);
                        break;
                    }
                }
                catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                {
                }

                if (i < retryCount - 1)
                {
                    Thread.Sleep(retryTimeOut);
                }
            }
        }

        /// <summary>
        /// Deletes all subdirectories within the specified directory without throwing exceptions.
        /// This method enumerates all subdirectories in the given directory and attempts to delete
        /// each one recursively. If any IO-related exceptions occur during enumeration or deletion,
        /// they are silently ignored.
        /// </summary>
        /// <param name="directory">The directory whose subdirectories should be deleted.</param>
        /// <remarks>
        /// This method is useful for cleanup operations where partial failure is acceptable.
        /// It will not delete the root directory itself, only its subdirectories.
        /// IO exceptions during directory enumeration or deletion are caught and ignored.
        /// </remarks>
        internal static void DeleteSubdirectoriesNoThrow(string directory)
        {
            try
            {
                foreach (string dir in FileSystems.Default.EnumerateDirectories(directory))
                {
                    DeleteDirectoryNoThrow(dir, recursive: true, retryCount: 1);
                }
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                // If we can't enumerate the directories, ignore. Other cases should be handled by DeleteDirectoryNoThrow.
            }
        }
    }
}
