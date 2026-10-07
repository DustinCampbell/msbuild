// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Build.Framework;

internal static class FrameworkErrorUtilities
{
    /// <summary>
    ///  Verifies in debug builds that the specified object's type provides a meaningful
    ///  <see cref="object.ToString()"/> implementation.
    /// </summary>
    /// <param name="parameter">The object to verify.</param>
    public static void ThrowIfTypeDoesNotImplementToString(object parameter)
    {
#if DEBUG
        if (string.Equals(parameter.GetType().ToString(), parameter.ToString(), StringComparison.Ordinal))
        {
            InternalError.Throw($"This type does not implement ToString() properly {parameter.GetType().FullName!}");
        }
#endif
    }

    /// <summary>
    ///  Throws an internal error when a lock on <paramref name="locker"/> is not held.
    /// </summary>
    /// <param name="locker">The object whose monitor should already be held.</param>
    public static void VerifyThrowInternalLockHeld(object locker)
    {
        Assumed.True(Monitor.IsEntered(locker), "Lock should already have been taken");
    }

    /// <summary>
    /// Helper to throw an InternalErrorException when the specified parameter is not a rooted path.
    /// This should be used ONLY if this would indicate a bug in MSBuild rather than
    /// anything caused by user action.
    /// </summary>
    /// <param name="value">Parameter that should be a rooted path.</param>
    public static void VerifyThrowInternalRooted(string value)
    {
        if (!Path.IsPathRooted(value))
        {
            InternalError.Throw($"{value} unexpectedly not a rooted path");
        }
    }

    /// <summary>
    ///  A utility that verifies the parameters provided to a standard <see cref="ICollection{T}.CopyTo"/> call.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    /// <param name="collection">The destination collection to copy into.</param>
    /// <param name="index">The zero-based index in <paramref name="collection"/> at which copying begins.</param>
    /// <param name="requiredCapacity">The number of elements that need to be copied.</param>
    /// <param name="collectionParamName">The name of the <paramref name="collection"/> parameter.</param>
    /// <param name="indexParamName">The name of the <paramref name="index"/> parameter.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="collection"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> falls outside of the bounds of <paramref name="collection"/>.</exception>
    /// <exception cref="ArgumentException">
    ///  If there is insufficient capacity to copy the collection contents into <paramref name="collection"/>
    ///  when starting at <paramref name="index"/>.
    /// </exception>
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
                FrameworkStrings.CollectionCopyToFailureProvidedArrayIsTooSmall.Resolve().Text,
                collectionParamName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidOperationException"/> containing a resolved resource string.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="message">The exception message.</param>
    [DoesNotReturn]
    public static void ThrowInvalidOperation<TResource>(TResource message)
        where TResource : IResourceString
        => throw new InvalidOperationException(message.Resolve().Text);

    [DoesNotReturn]
    public static void ThrowInvalidOperationMessage(string message)
        => throw new InvalidOperationException(message);

    [DoesNotReturn]
    public static void ThrowInvalidOperation<T1>(ResourceString resource, T1 arg0)
        => ThrowInvalidOperation(resource.WithArguments(arg0));

    [DoesNotReturn]
    public static void ThrowInvalidOperation<T1, T2>(ResourceString resource, T1 arg0, T2 arg1)
        => ThrowInvalidOperation(resource.WithArguments(arg0, arg1));

    [DoesNotReturn]
    public static void ThrowInvalidOperation<T1, T2, T3>(ResourceString resource, T1 arg0, T2 arg1, T3 arg2)
        => ThrowInvalidOperation(resource.WithArguments(arg0, arg1, arg2));

    [DoesNotReturn]
    public static void ThrowInvalidOperation<T1, T2, T3, T4>(ResourceString resource, T1 arg0, T2 arg1, T3 arg2, T4 arg3)
        => ThrowInvalidOperation(resource.WithArguments(arg0, arg1, arg2, arg3));

    /// <summary>
    ///  Throws an <see cref="InvalidOperationException"/> if <paramref name="condition"/> is
    ///  <see langword="false"/>.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">The exception message.</param>
    public static void VerifyThrowInvalidOperation<TResource>(
        [DoesNotReturnIf(false)] bool condition,
        TResource message)
        where TResource : IResourceString
    {
        if (!condition)
        {
            ThrowInvalidOperation(message);
        }
    }

    public static void VerifyThrowInvalidOperation<T1>(
        [DoesNotReturnIf(false)] bool condition,
        ResourceString resource,
        T1 arg0)
    {
        if (!condition)
        {
            ThrowInvalidOperation(resource.WithArguments(arg0));
        }
    }

    public static void VerifyThrowInvalidOperation<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        ResourceString resource,
        T1 arg0,
        T2 arg1)
    {
        if (!condition)
        {
            ThrowInvalidOperation(resource.WithArguments(arg0, arg1));
        }
    }

    public static void VerifyThrowInvalidOperation<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2)
    {
        if (!condition)
        {
            ThrowInvalidOperation(resource.WithArguments(arg0, arg1, arg2));
        }
    }

    public static void VerifyThrowInvalidOperation<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
    {
        if (!condition)
        {
            ThrowInvalidOperation(resource.WithArguments(arg0, arg1, arg2, arg3));
        }
    }

    /// <summary>
    ///  Throws an <see cref="ArgumentException"/> containing a resolved resource string.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The optional inner exception.</param>
    [DoesNotReturn]
    public static void ThrowArgument<TResource>(TResource message, Exception? innerException = null)
        where TResource : IResourceString
        => throw new ArgumentException(message.Resolve().Text, innerException);

    [DoesNotReturn]
    public static void ThrowArgument<T1>(ResourceString resource, T1 arg0)
        => ThrowArgument(resource.WithArguments(arg0));

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2>(ResourceString resource, T1 arg0, T2 arg1)
        => ThrowArgument(resource.WithArguments(arg0, arg1));

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2, T3>(ResourceString resource, T1 arg0, T2 arg1, T3 arg2)
        => ThrowArgument(resource.WithArguments(arg0, arg1, arg2));

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2, T3, T4>(ResourceString resource, T1 arg0, T2 arg1, T3 arg2, T4 arg3)
        => ThrowArgument(resource.WithArguments(arg0, arg1, arg2, arg3));

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2, T3, T4, T5>(ResourceString resource, T1 arg0, T2 arg1, T3 arg2, T4 arg3, T5 arg4)
        => ThrowArgument(resource.WithArguments(arg0, arg1, arg2, arg3, arg4));

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2, T3, T4, T5, T6>(
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3,
        T5 arg4,
        T6 arg5)
        => ThrowArgument(resource.WithArguments(arg0, arg1, arg2, arg3, arg4, arg5));

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2, T3, T4, T5, T6, T7>(
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3,
        T5 arg4,
        T6 arg5,
        T7 arg6)
        => ThrowArgument(resource.WithArguments(arg0, arg1, arg2, arg3, arg4, arg5, arg6));

    [DoesNotReturn]
    public static void ThrowArgument<T1>(Exception? innerException, ResourceString resource, T1 arg0)
        => ThrowArgument(resource.WithArguments(arg0), innerException);

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2>(Exception? innerException, ResourceString resource, T1 arg0, T2 arg1)
        => ThrowArgument(resource.WithArguments(arg0, arg1), innerException);

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2, T3>(Exception? innerException, ResourceString resource, T1 arg0, T2 arg1, T3 arg2)
        => ThrowArgument(resource.WithArguments(arg0, arg1, arg2), innerException);

    [DoesNotReturn]
    public static void ThrowArgument<T1, T2, T3, T4>(
        Exception? innerException,
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
        => ThrowArgument(resource.WithArguments(arg0, arg1, arg2, arg3), innerException);

    /// <summary>
    ///  Throws an <see cref="ArgumentException"/> if <paramref name="condition"/> is
    ///  <see langword="false"/>.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The optional inner exception.</param>
    public static void VerifyThrowArgument<TResource>(
        [DoesNotReturnIf(false)] bool condition,
        TResource message,
        Exception? innerException = null)
        where TResource : IResourceString
    {
        if (!condition)
        {
            ThrowArgument(message, innerException);
        }
    }

    public static void VerifyThrowArgument<T1>(
        [DoesNotReturnIf(false)] bool condition,
        ResourceString resource,
        T1 arg0)
        => VerifyThrowArgument(condition, resource.WithArguments(arg0));

    public static void VerifyThrowArgument<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        ResourceString resource,
        T1 arg0,
        T2 arg1)
        => VerifyThrowArgument(condition, resource.WithArguments(arg0, arg1));

    public static void VerifyThrowArgument<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2)
        => VerifyThrowArgument(condition, resource.WithArguments(arg0, arg1, arg2));

    public static void VerifyThrowArgument<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
        => VerifyThrowArgument(condition, resource.WithArguments(arg0, arg1, arg2, arg3));

    public static void VerifyThrowArgument<T1>(
        [DoesNotReturnIf(false)] bool condition,
        Exception? innerException,
        ResourceString resource,
        T1 arg0)
        => VerifyThrowArgument(condition, resource.WithArguments(arg0), innerException);

    public static void VerifyThrowArgument<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        Exception? innerException,
        ResourceString resource,
        T1 arg0,
        T2 arg1)
        => VerifyThrowArgument(condition, resource.WithArguments(arg0, arg1), innerException);

    public static void VerifyThrowArgument<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        Exception? innerException,
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2)
        => VerifyThrowArgument(condition, resource.WithArguments(arg0, arg1, arg2), innerException);

    public static void VerifyThrowArgument<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        Exception? innerException,
        ResourceString resource,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
        => VerifyThrowArgument(condition, resource.WithArguments(arg0, arg1, arg2, arg3), innerException);

    /// <summary>
    ///  Throws an <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRange(string? parameterName)
        => throw new ArgumentOutOfRangeException(parameterName);

    /// <summary>
    ///  Throws an <see cref="ArgumentNullException"/> if <paramref name="parameter"/> is
    ///  <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="parameter">The parameter to verify.</param>
    /// <param name="message">The preformatted exception message.</param>
    public static void VerifyThrowArgumentNull<TResource>(
        [NotNull] object? parameter,
        TResource message)
        where TResource : IResourceString
    {
        if (parameter is null)
        {
            throw new ArgumentNullException(message.Resolve().Text, (Exception?)null);
        }
    }

    public static void VerifyThrowArgumentNull(
        [NotNull] object? parameter,
        string? parameterName,
        ResourceString resource)
    {
        if (parameter is null)
        {
            throw new ArgumentNullException(resource.WithArguments(parameterName).Resolve().Text, (Exception?)null);
        }
    }

    /// <summary>
    ///  Throws an <see cref="ArgumentException"/> if the given collection is not null but of zero length.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    /// <param name="parameter">The collection to check.</param>
    /// <param name="parameterName">The name of the <paramref name="parameter"/>.</param>
    public static void VerifyThrowArgumentLengthIfNotNull<T>(IReadOnlyCollection<T>? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter?.Count == 0)
        {
            ThrowArgumentLength(parameterName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="ArgumentException"/> if the string has zero length, unless it is
    ///  null, in which case no exception is thrown.
    /// </summary>
    /// <param name="parameter">The string to check.</param>
    /// <param name="parameterName">The name of the <paramref name="parameter"/>.</param>
    public static void VerifyThrowArgumentLengthIfNotNull(string? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter?.Length == 0)
        {
            ThrowArgumentLength(parameterName);
        }
    }

    [DoesNotReturn]
    private static void ThrowArgumentLength(string? parameterName)
        => throw new ArgumentException(FrameworkStrings.Argument_EmptyString.Resolve().Text, parameterName);

    /// <summary>
    ///  Throws an <see cref="ArgumentNullException"/> if the given string parameter is null,
    ///  and an <see cref="ArgumentException"/> if it contains invalid path or file characters.
    /// </summary>
    /// <param name="parameter">The path to check.</param>
    /// <param name="parameterName">The name of the <paramref name="parameter"/>.</param>
    public static void VerifyThrowArgumentInvalidPath([NotNull] string? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(parameter, parameterName);

        if (FileUtilities.PathIsInvalid(parameter))
        {
            throw new ArgumentException(
                FrameworkStrings.ParameterCannotHaveInvalidPathChars.WithArguments(parameterName, parameter).Resolve().Text);
        }
    }
}
