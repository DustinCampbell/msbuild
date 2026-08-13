// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Quick-and-dirty telemetry for measuring expansion behavior.
/// </summary>
internal static class ExpansionTelemetry
{
    private const int StateMagic = 0x45585054;
    private const int StateVersion = 5;
    private const string NullOutput = "\0";
    internal const string TelemetryEnvironmentVariable = "MSBUILDEXPANSIONTELEMETRY";

    private static readonly object s_lock = new();
    private static string s_reportPath = GetReportPath();
    private static string s_statePath = s_reportPath + ".state";
    private static string s_mutexName = GetMutexName(s_reportPath);
    private static TelemetryData s_data = new();

    internal static bool Enabled => true;

    internal static string ReportPath => s_reportPath;

    internal static long GetTimestamp() => Stopwatch.GetTimestamp();

    internal static long GetElapsedTicks(long startTimestamp) => Stopwatch.GetTimestamp() - startTimestamp;

    internal static void RecordExpansion(
        string expression,
        string output,
        long elapsedTicks,
        long metadataElapsedTicks,
        long propertyElapsedTicks,
        long itemElapsedTicks,
        ExpanderOptions options,
        string callType,
        IElementLocation elementLocation)
    {
        if (!Enabled)
        {
            return;
        }

        lock (s_lock)
        {
            s_data.TotalExpansions++;
            s_data.TotalElapsedTicks += elapsedTicks;
            AddStageTiming(metadataElapsedTicks, ref s_data.MetadataExpansions, ref s_data.MetadataElapsedTicks);
            AddStageTiming(propertyElapsedTicks, ref s_data.PropertyExpansions, ref s_data.PropertyElapsedTicks);
            AddStageTiming(itemElapsedTicks, ref s_data.ItemExpansions, ref s_data.ItemElapsedTicks);

            if (!s_data.Expressions.TryGetValue(expression, out ExpressionData expressionData))
            {
                expressionData = new ExpressionData();
                s_data.Expressions.Add(expression, expressionData);
            }

            expressionData.Count++;
            expressionData.TotalElapsedTicks += elapsedTicks;
            expressionData.MinElapsedTicks = Math.Min(expressionData.MinElapsedTicks, elapsedTicks);
            expressionData.MaxElapsedTicks = Math.Max(expressionData.MaxElapsedTicks, elapsedTicks);
            string outputKey = output ?? NullOutput;
            expressionData.Outputs.TryGetValue(outputKey, out long outputCount);
            expressionData.Outputs[outputKey] = outputCount + 1;
            AddCount(expressionData.CallContexts, $"{callType}; {options} (0x{(int)options:X})", 1);
            AddCount(
                expressionData.Locations,
                $"{elementLocation.File}({elementLocation.Line.ToString(CultureInfo.InvariantCulture)},{elementLocation.Column.ToString(CultureInfo.InvariantCulture)})",
                1);

            RecordReferences(expression, "$(", s_data.PropertyReferences);
            RecordReferences(expression, "%(", s_data.MetadataReferences);
            RecordReferences(expression, "@(", s_data.ItemVectors);
        }
    }

    private static void AddStageTiming(long elapsedTicks, ref long count, ref long totalElapsedTicks)
    {
        if (elapsedTicks >= 0)
        {
            count++;
            totalElapsedTicks += elapsedTicks;
        }
    }

    private static void AddCount(Dictionary<string, long> counts, string key, long increment)
    {
        counts.TryGetValue(key, out long count);
        counts[key] = count + increment;
    }

    internal static void RecordTransform(
        string transform,
        string kind,
        string[] inputs,
        string[] outputs,
        long elapsedTicks)
    {
        if (!Enabled)
        {
            return;
        }

        string key = CreateTransformKey(transform, kind, inputs, outputs);
        lock (s_lock)
        {
            if (!s_data.Transforms.TryGetValue(key, out TransformData transformData))
            {
                transformData = new TransformData(transform, kind, inputs, outputs);
                s_data.Transforms.Add(key, transformData);
            }

            transformData.Count++;
            transformData.ElapsedTicks += elapsedTicks;
        }
    }

    internal static void RecordPropertyFunction(
        Type receiverType,
        string methodName,
        object[] arguments,
        string dispatch,
        long elapsedTicks,
        IElementLocation elementLocation)
    {
        string[] argumentValues = new string[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            argumentValues[i] = arguments[i]?.ToString();
        }

        string typeName = receiverType.FullName ?? receiverType.Name;
        string location = $"{elementLocation.File}({elementLocation.Line.ToString(CultureInfo.InvariantCulture)},{elementLocation.Column.ToString(CultureInfo.InvariantCulture)})";
        string key = CreatePropertyFunctionKey(typeName, methodName, argumentValues, dispatch);
        lock (s_lock)
        {
            if (!s_data.PropertyFunctions.TryGetValue(key, out PropertyFunctionData function))
            {
                function = new PropertyFunctionData(typeName, methodName, argumentValues, dispatch);
                s_data.PropertyFunctions.Add(key, function);
            }

            function.Count++;
            function.ElapsedTicks += elapsedTicks;
            AddCount(function.Locations, location, 1);
        }
    }

    internal static void ResetReport()
    {
        ConfigureReportPath(GetReportPath());

        lock (s_lock)
        {
            s_data = new TelemetryData();
        }

        ExecuteWithReportLock(() =>
        {
            File.Delete(s_reportPath);
            File.Delete(s_statePath);
            File.Delete(GetDetailReportPath("expressions"));
            File.Delete(GetDetailReportPath("references"));
            File.Delete(GetDetailReportPath("item-transforms"));
            File.Delete(GetDetailReportPath("property-functions"));
        });
    }

    internal static void ConfigureReportPath(string reportPath)
    {
        string fullPath = Path.GetFullPath(reportPath);
        lock (s_lock)
        {
            s_reportPath = fullPath;
            s_statePath = fullPath + ".state";
            s_mutexName = GetMutexName(fullPath);
            s_data = new TelemetryData();
        }
    }

    internal static void Flush(bool final = false)
    {
        if (!Enabled)
        {
            return;
        }

        TelemetryData snapshot;
        lock (s_lock)
        {
            snapshot = s_data;
            s_data = new TelemetryData();
        }

        try
        {
            ExecuteWithReportLock(() =>
            {
                TelemetryData aggregate = ReadState();
                aggregate.Merge(snapshot);
                WriteState(aggregate);
                WriteReport(aggregate);
                if (final)
                {
                    File.Delete(s_statePath);
                }
            });
        }
        catch (IOException e)
        {
            RestoreSnapshotAndReportFailure(snapshot, e);
        }
        catch (UnauthorizedAccessException e)
        {
            RestoreSnapshotAndReportFailure(snapshot, e);
        }
    }

    private static string GetReportPath()
    {
        string value = Environment.GetEnvironmentVariable(TelemetryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = $"msbuild-expansion-telemetry-{DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}.txt";
        }

        if (value == "1")
        {
            value = $"msbuild-expansion-telemetry-{DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}.txt";
        }

        return Path.GetFullPath(value);
    }

    private static string GetMutexName(string reportPath)
    {
        ulong hash = 14695981039346656037;
        string pathToHash = NativeMethodsShared.IsWindows ? reportPath.ToUpperInvariant() : reportPath;
        foreach (char character in pathToHash)
        {
            hash ^= character;
            hash *= 1099511628211;
        }

        return "MSBuildExpansionTelemetry_" + hash.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static void ExecuteWithReportLock(Action action)
    {
        string directory = Path.GetDirectoryName(s_reportPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using Mutex mutex = new(initiallyOwned: false, s_mutexName);
        bool lockTaken = false;
        try
        {
            try
            {
                lockTaken = mutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                lockTaken = true;
            }

            action();
        }
        finally
        {
            if (lockTaken)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static TelemetryData ReadState()
    {
        if (!File.Exists(s_statePath))
        {
            return new TelemetryData();
        }

        using FileStream stream = new(s_statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new(stream, Encoding.UTF8);
        if (reader.ReadInt32() != StateMagic || reader.ReadInt32() != StateVersion)
        {
            throw new InvalidDataException($"Unrecognized expansion telemetry state file '{s_statePath}'.");
        }

        TelemetryData data = new()
        {
            TotalExpansions = reader.ReadInt64(),
            TotalElapsedTicks = reader.ReadInt64(),
            MetadataExpansions = reader.ReadInt64(),
            MetadataElapsedTicks = reader.ReadInt64(),
            PropertyExpansions = reader.ReadInt64(),
            PropertyElapsedTicks = reader.ReadInt64(),
            ItemExpansions = reader.ReadInt64(),
            ItemElapsedTicks = reader.ReadInt64(),
        };

        ReadExpressions(reader, data.Expressions);
        ReadCounts(reader, data.PropertyReferences);
        ReadCounts(reader, data.MetadataReferences);
        ReadCounts(reader, data.ItemVectors);
        ReadTransforms(reader, data.Transforms);
        ReadPropertyFunctions(reader, data.PropertyFunctions);
        return data;
    }

    private static void WriteState(TelemetryData data)
    {
        string temporaryPath = s_statePath + "." + EnvironmentUtilities.CurrentProcessId.ToString(CultureInfo.InvariantCulture) + ".tmp";
        using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new(stream, Encoding.UTF8))
        {
            writer.Write(StateMagic);
            writer.Write(StateVersion);
            writer.Write(data.TotalExpansions);
            writer.Write(data.TotalElapsedTicks);
            writer.Write(data.MetadataExpansions);
            writer.Write(data.MetadataElapsedTicks);
            writer.Write(data.PropertyExpansions);
            writer.Write(data.PropertyElapsedTicks);
            writer.Write(data.ItemExpansions);
            writer.Write(data.ItemElapsedTicks);
            WriteExpressions(writer, data.Expressions);
            WriteCounts(writer, data.PropertyReferences);
            WriteCounts(writer, data.MetadataReferences);
            WriteCounts(writer, data.ItemVectors);
            WriteTransforms(writer, data.Transforms);
            WritePropertyFunctions(writer, data.PropertyFunctions);
        }

        File.Delete(s_statePath);
        File.Move(temporaryPath, s_statePath);
    }

    private static void WriteReport(TelemetryData data)
    {
        string expressionsPath = GetDetailReportPath("expressions");
        string referencesPath = GetDetailReportPath("references");
        string transformsPath = GetDetailReportPath("item-transforms");
        string propertyFunctionsPath = GetDetailReportPath("property-functions");

        DetailReportInfo expressionsReport = WriteDetailReport(
            expressionsPath,
            "MSBuild Expander Expressions",
            writer => WriteExpressionsReport(writer, data));
        DetailReportInfo referencesReport = WriteDetailReport(
            referencesPath,
            "MSBuild Expander References",
            writer =>
            {
                WriteReferenceSection(writer, "Property references", data.PropertyReferences);
                WriteReferenceSection(writer, "Metadata references", data.MetadataReferences);
                WriteReferenceSection(writer, "Item vectors", data.ItemVectors);
            });
        DetailReportInfo transformsReport = WriteDetailReport(
            transformsPath,
            "MSBuild Item Transforms",
            writer => WriteTransformsReport(writer, data.Transforms));
        DetailReportInfo propertyFunctionsReport = WriteDetailReport(
            propertyFunctionsPath,
            "MSBuild Property Functions",
            writer => WritePropertyFunctionsReport(writer, data.PropertyFunctions));

        using (StreamWriter writer = CreateReportWriter(s_reportPath))
        {
            WriteReportHeader(writer, "MSBuild Expander Telemetry Summary");
            writer.WriteLine("Detail reports");
            writer.WriteLine("--------------");
            WriteDetailReportInfo(writer, expressionsReport);
            WriteDetailReportInfo(writer, referencesReport);
            WriteDetailReportInfo(writer, transformsReport);
            WriteDetailReportInfo(writer, propertyFunctionsReport);
            writer.WriteLine();
            WriteSummary(writer, data);
        }
    }

    private static DetailReportInfo WriteDetailReport(string path, string title, Action<TextWriter> writeContent)
    {
        long lineCount;
        using (LineCountingTextWriter writer = new(path))
        {
            WriteReportHeader(writer, title);
            writeContent(writer);
            lineCount = writer.LineCount;
        }

        return new DetailReportInfo(path, new FileInfo(path).Length, lineCount);
    }

    private static void WriteDetailReportInfo(TextWriter writer, DetailReportInfo report)
    {
        writer.WriteLine($"{Path.GetFileName(report.Path)} - {FormatFileSize(report.Size)}, {FormatCount(report.LineCount)} lines");
    }

    private static StreamWriter CreateReportWriter(string path)
        => new(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static string GetDetailReportPath(string suffix)
    {
        string directory = Path.GetDirectoryName(s_reportPath);
        string fileName = Path.GetFileNameWithoutExtension(s_reportPath);
        string extension = Path.GetExtension(s_reportPath);
        return Path.Combine(directory, $"{fileName}-{suffix}{extension}");
    }

    private static void WriteReportHeader(TextWriter writer, string title)
    {
        writer.WriteLine(title);
        writer.WriteLine(new string('=', title.Length));
        writer.WriteLine($"Generated: {DateTime.UtcNow:O}");
        writer.WriteLine($"Stopwatch frequency: {FormatCount(Stopwatch.Frequency)} ticks/second");
        writer.WriteLine();
    }

    private static void WriteSummary(TextWriter writer, TelemetryData data)
    {
        writer.WriteLine("Summary");
        writer.WriteLine("-------");
        writer.WriteLine($"Total expansions: {FormatCount(data.TotalExpansions)}");
        writer.WriteLine($"Unique expressions: {FormatCount(data.Expressions.Count)}");

        GetOutputReuseCounts(data.Expressions, out long sameOutputs, out long differentOutputs);
        long noOpExpansions = GetNoOpCount(data.Expressions);
        writer.WriteLine($"Same output for the same input: {FormatCount(sameOutputs)}");
        writer.WriteLine($"Different output for the same input: {FormatCount(differentOutputs)}");
        writer.WriteLine($"No-op expansions: {FormatCount(noOpExpansions)}");
        WriteTiming(writer, "Total expansion time", data.TotalElapsedTicks, data.TotalExpansions);
        writer.WriteLine();

        writer.WriteLine("Expansion breakdown");
        writer.WriteLine("-------------------");
        WriteBreakdown(writer, "Property references", data.PropertyReferences, data.PropertyElapsedTicks, data.PropertyExpansions);
        GetPropertyReferenceBreakdown(data.PropertyReferences, out long simplePropertyReferences, out long propertyFunctionReferences);
        writer.WriteLine($"  Simple property lookups: {FormatCount(simplePropertyReferences)}");
        writer.WriteLine($"  Property function references: {FormatCount(propertyFunctionReferences)}");
        WritePropertyFunctionSummary(writer, data.PropertyFunctions);
        WriteBreakdown(writer, "Metadata references", data.MetadataReferences, data.MetadataElapsedTicks, data.MetadataExpansions);
        WriteBreakdown(writer, "Item vectors", data.ItemVectors, data.ItemElapsedTicks, data.ItemExpansions);
        GetItemVectorBreakdown(data.ItemVectors, out long simpleItemVectors, out long transformedItemVectors);
        writer.WriteLine($"  Simple item vectors: {FormatCount(simpleItemVectors)}");
        writer.WriteLine($"  Transformed item vectors: {FormatCount(transformedItemVectors)}");
        WriteItemTransformSummary(writer, data.Transforms);
        writer.WriteLine();

        writer.WriteLine("Time distribution");
        writer.WriteLine("-----------------");
        long unattributedTicks = Math.Max(
            0,
            data.TotalElapsedTicks - data.MetadataElapsedTicks - data.PropertyElapsedTicks - data.ItemElapsedTicks);
        WriteTimeShare(writer, "Metadata expansion", data.MetadataElapsedTicks, data.TotalElapsedTicks);
        WriteTimeShare(writer, "Property expansion", data.PropertyElapsedTicks, data.TotalElapsedTicks);
        WriteTimeShare(writer, "Item-vector expansion", data.ItemElapsedTicks, data.TotalElapsedTicks);
        WriteTimeShare(writer, "Other expansion work, including path adjustment", unattributedTicks, data.TotalElapsedTicks);
        writer.WriteLine();

        writer.WriteLine("Nested subsets (already included above)");
        long propertyFunctionTicks = GetPropertyFunctionTicks(data.PropertyFunctions);
        long transformTicks = GetTransformTicks(data.Transforms);
        WriteTimeShare(writer, "Property functions", propertyFunctionTicks, data.TotalElapsedTicks);
        WriteTimeShare(writer, "Item transforms", transformTicks, data.TotalElapsedTicks);
        writer.WriteLine();

        writer.WriteLine("Top expressions by cumulative expansion time");
        List<KeyValuePair<string, ExpressionData>> expressions = [.. data.Expressions];
        expressions.Sort((left, right) => right.Value.TotalElapsedTicks.CompareTo(left.Value.TotalElapsedTicks));
        int hotspotCount = Math.Min(20, expressions.Count);
        for (int i = 0; i < hotspotCount; i++)
        {
            KeyValuePair<string, ExpressionData> expression = expressions[i];
            writer.WriteLine($"{FormatCount(i + 1),4}. {FormatDecimal(GetTimePercentage(expression.Value.TotalElapsedTicks, data.TotalElapsedTicks)),10}%  {FormatDecimal(GetMilliseconds(expression.Value.TotalElapsedTicks)),16} ms  [{FormatCount(expression.Value.Count)}] {Escape(expression.Key)}");
        }

        writer.WriteLine();
        WriteTopPropertyFunctions(writer, data.PropertyFunctions, reflectionOnly: false);
        writer.WriteLine();
        WriteTopPropertyFunctions(writer, data.PropertyFunctions, reflectionOnly: true);
        writer.WriteLine();
        WriteTopItemTransforms(writer, data.Transforms);
    }

    private static void WriteExpressionsReport(TextWriter writer, TelemetryData data)
    {
        writer.WriteLine("Expressions");
        writer.WriteLine("-----------");
        List<KeyValuePair<string, ExpressionData>> expressions = [.. data.Expressions];
        expressions.Sort((left, right) => right.Value.TotalElapsedTicks.CompareTo(left.Value.TotalElapsedTicks));
        foreach (KeyValuePair<string, ExpressionData> expression in expressions)
        {
            GetOutputReuseCounts(expression.Value, out long sameOutputs, out long differentOutputs);
            long noOpCount = GetNoOpCount(expression.Key, expression.Value);
            GetOutputLengths(expression.Value, out long outputCharacters, out long outputCount, out int minOutputLength, out int maxOutputLength);
            writer.WriteLine($"[{FormatCount(expression.Value.Count)} expansions; {FormatCount(sameOutputs)} same; {FormatCount(differentOutputs)} different; {FormatCount(noOpCount)} no-op] {Escape(expression.Key)}");
            writer.WriteLine($"  timing: total={FormatDecimal(GetMilliseconds(expression.Value.TotalElapsedTicks))} ms; min={FormatDecimal(GetMicroseconds(expression.Value.MinElapsedTicks))} us; max={FormatDecimal(GetMicroseconds(expression.Value.MaxElapsedTicks))} us; avg={FormatDecimal(GetAverageMicroseconds(expression.Value.TotalElapsedTicks, expression.Value.Count))} us");
            writer.WriteLine($"  lengths: input={FormatCount(expression.Key.Length)}; output total={FormatCount(outputCharacters)}; min={FormatCount(minOutputLength)}; max={FormatCount(maxOutputLength)}; avg={FormatDecimal(outputCount == 0 ? 0 : (double)outputCharacters / outputCount)}");
            WriteCounts(writer, "context", expression.Value.CallContexts);
            WriteCounts(writer, "location", expression.Value.Locations);
            foreach (KeyValuePair<string, long> output in expression.Value.Outputs)
            {
                writer.WriteLine($"  output [{FormatCount(output.Value)}; length={(output.Key == NullOutput ? "<null>" : FormatCount(output.Key.Length))}]: {(output.Key == NullOutput ? "<null>" : Escape(output.Key))}");
            }
        }
    }

    private static void WriteTransformsReport(TextWriter writer, Dictionary<string, TransformData> transformData)
    {
        writer.WriteLine("Item transforms");
        writer.WriteLine("---------------");
        List<TransformData> transforms = [.. transformData.Values];
        transforms.Sort((left, right) => right.Count.CompareTo(left.Count));
        foreach (TransformData transform in transforms)
        {
            writer.WriteLine($"[{FormatCount(transform.Count)} uses; {FormatDecimal(GetMilliseconds(transform.ElapsedTicks))} ms; {transform.Kind}; cardinality {FormatCount(transform.Inputs.Length)} -> {FormatCount(transform.Outputs.Length)}] {Escape(transform.Transform)}");
            WriteValues(writer, "input", transform.Inputs);
            WriteValues(writer, "output", transform.Outputs);
        }
    }

    private static void WritePropertyFunctionsReport(TextWriter writer, Dictionary<string, PropertyFunctionData> functionData)
    {
        writer.WriteLine("Property functions");
        writer.WriteLine("------------------");
        List<PropertyFunctionData> functions = [.. functionData.Values];
        functions.Sort((left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        foreach (PropertyFunctionData function in functions)
        {
            bool wellKnown = function.Dispatch == "Well-known";
            bool reflection = function.Dispatch != "Well-known";
            bool lateBound = reflection;
            writer.WriteLine($"[{FormatCount(function.Count)} calls; {FormatDecimal(GetMilliseconds(function.ElapsedTicks))} ms] {Escape(function.TypeName)}::{Escape(function.MethodName)}");
            writer.WriteLine($"  dispatch: {function.Dispatch}; well-known={wellKnown}; reflection={reflection}; late-bound={lateBound}");
            WriteCounts(writer, "location", function.Locations);
            WriteValues(writer, "argument", function.Arguments);
        }
    }

    private static void WriteBreakdown(
        TextWriter writer,
        string name,
        Dictionary<string, long> references,
        long elapsedTicks,
        long expansionCount)
    {
        long referenceCount = 0;
        foreach (long count in references.Values)
        {
            referenceCount += count;
        }

        writer.WriteLine($"{name}: {FormatCount(referenceCount)} occurrences, {FormatCount(references.Count)} unique, {FormatCount(expansionCount)} expansion passes");
        WriteTiming(writer, $"  {name} expansion time", elapsedTicks, expansionCount);
    }

    private static void WriteTiming(TextWriter writer, string name, long ticks, long count)
    {
        double milliseconds = GetMilliseconds(ticks);
        double averageMicroseconds = count == 0 ? 0 : milliseconds * 1000 / count;
        writer.WriteLine($"{name}: {FormatDecimal(milliseconds)} ms cumulative ({FormatCount(ticks)} ticks), {FormatDecimal(averageMicroseconds)} us/expansion");
    }

    private static double GetMilliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static double GetMicroseconds(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;

    private static double GetAverageMicroseconds(long ticks, long count)
        => count == 0 ? 0 : GetMicroseconds(ticks) / count;

    private static void WriteTimeShare(TextWriter writer, string name, long ticks, long totalTicks)
    {
        writer.WriteLine($"{name}: {FormatDecimal(GetMilliseconds(ticks))} ms ({FormatDecimal(GetTimePercentage(ticks, totalTicks))}% of total expansion time)");
    }

    private static void WritePropertyFunctionSummary(
        TextWriter writer,
        Dictionary<string, PropertyFunctionData> functions)
    {
        long totalCalls = 0;
        long totalTicks = 0;
        long wellKnownCalls = 0;
        long wellKnownTicks = 0;
        long reflectionBinderCalls = 0;
        long reflectionBinderTicks = 0;
        long lateBoundFallbackCalls = 0;
        long lateBoundFallbackTicks = 0;

        foreach (PropertyFunctionData function in functions.Values)
        {
            totalCalls += function.Count;
            totalTicks += function.ElapsedTicks;

            switch (function.Dispatch)
            {
                case "Well-known":
                    wellKnownCalls += function.Count;
                    wellKnownTicks += function.ElapsedTicks;
                    break;
                case "Reflection binder":
                    reflectionBinderCalls += function.Count;
                    reflectionBinderTicks += function.ElapsedTicks;
                    break;
                case "Late-bound reflection":
                    lateBoundFallbackCalls += function.Count;
                    lateBoundFallbackTicks += function.ElapsedTicks;
                    break;
            }
        }

        long reflectionCalls = reflectionBinderCalls + lateBoundFallbackCalls;
        long reflectionTicks = reflectionBinderTicks + lateBoundFallbackTicks;

        writer.WriteLine("  Property function invocation breakdown:");
        writer.WriteLine($"    Total: {FormatCount(totalCalls)} calls, {FormatCount(functions.Count)} unique type/method/argument/dispatch combinations, {FormatDecimal(GetMilliseconds(totalTicks))} ms");
        WritePropertyFunctionDispatchSummary(writer, "Well-known fast path", wellKnownCalls, wellKnownTicks, totalCalls, totalTicks);
        WritePropertyFunctionDispatchSummary(writer, "Late-bound (reflection)", reflectionCalls, reflectionTicks, totalCalls, totalTicks);
        WritePropertyFunctionDispatchSummary(writer, "  Standard reflection binder", reflectionBinderCalls, reflectionBinderTicks, totalCalls, totalTicks);
        WritePropertyFunctionDispatchSummary(writer, "  Late-bound fallback", lateBoundFallbackCalls, lateBoundFallbackTicks, totalCalls, totalTicks);
    }

    private static void WritePropertyFunctionDispatchSummary(
        TextWriter writer,
        string name,
        long calls,
        long ticks,
        long totalCalls,
        long totalTicks)
    {
        writer.WriteLine($"    {name}: {FormatCount(calls)} calls ({FormatDecimal(GetPercentage(calls, totalCalls))}%), {FormatDecimal(GetMilliseconds(ticks))} ms ({FormatDecimal(GetPercentage(ticks, totalTicks))}% of property function time)");
    }

    private static void WriteTopPropertyFunctions(
        TextWriter writer,
        Dictionary<string, PropertyFunctionData> functionData,
        bool reflectionOnly)
    {
        writer.WriteLine(reflectionOnly
            ? "Top reflection property function calls by cumulative time"
            : "Top property function calls by cumulative time");

        List<PropertyFunctionData> functions = [];
        foreach (PropertyFunctionData function in functionData.Values)
        {
            if (!reflectionOnly || function.Dispatch != "Well-known")
            {
                functions.Add(function);
            }
        }

        functions.Sort((left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        int count = Math.Min(20, functions.Count);
        for (int i = 0; i < count; i++)
        {
            PropertyFunctionData function = functions[i];
            writer.WriteLine($"{FormatCount(i + 1),4}. {FormatDecimal(GetMilliseconds(function.ElapsedTicks)),16} ms  [{FormatCount(function.Count)}] [{function.Dispatch}] {Escape(function.TypeName)}::{Escape(function.MethodName)}({FormatArguments(function.Arguments)})");
        }
    }

    private static void WriteItemTransformSummary(
        TextWriter writer,
        Dictionary<string, TransformData> transforms)
    {
        long totalCalls = 0;
        long totalTicks = 0;
        Dictionary<string, TransformKindSummary> kinds = new(StringComparer.Ordinal);
        foreach (TransformData transform in transforms.Values)
        {
            totalCalls += transform.Count;
            totalTicks += transform.ElapsedTicks;
            if (!kinds.TryGetValue(transform.Kind, out TransformKindSummary summary))
            {
                summary = new TransformKindSummary();
                kinds.Add(transform.Kind, summary);
            }

            summary.Count += transform.Count;
            summary.ElapsedTicks += transform.ElapsedTicks;
        }

        writer.WriteLine("  Item transform invocation breakdown:");
        writer.WriteLine($"    Total: {FormatCount(totalCalls)} calls, {FormatCount(transforms.Count)} unique transform/input/output combinations, {FormatDecimal(GetMilliseconds(totalTicks))} ms");

        List<KeyValuePair<string, TransformKindSummary>> entries = [.. kinds];
        entries.Sort((left, right) => right.Value.ElapsedTicks.CompareTo(left.Value.ElapsedTicks));
        foreach (KeyValuePair<string, TransformKindSummary> entry in entries)
        {
            writer.WriteLine($"    {entry.Key}: {FormatCount(entry.Value.Count)} calls ({FormatDecimal(GetPercentage(entry.Value.Count, totalCalls))}%), {FormatDecimal(GetMilliseconds(entry.Value.ElapsedTicks))} ms ({FormatDecimal(GetPercentage(entry.Value.ElapsedTicks, totalTicks))}% of item transform time)");
        }
    }

    private static void WriteTopItemTransforms(TextWriter writer, Dictionary<string, TransformData> transformData)
    {
        writer.WriteLine("Top item transforms by cumulative time");
        List<TransformData> transforms = [.. transformData.Values];
        transforms.Sort((left, right) => right.ElapsedTicks.CompareTo(left.ElapsedTicks));
        int count = Math.Min(20, transforms.Count);
        for (int i = 0; i < count; i++)
        {
            TransformData transform = transforms[i];
            writer.WriteLine($"{FormatCount(i + 1),4}. {FormatDecimal(GetMilliseconds(transform.ElapsedTicks)),16} ms  [{FormatCount(transform.Count)}] [{transform.Kind}] {Escape(transform.Transform)} ({FormatCount(transform.Inputs.Length)} -> {FormatCount(transform.Outputs.Length)})");
        }
    }

    private static string FormatArguments(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        for (int i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(arguments[i] is null ? "<null>" : Escape(arguments[i]));
        }

        return builder.ToString();
    }

    private static double GetTimePercentage(long ticks, long totalTicks)
        => totalTicks == 0 ? 0 : ticks * 100.0 / totalTicks;

    private static double GetPercentage(long value, long total)
        => total == 0 ? 0 : value * 100.0 / total;

    private static string FormatCount(long value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string FormatDecimal(double value)
        => value.ToString("N3", CultureInfo.InvariantCulture);

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{FormatCount(bytes)} bytes";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{FormatCount(bytes)} bytes ({(bytes / 1024.0).ToString("N2", CultureInfo.InvariantCulture)} KiB)";
        }

        return $"{FormatCount(bytes)} bytes ({(bytes / (1024.0 * 1024)).ToString("N2", CultureInfo.InvariantCulture)} MiB)";
    }

    private static long GetPropertyFunctionTicks(Dictionary<string, PropertyFunctionData> functions)
    {
        long ticks = 0;
        foreach (PropertyFunctionData function in functions.Values)
        {
            ticks += function.ElapsedTicks;
        }

        return ticks;
    }

    private static long GetTransformTicks(Dictionary<string, TransformData> transforms)
    {
        long ticks = 0;
        foreach (TransformData transform in transforms.Values)
        {
            ticks += transform.ElapsedTicks;
        }

        return ticks;
    }

    private static void WriteCounts(TextWriter writer, string label, Dictionary<string, long> counts)
    {
        foreach (KeyValuePair<string, long> entry in counts)
        {
            writer.WriteLine($"  {label} [{FormatCount(entry.Value)}]: {Escape(entry.Key)}");
        }
    }

    private static void GetPropertyReferenceBreakdown(
        Dictionary<string, long> references,
        out long simplePropertyReferences,
        out long propertyFunctionReferences)
    {
        simplePropertyReferences = 0;
        propertyFunctionReferences = 0;
        foreach (KeyValuePair<string, long> reference in references)
        {
            if (IsPropertyFunctionReference(reference.Key))
            {
                propertyFunctionReferences += reference.Value;
            }
            else
            {
                simplePropertyReferences += reference.Value;
            }
        }
    }

    private static bool IsPropertyFunctionReference(string reference)
    {
        ReadOnlySpan<char> body = reference.AsSpan(2, reference.Length - 3);
        return body.IndexOfAny('.', '[', '$') >= 0 || body.IndexOf('(') >= 0;
    }

    private static void GetItemVectorBreakdown(
        Dictionary<string, long> references,
        out long simpleItemVectors,
        out long transformedItemVectors)
    {
        simpleItemVectors = 0;
        transformedItemVectors = 0;
        foreach (KeyValuePair<string, long> reference in references)
        {
            if (reference.Key.IndexOf("->", StringComparison.Ordinal) >= 0)
            {
                transformedItemVectors += reference.Value;
            }
            else
            {
                simpleItemVectors += reference.Value;
            }
        }
    }

    private static void WriteReferenceSection(TextWriter writer, string heading, Dictionary<string, long> references)
    {
        writer.WriteLine();
        writer.WriteLine(heading);
        writer.WriteLine(new string('-', heading.Length));
        List<KeyValuePair<string, long>> entries = [.. references];
        entries.Sort((left, right) => right.Value.CompareTo(left.Value));
        foreach (KeyValuePair<string, long> entry in entries)
        {
            writer.WriteLine($"[{FormatCount(entry.Value)}] {Escape(entry.Key)}");
        }
    }

    private static void WriteValues(TextWriter writer, string label, string[] values)
    {
        if (values.Length == 0)
        {
            writer.WriteLine($"  {label}: <empty>");
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteLine($"  {label}[{FormatCount(i)}]: {(values[i] is null ? "<null>" : Escape(values[i]))}");
        }
    }

    private static string Escape(string value)
    {
        StringBuilder builder = null;
        for (int i = 0; i < value.Length; i++)
        {
            string replacement = value[i] switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                _ => null,
            };

            if (replacement is not null)
            {
                builder ??= new StringBuilder(value.Length + 8).Append(value, 0, i);
                builder.Append(replacement);
            }
            else
            {
                builder?.Append(value[i]);
            }
        }

        return builder?.ToString() ?? value;
    }

    private static void RecordReferences(string expression, string prefix, Dictionary<string, long> references)
    {
        int index = 0;
        while ((index = expression.IndexOf(prefix, index, StringComparison.Ordinal)) >= 0)
        {
            int end = FindClosingParenthesis(expression, index + 2);
            if (end < 0)
            {
                return;
            }

            string reference = expression.Substring(index, end - index + 1);
            references.TryGetValue(reference, out long count);
            references[reference] = count + 1;
            index += 2;
        }
    }

    private static int FindClosingParenthesis(string expression, int index)
    {
        int depth = 1;
        char quote = '\0';
        for (; index < expression.Length; index++)
        {
            char character = expression[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"' or '`')
            {
                quote = character;
            }
            else if (character == '(')
            {
                depth++;
            }
            else if (character == ')' && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static string CreateTransformKey(string transform, string kind, string[] inputs, string[] outputs)
    {
        StringBuilder builder = new();
        AppendKeyPart(builder, transform);
        AppendKeyPart(builder, kind);
        foreach (string input in inputs)
        {
            AppendKeyPart(builder, input);
        }

        builder.Append('|');
        foreach (string output in outputs)
        {
            AppendKeyPart(builder, output);
        }

        return builder.ToString();
    }

    private static string CreatePropertyFunctionKey(
        string typeName,
        string methodName,
        string[] arguments,
        string dispatch)
    {
        StringBuilder builder = new();
        AppendKeyPart(builder, typeName);
        AppendKeyPart(builder, methodName);
        AppendKeyPart(builder, dispatch);
        foreach (string argument in arguments)
        {
            AppendKeyPart(builder, argument);
        }

        return builder.ToString();
    }

    private static void AppendKeyPart(StringBuilder builder, string value)
    {
        if (value is null)
        {
            builder.Append("-1:");
        }
        else
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }
    }

    private static void GetOutputReuseCounts(
        Dictionary<string, ExpressionData> expressions,
        out long sameOutputs,
        out long differentOutputs)
    {
        sameOutputs = 0;
        differentOutputs = 0;
        foreach (ExpressionData expression in expressions.Values)
        {
            GetOutputReuseCounts(expression, out long expressionSame, out long expressionDifferent);
            sameOutputs += expressionSame;
            differentOutputs += expressionDifferent;
        }
    }

    private static void GetOutputReuseCounts(ExpressionData expression, out long sameOutputs, out long differentOutputs)
    {
        sameOutputs = expression.Count - expression.Outputs.Count;
        differentOutputs = Math.Max(0, expression.Outputs.Count - 1);
    }

    private static long GetNoOpCount(Dictionary<string, ExpressionData> expressions)
    {
        long count = 0;
        foreach (KeyValuePair<string, ExpressionData> expression in expressions)
        {
            count += GetNoOpCount(expression.Key, expression.Value);
        }

        return count;
    }

    private static long GetNoOpCount(string expression, ExpressionData expressionData)
        => expressionData.Outputs.TryGetValue(expression, out long count) ? count : 0;

    private static void GetOutputLengths(
        ExpressionData expression,
        out long totalCharacters,
        out long outputCount,
        out int minLength,
        out int maxLength)
    {
        totalCharacters = 0;
        outputCount = 0;
        minLength = int.MaxValue;
        maxLength = 0;
        foreach (KeyValuePair<string, long> output in expression.Outputs)
        {
            if (output.Key == NullOutput)
            {
                continue;
            }

            int length = output.Key.Length;
            totalCharacters += length * output.Value;
            outputCount += output.Value;
            minLength = Math.Min(minLength, length);
            maxLength = Math.Max(maxLength, length);
        }

        if (outputCount == 0)
        {
            minLength = 0;
        }
    }

    private static void RestoreSnapshotAndReportFailure(TelemetryData snapshot, Exception exception)
    {
        lock (s_lock)
        {
            s_data.Merge(snapshot);
        }

        Console.Error.WriteLine($"MSBuild expansion telemetry could not write '{s_reportPath}': {exception.Message}");
    }

    private static void ReadExpressions(BinaryReader reader, Dictionary<string, ExpressionData> expressions)
    {
        int expressionCount = reader.ReadInt32();
        for (int i = 0; i < expressionCount; i++)
        {
            string expression = reader.ReadString();
            ExpressionData expressionData = new()
            {
                Count = reader.ReadInt64(),
                TotalElapsedTicks = reader.ReadInt64(),
                MinElapsedTicks = reader.ReadInt64(),
                MaxElapsedTicks = reader.ReadInt64(),
            };
            int outputCount = reader.ReadInt32();
            for (int j = 0; j < outputCount; j++)
            {
                expressionData.Outputs.Add(reader.ReadString(), reader.ReadInt64());
            }

            ReadCounts(reader, expressionData.CallContexts);
            ReadCounts(reader, expressionData.Locations);
            expressions.Add(expression, expressionData);
        }
    }

    private static void WriteExpressions(BinaryWriter writer, Dictionary<string, ExpressionData> expressions)
    {
        writer.Write(expressions.Count);
        foreach (KeyValuePair<string, ExpressionData> expression in expressions)
        {
            writer.Write(expression.Key);
            writer.Write(expression.Value.Count);
            writer.Write(expression.Value.TotalElapsedTicks);
            writer.Write(expression.Value.MinElapsedTicks);
            writer.Write(expression.Value.MaxElapsedTicks);
            writer.Write(expression.Value.Outputs.Count);
            foreach (KeyValuePair<string, long> output in expression.Value.Outputs)
            {
                writer.Write(output.Key);
                writer.Write(output.Value);
            }

            WriteCounts(writer, expression.Value.CallContexts);
            WriteCounts(writer, expression.Value.Locations);
        }
    }

    private static void ReadCounts(BinaryReader reader, Dictionary<string, long> counts)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            counts.Add(reader.ReadString(), reader.ReadInt64());
        }
    }

    private static void WriteCounts(BinaryWriter writer, Dictionary<string, long> counts)
    {
        writer.Write(counts.Count);
        foreach (KeyValuePair<string, long> count in counts)
        {
            writer.Write(count.Key);
            writer.Write(count.Value);
        }
    }

    private static void ReadTransforms(BinaryReader reader, Dictionary<string, TransformData> transforms)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            string transform = reader.ReadString();
            string kind = reader.ReadString();
            string[] inputs = ReadValues(reader);
            string[] outputs = ReadValues(reader);
            TransformData transformData = new(transform, kind, inputs, outputs)
            {
                Count = reader.ReadInt64(),
                ElapsedTicks = reader.ReadInt64(),
            };
            transforms.Add(CreateTransformKey(transform, kind, inputs, outputs), transformData);
        }
    }

    private static void WriteTransforms(BinaryWriter writer, Dictionary<string, TransformData> transforms)
    {
        writer.Write(transforms.Count);
        foreach (TransformData transform in transforms.Values)
        {
            writer.Write(transform.Transform);
            writer.Write(transform.Kind);
            WriteValues(writer, transform.Inputs);
            WriteValues(writer, transform.Outputs);
            writer.Write(transform.Count);
            writer.Write(transform.ElapsedTicks);
        }
    }

    private static void ReadPropertyFunctions(BinaryReader reader, Dictionary<string, PropertyFunctionData> functions)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            string typeName = reader.ReadString();
            string methodName = reader.ReadString();
            string[] arguments = ReadValues(reader);
            string dispatch = reader.ReadString();
            PropertyFunctionData function = new(typeName, methodName, arguments, dispatch)
            {
                Count = reader.ReadInt64(),
                ElapsedTicks = reader.ReadInt64(),
            };
            ReadCounts(reader, function.Locations);
            functions.Add(CreatePropertyFunctionKey(typeName, methodName, arguments, dispatch), function);
        }
    }

    private static void WritePropertyFunctions(BinaryWriter writer, Dictionary<string, PropertyFunctionData> functions)
    {
        writer.Write(functions.Count);
        foreach (PropertyFunctionData function in functions.Values)
        {
            writer.Write(function.TypeName);
            writer.Write(function.MethodName);
            WriteValues(writer, function.Arguments);
            writer.Write(function.Dispatch);
            writer.Write(function.Count);
            writer.Write(function.ElapsedTicks);
            WriteCounts(writer, function.Locations);
        }
    }

    private static string[] ReadValues(BinaryReader reader)
    {
        string[] values = new string[reader.ReadInt32()];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = reader.ReadBoolean() ? reader.ReadString() : null;
        }

        return values;
    }

    private static void WriteValues(BinaryWriter writer, string[] values)
    {
        writer.Write(values.Length);
        foreach (string value in values)
        {
            writer.Write(value is not null);
            if (value is not null)
            {
                writer.Write(value);
            }
        }
    }

    private sealed class TelemetryData
    {
        internal long TotalExpansions;
        internal long TotalElapsedTicks;
        internal long MetadataExpansions;
        internal long MetadataElapsedTicks;
        internal long PropertyExpansions;
        internal long PropertyElapsedTicks;
        internal long ItemExpansions;
        internal long ItemElapsedTicks;
        internal readonly Dictionary<string, ExpressionData> Expressions = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> PropertyReferences = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> MetadataReferences = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> ItemVectors = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, TransformData> Transforms = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, PropertyFunctionData> PropertyFunctions = new(StringComparer.Ordinal);

        internal void Merge(TelemetryData other)
        {
            TotalExpansions += other.TotalExpansions;
            TotalElapsedTicks += other.TotalElapsedTicks;
            MetadataExpansions += other.MetadataExpansions;
            MetadataElapsedTicks += other.MetadataElapsedTicks;
            PropertyExpansions += other.PropertyExpansions;
            PropertyElapsedTicks += other.PropertyElapsedTicks;
            ItemExpansions += other.ItemExpansions;
            ItemElapsedTicks += other.ItemElapsedTicks;

            foreach (KeyValuePair<string, ExpressionData> expression in other.Expressions)
            {
                if (!Expressions.TryGetValue(expression.Key, out ExpressionData expressionData))
                {
                    expressionData = new ExpressionData();
                    Expressions.Add(expression.Key, expressionData);
                }

                expressionData.Count += expression.Value.Count;
                expressionData.TotalElapsedTicks += expression.Value.TotalElapsedTicks;
                expressionData.MinElapsedTicks = Math.Min(expressionData.MinElapsedTicks, expression.Value.MinElapsedTicks);
                expressionData.MaxElapsedTicks = Math.Max(expressionData.MaxElapsedTicks, expression.Value.MaxElapsedTicks);
                MergeCounts(expressionData.Outputs, expression.Value.Outputs);
                MergeCounts(expressionData.CallContexts, expression.Value.CallContexts);
                MergeCounts(expressionData.Locations, expression.Value.Locations);
            }

            MergeCounts(PropertyReferences, other.PropertyReferences);
            MergeCounts(MetadataReferences, other.MetadataReferences);
            MergeCounts(ItemVectors, other.ItemVectors);

            foreach (KeyValuePair<string, TransformData> transform in other.Transforms)
            {
                if (!Transforms.TryGetValue(transform.Key, out TransformData transformData))
                {
                    transformData = new TransformData(transform.Value.Transform, transform.Value.Kind, transform.Value.Inputs, transform.Value.Outputs);
                    Transforms.Add(transform.Key, transformData);
                }

                transformData.Count += transform.Value.Count;
                transformData.ElapsedTicks += transform.Value.ElapsedTicks;
            }

            foreach (KeyValuePair<string, PropertyFunctionData> function in other.PropertyFunctions)
            {
                if (!PropertyFunctions.TryGetValue(function.Key, out PropertyFunctionData functionData))
                {
                    functionData = new PropertyFunctionData(
                        function.Value.TypeName,
                        function.Value.MethodName,
                        function.Value.Arguments,
                        function.Value.Dispatch);
                    PropertyFunctions.Add(function.Key, functionData);
                }

                functionData.Count += function.Value.Count;
                functionData.ElapsedTicks += function.Value.ElapsedTicks;
                MergeCounts(functionData.Locations, function.Value.Locations);
            }
        }

        private static void MergeCounts(Dictionary<string, long> destination, Dictionary<string, long> source)
        {
            foreach (KeyValuePair<string, long> entry in source)
            {
                destination.TryGetValue(entry.Key, out long count);
                destination[entry.Key] = count + entry.Value;
            }
        }
    }

    private sealed class ExpressionData
    {
        internal long Count;
        internal long TotalElapsedTicks;
        internal long MinElapsedTicks = long.MaxValue;
        internal long MaxElapsedTicks;
        internal readonly Dictionary<string, long> Outputs = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> CallContexts = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, long> Locations = new(StringComparer.Ordinal);
    }

    private sealed class TransformData(string transform, string kind, string[] inputs, string[] outputs)
    {
        internal string Transform { get; } = transform;
        internal string Kind { get; } = kind;
        internal string[] Inputs { get; } = inputs;
        internal string[] Outputs { get; } = outputs;
        internal long Count;
        internal long ElapsedTicks;
    }

    private sealed class TransformKindSummary
    {
        internal long Count;
        internal long ElapsedTicks;
    }

    private sealed class PropertyFunctionData(
        string typeName,
        string methodName,
        string[] arguments,
        string dispatch)
    {
        internal string TypeName { get; } = typeName;
        internal string MethodName { get; } = methodName;
        internal string[] Arguments { get; } = arguments;
        internal string Dispatch { get; } = dispatch;
        internal readonly Dictionary<string, long> Locations = new(StringComparer.Ordinal);
        internal long Count;
        internal long ElapsedTicks;
    }

    private readonly struct DetailReportInfo(string path, long size, long lineCount)
    {
        internal string Path { get; } = path;
        internal long Size { get; } = size;
        internal long LineCount { get; } = lineCount;
    }

    private sealed class LineCountingTextWriter(string path) : TextWriter
    {
        private readonly StreamWriter _writer = CreateReportWriter(path);

        public override Encoding Encoding => _writer.Encoding;

        internal long LineCount { get; private set; }

        public override void WriteLine()
        {
            _writer.WriteLine();
            LineCount++;
        }

        public override void WriteLine(string value)
        {
            _writer.WriteLine(value);
            LineCount++;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _writer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
