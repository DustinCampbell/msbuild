// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Shared;

/// <summary>
/// Encapsulates the definitions of the item-spec modifiers a.k.a. reserved item metadata.
/// </summary>
internal static class ItemSpecModifiers
{
    private enum ModifierKind
    {
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

    // These are all the well-known attributes.
    public static readonly ImmutableArray<string> All =
    [
        FullPath,
        RootDir,
        Filename,
        Extension,
        RelativeDir,
        Directory,
        RecursiveDir, // <-- Not derivable.
        Identity,
        ModifiedTime,
        CreatedTime,
        AccessedTime,
        DefiningProjectFullPath,
        DefiningProjectDirectory,
        DefiningProjectName,
        DefiningProjectExtension
    ];

    private static readonly FrozenDictionary<string, ModifierKind> s_modifierKindMap = new Dictionary<string, ModifierKind>(StringComparer.OrdinalIgnoreCase)
    {
        { FullPath, ModifierKind.FullPath },
        { RootDir, ModifierKind.RootDir },
        { Filename, ModifierKind.Filename },
        { Extension, ModifierKind.Extension },
        { RelativeDir, ModifierKind.RelativeDir },
        { Directory, ModifierKind.Directory },
        { RecursiveDir, ModifierKind.RecursiveDir },
        { Identity, ModifierKind.Identity },
        { ModifiedTime, ModifierKind.ModifiedTime },
        { CreatedTime, ModifierKind.CreatedTime },
        { AccessedTime, ModifierKind.AccessedTime },
        { DefiningProjectFullPath, ModifierKind.DefiningProjectFullPath },
        { DefiningProjectDirectory, ModifierKind.DefiningProjectDirectory },
        { DefiningProjectName, ModifierKind.DefiningProjectName },
        { DefiningProjectExtension, ModifierKind.DefiningProjectExtension },
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Indicates if the given name is reserved for an item-spec modifier.
    /// </summary>
    public static bool IsItemSpecModifier([NotNullWhen(true)] string? name)
        => name != null && s_modifierKindMap.ContainsKey(name);

    /// <summary>
    /// Indicates if the given name is reserved for a derivable item-spec modifier.
    /// Derivable means it can be computed given a file name.
    /// </summary>
    /// <param name="name">Name to check.</param>
    /// <returns>true, if name of a derivable modifier</returns>
    public static bool IsDerivableItemSpecModifier([NotNullWhen(true)] string? name)
        => name != null &&
           s_modifierKindMap.TryGetValue(name, out ModifierKind modifier) &&
           modifier != ModifierKind.RecursiveDir;

    /// <inheritdoc cref="GetItemSpecModifier(string, string, string, string, ref string?)"/>
    public static string GetItemSpecModifier(
        string modifier,
        string itemSpec,
        string? currentDirectory,
        string? definingProjectEscaped)
    {
        FrameworkErrorUtilities.VerifyThrow(itemSpec != null, "Need item-spec to modify.");
        FrameworkErrorUtilities.VerifyThrow(modifier != null, "Need modifier to apply to item-spec.");

        if (!s_modifierKindMap.TryGetValue(modifier, out ModifierKind modifierKind))
        {
            InternalErrorException.Throw($"\"{modifier}\" is not a valid item-spec modifier.");
            return null;
        }

        string? dummy = null;
        return GetItemSpecModifier(modifierKind, itemSpec, currentDirectory, definingProjectEscaped, ref dummy);
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
    /// <param name="modifier">The modifier to apply to the item-spec.</param>
    /// <param name="itemSpec">The item-spec to modify.</param>
    /// <param name="currentDirectory">The root directory for relative item-specs. When called on the Engine thread, this is the project directory. When called as part of building a task, it is null, indicating that the current directory should be used.</param>
    /// <param name="definingProjectEscaped">The path to the project that defined this item (may be null).</param>
    /// <param name="cachedFullPath">Full path if any was previously computed, to cache.</param>
    /// <returns>The modified item-spec (can be empty string, but will never be null).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the item-spec is not a path.</exception>
    public static string GetItemSpecModifier(
        string modifier,
        string itemSpec,
        string? currentDirectory,
        string? definingProjectEscaped,
        ref string? cachedFullPath)
    {
        FrameworkErrorUtilities.VerifyThrow(itemSpec != null, "Need item-spec to modify.");
        FrameworkErrorUtilities.VerifyThrow(modifier != null, "Need modifier to apply to item-spec.");

        if (!s_modifierKindMap.TryGetValue(modifier, out ModifierKind modifierKind))
        {
            InternalErrorException.Throw($"\"{modifier}\" is not a valid item-spec modifier.");
            return null;
        }

        return GetItemSpecModifier(modifierKind, itemSpec, currentDirectory, definingProjectEscaped, ref cachedFullPath);
    }

    private static string GetItemSpecModifier(
        ModifierKind modifierKind,
        string itemSpec,
        string? currentDirectory,
        string? definingProjectEscaped,
        ref string? fullPath)
    {
        try
        {
            switch (modifierKind)
            {
                case ModifierKind.FullPath:
                    EnsureFullPath(ref fullPath, itemSpec, currentDirectory);
                    return fullPath;

                case ModifierKind.RootDir:
                    EnsureFullPath(ref fullPath, itemSpec, currentDirectory);
                    return ComputeRootDir(fullPath);

                case ModifierKind.Filename:
                    return ComputeFilename(itemSpec);

                case ModifierKind.Extension:
                    return ComputeExtension(itemSpec);

                case ModifierKind.RelativeDir:
                    return ComputeRelativeDir(itemSpec);

                case ModifierKind.Directory:
                    EnsureFullPath(ref fullPath, itemSpec, currentDirectory);
                    return ComputeDirectory(fullPath);

                case ModifierKind.RecursiveDir:
                    // only the BuildItem class can compute this modifier -- so leave empty
                    return string.Empty;

                case ModifierKind.Identity:
                    return itemSpec;

                case ModifierKind.ModifiedTime:
                    return ComputeModifiedTime(itemSpec);

                case ModifierKind.CreatedTime:
                    return ComputeCreatedTime(itemSpec);

                case ModifierKind.AccessedTime:
                    return ComputeAccessedTime(itemSpec);
            }

            if (string.IsNullOrEmpty(definingProjectEscaped))
            {
                // We have nothing to work with, but that's sometimes OK -- so just return an empty string
                return string.Empty;
            }

            FrameworkErrorUtilities.VerifyThrow(definingProjectEscaped != null, $"{definingProjectEscaped} should not be null.");

            switch (modifierKind)
            {
                case ModifierKind.DefiningProjectDirectory:
                    string definingProjectFullPath = ComputeFullPath(definingProjectEscaped, currentDirectory);

                    // ItemSpecModifiers.Directory does not contain the root directory
                    return Path.Combine(
                        ComputeRootDir(definingProjectFullPath),
                        ComputeDirectory(definingProjectFullPath));

                case ModifierKind.DefiningProjectFullPath:
                    return ComputeFullPath(definingProjectEscaped, currentDirectory);

                case ModifierKind.DefiningProjectName:
                    return ComputeFilename(definingProjectEscaped);

                case ModifierKind.DefiningProjectExtension:
                    return ComputeExtension(definingProjectEscaped);
            }

            InternalErrorException.Throw($"\"{modifierKind}\" is not a valid item-spec modifier.");
        }
        catch (Exception e) when (FrameworkExceptionHandling.IsIoRelatedException(e))
        {
            InvalidOperationException.Throw(SR.FormatInvalidFileSpecForTransform(modifierKind, itemSpec, e.Message));
        }

        return null;
    }

    private static void EnsureFullPath([NotNull] ref string? fullPath, string itemSpec, string? currentDirectory)
        => fullPath ??= ComputeFullPath(itemSpec, currentDirectory);

    private static string ComputeFullPath(string itemSpec, string? currentDirectory)
    {
        currentDirectory ??= FrameworkFileUtilities.CurrentThreadWorkingDirectory ?? string.Empty;

        string fullPath = FrameworkFileUtilities.GetFullPath(itemSpec, currentDirectory);

        ThrowForUrl(fullPath, itemSpec, currentDirectory);

        return fullPath;
    }

    private static string ComputeRootDir(string fullPath)
    {
        string? rootDir = Path.GetPathRoot(fullPath);

        FrameworkErrorUtilities.VerifyThrow(rootDir != null, $"{nameof(rootDir)} should not be null.");

        if (FrameworkFileUtilities.EndsWithSlash(rootDir))
        {
            return rootDir;
        }

        FrameworkErrorUtilities.VerifyThrow(
            FileUtilitiesRegex.StartsWithUncPattern(rootDir),
            "Only UNC shares should be missing trailing slashes.");

        // restore/append trailing slash if Path.GetPathRoot() has either removed it, or failed to add it
        // (this happens with UNC shares)
        return rootDir + Path.DirectorySeparatorChar;
    }

    private static string ComputeFilename(string itemSpec)
    {
        if (!IsRootDirectory(itemSpec))
        {
            // Fix path to avoid problem with Path.GetFileNameWithoutExtension when backslashes in itemSpec on Unix
            return Path.GetFileNameWithoutExtension(FrameworkFileUtilities.FixFilePath(itemSpec));
        }

        // If the item-spec is a root directory, it can have no extension
        // NOTE: this is to prevent Path.GetExtension() from treating server and share elements in a UNC
        // file-spec as filenames e.g. \\server.ext, \\server\share.ext
        return string.Empty;
    }

    private static string ComputeExtension(string itemSpec)
    {
        if (!IsRootDirectory(itemSpec))
        {
            return Path.GetExtension(itemSpec);
        }

        // If the item-spec is a root directory, it can have no extension
        // NOTE: this is to prevent Path.GetExtension() from treating server and share elements in a UNC
        // file-spec as filenames e.g. \\server.ext, \\server\share.ext
        return string.Empty;
    }

    private static string ComputeRelativeDir(string itemSpec)
        => FrameworkFileUtilities.GetDirectory(itemSpec);

    private static string ComputeDirectory(string fullPath)
    {
        string directory = FrameworkFileUtilities.GetDirectory(fullPath);

        if (NativeMethods.IsWindows)
        {
            int length = FileUtilitiesRegex.StartsWithDrivePattern(directory)
                ? 2
                : FileUtilitiesRegex.StartsWithUncPatternMatchLength(directory);

            if (length >= 0)
            {
                FrameworkErrorUtilities.VerifyThrow(
                    (directory.Length > length) && FrameworkFileUtilities.IsSlash(directory[length]),
                    "Root directory must have a trailing slash.");

                return directory.Substring(length + 1);
            }

            return directory;
        }

        FrameworkErrorUtilities.VerifyThrow(
            !string.IsNullOrEmpty(directory) && FrameworkFileUtilities.IsSlash(directory[0]),
            "Expected a full non-windows path rooted at '/'.");

        // A full Unix path is always rooted at '/', and a root-relative path is the rest of the string.
        return directory.Substring(1);
    }

    private static string ComputeModifiedTime(string itemSpec)
    {
        // About to go out to the filesystem.  This means data is leaving the engine, so need to unescape it first.
        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

        if (FileSystems.Default.FileExists(unescapedItemSpec))
        {
            return File.GetLastWriteTime(unescapedItemSpec).ToString(FrameworkFileUtilities.FileTimeFormat);
        }

        // File does not exist, or path is a directory
        return string.Empty;
    }

    private static string ComputeCreatedTime(string itemSpec)
    {
        // About to go out to the filesystem.  This means data is leaving the engine, so need to unescape it first.
        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

        if (FileSystems.Default.FileExists(unescapedItemSpec))
        {
            return File.GetCreationTime(unescapedItemSpec).ToString(FrameworkFileUtilities.FileTimeFormat);
        }

        // File does not exist, or path is a directory
        return string.Empty;
    }

    private static string ComputeAccessedTime(string itemSpec)
    {
        // About to go out to the filesystem.  This means data is leaving the engine, so need to unescape it first.
        string unescapedItemSpec = EscapingUtilities.UnescapeAll(itemSpec);

        if (FileSystems.Default.FileExists(unescapedItemSpec))
        {
            return File.GetLastAccessTime(unescapedItemSpec).ToString(FrameworkFileUtilities.FileTimeFormat);
        }

        // File does not exist, or path is a directory
        return string.Empty;
    }

    /// <summary>
    /// Indicates whether the given path is a UNC or drive pattern root directory.
    /// <para>Note: This function mimics the behavior of checking if Path.GetDirectoryName(path) == null.</para>
    /// </summary>
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
            ((path.Length >= 3 && path[2] != '\\' && path[2] != '/') ||
            (path.Length >= 4 && path[3] != '\\' && path[3] != '/')))
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
