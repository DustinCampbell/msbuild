// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Globalization;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
#if !NET
using System.Linq;
#endif
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using Microsoft.Win32;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

#if FEATURE_MSIOREDIST
// File is intentionally NOT aliased — all typeof() comparisons use fully-qualified
// System.IO.File to match the types registered in AvailableStaticMethods.
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
        private const string LegacyRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory";

        private readonly IPropertyProvider<P> _properties;
        private readonly ExpanderOptions _options;
        private readonly IElementLocation _elementLocation;
        private readonly PropertiesUseTracker _propertiesUseTracker;
        private readonly IFileSystem _fileSystem;

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

            int propertyMarkerIndex = ExpressionShredder.IndexOfPropertyMarker(expression);
            if (propertyMarkerIndex == -1)
            {
                // Return the original string when it contains no property markers.
                return expression;
            }

            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            return expander.Expand(expression, propertyMarkerIndex);
        }

        private object Expand(string expression, int propertyMarkerIndex)
        {
            if (propertyMarkerIndex == 0 &&
                TryParsePropertyReference(expression, propertyMarkerIndex, out PropertyReference wholeExpressionProperty) &&
                wholeExpressionProperty.IsSimplePropertyName &&
                wholeExpressionProperty.EndIndexExclusive == expression.Length)
            {
                string propertyValue = LookupProperty(in wholeExpressionProperty);

                if (propertyValue != null && IsTruncationEnabled(_options))
                {
                    if (propertyValue.Length > CharacterLimitPerExpansion)
                    {
                        propertyValue = TruncateString(propertyValue);
                    }
                }

                return FileUtilities.MaybeAdjustFilePath(propertyValue);
            }

            // Preserve the type of a single expanded value, but concatenate multiple components as strings.
            using SpanBasedConcatenator results = new();

            // Index of the first character not yet copied or expanded.
            int index = 0;

            // Process each property marker in order.
            do
            {
                // Locate the property body while accounting for nested expressions and quoted text.
                if (!TryParsePropertyReference(expression, propertyMarkerIndex, out PropertyReference property))
                {
                    // Preserve an unterminated marker and the rest of the expression literally.
                    results.Add(expression, start: index, length: propertyMarkerIndex - index);
                    results.Add(expression, start: propertyMarkerIndex);
                    return results.GetResult();
                }

                // Append the literal text preceding the marker. Empty ranges are ignored.
                results.Add(expression, start: index, length: property.MarkerIndex - index);

                object propertyValue = ExpandProperty(property);

                if (propertyValue != null)
                {
                    if (IsTruncationEnabled(_options))
                    {
                        var value = propertyValue.ToString();
                        if (value.Length > CharacterLimitPerExpansion)
                        {
                            propertyValue = TruncateString(value);
                        }
                    }

                    // Add the expanded value as the next result component.
                    results.Add(propertyValue);
                }

                // Continue immediately after the property expression.
                index = property.EndIndexExclusive;
                propertyMarkerIndex = ExpressionShredder.IndexOfPropertyMarker(expression, index);
            }
            while (propertyMarkerIndex >= 0);

            // Append the literal suffix after the final property.
            results.Add(expression, start: index);

            return results.GetResult();
        }

        private object ExpandProperty(PropertyReference property)
        {
            // Compat: $() expands to an empty string.
            if (property.Length == 0)
            {
                return string.Empty;
            }

            if (property.IsSimplePropertyName)
            {
                return LookupProperty(property.Expression, property.StartIndex, property.EndIndex);
            }

            if (property.Length >= 9 &&
                property.PotentialRegistryFunction &&
                string.Compare(property.Expression, property.StartIndex, "Registry:", 0, 9, StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Expand a registry reference such as
                // $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation).
                // Registry expansion returns an empty string on non-Windows platforms.
                return ExpandRegistryValue(property.Expression.Substring(property.StartIndex, property.Length));
            }

            // Compat: this malformed legacy registry reference historically expanded to an empty string.
            if (property.Length == LegacyRegistryKey.Length &&
                string.Compare(property.Expression, property.StartIndex, LegacyRegistryKey, 0, LegacyRegistryKey.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return string.Empty;
            }

            // Compat: old WebProjects expect $(Solutions.VSVersion) to expand to an empty string rather
            // than be interpreted as a property function.
            if (property.Length == 19 && string.Equals(property.Expression, "$(Solutions.VSVersion)", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (property.PotentialPropertyFunction)
            {
                // Parse and evaluate the property function.
                return ExpandPropertyBody(
                    propertyBody: property.Expression.Substring(property.StartIndex, property.Length),
                    propertyValue: null);
            }

            // Look up a regular property directly from its segment of the original expression.
            return LookupProperty(property.Expression, property.StartIndex, property.EndIndex);
        }

        /// <summary>
        ///  Locates and classifies a property body.
        /// </summary>
        private static bool TryParsePropertyReference(string expression, int propertyMarkerIndex, out PropertyReference result)
        {
            int nestLevel = 1;
            int propertyStartIndex = propertyMarkerIndex + 2;
            int index = propertyStartIndex;
            int length = expression.Length;

            bool isSimplePropertyName = index < length &&
                XmlUtilities.IsValidInitialElementNameCharacter(expression[index]);
            bool potentialPropertyFunction = false;
            bool potentialRegistryFunction = false;

            while (index < length && nestLevel > 0)
            {
                char character = expression[index];

                if (isSimplePropertyName &&
                    index > propertyStartIndex &&
                    character != ')' &&
                    !XmlUtilities.IsValidSubsequentElementNameCharacter(character))
                {
                    isSimplePropertyName = false;
                }

                switch (character)
                {
                    case '\'' or '`' or '"':
                        index = expression.IndexOf(character, index + 1);
                        if (index < 0)
                        {
                            result = default;
                            return false;
                        }

                        break;

                    case '(':
                        nestLevel++;
                        break;

                    case ')':
                        nestLevel--;
                        break;

                    case '.':
                    case '[':
                    case '$':
                        potentialPropertyFunction = true;
                        break;

                    case ':':
                        potentialRegistryFunction = true;
                        break;
                }

                index++;
            }

            if (nestLevel != 0)
            {
                result = default;
                return false;
            }

            result = new PropertyReference(
                expression,
                propertyMarkerIndex,
                closingParenthesisIndex: index - 1,
                isSimplePropertyName,
                potentialPropertyFunction,
                potentialRegistryFunction);

            return true;
        }

        private readonly struct PropertyReference(
            string expression,
            int markerIndex,
            int closingParenthesisIndex,
            bool isSimplePropertyName,
            bool potentialPropertyFunction,
            bool potentialRegistryFunction)
        {
            public string Expression => expression;

            public int MarkerIndex => markerIndex;

            public int StartIndex => MarkerIndex + 2;

            public int EndIndex => closingParenthesisIndex - 1;

            public int Length => EndIndex - StartIndex + 1;

            public int ClosingParenthesisIndex => closingParenthesisIndex;

            public int EndIndexExclusive => ClosingParenthesisIndex + 1;

            public bool IsSimplePropertyName => isSimplePropertyName;

            public bool PotentialPropertyFunction => potentialPropertyFunction;

            public bool PotentialRegistryFunction => potentialRegistryFunction;
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

        private object ExpandPropertyBody(
            string propertyBody,
            object propertyValue)
        {
            Function function = null;
            string propertyName = propertyBody;

            // Trim the body for compatibility reasons:
            // Spaces are not valid property name chars, but $( Foo ) is allowed, and should always expand to BLANK.
            // Do a very fast check for leading and trailing whitespace, and trim them from the property body if we have any.
            // But we will do a property name lookup on the propertyName that we held onto.
            if (Char.IsWhiteSpace(propertyBody[0]) || Char.IsWhiteSpace(propertyBody[propertyBody.Length - 1]))
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
                        return ExpandPropertyBody(
                            propertyBody,
                            propertyValue);
                    }
                }
                else
                {
                    // In the event that we have been handed an unrecognized property body, throw
                    // an invalid function property exception.
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                    return null;
                }
            }

            // Find the property value in our property collection.  This
            // will automatically return "" (empty string) if the property
            // doesn't exist in the collection, and we're not executing a static function
            if (!String.IsNullOrEmpty(propertyName))
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
        /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
        /// </summary>
        private string LookupProperty(string propertyName)
            => LookupProperty(propertyName, 0, propertyName.Length - 1);

        private string LookupProperty(ref readonly PropertyReference property)
            => LookupProperty(property.Expression, property.StartIndex, property.EndIndex);

        /// <summary>
        /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
        /// </summary>
        private string LookupProperty(string propertyName, int startIndex, int endIndex)
        {
            P property = _properties.GetProperty(propertyName, startIndex, endIndex);

            bool isArtificial = property == null && ((endIndex - startIndex) >= 7) &&
                               MSBuildNameIgnoreCaseComparer.Default.Equals("MSBuild", propertyName, startIndex, 7);

            _propertiesUseTracker.TrackRead(propertyName, startIndex, endIndex, _elementLocation, property == null, isArtificial);

            if (isArtificial)
            {
                // It could be one of the MSBuildThisFileXXXX properties,
                // whose values vary according to the file they are in.
                return startIndex != 0 || endIndex != propertyName.Length
                    ? ExpandMSBuildThisFileProperty(propertyName.Substring(startIndex, endIndex - startIndex + 1))
                    : ExpandMSBuildThisFileProperty(propertyName);
            }

            if (property == null)
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
        /// If the property name provided is one of the special
        /// per file properties named "MSBuildThisFileXXXX" then returns the value of that property.
        /// If the location provided does not have a path (eg., if it comes from a file that has
        /// never been saved) then returns empty string.
        /// If the property name is not one of those properties, returns empty string.
        /// </summary>
        private string ExpandMSBuildThisFileProperty(string propertyName)
        {
            if (!ReservedPropertyNames.IsReservedProperty(propertyName))
            {
                return string.Empty;
            }

            if (_elementLocation.File.Length == 0)
            {
                return string.Empty;
            }

            // Because String.Equals checks the length first, and these strings are almost
            // all different lengths, this sequence is efficient.
            if (string.Equals(propertyName, ReservedPropertyNames.thisFile, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(_elementLocation.File);
            }

            if (string.Equals(propertyName, ReservedPropertyNames.thisFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileNameWithoutExtension(_elementLocation.File);
            }

            if (string.Equals(propertyName, ReservedPropertyNames.thisFileFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return FileUtilities.NormalizePath(_elementLocation.File);
            }

            if (string.Equals(propertyName, ReservedPropertyNames.thisFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetExtension(_elementLocation.File);
            }

            if (string.Equals(propertyName, ReservedPropertyNames.thisFileDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return FileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(_elementLocation.File));
            }

            if (string.Equals(propertyName, ReservedPropertyNames.thisFileDirectoryNoRoot, StringComparison.OrdinalIgnoreCase))
            {
                string directory = Path.GetDirectoryName(_elementLocation.File);
                int rootLength = Path.GetPathRoot(directory).Length;
                return FileUtilities.EnsureTrailingNoLeadingSlash(directory, rootLength);
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
            string registryLocation = registryExpression.Substring(9);

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
                        if (registryKeyName.StartsWith(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework",
                            StringComparison.OrdinalIgnoreCase) &&
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
