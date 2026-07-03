// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

internal abstract partial record ServerMessage
{
    private sealed class Factory : Factory<ServerMessage, Factory>
    {
        private static readonly Group s_allFactories = new(
            new(ServerMessageType.HandshakeResponse, static (reader, _) => ServerHandshakeMessage.ReadPayload(reader)),
            new(ServerMessageType.NodeGrant, static (reader, extendedFields) => NodeGrantMessage.ReadPayload(reader, extendedFields), supportsExtendedFields: true),
            new(WaitMessage.Instance),
            new(ServerMessageType.Error, static (reader, _) => new ErrorMessage(message: reader.ReadString())));

        private Factory(
            ServerMessage instance,
            bool supportsExtendedFields = false)
            : base(instance.MessageType, instance, supportsExtendedFields)
        {
        }

        private Factory(
            ServerMessageType messageType,
            Func<BinaryReader, byte, ServerMessage> messageCreator,
            bool supportsExtendedFields = false)
            : base(messageType, messageCreator, supportsExtendedFields)
        {
        }

        public static Factory FromMessageType(ServerMessageType messageType)
            => s_allFactories.GetFactory(messageType);
    }
}
