// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework
{
    // TODO: this should be unified with Shared\ErrorUtilities.cs, but it is hard to untangle everything
    //       because some of the errors there will use localized resources from different assemblies,
    //       which won't be referenceable in Framework.

    internal static class FrameworkErrorUtilities
    {
        /// <summary>
        /// This method should be used in places where one would normally put
        /// an "assert". It should be used to validate that our assumptions are
        /// true, where false would indicate that there must be a bug in our
        /// code somewhere. This should not be used to throw errors based on bad
        /// user input or anything that the user did wrong.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="unformattedMessage"></param>
        internal static void VerifyThrow([DoesNotReturnIf(false)] bool condition, string unformattedMessage)
        {
            if (!condition)
            {
                ThrowInternalError(unformattedMessage, innerException: null, args: null);
            }
        }

        /// <summary>
        /// Helper to throw an InternalErrorException when the specified parameter is null.
        /// This should be used ONLY if this would indicate a bug in MSBuild rather than
        /// anything caused by user action.
        /// </summary>
        /// <param name="parameter">The value of the argument.</param>
        /// <param name="parameterName">Parameter that should not be null.</param>
        internal static void VerifyThrowInternalNull([NotNull] object? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
        {
            if (parameter is null)
            {
                ThrowInternalError("{0} unexpectedly null", innerException: null, args: parameterName);
            }
        }

        /// <summary>
        /// Helper to throw an InternalErrorException when the specified parameter is null or zero length.
        /// This should be used ONLY if this would indicate a bug in MSBuild rather than
        /// anything caused by user action.
        /// </summary>
        /// <param name="parameterValue">The value of the argument.</param>
        /// <param name="parameterName">Parameter that should not be null or zero length</param>
        internal static void VerifyThrowInternalLength([NotNull] string? parameterValue, [CallerArgumentExpression(nameof(parameterValue))] string? parameterName = null)
        {
            VerifyThrowInternalNull(parameterValue, parameterName);

            if (parameterValue.Length == 0)
            {
                ThrowInternalError("{0} unexpectedly empty", innerException: null, args: parameterName);
            }
        }

        /// <summary>
        /// Throws InternalErrorException.
        /// This is only for situations that would mean that there is a bug in MSBuild itself.
        /// </summary>
        [DoesNotReturn]
        internal static void ThrowInternalError(string message)
        {
            throw new InternalErrorException(message);
        }

        /// <summary>
        /// Throws InternalErrorException.
        /// This is only for situations that would mean that there is a bug in MSBuild itself.
        /// </summary>
        [DoesNotReturn]
        internal static void ThrowInternalError(string message, Exception? innerException, params object?[]? args)
        {
            throw new InternalErrorException(
                args is null ?
                    message :
                    string.Format(message, args),
                innerException);
        }

        /// <summary>
        /// Throws an ArgumentNullException if the given string parameter is null
        /// and ArgumentException if it has zero length.
        /// </summary>
        internal static void VerifyThrowArgumentInvalidPath(string parameter, [CallerArgumentExpression(nameof(parameter))] string? paramName = null)
        {
            if (FileUtilities.PathIsInvalid(parameter))
            {
                throw new ArgumentException(SR.FormatParameterCannotHaveInvalidPathChars(paramName, parameter), paramName);
            }
        }

        /// <summary>
        ///  Verify the parameters provided to a standard <see cref="ICollection{T}.CopyTo(T[], int)"/> call.
        /// </summary>
        /// <typeparam name="T">The element type of the destination array.</typeparam>
        /// <param name="array">The destination array to copy elements into.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <param name="requiredCapacity">The number of elements that need to be copied.</param>
        /// <param name="arrayParamName">The caller's parameter name for <paramref name="array"/>.</param>
        /// <param name="arrayIndexParamName">The caller's parameter name for <paramref name="arrayIndex"/>.</param>
        /// <exception cref="ArgumentNullException">
        ///  If <paramref name="array"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///  If <paramref name="arrayIndex"/> falls outside of the bounds <paramref name="array"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///  If there is insufficient capacity to copy the collection contents into <paramref name="array"/>
        ///  when starting at <paramref name="arrayIndex"/>.
        /// </exception>
        internal static void VerifyCollectionCopyToArguments<T>(
            [NotNull] T[]? array,
            int arrayIndex,
            int requiredCapacity,
            [CallerArgumentExpression(nameof(array))] string? arrayParamName = null,
            [CallerArgumentExpression(nameof(arrayIndex))] string? arrayIndexParamName = null)
        {
            ArgumentNullException.ThrowIfNull(array, arrayParamName);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex, arrayIndexParamName);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(arrayIndex, array.Length, arrayIndexParamName);

            int arrayCapacity = array.Length - arrayIndex;
            if (requiredCapacity > arrayCapacity)
            {
                throw new ArgumentException(SR.CollectionCopyToFailureProvidedArrayIsTooSmall, arrayParamName);
            }
        }
    }
}
