// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Uri_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Uri), output)
{
    private static readonly Uri s_baseUri = new("https://user:password@example.com:8080/root/");
    private static readonly Uri s_uri = new(s_baseUri, "path/file.txt?query=value#fragment");

    [Fact]
    public void Uri_Constructors()
    {
        Constructor.Invoke([s_uri.AbsoluteUri]).ShouldBe(s_uri);
        Constructor.Invoke([s_uri.AbsoluteUri, "Absolute"]).ShouldBe(s_uri);
        Constructor.Invoke([s_baseUri, "path/file.txt?query=value#fragment"]).ShouldBe(s_uri);
        Constructor.Invoke([s_baseUri, new Uri("path/file.txt?query=value#fragment", UriKind.Relative)]).ShouldBe(s_uri);
    }

    [Fact]
    public void Uri_StaticMembers()
    {
        StaticMember(nameof(Uri.CheckHostName)).Invoke(["example.com"]).ShouldBe(UriHostNameType.Dns);
        StaticMember(nameof(Uri.CheckSchemeName)).Invoke(["https"]).ShouldBe(true);
        StaticMember(nameof(Uri.Compare)).Invoke([s_uri, s_uri, "AbsoluteUri", "SafeUnescaped", "Ordinal"]).ShouldBe(0);
        StaticMember(nameof(Uri.EscapeDataString)).Invoke(["a b"]).ShouldBe("a%20b");
#pragma warning disable SYSLIB0013
        StaticMember(nameof(Uri.EscapeUriString)).Invoke(["https://example.com/a b"]).ShouldBe(Uri.EscapeUriString("https://example.com/a b"));
#pragma warning restore SYSLIB0013
        StaticMember(nameof(Uri.HexEscape)).Invoke(["A"]).ShouldBe("%41");
        StaticMember(nameof(Uri.IsHexDigit)).Invoke(["A"]).ShouldBe(true);
        StaticMember(nameof(Uri.IsHexEncoding)).Invoke(["%41", "0"]).ShouldBe(true);
        StaticMember(nameof(Uri.SchemeDelimiter)).Invoke().ShouldBe(Uri.SchemeDelimiter);
        StaticMember(nameof(Uri.UnescapeDataString)).Invoke(["a%20b"]).ShouldBe("a b");
    }

    [Fact]
    public void Uri_InstanceMembers()
    {
        InstanceMember(nameof(Uri.AbsolutePath)).Invoke(s_uri).ShouldBe(s_uri.AbsolutePath);
        InstanceMember(nameof(Uri.AbsoluteUri)).Invoke(s_uri).ShouldBe(s_uri.AbsoluteUri);
        InstanceMember(nameof(Uri.Authority)).Invoke(s_uri).ShouldBe(s_uri.Authority);
        InstanceMember(nameof(Uri.DnsSafeHost)).Invoke(s_uri).ShouldBe(s_uri.DnsSafeHost);
        InstanceMember(nameof(Uri.Fragment)).Invoke(s_uri).ShouldBe(s_uri.Fragment);
        InstanceMember(nameof(Uri.Host)).Invoke(s_uri).ShouldBe(s_uri.Host);
        InstanceMember(nameof(Uri.HostNameType)).Invoke(s_uri).ShouldBe(s_uri.HostNameType);
#if NET
        InstanceMember(nameof(Uri.IdnHost)).Invoke(s_uri).ShouldBe(s_uri.IdnHost);
#endif
        InstanceMember(nameof(Uri.IsAbsoluteUri)).Invoke(s_uri).ShouldBe(s_uri.IsAbsoluteUri);
        InstanceMember(nameof(Uri.IsDefaultPort)).Invoke(s_uri).ShouldBe(s_uri.IsDefaultPort);
        InstanceMember(nameof(Uri.IsFile)).Invoke(s_uri).ShouldBe(s_uri.IsFile);
        InstanceMember(nameof(Uri.IsLoopback)).Invoke(s_uri).ShouldBe(s_uri.IsLoopback);
        InstanceMember(nameof(Uri.IsUnc)).Invoke(s_uri).ShouldBe(s_uri.IsUnc);
        InstanceMember(nameof(Uri.LocalPath)).Invoke(s_uri).ShouldBe(s_uri.LocalPath);
        InstanceMember(nameof(Uri.OriginalString)).Invoke(s_uri).ShouldBe(s_uri.OriginalString);
        InstanceMember(nameof(Uri.PathAndQuery)).Invoke(s_uri).ShouldBe(s_uri.PathAndQuery);
        InstanceMember(nameof(Uri.Port)).Invoke(s_uri).ShouldBe(s_uri.Port);
        InstanceMember(nameof(Uri.Query)).Invoke(s_uri).ShouldBe(s_uri.Query);
        InstanceMember(nameof(Uri.Scheme)).Invoke(s_uri).ShouldBe(s_uri.Scheme);
        InstanceMember(nameof(Uri.Segments)).Invoke(s_uri).ShouldBe(s_uri.Segments);
        InstanceMember(nameof(Uri.UserEscaped)).Invoke(s_uri).ShouldBe(s_uri.UserEscaped);
        InstanceMember(nameof(Uri.UserInfo)).Invoke(s_uri).ShouldBe(s_uri.UserInfo);
        InstanceMember(nameof(Uri.Equals)).Invoke(s_uri, [new Uri(s_uri.AbsoluteUri)]).ShouldBe(true);
        InstanceMember(nameof(Uri.GetComponents)).Invoke(s_uri, ["HostAndPort", "SafeUnescaped"]).ShouldBe(s_uri.GetComponents(UriComponents.HostAndPort, UriFormat.SafeUnescaped));
        InstanceMember(nameof(Uri.GetHashCode)).Invoke(s_uri).ShouldBe(s_uri.GetHashCode());
        InstanceMember(nameof(Uri.IsBaseOf)).Invoke(s_baseUri, [s_uri]).ShouldBe(true);
        InstanceMember(nameof(Uri.MakeRelativeUri)).Invoke(s_baseUri, [s_uri]).ShouldBe(s_baseUri.MakeRelativeUri(s_uri));
        InstanceMember(nameof(Uri.ToString)).Invoke(s_uri).ShouldBe(s_uri.ToString());
    }
}

public class WellKnownFunctions_UriBuilder_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(UriBuilder), output)
{
    [Fact]
    public void UriBuilder_Constructors()
    {
        Constructor.Invoke().ShouldBeOfType<UriBuilder>();
        Constructor.Invoke(["https://example.com/path"]).ShouldBeOfType<UriBuilder>();
        Constructor.Invoke([new Uri("https://example.com/path")]).ShouldBeOfType<UriBuilder>();
        Constructor.Invoke(["https", "example.com"]).ShouldBeOfType<UriBuilder>();
        Constructor.Invoke(["https", "example.com", "8080"]).ShouldBeOfType<UriBuilder>();
        Constructor.Invoke(["https", "example.com", "8080", "path"]).ShouldBeOfType<UriBuilder>();
        Constructor.Invoke(["https", "example.com", "8080", "path", "?query=value"]).ShouldBeOfType<UriBuilder>();
    }

    [Fact]
    public void UriBuilder_InstanceMembers()
    {
        var builder = new UriBuilder("https://user:password@example.com:8080/path?query=value#fragment");

        InstanceMember(nameof(UriBuilder.Fragment)).Invoke(builder).ShouldBe(builder.Fragment);
        InstanceMember(nameof(UriBuilder.Host)).Invoke(builder).ShouldBe(builder.Host);
        InstanceMember(nameof(UriBuilder.Password)).Invoke(builder).ShouldBe(builder.Password);
        InstanceMember(nameof(UriBuilder.Path)).Invoke(builder).ShouldBe(builder.Path);
        InstanceMember(nameof(UriBuilder.Port)).Invoke(builder).ShouldBe(builder.Port);
        InstanceMember(nameof(UriBuilder.Query)).Invoke(builder).ShouldBe(builder.Query);
        InstanceMember(nameof(UriBuilder.Scheme)).Invoke(builder).ShouldBe(builder.Scheme);
        InstanceMember(nameof(UriBuilder.Uri)).Invoke(builder).ShouldBe(builder.Uri);
        InstanceMember(nameof(UriBuilder.UserName)).Invoke(builder).ShouldBe(builder.UserName);
        InstanceMember(nameof(UriBuilder.Equals)).Invoke(builder, [new UriBuilder(builder.Uri)]).ShouldBe(true);
        InstanceMember(nameof(UriBuilder.GetHashCode)).Invoke(builder).ShouldBe(builder.GetHashCode());
        InstanceMember(nameof(UriBuilder.ToString)).Invoke(builder).ShouldBe(builder.ToString());
    }
}
