// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.Collections;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Collects ordinary Remove glob patterns in declaration order for one item type.
    /// </summary>
    /// <remarks>
    ///  Pattern storage is append-only and segmented. Computed pattern sets are independently owned,
    ///  treated as read-only, and never extended when more operations are collected.
    /// </remarks>
    private sealed class RemoveGlobPatternSequence
    {
        private readonly SegmentedList<RemovedGlob> _patterns = [];
        private Dictionary<(int Start, int End), (HashSet<string> Patterns, int HashCode)>? _retainedRanges;
        private HashSet<string>? _lastPatterns;
        private int _lastStart;
        private int _lastEnd;
        private int? _lastHashCode;

        /// <summary>
        ///  Gets the number of cached pattern ranges.
        /// </summary>
        public int CachedRangeCount => (_retainedRanges?.Count ?? 0) + (_lastPatterns is null ? 0 : 1);

        /// <summary>
        ///  Collects one pattern with the number of operations through its Remove declaration.
        /// </summary>
        /// <param name="pattern">The parsed, unescaped glob pattern.</param>
        /// <param name="operationCount">The position immediately after the Remove operation.</param>
        public void Add(string pattern, int operationCount)
            => _patterns.Add(new(pattern, operationCount));

        /// <summary>
        ///  Finds the exclusive pattern boundary for a requested operation sequence.
        /// </summary>
        /// <param name="operationCount">The number of operations visible to the request.</param>
        /// <returns>
        ///  The number of patterns declared within those operations, excluding all later declarations.
        /// </returns>
        public int GetPatternCount(int operationCount)
        {
            int end = _patterns.Count;
            if (end == 0 || _patterns[^1].OperationCount <= operationCount)
            {
                return end;
            }

            int start = 0;
            while (start < end)
            {
                int middle = start + ((end - start) / 2);
                if (_patterns[middle].OperationCount <= operationCount)
                {
                    start = middle + 1;
                }
                else
                {
                    end = middle;
                }
            }

            return start;
        }

        /// <summary>
        ///  Extends a backward-planning range to include this operation's patterns.
        /// </summary>
        /// <param name="start">The range's current starting boundary.</param>
        /// <param name="operationCount">The position immediately after the operation being visited.</param>
        /// <returns>
        ///  The starting boundary including all patterns from that operation.
        /// </returns>
        public int IncludePatternsFromOperation(int start, int operationCount)
        {
            while (start > 0 && _patterns[start - 1].OperationCount == operationCount)
            {
                start--;
            }

            return start;
        }

        /// <summary>
        ///  Removes this operation's patterns from a forward-evaluation range before applying it.
        /// </summary>
        /// <param name="start">The range's current starting boundary.</param>
        /// <param name="end">The requested sequence's exclusive pattern boundary.</param>
        /// <param name="operationCount">The position immediately after the operation being applied.</param>
        /// <returns>
        ///  The starting boundary containing only patterns from later operations.
        /// </returns>
        public int SkipPatternsFromOperation(int start, int end, int operationCount)
        {
            while (start < end && _patterns[start].OperationCount == operationCount)
            {
                start++;
            }

            return start;
        }

        /// <summary>
        ///  Identifies a bounded range of later Remove glob patterns without building its set.
        /// </summary>
        /// <param name="start">The inclusive pattern boundary.</param>
        /// <param name="end">The exclusive pattern boundary.</param>
        /// <returns>
        ///  The later Remove glob patterns in the range, or an empty range.
        /// </returns>
        public RemoveGlobPatterns GetRange(int start, int end)
        {
            Debug.Assert(start >= 0 && start <= end && end <= _patterns.Count);
            return start == end ? default : new(this, start, end);
        }

        /// <summary>
        ///  Gets a range's patterns without retaining every temporary set.
        /// </summary>
        /// <param name="start">The inclusive pattern boundary.</param>
        /// <param name="end">The exclusive pattern boundary.</param>
        /// <returns>
        ///  A privately owned pattern set that callers must not mutate.
        /// </returns>
        public HashSet<string> GetPatterns(int start, int end)
        {
            if (_retainedRanges is not null && _retainedRanges.TryGetValue((start, end), out var existing))
            {
                return existing.Patterns;
            }

            if (_lastPatterns is not null && _lastStart == start && _lastEnd == end)
            {
                return _lastPatterns;
            }

            HashSet<string> patterns = new(FileUtilities.PathComparer);
            for (int i = start; i < end; i++)
            {
                patterns.Add(_patterns[i].Pattern);
            }

            // Glob expansion and cache probes may request ranges that never become stored cache keys.
            _lastPatterns = patterns;
            _lastStart = start;
            _lastEnd = end;
            _lastHashCode = null;
            return patterns;
        }

        /// <summary>
        ///  Gets the stable content and order-independent hash without permanently retaining a queried range.
        /// </summary>
        /// <param name="start">The inclusive pattern boundary.</param>
        /// <param name="end">The exclusive pattern boundary.</param>
        /// <returns>
        ///  A memoized set and hash, retained temporarily unless admitted to the item cache.
        ///  The returned set must never be mutated.
        /// </returns>
        public (HashSet<string> Patterns, int HashCode) GetPatternsAndHash(int start, int end)
        {
            if (_retainedRanges is not null && _retainedRanges.TryGetValue((start, end), out var existing))
            {
                return existing;
            }

            HashSet<string> patterns = GetPatterns(start, end);
            if (_lastHashCode is not int hashCode)
            {
                hashCode = patterns.Count;
                foreach (string pattern in patterns)
                {
                    hashCode ^= FileUtilities.PathComparer.GetHashCode(pattern);
                }

                _lastHashCode = hashCode;
            }

            return (patterns, hashCode);
        }

        /// <summary>
        ///  Permanently retains a range after a new item-cache key has been stored.
        /// </summary>
        /// <param name="start">The inclusive pattern boundary.</param>
        /// <param name="end">The exclusive pattern boundary.</param>
        public void RetainRange(int start, int end)
        {
            var result = GetPatternsAndHash(start, end);
            _retainedRanges ??= [];
            _retainedRanges[(start, end)] = result;
            if (ReferenceEquals(_lastPatterns, result.Patterns))
            {
                _lastPatterns = null;
                _lastHashCode = null;
            }
        }

        private readonly record struct RemovedGlob(string Pattern, int OperationCount);
    }
}
