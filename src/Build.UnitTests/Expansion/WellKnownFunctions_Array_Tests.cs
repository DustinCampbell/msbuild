// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Array_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(string[]), output)
{
    [Fact]
    public void Array_Properties()
    {
        int[] array = [1, 2];

        InstanceMember(nameof(Array.IsFixedSize)).Invoke(array).ShouldBe(true);
        InstanceMember(nameof(Array.IsReadOnly)).Invoke(array).ShouldBe(false);
        InstanceMember(nameof(Array.IsSynchronized)).Invoke(array).ShouldBe(false);
        InstanceMember(nameof(Array.Length)).Invoke(array).ShouldBe(2);
        InstanceMember(nameof(Array.LongLength)).Invoke(array).ShouldBe(2L);
        InstanceMember(nameof(Array.Rank)).Invoke(array).ShouldBe(1);
        InstanceMember(nameof(Array.SyncRoot)).Invoke(array).ShouldBeSameAs(array);
    }

    [Fact]
    public void Array_ObjectMembers()
    {
        int[] array = [1, 2];

        InstanceMember(nameof(Array.Equals)).Invoke(array, [array]).ShouldBe(true);
        InstanceMember(nameof(Array.GetHashCode)).Invoke(array).ShouldBe(array.GetHashCode());
        InstanceMember(nameof(Array.ToString)).Invoke(array).ShouldBe(array.ToString());

        Array clone = InstanceMember(nameof(Array.Clone)).Invoke(array).ShouldBeOfType<int[]>();
        clone.ShouldNotBeSameAs(array);
        clone.ShouldBe(array);
    }

    [Fact]
    public void Array_CopyTo()
    {
        string[] source = ["a", "b"];
        string[] intDestination = new string[3];
        string[] longDestination = new string[3];

        InstanceMember(nameof(Array.CopyTo)).Invoke(source, [intDestination, "1"]).ShouldBeNull();
        InstanceMember(nameof(Array.CopyTo)).Invoke(source, [longDestination, 1L]).ShouldBeNull();

        intDestination[0].ShouldBeNull();
        intDestination[1].ShouldBe("a");
        intDestination[2].ShouldBe("b");
        longDestination[0].ShouldBeNull();
        longDestination[1].ShouldBe("a");
        longDestination[2].ShouldBe("b");
    }

    [Fact]
    public void Array_GetBoundsAndLengths()
    {
        Array array = Array.CreateInstance(typeof(string), [2], [3]);

        InstanceMember(nameof(Array.GetLength)).Invoke(array, ["0"]).ShouldBe(2);
        InstanceMember(nameof(Array.GetLongLength)).Invoke(array, ["0"]).ShouldBe(2L);
        InstanceMember(nameof(Array.GetLowerBound)).Invoke(array, ["0"]).ShouldBe(3);
        InstanceMember(nameof(Array.GetUpperBound)).Invoke(array, ["0"]).ShouldBe(4);
    }

    [Fact]
    public void Array_GetEnumerator()
    {
        IEnumerator enumerator = InstanceMember(nameof(Array.GetEnumerator))
            .Invoke(new[] { "first", "second" })
            .ShouldBeAssignableTo<IEnumerator>()!;

        enumerator.MoveNext().ShouldBe(true);
        enumerator.Current.ShouldBe("first");
    }

    [Fact]
    public void Array_GetValue_Int32Overloads()
    {
        Array oneDimension = new[] { "first", "second" };
        Array twoDimensions = new[,] { { "00", "01" }, { "10", "11" } };
        Array threeDimensions = new[,,] { { { "000", "001" }, { "010", "011" } } };

        InstanceMember(nameof(Array.GetValue)).Invoke(oneDimension, [1]).ShouldBe("second");
        InstanceMember(nameof(Array.GetValue)).Invoke(oneDimension, ["1"]).ShouldBe("second");
        InstanceMember(nameof(Array.GetValue)).Invoke(twoDimensions, [1, 0]).ShouldBe("10");
        InstanceMember(nameof(Array.GetValue)).Invoke(threeDimensions, [0, 1, 1]).ShouldBe("011");
        InstanceMember(nameof(Array.GetValue)).Invoke(twoDimensions, [new[] { 1, 1 }]).ShouldBe("11");
    }

    [Fact]
    public void Array_GetValue_Int64Overloads()
    {
        Array oneDimension = new[] { "first", "second" };
        Array twoDimensions = new[,] { { "00", "01" }, { "10", "11" } };
        Array threeDimensions = new[,,] { { { "000", "001" }, { "010", "011" } } };

        InstanceMember(nameof(Array.GetValue)).Invoke(oneDimension, [1L]).ShouldBe("second");
        InstanceMember(nameof(Array.GetValue)).Invoke(twoDimensions, [1L, 0L]).ShouldBe("10");
        InstanceMember(nameof(Array.GetValue)).Invoke(twoDimensions, [1L, 0]).ShouldBe("10");
        InstanceMember(nameof(Array.GetValue)).Invoke(threeDimensions, [0L, 1L, 1L]).ShouldBe("011");
        InstanceMember(nameof(Array.GetValue)).Invoke(twoDimensions, [new long[] { 1, 1 }]).ShouldBe("11");
    }

    [Fact]
    public void Array_Initialize()
        => InstanceMember(nameof(Array.Initialize)).Invoke(new int[1]).ShouldBeNull();

    [Fact]
    public void Array_SetValue_Int32Overloads()
    {
        Array oneDimension = new string[2];
        Array twoDimensions = new string[2, 2];
        Array threeDimensions = new string[1, 2, 2];

        InstanceMember(nameof(Array.SetValue)).Invoke(oneDimension, ["one", 1]).ShouldBeNull();
        InstanceMember(nameof(Array.SetValue)).Invoke(twoDimensions, ["two", 1, 0]).ShouldBeNull();
        InstanceMember(nameof(Array.SetValue)).Invoke(threeDimensions, ["three", 0, 1, 1]).ShouldBeNull();
        InstanceMember(nameof(Array.SetValue)).Invoke(twoDimensions, ["indices", new[] { 1, 1 }]).ShouldBeNull();

        oneDimension.GetValue(1).ShouldBe("one");
        twoDimensions.GetValue(1, 0).ShouldBe("two");
        threeDimensions.GetValue(0, 1, 1).ShouldBe("three");
        twoDimensions.GetValue(1, 1).ShouldBe("indices");
    }

    [Fact]
    public void Array_SetValue_Int64Overloads()
    {
        Array oneDimension = new string[2];
        Array twoDimensions = new string[2, 2];
        Array threeDimensions = new string[1, 2, 2];

        InstanceMember(nameof(Array.SetValue)).Invoke(oneDimension, ["one", 1L]).ShouldBeNull();
        InstanceMember(nameof(Array.SetValue)).Invoke(twoDimensions, ["two", 1L, 0L]).ShouldBeNull();
        InstanceMember(nameof(Array.SetValue)).Invoke(twoDimensions, ["mixed", 0L, 1]).ShouldBeNull();
        InstanceMember(nameof(Array.SetValue)).Invoke(threeDimensions, ["three", 0L, 1L, 1L]).ShouldBeNull();
        InstanceMember(nameof(Array.SetValue)).Invoke(twoDimensions, ["indices", new long[] { 1, 1 }]).ShouldBeNull();

        oneDimension.GetValue(1).ShouldBe("one");
        twoDimensions.GetValue(1, 0).ShouldBe("two");
        twoDimensions.GetValue(0, 1).ShouldBe("mixed");
        threeDimensions.GetValue(0, 1, 1).ShouldBe("three");
        twoDimensions.GetValue(1, 1).ShouldBe("indices");
    }

    [Fact]
    public void Array_InvalidArguments_NotHandled()
    {
        int[] array = [1, 2];

        InstanceMember(nameof(Array.GetLength)).NotHandled(array, ["invalid"]);
        InstanceMember(nameof(Array.GetValue)).NotHandled(array);
        InstanceMember(nameof(Array.GetValue)).NotHandled(array, ["invalid"]);
        InstanceMember(nameof(Array.SetValue)).NotHandled(array, [1]);
    }
}
