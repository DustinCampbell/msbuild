// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.Resources;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains methods that are useful for error checking and validation of project files.
    /// </summary>
    internal static class ProjectErrorUtilities
    {
        internal static void VerifyThrowInvalidProject(bool condition, IElementLocation elementLocation, ResourceString resource)
        {
            if (!condition)
            {
                ThrowInvalidProject(elementLocation, resource);
            }
        }

        internal static void ThrowInvalidProject(IElementLocation elementLocation, ResourceString resource)
            => ThrowInvalidProject(null, elementLocation, resource);

        internal static void ThrowInvalidProject<T1>(IElementLocation elementLocation, ResourceString resource, T1 arg0)
            => ThrowInvalidProject(null, elementLocation, resource.WithArguments(arg0));

        internal static void ThrowInvalidProject<T1, T2>(IElementLocation elementLocation, ResourceString resource, T1 arg0, T2 arg1)
            => ThrowInvalidProject(null, elementLocation, resource.WithArguments(arg0, arg1));

        internal static void ThrowInvalidProject<T1, T2, T3>(IElementLocation elementLocation, ResourceString resource, T1 arg0, T2 arg1, T3 arg2)
            => ThrowInvalidProject(null, elementLocation, resource.WithArguments(arg0, arg1, arg2));

        internal static void ThrowInvalidProject<T1, T2, T3, T4>(
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2,
            T4 arg3)
            => ThrowInvalidProject(null, elementLocation, resource.WithArguments(arg0, arg1, arg2, arg3));

        internal static void ThrowInvalidProject(IElementLocation elementLocation, ResourceString resource, params object[] args)
            => ThrowInvalidProject(null, elementLocation, resource, args);

        internal static void VerifyThrowInvalidProject<T1>(
            bool condition,
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0)
        {
            if (!condition)
            {
                ThrowInvalidProject(null, elementLocation, resource.WithArguments(arg0));
            }
        }

        internal static void VerifyThrowInvalidProject<T1, T2>(
            bool condition,
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0,
            T2 arg1)
        {
            if (!condition)
            {
                ThrowInvalidProject(null, elementLocation, resource.WithArguments(arg0, arg1));
            }
        }

        internal static void VerifyThrowInvalidProject<T1, T2, T3>(
            bool condition,
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2)
        {
            if (!condition)
            {
                ThrowInvalidProject(null, elementLocation, resource.WithArguments(arg0, arg1, arg2));
            }
        }

        internal static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
            bool condition,
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2,
            T4 arg3)
        {
            if (!condition)
            {
                ThrowInvalidProject(null, elementLocation, resource.WithArguments(arg0, arg1, arg2, arg3));
            }
        }

        internal static void VerifyThrowInvalidProject(
            bool condition,
            ResourceString errorSubCategory,
            IElementLocation elementLocation,
            ResourceString resource)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategory, elementLocation, resource);
            }
        }

        internal static void VerifyThrowInvalidProject<T1>(
            bool condition,
            ResourceString errorSubCategory,
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategory, elementLocation, resource.WithArguments(arg0));
            }
        }

        internal static void VerifyThrowInvalidProject<T1, T2>(
            bool condition,
            ResourceString errorSubCategory,
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0,
            T2 arg1)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategory, elementLocation, resource.WithArguments(arg0, arg1));
            }
        }

        internal static void VerifyThrowInvalidProject<T1, T2, T3>(
            bool condition,
            ResourceString errorSubCategory,
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategory, elementLocation, resource.WithArguments(arg0, arg1, arg2));
            }
        }

        internal static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
            bool condition,
            ResourceString errorSubCategory,
            IElementLocation elementLocation,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2,
            T4 arg3)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategory, elementLocation, resource.WithArguments(arg0, arg1, arg2, arg3));
            }
        }

        private static void ThrowInvalidProject<TResource>(
            ResourceString errorSubCategory,
            IElementLocation elementLocation,
            TResource resource)
            where TResource : IResourceString
        {
            Assumed.NotNull(elementLocation);

            string errorSubCategoryText = errorSubCategory?.Resolve().OriginalText;
            ResolvedResourceString resolved = resource.Resolve();

            throw new InvalidProjectFileException(
                elementLocation.File,
                elementLocation.Line,
                elementLocation.Column,
                0,
                0,
                resolved.Text,
                errorSubCategoryText,
                resolved.Code,
                resolved.HelpKeyword);
        }

        private static void ThrowInvalidProject(
            ResourceString errorSubCategory,
            IElementLocation elementLocation,
            ResourceString resource,
            params object[] args)
        {
            Assumed.NotNull(elementLocation);

            string errorSubCategoryText = errorSubCategory?.Resolve().OriginalText;
            string message = ResourceStringUtilities.FormatResourceStringStripCodeAndKeyword(
                out string errorCode,
                out string helpKeyword,
                resource,
                args);

            throw new InvalidProjectFileException(
                elementLocation.File,
                elementLocation.Line,
                elementLocation.Column,
                0,
                0,
                message,
                errorSubCategoryText,
                errorCode,
                helpKeyword);
        }
    }
}
