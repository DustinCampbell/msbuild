// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal partial class WellKnownFunctions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void LogFunctionCall(
        Type receiverType,
        StringSegment methodName,
        string fileName,
        object? objectInstance,
        ref FunctionArguments args)
    {
        string logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        string argSignature = string.Join(", ", args.MaterializeAll().Select(a => a?.GetType().Name ?? "null"));
        File.AppendAllText(
            logFile,
            $"ReceiverType={receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={methodName.ValueOrEmpty}({argSignature})\n");
    }

    internal static bool TryExecuteWellKnownFunction<T>(
        StringSegment methodName,
        Type receiverType,
        object? objectInstance,
        ref FunctionArguments args,
        IFileSystem fileSystem,
        string? startingDirectory,
        LoggingContext? loggingContext,
        IPropertyProvider<T> properties,
        out object? result)
        where T : class, IProperty
    {
        if (methodName.Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            return TryExecuteWellKnownConstructor(receiverType, ref args, out result);
        }

        if (objectInstance is string text)
        {
            return TryExecuteStringFunction(methodName, text, ref args, out result);
        }

        if (objectInstance is Version version)
        {
            return TryExecuteVersionFunction(methodName, version, ref args, out result);
        }

        if (objectInstance is CultureInfo culture)
        {
            return TryExecuteCultureInfoFunction(methodName, culture, ref args, out result);
        }

        if (objectInstance is DateTime dateTime)
        {
            return TryExecuteDateTimeFunction(methodName, dateTime, ref args, out result);
        }

        if (objectInstance is Array array)
        {
            if (methodName.Equals("GetValue", StringComparison.OrdinalIgnoreCase) && args.TryGetArg(out int index))
            {
                result = array.GetValue(index);
                return true;
            }

            if (methodName.Equals(nameof(Array.Length), StringComparison.OrdinalIgnoreCase) && args.Length == 0)
            {
                result = array.Length;
                return true;
            }
        }

        if (objectInstance is null)
        {
            if (receiverType == typeof(IntrinsicFunctions))
            {
                return TryExecuteIntrinsicFunction(
                    methodName,
                    ref args,
                    fileSystem,
                    startingDirectory,
                    loggingContext,
                    properties,
                    out result);
            }

            if (receiverType == typeof(string))
            {
                return TryExecuteStaticStringFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(Path))
            {
                return TryExecutePathFunction(methodName, out result, ref args);
            }

            if (receiverType == typeof(File))
            {
                return TryExecuteFileFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(Directory))
            {
                return TryExecuteDirectoryFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(Math))
            {
                if (methodName.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase) &&
                    args.TryGetArgs(out double maxLeft, out double maxRight))
                {
                    result = Math.Max(maxLeft, maxRight);
                    return true;
                }

                if (methodName.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase) &&
                    args.TryGetArgs(out double minLeft, out double minRight))
                {
                    result = Math.Min(minLeft, minRight);
                    return true;
                }
            }

            if (receiverType == typeof(Convert))
            {
                return TryExecuteConvertFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(Version))
            {
                return TryExecuteStaticVersionFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(CultureInfo))
            {
                return TryExecuteStaticCultureInfoFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(RuntimeInformation))
            {
                return TryExecuteRuntimeInformationFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(Environment))
            {
                return TryExecuteEnvironmentFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(DateTime))
            {
                return TryExecuteStaticDateTimeFunction(methodName, ref args, out result);
            }

            if (receiverType == typeof(Guid) &&
                methodName.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase) &&
                args.Length == 0)
            {
                result = Guid.NewGuid();
                return true;
            }

            if (receiverType == typeof(char) &&
                methodName.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase))
            {
                if (args.TryGetArg(out StringSegment character) && character.Length == 1)
                {
                    result = char.IsDigit(character[0]);
                    return true;
                }

                if (args.TryGetArgs(out string? characters, out int characterIndex) && characters is not null)
                {
                    result = char.IsDigit(characters, characterIndex);
                    return true;
                }
            }

            if (receiverType == typeof(Regex))
            {
                return TryExecuteRegexFunction(methodName, ref args, out result);
            }
        }

        if (methodName.Equals(nameof(ToString), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0 && objectInstance is not null)
            {
                result = objectInstance.ToString();
                return true;
            }

            if (objectInstance is int integer && args.TryGetArg(out string? format) && format is not null)
            {
                result = integer.ToString(format);
                return true;
            }
        }

        if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
        {
            LogFunctionCall(receiverType, methodName, "PropertyFunctionsRequiringReflection", objectInstance, ref args);
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteWellKnownConstructor(
        Type? receiverType,
        ref FunctionArguments args,
        out object? result)
    {
        if (receiverType == typeof(Version))
        {
            return TryExecuteVersionConstructor(ref args, out result);
        }

        if (receiverType == typeof(string))
        {
            if (args.Length == 0)
            {
                result = string.Empty;
                return true;
            }

            if (args.TryGetArg(out string? value) && value is not null)
            {
                result = value;
                return true;
            }
        }

        result = null;
        return false;
    }
}
