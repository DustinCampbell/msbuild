// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

internal static class ICheckContextExtensions
{
    public static void DispatchAsErrorFromText(
        this ICheckContext context,
        string? errorCode,
        string? helpKeyword,
        BuildEventFileInfo file,
        string message)
        => context.DispatchAsErrorFromText(Subcategory.None, errorCode, helpKeyword, file, message);

    public static void DispatchAsWarningFromText(
        this ICheckContext context,
        string? warningCode,
        string? helpKeyword,
        BuildEventFileInfo file,
        string message)
        => context.DispatchAsWarningFromText(Subcategory.None, warningCode, helpKeyword, file, message);
}
