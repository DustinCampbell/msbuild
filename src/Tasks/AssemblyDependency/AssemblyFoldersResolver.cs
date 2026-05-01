// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {AssemblyFolders}
    /// </summary>
    internal class AssemblyFoldersResolver : Resolver
    {
        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="searchPathElement">The corresponding element from the search path.</param>
        /// <param name="services">The services instance providing file system and assembly operations.</param>
        /// <param name="targetedRuntimeVesion">The targeted runtime version.</param>
        public AssemblyFoldersResolver(string searchPathElement, RARServices services, Version targetedRuntimeVesion)
            : base(searchPathElement, services, targetedRuntimeVesion, System.Reflection.ProcessorArchitecture.None, false)
        {
        }

        /// <inheritdoc/>
        public override bool Resolve(
            AssemblyNameExtension assemblyName,
            string sdkName,
            string rawFileNameCandidate,
            bool isPrimaryProjectReference,
            bool isImmutableFrameworkReference,
            bool wantSpecificVersion,
            string[] executableExtensions,
            string hintPath,
            string assemblyFolderKey,
            List<ResolutionSearchLocation> assembliesConsideredAndRejected,

            out string foundPath,
            out bool userRequestedSpecificFile)
        {
            foundPath = null;
            userRequestedSpecificFile = false;

#if FEATURE_WIN32_REGISTRY
            if (assemblyName != null)
            {
                // {AssemblyFolders} was passed in.
                foreach (string assemblyFolder in AssemblyFolder.GetAssemblyFolders(assemblyFolderKey))
                {
                    string resolvedPath = ResolveFromDirectory(assemblyName, isPrimaryProjectReference, wantSpecificVersion, executableExtensions, assemblyFolder, assembliesConsideredAndRejected);
                    if (resolvedPath != null)
                    {
                        foundPath = resolvedPath;
                        return true;
                    }
                }
            }
#endif
            return false;
        }
    }
}
