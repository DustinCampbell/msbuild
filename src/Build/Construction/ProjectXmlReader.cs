// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Expander = Microsoft.Build.Evaluation.Expander<Microsoft.Build.Evaluation.ProjectProperty, Microsoft.Build.Evaluation.ProjectItem>;
using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Parses project XML directly from an XmlReader into the ProjectElement tree,
    /// populating ElementData instead of creating an intermediate XmlDocument DOM.
    /// This is a forward-only, single-pass parser that produces the same tree as <see cref="ProjectParser"/>.
    /// </summary>
    internal class ProjectXmlReader
    {
        /// <summary>
        /// Maximum nesting level of Choose elements.
        /// </summary>
        internal const int MaximumChooseNesting = 50;

        private static readonly HashSet<string> ValidAttributesOnlyConditionAndLabel = new HashSet<string> { XMakeAttributes.condition, XMakeAttributes.label };
        private static readonly HashSet<string> ValidAttributesOnImport = new HashSet<string> { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.project, XMakeAttributes.sdk, XMakeAttributes.sdkVersion, XMakeAttributes.sdkMinimumVersion };
        private static readonly HashSet<string> ValidAttributesOnUsingTask = new HashSet<string> { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.taskName, XMakeAttributes.assemblyFile, XMakeAttributes.assemblyName, XMakeAttributes.taskFactory, XMakeAttributes.architecture, XMakeAttributes.runtime, XMakeAttributes.requiredPlatform, XMakeAttributes.requiredRuntime, XMakeAttributes.overrideUsingTask };
        private static readonly HashSet<string> ValidAttributesOnTarget = new HashSet<string> { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.name, XMakeAttributes.inputs, XMakeAttributes.outputs, XMakeAttributes.keepDuplicateOutputs, XMakeAttributes.dependsOnTargets, XMakeAttributes.beforeTargets, XMakeAttributes.afterTargets, XMakeAttributes.returns };
        private static readonly HashSet<string> ValidAttributesOnOnError = new HashSet<string> { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.executeTargets };
        private static readonly HashSet<string> ValidAttributesOnOutput = new HashSet<string> { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.taskParameter, XMakeAttributes.itemName, XMakeAttributes.propertyName };
        private static readonly HashSet<string> ValidAttributesOnUsingTaskParameter = new HashSet<string> { XMakeAttributes.parameterType, XMakeAttributes.output, XMakeAttributes.required };
        private static readonly HashSet<string> ValidAttributesOnUsingTaskBody = new HashSet<string> { XMakeAttributes.evaluate };

        private readonly XmlReader _reader;
        private readonly IXmlLineInfo? _lineInfo;
        private readonly ProjectRootElement _project;
        private readonly string _filePath;
        private bool _seenProjectExtensions;

        private ProjectXmlReader(XmlReader reader, ProjectRootElement project, string filePath)
        {
            _reader = reader;
            _lineInfo = reader as IXmlLineInfo;
            _project = project;
            _filePath = filePath ?? string.Empty;
        }

        /// <summary>
        /// Parses an XML reader into the provided ProjectRootElement using ElementData (no DOM).
        /// </summary>
        internal static void Parse(XmlReader reader, ProjectRootElement projectRootElement, string? filePath)
        {
            MSBuildEventSource.Log.ParseStart(filePath ?? string.Empty);
            {
                ProjectXmlReader parser = new ProjectXmlReader(reader, projectRootElement, filePath ?? string.Empty);
                parser.Parse();
            }
            MSBuildEventSource.Log.ParseStop(filePath ?? string.Empty);
        }

        /// <summary>
        /// Parses the project XML from the reader into the ProjectRootElement.
        /// </summary>
        private void Parse()
        {
            // Move to the root element, capturing the XML declaration encoding if present.
            // Also collect any whitespace/comments between the start of the document (or declaration)
            // and the root element, to preserve formatting when the DOM is materialized later.
            List<XmlTrivia>? rootLeadingTrivia = null;

            while (_reader.NodeType != XmlNodeType.Element)
            {
                if (_reader.NodeType == XmlNodeType.XmlDeclaration)
                {
                    string? encoding = _reader.GetAttribute("encoding");
                    if (!string.IsNullOrEmpty(encoding))
                    {
                        _project.XmlDeclaredEncoding = Encoding.GetEncoding(encoding);
                    }
                }
                else if (_reader.NodeType == XmlNodeType.Whitespace || _reader.NodeType == XmlNodeType.SignificantWhitespace)
                {
                    if (_project.PreserveFormatting)
                    {
                        rootLeadingTrivia ??= new List<XmlTrivia>();
                        rootLeadingTrivia.Add(new XmlTrivia(XmlTriviaKind.Whitespace, _reader.Value, 0, 0));
                    }
                }
                else if (_reader.NodeType == XmlNodeType.Comment)
                {
                    rootLeadingTrivia ??= new List<XmlTrivia>();
                    int line = _lineInfo?.LineNumber ?? 0;
                    int col = _lineInfo?.LinePosition ?? 0;
                    rootLeadingTrivia.Add(new XmlTrivia(XmlTriviaKind.Comment, _reader.Value, line, col));
                }

                if (!_reader.Read())
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        CreateLocation(),
                        "NoRootProjectElement",
                        XMakeElements.project);
                }
            }

            // Validate root element
            string rootName = _reader.LocalName;
            ElementLocation rootLocation = CreateLocation();

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                rootName != XMakeElements.visualStudioProject,
                rootLocation,
                "ProjectUpgradeNeeded",
                _filePath);

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                rootName == XMakeElements.project,
                rootLocation,
                "UnrecognizedElement",
                _reader.Name);

            // Validate namespace — prefixed elements (e.g. msb:Project) are invalid even if
            // namespace URI is correct, matching ProjectXmlUtilities.VerifyValidProjectNamespace.
            string namespaceURI = _reader.NamespaceURI;
            if (_reader.Prefix.Length != 0 ||
                (!string.IsNullOrEmpty(namespaceURI) && !string.Equals(namespaceURI, XMakeAttributes.defaultXmlNamespace, StringComparison.OrdinalIgnoreCase)))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    rootLocation,
                    "ProjectMustBeInMSBuildXmlNamespace",
                    XMakeAttributes.defaultXmlNamespace);
            }

            _project.XmlNamespace = namespaceURI;

            // Read root element data
            ElementData rootData = ReadCurrentElementData();
            if (rootLeadingTrivia is not null)
            {
                rootData.LeadingTrivia = rootLeadingTrivia.ToArray();
            }

            _project.SetElementDataFromParser(rootData, _project);

            // Parse children
            if (!_reader.IsEmptyElement)
            {
                _reader.Read(); // move past the root start element

                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        ParseProjectChild(trivia);
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        // Trivia after last child becomes trailing trivia on root
                        rootData.TrailingTrivia = trivia;
                    }
                    else
                    {
                        ThrowIfNonWhitespaceText(rootData);
                        _reader.Read();
                    }
                }
            }

            // Capture any content after the root element's closing tag (e.g., trailing newline).
            // The DOM preserves these as child nodes of the XmlDocument after the root element.
            if (_project.PreserveFormatting && _reader.Read())
            {
                List<XmlTrivia>? afterRoot = null;
                do
                {
                    if (_reader.NodeType == XmlNodeType.Whitespace || _reader.NodeType == XmlNodeType.SignificantWhitespace)
                    {
                        afterRoot ??= new List<XmlTrivia>();
                        afterRoot.Add(new XmlTrivia(XmlTriviaKind.Whitespace, _reader.Value, 0, 0));
                    }
                    else if (_reader.NodeType == XmlNodeType.Comment)
                    {
                        afterRoot ??= new List<XmlTrivia>();
                        afterRoot.Add(new XmlTrivia(XmlTriviaKind.Comment, _reader.Value, 0, 0));
                    }
                }
                while (_reader.Read());

                if (afterRoot is not null)
                {
                    _project.AfterRootTrivia = afterRoot.ToArray();
                }
            }
        }

        private void ParseProjectChild(XmlTrivia[]? leadingTrivia)
        {
            string childName = _reader.LocalName;
            ElementLocation childLocation = CreateLocation();

            ProjectElement? child = null;
            switch (childName)
            {
                case XMakeElements.propertyGroup:
                    child = ParsePropertyGroup(_project);
                    break;
                case XMakeElements.itemGroup:
                    child = ParseItemGroup(_project);
                    break;
                case XMakeElements.importGroup:
                    child = ParseImportGroup();
                    break;
                case XMakeElements.import:
                    child = ParseImport(_project);
                    break;
                case XMakeElements.usingTask:
                    child = ParseUsingTask();
                    break;
                case XMakeElements.target:
                    child = ParseTarget();
                    break;
                case XMakeElements.itemDefinitionGroup:
                    child = ParseItemDefinitionGroup(_project);
                    break;
                case XMakeElements.choose:
                    child = ParseChoose(_project, 0);
                    break;
                case XMakeElements.projectExtensions:
                    child = ParseProjectExtensions();
                    break;
                case XMakeElements.sdk:
                    child = ParseSdk();
                    break;
                case XMakeElements.error:
                case XMakeElements.warning:
                case XMakeElements.message:
                    ProjectErrorUtilities.ThrowInvalidProject(childLocation, "ErrorWarningMessageNotSupported", childName);
                    break;
                default:
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(childName, XMakeElements.project, childLocation);
                    break;
            }

            if (child is not null)
            {
                if (leadingTrivia is not null)
                {
                    child.DataSource!.LeadingTrivia = leadingTrivia;
                }

                _project.AppendParentedChildNoChecks(child);
            }
        }

        private ProjectPropertyGroupElement ParsePropertyGroup(ProjectElementContainer parent)
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnlyConditionAndLabel);
            ProjectPropertyGroupElement group = new ProjectPropertyGroupElement(data, parent, _project);

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        ThrowIfNonWhitespaceText(data);
                        ParseProperty(group, trivia);
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        ThrowIfNonWhitespaceText(data);
                        _reader.Read();
                    }
                }
            }

            _reader.Read(); // consume end element or empty element
            return group;
        }

        private void ParseProperty(ProjectPropertyGroupElement parent, XmlTrivia[]? leadingTrivia = null)
        {
            string name = _reader.LocalName;
            ElementLocation location = CreateLocation();

            VerifyValidElementName(name, location);
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                !XMakeElements.ReservedItemNames.Contains(name) && !ReservedPropertyNames.IsReservedProperty(name),
                location,
                "CannotModifyReservedProperty",
                name);

            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnlyConditionAndLabel);
            data.LeadingTrivia = leadingTrivia;
            // Use ReadInnerXmlContent which handles both pure text and mixed content
            // with GetXmlNodeInnerContents semantics (CDATA unwrap, comment strip, InnerXml for elements).
            ReadInnerXmlContent(data);

            // Normalize self-closing elements: DOM serializes <X/> as <X /> (with space before />).
            if (data.IsInnerXml && data.TextContent is not null)
            {
                data.TextContent = NormalizeSelfClosingTags(data.TextContent);
            }

            ProjectPropertyElement property = new ProjectPropertyElement(data, parent, _project);
            parent.AppendParentedChildNoChecks(property);
        }

        private ProjectItemGroupElement ParseItemGroup(ProjectElementContainer parent)
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnlyConditionAndLabel);
            ProjectItemGroupElement group = new ProjectItemGroupElement(data, parent, _project);

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        ThrowIfNonWhitespaceText(data);
                        ParseItem(group, trivia);
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        ThrowIfNonWhitespaceText(data);
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            return group;
        }

        private void ParseItem(ProjectItemGroupElement parent, XmlTrivia[]? leadingTrivia = null)
        {
            bool belowTarget = parent.Parent is ProjectTargetElement;
            string itemType = _reader.LocalName;
            ElementLocation location = CreateLocation();

            VerifyValidElementName(itemType, location);
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                !XMakeElements.ReservedItemNames.Contains(itemType),
                location,
                "CannotModifyReservedItem",
                itemType);

            ElementData data = ReadCurrentElementData();
            data.LeadingTrivia = leadingTrivia;

            // Validate item operations
            string include = data.GetAttributeValue(XMakeAttributes.include) ?? string.Empty;
            string exclude = data.GetAttributeValue(XMakeAttributes.exclude) ?? string.Empty;
            string remove = data.GetAttributeValue(XMakeAttributes.remove) ?? string.Empty;
            string update = data.GetAttributeValue(XMakeAttributes.update) ?? string.Empty;

            int exclusiveAttributeCount = 0;
            string exclusiveItemOperation = string.Empty;
            if (data.HasAttribute(XMakeAttributes.include)) { exclusiveAttributeCount++; exclusiveItemOperation = XMakeAttributes.include; }
            if (data.HasAttribute(XMakeAttributes.remove)) { exclusiveAttributeCount++; exclusiveItemOperation = XMakeAttributes.remove; }
            if (data.HasAttribute(XMakeAttributes.update)) { exclusiveAttributeCount++; exclusiveItemOperation = XMakeAttributes.update; }

            if (exclusiveAttributeCount > 1)
            {
                ElementLocation errorLoc = data.GetAttributeLocation(XMakeAttributes.remove) ?? data.GetAttributeLocation(XMakeAttributes.update) ?? location;
                ProjectErrorUtilities.ThrowInvalidProject(errorLoc, "InvalidAttributeExclusive");
            }

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                exclusiveAttributeCount == 1 || belowTarget,
                location,
                "IncludeRemoveOrUpdate",
                exclusiveItemOperation,
                itemType);

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                exclude.Length == 0 || include.Length > 0,
                data.GetAttributeLocation(XMakeAttributes.exclude) ?? location,
                "UnrecognizedAttribute",
                XMakeAttributes.exclude,
                itemType);

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                include.Length > 0 || !data.HasAttribute(XMakeAttributes.include),
                location,
                "MissingRequiredAttribute",
                XMakeAttributes.include,
                itemType);

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                remove.Length > 0 || !data.HasAttribute(XMakeAttributes.remove),
                location,
                "MissingRequiredAttribute",
                XMakeAttributes.remove,
                itemType);

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                update.Length > 0 || !data.HasAttribute(XMakeAttributes.update),
                location,
                "MissingRequiredAttribute",
                XMakeAttributes.update,
                itemType);

            ProjectItemElement item = new ProjectItemElement(data, parent, _project);

            // Check for metadata-as-attributes
            var itemAttrs = data.Attributes;
            for (int i = 0; i < itemAttrs.Length; i++)
            {
                ProjectParser.CheckMetadataAsAttributeName(itemAttrs[i].Name, out bool isKnownAttribute, out bool isValidMetadataNameInAttribute);

                if (!isKnownAttribute && !isValidMetadataNameInAttribute)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(itemAttrs[i].GetLocation(data.FilePath), "UnrecognizedAttribute", itemAttrs[i].Name, data.Name);
                }
                else if (isValidMetadataNameInAttribute)
                {
                    // Subtract 1 from column: in the DOM path, metadata-as-attribute location
                    // flows through XmlElementWithLocation constructor which always subtracts 1.
                    int metaCol = itemAttrs[i].Column > 0 ? itemAttrs[i].Column - 1 : 0;
                    var metaData = new ElementData(itemAttrs[i].Name, string.Empty, data.FilePath, itemAttrs[i].Line, metaCol);
                    metaData.TextContent = itemAttrs[i].Value;
                    // Create without parent first so ExpressedAsAttribute setter doesn't trigger DOM operations
                    ProjectMetadataElement metadatum = new ProjectMetadataElement(metaData, null!, _project);
                    metadatum.ExpressedAsAttribute = true;
                    metadatum.Parent = item;
                    item.AppendParentedChildNoChecks(metadatum);
                }
            }

            // Parse child metadata elements
            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        ParseMetadata(item, trivia);
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        ThrowIfNonWhitespaceText(data);
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            parent.AppendParentedChildNoChecks(item);
        }

        private void ParseMetadata(ProjectElementContainer parent, XmlTrivia[]? leadingTrivia = null)
        {
            string name = _reader.LocalName;
            ElementLocation location = CreateLocation();

            VerifyValidElementName(name, location);
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                !ItemSpecModifiers.IsItemSpecModifier(name),
                location,
                "ItemSpecModifierCannotBeCustomMetadata",
                name);
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                !XMakeElements.ReservedItemNames.Contains(name),
                location,
                "CannotModifyReservedItemMetadata",
                name);

            if (parent is ProjectItemElement itemElement)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    itemElement.Remove.Length == 0,
                    location,
                    "ChildElementsBelowRemoveNotAllowed",
                    name);
            }

            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnlyConditionAndLabel);
            data.LeadingTrivia = leadingTrivia;
            ReadTextContent(data);

            ProjectMetadataElement metadatum = new ProjectMetadataElement(data, parent, _project);

            // If the parent is an item definition, verify no item vectors
            if (parent is ProjectItemDefinitionElement)
            {
                bool containsItemVector = Expander.ExpressionContainsItemVector(metadatum.Value);
                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    !containsItemVector,
                    location,
                    "MetadataDefinitionCannotContainItemVectorExpression",
                    metadatum.Value,
                    metadatum.Name);
            }

            parent.AppendParentedChildNoChecks(metadatum);
        }

        private ProjectImportGroupElement ParseImportGroup()
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnlyConditionAndLabel);
            ProjectImportGroupElement importGroup = new ProjectImportGroupElement(data, _project, _project);

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        string childName = _reader.LocalName;
                        ElementLocation childLocation = CreateLocation();
                        ProjectErrorUtilities.VerifyThrowInvalidProject(
                            childName == XMakeElements.import,
                            childLocation,
                            "UnrecognizedChildElement",
                            childName,
                            XMakeElements.importGroup);

                        importGroup.AppendParentedChildNoChecks(ParseImport(importGroup));
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            return importGroup;
        }

        private ProjectImportElement ParseImport(ProjectElementContainer parent)
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnImport);
            ElementLocation location = data.Location;

            string projectAttr = data.GetAttributeValue(XMakeAttributes.project) ?? string.Empty;
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                projectAttr.Length > 0,
                location,
                "MissingRequiredAttribute",
                XMakeAttributes.project,
                XMakeElements.import);

            // Verify no children
            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(
                            CreateLocation(),
                            "UnrecognizedChildElement",
                            _reader.LocalName,
                            XMakeElements.import);
                    }

                    _reader.Read();
                }
            }

            _reader.Read();

            SdkReference? sdk = null;
            string? sdkAttr = data.GetAttributeValue(XMakeAttributes.sdk);
            if (sdkAttr != null)
            {
                sdk = new SdkReference(
                    sdkAttr,
                    data.GetAttributeValue(XMakeAttributes.sdkVersion),
                    data.GetAttributeValue(XMakeAttributes.sdkMinimumVersion));
            }

            return new ProjectImportElement(data, parent, _project, sdk);
        }

        private ProjectUsingTaskElement ParseUsingTask()
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnUsingTask);
            ElementLocation location = data.Location;

            string taskName = data.GetAttributeValue(XMakeAttributes.taskName) ?? string.Empty;
            ProjectErrorUtilities.VerifyThrowInvalidProject(taskName.Length > 0, location, "ProjectTaskNameEmpty");

            string assemblyName = data.GetAttributeValue(XMakeAttributes.assemblyName) ?? string.Empty;
            string assemblyFile = data.GetAttributeValue(XMakeAttributes.assemblyFile) ?? string.Empty;

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                (assemblyName.Length > 0) ^ (assemblyFile.Length > 0),
                location,
                "UsingTaskAssemblySpecification",
                XMakeElements.usingTask,
                XMakeAttributes.assemblyName,
                XMakeAttributes.assemblyFile);

            // If the attribute is present, it must be non-empty.
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                !data.HasAttribute(XMakeAttributes.assemblyName) || assemblyName.Length > 0,
                data.GetAttributeLocation(XMakeAttributes.assemblyName) ?? location,
                "InvalidAttributeValue",
                string.Empty,
                XMakeAttributes.assemblyName,
                data.Name);

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                !data.HasAttribute(XMakeAttributes.assemblyFile) || assemblyFile.Length > 0,
                data.GetAttributeLocation(XMakeAttributes.assemblyFile) ?? location,
                "InvalidAttributeValue",
                string.Empty,
                XMakeAttributes.assemblyFile,
                data.Name);

            ProjectUsingTaskElement usingTask = new ProjectUsingTaskElement(data, _project, _project);
            bool foundTaskElement = false;
            bool foundParameterGroup = false;

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        string childName = _reader.LocalName;
                        ElementLocation childLocation = CreateLocation();

                        switch (childName)
                        {
                            case XMakeElements.usingTaskParameterGroup:
                                if (foundParameterGroup)
                                {
                                    ProjectErrorUtilities.ThrowInvalidProject(childLocation, "InvalidChildElementDueToDuplication", childName, XMakeElements.usingTask);
                                }
                                usingTask.AppendParentedChildNoChecks(ParseUsingTaskParameterGroup(usingTask));
                                foundParameterGroup = true;
                                break;
                            case XMakeElements.usingTaskBody:
                                if (foundTaskElement)
                                {
                                    ProjectErrorUtilities.ThrowInvalidProject(childLocation, "InvalidChildElementDueToDuplication", childName, XMakeElements.usingTask);
                                }
                                usingTask.AppendParentedChildNoChecks(ParseUsingTaskBody(usingTask));
                                foundTaskElement = true;
                                break;
                            default:
                                ProjectXmlUtilities.ThrowProjectInvalidChildElement(childName, XMakeElements.usingTask, childLocation);
                                break;
                        }
                    }
                    else
                    {
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            return usingTask;
        }

        private UsingTaskParameterGroupElement ParseUsingTaskParameterGroup(ProjectUsingTaskElement parent)
        {
            ElementData data = ReadCurrentElementData();
            // Verify no attributes
            if (data.AttributeCount > 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(data.Location, "UnrecognizedAttribute", data.Attributes[0].Name, data.Name);
            }

            UsingTaskParameterGroupElement parameterGroup = new UsingTaskParameterGroupElement(data, parent, _project);
            HashSet<string> seenNames = new HashSet<string>();

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        string paramName = _reader.LocalName;
                        ElementLocation paramLocation = CreateLocation();

                        if (seenNames.Contains(paramName))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(paramLocation, "InvalidChildElementDueToDuplication", paramName, XMakeElements.usingTaskParameterGroup);
                        }

                        ElementData paramData = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnUsingTaskParameter);
                        VerifyValidElementName(paramName, paramLocation);

                        ProjectUsingTaskParameterElement parameter = new ProjectUsingTaskParameterElement(paramData, parameterGroup, _project);
                        parameterGroup.AppendParentedChildNoChecks(parameter);
                        seenNames.Add(paramName);

                        SkipToEndElement();
                    }
                    else
                    {
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            return parameterGroup;
        }

        private ProjectUsingTaskBodyElement ParseUsingTaskBody(ProjectUsingTaskElement parent)
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnUsingTaskBody);
            ReadInnerXmlContent(data);

            ProjectUsingTaskBodyElement body = new ProjectUsingTaskBodyElement(data, parent, _project);
            return body;
        }

        private ProjectTargetElement ParseTarget()
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnTarget);
            ElementLocation location = data.Location;

            string nameAttr = data.GetAttributeValue(XMakeAttributes.name) ?? string.Empty;
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                nameAttr.Length > 0,
                location,
                "MissingRequiredAttribute",
                XMakeAttributes.name,
                XMakeElements.target);

            string targetName = EscapingUtilities.UnescapeAll(nameAttr);
            int indexOfSpecialCharacter = targetName.AsSpan().IndexOfAny(XMakeElements.InvalidTargetNameCharacters);
            if (indexOfSpecialCharacter >= 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    data.GetAttributeLocation(XMakeAttributes.name) ?? location,
                    "NameInvalid",
                    targetName,
                    targetName[indexOfSpecialCharacter]);
            }

            ProjectTargetElement target = new ProjectTargetElement(data, _project, _project);
            ProjectOnErrorElement? onError = null;

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        string childName = _reader.LocalName;
                        ElementLocation childLocation = CreateLocation();

                        switch (childName)
                        {
                            case XMakeElements.propertyGroup:
                                if (onError != null)
                                {
                                    ProjectErrorUtilities.ThrowInvalidProject(onError.Location, "NodeMustBeLastUnderElement", XMakeElements.onError, XMakeElements.target, childName);
                                }
                                var pg = ParsePropertyGroup(target);
                                pg.DataSource!.LeadingTrivia = trivia;
                                target.AppendParentedChildNoChecks(pg);
                                break;
                            case XMakeElements.itemGroup:
                                if (onError != null)
                                {
                                    ProjectErrorUtilities.ThrowInvalidProject(onError.Location, "NodeMustBeLastUnderElement", XMakeElements.onError, XMakeElements.target, childName);
                                }
                                var ig = ParseItemGroup(target);
                                ig.DataSource!.LeadingTrivia = trivia;
                                target.AppendParentedChildNoChecks(ig);
                                break;
                            case XMakeElements.onError:
                                {
                                    ElementData onErrorData = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnOnError);
                                    onErrorData.LeadingTrivia = trivia;
                                    string executeTargets = onErrorData.GetAttributeValue(XMakeAttributes.executeTargets) ?? string.Empty;
                                    ProjectErrorUtilities.VerifyThrowInvalidProject(
                                        executeTargets.Length > 0,
                                        onErrorData.Location,
                                        "MissingRequiredAttribute",
                                        XMakeAttributes.executeTargets,
                                        XMakeElements.onError);
                                    VerifyNoChildElements(onErrorData);
                                    onError = new ProjectOnErrorElement(onErrorData, target, _project);
                                    target.AppendParentedChildNoChecks(onError);
                                    SkipToEndElement();
                                }
                                break;
                            case XMakeElements.itemDefinitionGroup:
                                ProjectErrorUtilities.ThrowInvalidProject(childLocation, "ItemDefinitionGroupNotLegalInsideTarget", childName);
                                break;
                            default:
                                if (onError != null)
                                {
                                    ProjectErrorUtilities.ThrowInvalidProject(onError.Location, "NodeMustBeLastUnderElement", XMakeElements.onError, XMakeElements.target, childName);
                                }
                                var taskElement = ParseTask(target);
                                taskElement.DataSource!.LeadingTrivia = trivia;
                                target.AppendParentedChildNoChecks(taskElement);
                                break;
                        }
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            return target;
        }

        private ProjectTaskElement ParseTask(ProjectTargetElement parent)
        {
            ElementData data = ReadCurrentElementData();
            ElementLocation location = data.Location;

            // Verify no badly-cased special attributes
            var taskAttrs = data.Attributes;
            for (int i = 0; i < taskAttrs.Length; i++)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    !XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(taskAttrs[i].Name),
                    taskAttrs[i].GetLocation(data.FilePath),
                    "BadlyCasedSpecialTaskAttribute",
                    taskAttrs[i].Name,
                    data.Name,
                    data.Name);
            }

            ProjectTaskElement task = new ProjectTaskElement(data, parent, _project);

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                bool hasChildElements = false;
                bool hasTextContent = false;

                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        hasChildElements = true;
                        string childName = _reader.LocalName;
                        ElementLocation childLocation = CreateLocation();

                        ProjectErrorUtilities.VerifyThrowInvalidProject(
                            childName == XMakeElements.output,
                            childLocation,
                            "UnrecognizedChildElement",
                            childName,
                            task.Name);

                        task.AppendParentedChildNoChecks(ParseOutput(task, trivia));
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        if (_reader.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(_reader.Value))
                        {
                            hasTextContent = true;
                        }

                        _reader.Read();
                    }
                }

                // If the element has only text content and no child elements, it's likely
                // a property that should be inside <PropertyGroup>.
                if (hasTextContent && !hasChildElements)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(location, "PropertyOutsidePropertyGroupInTarget", data.Name, parent.Name);
                }
            }

            _reader.Read();
            return task;
        }

        private ProjectOutputElement ParseOutput(ProjectTaskElement parent, XmlTrivia[]? leadingTrivia = null)
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnOutput);
            data.LeadingTrivia = leadingTrivia;
            ElementLocation location = data.Location;

            string taskParameter = data.GetAttributeValue(XMakeAttributes.taskParameter) ?? string.Empty;
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                taskParameter.Length > 0,
                location,
                "MissingRequiredAttribute",
                XMakeAttributes.taskParameter,
                XMakeElements.output);

            string? itemName = data.GetAttributeValue(XMakeAttributes.itemName);
            string? propertyName = data.GetAttributeValue(XMakeAttributes.propertyName);

            // Exactly one of ItemName/PropertyName must be present and non-empty.
            // Having both attributes present (even if one is empty) is invalid.
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                (itemName is null && !string.IsNullOrWhiteSpace(propertyName)) || (propertyName is null && !string.IsNullOrWhiteSpace(itemName)),
                location,
                "InvalidTaskOutputSpecification",
                parent.Name);

            if (!string.IsNullOrWhiteSpace(propertyName))
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    !ReservedPropertyNames.IsReservedProperty(propertyName),
                    location,
                    "CannotModifyReservedProperty",
                    propertyName);
            }

            VerifyNoChildElements(data);
            SkipToEndElement();

            return new ProjectOutputElement(data, parent, _project);
        }

        private ProjectItemDefinitionGroupElement ParseItemDefinitionGroup(ProjectElementContainer parent)
        {
            ElementData data = ReadCurrentElementDataAndValidateAttributes(ValidAttributesOnlyConditionAndLabel);
            ProjectItemDefinitionGroupElement group = new ProjectItemDefinitionGroupElement(data, parent, _project);

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        ParseItemDefinition(group, trivia);
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            return group;
        }

        private void ParseItemDefinition(ProjectItemDefinitionGroupElement parent, XmlTrivia[]? leadingTrivia = null)
        {
            string name = _reader.LocalName;
            ElementLocation location = CreateLocation();

            ElementData data = ReadCurrentElementData();
            data.LeadingTrivia = leadingTrivia;
            ProjectItemDefinitionElement itemDefinition = new ProjectItemDefinitionElement(data, parent, _project);

            // Check for metadata-as-attributes
            var defAttrs = data.Attributes;
            for (int i = 0; i < defAttrs.Length; i++)
            {
                ProjectParser.CheckMetadataAsAttributeName(defAttrs[i].Name, out bool isKnownAttribute, out bool isValidMetadataNameInAttribute);

                if (!isKnownAttribute && !isValidMetadataNameInAttribute)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(defAttrs[i].GetLocation(data.FilePath), "UnrecognizedAttribute", defAttrs[i].Name, data.Name);
                }
                else if (isValidMetadataNameInAttribute)
                {
                    // Subtract 1 from column: in the DOM path, metadata-as-attribute location
                    // flows through XmlElementWithLocation constructor which always subtracts 1.
                    int metaCol = defAttrs[i].Column > 0 ? defAttrs[i].Column - 1 : 0;
                    var metaData = new ElementData(defAttrs[i].Name, string.Empty, data.FilePath, defAttrs[i].Line, metaCol);
                    metaData.TextContent = defAttrs[i].Value;
                    ProjectMetadataElement metadatum = new ProjectMetadataElement(metaData, null!, _project);
                    metadatum.ExpressedAsAttribute = true;
                    metadatum.Parent = itemDefinition;
                    itemDefinition.AppendParentedChildNoChecks(metadatum);
                }
                else if (!ValidAttributesOnlyConditionAndLabel.Contains(defAttrs[i].Name))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(defAttrs[i].GetLocation(data.FilePath), "UnrecognizedAttribute", defAttrs[i].Name, data.Name);
                }
            }

            // Parse child metadata elements
            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        ParseMetadata(itemDefinition, trivia);
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            parent.AppendParentedChildNoChecks(itemDefinition);
        }

        private ProjectChooseElement ParseChoose(ProjectElementContainer parent, int nestingDepth)
        {
            ElementData data = ReadCurrentElementData();
            ElementLocation location = data.Location;

            // Verify no attributes
            if (data.AttributeCount > 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(location, "UnrecognizedAttribute", data.Attributes[0].Name, data.Name);
            }

            nestingDepth++;
            ProjectErrorUtilities.VerifyThrowInvalidProject(nestingDepth <= MaximumChooseNesting, location, "ChooseOverflow", MaximumChooseNesting);

            ProjectChooseElement choose = new ProjectChooseElement(data, parent, _project);
            bool foundWhen = false;
            bool foundOtherwise = false;

            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        string childName = _reader.LocalName;
                        ElementLocation childLocation = CreateLocation();

                        switch (childName)
                        {
                            case XMakeElements.when:
                                ProjectErrorUtilities.VerifyThrowInvalidProject(!foundOtherwise, childLocation, "WhenNotAllowedAfterOtherwise");
                                choose.AppendParentedChildNoChecks(ParseWhen(choose, nestingDepth));
                                foundWhen = true;
                                break;
                            case XMakeElements.otherwise:
                                ProjectErrorUtilities.VerifyThrowInvalidProject(!foundOtherwise, childLocation, "MultipleOtherwise");
                                foundOtherwise = true;
                                choose.AppendParentedChildNoChecks(ParseOtherwise(choose, nestingDepth));
                                break;
                            default:
                                ProjectXmlUtilities.ThrowProjectInvalidChildElement(childName, XMakeElements.choose, childLocation);
                                break;
                        }
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        data.TrailingTrivia = trivia;
                    }
                    else
                    {
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
            ProjectErrorUtilities.VerifyThrowInvalidProject(foundWhen, location, "ChooseMustContainWhen");
            return choose;
        }

        private ProjectWhenElement ParseWhen(ProjectChooseElement parent, int nestingDepth)
        {
            ElementLocation location = CreateLocation();
            ElementData data = ReadCurrentElementData();

            string condition = data.GetAttributeValue(XMakeAttributes.condition) ?? string.Empty;
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                condition.Length > 0,
                location,
                "MissingRequiredAttribute",
                XMakeAttributes.condition,
                XMakeElements.when);

            ProjectWhenElement when = new ProjectWhenElement(data, parent, _project);
            ParseWhenOtherwiseChildren(when, nestingDepth);
            return when;
        }

        private ProjectOtherwiseElement ParseOtherwise(ProjectChooseElement parent, int nestingDepth)
        {
            ElementData data = ReadCurrentElementData();
            // Verify no attributes
            if (data.AttributeCount > 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(data.Location, "UnrecognizedAttribute", data.Attributes[0].Name, data.Name);
            }

            ProjectOtherwiseElement otherwise = new ProjectOtherwiseElement(data, parent, _project);
            ParseWhenOtherwiseChildren(otherwise, nestingDepth);
            return otherwise;
        }

        private void ParseWhenOtherwiseChildren(ProjectElementContainer parent, int nestingDepth)
        {
            if (!_reader.IsEmptyElement)
            {
                _reader.Read();
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    var trivia = CollectTrivia();
                    if (_reader.NodeType == XmlNodeType.Element)
                    {
                        string childName = _reader.LocalName;
                        ElementLocation childLocation = CreateLocation();

                        switch (childName)
                        {
                            case XMakeElements.propertyGroup:
                                parent.AppendParentedChildNoChecks(ParsePropertyGroup(parent));
                                break;
                            case XMakeElements.itemGroup:
                                parent.AppendParentedChildNoChecks(ParseItemGroup(parent));
                                break;
                            case XMakeElements.choose:
                                parent.AppendParentedChildNoChecks(ParseChoose(parent, nestingDepth));
                                break;
                            case XMakeElements.itemDefinitionGroup:
                                parent.AppendParentedChildNoChecks(ParseItemDefinitionGroup(parent));
                                break;
                            default:
                                ProjectXmlUtilities.ThrowProjectInvalidChildElement(childName, parent.ElementName, childLocation);
                                break;
                        }
                    }
                    else if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (parent.DataSource is { } parentData)
                        {
                            parentData.TrailingTrivia = trivia;
                        }
                    }
                    else
                    {
                        _reader.Read();
                    }
                }
            }

            _reader.Read();
        }

        private ProjectExtensionsElement ParseProjectExtensions()
        {
            ElementData data = ReadCurrentElementData();
            ElementLocation location = data.Location;

            // Verify no attributes
            if (data.AttributeCount > 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(location, "UnrecognizedAttribute", data.Attributes[0].Name, data.Name);
            }

            ProjectErrorUtilities.VerifyThrowInvalidProject(!_seenProjectExtensions, location, "DuplicateProjectExtensions");
            _seenProjectExtensions = true;

            // Read inner XML as text content
            ReadInnerXmlContent(data);

            return new ProjectExtensionsElement(data, _project, _project);
        }

        private ProjectSdkElement ParseSdk()
        {
            ElementData data = ReadCurrentElementData();
            string sdkName = data.GetAttributeValue(XMakeAttributes.sdkName) ?? string.Empty;

            if (string.IsNullOrEmpty(sdkName))
            {
                ProjectErrorUtilities.ThrowInvalidProject(data.Location, "InvalidSdkElementName", data.Name);
            }

            SkipToEndElement();
            return new ProjectSdkElement(data, _project, _project);
        }

        #region Helper Methods

        /// <summary>
        /// Collects trivia (comments and whitespace) from the current reader position,
        /// advancing through any non-element, non-end-element nodes.
        /// Returns null if no comments are encountered (whitespace alone is not captured).
        /// After this call, the reader is positioned on an Element, EndElement, or EOF node.
        /// </summary>
        private XmlTrivia[]? CollectTrivia()
        {
            List<XmlTrivia>? trivia = null;

            while (_reader.NodeType == XmlNodeType.Comment ||
                   _reader.NodeType == XmlNodeType.Whitespace ||
                   _reader.NodeType == XmlNodeType.SignificantWhitespace)
            {
                if (_reader.NodeType == XmlNodeType.Comment)
                {
                    trivia ??= new List<XmlTrivia>();
                    int line = _lineInfo?.LineNumber ?? 0;
                    int col = _lineInfo?.LinePosition ?? 0;
                    trivia.Add(new XmlTrivia(XmlTriviaKind.Comment, _reader.Value, line, col));
                }
                else if (_project.PreserveFormatting)
                {
                    trivia ??= new List<XmlTrivia>();
                    int line = _lineInfo?.LineNumber ?? 0;
                    int col = _lineInfo?.LinePosition ?? 0;
                    trivia.Add(new XmlTrivia(XmlTriviaKind.Whitespace, _reader.Value, line, col));
                }

                _reader.Read();
            }

            return trivia?.ToArray();
        }

        /// <summary>
        /// Creates an ElementLocation from the current reader position.
        /// </summary>
        private ElementLocation CreateLocation()
        {
            if (_lineInfo != null && _lineInfo.HasLineInfo())
            {
                // XmlReader.Create produces a reader where LinePosition already points to the '<'
                // character for elements (unlike legacy XmlTextReader which points to the name).
                int column = _lineInfo.LinePosition;
                return ElementLocation.Create(_filePath, _lineInfo.LineNumber, column);
            }

            return ElementLocation.Create(_filePath);
        }

        /// <summary>
        /// Reads the current element's name, namespace, location, and all attributes from the XmlReader.
        /// Does NOT advance past the element — the reader remains positioned on the element's start tag.
        /// </summary>
        private ElementData ReadCurrentElementData()
        {
            string name = _reader.LocalName;
            string namespaceURI = _reader.NamespaceURI;
            int line = 0;
            int column = 0;
            if (_lineInfo != null && _lineInfo.HasLineInfo())
            {
                line = _lineInfo.LineNumber;
                // XmlReader.Create produces a reader where LinePosition on an Element already reports
                // the column of the '<' character (unlike the legacy XmlTextReader used by XmlDocument.Load,
                // which reported the position of the first character of the element name).
                // Do NOT subtract 1 here.
                column = _lineInfo.LinePosition;
            }

            // Validate namespace on child elements (root is validated separately in Parse()).
            if (_reader.Prefix.Length != 0 ||
                (!string.IsNullOrEmpty(namespaceURI) && !string.Equals(namespaceURI, XMakeAttributes.defaultXmlNamespace, StringComparison.OrdinalIgnoreCase)))
            {
                ElementLocation location = ElementLocation.Create(_filePath, line, column);
                ProjectErrorUtilities.ThrowInvalidProject(
                    location,
                    "ProjectMustBeInMSBuildXmlNamespace",
                    XMakeAttributes.defaultXmlNamespace);
            }

            bool isEmpty = _reader.IsEmptyElement;

            ElementData data = new ElementData(name, namespaceURI, _filePath, line, column);
            data.IsSelfClosing = isEmpty;

            if (_reader.HasAttributes)
            {
                int attrCount = _reader.AttributeCount;
                AttributeData[]? attrs = null;
                int actualCount = 0;

                _reader.MoveToFirstAttribute();
                do
                {
                    // Skip xmlns declarations
                    if (_reader.Prefix == "xmlns" || (_reader.Prefix == string.Empty && _reader.LocalName == "xmlns"))
                    {
                        continue;
                    }

                    string attrName = _reader.LocalName;
                    string attrValue = _reader.Value;
                    int attrLine = 0;
                    int attrColumn = 0;
                    if (_lineInfo != null && _lineInfo.HasLineInfo())
                    {
                        attrLine = _lineInfo.LineNumber;
                        attrColumn = _lineInfo.LinePosition;
                    }

                    attrs ??= new AttributeData[attrCount];
                    attrs[actualCount++] = new AttributeData(attrName, attrValue, attrLine, attrColumn);
                } while (_reader.MoveToNextAttribute());

                _reader.MoveToElement(); // Move back to element

                // Trim array if xmlns declarations reduced the count
                if (attrs is not null)
                {
                    if (actualCount < attrs.Length)
                    {
                        Array.Resize(ref attrs, actualCount);
                    }

                    data.SetAttributeArray(attrs);
                }
            }

            return data;
        }

        /// <summary>
        /// Reads element data and validates that all attributes are in the allowed set.
        /// </summary>
        private ElementData ReadCurrentElementDataAndValidateAttributes(HashSet<string> validAttributes)
        {
            ElementData data = ReadCurrentElementData();

            var attributes = data.Attributes;
            for (int i = 0; i < attributes.Length; i++)
            {
                if (!validAttributes.Contains(attributes[i].Name))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(attributes[i].GetLocation(data.FilePath), "UnrecognizedAttribute", attributes[i].Name, data.Name);
                }
            }

            return data;
        }

        /// <summary>
        /// Reads the text content of the current element. The reader must be positioned on the element's start tag.
        /// After this call, the reader is positioned on the end element (or has advanced past an empty element).
        /// </summary>
        private void ReadTextContent(ElementData data)
        {
            if (_reader.IsEmptyElement)
            {
                _reader.Read(); // Advance past empty element
                return;
            }

            _reader.Read(); // Move past start element
            string? text = null;

            while (_reader.NodeType != XmlNodeType.EndElement)
            {
                if (_reader.NodeType == XmlNodeType.Text || _reader.NodeType == XmlNodeType.CDATA)
                {
                    text = (text == null) ? _reader.Value : text + _reader.Value;
                    _reader.Read();
                }
                else if (_reader.NodeType == XmlNodeType.Comment || _reader.NodeType == XmlNodeType.Whitespace || _reader.NodeType == XmlNodeType.SignificantWhitespace)
                {
                    _reader.Read();
                }
                else
                {
                    // Element has child elements — this means the content is mixed/complex
                    // For properties with expression like <X>$(Y)</X>, text is the inner content
                    // For elements with child elements, don't set TextContent
                    break;
                }
            }

            data.TextContent = text;

            // If we broke out because of child elements, skip to end
            while (_reader.NodeType != XmlNodeType.EndElement)
            {
                _reader.Skip();
            }

            _reader.Read(); // Consume end element
        }

        /// <summary>
        /// Reads the inner XML of the current element as raw text (used for UsingTaskBody and ProjectExtensions).
        /// Applies the same normalization as GetXmlNodeInnerContents: if the content is pure CDATA or
        /// text with only XML comments, returns the text value (CDATA unwrapped, comments stripped).
        /// Otherwise, returns the raw InnerXml to preserve embedded XML elements.
        /// </summary>
        private void ReadInnerXmlContent(ElementData data)
        {
            if (_reader.IsEmptyElement)
            {
                _reader.Read();
                return;
            }

            string innerXml = _reader.ReadInnerXml();

            // Apply GetXmlNodeInnerContents semantics:
            // 1. If it starts with CDATA, treat as text (unwrap CDATA)
            // 2. If it contains no '<' (no markup), treat as text (unescape)
            // 3. If the only markup is comments, treat as text (strip comments)
            // 4. Otherwise, it's genuine XML — keep InnerXml as-is
            int firstLessThan = innerXml.IndexOf('<');
            if (firstLessThan == -1)
            {
                // No markup — just text. Unescape XML entities.
                data.TextContent = System.Net.WebUtility.HtmlDecode(innerXml);
                return;
            }

            ReadOnlySpan<char> trimmed = innerXml.AsSpan().TrimStart();
            if (trimmed.StartsWith("<![CDATA[".AsSpan(), StringComparison.Ordinal))
            {
                // CDATA — extract the text content.
                // Find the matching ]]> and extract content between <![CDATA[ and ]]>
                int cdataStart = innerXml.IndexOf("<![CDATA[", StringComparison.Ordinal) + 9;
                int cdataEnd = innerXml.LastIndexOf("]]>", StringComparison.Ordinal);
                if (cdataEnd > cdataStart)
                {
                    data.TextContent = innerXml.Substring(cdataStart, cdataEnd - cdataStart);
                }
                else
                {
                    data.TextContent = innerXml;
                    data.IsInnerXml = true;
                }

                return;
            }

            if (Internal.Utilities.ContainsNoTagsOtherThanComments(innerXml, firstLessThan))
            {
                // Only comments — strip them and return text.
                // Replicate InnerText behavior: remove comment nodes, keep text.
                data.TextContent = StripXmlComments(innerXml);
                return;
            }

            // Genuine XML markup — keep as InnerXml.
            data.TextContent = innerXml;
            data.IsInnerXml = true;
        }

        /// <summary>
        /// Strips XML comment nodes from a string, returning only non-comment text content.
        /// </summary>
        private static string StripXmlComments(string xml)
        {
            var sb = new System.Text.StringBuilder(xml.Length);
            int i = 0;
            while (i < xml.Length)
            {
                if (i + 3 < xml.Length && xml[i] == '<' && xml[i + 1] == '!' && xml[i + 2] == '-' && xml[i + 3] == '-')
                {
                    // Skip comment
                    int end = xml.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    if (end >= 0)
                    {
                        i = end + 3;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    sb.Append(xml[i]);
                    i++;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Normalizes self-closing tags by adding a space before "/>", matching DOM XmlWriter behavior.
        /// e.g., &lt;D/&gt; becomes &lt;D /&gt;
        /// </summary>
        private static string NormalizeSelfClosingTags(string xml)
        {
            // Fast path: no self-closing tags
            if (!xml.Contains("/>"))
            {
                return xml;
            }

            var sb = new System.Text.StringBuilder(xml.Length + 8);
            for (int i = 0; i < xml.Length; i++)
            {
                if (xml[i] == '/' && i + 1 < xml.Length && xml[i + 1] == '>' && i > 0 && xml[i - 1] != ' ')
                {
                    sb.Append(' ');
                }

                sb.Append(xml[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Skips past the current element (from start to end, including all children).
        /// The reader must be positioned on a start element or the data must have been read already.
        /// After this call, the reader is positioned after the end element.
        /// </summary>
        private void SkipToEndElement()
        {
            if (_reader.IsEmptyElement)
            {
                _reader.Read();
                return;
            }

            if (_reader.NodeType == XmlNodeType.Element)
            {
                _reader.Skip();
            }
            else
            {
                // We're inside the element already (past start tag), skip to end
                while (_reader.NodeType != XmlNodeType.EndElement)
                {
                    _reader.Read();
                }

                _reader.Read(); // consume end element
            }
        }

        /// <summary>
        /// Verifies that the element has no child elements (used for leaf elements).
        /// </summary>
        private void VerifyNoChildElements(ElementData data)
        {
            if (_reader.IsEmptyElement)
            {
                return;
            }

            // Read into the element content and verify no child elements or non-whitespace text.
            _reader.Read();
            while (_reader.NodeType != XmlNodeType.EndElement)
            {
                if (_reader.NodeType == XmlNodeType.Element)
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement(_reader.LocalName, data.Name, data.Location);
                }
                else if (_reader.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(_reader.Value))
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElement("#text", data.Name, data.Location);
                }
                else
                {
                    _reader.Read();
                }
            }
        }

        /// <summary>
        /// Verifies the element name is a valid MSBuild element name.
        /// </summary>
        private static void VerifyValidElementName(string name, ElementLocation location)
        {
            if (!XmlUtilities.IsValidElementName(name))
            {
                ProjectErrorUtilities.ThrowInvalidProject(location, "NameInvalid", name, string.Empty);
            }
        }

        /// <summary>
        /// Throws an InvalidProjectFileException if the reader is positioned on a non-whitespace text node.
        /// This matches the DOM-based XmlElementChildIterator behavior which rejects text content
        /// in container elements like PropertyGroup and ItemGroup.
        /// </summary>
        private void ThrowIfNonWhitespaceText(ElementData parentData)
        {
            if (_reader.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(_reader.Value))
            {
                ProjectXmlUtilities.ThrowProjectInvalidChildElement("#text", parentData.Name, parentData.Location);
            }
        }

        #endregion
    }
}
