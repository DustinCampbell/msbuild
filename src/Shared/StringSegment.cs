// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Shared;

/// <summary>
/// Represents a contiguous region of a string without allocating a new string.
/// This is a lightweight, allocation-free alternative to Substring for performance-critical code.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
internal readonly struct StringSegment : IEquatable<StringSegment>, IEquatable<string>
{
    public static StringSegment Empty => default;

    private readonly ReadOnlyMemory<char> _memory;

    /// <summary>
    /// Gets the underlying memory.
    /// </summary>
    public ReadOnlyMemory<char> Memory => _memory;

    /// <summary>
    /// Gets the length of this segment.
    /// </summary>
    public int Length => _memory.Length;

    /// <summary>
    /// Gets whether this segment is empty (length is zero).
    /// </summary>
    public bool IsEmpty => _memory.IsEmpty;

    /// <summary>
    /// Gets the character at the specified index within this segment.
    /// </summary>
    public char this[int index]
    {
        get
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(index >= 0 && index < _memory.Length);

            return _memory.Span[index];
        }
    }

    /// <summary>
    /// Creates a new StringSegment from the entire string.
    /// </summary>
    public StringSegment(string? str)
    {
        _memory = str.AsMemory();
    }

    /// <summary>
    /// Creates a new StringSegment from a portion of a string.
    /// </summary>
    public StringSegment(string str, int offset, int length)
    {
        ErrorUtilities.VerifyThrowArgumentNull(str);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(offset >= 0 && offset <= str.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(length >= 0 && offset + length <= str.Length);

        _memory = str.AsMemory(offset, length);
    }

    /// <summary>
    /// Creates a new StringSegment from a ReadOnlyMemory.
    /// </summary>
    private StringSegment(ReadOnlyMemory<char> memory)
    {
        _memory = memory;
    }

    /// <summary>
    /// Checks if this segment equals the specified object.
    /// </summary>
    public override bool Equals(object? obj)
        => obj is StringSegment segment
            ? Equals(segment)
            : obj is string str && Equals(str);

    /// <summary>
    /// Checks if this segment equals the specified string.
    /// </summary>
    public bool Equals(string? other)
        => other == null
            ? IsEmpty
            : _memory.Span.SequenceEqual(other.AsSpan());

    /// <summary>
    /// Checks if this segment equals another segment.
    /// </summary>
    public bool Equals(StringSegment other)
        => _memory.Span.SequenceEqual(other._memory.Span);

    /// <summary>
    /// Checks if this segment equals the specified string using the specified comparison type.
    /// </summary>
    public bool Equals(string? other, StringComparison comparisonType)
        => other == null
            ? IsEmpty
            : _memory.Span.Equals(other.AsSpan(), comparisonType);

    public static bool Equals(StringSegment a, StringSegment b, StringComparison comparisonType)
    {
        if (a.IsEmpty)
        {
            return b.IsEmpty;
        }

        if (b.IsEmpty)
        {
            return a.IsEmpty;
        }

        return comparisonType is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase
            ? a._memory.Span.Equals(b._memory.Span, comparisonType)
            : a.ToString().Equals(b.ToString(), comparisonType);
    }

    /// <summary>
    /// Returns the hash code for this segment.
    /// </summary>
    public override int GetHashCode()
    {
        ReadOnlySpan<char> span = _memory.Span;
        if (span.IsEmpty)
        {
            return 0;
        }

        unchecked
        {
            int hash1 = 5381;
            int hash2 = hash1;

            for (int i = 0; i < span.Length; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ span[i];
                if (i + 1 < span.Length)
                {
                    hash2 = ((hash2 << 5) + hash2) ^ span[i + 1];
                }
            }

            return hash1 + (hash2 * 1566083941);
        }
    }

    public int GetHashCode(bool ignoreCase)
    {
        if (!ignoreCase)
        {
            return GetHashCode();
        }

        ReadOnlySpan<char> span = _memory.Span;
        if (span.IsEmpty)
        {
            return 0;
        }

        unchecked
        {
            int hash1 = 5381;
            int hash2 = hash1;

            for (int i = 0; i < span.Length; i += 2)
            {
                // Convert to uppercase for consistent hashing
                char c1 = char.ToUpperInvariant(span[i]);
                hash1 = ((hash1 << 5) + hash1) ^ c1;

                if (i + 1 < span.Length)
                {
                    char c2 = char.ToUpperInvariant(span[i + 1]);
                    hash2 = ((hash2 << 5) + hash2) ^ c2;
                }
            }

            return hash1 + (hash2 * 1566083941);
        }
    }

    /// <summary>
    /// Returns a string representation of this segment. This allocates a new string.
    /// </summary>
    public override string ToString()
        => IsEmpty ? string.Empty : Strings.WeakIntern(_memory.Span);

    public static bool operator ==(StringSegment left, StringSegment right)
        => left.Equals(right);

    public static bool operator !=(StringSegment left, StringSegment right)
        => !left.Equals(right);

    public static bool operator ==(StringSegment left, string? right)
        => left.Equals(right);

    public static bool operator !=(StringSegment left, string? right)
        => !left.Equals(right);

    public static bool operator ==(string? left, StringSegment right)
        => right.Equals(left);

    public static bool operator !=(string? left, StringSegment right)
        => !right.Equals(left);

    public static implicit operator StringSegment(string? str)
        => new(str);

    public static implicit operator StringSegment(ReadOnlyMemory<char> memory)
        => new(memory);

    public static implicit operator ReadOnlyMemory<char>(StringSegment segment)
        => segment._memory;

    public static implicit operator ReadOnlySpan<char>(StringSegment segment)
        => segment._memory.Span;

    /// <summary>
    /// Creates a substring from a portion of this segment. This allocates a new string.
    /// </summary>
    public string Substring(int startIndex)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex <= _memory.Length);

        return !IsEmpty && startIndex < _memory.Length
            ? _memory.Slice(startIndex).ToString()
            : string.Empty;
    }

    /// <summary>
    /// Creates a substring from a portion of this segment. This allocates a new string.
    /// </summary>
    public string Substring(int startIndex, int length)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex <= _memory.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(length >= 0 && startIndex + length <= _memory.Length);

        return !IsEmpty && length > 0
            ? _memory.Slice(startIndex, length).ToString()
            : string.Empty;
    }

    /// <summary>
    /// Returns a new StringSegment representing a subsegment of this segment.
    /// </summary>
    public StringSegment Slice(int start)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(start >= 0 && start <= _memory.Length);

        return !IsEmpty && start < _memory.Length
            ? new(_memory.Slice(start))
            : default;
    }

    /// <summary>
    /// Returns a new StringSegment representing a subsegment of this segment.
    /// </summary>
    public StringSegment Slice(int start, int length)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(start >= 0 && start <= _memory.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(length >= 0 && start + length <= _memory.Length);

        return !IsEmpty && length > 0
            ? new(_memory.Slice(start, length))
            : default;
    }

    /// <summary>
    /// Returns true if the specified character is found in this segment.
    /// </summary>
    public bool Contains(char value)
        => IndexOf(value) >= 0;

    /// <summary>
    /// Returns true if the specified character is found in this segment using the specified comparison.
    /// </summary>
    public bool Contains(char value, StringComparison comparisonType)
        => IndexOf(value, comparisonType) >= 0;

    /// <summary>
    /// Returns true if the specified string is found in this segment.
    /// </summary>
    public bool Contains(string value)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return Contains(value.AsSpan());
    }

    /// <summary>
    /// Returns true if the specified string is found in this segment using the specified comparison.
    /// </summary>
    public bool Contains(string value, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return Contains(value.AsSpan(), comparisonType);
    }

    /// <summary>
    /// Returns true if the specified span is found in this segment.
    /// </summary>
    public bool Contains(ReadOnlySpan<char> value)
        => IndexOf(value) >= 0;

    /// <summary>
    /// Returns true if the specified span is found in this segment using the specified comparison.
    /// </summary>
    public bool Contains(ReadOnlySpan<char> value, StringComparison comparisonType)
        => IndexOf(value, comparisonType) >= 0;

    /// <summary>
    /// Returns the index of the first occurrence of the specified character, or -1 if not found.
    /// </summary>
    public int IndexOf(char value)
        => _memory.Span.IndexOf(value);

    /// <summary>
    /// Returns the index of the first occurrence of the specified character starting at the specified index, or -1 if not found.
    /// </summary>
    public int IndexOf(char value, int startIndex)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex <= _memory.Length);

        if (startIndex == _memory.Length)
        {
            return -1;
        }

        int index = _memory.Span.Slice(startIndex).IndexOf(value);
        return index >= 0 ? index + startIndex : -1;
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified character using the specified comparison, or -1 if not found.
    /// </summary>
    public int IndexOf(char value, StringComparison comparisonType)
    {
        ReadOnlySpan<char> span = _memory.Span;

        if (comparisonType == StringComparison.Ordinal)
        {
            return span.IndexOf(value);
        }

        // For other comparison types, we need to make a span to search with.
        Span<char> valueSpan = stackalloc char[1] { value };

        return span.IndexOf(valueSpan, comparisonType);
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified character within the specified range, or -1 if not found.
    /// </summary>
    public int IndexOf(char value, int startIndex, int count)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex <= _memory.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(count >= 0 && startIndex + count <= _memory.Length);

        if (count == 0)
        {
            return -1;
        }

        int index = _memory.Span.Slice(startIndex, count).IndexOf(value);
        return index >= 0 ? index + startIndex : -1;
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string, or -1 if not found.
    /// </summary>
    public int IndexOf(string value)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return IndexOf(value.AsSpan());
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string starting at the specified index, or -1 if not found.
    /// </summary>
    public int IndexOf(string value, int startIndex)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return IndexOf(value.AsSpan(), startIndex);
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string, or -1 if not found.
    /// </summary>
    public int IndexOf(string value, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return IndexOf(value.AsSpan(), comparisonType);
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string within the specified range, or -1 if not found.
    /// </summary>
    public int IndexOf(string value, int startIndex, int count)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return IndexOf(value.AsSpan(), startIndex, count);
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string starting at the specified index using the specified comparison, or -1 if not found.
    /// </summary>
    public int IndexOf(string value, int startIndex, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return IndexOf(value.AsSpan(), startIndex, comparisonType);
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string within the specified range using the specified comparison, or -1 if not found.
    /// </summary>
    public int IndexOf(string value, int startIndex, int count, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return IndexOf(value.AsSpan(), startIndex, count, comparisonType);
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string, or -1 if not found.
    /// </summary>
    public int IndexOf(ReadOnlySpan<char> value)
        => _memory.Span.IndexOf(value);

    /// <summary>
    /// Returns the index of the first occurrence of the specified string starting at the specified index, or -1 if not found.
    /// </summary>
    public int IndexOf(ReadOnlySpan<char> value, int startIndex)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex <= _memory.Length);

        if (startIndex == _memory.Length)
        {
            return value.Length == 0 ? startIndex : -1;
        }

        int index = _memory.Span.Slice(startIndex).IndexOf(value);
        return index >= 0 ? index + startIndex : -1;
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string, or -1 if not found.
    /// </summary>
    public int IndexOf(ReadOnlySpan<char> value, StringComparison comparisonType)
        => _memory.Span.IndexOf(value, comparisonType);

    /// <summary>
    /// Returns the index of the first occurrence of the specified string within the specified range, or -1 if not found.
    /// </summary>
    public int IndexOf(ReadOnlySpan<char> value, int startIndex, int count)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex <= _memory.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(count >= 0 && startIndex + count <= _memory.Length);

        if (count == 0)
        {
            return value.Length == 0 ? startIndex : -1;
        }

        int index = _memory.Span.Slice(startIndex, count).IndexOf(value);
        return index >= 0 ? index + startIndex : -1;
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string starting at the specified index using the specified comparison, or -1 if not found.
    /// </summary>
    public int IndexOf(ReadOnlySpan<char> value, int startIndex, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex <= _memory.Length);

        if (startIndex == _memory.Length)
        {
            return value.Length == 0
                ? startIndex
                : -1;
        }

        int index = _memory.Span.Slice(startIndex).IndexOf(value, comparisonType);
        return index >= 0 ? index + startIndex : -1;
    }

    /// <summary>
    /// Returns the index of the first occurrence of the specified string within the specified range using the specified comparison, or -1 if not found.
    /// </summary>
    public int IndexOf(ReadOnlySpan<char> value, int startIndex, int count, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex <= _memory.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(count >= 0 && startIndex + count <= _memory.Length);

        if (count == 0)
        {
            return value.Length == 0 ? startIndex : -1;
        }

        int index = _memory.Span.Slice(startIndex, count).IndexOf(value, comparisonType);
        return index >= 0 ? index + startIndex : -1;
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified character, or -1 if not found.
    /// </summary>
    public int LastIndexOf(char value)
        => _memory.Span.LastIndexOf(value);

    /// <summary>
    /// Returns the index of the last occurrence of the specified character starting from the specified index and searching backwards, or -1 if not found.
    /// </summary>
    public int LastIndexOf(char value, int startIndex)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex < _memory.Length);

        if (IsEmpty)
        {
            return -1;
        }

        return _memory.Span.Slice(0, startIndex + 1).LastIndexOf(value);
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified character using the specified comparison, or -1 if not found.
    /// </summary>
    public int LastIndexOf(char value, StringComparison comparisonType)
    {
        if (comparisonType == StringComparison.Ordinal)
        {
            return LastIndexOf(value);
        }

#if NET
        Span<char> valueSpan = stackalloc char[1] { value };
        return _memory.Span.LastIndexOf(valueSpan, comparisonType);
#else
        // For non-ordinal comparisons, search from the end
        Span<char> valueSpan = stackalloc char[1] { value };
        ReadOnlySpan<char> span = _memory.Span;

        for (int i = span.Length - 1; i >= 0; i--)
        {
            if (span.Slice(i, 1).Equals(valueSpan, comparisonType))
            {
                return i;
            }
        }

        return -1;
#endif
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified character within the specified range, or -1 if not found.
    /// </summary>
    public int LastIndexOf(char value, int startIndex, int count)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex < _memory.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(count >= 0 && count <= startIndex + 1);

        if (count == 0)
        {
            return -1;
        }

        int searchStart = startIndex - count + 1;

#if NET
        return _memory.Span.Slice(searchStart, count).LastIndexOf(value) is int index && index >= 0
            ? index + searchStart
            : -1;
#else
        ReadOnlySpan<char> span = _memory.Span;
        for (int i = startIndex; i >= searchStart; i--)
        {
            if (span[i] == value)
            {
                return i;
            }
        }

        return -1;
#endif
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified string, or -1 if not found.
    /// </summary>
    public int LastIndexOf(string value)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return LastIndexOf(value.AsSpan());
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified string starting from the specified index and searching backwards, or -1 if not found.
    /// </summary>
    public int LastIndexOf(string value, int startIndex)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return LastIndexOf(value.AsSpan(), startIndex);
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified string using the specified comparison, or -1 if not found.
    /// </summary>
    public int LastIndexOf(string value, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return LastIndexOf(value.AsSpan(), comparisonType);
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified string within the specified range, or -1 if not found.
    /// </summary>
    public int LastIndexOf(string value, int startIndex, int count)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return LastIndexOf(value.AsSpan(), startIndex, count);
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified string starting from the specified index using the specified comparison, or -1 if not found.
    /// </summary>
    public int LastIndexOf(string value, int startIndex, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return LastIndexOf(value.AsSpan(), startIndex, comparisonType);
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified string within the specified range using the specified comparison, or -1 if not found.
    /// </summary>
    public int LastIndexOf(string value, int startIndex, int count, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return LastIndexOf(value.AsSpan(), startIndex, count, comparisonType);
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified span, or -1 if not found.
    /// </summary>
    public int LastIndexOf(ReadOnlySpan<char> value)
    {
#if NET
        return _memory.Span.LastIndexOf(value);
#else
        if (value.IsEmpty)
        {
            return _memory.Length;
        }

        ReadOnlySpan<char> span = _memory.Span;
        for (int i = span.Length - value.Length; i >= 0; i--)
        {
            if (span.Slice(i, value.Length).SequenceEqual(value))
            {
                return i;
            }
        }

        return -1;
#endif
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified span starting from the specified index and searching backwards, or -1 if not found.
    /// </summary>
    public int LastIndexOf(ReadOnlySpan<char> value, int startIndex)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex < _memory.Length);

        if (IsEmpty)
        {
            return value.Length == 0 ? 0 : -1;
        }

#if NET
        return _memory.Span.Slice(0, startIndex + 1).LastIndexOf(value);
#else
        if (value.IsEmpty)
        {
            return startIndex;
        }

        ReadOnlySpan<char> span = _memory.Span;
        int searchEnd = Math.Min(startIndex, span.Length - value.Length);

        for (int i = searchEnd; i >= 0; i--)
        {
            if (span.Slice(i, value.Length).SequenceEqual(value))
            {
                return i;
            }
        }

        return -1;
#endif
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified span using the specified comparison, or -1 if not found.
    /// </summary>
    public int LastIndexOf(ReadOnlySpan<char> value, StringComparison comparisonType)
    {
#if NET
        return _memory.Span.LastIndexOf(value, comparisonType);
#else
        if (value.IsEmpty)
        {
            return _memory.Length;
        }

        ReadOnlySpan<char> span = _memory.Span;
        for (int i = span.Length - value.Length; i >= 0; i--)
        {
            if (span.Slice(i, value.Length).Equals(value, comparisonType))
            {
                return i;
            }
        }

        return -1;
#endif
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified span within the specified range, or -1 if not found.
    /// </summary>
    public int LastIndexOf(ReadOnlySpan<char> value, int startIndex, int count)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex < _memory.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(count >= 0 && count <= startIndex + 1);

        if (count == 0)
        {
            return value.Length == 0 ? startIndex : -1;
        }

        int searchStart = startIndex - count + 1;

#if NET
        return _memory.Span.Slice(searchStart, count).LastIndexOf(value) is int index && index >= 0
            ? index + searchStart
            : -1;
#else
        if (value.IsEmpty)
        {
            return startIndex;
        }

        ReadOnlySpan<char> span = _memory.Span;
        int searchEnd = Math.Min(startIndex, searchStart + count - value.Length);

        for (int i = searchEnd; i >= searchStart; i--)
        {
            if (span.Slice(i, value.Length).SequenceEqual(value))
            {
                return i;
            }
        }

        return -1;
#endif
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified span starting from the specified index using the specified comparison, or -1 if not found.
    /// </summary>
    public int LastIndexOf(ReadOnlySpan<char> value, int startIndex, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex < _memory.Length);

        if (IsEmpty)
        {
            return value.Length == 0 ? 0 : -1;
        }

#if NET
        return _memory.Span.Slice(0, startIndex + 1).LastIndexOf(value, comparisonType);
#else
        if (value.IsEmpty)
        {
            return startIndex;
        }

        ReadOnlySpan<char> span = _memory.Span;
        int searchEnd = Math.Min(startIndex, span.Length - value.Length);

        for (int i = searchEnd; i >= 0; i--)
        {
            if (span.Slice(i, value.Length).Equals(value, comparisonType))
            {
                return i;
            }
        }

        return -1;
#endif
    }

    /// <summary>
    /// Returns the index of the last occurrence of the specified span within the specified range using the specified comparison, or -1 if not found.
    /// </summary>
    public int LastIndexOf(ReadOnlySpan<char> value, int startIndex, int count, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentOutOfRange(startIndex >= 0 && startIndex < _memory.Length);
        ErrorUtilities.VerifyThrowArgumentOutOfRange(count >= 0 && count <= startIndex + 1);

        if (count == 0)
        {
            return value.Length == 0 ? startIndex : -1;
        }

        int searchStart = startIndex - count + 1;

#if NET
        return _memory.Span.Slice(searchStart, count).LastIndexOf(value, comparisonType) is int index && index >= 0
            ? index + searchStart
            : -1;
#else
        if (value.IsEmpty)
        {
            return startIndex;
        }

        ReadOnlySpan<char> span = _memory.Span;
        int searchEnd = Math.Min(startIndex, searchStart + count - value.Length);

        for (int i = searchEnd; i >= searchStart; i--)
        {
            if (span.Slice(i, value.Length).Equals(value, comparisonType))
            {
                return i;
            }
        }

        return -1;
#endif
    }

    /// <summary>
    /// Checks if this segment starts with the specified character.
    /// </summary>
    public bool StartsWith(char value)
        => !IsEmpty && _memory.Span[0] == value;

    public bool StartsWith(string value)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return StartsWith(value.AsSpan());
    }

    public bool StartsWith(string value, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return StartsWith(value.AsSpan(), comparisonType);
    }

    public bool StartsWith(ReadOnlySpan<char> value)
        => _memory.Span.StartsWith(value);

    public bool StartsWith(ReadOnlySpan<char> value, StringComparison comparisonType)
        => _memory.Span.StartsWith(value, comparisonType);

    /// <summary>
    /// Checks if this segment ends with the specified character.
    /// </summary>
    public bool EndsWith(char value)
        => !IsEmpty && _memory.Span[_memory.Length - 1] == value;

    public bool EndsWith(string value)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return EndsWith(value.AsSpan());
    }

    public bool EndsWith(string value, StringComparison comparisonType)
    {
        ErrorUtilities.VerifyThrowArgumentNull(value);

        return EndsWith(value.AsSpan(), comparisonType);
    }

    public bool EndsWith(ReadOnlySpan<char> value)
        => _memory.Span.EndsWith(value);

    public bool EndsWith(ReadOnlySpan<char> value, StringComparison comparisonType)
        => _memory.Span.EndsWith(value, comparisonType);

    public StringSegment Trim()
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.Trim());
#else
        ReadOnlySpan<char> span = _memory.Span;
        int start = ClampStart(span);
        int length = ClampEnd(span, start);

        return new(_memory.Slice(start, length));
#endif
    }

    public StringSegment Trim(char trimChar)
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.Trim(trimChar));
#else
        ReadOnlySpan<char> span = _memory.Span;
        int start = ClampStart(span, trimChar);
        int length = ClampEnd(span, start, trimChar);

        return new(_memory.Slice(start, length));
#endif
    }

    /// <summary>
    /// Trims whitespace from the beginning and end of this segment.
    /// </summary>
    public StringSegment Trim(ReadOnlySpan<char> trimChars)
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.Trim(trimChars));
#else
        ReadOnlySpan<char> span = _memory.Span;
        int start = ClampStart(span, trimChars);
        int length = ClampEnd(span, start, trimChars);

        return new(_memory.Slice(start, length));
#endif
    }

    /// <summary>
    /// Trims whitespace from the beginning of this segment.
    /// </summary>
    public StringSegment TrimStart()
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.TrimStart());
#else
        return new(_memory.Slice(ClampStart(_memory.Span)));
#endif
    }

    /// <summary>
    /// Trims whitespace from the beginning of this segment.
    /// </summary>
    public StringSegment TrimStart(char trimChar)
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.TrimStart(trimChar));
#else
        return new(_memory.Slice(ClampStart(_memory.Span, trimChar)));
#endif
    }

    /// <summary>
    /// Trims whitespace from the beginning of this segment.
    /// </summary>
    public StringSegment TrimStart(ReadOnlySpan<char> trimChars)
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.TrimStart(trimChars));
#else
        return new(_memory.Slice(ClampStart(_memory.Span, trimChars)));
#endif
    }

    /// <summary>
    /// Trims whitespace from the end of this segment.
    /// </summary>
    public StringSegment TrimEnd()
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.TrimEnd());
#else
        return new(_memory.Slice(0, ClampEnd(_memory.Span, 0)));
#endif
    }

    /// <summary>
    /// Trims whitespace from the end of this segment.
    /// </summary>
    public StringSegment TrimEnd(char trimChar)
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.TrimEnd(trimChar));
#else
        return new(_memory.Slice(0, ClampEnd(_memory.Span, 0, trimChar)));
#endif
    }

    /// <summary>
    /// Trims whitespace from the end of this segment.
    /// </summary>
    public StringSegment TrimEnd(ReadOnlySpan<char> trimChars)
    {
        if (IsEmpty)
        {
            return this;
        }

#if NET
        return new(_memory.TrimEnd(trimChars));
#else
        return new(_memory.Slice(0, ClampEnd(_memory.Span, 0, trimChars)));
#endif
    }

#if !NET
    private static int ClampStart(ReadOnlySpan<char> span)
    {
        int start = 0;

        for (; start < span.Length; start++)
        {
            if (!char.IsWhiteSpace(span[start]))
            {
                break;
            }
        }

        return start;
    }

    private static int ClampStart(ReadOnlySpan<char> span, char trimChar)
    {
        int start = 0;

        for (; start < span.Length; start++)
        {
            if (span[start] != trimChar)
            {
                break;
            }
        }

        return start;
    }

    private static int ClampStart(ReadOnlySpan<char> span, ReadOnlySpan<char> trimChars)
    {
        int start = 0;

        for (; start < span.Length; start++)
        {
            if (trimChars.IndexOf(span[start]) < 0)
            {
                break;
            }
        }

        return start;
    }

    private static int ClampEnd(ReadOnlySpan<char> span, int start)
    {
        int end = span.Length - 1;

        for (; end >= start; end--)
        {
            if (!char.IsWhiteSpace(span[end]))
            {
                break;
            }
        }

        return end - start + 1;
    }

    private static int ClampEnd(ReadOnlySpan<char> span, int start, char trimChar)
    {
        int end = span.Length - 1;

        for (; end >= start; end--)
        {
            if (span[end] != trimChar)
            {
                break;
            }
        }

        return end - start + 1;
    }

    private static int ClampEnd(ReadOnlySpan<char> span, int start, ReadOnlySpan<char> trimChars)
    {
        int end = span.Length - 1;

        for (; end >= start; end--)
        {
            if (trimChars.IndexOf(span[end]) < 0)
            {
                break;
            }
        }

        return end - start + 1;
    }
#endif
}
