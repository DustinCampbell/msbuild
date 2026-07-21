// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Framework.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class InvalidOperationExceptionExtensions_Tests
{
    private const string Name = "Sample";

    private static ResourceString CreateResource(string text)
    {
        TestResourceManager manager = TestResourceManager.Create(Name, text);
        var provider = new ResourceProvider(manager);

        return new(provider, Name, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Throw_ThrowsInvalidOperationExceptionWithStrippedCode()
    {
        ResourceString resource = CreateResource("MSB1234: Something went wrong");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.Throw(resource));

        exception.Message.ShouldBe("Something went wrong");
    }

    [Fact]
    public void Throw_WithOneArgument_FormatsAndStripsCode()
    {
        ResourceString resource = CreateResource("MSB1234: Hello {0}");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.Throw(resource, "World"));

        exception.Message.ShouldBe("Hello World");
    }

    [Fact]
    public void Throw_WithTwoArguments_FormatsAndStripsCode()
    {
        ResourceString resource = CreateResource("MSB1234: {0} and {1}");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.Throw(resource, "a", "b"));

        exception.Message.ShouldBe("a and b");
    }

    [Fact]
    public void Throw_WithThreeArguments_FormatsAndStripsCode()
    {
        ResourceString resource = CreateResource("MSB1234: {0}, {1}, {2}");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.Throw(resource, "a", "b", "c"));

        exception.Message.ShouldBe("a, b, c");
    }

    [Fact]
    public void Throw_WithParamsArray_FormatsAndStripsCode()
    {
        ResourceString resource = CreateResource("MSB1234: {0}-{1}-{2}-{3}");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.Throw(resource, ["a", "b", "c", "d"]));

        exception.Message.ShouldBe("a-b-c-d");
    }

    [Fact]
    public void ThrowIfFalse_WhenConditionIsFalse_Throws()
    {
        ResourceString resource = CreateResource("MSB1234: Boom");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.ThrowIfFalse(false, resource));

        exception.Message.ShouldBe("Boom");
    }

    [Fact]
    public void ThrowIfFalse_WhenConditionIsTrue_DoesNotThrow()
    {
        ResourceString resource = CreateResource("MSB1234: Boom");

        Should.NotThrow(() => InvalidOperationException.ThrowIfFalse(true, resource));
    }

    [Fact]
    public void ThrowIfFalse_WithArguments_FormatsAndStripsCode()
    {
        ResourceString resource = CreateResource("MSB1234: {0} and {1}");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.ThrowIfFalse(false, resource, "a", "b"));

        exception.Message.ShouldBe("a and b");
    }

    [Fact]
    public void ThrowIfTrue_WhenConditionIsTrue_Throws()
    {
        ResourceString resource = CreateResource("MSB1234: Boom");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.ThrowIfTrue(true, resource));

        exception.Message.ShouldBe("Boom");
    }

    [Fact]
    public void ThrowIfTrue_WhenConditionIsFalse_DoesNotThrow()
    {
        ResourceString resource = CreateResource("MSB1234: Boom");

        Should.NotThrow(() => InvalidOperationException.ThrowIfTrue(false, resource));
    }

    [Fact]
    public void ThrowIfTrue_WithArguments_FormatsAndStripsCode()
    {
        ResourceString resource = CreateResource("MSB1234: {0} and {1}");

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(() => InvalidOperationException.ThrowIfTrue(true, resource, "a", "b"));

        exception.Message.ShouldBe("a and b");
    }
}
