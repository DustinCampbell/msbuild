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
        IElementLocation location,
        string message)
        => context.DispatchAsErrorFromText(Subcategory.None, errorCode, helpKeyword, location, message);

    public static void DispatchAsWarningFromText(
        this ICheckContext context,
        string? warningCode,
        string? helpKeyword,
        IElementLocation location,
        string message)
        => context.DispatchAsWarningFromText(Subcategory.None, warningCode, helpKeyword, location, message);
}
