// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Stores an MSBuild-escaped string and lazily caches its unescaped form.
///  Automatically invalidates the unescaped cache when the escaped value is changed.
/// </summary>
internal struct CachedEscapedString(string escaped)
{
    private string _escaped = escaped;
    private string? _unescaped = null;

    /// <summary>
    ///  Gets or sets the escaped value.
    ///  Setting automatically invalidates the unescaped cache.
    /// </summary>
    public string Escaped
    {
        readonly get => _escaped;
        set
        {
            _escaped = value;
            _unescaped = null;
        }
    }

    /// <summary>
    ///  Gets the unescaped value, computing and caching it on first access.
    /// </summary>
    public string Unescaped
        => _unescaped ??= EscapingUtilities.UnescapeAll(_escaped);

    /// <summary>
    ///  Invalidates the unescaped cache. Call after the escaped value has been
    ///  modified externally via a <c>ref</c> parameter (e.g. by a translator).
    /// </summary>
    public void InvalidateCache()
        => _unescaped = null;

    /// <summary>
    ///  Translates the escaped value and invalidates the unescaped cache when deserializing.
    /// </summary>
    public void Translate(ITranslator translator)
    {
        translator.Translate(ref _escaped);

        if (translator.Mode == TranslationDirection.ReadFromStream)
        {
            _unescaped = null;
        }
    }
}
