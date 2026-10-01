// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Dispatches calls to optimized implementations of commonly used property functions.
/// </summary>
/// <remarks>
///  Reflection binding is expensive and can throw first-chance <see cref="MissingMethodException"/> exceptions.
///  Recognizing common calls avoids that cost and improves the debugging experience.
///  For background, see <see href="https://github.com/dotnet/msbuild/issues/2217">dotnet/msbuild#2217</see>.
/// </remarks>
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

    /// <summary>
    ///  Attempts to invoke a commonly used static function without reflection.
    /// </summary>
    /// <param name="receiverType">The type that declares the function.</param>
    /// <param name="methodName">The name of the function to call.</param>
    /// <param name="args">The function arguments.</param>
    /// <param name="context">The context for the expansion.</param>
    /// <returns>
    ///  The invocation result, including whether the function was handled.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeStatic(
        Type receiverType,
        string methodName,
        ref Arguments args,
        ref readonly ExpanderContext context)
        => receiverType == typeof(string)
            ? s_stringHandler.TryInvokeStatic(methodName, ref args)
         : receiverType == typeof(Math)
            ? s_mathHandler.TryInvokeStatic(methodName, ref args)
         : receiverType == typeof(IntrinsicFunctions)
            ? s_intrinsicFunctionsHandler.TryInvokeStatic(methodName, ref args, in context)
         : receiverType == typeof(Path)
            ? s_pathHandler.TryInvokeStatic(methodName, ref args)
         : receiverType == typeof(Version)
            ? s_versionHandler.TryInvokeStatic(methodName, ref args)
         : receiverType == typeof(Guid)
            ? s_guidHandler.TryInvokeStatic(methodName, ref args)
         : receiverType == typeof(char)
            ? s_charHandler.TryInvokeStatic(methodName, ref args)
         : receiverType == typeof(Regex)
            ? s_regexHandler.TryInvokeStatic(methodName, ref args)

         : NotHandled;

    /// <summary>
    ///  Attempts to invoke a commonly used instance function without reflection.
    /// </summary>
    /// <param name="objectInstance">The object on which to invoke the function.</param>
    /// <param name="methodName">The name of the function to call.</param>
    /// <param name="args">The function arguments.</param>
    /// <param name="context">The context for the expansion.</param>
    /// <returns>
    ///  The invocation result, including whether the function was handled.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeInstance(
        object objectInstance,
        string methodName,
        ref Arguments args,
        ref readonly ExpanderContext context)
        => objectInstance switch
        {
            string s => s_stringHandler.TryInvokeInstance(methodName, s, ref args),
            string[] a => s_stringArrayHandler.TryInvokeInstance(methodName, a, ref args),
            Version v => s_versionHandler.TryInvokeInstance(methodName, v, ref args),
            int i => s_int32Handler.TryInvokeInstance(methodName, i, ref args),

            _ => NotHandled,
        };

    /// <summary>
    ///  Attempts to invoke a commonly used constructor without reflection.
    /// </summary>
    /// <param name="receiverType">The type to construct.</param>
    /// <param name="args">The constructor arguments.</param>
    /// <param name="context">The context for the expansion.</param>
    /// <returns>
    ///  The invocation result, including whether the constructor was handled.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeConstructor(Type receiverType, ref Arguments args, ref readonly ExpanderContext context)
        => receiverType == typeof(string)
            ? s_stringHandler.TryInvokeConstructor(ref args)
            : receiverType == typeof(Version)
                ? s_versionHandler.TryInvokeConstructor(ref args)
                : NotHandled;

    private static WellKnownFunctionResult NotHandled
        => default;

    private static WellKnownFunctionResult Invoked(object? result)
        => new(WellKnownFunctionStatus.Invoked, result);
}
