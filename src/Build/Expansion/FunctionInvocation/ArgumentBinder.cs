// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Evaluation.Expander;

namespace Microsoft.Build.Expansion.FunctionInvocation;

/// <summary>
///  Selects the best applicable overload and converts its arguments using property-function binding rules.
/// </summary>
internal static partial class ArgumentBinder
{
    /// <summary>
    ///  Attempts to bind the supplied arguments to the best applicable candidate overload.
    /// </summary>
    /// <param name="arguments">The lazily materialized argument values.</param>
    /// <param name="overloads">The candidate overloads.</param>
    /// <param name="convertedArguments">
    ///  The caller-owned storage that receives the converted arguments when binding succeeds.
    /// </param>
    /// <param name="overloadIndex">
    ///  The selected overload's index within <paramref name="overloads"/>, or <c>-1</c> when binding does not succeed.
    /// </param>
    /// <returns>
    ///  The result of overload binding.
    /// </returns>
    /// <remarks>
    ///  The <paramref name="convertedArguments"/> contents are unspecified unless this method returns
    ///  <see cref="BindingResult.Success"/>. Standard reflection binding runs first. If it finds no applicable
    ///  overload, the candidates are tried in order using property-function conversion and invariant-culture
    ///  coercion. An ambiguous standard binding does not proceed to coercion.
    ///  <para>
    ///   When a parameter array is expanded, each converted trailing argument remains in its corresponding
    ///   destination slot. The eventual invoker is responsible for consuming or packing those elements.
    ///  </para>
    /// </remarks>
    public static BindingResult TryBind(
        ref Arguments arguments, ImmutableArray<Overload> overloads, Span<object?> convertedArguments, out int overloadIndex)
    {
        Assumed.False(overloads.IsDefault, "The overload array must be initialized.");
        Assumed.GreaterThanOrEqual(
            convertedArguments.Length,
            arguments.Length,
            "The converted argument storage must have room for every argument.");

        int bestIndex = -1;
        bool bestUsesParamArray = false;
        bool ambiguous = false;

        // Standard binding considers every applicable candidate. Candidate order cannot resolve ambiguity.
        for (int i = 0; i < overloads.Length; i++)
        {
            Overload candidate = overloads[i];
            if (!IsApplicable(ref arguments, candidate, out bool usesParamArray))
            {
                continue;
            }

            if (bestIndex < 0)
            {
                bestIndex = i;
                bestUsesParamArray = usesParamArray;
                continue;
            }

            Specificity specificity = FindMostSpecific(
                ref arguments,
                overloads[bestIndex],
                bestUsesParamArray,
                candidate,
                usesParamArray);

            if (specificity == Specificity.Neither)
            {
                ambiguous = true;
            }
            else if (specificity == Specificity.Second)
            {
                bestIndex = i;
                bestUsesParamArray = usesParamArray;
                ambiguous = false;
            }
        }

        if (bestIndex < 0)
        {
            // Property functions historically apply broader, ordered conversions only after standard binding fails.
            return TryBindWithConversions(ref arguments, overloads, convertedArguments, out overloadIndex);
        }

        if (ambiguous)
        {
            overloadIndex = -1;
            return BindingResult.Ambiguous;
        }

        Overload overload = overloads[bestIndex];
        BindStandardArguments(ref arguments, overload, bestUsesParamArray, convertedArguments);
        overloadIndex = bestIndex;
        return BindingResult.Success;
    }

    /// <summary>
    ///  Converts arguments for an overload already selected by standard binding.
    /// </summary>
    /// <param name="arguments">The source arguments.</param>
    /// <param name="overload">The selected overload.</param>
    /// <param name="usesParamArray">
    ///  <see langword="true"/> when trailing arguments bind to the element type of an expanded parameter array.
    /// </param>
    /// <param name="convertedArguments">The destination for the converted argument values.</param>
    private static void BindStandardArguments(
        ref Arguments arguments,
        Overload overload,
        bool usesParamArray,
        Span<object?> convertedArguments)
    {
        ImmutableArray<Parameter> parameters = overload.Parameters;
        for (int i = 0; i < arguments.Length; i++)
        {
            _ = arguments.TryGetArg(i, out object? argument);
            convertedArguments[i] = GetParameter(parameters, i, usesParamArray).Bind(argument);
        }
    }

    /// <summary>
    ///  Compares two applicable overloads using the specificity rules from the standard reflection binder.
    /// </summary>
    /// <param name="arguments">The source arguments whose runtime types participate in the comparison.</param>
    /// <param name="first">The first applicable overload.</param>
    /// <param name="firstUsesParamArray">
    ///  <see langword="true"/> when the first overload requires parameter-array expansion.
    /// </param>
    /// <param name="second">The second applicable overload.</param>
    /// <param name="secondUsesParamArray">
    ///  <see langword="true"/> when the second overload requires parameter-array expansion.
    /// </param>
    /// <returns>
    ///  Which overload is more specific, or <see cref="Specificity.Neither"/> when the comparison is ambiguous.
    /// </returns>
    /// <remarks>
    ///  A non-expanded overload is more specific than an expanded parameter-array overload. Otherwise each
    ///  argument position is compared independently. If every effective parameter type is equal, the overload
    ///  with more declared parameters is more specific, matching the reflection binder's parameter-array tie
    ///  breaker.
    /// </remarks>
    private static Specificity FindMostSpecific(
        ref Arguments arguments,
        Overload first,
        bool firstUsesParamArray,
        Overload second,
        bool secondUsesParamArray)
    {
        if (firstUsesParamArray != secondUsesParamArray)
        {
            return firstUsesParamArray ? Specificity.Second : Specificity.First;
        }

        ImmutableArray<Parameter> firstParameters = first.Parameters;
        ImmutableArray<Parameter> secondParameters = second.Parameters;
        bool firstIsMoreSpecific = false;
        bool secondIsMoreSpecific = false;

        for (int i = 0; i < arguments.Length; i++)
        {
            Parameter firstParameter = GetParameter(firstParameters, i, firstUsesParamArray);
            Parameter secondParameter = GetParameter(secondParameters, i, secondUsesParamArray);
            Type firstType = firstParameter.Type;
            Type secondType = secondParameter.Type;
            if (firstType == secondType)
            {
                continue;
            }

            _ = arguments.TryGetArg(i, out object? argument);
            Specificity specificity = FindMostSpecificType(firstParameter, secondParameter, argument?.GetType());
            if (specificity == Specificity.Neither)
            {
                return Specificity.Neither;
            }

            if (specificity == Specificity.First)
            {
                firstIsMoreSpecific = true;
            }
            else
            {
                secondIsMoreSpecific = true;
            }
        }

        return firstIsMoreSpecific == secondIsMoreSpecific
            ? firstIsMoreSpecific
                ? Specificity.Neither
                : firstParameters.Length > secondParameters.Length
                    ? Specificity.First
                    : secondParameters.Length > firstParameters.Length
                        ? Specificity.Second
                        : Specificity.Neither
            : firstIsMoreSpecific
                ? Specificity.First
                : Specificity.Second;
    }

    /// <summary>
    ///  Compares the effective parameter types for one argument position.
    /// </summary>
    /// <param name="firstParameter">The first parameter.</param>
    /// <param name="secondParameter">The second parameter.</param>
    /// <param name="argumentType">
    ///  The argument's runtime type, or <see langword="null"/> when the argument value is <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  Which parameter type is more specific, or <see cref="Specificity.Neither"/> when neither type wins.
    /// </returns>
    /// <remarks>
    ///  An exact runtime-type match wins first. Otherwise primitive types are compared by widening-conversion
    ///  direction and reference types by assignability. If only the first type converts or assigns to the second,
    ///  the first type is narrower and therefore more specific; the inverse selects the second type.
    /// </remarks>
    private static Specificity FindMostSpecificType(Parameter firstParameter, Parameter secondParameter, Type? argumentType)
    {
        Type firstType = firstParameter.Type;
        Type secondType = secondParameter.Type;

        if (firstType == argumentType)
        {
            return Specificity.First;
        }

        if (secondType == argumentType)
        {
            return Specificity.Second;
        }

        bool secondConvertsToFirst;
        bool firstConvertsToSecond;

        if (firstType.IsPrimitive && secondType.IsPrimitive)
        {
            secondConvertsToFirst = secondParameter.TypeCode.CanConvertTo(firstParameter.TypeCode);
            firstConvertsToSecond = firstParameter.TypeCode.CanConvertTo(secondParameter.TypeCode);
        }
        else
        {
            secondConvertsToFirst = firstType.IsAssignableFrom(secondType);
            firstConvertsToSecond = secondType.IsAssignableFrom(firstType);
        }

        if (secondConvertsToFirst == firstConvertsToSecond)
        {
            return Specificity.Neither;
        }

        return secondConvertsToFirst ? Specificity.Second : Specificity.First;
    }

    /// <summary>
    ///  Gets the effective parameter for one argument position.
    /// </summary>
    /// <param name="parameters">The declared parameters.</param>
    /// <param name="argumentIndex">The source argument index.</param>
    /// <param name="usesParamArray">
    ///  <see langword="true"/> when trailing arguments bind to the parameter-array element type.
    /// </param>
    /// <returns>
    ///  The declared parameter, or a descriptor for the parameter-array element type.
    /// </returns>
    private static Parameter GetParameter(ImmutableArray<Parameter> parameters, int argumentIndex, bool usesParamArray)
        => usesParamArray && argumentIndex >= parameters.Length - 1
            ? parameters[^1].GetParamArrayElement()
            : parameters[argumentIndex];

    /// <summary>
    ///  Determines whether standard reflection binding can apply an overload to the source arguments.
    /// </summary>
    /// <param name="arguments">The source arguments.</param>
    /// <param name="overload">The candidate overload.</param>
    /// <param name="usesParamArray">
    ///  Receives <see langword="true"/> when the candidate is applicable only by expanding its final parameter
    ///  array.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> when every argument is applicable; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  An explicit array argument binds to the parameter-array type without expansion. A <see langword="null"/>
    ///  final argument is treated as one expanded element, matching <c>System.DefaultBinder</c>. Unlike
    ///  ordinary parameters, a <see langword="null"/> argument is not applicable to a primitive parameter-array
    ///  element.
    /// </remarks>
    private static bool IsApplicable(ref Arguments arguments, Overload overload, out bool usesParamArray)
    {
        ImmutableArray<Parameter> parameters = overload.Parameters;
        if (parameters.IsEmpty)
        {
            usesParamArray = false;
            return arguments.Length == 0;
        }

        Parameter lastParameter = parameters[^1];
        if (!lastParameter.IsParamArray)
        {
            usesParamArray = false;
            if (parameters.Length != arguments.Length)
            {
                return false;
            }
        }
        else
        {
            int fixedParameterCount = parameters.Length - 1;
            if (arguments.Length < fixedParameterCount)
            {
                usesParamArray = false;
                return false;
            }

            usesParamArray = arguments.Length != parameters.Length;
            if (!usesParamArray)
            {
                _ = arguments.TryGetArg(fixedParameterCount, out object? lastArgument);
                usesParamArray = !lastParameter.Type.IsInstanceOfType(lastArgument);
            }
        }

        for (int i = 0; i < arguments.Length; i++)
        {
            _ = arguments.TryGetArg(i, out object? argument);
            Parameter parameter = GetParameter(parameters, i, usesParamArray);
            if ((usesParamArray && i >= parameters.Length - 1 && argument is null && parameter.Type.IsPrimitive) ||
                !parameter.IsApplicable(argument))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Tries the property-function conversion phase in overload order.
    /// </summary>
    /// <param name="arguments">The source arguments.</param>
    /// <param name="overloads">The candidate overloads in conversion order.</param>
    /// <param name="convertedArguments">The destination for converted values.</param>
    /// <param name="overloadIndex">The selected overload index, or <c>-1</c> when no candidate converts.</param>
    /// <returns>
    ///  <see cref="BindingResult.Success"/> for the first fully converted candidate; otherwise,
    ///  <see cref="BindingResult.NoApplicableOverload"/>.
    /// </returns>
    /// <remarks>
    ///  Conversion order is observable behavior. A failed conversion rejects only the current candidate and allows
    ///  the next candidate to be tried.
    /// </remarks>
    private static BindingResult TryBindWithConversions(
        ref Arguments arguments,
        ImmutableArray<Overload> overloads,
        Span<object?> convertedArguments,
        out int overloadIndex)
    {
        for (int i = 0; i < overloads.Length; i++)
        {
            if (TryConvertArguments(ref arguments, overloads[i].Parameters, convertedArguments))
            {
                overloadIndex = i;
                return BindingResult.Success;
            }
        }

        overloadIndex = -1;
        return BindingResult.NoApplicableOverload;
    }

    /// <summary>
    ///  Attempts to convert every source argument to one candidate's declared parameter types.
    /// </summary>
    /// <param name="arguments">The source arguments.</param>
    /// <param name="parameters">The candidate parameters.</param>
    /// <param name="convertedArguments">The destination for converted values.</param>
    /// <returns>
    ///  <see langword="true"/> when every argument converts; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  The reflection fallback applies conversion only to candidates with matching declared arity. It does not
    ///  synthesize or expand a parameter array during this phase.
    /// </remarks>
    private static bool TryConvertArguments(
        ref Arguments arguments,
        ImmutableArray<Parameter> parameters,
        Span<object?> convertedArguments)
    {
        if (parameters.Length != arguments.Length)
        {
            return false;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            _ = arguments.TryGetArg(i, out object? argument);
            if (!parameters[i].TryConvert(argument, out convertedArguments[i]))
            {
                return false;
            }
        }

        return true;
    }
}
