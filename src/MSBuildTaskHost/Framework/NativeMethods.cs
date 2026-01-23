// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

#nullable disable

namespace Microsoft.Build.Framework;

internal static partial class NativeMethods
{
    /// <summary>
    /// Default buffer size to use when dealing with the Windows API.
    /// </summary>
    internal const int MaxPath = 260;

    public static int GetLogicalCoreCount()
    {
        int result = GetLogicalCoreCountOnWindows();

        return result != -1 ? result : Environment.ProcessorCount;
    }

    /// <summary>
    /// Get the exact physical core count on Windows
    /// Useful for getting the exact core count in 32 bits processes,
    /// as Environment.ProcessorCount has a 32-core limit in that case.
    /// https://github.com/dotnet/runtime/blob/221ad5b728f93489655df290c1ea52956ad8f51c/src/libraries/System.Runtime.Extensions/src/System/Environment.Windows.cs#L171-L210.
    /// </summary>
    private static unsafe int GetLogicalCoreCountOnWindows()
    {
        const int ERROR_INSUFFICIENT_BUFFER = 122;

        uint length = 0;

        if (GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, null, ref length) ||
            Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
        {
            return -1;
        }

        // Allocate that much space
        byte* buffer = stackalloc byte[(int)length];

        // Call GetLogicalProcessorInformationEx with the allocated buffer
        if (GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, buffer, ref length))
        {
            // Walk each SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX in the buffer, where the Size of each dictates how
            // much space it's consuming.  For each group relation, count the number of active processors in each of its group infos.
            int processorCount = 0;
            byte* ptr = buffer;
            byte* endPtr = buffer + length;

            while (ptr < endPtr)
            {
                var current = (SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX*)ptr;

                if (current->Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                {
                    // Flags is 0 if the core has a single logical proc, LTP_PC_SMT if more than one
                    // for now, assume "more than 1" == 2, as it has historically been for hyperthreading
                    processorCount += (current->Processor.Flags == 0) ? 1 : 2;
                }

                ptr += current->Size;
            }

            return processorCount;
        }

        return -1;
    }

    private enum LOGICAL_PROCESSOR_RELATIONSHIP
    {
        RelationProcessorCore,
        RelationNumaNode,
        RelationCache,
        RelationProcessorPackage,
        RelationGroup,
        RelationAll = 0xffff
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
    {
        public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
        public uint Size;
        public PROCESSOR_RELATIONSHIP Processor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct PROCESSOR_RELATIONSHIP
    {
        public byte Flags;
        private byte EfficiencyClass;
        private fixed byte Reserved[20];
        public ushort GroupCount;
        public IntPtr GroupInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public uint ExitStatus;
        public IntPtr PebBaseAddress;
        public UIntPtr AffinityMask;
        public int BasePriority;
        public UIntPtr UniqueProcessId;
        public UIntPtr InheritedFromUniqueProcessId;

        public readonly uint Size
        {
            get
            {
                unsafe
                {
                    return (uint)sizeof(PROCESS_BASIC_INFORMATION);
                }
            }
        }
    };

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool GetLogicalProcessorInformationEx(
        LOGICAL_PROCESSOR_RELATIONSHIP RelationshipType,
        byte* Buffer,
        ref uint ReturnedLength);

    private static ProcessorArchitecture? s_processArchitecture;

    public static ProcessorArchitecture ProcessorArchitecture
    {
        get
        {
            return s_processArchitecture ??= ComputeProcessorArchitecture();

            static ProcessorArchitecture ComputeProcessorArchitecture()
            {
                // As defined in winnt.h:
                const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
                const ushort PROCESSOR_ARCHITECTURE_ARM = 5;
                const ushort PROCESSOR_ARCHITECTURE_IA64 = 6;
                const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;
                const ushort PROCESSOR_ARCHITECTURE_ARM64 = 12;

                GetSystemInfo(out SYSTEM_INFO systemInfo);

                return systemInfo.wProcessorArchitecture switch
                {
                    PROCESSOR_ARCHITECTURE_INTEL => ProcessorArchitecture.X86,
                    PROCESSOR_ARCHITECTURE_AMD64 => ProcessorArchitecture.X64,
                    PROCESSOR_ARCHITECTURE_ARM => ProcessorArchitecture.ARM,
                    PROCESSOR_ARCHITECTURE_IA64 => ProcessorArchitecture.IA64,
                    PROCESSOR_ARCHITECTURE_ARM64 => ProcessorArchitecture.ARM64,

                    _ => ProcessorArchitecture.Unknown,
                };
            }
        }
    }

    /// <summary>
    /// Structure that contain information about the system on which we are running
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        // This is a union of a DWORD and a struct containing 2 WORDs.
        internal ushort wProcessorArchitecture;
        internal ushort wReserved;

        internal uint dwPageSize;
        internal IntPtr lpMinimumApplicationAddress;
        internal IntPtr lpMaximumApplicationAddress;
        internal IntPtr dwActiveProcessorMask;
        internal uint dwNumberOfProcessors;
        internal uint dwProcessorType;
        internal uint dwAllocationGranularity;
        internal ushort wProcessorLevel;
        internal ushort wProcessorRevision;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    /// <summary>
    /// Given an error code, converts it to an HRESULT and throws the appropriate exception.
    /// </summary>
    /// <param name="errorCode"></param>
    private static void ThrowExceptionForErrorCode(int errorCode)
    {
        // See ndp\clr\src\bcl\system\io\__error.cs for this code as it appears in the CLR.

        // Something really bad went wrong with the call
        // translate the error into an exception

        // Convert the errorcode into an HRESULT (See MakeHRFromErrorCode in Win32Native.cs in
        // ndp\clr\src\bcl\microsoft\win32)
        errorCode = unchecked(((int)0x80070000) | errorCode);

        // Throw an exception as best we can
        Marshal.ThrowExceptionForHR(errorCode);
    }

    /// <summary>
    /// Internal, optimized GetCurrentDirectory implementation that simply delegates to the native method
    /// </summary>
    /// <returns></returns>
    public static unsafe string GetCurrentDirectory()
    {
        // Directory.GetCurrentDirectory on .NET 3.5 is slow and creates strings in its work.
        int bufferSize = GetCurrentDirectoryWin32(0, null);
        char* buffer = stackalloc char[bufferSize];
        int pathLength = GetCurrentDirectoryWin32(bufferSize, buffer);

        return new string(buffer, startIndex: 0, length: pathLength);
    }

    private static unsafe int GetCurrentDirectoryWin32(int nBufferLength, char* lpBuffer)
    {
        int pathLength = GetCurrentDirectory(nBufferLength, lpBuffer);
        VerifyThrowWin32Result(pathLength);
        return pathLength;
    }

    public static unsafe string GetFullPath(string path)
    {
        char* buffer = stackalloc char[MaxPath];
        int fullPathLength = GetFullPathWin32(path, MaxPath, buffer, IntPtr.Zero);

        // if user is using long paths we could need to allocate a larger buffer
        if (fullPathLength > MaxPath)
        {
            char* newBuffer = stackalloc char[fullPathLength];
            fullPathLength = GetFullPathWin32(path, fullPathLength, newBuffer, IntPtr.Zero);

            buffer = newBuffer;
        }

        // Avoid creating new strings unnecessarily
        return AreStringsEqual(buffer, fullPathLength, path) ? path : new string(buffer, startIndex: 0, length: fullPathLength);
    }

    private static unsafe int GetFullPathWin32(string target, int bufferLength, char* buffer, IntPtr mustBeZero)
    {
        int pathLength = GetFullPathName(target, bufferLength, buffer, mustBeZero);
        VerifyThrowWin32Result(pathLength);
        return pathLength;
    }

    /// <summary>
    /// Compare an unsafe char buffer with a <see cref="System.String"/> to see if their contents are identical.
    /// </summary>
    /// <param name="buffer">The beginning of the char buffer.</param>
    /// <param name="len">The length of the buffer.</param>
    /// <param name="s">The string.</param>
    /// <returns>True only if the contents of <paramref name="s"/> and the first <paramref name="len"/> characters in <paramref name="buffer"/> are identical.</returns>
    private static unsafe bool AreStringsEqual(char* buffer, int len, string s)
    {
        if (len != s.Length)
        {
            return false;
        }

        foreach (char ch in s)
        {
            if (ch != *buffer++)
            {
                return false;
            }
        }

        return true;
    }

    private static void VerifyThrowWin32Result(int result)
    {
        bool isError = result == 0;
        if (isError)
        {
            int code = Marshal.GetLastWin32Error();
            ThrowExceptionForErrorCode(code);
        }
    }

    public static bool SetCurrentDirectory(string path)
        => SetCurrentDirectoryWindows(path);

    [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "Using unmanaged equivalent for performance reasons")]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern unsafe int GetCurrentDirectory(int nBufferLength, char* lpBuffer);

    [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "Using unmanaged equivalent for performance reasons")]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetCurrentDirectory")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCurrentDirectoryWindows(string path);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern unsafe int GetFullPathName(string target, int bufferLength, char* buffer, IntPtr mustBeZero);

    public static bool DirectoryExists(string fullPath)
        => GetFileAttributesEx(fullPath, out WIN32_FILE_ATTRIBUTE_DATA data)
        && (data.fileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;

    public static bool FileExists(string fullPath)
        => GetFileAttributesEx(fullPath, out WIN32_FILE_ATTRIBUTE_DATA data)
        && (data.fileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0;

    public static bool FileOrDirectoryExists(string path)
        => GetFileAttributesEx(path, out _);

    private const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    /// <summary>
    /// Contains information about a file or directory; used by GetFileAttributesEx.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct WIN32_FILE_ATTRIBUTE_DATA
    {
        internal int fileAttributes;
        internal uint ftCreationTimeLow;
        internal uint ftCreationTimeHigh;
        internal uint ftLastAccessTimeLow;
        internal uint ftLastAccessTimeHigh;
        internal uint ftLastWriteTimeLow;
        internal uint ftLastWriteTimeHigh;
        internal uint fileSizeHigh;
        internal uint fileSizeLow;
    }

    private static bool GetFileAttributesEx(
        string lpFileName,
        out WIN32_FILE_ATTRIBUTE_DATA lpFileInformation)
        => GetFileAttributesEx(lpFileName, fInfoLevelId: 0, out lpFileInformation);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileAttributesEx(
        string lpFileName,
        int fInfoLevelId,
        out WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);
}
