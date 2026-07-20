// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ResourceProvider_Tests
{
    private static ResourceProvider CreateProvider(
        ImmutableArray<(string Key, string? Value)> primary,
        ImmutableArray<(string Key, string? Value)>? shared = null)
        => shared is { } sharedValue
            ? new(TestResourceManager.Create(primary), TestResourceManager.Create(sharedValue))
            : new(TestResourceManager.Create(primary));

    private static ResourceProvider CreateProvider(
        (string Key, string? Value) primary,
        (string Key, string? Value)? shared = null)
        => shared is { } sharedValue
            ? CreateProvider([primary], [sharedValue])
            : CreateProvider([primary]);

    private static ResourceProvider CreateProvider(string key, string? value)
        => new(TestResourceManager.Create(key, value));

    [Fact]
    public void PrimaryResources_ReturnsProvidedManager()
    {
        var provider = new ResourceProvider(TestResourceManager.Empty);

        provider.PrimaryResources.ShouldBeSameAs(TestResourceManager.Empty);
    }

    [Fact]
    public void SharedResources_ReturnsProvidedManager()
    {
        var provider = new ResourceProvider(
            primaryResources: TestResourceManager.Empty,
            sharedResources: TestResourceManager.Empty);

        provider.SharedResources.ShouldBeSameAs(TestResourceManager.Empty);
    }

    [Fact]
    public void SharedResources_IsNullWhenNotProvided()
    {
        var provider = new ResourceProvider(TestResourceManager.Empty);

        provider.SharedResources.ShouldBeNull();
    }

    [Fact]
    public void GetStringOrNull_ReturnsFromPrimary()
    {
        ResourceProvider provider = CreateProvider("Greeting", "Hello");

        provider.GetStringOrNull("Greeting").ShouldBe("Hello");
    }

    [Fact]
    public void GetStringOrNull_FallsBackToShared()
    {
        ResourceProvider provider = CreateProvider(
            primary: ("Greeting", "Hello"),
            shared: ("Farewell", "Goodbye"));

        provider.GetStringOrNull("Farewell").ShouldBe("Goodbye");
    }

    [Fact]
    public void GetStringOrNull_PrefersPrimaryOverShared()
    {
        ResourceProvider provider = CreateProvider(
            primary: ("Greeting", "FromPrimary"),
            shared: ("Greeting", "FromShared"));

        provider.GetStringOrNull("Greeting").ShouldBe("FromPrimary");
    }

    [Fact]
    public void GetStringOrNull_ReturnsNullWhenMissing()
    {
        ResourceProvider provider = CreateProvider(
            primary: ("Greeting", "Hello"),
            shared: ("Farewell", "Goodbye"));

        provider.GetStringOrNull("DoesNotExist").ShouldBeNull();
    }

    [Fact]
    public void GetStringOrNull_ReturnsNullWhenMissingAndNoShared()
    {
        ResourceProvider provider = CreateProvider("Greeting", "Hello");

        provider.GetStringOrNull("DoesNotExist").ShouldBeNull();
    }

    [Fact]
    public void GetString_ReturnsFromPrimary()
    {
        ResourceProvider provider = CreateProvider("Greeting", "Hello");

        provider.GetString("Greeting").ShouldBe("Hello");
    }

    [Fact]
    public void GetString_FallsBackToShared()
    {
        ResourceProvider provider = CreateProvider(
            primary: ("Greeting", "Hello"),
            shared: ("Farewell", "Goodbye"));

        provider.GetString("Farewell").ShouldBe("Goodbye");
    }

    [Fact]
    public void GetString_ThrowsWhenMissing()
    {
        ResourceProvider provider = CreateProvider("Greeting", "Hello");

        Should.Throw<InternalErrorException>(() => provider.GetString("DoesNotExist"));
    }
}
