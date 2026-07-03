// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

internal abstract partial record ClientMessage
{
    private sealed class Factory : Factory<ClientMessage, Factory>
    {
        private static readonly Group s_allFactories = new(
            new(ClientMessageType.Handshake, static (reader, _) => ClientHandshakeMessage.ReadPayload(reader)),
            new(ClientMessageType.RequestNodes, static (reader, _) => RequestNodesMessage.ReadPayload(reader)),
            new(ReleaseNodesMessage.Instance),
            new(HeartbeatMessage.Instance),
            new(ClientMessageType.JoinGrant, static (reader, _) => JoinGrantMessage.ReadPayload(reader)));

        private Factory(
            ClientMessage instance,
            bool supportsExtendedFields = false)
            : base(instance.MessageType, instance, supportsExtendedFields)
        {
        }

        private Factory(
            ClientMessageType messageType,
            Func<BinaryReader, byte, ClientMessage> messageCreator,
            bool supportsExtendedFields = false)
            : base(messageType, messageCreator, supportsExtendedFields)
        {
        }

        public static Factory FromMessageType(ClientMessageType messageType)
            => s_allFactories.GetFactory(messageType);
    }
}
