// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ExpressionTreeTest
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("on", true)]
    [InlineData("yes", true)]
    [InlineData("false", false)]
    [InlineData("off", false)]
    [InlineData("no", false)]
    public void SimpleEvaluationTests(string expression, bool expected)
    {
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties: new PropertyDictionary<ProjectPropertyInstance>(),
            FileSystems.Default);

        AssertParseEvaluate(expression, expander, expected);
    }

    [Theory]
    [InlineData("true == on", true)]
    [InlineData("TrUe == On", true)]
    [InlineData("true != false", true)]
    [InlineData("true==!false", true)]
    [InlineData("4 != 5", true)]
    [InlineData("-4 < 4", true)]
    [InlineData("5 == +5", true)]
    [InlineData("4 == 4.0", true)]
    [InlineData(".45 == '.45'", true)]
    [InlineData("4 == '4'", true)]
    [InlineData("'0' == '4'", false)]
    [InlineData("4 == 0x0004", true)]
    [InlineData("0.0 == 0", true)]
    [InlineData("simplestring == 'simplestring'", true)]
    public void EqualityTests(string expression, bool expected)
    {
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties: new PropertyDictionary<ProjectPropertyInstance>(),
            FileSystems.Default);

        AssertParseEvaluate(expression, expander, expected);
    }

    [Theory]
    [InlineData("1234 < 1235", true)]
    [InlineData("1234 <= 1235", true)]
    [InlineData("1235 < 1235", false)]
    [InlineData("1234 <= 1234", true)]
    [InlineData("1235 <= 1234", false)]
    [InlineData("1235 > 1234", true)]
    [InlineData("1235 >= 1235", true)]
    [InlineData("1235 >= 1234", true)]
    [InlineData("0.0==0", true)]
    public void RelationalTests(string expression, bool expected)
    {
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties: new PropertyDictionary<ProjectPropertyInstance>(),
            FileSystems.Default);

        AssertParseEvaluate(expression, expander, expected);
    }

    [Fact]
    public void AndandOrTests()
    {
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties: new PropertyDictionary<ProjectPropertyInstance>(),
            FileSystems.Default);

        AssertParseEvaluate("true == on and 1234 < 1235", expander, true);
    }

    [Fact]
    public void FunctionTests()
    {
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties: new PropertyDictionary<ProjectPropertyInstance>(),
            items: new ItemDictionary<ProjectItemInstance>(),
            FileSystems.Default,
            loggingContext: null);

        using (expander.OpenMetadataScope(StringMetadataTable.Empty))
        {
            string fileThatMustAlwaysExist = FileUtilities.GetTemporaryFileName();
            File.WriteAllText(fileThatMustAlwaysExist, "foo");

            string condition = "Exists('" + fileThatMustAlwaysExist + "')";

            var parser = new Parser();
            GenericExpressionNode tree = parser.Parse(condition, ParserOptions.AllowAll, ElementLocation.EmptyLocation);

            var state = new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                condition,
                expander,
                ExpanderOptions.ExpandAll,
                conditionedPropertiesInProject: null,
                Directory.GetCurrentDirectory(),
                ElementLocation.EmptyLocation,
                FileSystems.Default);

            Assert.True(tree.Evaluate(state));

            if (File.Exists(fileThatMustAlwaysExist))
            {
                File.Delete(fileThatMustAlwaysExist);
            }

            AssertParseEvaluate("Exists('c:\\IShouldntExist.sys')", expander, expected: false);
        }
    }

    [Theory]
    [InlineData("$(foo)", true)]
    [InlineData("!$(foo)", false)]
    [InlineData("$(simple) == 'simplestring'", true)]
    [InlineData("'simplestring' == $(simple)", true)]
    [InlineData("'foo' != $(simple)", true)]
    [InlineData("'simplestring' == '$(simple)'", true)]
    [InlineData("$(simple) == simplestring", true)]
    [InlineData("$(x86) == x86", true)]
    [InlineData("$(x86)==x86", true)]
    [InlineData("x86==$(x86)", true)]
    [InlineData("$(c1) == $(c2)", true)]
    [InlineData("'$(c1)' == $(c2)", true)]
    [InlineData("$(c1) != $(simple)", true)]
    [InlineData("$(one) == $(onepointzero)", true)]
    [InlineData("$(one) <= $(two)", true)]
    [InlineData("$(two) > $(onepointzero)", true)]
    [InlineData("$(one) != $(two)", true)]
    [InlineData("'$(no)'==false", true)]
    public void PropertyTests(string expression, bool expected)
    {
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("foo", "true"));
        properties.Set(ProjectPropertyInstance.Create("bar", "yes"));
        properties.Set(ProjectPropertyInstance.Create("one", "1"));
        properties.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
        properties.Set(ProjectPropertyInstance.Create("two", "2"));
        properties.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
        properties.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
        properties.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
        properties.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));
        properties.Set(ProjectPropertyInstance.Create("x86", "x86"));
        properties.Set(ProjectPropertyInstance.Create("no", "no"));

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties,
            items: new ItemDictionary<ProjectItemInstance>(),
            FileSystems.Default,
            loggingContext: null);

        AssertParseEvaluate(expression, expander, expected);
    }

    [Theory]
    [InlineData("@(Compile) == 'foo.cs;bar.cs;baz.cs'", true)]
    [InlineData("@(Compile,' ') == 'foo.cs bar.cs baz.cs'", true)]
    [InlineData("@(Compile,'') == 'foo.csbar.csbaz.cs'", true)]
    [InlineData("@(Compile->'%(Filename)') == 'foo;bar;baz'", true)]
    [InlineData("@(Compile -> 'temp\\%(Filename).xml', ' ') == 'temp\\foo.xml temp\\bar.xml temp\\baz.xml'", true)]
    [InlineData("@(Compile->'', '') == ''", true)]
    [InlineData("@(Compile->'') == ';;'", true)]
    [InlineData("@(Compile->'%(Nonexistent)', '') == ''", true)]
    [InlineData("@(Compile->'%(Nonexistent)') == ';;'", true)]
    [InlineData("@(Boolean)", true)]
    [InlineData("@(Boolean) == true", true)]
    [InlineData("'@(Empty, ';')' == ''", true)]
    public void ItemListTests(string expression, bool expected)
    {
        var parentProject = new ProjectInstance(ProjectRootElement.Create());

        ItemDictionary<ProjectItemInstance> items =
        [
            new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Boolean", "true", parentProject.FullPath),
        ];

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties: new PropertyDictionary<ProjectPropertyInstance>(),
            items,
            FileSystems.Default,
            loggingContext: null);

        AssertParseEvaluate(expression, expander, expected);
    }

    [Theory]
    [InlineData("'simplestring: true foo.cs;bar.cs;baz.cs' == '$(simple): $(foo) @(compile)'", true)]
    [InlineData("'$(c1) $(c2)' == 'Another (complex) one. Another (complex) one.'", true)]
    [InlineData("'CONTAINS%27QUOTE%27' == '$(TestQuote)'", true)]
    [InlineData("'Here%27s Johnny!' == '$(AnotherTestQuote)'", true)]
    [InlineData("'Test the %40 replacement' == $(Atsign)", true)]
    public void StringExpansionTests(string expression, bool expected)
    {
        var parentProject = new ProjectInstance(ProjectRootElement.Create());

        ItemDictionary<ProjectItemInstance> items =
        [
            new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath),
        ];

        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("foo", "true"));
        properties.Set(ProjectPropertyInstance.Create("bar", "yes"));
        properties.Set(ProjectPropertyInstance.Create("one", "1"));
        properties.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
        properties.Set(ProjectPropertyInstance.Create("two", "2"));
        properties.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
        properties.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
        properties.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
        properties.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));
        properties.Set(ProjectPropertyInstance.Create("TestQuote", "Contains'Quote'"));
        properties.Set(ProjectPropertyInstance.Create("AnotherTestQuote", "Here's Johnny!"));
        properties.Set(ProjectPropertyInstance.Create("Atsign", "Test the @ replacement"));

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate(expression, expander, expected);
    }

    [Theory]
    [InlineData("(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", true)]
    [InlineData("(($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1", true)]
    [InlineData("!((($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1)", false)]
    public void ComplexTests(string expression, bool expected)
    {
        var parentProject = new ProjectInstance(ProjectRootElement.Create());

        ItemDictionary<ProjectItemInstance> items =
        [
            new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath),
        ];

        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("foo", "true"));
        properties.Set(ProjectPropertyInstance.Create("bar", "yes"));
        properties.Set(ProjectPropertyInstance.Create("one", "1"));
        properties.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
        properties.Set(ProjectPropertyInstance.Create("two", "2"));
        properties.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
        properties.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
        properties.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
        properties.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate(expression, expander, expected);
    }

    /// <summary>
    /// Make sure when a non number is used in an expression which expects a numeric value that a error is emitted.
    /// </summary>
    [Fact]
    public void InvalidItemInConditionEvaluation()
    {
        var parentProject = new ProjectInstance(ProjectRootElement.Create());

        ItemDictionary<ProjectItemInstance> items = [new ProjectItemInstance(parentProject, "Compile", "a", parentProject.FullPath)];

        var properties = new PropertyDictionary<ProjectPropertyInstance>();

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluateThrow("@(Compile) > 0", expander, null);
    }

    [Fact]
    public void OldSyntaxTests()
    {
        var parentProject = new ProjectInstance(ProjectRootElement.Create());

        ItemDictionary<ProjectItemInstance> items =
        [
            new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath),
        ];

        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("foo", "true"));
        properties.Set(ProjectPropertyInstance.Create("bar", "yes"));
        properties.Set(ProjectPropertyInstance.Create("one", "1"));
        properties.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
        properties.Set(ProjectPropertyInstance.Create("two", "2"));
        properties.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
        properties.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
        properties.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
        properties.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate("(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", expander, true);
    }

    [Fact]
    public void ConditionedPropertyUpdateTests()
    {
        var parentProject = new ProjectInstance(ProjectRootElement.Create());

        ItemDictionary<ProjectItemInstance> items =
        [
            new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath),
        ];

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties: new PropertyDictionary<ProjectPropertyInstance>(),
            items,
            FileSystems.Default,
            loggingContext: null);

        var conditionedProperties = new Dictionary<string, List<string>>();
        var state = new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
            condition: string.Empty,
            expander,
            ExpanderOptions.ExpandAll,
            conditionedProperties,
            Directory.GetCurrentDirectory(),
            ElementLocation.EmptyLocation,
            FileSystems.Default);

        AssertParseEvaluate("'0' == '1'", expander, false, state);
        Assert.Empty(conditionedProperties);

        AssertParseEvaluate("$(foo) == foo", expander, false, state);
        Assert.Single(conditionedProperties);
        List<string> properties = conditionedProperties["foo"];
        Assert.Single(properties);

        AssertParseEvaluate("'$(foo)' != 'bar'", expander, true, state);
        Assert.Single(conditionedProperties);
        properties = conditionedProperties["foo"];
        Assert.Equal(2, properties.Count);

        AssertParseEvaluate("'$(branch)|$(build)|$(platform)' == 'lab22dev|debug|x86'", expander, false, state);
        Assert.Equal(4, conditionedProperties.Count);
        properties = conditionedProperties["foo"];
        Assert.Equal(2, properties.Count);
        properties = conditionedProperties["branch"];
        Assert.Single(properties);
        properties = conditionedProperties["build"];
        Assert.Single(properties);
        properties = conditionedProperties["platform"];
        Assert.Single(properties);

        AssertParseEvaluate("'$(branch)|$(build)|$(platform)' == 'lab21|debug|x86'", expander, false, state);
        Assert.Equal(4, conditionedProperties.Count);
        properties = conditionedProperties["foo"];
        Assert.Equal(2, properties.Count);
        properties = conditionedProperties["branch"];
        Assert.Equal(2, properties.Count);
        properties = conditionedProperties["build"];
        Assert.Single(properties);
        properties = conditionedProperties["platform"];
        Assert.Single(properties);

        AssertParseEvaluate("'$(branch)|$(build)|$(platform)' == 'lab23|retail|ia64'", expander, false, state);
        Assert.Equal(4, conditionedProperties.Count);
        properties = conditionedProperties["foo"];
        Assert.Equal(2, properties.Count);
        properties = conditionedProperties["branch"];
        Assert.Equal(3, properties.Count);
        properties = conditionedProperties["build"];
        Assert.Equal(2, properties.Count);
        properties = conditionedProperties["platform"];
        Assert.Equal(2, properties.Count);
    }

    [Theory]
    [InlineData("!true", false)]
    [InlineData("!(true)", false)]
    [InlineData("!($(foo) <= 5)", false)]
    [InlineData("!($(foo) <= 5 and $(bar) >= 15)", false)]
    public void NotTests(string expression, bool expected)
    {
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("foo", "4"));
        properties.Set(ProjectPropertyInstance.Create("bar", "32"));

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties,
            items: new ItemDictionary<ProjectItemInstance>(),
            FileSystems.Default,
            loggingContext: null);

        AssertParseEvaluate(expression, expander, expected);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("0")]
    [InlineData("$(platform) == xx > 1==2")]
    [InlineData("!0")]
    [InlineData(">")]
    [InlineData("true!=false==")]
    [InlineData("()")]
    [InlineData("!1")]
    [InlineData("true!=false==true")]
    [InlineData("'a'>'a'")]
    [InlineData("=='x'")]
    [InlineData("==")]
    [InlineData("1==(2")]
    [InlineData("'a'==('a'=='a')")]
    [InlineData("true == on and ''")]
    [InlineData("'' or 'true'")]
    public void NegativeTests(string expression)
    {
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties: new PropertyDictionary<ProjectPropertyInstance>(),
            FileSystems.Default);

        AssertParseEvaluateThrow(expression, expander);
    }

    private void AssertParseEvaluate(
        string expression,
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander,
        bool expected,
        ConditionEvaluator.IConditionEvaluationState? state = null)
    {
        using (expander.OpenMetadataScope(StringMetadataTable.Empty))
        {
            var parser = new Parser();
            GenericExpressionNode tree = parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);

            state ??= new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                condition: string.Empty,
                expander,
                ExpanderOptions.ExpandAll,
                conditionedPropertiesInProject: null,
                Directory.GetCurrentDirectory(),
                ElementLocation.EmptyLocation,
                FileSystems.Default);

            bool result = tree.Evaluate(state);
            Assert.Equal(expected, result);
        }
    }

    private void AssertParseEvaluateThrow(
        string expression,
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander,
        ConditionEvaluator.IConditionEvaluationState? state = null)
    {
        using (expander.OpenMetadataScope(StringMetadataTable.Empty))
        {
            _ = Assert.Throws<InvalidProjectFileException>(() =>
            {
                var parser = new Parser();
                GenericExpressionNode tree = parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);

                state ??= new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                    condition: string.Empty,
                    expander,
                    ExpanderOptions.ExpandAll,
                    conditionedPropertiesInProject: null,
                    Directory.GetCurrentDirectory(),
                    ElementLocation.EmptyLocation,
                    FileSystems.Default);

                _ = tree.Evaluate(state);
            });
        }
    }
}
