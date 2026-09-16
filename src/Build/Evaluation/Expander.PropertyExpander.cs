// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
#if !NET
using System.Linq;
#endif
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
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
        private const string SolutionsVsVersionProperty = "$(Solutions.VSVersion)";
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
            => PropertyValueConverter.ToString(
                ExpandPropertiesLeaveTypedAndEscaped(
                    expression,
                    properties,
                    options,
                    elementLocation,
                    propertiesUseTracker,
                    fileSystem));

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
        /// This method leaves the result typed and escaped.  Callers may need to convert to string, and unescape on their own as appropriate.
        /// </summary>
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

        /// <summary>
        ///  Expands the first property reference in <paramref name="text"/> and selects a direct, single-property,
        ///  or multi-property result path.
        /// </summary>
        /// <param name="text">The text containing one or more property references.</param>
        /// <param name="markerIndex">The index of the first <c>$(</c> marker.</param>
        /// <returns>
        ///  The typed result when one property reference occupies the entire text; otherwise, the expanded string.
        /// </returns>
        private object ExpandPropertiesLeaveTypedAndEscaped(string text, int markerIndex)
        {
            // Compat: Some WebProjects contain imports with conditions such as:
            //     Condition=" '$(Solutions.VSVersion)' == '8.0'"
            // This expression evaluated to empty in earlier MSBuild versions, but the dot otherwise makes it look
            // like a property function. Preserve the legacy result only when the complete expression matches.
            if (markerIndex == 0 &&
                string.Equals(text, SolutionsVsVersionProperty, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (!TryExpandPropertyReference(text, markerIndex, out int closingParenIndex, out object propertyValue))
            {
                // No expansion occurred, so preserve the complete malformed expression.
                return FileUtilities.MaybeAdjustFilePath(text);
            }

            if (markerIndex == 0 && closingParenIndex == text.Length - 1)
            {
                // Preserve a non-string function result when the property is the complete expression.
                return propertyValue is string stringValue
                    ? FileUtilities.MaybeAdjustFilePath(stringValue)
                    : propertyValue ?? string.Empty;
            }

            int index = closingParenIndex + 1;

            // Determine whether this is a one-property composite before paying for the general concatenator.
            int nextMarkerIndex = index < text.Length
                ? ExpressionShredder.IndexOfPropertyMarker(text, index)
                : -1;

            return nextMarkerIndex < 0
                ? ConcatenateSingleProperty(text, markerIndex, index, propertyValue)
                : ExpandMultipleProperties(text, markerIndex, propertyValue, index, nextMarkerIndex);
        }

        /// <summary>
        ///  Concatenates the adjusted literal prefix and suffix around one expanded property reference.
        /// </summary>
        /// <param name="text">The complete source text.</param>
        /// <param name="markerIndex">The index at which the property reference begins.</param>
        /// <param name="suffixIndex">The index immediately following the property reference.</param>
        /// <param name="propertyValue">The expanded property value.</param>
        /// <returns>
        ///  The weakly interned concatenation.
        /// </returns>
        private static string ConcatenateSingleProperty(string text, int markerIndex, int suffixIndex, object propertyValue)
        {
            string value = propertyValue?.ToString() ?? string.Empty;

            // Path adjustment can only preserve or shorten each component, so their original lengths provide an
            // upper bound for the destination buffer.
            int capacity = checked(markerIndex + value.Length + text.Length - suffixIndex);

            using BufferScope<char> buffer = new(capacity);
            Span<char> result = buffer;

            // Write each adjusted component directly into the final buffer to avoid intermediate strings.
            int length = 0;
            length += FileUtilities.MaybeAdjustFilePathAndCopyTo(text.AsSpan(0, markerIndex), result[length..]);
            length += FileUtilities.MaybeAdjustFilePathAndCopyTo(value, result[length..]);
            length += FileUtilities.MaybeAdjustFilePathAndCopyTo(text.AsSpan(suffixIndex), result[length..]);

            return Strings.WeakIntern(result[..length]);
        }

        /// <summary>
        ///  Expands and concatenates an expression containing multiple property references.
        /// </summary>
        /// <param name="text">The complete source text.</param>
        /// <param name="firstMarkerIndex">The index at which the already-expanded first property begins.</param>
        /// <param name="firstPropertyValue">The already-expanded first property value.</param>
        /// <param name="index">The index immediately following the first property reference.</param>
        /// <param name="markerIndex">The index at which the second property reference begins.</param>
        /// <returns>
        ///  The expanded result.
        /// </returns>
        private object ExpandMultipleProperties(string text, int firstMarkerIndex, object firstPropertyValue, int index, int markerIndex)
        {
            using SpanBasedConcatenator results = new();

            // Seed the concatenator with the work completed by the caller.
            if (firstMarkerIndex > 0)
            {
                results.Add(text.AsMemory(0, firstMarkerIndex));
            }

            if (firstPropertyValue != null)
            {
                results.Add(firstPropertyValue);
            }

            while (markerIndex >= 0)
            {
                if (markerIndex > index)
                {
                    results.Add(text.AsMemory(index, markerIndex - index));
                }

                if (!TryExpandPropertyReference(text, markerIndex, out int closingParenIndex, out object propertyValue))
                {
                    // Preserve the malformed marker and everything following it verbatim.
                    results.Add(text.AsMemory(markerIndex));
                    return results.GetResult();
                }

                if (propertyValue != null)
                {
                    results.Add(propertyValue);
                }

                index = closingParenIndex + 1;
                markerIndex = ExpressionShredder.IndexOfPropertyMarker(text, index);
            }

            if (index < text.Length)
            {
                results.Add(text.AsMemory(index));
            }

            return results.GetResult();
        }

        /// <summary>
        ///  Locates and expands one property reference.
        /// </summary>
        /// <param name="text">The complete source text.</param>
        /// <param name="markerIndex">The index of the opening <c>$(</c> marker.</param>
        /// <param name="closingParenIndex">
        ///  The index of the matching closing parenthesis, or <c>-1</c> when the reference is malformed.
        /// </param>
        /// <param name="propertyValue">
        ///  The expanded and potentially truncated property value, or <see langword="null"/> when expansion produces
        ///  no value.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> when a matching closing parenthesis was found; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        private bool TryExpandPropertyReference(string text, int markerIndex, out int closingParenIndex, out object propertyValue)
        {
            int startIndex = markerIndex + 2;
            closingParenIndex = FindClosingParenthesis(
                text,
                startIndex,
                out bool isPotentialPropertyFunction,
                out bool isPotentialRegistryFunction);

            if (closingParenIndex < 0)
            {
                propertyValue = null;
                return false;
            }

            int length = closingParenIndex - startIndex;

            if (length == 0)
            {
                // Compat: $() should return string.Empty
                propertyValue = string.Empty;
                return true;
            }

            propertyValue = isPotentialPropertyFunction || isPotentialRegistryFunction
                ? ExpandProperty(text, startIndex, closingParenIndex - 1, isPotentialRegistryFunction, isPotentialPropertyFunction)
                : LookupProperty(text, startIndex, closingParenIndex - 1);

            if (propertyValue != null && _isTruncationEnabled)
            {
                string value = propertyValue.ToString();
                if (value.Length > CharacterLimitPerExpansion)
                {
                    propertyValue = TruncateString(value);
                }
            }

            return true;
        }

        /// <summary>
        ///  Finds the closing parenthesis that matches the opening parenthesis immediately
        ///  preceding <paramref name="index"/>.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="index">The index at which to begin scanning.</param>
        /// <param name="isPotentialPropertyFunction">
        ///  Whether the property body might contain a property function.
        /// </param>
        /// <param name="isPotentialRegistryFunction">
        ///  Whether the property body might contain a registry function.
        /// </param>
        /// <returns>
        ///  The index of the matching closing parenthesis, or <c>-1</c> if it was not found.
        /// </returns>
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
            // startIndex and endIndex inclusively delimit the property body.
            int length = endIndex - startIndex + 1;

            // Compat hack: as a special case, $(HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory) should return string.Empty
            // Note that very few properties have this exact length, so this check should be fast.
            if (length == VstsDbDirectoryProperty.Length &&
                string.Compare(expression, startIndex, VstsDbDirectoryProperty, 0, VstsDbDirectoryProperty.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return string.Empty;
            }

            if (tryExtractRegistryFunction &&
                length >= RegistryPrefix.Length &&
                string.Compare(expression, startIndex, RegistryPrefix, 0, RegistryPrefix.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                // If the property body starts with any of our special objects, then deal with them
                // This is a registry reference, like $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)
                // Note: ExpandRegistryValue returns an empty string if not on Windows.
                return ExpandRegistryValue(expression, startIndex, length);
            }

            if (tryExtractPropertyFunction)
            {
                // This is likely to be a function expression
                return ExpandPropertyBody(
                    expression.Substring(startIndex, length),
                    propertyValue: null);
            }

            // This is a regular property
            return LookupProperty(expression, startIndex, endIndex);
        }

        /// <summary>
        /// Expand the body of the property, including any functions that it may contain.
        /// </summary>
        internal static object ExpandPropertyBody(
            string propertyBody,
            object propertyValue,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            return expander.ExpandPropertyBody(propertyBody, propertyValue);
        }

        private object ExpandPropertyBody(string propertyBody, object propertyValue)
        {
            Function function = null;
            string propertyName = propertyBody;

            // Trim the body for compatibility reasons:
            // Spaces are not valid property name chars, but $( Foo ) is allowed, and should always expand to BLANK.
            // Do a very fast check for leading and trailing whitespace, and trim them from the property body if we have any.
            // But we will do a property name lookup on the propertyName that we held onto.
            if (char.IsWhiteSpace(propertyBody[0]) || char.IsWhiteSpace(propertyBody[^1]))
            {
                propertyBody = propertyBody.Trim();
            }

            // If we don't have a clean propertybody then we'll do deeper checks to see
            // if what we have is a function
            if (!IsValidPropertyName(propertyBody))
            {
                if (propertyBody.Contains('.') || propertyBody[0] == '[')
                {
                    if (BuildParameters.DebugExpansion)
                    {
                        Console.WriteLine("Expanding: {0}", propertyBody);
                    }

                    // This is a function
                    function = Function.ExtractPropertyFunction(
                        propertyBody,
                        _elementLocation,
                        propertyValue,
                        _propertiesUseTracker,
                        _fileSystem,
                        _propertiesUseTracker.LoggingContext);

                    // We may not have been able to parse out a function
                    if (function != null)
                    {
                        // We will have either extracted the actual property name
                        // or realized that there is none (static function), and have recorded a null
                        propertyName = function.Receiver;
                    }
                    else
                    {
                        // In the event that we have been handed an unrecognized property body, throw
                        // an invalid function property exception.
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                        return null;
                    }
                }
                else if (propertyValue == null && propertyBody.Contains('[')) // a single property indexer
                {
                    int indexerStart = propertyBody.IndexOf('[');
                    int indexerEnd = propertyBody.IndexOf(']');

                    if (indexerStart < 0 || indexerEnd < 0)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidFunctionPropertyExpression", propertyBody, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets"));
                    }
                    else
                    {
                        propertyValue = LookupProperty(propertyBody, 0, indexerStart - 1);
                        propertyBody = propertyBody.Substring(indexerStart);

                        // recurse so that the function representing the indexer can be executed on the property value
                        return ExpandPropertyBody(propertyBody, propertyValue);
                    }
                }
                else
                {
                    // In the event that we have been handed an unrecognized property body, throw
                    // an invalid function property exception.
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidFunctionPropertyExpression", propertyBody, string.Empty);
                    return null;
                }
            }

            // Find the property value in our property collection.  This
            // will automatically return "" (empty string) if the property
            // doesn't exist in the collection, and we're not executing a static function
            if (!string.IsNullOrEmpty(propertyName))
            {
                propertyValue = LookupProperty(propertyName);
            }

            if (function != null)
            {
                try
                {
                    // Because of the rich expansion capabilities of MSBuild, we need to keep things
                    // as strings, since property expansion & string embedding can happen anywhere
                    // propertyValue can be null here, when we're invoking a static function
                    propertyValue = function.Execute(propertyValue, _properties, _options, _elementLocation);
                }
                catch (Exception) when (_options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                {
                    propertyValue = propertyBody;
                }
            }

            return propertyValue;
        }

        /// <summary>
        ///  Looks up a simple property reference by its complete name.
        /// </summary>
        /// <param name="propertyName">The property name to look up.</param>
        /// <returns>
        ///  The resolved property value, or <see cref="string.Empty"/> when the property is undefined.
        /// </returns>
        private string LookupProperty(string propertyName)
            => LookupProperty(propertyName, 0, propertyName.Length - 1);

        /// <summary>
        ///  Looks up a simple property reference within a region of a string.
        /// </summary>
        /// <param name="propertyName">The string containing the property name.</param>
        /// <param name="startIndex">The inclusive index at which the property name begins.</param>
        /// <param name="endIndex">The inclusive index at which the property name ends.</param>
        /// <returns>
        ///  The resolved property value, or <see cref="string.Empty"/> when the property is undefined.
        /// </returns>
        private string LookupProperty(string propertyName, int startIndex, int endIndex)
        {
            P property = _properties.GetProperty(propertyName, startIndex, endIndex);

            _propertiesUseTracker.TrackRead(propertyName, startIndex, endIndex, _elementLocation, isUninitialized: property is null);

            if (property is null)
            {
                // It could be one of the MSBuildThisFileXXXX properties, whose values vary according to the file they are in.
                return TryExpandMSBuildThisFileProperty(propertyName, startIndex, endIndex - startIndex + 1, out string thisFilePropertyValue)
                    ? thisFilePropertyValue
                    : string.Empty;
            }

            if (property is ProjectPropertyInstance.EnvironmentDerivedProjectPropertyInstance environmentDerivedProperty)
            {
                environmentDerivedProperty.loggingContext = _propertiesUseTracker.LoggingContext;
            }

            return property.GetEvaluatedValueEscaped(_elementLocation);
        }

        /// <summary>
        ///  Attempts to expand an <c>MSBuildThisFile*</c> property for the current element location.
        /// </summary>
        /// <param name="propertyName">The string containing the property name.</param>
        /// <param name="offset">The zero-based offset at which the property name begins.</param>
        /// <param name="length">The number of characters in the property name.</param>
        /// <param name="result">
        ///  When this method returns <see langword="true"/>, the expanded property value; otherwise,
        ///  <see langword="null"/>.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> when the name identifies an <c>MSBuildThisFile*</c> property and the current
        ///  element location has a file path; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryExpandMSBuildThisFileProperty(string propertyName, int offset, int length, out string result)
        {
            string filePath = _elementLocation.File;

            if (filePath.IsNullOrEmpty() ||
                !ReservedPropertyNames.TryGetThisFileProperty(propertyName, offset, length, out ReservedPropertyKind kind))
            {
                result = null;
                return false;
            }

            switch (kind)
            {
                case ReservedPropertyKind.ThisFile:
                    result = Path.GetFileName(filePath);
                    return true;

                case ReservedPropertyKind.ThisFileName:
                    result = Path.GetFileNameWithoutExtension(filePath);
                    return true;

                case ReservedPropertyKind.ThisFileFullPath:
                    result = FileUtilities.NormalizePath(filePath);
                    return true;

                case ReservedPropertyKind.ThisFileExtension:
                    result = Path.GetExtension(filePath);
                    return true;

                case ReservedPropertyKind.ThisFileDirectory:
                    result = FileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(filePath));
                    return true;

                case ReservedPropertyKind.ThisFileDirectoryNoRoot:
                    string directory = Path.GetDirectoryName(filePath);
                    int rootLength = Path.GetPathRoot(directory).Length;
                    result = FileUtilities.EnsureTrailingNoLeadingSlash(directory, rootLength);
                    return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Given a string like "Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation", return the value at that location
        /// in the registry. If the value isn't found, returns String.Empty.
        /// Properties may refer to a registry location by using the syntax for example
        /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
        /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
        /// the default value is desired.
        /// </summary>
        /// <param name="text">The string containing the registry expression.</param>
        /// <param name="startIndex">The index at which the registry expression begins.</param>
        /// <param name="length">The length of the registry expression.</param>
        private string ExpandRegistryValue(string text, int startIndex, int length)
        {
#if !NETFRAMEWORK
            // Non-.NET Framework MSBuild returns empty on non-Windows, where no registry is available.
            if (!NativeMethodsShared.IsWindows)
            {
                return string.Empty;
            }
#endif

            // Split off the value name -- the part after the "@" sign.
            // If there's no "@" sign, then it's the default value name we want.
            int locationStartIndex = startIndex + RegistryPrefix.Length;
            int locationEndIndex = startIndex + length;
            int locationLength = locationEndIndex - locationStartIndex;

            int atSignIndex = text.IndexOf('@', locationStartIndex, locationLength);

            if (atSignIndex >= 0 &&
                text.IndexOf('@', startIndex: atSignIndex + 1, count: locationEndIndex - atSignIndex - 1) >= 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    _elementLocation,
                    "InvalidRegistryPropertyExpression",
                    $"$({text.Substring(startIndex, length)})",
                    string.Empty);
            }

            string keyName, valueName;

            // We rely on the '@' character to delimit the key and its value, but the registry
            // allows this character to be used in the names of keys and the names of values.
            // Hence we use our standard escaping mechanism to allow users to access such keys
            // and values.
            if (atSignIndex >= 0)
            {
                keyName = EscapingUtilities.UnescapeAll(text, locationStartIndex, atSignIndex - locationStartIndex);

                valueName = atSignIndex < locationEndIndex - 1
                    ? EscapingUtilities.UnescapeAll(text, atSignIndex + 1, locationEndIndex - atSignIndex - 1)
                    : null;
            }
            else
            {
                keyName = EscapingUtilities.UnescapeAll(text, locationStartIndex, locationLength);
                valueName = null;
            }

            try
            {
                // Unless we are running under Windows, don't bother with anything but the user keys
                if (!NativeMethodsShared.IsWindows && !keyName.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                {
                    // Fake common request to HKLM that we can resolve
                    return keyName.StartsWith(@"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(valueName, "InstallRoot", StringComparison.OrdinalIgnoreCase)
                        ? NativeMethodsShared.FrameworkBasePath + Path.DirectorySeparatorChar
                        : string.Empty;
                }

                object value = Registry.GetValue(keyName, valueName, defaultValue: null);

                // Convert the result to a string that is reasonable for MSBuild
                return PropertyValueConverter.ToString(value);
            }
            catch (Exception ex) when (!ExceptionHandling.NotExpectedRegistryException(ex))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    _elementLocation,
                    "InvalidRegistryPropertyExpression",
                    $"$({text.Substring(startIndex, length)})",
                    ex.Message);

                return string.Empty;
            }
        }
    }
}
