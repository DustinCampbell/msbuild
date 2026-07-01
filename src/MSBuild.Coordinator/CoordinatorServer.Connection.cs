// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.Coordinator;

internal sealed partial class CoordinatorServer
{
    /// <summary>
    ///  Owns a newly-connected client pipe during server-side handshake and request negotiation.
    /// </summary>
    private sealed class Connection : IDisposable
    {
        private readonly NamedPipeServerStream _pipeStream;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly LockType _writeGate = new();

        /// <summary>
        ///  Gets the unique identifier received during the client handshake.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        ///  Gets the process ID received during the client handshake.
        /// </summary>
        public int ProcessId { get; private set; }

        /// <summary>
        ///  Gets the capabilities advertised by the client during handshake.
        /// </summary>
        public ImmutableArray<string> ClientCapabilities { get; private set; }

        private Connection(NamedPipeServerStream pipeStream)
        {
            _pipeStream = pipeStream;
            _reader = new BinaryReader(pipeStream, Encoding.UTF8, leaveOpen: true);
            _writer = new BinaryWriter(pipeStream, Encoding.UTF8, leaveOpen: true);
        }

        /// <summary>
        ///  Creates a handshaken server connection over an already-connected pipe.
        /// </summary>
        /// <returns>
        ///  A connection that owns the pipe, reader, and writer; or <see langword="null"/> if the handshake failed.
        /// </returns>
        public static Connection? TryCreate(NamedPipeServerStream pipeStream, ICoordinatorDebugOutput output)
        {
            var connection = new Connection(pipeStream);

            if (connection.TryHandshake(output))
            {
                return connection;
            }

            connection.Dispose();
            return null;
        }

        private bool TryHandshake(ICoordinatorDebugOutput output)
        {
            try
            {
                ClientMessage clientMessage = ReadClientMessage();

                if (clientMessage is not ClientHandshakeMessage handshake)
                {
                    output.WriteLine($"CoordinatorServer: Rejected client — first message was {clientMessage.GetType().Name}");
                    WriteServerMessage(new ErrorMessage("First message must be Handshake"));
                    return false;
                }

                Id = handshake.ConnectionId;
                ProcessId = handshake.ProcessId;
                ClientCapabilities = handshake.Capabilities;

                output.WriteLine($"CoordinatorServer: Handshake received (ConnectionId {Id}, PID {ProcessId}, Capabilities: [{string.Join(", ", ClientCapabilities)}])");

                if (ProcessId <= 0)
                {
                    output.WriteLine($"CoordinatorServer: Rejected client — invalid handshake (PID={ProcessId})");
                    WriteServerMessage(new ErrorMessage("Invalid handshake: ProcessId must be > 0"));
                    return false;
                }

                WriteServerMessage(new ServerHandshakeMessage(capabilities: []));

                return true;
            }
            catch (Exception ex)
            {
                output.WriteLine($"CoordinatorServer: Exception during handshake: {ex}");
                return false;
            }
        }

        public void Dispose()
        {
            _reader.Dispose();

            try
            {
                lock (_writeGate)
                {
                    _writer.Dispose();
                }
            }
            catch (IOException)
            {
                // The pipe may already be broken if the client disconnected.
            }

            _pipeStream.Dispose();
        }

        /// <summary>
        ///  Gets a value indicating whether the pipe is still connected to the client.
        /// </summary>
        public bool IsConnected => _pipeStream.IsConnected;

        /// <summary>
        ///  Reads the next client message from the coordinator pipe.
        /// </summary>
        public ClientMessage ReadClientMessage()
            => _reader.ReadClientMessage();

        /// <summary>
        ///  Writes a server message to the coordinator pipe.
        /// </summary>
        public void WriteServerMessage(ServerMessage message)
        {
            lock (_writeGate)
            {
                _writer.Write(message);
            }
        }
    }
}
