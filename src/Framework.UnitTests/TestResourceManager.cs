// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Resources;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  A <see cref="ResourceManager"/> test double that serves strings from an in-memory lookup and
///  counts how many times <see cref="GetString(string, CultureInfo)"/> is invoked.
/// </summary>
internal sealed class TestResourceManager : ResourceManager
{
    public static TestResourceManager Empty { get; } = new(static (_, _) => null);

    private readonly Func<string, CultureInfo?, string?> _getString;

    private TestResourceManager(Func<string, CultureInfo?, string?> getString)
        => _getString = getString;

    public static TestResourceManager Create(string key, string? value)
        => new((name, _) => string.Equals(name, key, StringComparison.Ordinal) ? value : null);

    public static TestResourceManager Create(params ImmutableArray<(string Key, string? Value)> resources)
    {
        if (resources.IsDefaultOrEmpty)
        {
            return Empty;
        }

        Dictionary<string, string?> map = resources.ToDictionary(
            static x => x.Key,
            static x => x.Value);

        return new((name, _) => map.TryGetValue(name, out string? value) ? value : null);
    }

    /// <summary>
    ///  Creates a manager that serves <paramref name="key"/> with a value that depends on the requested culture,
    ///  allowing tests to observe culture-sensitive resolution.
    /// </summary>
    public static TestResourceManager CreateForCultures(string key, Func<CultureInfo?, string?> valueByCulture)
        => new((name, culture) => string.Equals(name, key, StringComparison.Ordinal) ? valueByCulture(culture) : null);

    public int GetStringCallCount { get; private set; }

    public override string? GetString(string name, CultureInfo? culture)
    {
        GetStringCallCount++;
        return _getString(name, culture);
    }
}
