// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Expansion.FunctionInvocation;

/// <summary>
///  Describes the runtime type and shape of one overload parameter.
/// </summary>
internal readonly struct Parameter
{
    /// <summary>
    ///  Initializes a parameter descriptor.
    /// </summary>
    /// <param name="type">The runtime parameter type.</param>
    /// <param name="flags">The parameter shape.</param>
    public Parameter(Type type, ParameterFlags flags = ParameterFlags.None)
    {
        Assumed.NotNull(type);
        Type = type;
        TypeCode = Type.GetTypeCode(type);
        Flags = flags;
    }

    /// <summary>
    ///  Gets the runtime parameter type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    ///  Gets the cached type code used for primitive conversion.
    /// </summary>
    public TypeCode TypeCode { get; }

    /// <summary>
    ///  Gets the parameter shape.
    /// </summary>
    public ParameterFlags Flags { get; }

    /// <summary>
    ///  Gets a value indicating whether this parameter collects a variable number of trailing arguments.
    /// </summary>
    public bool IsParamArray => (Flags & ParameterFlags.ParamArray) != 0;

    /// <summary>
    ///  Determines whether standard reflection binding accepts an argument for this parameter.
    /// </summary>
    /// <param name="argument">The argument value.</param>
    /// <returns>
    ///  <see langword="true"/> for an exact type, an assignable reference type, <see langword="null"/>, or a
    ///  permitted primitive widening conversion; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool IsApplicable(object? argument)
    {
        if (argument is null)
        {
            // System.DefaultBinder treats null as typeless and applicable to every parameter type.
            return true;
        }

        Type argumentType = argument.GetType();
        if (Type == argumentType || Type == typeof(object))
        {
            return true;
        }

        return Type.IsPrimitive
            ? Type.GetTypeCode(argumentType).CanConvertTo(TypeCode)
            : Type.IsAssignableFrom(argumentType);
    }

    /// <summary>
    ///  Converts an argument already accepted by <see cref="IsApplicable"/> to the exact parameter type.
    /// </summary>
    /// <param name="argument">The applicable argument value.</param>
    /// <returns>
    ///  The value to pass to the selected member.
    /// </returns>
    /// <remarks>
    ///  Reflection treats <see langword="null"/> as applicable to value-type parameters and supplies their default
    ///  value during invocation. Enum values widen through their underlying primitive type.
    /// </remarks>
    internal object? Bind(object? argument)
    {
        if (argument is null)
        {
            // Reflection passes the zero-initialized value when null binds to a non-nullable value type.
            return Type.CreateDefault();
        }

        Type argumentType = argument.GetType();
        if (Type == argumentType || !Type.IsPrimitive)
        {
            return argument;
        }

        if (argumentType.IsEnum)
        {
            argument = Convert.ChangeType(argument, Enum.GetUnderlyingType(argumentType), CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(argument, Type, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///  Creates a descriptor for the element type of this parameter array.
    /// </summary>
    /// <returns>
    ///  A parameter descriptor without the <see cref="ParameterFlags.ParamArray"/> flag.
    /// </returns>
    internal Parameter GetParamArrayElement()
    {
        Assumed.True(IsParamArray);

        Type? elementType = Type.GetElementType();
        Assumed.NotNull(elementType);
        return new Parameter(elementType);
    }

    /// <summary>
    ///  Attempts the complete property-function conversion pipeline for one argument.
    /// </summary>
    /// <param name="argument">The argument value.</param>
    /// <param name="result">The converted value when successful.</param>
    /// <returns>
    ///  <see langword="true"/> when the argument can be converted; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Standard applicability is reused first. Target-specific conversions preserve existing well-known-function
    ///  behavior, including numeric string syntax, enum names, and <see cref="Version"/> parsing. Remaining types
    ///  use invariant-culture coercion. Expected conversion failures are reported as <see langword="false"/> so the
    ///  binder can try the next overload.
    /// </remarks>
    internal bool TryConvert(object? argument, out object? result)
    {
        if (IsApplicable(argument))
        {
            result = Bind(argument);
            return true;
        }

        if (TryConvertDirect(argument, out result))
        {
            return true;
        }

        if (Type.IsEnum)
        {
            result = null;
            return false;
        }

        try
        {
            result = Coerce(argument);
            return true;
        }
        catch (InvalidCastException)
        {
        }
        catch (FormatException)
        {
        }
        catch (OverflowException)
        {
        }

        result = null;
        return false;
    }

    /// <summary>
    ///  Applies the general property-function coercion used after target-specific conversion fails.
    /// </summary>
    /// <param name="argument">The argument value.</param>
    /// <returns>
    ///  The coerced value.
    /// </returns>
    /// <remarks>
    ///  Character arrays retain their historical conversion from an argument's string representation. All other
    ///  values use <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> with invariant culture.
    /// </remarks>
    private object? Coerce(object? argument)
    {
        if (argument is null)
        {
            return Type.CreateDefault();
        }

        if (Type == typeof(char[]))
        {
            return argument.ToString()!.ToCharArray();
        }

        return Convert.ChangeType(argument, Type, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///  Applies conversions that existing well-known functions perform before general reflection coercion.
    /// </summary>
    /// <param name="argument">The argument value.</param>
    /// <param name="result">The converted value when successful.</param>
    /// <returns>
    ///  <see langword="true"/> when a target-specific conversion succeeds; otherwise, <see langword="false"/>.
    /// </returns>
    private bool TryConvertDirect(object? argument, out object? result)
    {
        if (Type == typeof(char) && ArgumentParser.TryConvertToChar(argument, out char character))
        {
            result = character;
            return true;
        }

        if (Type == typeof(int) && ArgumentParser.TryConvertToInt(argument, out int integer))
        {
            result = integer;
            return true;
        }

        if (Type == typeof(long) && ArgumentParser.TryConvertToLong(argument, out long longInteger))
        {
            result = longInteger;
            return true;
        }

        if (Type == typeof(double) && ArgumentParser.TryConvertToDouble(argument, out double floatingPoint))
        {
            result = floatingPoint;
            return true;
        }

        if (Type == typeof(Version) && ArgumentParser.TryConvertToVersion(argument, out Version? version))
        {
            result = version;
            return true;
        }

        if (Type.IsEnum && TryConvertToEnum(argument, out object? enumValue))
        {
            result = enumValue;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    ///  Converts a nonnumeric string to this parameter's enum type.
    /// </summary>
    /// <param name="argument">The argument value.</param>
    /// <param name="result">The parsed enum value when successful.</param>
    /// <returns>
    ///  <see langword="true"/> when the argument names one or more enum values; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Numeric strings are deliberately rejected. Qualified enum members and <c>|</c>-separated flags are
    ///  normalized before parsing.
    /// </remarks>
    private bool TryConvertToEnum(object? argument, out object? result)
    {
        if (argument is string text &&
            !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            !ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            if (text.IndexOf('.') >= 0)
            {
                text = ArgumentParser.NormalizeEnumArgument(Type, text);
            }

            try
            {
                result = Enum.Parse(Type, text);
                return true;
            }
            catch (ArgumentException)
            {
            }
        }

        result = null;
        return false;
    }
}
