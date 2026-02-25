// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Framework
{
    internal static class ImmutableDictionaryExtensions
    {
#pragma warning disable IDE0052 // Private member can be removed as the value assigned to it is never read.
        private static readonly ImmutableDictionary<string, string> s_emptyMetadata =
            ImmutableDictionary<string, string>.Empty.WithComparers(MSBuildNameIgnoreCaseComparer.Default);
#pragma warning restore IDE0052

        extension(ImmutableDictionary)
        {
            /// <summary>
            ///  An empty dictionary pre-configured with a comparer for metadata dictionaries.
            /// </summary>
            public static ImmutableDictionary<string, string> EmptyMetadata => s_emptyMetadata;
        }

#if !TASKHOST
        /// <summary>
        /// Sets the given items while running a validation function on each key.
        /// </summary>
        /// <remarks>
        /// ProjectItemInstance.TaskItem exposes dictionary values as ProjectMetadataInstance. For perf reasons,
        /// we don't want to internally store ProjectMetadataInstance since it prevents us from sharing immutable
        /// dictionaries with Utilities.TaskItem, and it results in more than 2x memory allocated per-entry.
        /// </remarks>
        public static ImmutableDictionary<string, string> SetItems(
            this ImmutableDictionary<string, string> dictionary,
            IEnumerable<KeyValuePair<string, string>> items,
            Action<string> verifyThrowKey)
        {
            ImmutableDictionary<string, string>.Builder builder = dictionary.ToBuilder();

            foreach (KeyValuePair<string, string> item in items)
            {
                verifyThrowKey(item.Key);

                // Set null as empty string to match behavior with ProjectMetadataInstance.
                builder[item.Key] = item.Value ?? string.Empty;
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Sets the given items while running a validation function on each key.
        /// </summary>
        /// <remarks>
        /// ProjectItemInstance.TaskItem exposes dictionary values as ProjectMetadataInstance. For perf reasons,
        /// we don't want to internally store ProjectMetadataInstance since it prevents us from sharing immutable
        /// dictionaries with Utilities.TaskItem, and it results in more than 2x memory allocated per-entry.
        /// </remarks>
        public static ImmutableDictionary<string, string> SetItems(
            this ImmutableDictionary<string, string> dictionary,
            ReadOnlySpan<KeyValuePair<string, string>> items,
            Action<string> verifyThrowKey)
        {
            ImmutableDictionary<string, string>.Builder builder = dictionary.ToBuilder();

            foreach (KeyValuePair<string, string> item in items)
            {
                verifyThrowKey(item.Key);

                // Set null as empty string to match behavior with ProjectMetadataInstance.
                builder[item.Key] = item.Value ?? string.Empty;
            }

            return builder.ToImmutable();
        }
#endif
    }
}
