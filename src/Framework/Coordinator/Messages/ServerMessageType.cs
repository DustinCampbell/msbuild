// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Message types sent from the coordinator to an MSBuild client.
/// </summary>
/// <remarks>
///  On the wire, the type byte's high bit (0x80) is reserved to indicate that a single extended fields byte
///  follows the type byte, before the message's own payload. See <see cref="ServerMessage.ReadFrom"/> and
///  <see cref="Message{TMessageType}.WriteTo(System.IO.BinaryWriter)"/>. The remaining 7 bits (values 0-127) hold the type ordinal below.
/// </remarks>
internal enum ServerMessageType : byte
{
    /// <summary>
    ///  Handshake response. Payload: string[] capabilities.
    /// </summary>
    HandshakeResponse = 1,

    /// <summary>
    ///  A node grant. Payload depends on the flags value read for this message (see
    ///  <see cref="NodeGrantMessage.ExtendedFields"/>): always an int grantedNodes, plus a Guid grantId when
    ///  <see cref="NodeGrantMessage.ExtendedFields.GrantId"/> is set.
    /// </summary>
    NodeGrant = 2,

    /// <summary>
    ///  Indicates the client should wait for a grant. No payload.
    /// </summary>
    Wait = 3,

    /// <summary>
    ///  An error occurred. Payload: string message.
    /// </summary>
    Error = 4,
}
