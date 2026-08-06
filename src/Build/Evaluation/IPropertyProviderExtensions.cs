// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

internal static class IPropertyProviderExtensions
{
    public static T? GetProperty<T>(this IPropertyProvider<T> properties, StringSegment propertyName)
        where T : class
        => properties.GetProperty(propertyName.Buffer!, propertyName.Offset, propertyName.Offset + propertyName.Length - 1);
}
