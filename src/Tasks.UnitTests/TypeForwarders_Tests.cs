// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;
using Assembly = System.Reflection.Assembly;

namespace Microsoft.Build.UnitTests;

public class TypeForwarders_Tests
{
    [Fact]
    public void TaskLoggingHelperExtensionIsForwardedFromTasks()
    {
        Type expectedType = typeof(TaskLoggingHelperExtension);
        Assembly tasksAssembly = typeof(TaskExtension).Assembly;
        Assembly frameworkAssembly = typeof(IBuildEngine).Assembly;

        Type resolvedType = Type
            .GetType(
                $"{expectedType.FullName}, {tasksAssembly.FullName}",
                throwOnError: true,
                ignoreCase: false)
            .ShouldNotBeNull();

        resolvedType.ShouldBe(expectedType);
        resolvedType.Assembly.ShouldBe(frameworkAssembly);
    }
}
