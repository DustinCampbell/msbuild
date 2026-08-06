// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Text;
using Microsoft.Win32;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

#if FEATURE_MSIOREDIST
// File is intentionally NOT aliased — all typeof() comparisons use fully-qualified
// System.IO.File to match the types registered in AvailableStaticMethods.
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Expands property expressions, like $(Configuration) and
    ///  $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation).
    /// </summary>
    /// <remarks>
    ///  This is a private nested ref struct, exposed only through its static entry points.
    /// </remarks>
    private readonly ref struct PropertyExpander
    {
        private readonly IPropertyProvider<P> _properties;
        private readonly ExpanderOptions _options;
        private readonly IElementLocation _elementLocation;
        private readonly PropertiesUseTracker _propertiesUseTracker;
        private readonly IFileSystem _fileSystem;
        private readonly bool _truncationEnabled;

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
            _truncationEnabled = IsTruncationEnabled(options);
        }

        /// <summary>
        ///  Expands all property references in <paramref name="expression"/> and returns the escaped string result.
        /// </summary>
        internal static string ExpandPropertiesLeaveEscaped(
            string expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
            => ConvertToString(
                ExpandPropertiesLeaveTypedAndEscaped(
                    expression,
                    properties,
                    options,
                    elementLocation,
                    propertiesUseTracker,
                    fileSystem));

        /// <summary>
        ///  Expands all property references in <paramref name="expression"/> while preserving a typed result when
        ///  the expression consists of a single property reference.
        /// </summary>
        internal static object? ExpandPropertiesLeaveTypedAndEscaped(
            string expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            if ((options & ExpanderOptions.ExpandProperties) == 0 || expression.Length == 0)
            {
                return expression;
            }

            Assumed.NotNull(properties, "Cannot expand properties without providing properties");

            // Keep the overwhelmingly common no-property path on string. A segment is useful only after a property
            // reference has actually been found.
            int propertyStartIndex = expression.IndexOf("$(", StringComparison.Ordinal);
            if (propertyStartIndex == -1)
            {
                return expression;
            }

            StringSegment segment = expression;

            if (!TryExtractPropertyReference(segment, propertyStartIndex, out PropertyReference firstProperty))
            {
                return expression;
            }

            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            return expander.ExpandExpression(segment, firstProperty);
        }

        /// <summary>
        ///  Expands an argument containing nested property references without realizing literal arguments.
        /// </summary>
        internal static bool TryExpandPropertyFunctionArgument(
            StringSegment expression,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem,
            out object? result)
        {
            if ((options & ExpanderOptions.ExpandProperties) == 0 || expression.IsNullOrEmpty)
            {
                result = null;
                return false;
            }

            int propertyStart = expression.IndexOf("$(", StringComparison.Ordinal);
            if (propertyStart < 0)
            {
                result = null;
                return false;
            }

            Assumed.NotNull(properties, "Cannot expand properties without providing properties");

            if (!TryExtractPropertyReference(expression, propertyStart, out PropertyReference firstProperty))
            {
                result = null;
                return false;
            }

            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            result = expander.ExpandExpression(expression, firstProperty);
            return true;
        }

        /// <summary>
        ///  Scans an expression containing at least one property reference and expands each reference in order.
        /// </summary>
        private object? ExpandExpression(StringSegment expression, PropertyReference property)
        {
            if (property.CoversEntireExpression)
            {
                return TryExpand(property, out object? result)
                    ? result is string stringValue && !NativeMethodsShared.IsWindows
                        ? FileUtilities.MaybeAdjustFilePath(stringValue)
                        : result
                    : string.Empty;
            }

            using StringSegmentBuilder builder = default;
            int sourceIndex = 0;

            while (true)
            {
                if (property.Start > sourceIndex)
                {
                    builder.Append(expression[sourceIndex..property.Start]);
                }

                if (TryExpand(property, out object? propertyValue))
                {
                    builder.Append(propertyValue.ToString());
                }

                sourceIndex = property.End + 1;
                int propertyStart = expression.IndexOf("$(", sourceIndex, StringComparison.Ordinal);
                if (propertyStart < 0)
                {
                    break;
                }

                if (!TryExtractPropertyReference(expression, propertyStart, out property))
                {
                    if (propertyStart > sourceIndex)
                    {
                        builder.Append(expression[sourceIndex..propertyStart]);
                    }

                    builder.Append(expression[propertyStart..]);
                    sourceIndex = expression.Length;
                    break;
                }
            }

            if (sourceIndex < expression.Length)
            {
                builder.Append(expression[sourceIndex..]);
            }

            return builder.GetResult();
        }

        /// <summary>
        ///  Expands one syntactically complete property reference.
        /// </summary>
        private bool TryExpand(PropertyReference property, [NotNullWhen(true)] out object? result)
        {
            StringSegment propertyBody = property.Body;

            // Compat: $() should return String.Empty. The 77-character legacy property handled below must bypass
            // the common lookup path.
            if (!property.MayContainPropertyFunction
                && !property.MayContainRegistryExpression
                && !propertyBody.IsEmpty
                && propertyBody.Length != 77)
            {
                result = LookupProperty(propertyBody);
            }
            else if (propertyBody.IsEmpty)
            {
                result = string.Empty;
            }
            else if (property.MayContainRegistryExpression
                && propertyBody.StartsWith("Registry:", StringComparison.OrdinalIgnoreCase))
            {
                result = ExpandRegistryValue(propertyBody);
            }
            else if (propertyBody.Equals(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\9.0\VSTSDB@VSTSDBDirectory",
                StringComparison.OrdinalIgnoreCase))
            {
                // Compat: this legacy registry-shaped property always expands to empty.
                result = string.Empty;
            }
            else if (property.CoversEntireExpression
                && propertyBody.Equals("Solutions.VSVersion", StringComparison.Ordinal))
            {
                // Compat: WebProjects historically treated this expression as an undefined property.
                result = string.Empty;
            }
            else if (property.MayContainPropertyFunction)
            {
                result = ExpandPropertyBody(propertyBody, propertyValue: null);
            }
            else
            {
                result = LookupProperty(propertyBody);
            }

            if (_truncationEnabled && result is not null)
            {
                string value = result.ToString()!;
                if (value.Length > CharacterLimitPerExpansion)
                {
                    result = TruncateString(value);
                }
            }

            return result is not null;
        }

        private static bool TryExtractPropertyReference(
            StringSegment expression,
            int propertyStart,
            out PropertyReference property)
        {
            int bodyStart = propertyStart + 2;
            int propertyEnd = ScanForClosingParenthesis(
                expression[bodyStart..],
                out bool mayContainPropertyFunction,
                out bool mayContainRegistryExpression);

            if (propertyEnd < 0)
            {
                property = default;
                return false;
            }

            propertyEnd += bodyStart;
            property = new(
                propertyStart,
                propertyEnd,
                expression[bodyStart..propertyEnd],
                mayContainPropertyFunction,
                mayContainRegistryExpression,
                propertyStart == 0 && propertyEnd == expression.Length - 1);
            return true;
        }

        private readonly struct PropertyReference(
            int start,
            int end,
            StringSegment body,
            bool mayContainPropertyFunction,
            bool mayContainRegistryExpression,
            bool isEntireExpression)
        {
            internal int Start { get; } = start;
            internal int End { get; } = end;
            internal StringSegment Body { get; } = body;
            internal bool MayContainPropertyFunction { get; } = mayContainPropertyFunction;
            internal bool MayContainRegistryExpression { get; } = mayContainRegistryExpression;
            internal bool CoversEntireExpression { get; } = isEntireExpression;
        }

        /// <summary>
        ///  Expands the body of the property, including any functions that it may contain.
        /// </summary>
        internal static object? ExpandPropertyBody(
            StringSegment propertyBody,
            object? propertyValue,
            IPropertyProvider<P> properties,
            ExpanderOptions options,
            IElementLocation elementLocation,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem)
        {
            PropertyExpander expander = new(properties, options, elementLocation, propertiesUseTracker, fileSystem);
            return expander.ExpandPropertyBody(propertyBody, propertyValue);
        }

        private object? ExpandPropertyBody(
            StringSegment propertyBody,
            object? propertyValue)
        {
            Function? function = null;
            StringSegment propertyName = propertyBody;

            // Spaces are not valid property name characters, but $( Foo ) is allowed and expands to blank.
            // Preserve the original propertyName for the subsequent lookup.
            propertyBody = propertyBody.Trim();

            // If we don't have a clean propertybody then we'll do deeper checks to see
            // if what we have is a function
            if (!IsValidPropertyName(propertyBody))
            {
                if (propertyBody.Contains('.') || propertyBody.StartsWith('['))
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
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidFunctionPropertyExpression", propertyBody, string.Empty);
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
                        propertyValue = LookupProperty(propertyBody[..indexerStart]);
                        propertyBody = propertyBody[indexerStart..];

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
            if (!propertyName.IsNullOrEmpty)
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
                    propertyValue = propertyBody.Value;
                }
            }

            return propertyValue;
        }

        /// <summary>
        /// Convert the object into an MSBuild friendly string
        /// Arrays are supported.
        /// Will not return NULL.
        /// </summary>
        internal static string ConvertToString(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string stringValue => stringValue,
                IDictionary dictionary => ConvertDictionary(dictionary),
                IEnumerable enumerable => ConvertEnumerable(enumerable),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            };

            static string ConvertDictionary(IDictionary dictionary)
            {
                using RefArrayBuilder<StringSegment> builder = default;

                // If the return type is an IDictionary, then we convert this to
                // a semi-colon delimited set of A=B pairs.
                // Key and Value are converted to string and escaped
                bool first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!first)
                    {
                        builder.Add(";");
                    }

                    // convert and escape each key and value in the dictionary entry
                    builder.Add(EscapingUtilities.Escape(ConvertToString(entry.Key)));
                    builder.Add("=");
                    builder.Add(EscapingUtilities.Escape(ConvertToString(entry.Value)));
                    first = false;
                }

                return StringSegment.Join(string.Empty, builder.AsSpan());
            }

            static string ConvertEnumerable(IEnumerable enumerable)
            {
                using RefArrayBuilder<StringSegment> builder = default;

                // If the return is enumerable, then we'll convert to semi-colon delimited elements
                // each of which must be converted, so we'll recurse for each element
                bool first = true;

                foreach (object element in enumerable)
                {
                    if (!first)
                    {
                        builder.Add(";");
                    }

                    // we need to convert and escape each element of the array
                    builder.Add(EscapingUtilities.Escape(ConvertToString(element)));
                    first = false;
                }

                return StringSegment.Join(string.Empty, builder.AsSpan());
            }
        }

        /// <summary>
        /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo).
        /// </summary>
        private string LookupProperty(StringSegment propertyName)
        {
            P? property = _properties.GetProperty(propertyName);

            bool mayBeReservedProperty = property == null &&
                propertyName.Length > 7 &&
                propertyName.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase);

            if (mayBeReservedProperty)
            {
                // It could be one of the MSBuildThisFileXXXX properties,
                // whose values vary according to the file they are in.
                return ExpandMSBuildThisFileProperty(propertyName);
            }

            _propertiesUseTracker.TrackRead(propertyName, _elementLocation, property == null);

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
        private string ExpandMSBuildThisFileProperty(StringSegment propertyName)
        {
            if (!ReservedPropertyNames.IsReservedProperty(propertyName))
            {
                return string.Empty;
            }

            if (_elementLocation.File.Length == 0)
            {
                return string.Empty;
            }

            // Because StringSegment.Equals checks the length first, and these strings are almost
            // all different lengths, this sequence is efficient.
            if (propertyName.Equals(ReservedPropertyNames.thisFile, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(_elementLocation.File);
            }

            if (propertyName.Equals(ReservedPropertyNames.thisFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileNameWithoutExtension(_elementLocation.File);
            }

            if (propertyName.Equals(ReservedPropertyNames.thisFileFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return FileUtilities.NormalizePath(_elementLocation.File);
            }

            if (propertyName.Equals(ReservedPropertyNames.thisFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetExtension(_elementLocation.File);
            }

            if (propertyName.Equals(ReservedPropertyNames.thisFileDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return FileUtilities.EnsureTrailingSlash(Path.GetDirectoryName(_elementLocation.File)!);
            }

            if (propertyName.Equals(ReservedPropertyNames.thisFileDirectoryNoRoot, StringComparison.OrdinalIgnoreCase))
            {
                string directory = Path.GetDirectoryName(_elementLocation.File)!;
                int rootLength = Path.GetPathRoot(directory)!.Length;
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
        private string ExpandRegistryValue(StringSegment registryExpression)
        {
#if NET
            // .NET Core MSBuild used to always return empty, so match that behavior
            // on non-Windows (no registry).
            if (!NativeMethodsShared.IsWindows)
            {
                return string.Empty;
            }
#endif

            // Remove "Registry:" prefix
            StringSegment registryLocation = registryExpression[9..];

            // Split off the value name -- the part after the "@" sign. If there's no "@" sign,
            // then it's the default value name we want.
            int atSignIndex = registryLocation.IndexOf('@');

            if (registryLocation.IndexOf('@', atSignIndex + 1) >= 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidRegistryPropertyExpression", $"$({registryExpression})", string.Empty);
            }

            StringSegment registryKeyName;
            StringSegment valueName;

            if (atSignIndex >= 0)
            {
                // If there's no '@', or '@' is first, then we'll use null or String.Empty for the location; otherwise
                // the location is the part before the '@'
                registryKeyName = registryLocation[..atSignIndex];

                valueName = atSignIndex < registryLocation.Length - 1
                    ? registryLocation[(atSignIndex + 1)..]
                    : default;
            }
            else
            {
                registryKeyName = registryLocation;
                valueName = default;
            }

            // We rely on the '@' character to delimit the key and its value, but the registry
            // allows this character to be used in the names of keys and the names of values.
            // Hence we use our standard escaping mechanism to allow users to access such keys
            // and values.
            if (registryKeyName.Length > 0)
            {
                registryKeyName = EscapingUtilities.UnescapeAll(registryKeyName);
            }

            if (!valueName.IsNullOrEmpty)
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

                object? valueFromRegistry = Registry.GetValue(registryKeyName.Value!, valueName.Value, defaultValue: null);

                // This means either the key or value was not found in the registry. In this case,
                // return String.Empty to imitate the behavior of normal properties.
                return valueFromRegistry is not null
                    ? ConvertToString(valueFromRegistry)
                    : string.Empty;
            }
            catch (Exception ex) when (!ExceptionHandling.NotExpectedRegistryException(ex))
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "InvalidRegistryPropertyExpression", $"$({registryExpression})", ex.Message);
                return string.Empty;
            }
        }
    }
}
