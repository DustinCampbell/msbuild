// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Microsoft.Build.Framework;

internal delegate bool PropertyFunctionHandler(string methodName, object?[] arguments, out object? result);

internal static class PropertyFunctionHandlerRegistry
{
    private static readonly object s_lock = new();
    private static Registration[] s_registrations = [];

    public static bool Register(Type receiverType, PropertyFunctionHandler handler)
    {
        lock (s_lock)
        {
            Registration[] registrations = s_registrations;

            for (int i = 0; i < registrations.Length; i++)
            {
                if (registrations[i].ReceiverType == receiverType)
                {
                    return false;
                }
            }

            var updatedRegistrations = new Registration[registrations.Length + 1];
            Array.Copy(registrations, updatedRegistrations, registrations.Length);
            updatedRegistrations[^1] = new Registration(receiverType, handler);
            Volatile.Write(ref s_registrations, updatedRegistrations);
            return true;
        }
    }

    public static bool TryInvoke(Type receiverType, string methodName, object?[] arguments, out object? result)
    {
        Registration[] registrations = Volatile.Read(ref s_registrations);

        for (int i = 0; i < registrations.Length; i++)
        {
            if (registrations[i].ReceiverType == receiverType)
            {
                return registrations[i].Handler(methodName, arguments, out result);
            }
        }

        result = null;
        return false;
    }

    private readonly record struct Registration(Type ReceiverType, PropertyFunctionHandler Handler);
}
