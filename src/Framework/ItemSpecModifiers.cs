// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Resources;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Shared;

/// <summary>
/// Encapsulates the definitions of the item-spec modifiers a.k.a. reserved item metadata.
/// </summary>
internal static class ItemSpecModifiers
{
    public const string FullPath = "FullPath";
    public const string RootDir = "RootDir";
    public const string Filename = "Filename";
    public const string Extension = "Extension";
    public const string RelativeDir = "RelativeDir";
    public const string Directory = "Directory";
    public const string RecursiveDir = "RecursiveDir";
    public const string Identity = "Identity";
    public const string ModifiedTime = "ModifiedTime";
    public const string CreatedTime = "CreatedTime";
    public const string AccessedTime = "AccessedTime";
    public const string DefiningProjectFullPath = "DefiningProjectFullPath";
    public const string DefiningProjectDirectory = "DefiningProjectDirectory";
    public const string DefiningProjectName = "DefiningProjectName";
    public const string DefiningProjectExtension = "DefiningProjectExtension";

    public static readonly ImmutableArray<string> DefiningProjectModifiers =
    [
        DefiningProjectFullPath,
        DefiningProjectDirectory,
        DefiningProjectName,
        DefiningProjectExtension
    ];

    // These are all the well-known attributes.
    public static readonly ImmutableArray<string> All =
    [
        FullPath,
        RootDir,
        Filename,
        Extension,
        RelativeDir,
        Directory,
        RecursiveDir, // Not derivable.
        Identity,
        ModifiedTime,
        CreatedTime,
        AccessedTime,
        .. DefiningProjectModifiers
    ];

    private static readonly FrozenSet<string> s_definingProjectModifiersSet = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        DefiningProjectModifiers.AsSpan());

    private enum ItemSpecModifierKind
    {
        None,
        FullPath,
        RootDir,
        Filename,
        Extension,
        RelativeDir,
        Directory,
        RecursiveDir,
        Identity,
        ModifiedTime,
        CreatedTime,
        AccessedTime,
        DefiningProjectFullPath,
        DefiningProjectDirectory,
        DefiningProjectName,
        DefiningProjectExtension,
    }

    private static readonly FrozenDictionary<string, ItemSpecModifierKind> s_allModifiersMap = new Dictionary<string, ItemSpecModifierKind>(StringComparer.OrdinalIgnoreCase)
    {
        { FullPath, ItemSpecModifierKind.FullPath },
        { RootDir, ItemSpecModifierKind.RootDir },
        { Filename, ItemSpecModifierKind.Filename },
        { Extension, ItemSpecModifierKind.Extension },
        { RelativeDir, ItemSpecModifierKind.RelativeDir },
        { Directory, ItemSpecModifierKind.Directory },
        { RecursiveDir, ItemSpecModifierKind.RecursiveDir },
        { Identity, ItemSpecModifierKind.Identity },
        { ModifiedTime, ItemSpecModifierKind.ModifiedTime },
        { CreatedTime, ItemSpecModifierKind.CreatedTime },
        { AccessedTime, ItemSpecModifierKind.AccessedTime },
        { DefiningProjectFullPath, ItemSpecModifierKind.DefiningProjectFullPath },
        { DefiningProjectDirectory, ItemSpecModifierKind.DefiningProjectDirectory },
        { DefiningProjectName, ItemSpecModifierKind.DefiningProjectName },
        { DefiningProjectExtension, ItemSpecModifierKind.DefiningProjectExtension },
    }
    .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Indicates whether the given name is reserved as an item-spec modifier.
    /// </summary>
    /// <param name="name">Name to check.</param>
    public static bool IsItemSpecModifier([NotNullWhen(true)] string? name)
        => name != null && s_allModifiersMap.ContainsKey(name);

    /// <summary>
    ///  Indicates whether the given name is reserved as a derivable item-spec modifier.
    ///  Derivable means it can be computed given a file name.
    /// </summary>
    /// <param name="name">Name to check.</param>
    /// <returns>
    ///  <see langword="true"/>, if <paramref name="name"/> is a derivable modifier; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsDerivableItemSpecModifier([NotNullWhen(true)] string? name)
        => IsItemSpecModifier(name) && (name.Length != 12 || name[0] is not 'r' and not 'R'); // 'RecursiveDir'

    /// <summary>
    /// Performs path manipulations on the given item-spec as directed.
    /// Does not cache the result.
    /// </summary>
    public static string GetItemSpecModifier(string? currentDirectory, string itemSpec, string? definingProjectEscaped, string modifier)
    {
        string? dummy = null;
        return GetItemSpecModifier(currentDirectory, itemSpec, definingProjectEscaped, modifier, ref dummy);
    }

    /// <summary>
    /// Performs path manipulations on the given item-spec as directed.
    ///
    /// Supported modifiers:
    ///     %(FullPath)         = full path of item
    ///     %(RootDir)          = root directory of item
    ///     %(Filename)         = item filename without extension
    ///     %(Extension)        = item filename extension
    ///     %(RelativeDir)      = item directory as given in item-spec
    ///     %(Directory)        = full path of item directory relative to root
    ///     %(RecursiveDir)     = portion of item path that matched a recursive wildcard
    ///     %(Identity)         = item-spec as given
    ///     %(ModifiedTime)     = last write time of item
    ///     %(CreatedTime)      = creation time of item
    ///     %(AccessedTime)     = last access time of item
    ///
    /// NOTES:
    /// 1) This method always returns an empty string for the %(RecursiveDir) modifier because it does not have enough
    ///    information to compute it -- only the BuildItem class can compute this modifier.
    /// 2) All but the file time modifiers could be cached, but it's not worth the space. Only full path is cached, as the others are just string manipulations.
    /// </summary>
    /// <remarks>
    /// Methods of the Path class "normalize" slashes and periods. For example:
    /// 1) successive slashes are combined into 1 slash
    /// 2) trailing periods are discarded
    /// 3) forward slashes are changed to back-slashes
    ///
    /// As a result, we cannot rely on any file-spec that has passed through a Path method to remain the same. We will
    /// therefore not bother preserving slashes and periods when file-specs are transformed.
    ///
    /// Never returns null.
    /// </remarks>
    /// <param name="currentDirectory">The root directory for relative item-specs. When called on the Engine thread, this is the project directory. When called as part of building a task, it is null, indicating that the current directory should be used.</param>
    /// <param name="itemSpec">The item-spec to modify.</param>
    /// <param name="definingProjectEscaped">The path to the project that defined this item (may be null).</param>
    /// <param name="modifier">The modifier to apply to the item-spec.</param>
    /// <param name="fullPath">Full path if any was previously computed, to cache.</param>
    /// <returns>The modified item-spec (can be empty string, but will never be null).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the item-spec is not a path.</exception>
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Pre-existing")]
    internal static string GetItemSpecModifier(
        string? currentDirectory,
        string itemSpec,
        string? definingProjectEscaped,
        string modifier,
        ref string? fullPath)
    {
        ArgumentNullException.ThrowIfNull(itemSpec);
        ArgumentNullException.ThrowIfNull(modifier);

        if (s_allModifiersMap.TryGetValue(modifier, out ItemSpecModifierKind modifierKind))
        {
            try
            {
                switch (modifierKind)
                {
                    case ItemSpecModifierKind.FullPath:
                        return ComputeFullPath(currentDirectory, itemSpec, ref fullPath);

                    case ItemSpecModifierKind.RootDir:
                        return ComputeRootDir(currentDirectory, itemSpec, ref fullPath);

                    case ItemSpecModifierKind.Filename:
                        return ComputeFileName(itemSpec);

                    case ItemSpecModifierKind.Extension:
                        return ComputeExtension(itemSpec);

                    case ItemSpecModifierKind.RelativeDir:
                        return ComputeRelativeDir(itemSpec);

                    case ItemSpecModifierKind.Directory:
                        return ComputeDirectory(currentDirectory, itemSpec, ref fullPath);

                    case ItemSpecModifierKind.RecursiveDir:
                        // only the BuildItem class can compute this modifier -- so leave empty
                        return string.Empty;

                    case ItemSpecModifierKind.Identity:
                        return itemSpec;

                    case ItemSpecModifierKind.ModifiedTime:
                        return ComputeModifiedTime(itemSpec);

                    case ItemSpecModifierKind.CreatedTime:
                        return ComputeCreatedTime(itemSpec);

                    case ItemSpecModifierKind.AccessedTime:
                        return ComputeAccessedTime(itemSpec);
                }

                FrameworkErrorUtilities.VerifyThrow(s_definingProjectModifiersSet.Contains(modifier), "Only DefiningProject* item-spec modifiers should remain.");

                if (definingProjectEscaped.IsNullOrEmpty())
                {
                    // We have nothing to work with, but that's sometimes OK -- so just return string.Empty
                    return string.Empty;
                }

                switch (modifierKind)
                {
                    case ItemSpecModifierKind.DefiningProjectDirectory:
                        return ComputeDefiningProjectDirectory(currentDirectory, definingProjectEscaped);

                    case ItemSpecModifierKind.DefiningProjectFullPath:
                        return ComputeFullPath(currentDirectory, definingProjectEscaped);

                    case ItemSpecModifierKind.DefiningProjectName:
                        return ComputeFileName(definingProjectEscaped);

                    case ItemSpecModifierKind.DefiningProjectExtension:
                        return ComputeExtension(definingProjectEscaped);
                }
            }
            catch (Exception e) when (FrameworkExceptionHandling.IsIoRelatedException(e))
            {
                throw new InvalidOperationException(string.Format(SR.Shared_InvalidFilespecForTransform, modifier, itemSpec, e.Message));
            }
        }

        throw new InternalErrorException($"""
            "{modifier}" is not a valid item-spec modifier.
            """);
    }

    private static string ComputeFullPath(string? currentDirectory, string itemSpec, [NotNull] ref string? fullPath)
    {
        if (fullPath != null)
        {
            return fullPath;
        }

        fullPath = ComputeFullPath(currentDirectory, itemSpec);

        return fullPath;
    }

    private static string ComputeFullPath(string? currentDirectory, string itemSpec)
    {
        currentDirectory ??= FrameworkFileUtilities.CurrentThreadWorkingDirectory ?? string.Empty;

        string fullPath = FrameworkFileUtilities.GetFullPath(itemSpec, currentDirectory);

        ThrowForUrl(fullPath, itemSpec, currentDirectory);

        return fullPath;
    }

    private static string ComputeRootDir(string? currentDirectory, string itemSpec, [NotNull] ref string? fullPath)
        => ComputeRootDir(ComputeFullPath(currentDirectory, itemSpec, ref fullPath));

    private static string ComputeRootDir(string fullPath)
    {
        string? rootDir = Path.GetPathRoot(fullPath);
        FrameworkErrorUtilities.VerifyThrow(rootDir != null, "Path.GetPathRoot(...) should not return null for non-null input.");

        if (!FrameworkFileUtilities.EndsWithSlash(rootDir))
        {
            FrameworkErrorUtilities.VerifyThrow(
                FileUtilitiesRegex.StartsWithUncPattern(rootDir),
                "Only UNC shares should be missing trailing slashes.");

            // restore/append trailing slash if Path.GetPathRoot() has either removed it, or failed to add it
            // (this happens with UNC shares)
            rootDir += Path.DirectorySeparatorChar;
        }

        return rootDir;
    }

    private static string ComputeFileName(string itemSpec)
    {
        // if the item-spec is a root directory, it can have no filename
        if (IsRootDirectory(itemSpec))
        {
            // NOTE: this is to prevent Path.GetFileNameWithoutExtension() from treating server and share elements
            // in a UNC file-spec as filenames e.g. \\server, \\server\share
            return string.Empty;
        }

        // Fix path to avoid problem with Path.GetFileNameWithoutExtension when backslashes in itemSpec on Unix
        return Path.GetFileNameWithoutExtension(FrameworkFileUtilities.FixFilePath(itemSpec));
    }

    private static string ComputeExtension(string itemSpec)
    {
        // if the item-spec is a root directory, it can have no extension
        if (IsRootDirectory(itemSpec))
        {
            // NOTE: this is to prevent Path.GetExtension() from treating server and share elements in a UNC
            // file-spec as filenames e.g. \\server.ext, \\server\share.ext
            return string.Empty;
        }

        return Path.GetExtension(itemSpec);
    }

    private static string ComputeRelativeDir(string itemSpec)
        => FrameworkFileUtilities.GetDirectory(itemSpec);

    private static string ComputeDirectory(string? currentDirectory, string itemSpec, [NotNull] ref string? fullPath)
        => ComputeDirectory(ComputeFullPath(currentDirectory, itemSpec, ref fullPath));

    private static string ComputeDirectory(string fullPath)
    {
        string directory = FrameworkFileUtilities.GetDirectory(fullPath);

        if (NativeMethods.IsWindows)
        {
            int length = FileUtilitiesRegex.StartsWithDrivePattern(directory)
                ? 2
                : FileUtilitiesRegex.StartsWithUncPatternMatchLength(directory);

            if (length != -1)
            {
                FrameworkErrorUtilities.VerifyThrow(
                    directory.Length > length && FrameworkFileUtilities.IsSlash(directory[length]),
                    "Root directory must have a trailing slash.");

                directory = directory.Substring(length + 1);
            }

            return directory;
        }

        FrameworkErrorUtilities.VerifyThrow(
            !directory.IsNullOrEmpty() && FrameworkFileUtilities.IsSlash(directory[0]),
            "Expected a full non-windows path rooted at '/'.");

        // A full Unix path is always rooted at
        // `/`, and a root-relative path is the
        // rest of the string.
        return directory.Substring(1);
    }

    private static string ComputeModifiedTime(string itemSpec)
    {
        // About to go out to the filesystem.  This means data is leaving the engine, so we need to unescape it first.
        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

        return FileSystems.Default.FileExists(unescapedItemSpec)
            ? File.GetLastWriteTime(unescapedItemSpec).ToString(FrameworkFileUtilities.FileTimeFormat, provider: null)
            : string.Empty; // File does not exist, or path is a directory
    }

    private static string ComputeCreatedTime(string itemSpec)
    {
        // About to go out to the filesystem.  This means data is leaving the engine, so we need to unescape it first.
        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

        return FileSystems.Default.FileExists(unescapedItemSpec)
            ? File.GetCreationTime(unescapedItemSpec).ToString(FrameworkFileUtilities.FileTimeFormat, provider: null)
            : string.Empty; // File does not exist, or path is a directory
    }

    private static string ComputeAccessedTime(string itemSpec)
    {
        // About to go out to the filesystem.  This means data is leaving the engine, so we need to unescape it first.
        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

        return FileSystems.Default.FileExists(unescapedItemSpec)
            ? File.GetLastAccessTime(unescapedItemSpec).ToString(FrameworkFileUtilities.FileTimeFormat, provider: null)
            : string.Empty; // File does not exist, or path is a directory
    }

    private static string ComputeDefiningProjectDirectory(string? currentDirectory, string definingProjectEscaped)
    {
        string fullPath = ComputeFullPath(currentDirectory, definingProjectEscaped);

        // ItemSpecModifiers.Directory does not contain the root directory, so we must combine them.
        return Path.Combine(ComputeRootDir(fullPath), ComputeDirectory(fullPath));
    }

    /// <summary>
    ///  Indicates whether the given path is a UNC or drive pattern root directory.
    /// </summary>
    /// <remarks>
    ///  Note: This function mimics the behavior of checking if <c>Path.GetDirectoryName(path) == null</c>.
    /// </remarks>
    private static bool IsRootDirectory(string path)
    {
        // Eliminate all non-rooted paths
        if (!Path.IsPathRooted(path))
        {
            return false;
        }

        int uncMatchLength = FileUtilitiesRegex.StartsWithUncPatternMatchLength(path);

        // Determine if the given path is a standard drive/unc pattern root
        if (FileUtilitiesRegex.IsDrivePattern(path) ||
            FileUtilitiesRegex.IsDrivePatternWithSlash(path) ||
            uncMatchLength == path.Length)
        {
            return true;
        }

        // Eliminate all non-root unc paths.
        if (uncMatchLength != -1)
        {
            return false;
        }

        // Eliminate any drive patterns that don't have a slash after the colon or where the 4th character is a non-slash
        // A non-slash at [3] is specifically checked here because Path.GetDirectoryName
        // considers "C:///" a valid root.
        if (FileUtilitiesRegex.StartsWithDrivePattern(path) &&
            ((path.Length >= 3 && path[2] is not ('\\' or '/')) ||
             (path.Length >= 4 && path[3] is not ('\\' or '/'))))
        {
            return false;
        }

        // There are some edge cases that can get to this point.
        // After eliminating valid / invalid roots, fall back on original behavior.
        return Path.GetDirectoryName(path) == null;
    }

    /// <summary>
    /// Temporary check for something like http://foo which will end up like c:\foo\bar\http://foo
    /// We should either have no colon, or exactly one colon.
    /// UNDONE: This is a minimal safe change for Dev10. The correct fix should be to make GetFullPath/NormalizePath throw for this.
    /// </summary>
    private static void ThrowForUrl(string fullPath, string itemSpec, string currentDirectory)
    {
        if (fullPath.IndexOf(':') != fullPath.LastIndexOf(':'))
        {
            // Cause a better error to appear
            _ = Path.GetFullPath(Path.Combine(currentDirectory, itemSpec));
        }
    }
}
