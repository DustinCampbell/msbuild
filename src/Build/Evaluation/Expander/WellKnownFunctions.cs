// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared.FileSystem;

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
    private static readonly ConvertHandler s_convertHandler = new();
    private static readonly CultureInfoHandler s_cultureInfoHandler = new();
    private static readonly DateTimeHandler s_dateTimeHandler = new();
    private static readonly EnvironmentHandler s_environmentHandler = new();
    private static readonly FileSystemHandler s_fileSystemHandler = new();
    private static readonly GuidHandler s_guidHandler = new();
    private static readonly IntrinsicFunctionsHandler s_intrinsicFunctionsHandler = new();
    private static readonly MathHandler s_mathHandler = new();
    private static readonly OperatingSystemHandler s_operatingSystemHandler = new();
    private static readonly OSPlatformHandler s_osPlatformHandler = new();
    private static readonly PathHandler s_pathHandler = new();
    private static readonly PrimitiveHandler s_primitiveHandler = new();
    private static readonly RegexHandler s_regexHandler = new();
    private static readonly RuntimeInformationHandler s_runtimeInformationHandler = new();
    private static readonly ArrayHandler s_arrayHandler = new();
    private static readonly StringHandler s_stringHandler = new();
    private static readonly StringComparerHandler s_stringComparerHandler = new();
    private static readonly TimeSpanHandler s_timeSpanHandler = new();
    private static readonly UriHandler s_uriHandler = new();
    private static readonly VersionHandler s_versionHandler = new();

    /// <summary>
    ///  Attempts to invoke a commonly used static function without reflection.
    /// </summary>
    /// <param name="receiverType">The type that declares the function.</param>
    /// <param name="methodName">The name of the function to call.</param>
    /// <param name="args">The function arguments.</param>
    /// <param name="fileSystem">The file system used by intrinsic functions.</param>
    /// <returns>
    ///  The invocation result, including whether the function was handled.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeStatic(Type receiverType, string methodName, object?[] args, IFileSystem fileSystem)
        => receiverType == typeof(string)
            ? s_stringHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(Convert)
            ? s_convertHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(System.Globalization.CultureInfo)
            ? s_cultureInfoHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(DateTime)
            ? s_dateTimeHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(Environment)
            ? s_environmentHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(Directory) || receiverType == typeof(File)
            ? s_fileSystemHandler.TryInvokeStatic(receiverType, methodName, args)
         : receiverType == typeof(Math)
            ? s_mathHandler.TryInvokeStatic(methodName, args)
#if NET
         : receiverType == typeof(OperatingSystem)
#else
         : receiverType == typeof(Microsoft.Build.Framework.OperatingSystem)
#endif
            ? s_operatingSystemHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(System.Runtime.InteropServices.OSPlatform)
            ? s_osPlatformHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(IntrinsicFunctions)
            ? s_intrinsicFunctionsHandler.TryInvokeStatic(methodName, args, fileSystem)
         : receiverType == typeof(Path)
            ? s_pathHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(Version)
            ? s_versionHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(Guid)
            ? s_guidHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(char)
            ? s_charHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(Regex)
            ? s_regexHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(System.Runtime.InteropServices.RuntimeInformation)
            ? s_runtimeInformationHandler.TryInvokeStatic(methodName, args)
         : IsNumericOrCharType(receiverType)
            ? s_primitiveHandler.TryInvokeStatic(receiverType, methodName, args)
         : receiverType == typeof(TimeSpan)
            ? s_timeSpanHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(StringComparer)
            ? s_stringComparerHandler.TryInvokeStatic(methodName, args)
         : receiverType == typeof(Uri)
            ? s_uriHandler.TryInvokeStatic(methodName, args)

         : NotHandled;

    /// <summary>
    ///  Attempts to invoke a commonly used instance function without reflection.
    /// </summary>
    /// <param name="objectInstance">The object on which to invoke the function.</param>
    /// <param name="methodName">The name of the function to call.</param>
    /// <param name="args">The function arguments.</param>
    /// <returns>
    ///  The invocation result, including whether the function was handled.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeInstance(object objectInstance, string methodName, object?[] args)
        => objectInstance switch
        {
            string s => s_stringHandler.TryInvokeInstance(methodName, s, args),
            StringComparer comparer => s_stringComparerHandler.TryInvokeInstance(methodName, comparer, args),
            Array array => s_arrayHandler.TryInvokeInstance(methodName, array, args),
            CaptureCollection captures => s_regexHandler.TryInvokeInstance(methodName, captures, args),
            System.Globalization.CultureInfo culture => s_cultureInfoHandler.TryInvokeInstance(methodName, culture, args),
            DateTime dateTime => s_dateTimeHandler.TryInvokeInstance(methodName, dateTime, args),
            FileSystemInfo fileSystemInfo => s_fileSystemHandler.TryInvokeInstance(methodName, fileSystemInfo, args),
            GroupCollection groups => s_regexHandler.TryInvokeInstance(methodName, groups, args),
            Match match => s_regexHandler.TryInvokeInstance(methodName, match, args),
            MatchCollection matches => s_regexHandler.TryInvokeInstance(methodName, matches, args),
            Group group => s_regexHandler.TryInvokeInstance(methodName, group, args),
            Capture capture => s_regexHandler.TryInvokeInstance(methodName, capture, args),
            Guid guid => s_guidHandler.TryInvokeInstance(methodName, guid, args),
            Regex regex => s_regexHandler.TryInvokeInstance(methodName, regex, args),
            Uri uri => s_uriHandler.TryInvokeInstance(methodName, uri, args),
            UriBuilder builder => s_uriHandler.TryInvokeInstance(methodName, builder, args),
            Version v => s_versionHandler.TryInvokeInstance(methodName, v, args),
            System.Runtime.InteropServices.OSPlatform platform => s_osPlatformHandler.TryInvokeInstance(methodName, platform, args),
            _ when objectInstance.GetType().IsPrimitive || objectInstance is decimal
                => s_primitiveHandler.TryInvokeInstance(methodName, objectInstance, args),

            _ => NotHandled,
        };

    /// <summary>
    ///  Attempts to invoke a context-dependent static function without reflection.
    /// </summary>
    /// <typeparam name="T">The type of property supplied by <paramref name="properties"/>.</typeparam>
    /// <param name="receiverType">The type that declares the function.</param>
    /// <param name="methodName">The name of the function to call.</param>
    /// <param name="args">The function arguments.</param>
    /// <param name="properties">The properties available to the function.</param>
    /// <param name="loggingContext">The logging context for the function.</param>
    /// <returns>
    ///  The invocation result, including whether the function was handled.
    /// </returns>
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
    ///  Attempts to invoke a commonly used constructor without reflection.
    /// </summary>
    /// <param name="receiverType">The type to construct.</param>
    /// <param name="args">The constructor arguments.</param>
    /// <returns>
    ///  The invocation result, including whether the constructor was handled.
    /// </returns>
    public static WellKnownFunctionResult TryInvokeConstructor(Type receiverType, object?[] args)
        => receiverType == typeof(string)
            ? s_stringHandler.TryInvokeConstructor(args)
            : receiverType == typeof(System.Globalization.CultureInfo)
            ? s_cultureInfoHandler.TryInvokeConstructor(args)
            : receiverType == typeof(Regex)
            ? s_regexHandler.TryInvokeConstructor(args)
            : receiverType == typeof(Guid)
            ? s_guidHandler.TryInvokeConstructor(args)
            : receiverType == typeof(Uri) || receiverType == typeof(UriBuilder)
            ? s_uriHandler.TryInvokeConstructor(receiverType, args)
            : receiverType == typeof(Version)
            ? s_versionHandler.TryInvokeConstructor(args)
            : NotHandled;

    private static WellKnownFunctionResult NotHandled
        => default;

    private static WellKnownFunctionResult Invoked(object? result)
        => new(WellKnownFunctionStatus.Invoked, result);

    private static bool IsNumericOrCharType(Type type)
        => type == typeof(byte)
        || type == typeof(sbyte)
        || type == typeof(short)
        || type == typeof(ushort)
        || type == typeof(int)
        || type == typeof(uint)
        || type == typeof(long)
        || type == typeof(ulong)
        || type == typeof(float)
        || type == typeof(double)
        || type == typeof(decimal)
        || type == typeof(char);
}
