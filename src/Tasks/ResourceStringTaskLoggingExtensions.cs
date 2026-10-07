// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks;

internal static class ResourceStringTaskLoggingExtensions
{
    internal static string FormatResource(
        this TaskLoggingHelper log,
        ResourceString resource,
        params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(resource);
        return ResourceStringUtilities.FormatResourceStringIgnoreCodeAndKeyword(resource, args);
    }

    internal static string GetResource(this TaskLoggingHelper log, ResourceString resource)
        => log.FormatResource(resource);

    internal static void LogMessageFromResource(
        this TaskLoggingHelper log,
        ResourceString messageResource,
        params object?[] messageArgs)
        => log.LogMessageFromResource(MessageImportance.Normal, messageResource, messageArgs);

    internal static void LogMessageFromResource(
        this TaskLoggingHelper log,
        MessageImportance importance,
        ResourceString messageResource,
        params object?[] messageArgs)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(messageResource);

        if (log.LogsMessagesOfImportance(importance))
        {
            log.LogMessage(
                importance,
                ResourceStringUtilities.FormatResourceStringIgnoreCodeAndKeyword(messageResource, messageArgs));
        }
    }

    internal static void LogErrorFromResource(
        this TaskLoggingHelper log,
        ResourceString messageResource,
        params object?[] messageArgs)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(messageResource);
        log.LogError(ResourceStringUtilities.FormatResourceStringIgnoreCodeAndKeyword(messageResource, messageArgs));
    }

    internal static void LogErrorFromResource(
        this TaskLoggingHelper log,
        ResourceString? subcategoryResource,
        string? errorCode,
        string? helpKeyword,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ResourceString messageResource,
        params object?[] messageArgs)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(messageResource);
        log.LogError(
            subcategoryResource?.Resolve().OriginalText,
            errorCode,
            helpKeyword,
            file,
            lineNumber,
            columnNumber,
            endLineNumber,
            endColumnNumber,
            ResourceStringUtilities.FormatResourceStringIgnoreCodeAndKeyword(messageResource, messageArgs));
    }

    internal static void LogErrorWithCodeFromResource(
        this TaskLoggingHelper log,
        ResourceString messageResource,
        params object?[] messageArgs)
        => log.LogErrorWithCodeFromResource(null, null, 0, 0, 0, 0, messageResource, messageArgs);

    internal static void LogErrorWithCodeFromResource(
        this TaskLoggingHelper log,
        ResourceString? subcategoryResource,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ResourceString messageResource,
        params object?[] messageArgs)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(messageResource);

        string message = ResourceStringUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode,
            out _,
            messageResource,
            messageArgs);
        string? helpKeyword = log.HelpKeywordPrefix is null ? null : log.HelpKeywordPrefix + messageResource.ResourceName;

        log.LogError(
            subcategoryResource?.Resolve().OriginalText,
            errorCode,
            helpKeyword,
            file,
            lineNumber,
            columnNumber,
            endLineNumber,
            endColumnNumber,
            message);
    }

    internal static void LogWarningFromResource(
        this TaskLoggingHelper log,
        ResourceString messageResource,
        params object?[] messageArgs)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(messageResource);
        log.LogWarning(ResourceStringUtilities.FormatResourceStringIgnoreCodeAndKeyword(messageResource, messageArgs));
    }

    internal static void LogWarningFromResource(
        this TaskLoggingHelper log,
        ResourceString? subcategoryResource,
        string? warningCode,
        string? helpKeyword,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ResourceString messageResource,
        params object?[] messageArgs)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(messageResource);
        log.LogWarning(
            subcategoryResource?.Resolve().OriginalText,
            warningCode,
            helpKeyword,
            file,
            lineNumber,
            columnNumber,
            endLineNumber,
            endColumnNumber,
            ResourceStringUtilities.FormatResourceStringIgnoreCodeAndKeyword(messageResource, messageArgs));
    }

    internal static void LogWarningWithCodeFromResource(
        this TaskLoggingHelper log,
        ResourceString messageResource,
        params object?[] messageArgs)
        => log.LogWarningWithCodeFromResource(null, null, 0, 0, 0, 0, messageResource, messageArgs);

    internal static void LogWarningWithCodeFromResource(
        this TaskLoggingHelper log,
        ResourceString? subcategoryResource,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ResourceString messageResource,
        params object?[] messageArgs)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(messageResource);

        string message = ResourceStringUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? warningCode,
            out _,
            messageResource,
            messageArgs);
        string? helpKeyword = log.HelpKeywordPrefix is null ? null : log.HelpKeywordPrefix + messageResource.ResourceName;

        log.LogWarning(
            subcategoryResource?.Resolve().OriginalText,
            warningCode,
            helpKeyword,
            file,
            lineNumber,
            columnNumber,
            endLineNumber,
            endColumnNumber,
            message);
    }

    internal static void LogErrorWithCodeAndExceptionFromResource(
        this TaskLoggingHelper log,
        Exception exception,
        bool showStackTrace,
        bool showDetail,
        ResourceString messageResource,
        params object?[] messageArgs)
        => log.LogErrorWithCodeAndExceptionFromResources(
            exception,
            showStackTrace,
            showDetail,
            messageResource.ResourceName,
            messageArgs);

    internal static void LogErrorWithCodeAndExceptionFromResource(
        this TaskLoggingHelper log,
        Exception exception,
        bool showStackTrace,
        bool showDetail,
        string? file,
        ResourceString messageResource,
        params object?[] messageArgs)
        => log.LogErrorWithCodeAndExceptionFromResources(
            exception,
            showStackTrace,
            showDetail,
            file,
            messageResource.ResourceName,
            messageArgs);

    internal static void LogErrorWithCodeAndExceptionFromResource(
        this TaskLoggingHelper log,
        Exception exception,
        bool showStackTrace,
        bool showDetail,
        string? subcategory,
        string? file,
        int lineNumber,
        int columnNumber,
        int endLineNumber,
        int endColumnNumber,
        ResourceString messageResource,
        params object?[] messageArgs)
        => log.LogErrorWithCodeAndExceptionFromResources(
            exception,
            showStackTrace,
            showDetail,
            subcategory,
            file,
            lineNumber,
            columnNumber,
            endLineNumber,
            endColumnNumber,
            messageResource.ResourceName,
            messageArgs);
}
