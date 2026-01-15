// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.Build.Text;

internal static class StringExtensions
{
    public delegate void BuildAction(ref RefStringBuilder builder);

    public delegate void BuildAction<TArg>(TArg arg, ref RefStringBuilder builder);

    extension(string)
    {
        public static string Build(BuildAction action)
        {
            var builder = new RefStringBuilder();
            return BuildCore(action, ref builder);
        }

        public static string Build(int initialCapacity, BuildAction action)
        {
            var builder = new RefStringBuilder(initialCapacity);
            return BuildCore(action, ref builder);
        }

        public static string Build<TArg>(TArg arg, BuildAction<TArg> action)
        {
            var builder = new RefStringBuilder();
            return BuildCore(action, arg, ref builder);
        }

        public static string Build<TArg>(TArg arg, int initialCapacity, BuildAction<TArg> action)
        {
            var builder = new RefStringBuilder(initialCapacity);
            return BuildCore(action, arg, ref builder);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BuildCore(BuildAction action, ref RefStringBuilder builder)
    {
        try
        {
            action(ref builder);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BuildCore<TArg>(BuildAction<TArg> action, TArg arg, ref RefStringBuilder builder)
    {
        try
        {
            action(arg, ref builder);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }
}
