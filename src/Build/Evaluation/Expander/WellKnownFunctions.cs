// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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

    internal static WellKnownFunctionResult TryInvokeStatic(
        Type receiverType,
        string methodName,
        ref FunctionArguments args,
        ref readonly ExecutionContext context,
        out object? returnVal)
    {
        // UNDONE: Directly returning NotRecognized from some handlers bypasses reflection-fallback logging below.
        // Preserve that behavior until logging is made consistent while adding more well-known functions.
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
            WellKnownFunctionResult result = s_stringHandler.TryInvokeStatic(methodName, ref args, out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }
        else if (receiverType == typeof(Math))
        {
            WellKnownFunctionResult result = s_mathHandler.TryInvokeStatic(methodName, ref args, out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }
        else if (receiverType == typeof(Version))
        {
            WellKnownFunctionResult result = s_versionHandler.TryInvokeStatic(methodName, ref args, out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }
        else if (receiverType == typeof(Guid))
        {
            WellKnownFunctionResult result = s_guidHandler.TryInvokeStatic(methodName, ref args, out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }
        else if (receiverType == typeof(char))
        {
            WellKnownFunctionResult result = s_charHandler.TryInvokeStatic(methodName, ref args, out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }
        else if (receiverType == typeof(System.Text.RegularExpressions.Regex))
        {
            WellKnownFunctionResult result = s_regexHandler.TryInvokeStatic(methodName, ref args, out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(isStatic: true, receiverType: receiverType, methodName: methodName, args: ref args);
        }

        return NotRecognized(out returnVal);
    }

    internal static WellKnownFunctionResult TryInvokeInstance(
        object objectInstance,
        string methodName,
        ref FunctionArguments args,
        out object? returnVal)
    {
        // UNDONE: Directly returning NotRecognized from the string handler bypasses reflection-fallback logging below.
        // Preserve that behavior until logging is made consistent while adding more well-known functions.
        if (objectInstance is string text)
        {
            return s_stringHandler.TryInvokeInstance(text, methodName, ref args, out returnVal);
        }

        if (objectInstance is string[] stringArray)
        {
            WellKnownFunctionResult result = s_stringArrayHandler.TryInvokeInstance(
                stringArray,
                methodName,
                ref args,
                out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }
        else if (objectInstance is int integer)
        {
            WellKnownFunctionResult result = s_int32Handler.TryInvokeInstance(integer, methodName, ref args, out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }
        else if (objectInstance is Version version)
        {
            WellKnownFunctionResult result = s_versionHandler.TryInvokeInstance(version, methodName, ref args, out returnVal);

            if (result != WellKnownFunctionResult.NotRecognized)
            {
                return result;
            }
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(isStatic: false, receiverType: objectInstance.GetType(), methodName: methodName, args: ref args);
        }

        return NotRecognized(out returnVal);
    }

    internal static WellKnownFunctionResult TryInvokeConstructor(
        Type receiverType,
        ref FunctionArguments args,
        out object? returnVal)
    {
        // UNDONE: Constructor calls that fall back to reflection are not recorded in the
        // PropertyFunctionsRequiringReflection log.
        if (receiverType == typeof(string))
        {
            if (args.Count == 0)
            {
                returnVal = string.Empty;
                return WellKnownFunctionResult.Handled;
            }

            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value))
            {
                returnVal = value;
                return WellKnownFunctionResult.Handled;
            }
        }

        return NotRecognized(out returnVal);
    }

    private static WellKnownFunctionResult NotRecognized(out object? result)
    {
        result = null;
        return WellKnownFunctionResult.NotRecognized;
    }

    private static void LogFunctionCall(bool isStatic, Type receiverType, string methodName, ref FunctionArguments args)
    {
        string logFile = Path.Combine(Directory.GetCurrentDirectory(), "PropertyFunctionsRequiringReflection");

        using var builder = new ValueStringBuilder(initialCapacity: 256);

        if (isStatic)
        {
            builder.Append("[static] Type=");
        }
        else
        {
            builder.Append("[instance] Type=");
        }

        builder.Append(receiverType.FullName);
        builder.Append("; ");

        builder.Append("MethodName=");
        builder.Append(methodName);
        builder.Append("(");

        bool isFirst = true;
        foreach (object? arg in args.MaterializeAll())
        {
            if (!isFirst)
            {
                builder.Append(", ");
            }
            else
            {
                isFirst = false;
            }

            builder.Append(arg?.GetType().Name ?? "null");
        }

        builder.Append(")\n");

        File.AppendAllText(logFile, builder.ToString());
    }
}
