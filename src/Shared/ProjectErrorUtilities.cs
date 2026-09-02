// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using Microsoft.Build.Framework.Utilities;
#endif

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Exceptions;

namespace Microsoft.Build.Shared;

/// <summary>
///  Provides helpers for validating project-file input and throwing localized
///  <see cref="InvalidProjectFileException"/> instances.
/// </summary>
/// <remarks>
///  Use these helpers for invalid user-authored project content. Use <see cref="Assumed"/> for internal
///  programming errors. An overload accepting an inner exception could improve diagnostics for hosts.
/// </remarks>
internal static class ProjectErrorUtilities
{
    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> when <paramref name="condition"/> is
    ///  <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <exception cref="InvalidProjectFileException">
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    internal static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName)
    {
        if (!condition)
        {
            ThrowInvalidProject(location, resourceName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> when <paramref name="condition"/> is
    ///  <see langword="false"/>, formatting the localized message with one argument.
    /// </summary>
    /// <typeparam name="T1">The type of the formatting argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="arg0">The value used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    internal static void VerifyThrowInvalidProject<T1>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0)
    {
        if (!condition)
        {
            ThrowInvalidProject(location, resourceName, arg0);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> when <paramref name="condition"/> is
    ///  <see langword="false"/>, formatting the localized message with two arguments.
    /// </summary>
    /// <typeparam name="T1">The type of the first formatting argument.</typeparam>
    /// <typeparam name="T2">The type of the second formatting argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="arg0">The first value used to format the localized resource.</param>
    /// <param name="arg1">The second value used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    internal static void VerifyThrowInvalidProject<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1)
    {
        if (!condition)
        {
            ThrowInvalidProject(location, resourceName, arg0, arg1);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> when <paramref name="condition"/> is
    ///  <see langword="false"/>, formatting the localized message with three arguments.
    /// </summary>
    /// <typeparam name="T1">The type of the first formatting argument.</typeparam>
    /// <typeparam name="T2">The type of the second formatting argument.</typeparam>
    /// <typeparam name="T3">The type of the third formatting argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="arg0">The first value used to format the localized resource.</param>
    /// <param name="arg1">The second value used to format the localized resource.</param>
    /// <param name="arg2">The third value used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    internal static void VerifyThrowInvalidProject<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2)
    {
        if (!condition)
        {
            ThrowInvalidProject(location, resourceName, arg0, arg1, arg2);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> when <paramref name="condition"/> is
    ///  <see langword="false"/>, formatting the localized message with four arguments.
    /// </summary>
    /// <typeparam name="T1">The type of the first formatting argument.</typeparam>
    /// <typeparam name="T2">The type of the second formatting argument.</typeparam>
    /// <typeparam name="T3">The type of the third formatting argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth formatting argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="arg0">The first value used to format the localized resource.</param>
    /// <param name="arg1">The second value used to format the localized resource.</param>
    /// <param name="arg2">The third value used to format the localized resource.</param>
    /// <param name="arg3">The fourth value used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    internal static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
    {
        if (!condition)
        {
            ThrowInvalidProject(location, resourceName, arg0, arg1, arg2, arg3);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> using a localized resource with no formatting
    ///  arguments.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(IElementLocation location, string resourceName)
    {
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode,
            out string? helpKeyword,
            resourceName);

        ThrowInvalidProjectCore(message, errorCode, helpKeyword, location);
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/>, formatting the localized message with one
    ///  argument.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="arg0">The value used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(IElementLocation location, string resourceName, object? arg0)
    {
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode,
            out string? helpKeyword,
            resourceName,
            arg0);

        ThrowInvalidProjectCore(message, errorCode, helpKeyword, location);
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/>, formatting the localized message with two
    ///  arguments.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="arg0">The first value used to format the localized resource.</param>
    /// <param name="arg1">The second value used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(
        IElementLocation location,
        string resourceName,
        object? arg0,
        object? arg1)
    {
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode,
            out string? helpKeyword,
            resourceName,
            arg0,
            arg1);

        ThrowInvalidProjectCore(message, errorCode, helpKeyword, location);
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/>, formatting the localized message with three
    ///  arguments.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="arg0">The first value used to format the localized resource.</param>
    /// <param name="arg1">The second value used to format the localized resource.</param>
    /// <param name="arg2">The third value used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(
        IElementLocation location,
        string resourceName,
        object? arg0,
        object? arg1,
        object? arg2)
    {
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode,
            out string? helpKeyword,
            resourceName,
            arg0,
            arg1,
            arg2);

        ThrowInvalidProjectCore(message, errorCode, helpKeyword, location);
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/>, formatting the localized message with a parameter
    ///  array.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="args">The values used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    /// <remarks>
    ///  The expanded <see langword="params"/> form allows the compiler to avoid allocating an argument array.
    ///  Value-type arguments may still be boxed as <see cref="object"/>.
    /// </remarks>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(IElementLocation location, string resourceName, params object?[] args)
    {
        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode,
            out string? helpKeyword,
            resourceName,
            args);

        ThrowInvalidProjectCore(message, errorCode, helpKeyword, location);
    }

#if NET

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/>, formatting the localized message with arguments
    ///  supplied through a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The name of the localized resource used for the error message.</param>
    /// <param name="args">The values used to format the localized resource.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    /// <remarks>
    ///  The expanded <see langword="params"/> form allows the compiler to avoid allocating an argument array.
    ///  Value-type arguments may still be boxed as <see cref="object"/>.
    /// </remarks>
    [DoesNotReturn]
    internal static void ThrowInvalidProject(
        IElementLocation location,
        string resourceName,
        params ReadOnlySpan<object?> args)
    {
        string helpKeyword = ResourceUtilities.GetHelpKeyword(resourceName);
        string message = MessageFormatter.Format(ResourceUtilities.GetResourceString(resourceName), args);

        message = MessageParser.TryParseMSBuildCode(message, out string? errorCode, out string? strippedMessage)
            ? strippedMessage
            : message;

        ThrowInvalidProjectCore(message, errorCode, helpKeyword, location);
    }

#endif

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> using a formatted message and its diagnostic
    ///  metadata.
    /// </summary>
    /// <param name="message">The formatted error message without its code or help keyword.</param>
    /// <param name="errorCode">The MSBuild error code, or <see langword="null"/>.</param>
    /// <param name="helpKeyword">The IDE help keyword, or <see langword="null"/>.</param>
    /// <param name="location">The project element location associated with the error.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private static void ThrowInvalidProjectCore(
        string message,
        string? errorCode,
        string? helpKeyword,
        IElementLocation location)
    {
        Assumed.NotNull(location);

        throw new InvalidProjectFileException(
            location.File,
            location.Line,
            location.Column,
            endLineNumber: 0,
            endColumnNumber: 0,
            message,
            errorSubcategory: null,
            errorCode,
            helpKeyword);
    }
}
