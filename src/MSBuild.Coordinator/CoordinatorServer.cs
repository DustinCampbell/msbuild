// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  The coordinator server that listens for MSBuild client connections on a named pipe,
///  manages node grants, and monitors build health via heartbeats.
/// </summary>
/// <param name="settings">Configuration for the coordinator (pipe name, budget, timeouts, etc.).</param>
/// <param name="output">Optional debug trace output. Defaults to file-based trace logging gated on MSBUILDDEBUGCOMM.</param>
internal sealed partial class CoordinatorServer(CoordinatorSettings settings, ICoordinatorDebugOutput? output = null) : IDisposable
{
    private readonly CoordinatorSettings _settings = settings;
    private readonly NodeBudgetManager _budgetManager = new(settings.TotalNodeBudget);
    private readonly string _pipeName = settings.PipeName;
    private readonly int _heartbeatIntervalMs = settings.HeartbeatIntervalMs;
    private readonly int _shutdownTimeoutMs = settings.ShutdownTimeoutMs;
    private readonly Dictionary<Guid, ConnectedClient> _clientsById = [];
    private readonly Dictionary<Guid, BuildGrant> _activeRootGrantsById = [];
    private readonly ReaderWriterLockSlim _clientsLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ICoordinatorDebugOutput _output = output ?? DefaultDebugOutput.Instance;
    private Timer? _heartbeatMonitor;
    private Timer? _shutdownTimer;

    public void Dispose()
    {
        _cts.Cancel();
        _heartbeatMonitor?.Dispose();
        _shutdownTimer?.Dispose();
        _cts.Dispose();
        _clientsLock.Dispose();
    }

    /// <summary>
    ///  Runs the coordinator server until cancellation is requested or the auto-shutdown
    ///  timeout elapses with no active or waiting builds.
    /// </summary>
    /// <param name="cancellationToken">Token to signal the server should stop accepting connections.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        CancellationToken token = linked.Token;

        // Start heartbeat monitoring.
        _heartbeatMonitor = new Timer(
            CheckHeartbeats,
            state: null,
            dueTime: _heartbeatIntervalMs,
            period: _heartbeatIntervalMs);

        // Start auto-shutdown timer.
        ResetShutdownTimer();

        _output.WriteLine($"CoordinatorServer: Accept loop started on pipe '{_pipeName}' (budget={_settings.TotalNodeBudget})");

        ConcurrentDictionary<Task, byte> clientTasks = [];

        try
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? pipeStream = await WaitForClientAsync(token);

                if (pipeStream is null)
                {
                    break;
                }

                // Dispatch client handling to the thread pool so the accept loop immediately
                // creates the next pipe instance. This is necessary because HandleClientAsync
                // performs a synchronous read (ClientMessage.Read) before its first await,
                // which would block the accept loop if run inline.
                //
                // CancellationToken.None is intentional: we don't want Task.Run to cancel
                // before HandleClientAsync starts, which would orphan the pipe stream.
                // HandleClientAsync receives the cancellation token for its own loop.
                Task clientTask = Task.Run(() => HandleClientAsync(pipeStream, token), CancellationToken.None);
                clientTasks.TryAdd(clientTask, 0);

                // Remove task from tracking when it completes to prevent unbounded growth.
                _ = clientTask.ContinueWith(_ => clientTasks.TryRemove(clientTask, out byte _), TaskScheduler.Default);
            }
        }
        finally
        {
            _output.WriteLine("CoordinatorServer: Accept loop exiting");
            _heartbeatMonitor?.Dispose();
            _shutdownTimer?.Dispose();

            // Wait for all remaining client tasks to complete before exiting.
            // This ensures logging (which may reference the test context) completes cleanly.
            ICollection<Task> remainingTasks = clientTasks.Keys;

            if (remainingTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(remainingTasks);
                }
                catch
                {
                    // Swallow exceptions from client tasks; they're already logged.
                }
            }
        }
    }

    /// <summary>
    ///  Waits for a client to connect to the named pipe.
    /// </summary>
    /// <param name="token">Cancellation token to abort the wait.</param>
    /// <returns>
    ///  The connected pipe stream, or <see langword="null"/> if the wait was cancelled.
    /// </returns>
    private async Task<NamedPipeServerStream?> WaitForClientAsync(CancellationToken token)
    {
        NamedPipeServerStream pipeStream = new(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        try
        {
            await pipeStream.WaitForConnectionAsync(token);
            return pipeStream;
        }
        catch (OperationCanceledException)
        {
            pipeStream.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Handles a single client connection for its entire lifetime.
    /// </summary>
    /// <param name="pipeStream">The connected pipe stream for this client.</param>
    /// <param name="token">Cancellation token to signal the client loop should exit.</param>
    private async Task HandleClientAsync(NamedPipeServerStream pipeStream, CancellationToken token)
    {
        ConnectedClient? client = null;

        try
        {
            // Establish client identity and capabilities. The grant request is a separate
            // protocol message and is handled below.
            using Connection? connection = Connection.TryCreate(pipeStream, _output);
            if (connection is null)
            {
                return;
            }

            ClientMessage requestMessage = connection.ReadClientMessage();

            if (requestMessage is not (RequestNodesMessage or JoinGrantMessage))
            {
                _output.WriteLine($"CoordinatorServer: Rejected client — second message was {requestMessage.GetType().Name}");
                connection.WriteServerMessage(new ErrorMessage("Second message must be RequestNodes or JoinGrant"));
                return;
            }

            int requestedNodes = requestMessage switch
            {
                RequestNodesMessage request => request.RequestedNodes,
                JoinGrantMessage join => join.RequestedNodes,
                _ => Assumed.Unreachable<int>(),
            };

            bool clientSupportsNestedGrants = connection.ClientCapabilities.Contains(Capabilities.NestedGrants);

            // A client may only join an existing grant if it opted into the nested-grants
            // protocol. That guarantees it can understand a grant response carrying a grant ID.
            if (requestMessage is JoinGrantMessage && !clientSupportsNestedGrants)
            {
                _output.WriteLine("CoordinatorServer: Rejected client — JoinGrant requires nested-grants capability");
                connection.WriteServerMessage(new ErrorMessage("JoinGrant requires nested-grants capability"));
                return;
            }

            if (connection.ProcessId <= 0 || requestedNodes <= 0)
            {
                _output.WriteLine($"CoordinatorServer: Rejected client — invalid request (PID={connection.ProcessId}, RequestedNodes={requestedNodes})");
                connection.WriteServerMessage(new ErrorMessage("Invalid request: ProcessId and RequestedNodes must be > 0"));
                return;
            }

            _output.WriteLine($"CoordinatorServer: Client connected (PID {connection.ProcessId}, ConnectionId {connection.Id}, requested {requestedNodes} nodes)");

            // Root requests create a new grant and participate in global budget allocation.
            // Nested requests validate an existing root grant token and borrow that grant's
            // node count without consuming additional global budget.
            BuildGrant grant = requestMessage is JoinGrantMessage joinRequest
                ? CreateNestedGrant(connection.Id, connection.ProcessId, requestedNodes, joinRequest.GrantId)
                : new BuildGrant(connection.Id, connection.ProcessId, requestedNodes);

            // Nested grants are immediate grant-or-error. They should never enter the wait queue
            // because they don't consume global coordinator budget.
            if (grant.IsNested && grant.GrantedNodes == 0)
            {
                _output.WriteLine($"CoordinatorServer: Rejected nested client — grant {grant.GrantId} is not active");
                connection.WriteServerMessage(new ErrorMessage("Requested coordinator grant is not active"));
                return;
            }

            // Once a client is accepted, transfer pipe ownership to ConnectedClient so
            // cleanup and subsequent message I/O are tied to the grant lifecycle.
            client = new ConnectedClient(connection, grant);

            using (_clientsLock.EnterDisposableWriteLock())
            {
                _clientsById[connection.Id] = client;
            }

            int grantedNodes = grant.IsNested
                ? grant.GrantedNodes
                : _budgetManager.TryGrant(grant);

            if (grantedNodes > 0)
            {
                _output.WriteLine($"CoordinatorServer: Granted {grantedNodes} nodes to PID {connection.ProcessId}");

                // Only root grants are tracked by grant ID. Nested grants reference their root
                // grant ID, but releasing a nested connection must not invalidate that root grant.
                if (!grant.IsNested)
                {
                    TrackRootGrant(grant);
                }

                WriteGrantMessage(client, grant);
            }
            else
            {
                _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} queued (no nodes available)");
                client.WriteServerMessage(WaitMessage.Instance);

                // The grant will be fulfilled later when resources free up.
            }

            ResetShutdownTimer();

            // Process subsequent messages (heartbeats and release).
            while (!token.IsCancellationRequested && pipeStream.IsConnected)
            {
                ClientMessage message;

                try
                {
                    message = await Task.Run(client.ReadClientMessage, token);
                }
                catch (EndOfStreamException)
                {
                    _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} disconnected (end of stream)");

                    // Client disconnected.
                    break;
                }
                catch (IOException)
                {
                    _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} disconnected (pipe broken)");

                    // Pipe broken.
                    break;
                }

                switch (message)
                {
                    case HeartbeatMessage:
                        grant.LastHeartbeat = DateTime.UtcNow;
                        break;

                    case ReleaseNodesMessage:
                        _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} released grant");
                        ReleaseClient(client);
                        client.Dispose();
                        client = null;
                        return;
                }
            }
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            _output.WriteLine($"CoordinatorServer: Exception handling client: {ex.Message}");

            // Swallow exceptions from individual client handling.
        }
        finally
        {
            // If we get here without an explicit release, treat it as a crash/disconnect.
            if (client is not null)
            {
                ReleaseClient(client);
                client.Dispose();
            }
            else
            {
                pipeStream.Dispose();
            }
        }
    }

    /// <summary>
    ///  Releases a client's grant and notifies any builds that were waiting for resources.
    /// </summary>
    /// <param name="client">The client whose grant is being released.</param>
    private void ReleaseClient(ConnectedClient client)
    {
        using (_clientsLock.EnterDisposableWriteLock())
        {
            // Only remove if this connection is still current for the connection ID.
            if (_clientsById.TryGetValue(client.ConnectionId, out var current) &&
                current == client)
            {
                _clientsById.Remove(client.ConnectionId);
            }

            if (!client.Grant.IsNested)
            {
                _activeRootGrantsById.Remove(client.Grant.GrantId);
            }
        }

        ImmutableArray<BuildGrant> newlyGranted = _budgetManager.Release(client.Grant);

        if (newlyGranted.Length > 0)
        {
            _output.WriteLine($"CoordinatorServer: Draining wait queue, {newlyGranted.Length} build(s) to notify");
        }

        // Notify newly granted builds outside the locks.
        foreach (BuildGrant grant in newlyGranted)
        {
            bool found;
            ConnectedClient? waitingClient;
            using (_clientsLock.EnterDisposableReadLock())
            {
                found = _clientsById.TryGetValue(grant.ConnectionId, out waitingClient);
            }

            if (found && waitingClient is not null)
            {
                try
                {
                    _output.WriteLine($"CoordinatorServer: Granting {grant.GrantedNodes} deferred nodes to PID {grant.ProcessId}");
                    TrackRootGrant(grant);
                    WriteGrantMessage(waitingClient, grant);
                }
                catch (IOException)
                {
                    _output.WriteLine($"CoordinatorServer: PID {grant.ProcessId} disconnected while waiting");

                    // Client disconnected while waiting. Release their grant too.
                    ReleaseClient(waitingClient);
                }
            }
        }

        ResetShutdownTimer();
    }

    private BuildGrant CreateNestedGrant(Guid connectionId, int processId, int requestedNodes, Guid grantId)
    {
        using (_clientsLock.EnterDisposableReadLock())
        {
            if (_activeRootGrantsById.TryGetValue(grantId, out BuildGrant? rootGrant) &&
                rootGrant.IsActive)
            {
                return new BuildGrant(connectionId, processId, requestedNodes, grantId, isNested: true)
                {
                    GrantedNodes = Math.Min(requestedNodes, rootGrant.GrantedNodes),
                };
            }
        }

        return new BuildGrant(connectionId, processId, requestedNodes, grantId, isNested: true);
    }

    private void TrackRootGrant(BuildGrant grant)
    {
        using (_clientsLock.EnterDisposableWriteLock())
        {
            _activeRootGrantsById[grant.GrantId] = grant;
        }
    }

    private static void WriteGrantMessage(ConnectedClient client, BuildGrant grant)
    {
        if (client.Capabilities.Contains(Capabilities.NestedGrants))
        {
            client.WriteServerMessage(new NodeGrantWithIdMessage(grant.GrantId, grant.GrantedNodes));
        }
        else
        {
            client.WriteServerMessage(new NodeGrantMessage(grant.GrantedNodes));
        }
    }

    /// <summary>
    ///  Periodically checks for builds that have missed heartbeats and reclaims their grants.
    /// </summary>
    /// <param name="state">Timer callback state (unused).</param>
    private void CheckHeartbeats(object? state)
    {
        DateTime threshold = DateTime.UtcNow - TimeSpan.FromMilliseconds(_settings.HeartbeatTimeoutMs);

        List<ConnectedClient> clientsToCheck;

        using (_clientsLock.EnterDisposableReadLock())
        {
            clientsToCheck = [.. _clientsById.Values];
        }

        foreach (ConnectedClient client in clientsToCheck)
        {
            if (client.Grant.LastHeartbeat >= threshold)
            {
                continue;
            }

            // Check if the process is still alive before reclaiming.
            if (IsProcessAlive(client.ProcessId))
            {
                continue;
            }

            _output.WriteLine($"CoordinatorServer: Reclaiming grant from dead PID {client.ProcessId}");

            // ReleaseClient will acquire its own write lock.
            ReleaseClient(client);
        }
    }

    /// <summary>
    ///  Checks whether a process with the given ID is still running.
    /// </summary>
    /// <param name="processId">The OS process ID to check.</param>
    /// <returns>
    ///  <see langword="true"/> if the process exists and has not exited; otherwise <see langword="false"/>.
    /// </returns>
    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist.
            return false;
        }
    }

    /// <summary>
    ///  Resets the auto-shutdown timer. If no builds are active or waiting when the timer
    ///  fires, the coordinator shuts down.
    /// </summary>
    private void ResetShutdownTimer()
    {
        var newTimer = new Timer(
            _ =>
            {
                if (_budgetManager.IsIdle)
                {
                    _output.WriteLine("CoordinatorServer: Auto-shutdown (no active or waiting builds)");
                    _cts.Cancel();
                }
            },
            state: null,
            dueTime: _shutdownTimeoutMs,
            period: Timeout.Infinite);

        Interlocked.Exchange(ref _shutdownTimer, newTimer)?.Dispose();
    }
}
