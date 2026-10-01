// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class PropertyParser_Tests
    {
        [Fact]
        public void GetTable1()
        {
            PropertyParser.GetTable(null, "Properties", null, out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have null table.
            propertiesTable.ShouldBeNull();
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable3()
        {
            PropertyParser.GetTable(null, "Properties",
                ["Configuration=Debug"], out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug

            propertiesTable.Count.ShouldBe(1);
            propertiesTable["Configuration"].ShouldBe("Debug");
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable4()
        {
            PropertyParser.GetTable(
                null,
                "Properties",
                ["Configuration=Debug", "Platform=AnyCPU", "VBL=Lab22Dev"],
                out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU
            //      VBL                 Lab22Dev

            propertiesTable.Count.ShouldBe(3);
            propertiesTable["Configuration"].ShouldBe("Debug");
            propertiesTable["Platform"].ShouldBe("AnyCPU");
            propertiesTable["VBL"].ShouldBe("Lab22Dev");
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable5()
        {
            PropertyParser.GetTable(
                null,
                "Properties",
                ["Configuration = Debug", "Platform \t=       AnyCPU"],
                out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU

            propertiesTable.Count.ShouldBe(2);
            propertiesTable["Configuration"].ShouldBe("Debug");
            propertiesTable["Platform"].ShouldBe("AnyCPU");
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable6()
        {
            PropertyParser.GetTable(null, "Properties",
                ["Configuration=", "Platform =  "], out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       <blank>
            //      Platform            <blank>

            propertiesTable.Count.ShouldBe(2);
            propertiesTable["Configuration"].ShouldBe(string.Empty);
            propertiesTable["Platform"].ShouldBe(string.Empty);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable7()
        {
            // This is a failure case.
            PropertyParser.GetTable(null, "Properties", ["=Debug"], out _).ShouldBeFalse();
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable8()
        {
            // This is a failure case.  (Second property "x86" doesn't have a value.)
            PropertyParser.GetTable(null, "Properties",
                ["Configuration=Debug", "x86"], out _).ShouldBeFalse();
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable9()
        {
            PropertyParser.GetTable(null, "Properties",
                ["DependsOn = Clean; Build"], out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          Clean; Build

            propertiesTable.Count.ShouldBe(1);
            propertiesTable["DependsOn"].ShouldBe("Clean; Build");
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable10()
        {
            PropertyParser.GetTable(null, "Properties",
                ["Depends On = CleanBuild"], out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          CleanBuild

            propertiesTable.Count.ShouldBe(1);
            propertiesTable["Depends On"].ShouldBe("CleanBuild");
        }

        [Fact]
        public void GetPropertiesTableWithEscaping1()
        {
            PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                ["Configuration = Debug", "Platform = Any CPU"], out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            Any CPU

            propertiesTable.Count.ShouldBe(2);
            propertiesTable["Configuration"].ShouldBe("Debug");
            propertiesTable["Platform"].ShouldBe("Any CPU");
        }

        [Fact]
        public void GetPropertiesTableWithEscaping2()
        {
            PropertyParser.GetTableWithEscaping(
                null,
                "Properties",
                "Properties",
                ["WarningsAsErrors = 1234", "5678", "9999", "Configuration=Debug"],
                out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      WarningsAsErrors    1234;5678;9999
            //      Configuration       Debug

            propertiesTable.Count.ShouldBe(2);
            propertiesTable["WarningsAsErrors"].ShouldBe("1234;5678;9999");
            propertiesTable["Configuration"].ShouldBe("Debug");
        }

        [Fact]
        public void GetPropertiesTableWithEscaping3()
        {
            PropertyParser.GetTableWithEscaping(
                null,
                "Properties",
                "Properties",
                [@"OutDir=c:\Rajeev;s Stuff\binaries", "Configuration=Debug"],
                out Dictionary<string, string> propertiesTable).ShouldBeTrue();

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      OutDir              c:\Rajeev%3bs Stuff\binaries
            //      Configuration       Debug

            propertiesTable.Count.ShouldBe(2);
            propertiesTable["OutDir"].ShouldBe(@"c:\Rajeev%3bs Stuff\binaries");
            propertiesTable["Configuration"].ShouldBe("Debug");
        }
    }
}
