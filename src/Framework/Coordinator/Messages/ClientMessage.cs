// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Base type for all messages sent from an MSBuild client to the coordinator.
/// </summary>
internal abstract partial record ClientMessage : Message<ClientMessageType>
{
    protected ClientMessage(ClientMessageType messageType)
        : base(messageType)
    {
    }

    /// <summary>
    ///  Reads a client-to-coordinator message from the stream.
    /// </summary>
    public static ClientMessage ReadFrom(BinaryReader reader)
    {
        (ClientMessageType messageType, bool hasExtendedFields) = ReadTypeByte(reader);
        Factory factory = Factory.FromMessageType(messageType);

        if (hasExtendedFields)
        {
            Assumed.True(factory.SupportsExtendedFields, $"Message type {factory.MessageType} does not support extended fields.");
        }

        byte extendedFields = hasExtendedFields ? ReadExtendedFieldsByte(reader) : (byte)0;

        return factory.Create(reader, extendedFields);
    }
}
