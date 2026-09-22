// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Expansion;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class Expander_Tests
{
    private static readonly string s_rootPathPrefix = NativeMethodsShared.IsWindows ? @"C:\" : Path.VolumeSeparatorChar.ToString();

    [Fact]
    public void ExpandAllIntoTaskItems0()
    {
        var expander = ExpanderFactory.Create(Properties());

        IList<TaskItem>? items = expander.ExpandIntoTaskItemsLeaveEscaped(
            "",
            ExpanderOptions.ExpandProperties,
            location: null!);

        ObjectModelHelpers.AssertItemsMatch("", GetTaskArrayFromItemList(items!));
    }

    [Fact]
    public void ExpandAllIntoTaskItems1()
    {
        var expander = ExpanderFactory.Create(Properties());

        IList<TaskItem>? items = expander.ExpandIntoTaskItemsLeaveEscaped(
            "foo",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch("foo", GetTaskArrayFromItemList(items!));
    }

    [Fact]
    public void ExpandAllIntoTaskItems2()
    {
        var expander = ExpanderFactory.Create(Properties());

        IList<TaskItem>? items = expander.ExpandIntoTaskItemsLeaveEscaped(
            "foo;bar",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch(
            """
            foo
            bar
            """,
            GetTaskArrayFromItemList(items!));
    }

    [Fact]
    public void ExpandAllIntoTaskItems3()
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();

        List<ProjectItemInstance> items1 = [
            new(project, "Compile", "foo.cs", project.FullPath),
            new(project, "Compile", "bar.cs", project.FullPath),
        ];

        List<ProjectItemInstance> items2 = [
            new(project, "Resource", "bing.resx", project.FullPath)
        ];

        var itemsByType = Items();
        itemsByType.ImportItems(items1);
        itemsByType.ImportItems(items2);

        TestLoggingContext loggingContext = new(loggingService: null, new BuildEventContext(1, 2, 3, 4));

        var expander = ExpanderFactory.Create(Properties(), itemsByType, loggingContext);

        IList<TaskItem>? items = expander.ExpandIntoTaskItemsLeaveEscaped(
            "foo;bar;@(compile);@(resource)",
            ExpanderOptions.ExpandPropertiesAndItems,
            MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch(
            """
            foo
            bar
            foo.cs
            bar.cs
            bing.resx
            """,
            GetTaskArrayFromItemList(items!));
    }

    [Fact]
    public void ExpandAllIntoTaskItems4()
    {
        var expander = ExpanderFactory.Create(Properties(
            ("a", "aaa"),
            ("b", "bbb"),
            ("c", "cc;dd")));

        IList<TaskItem>? items = expander.ExpandIntoTaskItemsLeaveEscaped(
            "foo$(a);$(b);$(c)",
            ExpanderOptions.ExpandProperties,
            MockElementLocation.Instance);

        ObjectModelHelpers.AssertItemsMatch(
            """
            fooaaa
            bbb
            cc
            dd
            """,
            GetTaskArrayFromItemList(items!));
    }

    /// <summary>
    ///  Creates a set of complicated item metadata and properties, and items to exercise
    ///  the Expander class.  The data here contains escaped characters, metadata that
    ///  references properties, properties that reference items, and other complex scenarios.
    /// </summary>
    private static IExpander<ProjectPropertyInstance, ProjectItemInstance> CreateComplexExpander()
    {
        var metadata = Metadata(
            ("Culture", "abc%253bdef;$(Gee_Aych_Ayee)"),
            ("Language", "english"));

        var project = ProjectHelpers.CreateEmptyProjectInstance();

        ProjectItemInstance i1 = new(
            project,
            "IntermediateAssembly",
            NativeMethodsShared.IsWindows ? @"subdir1\engine.dll" : "subdir1/engine.dll",
            project.FullPath);
        i1.SetMetadata("aaa", "111");

        ProjectItemInstance i2 = new(
            project,
            "IntermediateAssembly",
            NativeMethodsShared.IsWindows ? @"subdir2\tasks.dll" : "subdir2/tasks.dll",
            project.FullPath);
        i2.SetMetadata("bbb", "222");

        List<ProjectItemInstance> intermediateAssemblyItemGroup = [i1, i2];

        ProjectItemInstance i3 = new(project, "Content", "splash.bmp", project.FullPath);
        i3.SetMetadata("ccc", "333");
        List<ProjectItemInstance> contentItemGroup = [i3];

        ProjectItemInstance i4 = new(project, "Resource", "string$(p).resx", project.FullPath);
        i4.SetMetadata("ddd", "444");

        ProjectItemInstance i5 = new(project, "Resource", "dialogs%253b.resx", project.FullPath);
        i5.SetMetadata("eee", "555");

        List<ProjectItemInstance> resourceItemGroup = [i4, i5];

        ProjectItemInstance i6 = new ProjectItemInstance(project, "Content", "about.bmp", project.FullPath);
        i6.SetMetadata("fff", "666");

        List<ProjectItemInstance> contentItemGroup2 = [i6];

        ItemDictionary<ProjectItemInstance> secondaryItemsByName = new ItemDictionary<ProjectItemInstance>();
        secondaryItemsByName.ImportItems(resourceItemGroup);
        secondaryItemsByName.ImportItems(contentItemGroup2);

        var properties = Properties(
            ("Gee_Aych_Ayee", "ghi"),
            ("OutputPath", @"\jk ; l\mno%253bpqr\stu"),
            ("TargetPath", "@(IntermediateAssembly->'%(RelativeDir)')"));

        Lookup lookup = new(secondaryItemsByName, properties);

        // Add primary items
        lookup.EnterScope("x");
        lookup.PopulateWithItems("IntermediateAssembly", intermediateAssemblyItemGroup);
        lookup.PopulateWithItems("Content", contentItemGroup);

        return ExpanderFactory.Create(lookup, lookup, metadata);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoTaskItems with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoTaskItemsComplex()
    {
        var expander = CreateComplexExpander();

        IList<TaskItem>? taskItems = expander.ExpandIntoTaskItemsLeaveEscaped(
            "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)",
            ExpanderOptions.ExpandAll,
            MockElementLocation.Instance);

        // the following items are passed to the TaskItem constructor, and thus their ItemSpecs should be
        // in escaped form.
        string expectedItemsString = $"""
            string$(p): ddd=444
            dialogs%253b: eee=555
            splash.bmp: ccc=333
            \jk
            l\mno%253bpqr\stu
            subdir1{Path.DirectorySeparatorChar}: aaa=111
            subdir2{Path.DirectorySeparatorChar}: bbb=222
            english_abc%253bdef
            ghi
            """;

        ObjectModelHelpers.AssertItemsMatch(expectedItemsString, GetTaskArrayFromItemList(taskItems!));
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data but in a piecemeal fashion.
    /// </summary>
    [Theory]
    [MemberData(nameof(ComplexStringExpansionPieces))]
    public void ExpandAllIntoStringExpandsComplexExpressionPieces(string expression, string expected)
    {
        var expander = CreateComplexExpander();

        expander.ExpandIntoStringAndUnescape(expression, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

    public static TheoryData<string, string> ComplexStringExpansionPieces => new()
    {
        { "@(Resource->'%(Filename)') ;", "string$(p);dialogs%3b ;" },
        { "@(Content)", "splash.bmp" },
        { "@(NonExistent)", "" },
        { "$(NonExistent)", "" },
        { "%(NonExistent)", "" },
        { "$(OutputPath)", @"\jk ; l\mno%3bpqr\stu" },
        { "$(TargetPath)", $"subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar}" },
        { "%(Language)_%(Culture)", "english_abc%3bdef;ghi" },
    };

    /// <summary>
    ///  Exercises ExpandAllIntoString with an item list using a transform that is empty.
    /// </summary>
    [Theory]
    [InlineData("@(IntermediateAssembly->'')")]
    [InlineData("@(IntermediateAssembly->'%(goop)')")]
    public void ExpandAllIntoStringWithEmptyTransformReturnsSeparators(string expression)
    {
        var expander = CreateComplexExpander();

        expander.ExpandIntoStringAndUnescape(expression, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe(";");
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringComplex()
    {
        var expander = CreateComplexExpander();

        string input = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        expander.ExpandIntoStringAndUnescape(input, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe($"""
                string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%3bdef;ghi
                """);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringLeaveEscapedComplex()
    {
        var expander = CreateComplexExpander();

        string input = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        expander.ExpandIntoStringLeaveEscaped(input, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldBe($"""
                string$(p);dialogs%253b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%253bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%253bdef;ghi
                """);
    }

    /// <summary>
    ///  Exercises ExpandIntoStringAndUnescape and ExpanderOptions.Truncate.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringTruncated()
    {
        string manySpaces = new(' ', 2000);
        var properties = Properties("ManySpacesProperty", manySpaces);
        var metadata = Metadata("ManySpacesMetadata", manySpaces);

        var projectItemGroups = new ItemDictionary<ProjectItemInstance>();

        var lookup = new Lookup(projectItemGroups, properties);
        lookup.EnterScope("x");

        var project = ProjectHelpers.CreateEmptyProjectInstance();

        lookup.PopulateWithItems("ManySpacesItem", [
            new(project, "ManySpacesItem", "Foo", project.FullPath),
            new(project, "ManySpacesItem", manySpaces, project.FullPath),
            new(project, "ManySpacesItem", "Bar", project.FullPath),
        ]);

        lookup.PopulateWithItems("Exactly1024", [
            new(project, "Exactly1024", "".PadLeft(1024), project.FullPath),
            new(project, "Exactly1024", "Foo", project.FullPath),
        ]);

        lookup.PopulateWithItems("ManyItems", [
            .. Enumerable.Range(0, 50).Select(index =>
                {
                    ProjectItemInstance item = new(project, "ManyItems", $"ThisIsAFairlyLongFileName_{index}.bmp", project.FullPath);
                    item.SetMetadata("Foo", $"ThisIsAFairlyLongMetadataValue_{index}");
                    return item;
                })
            ]);

        var expander = ExpanderFactory.Create(lookup, lookup, metadata);

        string input = "'%(ManySpacesMetadata)' != '' and '$(ManySpacesProperty)' != '' and '@(ManySpacesItem)' != '' and '@(Exactly1024)' != '' and '@(ManyItems)' != '' and '@(ManyItems->'%(Foo)')' != '' and '@(ManyItems->'%(Nonexistent)')' != ''";

        string expected =
            $"'{"",1021}...' != '' and " +
            $"'{"",1021}...' != '' and " +
            $"'Foo;{"",1017}...' != '' and " +
            $"'{"",1024};...' != '' and " +
            "'ThisIsAFairlyLongFileName_0.bmp;ThisIsAFairlyLongFileName_1.bmp;ThisIsAFairlyLongFileName_2.bmp;...' != '' and " +
            "'ThisIsAFairlyLongMetadataValue_0;ThisIsAFairlyLongMetadataValue_1;ThisIsAFairlyLongMetadataValue_2;...' != '' and " +
            $"';;;...' != ''";

        // NOTE: semicolons in the last part are *weird* because they don't actually mean anything and you get logging like
        //     Target "Build" skipped, due to false condition; ( '@(I->'%(nonexistent)')' == '' ) was evaluated as ( ';' == '' ).
        // but that goes back to MSBuild 4.something so I'm codifying it in this test. If you're here because you cleaned it up
        // and want to fix the test my current opinion is that's fine.

        expander.ExpandIntoStringAndUnescape(input, ExpanderOptions.ExpandAll | ExpanderOptions.Truncate, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a string that does not need expanding.
    ///  In this case the expanded string should be reference identical to the passed in string.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringExpectIdenticalReference()
    {
        var expander = CreateComplexExpander();

        // Create a non-literal string to represent input constructed at runtime during a build.
        string input = $"abc123{new Random().Next()}";
        string expandedString = expander
            .ExpandIntoStringLeaveEscaped(input, ExpanderOptions.ExpandAll, MockElementLocation.Instance)
            .ShouldNotBeNull();

        // Verify the input and result remain non-interned.
        string.IsInterned(input).ShouldBeNull();
        string.IsInterned(expandedString).ShouldBeNull();

        // Finally verify Expander indeed didn't create a new string.
        input.ShouldBeSameAs(expandedString);
    }

    /// <summary>
    ///  Exercises ExpandAllIntoString with a complex set of data and various expander options.
    /// </summary>
    [Theory]
    [MemberData(nameof(ComplexStringExpansionOptions))]
    internal void ExpandAllIntoStringHonorsExpanderOptions(ExpanderOptions options, string expected)
    {
        var expander = CreateComplexExpander();

        const string Expression = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        expander.ExpandIntoStringAndUnescape(Expression, options, MockElementLocation.Instance)
            .ShouldBe(expected);
    }

    public static IEnumerable<object[]> ComplexStringExpansionOptions =>
    [
        [
            ExpanderOptions.ExpandProperties,
            """@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ; %(NonExistent) ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; %(Language)_%(Culture)"""
        ],
        [
            ExpanderOptions.ExpandPropertiesAndMetadata,
            """@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ;  ;  ; \jk ; l\mno%3bpqr\stu ; @(IntermediateAssembly->'%(RelativeDir)') ; english_abc%3bdef;ghi"""
        ],
        [
            ExpanderOptions.ExpandAll,
            $"""string$(p);dialogs%3b ; splash.bmp ;  ;  ;  ; \jk ; l\mno%3bpqr\stu ; subdir1{Path.DirectorySeparatorChar};subdir2{Path.DirectorySeparatorChar} ; english_abc%3bdef;ghi"""
        ],
        [
            ExpanderOptions.ExpandItems,
            """string$(p);dialogs%3b ; splash.bmp ;  ; $(NonExistent) ; %(NonExistent) ; $(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)"""
        ],
    ];

    /// <summary>
    ///  Exercises ExpandAllIntoStringListLeaveEscaped with a complex set of data.
    /// </summary>
    [Fact]
    public void ExpandAllIntoStringListLeaveEscapedComplex()
    {
        var expander = CreateComplexExpander();

        string value = "@(Resource->'%(Filename)') ; @(Content) ; @(NonExistent) ; $(NonExistent) ; %(NonExistent) ; " +
            "$(OutputPath) ; $(TargetPath) ; %(Language)_%(Culture)";

        IList<string> expanded = [.. expander.ExpandIntoStringListLeaveEscaped(value, ExpanderOptions.ExpandAll, MockElementLocation.Instance)];

        expanded.Count.ShouldBe(9);
        expanded[0].ShouldBe("string$(p)");
        expanded[1].ShouldBe("dialogs%253b");
        expanded[2].ShouldBe("splash.bmp");
        expanded[3].ShouldBe("\\jk");
        expanded[4].ShouldBe("l\\mno%253bpqr\\stu");
        expanded[5].ShouldBe($"subdir1{Path.DirectorySeparatorChar}");
        expanded[6].ShouldBe($"subdir2{Path.DirectorySeparatorChar}");
        expanded[7].ShouldBe("english_abc%253bdef");
        expanded[8].ShouldBe("ghi");
    }

    /// <summary>
    ///  Expand a property function that references item metadata
    /// </summary>
    [Fact]
    public void PropertyFunctionConsumingItemMetadata()
    {
        var project = ProjectHelpers.CreateEmptyProjectInstance();
        var properties = Properties("SomePath", Path.Combine(s_rootPathPrefix, "some", "path"));
        var metadata = Metadata("Compile.Identity", "fOo.Cs");
        var items = Items();
        items.ImportItems([new ProjectItemInstance(project, "Compile", "fOo.Cs", project.FullPath)]);

        var expander = ExpanderFactory.Create(properties, items, metadata);

        string? result = expander.ExpandIntoStringLeaveEscaped(
            "$([System.IO.Path]::Combine($(SomePath),%(Compile.Identity)))",
            ExpanderOptions.ExpandAll,
            MockElementLocation.Instance);

        result.ShouldBe(Path.Combine(s_rootPathPrefix, "some", "path", "fOo.Cs"));
    }

    /// <summary>
    ///  Expand a property function which is a string constructor referencing item metadata.
    /// </summary>
    /// <remarks>
    ///  Note that referencing a non-existent metadatum results in binding to a parameter-less String constructor. This constructor
    ///  does not exist in BCL but it is special-cased in the expander logic and handled to return an empty string.
    /// </remarks>
    [Theory]
    [InlineData("language", "english")]
    [InlineData("nonexistent", "")]
    public void PropertyStringConstructorConsumingItemMetadata(string name, string value)
        => ExpandPropertiesAndMetadata($"$([System.String]::new(%({name})))", Metadata("Language", "english"))
            .ShouldBe(value);

    private static ITaskItem[] GetTaskArrayFromItemList(IList<TaskItem> list)
    {
        ITaskItem[] items = new ITaskItem[list.Count];
        for (int i = 0; i < list.Count; ++i)
        {
            items[i] = list[i];
        }

        return items;
    }
}
