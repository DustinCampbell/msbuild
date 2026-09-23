// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private static readonly CharHandler s_charHandler = new();
    private static readonly GuidHandler s_guidHandler = new();
    private static readonly Int32Handler s_int32Handler = new();
    private static readonly IntrinsicFunctionsHandler s_intrinsicFunctionsHandler = new();
    private static readonly MathHandler s_mathHandler = new();
    private static readonly PathHandler s_pathHandler = new();
    private static readonly RegexHandler s_regexHandler = new();
    private static readonly StringArrayHandler s_stringArrayHandler = new();
    private static readonly StringHandler s_stringHandler = new();
    private static readonly VersionHandler s_versionHandler = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogFunctionCall(Type receiverType, string methodName, string fileName, object? objectInstance, object?[] args)
    {
        string logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);

        string argSignature = args is not null
            ? string.Join(", ", args.Select(a => a?.GetType().Name ?? "null"))
            : string.Empty;

        File.AppendAllText(logFile, $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName}({argSignature})\n");
    }

    /// <summary>
    ///  Shortcut to avoid calling into binding if we recognize some of the most common functions.
    ///  Binding is expensive and throws first-chance <see cref="MissingMethodException"/> exceptions,
    ///  which is bad for the debugging experience and has a performance cost.
    ///  A typical binding operation with an exception can take ~1.500 ms; this call is ~0.050 ms
    ///  (rough numbers just for comparison).
    ///  See https://github.com/dotnet/msbuild/issues/2217.
    /// </summary>
    /// <param name="receiverType">The type that declares the function.</param>
    /// <param name="methodName">The name of the function to call.</param>
    /// <param name="args">The function arguments.</param>
    /// <param name="fileSystem">The file system used by intrinsic functions.</param>
    /// <returns>
    ///  The invocation status and result.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeStatic(Type receiverType, string methodName, object?[] args, IFileSystem fileSystem)
    {
        WellKnownFunctionResult result = WellKnownFunctionResult.NotHandled;

        if (receiverType == typeof(string))
        {
            result = s_stringHandler.TryInvokeStatic(methodName, args);
        }
        else if (receiverType == typeof(Math))
        {
            result = s_mathHandler.TryInvokeStatic(methodName, args);
        }
        else if (receiverType == typeof(IntrinsicFunctions))
        {
            result = s_intrinsicFunctionsHandler.TryInvokeStatic(methodName, args, fileSystem);
        }
        else if (receiverType == typeof(Path))
        {
            result = s_pathHandler.TryInvokeStatic(methodName, args);
        }
        else if (receiverType == typeof(Version))
        {
            result = s_versionHandler.TryInvokeStatic(methodName, args);
        }
        else if (receiverType == typeof(Guid))
        {
            result = s_guidHandler.TryInvokeStatic(methodName, args);
        }
        else if (receiverType == typeof(char))
        {
            result = s_charHandler.TryInvokeStatic(methodName, args);
        }
        else if (receiverType == typeof(Regex))
        {
            result = s_regexHandler.TryInvokeStatic(methodName, args);
        }

        if (result.Status == WellKnownFunctionStatus.Invoked)
        {
            return result;
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", null, args);
        }

        return NotHandled;
    }

    public static WellKnownFunctionResult TryInvokeInstance(object objectInstance, string methodName, object?[] args)
    {
        WellKnownFunctionResult result = WellKnownFunctionResult.NotHandled;

        if (objectInstance is string s)
        {
            result = s_stringHandler.TryInvokeInstance(methodName, s, args);
        }
        else if (objectInstance is string[] a)
        {
            result = s_stringArrayHandler.TryInvokeInstance(methodName, a, args);
        }
        else if (objectInstance is Version v)
        {
            result = s_versionHandler.TryInvokeInstance(methodName, v, args);
        }
        else if (objectInstance is int i)
        {
            result = s_int32Handler.TryInvokeInstance(methodName, i, args);
        }

        if (result.Status == WellKnownFunctionStatus.Invoked)
        {
            return result;
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(objectInstance.GetType(), methodName, "PropertyFunctionsRequiringReflection", objectInstance, args);
        }

        return NotHandled;
    }

    public static WellKnownFunctionResult TryInvokeStatic<T>(
        Type receiverType,
        string methodName,
        object?[] args,
        IPropertyProvider<T> properties,
        LoggingContext loggingContext)
        where T : class, IProperty
        => receiverType == typeof(IntrinsicFunctions)
            ? s_intrinsicFunctionsHandler.TryInvokeStatic(methodName, args, properties, loggingContext)
            : NotHandled;

    /// <summary>
    ///  Shortcut to avoid calling into binding if we recognize some of the most common constructors.
    ///  Analogous to <see cref="TryInvokeStatic"/> but guaranteed not to throw.
    /// </summary>
    /// <param name="receiverType">The receiver type for the constructor.</param>
    /// <param name="args">Arguments.</param>
    /// <returns>
    ///  The invocation status and result.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeConstructor(Type receiverType, object?[] args)
    {
        if (receiverType == typeof(string))
        {
            if (args.Length == 0)
            {
                return Invoked(string.Empty);
            }

            if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
            {
                return Invoked(arg0);
            }
        }

        return NotHandled;
    }

    private static WellKnownFunctionResult NotHandled
        => default;

    private static WellKnownFunctionResult Invoked(object? result)
        => new(WellKnownFunctionStatus.Invoked, result);
}
