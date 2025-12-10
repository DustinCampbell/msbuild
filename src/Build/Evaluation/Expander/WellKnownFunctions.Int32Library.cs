// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class Int32Library : FunctionLibrary
        {
            public static readonly Int32Library Instance = new();

            private Int32Library()
            {
            }

            protected override void Initialize(ref Builder builder)
            {
                builder.Add<int>(nameof(int.ToString), Int32_ToString);
            }

            private static bool Int32_ToString(int i, ReadOnlySpan<object?> args, out object? result)
            {
                switch (args)
                {
                    case []:
                        result = i.ToString();
                        return true;

                    case [string format]:
                        result = i.ToString(format);
                        return true;

                    default:
                        result = null;
                        return false;
                }
            }
        }
    }
}
