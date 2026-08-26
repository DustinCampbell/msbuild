// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NET
using System.Runtime.CompilerServices;
#endif
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public class PropertyFunctionParser_Tests
{
    public static TheoryData<string, string?[]> ArgumentCases { get; } = new()
    {
        { "", [] },
        { " ", [string.Empty] },
        { "a", ["a"] },
        { " a ", ["a"] },
        { "a,b", ["a", "b"] },
        { "a,", ["a"] },
        { ",", [string.Empty] },
        { ",,", [string.Empty, string.Empty] },
        { "a,,", ["a", string.Empty] },
        { ",a", [string.Empty, "a"] },
        { "a, ", ["a", string.Empty] },
        { "null", [null] },
        { "NULL", [null] },
        { "'null'", ["null"] },
        { "''", [string.Empty] },
        { "``", [string.Empty] },
        { "\"\"", [string.Empty] },
        { "'a,b'", ["a,b"] },
        { "`a(b)c`", ["a(b)c"] },
        { "\"a[b]c\"", ["a[b]c"] },
        { "$(Nested)", ["$(Nested)"] },
        { "$(Nested.Method(1, 2))", ["$(Nested.Method(1, 2))"] },
        { "$([System.String]::Concat('a', 'b'))", ["$([System.String]::Concat('a', 'b'))"] },
        { "before$(Nested.Method(1, 2))after,x", ["before$(Nested.Method(1, 2))after", "x"] },
        { "'a',`b`,\"c\"", ["a", "b", "c"] },
        { "out _", ["out _"] },
        { "'(((())))'", ["(((())))"] },
        { "'$([Type]::Method(a,b))'", ["$([Type]::Method(a,b))"] },
    };

    public static TheoryData<string, string?[], int> IndexerCases { get; } = new()
    {
        { "Value[]", [], 1 },
        { "Value[ ]", [string.Empty], 1 },
        { "Value[0]", ["0"], 1 },
        { "Value[0, 1]", ["0", "1"], 1 },
        { "Value[']']", ["]"], 1 },
        { "Value[\"]\"]", ["]"], 1 },
        { "Value[`]`]", ["]"], 1 },
        { "Value['[']", ["["], 1 },
        { "Value[$(Other[0])]", ["$(Other[0])"], 1 },
        { "Value[$([System.String]::Concat(']', ']'))]", ["$([System.String]::Concat(']', ']'))"], 1 },
        { "Value[$(Other.Method(\"]\"))]", ["$(Other.Method(\"]\"))"], 1 },
        { "Value[']'].Length", ["]"], 2 },
        { "Value[$(Other[0])][1]", ["$(Other[0])"], 2 },
    };

    [Fact]
    public void PlainPropertyIsNotAPropertyFunction()
    {
        bool parsed = PropertyFunctionParser.TryParse("Configuration", MockElementLocation.Instance, out PropertyFunctionExpression expression);

        parsed.ShouldBeFalse();
        expression.Invocations.Count.ShouldBe(0);
        expression.Text.HasValue.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Configuration")]
    [InlineData("_Configuration")]
    [InlineData("Δοκιμή")]
    [InlineData("Value-Name")]
    [InlineData("Value:Name")]
    [InlineData(" ")]
    public void RootWithoutAccessIsNotAPropertyFunction(string input)
    {
        bool parsed = PropertyFunctionParser.TryParse(input, MockElementLocation.Instance, out PropertyFunctionExpression expression);

        parsed.ShouldBeFalse();
        expression.Invocations.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Value.Length", ReceiverKind.MSBuildProperty, MemberKind.PropertyOrField, 1)]
    [InlineData("Value.get_Length()", ReceiverKind.MSBuildProperty, MemberKind.Method, 1)]
    [InlineData("Value[0]", ReceiverKind.MSBuildProperty, MemberKind.Indexer, 1)]
    [InlineData(" Value . Length ", ReceiverKind.MSBuildProperty, MemberKind.PropertyOrField, 1)]
    [InlineData("_Value.Method()", ReceiverKind.MSBuildProperty, MemberKind.Method, 1)]
    [InlineData("Value-Name.Method()", ReceiverKind.MSBuildProperty, MemberKind.Method, 1)]
    [InlineData("[System.DateTime]::Now", ReceiverKind.Static, MemberKind.PropertyOrField, 1)]
    [InlineData("[System.String]::Concat('a','b')", ReceiverKind.Static, MemberKind.Method, 1)]
    [InlineData("[System.String]::Empty.Length", ReceiverKind.Static, MemberKind.PropertyOrField, 2)]
    [InlineData("[System.String]::Concat('a','b').Length.ToString()", ReceiverKind.Static, MemberKind.Method, 3)]
    [InlineData("Value.Method().Property", ReceiverKind.MSBuildProperty, MemberKind.Method, 2)]
    [InlineData("Value.Property.Method()", ReceiverKind.MSBuildProperty, MemberKind.PropertyOrField, 2)]
    [InlineData("Value[0].Length", ReceiverKind.MSBuildProperty, MemberKind.Indexer, 2)]
    [InlineData("Value.Method()[0]", ReceiverKind.MSBuildProperty, MemberKind.Method, 2)]
    [InlineData("Value[0][1]", ReceiverKind.MSBuildProperty, MemberKind.Indexer, 2)]
    [InlineData("Value.Method() [0] . Length", ReceiverKind.MSBuildProperty, MemberKind.Method, 3)]
    [InlineData("Value.Property [0]", ReceiverKind.MSBuildProperty, MemberKind.PropertyOrField, 2)]
    [InlineData("Value.Method((1))", ReceiverKind.MSBuildProperty, MemberKind.Method, 1)]
    [InlineData("Value.Method(')')", ReceiverKind.MSBuildProperty, MemberKind.Method, 1)]
    [InlineData("Value.Method('(')", ReceiverKind.MSBuildProperty, MemberKind.Method, 1)]
    [InlineData("Value.Method('[')", ReceiverKind.MSBuildProperty, MemberKind.Method, 1)]
    [InlineData("Value.Method(']')", ReceiverKind.MSBuildProperty, MemberKind.Method, 1)]
    [InlineData("[Type]::Method($(Nested.Method(1, 2)))", ReceiverKind.Static, MemberKind.Method, 1)]
    internal void ParsesEveryGrammarForm(string input, ReceiverKind expectedReceiverKind, MemberKind expectedMemberKind, int expectedInvocationCount)
    {
        PropertyFunctionExpression expression = Parse(input);
        PropertyFunctionInvocation first = expression.Invocations[0];

        expression.Invocations.Count.ShouldBe(expectedInvocationCount);
        first.ReceiverKind.ShouldBe(expectedReceiverKind);
        first.MemberKind.ShouldBe(expectedMemberKind);
    }

    [Theory]
    [MemberData(nameof(ArgumentCases))]
    public void ParsesAndNormalizesEveryArgumentForm(string argumentText, string?[] expectedArguments)
    {
        string input = $"[Type]::Method({argumentText})";
        PropertyFunctionInvocation invocation = Parse(input).Invocations[0];

        AssertArguments(invocation.Arguments, expectedArguments);

        foreach (StringSegment argument in invocation.Arguments)
        {
            if (argument.Length > 0)
            {
                argument.Buffer.ShouldBeSameAs(input);
            }
        }
    }

    [Theory]
    [MemberData(nameof(IndexerCases))]
    public void ParsesIndexerDelimitersAtomically(string input, string?[] expectedArguments, int expectedInvocationCount)
    {
        PropertyFunctionExpression expression = Parse(input);
        PropertyFunctionInvocation indexer = expression.Invocations[0];

        expression.Invocations.Count.ShouldBe(expectedInvocationCount);
        indexer.MemberKind.ShouldBe(MemberKind.Indexer);
        AssertArguments(indexer.Arguments, expectedArguments);
    }

    [Fact]
    public void ParsesStaticMethodAndCompleteAccessChain()
    {
        const string input = """[System.Text.RegularExpressions.Regex]::Match($(Input), `EXPORT\s+(.+)`).Groups[1].Value""";

        PropertyFunctionExpression expression = Parse(input);

        expression.Invocations.Count.ShouldBe(4);
        expression.Invocations[2].Arguments[0].Value.ShouldBe("1");

        AssertInvocation(
            expression.Invocations[0],
            """[System.Text.RegularExpressions.Regex]::Match($(Input), `EXPORT\s+(.+)`)""",
            ReceiverKind.Static,
            "System.Text.RegularExpressions.Regex",
            MemberKind.Method,
            memberName: "Match",
            arguments: ["$(Input)", @"EXPORT\s+(.+)"]);

        AssertInvocation(
            expression.Invocations[1],
            ".Groups",
            ReceiverKind.Chained,
            receiver: null,
            MemberKind.PropertyOrField,
            memberName: "Groups",
            arguments: []);

        AssertInvocation(
            expression.Invocations[2],
            "[1]",
            ReceiverKind.Chained,
            receiver: null,
            MemberKind.Indexer,
            memberName: string.Empty,
            arguments: ["1"]);

        AssertInvocation(
            expression.Invocations[3],
            ".Value",
            ReceiverKind.Chained,
            receiver: null,
            MemberKind.PropertyOrField,
            memberName: "Value",
            arguments: []);

        foreach (PropertyFunctionInvocation invocation in expression.Invocations)
        {
            invocation.Text.Buffer.ShouldBeSameAs(input);
        }
    }

    [Fact]
    public void ParsesMSBuildPropertyMethodChain()
    {
        const string input = "  Configuration .Trim().ToUpperInvariant()[0]";

        PropertyFunctionExpression expression = Parse(input);
        expression.Invocations.Count.ShouldBe(3);

        AssertInvocation(
            expression.Invocations[0],
            "  Configuration .Trim()",
            ReceiverKind.MSBuildProperty,
            "Configuration",
            MemberKind.Method,
            memberName: "Trim",
            arguments: []);

        AssertInvocation(
            expression.Invocations[1],
            ".ToUpperInvariant()",
            ReceiverKind.Chained,
            receiver: null,
            MemberKind.Method,
            memberName: "ToUpperInvariant",
            arguments: []);

        AssertInvocation(
            expression.Invocations[2],
            "[0]",
            ReceiverKind.Chained,
            receiver: null,
            MemberKind.Indexer,
            memberName: string.Empty,
            arguments: ["0"]);
    }

    [Fact]
    public void ParsesRootIndexer()
    {
        const string input = "Values[$(Position)]";

        PropertyFunctionExpression expression = Parse(input);
        expression.Invocations.Count.ShouldBe(1);
        AssertInvocation(
            expression.Invocations[0],
            input,
            ReceiverKind.MSBuildProperty,
            "Values",
            MemberKind.Indexer,
            memberName: string.Empty,
            arguments: ["$(Position)"]);
    }

    [Fact]
    public void ParsesStaticPropertyFollowedByMethod()
    {
        const string input = "[System.Int32]::MaxValue.ToString()";

        PropertyFunctionExpression expression = Parse(input);
        expression.Invocations.Count.ShouldBe(2);

        AssertInvocation(
            expression.Invocations[0],
            "[System.Int32]::MaxValue",
            ReceiverKind.Static,
            "System.Int32",
            MemberKind.PropertyOrField,
            memberName: "MaxValue",
            arguments: []);

        AssertInvocation(
            expression.Invocations[1],
            ".ToString()",
            ReceiverKind.Chained,
            receiver: null,
            MemberKind.Method,
            memberName: "ToString",
            arguments: []);
    }

    [Fact]
    public void ParsesChainBeyondInitialBuilderCapacity()
    {
        const string input = "Value.A.B.C.D.E.F";

        PropertyFunctionExpression expression = Parse(input);

        expression.Invocations.Count.ShouldBe(6);
        expression.Invocations[0].MemberName.Value.ShouldBe("A");
        expression.Invocations[3].MemberName.Value.ShouldBe("D");
        expression.Invocations[5].MemberName.Value.ShouldBe("F");
    }

    [Fact]
    public void PreservesQuotedAndNestedArgumentsAtomically()
    {
        const string input = """[Type]::Method('a,b', `c(d)`, "$(x,y)", $(Nested.Call(1, 2)))""";

        PropertyFunctionExpression expression = Parse(input);

        expression.Invocations.Count.ShouldBe(1);
        AssertArguments(expression.Invocations[0].Arguments, ["a,b", "c(d)", "$(x,y)", "$(Nested.Call(1, 2))"]);
    }

    [Fact]
    public void NormalizesNullEmptyAndTrailingArgumentsCompatibly()
    {
        const string input = """[Type]::Method( null , '', ``, "value", $(Nested.Call(1, 2)), a,, )""";

        PropertyFunctionExpression expression = Parse(input);
        ArgumentList arguments = expression.Invocations[0].Arguments;

        expression.Invocations.Count.ShouldBe(1);
        arguments.Count.ShouldBe(8);
        arguments[0].HasValue.ShouldBeFalse();
        arguments[1].Value.ShouldBe(string.Empty);
        arguments[2].Value.ShouldBe(string.Empty);
        arguments[3].Value.ShouldBe("value");
        arguments[4].Value.ShouldBe("$(Nested.Call(1, 2))");
        arguments[5].Value.ShouldBe("a");
        arguments[6].Value.ShouldBe(string.Empty);
        arguments[7].Value.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("[Type]::Method()", 0)]
    [InlineData("[Type]::Method(a)", 1)]
    [InlineData("[Type]::Method(a,)", 1)]
    [InlineData("[Type]::Method(,)", 1)]
    [InlineData("[Type]::Method(a,,)", 2)]
    [InlineData("[Type]::Method(a, )", 2)]
    public void PreservesLegacyArgumentCounting(string input, int expectedCount)
    {
        PropertyFunctionExpression expression = Parse(input);

        expression.Invocations.Count.ShouldBe(1);
        expression.Invocations[0].Arguments.Count.ShouldBe(expectedCount);
    }

    [Fact]
    public void ParsesAStringSegmentWithANonZeroOffset()
    {
        const string expressionText = "[System.String]::Concat('a', 'b').Length";
        string buffer = $"prefix{expressionText}suffix";
        StringSegment input = new(buffer, "prefix".Length, expressionText.Length);

        PropertyFunctionExpression expression = Parse(input);

        expression.Text.Offset.ShouldBe("prefix".Length);
        expression.Text.Length.ShouldBe(expressionText.Length);
        expression.Text.Buffer.ShouldBeSameAs(buffer);

        expression.Invocations.Count.ShouldBe(2);
        expression.Invocations[0].Receiver.Value.ShouldBe("System.String");
        expression.Invocations[0].MemberName.Value.ShouldBe("Concat");
        expression.Invocations[0].Receiver.Buffer.ShouldBeSameAs(buffer);
        expression.Invocations[0].MemberName.Buffer.ShouldBeSameAs(buffer);
    }

    [Fact]
    public void ValidatesTheCompleteChainBeforeReturning()
    {
        const string input = "[System.String]::Concat('a', 'b').Length.";

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => PropertyFunctionParser.TryParse(input, MockElementLocation.Instance, out _));

        exception.ErrorCode.ShouldBe("MSB4184");
        exception.BaseMessage.ShouldContain(input);
    }

    [Theory]
    [InlineData("Value.Method(", "InvalidFunctionPropertyExpressionDetailMismatchedParenthesis")]
    [InlineData("Value.Method((1)", "InvalidFunctionPropertyExpressionDetailMismatchedParenthesis")]
    [InlineData("Value.Method($(Other.Method(1))", "InvalidFunctionPropertyExpressionDetailMismatchedParenthesis")]
    [InlineData("Value.Method('unterminated)", "InvalidFunctionPropertyExpressionDetailMismatchedQuote")]
    [InlineData("Value[$(Other.Method(1)]", "InvalidFunctionPropertyExpressionDetailMismatchedParenthesis")]
    [InlineData("Value[$(Other[0]]", "InvalidFunctionPropertyExpressionDetailMismatchedParenthesis")]
    [InlineData("Value['unterminated]", "InvalidFunctionPropertyExpressionDetailMismatchedQuote")]
    [InlineData("Value[\"unterminated]", "InvalidFunctionPropertyExpressionDetailMismatchedQuote")]
    [InlineData("Value[`unterminated]", "InvalidFunctionPropertyExpressionDetailMismatchedQuote")]
    [InlineData("Value[0", "InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets")]
    [InlineData("Value[$(Other)", "InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets")]
    [InlineData("Value['x'", "InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets")]
    public void ReportsDetailedPropertyFunctionSyntaxErrors(string input, string detailResourceName)
    {
        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => PropertyFunctionParser.TryParse(input, MockElementLocation.Instance, out _));

        exception.ErrorCode.ShouldBe("MSB4184");
        exception.BaseMessage.ShouldContain(input);
        exception.BaseMessage.ShouldContain(AssemblyResources.GetString(detailResourceName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData(".Length")]
    [InlineData("1Value.Length")]
    [InlineData("-Value.Length")]
    [InlineData("Value Name.Length")]
    [InlineData("Value:Name.Length")]
    [InlineData("Δοκιμή.Length")]
    [InlineData("$Value.Length")]
    [InlineData("$(Value).Length")]
    [InlineData("`Value.Length")]
    [InlineData("Value.")]
    [InlineData("Value..Length")]
    [InlineData("Value.[0]")]
    [InlineData("Value.Method()tail")]
    [InlineData("Value.Method()]")]
    [InlineData("Value.Method().")]
    [InlineData("Value.Method()..Length")]
    [InlineData("Value.Method()(")]
    [InlineData("Value.Method()::Other")]
    [InlineData("Value[0]tail")]
    [InlineData("Value[0]]")]
    [InlineData("Value[0].")]
    public void ReportsGeneralPropertyFunctionSyntaxErrors(string input)
    {
        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => PropertyFunctionParser.TryParse(input, MockElementLocation.Instance, out _));

        exception.ErrorCode.ShouldBe("MSB4184");
        exception.BaseMessage.ShouldContain(input);
    }

    [Theory]
    [InlineData("[System.String.Concat()")]
    [InlineData("[System.String]Concat()")]
    [InlineData("[System.String]:Method()")]
    [InlineData("[System.String]::")]
    [InlineData("[System.String].Length")]
    [InlineData("[System.String]")]
    [InlineData("[System.String")]
    [InlineData("[::Method()")]
    [InlineData("[System.String] ::Method()")]
    public void ReportsStaticFunctionSyntaxErrors(string input)
    {
        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => PropertyFunctionParser.TryParse(input, MockElementLocation.Instance, out _));

        exception.ErrorCode.ShouldBe("MSB4186");
        exception.BaseMessage.ShouldContain(input);
    }

    [Fact]
    public void ErrorForSlicedInputReportsOnlyTheSegment()
    {
        const string input = "Value.Method(";
        string buffer = $"prefix{input}suffix";
        StringSegment segment = new(buffer, "prefix".Length, input.Length);

        InvalidProjectFileException exception = Should.Throw<InvalidProjectFileException>(
            () => PropertyFunctionParser.TryParse(segment, new MockElementLocation("test.proj"), out _));

        exception.ErrorCode.ShouldBe("MSB4184");
        exception.BaseMessage.ShouldContain(input);
        exception.BaseMessage.ShouldNotContain(buffer);
        exception.ProjectFile.ShouldEndWith("test.proj");
    }

#if NET
    [Fact]
    public void SingleInvocationWithOneArgumentAllocatesNoMemory()
    {
        const string input = "Value.Substring(1)";

        for (int index = 0; index < 100; index++)
        {
            ParseAndConsume(input).ShouldBeGreaterThan(0);
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        int checksum = 0;

        for (int index = 0; index < 1_000; index++)
        {
            checksum += ParseAndConsume(input);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        checksum.ShouldBeGreaterThan(0);
        allocated.ShouldBe(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ParseAndConsume(string input)
    {
        if (!PropertyFunctionParser.TryParse(input, MockElementLocation.Instance, out PropertyFunctionExpression expression))
        {
            return -1;
        }

        int checksum = expression.Invocations.Count;

        foreach (PropertyFunctionInvocation invocation in expression)
        {
            checksum += invocation.Text.Offset;
            checksum += invocation.Text.Length;
            checksum += invocation.Receiver.Length;
            checksum += invocation.MemberName.Length;
            checksum += invocation.Arguments.Count;

            foreach (StringSegment argument in invocation.Arguments)
            {
                checksum += argument.Length;
                checksum += argument.HasValue ? 1 : 0;
            }
        }

        return checksum;
    }
#endif

    private static PropertyFunctionExpression Parse(StringSegment input)
    {
        bool parsed = PropertyFunctionParser.TryParse(input, MockElementLocation.Instance, out PropertyFunctionExpression expression);

        parsed.ShouldBeTrue();
        expression.Text.ShouldBe(input);
        return expression;
    }

    private static void AssertInvocation(
        PropertyFunctionInvocation invocation,
        string text,
        ReceiverKind receiverKind,
        string? receiver,
        MemberKind memberKind,
        string memberName,
        string?[] arguments)
    {
        invocation.Text.Value.ShouldBe(text);
        invocation.ReceiverKind.ShouldBe(receiverKind);
        invocation.Receiver.Value.ShouldBe(receiver);
        invocation.MemberKind.ShouldBe(memberKind);
        invocation.MemberName.Value.ShouldBe(memberName);
        AssertArguments(invocation.Arguments, arguments);
    }

    private static void AssertArguments(ArgumentList arguments, string?[] expected)
    {
        arguments.Count.ShouldBe(expected.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            arguments[i].Value.ShouldBe(expected[i]);
        }
    }
}
