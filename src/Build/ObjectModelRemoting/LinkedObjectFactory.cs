// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// implemented by MSBuild objects that support remote linking;
    /// </summary>
    internal interface ILinkableObject
    {
        /// <summary>
        /// Gets the current link, if any. For local objects returns null;
        /// </summary>
        object? Link { get; }
    }

#nullable disable

    /// <summary>
    /// Provide facility to ExternalProjectsProvider implementation
    /// to create local OM objects based on the remote link.
    /// These object are fully useful for associated Collection.
    /// </summary>
    public class LinkedObjectsFactory
    {
        private LinkedObjectsFactory(ProjectCollection collection)
        {
            Collection = collection;
        }

        /// <summary>
        /// Acquire a <see cref="LinkedObjectsFactory"/> instance for a given ProjectCollection.
        /// Allows creating a local MSBuild OM objects representing externally hosted Projects.
        /// </summary>
        public static LinkedObjectsFactory Get(ProjectCollection collection)
            => new LinkedObjectsFactory(collection);

        /// <summary>
        /// Get the underlying "link" proxy for a given MSBuild object model object (null if it is not linked).
        /// can be used by ExternalProjectsProvider to prevent double linking when implementing remote calls.
        /// </summary>
        public static object GetLink(object obj)
        {
            var linkable = obj as ILinkableObject;
            return linkable?.Link;
        }

        /// <summary>
        /// Check if an msbuild object is local (aka not from External Project)
        /// </summary>
        public static bool IsLocal(object obj)
            => GetLink(obj) == null;

        /// <summary>
        /// Local collection.
        /// </summary>
        public ProjectCollection Collection { get; }

        /// <summary>
        /// Gets only locally load projects, excluding external
        /// </summary>
        public static IReadOnlyCollection<Project> GetLocalProjects(ProjectCollection collection, string projectFile = null)
            => (IReadOnlyCollection<Project>)collection.GetLoadedProjects(false, projectFile);

        #region Evaluation

        public ProjectItem Create(ProjectItemLink link, Project project = null, ProjectItemElement xml = null)
        {
            project ??= link.Project;
            xml ??= link.Xml;

            return new LinkedProjectItem(xml, project, link);
        }

        public ProjectItemDefinition Create(ProjectItemDefinitionLink link, Project project = null)
        {
            project ??= link.Project;

            return new LinkedProjectItemDefinition(link, project, link.ItemType);
        }

        public Project Create(ProjectLink link)
        {
            // note we do not use wrapper LinkedProjects class in this case.
            // Project element storage is in fact increased to support linked (with few bytes)
            // but since the Projects objects number are relatively low, this is not a big concern
            // as with other items that can be typically  1000s of times the number of projects.
            // That is done for simplicity, but if needed we can use the same approach here as well.
            return new Project(Collection, link);
        }

        public ProjectMetadata Create(ProjectMetadataLink link, object parent = null)
        {
            parent ??= link.Parent;

            return new LinkedProjectMetadata(parent, link);
        }

        public ProjectProperty Create(ProjectPropertyLink link, Project project = null)
        {
            project ??= link.Project;

            return new LinkedProjectProperty(project, link);
        }

        public ResolvedImport Create(ProjectImportElement importingElement, ProjectRootElement importedProject, int versionEvaluated, SdkResult sdkResult, bool isImported)
            => new ResolvedImport(importingElement, importedProject, versionEvaluated, sdkResult, isImported);

        #endregion

        #region Construction

        public ProjectRootElement Create(ProjectRootElementLink link)
            => new(link);

        public ProjectChooseElement Create(ProjectChooseElementLink link)
            => new LinkedProjectChooseElement(link);

        public ProjectExtensionsElement Create(ProjectExtensionsElementLink link)
            => new LinkedProjectExtensionsElement(link);

        public ProjectImportElement Create(ProjectImportElementLink link)
            => new LinkedProjectImportElement(link);

        public ProjectImportGroupElement Create(ProjectImportGroupElementLink link)
            => new LinkedProjectImportGroupElement(link);

        public ProjectItemDefinitionElement Create(ProjectItemDefinitionElementLink link)
            => new LinkedProjectItemDefinitionElement(link);

        public ProjectItemDefinitionGroupElement Create(ProjectItemDefinitionGroupElementLink link)
            => new LinkedProjectItemDefinitionGroupElement(link);

        public ProjectItemElement Create(ProjectItemElementLink link)
            => new LinkedProjectItemElement(link);

        public ProjectItemGroupElement Create(ProjectItemGroupElementLink link)
            => new LinkedProjectItemGroupElement(link);

        public ProjectMetadataElement Create(ProjectMetadataElementLink link)
            => new LinkedProjectMetadataElement(link);

        public ProjectOnErrorElement Create(ProjectOnErrorElementLink link)
            => new LinkedProjectOnErrorElement(link);

        public ProjectOtherwiseElement Create(ProjectOtherwiseElementLink link)
            => new LinkedProjectOtherwiseElement(link);

        public ProjectOutputElement Create(ProjectOutputElementLink link)
            => new(link);

        public ProjectPropertyElement Create(ProjectPropertyElementLink link)
            => new LinkedProjectPropertyElement(link);

        public ProjectPropertyGroupElement Create(ProjectPropertyGroupElementLink link)
            => new LinkedProjectPropertyGroupElement(link);

        public ProjectSdkElement Create(ProjectSdkElementLink link)
            => new LinkedProjectSdkElement(link);

        public ProjectTargetElement Create(ProjectTargetElementLink link)
            => new LinkedProjectTargetElement(link);

        public ProjectTaskElement Create(ProjectTaskElementLink link)
            => new LinkedProjectTaskElement(link);

        public ProjectUsingTaskBodyElement Create(ProjectUsingTaskBodyElementLink link)
            => new LinkedProjectUsingTaskBodyElement(link);

        public ProjectUsingTaskElement Create(ProjectUsingTaskElementLink link)
            => new(link);

        public ProjectUsingTaskParameterElement Create(ProjectUsingTaskParameterElementLink link)
            => new LinkedProjectUsingTaskParameterElement(link);

        public ProjectWhenElement Create(ProjectWhenElementLink link)
            => new LinkedProjectWhenElement(link);

        public UsingTaskParameterGroupElement Create(UsingTaskParameterGroupElementLink link)
            => new LinkedUsingTaskParameterGroupElement(link);

        #endregion

        #region Linked classes helpers
        // Using the pattern with overloaded classes that provide "Link" object so we ensure we do not increase the
        // memory storage of original items (with the Link field) while it is small, some of the MSBuild items can be created
        // in millions so it does adds up otherwise.

        private class LinkedProjectItem : ProjectItem, ILinkableObject, IImmutableInstanceProvider<ProjectItemInstance>
        {
            private ProjectItemInstance _immutableInstance;

            internal LinkedProjectItem(ProjectItemElement xml, Project project, ProjectItemLink link)
                : base(xml, project)
            {
                Link = link;
            }

            public ProjectItemInstance ImmutableInstance => _immutableInstance;

            public ProjectItemInstance GetOrSetImmutableInstance(ProjectItemInstance instance)
            {
                Interlocked.CompareExchange(ref _immutableInstance, instance, null);

                return _immutableInstance;
            }

            internal override ProjectItemLink Link { get; }

            object ILinkableObject.Link => Link;
        }

        private class LinkedProjectItemDefinition : ProjectItemDefinition, ILinkableObject, IImmutableInstanceProvider<ProjectItemDefinitionInstance>
        {
            private ProjectItemDefinitionInstance _immutableInstance;

            internal LinkedProjectItemDefinition(ProjectItemDefinitionLink link, Project project, string itemType)
                : base(project, itemType)
            {
                Link = link;
            }

            public ProjectItemDefinitionInstance ImmutableInstance => _immutableInstance;

            public ProjectItemDefinitionInstance GetOrSetImmutableInstance(ProjectItemDefinitionInstance instance)
            {
                Interlocked.CompareExchange(ref _immutableInstance, instance, null);

                return _immutableInstance;
            }

            internal override ProjectItemDefinitionLink Link { get; }
            object ILinkableObject.Link => Link;
        }

        private class LinkedProjectMetadata : ProjectMetadata, ILinkableObject, IImmutableInstanceProvider<ProjectMetadataInstance>
        {
            private ProjectMetadataInstance _immutableInstance;

            internal LinkedProjectMetadata(object parent, ProjectMetadataLink link)
                : base(parent, link.Xml)
            {
                Link = link;
            }

            public ProjectMetadataInstance ImmutableInstance => _immutableInstance;

            public ProjectMetadataInstance GetOrSetImmutableInstance(ProjectMetadataInstance instance)
            {
                Interlocked.CompareExchange(ref _immutableInstance, instance, null);

                return _immutableInstance;
            }

            internal override ProjectMetadataLink Link { get; }
            object ILinkableObject.Link => Link;
        }

        private class LinkedProjectProperty : ProjectProperty, ILinkableObject, IImmutableInstanceProvider<ProjectPropertyInstance>
        {
            private ProjectPropertyInstance _immutableInstance;

            internal ProjectPropertyLink Link { get; }
            object ILinkableObject.Link => Link;

            /// <summary>
            /// Creates a regular evaluated property, with backing XML.
            /// Called by Project.SetProperty.
            /// Property MAY NOT have reserved name and MAY NOT overwrite a global property.
            /// Predecessor is any immediately previous property that was overridden by this one during evaluation and may be null.
            /// </summary>
            internal LinkedProjectProperty(Project project, ProjectPropertyLink link)
                : base(project)
            {
                Link = link;
            }

            public ProjectPropertyInstance ImmutableInstance => _immutableInstance;

            public ProjectPropertyInstance GetOrSetImmutableInstance(ProjectPropertyInstance instance)
            {
                Interlocked.CompareExchange(ref _immutableInstance, instance, null);

                return _immutableInstance;
            }

            public override string Name => Link.Name;

            public override string UnevaluatedValue
            {
                get => Link.UnevaluatedValue;
                set => Link.UnevaluatedValue = value;
            }

            public override bool IsEnvironmentProperty => Link.IsEnvironmentProperty;

            public override bool IsGlobalProperty => Link.IsGlobalProperty;

            public override bool IsReservedProperty => Link.IsReservedProperty;

            public override ProjectPropertyElement Xml => Link.Xml;

            public override ProjectProperty Predecessor => Link.Predecessor;

            public override bool IsImported => Link.IsImported;

            internal override string EvaluatedValueEscapedInternal => Link.EvaluatedIncludeEscaped;
        }
        #endregion
    }
}
