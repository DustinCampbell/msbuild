// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Expansion.FunctionInvocation;

/// <summary>
///  Provides standard-reflection primitive conversion rules for <see cref="TypeCode"/> values.
/// </summary>
internal static partial class TypeCodeExtensions
{
    private const PrimitiveTypes CharConversions =
        PrimitiveTypes.Char | PrimitiveTypes.UInt16 | PrimitiveTypes.UInt32 | PrimitiveTypes.Int32 |
        PrimitiveTypes.UInt64 | PrimitiveTypes.Int64 | PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes SByteConversions =
        PrimitiveTypes.SByte | PrimitiveTypes.Int16 | PrimitiveTypes.Int32 | PrimitiveTypes.Int64 |
        PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes ByteConversions =
        PrimitiveTypes.Byte | PrimitiveTypes.Char | PrimitiveTypes.UInt16 | PrimitiveTypes.Int16 |
        PrimitiveTypes.UInt32 | PrimitiveTypes.Int32 | PrimitiveTypes.UInt64 | PrimitiveTypes.Int64 |
        PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes Int16Conversions =
        PrimitiveTypes.Int16 | PrimitiveTypes.Int32 | PrimitiveTypes.Int64 | PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes UInt16Conversions =
        PrimitiveTypes.UInt16 | PrimitiveTypes.UInt32 | PrimitiveTypes.Int32 | PrimitiveTypes.UInt64 |
        PrimitiveTypes.Int64 | PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes Int32Conversions =
        PrimitiveTypes.Int32 | PrimitiveTypes.Int64 | PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes UInt32Conversions =
        PrimitiveTypes.UInt32 | PrimitiveTypes.UInt64 | PrimitiveTypes.Int64 | PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes Int64Conversions =
        PrimitiveTypes.Int64 | PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes UInt64Conversions =
        PrimitiveTypes.UInt64 | PrimitiveTypes.Single | PrimitiveTypes.Double;

    private const PrimitiveTypes SingleConversions =
        PrimitiveTypes.Single | PrimitiveTypes.Double;

    extension(TypeCode source)
    {
        /// <summary>
        ///  Determines whether the standard reflection binder can convert the source type code to the target type
        ///  code using its primitive conversion rules.
        /// </summary>
        /// <param name="target">The target type code.</param>
        /// <returns>
        ///  <see langword="true"/> when the conversion is supported; otherwise, <see langword="false"/>.
        /// </returns>
        public bool CanConvertTo(TypeCode target)
        {
            PrimitiveTypes targets = source switch
            {
                TypeCode.Boolean => PrimitiveTypes.Boolean,
                TypeCode.Char => CharConversions,
                TypeCode.SByte => SByteConversions,
                TypeCode.Byte => ByteConversions,
                TypeCode.Int16 => Int16Conversions,
                TypeCode.UInt16 => UInt16Conversions,
                TypeCode.Int32 => Int32Conversions,
                TypeCode.UInt32 => UInt32Conversions,
                TypeCode.Int64 => Int64Conversions,
                TypeCode.UInt64 => UInt64Conversions,
                TypeCode.Single => SingleConversions,
                TypeCode.Double => PrimitiveTypes.Double,
                TypeCode.Decimal => PrimitiveTypes.Decimal,
                TypeCode.DateTime => PrimitiveTypes.DateTime,
                TypeCode.String => PrimitiveTypes.String,
                _ => PrimitiveTypes.None,
            };

            PrimitiveTypes targetType = (PrimitiveTypes)(1 << (int)target);
            return (targets & targetType) != 0;
        }
    }
}
