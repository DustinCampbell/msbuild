// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This class provides access to the Framework assembly's resources.
    /// </summary>
    /// <remarks>
    /// Named FrameworkResources (not AssemblyResources) to avoid conflicts with
    /// Microsoft.Build.Shared.AssemblyResources which is visible via InternalsVisibleTo.
    /// </remarks>
    internal static class FrameworkResources
    {
        internal static ResourceManager PrimaryResources
            => field ??= CreateResourceManager("Microsoft.Build.Framework.Strings");

        internal static ResourceManager SharedResources
            => field ??= CreateResourceManager("Microsoft.Build.Framework.Strings.shared");

        private static ResourceManager CreateResourceManager(string name)
            => new(name, typeof(FrameworkResources).GetTypeInfo().Assembly);

        /// <summary>
        /// Loads the specified resource string.
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="name">The name of the string resource to load.</param>
        /// <returns>The resource string.</returns>
        internal static string GetString(string name)
        {
            // NOTE: the ResourceManager.GetString() method is thread-safe
            string? resource = PrimaryResources.GetString(name, CultureInfo.CurrentUICulture) ??
                               SharedResources.GetString(name, CultureInfo.CurrentUICulture);

            FrameworkErrorUtilities.VerifyThrow(resource != null, $"Missing resource '{name}'");

            return resource;
        }
    }
}
