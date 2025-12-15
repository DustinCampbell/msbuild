// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private interface IStaticMethodLibrary
    {
        Result TryExecute(string name, ReadOnlySpan<object?> args);
    }

    private interface IInstanceMethodLibrary<T>
    {
        Result TryExecute(T instance, string name, ReadOnlySpan<object?> args);
    }

    private interface ICustomToStringProvider<T>
    {
        Result TryExecuteToString(T instance, ReadOnlySpan<object?> args);
    }

    private abstract class BaseMemberLibrary
    {
        protected static Result TryExecuteArithmeticFunction(
            ReadOnlySpan<object?> args,
            Func<int, int, int> integerFunc)
            => args is [var arg0, var arg1] &&
                TryConvertToInt(arg0, out var a) &&
                TryConvertToInt(arg1, out var b)
                ? Result.From(integerFunc(a, b))
                : Result.None;

        protected static Result TryExecuteArithmeticFunction(
            ReadOnlySpan<object?> args,
            Func<double, double, double> realFunc)
            => args is [var arg0, var arg1] &&
                TryConvertToDouble(arg0, out var a) &&
                TryConvertToDouble(arg1, out var b)
                ? Result.From(realFunc(a, b))
                : Result.None;

        /// <summary>
        ///  Executes an arithmetic operation on two arguments, automatically selecting between integer and floating-point overloads.
        ///  Prefers integer arithmetic if both arguments can be converted to long; otherwise, uses double arithmetic.
        /// </summary>
        /// <param name="args">The array of arguments (must contain exactly two elements).</param>
        /// <param name="integerFunc">The operation to perform for integer arithmetic.</param>
        /// <param name="realFunc">The operation to perform for floating-point arithmetic.</param>
        /// <returns>
        ///  <see langword="true"/> if the operation was successfully executed; otherwise, <see langword="false"/>.
        /// </returns>
        protected static Result TryExecuteArithmeticFunctionWithOverflow(
            ReadOnlySpan<object?> args,
            Func<long, long, long> integerFunc,
            Func<double, double, double> realFunc)
        {
            if (args is [var arg0, var arg1])
            {
                if (TryConvertToLong(arg0, out long longA) &&
                    TryConvertToLong(arg1, out long longB))
                {
                    return Result.From(integerFunc(longA, longB));
                }

                if (TryConvertToDouble(arg0, out double doubleA) &&
                    TryConvertToDouble(arg1, out double doubleB))
                {
                    return Result.From(realFunc(doubleA, doubleB));
                }
            }

            return Result.None;
        }

        protected sealed class FunctionIdLookup<T>
            where T : struct, Enum
        {
            public static readonly FunctionIdLookup<T> Instance = new();

            private readonly FrozenDictionary<string, T> _nameToId;

            private FunctionIdLookup()
            {
                Type enumType = typeof(T);
                string[] names = Enum.GetNames(enumType);
                T[] values = (T[])Enum.GetValues(enumType);

                var entries = new Entry[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    string nameUpper = names[i].ToUpperInvariant();
                    entries[i] = new Entry
                    {
                        NameUpper = nameUpper,
                        Hash = ComputeHash(nameUpper),
                        Id = values[i]
                    };
                }

                _nameToId = entries
                   .Select(e => new KeyValuePair<string, T>(e.NameUpper, e.Id))
                   .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryFind(string name, out T id)
                => _nameToId.TryGetValue(name, out id);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryFindMatch(string name, T id)
                => _nameToId.TryGetValue(name, out T foundId) &&
                   EqualityComparer<T>.Default.Equals(id, foundId);

            private struct Entry
            {
                public string NameUpper;
                public int Hash;
                public T Id;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ComputeHash(string s)
            {
                uint hash = 5381;
                foreach (char c in s)
                {
                    hash = ((hash << 5) + hash) ^ c;
                }

                return (int)hash;
            }
        }
    }
}
