// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Shared;

internal static class MSBuildArchitecture
{
    public static string Current => field ??= IntPtr.Size == sizeof(long) ? x64 : x86;

    internal const string x86 = "x86";
    internal const string x64 = "x64";
    internal const string arm64 = "arm64";
    internal const string currentArchitecture = "CurrentArchitecture";
    internal const string any = "*";
}
