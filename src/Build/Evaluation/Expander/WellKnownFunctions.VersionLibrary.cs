// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class VersionLibrary : BaseMemberLibrary, IStaticMethodLibrary, IInstanceMethodLibrary<Version>, ICustomToStringProvider<Version>
    {
        public static readonly VersionLibrary Instance = new();

        private enum StaticId
        {
            Parse
        }

        private enum InstanceId
        {
            CompareTo,
            Revision
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;
        private static readonly FunctionIdLookup<InstanceId> s_instanceIds = FunctionIdLookup<InstanceId>.Instance;

        private VersionLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFindMatch(name, StaticId.Parse)
                ? Version_Parse(args)
                : Result.None;

        public Result TryExecute(Version instance, string name, ReadOnlySpan<object?> args)
            => s_instanceIds.TryFind(name, out InstanceId id)
                ? id switch
                {
                    InstanceId.CompareTo => Version_CompareTo(instance, args),
                    InstanceId.Revision => Version_Revision(instance, args),
                    _ => Result.None,
                }
                : Result.None;

        public Result TryExecuteToString(Version v, ReadOnlySpan<object?> args)
            => args is [var arg0] && TryConvertToInt(arg0, out int fieldCount)
                ? Result.From(v.ToString(fieldCount))
                : Result.None;

        private static Result Version_Parse(ReadOnlySpan<object?> args)
            => args is [string input]
                ? Result.From(Version.Parse(input))
                : Result.None;

        private static Result Version_CompareTo(Version v, ReadOnlySpan<object?> args)
            => args is [var arg0] && arg0 is Version or null
                ? Result.From(v.CompareTo((Version?)arg0))
                : Result.None;

        private static Result Version_Revision(Version v, ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(v.Revision)
                : Result.None;
    }
}
