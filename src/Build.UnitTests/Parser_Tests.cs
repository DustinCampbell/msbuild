// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;



#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class ParserTest
    {
        /// <summary>
        ///  Make a fake element location for methods who need one.
        /// </summary>
        private MockElementLocation _elementLocation = MockElementLocation.Instance;

        /// <summary>
        /// </summary>
        [Fact]
        public void SimpleParseTest()
        {
            Console.WriteLine("SimpleParseTest()");
            ParseResult tree = Parser.Parse("$(foo)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("$(foo)=='hello'", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("$(foo)==''", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("$(debug) and $(buildlab) and $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("$(debug) or $(buildlab) or $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("$(debug) and $(buildlab) or $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("$(full) or $(debug) and $(buildlab)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("%(culture)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("%(culture)=='french'", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("'foo_%(culture)'=='foo_french'", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("true", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("false", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("0", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("0.0 == 0", ParserOptions.AllowAll, _elementLocation);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ComplexParseTest()
        {
            Console.WriteLine("ComplexParseTest()");
            ParseResult tree = Parser.Parse("$(foo)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("($(foo) or $(bar)) and $(baz)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("$(foo) <= 5 and $(bar) >= 15", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == simplestring) and 'a more complex string' != $(quux)", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("(($(foo) or $(bar) == false) and !($(baz) == simplestring))", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("(($(foo) or Exists('c:\\foo.txt')) and !(($(baz) == simplestring)))", ParserOptions.AllowAll, _elementLocation);


            tree = Parser.Parse("'CONTAINS%27QUOTE%27' == '$(TestQuote)'", ParserOptions.AllowAll, _elementLocation);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void NotParseTest()
        {
            Console.WriteLine("NegationParseTest()");
            ParseResult tree = Parser.Parse("!true", ParserOptions.AllowAll, _elementLocation);

            tree = Parser.Parse("!(true)", ParserOptions.AllowAll, _elementLocation);

            tree = Parser.Parse("!($(foo) <= 5)", ParserOptions.AllowAll, _elementLocation);

            tree = Parser.Parse("!(%(foo) <= 5)", ParserOptions.AllowAll, _elementLocation);

            tree = Parser.Parse("!($(foo) <= 5 and $(bar) >= 15)", ParserOptions.AllowAll, _elementLocation);
        }
        /// <summary>
        /// </summary>
        [Fact]
        public void FunctionCallParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            ParseResult tree = Parser.Parse("SimpleFunctionCall()", ParserOptions.AllowAll, _elementLocation);

            tree = Parser.Parse("SimpleFunctionCall( 1234 )", ParserOptions.AllowAll, _elementLocation);
            tree = Parser.Parse("SimpleFunctionCall( true )", ParserOptions.AllowAll, _elementLocation);
            tree = Parser.Parse("SimpleFunctionCall( $(property) )", ParserOptions.AllowAll, _elementLocation);

            tree = Parser.Parse("SimpleFunctionCall( $(property), 1234, abcd, 'abcd efgh' )", ParserOptions.AllowAll, _elementLocation);
        }

        [Fact]
        public void ItemListParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Assert.True(Parser.Parse("@(foo) == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation).IsError);
            Assert.True(Parser.Parse("'a.cs;b.cs' == @(foo)", ParserOptions.AllowProperties, _elementLocation).IsError);
            Assert.True(Parser.Parse("'@(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation).IsError);
            Assert.True(Parser.Parse("'otherstuff@(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation).IsError);
            Assert.True(Parser.Parse("'@(foo)otherstuff' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation).IsError);
            Assert.True(Parser.Parse("somefunction(@(foo), 'otherstuff')", ParserOptions.AllowProperties, _elementLocation).IsError);
        }

        [Fact]
        public void ItemFuncParseTest()
        {
            Console.WriteLine("ItemFuncParseTest()");

            ParseResult tree = Parser.Parse("@(item->foo('ab'))", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            Assert.IsType<StringExpressionNode>(tree.Node);
            Assert.Equal("@(item->foo('ab'))", tree.Node.GetUnexpandedValue(null));

            tree = Parser.Parse("!@(item->foo())", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            Assert.IsType<NotExpressionNode>(tree.Node);

            tree = Parser.Parse("(@(item->foo('ab')) and @(item->foo('bc')))", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            Assert.IsType<AndExpressionNode>(tree.Node);
        }

        [Fact]
        public void MetadataParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Assert.True(Parser.Parse("%(foo) == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation).IsError);
            Assert.True(Parser.Parse("'a.cs;b.cs' == %(foo)", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation).IsError);
            Assert.True(Parser.Parse("'%(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation).IsError);
            Assert.True(Parser.Parse("'otherstuff%(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation).IsError);
            Assert.True(Parser.Parse("'%(foo)otherstuff' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation).IsError);
            Assert.True(Parser.Parse("somefunction(%(foo), 'otherstuff')", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation).IsError);
        }

        /// <summary>
        /// Tests the special error for "=" (should be "==").
        /// </summary>
        [Fact]
        public void SingleEqualsProducesError()
        {
            ParseResult result = Parser.Parse("a=b", ParserOptions.AllowProperties, _elementLocation);
            result.IsError.ShouldBeTrue();
            result.ErrorResource.ShouldBe("IllFormedEqualsInCondition");
        }

        /// <summary>
        /// Tests the special errors for "$(" and "$x" patterns.
        /// </summary>
        [Theory]
        [InlineData("$(", "IllFormedPropertyCloseParenthesisInCondition")]
        [InlineData("$x", "IllFormedPropertyOpenParenthesisInCondition")]
        internal void IllFormedProperty(string expression, string expectedResource)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowProperties, _elementLocation);
            result.IsError.ShouldBeTrue();
            result.ErrorResource.ShouldBe(expectedResource);
        }

        /// <summary>
        /// Tests that spaces adjacent to property name boundaries produce errors.
        /// </summary>
        [Theory]
        [InlineData("$(x )")]
        [InlineData("$( x)")]
        [InlineData("$([MSBuild]::DoSomething($(space ))")]
        [InlineData("$([MSBuild]::DoSomething($(_space ))")]
        internal void SpaceInPropertyProducesError(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowProperties, _elementLocation);
            result.IsError.ShouldBeTrue();
            result.ErrorResource.ShouldBe("IllFormedPropertySpaceInCondition");
        }

        /// <summary>
        /// Tests that spaces in the middle of property expressions (not adjacent to name boundaries) are OK.
        /// </summary>
        [Theory]
        [InlineData("$(x.StartsWith( 'y' ))")]
        [InlineData("$(x.StartsWith ('y'))")]
        [InlineData("$( x.StartsWith( $(SpacelessProperty) ) )")]
        [InlineData("$( x.StartsWith( $(_SpacelessProperty) ) )")]
        [InlineData("$(x.StartsWith('Foo', StringComparison.InvariantCultureIgnoreCase))")]
        internal void SpaceInMiddleOfPropertyIsValid(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowProperties, _elementLocation);
            result.IsError.ShouldBeFalse();
        }

        /// <summary>
        /// Tests the special errors for "@(" and "@x" and similar malformed item list patterns.
        /// </summary>
        [Theory]
        [InlineData("@(", "IllFormedItemListCloseParenthesisInCondition")]
        [InlineData("@x", "IllFormedItemListOpenParenthesisInCondition")]
        [InlineData("@(x", "IllFormedItemListCloseParenthesisInCondition")]
        [InlineData("@(x->'%(y)", "IllFormedItemListQuoteInCondition")]
        [InlineData("@(x->'%(y)', 'x", "IllFormedItemListQuoteInCondition")]
        [InlineData("@(x->'%(y)', 'x'", "IllFormedItemListCloseParenthesisInCondition")]
        internal void IllFormedItemList(string expression, string expectedResource)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.IsError.ShouldBeTrue();
            result.ErrorResource.ShouldBe(expectedResource);
        }

        /// <summary>
        /// Tests the special error for unterminated quotes.
        /// </summary>
        [Theory]
        [InlineData("false or 'abc")]
        [InlineData("'")]
        internal void IllFormedQuotedString(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.IsError.ShouldBeTrue();
            result.ErrorResource.ShouldBe("IllFormedQuotedStringInCondition");
        }

        /// <summary>
        /// Tests that item lists are rejected when only properties are allowed.
        /// </summary>
        [Theory]
        [InlineData("@(foo)")]
        [InlineData("1234 == '@(foo)'")]
        [InlineData("'1234 @(foo)' == ''")]
        internal void ItemListNotAllowedWhenDisabled(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowProperties, _elementLocation);
            result.IsError.ShouldBeTrue();
            result.ErrorResource.ShouldBe("ItemListNotAllowedInThisConditional");
        }

        /// <summary>
        /// Tests that unterminated quoted strings fail.
        /// </summary>
        [Fact]
        public void UnterminatedQuotedString()
        {
            ParseResult result = Parser.Parse("'$(DEBUG) == true", ParserOptions.AllowAll, _elementLocation);
            result.IsError.ShouldBeTrue();
        }

        /// <summary>
        /// Tests that numeric literals parse successfully.
        /// </summary>
        [Theory]
        [InlineData("1234 == 1234")]
        [InlineData("-1234 == -1234")]
        [InlineData("+1234 == +1234")]
        [InlineData("1234.1234 == 1234.1234")]
        [InlineData(".1234 == .1234")]
        [InlineData("1234. == 1234.")]
        [InlineData("0x1234 == 0x1234")]
        [InlineData("0X1234abcd == 0X1234abcd")]
        [InlineData("0x1234ABCD == 0x1234ABCD")]
        internal void NumericLiterals(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.IsError.ShouldBeFalse();
        }

        /// <summary>
        /// Tests that various single-token expressions parse successfully.
        /// </summary>
        [Theory]
        [InlineData("$(foo) == ''")]
        [InlineData("@(foo) == ''")]
        [InlineData("abcde == ''")]
        [InlineData("'abc-efg' == ''")]
        [InlineData("true and true")]
        [InlineData("true or true")]
        [InlineData("true and false")]
        [InlineData("true or false")]
        internal void BasicTokenTypes(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.IsError.ShouldBeFalse();
        }

        /// <summary>
        /// Tests that string edge cases with embedded item lists and escaped characters parse correctly.
        /// </summary>
        [Theory]
        [InlineData("'String with a $(Property) inside' == ''")]
        [InlineData("@(Foo, ' ') == ''")]
        [InlineData("'@(Foo, '' '')' == ''")]
        [InlineData("'%40(( ' == ''")]
        [InlineData("'@(Complex_ItemType-123, '';'')' == ''")]
        [InlineData("@(list, ' ') == ''")]
        [InlineData("@(files->'%(Filename)') == ''")]
        internal void StringEdgeCases(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.IsError.ShouldBeFalse();
        }

        /// <summary>
        /// Tests that function call expressions with various argument types parse correctly.
        /// </summary>
        [Theory]
        [InlineData("Foo()")]
        [InlineData("Foo( 1 )")]
        [InlineData("Foo( $(Property) )")]
        [InlineData("Foo( @(ItemList) )")]
        [InlineData("Foo( simplestring )")]
        [InlineData("Foo( 'Not a Simple String' )")]
        [InlineData("Foo( 'Not a Simple String', 1234 )")]
        [InlineData("Foo( $(Property), 'Not a Simple String', 1234 )")]
        [InlineData("Foo( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )")]
        internal void FunctionCallExpressions(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.IsError.ShouldBeFalse();
        }

        /// <summary>
        /// Tests that expressions with no whitespace between tokens parse correctly.
        /// </summary>
        [Theory]
        [InlineData("'abc-efg'==$(foo)")]
        [InlineData("$(debug)!=true")]
        [InlineData("$(VERSION)<5")]
        [InlineData("$(DEBUG) and $(FOO)")]
        internal void NoWhitespaceBetweenTokens(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.IsError.ShouldBeFalse();
        }

        /// <summary>
        /// Tests error position reporting.
        /// </summary>
        [Theory]
        [InlineData("1==0xFG", 7, ParserOptions.AllowAll)]                   // Position of G
        [InlineData("1==-0xF", 6, ParserOptions.AllowAll)]                   // Position of x
        [InlineData("1234=5678", 6, ParserOptions.AllowAll)]                 // Position of '5'
        [InlineData(" ", 2, ParserOptions.AllowAll)]                         // Position of End of Input
        [InlineData(" (", 3, ParserOptions.AllowAll)]                        // Position of End of Input
        [InlineData(" false or  ", 12, ParserOptions.AllowAll)]              // Position of End of Input
        [InlineData(" \"foo", 2, ParserOptions.AllowAll)]                    // Position of open quote
        [InlineData(" @(foo", 2, ParserOptions.AllowAll)]                    // Position of @
        [InlineData(" @(", 2, ParserOptions.AllowAll)]                       // Position of @
        [InlineData(" $", 2, ParserOptions.AllowAll)]                        // Position of $
        [InlineData(" $(foo", 2, ParserOptions.AllowAll)]                    // Position of $
        [InlineData(" $(", 2, ParserOptions.AllowAll)]                       // Position of $
        [InlineData(" @(foo)", 2, ParserOptions.AllowProperties)]            // Position of @
        [InlineData(" '@(foo)'", 3, ParserOptions.AllowProperties)]          // Position of @
        [InlineData("'%24%28x' == '%24(x''", 21, ParserOptions.AllowAll)]    // Position of extra quote
        internal void ErrorPosition(string expression, int expectedPosition, ParserOptions options)
        {
            ParseResult result = Parser.Parse(expression, options, _elementLocation);
            result.IsError.ShouldBeTrue();
            result.ErrorPosition.ShouldBe(expectedPosition);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void NegativeTests()
        {
            // Note no close quote ----------------------------------------------------V
            Assert.True(Parser.Parse("'a more complex' == 'asdf", ParserOptions.AllowAll, _elementLocation).IsError);

            // Note no close quote ----------------------------------------------------V
            Assert.True(Parser.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == 'simple string) and 'a more complex string' != $(quux)", ParserOptions.AllowAll, _elementLocation).IsError);

            // Correct tokens, but bad parse -----------V
            Assert.True(Parser.Parse("($(foo) == 'simple string') $(bar)", ParserOptions.AllowAll, _elementLocation).IsError);

            // Correct tokens, but bad parse -----------V
            Assert.True(Parser.Parse("=='x'", ParserOptions.AllowAll, _elementLocation).IsError);

            // Correct tokens, but bad parse -----------V
            Assert.True(Parser.Parse("==", ParserOptions.AllowAll, _elementLocation).IsError);

            // Correct tokens, but bad parse -----------V
            Assert.True(Parser.Parse(">", ParserOptions.AllowAll, _elementLocation).IsError);

            // Correct tokens, but bad parse -----------V
            Assert.True(Parser.Parse("true!=false==", ParserOptions.AllowAll, _elementLocation).IsError);

            // Correct tokens, but bad parse -----------V
            Assert.True(Parser.Parse("true!=false==true", ParserOptions.AllowAll, _elementLocation).IsError);

            // Correct tokens, but bad parse -----------V
            Assert.True(Parser.Parse("1==(2", ParserOptions.AllowAll, _elementLocation).IsError);
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

        [Theory]
        [InlineData("true", true)]
        [InlineData("True", true)]
        [InlineData("TRUE", true)]
        [InlineData("on", true)]
        [InlineData("ON", true)]
        [InlineData("yes", true)]
        [InlineData("YES", true)]
        [InlineData("false", false)]
        [InlineData("False", false)]
        [InlineData("FALSE", false)]
        [InlineData("off", false)]
        [InlineData("OFF", false)]
        [InlineData("no", false)]
        [InlineData("NO", false)]
        [InlineData("!true", false)]
        [InlineData("!True", false)]
        [InlineData("!false", true)]
        [InlineData("!False", true)]
        [InlineData("!on", false)]
        [InlineData("!off", true)]
        [InlineData("!yes", false)]
        [InlineData("!no", true)]
        public void BooleanKeyword_ProducesBooleanExpressionNode(string keyword, bool expected)
        {
            ParseResult result = Parser.Parse(keyword, ParserOptions.AllowAll, _elementLocation);
            result.Node.ShouldBeOfType<BooleanExpressionNode>();
            result.Node.TryEvaluateAsBoolean(null, out bool actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Theory]
        [InlineData("'true'", true)]
        [InlineData("'True'", true)]
        [InlineData("'on'", true)]
        [InlineData("'yes'", true)]
        [InlineData("'false'", false)]
        [InlineData("'off'", false)]
        [InlineData("'no'", false)]
        [InlineData("'!true'", false)]
        [InlineData("'!false'", true)]
        [InlineData("'!on'", false)]
        [InlineData("'!off'", true)]
        [InlineData("'!yes'", false)]
        [InlineData("'!no'", true)]
        public void QuotedBooleanKeyword_ProducesBooleanExpressionNode(string expression, bool expected)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.Node.ShouldBeOfType<BooleanExpressionNode>();
            result.Node.TryEvaluateAsBoolean(null, out bool actual).ShouldBeTrue();
            actual.ShouldBe(expected);
        }

        [Theory]
        [InlineData("'$(foo)'")]
        [InlineData("'hello'")]
        [InlineData("'truthy'")]
        [InlineData("'falsehood'")]
        public void NonBooleanString_ProducesStringExpressionNode(string expression)
        {
            ParseResult result = Parser.Parse(expression, ParserOptions.AllowAll, _elementLocation);
            result.Node.ShouldBeOfType<StringExpressionNode>();
        }
    }
}
