// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.BackEnd;

internal sealed partial class CoordinatorClient
{
    internal static class TestAccessor
    {
        /// <summary>
        ///  Attempts to connect to a coordinator using the provided settings and request a node grant.
        ///  This overload does not attempt to launch the coordinator and is intended for testing.
        /// </summary>
        /// <param name="requestedNodes">The number of nodes to request from the coordinator.</param>
        /// <param name="settings">Coordinator connection settings (pipe name, timeouts, etc.).</param>
        /// <param name="output">Debug trace output for diagnostic logging.</param>
        /// <returns>
        ///  A connected <see cref="CoordinatorClient"/> instance, or <see langword="null"/> if the connection fails.
        /// </returns>
        public static CoordinatorClient? TryConnectToServer(int requestedNodes, CoordinatorSettings settings, ICoordinatorDebugOutput output)
        {
            Connection? connection = null;

            try
            {
                output.WriteLine($"CoordinatorClient: Connecting to test pipe '{settings.PipeName}'");

                connection = TryConnectToCoordinator(settings.PipeName, settings.ProcessId, settings.ConnectionTimeoutMs, output);
                if (connection is null)
                {
                    output.WriteLine("CoordinatorClient: Test connection timed out");
                    return null;
                }

                output.WriteLine("CoordinatorClient: Connected to test server");

                var client = TryNegotiate(connection, requestedNodes, settings, output, loggingService: null);

                if (client is not null)
                {
                    // Ownership transferred to CoordinatorClient.
                    connection = null;
                }
                else
                {
                    output.WriteLine("CoordinatorClient: Test negotiation failed");
                }

                return client;
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                output.WriteLine($"CoordinatorClient: Exception during test connect: {ex.Message}");
                return null;
            }
            finally
            {
                connection?.Dispose();
            }
        }
    }
}
