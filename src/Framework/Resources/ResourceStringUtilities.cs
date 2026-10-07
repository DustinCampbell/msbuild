// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework.Resources;

/// <summary>
///  Provides common operations for deferred resource strings.
/// </summary>
internal static class ResourceStringUtilities
{
    /// <summary>
    ///  Resolves a resource string.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="resource">The resource string to resolve.</param>
    /// <returns>
    ///  The resolved resource string and its diagnostic metadata.
    /// </returns>
    public static ResolvedResourceString Resolve<TResource>(TResource resource)
        where TResource : IResourceString
        => resource.Resolve();

    /// <summary>
    ///  Resolves a resource string and returns its localized text with any MSBuild diagnostic code removed.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="resource">The resource string to resolve.</param>
    /// <returns>
    ///  The localized resource text.
    /// </returns>
    public static string GetText<TResource>(TResource resource)
        where TResource : IResourceString
        => resource.Resolve().Text;

    /// <summary>
    ///  Resolves a resource string and returns its localized text without removing an MSBuild diagnostic code.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="resource">The resource string to resolve.</param>
    /// <returns>
    ///  The localized resource text including any diagnostic code.
    /// </returns>
    public static string GetOriginalText<TResource>(TResource resource)
        where TResource : IResourceString
        => resource.Resolve().OriginalText;

    public static string GetResourceString(ResourceString resource)
        => resource.Resolve().OriginalText;

    public static string FormatResourceStringStripCodeAndKeyword(ResourceString resource)
        => resource.Resolve().Text;

    public static string FormatResourceStringStripCodeAndKeyword<T1>(ResourceString resource, T1 arg0)
        => resource.WithArguments(arg0).Resolve().Text;

    public static string FormatResourceStringStripCodeAndKeyword<T1, T2>(ResourceString resource, T1 arg0, T2 arg1)
        => resource.WithArguments(arg0, arg1).Resolve().Text;

    public static string FormatResourceStringStripCodeAndKeyword<T1, T2, T3>(ResourceString resource, T1 arg0, T2 arg1, T3 arg2)
        => resource.WithArguments(arg0, arg1, arg2).Resolve().Text;

    public static string FormatResourceStringStripCodeAndKeyword(ResourceString resource, params object?[] args)
        => MessageFormatter.Format(resource.Resolve().Text, args);

    public static string FormatResourceStringStripCodeAndKeyword(
        out string? code,
        out string? helpKeyword,
        ResourceString resource)
    {
        ResolvedResourceString resolved = resource.Resolve();
        code = resolved.Code;
        helpKeyword = resolved.HelpKeyword;
        return resolved.Text;
    }

    public static string FormatResourceStringStripCodeAndKeyword<T1>(
        out string? code,
        out string? helpKeyword,
        ResourceString resource,
        T1 arg0)
        => GetResolvedText(resource.WithArguments(arg0), out code, out helpKeyword);

    public static string FormatResourceStringStripCodeAndKeyword<T1, T2>(
        out string? code,
        out string? helpKeyword,
        ResourceString resource,
        T1 arg0,
        T2 arg1)
        => GetResolvedText(resource.WithArguments(arg0, arg1), out code, out helpKeyword);

    public static string FormatResourceStringStripCodeAndKeyword<T1, T2, T3>(
        out string? code,
        out string? helpKeyword,
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2)
        => GetResolvedText(resource.WithArguments(arg0, arg1, arg2), out code, out helpKeyword);

    public static string FormatResourceStringStripCodeAndKeyword(
        out string? code,
        out string? helpKeyword,
        ResourceString resource,
        params object?[] args)
    {
        ResolvedResourceString resolved = resource.Resolve();
        code = resolved.Code;
        helpKeyword = resolved.HelpKeyword;
        return MessageFormatter.Format(resolved.Text, args);
    }

    public static string FormatResourceStringIgnoreCodeAndKeyword(ResourceString resource)
        => resource.Resolve().OriginalText;

    public static string FormatResourceStringIgnoreCodeAndKeyword<T1>(ResourceString resource, T1 arg0)
        => resource.WithArguments(arg0).Resolve().OriginalText;

    public static string FormatResourceStringIgnoreCodeAndKeyword<T1, T2>(ResourceString resource, T1 arg0, T2 arg1)
        => resource.WithArguments(arg0, arg1).Resolve().OriginalText;

    public static string FormatResourceStringIgnoreCodeAndKeyword<T1, T2, T3>(ResourceString resource, T1 arg0, T2 arg1, T3 arg2)
        => resource.WithArguments(arg0, arg1, arg2).Resolve().OriginalText;

    public static string FormatResourceStringIgnoreCodeAndKeyword(ResourceString resource, params object?[] args)
        => MessageFormatter.Format(resource.Resolve().OriginalText, args);

    [Conditional("DEBUG")]
    public static void VerifyResourceStringExists(ResourceString resource)
        => resource.Resolve();

    private static string GetResolvedText<TResource>(
        TResource resource,
        out string? code,
        out string? helpKeyword)
        where TResource : IResourceString
    {
        ResolvedResourceString resolved = resource.Resolve();
        code = resolved.Code;
        helpKeyword = resolved.HelpKeyword;
        return resolved.Text;
    }
}
