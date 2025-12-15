// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Buffers;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringLibrary : BaseMemberLibrary, IStaticMethodLibrary, IInstanceMethodLibrary<string>
    {
        public static readonly StringLibrary Instance = new();

        private enum StaticId
        {
            IsNullOrWhiteSpace,
            IsNullOrEmpty,
            Copy,
            New
        }

        private enum InstanceId
        {
            StartsWith,
            Replace,
            Contains,
            ToUpperInvariant,
            ToLowerInvariant,
            EndsWith,
            ToLower,
            IndexOf,
            IndexOfAny,
            LastIndexOf,
            LastIndexOfAny,
            Length,
            Split,
            Substring,
            PadLeft,
            PadRight,
            Trim,
            TrimStart,
            TrimEnd,
            Get_Chars,
            Equals
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;
        private static readonly FunctionIdLookup<InstanceId> s_instanceIds = FunctionIdLookup<InstanceId>.Instance;

        private StringLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFind(name, out StaticId id)
                ? id switch
                {
                    StaticId.IsNullOrWhiteSpace => String_IsNullOrWhiteSpace(args),
                    StaticId.IsNullOrEmpty => String_IsNullOrEmpty(args),
                    StaticId.Copy => String_Copy(args),
                    StaticId.New => String_New(args),
                    _ => Result.None,
                }
                : Result.None;

        public Result TryExecute(string instance, string name, ReadOnlySpan<object?> args)
            => s_instanceIds.TryFind(name, out InstanceId id)
                ? id switch
                {
                    InstanceId.StartsWith => String_StartsWith(instance, args),
                    InstanceId.Replace => String_Replace(instance, args),
                    InstanceId.Contains => String_Contains(instance, args),
                    InstanceId.ToUpperInvariant => String_ToUpperInvariant(instance, args),
                    InstanceId.ToLowerInvariant => String_ToLowerInvariant(instance, args),
                    InstanceId.EndsWith => String_EndsWith(instance, args),
                    InstanceId.ToLower => String_ToLower(instance, args),
                    InstanceId.IndexOf => String_IndexOf(instance, args),
                    InstanceId.IndexOfAny => String_IndexOfAny(instance, args),
                    InstanceId.LastIndexOf => String_LastIndexOf(instance, args),
                    InstanceId.LastIndexOfAny => String_LastIndexOfAny(instance, args),
                    InstanceId.Length => String_Length(instance, args),
                    InstanceId.Split => String_Split(instance, args),
                    InstanceId.Substring => String_Substring(instance, args),
                    InstanceId.PadLeft => String_PadLeft(instance, args),
                    InstanceId.PadRight => String_PadRight(instance, args),
                    InstanceId.Trim => String_Trim(instance, args),
                    InstanceId.TrimStart => String_TrimStart(instance, args),
                    InstanceId.TrimEnd => String_TrimEnd(instance, args),
                    InstanceId.Get_Chars => String_Get_Chars(instance, args),
                    InstanceId.Equals => String_Equals(instance, args),
                    _ => Result.None,
                }
                : Result.None;

        private static Result String_IsNullOrWhiteSpace(ReadOnlySpan<object?> args)
            => args is [string or null]
                ? Result.From(string.IsNullOrWhiteSpace((string?)args[0]))
                : Result.None;

        private static Result String_IsNullOrEmpty(ReadOnlySpan<object?> args)
            => args is [string or null]
                ? Result.From(string.IsNullOrEmpty((string?)args[0]))
                : Result.None;

#pragma warning disable CS0618 // Type or member is obsolete
        private static Result String_Copy(ReadOnlySpan<object?> args)
            => args is [string str]
                ? Result.From(string.Copy(str))
            : Result.None;
#pragma warning restore CS0618 // Type or member is obsolete

        private static Result String_New(ReadOnlySpan<object?> args)
            => args switch
            {
                [] => Result.From(string.Empty),
                [string value] => Result.From(value),
                _ => Result.None,
            };

        private static Result String_StartsWith(string instance, ReadOnlySpan<object?> args)
            => args is [string value]
            ? Result.From(instance.StartsWith(value))
            : Result.None;

        private static Result String_Replace(string instance, ReadOnlySpan<object?> args)
            => args is [string oldValue, string or null]
            ? Result.From(instance.Replace(oldValue, (string?)args[1]))
            : Result.None;

        private static Result String_Contains(string instance, ReadOnlySpan<object?> args)
            => args is [string value]
                ? Result.From(instance.Contains(value))
                : Result.None;

        private static Result String_ToUpperInvariant(string instance, ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(instance.ToUpperInvariant())
                : Result.None;

        private static Result String_ToLowerInvariant(string instance, ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(instance.ToLowerInvariant())
                : Result.None;

        private static Result String_EndsWith(string instance, ReadOnlySpan<object?> args)
            => args switch
            {
                [string value] => Result.From(instance.EndsWith(value)),
                [string value, string arg1] when TryConvertToEnum<StringComparison>(arg1, out var comparisonType) => Result.From(instance.EndsWith(value, comparisonType)),
                _ => Result.None,
            };

        private static Result String_ToLower(string instance, ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(instance.ToLower())
                : Result.None;

        private static Result String_IndexOf(string instance, ReadOnlySpan<object?> args)
            => args switch
            {
                // Handle 1-argument overloads:
                // - IndexOf(char)
                // - IndexOf(string)
                [var arg0] => arg0 switch
                {
                    string and [var c] => Result.From(instance.IndexOf(c)),
                    string s => Result.From(instance.IndexOf(s)),
                    char c => Result.From(instance.IndexOf(c)),

                    _ => Result.None
                },

                // Handle 2-argument overloads:
                // - IndexOf(char, int)
                // - IndexOf(string, int)
                // - IndexOf(string, StringComparison)
                [var arg0, var arg1] => arg0 switch
                {
                    string and [var c] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.IndexOf(c, startIndex)),
                    string s when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.IndexOf(s, startIndex)),
                    string s when TryConvertToEnum(arg1, out StringComparison comparisonType)
                        => Result.From(instance.IndexOf(s, comparisonType)),
                    char c when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.IndexOf(c, startIndex)),

                    _ => Result.None
                },

                // Handle 3-argument overloads:
                // - IndexOf(char, int, int)
                // - IndexOf(string, int, int)
                // - IndexOf(string, int, StringComparison)
                [var arg0, var arg1, var arg2] => arg0 switch
                {
                    string and [var c] when TryConvertToInt(arg1, out int startIndex) &&
                                            TryConvertToInt(arg2, out int count)
                        => Result.From(instance.IndexOf(c, startIndex, count)),
                    string s when TryConvertToInt(arg1, out int startIndex) &&
                                  TryConvertToInt(arg2, out int count)
                        => Result.From(instance.IndexOf(s, startIndex, count)),
                    string s when TryConvertToInt(arg1, out int startIndex) &&
                                  TryConvertToEnum(arg2, out StringComparison comparisonType)
                        => Result.From(instance.IndexOf(s, startIndex, comparisonType)),
                    char c when TryConvertToInt(arg1, out int startIndex) &&
                                TryConvertToInt(arg2, out int count)
                        => Result.From(instance.IndexOf(c, startIndex, count)),

                    _ => Result.None
                },

                // Handle 4-argument overload:
                // - IndexOf(char, int, int, StringComparison)
                [var arg0, var arg1, var arg2, var arg3] => arg0 switch
                {
                    string s when TryConvertToInt(arg1, out int startIndex) &&
                                  TryConvertToInt(arg2, out int count) &&
                                  TryConvertToEnum(arg3, out StringComparison comparisonType)
                        => Result.From(instance.IndexOf(s, startIndex, count, comparisonType)),

                    _ => Result.None
                },

                _ => Result.None
            };

        private static Result String_IndexOfAny(string instance, ReadOnlySpan<object?> args)
        {
            return args switch
            {
                // Handle 1-argument overloads:
                // - IndexOfAny(string) -> Treat arg0 as ReadOnlySpan<char>
                // - IndexOfAny(char[])
                // - IndexOfAny(string[]) -> Only when all elements are of length 1.
                [var arg0] => arg0 switch
                {
                    string and [var c] => Result.From(instance.IndexOf(c)),
                    string and [var c1, var c2] => Result.From(instance.IndexOfAny(c1, c2)),
                    string and [var c1, var c2, var c3] => Result.From(instance.IndexOf(c1, c2, c3)),

                    string values => Result.From(instance.AsSpan().IndexOfAny(values.AsSpan())),

                    char[] and [var c] => Result.From(instance.IndexOf(c)),
                    char[] and [var c1, var c2] => Result.From(instance.AsSpan().IndexOfAny(c1, c2)),
                    char[] and [var c1, var c2, var c3] => Result.From(instance.AsSpan().IndexOfAny(c1, c2, c3)),
                    char[] anyOf => Result.From(instance.IndexOfAny(anyOf)),

                    string[] and [[var c]] => Result.From(instance.IndexOf(c)),
                    string[] and [[var c1], [var c2]] => Result.From(instance.AsSpan().IndexOfAny(c1, c2)),
                    string[] and [[var c1], [var c2], [var c3]] => Result.From(instance.AsSpan().IndexOfAny(c1, c2, c3)),

                    string[] and [[_], [_], [_], ..] anyOf when CanBeTreatedAsCharArray(anyOf)
                        => Result.From(IndexOfAny(instance, anyOf)),

                    _ => Result.None
                },

                // Handle 2-argument overloads:
                // - IndexOfAny(string, int) -> Treat arg0 as ReadOnlySpan<char>
                // - IndexOfAny(char[], int)
                // - IndexOfAny(string[], int) -> Only when all elements are of length 1.
                [var arg0, var arg1] => arg0 switch
                {
                    string and [var c] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.IndexOf(c, startIndex)),
                    string and [var c1, var c2] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.AsSpan(startIndex).IndexOfAny(c1, c2)),
                    string and [var c1, var c2, var c3] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.AsSpan(startIndex).IndexOfAny(c1, c2, c3)),

                    string values when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.AsSpan(startIndex).IndexOfAny(values.AsSpan())),

                    char[] and [var c] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.IndexOf(c, startIndex)),
                    char[] and [var c1, var c2] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.AsSpan(startIndex).IndexOfAny(c1, c2)),
                    char[] and [var c1, var c2, var c3] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.AsSpan(startIndex).IndexOfAny(c1, c2, c3)),
                    char[] anyOf when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.IndexOfAny(anyOf, startIndex)),

                    string[] and [[var c]] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.IndexOf(c, startIndex)),
                    string[] and [[var c1], [var c2]] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.AsSpan(startIndex).IndexOfAny(c1, c2)),
                    string[] and [[var c1], [var c2], [var c3]] when TryConvertToInt(arg1, out int startIndex)
                        => Result.From(instance.AsSpan(startIndex).IndexOfAny(c1, c2, c3)),

                    string[] and [[_], [_], [_], ..] anyOf when CanBeTreatedAsCharArray(anyOf) &&
                                                                TryConvertToInt(arg1, out int startIndex)
                        => Result.From(IndexOfAny(instance.AsSpan(startIndex), anyOf)),

                    _ => Result.None
                },

                // Handle 3-argument overloads:
                // - IndexOfAny(string, int, int) -> Treat arg0 as ReadOnlySpan<char>
                // - IndexOfAny(char[], int, int)
                // - IndexOfAny(string[], int, int) -> Only when all elements are of length 1.
                [var arg0, var arg1, var arg2] => arg0 switch
                {
                    string values when TryConvertToInt(arg1, out int startIndex) &&
                                       TryConvertToInt(arg2, out int count)
                        => Result.From(instance.AsSpan(startIndex, count).IndexOfAny(values.AsSpan())),

                    char[] and [var c] when TryConvertToInt(arg1, out int startIndex) &&
                                            TryConvertToInt(arg2, out int count)
                        => Result.From(instance.IndexOf(c, startIndex)),
                    char[] and [var c1, var c2] when TryConvertToInt(arg1, out int startIndex) &&
                                                     TryConvertToInt(arg2, out int count)
                        => Result.From(instance.AsSpan(startIndex, count).IndexOfAny(c1, c2)),
                    char[] and [var c1, var c2, var c3] when TryConvertToInt(arg1, out int startIndex) &&
                                                             TryConvertToInt(arg2, out int count)
                        => Result.From(instance.AsSpan(startIndex, count).IndexOfAny(c1, c2, c3)),
                    char[] anyOf when TryConvertToInt(arg1, out int startIndex) &&
                                      TryConvertToInt(arg2, out int count)
                        => Result.From(instance.IndexOfAny(anyOf, startIndex, count)),

                    string[] and [[var c]] when TryConvertToInt(arg1, out int startIndex) &&
                                                TryConvertToInt(arg2, out int count)
                        => Result.From(instance.IndexOf(c, startIndex, count)),
                    string[] and [[var c1], [var c2]] when TryConvertToInt(arg1, out int startIndex) &&
                                                           TryConvertToInt(arg2, out int count)
                        => Result.From(instance.AsSpan(startIndex, count).IndexOfAny(c1, c2)),
                    string[] and [[var c1], [var c2], [var c3]] when TryConvertToInt(arg1, out int startIndex) &&
                                                                     TryConvertToInt(arg2, out int count)
                        => Result.From(instance.AsSpan(startIndex, count).IndexOfAny(c1, c2, c3)),

                    string[] and [[_], [_], [_], ..] anyOf when CanBeTreatedAsCharArray(anyOf) &&
                                                                TryConvertToInt(arg1, out int startIndex) &&
                                                                TryConvertToInt(arg2, out int count)
                        => Result.From(IndexOfAny(instance.AsSpan(startIndex, count), anyOf)),

                    _ => Result.None
                },

                _ => Result.None,
            };

            static int IndexOfAny(ReadOnlySpan<char> s, string[] values)
            {
                Debug.Assert(CanBeTreatedAsCharArray(values));

                using BufferScope<char> scope = values.Length <= 256
                    ? new(stackalloc char[values.Length])
                    : new(minimumLength: values.Length);

                Span<char> chars = scope[..values.Length];
                var writer = new SpanWriter<char>(chars);

                foreach (string value in values)
                {
                    writer.TryWrite(value[0]);
                }

                Debug.Assert(writer.Position == writer.Length);

                return s.IndexOfAny(chars);
            }
        }

        private static Result String_LastIndexOf(string instance, ReadOnlySpan<object?> args)
            => args switch
            {
                [string value] => Result.From(instance.LastIndexOf(value)),
                [string value, var arg1] when TryConvertToInt(arg1, out int startIndex)
                    => Result.From(instance.LastIndexOf(value, startIndex)),
                [string value, string arg1] when TryConvertToEnum(arg1, out StringComparison comparisonType)
                    => Result.From(instance.LastIndexOf(value, comparisonType)),

                _ => Result.None,
            };

        private static Result String_LastIndexOfAny(string instance, ReadOnlySpan<object?> args)
            => args is [string values]
                ? Result.From(instance.AsSpan().LastIndexOfAny(values))
                : Result.None;

        private static Result String_Length(string instance, ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(instance.Length)
                : Result.None;

        private static Result String_Substring(string instance, ReadOnlySpan<object?> args)
            => args switch
            {
                [var arg0] when TryConvertToInt(arg0, out int startIndex)
                    => Result.From(instance.Substring(startIndex)),
                [var arg0, var arg1] when TryConvertToInt(arg0, out int startIndex) &&
                                          TryConvertToInt(arg1, out int length)
                    => Result.From(instance.Substring(startIndex, length)),
                _ => Result.None,
            };

        private static Result String_Split(string instance, ReadOnlySpan<object?> args)
            => args is [var value] && TryConvertToChar(value, out char separator)
                ? Result.From(instance.Split(separator))
                : Result.None;

        private static Result String_PadLeft(string instance, ReadOnlySpan<object?> args)
            => args switch
            {
                [var arg0] when TryConvertToInt(arg0, out int totalWidth)
                    => Result.From(instance.PadLeft(totalWidth)),
                [var arg0, var arg1] when TryConvertToInt(arg0, out int totalWidth) &&
                                          TryConvertToChar(arg1, out char paddingChar)
                    => Result.From(instance.PadLeft(totalWidth, paddingChar)),

                _ => Result.None,
            };

        private static Result String_PadRight(string instance, ReadOnlySpan<object?> args)
            => args switch
            {
                [var arg0] when TryConvertToInt(arg0, out int totalWidth)
                    => Result.From(instance.PadRight(totalWidth)),
                [var arg0, var arg1] when TryConvertToInt(arg0, out int totalWidth) &&
                                          TryConvertToChar(arg1, out char paddingChar)
                    => Result.From(instance.PadRight(totalWidth, paddingChar)),

                _ => Result.None,
            };

        private Result String_Trim(string instance, ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(instance.Trim())
                : Result.None;

        private static Result String_TrimStart(string instance, ReadOnlySpan<object?> args)
            => args is [string trimChars] && trimChars.Length > 0
                ? Result.From(trimChars.Length == 1
                    ? instance.TrimStart(trimChars[0])
                    : instance.TrimStart(trimChars.ToCharArray()))
                : Result.None;

        private static Result String_TrimEnd(string instance, ReadOnlySpan<object?> args)
            => args is [string trimChars] && trimChars.Length > 0
                ? Result.From(trimChars.Length == 1
                    ? instance.TrimEnd(trimChars[0])
                    : instance.TrimEnd(trimChars.ToCharArray()))
                : Result.None;

        private static Result String_Get_Chars(string instance, ReadOnlySpan<object?> args)
            => args is [var arg0] && TryConvertToInt(arg0, out int index)
                ? Result.From(instance[index])
                : Result.None;

        private static Result String_Equals(string instance, ReadOnlySpan<object?> args)
            => args is [string or null]
                ? Result.From(instance.Equals((string?)args[0]))
                : Result.None;

        private static bool CanBeTreatedAsCharArray(string[] values)
        {
            foreach (string value in values)
            {
                if (value.Length != 1)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
