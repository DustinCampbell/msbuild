// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public sealed class AssemblyNameEx_Tests
{
    /// <summary>
    ///  Delegate defines a function that produces an AssemblyNameExtension from a string.
    /// </summary>
    private delegate AssemblyNameExtension ProduceAssemblyNameEx(string name);

    private static readonly ImmutableArray<string> s_assemblyStrings =
    [
        "System.Xml, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Xml, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XML, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XM, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XM, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XM, Version=2.0.0.0, Culture=neutral",
        "System.XM, Version=2.0.0.0",
        "System.XM, PublicKeyToken=b03f5f7f11d50a3a",
        "System.XM, Culture=neutral",
        "System.Xml",
        "System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Drawing",
    ];

    private static readonly ImmutableArray<string> s_assembliesForPartialMatch =
    [
        "System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes",
        "System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No",
        "System.Xml, Culture=en, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Xml, Version=10.0.0.0, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Xml, Version=10.0.0.0, Culture=en",
    ];

    /// <summary>   
    /// All the different ways the same assembly name can be represented.
    /// </summary>
    private static readonly ImmutableArray<ProduceAssemblyNameEx> s_producers =
    [
        ProduceAsString,
        ProduceAsAssemblyName,
        ProduceAsBoth,
        ProduceAsLowerString,
        ProduceAsLowerAssemblyName,
        ProduceAsLowerBoth,
    ];

    private static AssemblyNameExtension ProduceAsString(string name)
        => new(name);

    private static AssemblyNameExtension ProduceAsLowerString(string name)
        => new(name.ToLower());

    private static AssemblyNameExtension ProduceAsAssemblyName(string name)
        => new(new AssemblyName(name));

    private static AssemblyNameExtension ProduceAsLowerAssemblyName(string name)
        => new(new AssemblyName(name.ToLower()));

    private static AssemblyNameExtension ProduceAsBoth(string name)
    {
        AssemblyNameExtension result = new(new AssemblyName(name));

        // Force the string version to be produced too.
        _ = result.FullName;

        return result;
    }

    private static AssemblyNameExtension ProduceAsLowerBoth(string name)
        => ProduceAsBoth(name.ToLower());

    [Fact]
    public void CompareBaseNameTo()
    {
        // For each pair of assembly strings...
        foreach (string assemblyString1 in s_assemblyStrings)
        {
            var assemblyName1 = new AssemblyName(assemblyString1);

            foreach (string assemblyString2 in s_assemblyStrings)
            {
                var assemblyName2 = new AssemblyName(assemblyString2);

                // ...and for each pair of production methods...
                foreach (ProduceAssemblyNameEx produce1 in s_producers)
                {
                    foreach (ProduceAssemblyNameEx produce2 in s_producers)
                    {
                        AssemblyNameExtension a1 = produce1(assemblyString1);
                        AssemblyNameExtension a2 = produce2(assemblyString2);

                        int result = a1.CompareBaseNameTo(a2);
                        int resultBaseline = string.Compare(assemblyName1.Name, assemblyName2.Name, StringComparison.OrdinalIgnoreCase);

                        result.ShouldBe(resultBaseline);
                    }
                }
            }
        }
    }

    [Fact]
    public void CompareTo()
    {
        // For each pair of assembly strings...
        foreach (string assemblyString1 in s_assemblyStrings)
        {
            foreach (string assemblyString2 in s_assemblyStrings)
            {
                // ...and for each pair of production methods...
                foreach (ProduceAssemblyNameEx produce1 in s_producers)
                {
                    foreach (ProduceAssemblyNameEx produce2 in s_producers)
                    {
                        AssemblyNameExtension a1 = produce1(assemblyString1);
                        AssemblyNameExtension a2 = produce2(assemblyString2);

                        int result = a1.CompareTo(a2);

                        if (a1.Equals(a2))
                        {
                            result.ShouldBe(0);
                        }

                        if (a1.CompareBaseNameTo(a2) != 0)
                        {
                            result.ShouldBe(a1.CompareBaseNameTo(a2));
                        }

                        if (a1.CompareBaseNameTo(a2) == 0 // Only check version if basenames match
                            && a1.Version != a2.Version)
                        {
                            if (a1.Version is null)
                            {
                                // Expect -1 if a1.Version is null and the baseNames match
                                result.ShouldBe(-1);
                            }
                            else
                            {
                                result.ShouldBe(a1.Version.CompareTo(a2.Version));
                            }
                        }

                        int resultBaseline = string.Compare(a1.FullName, a2.FullName, StringComparison.OrdinalIgnoreCase);

                        // Only check to see if the result and the resultBaseline match when the result baseline is 0 and the result is not 0.
                        if (resultBaseline != result && resultBaseline == 0)
                        {
                            result.ShouldBe(resultBaseline);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void ExerciseMiscMethods()
    {
        AssemblyNameExtension a1 = s_producers[0](s_assemblyStrings[0]);

        var newVersion = new Version(1, 2);
        a1.ReplaceVersion(newVersion);
        a1.Version.ShouldBe(newVersion);

        _ = a1.ToString().ShouldNotBeNull();
    }

    [Fact]
    public void EscapeDisplayNameCharacters()
    {
        // Those characters are Equals(=), Comma(,), Quote("), Apostrophe('), Backslash(\).
        string displayName = @"Hello,""Don't"" eat the \CAT";
        AssemblyNameExtension.EscapeDisplayNameCharacters(displayName).ShouldBe(@"Hello\,\""Don\'t\"" eat the \\CAT", StringCompareShould.IgnoreCase);
    }

    [Fact]
    public void AreEquals()
    {
        // For each pair of assembly strings...
        foreach (string assemblyString1 in s_assemblyStrings)
        {
            foreach (string assemblyString2 in s_assemblyStrings)
            {
                // ...and for each pair of production methods...
                foreach (ProduceAssemblyNameEx produce1 in s_producers)
                {
                    foreach (ProduceAssemblyNameEx produce2 in s_producers)
                    {
                        AssemblyNameExtension a1 = produce1(assemblyString1);
                        AssemblyNameExtension a2 = produce2(assemblyString2);

                        // Baseline is a mismatch which is known to exercise
                        // the full code path.
                        AssemblyNameExtension a3 = ProduceAsAssemblyName(assemblyString1);
                        AssemblyNameExtension a4 = ProduceAsString(assemblyString2);

                        bool result = a1.Equals(a2);
                        bool resultBaseline = a3.Equals(a4);

                        result.ShouldBe(resultBaseline);
                    }
                }
            }
        }
    }

    [Fact]
    public void EqualsIgnoreVersion()
    {
        // For each pair of assembly strings...
        foreach (string assemblyString1 in s_assemblyStrings)
        {
            foreach (string assemblyString2 in s_assemblyStrings)
            {
                // ...and for each pair of production methods...
                foreach (ProduceAssemblyNameEx produce1 in s_producers)
                {
                    foreach (ProduceAssemblyNameEx produce2 in s_producers)
                    {
                        AssemblyNameExtension a1 = produce1(assemblyString1);
                        AssemblyNameExtension a2 = produce2(assemblyString2);

                        // Baseline is a mismatch which is known to exercise
                        // the full code path.
                        AssemblyNameExtension a3 = ProduceAsAssemblyName(assemblyString1);
                        AssemblyNameExtension a4 = ProduceAsString(assemblyString2);

                        bool result = a1.EqualsIgnoreVersion(a2);
                        bool resultBaseline = a3.EqualsIgnoreVersion(a4);

                        result.ShouldBe(resultBaseline);
                    }
                }
            }
        }
    }

    /// <summary>
    /// This repros a bug that was found while coding AssemblyNameExtension.
    /// </summary>
    [Fact]
    public void CompareBaseNameRealCase1()
    {
        AssemblyNameExtension a1 = ProduceAsBoth("System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        AssemblyNameExtension a2 = ProduceAsString("System.Drawing");

        // Base names should be equal.
        a1.CompareBaseNameTo(a2).ShouldBe(0);
    }

    [Fact]
    public void CreateAssemblyNameExtensionWithNoSimpleName()
        => Should.Throw<FileLoadException>(() =>
        {
            _ = new AssemblyNameExtension("Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", true);
        });

    [Fact]
    public void CreateAssemblyNameExtensionWithNoSimpleName2()
        => Should.Throw<FileLoadException>(() =>
        {
            var extension = new AssemblyNameExtension("Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            var extension2 = new AssemblyNameExtension("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
            _ = extension2.PartialNameCompare(extension);
        });

    [Theory]
    [InlineData("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", "A", "2.0.0.0", "en", true)]
    [InlineData("A, Version=2.0.0.0, PublicKeyToken=b03f5f7f11d50a3a", "A", "2.0.0.0", null, true)]
    [InlineData("A, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", "A", null, "en", true)]
    [InlineData("A, PublicKeyToken=b03f5f7f11d50a3a", "A", null, null, true)]
    [InlineData("A", "A", null, null, false)]
    public void CreateAssemblyNameWithNameAndVersionCulturePublicKey(
        string assemblyName,
        string expectedName,
        string? expectedVersion,
        string? expectedCulture,
        bool expectPublicKeyToken)
    {
        var extension = new AssemblyNameExtension(assemblyName);

        extension.Name.ShouldBe(expectedName);

        if (expectedVersion is not null)
        {
            extension.Version.ShouldBe(new Version(expectedVersion));
        }
        else
        {
            extension.Version.ShouldBeNull();
        }

        if (expectedCulture is not null)
        {
            extension.CultureInfo.ShouldBe(new CultureInfo(expectedCulture));
        }
        else
        {
            extension.CultureInfo.ShouldBeNull();
        }

        if (expectPublicKeyToken)
        {
            extension.FullName.ShouldContain("b03f5f7f11d50a3a");
        }
    }

    [Theory]
    [InlineData("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, ProcessorArchitecture=MSIL", true)]
    [InlineData("A, Version=2.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", false)]
    public void CreateAssemblyNameWithNameAndProcessorArchitecture(string assemblyName, bool expectProcessorArchitecture)
    {
        AssemblyNameExtension extension = new(assemblyName);

        extension.Name.ShouldBe("A");
        extension.Version.ShouldBe(new Version("2.0.0.0"));
        extension.CultureInfo.ShouldBe(new CultureInfo("en"));
        extension.FullName.ShouldContain("b03f5f7f11d50a3a");

        if (expectProcessorArchitecture)
        {
            extension.FullName.ShouldContain("MSIL");
        }

        extension.HasProcessorArchitectureInFusionName.ShouldBe(expectProcessorArchitecture);
    }

    /// <summary>
    /// Verify partial matching on the simple name works.
    /// </summary>
    [Fact]
    public void TestAssemblyPatialMatchSimpleName()
    {
        var assemblyNameToMatch = new AssemblyNameExtension("System.Xml");
        var assemblyNameToNotMatch = new AssemblyNameExtension("System.Xmla");

        foreach (string assembly in s_assembliesForPartialMatch)
        {
            var assemblyToCompare = new AssemblyNameExtension(assembly);

            Assert.True(assemblyNameToMatch.PartialNameCompare(assemblyToCompare));
            Assert.True(assemblyNameToMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName));
            Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
            Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName));
        }
    }

    /// <summary>
    /// Verify partial matching on the simple name and version.
    /// </summary>
    [Fact]
    public void TestAssemblyPatialMatchSimpleNameVersion()
    {
        var assemblyNameToMatchVersion = new AssemblyNameExtension("System.Xml, Version=10.0.0.0");
        var assemblyNameToNotMatch = new AssemblyNameExtension("System.Xml, Version=5.0.0.0");
        var assemblyMatchNoVersion = new AssemblyNameExtension("System.Xml");

        foreach (string assembly in s_assembliesForPartialMatch)
        {
            var assemblyToCompare = new AssemblyNameExtension(assembly);

            // If there is a version make sure the assembly name with the correct version matches
            // Make sure the assembly with the wrong version does not match
            if (assemblyToCompare.Version != null)
            {
                Assert.True(assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));

                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));

                // Matches because version is not specified
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));
            }
            else
            {
                // If there is no version make names with a version specified do not match
                Assert.False(assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToMatchVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));

                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));

                // Matches because version is not specified
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Version));
            }
        }
    }

    /// <summary>
    /// Verify partial matching on the simple name and culture.
    /// </summary>
    [Fact]
    public void TestAssemblyPatialMatchSimpleNameCulture()
    {
        var assemblyNameToMatchCulture = new AssemblyNameExtension("System.Xml, Culture=en");
        var assemblyNameToNotMatch = new AssemblyNameExtension("System.Xml, Culture=de-DE");
        var assemblyMatchNoVersion = new AssemblyNameExtension("System.Xml");

        foreach (string assembly in s_assembliesForPartialMatch)
        {
            var assemblyToCompare = new AssemblyNameExtension(assembly);

            // If there is a version make sure the assembly name with the correct culture matches
            // Make sure the assembly with the wrong culture does not match
            if (assemblyToCompare.CultureInfo != null)
            {
                Assert.True(assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));

                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));

                // Matches because culture is not specified
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));
            }
            else
            {
                // If there is no version make names with a culture specified do not match
                Assert.False(assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToMatchCulture.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));

                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));

                // Matches because culture is not specified
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.Culture));
            }
        }
    }

    /// <summary>
    /// Verify partial matching on the simple name and PublicKeyToken.
    /// </summary>
    [Fact]
    public void TestAssemblyPatialMatchSimpleNamePublicKeyToken()
    {
        var assemblyNameToMatchPublicToken = new AssemblyNameExtension("System.Xml, PublicKeyToken=b03f5f7f11d50a3a");
        var assemblyNameToNotMatch = new AssemblyNameExtension("System.Xml, PublicKeyToken=b03f5f7f11d50a3b");
        var assemblyMatchNoVersion = new AssemblyNameExtension("System.Xml");

        foreach (string assembly in s_assembliesForPartialMatch)
        {
            var assemblyToCompare = new AssemblyNameExtension(assembly);

            // If there is a version make sure the assembly name with the correct publicKeyToken matches
            // Make sure the assembly with the wrong publicKeyToken does not match
            if (assemblyToCompare.GetPublicKeyToken() != null)
            {
                Assert.True(assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));

                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));

                // Matches because publicKeyToken is not specified
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));
            }
            else
            {
                // If there is no version make names with a publicKeyToken specified do not match
                Assert.False(assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToMatchPublicToken.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));

                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));

                // Matches because publicKeyToken is not specified
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyMatchNoVersion.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName | PartialComparisonFlags.PublicKeyToken));
            }
        }
    }

    /// <summary>
    /// Verify partial matching on the simple name and retargetable.
    /// </summary>
    [Fact]
    public void TestAssemblyPartialMatchSimpleNameRetargetable()
    {
        var assemblyNameToMatchRetargetable = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");
        var assemblyNameToNotMatch = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");
        var assemblyMatchNoRetargetable = new AssemblyNameExtension("System.Xml");

        foreach (string assembly in s_assembliesForPartialMatch)
        {
            var assemblyToCompare = new AssemblyNameExtension(assembly);

            if (assemblyToCompare.FullName.Contains("Retargetable=Yes", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));

                Assert.True(assemblyToCompare.PartialNameCompare(assemblyNameToNotMatch));
                Assert.False(assemblyToCompare.PartialNameCompare(assemblyNameToNotMatch, PartialComparisonFlags.SimpleName, true));

                Assert.False(assemblyToCompare.PartialNameCompare(assemblyMatchNoRetargetable));
                Assert.False(assemblyToCompare.PartialNameCompare(assemblyMatchNoRetargetable, PartialComparisonFlags.SimpleName, true));

                Assert.True(assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare));
                Assert.False(assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));
            }
            else
            {
                Assert.False(assemblyNameToMatchRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));

                // Match because retargetable false is the same as no retargetable bit
                bool match = assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare);
                if (assemblyToCompare.FullName.Contains("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(match);
                }
                else
                {
                    Assert.False(match);
                }

                Assert.True(assemblyNameToNotMatch.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));

                Assert.True(assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare));
                Assert.True(assemblyMatchNoRetargetable.PartialNameCompare(assemblyToCompare, PartialComparisonFlags.SimpleName, true));
            }
        }
    }

    /// <summary>
    /// Make sure that our assemblyNameComparers correctly work.
    /// </summary>
    [Fact]
    public void VerifyAssemblyNameComparers()
    {
        var a = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");
        var b = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");
        var c = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes");

        var d = new AssemblyNameExtension("System.Xml, Version=9.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");
        var e = new AssemblyNameExtension("System.Xml, Version=11.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=No");

        AssemblyNameComparer.GenericComparer.Equals(a, b).ShouldBeTrue();
        AssemblyNameComparer.GenericComparer.Equals(a, d).ShouldBeFalse();

        AssemblyNameComparer.GenericComparerConsiderRetargetable.Equals(a, b).ShouldBeFalse();
        AssemblyNameComparer.GenericComparerConsiderRetargetable.Equals(a, c).ShouldBeTrue();
        AssemblyNameComparer.GenericComparerConsiderRetargetable.Equals(a, d).ShouldBeFalse();

        AssemblyNameComparer.Comparer.Compare(a, b).ShouldBe(0);
        AssemblyNameComparer.Comparer.Compare(a, d).ShouldBeGreaterThan(0);
        AssemblyNameComparer.Comparer.Compare(a, e).ShouldBeLessThan(0);

        AssemblyNameComparer.ComparerConsiderRetargetable.Compare(a, c).ShouldBe(0);
        AssemblyNameComparer.ComparerConsiderRetargetable.Compare(a, b).ShouldBeGreaterThan(0);
        AssemblyNameComparer.ComparerConsiderRetargetable.Compare(a, d).ShouldBeGreaterThan(0);
        AssemblyNameComparer.ComparerConsiderRetargetable.Compare(a, e).ShouldBeLessThan(0);
    }

    /// <summary>
    /// Make sure the reverse version comparer will compare the version in a way that would sort them in reverse order.
    /// </summary>
    [Fact]
    public void VerifyReverseVersionComparer()
    {
        var x = new AssemblyNameExtension("System, Version=2.0.0.0");
        var y = new AssemblyNameExtension("System, Version=1.0.0.0");
        var z = new AssemblyNameExtension("System, Version=2.0.0.0");
        var a = new AssemblyNameExtension("Zar, Version=3.0.0.0");

        AssemblyNameReverseVersionComparer reverseComparer = new();
        reverseComparer.Compare(x, y).ShouldBe(-1);
        reverseComparer.Compare(y, x).ShouldBe(1);
        reverseComparer.Compare(x, z).ShouldBe(0);
        reverseComparer.Compare(null, null).ShouldBe(0);
        reverseComparer.Compare(x, null).ShouldBe(-1);
        reverseComparer.Compare(null, y).ShouldBe(1);
        reverseComparer.Compare(a, x).ShouldBe(-1);

        List<AssemblyNameExtension> assemblies = [y, x, z];
        assemblies.Sort(AssemblyNameReverseVersionComparer.GenericComparer);

        assemblies[0].ShouldBe(x);
        assemblies[1].ShouldBe(z);
        assemblies[2].ShouldBe(y);
    }

    [Theory]
    [InlineData("System.Xml")]
    [InlineData("System.XML, Version=2.0.0.0")]
    [InlineData("System.Xml, Culture=de-DE")]
    [InlineData("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a, Retargetable=Yes")]
    [InlineData("System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    public void VerifyAssemblyNameExSerializationByTranslator(string assemblyName)
    {
        AssemblyNameExtension assemblyNameOriginal = new(assemblyName);
        AssemblyNameExtension? assemblyNameDeserialized = null;

        var serializationStream = new MemoryStream();
        ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(serializationStream);

        writeTranslator.Translate(ref assemblyNameOriginal, (ITranslator t) => new AssemblyNameExtension(t));

        _ = serializationStream.Seek(0, SeekOrigin.Begin);
        ITranslator readTranslator = BinaryTranslator.GetReadTranslator(serializationStream, InterningBinaryReader.PoolingBuffer);

        readTranslator.Translate(ref assemblyNameDeserialized, (ITranslator t) => new AssemblyNameExtension(t));

        assemblyNameDeserialized.ShouldBe(assemblyNameOriginal);
    }

    [Fact]
    public void VerifyAssemblyNameExSerializationWithRemappedFromByTranslator()
    {
        var assemblyNameOriginal = new AssemblyNameExtension("System.Xml, Version=10.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
        var assemblyRemappedFrom = new AssemblyNameExtension("System.Xml, Version=9.0.0.0, Culture=en, PublicKeyToken=b03f5f7f11d50a3a");
        assemblyRemappedFrom.MarkImmutable();
        assemblyNameOriginal.AddRemappedAssemblyName(assemblyRemappedFrom);
        assemblyNameOriginal.RemappedFromEnumerator.Count().ShouldBe(1);

        AssemblyNameExtension? assemblyNameDeserialized = null;

        var serializationStream = new MemoryStream();
        ITranslator writeTranslator = BinaryTranslator.GetWriteTranslator(serializationStream);

        writeTranslator.Translate(ref assemblyNameOriginal, (ITranslator t) => new AssemblyNameExtension(t));

        _ = serializationStream.Seek(0, SeekOrigin.Begin);
        ITranslator readTranslator = BinaryTranslator.GetReadTranslator(serializationStream, InterningBinaryReader.PoolingBuffer);

        readTranslator.Translate(ref assemblyNameDeserialized, (ITranslator t) => new AssemblyNameExtension(t));

        assemblyNameDeserialized.ShouldNotBeNull().Equals(assemblyNameOriginal).ShouldBeTrue();
        assemblyNameDeserialized.RemappedFromEnumerator.Count().ShouldBe(1);
        assemblyNameDeserialized.RemappedFromEnumerator.First().ShouldBe(assemblyRemappedFrom);
    }
}
