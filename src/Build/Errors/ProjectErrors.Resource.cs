// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;
using Microsoft.Build.Shared;

namespace Microsoft.Build;

internal static partial class ProjectErrors
{
    internal readonly struct Resource(ResourceManager resourceManager, string resourceName)
    {
        private readonly StringResource _resource = new(resourceManager, resourceName);

        public string ResourceName => resourceName;

        public Throwable Format(object? arg0)
            => new(_resource.Format(arg0), _resource.ErrorCode, _resource.HelpKeyword);

        public Throwable Format(object? arg0, object? arg1)
            => new(_resource.Format(arg0, arg1), _resource.ErrorCode, _resource.HelpKeyword);

        public Throwable Format(object? arg0, object? arg1, object? arg2)
            => new(_resource.Format(arg0, arg1, arg2), _resource.ErrorCode, _resource.HelpKeyword);

        public Throwable Format(params object?[] args)
            => new(_resource.Format(args), _resource.ErrorCode, _resource.HelpKeyword);

        public void Throw(IElementLocation location)
        {
            var throwable = new Throwable(_resource.Message, _resource.ErrorCode, _resource.HelpKeyword);
            throwable.Throw(location);
        }
    }
}
