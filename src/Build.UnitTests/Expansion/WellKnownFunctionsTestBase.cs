// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;
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
        => new(receiverType, memberName);

    protected StaticMemberInvoker StaticMember(string memberName)
        => new(receiverType, memberName);

    protected readonly struct ConstructorInvoker(Type receiverType)
    {
        public object? Invoke()
            => Invoke(args: []);

        public object? Invoke(object?[] args)
        {
            bool invoked = WellKnownFunctions.TryExecuteWellKnownConstructorNoThrow(receiverType, out object? result, args!);
            invoked.ShouldBeTrue();

            return result;
        }

        public void NotHandled()
            => NotHandled([]);

        public void NotHandled(object?[] args)
        {
            bool invoked = WellKnownFunctions.TryExecuteWellKnownConstructorNoThrow(receiverType, out object? result, args!);
            invoked.ShouldBeFalse();
            result.ShouldBeNull();
        }
    }

    protected readonly struct InstanceMemberInvoker(Type receiverType, string memberName)
    {
        public object? Invoke(object objectInstance)
            => Invoke(objectInstance, args: []);

        public object? Invoke(object objectInstance, object?[] args)
        {
            bool invoked = WellKnownFunctions.TryExecuteWellKnownFunction(
                memberName, receiverType, FileSystems.Default, out object? result, objectInstance, args!);
            invoked.ShouldBeTrue();

            return result;
        }

        public void NotHandled(object objectInstance)
            => NotHandled(objectInstance, args: []);

        public void NotHandled(object objectInstance, object?[] args)
        {
            bool invoked = WellKnownFunctions.TryExecuteWellKnownFunction(
                memberName, receiverType, FileSystems.Default, out object? result, objectInstance, args!);
            invoked.ShouldBeFalse();
            result.ShouldBeNull();
        }
    }

    protected readonly struct StaticMemberInvoker(Type receiverType, string memberName)
    {
        public object? Invoke()
            => Invoke(args: []);

        public object? Invoke(object?[] args)
        {
            bool invoked = WellKnownFunctions.TryExecuteWellKnownFunction(
                memberName, receiverType, FileSystems.Default, out object? result, objectInstance: null!, args!);
            invoked.ShouldBeTrue();

            return result;
        }

        internal object? Invoke(IPropertyProvider<ProjectPropertyInstance> properties, LoggingContext loggingContext)
            => Invoke(args: [], properties, loggingContext);

        internal object? Invoke(object?[] args, IPropertyProvider<ProjectPropertyInstance> properties, LoggingContext loggingContext)
        {
            bool invoked = WellKnownFunctions.TryExecuteWellKnownFunctionWithPropertiesParam(
                memberName, receiverType, loggingContext, properties, out object? result, null!, args!);
            invoked.ShouldBeTrue();

            return result;
        }

        public void NotHandled()
            => NotHandled(args: []);

        public void NotHandled(object?[] args)
        {
            bool invoked = WellKnownFunctions.TryExecuteWellKnownFunction(
                memberName, receiverType, FileSystems.Default, out object? result, objectInstance: null!, args!);
            invoked.ShouldBeFalse();
            result.ShouldBeNull();
        }

        internal void NotHandled(IPropertyProvider<ProjectPropertyInstance> properties, LoggingContext loggingContext)
            => NotHandled(args: [], properties, loggingContext);

        internal void NotHandled(object?[] args, IPropertyProvider<ProjectPropertyInstance> properties, LoggingContext loggingContext)
        {
            bool invoked = WellKnownFunctions.TryExecuteWellKnownFunctionWithPropertiesParam(
                memberName, receiverType, loggingContext, properties, out object? result, null!, args!);
            invoked.ShouldBeFalse();
            result.ShouldBeNull();
        }
    }
}
