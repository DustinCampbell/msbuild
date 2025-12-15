// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class RegexLibrary : BaseMemberLibrary, IStaticMethodLibrary
    {
        public static readonly RegexLibrary Instance = new();

        private enum StaticId
        {
            Escape,
            IsMatch,
            Match,
            Replace
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;

        private RegexLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFind(name, out StaticId id)
                ? id switch
                {
                    StaticId.Escape => Regex_Escape(args),
                    StaticId.IsMatch => Regex_IsMatch(args),
                    StaticId.Match => Regex_Match(args),
                    StaticId.Replace => Regex_Replace(args),
                    _ => Result.None,
                }
                : Result.None;

        private static Result Regex_Escape(ReadOnlySpan<object?> args)
            => args is [string str]
                ? Result.From(Regex.Escape(str))
                : Result.None;

        private static Result Regex_IsMatch(ReadOnlySpan<object?> args)
            => args is [string input, string pattern]
                ? Result.From(Regex.IsMatch(input, pattern))
                : Result.None;

        private static Result Regex_Match(ReadOnlySpan<object?> args)
            => args is [string input, string pattern]
                ? Result.From(Regex.Match(input, pattern))
                : Result.None;

        private static Result Regex_Replace(ReadOnlySpan<object?> args)
            => args is [string input, string pattern, string replacement]
                ? Result.From(Regex.Replace(input, pattern, replacement))
                : Result.None;
    }
}
