// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal ref partial struct Parser
{
    private sealed class KnownFunction(KnownFunctionKind kind, string name, int argumentCount)
    {
        public KnownFunctionKind Kind => kind;
        public string Name => name;
        public int ArgumentCount => argumentCount;
    }
}
