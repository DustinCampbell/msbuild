// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    private static partial class ItemExpander
    {
        private static partial class Transforms
        {
            private struct Builder : IDisposable
            {
                /// <summary>
                ///  A thread-static string builder for use in ExpandQuotedExpressionFunction.
                ///  In theory we should be able to use shared instance, but in a profile it appears something higher in
                ///  the call-stack is already borrowing the instance, so it ends up always allocating.
                ///  This should not be used outside of ExpandQuotedExpressionFunction unless validated to not conflict.
                /// </summary>
                [ThreadStatic]
                private static SpanBasedStringBuilder? s_includeBuilder;

                private static SpanBasedStringBuilder GetBuilder()
                {
                    SpanBasedStringBuilder builder = s_includeBuilder ?? new SpanBasedStringBuilder();
                    s_includeBuilder = null;
                    return builder;
                }

                private SpanBasedStringBuilder? _builder;
                private string? _firstString;
                private ReadOnlyMemory<char> _firstSpan;
                private bool _hasFirstValue;

                [MemberNotNullWhen(true, nameof(_builder))]
                private readonly bool HasBuilder => _builder is not null;

                public void Add(string? value)
                {
                    if (value.IsNullOrEmpty())
                    {
                        return;
                    }

                    if (HasBuilder)
                    {
                        _builder.Append(value);
                    }
                    else if (_hasFirstValue)
                    {
                        FlushFirstValue();
                        _builder.Append(value);
                    }
                    else
                    {
                        _firstString = value;
                        _hasFirstValue = true;
                    }
                }

                public void Add(string value, int start, int length)
                {
                    if (length == 0)
                    {
                        return;
                    }

                    if (HasBuilder)
                    {
                        _builder.Append(value, start, length);
                    }
                    else if (_hasFirstValue)
                    {
                        FlushFirstValue();
                        _builder.Append(value, start, length);
                    }
                    else
                    {
                        _firstSpan = value.AsMemory(start, length);
                        _hasFirstValue = true;
                    }
                }

                public string GetResultAndReset()
                {
                    string result;
                    if (HasBuilder)
                    {
                        result = _builder.ToString();
                        _builder.Clear();
                    }
                    else if (_firstString is not null)
                    {
                        result = _firstString;
                    }
                    else if (_hasFirstValue)
                    {
                        result = _firstSpan.ToString();
                    }
                    else
                    {
                        result = string.Empty;
                    }

                    _firstString = null;
                    _firstSpan = default;
                    _hasFirstValue = false;

                    return result;
                }

                public void Dispose()
                {
                    if (_builder is not null)
                    {
                        _builder.Clear();
                        s_includeBuilder = _builder;
                        _builder = null;
                    }

                    _firstString = null;
                    _firstSpan = default;
                    _hasFirstValue = false;
                }

                [MemberNotNull(nameof(_builder))]
                private void FlushFirstValue()
                {
                    _builder ??= GetBuilder();

                    if (_firstString is not null)
                    {
                        _builder.Append(_firstString);
                        _firstString = null;
                    }
                    else
                    {
                        _builder.Append(_firstSpan);
                        _firstSpan = default;
                    }

                    _hasFirstValue = false;
                }
            }
        }
    }
}
