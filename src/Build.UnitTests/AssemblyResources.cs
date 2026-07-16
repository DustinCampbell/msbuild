// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Test-only stand-in for the AssemblyResources class that used to be compiled into
    /// Microsoft.Build. It delegates to the Microsoft.Build resource catalog (SR), which is
    /// visible to this test assembly via InternalsVisibleTo, so tests observe the exact same
    /// localized strings the product assembly produces.
    /// </summary>
    internal static class AssemblyResources
    {
        /// <summary>
        /// Loads the specified resource string from Microsoft.Build's resources.
        /// </summary>
        /// <returns>The resource string, or null if not found.</returns>
        internal static string GetString(string name) => SR.GetStringOrNull(name);
    }
}
