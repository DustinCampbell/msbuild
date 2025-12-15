// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation.Expander;

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
    internal static Result TryExecute(Type receiverType, string methodName, object? instance, ReadOnlySpan<object?> args)
        => instance switch
        {
            null => TryExecute(receiverType, methodName, args),
            string s => TryExecute(s, methodName, args),
            string[] s => TryExecute(s, methodName, args),
            Version v => TryExecute(v, methodName, args),
            int i => TryExecute(i, methodName, args),

            _ => LogReflectionRequired(receiverType, methodName, instance, args),
        };

    private static Result LogReflectionRequired(Type receiverType, string methodName, object? instance, ReadOnlySpan<object?> args)
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

        return Result.None;
    }

    private static Result TryExecute<T>(T instance, string methodName, ReadOnlySpan<object?> args)
    {
        if (methodName.Equals("ToString", StringComparison.OrdinalIgnoreCase))
        {
            return TryExecuteToString(instance, args);
        }

        if (TryGetLibrary(out IInstanceMethodLibrary<T>? library))
        {
            return library.TryExecute(instance, methodName, args);
        }

        return Result.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Result TryExecuteToString<T>(T instance, ReadOnlySpan<object?> args)
    {
        if (instance is null)
        {
            return Result.None;
        }

        // PERF: Handle no args case first to avoid dispatch.
        if (args is [])
        {
            return Result.From(instance.ToString());
        }

        return instance switch
        {
            int i => Int32Library.Instance.TryExecuteToString(i, args),
            Version v => VersionLibrary.Instance.TryExecuteToString(v, args),
            Guid g => GuidLibrary.Instance.TryExecuteToString(g, args),

            _ => Result.None,
        };
    }

    private static Result TryExecute(Type receiverType, string methodName, ReadOnlySpan<object?> args)
        => TryGetLibrary(receiverType, out IStaticMethodLibrary? library)
            ? library.TryExecute(methodName, args)
            : Result.None;

    private static bool TryGetLibrary(Type type, [NotNullWhen(true)] out IStaticMethodLibrary? library)
    {
        // PERF: For this small set of types, it is considerably faster to use a series of if
        // statements rather than a dictionary lookup. The JIT is able to optimize this well.
        // If the set of types grows significantly, consider switching to a dictionary.

        if (type == typeof(string))
        {
            library = StringLibrary.Instance;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetLibrary<T>([NotNullWhen(true)] out IInstanceMethodLibrary<T>? library)
    {
        // PERF: For this small set of types, it is considerably faster to use a series of if
        // statements rather than a dictionary lookup. The JIT is able to optimize this well.
        // If the set of types grows significantly, consider switching to a dictionary.

        if (typeof(T) == typeof(string))
        {
            library = (IInstanceMethodLibrary<T>)(object)StringLibrary.Instance;
            return true;
        }

        if (typeof(T) == typeof(string[]))
        {
            library = (IInstanceMethodLibrary<T>)(object)StringArrayLibrary.Instance;
            return true;
        }

        if (typeof(T) == typeof(int))
        {
            library = (IInstanceMethodLibrary<T>)(object)Int32Library.Instance;
            return true;
        }

        if (typeof(T) == typeof(Version))
        {
            library = (IInstanceMethodLibrary<T>)(object)VersionLibrary.Instance;
            return true;
        }

        library = null;
        return false;
    }
}
