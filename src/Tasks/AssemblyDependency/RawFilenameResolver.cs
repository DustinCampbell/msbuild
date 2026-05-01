// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {RawFileName}
    /// </summary>
    internal class RawFilenameResolver : Resolver
    {
        /// <summary>
        /// Construct.
        /// </summary>
        public RawFilenameResolver(string searchPathElement, RARServices services, Version targetedRuntimeVesion)
            : base(searchPathElement, services, targetedRuntimeVesion, ProcessorArchitecture.None, false)
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

            if (rawFileNameCandidate != null)
            {
                // {RawFileName} was passed in.
                if (isImmutableFrameworkReference || services.FileExists(rawFileNameCandidate))
                {
                    userRequestedSpecificFile = true;
                    foundPath = rawFileNameCandidate;
                    return true;
                }

                if (assembliesConsideredAndRejected != null)
                {
                    var considered = new ResolutionSearchLocation
                    {
                        FileNameAttempted = rawFileNameCandidate,
                        SearchPath = searchPathElement,
                        Reason = NoMatchReason.NotAFileNameOnDisk
                    };
                    assembliesConsideredAndRejected.Add(considered);
                }
            }

            return false;
        }
    }
}
