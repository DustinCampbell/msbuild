// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests;

public class ExpressionTreeTest(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData("true", true)]
    [InlineData("on", true)]
    [InlineData("yes", true)]
    [InlineData("false", false)]
    [InlineData("off", false)]
    [InlineData("no", false)]
    public void SimpleEvaluationTests(string expression, bool expected)
    {
        var parser = new Parser();
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);

        AssertParseEvaluate(parser, expression, expander, expected);
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
        var parser = new Parser();
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);

        AssertParseEvaluate(parser, expression, expander, expected);
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
        var parser = new Parser();
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);

        AssertParseEvaluate(parser, expression, expander, expected);
    }

    [Theory]
    [InlineData("true == on and 1234 < 1235", true)]
    public void AndandOrTests(string expression, bool expected)
    {
        var parser = new Parser();
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);

        AssertParseEvaluate(parser, expression, expander, expected);
    }

    [Fact]
    public void FunctionTests()
    {
        var parser = new Parser();
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var items = new ItemDictionary<ProjectItemInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null)
        {
            Metadata = StringMetadataTable.Empty,
        };

        string fileThatMustAlwaysExist = FileUtilities.GetTemporaryFileName();
        File.WriteAllText(fileThatMustAlwaysExist, "foo");
        string command = $"Exists('{fileThatMustAlwaysExist}')";

        GenericExpressionNode tree = parser.Parse(command, ParserOptions.AllowAll, ElementLocation.EmptyLocation);

        ConditionEvaluator.IConditionEvaluationState state =
            new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                command,
                expander,
                ExpanderOptions.ExpandAll,
                conditionedPropertiesInProject: null,
                Directory.GetCurrentDirectory(),
                ElementLocation.EmptyLocation,
                FileSystems.Default);

        bool value = tree.Evaluate(state);
        Assert.True(value);

        if (File.Exists(fileThatMustAlwaysExist))
        {
            File.Delete(fileThatMustAlwaysExist);
        }

        AssertParseEvaluate(parser, @"Exists('c:\IShouldntExist.sys')", expander, expected: false);
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
        var parser = new Parser();

        PropertyDictionary<ProjectPropertyInstance> properties = [
            ProjectPropertyInstance.Create("foo", "true"),
            ProjectPropertyInstance.Create("bar", "yes"),
            ProjectPropertyInstance.Create("one", "1"),
            ProjectPropertyInstance.Create("onepointzero", "1.0"),
            ProjectPropertyInstance.Create("two", "2"),
            ProjectPropertyInstance.Create("simple", "simplestring"),
            ProjectPropertyInstance.Create("complex", "This is a complex string"),
            ProjectPropertyInstance.Create("c1", "Another (complex) one."),
            ProjectPropertyInstance.Create("c2", "Another (complex) one."),
            ProjectPropertyInstance.Create("x86", "x86"),
            ProjectPropertyInstance.Create("no", "no")
        ];

        var items = new ItemDictionary<ProjectItemInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate(parser, expression, expander, expected);
    }

    [Theory]
    [InlineData("@(Compile) == 'foo.cs;bar.cs;baz.cs'", true)]
    [InlineData("@(Compile,' ') == 'foo.cs bar.cs baz.cs'", true)]
    [InlineData("@(Compile,'') == 'foo.csbar.csbaz.cs'", true)]
    [InlineData("@(Compile->'%(Filename)') == 'foo;bar;baz'", true)]
    [InlineData(@"@(Compile -> 'temp\%(Filename).xml', ' ') == 'temp\foo.xml temp\bar.xml temp\baz.xml'", true)]
    [InlineData("@(Compile->'', '') == ''", true)]
    [InlineData("@(Compile->'') == ';;'", true)]
    [InlineData("@(Compile->'%(Nonexistent)', '') == ''", true)]
    [InlineData("@(Compile->'%(Nonexistent)') == ';;'", true)]
    [InlineData("@(Boolean)", true)]
    [InlineData("@(Boolean) == true", true)]
    [InlineData("'@(Empty, ';')' == ''", true)]
    public void ItemListTests(string expression, bool expected)
    {
        var parser = new Parser();
        var properties = new PropertyDictionary<ProjectPropertyInstance>();

        var parentProject = new ProjectInstance(ProjectRootElement.Create());
        ItemDictionary<ProjectItemInstance> items = [
            new(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new(parentProject, "Compile", "baz.cs", parentProject.FullPath),
            new(parentProject, "Boolean", "true", parentProject.FullPath),
        ];

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate(parser, expression, expander, expected);
    }

    [Theory]
    [InlineData("'simplestring: true foo.cs;bar.cs;baz.cs' == '$(simple): $(foo) @(compile)'", true)]
    [InlineData("'$(c1) $(c2)' == 'Another (complex) one. Another (complex) one.'", true)]
    [InlineData("'CONTAINS%27QUOTE%27' == '$(TestQuote)'", true)]
    [InlineData("'Here%27s Johnny!' == '$(AnotherTestQuote)'", true)]
    [InlineData("'Test the %40 replacement' == $(Atsign)", true)]
    public void StringExpansionTests(string expression, bool expected)
    {
        var parser = new Parser();

        var parentProject = new ProjectInstance(ProjectRootElement.Create());
        ItemDictionary<ProjectItemInstance> items = [
            new(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new(parentProject, "Compile", "baz.cs", parentProject.FullPath),
        ];

        PropertyDictionary<ProjectPropertyInstance> properties = [
            ProjectPropertyInstance.Create("foo", "true"),
            ProjectPropertyInstance.Create("bar", "yes"),
            ProjectPropertyInstance.Create("one", "1"),
            ProjectPropertyInstance.Create("onepointzero", "1.0"),
            ProjectPropertyInstance.Create("two", "2"),
            ProjectPropertyInstance.Create("simple", "simplestring"),
            ProjectPropertyInstance.Create("complex", "This is a complex string"),
            ProjectPropertyInstance.Create("c1", "Another (complex) one."),
            ProjectPropertyInstance.Create("c2", "Another (complex) one."),
            ProjectPropertyInstance.Create("TestQuote", "Contains'Quote'"),
            ProjectPropertyInstance.Create("AnotherTestQuote", "Here's Johnny!"),
            ProjectPropertyInstance.Create("Atsign", "Test the @ replacement")
        ];

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate(parser, expression, expander, expected);
    }

    [Theory]
    [InlineData("(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", true)]
    [InlineData("(($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1", true)]
    [InlineData("!((($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1)", false)]
    public void ComplexTests(string expression, bool expected)
    {
        var parser = new Parser();

        var parentProject = new ProjectInstance(ProjectRootElement.Create());
        ItemDictionary<ProjectItemInstance> items = [
            new(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new(parentProject, "Compile", "baz.cs", parentProject.FullPath),
        ];

        PropertyDictionary<ProjectPropertyInstance> properties = [
            ProjectPropertyInstance.Create("foo", "true"),
            ProjectPropertyInstance.Create("bar", "yes"),
            ProjectPropertyInstance.Create("one", "1"),
            ProjectPropertyInstance.Create("onepointzero", "1.0"),
            ProjectPropertyInstance.Create("two", "2"),
            ProjectPropertyInstance.Create("simple", "simplestring"),
            ProjectPropertyInstance.Create("complex", "This is a complex string"),
            ProjectPropertyInstance.Create("c1", "Another (complex) one."),
            ProjectPropertyInstance.Create("c2", "Another (complex) one.")
        ];

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate(parser, expression, expander, expected);
    }

    /// <summary>
    /// Make sure when a non number is used in an expression which expects a numeric value that a error is emitted.
    /// </summary>
    [Fact]
    public void InvalidItemInConditionEvaluation()
    {
        var parser = new Parser();

        var parentProject = new ProjectInstance(ProjectRootElement.Create());
        ItemDictionary<ProjectItemInstance> items = [new(parentProject, "Compile", "a", parentProject.FullPath)];

        var properties = new PropertyDictionary<ProjectPropertyInstance>();

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluateThrow(parser, "@(Compile) > 0", expander);
    }

    [Theory]
    [InlineData("(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", true)]
    public void OldSyntaxTests(string expression, bool expected)
    {
        var parser = new Parser();

        var parentProject = new ProjectInstance(ProjectRootElement.Create());
        ItemDictionary<ProjectItemInstance> items = [
            new(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new(parentProject, "Compile", "baz.cs", parentProject.FullPath),
        ];

        PropertyDictionary<ProjectPropertyInstance> properties = [
            ProjectPropertyInstance.Create("foo", "true"),
            ProjectPropertyInstance.Create("bar", "yes"),
            ProjectPropertyInstance.Create("one", "1"),
            ProjectPropertyInstance.Create("onepointzero", "1.0"),
            ProjectPropertyInstance.Create("two", "2"),
            ProjectPropertyInstance.Create("simple", "simplestring"),
            ProjectPropertyInstance.Create("complex", "This is a complex string"),
            ProjectPropertyInstance.Create("c1", "Another (complex) one."),
            ProjectPropertyInstance.Create("c2", "Another (complex) one.")
        ];

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate(parser, expression, expander, expected);
    }

    [Fact]
    public void ConditionedPropertyUpdateTests()
    {
        var parser = new Parser();

        var parentProject = new ProjectInstance(ProjectRootElement.Create());
        ItemDictionary<ProjectItemInstance> items = [
            new(parentProject, "Compile", "foo.cs", parentProject.FullPath),
            new(parentProject, "Compile", "bar.cs", parentProject.FullPath),
            new(parentProject, "Compile", "baz.cs", parentProject.FullPath),
        ];

        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        var conditionedProperties = new Dictionary<string, List<string>>();

        ConditionEvaluator.IConditionEvaluationState state =
            new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                    condition: string.Empty,
                    expander,
                    ExpanderOptions.ExpandAll,
                    conditionedProperties,
                    Directory.GetCurrentDirectory(),
                    ElementLocation.EmptyLocation,
                    FileSystems.Default);

        AssertParseEvaluate(parser, "'0' == '1'", expander, expected: false, state);
        Assert.Empty(conditionedProperties);

        AssertParseEvaluate(parser, "$(foo) == foo", expander, expected: false, state);
        _ = Assert.Single(conditionedProperties);
        _ = Assert.Single(conditionedProperties["foo"]);

        AssertParseEvaluate(parser, "'$(foo)' != 'bar'", expander, expected: true, state);
        _ = Assert.Single(conditionedProperties);
        Assert.Equal(2, conditionedProperties["foo"].Count);

        AssertParseEvaluate(parser, "'$(branch)|$(build)|$(platform)' == 'lab22dev|debug|x86'", expander, false, state);
        Assert.Equal(4, conditionedProperties.Count);
        Assert.Equal(2, conditionedProperties["foo"].Count);
        _ = Assert.Single(conditionedProperties["branch"]);
        _ = Assert.Single(conditionedProperties["build"]);
        _ = Assert.Single(conditionedProperties["platform"]);

        AssertParseEvaluate(parser, "'$(branch)|$(build)|$(platform)' == 'lab21|debug|x86'", expander, false, state);
        Assert.Equal(4, conditionedProperties.Count);
        Assert.Equal(2, conditionedProperties["foo"].Count);
        Assert.Equal(2, conditionedProperties["branch"].Count);
        _ = Assert.Single(conditionedProperties["build"]);
        _ = Assert.Single(conditionedProperties["platform"]);

        AssertParseEvaluate(parser, "'$(branch)|$(build)|$(platform)' == 'lab23|retail|ia64'", expander, false, state);
        Assert.Equal(4, conditionedProperties.Count);
        Assert.Equal(2, conditionedProperties["foo"].Count);
        Assert.Equal(3, conditionedProperties["branch"].Count);
        Assert.Equal(2, conditionedProperties["build"].Count);
        Assert.Equal(2, conditionedProperties["platform"].Count);

        DumpDictionary(conditionedProperties);
    }

    [Theory]
    [InlineData("!true", false)]
    [InlineData("!(true)", false)]
    [InlineData("!($(foo) <= 5)", false)]
    [InlineData("!($(foo) <= 5 and $(bar) >= 15)", false)]
    public void NotTests(string expression, bool expected)
    {
        _output.WriteLine("NegationParseTest()");
        var parser = new Parser();

        PropertyDictionary<ProjectPropertyInstance> properties = [
            ProjectPropertyInstance.Create("foo", "4"),
            ProjectPropertyInstance.Create("bar", "32")
        ];

        var items = new ItemDictionary<ProjectItemInstance>();

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
            properties, items, FileSystems.Default, loggingContext: null);

        AssertParseEvaluate(parser, expression, expander, expected);
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
        var parser = new Parser();
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);

        AssertParseEvaluateThrow(parser, expression, expander);
    }

    private void DumpDictionary(Dictionary<string, List<string>> propertyDictionary)
    {
        var line = new StringBuilder();

        foreach (KeyValuePair<string, List<string>> entry in propertyDictionary)
        {
            _ = line.Clear();
            _ = line.Append($"  {entry.Key}:\t");

            foreach (string property in entry.Value)
            {
                _ = line.Append($"{property}, ");
            }

            _output.WriteLine(line.ToString());
        }
    }

    private void AssertParseEvaluate(
        Parser parser,
        string expression,
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander,
        bool expected,
        ConditionEvaluator.IConditionEvaluationState? state = null)
    {
        expander.Metadata ??= StringMetadataTable.Empty;

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

    private void AssertParseEvaluateThrow(
        Parser parser,
        string expression,
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander,
        ConditionEvaluator.IConditionEvaluationState? state = null)
    {
        expander.Metadata ??= StringMetadataTable.Empty;

        _ = Assert.Throws<InvalidProjectFileException>(() =>
        {
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
