// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

namespace Microsoft.Build.Framework.Coordinator;

internal abstract partial record Message<TMessageType>
    where TMessageType : struct, Enum
{
    protected abstract partial class Factory<TMessage, TFactory>
        where TMessage : Message<TMessageType>
        where TFactory : Factory<TMessage, TFactory>
    {
        protected sealed class Group
        {
            private readonly TFactory[] _factories;

            public Group(params ImmutableArray<TFactory> factories)
            {
                _factories = new TFactory[factories.Length];

                // First, assign factories to each index based on message type.
                foreach (TFactory factory in factories)
                {
                    int index = GetIndex(factory.MessageType);
                    Assumed.InRange(index, 0, factories.Length - 1);
                    Assumed.Null(_factories[index]);

                    _factories[index] = factory;
                }

                // Then, ensure each index was assigned.
                foreach (TFactory factory in _factories)
                {
                    Assumed.NotNull(factory);
                }
            }

            public TFactory GetFactory(TMessageType messageType)
            {
                int index = GetIndex(messageType);

                return index >= 0 && index < _factories.Length
                    ? _factories[index]
                    : Assumed.Unreachable<TFactory>($"Invalid {typeof(TMessageType).Name}: {messageType}");
            }

            private static int GetIndex(TMessageType messageType)
                => GetTypeByte(messageType) - 1;
        }
    }
}
