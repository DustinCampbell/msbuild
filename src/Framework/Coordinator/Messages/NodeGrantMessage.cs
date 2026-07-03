// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  A node grant from the coordinator.
/// </summary>
/// <remarks>
///  This message follows the "upgradeable message" pattern used for coordinator capabilities: rather than
///  introducing a new message *type* (C# class, or <see cref="ServerMessageType"/> value) each time a new
///  capability affects the grant payload, the existing message class gains an additional field, gated by a bit
///  in <see cref="ExtendedFields"/>. Callers compute the flags from the peer's negotiated capabilities and pass
///  them to the constructor. When no flags are set, the legacy payload shape is used and no extended fields byte
///  is emitted on the wire; when flags are set, <see cref="Message{TMessageType}.ExtendedFieldsByte"/> is emitted
///  and interpreted before the payload. Only the fields *within* that value are capability-gated: a peer that
///  doesn't support <c>nested-grants</c> never has
///  <see cref="ExtendedFields.GrantId"/> set, so it never receives the extra Guid bytes. This keeps the C#
///  message surface (and the <see cref="ServerMessageType"/> enum) stable as capabilities are added, while still
///  preserving binary compatibility with older builds of the coordinator or its clients.
/// </remarks>
internal sealed partial record NodeGrantMessage : ServerMessage
{
    private readonly ExtendedFields _extendedFields;

    protected override byte ExtendedFieldsByte => (byte)_extendedFields;

    /// <summary>
    ///  The root grant token that nested clients can use to join this grant, or <see cref="Guid.Empty"/> if
    ///  <see cref="ExtendedFields.GrantId"/> is not set.
    /// </summary>
    public Guid GrantId { get; }

    public int GrantedNodes { get; }

    public NodeGrantMessage(int grantedNodes)
        : this(grantId: Guid.Empty, grantedNodes, ExtendedFields.None)
    {
    }

    public NodeGrantMessage(Guid grantId, int grantedNodes)
        : this(grantId, grantedNodes, ExtendedFields.GrantId)
    {
    }

    private NodeGrantMessage(Guid grantId, int grantedNodes, ExtendedFields extendedFields)
        : base(ServerMessageType.NodeGrant)
    {
        Assumed.True(
            (extendedFields & ExtendedFields.GrantId) != 0 || grantId == Guid.Empty,
            $"{nameof(grantId)} must be empty if {nameof(ExtendedFields)}.{nameof(ExtendedFields.GrantId)} is not set.");

        GrantId = grantId;
        GrantedNodes = grantedNodes;
        _extendedFields = extendedFields;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        if ((_extendedFields & ExtendedFields.GrantId) != 0)
        {
            writer.WriteGuid(GrantId);
        }

        writer.Write(GrantedNodes);
    }

    internal static NodeGrantMessage ReadPayload(BinaryReader reader, byte extendedFieldsByte)
    {
        Assumed.Zero(
            extendedFieldsByte & ~(byte)ExtendedFields.AllFieldsMask,
            $"Unknown {nameof(ExtendedFields)} bits: 0x{extendedFieldsByte:X2}");

        var extendedFields = (ExtendedFields)extendedFieldsByte;

        Guid grantId = (extendedFields & ExtendedFields.GrantId) != 0
            ? reader.ReadGuid()
            : Guid.Empty;

        int grantedNodes = reader.ReadInt32();

        return new(grantId, grantedNodes, extendedFields);
    }
}
