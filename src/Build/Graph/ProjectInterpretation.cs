// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Graph
{
    internal sealed class ProjectInterpretation
    {
        private const string FullPathMetadataName = "FullPath";
        private const string ToolsVersionMetadataName = "ToolsVersion";
        private const string SetConfigurationMetadataName = "SetConfiguration";
        private const string SetPlatformMetadataName = "SetPlatform";
        private const string SetTargetFrameworkMetadataName = "SetTargetFramework";
        private const string GlobalPropertiesToRemoveMetadataName = "GlobalPropertiesToRemove";
        private const string ProjectReferenceTargetIsOuterBuildMetadataName = "OuterBuild";
        private const string InnerBuildReferenceItemName = "_ProjectSelfReference";
        internal static string TransitiveReferenceItemName = "_TransitiveProjectReference";
        internal const string AddTransitiveProjectReferencesInStaticGraphPropertyName = "AddTransitiveProjectReferencesInStaticGraph";
        private const string PlatformLookupTableMetadataName = "PlatformLookupTable";
        private const string PlatformMetadataName = "Platform";
        private const string PlatformsMetadataName = "Platforms";
        private const string EnableDynamicPlatformResolutionPropertyName = "EnableDynamicPlatformResolution";
        private const string OverridePlatformNegotiationValue = "OverridePlatformNegotiationValue";
        private const string ShouldUnsetParentConfigurationAndPlatformPropertyName = "ShouldUnsetParentConfigurationAndPlatform";
        private const string ProjectMetadataName = "Project";
        private const string ConfigurationMetadataName = "Configuration";

        public static ProjectInterpretation Instance = new ProjectInterpretation();

        internal enum ProjectType
        {
            OuterBuild,
            InnerBuild,
            NonMultitargeting,
        }

        internal readonly record struct ReferenceInfo(ConfigurationMetadata ReferenceConfiguration, ProjectItemInstance ProjectReferenceItem);

        private readonly struct TargetSpecification
        {
            public TargetSpecification(string target, bool skipIfNonexistent)
            {
                // Verify that if this target is skippable then it equals neither
                // ".default" nor ".projectReferenceTargetsOrDefaultTargets".
                ErrorUtilities.VerifyThrow(
                    !skipIfNonexistent || (!target.Equals(MSBuildConstants.DefaultTargetsMarker)
                    && !target.Equals(MSBuildConstants.ProjectReferenceTargetsOrDefaultTargetsMarker)),
                    $"{target} cannot be marked as SkipNonexistentTargets");
                Target = target;
                SkipIfNonexistent = skipIfNonexistent;
            }

            public string Target { get; }

            public bool SkipIfNonexistent { get; }
        }

        public IEnumerable<ReferenceInfo> GetReferences(ProjectGraphNode projectGraphNode, ProjectCollection projectCollection, ProjectGraph.ProjectInstanceFactoryFunc projectInstanceFactory)
        {
            IEnumerable<ProjectItemInstance> projectReferenceItems;

            ProjectInstance requesterInstance = projectGraphNode.ProjectInstance;

            switch (projectGraphNode.ProjectType)
            {
                case ProjectType.OuterBuild:
                    projectReferenceItems = ConstructInnerBuildReferences(requesterInstance);
                    break;
                case ProjectType.InnerBuild:
                case ProjectType.NonMultitargeting:
                    projectReferenceItems = requesterInstance.GetItems(ItemTypeNames.ProjectReference);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            SolutionConfiguration solutionConfiguration = null;
            string solutionConfigurationXml = requesterInstance.GetEngineRequiredPropertyValue(SolutionProjectGenerator.CurrentSolutionConfigurationContents);
            if (!string.IsNullOrWhiteSpace(solutionConfigurationXml))
            {
                solutionConfiguration = new SolutionConfiguration(solutionConfigurationXml);
            }

            foreach (ProjectItemInstance projectReferenceItem in projectReferenceItems)
            {
                if (!String.IsNullOrEmpty(projectReferenceItem.GetMetadataValue(ToolsVersionMetadataName)))
                {
                    throw new InvalidOperationException(
                        String.Format(
                            CultureInfo.InvariantCulture,
                            ResourceUtilities.GetResourceString(
                                "ProjectGraphDoesNotSupportProjectReferenceWithToolset"),
                            projectReferenceItem.EvaluatedInclude,
                            requesterInstance.FullPath));
                }

                string projectReferenceFullPath = projectReferenceItem.GetMetadataValue(FullPathMetadataName);
                bool enableDynamicPlatformResolution = ConversionUtilities.ValidBooleanTrue(requesterInstance.GetEngineRequiredPropertyValue(EnableDynamicPlatformResolutionPropertyName));

                PropertyDictionary<ProjectPropertyInstance> referenceGlobalProperties = GetGlobalPropertiesForItem(
                    projectReferenceItem,
                    requesterInstance.GlobalPropertiesDictionary,
                    // Only allow reuse in scenarios where we will not mutate the collection.
                    // TODO: Should these mutations be moved to globalPropertiesModifiers in the future?
                    allowCollectionReuse: solutionConfiguration == null && !enableDynamicPlatformResolution,
                    projectGraphNode.ProjectType,
                    requesterInstance);

                bool configurationDefined = false;

                // Match what AssignProjectConfiguration does to resolve project references.
                if (solutionConfiguration != null)
                {
                    string projectGuid = projectReferenceItem.GetMetadataValue(ProjectMetadataName);
                    if (solutionConfiguration.TryGetProjectByGuid(projectGuid, out XmlElement projectElement)
                        || solutionConfiguration.TryGetProjectByAbsolutePath(projectReferenceFullPath, out projectElement))
                    {
                        // Note: AssignProjectConfiguration sets various metadata on the ProjectReference item, but ultimately it just translates to the Configuration and Platform global properties on the MSBuild task.
                        string projectConfiguration = projectElement.InnerText;
                        string[] configurationPlatformParts = projectConfiguration.Split(SolutionConfiguration.ConfigPlatformSeparator[0]);
                        SetProperty(referenceGlobalProperties, ConfigurationMetadataName, configurationPlatformParts[0]);

                        if (configurationPlatformParts.Length > 1)
                        {
                            SetProperty(referenceGlobalProperties, PlatformMetadataName, configurationPlatformParts[1]);
                        }
                        else
                        {
                            referenceGlobalProperties.Remove(PlatformMetadataName);
                        }

                        configurationDefined = true;
                    }
                    else
                    {
                        // Note: ShouldUnsetParentConfigurationAndPlatform defaults to true in the AssignProjectConfiguration target when building a solution, so check that it's not false instead of checking that it's true.
                        bool shouldUnsetParentConfigurationAndPlatform = !ConversionUtilities.ValidBooleanFalse(requesterInstance.GetEngineRequiredPropertyValue(ShouldUnsetParentConfigurationAndPlatformPropertyName));
                        if (shouldUnsetParentConfigurationAndPlatform)
                        {
                            referenceGlobalProperties.Remove(ConfigurationMetadataName);
                            referenceGlobalProperties.Remove(PlatformMetadataName);
                        }
                        else
                        {
                            configurationDefined = true;
                        }
                    }
                }

                // Note: Dynamic platform resolution is not enabled for sln-based builds,
                // unless the project isn't known to the solution.
                if (enableDynamicPlatformResolution && !configurationDefined && !projectReferenceItem.HasMetadata(SetPlatformMetadataName))
                {
                    string requesterPlatform = requesterInstance.GetEngineRequiredPropertyValue("Platform");
                    string requesterPlatformLookupTable = requesterInstance.GetEngineRequiredPropertyValue("PlatformLookupTable");

                    var projectInstance = projectInstanceFactory(
                        projectReferenceFullPath,
                        null, // Platform negotiation requires an evaluation with no global properties first
                        projectCollection);

                    string overridePlatformNegotiationMetadataValue = projectReferenceItem.GetMetadataValue(OverridePlatformNegotiationValue);

                    var selectedPlatform = PlatformNegotiation.GetNearestPlatform(overridePlatformNegotiationMetadataValue, projectInstance.GetEngineRequiredPropertyValue(PlatformMetadataName), projectInstance.GetEngineRequiredPropertyValue(PlatformsMetadataName), projectInstance.GetEngineRequiredPropertyValue(PlatformLookupTableMetadataName), requesterInstance.GetEngineRequiredPropertyValue(PlatformLookupTableMetadataName), projectInstance.FullPath, requesterInstance.GetEngineRequiredPropertyValue(PlatformMetadataName));

                    if (selectedPlatform.Equals(String.Empty))
                    {
                        referenceGlobalProperties.Remove(PlatformMetadataName);
                    }
                    else
                    {
                        SetProperty(referenceGlobalProperties, PlatformMetadataName, selectedPlatform);
                    }
                }

                var referenceConfig = new ConfigurationMetadata(projectReferenceFullPath, referenceGlobalProperties);

                yield return new ReferenceInfo(referenceConfig, projectReferenceItem);

                static void SetProperty(PropertyDictionary<ProjectPropertyInstance> properties, string propertyName, string propertyValue)
                {
                    ProjectPropertyInstance propertyInstance = ProjectPropertyInstance.Create(propertyName, propertyValue);
                    properties[propertyName] = propertyInstance;
                }
            }
        }

        internal static string GetInnerBuildPropertyValue(ProjectInstance project)
        {
            return project.GetPropertyValue(GetInnerBuildPropertyName(project));
        }

        internal static string GetInnerBuildPropertyName(ProjectInstance project)
        {
            return project.GetPropertyValue(PropertyNames.InnerBuildProperty);
        }

        internal static string GetInnerBuildPropertyValues(ProjectInstance project)
        {
            return project.GetPropertyValue(project.GetPropertyValue(PropertyNames.InnerBuildPropertyValues));
        }

        internal static ProjectType GetProjectType(ProjectInstance project)
        {
            var isOuterBuild = String.IsNullOrWhiteSpace(GetInnerBuildPropertyValue(project)) && !String.IsNullOrWhiteSpace(GetInnerBuildPropertyValues(project));
            var isInnerBuild = !String.IsNullOrWhiteSpace(GetInnerBuildPropertyValue(project));

            ErrorUtilities.VerifyThrow(!(isOuterBuild && isInnerBuild), $"A project cannot be an outer and inner build at the same time: ${project.FullPath}");

            return isOuterBuild
                ? ProjectType.OuterBuild
                : isInnerBuild
                    ? ProjectType.InnerBuild
                    : ProjectType.NonMultitargeting;
        }

        /// <summary>
        /// To avoid calling nuget at graph construction time, the graph is initially constructed with nodes referencing outer build nodes which in turn
        /// reference inner build nodes. However at build time, the inner builds are referenced directly by the nodes referencing the outer build.
        /// Change the graph to mimic this behaviour.
        /// Example: Node -> Outer -> Inner go to: Node -> Outer; Node->Inner; Outer -> Inner. Inner build edges get added to Node.
        /// </summary>
        public void AddInnerBuildEdges(Dictionary<ConfigurationMetadata, ParsedProject> allNodes, GraphBuilder graphBuilder)
        {
            foreach (KeyValuePair<ConfigurationMetadata, ParsedProject> node in allNodes)
            {
                ProjectGraphNode outerBuild = node.Value.GraphNode;

                if (outerBuild.ProjectType == ProjectType.OuterBuild && outerBuild.ReferencingProjects.Count != 0)
                {
                    foreach (ProjectGraphNode innerBuild in outerBuild.ProjectReferences)
                    {
                        foreach (ProjectGraphNode outerBuildReferencingProject in outerBuild.ReferencingProjects)
                        {
                            // Which edge should be used to connect the outerBuildReferencingProject to the inner builds?
                            // Decided to use the outerBuildBuildReferencingProject -> outerBuild edge in order to preserve any extra metadata
                            // information that may be present on the edge, like the "Targets" metadata which specifies what
                            // targets to call on the references.
                            ProjectItemInstance newInnerBuildEdge = graphBuilder.Edges[(outerBuildReferencingProject, outerBuild)];

                            if (outerBuildReferencingProject.ProjectReferences.Contains(innerBuild))
                            {
                                ErrorUtilities.VerifyThrow(
                                    graphBuilder.Edges[(outerBuildReferencingProject, innerBuild)]
                                        .ItemType.Equals(
                                            TransitiveReferenceItemName,
                                            StringComparison.OrdinalIgnoreCase),
                                    "Only transitive references may reference inner builds that got generated by outer builds");

                                outerBuildReferencingProject.RemoveReference(innerBuild, graphBuilder.Edges);
                            }

                            outerBuildReferencingProject.AddProjectReference(innerBuild, newInnerBuildEdge, graphBuilder.Edges);
                        }
                    }
                }
            }
        }

        private static IEnumerable<ProjectItemInstance> ConstructInnerBuildReferences(ProjectInstance outerBuild)
        {
            var globalPropertyName = GetInnerBuildPropertyName(outerBuild);
            var globalPropertyValues = GetInnerBuildPropertyValues(outerBuild);

            ErrorUtilities.VerifyThrow(!String.IsNullOrWhiteSpace(globalPropertyName), "Must have an inner build property");
            ErrorUtilities.VerifyThrow(!String.IsNullOrWhiteSpace(globalPropertyValues), "Must have values for the inner build property");

            foreach (var globalPropertyValue in ExpressionShredder.SplitSemiColonSeparatedList(globalPropertyValues))
            {
                yield return new ProjectItemInstance(
                    project: outerBuild,
                    itemType: InnerBuildReferenceItemName,
                    includeEscaped: outerBuild.FullPath,
                    directMetadata: [new KeyValuePair<string, string>(ItemMetadataNames.PropertiesMetadataName, $"{globalPropertyName}={globalPropertyValue}")],
                    definingFileEscaped: outerBuild.FullPath);
            }
        }

        /// <summary>
        ///     Gets the effective global properties for an item that will get passed to <see cref="MSBuild.Projects"/>.
        /// </summary>
        /// <remarks>
        ///     The behavior of this method matches the hardcoded behaviour of the msbuild task
        ///     and the project reference global properties modification done at build time in targets / tasks.
        /// </remarks>
        private static PropertyDictionary<ProjectPropertyInstance> GetGlobalPropertiesForItem(
            ProjectItemInstance projectReference,
            PropertyDictionary<ProjectPropertyInstance> requesterGlobalProperties,
            bool allowCollectionReuse,
            ProjectType projectType,
            ProjectInstance requesterInstance)
        {
            ErrorUtilities.VerifyThrowInternalNull(projectReference);
            ArgumentNullException.ThrowIfNull(requesterGlobalProperties);

            string propertiesMetadata = projectReference.GetMetadataValue(ItemMetadataNames.PropertiesMetadataName);
            using var properties = new RefArrayBuilder<(string Name, string Value)>(initialCapacity: 8);
            ref var refProperties = ref properties.AsRef();
            SplitPropertyNameValuePairs(ItemMetadataNames.PropertiesMetadataName, propertiesMetadata, ref refProperties);

            string additionalPropertiesMetadata = projectReference.GetMetadataValue(ItemMetadataNames.AdditionalPropertiesMetadataName);
            using var additionalProperties = new RefArrayBuilder<(string Name, string Value)>(initialCapacity: 8);
            SplitPropertyNameValuePairs(ItemMetadataNames.AdditionalPropertiesMetadataName, additionalPropertiesMetadata, ref additionalProperties.AsRef());

            string undefinePropertiesMetadata = projectReference.GetMetadataValue(ItemMetadataNames.UndefinePropertiesMetadataName);
            using var undefineProperties = new RefArrayBuilder<string>(initialCapacity: 8);
            SplitPropertyNames(undefinePropertiesMetadata, ref undefineProperties.AsRef());

            // For non-OuterBuild project types, compute the effective properties from the project reference.
            // The behavior of this should match the logic in the SDK.
            using var globalPropertiesToRemove = new RefArrayBuilder<string>(initialCapacity: 8);

            if (projectType is not ProjectType.OuterBuild)
            {
                string globalPropertiesToRemoveMetadata = projectReference.GetMetadataValue(GlobalPropertiesToRemoveMetadataName);
                SplitPropertyNames(globalPropertiesToRemoveMetadata, ref globalPropertiesToRemove.AsRef());

                // The properties on the project reference supersede the ones from the MSBuild task instead of appending.
                if (properties.Count == 0)
                {
                    // TODO: Mimic AssignProjectConfiguration's behavior for determining the values for these.
                    var setConfigurationString = projectReference.GetMetadataValue(SetConfigurationMetadataName);
                    var setPlatformString = projectReference.GetMetadataValue(SetPlatformMetadataName);
                    var setTargetFrameworkString = projectReference.GetMetadataValue(SetTargetFrameworkMetadataName);

                    if (!string.IsNullOrEmpty(setConfigurationString) || !string.IsNullOrEmpty(setPlatformString) || !string.IsNullOrEmpty(setTargetFrameworkString))
                    {
                        refProperties.Count = 0;

                        SplitPropertyNameValuePairs(
                            ItemMetadataNames.PropertiesMetadataName,
                            $"{setConfigurationString};{setPlatformString};{setTargetFrameworkString}",
                            ref refProperties);
                    }
                }
            }

            // For InnerBuild, we also need to undefine the inner build property.
            string innerBuildPropertyName = projectType == ProjectType.InnerBuild
                ? GetInnerBuildPropertyName(requesterInstance)
                : null;

            // Check if everything is empty — if so, we can reuse the requester's properties.
            if (properties.IsEmpty
                && additionalProperties.IsEmpty
                && undefineProperties.IsEmpty
                && globalPropertiesToRemove.IsEmpty
                && innerBuildPropertyName is null
                && allowCollectionReuse)
            {
                return requesterGlobalProperties;
            }

            // Make a copy to avoid mutating the requester
            var globalProperties = new PropertyDictionary<ProjectPropertyInstance>(requesterGlobalProperties);

            // Append properties as specified by the various metadata
            foreach (var (name, value) in properties.AsSpan())
            {
                globalProperties[name] = ProjectPropertyInstance.Create(name, value);
            }

            foreach (var (name, value) in additionalProperties.AsSpan())
            {
                globalProperties[name] = ProjectPropertyInstance.Create(name, value);
            }

            // Remove properties:
            // - undefine metadata
            // - GlobalPropertiesToRemove metadata
            // - InnerBuildProperty
            // - inner build property name

            foreach (var propertyName in undefineProperties.AsSpan())
            {
                globalProperties.Remove(propertyName);
            }

            if (!globalPropertiesToRemove.IsEmpty)
            {
                foreach (var propertyName in globalPropertiesToRemove.AsSpan())
                {
                    globalProperties.Remove(propertyName);
                }
            }

            if (projectType is not ProjectType.OuterBuild)
            {
                globalProperties.Remove("InnerBuildProperty");
            }

            if (innerBuildPropertyName is not null)
            {
                globalProperties.Remove(innerBuildPropertyName);
            }

            return globalProperties;
        }

        private static void SplitPropertyNameValuePairs(string syntaxName, string propertyNameValuePairs, ref RefArrayBuilder<(string Name, string Value)> builder)
        {
            if (string.IsNullOrEmpty(propertyNameValuePairs))
            {
                return;
            }

            int startIndex = 0;

            while (startIndex < propertyNameValuePairs.Length)
            {
                int semicolonIndex = propertyNameValuePairs.IndexOf(';', startIndex);

                if (semicolonIndex < 0)
                {
                    semicolonIndex = propertyNameValuePairs.Length;
                }

                if (semicolonIndex > startIndex)
                {
                    int segmentLength = semicolonIndex - startIndex;
                    int equalsIndex = propertyNameValuePairs.IndexOf('=', startIndex, segmentLength);

                    if (equalsIndex >= 0)
                    {
                        string name = TrimmedSubstring(propertyNameValuePairs, startIndex, equalsIndex - startIndex);

                        if (name.Length == 0)
                        {
                            ThrowInvalidProperty(syntaxName, propertyNameValuePairs);
                        }

                        string value = TrimmedSubstring(propertyNameValuePairs, equalsIndex + 1, semicolonIndex - equalsIndex - 1);

                        builder.Add((name, EscapingUtilities.Escape(value)));
                    }
                    else if (builder.Count > 0)
                    {
                        // No '=' sign means this fragment is a continuation of the previous property's value.
                        // This happens when the value contained semicolons (e.g., WarningsAsErrors=1234;5678;9999
                        // becomes segments ["WarningsAsErrors=1234", "5678", "9999"] after splitting on ';').
                        string value = TrimmedSubstring(propertyNameValuePairs, startIndex, segmentLength);

                        ref var previous = ref builder[^1];
                        previous = (previous.Name, previous.Value + ";" + EscapingUtilities.Escape(value));
                    }
                    else
                    {
                        ThrowInvalidProperty(syntaxName, propertyNameValuePairs);
                    }
                }

                startIndex = semicolonIndex + 1;
            }

            // Returns the trimmed substring between startIndex (inclusive) and endIndex (exclusive),
            // or empty if the trimmed result is empty.
            static string TrimmedSubstring(string text, int startIndex, int length)
            {
                int endIndex = startIndex + length;

                while (startIndex < endIndex && char.IsWhiteSpace(text[startIndex]))
                {
                    startIndex++;
                }

                while (endIndex > startIndex && char.IsWhiteSpace(text[endIndex - 1]))
                {
                    endIndex--;
                }

                return endIndex > startIndex
                    ? text.Substring(startIndex, endIndex - startIndex)
                    : string.Empty;
            }

            static void ThrowInvalidProperty(string syntaxName, string propertyNameValuePairs)
                => throw new InvalidProjectFileException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ResourceUtilities.GetResourceString("General.InvalidPropertyError"),
                        syntaxName,
                        propertyNameValuePairs));
        }

        private static void SplitPropertyNames(string propertyNames, ref RefArrayBuilder<string> builder)
        {
            if (string.IsNullOrEmpty(propertyNames))
            {
                return;
            }

            int startIndex = 0;

            while (startIndex < propertyNames.Length)
            {
                int semicolonIndex = propertyNames.IndexOf(';', startIndex);

                if (semicolonIndex < 0)
                {
                    semicolonIndex = propertyNames.Length;
                }

                if (semicolonIndex > startIndex)
                {
                    builder.Add(propertyNames.Substring(startIndex, semicolonIndex - startIndex));
                }

                startIndex = semicolonIndex + 1;
            }
        }

        public readonly struct TargetsToPropagate
        {
            private readonly ImmutableArray<TargetSpecification> _outerBuildTargets;
            private readonly ImmutableArray<TargetSpecification> _allTargets;

            private TargetsToPropagate(ImmutableArray<TargetSpecification> outerBuildTargets, ImmutableArray<TargetSpecification> nonOuterBuildTargets)
            {
                _outerBuildTargets = outerBuildTargets;

                // This is used as the list of entry targets for both inner builds and non-multitargeting projects.
                // It represents the concatenation of outer build targets and non outer build targets, in this order.
                // Non-multitargeting projects use these targets because they act as both outer and inner builds.
                _allTargets = outerBuildTargets.AddRange(nonOuterBuildTargets);
            }

            /// <summary>
            /// Given a project and a set of entry targets the project would get called with,
            /// parse the project's project reference target specification and compute how the target would call its references.
            ///
            /// The calling code should then call <see cref="GetApplicableTargetsForReference"/> for each of the project's references
            /// to get the concrete targets for each reference.
            /// </summary>
            /// <param name="project">Project containing the PRT protocol</param>
            /// <param name="entryTargets">Targets with which <paramref name="project"/> will get called</param>
            /// <returns></returns>
            public static TargetsToPropagate FromProjectAndEntryTargets(ProjectInstance project, ImmutableArray<string> entryTargets)
            {
                var targetsForOuterBuild = ImmutableArray.CreateBuilder<TargetSpecification>();
                var targetsForInnerBuild = ImmutableArray.CreateBuilder<TargetSpecification>();

                ICollection<ProjectItemInstance> projectReferenceTargets = project.GetItems(ItemTypeNames.ProjectReferenceTargets);

                foreach (string entryTarget in entryTargets)
                {
                    foreach (ProjectItemInstance projectReferenceTarget in projectReferenceTargets)
                    {
                        if (projectReferenceTarget.EvaluatedInclude.Equals(entryTarget, StringComparison.OrdinalIgnoreCase))
                        {
                            string targetsMetadataValue = projectReferenceTarget.GetMetadataValue(ItemMetadataNames.ProjectReferenceTargetsMetadataName);
                            bool skipNonexistentTargets = MSBuildStringIsTrue(projectReferenceTarget.GetMetadataValue("SkipNonexistentTargets"));
                            bool targetsAreForOuterBuild = MSBuildStringIsTrue(projectReferenceTarget.GetMetadataValue(ProjectReferenceTargetIsOuterBuildMetadataName));

                            ImmutableArray<TargetSpecification>.Builder targets = targetsAreForOuterBuild
                                ? targetsForOuterBuild
                                : targetsForInnerBuild;

                            foreach (string target in ExpressionShredder.SplitSemiColonSeparatedList(targetsMetadataValue))
                            {
                                targets.Add(new TargetSpecification(target, skipNonexistentTargets));
                            }
                        }
                    }
                }

                return new TargetsToPropagate(targetsForOuterBuild.ToImmutable(), targetsForInnerBuild.ToImmutable());
            }

            public ImmutableArray<string> GetApplicableTargetsForReference(ProjectGraphNode projectGraphNode)
            {
                ImmutableArray<string> RemoveNonexistentTargetsIfSkippable(ImmutableArray<TargetSpecification> targets)
                {
                    var builder = ImmutableArray.CreateBuilder<string>(targets.Length);

                    foreach (TargetSpecification t in targets)
                    {
                        if (!t.SkipIfNonexistent || projectGraphNode.ProjectInstance.Targets.ContainsKey(t.Target))
                        {
                            builder.Add(t.Target);
                        }
                    }

                    return builder.ToImmutable();
                }

                return projectGraphNode.ProjectType switch
                {
                    ProjectType.InnerBuild => RemoveNonexistentTargetsIfSkippable(_allTargets),
                    ProjectType.OuterBuild => RemoveNonexistentTargetsIfSkippable(_outerBuildTargets),
                    ProjectType.NonMultitargeting => RemoveNonexistentTargetsIfSkippable(_allTargets),
                    _ => throw new ArgumentOutOfRangeException(),
                };
            }
        }

        public bool RequiresTransitiveProjectReferences(ProjectGraphNode projectGraphNode)
        {
            // Outer builds do not get edges based on ProjectReference or their transitive closure, only inner builds do.
            if (projectGraphNode.ProjectType == ProjectType.OuterBuild)
            {
                return false;
            }

            ProjectInstance projectInstance = projectGraphNode.ProjectInstance;

            // special case for Quickbuild which updates msbuild binaries independent of props/targets. Remove this when all QB repos will have
            // migrated to new enough Visual Studio versions whose Microsoft.Managed.After.Targets enable transitive references.
            if (string.IsNullOrWhiteSpace(projectInstance.GetEngineRequiredPropertyValue(AddTransitiveProjectReferencesInStaticGraphPropertyName)) &&
                MSBuildStringIsTrue(projectInstance.GetEngineRequiredPropertyValue(PropertyNames.UsingMicrosoftNETSdk)) &&
                MSBuildStringIsFalse(projectInstance.GetEngineRequiredPropertyValue("DisableTransitiveProjectReferences")))
            {
                return true;
            }

            return MSBuildStringIsTrue(
                projectInstance.GetEngineRequiredPropertyValue(AddTransitiveProjectReferencesInStaticGraphPropertyName));
        }

        private static bool MSBuildStringIsTrue(string msbuildString) =>
            ConversionUtilities.ConvertStringToBool(msbuildString, nullOrWhitespaceIsFalse: true);

        private static bool MSBuildStringIsFalse(string msbuildString) => !MSBuildStringIsTrue(msbuildString);
    }
}
