// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Build.TaskHost.Utilities;

/// <summary>
///  A cached reusable instance of <see cref="StringBuilder"/>.
/// </summary>
/// <remarks>
///  An optimization that reduces the number of instances of <see cref="StringBuilder"/> constructed and collected.
/// </remarks>
internal static class StringBuilderCache
{
    // The value 512 was chosen empirically as 95% percentile of returning string length.
    private const int MAX_BUILDER_SIZE = 512;

    [ThreadStatic]
    private static StringBuilder? t_cachedInstance;

    /// <summary>
    ///  Get a <see cref="StringBuilder"/> of at least the specified capacity.
    /// </summary>
    /// <param name="capacity">The suggested starting size of this instance.</param>
    /// <returns>
    ///  A <see cref="StringBuilder"/> that may or may not be reused.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   This can be called any number of times; if a <see cref="StringBuilder"/> is in the cache then
    ///   it will be returned and the cache emptied. Subsequent calls will return a new <see cref="StringBuilder"/>.
    ///  </para>
    ///  <para>
    ///   The <see cref="StringBuilder"/> instance is cached in Thread Local Storage and so there is one per thread.
    ///  </para>
    /// </remarks>
    public static StringBuilder Acquire(int capacity = 16 /*StringBuilder.DefaultCapacity*/)
    {
        StringBuilder? builder;

        if (capacity <= MAX_BUILDER_SIZE)
        {
            builder = t_cachedInstance;
            t_cachedInstance = null;

            if (builder != null)
            {
                // Avoid StringBuilder block fragmentation by getting a new StringBuilder
                // when the requested size is larger than the current capacity
                if (capacity <= builder.Capacity)
                {
                    builder.Length = 0;
                    return builder;
                }
            }
        }

        return new StringBuilder(capacity);
    }

    /// <summary>
    ///  Return the specified builder to the cache if it is not too big.
    /// </summary>
    /// <param name="builder">
    ///  The <see cref="StringBuilder"/> to cache. Likely returned from <see cref="Acquire(int)"/>.
    /// </param>
    /// <remarks>
    ///  <para>
    ///   The <see cref="StringBuilder"/> should not be used after it has been released.
    ///  </para>
    ///  <para>
    ///   Unbalanced releases are perfectly acceptable. It will merely cause the runtime to create a new
    ///   <see cref="StringBuilder"/> next time Acquire is called.
    ///  </para>
    /// </remarks>
    public static void Release(StringBuilder builder)
    {
        if (builder.Capacity <= MAX_BUILDER_SIZE)
        {
            // Assert we are not replacing another string builder. That could happen when Acquire is reentered.
            // User of StringBuilderCache has to make sure that calling method call stacks do not also use StringBuilderCache.
            Debug.Assert(t_cachedInstance == null, "Unexpected replacing of other StringBuilder.");
            t_cachedInstance = builder;
        }
    }

    /// <summary>
    ///  Get a string and return the specified builder to the cache.
    /// </summary>
    /// <param name="builder">Builder to cache (if it's not too big).</param>
    /// <returns>
    ///  The <see langword="string"/> equivalent to <paramref name="builder"/>'s contents.
    /// </returns>
    /// <remarks>
    ///  Convenience method equivalent to calling <see cref="StringBuilder.ToString()"/>
    ///  followed by <see cref="Release"/>.
    /// </remarks>
    public static string GetStringAndRelease(StringBuilder builder)
    {
        string result = builder.ToString();
        Release(builder);

        return result;
    }
}
