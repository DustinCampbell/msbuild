// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET || FEATURE_MSIOREDIST
#define FEATURE_PATH_SPANS
#endif

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;
#if FEATURE_MSIOREDIST
using PathOperations = Microsoft.IO.Path;
#else
using PathOperations = System.IO.Path;
#endif

namespace Microsoft.Build.Evaluation.Expander;

internal partial class WellKnownFunctions
{
#if NETFRAMEWORK
    private static readonly char[] s_invalidPathChars = System.IO.Path.GetInvalidPathChars();
#endif

    private enum PathFunction : byte
    {
        None,
        AltDirectorySeparatorChar,
        ChangeExtension,
        Combine,
        DirectorySeparatorChar,
        GetDirectoryName,
        GetExtension,
        GetFileName,
        GetFileNameWithoutExtension,
        GetFullPath,
        GetPathRoot,
        GetRandomFileName,
        GetTempFileName,
        GetTempPath,
        HasExtension,
        IsPathRooted,
        Join,
        PathSeparator,
        VolumeSeparatorChar,
    }

    internal static bool TryExecutePathFunction(
        StringSegment methodName,
        out object? result,
        ref FunctionArguments args)
    {
        switch (GetPathFunction(methodName))
        {
            case PathFunction.Combine:
                return TryExecutePathCombine(ref args, out result);

            case PathFunction.Join:
                return TryExecutePathJoin(ref args, out result);

            case PathFunction.DirectorySeparatorChar when args.Length == 0:
                result = PathOperations.DirectorySeparatorChar;
                return true;

            case PathFunction.AltDirectorySeparatorChar when args.Length == 0:
                result = PathOperations.AltDirectorySeparatorChar;
                return true;

            case PathFunction.PathSeparator when args.Length == 0:
                result = PathOperations.PathSeparator;
                return true;

            case PathFunction.VolumeSeparatorChar when args.Length == 0:
                result = PathOperations.VolumeSeparatorChar;
                return true;

            case PathFunction.GetTempPath when args.Length == 0:
                result = PathOperations.GetTempPath();
                return true;

            case PathFunction.GetTempFileName when args.Length == 0:
                result = PathOperations.GetTempFileName();
                return true;

            case PathFunction.GetRandomFileName when args.Length == 0:
                result = PathOperations.GetRandomFileName();
                return true;

            case PathFunction.GetFullPath:
                if (args.Length == 1 && TryGetPathArgument(ref args, 0, out StringSegment fullPath))
                {
                    string? currentDirectory = FileUtilities.CurrentThreadWorkingDirectory;
                    string path = fullPath.ValueOrEmpty;
                    result = currentDirectory is not null && currentDirectory.Length > 0
                        ? PathOperations.GetFullPath(PathOperations.Combine(currentDirectory, path))
                        : PathOperations.GetFullPath(path);
                    return true;
                }

                break;

            case PathFunction.IsPathRooted:
                if (args.Length == 1 && TryGetPathArgument(ref args, 0, out StringSegment rootedPath))
                {
                    result = IsPathRooted(rootedPath);
                    return true;
                }

                break;

            case PathFunction.GetFileName:
                if (args.Length == 1 && TryGetPathArgument(ref args, 0, out StringSegment fileNamePath))
                {
                    result = GetFileName(fileNamePath);
                    return true;
                }

                break;

            case PathFunction.GetDirectoryName:
                if (args.Length == 1 && TryGetPathArgument(ref args, 0, out StringSegment directoryNamePath))
                {
                    result = GetDirectoryName(directoryNamePath);
                    return true;
                }

                break;

            case PathFunction.GetFileNameWithoutExtension:
                if (args.Length == 1 && TryGetPathArgument(ref args, 0, out StringSegment fileNameWithoutExtensionPath))
                {
                    result = PathOperations.GetFileNameWithoutExtension(fileNameWithoutExtensionPath.ValueOrEmpty);
                    return true;
                }

                break;

            case PathFunction.GetExtension:
                if (args.Length == 1 && TryGetPathArgument(ref args, 0, out StringSegment extensionPath))
                {
                    result = GetExtension(extensionPath);
                    return true;
                }

                break;

            case PathFunction.ChangeExtension:
                if (args.Length == 2 &&
                    TryGetPathArgument(ref args, 0, out StringSegment changeExtensionPath) &&
                    args.TryGetSegment(1, out StringSegment extension))
                {
                    result = PathOperations.ChangeExtension(changeExtensionPath.ValueOrEmpty, extension.Value);
                    return true;
                }

                break;

            case PathFunction.GetPathRoot:
                if (args.Length == 1 && TryGetPathArgument(ref args, 0, out StringSegment rootPath))
                {
                    result = GetPathRoot(rootPath);
                    return true;
                }

                break;

            case PathFunction.HasExtension:
                if (args.Length == 1 && TryGetPathArgument(ref args, 0, out StringSegment hasExtensionPath))
                {
                    result = HasExtension(hasExtensionPath);
                    return true;
                }

                break;
        }

        result = null;
        return false;
    }

    private static bool TryExecutePathCombine(ref FunctionArguments args, out object? result)
    {
        StringSegment path1;
        StringSegment path2;
        StringSegment path3;
        StringSegment path4;

        switch (args.Length)
        {
            case 1 when TryGetPathArgument(ref args, 0, out StringSegment path):
                result = path.ValueOrEmpty;
                return true;

            case 2 when TryGetPathArguments(ref args, out path1, out path2):
                result = Combine(path1, path2);
                return true;

            case 3 when TryGetPathArguments(ref args, out path1, out path2, out path3):
                result = Combine(path1, path2, path3);
                return true;

            case 4 when TryGetPathArguments(ref args, out path1, out path2, out path3, out path4):
                result = Combine(path1, path2, path3, path4);
                return true;

            default:
                if (args.Length > 4 && TryGetPathArguments(ref args, out string[]? paths))
                {
                    result = PathOperations.Combine(paths);
                    return true;
                }

                break;
        }

        result = null;
        return false;
    }

    private static bool TryExecutePathJoin(ref FunctionArguments args, out object? result)
    {
#if FEATURE_PATH_SPANS
        StringSegment path1;
        StringSegment path2;
        StringSegment path3;
        StringSegment path4;

        switch (args.Length)
        {
            case 2 when TryGetPathArguments(ref args, out path1, out path2):
                result = PathOperations.Join(path1.AsSpan(), path2.AsSpan());
                return true;

            case 3 when TryGetPathArguments(ref args, out path1, out path2, out path3):
                result = PathOperations.Join(path1.AsSpan(), path2.AsSpan(), path3.AsSpan());
                return true;

            case 4 when TryGetPathArguments(ref args, out path1, out path2, out path3, out path4):
                result = PathOperations.Join(path1.AsSpan(), path2.AsSpan(), path3.AsSpan(), path4.AsSpan());
                return true;
        }
#endif

        result = null;
        return false;
    }

    private static bool TryGetPathArgument(ref FunctionArguments args, int index, out StringSegment path)
    {
        if (args.TryGetSegment(index, out path))
        {
            path = FileUtilities.FixFilePath(path);
#if NETFRAMEWORK
            if (path.IndexOfAny(s_invalidPathChars) >= 0)
            {
                return false;
            }
#endif
            return true;
        }

        return false;
    }

    private static bool TryGetPathArguments(
        ref FunctionArguments args,
        out StringSegment path1,
        out StringSegment path2)
    {
        path1 = default;
        path2 = default;
        return TryGetPathArgument(ref args, 0, out path1) && TryGetPathArgument(ref args, 1, out path2);
    }

    private static bool TryGetPathArguments(
        ref FunctionArguments args,
        out StringSegment path1,
        out StringSegment path2,
        out StringSegment path3)
    {
        path1 = default;
        path2 = default;
        path3 = default;
        return TryGetPathArgument(ref args, 0, out path1) &&
               TryGetPathArgument(ref args, 1, out path2) &&
               TryGetPathArgument(ref args, 2, out path3);
    }

    private static bool TryGetPathArguments(
        ref FunctionArguments args,
        out StringSegment path1,
        out StringSegment path2,
        out StringSegment path3,
        out StringSegment path4)
    {
        path1 = default;
        path2 = default;
        path3 = default;
        path4 = default;
        return TryGetPathArgument(ref args, 0, out path1) &&
               TryGetPathArgument(ref args, 1, out path2) &&
               TryGetPathArgument(ref args, 2, out path3) &&
               TryGetPathArgument(ref args, 3, out path4);
    }

    private static bool TryGetPathArguments(
        ref FunctionArguments args,
        [NotNullWhen(true)] out string[]? paths)
    {
        paths = new string[args.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            if (!TryGetPathArgument(ref args, i, out StringSegment path))
            {
                paths = null;
                return false;
            }

            paths[i] = path.ValueOrEmpty;
        }

        return true;
    }

    private static string Combine(StringSegment path1, StringSegment path2)
    {
#if FEATURE_PATH_SPANS
        return IsPathRooted(path2)
            ? path2.ValueOrEmpty
            : PathOperations.Join(path1.AsSpan(), path2.AsSpan());
#else
        return PathOperations.Combine(path1.ValueOrEmpty, path2.ValueOrEmpty);
#endif
    }

    private static string Combine(StringSegment path1, StringSegment path2, StringSegment path3)
    {
#if FEATURE_PATH_SPANS
        if (IsPathRooted(path3))
        {
            return path3.ValueOrEmpty;
        }

        return IsPathRooted(path2)
            ? PathOperations.Join(path2.AsSpan(), path3.AsSpan())
            : PathOperations.Join(path1.AsSpan(), path2.AsSpan(), path3.AsSpan());
#else
        return PathOperations.Combine(path1.ValueOrEmpty, path2.ValueOrEmpty, path3.ValueOrEmpty);
#endif
    }

    private static string Combine(
        StringSegment path1,
        StringSegment path2,
        StringSegment path3,
        StringSegment path4)
    {
#if FEATURE_PATH_SPANS
        if (IsPathRooted(path4))
        {
            return path4.ValueOrEmpty;
        }

        if (IsPathRooted(path3))
        {
            return PathOperations.Join(path3.AsSpan(), path4.AsSpan());
        }

        return IsPathRooted(path2)
            ? PathOperations.Join(path2.AsSpan(), path3.AsSpan(), path4.AsSpan())
            : PathOperations.Join(path1.AsSpan(), path2.AsSpan(), path3.AsSpan(), path4.AsSpan());
#else
        return PathOperations.Combine(path1.ValueOrEmpty, path2.ValueOrEmpty, path3.ValueOrEmpty, path4.ValueOrEmpty);
#endif
    }

    private static bool IsPathRooted(StringSegment path)
#if FEATURE_PATH_SPANS
        => PathOperations.IsPathRooted(path.AsSpan());
#else
        => PathOperations.IsPathRooted(path.ValueOrEmpty);
#endif

    private static string GetFileName(StringSegment path)
#if FEATURE_PATH_SPANS
        => PathOperations.GetFileName(path.AsSpan()).ToString();
#else
        => PathOperations.GetFileName(path.ValueOrEmpty);
#endif

    private static string? GetDirectoryName(StringSegment path)
    {
#if FEATURE_PATH_SPANS
        ReadOnlySpan<char> directory = PathOperations.GetDirectoryName(path.AsSpan());
        return directory.IsEmpty ? PathOperations.GetDirectoryName(path.ValueOrEmpty) : directory.ToString();
#else
        return PathOperations.GetDirectoryName(path.ValueOrEmpty);
#endif
    }

    private static string GetExtension(StringSegment path)
#if FEATURE_PATH_SPANS
        => PathOperations.GetExtension(path.AsSpan()).ToString();
#else
        => PathOperations.GetExtension(path.ValueOrEmpty);
#endif

    private static string? GetPathRoot(StringSegment path)
    {
#if FEATURE_PATH_SPANS
        ReadOnlySpan<char> root = PathOperations.GetPathRoot(path.AsSpan());
        return root.IsEmpty ? PathOperations.GetPathRoot(path.ValueOrEmpty) : root.ToString();
#else
        return PathOperations.GetPathRoot(path.ValueOrEmpty);
#endif
    }

    private static bool HasExtension(StringSegment path)
#if FEATURE_PATH_SPANS
        => PathOperations.HasExtension(path.AsSpan());
#else
        => PathOperations.HasExtension(path.ValueOrEmpty);
#endif

    private static PathFunction GetPathFunction(StringSegment name)
    {
        switch (name.Length)
        {
            case 4 when name.Equals("Join", StringComparison.OrdinalIgnoreCase):
                return PathFunction.Join;
            case 7 when name.Equals(nameof(PathOperations.Combine), StringComparison.OrdinalIgnoreCase):
                return PathFunction.Combine;
            case 11:
                if (name.Equals(nameof(PathOperations.GetFileName), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.GetFileName;
                }

                if (name.Equals(nameof(PathOperations.GetFullPath), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.GetFullPath;
                }

                if (name.Equals(nameof(PathOperations.GetPathRoot), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.GetPathRoot;
                }

                if (name.Equals(nameof(PathOperations.GetTempPath), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.GetTempPath;
                }

                break;
            case 12:
                if (name.Equals(nameof(PathOperations.GetExtension), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.GetExtension;
                }

                if (name.Equals(nameof(PathOperations.HasExtension), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.HasExtension;
                }

                if (name.Equals(nameof(PathOperations.IsPathRooted), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.IsPathRooted;
                }

                break;
            case 13 when name.Equals(nameof(PathOperations.PathSeparator), StringComparison.OrdinalIgnoreCase):
                return PathFunction.PathSeparator;
            case 15:
                if (name.Equals(nameof(PathOperations.ChangeExtension), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.ChangeExtension;
                }

                if (name.Equals(nameof(PathOperations.GetTempFileName), StringComparison.OrdinalIgnoreCase))
                {
                    return PathFunction.GetTempFileName;
                }

                break;
            case 16 when name.Equals(nameof(PathOperations.GetDirectoryName), StringComparison.OrdinalIgnoreCase):
                return PathFunction.GetDirectoryName;
            case 17 when name.Equals(nameof(PathOperations.GetRandomFileName), StringComparison.OrdinalIgnoreCase):
                return PathFunction.GetRandomFileName;
            case 19 when name.Equals(nameof(PathOperations.VolumeSeparatorChar), StringComparison.OrdinalIgnoreCase):
                return PathFunction.VolumeSeparatorChar;
            case 22 when name.Equals(nameof(PathOperations.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase):
                return PathFunction.DirectorySeparatorChar;
            case 25 when name.Equals(nameof(PathOperations.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase):
                return PathFunction.AltDirectorySeparatorChar;
            case 27 when name.Equals(nameof(PathOperations.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase):
                return PathFunction.GetFileNameWithoutExtension;
        }

        return PathFunction.None;
    }
}
