// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class MetadataExpansion_Tests
{
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
    public void InvalidPathInDirectMetadata()
    {
        const string Content = """
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include=':|?*'>
                        <m>%(FullPath)</m>
                    </x>
                </ItemGroup>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectContentUsingBuildManagerExpectResult(Content, BuildResultCode.Failure);

        log.AssertLogContains("MSB4248");
    }

    [LongPathSupportDisabledFact(fullFrameworkOnly: true, additionalMessage: "new enough dotnet.exe transparently opts into long paths")]
    public void PathTooLongInDirectMetadata()
    {
        string content = $"""
            <Project DefaultTargets='Build'>
                <ItemGroup>
                    <x Include='{new string('x', 250)}'>
                        <m>%(FullPath)</m>
                    </x>
                </ItemGroup>
            </Project>
            """;

        MockLogger log = Helpers.BuildProjectContentUsingBuildManagerExpectResult(content, BuildResultCode.Failure);

        log.AssertLogContains("MSB4248");
    }

    [Fact]
    public void DirectItemMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <ItemGroup>
                    <Foo Include=`Foo`>
                        <SENSITIVE>X</SENSITIVE>
                    </Foo>
                </ItemGroup>
                <Target Name=`Build`>
                    <Message Importance=`high` Text=`QualifiedNotMatchCase %(Foo.FileName)=%(Foo.sensitive)`/>
                    <Message Importance=`high` Text=`QualifiedMatchCase %(Foo.FileName)=%(Foo.SENSITIVE)`/>

                    <Message Importance=`high` Text=`UnqualifiedNotMatchCase %(Foo.FileName)=%(sensitive)`/>
                    <Message Importance=`high` Text=`UnqualifiedMatchCase %(Foo.FileName)=%(SENSITIVE)`/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("QualifiedNotMatchCase Foo=X");
        log.AssertLogContains("QualifiedMatchCase Foo=X");
        log.AssertLogContains("UnqualifiedNotMatchCase Foo=X");
        log.AssertLogContains("UnqualifiedMatchCase Foo=X");
    }

    [Fact]
    public void ItemDefinitionGroupMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <ItemDefinitionGroup>
                    <Foo>
                        <SENSITIVE>X</SENSITIVE>
                    </Foo>
                </ItemDefinitionGroup>
                <ItemGroup>
                    <Foo Include=`Foo`/>
                </ItemGroup>
                <Target Name=`Build`>
                    <Message Importance=`high` Text=`QualifiedNotMatchCase %(Foo.FileName)=%(Foo.sensitive)`/>
                    <Message Importance=`high` Text=`QualifiedMatchCase %(Foo.FileName)=%(Foo.SENSITIVE)`/>

                    <Message Importance=`high` Text=`UnqualifiedNotMatchCase %(Foo.FileName)=%(sensitive)`/>
                    <Message Importance=`high` Text=`UnqualifiedMatchCase %(Foo.FileName)=%(SENSITIVE)`/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("QualifiedNotMatchCase Foo=X");
        log.AssertLogContains("QualifiedMatchCase Foo=X");
        log.AssertLogContains("UnqualifiedNotMatchCase Foo=X");
        log.AssertLogContains("UnqualifiedMatchCase Foo=X");
    }

    [Fact]
    public void WellKnownMetadataReferenceShouldBeCaseInsensitive()
    {
        MockLogger log = Helpers.BuildProjectWithNewOMExpectSuccess("""
            <Project>
                <ItemGroup>
                    <Foo Include=`Foo`/>
                </ItemGroup>
                <Target Name=`Build`>
                    <Message Importance=`high` Text=`QualifiedNotMatchCase %(Foo.Identity)=%(Foo.FILENAME)`/>
                    <Message Importance=`high` Text=`QualifiedMatchCase %(Foo.Identity)=%(Foo.FileName)`/>

                    <Message Importance=`high` Text=`UnqualifiedNotMatchCase %(Foo.Identity)=%(FILENAME)`/>
                    <Message Importance=`high` Text=`UnqualifiedMatchCase %(Foo.Identity)=%(FileName)`/>
                </Target>
            </Project>
            """);

        log.AssertLogContains("QualifiedNotMatchCase Foo=Foo");
        log.AssertLogContains("QualifiedMatchCase Foo=Foo");
        log.AssertLogContains("UnqualifiedNotMatchCase Foo=Foo");
        log.AssertLogContains("UnqualifiedMatchCase Foo=Foo");
    }

    /// <summary>
    ///  Parity tests for the hand-written metadata scanner. These pin the exact expanded result for
    ///  whitespace handling, malformed references, nested references, qualified vs. unqualified
    ///  names, and missing metadata so future edits to the scanner cannot silently regress them.
    /// </summary>
    [Theory]
    [MemberData(nameof(MetadataScannerEdgeCasesData))]
    public void ExpandMetadata_ScannerEdgeCases(string input, string expected)
        => ExpandMetadata(input, SimpleMetadata())
            .ShouldBe(expected);

    public static TheoryData<string, string> MetadataScannerEdgeCasesData => new()
    {
        // Simple expansion, unqualified and qualified.
        { "%(Culture)", "en-US" },
        { "%(Foo)", "Bar" },
        { "%(Compile.Link)", "Link.cs" },

        // Whitespace around the parentheses and the dot separator is allowed.
        { "%( Culture )", "en-US" },
        { "%( Compile . Link )", "Link.cs" },

        // Missing metadata expands to empty; a missing qualifier does not fall back to the unqualified key.
        { "%(DoesNotExist)", "" },
        { "%(Other.Foo)", "" },

        // Malformed references are emitted verbatim.
        { "%(", "%(" },
        { "%()", "%()" },
        { "%( )", "%( )" },
        { "%(.x)", "%(.x)" },

        // The outer reference is not closed by ')', so only the inner reference expands.
        { "%(Culture%(Foo))", "%(CultureBar)" },

        // Mixed with surrounding literal text and adjacent references.
        { "prefix_%(Culture)_suffix", "prefix_en-US_suffix" },
        { "%(Culture)%(Foo)", "en-USBar" },
    };

    [Theory]
    [MemberData(nameof(MetadataNoExpansionReturnsOriginalStringData))]
    internal void ExpandMetadata_NoExpansionReturnsOriginalString(string input, ExpanderOptions options)
        => ExpandMetadata(input, SimpleMetadata(), options)
            .ShouldBeSameAs(input);

    public static IEnumerable<object[]> MetadataNoExpansionReturnsOriginalStringData =>
    [
        ["%(", ExpanderOptions.ExpandMetadata],
        ["%(Culture)", ExpanderOptions.ExpandBuiltInMetadata],
        ["%(Filename)", ExpanderOptions.ExpandCustomMetadata],
    ];

    /// <summary>
    ///  Parity tests for metadata expansion in the gaps between (and within the separators of) item
    ///  vector expressions. Items are intentionally left unexpanded (ExpandMetadata only) so the
    ///  assertions isolate the gap/separator boundary handling in ScanAndExpandMetadataInGaps,
    ///  including the case where "@(" appears but does not form a well-formed item vector.
    /// </summary>
    [Theory]
    [MemberData(nameof(MetadataItemVectorGapsAndSeparatorsData))]
    public void ExpandMetadata_ItemVectorGapsAndSeparators(string input, string expected)
        => ExpandMetadata(input, SimpleMetadata())
            .ShouldBe(expected);

    public static TheoryData<string, string> MetadataItemVectorGapsAndSeparatorsData => new()
    {
        // Metadata after, before, and between item vectors.
        { "@(Compile)%(Culture)", "@(Compile)en-US" },
        { "%(Culture)@(Compile)", "en-US@(Compile)" },
        { "@(A)%(Culture)@(B)", "@(A)en-US@(B)" },

        // A lone item vector has no gaps and is returned unchanged, even with embedded metadata in a transform.
        { "@(Compile)", "@(Compile)" },
        { "@(Compile->'%(Filename)')", "@(Compile->'%(Filename)')" },

        // Metadata embedded in an item vector's separator is expanded in place.
        { "@(Compile, '%(Culture)')", "@(Compile, 'en-US')" },

        // "@(" that does not form a valid item vector still has its surrounding metadata expanded.
        { "%(Culture)@(", "en-US@(" },
    };

    /// <summary>
    ///  Verifies the built-in vs. custom metadata gating in the scanner: a reference is expanded only
    ///  when the matching <see cref="ExpanderOptions"/> flag is set; otherwise it is emitted verbatim.
    /// </summary>
    /// <remarks>
    ///  Declared <c>internal</c> because <see cref="ExpanderOptions"/> is internal; this assembly is
    ///  configured to discover non-public test methods.
    /// </remarks>
    [Theory]
    [MemberData(nameof(MetadataBuiltInVsCustomGatingData))]
    internal void ExpandMetadata_BuiltInVsCustomGating(string input, ExpanderOptions options, string expected)
        => ExpandMetadata(input, SimpleMetadata(), options)
            .ShouldBe(expected);

    public static IEnumerable<object[]> MetadataBuiltInVsCustomGatingData =>
    [
        // Custom metadata (Culture) only expands with ExpandCustomMetadata.
        ["%(Culture)", ExpanderOptions.ExpandCustomMetadata, "en-US"],
        ["%(Culture)", ExpanderOptions.ExpandBuiltInMetadata, "%(Culture)"],

        // Built-in metadata (Filename) only expands with ExpandBuiltInMetadata.
        ["%(Filename)", ExpanderOptions.ExpandBuiltInMetadata, "App"],
        ["%(Filename)", ExpanderOptions.ExpandCustomMetadata, "%(Filename)"],
    ];

    private static IMetadataTable SimpleMetadata()
        => Metadata(
            ("Culture", "en-US"),
            ("Foo", "Bar"),
            ("Compile.Link", "Link.cs"),
            ("Filename", "App"));
}
