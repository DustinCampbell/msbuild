// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal ref partial struct Parser
{
    public readonly struct Error(string resourceName, int position, object[] formatArgs)
    {
        public int Position => position;
        public string ResourceName => resourceName;
        public object[] FormatArgs => formatArgs ?? [];
    }
}
