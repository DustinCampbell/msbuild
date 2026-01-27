// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation;
using Shouldly;

namespace Microsoft.Build.UnitTests;

internal static class NodeVerifiers
{
    public static void Verify<T>(this GenericExpressionNode node, Action<T> verifier)
        where T : GenericExpressionNode
        => verifier(node.ShouldBeAssignableTo<T>().ShouldNotBeNull());

    public static void Verify<TLeft, TRight>(this OperatorExpressionNode node, Action<TLeft, TRight> verifier)
        where TLeft : GenericExpressionNode
        where TRight : GenericExpressionNode
        => verifier(
            node.LeftChild.ShouldBeAssignableTo<TLeft>().ShouldNotBeNull(),
            node.RightChild.ShouldBeAssignableTo<TRight>().ShouldNotBeNull());

    public static void Verify<TLeft, TRight>(this OperatorExpressionNode node, Action<TLeft> leftVerifier, Action<TRight> rightVerifier)
        where TLeft : GenericExpressionNode
        where TRight : GenericExpressionNode
    {
        leftVerifier(node.LeftChild.ShouldBeAssignableTo<TLeft>().ShouldNotBeNull());
        rightVerifier(node.RightChild.ShouldBeAssignableTo<TRight>().ShouldNotBeNull());
    }

    public static void Verify<TChild>(this NotExpressionNode node, Action<TChild> verifier)
        where TChild : GenericExpressionNode
        => verifier(node.LeftChild.ShouldBeAssignableTo<TChild>().ShouldNotBeNull());

    public static void Verify(this StringExpressionNode node, string value)
        => node.Value.ShouldBe(value);

    public static void Verify(this NumericExpressionNode node, string value)
        => node.Value.ShouldBe(value);

    public static void Verify(this BooleanLiteralNode node, bool value)
        => node.Value.ShouldBe(value);

    public static void Verify(this FunctionCallExpressionNode node, string name)
    {
        node.Name.ShouldBe(name);
        node.Arguments.Length.ShouldBe(0);
    }

    public static void Verify<TArg>(
        this FunctionCallExpressionNode node,
        string name,
        Action<TArg> argVerifier)
        where TArg : GenericExpressionNode
    {
        node.Name.ShouldBe(name);
        node.Arguments.Length.ShouldBe(1);
        argVerifier(node.Arguments[0].ShouldBeAssignableTo<TArg>().ShouldNotBeNull());
    }

    public static void Verify<TArg1, TArg2>(
        this FunctionCallExpressionNode node,
        string name,
        Action<TArg1> arg1Verifier,
        Action<TArg2> arg2Verifier)
        where TArg1 : GenericExpressionNode
        where TArg2 : GenericExpressionNode
    {
        node.Name.ShouldBe(name);
        node.Arguments.Length.ShouldBe(2);
        arg1Verifier(node.Arguments[0].ShouldBeAssignableTo<TArg1>().ShouldNotBeNull());
        arg2Verifier(node.Arguments[1].ShouldBeAssignableTo<TArg2>().ShouldNotBeNull());
    }

    public static void Verify<TArg1, TArg2, TArg3>(
        this FunctionCallExpressionNode node,
        string name,
        Action<TArg1> arg1Verifier,
        Action<TArg2> arg2Verifier,
        Action<TArg3> arg3Verifier)
        where TArg1 : GenericExpressionNode
        where TArg2 : GenericExpressionNode
        where TArg3 : GenericExpressionNode
    {
        node.Name.ShouldBe(name);
        node.Arguments.Length.ShouldBe(3);
        arg1Verifier(node.Arguments[0].ShouldBeAssignableTo<TArg1>().ShouldNotBeNull());
        arg2Verifier(node.Arguments[1].ShouldBeAssignableTo<TArg2>().ShouldNotBeNull());
        arg3Verifier(node.Arguments[2].ShouldBeAssignableTo<TArg3>().ShouldNotBeNull());
    }

    public static void Verify<TArg1, TArg2, TArg3, TArg4>(
        this FunctionCallExpressionNode node,
        string name,
        Action<TArg1> arg1Verifier,
        Action<TArg2> arg2Verifier,
        Action<TArg3> arg3Verifier,
        Action<TArg4> arg4Verifier)
        where TArg1 : GenericExpressionNode
        where TArg2 : GenericExpressionNode
        where TArg3 : GenericExpressionNode
        where TArg4 : GenericExpressionNode
    {
        node.Name.ShouldBe(name);
        node.Arguments.Length.ShouldBe(4);
        arg1Verifier(node.Arguments[0].ShouldBeAssignableTo<TArg1>().ShouldNotBeNull());
        arg2Verifier(node.Arguments[1].ShouldBeAssignableTo<TArg2>().ShouldNotBeNull());
        arg3Verifier(node.Arguments[2].ShouldBeAssignableTo<TArg3>().ShouldNotBeNull());
        arg4Verifier(node.Arguments[3].ShouldBeAssignableTo<TArg4>().ShouldNotBeNull());
    }
}
