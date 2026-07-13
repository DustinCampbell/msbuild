// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;

namespace Microsoft.Build;

internal static partial class ProjectErrors
{
    internal readonly struct Throwable(string message, string? errorCode, string helpKeyword)
    {
        public void Throw(IElementLocation location)
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
}
