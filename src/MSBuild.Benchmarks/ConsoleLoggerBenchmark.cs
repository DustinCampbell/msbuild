// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Benchmarks;

/// <summary>
/// Allocation-focused benchmarks for the parallel console logger and its aligner.
/// Covers the console-logger allocation optimizations:
///   #1 streaming aligned output straight to the writer (avoids the final aligned string),
///   #3 padding built via <see cref="System.Text.StringBuilder"/> rather than <c>new string(' ', N)</c> concatenation,
///   #4 <c>WritePretty</c> avoiding a throwaway <see cref="System.Text.StringBuilder"/> per call.
/// </summary>
[MemoryDiagnoser]
public class ConsoleLoggerBenchmark
{
    /// <summary>
    /// A realistic multi-line message that is wider than the simulated console buffer, so the
    /// aligner both wraps long lines and indents every produced line.
    /// </summary>
    private const string MultiLineMessage =
        "Building project Foo.csproj with a fairly long descriptive message that will exceed the console buffer width and therefore needs to be wrapped across several lines.\n" +
        "  Restored 12 packages in 3.4s.\n" +
        "  CSC : warning CS0168: The variable 'x' is declared but never used in a rather verbose diagnostic message that also wraps.\n" +
        "  Done building project Foo.csproj.";

    private const int PrefixWidth = 8;

    private BenchStringBuilderProvider _provider = null!;
    private ConsoleOutputAligner _aligner = null!;
    private NullTextWriter _writer = null!;

    private ParallelConsoleLogger _logger = null!;
    private ArrayList _items = null!;

    [GlobalSetup]
    public void Setup()
    {
        _provider = new BenchStringBuilderProvider();
        _writer = new NullTextWriter();

        // bufferWidth 80 with alignment on forces the aligner to wrap and indent, exercising the
        // multi-line split path (the interesting case for the streaming overload).
        _aligner = new ConsoleOutputAligner(bufferWidth: 80, alignMessages: true, _provider);

        // A diagnostic-verbosity parallel logger writing to a no-op TextWriter. Because the write
        // handler is a plain TextWriter.Write(string), the logger takes the direct-writer streaming
        // path (#1). Initialize(null) is supported and wires up the aligner and prefix width.
        _logger = new ParallelConsoleLogger(LoggerVerbosity.Diagnostic, _writer.Write, NoOpColor, NoOpReset);
        _logger.Initialize(null);

        // A single item type with several items, each carrying a few metadata pairs. This drives
        // WriteItemType/WriteItemSpec/WriteMetadata (the #3 padding call sites).
        _items = new ArrayList();
        for (int i = 0; i < 8; i++)
        {
            var item = new TaskItem($"src/File{i}.cs");
            item.SetMetadata("Culture", "en-US");
            item.SetMetadata("CopyToOutputDirectory", "PreserveNewest");
            item.SetMetadata("Link", $"Linked/File{i}.cs");
            _items.Add(item);
        }
    }

    private static void NoOpColor(ConsoleColor color)
    {
    }

    private static void NoOpReset()
    {
    }

    // --- #1: aligner string vs. streaming ---

    /// <summary>
    /// Baseline: build the aligned output and materialize it as a string (the pre-optimization path
    /// still used when the write handler is not a plain <see cref="System.IO.TextWriter"/>).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Align_ToString()
        => _writer.Write(_aligner.AlignConsoleOutput(MultiLineMessage, prefixAlreadyWritten: false, PrefixWidth));

    /// <summary>
    /// Optimized: stream the aligned output directly to the writer, avoiding the final string.
    /// </summary>
    [Benchmark]
    public void Align_Stream()
        => _aligner.AlignConsoleOutput(_writer, MultiLineMessage, prefixAlreadyWritten: false, PrefixWidth);

    // --- #4: WritePretty ---

    [Benchmark]
    public void WritePretty_Unindented()
        => _logger.WritePretty(0, "A moderately long single line of build output text.");

    [Benchmark]
    public void WritePretty_Indented()
        => _logger.WritePretty(3, "A moderately long single line of build output text.");

    // --- #3: item output (padding via new string(' ', N)) ---

    [Benchmark]
    public void OutputItems()
        => _logger.OutputItems("Compile", _items);

    /// <summary>
    /// A reusable-single-instance <see cref="IStringBuilderProvider"/> matching the logger's own
    /// pooling behavior, so the aligner benchmarks reflect production allocation characteristics.
    /// </summary>
    private sealed class BenchStringBuilderProvider : IStringBuilderProvider
    {
        private System.Text.StringBuilder? _cached = new System.Text.StringBuilder(0x100);

        public System.Text.StringBuilder Acquire(int capacity)
        {
            System.Text.StringBuilder? sb = System.Threading.Interlocked.Exchange(ref _cached, null);
            if (sb is null || sb.Capacity < capacity)
            {
                return new System.Text.StringBuilder(capacity);
            }

            sb.Clear();
            return sb;
        }

        public string GetStringAndRelease(System.Text.StringBuilder builder)
        {
            string result = builder.ToString();
            Release(builder);
            return result;
        }

        public void Release(System.Text.StringBuilder builder)
            => System.Threading.Interlocked.Exchange(ref _cached, builder);
    }

    /// <summary>
    /// A <see cref="System.IO.TextWriter"/> that discards everything written to it. Overrides the
    /// span overload on modern runtimes so the streaming path measures no incidental allocations.
    /// </summary>
    private sealed class NullTextWriter : System.IO.TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.Unicode;

        public override void Write(char value)
        {
        }

        public override void Write(string? value)
        {
        }

        public override void Write(char[] buffer, int index, int count)
        {
        }

#if NET
        public override void Write(ReadOnlySpan<char> buffer)
        {
        }
#endif
    }
}
