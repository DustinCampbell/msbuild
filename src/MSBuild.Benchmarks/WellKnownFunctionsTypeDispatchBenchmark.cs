// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class WellKnownFunctionsTypeDispatchBenchmark
{
    private const byte CharLibrary = 1;
    private const byte GuidLibrary = 2;
    private const byte IntLibrary = 3;
    private const byte IntrinsicLibrary = 4;
    private const byte MathLibrary = 5;
    private const byte PathLibrary = 6;
    private const byte RegexLibrary = 7;
    private const byte StringLibrary = 8;
    private const byte StringArrayLibrary = 9;
    private const byte VersionLibrary = 10;

    private static readonly FrozenDictionary<Type, byte> s_libraryLookup = new Dictionary<Type, byte>
    {
        { typeof(char), CharLibrary },
        { typeof(Guid), GuidLibrary },
        { typeof(int), IntLibrary },
        { typeof(IntrinsicFunctions), IntrinsicLibrary },
        { typeof(Math), MathLibrary },
        { typeof(Path), PathLibrary },
        { typeof(Regex), RegexLibrary },
        { typeof(string), StringLibrary },
        { typeof(string[]), StringArrayLibrary },
        { typeof(Version), VersionLibrary },
    }.ToFrozenDictionary();

    private static readonly Type[] s_knownReceivers =
    {
        typeof(string),
        typeof(string[]),
        typeof(int),
        typeof(Version),
        typeof(Guid),
        typeof(Path),
        typeof(Math),
        typeof(char),
        typeof(IntrinsicFunctions),
        typeof(Regex),
    };

    private readonly Random _random = new(42);

    private Type[] _receivers = [];

    [Params(0, 5)]
    public int UnknownEvery { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _receivers = new Type[2000];

        for (int i = 0; i < _receivers.Length; i++)
        {
            bool emitUnknown = UnknownEvery > 0 && (i + 1) % UnknownEvery == 0;

            _receivers[i] = emitUnknown
                ? GetUnknownReceiver()
                : s_knownReceivers[i % s_knownReceivers.Length];
        }
    }

    [Benchmark(Baseline = true)]
    public int FrozenDictionaryLookup()
    {
        int hits = 0;

        foreach (Type receiver in _receivers)
        {
            if (s_libraryLookup.TryGetValue(receiver, out byte library))
            {
                hits += library;
            }
        }

        return hits;
    }

    [Benchmark]
    public int DirectTypeChecks()
    {
        int hits = 0;

        foreach (Type receiver in _receivers)
        {
            if (TryGetLibrary(receiver, out byte library))
            {
                hits += library;
            }
        }

        return hits;
    }

    private static bool TryGetLibrary(Type receiverType, out byte library)
    {
        if (receiverType == typeof(char))
        {
            library = CharLibrary;
            return true;
        }

        if (receiverType == typeof(Guid))
        {
            library = GuidLibrary;
            return true;
        }

        if (receiverType == typeof(int))
        {
            library = IntLibrary;
            return true;
        }

        if (receiverType == typeof(IntrinsicFunctions))
        {
            library = IntrinsicLibrary;
            return true;
        }

        if (receiverType == typeof(Math))
        {
            library = MathLibrary;
            return true;
        }

        if (receiverType == typeof(Path))
        {
            library = PathLibrary;
            return true;
        }

        if (receiverType == typeof(Regex))
        {
            library = RegexLibrary;
            return true;
        }

        if (receiverType == typeof(string))
        {
            library = StringLibrary;
            return true;
        }

        if (receiverType == typeof(string[]))
        {
            library = StringArrayLibrary;
            return true;
        }

        if (receiverType == typeof(Version))
        {
            library = VersionLibrary;
            return true;
        }

        library = default;
        return false;
    }

    private Type GetUnknownReceiver()
    {
        // Mix a few different unknown types to avoid biasing caches on a single missing key.
        int pick = _random.Next(0, 3);

        return pick switch
        {
            0 => typeof(TimeSpan),
            1 => typeof(Uri),
            _ => typeof(object),
        };
    }
}
