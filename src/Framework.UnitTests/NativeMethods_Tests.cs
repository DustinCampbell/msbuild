// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Shouldly;
#if FEATURE_WINDOWSINTEROP
using Windows.Win32;
using Windows.Win32.Foundation;
#endif
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    /// <summary>
    ///  Tests for <see cref="NativeMethods"/>.
    /// </summary>
    public class NativeMethods_Tests
    {
        private delegate uint GetProcessIdDelegate();

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void QueryIsScreenAndTryEnableAnsiColorCodes_HonorsOverride(bool acceptAnsi, bool outputIsScreen)
        {
            try
            {
                NativeMethods.ConsoleConfigurationOverride = (acceptAnsi, outputIsScreen);

                (bool actualAnsi, bool actualScreen, uint? originalConsoleMode) =
                    NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();

                actualAnsi.ShouldBe(acceptAnsi);
                actualScreen.ShouldBe(outputIsScreen);

                // The node must not touch its own console mode when an override is present, so there is
                // nothing to restore.
                originalConsoleMode.ShouldBeNull();
            }
            finally
            {
                NativeMethods.ConsoleConfigurationOverride = null;
            }
        }

        [Fact]
        public void QueryIsScreenAndTryEnableAnsiColorCodes_OverrideAppliesToStandardError()
        {
            try
            {
                NativeMethods.ConsoleConfigurationOverride = (acceptAnsiColorCodes: true, outputIsScreen: true);

                (bool actualAnsi, bool actualScreen, uint? originalConsoleMode) =
                    NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes(useStandardError: true);

                actualAnsi.ShouldBeTrue();
                actualScreen.ShouldBeTrue();
                originalConsoleMode.ShouldBeNull();
            }
            finally
            {
                NativeMethods.ConsoleConfigurationOverride = null;
            }
        }

        /// <summary>
        /// Verify that GetProcAddress works. This previously failed due to incorrect P/Invoke attributes.
        /// </summary>
        [WindowsOnlyFact("No Kernel32.dll except on Windows.")]
        [SupportedOSPlatform("windows6.1")]
        public void TestGetProcAddress()
        {
            HMODULE kernel32Dll = PInvoke.LoadLibrary("kernel32.dll");
            try
            {
                kernel32Dll.IsNull.ShouldBeFalse();
                IntPtr processHandle = (IntPtr)PInvoke.GetProcAddress(kernel32Dll, "GetCurrentProcessId").Value;

                processHandle.ShouldNotBe(IntPtr.Zero);

                GetProcessIdDelegate processIdDelegate =
                    Marshal.GetDelegateForFunctionPointer<GetProcessIdDelegate>(processHandle);

                processIdDelegate().ShouldBe((uint)Process.GetCurrentProcess().Id);
            }
            finally
            {
                if (!kernel32Dll.IsNull)
                {
                    PInvoke.FreeLibrary(kernel32Dll);
                }
            }
        }

        [Fact]
        public void GetLastWriteFileUtcTimeReturnsMinValueForMissingFile()
        {
            string nonexistentFile = FileUtilities.GetTemporaryFileName();

            NativeMethods.GetLastWriteFileUtcTime(nonexistentFile).ShouldBe(DateTime.MinValue);
        }

        [Fact]
        public void GetLastWriteFileUtcTimeReturnsMinValueForDirectory()
        {
            string directory = FileUtilities.GetTemporaryDirectory(createDirectory: true);

            NativeMethods.GetLastWriteFileUtcTime(directory).ShouldBe(DateTime.MinValue);
        }

        [Fact]
        public void GetLastWriteDirectoryUtcTimeReturnsMinValueForFile()
        {
            string file = FileUtilities.GetTemporaryFile();

            NativeMethods.GetLastWriteDirectoryUtcTime(file, out DateTime directoryTime).ShouldBeFalse();
            directoryTime.ShouldBe(DateTime.MinValue);
        }

        [Fact]
        public void SetCurrentDirectoryDoesNotSetNonexistentFolder()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string nonexistentDirectory = Path.Combine(currentDirectory, "foo", "bar", "baz");

            if (Directory.Exists(nonexistentDirectory))
            {
                for (int i = 0; i < 10; i++)
                {
                    nonexistentDirectory = $"{Path.Combine(currentDirectory, "foo", "bar", "baz")}{Guid.NewGuid()}";

                    if (!Directory.Exists(nonexistentDirectory))
                    {
                        break;
                    }
                }
            }

            Directory.Exists(nonexistentDirectory).ShouldBeFalse(
                "Tried 10 times to get a nonexistent directory name and failed -- please try again");

            Should.NotThrow(() => NativeMethods.SetCurrentDirectory(nonexistentDirectory));
            Directory.GetCurrentDirectory().ShouldBe(currentDirectory);
        }
    }
}
