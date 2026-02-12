// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !TASKHOST
using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Shared;
#endif

#if FEATURE_LEGACY_GETFULLPATH
using Microsoft.Build.Framework.Resources;
#endif

#if NETFRAMEWORK && !TASKHOST
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.Framework
{
    // TODO: this should be unified with Shared\FileUtilities, but it is hard to untangle everything in one go.
    // Moved some of the methods here for now.

    /// <summary>
    /// This class contains utility methods for file IO.
    /// Functions from FileUtilities are transferred here as part of the effort to remove Shared files.
    /// </summary>
    internal static class FrameworkFileUtilities
    {
        // ISO 8601 Universal time with sortable format
        internal const string FileTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

        private const char UnixDirectorySeparator = '/';
        private const char WindowsDirectorySeparator = '\\';

        internal static readonly char[] Slashes = [UnixDirectorySeparator, WindowsDirectorySeparator];

#if !TASKHOST
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
#endif

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
        /// Determines the full path for the given file-spec.
        /// ASSUMES INPUT IS STILL ESCAPED
        /// </summary>
        /// <param name="fileSpec">The file spec to get the full path of.</param>
        /// <param name="currentDirectory"></param>
        /// <param name="escape">Whether to escape the path after getting the full path.</param>
        /// <returns>Full path to the file, escaped if not specified otherwise.</returns>
        internal static string GetFullPath(string fileSpec, string currentDirectory, bool escape = true)
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

        /// <summary>
        /// Gets the canonicalized full path of the provided path.
        /// Guidance for use: call this on all paths accepted through public entry
        /// points that need normalization. After that point, only verify the path
        /// is rooted, using ErrorUtilities.VerifyThrowPathRooted.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        internal static string NormalizePath(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            string fullPath = GetFullPath(path);
            return FixFilePath(fullPath);
        }

        internal static string NormalizePath(string directory, string file)
            => NormalizePath(Path.Combine(directory, file));

        internal static string NormalizePath(params string[] paths)
            => NormalizePath(Path.Combine(paths));

        private static string GetFullPath(string path)
        {
#if FEATURE_LEGACY_GETFULLPATH
            if (NativeMethods.IsWindows)
            {
                string uncheckedFullPath = NativeMethods.GetFullPath(path);

                if (IsPathTooLong(uncheckedFullPath))
                {
                    throw new PathTooLongException(string.Format(SR.Shared_PathTooLong, path, NativeMethods.MaxPath));
                }

                // We really don't care about extensions here, but System.IO.Path.HasExtension provides a great way to
                // invoke the CLR's invalid path checks (these are independent of path length).
                //
                // Note: Microsoft.IO.Path.HasExtension and the modern .NET System.IO.Path.HasExtension do not provide the same path validation.
                _ = System.IO.Path.HasExtension(uncheckedFullPath);

                // If we detect we are a UNC path then we need to use the regular get full path in order to do the correct checks for UNC formatting
                // and security checks for strings like \\?\GlobalRoot
                return IsUNCPath(uncheckedFullPath)
                    ? System.IO.Path.GetFullPath(uncheckedFullPath)
                    : uncheckedFullPath;
            }
#endif
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Extracts the directory from the given file-spec.
        /// </summary>
        /// <param name="fileSpec">The filespec.</param>
        /// <returns>directory path</returns>
        internal static string GetDirectory(string fileSpec)
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

        public static bool IsPathTooLong(string path)
        {
            // >= not > because MAX_PATH assumes a trailing null
            return path.Length >= NativeMethods.MaxPath;
        }

#if !TASKHOST
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
        /// Resolves relative segments like "." and "..". Fixes directory separators on Windows like Path.GetFullPath does.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        internal static AbsolutePath RemoveRelativeSegments(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                return path;
            }

            if (!MayHaveRelativeSegment(path.Value) && !HasUnixDirectorySeparatorOnWindows(path.Value))
            {
                return path;
            }

            return new AbsolutePath(Path.GetFullPath(path.Value),
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
#endif
    }
}
