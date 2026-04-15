// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build;

#pragma warning disable CS9113 // Parameter is unread.
#pragma warning disable IDE0060 // Remove unused parameter

internal static partial class Assumed
{
    /// <summary>
    ///  Interpolated string handler used by the interpolated string overload of
    ///  <c>Null</c> to defer string formatting until the assertion fails.
    /// </summary>
    /// <typeparam name="T">The type of value being checked for <see langword="null"/>.</typeparam>
    [InterpolatedStringHandler]
    public ref struct IfNullInterpolatedStringHandler<T>
    {
        private StringBuilderHelper _builder;

        public IfNullInterpolatedStringHandler(int literalLength, int formattedCount, T? value, out bool success)
        {
            success = value is not null;
            _builder = success ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Interpolated string handler used by the interpolated string overload of
    ///  <c>NotNull</c> to defer string formatting until the assertion fails.
    /// </summary>
    /// <typeparam name="T">The type of value being checked for non-<see langword="null"/>.</typeparam>
    [InterpolatedStringHandler]
    public ref struct IfNotNullInterpolatedStringHandler<T>
    {
        private StringBuilderHelper _builder;

        public IfNotNullInterpolatedStringHandler(int literalLength, int formattedCount, T? value, out bool success)
        {
            success = value is null;
            _builder = success ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Interpolated string handler used by
    ///  <see cref="True(bool, IfTrueInterpolatedStringHandler, string?, int)"/>
    ///  to defer string formatting until the assertion fails.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct IfTrueInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public IfTrueInterpolatedStringHandler(int literalLength, int formattedCount, bool condition, out bool success)
        {
            success = !condition;
            _builder = success ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Interpolated string handler used by
    ///  <see cref="False(bool, IfFalseInterpolatedStringHandler, string?, int)"/>
    ///  to defer string formatting until the assertion fails.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct IfFalseInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public IfFalseInterpolatedStringHandler(int literalLength, int formattedCount, bool condition, out bool success)
        {
            success = condition;
            _builder = success ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    ///  Interpolated string handler for <see cref="Unreachable(UnreachableInterpolatedStringHandler, string?, int)"/>
    ///  and <see cref="Unreachable{T}(UnreachableInterpolatedStringHandler, string?, int)"/>.
    ///  Always formats the message since the method unconditionally throws.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct UnreachableInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        private StringBuilderHelper _builder = new(literalLength);

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }
}
