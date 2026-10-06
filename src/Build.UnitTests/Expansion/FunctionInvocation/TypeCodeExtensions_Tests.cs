// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.Build.Expansion.FunctionInvocation;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion.FunctionInvocation;

[Trait("Category", "expansion")]
public class TypeCodeExtensions_Tests
{
    public static TheoryData<TypeCode, TypeCode, bool> PrimitiveConversionCases
    {
        get
        {
            (TypeCode Source, TypeCode[] Targets)[] conversions =
            [
                (TypeCode.Boolean, [TypeCode.Boolean]),
                (TypeCode.Char,
                    [TypeCode.Char, TypeCode.UInt16, TypeCode.UInt32, TypeCode.Int32, TypeCode.UInt64, TypeCode.Int64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.SByte,
                    [TypeCode.SByte, TypeCode.Int16, TypeCode.Int32, TypeCode.Int64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.Byte,
                    [TypeCode.Byte, TypeCode.Char, TypeCode.UInt16, TypeCode.Int16, TypeCode.UInt32, TypeCode.Int32,
                     TypeCode.UInt64, TypeCode.Int64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.Int16, [TypeCode.Int16, TypeCode.Int32, TypeCode.Int64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.UInt16,
                    [TypeCode.UInt16, TypeCode.UInt32, TypeCode.Int32, TypeCode.UInt64, TypeCode.Int64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.Int32, [TypeCode.Int32, TypeCode.Int64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.UInt32, [TypeCode.UInt32, TypeCode.UInt64, TypeCode.Int64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.Int64, [TypeCode.Int64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.UInt64, [TypeCode.UInt64, TypeCode.Single, TypeCode.Double]),
                (TypeCode.Single, [TypeCode.Single, TypeCode.Double]),
                (TypeCode.Double, [TypeCode.Double]),
                (TypeCode.Decimal, [TypeCode.Decimal]),
                (TypeCode.DateTime, [TypeCode.DateTime]),
                (TypeCode.String, [TypeCode.String]),
            ];

            var expectedConversions = conversions.ToDictionary(static c => c.Source, static c => c.Targets);
            var result = new TheoryData<TypeCode, TypeCode, bool>();
            foreach (TypeCode source in Enum.GetValues(typeof(TypeCode)))
            {
                expectedConversions.TryGetValue(source, out TypeCode[]? targets);

                foreach (TypeCode target in Enum.GetValues(typeof(TypeCode)))
                {
                    result.Add(source, target, targets?.Contains(target) == true);
                }
            }

            return result;
        }
    }

    [Theory]
    [MemberData(nameof(PrimitiveConversionCases))]
    public void PrimitiveConversionsMatchDefaultBinderTable(TypeCode source, TypeCode target, bool expected)
        => source.CanConvertTo(target).ShouldBe(expected);
}
