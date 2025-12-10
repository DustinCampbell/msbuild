// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
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
            if (TryGetLibrary(typeof(T), out var library))
            {
                return library.TryExecute(instance, methodName, args, out result);
            }

            result = default;
            return false;
        }

        private static bool TryExecute(Type receiverType, string methodName, ReadOnlySpan<object?> args, out object? result)
        {
            if (TryGetLibrary(receiverType, out var library))
            {
                return library.TryExecute(methodName, args, out result);
            }

            result = null;
            return false;
        }

        private static bool TryGetLibrary(Type type, [NotNullWhen(true)] out FunctionLibrary? library)
        {
            // PERF: For this small set of types, it is considerably faster to use a series of if
            // statements rather than a dictionary lookup. The JIT is able to optimize this well.
            // If the set of types grows significantly, consider switching to a dictionary.

            if (type == typeof(string))
            {
                library = StringLibrary.Instance;
                return true;
            }

            if (type == typeof(string[]))
            {
                library = StringArrayLibrary.Instance;
                return true;
            }

            if (type == typeof(Path))
            {
                library = PathLibrary.Instance;
                return true;
            }

            if (type == typeof(IntrinsicFunctions))
            {
                library = IntrinsicLibrary.Instance;
                return true;
            }

            if (type == typeof(Math))
            {
                library = MathLibrary.Instance;
                return true;
            }

            if (type == typeof(Directory))
            {
                library = DirectoryLibrary.Instance;
                return true;
            }

            if (type == typeof(char))
            {
                library = CharLibrary.Instance;
                return true;
            }

            if (type == typeof(int))
            {
                library = Int32Library.Instance;
                return true;
            }

            if (type == typeof(Guid))
            {
                library = GuidLibrary.Instance;
                return true;
            }

            if (type == typeof(Version))
            {
                library = VersionLibrary.Instance;
                return true;
            }

            if (type == typeof(Regex))
            {
                library = RegexLibrary.Instance;
                return true;
            }

            library = null;
            return false;
        }
    }
}
