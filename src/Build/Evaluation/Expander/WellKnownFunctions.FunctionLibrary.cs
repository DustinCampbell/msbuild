// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private abstract class FunctionLibrary
        {
            protected delegate bool StaticFunc(ReadOnlySpan<object?> args, out object? result);
            protected delegate bool InstanceFunc<T>(T instance, ReadOnlySpan<object?> args, out object? result);

            protected readonly ref struct Builder(
                Dictionary<string, object> nameToInstanceFuncMap,
                Dictionary<string, StaticFunc> nameToStaticFuncMap)
            {
                private readonly Dictionary<string, object> _nameToInstanceFuncMap = nameToInstanceFuncMap;
                private readonly Dictionary<string, StaticFunc> _nameToStaticFuncMap = nameToStaticFuncMap;

                public void Add<TInstance>(string methodName, InstanceFunc<TInstance> instanceFunc)
                    => _nameToInstanceFuncMap.Add(methodName, instanceFunc);

                public void Add(string methodName, StaticFunc staticFunc)
                    => _nameToStaticFuncMap.Add(methodName, staticFunc);
            }

            private readonly FrozenDictionary<string, object> _nameToInstanceFuncMap;
            private readonly FrozenDictionary<string, StaticFunc> _nameToStaticFuncMap;

            protected FunctionLibrary()
            {
                var nameToInstanceFuncMap = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var nameToStaticFuncMap = new Dictionary<string, StaticFunc>(StringComparer.OrdinalIgnoreCase);

                var builder = new Builder(nameToInstanceFuncMap, nameToStaticFuncMap);
                Initialize(ref builder);

                _nameToInstanceFuncMap = nameToInstanceFuncMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
                _nameToStaticFuncMap = nameToStaticFuncMap.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            }

            protected abstract void Initialize(ref Builder builder);

            public bool TryExecute<T>(T instance, string methodName, ReadOnlySpan<object?> args, out object? result)
            {
                if (_nameToInstanceFuncMap.TryGetValue(methodName, out var obj) &&
                    obj is InstanceFunc<T> instanceFunc)
                {
                    return instanceFunc(instance, args, out result);
                }

                result = null;
                return false;
            }

            public bool TryExecute(string methodName, ReadOnlySpan<object?> args, out object? result)
            {
                if (_nameToStaticFuncMap.TryGetValue(methodName, out var staticFunc))
                {
                    return staticFunc(args, out result);
                }

                result = null;
                return false;
            }

            protected static bool TryExecuteArithmeticFunction(
                ReadOnlySpan<object?> args,
                Func<int, int, int> integerFunc,
                out object? result)
            {
                if (args is [var arg0, var arg1] &&
                    TryConvertToInt(arg0, out var a) &&
                    TryConvertToInt(arg1, out var b))
                {
                    result = integerFunc(a, b);
                    return true;
                }

                result = null;
                return false;
            }

            protected static bool TryExecuteArithmeticFunction(
                ReadOnlySpan<object?> args,
                Func<double, double, double> realFunc,
                out object? result)
            {
                if (args is [var arg0, var arg1] &&
                    TryConvertToDouble(arg0, out var a) &&
                    TryConvertToDouble(arg1, out var b))
                {
                    result = realFunc(a, b);
                    return true;
                }

                result = null;
                return false;
            }

            /// <summary>
            ///  Executes an arithmetic operation on two arguments, automatically selecting between integer and floating-point overloads.
            ///  Prefers integer arithmetic if both arguments can be converted to long; otherwise, uses double arithmetic.
            /// </summary>
            /// <param name="args">The array of arguments (must contain exactly two elements).</param>
            /// <param name="integerFunc">The operation to perform for integer arithmetic.</param>
            /// <param name="realFunc">The operation to perform for floating-point arithmetic.</param>
            /// <param name="result">The result of the operation, or <see langword="null"/> if execution failed.</param>
            /// <returns>
            ///  <see langword="true"/> if the operation was successfully executed; otherwise, <see langword="false"/>.
            /// </returns>
            protected static bool TryExecuteArithmeticFunctionWithOverflow(
                ReadOnlySpan<object?> args,
                Func<long, long, long> integerFunc,
                Func<double, double, double> realFunc,
                out object? result)
            {
                if (args is [var arg0, var arg1])
                {
                    if (TryConvertToLong(arg0, out var longA) &&
                        TryConvertToLong(arg1, out var longB))
                    {
                        result = integerFunc(longA, longB);
                        return true;
                    }

                    if (TryConvertToDouble(arg0, out var doubleA) &&
                        TryConvertToDouble(arg1, out var doubleB))
                    {
                        result = realFunc(doubleA, doubleB);
                        return true;
                    }
                }

                result = null;
                return false;
            }
        }
    }
}
