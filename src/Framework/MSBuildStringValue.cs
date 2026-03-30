// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
///  Holds a string in one or both of its MSBuild-escaped and unescaped forms, lazily
///  computing and caching the complementary form on first access.
/// </summary>
/// <remarks>
///  <para>
///   MSBuild stores item specs and metadata values in their escaped form (e.g.
///   <c>%28</c> for <c>(</c>) and unescapes on every read. This type eliminates that
///   repeated work by computing each form at most once and caching the result.
///  </para>
///  <para>
///   In the overwhelmingly common case where the string contains no MSBuild special
///   characters, both <see cref="Escaped"/> and <see cref="Unescaped"/> return the
///   exact same <see cref="string"/> instance — no second allocation is ever made.
///  </para>
///  <para>
///   Thread safety: both properties use a relaxed publish pattern
///   (<see cref="Volatile.Read{T}"/> / <see cref="Volatile.Write{T}"/>).  If two
///   threads race to compute the same form they will both produce the same value;
///   one result is silently discarded.  No lock is needed.
///  </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class MSBuildStringValue : IEquatable<MSBuildStringValue>, IEquatable<string>, ITranslatable
{
    // Invariant: at least one field is non-null at all times.
    // A null field means "not yet computed"; compute it on first access.
    private string? _escaped;
    private string? _unescaped;

    /// <summary>
    ///  Gets an <see cref="MSBuildStringValue"/> that wraps <see cref="string.Empty"/>.
    /// </summary>
    public static MSBuildStringValue Empty { get; } = new(string.Empty, string.Empty);

    private MSBuildStringValue(string? escaped, string? unescaped)
    {
        Debug.Assert(escaped is not null || unescaped is not null,
            "At least one form must be provided.");

        _escaped = escaped;
        _unescaped = unescaped;
    }

    /// <summary>
    ///  Creates an <see cref="MSBuildStringValue"/> from a value already in MSBuild-escaped form.
    /// </summary>
    public static MSBuildStringValue FromEscaped(string escaped)
    {
        ArgumentNullException.ThrowIfNull(escaped);

        return new(escaped, unescaped: null);
    }

    /// <summary>
    ///  Creates an <see cref="MSBuildStringValue"/> from a plain (unescaped) value.
    /// </summary>
    public static MSBuildStringValue FromUnescaped(string unescaped)
    {
        ArgumentNullException.ThrowIfNull(unescaped);

        FrameworkErrorUtilities.VerifyThrow(!EscapingUtilities.ContainsEscapeSequence(unescaped),
            $"The unescaped value passed to FromBoth contains %XX escape sequences and is likely already escaped: \"{unescaped}\"");

        return new(escaped: null, unescaped);
    }

    /// <summary>
    ///  Creates an <see cref="MSBuildStringValue"/> from a string whose escaping status is
    ///  not known ahead of time.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   If <paramref name="value"/> contains <c>%XX</c> escape sequences (where <c>XX</c>
    ///   is a valid hexadecimal pair), it is treated as escaped.  Otherwise it is treated
    ///   as unescaped.  In the overwhelmingly common case where the string contains no
    ///   special characters, both forms are identical and only a single reference is stored.
    ///  </para>
    ///  <para>
    ///   Callers who know which form the string is in should prefer
    ///   <see cref="FromEscaped"/> or <see cref="FromUnescaped"/> — they are cheaper
    ///   because they skip the probe entirely.
    ///  </para>
    /// </remarks>
    public static MSBuildStringValue From(string value)
    {
        if (EscapingUtilities.ContainsEscapeSequence(value))
        {
            // value has %XX sequences → treat as escaped; unescape lazily.
            return new(escaped: value, unescaped: null);
        }

        // No %XX sequences → value is unescaped; escape lazily.
        return new(escaped: null, unescaped: value);
    }

    /// <summary>
    ///  Gets the MSBuild-escaped form of the value (e.g. <c>%28</c> for <c>(</c>).
    /// </summary>
    public string Escaped
    {
        get
        {
            string? escaped = Volatile.Read(ref _escaped);

            if (escaped is null)
            {
                // _unescaped is non-null when _escaped is null (class invariant).
                escaped = EscapingUtilities.Escape(_unescaped!);

                // In the common case Escape returns the same reference, so we can
                // immediately set _unescaped too — no future work required.
                if (ReferenceEquals(escaped, _unescaped))
                {
                    Volatile.Write(ref _unescaped, escaped); // make explicit (already set)
                }

                Volatile.Write(ref _escaped, escaped);
            }

            return escaped;
        }
    }

    /// <summary>
    ///  Gets the unescaped (plain) form of the value (e.g. <c>(</c> for <c>%28</c>).
    /// </summary>
    public string Unescaped
    {
        get
        {
            string? unescaped = Volatile.Read(ref _unescaped);

            if (unescaped is null)
            {
                // _escaped is non-null when _unescaped is null (class invariant).
                unescaped = EscapingUtilities.UnescapeAll(_escaped!);

                // In the overwhelmingly common case UnescapeAll returns the same
                // reference, so fill in _escaped too — it won't need computing later.
                if (ReferenceEquals(unescaped, _escaped))
                {
                    Volatile.Write(ref _escaped, unescaped); // make explicit (already set)
                }

                Volatile.Write(ref _unescaped, unescaped);
            }

            return unescaped;
        }
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <see cref="Escaped"/> and <see cref="Unescaped"/>
    ///  are known to be the same string without computing the missing form.
    /// </summary>
    /// <remarks>
    ///  Returns <see langword="false"/> when only one form has been computed; the forms
    ///  <em>may</em> still be equal — use <see cref="string.Equals(string, string)"/> if
    ///  you need a definitive answer.
    /// </remarks>
    public bool AreBothFormsKnownEqual
        => _escaped is not null
        && _unescaped is not null
        && ReferenceEquals(_escaped, _unescaped);

    // Note: Equality is defined on the escaped form, which is canonical in MSBuild.

    /// <inheritdoc/>
    public bool Equals(MSBuildStringValue? other)
        => other is not null
        && string.Equals(Escaped, other.Escaped, StringComparison.Ordinal);

    /// <summary>
    ///  Returns <see langword="true"/> if the escaped form of this value equals
    ///  <paramref name="other"/> under ordinal comparison.
    /// </summary>
    public bool Equals(string? other)
        => string.Equals(Escaped, other, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj switch
        {
            MSBuildStringValue other => Equals(other),
            string other => Equals(other),
            _ => false,
        };

    /// <inheritdoc/>
    public override int GetHashCode()
        => Escaped.GetHashCode();

    /// <summary>
    ///  Returns the escaped form of the value.
    /// </summary>
    public override string ToString()
        => Escaped;

    void ITranslatable.Translate(ITranslator translator)
    {
        translator.Translate(ref _escaped);
        translator.Translate(ref _unescaped);
    }

    public static MSBuildStringValue FactoryForDeserialization(ITranslator translator)
    {
        string? escaped = null;
        string? unescaped = null;

        translator.Translate(ref escaped);
        translator.Translate(ref unescaped);

        return new MSBuildStringValue(escaped, unescaped);
    }

    public static bool operator ==(MSBuildStringValue? left, MSBuildStringValue? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(MSBuildStringValue? left, MSBuildStringValue? right)
        => !(left == right);

    public static bool operator ==(MSBuildStringValue? left, string? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(MSBuildStringValue? left, string? right)
        => !(left == right);

    public static bool operator ==(string? left, MSBuildStringValue? right)
        => right == left;

    public static bool operator !=(string? left, MSBuildStringValue? right)
        => !(left == right);

    private string DebuggerDisplay
    {
        get
        {
            string escaped = _escaped ?? "<not computed>";
            string unescaped = _unescaped ?? "<not computed>";

            return ReferenceEquals(_escaped, _unescaped)
                ? $"\"{escaped}\" (escaped == unescaped)"
                : $"escaped=\"{escaped}\"  unescaped=\"{unescaped}\"";
        }
    }
}
