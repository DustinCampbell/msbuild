// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.Coordinator;

internal sealed partial class CoordinatorServer
{
    /// <summary>
    ///  Tracks an accepted MSBuild client alongside its <see cref="BuildGrant"/>.
    /// </summary>
    private sealed class ConnectedClient : IDisposable
    {
        private readonly Connection _connection;

        /// <summary>
        ///  Gets the unique identifier for this connection, assigned by the client during handshake.
        /// </summary>
        public Guid ConnectionId { get; }

        /// <summary>
        ///  Gets the process ID of the connected MSBuild client.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        ///  Gets the capabilities advertised by the client during handshake.
        /// </summary>
        public ImmutableArray<string> Capabilities { get; }

        /// <summary>
        ///  Gets the build grant associated with this client.
        /// </summary>
        public BuildGrant Grant { get; }

        /// <summary>
        ///  Gets a value indicating whether the pipe is still connected to the client.
        /// </summary>
        public bool IsConnected => _connection.IsConnected;

        /// <summary>
        ///  Creates a connected client by taking ownership of a negotiated connection.
        /// </summary>
        public ConnectedClient(Connection connection, BuildGrant grant)
        {
            _connection = connection;

            ConnectionId = connection.Id;
            ProcessId = connection.ProcessId;
            Capabilities = connection.ClientCapabilities;
            Grant = grant;
        }

        /// <summary>
        ///  Reads the next client message from this connected client.
        /// </summary>
        public ClientMessage ReadClientMessage()
            => _connection.ReadClientMessage();

        /// <summary>
        ///  Writes a server message to this connected client.
        /// </summary>
        public void WriteServerMessage(ServerMessage message)
            => _connection.WriteServerMessage(message);

        public void Dispose()
            => _connection.Dispose();
    }
}
