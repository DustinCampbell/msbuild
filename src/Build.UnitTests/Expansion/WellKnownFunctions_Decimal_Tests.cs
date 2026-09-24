// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Decimal_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(decimal), output)
{
    [Fact]
    public void Decimal_Constructors()
    {
        Constructor.Invoke([42]).ShouldBe(42m);
        Constructor.Invoke([42u]).ShouldBe(42m);
        Constructor.Invoke([42L]).ShouldBe(42m);
        Constructor.Invoke([42UL]).ShouldBe(42m);
        Constructor.Invoke([42f]).ShouldBe(42m);
        Constructor.Invoke([42d]).ShouldBe(42m);
        Constructor.Invoke([decimal.GetBits(42m)]).ShouldBe(42m);
        Constructor.Invoke([42, 0, 0, false, (byte)1]).ShouldBe(4.2m);
        Constructor.Invoke([(byte)42]).ShouldBe(42m);
        Constructor.Invoke(["42"]).ShouldBe(42m);
        Constructor.Invoke([(short)42, (byte)0, (byte)0, "false", "1"]).ShouldBe(4.2m);
    }

    [Fact]
    public void Decimal_ClassicUnaryMethods()
    {
        StaticMember(nameof(decimal.Ceiling)).Invoke([1.2m]).ShouldBe(2m);
        StaticMember(nameof(decimal.Floor)).Invoke([1.8m]).ShouldBe(1m);
        StaticMember(nameof(decimal.GetBits)).Invoke([42m]).ShouldBe(decimal.GetBits(42m));
        StaticMember(nameof(decimal.Negate)).Invoke([42m]).ShouldBe(-42m);
        StaticMember(nameof(decimal.ToByte)).Invoke([42m]).ShouldBe((byte)42);
        StaticMember(nameof(decimal.ToDouble)).Invoke([42m]).ShouldBe(42d);
        StaticMember(nameof(decimal.ToInt16)).Invoke([42m]).ShouldBe((short)42);
        StaticMember(nameof(decimal.ToInt32)).Invoke([42m]).ShouldBe(42);
        StaticMember(nameof(decimal.ToInt64)).Invoke([42m]).ShouldBe(42L);
        StaticMember(nameof(decimal.ToOACurrency)).Invoke([4.2m]).ShouldBe(42000L);
        StaticMember(nameof(decimal.ToSByte)).Invoke([42m]).ShouldBe((sbyte)42);
        StaticMember(nameof(decimal.ToSingle)).Invoke([42m]).ShouldBe(42f);
        StaticMember(nameof(decimal.ToUInt16)).Invoke([42m]).ShouldBe((ushort)42);
        StaticMember(nameof(decimal.ToUInt32)).Invoke([42m]).ShouldBe(42u);
        StaticMember(nameof(decimal.ToUInt64)).Invoke([42m]).ShouldBe(42UL);
        StaticMember(nameof(decimal.Truncate)).Invoke([1.8m]).ShouldBe(1m);
        StaticMember(nameof(decimal.FromOACurrency)).Invoke([42000L]).ShouldBe(4.2m);
    }

    [Fact]
    public void Decimal_ClassicBinaryMethods()
    {
        StaticMember(nameof(decimal.Add)).Invoke([4m, 2m]).ShouldBe(6m);
        StaticMember(nameof(decimal.Compare)).Invoke([4m, 2m]).ShouldBe(1);
        StaticMember(nameof(decimal.Divide)).Invoke([4m, 2m]).ShouldBe(2m);
        StaticMember(nameof(decimal.Equals)).Invoke([4m, 4m]).ShouldBe(true);
        StaticMember(nameof(decimal.Multiply)).Invoke([4m, 2m]).ShouldBe(8m);
        StaticMember(nameof(decimal.Remainder)).Invoke([5m, 2m]).ShouldBe(1m);
        StaticMember(nameof(decimal.Subtract)).Invoke([4m, 2m]).ShouldBe(2m);
        StaticMember(nameof(decimal.Add)).Invoke(["4", (short)2]).ShouldBe(6m);
    }

    [Fact]
    public void Decimal_Round()
    {
        StaticMember(nameof(decimal.Round)).Invoke([1.6m]).ShouldBe(2m);
        StaticMember(nameof(decimal.Round)).Invoke([1.26m, 1]).ShouldBe(1.3m);
        StaticMember(nameof(decimal.Round)).Invoke([1.5m, MidpointRounding.ToEven]).ShouldBe(2m);
        StaticMember(nameof(decimal.Round)).Invoke([1.25m, 1, MidpointRounding.ToEven]).ShouldBe(1.2m);
    }

    [Fact]
    public void Decimal_Operators()
    {
        StaticMember("op_Addition").Invoke([4m, 2m]).ShouldBe(6m);
        StaticMember("op_Decrement").Invoke([4m]).ShouldBe(3m);
        StaticMember("op_Division").Invoke([4m, 2m]).ShouldBe(2m);
        StaticMember("op_Equality").Invoke([4m, 4m]).ShouldBe(true);
        StaticMember("op_GreaterThan").Invoke([4m, 2m]).ShouldBe(true);
        StaticMember("op_GreaterThanOrEqual").Invoke([4m, 4m]).ShouldBe(true);
        StaticMember("op_Implicit").Invoke([42]).ShouldBe(42m);
        StaticMember("op_Implicit").Invoke(['*']).ShouldBe(42m);
        StaticMember("op_Explicit").Invoke([42d]).ShouldBe(42m);
        StaticMember("op_Increment").Invoke([4m]).ShouldBe(5m);
        StaticMember("op_Inequality").Invoke([4m, 2m]).ShouldBe(true);
        StaticMember("op_LessThan").Invoke([2m, 4m]).ShouldBe(true);
        StaticMember("op_LessThanOrEqual").Invoke([4m, 4m]).ShouldBe(true);
        StaticMember("op_Modulus").Invoke([5m, 2m]).ShouldBe(1m);
        StaticMember("op_Multiply").Invoke([4m, 2m]).ShouldBe(8m);
        StaticMember("op_Subtraction").Invoke([4m, 2m]).ShouldBe(2m);
        StaticMember("op_UnaryNegation").Invoke([4m]).ShouldBe(-4m);
        StaticMember("op_UnaryPlus").Invoke([4m]).ShouldBe(4m);
    }

    [Fact]
    public void Decimal_InstanceMembers()
    {
        InstanceMember(nameof(decimal.CompareTo)).Invoke(42m, [41m]).ShouldBe(1);
        InstanceMember(nameof(decimal.Equals)).Invoke(42m, [42m]).ShouldBe(true);
        InstanceMember(nameof(decimal.GetHashCode)).Invoke(42m).ShouldBe(42m.GetHashCode());
        InstanceMember(nameof(decimal.GetTypeCode)).Invoke(42m).ShouldBe(TypeCode.Decimal);
        InstanceMember(nameof(decimal.ToString)).Invoke(42m, ["F1", CultureInfo.InvariantCulture]).ShouldBe("42.0");
#if NET
        InstanceMember(nameof(decimal.Scale)).Invoke(4.2m).ShouldBe((byte)1);
#endif
    }

#if NET
    [Fact]
    public void Decimal_ModernMethods()
    {
        StaticMember(nameof(decimal.Abs)).Invoke([-4m]).ShouldBe(4m);
        StaticMember(nameof(decimal.Clamp)).Invoke([4m, 1m, 3m]).ShouldBe(3m);
        StaticMember(nameof(decimal.CopySign)).Invoke([4m, -2m]).ShouldBe(-4m);
        StaticMember(nameof(decimal.IsCanonical)).Invoke([4m]).ShouldBe(true);
        StaticMember(nameof(decimal.IsEvenInteger)).Invoke([4m]).ShouldBe(true);
        StaticMember(nameof(decimal.IsInteger)).Invoke([4m]).ShouldBe(true);
        StaticMember(nameof(decimal.IsNegative)).Invoke([-4m]).ShouldBe(true);
        StaticMember(nameof(decimal.IsOddInteger)).Invoke([3m]).ShouldBe(true);
        StaticMember(nameof(decimal.IsPositive)).Invoke([4m]).ShouldBe(true);
        StaticMember(nameof(decimal.Max)).Invoke([4m, 2m]).ShouldBe(4m);
        StaticMember(nameof(decimal.MaxMagnitude)).Invoke([-4m, 2m]).ShouldBe(-4m);
        StaticMember(nameof(decimal.Min)).Invoke([4m, 2m]).ShouldBe(2m);
        StaticMember(nameof(decimal.MinMagnitude)).Invoke([-4m, 2m]).ShouldBe(2m);
        StaticMember(nameof(decimal.Sign)).Invoke([-4m]).ShouldBe(-1);
    }
#endif

    [Fact]
    public void Decimal_InvalidArguments_NotHandled()
    {
        Constructor.NotHandled([42m]);
        Constructor.NotHandled([new object()]);
        StaticMember(nameof(decimal.Add)).NotHandled([1m]);
        StaticMember("op_Explicit").NotHandled([1m]);
    }
}
