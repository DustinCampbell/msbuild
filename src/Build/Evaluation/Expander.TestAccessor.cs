// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(Expander<P, I> instance)
    {
        /// <summary>
        /// Used only for unit tests. Expands the property expression (including any metadata expressions) and returns
        /// the result typed (i.e. not converted into a string if the result is a function return).
        /// </summary>
        internal object ExpandPropertiesLeaveTypedAndEscaped(
            string expression,
            ExpanderOptions options,
            IElementLocation elementLocation)
        {
            if (expression.Length == 0)
            {
                return string.Empty;
            }

            ErrorUtilities.VerifyThrowInternalNull(elementLocation);

            string metaExpanded = MetadataExpander.ExpandMetadataLeaveEscaped(
                expression,
                instance._metadata,
                options,
                elementLocation);

            return PropertyExpander.ExpandPropertiesLeaveTypedAndEscaped(
                metaExpanded,
                instance._properties,
                options,
                elementLocation,
                instance._propertiesUseTracker,
                instance._fileSystem);
        }
    }
}
