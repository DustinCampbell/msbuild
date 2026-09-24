// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_CultureInfo_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(CultureInfo), output)
{
    [Fact]
    public void CultureInfo_Constructors()
    {
        Constructor.Invoke(["en-US"]).ShouldBeOfType<CultureInfo>().Name.ShouldBe("en-US");
        Constructor.Invoke(["en-US", false]).ShouldBeOfType<CultureInfo>().UseUserOverride.ShouldBeFalse();
        Constructor.Invoke([1033]).ShouldBeOfType<CultureInfo>().LCID.ShouldBe(1033);
        Constructor.Invoke([1033, false]).ShouldBeOfType<CultureInfo>().UseUserOverride.ShouldBeFalse();
    }

    [Fact]
    public void CultureInfo_Members()
    {
        StaticMember(nameof(CultureInfo.CurrentUICulture)).Invoke().ShouldBe(CultureInfo.CurrentUICulture);
        StaticMember(nameof(CultureInfo.GetCultureInfo)).Invoke(["en-US"]).ShouldBe(CultureInfo.GetCultureInfo("en-US"));
        StaticMember(nameof(CultureInfo.GetCultureInfo)).Invoke([1033]).ShouldBe(CultureInfo.GetCultureInfo(1033));
        StaticMember(nameof(CultureInfo.GetCultureInfo)).Invoke(["en-US", "fr-FR"]).ShouldBe(CultureInfo.GetCultureInfo("en-US", "fr-FR"));
        InstanceMember(nameof(CultureInfo.ToString)).Invoke(CultureInfo.InvariantCulture).ShouldBe(CultureInfo.InvariantCulture.ToString());
    }
}
