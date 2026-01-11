// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public class ConditionalEvaluatorTests
{
    public static TheoryData<string, bool> BooleanLiteralTestData
    {
        get
        {
            var data = new TheoryData<string, bool>();

            AddPermuations("true", "false");
            AddPermuations("true", "off");
            AddPermuations("true", "no");
            AddPermuations("on", "false");
            AddPermuations("on", "off");
            AddPermuations("on", "no");
            AddPermuations("yes", "false");
            AddPermuations("yes", "off");
            AddPermuations("yes", "no");

            return data;

            void AddPermuations(string @true, string @false)
            {
                // Basic boolean literals
                data.Add($"{@true}", true);
                data.Add($"!{@true}", false);
                data.Add($"'{@true}'", true);
                data.Add($"'!{@true}'", false);

                data.Add($"{@false}", false);
                data.Add($"!{@false}", true);
                data.Add($"'{@false}'", false);
                data.Add($"'!{@false}'", true);

                // Equality and inequality comparisons
                data.Add($"{@true} == {@true}", true);
                data.Add($"{@true} == {@false}", false);
                data.Add($"{@true} != {@true}", false);
                data.Add($"{@true} != {@false}", true);
                data.Add($"{@false} == {@false}", true);
                data.Add($"{@false} == {@true}", false);
                data.Add($"{@false} != {@false}", false);
                data.Add($"{@false} != {@true}", true);

                // AND operations
                data.Add($"{@true} and {@true}", true);
                data.Add($"{@true} and {@false}", false);
                data.Add($"{@false} and {@true}", false);
                data.Add($"{@false} and {@false}", false);
                data.Add($"!{@true} and {@true}", false);
                data.Add($"{@true} and !{@false}", true);
                data.Add($"!{@true} and !{@false}", false);

                // OR operations
                data.Add($"{@true} or {@true}", true);
                data.Add($"{@true} or {@false}", true);
                data.Add($"{@false} or {@true}", true);
                data.Add($"{@false} or {@false}", false);
                data.Add($"!{@true} or {@true}", true);
                data.Add($"{@false} or !{@false}", true);
                data.Add($"!{@true} or !{@true}", false);

                // Complex combinations with AND/OR
                data.Add($"{@true} and {@true} or {@false}", true);
                data.Add($"{@false} and {@true} or {@true}", true);
                data.Add($"{@true} or {@false} and {@false}", true);
                data.Add($"{@false} or {@false} and {@true}", false);
                data.Add($"{@true} and {@true} and {@true}", true);
                data.Add($"{@true} and {@true} and {@false}", false);
                data.Add($"{@true} or {@true} or {@false}", true);
                data.Add($"{@false} or {@false} or {@false}", false);

                // Parenthesized complex expressions
                data.Add($"({@true} or {@false}) and {@true}", true);
                data.Add($"({@true} or {@false}) and {@false}", false);
                data.Add($"{@true} and ({@true} or {@false})", true);
                data.Add($"{@false} and ({@true} or {@false})", false);
                data.Add($"({@true} and {@false}) or {@true}", true);
                data.Add($"({@false} and {@false}) or {@false}", false);

                // Complex negations
                data.Add($"!({@true} and {@true})", false);
                data.Add($"!({@true} and {@false})", true);
                data.Add($"!({@false} or {@false})", true);
                data.Add($"!({@true} or {@false})", false);
                data.Add($"!{@true} and !{@false}", false);
                data.Add($"!{@false} and !{@false}", true);
                data.Add($"!{@true} or !{@true}", false);
                data.Add($"!{@false} or !{@false}", true);

                // Triple combinations
                data.Add($"{@true} and {@true} and {@true}", true);
                data.Add($"{@true} and {@false} and {@true}", false);
                data.Add($"{@false} or {@true} or {@false}", true);
                data.Add($"{@false} or {@false} or {@true}", true);
                data.Add($"{@false} or {@true} and {@false}", false);
                data.Add($"({@true} and {@true}) or ({@false} and {@true})", true);
                data.Add($"({@true} or {@false}) and ({@true} or {@false})", true);
                data.Add($"({@false} or {@false}) and ({@true} or {@false})", false);

                // Comparisons with logical operators
                data.Add($"{@true} == {@true} and {@true} == {@true}", true);
                data.Add($"{@true} == {@true} and {@true} == {@false}", false);
                data.Add($"{@true} == {@false} or {@true} == {@true}", true);
                data.Add($"{@false} != {@true} and {@true} != {@false}", true);
                data.Add($"({@true} == {@true}) and ({@false} == {@false})", true);
            }
        }
    }

    [Theory]
    [MemberData(nameof(BooleanLiteralTestData))]
    public void BooleanLiteralTests(string expression, bool expectedResult)
    {
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);

        bool result = ConditionEvaluator.EvaluateCondition(
            expression,
            ParserOptions.AllowAll,
            expander,
            ExpanderOptions.ExpandProperties,
            Directory.GetCurrentDirectory(),
            MockElementLocation.Instance,
            FileSystems.Default,
            loggingContext: null);

        Assert.Equal(expectedResult, result);
    }
}
