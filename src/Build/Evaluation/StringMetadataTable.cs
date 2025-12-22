// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Wraps a table of metadata values in which keys
/// may be qualified ("itemtype.name") or unqualified ("name").
/// </summary>
internal sealed class StringMetadataTable : IMetadataTable
{
    public static readonly StringMetadataTable Empty = new(null);

    /// <summary>
    /// Table of metadata values.
    /// Each key may be qualified ("itemtype.name") or unqualified ("name").
    /// Unqualified are considered to apply to all item types.
    /// May be null, if empty.
    /// </summary>
    private readonly Dictionary<string, string>? _metadata;

    /// <summary>
    /// Constructor taking a table of metadata in which keys
    /// may be a mixture of qualified ("itemtype.name") and unqualified ("name").
    /// Unqualified keys are considered to apply to all item types.
    /// Metadata may be null, indicating it is empty.
    /// </summary>
    private StringMetadataTable(Dictionary<string, string>? metadata)
    {
        _metadata = metadata;
    }

    public static StringMetadataTable Create(Dictionary<string, string>? metadata)
        => metadata == null || metadata.Count == 0
            ? Empty
            : new StringMetadataTable(metadata);

    public static StringMetadataTable Create(params ReadOnlySpan<(string Name, string Value)> metadata)
    {
        if (metadata.Length == 0)
        {
            return Empty;
        }

        var dictionary = new Dictionary<string, string>(metadata.Length, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in metadata)
        {
            dictionary.Add(name, value);
        }

        return new StringMetadataTable(dictionary);
    }

    /// <summary>
    /// Retrieves any value we have in our metadata table for the metadata name specified.
    /// If no value is available, returns empty string.
    /// </summary>
    public string GetEscapedValue(string name)
        => GetEscapedValue(null, name);

    /// <summary>
    /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
    /// If no value is available, returns empty string.
    /// </summary>
    public string GetEscapedValue(string? itemType, string name)
        => GetEscapedValueIfPresent(itemType, name) ?? string.Empty;

    /// <summary>
    /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
    /// If no value is available, returns null.
    /// </summary>
    public string? GetEscapedValueIfPresent(string? itemType, string name)
    {
        if (_metadata == null)
        {
            return null;
        }

        string key = itemType == null
            ? name
            : $"{itemType}.{name}";

        _metadata.TryGetValue(key, out string? value);

        return value;
    }
}
