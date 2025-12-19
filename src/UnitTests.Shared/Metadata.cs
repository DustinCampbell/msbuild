// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests;

[CollectionBuilder(typeof(Metadata), nameof(Create))]
public sealed class Metadata : IXunitSerializable, IEnumerable<(string Name, string Value)>
{
    private readonly List<(string Name, string Value)> _items = [];

    public static Metadata Create(ReadOnlySpan<(string Name, string Value)> items)
    {
        var metadata = new Metadata();

        foreach (var item in items)
        {
            metadata.Add(item);
        }

        return metadata;
    }

    public void Add((string Name, string Value) item)
        => _items.Add(item);

    public void Add(string name, string value)
        => _items.Add((name, value));

    public IEnumerator<(string Name, string Value)> GetEnumerator()
    {
        foreach (var item in _items)
        {
            yield return item;
        }
    }

    public Dictionary<string, string> ToDictionary(StringComparer? comparer = null)
    {
        comparer ??= StringComparer.OrdinalIgnoreCase;

        var dictionary = new Dictionary<string, string>(capacity: _items.Count, comparer);

        foreach (var (name, value) in _items)
        {
            dictionary[name] = value;
        }

        return dictionary;
    }

    void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
    {
        var names = info.GetValue<string[]>("Names");
        var values = info.GetValue<string[]>("Values");

        for (int i = 0; i < names.Length; i++)
        {
            _items.Add((names[i], values[i]));
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void IXunitSerializable.Serialize(IXunitSerializationInfo info)
    {
        var names = new string[_items.Count];
        var values = new string[_items.Count];

        for (int i = 0; i < _items.Count; i++)
        {
            names[i] = _items[i].Name;
            values[i] = _items[i].Value;
        }

        info.AddValue("Names", names);
        info.AddValue("Values", values);
    }
}
