// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.TaskHost.BackEnd;

#nullable disable

namespace Microsoft.Build.TaskHost;

/// <summary>
/// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
/// </summary>
internal sealed class NodeEndpointOutOfProcTaskHost : NodeEndpointOutOfProcBase
{
    internal bool _nodeReuse;

    /// <summary>
    /// Instantiates an endpoint to act as a client.
    /// </summary>
    /// <param name="nodeReuse">Whether node reuse is enabled.</param>
    /// <param name="parentPacketVersion">The packet version supported by the parent. 1 if parent doesn't support version negotiation.</param>
    internal NodeEndpointOutOfProcTaskHost(bool nodeReuse, byte parentPacketVersion)
    {
        _nodeReuse = nodeReuse;
        InternalConstruct(pipeName: null, parentPacketVersion);
    }

    /// <summary>
    /// Returns the host handshake for this node endpoint
    /// </summary>
    protected override Handshake GetHandshake() =>
        new(CommunicationsUtilities.GetHandshakeOptions(taskHost: true, taskHostParameters: TaskHostParameters.Empty, nodeReuse: _nodeReuse));
}
