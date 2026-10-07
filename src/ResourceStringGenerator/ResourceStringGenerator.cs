// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Build.ResourceStringGenerator;

/// <summary>
///  Generates lazy resource-string catalogs from explicitly configured neutral RESX additional files.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ResourceStringGenerator : IIncrementalGenerator
{
    private const string MetadataPrefix = "build_metadata.AdditionalFiles.";
    private const string ResourceStringType = "global::Microsoft.Build.Framework.Resources.ResourceString";

    private static readonly DiagnosticDescriptor s_invalidResx = new(
        "MSBRSG001",
        "Invalid resource file",
        "Resource file '{0}' could not be read: {1}",
        "ResourceStringGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_invalidConfiguration = new(
        "MSBRSG002",
        "Invalid resource catalog configuration",
        "Resource file '{0}' has invalid resource catalog configuration: {1}",
        "ResourceStringGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_duplicateKey = new(
        "MSBRSG003",
        "Duplicate resource key",
        "Resource key '{0}' is included more than once in catalog '{1}'",
        "ResourceStringGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_identifierCollision = new(
        "MSBRSG004",
        "Resource identifier collision",
        "Resource keys '{0}' and '{1}' both generate the C# identifier '{2}' in catalog '{3}'",
        "ResourceStringGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_invalidKey = new(
        "MSBRSG005",
        "Invalid resource key",
        "Resource file '{0}' contains a data element without a non-empty name",
        "ResourceStringGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_generatedMemberCollision = new(
        "MSBRSG006",
        "Resource identifier collision",
        "Resource key '{0}' generates the C# identifier '{1}', which is reserved by catalog '{2}'",
        "ResourceStringGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    ///  Initializes the incremental generator.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ResourceFile?> resourceFiles = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, cancellationToken) => ParseResourceFile(
                pair.Left,
                pair.Right.GetOptions(pair.Left),
                cancellationToken));

        context.RegisterSourceOutput(
            resourceFiles.Collect(),
            static (productionContext, files) => GenerateCatalogs(productionContext, files));
    }

    private static ResourceFile? ParseResourceFile(
        AdditionalText additionalText,
        AnalyzerConfigOptions options,
        CancellationToken cancellationToken)
    {
        bool hasNamespace = TryGetMetadata(options, "ResourceCatalogNamespace", out string? catalogNamespace);
        bool hasClass = TryGetMetadata(options, "ResourceCatalogClass", out string? catalogClass);
        bool hasAccessor = TryGetMetadata(options, "ResourceCatalogResourceAccessorType", out string? accessorType);
        bool hasResourceManager = TryGetMetadata(
            options,
            "ResourceCatalogResourceManagerExpression",
            out string? resourceManagerExpression);
        bool hasCulture = TryGetMetadata(options, "ResourceCatalogCultureExpression", out string? cultureExpression);
        bool hasHelpKeywordPrefix = TryGetMetadata(options, "ResourceCatalogHelpKeywordPrefix", out string? helpKeywordPrefix);

        if (!hasNamespace && !hasClass && !hasAccessor && !hasResourceManager && !hasCulture && !hasHelpKeywordPrefix)
        {
            return null;
        }

        SourceText? sourceText = additionalText.GetText(cancellationToken);
        Location fileLocation = CreateLocation(additionalText.Path, sourceText, lineNumber: 0, key: null);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (!hasNamespace || string.IsNullOrWhiteSpace(catalogNamespace))
        {
            diagnostics.Add(Diagnostic.Create(
                s_invalidConfiguration,
                fileLocation,
                additionalText.Path,
                "ResourceCatalogNamespace is required"));
        }
        else if (SyntaxFactory.ParseName(catalogNamespace!).ContainsDiagnostics)
        {
            diagnostics.Add(Diagnostic.Create(
                s_invalidConfiguration,
                fileLocation,
                additionalText.Path,
                $"'{catalogNamespace}' is not a valid C# namespace"));
        }

        if (!hasClass || string.IsNullOrWhiteSpace(catalogClass) || !SyntaxFacts.IsValidIdentifier(catalogClass))
        {
            diagnostics.Add(Diagnostic.Create(
                s_invalidConfiguration,
                fileLocation,
                additionalText.Path,
                "ResourceCatalogClass must be a valid C# identifier"));
        }

        if (hasAccessor == hasResourceManager)
        {
            diagnostics.Add(Diagnostic.Create(
                s_invalidConfiguration,
                fileLocation,
                additionalText.Path,
                "specify exactly one of ResourceCatalogResourceAccessorType or ResourceCatalogResourceManagerExpression"));
        }

        if (hasCulture && !hasResourceManager)
        {
            diagnostics.Add(Diagnostic.Create(
                s_invalidConfiguration,
                fileLocation,
                additionalText.Path,
                "ResourceCatalogCultureExpression is supported only with ResourceCatalogResourceManagerExpression"));
        }

        if (sourceText is null)
        {
            diagnostics.Add(Diagnostic.Create(
                s_invalidResx,
                fileLocation,
                additionalText.Path,
                "the compiler did not provide the file contents"));
            return new ResourceFile(
                additionalText.Path,
                catalogNamespace,
                catalogClass,
                null,
                null,
                helpKeywordPrefix,
                [],
                diagnostics.ToImmutable());
        }

        var entries = ImmutableArray.CreateBuilder<ResourceEntry>();

        try
        {
            using var stringReader = new StringReader(sourceText.ToString());
            using XmlReader reader = XmlReader.Create(
                stringReader,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "data")
                {
                    continue;
                }

                string? type = reader.GetAttribute("type");
                string? mimeType = reader.GetAttribute("mimetype");
                if (type is not null || mimeType is not null)
                {
                    continue;
                }

                string? key = reader.GetAttribute("name");
                int lineNumber = reader is IXmlLineInfo lineInfo && lineInfo.HasLineInfo() ? lineInfo.LineNumber - 1 : 0;
                Location location = CreateLocation(additionalText.Path, sourceText, lineNumber, key);

                if (string.IsNullOrEmpty(key))
                {
                    diagnostics.Add(Diagnostic.Create(s_invalidKey, location, additionalText.Path));
                    continue;
                }

                entries.Add(new ResourceEntry(key, CreateIdentifier(key), location));
            }
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            diagnostics.Add(Diagnostic.Create(s_invalidResx, fileLocation, additionalText.Path, ex.Message));
        }

        string? effectiveResourceManager = hasAccessor ? $"{accessorType}.ResourceManager" : resourceManagerExpression;
        string? effectiveCulture = hasAccessor ? $"{accessorType}.Culture" : cultureExpression;

        return new ResourceFile(
            additionalText.Path,
            catalogNamespace,
            catalogClass,
            effectiveResourceManager,
            effectiveCulture,
            hasHelpKeywordPrefix ? helpKeywordPrefix : "MSBuild.",
            entries.ToImmutable(),
            diagnostics.ToImmutable());
    }

    private static void GenerateCatalogs(SourceProductionContext context, ImmutableArray<ResourceFile?> resourceFiles)
    {
        var catalogs = new Dictionary<string, List<ResourceFile>>(StringComparer.Ordinal);

        foreach (ResourceFile? resourceFile in resourceFiles)
        {
            if (resourceFile is null)
            {
                continue;
            }

            foreach (Diagnostic diagnostic in resourceFile.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }

            if (!resourceFile.CanGenerate)
            {
                continue;
            }

            string catalogKey = resourceFile.CatalogNamespace + "\0" + resourceFile.CatalogClass;
            if (!catalogs.TryGetValue(catalogKey, out List<ResourceFile>? files))
            {
                files = [];
                catalogs.Add(catalogKey, files);
            }

            files.Add(resourceFile);
        }

        foreach (List<ResourceFile> files in catalogs.Values)
        {
            files.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
            GenerateCatalog(context, files);
        }
    }

    private static void GenerateCatalog(SourceProductionContext context, List<ResourceFile> files)
    {
        ResourceFile firstFile = files[0];
        string catalogDisplayName = firstFile.CatalogNamespace + "." + firstFile.CatalogClass;
        var keys = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal);
        var identifiers = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal);
        var generatedMembers = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal);
        var reservedMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "GetResourceString",
        };
        var generatedEntries = new List<GeneratedEntry>();

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            reservedMembers.Add("s_resourceManager" + fileIndex);
            if (files[fileIndex].CultureExpression is not null)
            {
                reservedMembers.Add("s_getCulture" + fileIndex);
            }
        }

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            ResourceFile file = files[fileIndex];
            foreach (ResourceEntry entry in file.Entries)
            {
                if (keys.ContainsKey(entry.Key))
                {
                    context.ReportDiagnostic(Diagnostic.Create(s_duplicateKey, entry.Location, entry.Key, catalogDisplayName));
                    continue;
                }

                keys.Add(entry.Key, entry);

                if (identifiers.TryGetValue(entry.Identifier, out ResourceEntry? existingEntry))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        s_identifierCollision,
                        entry.Location,
                        existingEntry.Key,
                        entry.Key,
                        entry.Identifier,
                        catalogDisplayName));
                    continue;
                }

                string propertyIdentifier = entry.Identifier.TrimStart('@');
                string fieldIdentifier = "s_" + propertyIdentifier;

                if (reservedMembers.Contains(propertyIdentifier))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        s_generatedMemberCollision,
                        Location.None,
                        entry.Key,
                        propertyIdentifier,
                        catalogDisplayName));
                    continue;
                }

                if (reservedMembers.Contains(fieldIdentifier))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        s_generatedMemberCollision,
                        Location.None,
                        entry.Key,
                        fieldIdentifier,
                        catalogDisplayName));
                    continue;
                }

                if (generatedMembers.TryGetValue(propertyIdentifier, out ResourceEntry? existingMemberEntry))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        s_identifierCollision,
                        Location.None,
                        existingMemberEntry.Key,
                        entry.Key,
                        propertyIdentifier,
                        catalogDisplayName));
                    continue;
                }

                if (generatedMembers.TryGetValue(fieldIdentifier, out existingMemberEntry))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        s_identifierCollision,
                        Location.None,
                        existingMemberEntry.Key,
                        entry.Key,
                        fieldIdentifier,
                        catalogDisplayName));
                    continue;
                }

                identifiers.Add(entry.Identifier, entry);
                generatedMembers.Add(propertyIdentifier, entry);
                generatedMembers.Add(fieldIdentifier, entry);
                generatedEntries.Add(new GeneratedEntry(entry, fileIndex));
            }
        }

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.Append("namespace ").Append(firstFile.CatalogNamespace).AppendLine(";");
        source.AppendLine();
        source.Append("internal static partial class ").Append(firstFile.CatalogClass).AppendLine();
        source.AppendLine("{");

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            ResourceFile file = files[fileIndex];
            source.Append("    private static readonly global::System.Resources.ResourceManager s_resourceManager")
                .Append(fileIndex)
                .Append(" = ")
                .Append(file.ResourceManagerExpression)
                .AppendLine(";");

            if (file.CultureExpression is not null)
            {
                source.Append("    private static readonly global::System.Func<global::System.Globalization.CultureInfo?> s_getCulture")
                    .Append(fileIndex)
                    .Append(" = static () => ")
                    .Append(file.CultureExpression)
                    .AppendLine(";");
            }
        }

        if (files.Count > 0 && generatedEntries.Count > 0)
        {
            source.AppendLine();
        }

        source.Append("    public static ")
            .Append(ResourceStringType)
            .AppendLine(" GetResourceString(string name)");
        source.AppendLine("        => name switch");
        source.AppendLine("        {");

        foreach (GeneratedEntry generatedEntry in generatedEntries)
        {
            source.Append("            ")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(generatedEntry.Entry.Key, quote: true))
                .Append(" => ")
                .Append(generatedEntry.Entry.Identifier)
                .AppendLine(",");
        }

        source.AppendLine("            _ => throw new global::System.ArgumentException($\"Unknown resource key '{name}'.\", nameof(name)),");
        source.AppendLine("        };");

        if (generatedEntries.Count > 0)
        {
            source.AppendLine();
        }

        for (int index = 0; index < generatedEntries.Count; index++)
        {
            GeneratedEntry generatedEntry = generatedEntries[index];
            ResourceFile file = files[generatedEntry.FileIndex];
            string fieldIdentifier = "s_" + generatedEntry.Entry.Identifier.TrimStart('@');

            source.Append("    private static ")
                .Append(ResourceStringType)
                .Append("? ")
                .Append(fieldIdentifier)
                .AppendLine(";");
            source.AppendLine();
            source.Append("    public static ")
                .Append(ResourceStringType)
                .Append(' ')
                .Append(generatedEntry.Entry.Identifier)
                .AppendLine();
            source.Append("        => ")
                .Append(fieldIdentifier)
                .Append(" ??= new ")
                .Append(ResourceStringType)
                .Append("(s_resourceManager")
                .Append(generatedEntry.FileIndex)
                .Append(", ");

            if (file.CultureExpression is null)
            {
                source.Append("null");
            }
            else
            {
                source.Append("s_getCulture").Append(generatedEntry.FileIndex);
            }

            source.Append(", ")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(generatedEntry.Entry.Key, quote: true))
                .Append(", ")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(file.HelpKeywordPrefix, quote: true))
                .AppendLine(");");

            if (index < generatedEntries.Count - 1)
            {
                source.AppendLine();
            }
        }

        source.AppendLine("}");

        string hintName = CreateHintName(catalogDisplayName);
        context.AddSource(hintName, SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static string CreateIdentifier(string key)
    {
        var identifier = new StringBuilder(key.Length + 1);

        for (int index = 0; index < key.Length; index++)
        {
            char character = key[index];

            if (index == 0 && !SyntaxFacts.IsIdentifierStartCharacter(character))
            {
                identifier.Append('_');
            }

            identifier.Append(SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_');
        }

        if (identifier.Length == 0 || !SyntaxFacts.IsIdentifierStartCharacter(identifier[0]))
        {
            identifier.Insert(0, '_');
        }

        string result = identifier.ToString();
        return SyntaxFacts.GetKeywordKind(result) == SyntaxKind.None ? result : "@" + result;
    }

    private static string CreateHintName(string catalogDisplayName)
    {
        var hintName = new StringBuilder(catalogDisplayName.Length + 32);

        foreach (char character in catalogDisplayName)
        {
            hintName.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        hintName
            .Append('_')
            .Append(ComputeStableHash(catalogDisplayName).ToString("X8"))
            .Append(".ResourceStrings.g.cs");
        return hintName.ToString();
    }

    private static uint ComputeStableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        uint hash = offsetBasis;

        foreach (char character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private static Location CreateLocation(string path, SourceText? sourceText, int lineNumber, string? key)
    {
        if (sourceText is null || sourceText.Lines.Count == 0)
        {
            return Location.Create(path, new TextSpan(0, 0), new LinePositionSpan(default, default));
        }

        if (lineNumber < 0 || lineNumber >= sourceText.Lines.Count)
        {
            lineNumber = 0;
        }

        TextLine line = sourceText.Lines[lineNumber];
        int column = 0;
        int length = 0;

        if (!string.IsNullOrEmpty(key))
        {
            string lineText = line.ToString();
            int keyIndex = lineText.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                column = keyIndex;
                length = key!.Length;
            }
        }

        var span = new TextSpan(line.Start + column, length);
        var lineSpan = new LinePositionSpan(
            new LinePosition(lineNumber, column),
            new LinePosition(lineNumber, column + length));
        return Location.Create(path, span, lineSpan);
    }

    private static bool TryGetMetadata(AnalyzerConfigOptions options, string name, out string? value)
    {
        if (options.TryGetValue(MetadataPrefix + name, out string? metadataValue) &&
            !string.IsNullOrWhiteSpace(metadataValue))
        {
            value = metadataValue.Trim();
            return true;
        }

        value = null;
        return false;
    }

    private sealed class ResourceFile(
        string path,
        string? catalogNamespace,
        string? catalogClass,
        string? resourceManagerExpression,
        string? cultureExpression,
        string? helpKeywordPrefix,
        ImmutableArray<ResourceEntry> entries,
        ImmutableArray<Diagnostic> diagnostics)
    {
        public string Path { get; } = path;

        public string CatalogNamespace { get; } = catalogNamespace ?? string.Empty;

        public string CatalogClass { get; } = catalogClass ?? string.Empty;

        public string? ResourceManagerExpression { get; } = resourceManagerExpression;

        public string? CultureExpression { get; } = cultureExpression;

        public string HelpKeywordPrefix { get; } = helpKeywordPrefix ?? "MSBuild.";

        public ImmutableArray<ResourceEntry> Entries { get; } = entries;

        public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;

        public bool CanGenerate =>
            Diagnostics.IsEmpty &&
            CatalogNamespace.Length > 0 &&
            CatalogClass.Length > 0 &&
            ResourceManagerExpression is not null;
    }

    private sealed class ResourceEntry(string key, string identifier, Location location)
    {
        public string Key { get; } = key;

        public string Identifier { get; } = identifier;

        public Location Location { get; } = location;
    }

    private readonly struct GeneratedEntry(ResourceEntry entry, int fileIndex)
    {
        public ResourceEntry Entry { get; } = entry;

        public int FileIndex { get; } = fileIndex;
    }
}
