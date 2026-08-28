// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Globalization;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Text;
using Microsoft.NET.StringTools;
using Microsoft.Win32;

#if FEATURE_MSIOREDIST
// File is intentionally NOT aliased — all typeof() comparisons use fully-qualified
// System.IO.File to match the types registered in AvailableStaticMembers.
using Path = Microsoft.IO.Path;
#endif

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// Expands property expressions, like $(Configuration) and $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation).
    /// </summary>
    /// <remarks>
    /// This is a private nested type, exposed only through the Expander class.
    /// That allows it to hide its private methods even from Expander.
    /// </remarks>
    private readonly ref struct PropertyExpander
    {
        private const string RegistryPrefix = "Registry:";
        private const string SolutionsVsVersionProperty = "Solutions.VSVersion";
        private const string SolutionsVsVersionExpression = $"$({SolutionsVsVersionProperty})";
        private const string VstsDbDirectoryProperty = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory";

        private readonly IPropertyProvider<P> _properties;
        private readonly ExpanderOptions _options;
        private readonly IElementLocation _elementLocation;
        private readonly PropertiesUseTracker _propertiesUseTracker;
        private readonly IFileSystem _fileSystem;
        private readonly bool _isTruncationEnabled;

        private PropertyExpander(
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            _properties = properties;
            _options = options;
            _elementLocation = elementLocation;
            _propertiesUseTracker = propertiesUseTracker;
            _fileSystem = fileSystem;
            _isTruncationEnabled = IsTruncationEnabled(options);
        }

        /// <summary>
        /// This method takes a string which may contain any number of
        /// "$(propertyname)" tags in it.  It replaces all those tags with
        /// the actual property values, and returns a new string.  For example,
        ///
        ///     string processedString =
        ///         propertyBag.ExpandProperties("Value of NoLogo is $(NoLogo).");
        ///
        /// This code might produce:
        ///
        ///     processedString = "Value of NoLogo is true."
        ///
        /// If the sourceString contains an embedded property which doesn't
        /// have a value, then we replace that tag with an empty string.
        ///
        /// This method leaves the result escaped.  Callers may need to unescape on their own as appropriate.
        /// </summary>
        internal static string ExpandPropertiesLeaveEscaped(
            string expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            return
                ConvertToString(
                    ExpandPropertiesLeaveTypedAndEscaped(
                        expression,
                        properties,
                        options,
                        elementLocation,
                        propertiesUseTracker,
                        fileSystem));
        }

        /// <summary>
        ///  Expands property references in <paramref name="expression"/> while preserving typed results.
        /// </summary>
        /// <param name="expression">The expression to expand.</param>
        /// <param name="properties">The provider used to resolve property values.</param>
        /// <param name="options">The options controlling expansion behavior.</param>
        /// <param name="elementLocation">The location associated with the expression.</param>
        /// <param name="propertiesUseTracker">The tracker notified when properties are read.</param>
        /// <param name="fileSystem">The file system used by property functions.</param>
        /// <returns>
        ///  The expanded value. A single expansion can preserve its runtime type; concatenated results are
        ///  returned as strings.
        /// </returns>
        /// <remarks>
        ///  The result remains escaped. Callers are responsible for unescaping it when appropriate.
        /// </remarks>
        internal static object ExpandPropertiesLeaveTypedAndEscaped(
            string expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            if (((options & ExpanderOptions.ExpandProperties) == 0) || expression.IsNullOrEmpty())
            {
                return expression;
            }

            Assumed.NotNull(properties, "Cannot expand properties without providing properties");

            // If there are no substitutions, then just return the string.
            int markerIndex = ExpressionShredder.IndexOfPropertyMarker(expression);
            if (markerIndex == -1)
            {
                return expression;
            }

            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            return expander.ExpandPropertiesLeaveTypedAndEscaped(expression, markerIndex);
        }

        private object ExpandPropertiesLeaveTypedAndEscaped(string expression, int markerIndex)
        {
            using SpanBasedConcatenator results = new();
            int index = 0;

            while (markerIndex >= 0)
            {
                if (markerIndex - index > 0)
                {
                    results.Add(expression.AsMemory(index, markerIndex - index));
                }

                int startIndex = markerIndex + 2;
                int closingParenIndex = FindClosingParenthesis(
                    expression,
                    startIndex,
                    out bool isPotentialPropertyFunction,
                    out bool isPotentialRegistryFunction);

                if (closingParenIndex < 0)
                {
                    results.Add(expression.AsMemory(markerIndex));
                    return results.GetResult();
                }

                int length = closingParenIndex - startIndex;
                object propertyValue = length == 0
                    ? string.Empty
                    : !isPotentialPropertyFunction && !isPotentialRegistryFunction
                        ? LookupProperty(expression, startIndex, closingParenIndex - 1)
                        : ExpandProperty(
                            expression,
                            startIndex,
                            closingParenIndex - 1,
                            isPotentialRegistryFunction,
                            isPotentialPropertyFunction);

                if (propertyValue != null)
                {
                    if (_isTruncationEnabled)
                    {
                        string value = propertyValue.ToString();
                        if (value.Length > CharacterLimitPerExpansion)
                        {
                            propertyValue = TruncateString(value);
                        }
                    }

                    results.Add(propertyValue);
                }

                index = closingParenIndex + 1;
                markerIndex = ExpressionShredder.IndexOfPropertyMarker(expression, index);
            }

            if (expression.Length - index > 0)
            {
                results.Add(expression.AsMemory(index));
            }

            return results.GetResult();
        }

        /// <summary>
        ///  Expands property references directly from a <see cref="StringSegment"/> while preserving typed
        ///  results.
        /// </summary>
        /// <param name="expression">The expression segment to expand.</param>
        /// <param name="properties">The provider used to resolve property values.</param>
        /// <param name="options">The options controlling expansion behavior.</param>
        /// <param name="elementLocation">The location associated with the expression.</param>
        /// <param name="propertiesUseTracker">The tracker notified when properties are read.</param>
        /// <param name="fileSystem">The file system used by property functions.</param>
        /// <returns>
        ///  The expanded value. A single expansion can preserve its runtime type; concatenated results are
        ///  returned as strings.
        /// </returns>
        /// <remarks>
        ///  Marker discovery operates on the segment without first materializing it as a string. The result
        ///  remains escaped.
        /// </remarks>
        internal static object ExpandPropertiesLeaveTypedAndEscaped(
            StringSegment expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            if (((options & ExpanderOptions.ExpandProperties) == 0) || expression.IsNullOrEmpty)
            {
                // Preserve the string/null result contract when expansion is disabled or the source is empty.
                return expression.Value;
            }

            Assumed.NotNull(properties, "Cannot expand properties without providing properties");

            // If there are no substitutions, then just return the string.
            int markerIndex = IndexOfPropertyMarker(expression, startIndex: 0);
            if (markerIndex == -1)
            {
                // Materialize only after proving that no property expansion is needed.
                return expression.Value;
            }

            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            return expander.ExpandPropertiesLeaveTypedAndEscaped(expression, markerIndex);
        }

        /// <summary>
        ///  Expands an expression after its first property marker has been located.
        /// </summary>
        /// <param name="expression">The non-empty expression segment to expand.</param>
        /// <param name="markerIndex">
        ///  The segment-relative index of the first <c>$(</c> marker.
        /// </param>
        /// <returns>
        ///  The expanded value, preserving a single result's runtime type when possible.
        /// </returns>
        private object ExpandPropertiesLeaveTypedAndEscaped(StringSegment expression, int markerIndex)
        {
            // COMPAT: WebProjects may have an import with a condition like
            // Condition=" '$(Solutions.VSVersion)' == '8.0'". These evaluated to empty in earlier MSBuild
            // versions but are otherwise parsed as property functions now. Comparing the complete segment
            // intentionally excludes embedded occurrences from this compatibility behavior.
            if (markerIndex == 0
                && expression.Equals(SolutionsVsVersionExpression, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            // We will build our set of results as object components
            // so that we can either maintain the object's type in the event
            // that we have a single component, or convert to a string
            // if concatenation is required.
            using SpanBasedConcatenator results = new();

            // The index is the zero-based index into the expression,
            // where we've essentially read up to and copied into the target string.
            int index = 0;

            // Search for "$(" in the expression.  Loop until we don't find it any more.
            while (markerIndex >= 0)
            {
                // Append the result with the portion of the expression up to
                // (but not including) the "$(", and advance the index pointer.
                if (markerIndex - index > 0)
                {
                    results.Add(expression.AsMemory(index, markerIndex - index));
                }

                int startIndex = markerIndex + 2;

                // Following the "$(" we need to locate the matching ')'
                // Scan for the matching closing bracket, skipping any nested ones
                // This is a very complete, fast validation of parenthesis matching including for nested
                // function calls.
                int closingParenIndex = FindClosingParenthesis(
                    expression,
                    startIndex,
                    out bool isPotentialPropertyFunction,
                    out bool isPotentialRegistryFunction);

                if (closingParenIndex < 0)
                {
                    // If we didn't find the closing parenthesis, that means this
                    // isn't really a well-formed property. Copy the remainder of the
                    // expression (starting with the "$(" that we found) into the result, and return.
                    results.Add(expression.AsMemory(markerIndex));
                    return results.GetResult();
                }

                // Expand the property body between the "$(" marker and its matching closing parenthesis.
                StringSegment propertyBody = expression[startIndex..closingParenIndex];

                object propertyValue = propertyBody.Length == 0
                    ? string.Empty // Compat: $() should return string.Empty
                    : !isPotentialPropertyFunction && !isPotentialRegistryFunction
                        ? LookupProperty(propertyBody)
                        : ExpandProperty(propertyBody, isPotentialRegistryFunction, isPotentialPropertyFunction);

                if (propertyValue != null)
                {
                    if (_isTruncationEnabled)
                    {
                        string value = propertyValue.ToString();
                        if (value.Length > CharacterLimitPerExpansion)
                        {
                            propertyValue = TruncateString(value);
                        }
                    }

                    results.Add(propertyValue);
                }

                index = closingParenIndex + 1;
                markerIndex = IndexOfPropertyMarker(expression, index);
            }

            // If we couldn't find any more property markers in the expression just copy the remainder into the result.
            if (expression.Length - index > 0)
            {
                results.Add(expression.AsMemory(index));
            }

            return results.GetResult();
        }

        /// <summary>
        ///  Finds the first property marker at or after a segment-relative index.
        /// </summary>
        /// <param name="segment">The segment to search.</param>
        /// <param name="startIndex">The segment-relative index at which to begin searching.</param>
        /// <returns>
        ///  The segment-relative index of the marker, or <c>-1</c> if no marker is found.
        /// </returns>
        private static int IndexOfPropertyMarker(StringSegment segment, int startIndex)
        {
            int markerIndex = ExpressionShredder.IndexOfPropertyMarker(
                segment.Buffer,
                startIndex: segment.Offset + startIndex,
                count: segment.Length - startIndex);

            // ExpressionShredder returns a buffer-relative index; translate it to this segment's
            // coordinates.
            return markerIndex >= 0
                ? markerIndex - segment.Offset
                : -1;
        }

        private static int FindClosingParenthesis(
            string expression,
            int index,
            out bool isPotentialPropertyFunction,
            out bool isPotentialRegistryFunction)
        {
            int nestLevel = 1;
            int length = expression.Length;

            isPotentialPropertyFunction = false;
            isPotentialRegistryFunction = false;

            while (index < length && nestLevel > 0)
            {
                char character = expression[index];

                switch (character)
                {
                    case '\'' or '`' or '"':
                        int quoteIndex = expression.IndexOf(character, index + 1);

                        if (quoteIndex < 0)
                        {
                            return -1;
                        }

                        index = quoteIndex;
                        break;

                    case '(':
                        nestLevel++;
                        break;

                    case ')':
                        nestLevel--;
                        break;

                    case '.' or '[' or '$':
                        isPotentialPropertyFunction = true;
                        break;

                    case ':':
                        isPotentialRegistryFunction = true;
                        break;
                }

                index++;
            }

            return nestLevel == 0 ? index - 1 : -1;
        }

        /// <summary>
        ///  Finds the closing parenthesis that matches the opening parenthesis immediately
        ///  preceding <paramref name="index"/>.
        /// </summary>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="index">The segment-relative index at which to begin scanning.</param>
        /// <param name="isPotentialPropertyFunction">
        ///  Whether the property body might contain a property function.
        /// </param>
        /// <param name="isPotentialRegistryFunction">
        ///  Whether the property body might contain a registry function.
        /// </param>
        /// <returns>
        ///  The segment-relative index of the matching closing parenthesis, or <c>-1</c> if it was not
        ///  found.
        /// </returns>
        private static int FindClosingParenthesis(
            StringSegment expression,
            int index,
            out bool isPotentialPropertyFunction,
            out bool isPotentialRegistryFunction)
        {
            int nestLevel = 1;
            int length = expression.Length;

            isPotentialPropertyFunction = false;
            isPotentialRegistryFunction = false;

            // Scan for our closing ')'
            while (index < length && nestLevel > 0)
            {
                char character = expression[index];

                switch (character)
                {
                    case '\'' or '`' or '"':
                        int quoteIndex = expression.IndexOf(character, index + 1);

                        if (quoteIndex < 0)
                        {
                            return -1;
                        }

                        index = quoteIndex;
                        break;

                    case '(':
                        nestLevel++;
                        break;

                    case ')':
                        nestLevel--;
                        break;

                    case '.' or '[' or '$':
                        isPotentialPropertyFunction = true;
                        break;

                    case ':':
                        isPotentialRegistryFunction = true;
                        break;
                }

                index++;
            }

            // We will have parsed past the ')', so step back one character.
            return nestLevel == 0 ? index - 1 : -1;
        }

        private object ExpandProperty(
            string expression,
            int startIndex,
            int endIndex,
            bool tryExtractRegistryFunction,
            bool tryExtractPropertyFunction)
        {
            int length = endIndex - startIndex + 1;

            if (length == VstsDbDirectoryProperty.Length &&
                string.Compare(expression, startIndex, VstsDbDirectoryProperty, 0, length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return string.Empty;
            }

            if (length == SolutionsVsVersionProperty.Length &&
                string.Equals(expression, SolutionsVsVersionExpression, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (tryExtractRegistryFunction &&
                length >= RegistryPrefix.Length &&
                string.Compare(expression, startIndex, RegistryPrefix, 0, RegistryPrefix.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return ExpandRegistryValue(expression.Substring(startIndex, length));
            }

            if (tryExtractPropertyFunction)
            {
                return ExpandPropertyBody(new StringSegment(expression, startIndex, length));
            }

            return LookupProperty(expression, startIndex, endIndex);
        }

        /// <summary>
        ///  Expands a non-empty property body after it has been classified during parenthesis matching.
        /// </summary>
        /// <param name="text">
        ///  The property body, excluding the surrounding <c>$(</c> and <c>)</c>.
        /// </param>
        /// <param name="tryExtractRegistryFunction">
        ///  Whether <paramref name="text"/> might be a registry expression.
        /// </param>
        /// <param name="tryExtractPropertyFunction">
        ///  Whether <paramref name="text"/> might be a property-function expression.
        /// </param>
        /// <returns>
        ///  The expanded property, registry, or property-function value.
        /// </returns>
        private object ExpandProperty(
            StringSegment text,
            bool tryExtractRegistryFunction,
            bool tryExtractPropertyFunction)
        {
            // Compat hack: as a special case, $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory) should return string.Empty
            // Note that very few properties have this exact length, so this check should be fast.
            if (text.Equals(VstsDbDirectoryProperty, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (tryExtractRegistryFunction &&
                text.Length >= RegistryPrefix.Length &&
                text.StartsWith(RegistryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // If the property body starts with any of our special objects, then deal with them
                // This is a registry reference, like $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)
                // Note: ExpandRegistryValue returns an empty string if not on Windows.
                return ExpandRegistryValue(text.ValueOrEmpty);
            }

            if (tryExtractPropertyFunction)
            {
                // This is likely to be a function expression
                return ExpandPropertyBody(text);
            }

            // This is a regular property
            return LookupProperty(text);
        }

        /// <summary>
        ///  Expands a property body, including its complete property-function invocation chain.
        /// </summary>
        /// <param name="propertyBody">
        ///  The non-empty property body, excluding the surrounding <c>$(</c> and <c>)</c>.
        /// </param>
        /// <returns>
        ///  The property value after applying every parsed invocation.
        /// </returns>
        private object ExpandPropertyBody(StringSegment propertyBody)
        {
            if (char.IsWhiteSpace(propertyBody[0]) || char.IsWhiteSpace(propertyBody[^1]))
            {
                propertyBody = propertyBody.Trim();
            }

            if (BuildParameters.DebugExpansion)
            {
                Console.WriteLine("Expanding: {0}", propertyBody);
            }

            if (!PropertyFunctionParser.TryParse(propertyBody, _elementLocation, out PropertyFunctionExpression propertyFunction))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    _elementLocation,
                    "InvalidFunctionPropertyExpression",
                    propertyBody.ValueOrEmpty,
                    string.Empty);
                return null;
            }

            ref readonly OneOrMany<PropertyFunctionInvocation> invocations = ref propertyFunction.Invocations;
            ref readonly PropertyFunctionInvocation firstInvocation =
                ref OneOrMany<PropertyFunctionInvocation>.ItemRefUnchecked(in invocations, 0);
            object propertyValue = firstInvocation.ReceiverKind == ReceiverKind.MSBuildProperty
                ? LookupProperty(firstInvocation.Receiver)
                : null;

            PropertyFunctionExecutionContext<P> functionContext = new(
                _properties,
                _options,
                _propertiesUseTracker,
                _fileSystem,
                _propertiesUseTracker.LoggingContext,
                _elementLocation);

            int invocationCount = invocations.Count;
            for (int i = 0; i < invocationCount; i++)
            {
                ref readonly PropertyFunctionInvocation invocation = ref (i == 0
                    ? ref firstInvocation
                    : ref OneOrMany<PropertyFunctionInvocation>.ItemRefUnchecked(in invocations, i));
                try
                {
                    // Preserve the live result as the receiver for the next parsed function.
                    if (!PropertyFunctionExecutor.Execute(
                        invocation,
                        propertyValue,
                        in functionContext,
                        out propertyValue))
                    {
                        break;
                    }
                }
                catch (Exception) when (_options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    int invocationStartIndex = invocation.Text.Offset - propertyFunction.Text.Offset;
                    propertyValue = invocationStartIndex == 0
                        ? propertyBody.ValueOrEmpty
                        : propertyBody[invocationStartIndex..].ValueOrEmpty;
                    break;
                }
            }

            return propertyValue;
        }

        /// <summary>
        /// Convert the object into an MSBuild friendly string
        /// Arrays are supported.
        /// Will not return NULL.
        /// </summary>
        internal static string ConvertToString(object valueToConvert)
        {
            if (valueToConvert == null)
            {
                return String.Empty;
            }
            // If the value is a string, then there is nothing to do
            if (valueToConvert is string stringValue)
            {
                return stringValue;
            }

            string convertedString;
            if (valueToConvert is IDictionary dictionary)
            {
                // If the return type is an IDictionary, then we convert this to
                // a semi-colon delimited set of A=B pairs.
                // Key and Value are converted to string and escaped
                if (dictionary.Count > 0)
                {
                    using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(";");
                        }

                        // convert and escape each key and value in the dictionary entry
                        builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Key)));
                        builder.Append("=");
                        builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Value)));
                    }

                    convertedString = builder.ToString();
                }
                else
                {
                    convertedString = string.Empty;
                }
            }
            else if (valueToConvert is IEnumerable enumerable)
            {
                // If the return is enumerable, then we'll convert to semi-colon delimited elements
                // each of which must be converted, so we'll recurse for each element
                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

                foreach (object element in enumerable)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(";");
                    }

                    // we need to convert and escape each element of the array
                    builder.Append(EscapingUtilities.Escape(ConvertToString(element)));
                }

                convertedString = builder.ToString();
            }
            else
            {
                // The fall back is always to just convert to a string directly.
                // Issue: https://github.com/dotnet/msbuild/issues/9757
                convertedString = Convert.ToString(valueToConvert, CultureInfo.InvariantCulture);
            }

            return convertedString;
        }

        /// <summary>
        ///  Looks up a simple property reference and records the read.
        /// </summary>
        /// <param name="propertyName">The string containing the property name.</param>
        /// <param name="startIndex">The inclusive index at which the property name starts.</param>
        /// <param name="endIndex">The inclusive index at which the property name ends.</param>
        /// <returns>
        ///  The escaped property value, or an empty string when the property is uninitialized.
        /// </returns>
        /// <remarks>
        ///  Context-dependent <c>MSBuildThisFile*</c> properties are expanded when they are not present in the
        ///  property provider.
        /// </remarks>
        private string LookupProperty(string propertyName, int startIndex, int endIndex)
        {
            P property = _properties.GetProperty(propertyName, startIndex, endIndex);

            bool isUninitialized = property is null;
            bool isArtificial = isUninitialized
                && endIndex - startIndex >= "MSBuild".Length
                && MSBuildNameIgnoreCaseComparer.Default.Equals("MSBuild", propertyName, startIndex, "MSBuild".Length);

            _propertiesUseTracker.TrackRead(propertyName, startIndex, endIndex, _elementLocation, isUninitialized, isArtificial);

            if (isArtificial)
            {
                return ExpandMSBuildThisFileProperty(new StringSegment(propertyName, startIndex, endIndex - startIndex + 1));
            }

            if (isUninitialized)
            {
                return string.Empty;
            }

            if (property is ProjectPropertyInstance.EnvironmentDerivedProjectPropertyInstance environmentDerivedProperty)
            {
                environmentDerivedProperty.loggingContext = _propertiesUseTracker.LoggingContext;
            }

            return property.GetEvaluatedValueEscaped(_elementLocation);
        }

        private string LookupProperty(StringSegment propertyName)
        {
            string buffer = propertyName.Buffer;
            int startIndex = propertyName.Offset;
            int endIndex = startIndex + propertyName.Length - 1;

            P property = _properties.GetProperty(buffer, startIndex, endIndex);

            bool isUninitialized = property is null;
            bool isArtificial = isUninitialized
                && propertyName.Length > "MSBuild".Length
                && propertyName.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase);

            _propertiesUseTracker.TrackRead(buffer, startIndex, endIndex, _elementLocation, isUninitialized, isArtificial);

            if (isArtificial)
            {
                // It could be one of the MSBuildThisFileXXXX properties,
                // whose values vary according to the file they are in.
                return ExpandMSBuildThisFileProperty(propertyName);
            }

            if (isUninitialized)
            {
                return string.Empty;
            }

            if (property is ProjectPropertyInstance.EnvironmentDerivedProjectPropertyInstance environmentDerivedProperty)
            {
                environmentDerivedProperty.loggingContext = _propertiesUseTracker.LoggingContext;
            }

            return property.GetEvaluatedValueEscaped(_elementLocation);
        }

        /// <summary>
        ///  Expands a context-dependent <c>MSBuildThisFile*</c> property.
        /// </summary>
        /// <param name="propertyName">The property name to expand.</param>
        /// <returns>
        ///  The value derived from the current element location, or an empty string when the name is not
        ///  recognized or the location has no file.
        /// </returns>
        private string ExpandMSBuildThisFileProperty(StringSegment propertyName)
        {
            const string Prefix = "MSBuildThisFile";

            if (_elementLocation.File.Length == 0 ||
                !propertyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            StringSegment suffix = propertyName[Prefix.Length..];

            switch (suffix)
            {
                case []: // MSBuildThisFile
                    return Path.GetFileName(_elementLocation.File);

                case { Length: 4 } and ['N' or 'n', ..]: // MSBuildThisFileName
                    if (suffix.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetFileNameWithoutExtension(_elementLocation.File);
                    }

                    break;

                case { Length: 8 } and ['F' or 'f', ..]: // MSBuildThisFileFullPath
                    if (suffix.Equals("FullPath", StringComparison.OrdinalIgnoreCase))
                    {
                        return FileUtilities.NormalizePath(_elementLocation.File);
                    }

                    break;

                case { Length: 9 } and ['E' or 'e', ..]: // MSBuildThisFileExtension
                    if (suffix.Equals("Extension", StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.GetExtension(_elementLocation.File);
                    }

                    break;

                case { Length: 9 } and ['D' or 'd', ..]: // MSBuildThisFileDirectory
                    if (suffix.Equals("Directory", StringComparison.OrdinalIgnoreCase))
                    {
                        return FileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(_elementLocation.File));
                    }

                    break;

                case { Length: 15 } and ['D' or 'd', ..]: // MSBuildThisFileDirectoryNoRoot
                    if (suffix.Equals("DirectoryNoRoot", StringComparison.OrdinalIgnoreCase))
                    {
                        string directory = Path.GetDirectoryName(_elementLocation.File);
                        int rootLength = Path.GetPathRoot(directory).Length;
                        return FileUtilities.EnsureTrailingNoLeadingSlash(directory, rootLength);
                    }

                    break;
            }

            return string.Empty;
        }

        /// <summary>
        /// Given a string like "Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation", return the value at that location
        /// in the registry. If the value isn't found, returns String.Empty.
        /// Properties may refer to a registry location by using the syntax for example
        /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
        /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
        /// the default value is desired.
        /// </summary>
        private string ExpandRegistryValue(string registryExpression)
        {
#if RUNTIME_TYPE_NETCORE
            // .NET Core MSBuild used to always return empty, so match that behavior
            // on non-Windows (no registry).
            if (!NativeMethodsShared.IsWindows)
            {
                return string.Empty;
            }
#endif

            // Remove "Registry:" prefix
            string registryLocation = registryExpression.Substring(RegistryPrefix.Length);

            // Split off the value name -- the part after the "@" sign. If there's no "@" sign, then it's the default value name
            // we want.
            int firstAtSignOffset = registryLocation.IndexOf('@');
            int lastAtSignOffset = registryLocation.LastIndexOf('@');

            ProjectErrorUtilities.VerifyThrowInvalidProject(firstAtSignOffset == lastAtSignOffset, _elementLocation, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", String.Empty);

            string valueName = lastAtSignOffset == -1 || lastAtSignOffset == registryLocation.Length - 1
                ? null : registryLocation.Substring(lastAtSignOffset + 1);

            // If there's no '@', or '@' is first, then we'll use null or String.Empty for the location; otherwise
            // the location is the part before the '@'
            string registryKeyName = lastAtSignOffset != -1 ? registryLocation.Substring(0, lastAtSignOffset) : registryLocation;

            string result = String.Empty;
            if (registryKeyName != null)
            {
                // We rely on the '@' character to delimit the key and its value, but the registry
                // allows this character to be used in the names of keys and the names of values.
                // Hence we use our standard escaping mechanism to allow users to access such keys
                // and values.
                registryKeyName = EscapingUtilities.UnescapeAll(registryKeyName);

                if (valueName != null)
                {
                    valueName = EscapingUtilities.UnescapeAll(valueName);
                }

                try
                {
                    // Unless we are running under Windows, don't bother with anything but the user keys
                    if (!NativeMethodsShared.IsWindows && !registryKeyName.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fake common requests to HKLM that we can resolve

                        // This is the base path of the framework
                        if (registryKeyName.StartsWith(@"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework", StringComparison.OrdinalIgnoreCase) &&
                            valueName.Equals("InstallRoot", StringComparison.OrdinalIgnoreCase))
                        {
                            return NativeMethodsShared.FrameworkBasePath + Path.DirectorySeparatorChar;
                        }

                        return string.Empty;
                    }

                    object valueFromRegistry = Registry.GetValue(registryKeyName, valueName, null /* default if key or value name is not found */);

                    if (valueFromRegistry != null)
                    {
                        // Convert the result to a string that is reasonable for MSBuild
                        result = ConvertToString(valueFromRegistry);
                    }
                    else
                    {
                        // This means either the key or value was not found in the registry.  In this case,
                        // we simply expand the property value to String.Empty to imitate the behavior of
                        // normal properties.
                        result = String.Empty;
                    }
                }
                catch (Exception ex) when (!ExceptionHandling.NotExpectedRegistryException(ex))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidRegistryPropertyExpression", $"$({registryExpression})", ex.Message);
                }
            }

            return result;
        }
    }
}
