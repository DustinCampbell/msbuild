// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.NET.StringTools;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class implements static methods to assist with unescaping of %XX codes
    /// in the MSBuild file format.
    /// </summary>
    /// <remarks>
    /// PERF: since we escape and unescape relatively frequently, it may be worth caching
    /// the last N strings that were (un)escaped
    /// </remarks>
    internal static class EscapingUtilities
    {
        private sealed record class CallerInfo
        {
            private int? _hashCode;

            public string MemberName { get; set; }
            public string FilePath { get; set; }
            public int LineNumber { get; set; }

            public string DisplayString => field ??= $"{FilePath}:{LineNumber} ({MemberName})";

            public bool Equals(CallerInfo other)
                => LineNumber == other.LineNumber &&
                   MemberName == other.MemberName &&
                   FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase);

            public override int GetHashCode()
                => _hashCode ??= MemberName.GetHashCode() ^ StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath) ^ LineNumber;

            public override string ToString() => DisplayString;
        }

        private sealed class EscapeCallStats(string input, bool cache, CallerInfo caller)
        {
            public string Input => input;

            public bool Cache => cache;

            public CallerInfo Caller => caller;

            public bool NoSpecialChars { get; set; }

            public bool UsedCachedOutput { get; set; }

            public string Output { get; set; }

            private bool? _hasChange;

            public bool HasChange => _hasChange ??= !string.Equals(input, Output, StringComparison.Ordinal);
        }

        private sealed class UnescapeAllCallStats(string input, bool trim, CallerInfo caller)
        {
            public string Input => input;

            public bool Trim => trim;

            public CallerInfo Caller => caller;

            public string Output { get; set; }

            private bool? _hasChange;

            public bool HasChange => _hasChange ??= !string.Equals(input, Output, StringComparison.Ordinal);
        }

        /// <summary>
        /// Optional cache of escaped strings for use when needing to escape in performance-critical scenarios with significant
        /// expected string reuse.
        /// </summary>
        private static readonly Dictionary<string, string> s_unescapedToEscapedStrings = new Dictionary<string, string>(StringComparer.Ordinal);

        // Invocation counters
        private static long s_escapeCallCount = 0;
        private static long s_unescapeAllCallCount = 0;
        private static long s_containsEscapedWildcardsCallCount = 0;

        // Cache tracking for Escape
        private static long s_escapeCacheHits = 0;
        private static long s_escapeCacheMisses = 0;

        // Telemetry dictionaries - using ConcurrentDictionary and ConcurrentBag for lock-free thread safety
        private static readonly ConcurrentDictionary<string, ConcurrentBag<EscapeCallStats>> s_escapeCallStats = new ConcurrentDictionary<string, ConcurrentBag<EscapeCallStats>>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, ConcurrentBag<UnescapeAllCallStats>> s_unescapeCallStats = new ConcurrentDictionary<string, ConcurrentBag<UnescapeAllCallStats>>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, long> s_containsEscapedWildcardsInputFrequency = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        private static long s_containsEscapedWildcardsTrueFrequency = 0;
        private static long s_containsEscapedWildcardsFalseFrequency = 0;

        // Caller tracking dictionaries
        private static readonly ConcurrentDictionary<CallerInfo, long> s_escapeCallerFrequency = new ConcurrentDictionary<CallerInfo, long>();
        private static readonly ConcurrentDictionary<CallerInfo, long> s_unescapeCallerFrequency = new ConcurrentDictionary<CallerInfo, long>();
        private static readonly ConcurrentDictionary<CallerInfo, long> s_containsEscapedWildcardsCallerFrequency = new ConcurrentDictionary<CallerInfo, long>();

        /// <summary>
        /// Gets the current process ID in a cross-platform way.
        /// </summary>
        private static void GetCurrentAssemblyNameAndProcessId(out string assemblyName, out int processId)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var process = Process.GetCurrentProcess();

            assemblyName = assembly.GetName().Name;
            processId = process.Id;
        }

        /// <summary>
        /// Helper to format top N items from a frequency dictionary.
        /// </summary>
        private static string FormatTopFrequencies<TKey>(IEnumerable<KeyValuePair<TKey, long>> frequencies, int topN = 20)
            where TKey : notnull
        {
            if (!frequencies.Any())
            {
                return "    (none)\n";
            }

            var sb = new StringBuilder();
            var sorted = frequencies.OrderByDescending(kvp => kvp.Value).Take(topN);
            int rank = 1;

            foreach (var kvp in sorted)
            {
                string key = kvp.Key.ToString();
                long value = kvp.Value;

                string displayValue = key.Length > 256 ? key.Substring(0, 253) + "..." : key;
                displayValue = displayValue.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
                sb.AppendLine($"    {rank,3}. [{value,8:N0}x] {displayValue}");
                rank++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes the invocation counters to a file.
        /// </summary>
        public static void WriteCountersToFile()
        {
            GetCurrentAssemblyNameAndProcessId(out string assemblyName, out int processId);

            DateTime timestamp = DateTime.Now;

            var sb = new StringBuilder();

            sb.AppendLine("MSBuild EscapingUtilities Invocation Counters");
            sb.AppendLine("============================================");
            sb.AppendLine($"Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Assembly Name: {assemblyName}");
            sb.AppendLine($"Process ID: {processId}");
            sb.AppendLine();

            // Basic call counts
            long escapeCount = Interlocked.Read(ref s_escapeCallCount);
            long unescapeCount = Interlocked.Read(ref s_unescapeAllCallCount);
            long containsWildcardsCount = Interlocked.Read(ref s_containsEscapedWildcardsCallCount);
            long totalCalls = escapeCount + unescapeCount + containsWildcardsCount;

            sb.AppendLine("CALL COUNTS");
            sb.AppendLine("===========");
            sb.AppendLine($"Escape calls:                    {escapeCount,12:N0}");
            sb.AppendLine($"UnescapeAll calls:               {unescapeCount,12:N0}");
            sb.AppendLine($"ContainsEscapedWildcards calls:  {containsWildcardsCount,12:N0}");
            sb.AppendLine($"Total calls:                     {totalCalls,12:N0}");
            sb.AppendLine();

            // Escape statistics
            sb.AppendLine("ESCAPE STATISTICS");
            sb.AppendLine("=================");

            var escapeCalls = new List<EscapeCallStats>();
            var escapeInputs = new HashSet<string>(StringComparer.Ordinal);
            var escapeOutputs = new HashSet<string>(StringComparer.Ordinal);

            foreach (var kvp in s_escapeCallStats.ToArray())
            {
                foreach (var stats in kvp.Value.ToArray())
                {
                    escapeCalls.Add(stats);
                    escapeInputs.Add(stats.Input);
                    escapeOutputs.Add(stats.Output);
                }
            }

            long escapeCacheHits = Interlocked.Read(ref s_escapeCacheHits);
            long escapeCacheMisses = Interlocked.Read(ref s_escapeCacheMisses);
            long escapeCacheTotal = escapeCacheHits + escapeCacheMisses;
            double escapeCacheHitRate = escapeCacheTotal > 0 ? (escapeCacheHits * 100.0 / escapeCacheTotal) : 0;

            sb.AppendLine($"Unique input strings:            {escapeInputs.Count,12:N0}");
            sb.AppendLine($"Unique output strings:           {escapeOutputs.Count,12:N0}");
            sb.AppendLine($"Cache hits:                      {escapeCacheHits,12:N0}");
            sb.AppendLine($"Cache misses:                    {escapeCacheMisses,12:N0}");
            sb.AppendLine($"Cache hit rate:                  {escapeCacheHitRate,11:F2}%");
            sb.AppendLine();

            // Count how many times the same result is produced
            long noChangeCount = escapeCalls.Count(stats => !stats.HasChange);

            sb.AppendLine($"Calls where input == output:     {noChangeCount,12:N0} ({(escapeCount > 0 ? noChangeCount * 100.0 / escapeCount : 0):F2}%)");
            sb.AppendLine();

            sb.AppendLine("Top 100 Most Frequent Escape Inputs:");
            sb.Append(FormatTopFrequencies(s_escapeCallStats.ToArray().Select(kvp => new KeyValuePair<string, long>(kvp.Key, kvp.Value.Count)), 100));
            sb.AppendLine();

            sb.AppendLine("Top 50 Most Frequent Escape Callers:");
            sb.Append(FormatTopFrequencies(s_escapeCallerFrequency.ToArray(), 50));
            sb.AppendLine();

            // UnescapeAll statistics
            sb.AppendLine("UNESCAPE STATISTICS");
            sb.AppendLine("===================");

            var unescapeCalls = new List<UnescapeAllCallStats>();
            var unescapeInputs = new HashSet<string>(StringComparer.Ordinal);
            var unescapeOutputs = new HashSet<string>(StringComparer.Ordinal);

            foreach (var kvp in s_unescapeCallStats.ToArray())
            {
                foreach (var stats in kvp.Value.ToArray())
                {
                    unescapeCalls.Add(stats);
                    unescapeInputs.Add(stats.Input);
                    unescapeOutputs.Add(stats.Output);
                }
            }

            sb.AppendLine($"Unique input strings:            {unescapeInputs.Count,12:N0}");
            sb.AppendLine($"Unique output strings:           {unescapeOutputs.Count,12:N0}");
            sb.AppendLine();

            // Count how many times the same result is produced
            long unescapeNoChangeCount = unescapeCalls.Count(stats => !stats.HasChange);

            sb.AppendLine($"Calls where input == output:     {unescapeNoChangeCount,12:N0} ({(unescapeCount > 0 ? unescapeNoChangeCount * 100.0 / unescapeCount : 0):F2}%)");
            sb.AppendLine();

            sb.AppendLine("Top 100 Most Frequent UnescapeAll Inputs:");
            sb.Append(FormatTopFrequencies(s_unescapeCallStats.ToArray().Select(kvp => new KeyValuePair<string, long>(kvp.Key, kvp.Value.Count)), 100));
            sb.AppendLine();

            sb.AppendLine("Top 50 Most Frequent UnescapeAll Callers:");
            sb.Append(FormatTopFrequencies(s_unescapeCallerFrequency.ToArray(), 50));
            sb.AppendLine();

            // ContainsEscapedWildcards statistics
            sb.AppendLine("CONTAINS ESCAPED WILDCARDS STATISTICS");
            sb.AppendLine("=====================================");

            var wildcardsInputs = s_containsEscapedWildcardsInputFrequency.ToArray();
            long wildcardsTrueResults = Interlocked.Read(ref s_containsEscapedWildcardsTrueFrequency);
            long wildcardsFalseResults = Interlocked.Read(ref s_containsEscapedWildcardsFalseFrequency);

            sb.AppendLine($"Unique input strings:            {wildcardsInputs.Length,12:N0}");
            sb.AppendLine($"Returned true:                   {wildcardsTrueResults,12:N0} ({(containsWildcardsCount > 0 ? wildcardsTrueResults * 100.0 / containsWildcardsCount : 0):F2}%)");
            sb.AppendLine($"Returned false:                  {wildcardsFalseResults,12:N0} ({(containsWildcardsCount > 0 ? wildcardsFalseResults * 100.0 / containsWildcardsCount : 0):F2}%)");
            sb.AppendLine();

            sb.AppendLine("Top 20 Most Frequent ContainsEscapedWildcards Inputs:");
            sb.Append(FormatTopFrequencies(wildcardsInputs));
            sb.AppendLine();

            sb.AppendLine("Top 50 Most Frequent ContainsEscapedWildcards Callers:");
            sb.Append(FormatTopFrequencies(s_containsEscapedWildcardsCallerFrequency.ToArray(), 50));

            string fileName = $"MSBuild_EscapingUtilities_Counters_{timestamp:yyyyMMdd_HHmmss}_{assemblyName}_{processId}.txt";
            string outputPath = Path.Combine(Path.GetTempPath(), fileName);

            int counter = 0;

            while (File.Exists(outputPath))
            {
                counter++;
                fileName = $"MSBuild_EscapingUtilities_Counters_{timestamp:yyyyMMdd_HHmmss}_{assemblyName}_{processId}_{counter}.txt";
                outputPath = Path.Combine(Path.GetTempPath(), fileName);
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        private static bool TryDecodeHexDigit(char character, out int value)
        {
            if (character >= '0' && character <= '9')
            {
                value = character - '0';
                return true;
            }
            if (character >= 'A' && character <= 'F')
            {
                value = character - 'A' + 10;
                return true;
            }
            if (character >= 'a' && character <= 'f')
            {
                value = character - 'a' + 10;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Replaces all instances of %XX in the input string with the character represented
        /// by the hexadecimal number XX.
        /// </summary>
        /// <param name="escapedString">The string to unescape.</param>
        /// <param name="trim">If the string should be trimmed before being unescaped.</param>
        /// <param name="callerMemberName">Automatically populated with the caller's member name.</param>
        /// <param name="callerFilePath">Automatically populated with the caller's file path.</param>
        /// <param name="callerLineNumber">Automatically populated with the caller's line number.</param>
        /// <returns>unescaped string</returns>
        internal static string UnescapeAll(
            string escapedString,
            bool trim = false,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            Interlocked.Increment(ref s_unescapeAllCallCount);

            // Track caller frequency
            CallerInfo caller = new CallerInfo
            {
                MemberName = callerMemberName,
                FilePath = callerFilePath,
                LineNumber = callerLineNumber
            };

            TrackUnescapeCaller(caller);

            // Track input frequency
            UnescapeAllCallStats stats = CreateUnescapeAllCallStats(escapedString, trim, caller);

            // If the string doesn't contain anything, then by definition it doesn't
            // need unescaping.
            if (String.IsNullOrEmpty(escapedString))
            {
                stats.Output = escapedString;
                return escapedString;
            }

            // If there are no percent signs, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            int indexOfPercent = escapedString.IndexOf('%');
            if (indexOfPercent == -1)
            {
                string result = trim ? escapedString.Trim() : escapedString;
                stats.Output = result;
                return result;
            }

            // This is where we're going to build up the final string to return to the caller.
            StringBuilder unescapedString = StringBuilderCache.Acquire(escapedString.Length);

            int currentPosition = 0;
            int escapedStringLength = escapedString.Length;
            if (trim)
            {
                while (currentPosition < escapedString.Length && Char.IsWhiteSpace(escapedString[currentPosition]))
                {
                    currentPosition++;
                }
                if (currentPosition == escapedString.Length)
                {
                    stats.Output = string.Empty;
                    return String.Empty;
                }
                while (Char.IsWhiteSpace(escapedString[escapedStringLength - 1]))
                {
                    escapedStringLength--;
                }
            }

            // Loop until there are no more percent signs in the input string.
            while (indexOfPercent != -1)
            {
                // There must be two hex characters following the percent sign
                // for us to even consider doing anything with this.
                if (
                        (indexOfPercent <= (escapedStringLength - 3)) &&
                        TryDecodeHexDigit(escapedString[indexOfPercent + 1], out int digit1) &&
                        TryDecodeHexDigit(escapedString[indexOfPercent + 2], out int digit2))
                {
                    // First copy all the characters up to the current percent sign into
                    // the destination.
                    unescapedString.Append(escapedString, currentPosition, indexOfPercent - currentPosition);

                    // Convert the %XX to an actual real character.
                    char unescapedCharacter = (char)((digit1 << 4) + digit2);

                    // if the unescaped character is not on the exception list, append it
                    unescapedString.Append(unescapedCharacter);

                    // Advance the current pointer to reflect the fact that the destination string
                    // is up to date with everything up to and including this escape code we just found.
                    currentPosition = indexOfPercent + 3;
                }

                // Find the next percent sign.
                indexOfPercent = escapedString.IndexOf('%', indexOfPercent + 1);
            }

            // Okay, there are no more percent signs in the input string, so just copy the remaining
            // characters into the destination.
            unescapedString.Append(escapedString, currentPosition, escapedStringLength - currentPosition);

            string finalResult = StringBuilderCache.GetStringAndRelease(unescapedString);
            stats.Output = finalResult;
            return finalResult;
        }

        private static void TrackUnescapeCaller(CallerInfo caller)
        {
            s_unescapeCallerFrequency.AddOrUpdate(caller, 1, (_, count) => count + 1);
        }

        private static UnescapeAllCallStats CreateUnescapeAllCallStats(string input, bool trim, CallerInfo caller)
        {
            UnescapeAllCallStats stats = null;

            if (input != null)
            {
                var statsList = s_unescapeCallStats.GetOrAdd(input, _ => new ConcurrentBag<UnescapeAllCallStats>());
                stats = new UnescapeAllCallStats(input, trim, caller);
                statsList.Add(stats);
            }

            return stats;
        }

        /// <summary>
        /// Adds instances of %XX in the input string where the char to be escaped appears
        /// XX is the hex value of the ASCII code for the char.  Interns and caches the result.
        /// </summary>
        /// <comment>
        /// NOTE:  Only recommended for use in scenarios where there's expected to be significant
        /// repetition of the escaped string.  Cache currently grows unbounded.
        /// </comment>
        internal static string EscapeWithCaching(
            string unescapedString,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            Interlocked.Increment(ref s_escapeCallCount);

            // Track caller frequency
            CallerInfo caller = new CallerInfo
            {
                MemberName = callerMemberName,
                FilePath = callerFilePath,
                LineNumber = callerLineNumber
            };

            TrackEscapeCaller(caller);

            EscapeCallStats stats = CreateEscapeCallStats(unescapedString, cache: true, caller);

            // If there are no special chars, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            if (String.IsNullOrEmpty(unescapedString))
            {
                return unescapedString;
            }

            string result = EscapeWithOptionalCaching(unescapedString, cache: true, stats);

            // Track output frequency
            if (result != null)
            {
                stats.Output = result;
            }

            return result;
        }

        /// <summary>
        /// Adds instances of %XX in the input string where the char to be escaped appears
        /// XX is the hex value of the ASCII code for the char.
        /// </summary>
        /// <param name="unescapedString">The string to escape.</param>
        /// <param name="callerMemberName">Automatically populated with the caller's member name.</param>
        /// <param name="callerFilePath">Automatically populated with the caller's file path.</param>
        /// <param name="callerLineNumber">Automatically populated with the caller's line number.</param>
        /// <returns>escaped string</returns>
        internal static string Escape(
            string unescapedString,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            Interlocked.Increment(ref s_escapeCallCount);

            // Track caller frequency
            CallerInfo caller = new CallerInfo
            {
                MemberName = callerMemberName,
                FilePath = callerFilePath,
                LineNumber = callerLineNumber
            };

            TrackEscapeCaller(caller);

            // Track input frequency
            EscapeCallStats stats = CreateEscapeCallStats(unescapedString, cache: false, caller);

            // If there are no special chars, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            if (String.IsNullOrEmpty(unescapedString))
            {
                return unescapedString;
            }

            string result = EscapeWithOptionalCaching(unescapedString, cache: false, stats);

            // Track output frequency
            if (result != null)
            {
                stats.Output = result;
            }

            return result;
        }

        /// <summary>
        /// Adds instances of %XX in the input string where the char to be escaped appears
        /// XX is the hex value of the ASCII code for the char.  Caches if requested.
        /// </summary>
        /// <param name="unescapedString">The string to escape.</param>
        /// <param name="cache">
        /// True if the cache should be checked, and if the resultant string
        /// should be cached.
        /// </param>
        /// <param name="stats"></param>
        private static string EscapeWithOptionalCaching(string unescapedString, bool cache, EscapeCallStats stats)
        {
            // If there are no special chars, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            if (!ContainsReservedCharacters(unescapedString))
            {
                stats.NoSpecialChars = true;
                return unescapedString;
            }

            // next, if we're caching, check to see if it's already there.
            if (cache)
            {
                lock (s_unescapedToEscapedStrings)
                {
                    string cachedEscapedString;
                    if (s_unescapedToEscapedStrings.TryGetValue(unescapedString, out cachedEscapedString))
                    {
                        stats.UsedCachedOutput = true;
                        return cachedEscapedString;
                    }
                }
            }

            // This is where we're going to build up the final string to return to the caller.
            StringBuilder escapedStringBuilder = StringBuilderCache.Acquire(unescapedString.Length * 2);

            AppendEscapedString(escapedStringBuilder, unescapedString);

            if (!cache)
            {
                return StringBuilderCache.GetStringAndRelease(escapedStringBuilder);
            }

            string escapedString = Strings.WeakIntern(escapedStringBuilder.ToString());
            StringBuilderCache.Release(escapedStringBuilder);

            lock (s_unescapedToEscapedStrings)
            {
                s_unescapedToEscapedStrings[unescapedString] = escapedString;
            }

            return escapedString;
        }

        private static void TrackEscapeCaller(CallerInfo caller)
        {
            s_escapeCallerFrequency.AddOrUpdate(caller, 1, (_, count) => count + 1);
        }

        private static EscapeCallStats CreateEscapeCallStats(string input, bool cache, CallerInfo caller)
        {
            EscapeCallStats stats = null;

            if (input != null)
            {
                var statsList = s_escapeCallStats.GetOrAdd(input, _ => new ConcurrentBag<EscapeCallStats>());
                stats = new EscapeCallStats(input, cache, caller);
                statsList.Add(stats);
            }

            return stats;
        }

        /// <summary>
        /// Before trying to actually escape the string, it can be useful to call this method to determine
        /// if escaping is necessary at all.  This can save lots of calls to copy around item metadata
        /// that is really the same whether escaped or not.
        /// </summary>
        /// <param name="unescapedString"></param>
        /// <returns></returns>
        private static bool ContainsReservedCharacters(
            string unescapedString)
        {
            return -1 != unescapedString.IndexOfAny(s_charsToEscape);
        }

        /// <summary>
        /// Determines whether the string contains the escaped form of '*' or '?'.
        /// </summary>
        /// <param name="escapedString"></param>
        /// <param name="callerMemberName">Automatically populated with the caller's member name.</param>
        /// <param name="callerFilePath">Automatically populated with the caller's file path.</param>
        /// <param name="callerLineNumber">Automatically populated with the caller's line number.</param>
        /// <returns></returns>
        internal static bool ContainsEscapedWildcards(
            string escapedString,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            Interlocked.Increment(ref s_containsEscapedWildcardsCallCount);

            // Track caller frequency
            CallerInfo caller = new CallerInfo
            {
                MemberName = callerMemberName,
                FilePath = callerFilePath,
                LineNumber = callerLineNumber
            };

            TrackContainsEscapedWildcardsCaller(caller);

            // Track input frequency
            if (escapedString != null)
            {
                s_containsEscapedWildcardsInputFrequency.AddOrUpdate(escapedString, 1, (_, count) => count + 1);
            }

            if (escapedString.Length < 3)
            {
                TrackContainsEscapedWildcardsResult(false);
                return false;
            }
            // Look for the first %. We know that it has to be followed by at least two more characters so we subtract 2
            // from the length to search.
            int index = escapedString.IndexOf('%', 0, escapedString.Length - 2);
            while (index != -1)
            {
                if (escapedString[index + 1] == '2' && (escapedString[index + 2] == 'a' || escapedString[index + 2] == 'A'))
                {
                    // %2a or %2A
                    TrackContainsEscapedWildcardsResult(true);
                    return true;
                }
                if (escapedString[index + 1] == '3' && (escapedString[index + 2] == 'f' || escapedString[index + 2] == 'F'))
                {
                    // %3f or %3F
                    TrackContainsEscapedWildcardsResult(true);
                    return true;
                }
                // Continue searching for % starting at (index + 1). We know that it has to be followed by at least two
                // more characters so we subtract 2 from the length of the substring to search.
                index = escapedString.IndexOf('%', index + 1, escapedString.Length - (index + 1) - 2);
            }
            TrackContainsEscapedWildcardsResult(false);
            return false;
        }

        private static void TrackContainsEscapedWildcardsCaller(CallerInfo caller)
        {
            s_containsEscapedWildcardsCallerFrequency.AddOrUpdate(caller, 1, (_, count) => count + 1);
        }

        private static void TrackContainsEscapedWildcardsResult(bool result)
        {
            if (result)
            {
                Interlocked.Increment(ref s_containsEscapedWildcardsTrueFrequency);
            }
            else
            {
                Interlocked.Increment(ref s_containsEscapedWildcardsFalseFrequency);
            }
        }

        /// <summary>
        /// Convert the given integer into its hexadecimal representation.
        /// </summary>
        /// <param name="x">The number to convert, which must be non-negative and less than 16</param>
        /// <returns>The character which is the hexadecimal representation of <paramref name="x"/>.</returns>
        private static char HexDigitChar(int x)
        {
            return (char)(x + (x < 10 ? '0' : ('a' - 10)));
        }

        /// <summary>
        /// Append the escaped version of the given character to a <see cref="StringBuilder"/>.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to which to append.</param>
        /// <param name="ch">The character to escape.</param>
        private static void AppendEscapedChar(StringBuilder sb, char ch)
        {
            // Append the escaped version which is a percent sign followed by two hexadecimal digits
            sb.Append('%');
            sb.Append(HexDigitChar(ch / 0x10));
            sb.Append(HexDigitChar(ch & 0x0F));
        }

        /// <summary>
        /// Append the escaped version of the given string to a <see cref="StringBuilder"/>.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to which to append.</param>
        /// <param name="unescapedString">The unescaped string.</param>
        private static void AppendEscapedString(StringBuilder sb, string unescapedString)
        {
            // Replace each unescaped special character with an escape sequence one
            for (int idx = 0; ;)
            {
                int nextIdx = unescapedString.IndexOfAny(s_charsToEscape, idx);
                if (nextIdx == -1)
                {
                    sb.Append(unescapedString, idx, unescapedString.Length - idx);
                    break;
                }

                sb.Append(unescapedString, idx, nextIdx - idx);
                AppendEscapedChar(sb, unescapedString[nextIdx]);
                idx = nextIdx + 1;
            }
        }

        /// <summary>
        /// Special characters that need escaping.
        /// It's VERY important that the percent character is the FIRST on the list - since it's both a character
        /// we escape and use in escape sequences, we can unintentionally escape other escape sequences if we
        /// don't process it first. Of course we'll have a similar problem if we ever decide to escape hex digits
        /// (that would require rewriting the algorithm) but since it seems unlikely that we ever do, this should
        /// be good enough to avoid complicating the algorithm at this point.
        /// </summary>
        private static readonly char[] s_charsToEscape = { '%', '*', '?', '@', '$', '(', ')', ';', '\'' };
    }
}
