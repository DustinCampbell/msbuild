// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Text;

internal ref struct RefStringBuilder
{
    private RefArrayBuilder<char> _chars;

    public readonly int Length => _chars.Count;

    public RefStringBuilder(int initialCapacity)
    {
        _chars = new RefArrayBuilder<char>(initialCapacity);
    }

    public void Dispose()
    {
        _chars.Dispose();
    }

    public void Append(char c)
    {
        _chars.Add(c);
    }

    public void Append(ReadOnlySpan<char> span)
    {
        _chars.AddRange(span);
    }

    public void Append(ReadOnlyMemory<char> memory)
    {
        _chars.AddRange(memory.Span);
    }

#if NET

    public void Append(sbyte value) => AppendSpanFormattable(value);

    public void Append(sbyte value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(byte value) => AppendSpanFormattable(value);

    public void Append(byte value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(short value) => AppendSpanFormattable(value);

    public void Append(short value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(int value) => AppendSpanFormattable(value);

    public void Append(int value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(long value) => AppendSpanFormattable(value);

    public void Append(long value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(float value) => AppendSpanFormattable(value);

    public void Append(float value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(double value) => AppendSpanFormattable(value);

    public void Append(double value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(decimal value) => AppendSpanFormattable(value);

    public void Append(decimal value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(ushort value) => AppendSpanFormattable(value);

    public void Append(ushort value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(uint value) => AppendSpanFormattable(value);

    public void Append(uint value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    public void Append(ulong value) => AppendSpanFormattable(value);

    public void Append(ulong value, IFormatProvider? provider) => AppendSpanFormattable(value, provider);

    private void AppendSpanFormattable<T>(T value)
        where T : ISpanFormattable
    {
        if (value.TryFormat(_chars.Remaining, out int charsWritten, format: default, provider: null))
        {
            _chars.Count += charsWritten;
            return;
        }

        Append(value.ToString());
    }

    private void AppendSpanFormattable<T>(T value, IFormatProvider? provider)
        where T : ISpanFormattable
    {
        if (value.TryFormat(_chars.Remaining, out int charsWritten, format: default, provider))
        {
            _chars.Count += charsWritten;
            return;
        }

        Append(value.ToString(null, provider));
    }

#else

    public void Append(sbyte value) => AppendConvertible(value);

    public void Append(sbyte value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(byte value) => AppendConvertible(value);

    public void Append(byte value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(short value) => AppendConvertible(value);

    public void Append(short value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(int value) => AppendConvertible(value);

    public void Append(int value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(long value) => AppendConvertible(value);

    public void Append(long value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(float value) => AppendConvertible(value);

    public void Append(float value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(double value) => AppendConvertible(value);

    public void Append(double value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(decimal value) => AppendConvertible(value);

    public void Append(decimal value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(ushort value) => AppendConvertible(value);

    public void Append(ushort value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(uint value) => AppendConvertible(value);

    public void Append(uint value, IFormatProvider? provider) => AppendConvertible(value, provider);

    public void Append(ulong value) => AppendConvertible(value);

    public void Append(ulong value, IFormatProvider? provider) => AppendConvertible(value, provider);

    private void AppendConvertible<T>(T value)
        where T : IConvertible
        => Append(value.ToString(provider: null));

    private void AppendConvertible<T>(T value, IFormatProvider? provider)
        where T : IConvertible
        => Append(value.ToString(provider));

#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendEscaped(char c)
    {
        if (EscapeChars.TryGetEscapeSequence(c, out ReadOnlySpan<char> escapeSequence))
        {
            _chars.AddRange(escapeSequence);
        }
        else
        {
            _chars.Add(c);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendEscaped(ReadOnlySpan<char> span)
    {
        foreach (char c in span)
        {
            AppendEscaped(c);
        }
    }

    public void Clear()
    {
        _chars.Clear();
    }

    public override readonly string ToString()
        => _chars.AsSpan().ToString();
}
