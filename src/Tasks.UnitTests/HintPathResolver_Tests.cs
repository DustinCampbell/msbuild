// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    public class HintPathResolver_Tests : IDisposable
    {
        private readonly TestEnvironment _env;

        public HintPathResolver_Tests(ITestOutputHelper testOutput)
        {
            _env = TestEnvironment.Create(testOutput);
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        [Fact]
        public void CanResolveHintPath()
        {
            var tempFile = _env.CreateFile("FakeSystem.Net.Http.dll", "");
            bool result = ResolveHintPath(tempFile.Path);

            result.ShouldBe(true);
        }

        [WindowsOnlyFact]
        public void CanResolveLongNonNormalizedHintPath()
        {
            var tempfolder = _env.DefaultTestDirectory.CreateDirectory("tempfolder_for_CanResolveLongHintPath");
            tempfolder.CreateFile("FakeSystem.Net.Http.dll");
            var longTempFilePath = tempfolder.Path + "\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\FakeSystem.Net.Http.dll";
            bool result = ResolveHintPath(longTempFilePath);

            result.ShouldBe(true);
        }

        [Fact]
        public void CanNotResolveHintPathWithNewLine()
        {
            var tempFile = _env.CreateFile("FakeSystem.Net.Http.dll", "");
            bool result = ResolveHintPath(Environment.NewLine + tempFile.Path + Environment.NewLine);

            result.ShouldBe(false);
        }

        [Fact]
        public void CanNotResolveHintPathWithSpace()
        {
            var tempFile = _env.CreateFile("FakeSystem.Net.Http.dll", "");
            bool result = ResolveHintPath("  " + tempFile.Path + "  ");

            result.ShouldBe(false);
        }

        private bool ResolveHintPath(string hintPath)
        {
            var services = new HintPathTestServices();
            var hintPathResolver = new HintPathResolver(
                searchPathElement: "{HintPathFromItem}",
                services: services,
                targetedRuntimeVesion: Version.Parse("4.0.30319"));

            var result = hintPathResolver.Resolve(new AssemblyNameExtension("FakeSystem.Net.Http"),
                sdkName: "",
                rawFileNameCandidate: "FakeSystem.Net.Http",
                isPrimaryProjectReference: true,
                isImmutableFrameworkReference: false,
                wantSpecificVersion: false,
                executableExtensions: new string[] { ".winmd", ".dll", ".exe" },
                hintPath: hintPath,
                assemblyFolderKey: "",
                assembliesConsideredAndRejected: new List<ResolutionSearchLocation>(),
                foundPath: out var findPath,
                userRequestedSpecificFile: out var userResquestedSpecificFile);
            return result;
        }

        /// <summary>
        /// Test services class that provides only FileExists functionality.
        /// Other methods are not called in this code path.
        /// </summary>
        private sealed class HintPathTestServices : RARFileSystemServices
        {
            public override bool FileExists(string path) => FileUtilities.FileExistsNoThrow(path);

            public override AssemblyNameExtension GetAssemblyName(string path) => throw new NotImplementedException("not called in this code path");

            public override string GetAssemblyRuntimeVersion(string path) => throw new NotImplementedException("not called in this code path");
        }
    }
}
