// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation;

public class ParserTest
{
    [Theory]
    [InlineData("$(foo)")]
    [InlineData("$(foo)=='hello'")]
    [InlineData("$(foo)==''")]
    [InlineData("$(debug) and $(buildlab) and $(full)")]
    [InlineData("$(debug) or $(buildlab) or $(full)")]
    [InlineData("$(debug) and $(buildlab) or $(full)")]
    [InlineData("$(full) or $(debug) and $(buildlab)")]
    [InlineData("%(culture)")]
    [InlineData("%(culture)=='french'")]
    [InlineData("%(from1.Identity)")]
    [InlineData("%(from1.Identity) == b")]
    [InlineData("'foo_%(culture)'=='foo_french'")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("0.0 == 0")]
    public void SimpleParseTest(string expression)
    {
        _ = Parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);
    }

    [Theory]
    [InlineData("$(foo)")]
    [InlineData("$(foo) or $(bar) and $(baz)")]
    [InlineData("$(foo) <= 5 and $(bar) >= 15")]
    [InlineData("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == simplestring) and 'a more complex string' != $(quux)")]
    [InlineData("(($(foo) or $(bar) == false) and !($(baz) == simplestring))")]
    [InlineData("(($(foo) or Exists('c:\\foo.txt')) and !(($(baz) == simplestring)))")]
    [InlineData("'CONTAINS%27QUOTE%27' == '$(TestQuote)'")]
    [InlineData("'$(MSBuildToolsVersion)' == '3.5' and ('$(DisableOutOfProcTaskHost)' != '' or !$([MSBuild]::DoesTaskHostExist(`CLR2`,`CurrentArchitecture`)))")]
    public void ComplexParseTest(string expression)
    {
        _ = Parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);
    }

    /// <summary>
    /// </summary>
    [Fact]
    public void NotParseTest()
    {
        Console.WriteLine("NegationParseTest()");
        GenericExpressionNode tree = Parser.Parse("!true", ParserOptions.AllowAll, MockElementLocation.Instance);

        tree = Parser.Parse("!(true)", ParserOptions.AllowAll, MockElementLocation.Instance);

        tree = Parser.Parse("!($(foo) <= 5)", ParserOptions.AllowAll, MockElementLocation.Instance);

        tree = Parser.Parse("!(%(foo) <= 5)", ParserOptions.AllowAll, MockElementLocation.Instance);

        tree = Parser.Parse("!($(foo) <= 5 and $(bar) >= 15)", ParserOptions.AllowAll, MockElementLocation.Instance);
    }

    [Theory]
    [InlineData("SimpleFunctionCall()")]
    [InlineData("SimpleFunctionCall( 1234 )")]
    [InlineData("SimpleFunctionCall( true )")]
    [InlineData("SimpleFunctionCall( $(property) )")]
    [InlineData("SimpleFunctionCall( $(property), 1234, abcd, 'abcd efgh' )")]
    public void FunctionCallParseTest(string expression)
    {
        _ = Parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);
    }

    [Fact]
    public void ItemListParseTest()
    {
        Console.WriteLine("FunctionCallParseTest()");
        GenericExpressionNode tree;
        bool fExceptionCaught = false;
        try
        {
            tree = Parser.Parse("@(foo) == 'a.cs;b.cs'", ParserOptions.AllowProperties, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        fExceptionCaught = false;
        try
        {
            tree = Parser.Parse("'a.cs;b.cs' == @(foo)", ParserOptions.AllowProperties, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        fExceptionCaught = false;
        try
        {
            tree = Parser.Parse("'@(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        fExceptionCaught = false;
        try
        {
            tree = Parser.Parse("'otherstuff@(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        fExceptionCaught = false;
        try
        {
            tree = Parser.Parse("'@(foo)otherstuff' == 'a.cs;b.cs'", ParserOptions.AllowProperties, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        fExceptionCaught = false;
        try
        {
            tree = Parser.Parse("somefunction(@(foo), 'otherstuff')", ParserOptions.AllowProperties, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);
    }

    /// <summary>
    /// Tests the special errors for "$(" and "$x" and similar cases.
    /// </summary>
    [Fact]
    public void IllFormedProperty()
    {
        var ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse("$x", ParserOptions.AllowProperties, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4110", ex.ErrorCode);

        ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse("$(x", ParserOptions.AllowProperties, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4109", ex.ErrorCode);
    }

    /// <summary>
    /// Tests the special errors for "@(" and "@x" and similar cases.
    /// </summary>
    [Fact]
    public void IllFormedItemList()
    {
        var ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse("@(", ParserOptions.AllowAll, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4106", ex.ErrorCode);

        ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse("@x", ParserOptions.AllowAll, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4107", ex.ErrorCode);

        ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse("@(x", ParserOptions.AllowAll, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4106", ex.ErrorCode);

        ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse("@(x->'%(y)", ParserOptions.AllowAll, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4108", ex.ErrorCode);

        ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse("@(x->'%(y)', 'x", ParserOptions.AllowAll, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4108", ex.ErrorCode);

        ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse("@(x->'%(y)', 'x'", ParserOptions.AllowAll, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4106", ex.ErrorCode);
    }

    /// <summary>
    /// Tests the space errors case.
    /// </summary>
    [Theory]
    [InlineData("$(x )")]
    [InlineData("$( x)")]
    [InlineData("$([MSBuild]::DoSomething($(space ))")]
    [InlineData("$([MSBuild]::DoSomething($(_space ))")]
    public void SpaceProperty(string expression)
    {
        var ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse(expression, ParserOptions.AllowProperties, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4259", ex.ErrorCode);
    }

    /// <summary>
    /// Tests the parsing of item lists.
    /// </summary>
    [Theory]
    [InlineData("@(foo)")]
    [InlineData("1234 '@(foo)'")]
    [InlineData("'1234 @(foo)'")]
    public void ItemListTests(string expression)
    {
        var ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse(expression, ParserOptions.AllowProperties, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4099", ex.ErrorCode);
    }

    [Fact]
    public void ItemFuncParseTest()
    {
        Console.WriteLine("ItemFuncParseTest()");

        GenericExpressionNode tree = Parser.Parse("@(item->foo('ab'))",
            ParserOptions.AllowProperties | ParserOptions.AllowItemLists, MockElementLocation.Instance);
        Assert.IsType<StringExpressionNode>(tree);
        Assert.Equal("@(item->foo('ab'))", tree.GetUnexpandedValue(null));

        tree = Parser.Parse("!@(item->foo())",
            ParserOptions.AllowProperties | ParserOptions.AllowItemLists, MockElementLocation.Instance);
        Assert.IsType<NotExpressionNode>(tree);

        tree = Parser.Parse("(@(item->foo('ab')) and @(item->foo('bc')))",
            ParserOptions.AllowProperties | ParserOptions.AllowItemLists, MockElementLocation.Instance);
        Assert.IsType<AndExpressionNode>(tree);
    }

    [Fact]
    public void ItemMetadata1()
    {
        var root = Parser.Parse("%(foo)", ParserOptions.AllowAll, MockElementLocation.Instance);

        var stringNode = Assert.IsType<StringExpressionNode>(root);
        Assert.Equal("%(foo)", stringNode.Value);
        Assert.True(stringNode.Expandable);
    }

    [Fact]
    public void ItemMetadata2()
    {
        var root = Parser.Parse("%(foo.boo)", ParserOptions.AllowAll, MockElementLocation.Instance);

        var stringNode = Assert.IsType<StringExpressionNode>(root);
        Assert.Equal("%(foo.boo)", stringNode.Value);
        Assert.True(stringNode.Expandable);
    }

    [Fact]
    public void ItemMetadata3()
    {
        var root = Parser.Parse("%(foo) == a", ParserOptions.AllowAll, MockElementLocation.Instance);

        var equalNode = Assert.IsType<EqualExpressionNode>(root);

        var left = Assert.IsType<StringExpressionNode>(equalNode.Left);
        Assert.Equal("%(foo)", left.Value);
        Assert.True(left.Expandable);

        var right = Assert.IsType<StringExpressionNode>(equalNode.Right);
        Assert.Equal("a", right.Value);
        Assert.False(right.Expandable);
    }

    [Fact]
    public void ItemMetadata4()
    {
        var root = Parser.Parse("%(foo.boo) == b", ParserOptions.AllowAll, MockElementLocation.Instance);

        var equalNode = Assert.IsType<EqualExpressionNode>(root);

        var left = Assert.IsType<StringExpressionNode>(equalNode.Left);
        Assert.Equal("%(foo.boo)", left.Value);
        Assert.True(left.Expandable);

        var right = Assert.IsType<StringExpressionNode>(equalNode.Right);
        Assert.Equal("b", right.Value);
        Assert.False(right.Expandable);
    }

    [Theory]
    [InlineData("%(foo) == 'a.cs;b.cs'")]
    [InlineData("'a.cs;b.cs' == %(foo)")]
    [InlineData("'%(foo)' == 'a.cs;b.cs'")]
    [InlineData("'otherstuff%(foo)' == 'a.cs;b.cs'")]
    [InlineData("'%(foo)otherstuff' == 'a.cs;b.cs'")]
    [InlineData("somefunction(%(foo), 'otherstuff')")]
    public void ItemMetadataDisallowed(string expression)
    {
        var ex = Assert.Throws<InvalidProjectFileException>(() =>
        {
            _ = Parser.Parse(expression, ParserOptions.AllowProperties | ParserOptions.AllowItemLists, MockElementLocation.Instance);
        });

        Assert.Equal("MSB4191", ex.ErrorCode);
    }

    [Fact]
    public void NegativeTests()
    {
        Console.WriteLine("NegativeTests()");
        GenericExpressionNode tree;
        bool fExceptionCaught;

        try
        {
            fExceptionCaught = false;
            // Note no close quote ----------------------------------------------------V
            tree = Parser.Parse("'a more complex' == 'asdf", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        try
        {
            fExceptionCaught = false;
            // Note no close quote ----------------------------------------------------V
            tree = Parser.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == 'simple string) and 'a more complex string' != $(quux)", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);
        try
        {
            fExceptionCaught = false;
            // Correct tokens, but bad parse -----------V
            tree = Parser.Parse("($(foo) == 'simple string') $(bar)", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        try
        {
            fExceptionCaught = false;
            // Correct tokens, but bad parse -----------V
            tree = Parser.Parse("=='x'", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        try
        {
            fExceptionCaught = false;
            // Correct tokens, but bad parse -----------V
            tree = Parser.Parse("==", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        try
        {
            fExceptionCaught = false;
            // Correct tokens, but bad parse -----------V
            tree = Parser.Parse(">", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);
        try
        {
            fExceptionCaught = false;
            // Correct tokens, but bad parse -----------V
            tree = Parser.Parse("true!=false==", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);

        try
        {
            fExceptionCaught = false;
            // Correct tokens, but bad parse -----------V
            tree = Parser.Parse("true!=false==true", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);
        try
        {
            fExceptionCaught = false;
            // Correct tokens, but bad parse -----------V
            tree = Parser.Parse("1==(2", ParserOptions.AllowAll, MockElementLocation.Instance);
        }
        catch (InvalidProjectFileException e)
        {
            Console.WriteLine(e.BaseMessage);
            fExceptionCaught = true;
        }
        Assert.True(fExceptionCaught);
    }

    /// <summary>
    /// This test verifies that we trigger warnings for expressions that
    /// could be incorrectly evaluated
    /// </summary>
    [Fact]
    public void VerifyWarningForOrder()
    {
        // Create a project file that has an expression
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 and $(b) == 2 or $(c) == 3`/>
                        </Target>
                    </Project>
                ");

        // Make sure the log contains the correct strings.
        Assert.Contains("MSB4130:", ml.FullLog); // "Need to warn for this expression - (a) == 1 and $(b) == 2 or $(c) == 3."

        ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 or $(b) == 2 and $(c) == 3`/>
                        </Target>
                    </Project>
                ");

        // Make sure the log contains the correct strings.
        Assert.Contains("MSB4130:", ml.FullLog); // "Need to warn for this expression - (a) == 1 or $(b) == 2 and $(c) == 3."

        ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4`/>
                        </Target>
                    </Project>
                ");

        // Make sure the log contains the correct strings.
        Assert.Contains("MSB4130:", ml.FullLog); // "Need to warn for this expression - ($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4."
    }

    /// <summary>
    /// This test verifies that we don't trigger warnings for expressions that
    /// couldn't be incorrectly evaluated
    /// </summary>
    [Fact]
    public void VerifyNoWarningForOrder()
    {
        // Create a project file that has an expression
        MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 and $(b) == 2 and $(c) == 3`/>
                        </Target>
                    </Project>
                ");

        // Make sure the log contains the correct strings.
        Assert.DoesNotContain("MSB4130:", ml.FullLog); // "No need to warn for this expression - (a) == 1 and $(b) == 2 and $(c) == 3."

        ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 or $(b) == 2 or $(c) == 3`/>
                        </Target>
                    </Project>
                ");

        // Make sure the log contains the correct strings.
        Assert.DoesNotContain("MSB4130:", ml.FullLog); // "No need to warn for this expression - (a) == 1 or $(b) == 2 or $(c) == 3."

        ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 and $(b) == 2) or $(c) == 3`/>
                        </Target>
                    </Project>
                ");

        // Make sure the log contains the correct strings.
        Assert.DoesNotContain("MSB4130:", ml.FullLog); // "No need to warn for this expression - ($(a) == 1 and $(b) == 2) or $(c) == 3."

        ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 or $(b) == 2) and $(c) == 3`/>
                        </Target>
                    </Project>
                ");

        // Make sure the log contains the correct strings.
        Assert.DoesNotContain("MSB4130:", ml.FullLog); // "No need to warn for this expression - ($(a) == 1 or $(b) == 2) and $(c) == 3."
    }

    // see https://github.com/dotnet/msbuild/issues/5436
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SupportItemDefinationGroupInWhenOtherwise(bool context)
    {
        var projectContent = $@"
                <Project ToolsVersion= `msbuilddefaulttoolsversion` xmlns= `msbuildnamespace`>
                    <Choose>
                        <When Condition= `{context}`>
                            <PropertyGroup>
                                <Foo>bar</Foo>
                            </PropertyGroup>
                            <ItemGroup>
                                <A Include= `$(Foo)`>
                                    <n>n1</n>
                                </A>
                            </ItemGroup>
                            <ItemDefinitionGroup>
                                <A>
                                    <m>m1</m>
                                    <n>n2</n>
                                </A>
                            </ItemDefinitionGroup>
                        </When>
                        <Otherwise>
                            <PropertyGroup>
                                <Foo>bar</Foo>
                            </PropertyGroup>
                            <ItemGroup>
                                <A Include= `$(Foo)`>
                                    <n>n1</n>
                                </A>
                            </ItemGroup>
                            <ItemDefinitionGroup>
                                <A>
                                    <m>m2</m>
                                    <n>n2</n>
                                </A>
                            </ItemDefinitionGroup>
                        </Otherwise>
                    </Choose>
                </Project>
                ".Cleanup();


        var project = ObjectModelHelpers.CreateInMemoryProject(projectContent);

        var projectItem = project.GetItems("A").FirstOrDefault();
        Assert.Equal("bar", projectItem.EvaluatedInclude);

        var metadatam = projectItem.GetMetadata("m");
        if (context)
        {
            // Go to when
            Assert.Equal("m1", metadatam.EvaluatedValue);
        }
        else
        {
            // Go to Otherwise
            Assert.Equal("m2", metadatam.EvaluatedValue);
        }

        var metadatan = projectItem.GetMetadata("n");
        Assert.Equal("n1", metadatan.EvaluatedValue);
        Assert.Equal("n2", metadatan.Predecessor.EvaluatedValue);
    }
}
