// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Information about a managed assembly.
    /// </summary>
    /// <remarks>
    /// After initial construction, this object is readonly and data-only,
    /// allowing it to be safely cached.
    /// </remarks>
    internal class AssemblyMetadata
    {
        public readonly AssemblyNameExtension[] Dependencies;
        public readonly FrameworkName FrameworkName;
        public readonly string[] ScatterFiles;

        /// <summary>
        /// Creates an instance of <see cref="AssemblyMetadata"/> by reading the assembly at the specified path.
        /// </summary>
        /// <param name="path">The path to the assembly file.</param>
        public AssemblyMetadata(string path)
        {
            using (var import = new AssemblyInformation(path))
            {
                Dependencies = import.Dependencies;
                FrameworkName = import.FrameworkNameAttribute;
                ScatterFiles = NativeMethodsShared.IsWindows ? import.Files : null;
            }
        }

        /// <summary>
        /// Creates an instance of <see cref="AssemblyMetadata"/> with the specified values.
        /// </summary>
        /// <param name="dependencies">The assembly dependencies.</param>
        /// <param name="scatterFiles">The scatter files associated with the assembly.</param>
        /// <param name="frameworkName">The target framework of the assembly.</param>
        public AssemblyMetadata(
            AssemblyNameExtension[] dependencies,
            string[] scatterFiles,
            FrameworkName frameworkName)
        {
            Dependencies = dependencies;
            ScatterFiles = scatterFiles;
            FrameworkName = frameworkName;
        }
    }
}
