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
