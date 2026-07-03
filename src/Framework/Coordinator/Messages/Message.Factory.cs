// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

internal abstract partial record Message<TMessageType>
    where TMessageType : struct, Enum
{
    protected abstract partial class Factory<TMessage, TFactory>
        where TMessage : Message<TMessageType>
        where TFactory : Factory<TMessage, TFactory>
    {
        public TMessageType MessageType { get; }
        public bool SupportsExtendedFields { get; }

        private readonly TMessage? _instance;
        private readonly Func<BinaryReader, byte, TMessage>? _messageCreator;

        protected Factory(TMessageType messageType, Func<BinaryReader, byte, TMessage> messageCreator, bool supportsExtendedFields)
        {
            MessageType = messageType;
            _instance = null;
            _messageCreator = messageCreator;
            SupportsExtendedFields = supportsExtendedFields;
        }

        protected Factory(TMessageType messageType, TMessage instance, bool supportsExtendedFields)
        {
            MessageType = messageType;
            _instance = instance;
            _messageCreator = null;
            SupportsExtendedFields = supportsExtendedFields;
        }

        public TMessage Create(BinaryReader reader, byte extendedFields)
        {
            if (_instance is not null)
            {
                return _instance;
            }

            Assumed.NotNull(_messageCreator, "Message factory must have either an instance or a message creator.");

            return _messageCreator(reader, extendedFields);
        }
    }
}
