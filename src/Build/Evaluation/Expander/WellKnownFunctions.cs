// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private static readonly CharLibrary s_charLibrary = new();
        private static readonly GuidLibrary s_guidLibrary = new();
        private static readonly Int32Library s_intLibrary = new();
        private static readonly IntrinsicLibrary s_intrinsicLibrary = new();
        private static readonly MathLibrary s_mathLibrary = new();
        private static readonly PathLibrary s_pathLibrary = new();
        private static readonly RegexLibrary s_regexLibrary = new();
        private static readonly StringLibrary s_stringLibrary = new();
        private static readonly StringArrayLibrary s_stringArrayLibrary = new();
        private static readonly VersionLibrary s_versionLibrary = new();

        private static readonly FrozenDictionary<Type, FunctionLibrary> s_typeToFunctionLibraryMap = new Dictionary<Type, FunctionLibrary>()
        {
            { typeof(char), s_charLibrary },
            { typeof(Guid), s_guidLibrary },
            { typeof(int), s_intLibrary },
            { typeof(IntrinsicFunctions), s_intrinsicLibrary },
            { typeof(Math), s_mathLibrary },
            { typeof(Path), s_pathLibrary },
            { typeof(Regex), s_regexLibrary },
            { typeof(string), s_stringLibrary },
            { typeof(string[]), s_stringArrayLibrary },
            { typeof(Version), s_versionLibrary },
        }
        .ToFrozenDictionary();

        /// <summary>
        /// Shortcut to avoid calling into binding if we recognize some most common functions.
        /// Binding is expensive and throws first-chance MissingMethodExceptions, which is
        /// bad for debugging experience and has a performance cost.
        /// A typical binding operation with exception can take ~1.500 ms; this call is ~0.050 ms
        /// (rough numbers just for comparison).
        /// See https://github.com/dotnet/msbuild/issues/2217.
        /// </summary>
        /// <param name="receiverType"> </param>
        /// <param name="methodName"> </param>
        /// <param name="instance">Object that the function is called on.</param>
        /// <param name="args">arguments.</param>
        /// <param name="result">The value returned from the function call.</param>
        /// <returns>True if the well known function call binding was successful.</returns>
        internal static bool TryExecute(Type receiverType, string methodName, object? instance, ReadOnlySpan<object?> args, out object? result)
            => instance switch
            {
                null when TryExecute(receiverType, methodName, args, out result) => true,
                string s when TryExecute(s, methodName, args, out result) => true,
                string[] s when TryExecute(s, methodName, args, out result) => true,
                Version v when TryExecute(v, methodName, args, out result) => true,
                int i when TryExecute(i, methodName, args, out result) => true,

                _ => LogReflectionRequired(receiverType, methodName, instance, args, out result),
            };

        private static bool LogReflectionRequired(Type receiverType, string methodName, object? instance, ReadOnlySpan<object?> args, out object? returnVal)
        {
            if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
            {
                string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "PropertyFunctionsRequiringReflection");

                using var builder = new SpanBasedStringBuilder();

                bool first = true;

                foreach (object? arg in args)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        builder.Append(", ");
                    }

                    builder.Append(arg?.GetType().Name ?? "null");
                }

                string signature = builder.ToString();

                File.AppendAllText(logFilePath, $"ReceiverType={receiverType.FullName}; ObjectInstanceType={instance?.GetType().FullName}; MethodName={methodName}({signature})\n");
            }

            returnVal = null;
            return false;
        }

        private static bool TryExecute<T>(T instance, string methodName, ReadOnlySpan<object?> args, out object? result)
        {
            if (s_typeToFunctionLibraryMap.TryGetValue(typeof(T), out var library))
            {
                return library.TryExecute(instance, methodName, args, out result);
            }

            result = default;
            return false;
        }

        private static bool TryExecute(Type receiverType, string methodName, ReadOnlySpan<object?> args, out object? result)
        {
            if (s_typeToFunctionLibraryMap.TryGetValue(receiverType, out var library))
            {
                return library.TryExecute(methodName, args, out result);
            }

            if (args.Length == 3 && s_regexLibrary.TryExecute(methodName, args, out result))
            {
                return true;
            }

            result = null;
            return false;
        }
    }
}
