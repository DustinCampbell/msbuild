// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class ExpressionTreeTest
    {
        /// <summary>
        /// </summary>
        [Fact]
        public void SimpleEvaluationTests()
        {
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), FileSystems.Default);

            AssertParseEvaluate("true", expander, true);
            AssertParseEvaluate("on", expander, true);
            AssertParseEvaluate("yes", expander, true);
            AssertParseEvaluate("false", expander, false);
            AssertParseEvaluate("off", expander, false);
            AssertParseEvaluate("no", expander, false);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void EqualityTests()
        {
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), FileSystems.Default);

            AssertParseEvaluate("true == on", expander, true);
            AssertParseEvaluate("TrUe == On", expander, true);
            AssertParseEvaluate("true != false", expander, true);
            AssertParseEvaluate("true==!false", expander, true);
            AssertParseEvaluate("4 != 5", expander, true);
            AssertParseEvaluate("-4 < 4", expander, true);
            AssertParseEvaluate("5 == +5", expander, true);
            AssertParseEvaluate("4 == 4.0", expander, true);
            AssertParseEvaluate("4 == 4.0", expander, true);
            AssertParseEvaluate(".45 == '.45'", expander, true);
            AssertParseEvaluate("4 == '4'", expander, true);
            AssertParseEvaluate("'0' == '4'", expander, false);
            AssertParseEvaluate("4 == 0x0004", expander, true);
            AssertParseEvaluate("0.0 == 0", expander, true);
            AssertParseEvaluate("simplestring == 'simplestring'", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void RelationalTests()
        {
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), FileSystems.Default);

            AssertParseEvaluate("1234 < 1235", expander, true);
            AssertParseEvaluate("1234 <= 1235", expander, true);
            AssertParseEvaluate("1235 < 1235", expander, false);
            AssertParseEvaluate("1234 <= 1234", expander, true);
            AssertParseEvaluate("1235 <= 1234", expander, false);
            AssertParseEvaluate("1235 > 1234", expander, true);
            AssertParseEvaluate("1235 >= 1235", expander, true);
            AssertParseEvaluate("1235 >= 1234", expander, true);
            AssertParseEvaluate("0.0==0", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void AndandOrTests()
        {
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), FileSystems.Default);

            AssertParseEvaluate("true == on and 1234 < 1235", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void FunctionTests()
        {
            GenericExpressionNode tree;
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), new ItemDictionary<ProjectItemInstance>(), FileSystems.Default, null);
            expander.Metadata = new StringMetadataTable(null);
            bool value;

            string fileThatMustAlwaysExist = FileUtilities.GetTemporaryFileName();
            File.WriteAllText(fileThatMustAlwaysExist, "foo");
            string command = "Exists('" + fileThatMustAlwaysExist + "')";
            tree = Parser.Parse(command, ParserOptions.AllowAll, ElementLocation.EmptyLocation);

            ConditionEvaluator.IConditionEvaluationState state =
                            new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                                    command,
                                    expander,
                                    ExpanderOptions.ExpandAll,
                                    null,
                                    Directory.GetCurrentDirectory(),
                                    ElementLocation.EmptyLocation,
                                    FileSystems.Default);

            value = tree.Evaluate(state);
            Assert.True(value);

            if (File.Exists(fileThatMustAlwaysExist))
            {
                File.Delete(fileThatMustAlwaysExist);
            }

            AssertParseEvaluate("Exists('c:\\IShouldntExist.sys')", expander, false);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void PropertyTests()
        {
            var propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
            propertyBag.Set(ProjectPropertyInstance.Create("foo", "true"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "yes"));
            propertyBag.Set(ProjectPropertyInstance.Create("one", "1"));
            propertyBag.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
            propertyBag.Set(ProjectPropertyInstance.Create("two", "2"));
            propertyBag.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
            propertyBag.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
            propertyBag.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("x86", "x86"));
            propertyBag.Set(ProjectPropertyInstance.Create("no", "no"));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, new ItemDictionary<ProjectItemInstance>(), FileSystems.Default, null);
            AssertParseEvaluate("$(foo)", expander, true);
            AssertParseEvaluate("!$(foo)", expander, false);
            // Test properties with strings
            AssertParseEvaluate("$(simple) == 'simplestring'", expander, true);
            AssertParseEvaluate("'simplestring' == $(simple)", expander, true);
            AssertParseEvaluate("'foo' != $(simple)", expander, true);
            AssertParseEvaluate("'simplestring' == '$(simple)'", expander, true);
            AssertParseEvaluate("$(simple) == simplestring", expander, true);
            AssertParseEvaluate("$(x86) == x86", expander, true);
            AssertParseEvaluate("$(x86)==x86", expander, true);
            AssertParseEvaluate("x86==$(x86)", expander, true);
            AssertParseEvaluate("$(c1) == $(c2)", expander, true);
            AssertParseEvaluate("'$(c1)' == $(c2)", expander, true);
            AssertParseEvaluate("$(c1) != $(simple)", expander, true);
            AssertParseEvaluate("$(c1) == $(c2)", expander, true);
            // Test properties with numbers
            AssertParseEvaluate("$(one) == $(onepointzero)", expander, true);
            AssertParseEvaluate("$(one) <= $(two)", expander, true);
            AssertParseEvaluate("$(two) > $(onepointzero)", expander, true);
            AssertParseEvaluate("$(one) != $(two)", expander, true);
            AssertParseEvaluate("'$(no)'==false", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ItemListTests()
        {
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Boolean", "true", parentProject.FullPath));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), itemBag, FileSystems.Default, null);

            AssertParseEvaluate("@(Compile) == 'foo.cs;bar.cs;baz.cs'", expander, true);
            AssertParseEvaluate("@(Compile,' ') == 'foo.cs bar.cs baz.cs'", expander, true);
            AssertParseEvaluate("@(Compile,'') == 'foo.csbar.csbaz.cs'", expander, true);
            AssertParseEvaluate("@(Compile->'%(Filename)') == 'foo;bar;baz'", expander, true);
            AssertParseEvaluate("@(Compile -> 'temp\\%(Filename).xml', ' ') == 'temp\\foo.xml temp\\bar.xml temp\\baz.xml'", expander, true);
            AssertParseEvaluate("@(Compile->'', '') == ''", expander, true);
            AssertParseEvaluate("@(Compile->'') == ';;'", expander, true);
            AssertParseEvaluate("@(Compile->'%(Nonexistent)', '') == ''", expander, true);
            AssertParseEvaluate("@(Compile->'%(Nonexistent)') == ';;'", expander, true);
            AssertParseEvaluate("@(Boolean)", expander, true);
            AssertParseEvaluate("@(Boolean) == true", expander, true);
            AssertParseEvaluate("'@(Empty, ';')' == ''", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void StringExpansionTests()
        {
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
            propertyBag.Set(ProjectPropertyInstance.Create("foo", "true"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "yes"));
            propertyBag.Set(ProjectPropertyInstance.Create("one", "1"));
            propertyBag.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
            propertyBag.Set(ProjectPropertyInstance.Create("two", "2"));
            propertyBag.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
            propertyBag.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
            propertyBag.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("TestQuote", "Contains'Quote'"));
            propertyBag.Set(ProjectPropertyInstance.Create("AnotherTestQuote", "Here's Johnny!"));
            propertyBag.Set(ProjectPropertyInstance.Create("Atsign", "Test the @ replacement"));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag, FileSystems.Default, null);

            AssertParseEvaluate("'simplestring: true foo.cs;bar.cs;baz.cs' == '$(simple): $(foo) @(compile)'", expander, true);
            AssertParseEvaluate("'$(c1) $(c2)' == 'Another (complex) one. Another (complex) one.'", expander, true);
            AssertParseEvaluate("'CONTAINS%27QUOTE%27' == '$(TestQuote)'", expander, true);
            AssertParseEvaluate("'Here%27s Johnny!' == '$(AnotherTestQuote)'", expander, true);
            AssertParseEvaluate("'Test the %40 replacement' == $(Atsign)", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ComplexTests()
        {
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
            propertyBag.Set(ProjectPropertyInstance.Create("foo", "true"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "yes"));
            propertyBag.Set(ProjectPropertyInstance.Create("one", "1"));
            propertyBag.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
            propertyBag.Set(ProjectPropertyInstance.Create("two", "2"));
            propertyBag.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
            propertyBag.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
            propertyBag.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag, FileSystems.Default, null);

            AssertParseEvaluate("(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", expander, true);
            AssertParseEvaluate("(($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1", expander, true);
            AssertParseEvaluate("!((($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1)", expander, false);
        }

        /// <summary>
        /// Make sure when a non number is used in an expression which expects a numeric value that a error is emitted.
        /// </summary>
        [Fact]
        public void InvalidItemInConditionEvaluation()
        {
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "a", parentProject.FullPath));

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag, FileSystems.Default, null);

            AssertParseEvaluateThrow("@(Compile) > 0", expander, null);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void OldSyntaxTests()
        {
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();

            propertyBag.Set(ProjectPropertyInstance.Create("foo", "true"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "yes"));
            propertyBag.Set(ProjectPropertyInstance.Create("one", "1"));
            propertyBag.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
            propertyBag.Set(ProjectPropertyInstance.Create("two", "2"));
            propertyBag.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
            propertyBag.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
            propertyBag.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag, FileSystems.Default, null);

            AssertParseEvaluate("(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ConditionedPropertyUpdateTests()
        {
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), itemBag, FileSystems.Default, null);
            Dictionary<string, List<string>> conditionedProperties = new Dictionary<string, List<string>>();
            ConditionEvaluator.IConditionEvaluationState state =
                               new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                                       String.Empty,
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
            DumpDictionary(conditionedProperties);
        }

        private static void DumpDictionary(Dictionary<string, List<string>> propertyDictionary)
        {
            foreach (KeyValuePair<string, List<String>> entry in propertyDictionary)
            {
                Console.Write("  {0}:\t", entry.Key);

                List<String> properties = entry.Value;

                foreach (string property in properties)
                {
                    Console.Write("{0}, ", property);
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void NotTests()
        {
            Console.WriteLine("NegationParseTest()");

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
            propertyBag.Set(ProjectPropertyInstance.Create("foo", "4"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "32"));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, new ItemDictionary<ProjectItemInstance>(), FileSystems.Default, null);

            AssertParseEvaluate("!true", expander, false);
            AssertParseEvaluate("!(true)", expander, false);
            AssertParseEvaluate("!($(foo) <= 5)", expander, false);
            AssertParseEvaluate("!($(foo) <= 5 and $(bar) >= 15)", expander, false);
        }

        private void AssertParseEvaluate(string expression, Expander<ProjectPropertyInstance, ProjectItemInstance> expander, bool expected)
            => AssertParseEvaluate(expression, expander, expected, null);

        private void AssertParseEvaluate(string expression, Expander<ProjectPropertyInstance, ProjectItemInstance> expander, bool expected, ConditionEvaluator.IConditionEvaluationState state)
        {
            if (expander.Metadata == null)
            {
                expander.Metadata = new StringMetadataTable(null);
            }

            GenericExpressionNode tree = Parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);

            if (state == null)
            {
                state =
                new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                        String.Empty,
                        expander,
                        ExpanderOptions.ExpandAll,
                        null,
                        Directory.GetCurrentDirectory(),
                        ElementLocation.EmptyLocation,
                        FileSystems.Default);
            }

            bool result = tree.Evaluate(state);
            Assert.Equal(expected, result);
        }

        private void AssertParseEvaluateThrow(string expression, Expander<ProjectPropertyInstance, ProjectItemInstance> expander)
            => AssertParseEvaluateThrow(expression, expander, null);

        private void AssertParseEvaluateThrow(string expression, Expander<ProjectPropertyInstance, ProjectItemInstance> expander, ConditionEvaluator.IConditionEvaluationState state)
        {
            bool fExceptionCaught;

            if (expander.Metadata == null)
            {
                expander.Metadata = new StringMetadataTable(null);
            }

            try
            {
                fExceptionCaught = false;
                GenericExpressionNode tree = Parser.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);
                if (state == null)
                {
                    state =
                    new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>(
                            String.Empty,
                            expander,
                            ExpanderOptions.ExpandAll,
                            null,
                            Directory.GetCurrentDirectory(),
                            ElementLocation.EmptyLocation,
                            FileSystems.Default);
                }
                tree.Evaluate(state);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }

            Assert.True(fExceptionCaught);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void NegativeTests()
        {
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), FileSystems.Default);

            AssertParseEvaluateThrow("foo", expander);
            AssertParseEvaluateThrow("0", expander);
            AssertParseEvaluateThrow("$(platform) == xx > 1==2", expander);
            AssertParseEvaluateThrow("!0", expander);
            AssertParseEvaluateThrow(">", expander);
            AssertParseEvaluateThrow("true!=false==", expander);
            AssertParseEvaluateThrow("()", expander);
            AssertParseEvaluateThrow("!1", expander);
            AssertParseEvaluateThrow("true!=false==true", expander);
            AssertParseEvaluateThrow("'a'>'a'", expander);
            AssertParseEvaluateThrow("=='x'", expander);
            AssertParseEvaluateThrow("==", expander);
            AssertParseEvaluateThrow("1==(2", expander);
            AssertParseEvaluateThrow("'a'==('a'=='a')", expander);
            AssertParseEvaluateThrow("true == on and ''", expander);
            AssertParseEvaluateThrow("'' or 'true'", expander);
        }
    }
}
