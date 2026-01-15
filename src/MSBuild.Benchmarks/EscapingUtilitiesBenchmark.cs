// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class EscapingUtilitiesBenchmark
{
    // Representative strings for quick inner-loop testing
    private const string SimpleString = "This is a much longer string with no special characters that need escaping or any escape" +
        "sequences that need unescaping. It contains multiple sentences and goes on for quite a while to test performance with " +
        "longer inputs that does not actually need any escaping or unescaping work done.";

    private const string UnescapedString_WithSpecialCharacters = "This is a much longer string that definitely has very special " +
        "characters like $ and %() that need escaping. This string goes on for awhile with occassional special characters @ key" +
        "locations. Does that make sense?";

    private const string EscapedString = "This is a longer string with several special characters scattered throughout." +
        " It has %2a and %3f wildcards%2c %40 symbols%2c %24 variables%2c %28parentheses%29%2c and %27quotes%27 that all" +
        " need to be escaped properly.";

    [Benchmark]
    public string Escape_SimpleString() => EscapingUtilities.Escape(SimpleString);

    [Benchmark]
    public string Escape_UnescapedString_WithSpecialCharacters() => EscapingUtilities.Escape(UnescapedString_WithSpecialCharacters);

    [Benchmark]
    public string Unescape_SimpleString() => EscapingUtilities.UnescapeAll(SimpleString);

    [Benchmark]
    public string Unescape_StringWithEscapeSequences() => EscapingUtilities.UnescapeAll(EscapedString);
}
