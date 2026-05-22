// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction;

public class ElementData_Tests
{
    [Fact]
    public void Constructor_SetsNameAndLocation()
    {
        var data = new ElementData("PropertyGroup", "http://schemas.microsoft.com/developer/msbuild/2003", "test.proj", 10, 5);

        data.Name.ShouldBe("PropertyGroup");
        data.NamespaceURI.ShouldBe("http://schemas.microsoft.com/developer/msbuild/2003");
        data.Location.File.ShouldBe("test.proj");
        data.Location.Line.ShouldBe(10);
        data.Location.Column.ShouldBe(5);
        data.AttributeCount.ShouldBe(0);
        data.TextContent.ShouldBeNull();
    }

    [Fact]
    public void Constructor_NullNamespace_BecomesEmptyString()
    {
        var data = new ElementData("Project", null!, "", 0, 0);

        data.NamespaceURI.ShouldBe(string.Empty);
    }

    [Fact]
    public void SetAttributeArray_SetsAttributes()
    {
        var data = new ElementData("Item", "", "test.proj", 1, 1);
        data.SetAttributeArray([new AttributeData("Include", "*.cs", 5, 20)]);

        data.AttributeCount.ShouldBe(1);
    }

    [Fact]
    public void GetAttributeValue_ReturnsCorrectValue()
    {
        var data = new ElementData("Item", "", "", 0, 0);
        data.SetAttributeArray(
        [
            new AttributeData("Include", "*.cs", 0, 0),
            new AttributeData("Exclude", "obj/**", 0, 0),
        ]);

        data.GetAttributeValue("Include").ShouldBe("*.cs");
        data.GetAttributeValue("Exclude").ShouldBe("obj/**");
    }

    [Fact]
    public void GetAttributeValue_CaseInsensitive()
    {
        var data = new ElementData("Item", "", "", 0, 0);
        data.SetAttributeArray([new AttributeData("Condition", "'$(X)'==''", 0, 0)]);

        data.GetAttributeValue("condition").ShouldBe("'$(X)'==''");
        data.GetAttributeValue("CONDITION").ShouldBe("'$(X)'==''");
    }

    [Fact]
    public void GetAttributeValue_NotFound_ReturnsNull()
    {
        var data = new ElementData("Item", "", "", 0, 0);

        data.GetAttributeValue("Include").ShouldBeNull();
    }

    [Fact]
    public void GetAttributeLocation_ReturnsCorrectLocation()
    {
        var data = new ElementData("Item", "", "test.proj", 1, 1);
        data.SetAttributeArray([new AttributeData("Include", "*.cs", 7, 15)]);

        var loc = data.GetAttributeLocation("Include");
        loc.ShouldNotBeNull();
        loc!.File.ShouldBe("test.proj");
        loc.Line.ShouldBe(7);
        loc.Column.ShouldBe(15);
    }

    [Fact]
    public void GetAttributeLocation_NotFound_ReturnsNull()
    {
        var data = new ElementData("Item", "", "", 0, 0);

        data.GetAttributeLocation("Include").ShouldBeNull();
    }

    [Fact]
    public void SetAttribute_UpdatesExistingValue()
    {
        var data = new ElementData("Property", "", "", 0, 0);
        data.SetAttributeArray([new AttributeData("Condition", "old", 0, 0)]);

        data.SetAttribute("Condition", "new");

        data.GetAttributeValue("Condition").ShouldBe("new");
        data.AttributeCount.ShouldBe(1);
    }

    [Fact]
    public void SetAttribute_AddsNewAttribute()
    {
        var data = new ElementData("Property", "", "", 0, 0);

        data.SetAttribute("Condition", "'$(X)'==''");

        data.GetAttributeValue("Condition").ShouldBe("'$(X)'==''");
        data.AttributeCount.ShouldBe(1);
    }

    [Fact]
    public void SetAttribute_CaseInsensitiveMatch()
    {
        var data = new ElementData("Item", "", "", 0, 0);
        data.SetAttributeArray([new AttributeData("Include", "old.cs", 0, 0)]);

        data.SetAttribute("include", "new.cs");

        data.GetAttributeValue("Include").ShouldBe("new.cs");
        data.AttributeCount.ShouldBe(1);
    }

    [Fact]
    public void RemoveAttribute_RemovesExisting()
    {
        var data = new ElementData("Item", "", "", 0, 0);
        data.SetAttributeArray(
        [
            new AttributeData("Include", "*.cs", 0, 0),
            new AttributeData("Exclude", "obj/**", 0, 0),
        ]);

        data.RemoveAttribute("Include").ShouldBeTrue();

        data.AttributeCount.ShouldBe(1);
        data.GetAttributeValue("Include").ShouldBeNull();
        data.GetAttributeValue("Exclude").ShouldBe("obj/**");
    }

    [Fact]
    public void RemoveAttribute_NotFound_ReturnsFalse()
    {
        var data = new ElementData("Item", "", "", 0, 0);

        data.RemoveAttribute("Include").ShouldBeFalse();
    }

    [Fact]
    public void HasAttribute_ReturnsTrueWhenExists()
    {
        var data = new ElementData("Item", "", "", 0, 0);
        data.SetAttributeArray([new AttributeData("Include", "*.cs", 0, 0)]);

        data.HasAttribute("Include").ShouldBeTrue();
        data.HasAttribute("include").ShouldBeTrue();
    }

    [Fact]
    public void HasAttribute_ReturnsFalseWhenMissing()
    {
        var data = new ElementData("Item", "", "", 0, 0);

        data.HasAttribute("Include").ShouldBeFalse();
    }

    [Fact]
    public void ClearAttributes_RemovesAll()
    {
        var data = new ElementData("Item", "", "", 0, 0);
        data.SetAttributeArray(
        [
            new AttributeData("Include", "*.cs", 0, 0),
            new AttributeData("Exclude", "obj/**", 0, 0),
        ]);

        data.ClearAttributes();

        data.AttributeCount.ShouldBe(0);
    }

    [Fact]
    public void TextContent_CanBeSetAndRead()
    {
        var data = new ElementData("OutputType", "", "", 0, 0);

        data.TextContent = "Library";

        data.TextContent.ShouldBe("Library");
    }

    [Fact]
    public void Name_CanBeChanged()
    {
        var data = new ElementData("OldName", "", "", 0, 0);

        data.Name = "NewName";

        data.Name.ShouldBe("NewName");
    }

    [Fact]
    public void IsSelfClosing_DefaultsFalse()
    {
        var data = new ElementData("Item", "", "", 0, 0);

        data.IsSelfClosing.ShouldBeFalse();
    }

    [Fact]
    public void LeadingWhitespace_CanBeSet()
    {
        var data = new ElementData("Item", "", "", 0, 0);

        data.LeadingWhitespace = "    ";

        data.LeadingWhitespace.ShouldBe("    ");
    }

    [Fact]
    public void Attributes_ReturnsAllInOrder()
    {
        var data = new ElementData("Item", "", "", 0, 0);
        data.SetAttributeArray(
        [
            new AttributeData("Include", "a.cs", 0, 0),
            new AttributeData("Exclude", "b.cs", 0, 0),
            new AttributeData("Condition", "'$(X)'==''", 0, 0),
        ]);

        var attrs = data.Attributes;

        attrs.Length.ShouldBe(3);
        attrs[0].Name.ShouldBe("Include");
        attrs[1].Name.ShouldBe("Exclude");
        attrs[2].Name.ShouldBe("Condition");
    }

    [Fact]
    public void GetAttributeLocation_UsesFilePath()
    {
        var data = new ElementData("Item", "", "myfile.proj", 1, 1);
        data.SetAttributeArray([new AttributeData("Include", "*.cs", 3, 10)]);

        var loc = data.GetAttributeLocation("Include");

        loc.ShouldNotBeNull();
        loc!.File.ShouldBe("myfile.proj");
        loc.Line.ShouldBe(3);
        loc.Column.ShouldBe(10);
    }

    [Fact]
    public void GetAttributeLocation_NotFound_ReturnsNull2()
    {
        var data = new ElementData("Item", "", "", 0, 0);

        data.GetAttributeLocation("Include").ShouldBeNull();
    }
}
