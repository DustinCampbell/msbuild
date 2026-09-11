// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private static readonly CharHandler s_charHandler = new();
    private static readonly GuidHandler s_guidHandler = new();
    private static readonly Int32Handler s_int32Handler = new();
    private static readonly IntrinsicHandler s_intrinsicHandler = new();
    private static readonly MathHandler s_mathHandler = new();
    private static readonly PathHandler s_pathHandler = new();
    private static readonly RegexHandler s_regexHandler = new();
    private static readonly StringArrayHandler s_stringArrayHandler = new();
    private static readonly StringHandler s_stringHandler = new();
    private static readonly VersionHandler s_versionHandler = new();

    /// <summary>
    /// Shortcut to avoid calling into binding if we recognize some most common functions.
    /// Binding is expensive and throws first-chance MissingMethodExceptions, which is
    /// bad for debugging experience and has a performance cost.
    /// A typical binding operation with exception can take ~1.500 ms; this call is ~0.050 ms
    /// (rough numbers just for comparison).
    /// See https://github.com/dotnet/msbuild/issues/2217.
    /// </summary>
    /// <param name="methodName"> </param>
    /// <param name="receiverType"> </param>
    /// <param name="objectInstance">Object that the function is called on.</param>
    /// <param name="args">arguments.</param>
    /// <param name="context">Dependencies used to execute contextual functions.</param>
    /// <param name="returnVal">The value returned from the function call.</param>
    /// <returns>True if the well known function call binding was successful.</returns>
    internal static bool TryExecuteWellKnownFunction(
        string methodName,
        Type receiverType,
        object? objectInstance,
        ref FunctionArguments args,
        ref readonly ExecutionContext context,
        out object? returnVal)
    {
        // UNDONE: Directly returning false from some handlers bypasses reflection-fallback logging below.
        // Preserve that behavior until logging is made consistent while adding more well-known functions.
        if (objectInstance is string text)
        {
            return s_stringHandler.TryInvokeInstance(text, methodName, ref args, out returnVal);
        }

        if (objectInstance is null)
        {
            if (receiverType == typeof(IntrinsicFunctions))
            {
                return s_intrinsicHandler.TryInvokeStatic(methodName, ref args, in context, out returnVal);
            }

            if (receiverType == typeof(Path))
            {
                return s_pathHandler.TryInvokeStatic(methodName, ref args, out returnVal);
            }

            if (receiverType == typeof(string))
            {
                if (s_stringHandler.TryInvokeStatic(methodName, ref args, out returnVal))
                {
                    return true;
                }
            }
            else if (receiverType == typeof(Math))
            {
                if (s_mathHandler.TryInvokeStatic(methodName, ref args, out returnVal))
                {
                    return true;
                }
            }
            else if (receiverType == typeof(Version))
            {
                if (s_versionHandler.TryInvokeStatic(methodName, ref args, out returnVal))
                {
                    return true;
                }
            }
            else if (receiverType == typeof(Guid))
            {
                if (s_guidHandler.TryInvokeStatic(methodName, ref args, out returnVal))
                {
                    return true;
                }
            }
            else if (receiverType == typeof(char))
            {
                if (s_charHandler.TryInvokeStatic(methodName, ref args, out returnVal))
                {
                    return true;
                }
            }
            else if (receiverType == typeof(System.Text.RegularExpressions.Regex))
            {
                if (s_regexHandler.TryInvokeStatic(methodName, ref args, out returnVal))
                {
                    return true;
                }
            }
        }
        else if (objectInstance is string[] stringArray)
        {
            if (s_stringArrayHandler.TryInvokeInstance(stringArray, methodName, ref args, out returnVal))
            {
                return true;
            }
        }
        else if (objectInstance is int integer)
        {
            if (s_int32Handler.TryInvokeInstance(integer, methodName, ref args, out returnVal))
            {
                return true;
            }
        }
        else if (objectInstance is Version version)
        {
            if (s_versionHandler.TryInvokeInstance(version, methodName, ref args, out returnVal))
            {
                return true;
            }
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", objectInstance, ref args);
        }

        return NotHandled(out returnVal);
    }

    /// <summary>
    /// Shortcut to avoid calling into binding if we recognize some most common constructors.
    /// Analogous to TryExecuteWellKnownFunction but guaranteed to not throw.
    /// </summary>
    /// <param name="receiverType"> Receiver type for the constructor. </param>
    /// <param name="args">Arguments.</param>
    /// <param name="context">Dependencies used to execute contextual functions.</param>
    /// <param name="returnVal">The instance as created by the constructor call.</param>
    /// <returns>True if the well known constructor call binding was successful.</returns>
    internal static bool TryExecuteWellKnownConstructorNoThrow(
        Type? receiverType,
        ref FunctionArguments args,
        ref readonly ExecutionContext context,
        out object? returnVal)
    {
        // UNDONE: Constructor calls that fall back to reflection are not recorded in the
        // PropertyFunctionsRequiringReflection log.
        if (receiverType == typeof(string))
        {
            if (args.Count == 0)
            {
                returnVal = string.Empty;
                return true;
            }

            if (args.TryGetArg(out string? value))
            {
                returnVal = value;
                return true;
            }
        }

        return NotHandled(out returnVal);
    }

    private static bool NotHandled(out object? result)
    {
        result = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogFunctionCall(
        Type receiverType,
        string methodName,
        string fileName,
        object? objectInstance,
        ref FunctionArguments args)
    {
        var logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        var argSignature = string.Join(", ", args.MaterializeAll().Select(a => a?.GetType().Name ?? "null"));

        File.AppendAllText(
            logFile,
            $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName}({argSignature})\n");
    }
}
