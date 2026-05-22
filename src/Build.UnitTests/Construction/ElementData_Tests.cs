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
        var location = ElementLocation.Create("test.proj", 10, 5);
        var data = new ElementData("PropertyGroup", "http://schemas.microsoft.com/developer/msbuild/2003", location);

        data.Name.ShouldBe("PropertyGroup");
        data.NamespaceURI.ShouldBe("http://schemas.microsoft.com/developer/msbuild/2003");
        data.Location.ShouldBe(location);
        data.AttributeCount.ShouldBe(0);
        data.TextContent.ShouldBeNull();
    }

    [Fact]
    public void Constructor_NullNamespace_BecomesEmptyString()
    {
        var data = new ElementData("Project", null!, ElementLocation.EmptyLocation);

        data.NamespaceURI.ShouldBe(string.Empty);
    }

    [Fact]
    public void AddAttribute_IncreasesCount()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        var attrLocation = ElementLocation.Create("test.proj", 5, 20);

        data.AddAttribute(new AttributeData("Include", "*.cs", attrLocation));

        data.AttributeCount.ShouldBe(1);
    }

    [Fact]
    public void GetAttributeValue_ReturnsCorrectValue()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Include", "*.cs", ElementLocation.EmptyLocation));
        data.AddAttribute(new AttributeData("Exclude", "obj/**", ElementLocation.EmptyLocation));

        data.GetAttributeValue("Include").ShouldBe("*.cs");
        data.GetAttributeValue("Exclude").ShouldBe("obj/**");
    }

    [Fact]
    public void GetAttributeValue_CaseInsensitive()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Condition", "'$(X)'==''", ElementLocation.EmptyLocation));

        data.GetAttributeValue("condition").ShouldBe("'$(X)'==''");
        data.GetAttributeValue("CONDITION").ShouldBe("'$(X)'==''");
    }

    [Fact]
    public void GetAttributeValue_NotFound_ReturnsNull()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);

        data.GetAttributeValue("Include").ShouldBeNull();
    }

    [Fact]
    public void GetAttributeLocation_ReturnsCorrectLocation()
    {
        var location = ElementLocation.Create("test.proj", 7, 15);
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Include", "*.cs", location));

        data.GetAttributeLocation("Include").ShouldBe(location);
    }

    [Fact]
    public void GetAttributeLocation_NotFound_ReturnsNull()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);

        data.GetAttributeLocation("Include").ShouldBeNull();
    }

    [Fact]
    public void SetAttribute_UpdatesExistingValue()
    {
        var data = new ElementData("Property", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Condition", "old", ElementLocation.EmptyLocation));

        data.SetAttribute("Condition", "new");

        data.GetAttributeValue("Condition").ShouldBe("new");
        data.AttributeCount.ShouldBe(1);
    }

    [Fact]
    public void SetAttribute_AddsNewAttribute()
    {
        var data = new ElementData("Property", "", ElementLocation.EmptyLocation);

        data.SetAttribute("Condition", "'$(X)'==''");

        data.GetAttributeValue("Condition").ShouldBe("'$(X)'==''");
        data.AttributeCount.ShouldBe(1);
    }

    [Fact]
    public void SetAttribute_CaseInsensitiveMatch()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Include", "old.cs", ElementLocation.EmptyLocation));

        data.SetAttribute("include", "new.cs");

        data.GetAttributeValue("Include").ShouldBe("new.cs");
        data.AttributeCount.ShouldBe(1);
    }

    [Fact]
    public void RemoveAttribute_RemovesExisting()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Include", "*.cs", ElementLocation.EmptyLocation));
        data.AddAttribute(new AttributeData("Exclude", "obj/**", ElementLocation.EmptyLocation));

        data.RemoveAttribute("Include").ShouldBeTrue();

        data.AttributeCount.ShouldBe(1);
        data.GetAttributeValue("Include").ShouldBeNull();
        data.GetAttributeValue("Exclude").ShouldBe("obj/**");
    }

    [Fact]
    public void RemoveAttribute_NotFound_ReturnsFalse()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);

        data.RemoveAttribute("Include").ShouldBeFalse();
    }

    [Fact]
    public void HasAttribute_ReturnsTrueWhenExists()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Include", "*.cs", ElementLocation.EmptyLocation));

        data.HasAttribute("Include").ShouldBeTrue();
        data.HasAttribute("include").ShouldBeTrue();
    }

    [Fact]
    public void HasAttribute_ReturnsFalseWhenMissing()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);

        data.HasAttribute("Include").ShouldBeFalse();
    }

    [Fact]
    public void ClearAttributes_RemovesAll()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Include", "*.cs", ElementLocation.EmptyLocation));
        data.AddAttribute(new AttributeData("Exclude", "obj/**", ElementLocation.EmptyLocation));

        data.ClearAttributes();

        data.AttributeCount.ShouldBe(0);
    }

    [Fact]
    public void TextContent_CanBeSetAndRead()
    {
        var data = new ElementData("OutputType", "", ElementLocation.EmptyLocation);

        data.TextContent = "Library";

        data.TextContent.ShouldBe("Library");
    }

    [Fact]
    public void Name_CanBeChanged()
    {
        var data = new ElementData("OldName", "", ElementLocation.EmptyLocation);

        data.Name = "NewName";

        data.Name.ShouldBe("NewName");
    }

    [Fact]
    public void IsSelfClosing_DefaultsFalse()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);

        data.IsSelfClosing.ShouldBeFalse();
    }

    [Fact]
    public void LeadingWhitespace_CanBeSet()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);

        data.LeadingWhitespace = "    ";

        data.LeadingWhitespace.ShouldBe("    ");
    }

    [Fact]
    public void Attributes_ReturnsAllInOrder()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Include", "a.cs", ElementLocation.EmptyLocation));
        data.AddAttribute(new AttributeData("Exclude", "b.cs", ElementLocation.EmptyLocation));
        data.AddAttribute(new AttributeData("Condition", "'$(X)'==''", ElementLocation.EmptyLocation));

        var attrs = data.AttributeList;

        attrs.Count.ShouldBe(3);
        attrs[0].Name.ShouldBe("Include");
        attrs[1].Name.ShouldBe("Exclude");
        attrs[2].Name.ShouldBe("Condition");
    }

    [Fact]
    public void GetAttribute_ReturnsAttributeData()
    {
        var location = ElementLocation.Create("test.proj", 3, 10);
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);
        data.AddAttribute(new AttributeData("Include", "*.cs", location));

        var attr = data.GetAttribute("Include");

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("Include");
        attr.Value.ShouldBe("*.cs");
        attr.Location.ShouldBe(location);
    }

    [Fact]
    public void GetAttribute_NotFound_ReturnsNull()
    {
        var data = new ElementData("Item", "", ElementLocation.EmptyLocation);

        data.GetAttribute("Include").ShouldBeNull();
    }
}
