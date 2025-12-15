// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringArrayLibrary : BaseMemberLibrary, IInstanceMethodLibrary<string[]>
    {
        public static readonly StringArrayLibrary Instance = new();

        private enum InstanceId
        {
            GetValue
        }

        private static readonly FunctionIdLookup<InstanceId> s_instanceIds = FunctionIdLookup<InstanceId>.Instance;

        private StringArrayLibrary()
        {
        }

        public Result TryExecute(string[] instance, string name, ReadOnlySpan<object?> args)
            => s_instanceIds.TryFindMatch(name, InstanceId.GetValue)
                ? StringArray_GetValue(instance, args)
                : Result.None;

        private static Result StringArray_GetValue(string[] instance, ReadOnlySpan<object?> args)
            => args is [var arg0] && TryConvertToInt(arg0, out var index)
                ? Result.From(instance[index])
                : Result.None;
    }
}
