// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class EscapingUtilitiesBenchmark
{
    private const string ShortPath = @"src\MyProject\MyFile.cs";
    private const string ShortPath_WithSpecialCharacters = @"C:\Program Files (x86)\MSBuild\Current\Bin";

    private const string ShortPropertyReference = "$(Configuration)|$(Platform)";
    private const string ShortPropertyReference_Escaped = "%24%28Configuration%29%7c%24%28Platform%29";

    private const string LongString = "This is a much longer string with no special characters that need escaping or any escape " +
        "sequences that need unescaping. It contains multiple sentences and goes on for quite a while to test performance with " +
        "longer inputs that does not actually need any escaping or unescaping work done.";

    private const string LongString_WithSpecialCharacters = "This is a much longer string that definitely has very special " +
        "characters like $ and %() that need escaping. This string goes on for awhile with occassional special characters @ key" +
        "locations. Does that make sense?";

    private const string LongString_WithEscapeSequences = "This is a longer string with several special characters scattered throughout. " +
        "It has %2a and %3f wildcards%2c %40 symbols%2c %24 variables%2c %28parentheses%29%2c and %27quotes%27 that all " +
        "need to be escaped properly.";

    [Benchmark]
    public string Escape_ShortPath()
        => EscapingUtilities.Escape(ShortPath);

    [Benchmark]
    public string Escape_ShortPath_WithSpecialCharacters()
        => EscapingUtilities.Escape(ShortPath_WithSpecialCharacters);

    [Benchmark]
    public string Escape_ShortPropertyReference()
        => EscapingUtilities.Escape(ShortPropertyReference);

    [Benchmark]
    public string Unescape_ShortPropertyReference_Escaped()
        => EscapingUtilities.UnescapeAll(ShortPropertyReference_Escaped);

    [Benchmark]
    public string Escape_LongString()
        => EscapingUtilities.Escape(LongString);

    [Benchmark]
    public string Escape_LongString_WithSpecialCharacters()
        => EscapingUtilities.Escape(LongString_WithSpecialCharacters);

    [Benchmark]
    public string Unescape_LongString()
        => EscapingUtilities.UnescapeAll(LongString);

    [Benchmark]
    public string Unescape_LongString_WithEscapeSequences()
        => EscapingUtilities.UnescapeAll(LongString_WithEscapeSequences);
}
