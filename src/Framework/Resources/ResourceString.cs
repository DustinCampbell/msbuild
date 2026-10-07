// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Resources;
using System.Threading;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework.Resources;

/// <summary>
///  Represents a lazily resolved, culture-sensitive resource string.
/// </summary>
internal sealed class ResourceString : IResourceString
{
    private readonly Func<CultureInfo?>? _getCulture;
    private readonly string _helpKeywordPrefix;
    private readonly string _resourceName;
    private readonly ResourceManager _resourceManager;
    private volatile CacheEntry? _cache;
    private string? _helpKeyword;

    /// <summary>
    ///  Initializes a resource string descriptor.
    /// </summary>
    /// <param name="resourceManager">The resource manager that owns the resource.</param>
    /// <param name="getCulture">Returns an explicitly configured resource culture, or <see langword="null"/>.</param>
    /// <param name="resourceName">The exact RESX key.</param>
    /// <param name="helpKeywordPrefix">The prefix used to construct the resource's help keyword.</param>
    public ResourceString(
        ResourceManager resourceManager,
        Func<CultureInfo?>? getCulture,
        string resourceName,
        string helpKeywordPrefix)
    {
        ArgumentNullException.ThrowIfNull(resourceManager);
        ArgumentNullException.ThrowIfNull(resourceName);
        ArgumentNullException.ThrowIfNull(helpKeywordPrefix);

        _resourceManager = resourceManager;
        _getCulture = getCulture;
        _resourceName = resourceName;
        _helpKeywordPrefix = helpKeywordPrefix;
    }

    /// <summary>
    ///  Gets the exact RESX key.
    /// </summary>
    public string ResourceName => _resourceName;

    /// <summary>
    ///  Resolves the resource string for the effective culture.
    /// </summary>
    /// <returns>
    ///  The resolved resource string.
    /// </returns>
    public ResolvedResourceString Resolve()
        => Resolve(_getCulture?.Invoke() ?? CultureInfo.CurrentUICulture);

    /// <summary>
    ///  Resolves the resource string for a specific culture.
    /// </summary>
    /// <param name="culture">The culture to use for the resource lookup.</param>
    /// <returns>
    ///  The resolved resource string.
    /// </returns>
    public ResolvedResourceString Resolve(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        CacheEntry? cache = _cache;

        if (cache is null || !cache.Culture.Equals(culture))
        {
            string resourceText = _resourceManager.GetString(_resourceName, culture)
                ?? throw new MissingManifestResourceException($"The resource string '{_resourceName}' was not found.");

            if (!MessageParser.TryParseMSBuildCode(resourceText, out string? code, out string? text))
            {
                code = null;
                text = resourceText;
            }

            cache = new CacheEntry(culture, resourceText, text, code);
            _cache = cache;
        }

        return new ResolvedResourceString(cache.OriginalText, cache.Text, cache.Code, GetHelpKeyword());
    }

    public FormattedResourceString<T1> WithArguments<T1>(T1 arg0)
        => new(this, arg0);

    public FormattedResourceString<T1, T2> WithArguments<T1, T2>(T1 arg0, T2 arg1)
        => new(this, arg0, arg1);

    public FormattedResourceString<T1, T2, T3> WithArguments<T1, T2, T3>(T1 arg0, T2 arg1, T3 arg2)
        => new(this, arg0, arg1, arg2);

    public FormattedResourceString<T1, T2, T3, T4> WithArguments<T1, T2, T3, T4>(T1 arg0, T2 arg1, T3 arg2, T4 arg3)
        => new(this, arg0, arg1, arg2, arg3);

    public FormattedResourceString<T1, T2, T3, T4, T5> WithArguments<T1, T2, T3, T4, T5>(
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3,
        T5 arg4)
        => new(this, arg0, arg1, arg2, arg3, arg4);

    public FormattedResourceString<T1, T2, T3, T4, T5, T6> WithArguments<T1, T2, T3, T4, T5, T6>(
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3,
        T5 arg4,
        T6 arg5)
        => new(this, arg0, arg1, arg2, arg3, arg4, arg5);

    public FormattedResourceString<T1, T2, T3, T4, T5, T6, T7> WithArguments<T1, T2, T3, T4, T5, T6, T7>(
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3,
        T5 arg4,
        T6 arg5,
        T7 arg6)
        => new(this, arg0, arg1, arg2, arg3, arg4, arg5, arg6);

    private string GetHelpKeyword()
    {
        string? helpKeyword = _helpKeyword;
        if (helpKeyword is not null)
        {
            return helpKeyword;
        }

        helpKeyword = _helpKeywordPrefix + _resourceName;
        Interlocked.CompareExchange(ref _helpKeyword, helpKeyword, null);

        return _helpKeyword!;
    }

    private sealed class CacheEntry(CultureInfo culture, string originalText, string text, string? code)
    {
        public CultureInfo Culture { get; } = culture;

        public string OriginalText { get; } = originalText;

        public string Text { get; } = text;

        public string? Code { get; } = code;
    }
}
