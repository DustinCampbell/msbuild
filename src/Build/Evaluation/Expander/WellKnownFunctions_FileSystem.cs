// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class FileSystemHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(Type receiverType, string methodName, object?[] args)
        {
            if (receiverType == typeof(Directory))
            {
                return TryInvokeDirectory(methodName, args);
            }

            if (receiverType == typeof(File))
            {
                return TryInvokeFile(methodName, args);
            }

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, FileSystemInfo fileSystemInfo, object?[] args)
        {
            if (args is not [])
            {
                return NotHandled;
            }

            if (methodName.Equals(nameof(FileSystemInfo.Attributes), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.Attributes);
            }

            if (methodName.Equals(nameof(FileSystemInfo.CreationTime), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.CreationTime);
            }

            if (methodName.Equals(nameof(FileSystemInfo.CreationTimeUtc), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.CreationTimeUtc);
            }

            if (methodName.Equals(nameof(FileSystemInfo.Exists), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.Exists);
            }

            if (methodName.Equals(nameof(FileSystemInfo.Extension), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.Extension);
            }

            if (methodName.Equals(nameof(FileSystemInfo.FullName), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.FullName);
            }

            if (methodName.Equals(nameof(FileSystemInfo.LastAccessTime), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.LastAccessTime);
            }

            if (methodName.Equals(nameof(FileSystemInfo.LastAccessTimeUtc), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.LastAccessTimeUtc);
            }

            if (methodName.Equals(nameof(FileSystemInfo.LastWriteTime), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.LastWriteTime);
            }

            if (methodName.Equals(nameof(FileSystemInfo.LastWriteTimeUtc), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.LastWriteTimeUtc);
            }

            if (methodName.Equals(nameof(FileSystemInfo.Name), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.Name);
            }

#if NET
            if (methodName.Equals(nameof(FileSystemInfo.LinkTarget), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.LinkTarget);
            }
#endif

            if (methodName.Equals(nameof(FileSystemInfo.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(fileSystemInfo.ToString());
            }

            if (fileSystemInfo is DirectoryInfo directory)
            {
                if (methodName.Equals(nameof(DirectoryInfo.Parent), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(directory.Parent);
                }

                if (methodName.Equals(nameof(DirectoryInfo.Root), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(directory.Root);
                }
            }
            else if (fileSystemInfo is FileInfo file)
            {
                if (methodName.Equals(nameof(FileInfo.Directory), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(file.Directory);
                }

                if (methodName.Equals(nameof(FileInfo.DirectoryName), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(file.DirectoryName);
                }

                if (methodName.Equals(nameof(FileInfo.IsReadOnly), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(file.IsReadOnly);
                }

                if (methodName.Equals(nameof(FileInfo.Length), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(file.Length);
                }
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeDirectory(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(Directory.Exists), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Directory.Exists(path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Directory.GetDirectories), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string path] => Invoked(Directory.GetDirectories(path)),
                    [string path, string searchPattern] => Invoked(Directory.GetDirectories(path, searchPattern)),
                    [string path, string searchPattern, var searchOption]
                        when ArgumentParser.TryConvertToEnum(searchOption, out SearchOption convertedSearchOption)
                        => Invoked(Directory.GetDirectories(path, searchPattern, convertedSearchOption)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Directory.GetFiles), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string path] => Invoked(Directory.GetFiles(path)),
                    [string path, string searchPattern] => Invoked(Directory.GetFiles(path, searchPattern)),
                    [string path, string searchPattern, var searchOption]
                        when ArgumentParser.TryConvertToEnum(searchOption, out SearchOption convertedSearchOption)
                        => Invoked(Directory.GetFiles(path, searchPattern, convertedSearchOption)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Directory.GetLastAccessTime), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Directory.GetLastAccessTime(path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Directory.GetLastWriteTime), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Directory.GetLastWriteTime(path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Directory.GetParent), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Directory.GetParent(path)) : NotHandled;
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeFile(string methodName, object?[] args)
        {
            if (args is not [string path])
            {
                return NotHandled;
            }

            if (methodName.Equals(nameof(File.Exists), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.Exists(path));
            }

            if (methodName.Equals(nameof(File.GetAttributes), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.GetAttributes(path));
            }

            if (methodName.Equals(nameof(File.GetCreationTime), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.GetCreationTime(path));
            }

            if (methodName.Equals(nameof(File.GetCreationTimeUtc), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.GetCreationTimeUtc(path));
            }

            if (methodName.Equals(nameof(File.GetLastAccessTime), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.GetLastAccessTime(path));
            }

            if (methodName.Equals(nameof(File.GetLastWriteTime), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.GetLastWriteTime(path));
            }

            if (methodName.Equals(nameof(File.GetLastWriteTimeUtc), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.GetLastWriteTimeUtc(path));
            }

            if (methodName.Equals(nameof(File.ReadAllBytes), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.ReadAllBytes(path));
            }

            if (methodName.Equals(nameof(File.ReadAllText), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(File.ReadAllText(path));
            }

            return NotHandled;
        }
    }
}
