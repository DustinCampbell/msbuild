// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;

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
        }
    }
}
