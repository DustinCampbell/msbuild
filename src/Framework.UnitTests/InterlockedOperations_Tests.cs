// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class InterlockedOperations_Tests
{
    [Fact]
    public void Initialize_Value_StoresValueWhenNull()
    {
        string? target = null;

        string result = InterlockedOperations.Initialize(ref target, "hello");

        result.ShouldBe("hello");
        target.ShouldBe("hello");
    }

    [Fact]
    public void Initialize_Value_KeepsExistingValue()
    {
        string? target = "first";

        string result = InterlockedOperations.Initialize(ref target, "second");

        result.ShouldBe("first");
        target.ShouldBe("first");
    }

    [Fact]
    public void Initialize_Factory_InvokesFactoryWhenNull()
    {
        object? target = null;
        int factoryCalls = 0;

        object result = InterlockedOperations.Initialize(ref target, "state", state =>
        {
            factoryCalls++;
            return state.Length;
        });

        result.ShouldBe(5);
        target.ShouldBe(5);
        factoryCalls.ShouldBe(1);
    }

    [Fact]
    public void Initialize_Factory_DoesNotInvokeFactoryWhenInitialized()
    {
        object? target = "already";
        int factoryCalls = 0;

        object result = InterlockedOperations.Initialize(ref target, "state", _ =>
        {
            factoryCalls++;
            return 42;
        });

        result.ShouldBe("already");
        target.ShouldBe("already");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Initialize_Factory_PassesStateToFactory()
    {
        object? target = null;
        object state = new();
        object? observedState = null;

        _ = InterlockedOperations.Initialize(ref target, state, s =>
        {
            observedState = s;
            return "value";
        });

        observedState.ShouldBeSameAs(state);
    }

    [Fact]
    public void Initialize_Box_InvokesFactoryWhenNull()
    {
        StrongBox<string?>? target = null;
        int factoryCalls = 0;

        string? result = InterlockedOperations.Initialize(ref target, () =>
        {
            factoryCalls++;
            return "boxed";
        });

        result.ShouldBe("boxed");
        target.ShouldNotBeNull();
        target!.Value.ShouldBe("boxed");
        factoryCalls.ShouldBe(1);
    }

    [Fact]
    public void Initialize_Box_TreatsNullAsInitializedValue()
    {
        StrongBox<string?>? target = null;
        int factoryCalls = 0;

        string? first = InterlockedOperations.Initialize(ref target, () =>
        {
            factoryCalls++;
            return null;
        });

        // A null value is a valid initialized value: the box is stored, so a
        // second call must return that null without re-invoking the factory.
        string? second = InterlockedOperations.Initialize(ref target, () =>
        {
            factoryCalls++;
            return "should-not-run";
        });

        first.ShouldBeNull();
        second.ShouldBeNull();
        factoryCalls.ShouldBe(1);
    }

    [Fact]
    public void Initialize_Box_DoesNotInvokeFactoryWhenInitialized()
    {
        StrongBox<string?>? target = new("existing");
        int factoryCalls = 0;

        string? result = InterlockedOperations.Initialize(ref target, () =>
        {
            factoryCalls++;
            return "should-not-run";
        });

        result.ShouldBe("existing");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Initialize_Box_SupportsValueTypes()
    {
        StrongBox<int>? target = null;

        int result = InterlockedOperations.Initialize(ref target, () => 123);

        result.ShouldBe(123);
        target.ShouldNotBeNull();
        target!.Value.ShouldBe(123);
    }

    [Fact]
    public void Initialize_BoxWithState_InvokesFactoryWhenNullAndPassesState()
    {
        StrongBox<string?>? target = null;
        int factoryCalls = 0;

        string? result = InterlockedOperations.Initialize(ref target, "state", state =>
        {
            factoryCalls++;
            return state.ToUpperInvariant();
        });

        result.ShouldBe("STATE");
        target.ShouldNotBeNull();
        target!.Value.ShouldBe("STATE");
        factoryCalls.ShouldBe(1);
    }

    [Fact]
    public void Initialize_BoxWithState_DoesNotInvokeFactoryWhenInitialized()
    {
        StrongBox<string?>? target = new("existing");
        int factoryCalls = 0;

        string? result = InterlockedOperations.Initialize(ref target, "state", _ =>
        {
            factoryCalls++;
            return "should-not-run";
        });

        result.ShouldBe("existing");
        factoryCalls.ShouldBe(0);
    }

    [Fact]
    public void Initialize_BoxWithState_TreatsNullAsInitializedValue()
    {
        StrongBox<string?>? target = null;
        int factoryCalls = 0;

        string? first = InterlockedOperations.Initialize(ref target, "state", _ =>
        {
            factoryCalls++;
            return (string?)null;
        });

        string? second = InterlockedOperations.Initialize(ref target, "state", _ =>
        {
            factoryCalls++;
            return "should-not-run";
        });

        first.ShouldBeNull();
        second.ShouldBeNull();
        factoryCalls.ShouldBe(1);
    }

    [Fact]
    public void Initialize_Factory_OnlyOneValueWinsUnderContention()
    {
        object? target = null;
        var results = new ConcurrentBag<object>();
        const int ThreadCount = 16;
        using var barrier = new Barrier(ThreadCount);

        _ = Parallel.For(0, ThreadCount, _ =>
        {
            barrier.SignalAndWait();

            // Each call produces a distinct instance, but only one may be published.
            object result = InterlockedOperations.Initialize(ref target, 0, _ => new object());
            results.Add(result);
        });

        target.ShouldNotBeNull();
        foreach (object result in results)
        {
            result.ShouldBeSameAs(target);
        }
    }

    [Fact]
    public void Initialize_Box_OnlyOneValueWinsUnderContention()
    {
        StrongBox<object?>? target = null;
        var results = new ConcurrentBag<object?>();
        const int ThreadCount = 16;
        using var barrier = new Barrier(ThreadCount);

        _ = Parallel.For(0, ThreadCount, _ =>
        {
            barrier.SignalAndWait();

            object? result = InterlockedOperations.Initialize(ref target, () => new object());
            results.Add(result);
        });

        target.ShouldNotBeNull();
        foreach (object? result in results)
        {
            result.ShouldBeSameAs(target!.Value);
        }
    }
}
