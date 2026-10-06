// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Expansion.FunctionInvocation;

internal static partial class TypeCodeExtensions
{
    /// <summary>
    ///  Represents sets of target type codes in the standard reflection binder's primitive conversion table.
    /// </summary>
    [Flags]
    private enum PrimitiveTypes
    {
        None = 0,
        Boolean = 1 << TypeCode.Boolean,
        Char = 1 << TypeCode.Char,
        SByte = 1 << TypeCode.SByte,
        Byte = 1 << TypeCode.Byte,
        Int16 = 1 << TypeCode.Int16,
        UInt16 = 1 << TypeCode.UInt16,
        Int32 = 1 << TypeCode.Int32,
        UInt32 = 1 << TypeCode.UInt32,
        Int64 = 1 << TypeCode.Int64,
        UInt64 = 1 << TypeCode.UInt64,
        Single = 1 << TypeCode.Single,
        Double = 1 << TypeCode.Double,
        Decimal = 1 << TypeCode.Decimal,
        DateTime = 1 << TypeCode.DateTime,
        String = 1 << TypeCode.String,
    }
}
