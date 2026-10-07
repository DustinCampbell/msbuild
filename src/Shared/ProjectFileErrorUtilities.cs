// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework.Resources;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains methods that are useful for error checking and validation of project files.
    /// </summary>
    internal static class ProjectFileErrorUtilities
    {
        internal static void ThrowInvalidProjectFile(
            BuildEventFileInfo projectFile,
            ResourceString resource)
            => VerifyThrowInvalidProjectFileCore(false, null, projectFile, null, resource);

        internal static void ThrowInvalidProjectFile<T1>(
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0)
            => VerifyThrowInvalidProjectFileCore(false, null, projectFile, null, resource.WithArguments(arg0));

        internal static void ThrowInvalidProjectFile<T1, T2>(
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0,
            T2 arg1)
            => VerifyThrowInvalidProjectFileCore(false, null, projectFile, null, resource.WithArguments(arg0, arg1));

        internal static void ThrowInvalidProjectFile<T1, T2, T3>(
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2)
            => VerifyThrowInvalidProjectFileCore(false, null, projectFile, null, resource.WithArguments(arg0, arg1, arg2));

        internal static void ThrowInvalidProjectFile(
            BuildEventFileInfo projectFile,
            ResourceString resource,
            params object[] args)
            => VerifyThrowInvalidProjectFileWithArguments(false, null, projectFile, null, resource, args);

        internal static void ThrowInvalidProjectFile<T1>(
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            T1 arg0)
            => VerifyThrowInvalidProjectFileCore(false, null, projectFile, innerException, resource.WithArguments(arg0));

        internal static void ThrowInvalidProjectFile<T1, T2>(
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            T1 arg0,
            T2 arg1)
            => VerifyThrowInvalidProjectFileCore(false, null, projectFile, innerException, resource.WithArguments(arg0, arg1));

        internal static void ThrowInvalidProjectFile<T1, T2, T3>(
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2)
            => VerifyThrowInvalidProjectFileCore(false, null, projectFile, innerException, resource.WithArguments(arg0, arg1, arg2));

        internal static void ThrowInvalidProjectFile(
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            params object[] args)
            => VerifyThrowInvalidProjectFileWithArguments(false, null, projectFile, innerException, resource, args);

        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            BuildEventFileInfo projectFile,
            ResourceString resource)
            => VerifyThrowInvalidProjectFileCore(condition, null, projectFile, null, resource);

        internal static void VerifyThrowInvalidProjectFile<T1>(
            bool condition,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0)
            => VerifyThrowInvalidProjectFileCore(condition, null, projectFile, null, resource.WithArguments(arg0));

        internal static void VerifyThrowInvalidProjectFile<T1, T2>(
            bool condition,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0,
            T2 arg1)
            => VerifyThrowInvalidProjectFileCore(condition, null, projectFile, null, resource.WithArguments(arg0, arg1));

        internal static void VerifyThrowInvalidProjectFile<T1, T2, T3>(
            bool condition,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2)
            => VerifyThrowInvalidProjectFileCore(condition, null, projectFile, null, resource.WithArguments(arg0, arg1, arg2));

        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            params object[] args)
            => VerifyThrowInvalidProjectFileWithArguments(condition, null, projectFile, null, resource, args);

        internal static void ThrowInvalidProjectFile(
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource)
            => VerifyThrowInvalidProjectFileCore(false, errorSubCategory, projectFile, null, resource);

        internal static void ThrowInvalidProjectFile<T1>(
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0)
            => VerifyThrowInvalidProjectFileCore(false, errorSubCategory, projectFile, null, resource.WithArguments(arg0));

        internal static void ThrowInvalidProjectFile<T1, T2>(
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0,
            T2 arg1)
            => VerifyThrowInvalidProjectFileCore(false, errorSubCategory, projectFile, null, resource.WithArguments(arg0, arg1));

        internal static void ThrowInvalidProjectFile<T1, T2, T3>(
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2)
            => VerifyThrowInvalidProjectFileCore(false, errorSubCategory, projectFile, null, resource.WithArguments(arg0, arg1, arg2));

        internal static void ThrowInvalidProjectFile(
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            params object[] args)
            => VerifyThrowInvalidProjectFileWithArguments(false, errorSubCategory, projectFile, null, resource, args);

        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource)
            => VerifyThrowInvalidProjectFileCore(condition, errorSubCategory, projectFile, null, resource);

        internal static void VerifyThrowInvalidProjectFile<T1>(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0)
            => VerifyThrowInvalidProjectFileCore(condition, errorSubCategory, projectFile, null, resource.WithArguments(arg0));

        internal static void VerifyThrowInvalidProjectFile<T1, T2>(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0,
            T2 arg1)
            => VerifyThrowInvalidProjectFileCore(condition, errorSubCategory, projectFile, null, resource.WithArguments(arg0, arg1));

        internal static void VerifyThrowInvalidProjectFile<T1, T2, T3>(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2)
            => VerifyThrowInvalidProjectFileCore(condition, errorSubCategory, projectFile, null, resource.WithArguments(arg0, arg1, arg2));

        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            ResourceString resource,
            params object[] args)
            => VerifyThrowInvalidProjectFileWithArguments(condition, errorSubCategory, projectFile, null, resource, args);

        internal static void VerifyThrowInvalidProjectFile<T1>(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            T1 arg0)
            => VerifyThrowInvalidProjectFileCore(condition, errorSubCategory, projectFile, innerException, resource.WithArguments(arg0));

        internal static void VerifyThrowInvalidProjectFile<T1, T2>(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            T1 arg0,
            T2 arg1)
            => VerifyThrowInvalidProjectFileCore(condition, errorSubCategory, projectFile, innerException, resource.WithArguments(arg0, arg1));

        internal static void VerifyThrowInvalidProjectFile<T1, T2, T3>(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            T1 arg0,
            T2 arg1,
            T3 arg2)
            => VerifyThrowInvalidProjectFileCore(condition, errorSubCategory, projectFile, innerException, resource.WithArguments(arg0, arg1, arg2));

        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            params object[] args)
            => VerifyThrowInvalidProjectFileWithArguments(condition, errorSubCategory, projectFile, innerException, resource, args);

        private static void VerifyThrowInvalidProjectFileWithArguments(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            Exception innerException,
            ResourceString resource,
            object[] args)
        {
            Assumed.NotNull(
                projectFile,
                "Must specify the invalid project file. If project file is not available, use VerifyThrowInvalidProject() and pass in the XML node instead.");

            if (!condition)
            {
                string errorSubCategoryText = errorSubCategory?.Resolve().OriginalText;
                string message = ResourceStringUtilities.FormatResourceStringStripCodeAndKeyword(
                    out string errorCode,
                    out string helpKeyword,
                    resource,
                    args);

                throw new InvalidProjectFileException(
                    projectFile.File,
                    projectFile.Line,
                    projectFile.Column,
                    projectFile.EndLine,
                    projectFile.EndColumn,
                    message,
                    errorSubCategoryText,
                    errorCode,
                    helpKeyword,
                    innerException);
            }
        }

        private static void VerifyThrowInvalidProjectFileCore<TResource>(
            bool condition,
            ResourceString errorSubCategory,
            BuildEventFileInfo projectFile,
            Exception innerException,
            TResource resource)
            where TResource : IResourceString
        {
            Assumed.NotNull(
                projectFile,
                "Must specify the invalid project file. If project file is not available, use VerifyThrowInvalidProject() and pass in the XML node instead.");

            if (!condition)
            {
                string errorSubCategoryText = errorSubCategory?.Resolve().OriginalText;
                ResolvedResourceString resolved = resource.Resolve();

                throw new InvalidProjectFileException(
                    projectFile.File,
                    projectFile.Line,
                    projectFile.Column,
                    projectFile.EndLine,
                    projectFile.EndColumn,
                    resolved.Text,
                    errorSubCategoryText,
                    resolved.Code,
                    resolved.HelpKeyword,
                    innerException);
            }
        }
    }
}
