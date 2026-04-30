// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {GAC}
    /// </summary>
    internal class GacResolver : Resolver
    {
        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="targetProcessorArchitecture">Like x86 or IA64\AMD64, the processor architecture being targeted.</param>
        /// <param name="searchPathElement">The search path element.</param>
        /// <param name="services">The services instance providing file system and assembly operations.</param>
        /// <param name="targetedRuntimeVesion">The targeted runtime version.</param>
        public GacResolver(System.Reflection.ProcessorArchitecture targetProcessorArchitecture, string searchPathElement, RARFileSystemServices services, Version targetedRuntimeVesion)
            : base(searchPathElement, services, targetedRuntimeVesion, targetProcessorArchitecture, true)
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

            if (assemblyName != null)
            {
                // {GAC} was passed in.
                string gacResolved = services.GetAssemblyPathInGac(assemblyName, targetProcessorArchitecture,
                    targetedRuntimeVersion, fullFusionName: false, specificVersion: wantSpecificVersion);

                if (!string.IsNullOrEmpty(gacResolved) && services.FileExists(gacResolved))
                {
                    foundPath = gacResolved;
                    return true;
                }
                else
                {
                    // Record this as a location that was considered.
                    if (assembliesConsideredAndRejected != null)
                    {
                        var considered = new ResolutionSearchLocation
                        {
                            FileNameAttempted = assemblyName.FullName,
                            SearchPath = searchPathElement,
                            AssemblyName = assemblyName,
                            Reason = NoMatchReason.NotInGac
                        };
                        assembliesConsideredAndRejected.Add(considered);
                    }
                }
            }

            return false;
        }
    }
}
