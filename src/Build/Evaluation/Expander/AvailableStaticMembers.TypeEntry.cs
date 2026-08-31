// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class AvailableStaticMembers
{
    /// <summary>
    ///  Holds either an already resolved receiver type or the assembly-qualified name used to resolve it lazily.
    /// </summary>
    private sealed class TypeEntry
    {
        private readonly string? _assemblyQualifiedName;
        private Type? _receiverType;

        /// <summary>
        ///  Initializes an entry with an already resolved receiver type.
        /// </summary>
        /// <param name="receiverType">The receiver type.</param>
        public TypeEntry(Type receiverType)
        {
            _receiverType = receiverType;
        }

        /// <summary>
        ///  Initializes an entry whose receiver type will be resolved lazily.
        /// </summary>
        /// <param name="assemblyQualifiedName">The assembly-qualified receiver type name.</param>
        public TypeEntry(string assemblyQualifiedName)
        {
            _assemblyQualifiedName = assemblyQualifiedName;
        }

        /// <summary>
        ///  Gets the receiver type, resolving and caching it when necessary.
        /// </summary>
        /// <returns>
        ///  The resolved receiver type.
        /// </returns>
        public Type Resolve()
        {
            Type? receiverType = Volatile.Read(ref _receiverType);
            if (receiverType is not null)
            {
                return receiverType;
            }

            string? assemblyQualifiedName = _assemblyQualifiedName;
            Assumed.NotNull(assemblyQualifiedName);

            Type? resolvedType = ResolveTypeByName(assemblyQualifiedName);
            Assumed.NotNull(resolvedType, $"Type information was present in the allowlist cache as {assemblyQualifiedName} but the type could not be loaded.");

            return Interlocked.CompareExchange(ref _receiverType, resolvedType, comparand: null) ?? resolvedType;
        }
    }
}
