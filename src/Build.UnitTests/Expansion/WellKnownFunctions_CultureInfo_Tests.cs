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
#if NET
        StaticMember(nameof(CultureInfo.GetCultureInfo)).Invoke(["en-US", true]).ShouldBe(CultureInfo.GetCultureInfo("en-US", true));
#endif
    }

    [Fact]
    public void CultureInfo_InstanceProperties()
    {
        CultureInfo culture = new("en-US", false);

        InstanceMember(nameof(CultureInfo.Calendar)).Invoke(culture).ShouldBe(culture.Calendar);
        InstanceMember(nameof(CultureInfo.CompareInfo)).Invoke(culture).ShouldBe(culture.CompareInfo);
        InstanceMember(nameof(CultureInfo.CultureTypes)).Invoke(culture).ShouldBe(culture.CultureTypes);
        InstanceMember(nameof(CultureInfo.DateTimeFormat)).Invoke(culture).ShouldBe(culture.DateTimeFormat);
        InstanceMember(nameof(CultureInfo.DisplayName)).Invoke(culture).ShouldBe(culture.DisplayName);
        InstanceMember(nameof(CultureInfo.EnglishName)).Invoke(culture).ShouldBe(culture.EnglishName);
        InstanceMember(nameof(CultureInfo.IetfLanguageTag)).Invoke(culture).ShouldBe(culture.IetfLanguageTag);
        InstanceMember(nameof(CultureInfo.IsNeutralCulture)).Invoke(culture).ShouldBe(culture.IsNeutralCulture);
        InstanceMember(nameof(CultureInfo.IsReadOnly)).Invoke(culture).ShouldBe(culture.IsReadOnly);
        InstanceMember(nameof(CultureInfo.KeyboardLayoutId)).Invoke(culture).ShouldBe(culture.KeyboardLayoutId);
        InstanceMember(nameof(CultureInfo.LCID)).Invoke(culture).ShouldBe(culture.LCID);
        InstanceMember(nameof(CultureInfo.Name)).Invoke(culture).ShouldBe(culture.Name);
        InstanceMember(nameof(CultureInfo.NativeName)).Invoke(culture).ShouldBe(culture.NativeName);
        InstanceMember(nameof(CultureInfo.NumberFormat)).Invoke(culture).ShouldBe(culture.NumberFormat);
        InstanceMember(nameof(CultureInfo.OptionalCalendars))
            .Invoke(culture)
            .ShouldBeOfType<Calendar[]>()
            .Length.ShouldBe(culture.OptionalCalendars.Length);
        InstanceMember(nameof(CultureInfo.Parent)).Invoke(culture).ShouldBe(culture.Parent);
        InstanceMember(nameof(CultureInfo.TextInfo)).Invoke(culture).ShouldBe(culture.TextInfo);
        InstanceMember(nameof(CultureInfo.ThreeLetterISOLanguageName)).Invoke(culture).ShouldBe(culture.ThreeLetterISOLanguageName);
        InstanceMember(nameof(CultureInfo.ThreeLetterWindowsLanguageName)).Invoke(culture).ShouldBe(culture.ThreeLetterWindowsLanguageName);
        InstanceMember(nameof(CultureInfo.TwoLetterISOLanguageName)).Invoke(culture).ShouldBe(culture.TwoLetterISOLanguageName);
        InstanceMember(nameof(CultureInfo.UseUserOverride)).Invoke(culture).ShouldBe(culture.UseUserOverride);
    }

    [Fact]
    public void CultureInfo_InstanceMethods()
    {
        CultureInfo culture = new("en-US");

        InstanceMember(nameof(CultureInfo.Clone)).Invoke(culture).ShouldBe(culture.Clone());
        InstanceMember(nameof(CultureInfo.Equals)).Invoke(culture, [new CultureInfo("en-US")]).ShouldBe(true);
        InstanceMember(nameof(CultureInfo.GetConsoleFallbackUICulture)).Invoke(culture).ShouldBe(culture.GetConsoleFallbackUICulture());
        InstanceMember(nameof(CultureInfo.GetFormat)).Invoke(culture, [typeof(NumberFormatInfo)]).ShouldBe(culture.NumberFormat);
        InstanceMember(nameof(CultureInfo.GetHashCode)).Invoke(culture).ShouldBe(culture.GetHashCode());
        InstanceMember(nameof(CultureInfo.ToString)).Invoke(culture).ShouldBe(culture.ToString());
        InstanceMember(nameof(CultureInfo.ClearCachedData)).Invoke(culture).ShouldBeNull();

        DateTimeFormatInfo dateTimeFormat = new();
        InstanceMember("set_DateTimeFormat").Invoke(culture, [dateTimeFormat]).ShouldBeNull();
        culture.DateTimeFormat.ShouldBeSameAs(dateTimeFormat);

        NumberFormatInfo numberFormat = new();
        InstanceMember("set_NumberFormat").Invoke(culture, [numberFormat]).ShouldBeNull();
        culture.NumberFormat.ShouldBeSameAs(numberFormat);
    }
}
