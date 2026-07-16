// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

/// <summary>
/// Allocation-focused benchmarks for <see cref="EventArgsFormatting.FormatEventMessage(BuildErrorEventArgs, bool, string)"/>,
/// which formats every error/warning line the console loggers emit. The former implementation built a positional
/// format template string and ran it through <c>StringBuilder.AppendFormat(object[])</c>, boxing the integer
/// arguments; these benchmarks capture the effect of emitting the pieces directly instead.
/// </summary>
[MemoryDiagnoser]
public class EventArgsFormattingBenchmark
{
    private BuildErrorEventArgs _error = null!;
    private BuildErrorEventArgs _multiLineError = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A representative compiler error with a file, line/column span, code, and single-line message.
        _error = new BuildErrorEventArgs(
            subcategory: null,
            code: "CS0168",
            file: "src/Foo/Bar.cs",
            lineNumber: 42,
            columnNumber: 13,
            endLineNumber: 42,
            endColumnNumber: 27,
            message: "The variable 'x' is declared but never used",
            helpKeyword: null,
            senderName: "Csc");

        _multiLineError = new BuildErrorEventArgs(
            subcategory: null,
            code: "MSB4018",
            file: "src/Foo/Bar.targets",
            lineNumber: 10,
            columnNumber: 5,
            endLineNumber: 0,
            endColumnNumber: 0,
            message: "The task failed unexpectedly.\nSystem.InvalidOperationException: something went wrong\n   at Foo.Bar()",
            helpKeyword: null,
            senderName: "MSBuild");
    }

    [Benchmark(Baseline = true)]
    public string FormatError()
        => EventArgsFormatting.FormatEventMessage(_error, showProjectFile: false, projectConfigurationDescription: null);

    [Benchmark]
    public string FormatMultiLineError()
        => EventArgsFormatting.FormatEventMessage(_multiLineError, showProjectFile: false, projectConfigurationDescription: null);
}
