// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal;

/// <summary>
///  Provides helpers for reporting invalid project errors at specific locations.
/// </summary>
/// <remarks>
///  Use these methods for errors caused by invalid project content. Use <see cref="Assumed"/> for internal
///  invariants. The generic verification overloads defer format-argument allocation and boxing until the condition
///  fails.
/// </remarks>
internal static class ProjectErrorUtilities
{
    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> for the specified location.
    /// </summary>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="args">The arguments used to format the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(IElementLocation location, string resourceName, params object?[] args)
        => ThrowInvalidProjectCore(
            errorSubCategoryResourceName: null,
            location,
            innerException: null,
            resourceName,
            args);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception for the specified location.
    /// </summary>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="args">The arguments used to format the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        params object?[] args)
        => ThrowInvalidProjectCore(
            errorSubCategoryResourceName: null,
            location,
            innerException,
            resourceName,
            args);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with the specified error subcategory.
    /// </summary>
    /// <param name="errorSubCategoryResourceName">
    ///  The name of the error-subcategory resource, or <see langword="null"/> for no subcategory.
    /// </param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="args">The arguments used to format the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(
        string? errorSubCategoryResourceName,
        IElementLocation location,
        string resourceName,
        params object?[] args)
        => ThrowInvalidProjectCore(
            errorSubCategoryResourceName,
            location,
            innerException: null,
            resourceName,
            args);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> when the specified condition is
    ///  <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName)
        => VerifyThrowInvalidProject(condition, errorSubCategoryResourceName: null, location, resourceName);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with one format argument when the specified condition is
    ///  <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject<T1>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0)
        => VerifyThrowInvalidProject(condition, errorSubCategoryResourceName: null, location, resourceName, arg0);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with two format arguments when the specified condition is
    ///  <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <param name="arg1">The second format argument.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1)
        => VerifyThrowInvalidProject(condition, errorSubCategoryResourceName: null, location, resourceName, arg0, arg1);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with three format arguments when the specified condition
    ///  is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <param name="arg1">The second format argument.</param>
    /// <param name="arg2">The third format argument.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2)
        => VerifyThrowInvalidProject(
            condition,
            errorSubCategoryResourceName: null,
            location,
            resourceName,
            arg0,
            arg1,
            arg2);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with four format arguments when the specified condition is
    ///  <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth format argument.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <param name="arg1">The second format argument.</param>
    /// <param name="arg2">The third format argument.</param>
    /// <param name="arg3">The fourth format argument.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
        => VerifyThrowInvalidProject(
            condition,
            errorSubCategoryResourceName: null,
            location,
            resourceName,
            arg0,
            arg1,
            arg2,
            arg3);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with the specified error subcategory when the condition is
    ///  <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="errorSubCategoryResourceName">
    ///  The name of the error-subcategory resource, or <see langword="null"/> for no subcategory.
    /// </param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        string? errorSubCategoryResourceName,
        IElementLocation location,
        string resourceName)
    {
        if (!condition)
        {
            ThrowInvalidProject(errorSubCategoryResourceName, location, resourceName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with the specified error subcategory and one format
    ///  argument when the condition is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="errorSubCategoryResourceName">
    ///  The name of the error-subcategory resource, or <see langword="null"/> for no subcategory.
    /// </param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject<T1>(
        [DoesNotReturnIf(false)] bool condition,
        string? errorSubCategoryResourceName,
        IElementLocation location,
        string resourceName,
        T1 arg0)
    {
        if (!condition)
        {
            ThrowInvalidProject(errorSubCategoryResourceName, location, resourceName, arg0);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with the specified error subcategory and two format
    ///  arguments when the condition is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="errorSubCategoryResourceName">
    ///  The name of the error-subcategory resource, or <see langword="null"/> for no subcategory.
    /// </param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <param name="arg1">The second format argument.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        string? errorSubCategoryResourceName,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1)
    {
        if (!condition)
        {
            ThrowInvalidProject(errorSubCategoryResourceName, location, resourceName, arg0, arg1);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with the specified error subcategory and three format
    ///  arguments when the condition is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="errorSubCategoryResourceName">
    ///  The name of the error-subcategory resource, or <see langword="null"/> for no subcategory.
    /// </param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <param name="arg1">The second format argument.</param>
    /// <param name="arg2">The third format argument.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        string? errorSubCategoryResourceName,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2)
    {
        if (!condition)
        {
            ThrowInvalidProject(errorSubCategoryResourceName, location, resourceName, arg0, arg1, arg2);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with the specified error subcategory and four format
    ///  arguments when the condition is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth format argument.</typeparam>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="errorSubCategoryResourceName">
    ///  The name of the error-subcategory resource, or <see langword="null"/> for no subcategory.
    /// </param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <param name="arg1">The second format argument.</param>
    /// <param name="arg2">The third format argument.</param>
    /// <param name="arg3">The fourth format argument.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        string? errorSubCategoryResourceName,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
    {
        if (!condition)
        {
            ThrowInvalidProject(errorSubCategoryResourceName, location, resourceName, arg0, arg1, arg2, arg3);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception and the specified error
    ///  subcategory when the condition is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="errorSubCategoryResourceName">
    ///  The name of the error-subcategory resource, or <see langword="null"/> for no subcategory.
    /// </param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="args">The arguments used to format the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    internal static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        string? errorSubCategoryResourceName,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        params object?[] args)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(errorSubCategoryResourceName, location, innerException, resourceName, args);
        }
    }

    /// <summary>
    ///  Creates and throws an <see cref="InvalidProjectFileException"/> using the specified diagnostic information.
    /// </summary>
    /// <param name="errorSubCategoryResourceName">
    ///  The name of the error-subcategory resource, or <see langword="null"/> for no subcategory.
    /// </param>
    /// <param name="location">The project location associated with the error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the error message resource.</param>
    /// <param name="args">The arguments used to format the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private static void ThrowInvalidProjectCore(
        string? errorSubCategoryResourceName,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object?[] args)
    {
        Assumed.NotNull(location);

#if DEBUG
        if (errorSubCategoryResourceName is not null)
        {
            ResourceUtilities.VerifyResourceStringExists(errorSubCategoryResourceName);
        }

        ResourceUtilities.VerifyResourceStringExists(resourceName);
#endif

        string? errorSubCategory = errorSubCategoryResourceName is null
            ? null
            : AssemblyResources.GetString(errorSubCategoryResourceName);

        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode,
            out string? helpKeyword,
            resourceName,
            args);

        throw new InvalidProjectFileException(
            location.File,
            location.Line,
            location.Column,
            endLineNumber: 0,
            endColumnNumber: 0,
            message,
            errorSubCategory,
            errorCode,
            helpKeyword,
            innerException);
    }
}
