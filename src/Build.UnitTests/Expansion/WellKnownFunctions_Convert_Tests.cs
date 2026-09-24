// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Convert_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Convert), output)
{
    [Theory]
    [MemberData(nameof(ConversionData))]
    public void Convert_Value(string methodName, object? value, object expected)
        => StaticMember(methodName)
            .Invoke([value])
            .ShouldBe(expected);

    public static TheoryData<string, object?, object> ConversionData => new()
    {
        { nameof(Convert.ToBoolean), "true", true },
        { nameof(Convert.ToByte), "42", (byte)42 },
        { nameof(Convert.ToChar), "x", 'x' },
        { nameof(Convert.ToDateTime), "2026-09-23", new DateTime(2026, 9, 23) },
        { nameof(Convert.ToDecimal), "1.25", 1.25M },
        { nameof(Convert.ToDouble), "1.25", 1.25D },
        { nameof(Convert.ToInt16), "42", (short)42 },
        { nameof(Convert.ToInt32), "42", 42 },
        { nameof(Convert.ToInt64), "42", 42L },
        { nameof(Convert.ToSByte), "-42", (sbyte)-42 },
        { nameof(Convert.ToSingle), "1.25", 1.25F },
        { nameof(Convert.ToString), 42, "42" },
        { nameof(Convert.ToUInt16), "42", (ushort)42 },
        { nameof(Convert.ToUInt32), "42", 42U },
        { nameof(Convert.ToUInt64), "42", 42UL },
    };

    [Fact]
    public void Convert_BoxedNumericValue()
        => StaticMember(nameof(Convert.ToInt64))
            .Invoke([1.5D])
            .ShouldBe(2L);

    [Fact]
    public void Convert_ValueWithProvider()
        => StaticMember(nameof(Convert.ToDouble))
            .Invoke(["1.25", CultureInfo.InvariantCulture])
            .ShouldBe(1.25D);

    [Theory]
    [InlineData(16)]
    [InlineData("16")]
    public void Convert_ValueWithBase(object fromBase)
        => StaticMember(nameof(Convert.ToInt32))
            .Invoke(["FF", fromBase])
            .ShouldBe(255);

    [Fact]
    public void Convert_ChangeType_TypeCode()
        => StaticMember(nameof(Convert.ChangeType))
            .Invoke(["42", TypeCode.Int32])
            .ShouldBe(42);

    [Fact]
    public void Convert_ChangeType_QualifiedTypeCodeString()
        => StaticMember(nameof(Convert.ChangeType))
            .Invoke(["42", "TypeCode.Int32"])
            .ShouldBe(42);

    [Fact]
    public void Convert_GetTypeCode()
        => StaticMember(nameof(Convert.GetTypeCode))
            .Invoke([42L])
            .ShouldBe(TypeCode.Int64);

    [Fact]
    public void Convert_IsDBNull()
        => StaticMember(nameof(Convert.IsDBNull))
            .Invoke([DBNull.Value])
            .ShouldBe(true);

    [Fact]
    public void Convert_Base64()
    {
        byte[] bytes = [1, 2, 3];

        string encoded = StaticMember(nameof(Convert.ToBase64String))
            .Invoke([bytes])
            .ShouldBeOfType<string>();

        StaticMember(nameof(Convert.FromBase64String))
            .Invoke([encoded])
            .ShouldBe(bytes);
    }

#if NET
    [Fact]
    public void Convert_Hex()
    {
        byte[] bytes = [0xAB, 0xCD];

        StaticMember(nameof(Convert.ToHexString))
            .Invoke([bytes])
            .ShouldBe("ABCD");

        StaticMember(nameof(Convert.ToHexStringLower))
            .Invoke([bytes])
            .ShouldBe("abcd");

        StaticMember(nameof(Convert.FromHexString))
            .Invoke(["ABCD"])
            .ShouldBe(bytes);
    }
#endif

    [Fact]
    public void Convert_InvalidArguments_NotHandled()
        => StaticMember(nameof(Convert.ToInt32))
            .NotHandled([]);
}
