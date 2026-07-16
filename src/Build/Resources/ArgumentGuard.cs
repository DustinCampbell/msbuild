// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build;

/// <summary>
///  Argument-validation helpers that were previously provided by the shared <c>ErrorUtilities</c>
///  class. These are re-homed in Microsoft.Build on top of the <see cref="Framework.ResourceString"/>
///  design (see <see cref="SR"/>) so that Microsoft.Build no longer needs the resource-name-based
///  <c>ErrorUtilities</c>/<c>ResourceUtilities</c> helpers.
/// </summary>
internal static class ArgumentGuard
{
    /// <summary>
    ///  Throws an <see cref="ArgumentOutOfRangeException"/> for the named parameter.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRange(string? parameterName)
        => throw new ArgumentOutOfRangeException(parameterName);

    /// <summary>
    ///  Throws an <see cref="ArgumentException"/> if the given collection is not null but of zero length.
    /// </summary>
    public static void VerifyThrowArgumentLengthIfNotNull<T>([MaybeNull] IReadOnlyCollection<T>? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter?.Count == 0)
        {
            ThrowArgumentLength(parameterName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="ArgumentException"/> if the string has zero length, unless it is null,
    ///  in which case no exception is thrown.
    /// </summary>
    public static void VerifyThrowArgumentLengthIfNotNull(string? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter?.Length == 0)
        {
            ThrowArgumentLength(parameterName);
        }
    }

    [DoesNotReturn]
    private static void ThrowArgumentLength(string? parameterName)
        => throw new ArgumentException(SR.Shared_ParameterCannotHaveZeroLength.FormatStripCode(parameterName));

    /// <summary>
    ///  Throws an <see cref="ArgumentNullException"/> if the given string parameter is null and an
    ///  <see cref="ArgumentException"/> if it contains invalid path characters.
    /// </summary>
    public static void VerifyThrowArgumentInvalidPath([NotNull] string parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(parameter, parameterName);

        if (FileUtilities.PathIsInvalid(parameter))
        {
            ArgumentException.Throw(SR.Shared_ParameterCannotHaveInvalidPathChars, parameterName, parameter);
        }
    }

    /// <summary>
    ///  Throws an <see cref="ArgumentNullException"/> if the given parameter is null, using the
    ///  specified resource for the message.
    /// </summary>
    public static void VerifyThrowArgumentNull([NotNull] object? parameter, string? parameterName, Framework.ResourceString resource)
    {
        if (parameter is null)
        {
            // Most ArgumentNullException overloads append their own rather clunky multi-line message,
            // so use the one overload that doesn't.
            throw new ArgumentNullException(resource.FormatStripCode(parameterName), (Exception?)null);
        }
    }

    /// <summary>
    ///  Verifies the parameters provided to a standard <see cref="ICollection{T}.CopyTo"/> call.
    /// </summary>
    public static void VerifyCollectionCopyToArguments<T>(
        [NotNull] ICollection<T>? collection,
        int index,
        int requiredCapacity,
        [CallerArgumentExpression(nameof(collection))] string? collectionParamName = null,
        [CallerArgumentExpression(nameof(index))] string? indexParamName = null)
    {
        ArgumentNullException.ThrowIfNull(collection, collectionParamName);
        ArgumentOutOfRangeException.ThrowIfNegative(index, indexParamName);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, collection.Count, indexParamName);

        int capacity = collection.Count - index;
        if (requiredCapacity > capacity)
        {
            throw new ArgumentException(
                SR.CollectionCopyToFailureProvidedArrayIsTooSmall.Text,
                collectionParamName);
        }
    }
}
