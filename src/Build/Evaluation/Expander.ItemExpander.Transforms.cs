// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Text;
using Microsoft.NET.StringTools;

#if FEATURE_MSIOREDIST
// File is intentionally NOT aliased — all typeof() comparisons use fully-qualified
// System.IO.File to match the types registered in AvailableStaticMethods.
using Directory = Microsoft.IO.Directory;
using Path = Microsoft.IO.Path;
#endif

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    private static partial class ItemExpander
    {
        /// <summary>
        /// The set of functions that called during an item transformation, e.g. @(CLCompile->ContainsMetadata('MetaName', 'metaValue')).
        /// </summary>
        private static partial class Transforms
        {
            /// <summary>
            /// The number of characters added by a quoted expression.
            /// 3 characters for
            ///  </summary>
            private const int QuotedExpressionSurroundCharCount = 3;

            /// <summary>
            /// A thread-static string builder for use in ExpandQuotedExpressionFunction.
            /// In theory we should be able to use shared instance, but in a profile it appears something higher in
            /// the call-stack is already borrowing the instance, so it ends up always allocating.
            /// This should not be used outside of ExpandQuotedExpressionFunction unless validated to not conflict.
            /// </summary>
            [ThreadStatic]
            private static SpanBasedStringBuilder s_includeBuilder;

            /// <summary>
            /// Intrinsic function that adds the number of items in the list.
            /// </summary>
            internal static void Count(List<TransformEntry> input, List<TransformEntry> output)
                => output.Add(new TransformEntry(input.Count.ToString(CultureInfo.InvariantCulture), item: null));

            /// <summary>
            /// Intrinsic function that adds the specified built-in modifer value of the items in input
            /// Each entry pairs the current item include with the item under transformation.
            /// </summary>
            internal static void ItemSpecModifierFunction(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                bool includeNullEntries,
                StringSegment functionName,
                ItemSpecModifierKind modifierKind,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (TransformEntry item in input)
                {
                    // If the item include has become empty,
                    // this is the end of the pipeline for this item
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    string result = null;

                    try
                    {
                        // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                        // In that case,
                        // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                        // only exist within a target where we can trust the current directory
                        // 2. in single process mode we get the project directory set for the thread
                        string directoryToUse = item.Item.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? Directory.GetCurrentDirectory();
                        string definingProjectEscaped = item.Item.GetMetadataValueEscaped(ItemSpecModifiers.DefiningProjectFullPath);

                        result = ItemSpecModifiers.GetItemSpecModifier(item.Value, modifierKind, directoryToUse, definingProjectEscaped);
                    }
                    // InvalidOperationException is how GetItemSpecModifier communicates invalid conditions upwards, so
                    // we do not want to rethrow in that case.
                    catch (Exception e) when (!ExceptionHandling.NotExpectedException(e) || e is InvalidOperationException)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Value, e.Message);
                    }

                    if (!String.IsNullOrEmpty(result))
                    {
                        // GetItemSpecModifier will have returned us an escaped string
                        // there is nothing more to do than yield it into the pipeline
                        output.Add(new TransformEntry(result, item.Item));
                    }
                    else if (includeNullEntries)
                    {
                        output.Add(new TransformEntry(null, item.Item));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the subset of items that actually exist on disk.
            /// </summary>
            internal static void Exists(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (TransformEntry item in input)
                {
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Value);

                    string rootedPath = null;
                    try
                    {
                        // If we're a projectitem instance then we need to get
                        // the project directory and be relative to that
                        if (Path.IsPathRooted(unescapedPath))
                        {
                            rootedPath = unescapedPath;
                        }
                        else
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case,
                            // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            // 2. in single process mode we get the project directory set for the thread
                            string baseDirectoryToUse = item.Item.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                            rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                        }
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Value, e.Message);
                    }

                    if (FileSystems.Default.FileOrDirectoryExists(rootedPath))
                    {
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that combines the existing paths of the input items with a given relative path.
            /// </summary>
            internal static void Combine(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string relativePath = arguments[0].ValueOrEmpty;

                foreach (TransformEntry item in input)
                {
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Value);
                    string combinedPath = Path.Combine(unescapedPath, relativePath);
                    string escapedPath = EscapingUtilities.Escape(combinedPath);
                    output.Add(new TransformEntry(escapedPath, null));
                }
            }

            /// <summary>
            /// Intrinsic function that adds all ancestor directories of the given items.
            /// </summary>
            internal static void GetPathsOfAllDirectoriesAbove(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                // Phase 1: find all the applicable directories.

                SortedSet<string> directories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (TransformEntry item in input)
                {
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    string directoryName = null;

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Value);

                    try
                    {
                        string rootedPath;

                        // If we're a projectitem instance then we need to get
                        // the project directory and be relative to that
                        if (Path.IsPathRooted(unescapedPath))
                        {
                            rootedPath = unescapedPath;
                        }
                        else
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case,
                            // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            // 2. in single process mode we get the project directory set for the thread
                            string baseDirectoryToUse = item.Item.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                            rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                        }

                        // Normalize the path to remove elements like "..".
                        // Otherwise we run the risk of returning two or more different paths that represent the
                        // same directory.
                        rootedPath = FileUtilities.NormalizePath(rootedPath);
                        directoryName = Path.GetDirectoryName(rootedPath);
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Value, e.Message);
                    }

                    while (!String.IsNullOrEmpty(directoryName))
                    {
                        if (directories.Contains(directoryName))
                        {
                            // We've already got this directory (and all its ancestors) in the set.
                            break;
                        }

                        directories.Add(directoryName);
                        directoryName = Path.GetDirectoryName(directoryName);
                    }
                }

                // Phase 2: Go through the directories and return them in order

                foreach (string directoryPath in directories)
                {
                    string escapedDirectoryPath = EscapingUtilities.Escape(directoryPath);
                    output.Add(new TransformEntry(escapedDirectoryPath, null));
                }
            }

            /// <summary>
            /// Intrinsic function that adds the DirectoryName of the items in input
            /// UNDONE: This can be removed in favor of a built-in %(DirectoryName) metadata in future.
            /// </summary>
            internal static void DirectoryName(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                bool includeNullEntries,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                Dictionary<string, string> directoryNameTable = new Dictionary<string, string>(input.Count, StringComparer.OrdinalIgnoreCase);

                foreach (TransformEntry item in input)
                {
                    // If the item include has become empty,
                    // this is the end of the pipeline for this item
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    string directoryName;
                    if (!directoryNameTable.TryGetValue(item.Value, out directoryName))
                    {
                        // Unescape as we are passing to the file system
                        string unescapedPath = EscapingUtilities.UnescapeAll(item.Value);

                        try
                        {
                            string rootedPath;

                            // If we're a projectitem instance then we need to get
                            // the project directory and be relative to that
                            if (Path.IsPathRooted(unescapedPath))
                            {
                                rootedPath = unescapedPath;
                            }
                            else
                            {
                                // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                                // In that case,
                                // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                                // only exist within a target where we can trust the current directory
                                // 2. in single process mode we get the project directory set for the thread
                                string baseDirectoryToUse = item.Item.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                                rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                            }

                            directoryName = Path.GetDirectoryName(rootedPath);
                        }
                        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Value, e.Message);
                        }

                        // Escape as this is going back into the engine
                        directoryName = EscapingUtilities.Escape(directoryName);
                        directoryNameTable[unescapedPath] = directoryName;
                    }

                    if (!String.IsNullOrEmpty(directoryName))
                    {
                        // return a result through the enumerator
                        output.Add(new TransformEntry(directoryName, item.Item));
                    }
                    else if (includeNullEntries)
                    {
                        output.Add(new TransformEntry(null, item.Item));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the contents of the metadata in specified in argument[0].
            /// </summary>
            internal static void Metadata(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                bool includeNullEntries,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                StringSegment metadataName = arguments[0];

                foreach (TransformEntry item in input)
                {
                    if (item.Item != null)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName.ValueOrEmpty, ex.Message);
                        }

                        if (!String.IsNullOrEmpty(metadataValue))
                        {
                            // It may be that the itemspec has unescaped ';'s in it so we need to split here to handle
                            // that case.
                            if (metadataValue.IndexOf(';') >= 0)
                            {
                                var splits = ExpressionShredder.SplitSemiColonSeparatedList(metadataValue);

                                foreach (var itemSpec in splits)
                                {
                                    // return a result through the enumerator
                                    output.Add(new TransformEntry(itemSpec.ValueOrEmpty, item.Item));
                                }
                            }
                            else
                            {
                                // return a result through the enumerator
                                output.Add(new TransformEntry(metadataValue, item.Item));
                            }
                        }
                        else if (metadataValue != String.Empty && includeNullEntries)
                        {
                            output.Add(new TransformEntry(metadataValue, item.Item));
                        }
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only the items from input that have a distinct Include
            /// Using a case sensitive comparison.
            /// </summary>
            internal static void DistinctWithCase(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
                => DistinctWithComparer(input, output, arguments, StringComparer.Ordinal, functionName, elementLocation);

            /// <summary>
            /// Intrinsic function that adds only the items from input that have a distinct Include
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void Distinct(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
                => DistinctWithComparer(input, output, arguments, StringComparer.OrdinalIgnoreCase, functionName, elementLocation);

            /// <summary>
            /// Intrinsic function that adds only the items from input that have a distinct Include
            /// using the specified comparer.
            /// </summary>
            private static void DistinctWithComparer(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringComparer comparer,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                // This dictionary will ensure that we only return one result per unique itemspec
                HashSet<string> seenItems = new HashSet<string>(input.Count, comparer);

                foreach (TransformEntry item in input)
                {
                    if (item.Value != null && seenItems.Add(item.Value))
                    {
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function reverses the item list.
            /// </summary>
            internal static void Reverse(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                for (int i = input.Count - 1; i >= 0; i--)
                {
                    output.Add(input[i]);
                }
            }

            /// <summary>
            ///  Intrinsic function that transforms expressions like the %(foo) in @(Compile->'%(foo)').
            /// </summary>
            internal static void ExpandQuotedExpressionFunction(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                bool includeNullEntries,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                ExpandQuotedExpressionFunction(input, output, arguments[0], includeNullEntries, elementLocation);
            }

            internal static void ExpandQuotedExpressionFunction(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment text,
                bool includeNullEntries,
                IElementLocation elementLocation)
            {
                OneOrMultipleMetadataMatches matches = GetQuotedExpressionMatches(text, elementLocation);

                switch (matches.Type)
                {
                    case MetadataMatchType.None:
                        ExpandLiteralTransform(input, output, text, includeNullEntries);
                        return;

                    case MetadataMatchType.ExactSingle:
                        ExpandExactMetadataTransform(input, output, matches.Single, includeNullEntries, elementLocation);
                        return;

                    case MetadataMatchType.InexactSingle:
                        ExpandSingleMetadataTransform(input, output, text, matches.Single, includeNullEntries, elementLocation);
                        return;

                    case MetadataMatchType.Multiple:
                        ExpandMultipleMetadataTransform(input, output, text, matches.Multiple, includeNullEntries, elementLocation);
                        return;
                }
            }

            private static void ExpandLiteralTransform(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment literal,
                bool includeNullEntries)
            {
                string literalString = literal.ValueOrEmpty;
                foreach (TransformEntry item in input)
                {
                    AddTransformResult(output, item.Value is null ? null : literalString, item.Item, includeNullEntries);
                }
            }

            private static void ExpandExactMetadataTransform(
                List<TransformEntry> input,
                List<TransformEntry> output,
                MetadataMatch match,
                bool includeNullEntries,
                IElementLocation elementLocation)
            {
                foreach (TransformEntry item in input)
                {
                    string include = null;
                    if (item.Value is not null)
                    {
                        include = GetMetadataValueFromMatch(match, item.Value, item.Item, elementLocation);
                    }

                    AddTransformResult(output, include, item.Item, includeNullEntries);
                }
            }

            private static void ExpandSingleMetadataTransform(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment text,
                MetadataMatch match,
                bool includeNullEntries,
                IElementLocation elementLocation)
            {
                SpanBasedStringBuilder includeBuilder = s_includeBuilder ?? new SpanBasedStringBuilder();
                s_includeBuilder = null;

                int prefixLength = match.Index;
                int suffixIndex = match.Index + match.Length;
                int suffixLength = text.Length - suffixIndex;

                foreach (TransformEntry item in input)
                {
                    string include = null;
                    if (item.Value is not null)
                    {
                        if (prefixLength > 0)
                        {
                            includeBuilder.Append(text.AsMemory(0, prefixLength));
                        }

                        includeBuilder.Append(GetMetadataValueFromMatch(match, item.Value, item.Item, elementLocation));

                        if (suffixLength > 0)
                        {
                            includeBuilder.Append(text.AsMemory(suffixIndex, suffixLength));
                        }

                        include = includeBuilder.ToString();
                        includeBuilder.Clear();
                    }

                    AddTransformResult(output, include, item.Item, includeNullEntries);
                }

                s_includeBuilder = includeBuilder;
            }

            private static void ExpandMultipleMetadataTransform(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment text,
                List<MetadataMatch> matches,
                bool includeNullEntries,
                IElementLocation elementLocation)
            {
                SpanBasedStringBuilder includeBuilder = s_includeBuilder ?? new SpanBasedStringBuilder();
                s_includeBuilder = null;

                foreach (TransformEntry item in input)
                {
                    string include = null;
                    if (item.Value is not null)
                    {
                        int currentIndex = 0;
                        foreach (MetadataMatch match in matches)
                        {
                            includeBuilder.Append(text.AsMemory(currentIndex, match.Index - currentIndex));
                            includeBuilder.Append(GetMetadataValueFromMatch(match, item.Value, item.Item, elementLocation));
                            currentIndex = match.Index + match.Length;
                        }

                        includeBuilder.Append(text.AsMemory(currentIndex, text.Length - currentIndex));
                        include = includeBuilder.ToString();
                        includeBuilder.Clear();
                    }

                    AddTransformResult(output, include, item.Item, includeNullEntries);
                }

                s_includeBuilder = includeBuilder;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void AddTransformResult(List<TransformEntry> output, string include, I item, bool includeNullEntries)
            {
                // Empty transforms are optionally retained as null entries so callers can correlate
                // transform results with their source items.
                if (!include.IsNullOrEmpty())
                {
                    output.Add(new TransformEntry(include, item));
                }
                else if (includeNullEntries)
                {
                    output.Add(new TransformEntry(value: null, item));
                }
            }

            /// <summary>
            /// Scans the input string for unqualified metadata references of the form <c>%(Name)</c>.
            /// In the vast majority of cases, we'll only have 1-2 matches.
            /// Qualified metadata (e.g. <c>%(ItemType.Name)</c>) is not allowed in transforms and will throw.
            /// </summary>
            private static OneOrMultipleMetadataMatches GetQuotedExpressionMatches(
                StringSegment text,
                IElementLocation elementLocation)
            {
                int metadataMarkerIndex = ExpressionShredder.IndexOfMetadataMarker(text);
                if (metadataMarkerIndex == -1)
                {
                    return OneOrMultipleMetadataMatches.None;
                }

                List<MetadataMatch> multipleMatches = null;
                MetadataMatch firstMatch = default;
                bool hasFirstMatch = false;

                do
                {
                    int refEnd = metadataMarkerIndex + 2;

                    if (!ExpressionShredder.TryParseMetadataExpression(
                        text,
                        ref refEnd,
                        text.Length,
                        out StringSegment itemType,
                        out StringSegment name))
                    {
                        metadataMarkerIndex = ExpressionShredder.IndexOfMetadataMarker(text, metadataMarkerIndex + 2);
                        continue;
                    }

                    // Qualified metadata is not allowed in transforms.
                    if (itemType.HasValue)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(
                            elementLocation,
                            "QualifiedMetadataInTransformNotAllowed",
                            text.Slice(metadataMarkerIndex, refEnd - metadataMarkerIndex).ValueOrEmpty,
                            name.ValueOrEmpty);
                    }

                    int matchLength = refEnd - metadataMarkerIndex;

                    if (!hasFirstMatch)
                    {
                        firstMatch = new MetadataMatch(metadataMarkerIndex, matchLength, name);
                        hasFirstMatch = true;
                    }
                    else
                    {
                        multipleMatches ??= [firstMatch];
                        multipleMatches.Add(new MetadataMatch(metadataMarkerIndex, matchLength, name));
                    }

                    metadataMarkerIndex = ExpressionShredder.IndexOfMetadataMarker(text, refEnd);
                }
                while (metadataMarkerIndex >= 0);

                if (multipleMatches != null)
                {
                    return new OneOrMultipleMetadataMatches(multipleMatches);
                }

                if (hasFirstMatch)
                {
                    return new OneOrMultipleMetadataMatches(
                        text,
                        firstMatch.Index,
                        firstMatch.Length,
                        firstMatch.Name);
                }

                return OneOrMultipleMetadataMatches.None;
            }

            /// <summary>
            /// Intrinsic function that transforms expressions by invoking methods of System.String on the itemspec
            /// of the item in the pipeline.
            /// </summary>
            internal static void ExecuteStringFunction(
                Expander<P, I> expander,
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                bool includeNullEntries,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                // Transform: expression is like @(Compile->'%(foo)'), so create completely new items,
                // using the Include from the source items
                string methodName = functionName.ValueOrEmpty;
                foreach (TransformEntry item in input)
                {
                    Function function = new Function(
                        typeof(string),
                        item.Value,
                        item.Value,
                        methodName,
                        arguments,
                        BindingFlags.Public | BindingFlags.InvokeMethod,
                        string.Empty,
                        expander.PropertiesUseTracker,
                        expander._fileSystem,
                        expander._loggingContext);

                    object result = function.Execute(item.Value, expander._properties, ExpanderOptions.ExpandAll, elementLocation);

                    string include = PropertyExpander.ConvertToString(result);

                    // We pass in the existing item so we can copy over its metadata
                    if (include.Length > 0)
                    {
                        output.Add(new TransformEntry(include, item.Item));
                    }
                    else if (includeNullEntries)
                    {
                        output.Add(new TransformEntry(null, item.Item));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the items from input with their metadata cleared, i.e. only the itemspec is retained.
            /// </summary>
            internal static void ClearMetadata(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                bool includeNullEntries,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (TransformEntry item in input)
                {
                    if (includeNullEntries || item.Value != null)
                    {
                        output.Add(new TransformEntry(item.Value, null));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only those items that have a not-blank value for the metadata specified
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void HasMetadata(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                StringSegment metadataName = arguments[0];

                foreach (TransformEntry item in input)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName.ValueOrEmpty, ex.Message);
                    }

                    // GetMetadataValueEscaped returns empty string for missing metadata,
                    // but IItem specifies it should return null
                    if (!string.IsNullOrEmpty(metadataValue))
                    {
                        // return a result through the enumerator
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only those items have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void WithMetadataValue(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                StringSegment metadataName = arguments[0];
                StringSegment metadataValueToFind = arguments[1];

                foreach (TransformEntry item in input)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName.ValueOrEmpty, ex.Message);
                    }

                    if (metadataValue != null && metadataValueToFind.Equals(metadataValue, StringComparison.OrdinalIgnoreCase))
                    {
                        // return a result through the enumerator
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds those items don't have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void WithoutMetadataValue(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                StringSegment metadataName = arguments[0];
                StringSegment metadataValueToFind = arguments[1];

                foreach (TransformEntry item in input)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName.ValueOrEmpty, ex.Message);
                    }

                    if (!metadataValueToFind.Equals(metadataValue, StringComparison.OrdinalIgnoreCase))
                    {
                        // return a result through the enumerator
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds a boolean to indicate if any of the items have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void AnyHaveMetadataValue(
                List<TransformEntry> input,
                List<TransformEntry> output,
                StringSegment[] arguments,
                StringSegment functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                StringSegment metadataName = arguments[0];
                StringSegment metadataValueToFind = arguments[1];
                bool metadataFound = false;

                foreach (TransformEntry item in input)
                {
                    if (item.Item != null)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName.ValueOrEmpty, ex.Message);
                        }

                        if (metadataValue != null && metadataValueToFind.Equals(metadataValue, StringComparison.OrdinalIgnoreCase))
                        {
                            metadataFound = true;

                            // return a result through the enumerator
                            output.Add(new TransformEntry("true", item.Item));

                            // break out as soon as we found a match
                            return;
                        }
                    }
                }

                if (!metadataFound)
                {
                    // We did not locate an item with the required metadata
                    output.Add(new TransformEntry("false", null));
                }
            }

            /// <summary>
            /// Expands the metadata in the match provided into a string result.
            /// The match is expected to be the content of a transform.
            /// For example, representing "%(Filename.obj)" in the original expression "@(Compile->'%(Filename.obj)')".
            /// </summary>
            private static string GetMetadataValueFromMatch(
                MetadataMatch match,
                string itemSpec,
                IItem sourceOfMetadata,
                IElementLocation elementLocation)
            {
                string value = null;
                try
                {
                    if (match.IsDerivableItemSpecModifier)
                    {
                        ItemSpecModifierKind modifierKind = match.ModifierKind;
                        string directoryToUse = null;
                        string definingProjectEscaped = null;

                        // Only path-based and defining-project modifiers need project context. ProjectItem and
                        // ProjectItemInstance provide ProjectDirectory; otherwise, single-process evaluation sets
                        // the thread working directory, and multiprocess TaskItems can trust the process directory.
                        switch (modifierKind)
                        {
                            case ItemSpecModifierKind.FullPath:
                            case ItemSpecModifierKind.RootDir:
                            case ItemSpecModifierKind.Directory:
                                directoryToUse = sourceOfMetadata.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? Directory.GetCurrentDirectory();
                                break;

                            case ItemSpecModifierKind.DefiningProjectFullPath:
                            case ItemSpecModifierKind.DefiningProjectDirectory:
                            case ItemSpecModifierKind.DefiningProjectName:
                            case ItemSpecModifierKind.DefiningProjectExtension:
                                directoryToUse = sourceOfMetadata.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? Directory.GetCurrentDirectory();
                                definingProjectEscaped = sourceOfMetadata.GetMetadataValueEscaped(ItemSpecModifiers.DefiningProjectFullPath);
                                break;
                        }

                        value = ItemSpecModifiers.GetItemSpecModifier(itemSpec, modifierKind, directoryToUse, definingProjectEscaped);
                    }
                    else
                    {
                        value = sourceOfMetadata.GetMetadataValueEscaped(match.Name);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        elementLocation,
                        "CannotEvaluateItemMetadata",
                        match.Name.ValueOrEmpty,
                        ex.Message);
                }

                return value;
            }
        }
    }
}
