// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Shared;

internal static class EventsCreatorHelper
{
    public static BuildMessageEventArgs CreateMessageEventFromText(
        BuildEventContext buildEventContext,
        MessageImportance importance,
        string message,
        params object?[]? messageArgs)
    {
        Assumed.NotNull(buildEventContext);
        Assumed.NotNull(message);

        return new(
            message,
            helpKeyword: null,
            senderName: "MSBuild",
            importance,
            DateTime.UtcNow,
            messageArgs)
        {
            BuildEventContext = buildEventContext
        };
    }

    public static BuildErrorEventArgs CreateErrorEventFromText(
        BuildEventContext buildEventContext,
        Subcategory subcategory,
        string? errorCode,
        string? helpKeyword,
        IElementLocation location,
        string message)
    {
        Assumed.NotNull(buildEventContext);
        Assumed.NotNull(location);
        Assumed.NotNull(message);

        return new(
            subcategory.GetDisplayString(),
            errorCode,
            location.File,
            location.Line,
            location.Column,
            endLineNumber: 0,
            endColumnNumber: 0,
            message,
            helpKeyword,
            "MSBuild")
        {
            BuildEventContext = buildEventContext,
        };
    }

    public static BuildWarningEventArgs CreateWarningEventFromText(
        BuildEventContext buildEventContext,
        Subcategory subcategory,
        string? errorCode,
        string? helpKeyword,
        IElementLocation location,
        string message)
    {
        Assumed.NotNull(buildEventContext);
        Assumed.NotNull(location);
        Assumed.NotNull(message);

        return new(
            subcategory.GetDisplayString(),
            errorCode,
            location.File,
            location.Line,
            location.Column,
            endLineNumber: 0,
            endColumnNumber: 0,
            message,
            helpKeyword,
            "MSBuild")
        {
            BuildEventContext = buildEventContext,
        };
    }
}
