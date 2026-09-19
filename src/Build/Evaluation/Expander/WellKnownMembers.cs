// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

#if NET
using OperatingSystem = System.OperatingSystem;
#else
using OperatingSystem = Microsoft.Build.Framework.OperatingSystem;
#endif

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private static readonly CharHandler s_charHandler = new();
    private static readonly ConvertHandler s_convertHandler = new();
    private static readonly CultureInfoHandler s_cultureInfoHandler = new();
    private static readonly DateTimeHandler s_dateTimeHandler = new();
    private static readonly DirectoryHandler s_directoryHandler = new();
    private static readonly DoubleHandler s_doubleHandler = new();
    private static readonly EnvironmentHandler s_environmentHandler = new();
    private static readonly FileHandler s_fileHandler = new();
    private static readonly GuidHandler s_guidHandler = new();
    private static readonly Int32Handler s_int32Handler = new();
    private static readonly Int64Handler s_int64Handler = new();
    private static readonly IntrinsicHandler s_intrinsicHandler = new();
    private static readonly MathHandler s_mathHandler = new();
    private static readonly PathHandler s_pathHandler = new();
    private static readonly OperatingSystemHandler s_operatingSystemHandler = new();
    private static readonly OSPlatformHandler s_osPlatformHandler = new();
    private static readonly RegexHandler s_regexHandler = new();
    private static readonly RegexGroupHandler s_regexGroupHandler = new();
    private static readonly RegexGroupCollectionHandler s_regexGroupCollectionHandler = new();
    private static readonly RegexMatchHandler s_regexMatchHandler = new();
    private static readonly RuntimeInformationHandler s_runtimeInformationHandler = new();
    private static readonly StringArrayHandler s_stringArrayHandler = new();
    private static readonly StringHandler s_stringHandler = new();
    private static readonly TimeSpanHandler s_timeSpanHandler = new();
    private static readonly VersionHandler s_versionHandler = new();

    internal static WellKnownMemberResult TryInvokeStatic(
        Type receiverType,
        StringSegment memberName,
        ref FunctionArguments args,
        ref readonly ExecutionContext context)
    {
        if (receiverType == typeof(IntrinsicFunctions))
        {
            return s_intrinsicHandler.TryInvokeStatic(memberName, ref args, in context);
        }

        if (receiverType == typeof(Path))
        {
            return s_pathHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(Directory))
        {
            return s_directoryHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(File))
        {
            return s_fileHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(string))
        {
            return s_stringHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(Environment))
        {
            return s_environmentHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(Math))
        {
            return s_mathHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(Version))
        {
            return s_versionHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(CultureInfo))
        {
            return s_cultureInfoHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(Guid))
        {
            return s_guidHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(char))
        {
            return s_charHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(Convert))
        {
            return s_convertHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(DateTime))
        {
            return s_dateTimeHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(TimeSpan))
        {
            return s_timeSpanHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(OperatingSystem))
        {
            return s_operatingSystemHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(RuntimeInformation))
        {
            return s_runtimeInformationHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(OSPlatform))
        {
            return s_osPlatformHandler.TryInvokeStatic(memberName, ref args);
        }

        if (receiverType == typeof(System.Text.RegularExpressions.Regex))
        {
            return s_regexHandler.TryInvokeStatic(memberName, ref args);
        }

        return NotRecognized;
    }

    internal static WellKnownMemberResult TryGetStatic(Type receiverType, StringSegment memberName)
    {
        if (receiverType == typeof(Path))
        {
            return s_pathHandler.TryGetStatic(memberName);
        }

        if (receiverType == typeof(int))
        {
            return s_int32Handler.TryGetStatic(memberName);
        }

        if (receiverType == typeof(long))
        {
            return s_int64Handler.TryGetStatic(memberName);
        }

        if (receiverType == typeof(double))
        {
            return s_doubleHandler.TryGetStatic(memberName);
        }

        if (receiverType == typeof(DateTime))
        {
            return s_dateTimeHandler.TryGetStatic(memberName);
        }

        if (receiverType == typeof(OSPlatform))
        {
            return s_osPlatformHandler.TryGetStatic(memberName);
        }

        if (receiverType == typeof(RuntimeInformation))
        {
            return s_runtimeInformationHandler.TryGetStatic(memberName);
        }

        return NotRecognized;
    }

    internal static WellKnownMemberResult TryInvokeInstance(object objectInstance, StringSegment memberName, ref FunctionArguments args)
    {
        switch (objectInstance)
        {
            case string s:
                return s_stringHandler.TryInvokeInstance(s, memberName, ref args);

            case string[] stringArray:
                return s_stringArrayHandler.TryInvokeInstance(stringArray, memberName, ref args);

            case double d:
                return s_doubleHandler.TryInvokeInstance(d, memberName, ref args);

            case int i:
                return s_int32Handler.TryInvokeInstance(i, memberName, ref args);

            case long l:
                return s_int64Handler.TryInvokeInstance(l, memberName, ref args);

            case DateTime dt:
                return s_dateTimeHandler.TryInvokeInstance(dt, memberName, ref args);

            case TimeSpan ts:
                return s_timeSpanHandler.TryInvokeInstance(ts, memberName, ref args);

            case Version version:
                return s_versionHandler.TryInvokeInstance(version, memberName, ref args);

            case CultureInfo c:
                return s_cultureInfoHandler.TryInvokeInstance(c, memberName, ref args);

            case OSPlatform op:
                return s_osPlatformHandler.TryInvokeInstance(op, memberName, ref args);

            case System.Text.RegularExpressions.GroupCollection gc:
                return s_regexGroupCollectionHandler.TryInvokeInstance(gc, memberName, ref args);
        }

        return NotRecognized;
    }

    internal static WellKnownMemberResult TryGetInstance(object objectInstance, StringSegment memberName)
        => objectInstance switch
        {
            string s => s_stringHandler.TryGetInstance(s, memberName),
            DateTime dt => s_dateTimeHandler.TryGetInstance(dt, memberName),
            System.Text.RegularExpressions.Match m => s_regexMatchHandler.TryGetInstance(m, memberName),
            System.Text.RegularExpressions.Group g => s_regexGroupHandler.TryGetInstance(g, memberName),
            _ => NotRecognized,
        };

    internal static WellKnownMemberResult TryInvokeConstructor(Type receiverType, ref FunctionArguments args)
    {
        if (receiverType == typeof(string))
        {
            if (args.Count == 0)
            {
                return Handled(string.Empty);
            }

            if (args.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? value))
            {
                return Handled(value);
            }
        }

        if (receiverType == typeof(Version) &&
            args.Count == 1 &&
            FunctionArgumentCoercion.TryCoerce(args.GetValue(0), out string? version))
        {
            return Handled(new Version(version));
        }

        return NotRecognized;
    }

    private static WellKnownMemberResult Handled(object? result)
        => WellKnownMemberResult.Handled(result);

    private static WellKnownMemberResult InvalidArguments
        => WellKnownMemberResult.InvalidArguments;

    private static WellKnownMemberResult NotRecognized
        => WellKnownMemberResult.NotRecognized;
}
