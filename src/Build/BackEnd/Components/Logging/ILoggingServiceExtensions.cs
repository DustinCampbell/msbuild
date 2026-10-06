// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd.Logging
{
    internal static class ILoggingServiceExtensions
    {
        public static void LogErrorFromText(
            this ILoggingService service,
            BuildEventContext buildEventContext,
            string errorCode,
            string helpKeyword,
            IElementLocation location,
            string message)
            => service.LogErrorFromText(buildEventContext, Subcategory.None, errorCode, helpKeyword, location, message);

        public static void LogWarning(
            this ILoggingService service,
            BuildEventContext buildEventContext,
            IElementLocation location,
            string messageResourceName,
            params object[] messageArgs)
            => service.LogWarning(buildEventContext, Subcategory.None, location, messageResourceName, messageArgs);

        public static void LogWarningFromText(
            this ILoggingService service,
            BuildEventContext buildEventContext,
            string warningCode,
            string helpKeyword,
            IElementLocation location,
            string message)
            => service.LogWarningFromText(buildEventContext, Subcategory.None, warningCode, helpKeyword, location, message);
    }
}
