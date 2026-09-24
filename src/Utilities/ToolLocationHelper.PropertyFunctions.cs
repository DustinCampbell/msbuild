// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
#pragma warning disable CA2255 // The module initializer registers this assembly's reflection-free property-function handler.
    [ModuleInitializer]
    internal static void RegisterPropertyFunctionHandler()
        => PropertyFunctionHandlerRegistry.Register(typeof(ToolLocationHelper), TryInvokePropertyFunction);
#pragma warning restore CA2255

    private static bool TryInvokePropertyFunction(string methodName, object?[] arguments, out object? result)
    {
        if (arguments is [])
        {
            if (methodName.Equals(nameof(CurrentToolsVersion), StringComparison.OrdinalIgnoreCase))
            {
                result = CurrentToolsVersion;
                return true;
            }

            if (methodName.Equals(nameof(PathToSystem), StringComparison.OrdinalIgnoreCase))
            {
                result = PathToSystem;
                return true;
            }
        }

        result = null;
        return false;
    }
}
