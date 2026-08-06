// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Information about property being accessed - whether during evaluation or build.
/// </summary>
internal sealed class PropertyReadData(
    string projectFilePath,
    int? projectConfigurationId,
    StringSegment propertyName,
    IMSBuildElementLocation elementLocation,
    bool isUninitialized,
    PropertyReadContext propertyReadContext)
    : CheckData(projectFilePath, projectConfigurationId)
{
    public PropertyReadData(
        string projectFilePath,
        int? projectConfigurationId,
        PropertyReadInfo propertyReadInfo)
        : this(
            projectFilePath,
            projectConfigurationId,
            propertyReadInfo.PropertyName,
            propertyReadInfo.ElementLocation,
            propertyReadInfo.IsUninitialized,
            propertyReadInfo.PropertyReadContext)
    {
    }

    /// <summary>
    /// Name of the property that was accessed.
    /// </summary>
    public string PropertyName => field ??= propertyName.ValueOrEmpty;

    /// <summary>
    /// Location of the property access.
    /// </summary>
    public IMSBuildElementLocation ElementLocation { get; } = elementLocation;

    /// <summary>
    /// Indicates whether the property was accessed before being initialized.
    /// </summary>
    public bool IsUninitialized { get; } = isUninitialized;

    /// <summary>
    /// Gets the context type in which the property was accessed.
    /// </summary>
    public PropertyReadContext PropertyReadContext { get; } = propertyReadContext;
}
