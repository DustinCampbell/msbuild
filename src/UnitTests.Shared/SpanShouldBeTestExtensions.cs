// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Shouldly;

[DebuggerStepThrough]
[ShouldlyMethods]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SpanShouldBeTestExtensions
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBe(
        this ReadOnlySpan<char> actual,
        ReadOnlySpan<char> expected,
        string? customMessage = null)
        => ShouldBe(actual, expected, customMessage, 0);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBe(
        this ReadOnlySpan<char> actual,
        ReadOnlySpan<char> expected,
        StringCompareShould options)
        => ShouldBe(actual, expected, customMessage: null, options);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBe(
        this ReadOnlySpan<char> actual,
        ReadOnlySpan<char> expected,
        string? customMessage,
        StringCompareShould options)
        => actual.ToString().ShouldBe(expected.ToString(), customMessage, options);
}
