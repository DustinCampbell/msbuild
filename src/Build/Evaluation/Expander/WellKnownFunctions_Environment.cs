// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class EnvironmentHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(Environment.ExpandEnvironmentVariables), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string value]
                    ? Invoked(Environment.ExpandEnvironmentVariables(value))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(Environment.GetEnvironmentVariable), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string variable] => Invoked(Environment.GetEnvironmentVariable(variable)),
                    [string variable, var target] when ArgumentParser.TryConvertToEnum(target, out EnvironmentVariableTarget convertedTarget)
                        => Invoked(Environment.GetEnvironmentVariable(variable, convertedTarget)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Environment.GetEnvironmentVariables), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [] => Invoked(Environment.GetEnvironmentVariables()),
                    [var target] when ArgumentParser.TryConvertToEnum(target, out EnvironmentVariableTarget convertedTarget)
                        => Invoked(Environment.GetEnvironmentVariables(convertedTarget)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Environment.GetFolderPath), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [var folder] when ArgumentParser.TryConvertToEnum(folder, out Environment.SpecialFolder convertedFolder)
                        => Invoked(Environment.GetFolderPath(convertedFolder)),
                    [var folder, var option]
                        when ArgumentParser.TryConvertToEnum(folder, out Environment.SpecialFolder convertedFolder) &&
                             ArgumentParser.TryConvertToEnum(option, out Environment.SpecialFolderOption convertedOption)
                        => Invoked(Environment.GetFolderPath(convertedFolder, convertedOption)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Environment.GetLogicalDrives), StringComparison.OrdinalIgnoreCase))
            {
                return args is []
                    ? Invoked(Environment.GetLogicalDrives())
                    : NotHandled;
            }

            if (args is not [])
            {
                return NotHandled;
            }

            if (methodName.Equals(nameof(Environment.CommandLine), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.CommandLine);
            }

            if (methodName.Equals(nameof(Environment.Is64BitOperatingSystem), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.Is64BitOperatingSystem);
            }

            if (methodName.Equals(nameof(Environment.Is64BitProcess), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.Is64BitProcess);
            }

            if (methodName.Equals(nameof(Environment.MachineName), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.MachineName);
            }

            if (methodName.Equals(nameof(Environment.NewLine), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.NewLine);
            }

            if (methodName.Equals(nameof(Environment.OSVersion), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.OSVersion);
            }

            if (methodName.Equals(nameof(Environment.ProcessorCount), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.ProcessorCount);
            }

            if (methodName.Equals(nameof(Environment.StackTrace), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.StackTrace);
            }

            if (methodName.Equals(nameof(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.SystemDirectory);
            }

            if (methodName.Equals(nameof(Environment.SystemPageSize), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.SystemPageSize);
            }

            if (methodName.Equals(nameof(Environment.TickCount), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.TickCount);
            }

            if (methodName.Equals(nameof(Environment.UserDomainName), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.UserDomainName);
            }

            if (methodName.Equals(nameof(Environment.UserInteractive), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.UserInteractive);
            }

            if (methodName.Equals(nameof(Environment.UserName), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.UserName);
            }

            if (methodName.Equals(nameof(Environment.Version), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.Version);
            }

            if (methodName.Equals(nameof(Environment.WorkingSet), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(Environment.WorkingSet);
            }

            return NotHandled;
        }
    }
}
