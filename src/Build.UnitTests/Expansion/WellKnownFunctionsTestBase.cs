// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation.Expander;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public abstract class WellKnownFunctionsTestBase(Type receiverType, ITestOutputHelper output)
{
    protected Type ReceiverType => receiverType;

    protected ITestOutputHelper Output => output;

    protected ConstructorInvoker Constructor => new(receiverType);

    protected InstanceMemberInvoker InstanceMember(string memberName)
        => new(memberName);

    protected StaticMemberInvoker StaticMember(string memberName)
        => new(receiverType, memberName);

    protected readonly struct ConstructorInvoker(Type receiverType)
    {
        public object? Invoke()
            => Invoke(args: []);

        public object? Invoke(object?[] args)
        {
            ExpanderContext context = default;

            WellKnownFunctionResult result = WellKnownFunctions.TryInvokeConstructor(receiverType, args, in context);
            result.Status.ShouldBe(WellKnownFunctionStatus.Invoked);

            return result.Result;
        }

        public void NotHandled()
            => NotHandled([]);

        public void NotHandled(object?[] args)
        {
            ExpanderContext context = default;

            WellKnownFunctionResult result = WellKnownFunctions.TryInvokeConstructor(receiverType, args, in context);
            result.Status.ShouldBe(WellKnownFunctionStatus.NotHandled);
            result.Result.ShouldBeNull();
        }
    }

    protected readonly struct InstanceMemberInvoker(string memberName)
    {
        public object? Invoke(object objectInstance)
            => Invoke(objectInstance, args: []);

        public object? Invoke(object objectInstance, object?[] args)
        {
            ExpanderContext context = default;

            WellKnownFunctionResult result = WellKnownFunctions.TryInvokeInstance(objectInstance, memberName, args, in context);
            result.Status.ShouldBe(WellKnownFunctionStatus.Invoked);

            return result.Result;
        }

        public void NotHandled(object objectInstance)
            => NotHandled(objectInstance, args: []);

        public void NotHandled(object objectInstance, object?[] args)
        {
            ExpanderContext context = default;

            WellKnownFunctionResult result = WellKnownFunctions.TryInvokeInstance(objectInstance, memberName, args, in context);
            result.Status.ShouldBe(WellKnownFunctionStatus.NotHandled);
            result.Result.ShouldBeNull();
        }
    }

    protected readonly struct StaticMemberInvoker(Type receiverType, string memberName)
    {
        public object? Invoke()
            => Invoke(args: []);

        public object? Invoke(object?[] args)
        {
            ExpanderContext context = default;

            WellKnownFunctionResult result = WellKnownFunctions.TryInvokeStatic(receiverType, memberName, args, in context);
            result.Status.ShouldBe(WellKnownFunctionStatus.Invoked);

            return result.Result;
        }

        internal object? Invoke(ref readonly ExpanderContext context)
            => Invoke(args: [], in context);

        internal object? Invoke(object?[] args, ref readonly ExpanderContext context)
        {
            WellKnownFunctionResult result = WellKnownFunctions.TryInvokeStatic(receiverType, memberName, args, in context);
            result.Status.ShouldBe(WellKnownFunctionStatus.Invoked);

            return result.Result;
        }

        public void NotHandled()
            => NotHandled(args: []);

        public void NotHandled(object?[] args)
        {
            ExpanderContext context = default;

            WellKnownFunctionResult result = WellKnownFunctions.TryInvokeStatic(receiverType, memberName, args, in context);
            result.Status.ShouldBe(WellKnownFunctionStatus.NotHandled);
            result.Result.ShouldBeNull();
        }

        internal void NotHandled(ref readonly ExpanderContext context)
            => NotHandled(args: [], in context);

        internal void NotHandled(object?[] args, ref readonly ExpanderContext context)
        {
            WellKnownFunctionResult result = WellKnownFunctions.TryInvokeStatic(receiverType, memberName, args, in context);
            result.Status.ShouldBe(WellKnownFunctionStatus.NotHandled);
            result.Result.ShouldBeNull();
        }
    }
}
