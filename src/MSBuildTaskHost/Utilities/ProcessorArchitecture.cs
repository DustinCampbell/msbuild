// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.TaskHost.Utilities;

/// <summary>
/// Processor architecture values.
/// </summary>
internal enum ProcessorArchitecture
{
    // Intel 32 bit
    X86,

    // AMD64 64 bit
    X64,

    // Itanium 64
    IA64,

    // ARM
    ARM,

    // ARM64
    ARM64,

    // WebAssembly
    WASM,

    // S390x
    S390X,

    // LongAarch64
    LOONGARCH64,

    // 32-bit ARMv6
    ARMV6,

    // PowerPC 64-bit (little-endian)
    PPC64LE,

    // Who knows
    Unknown
}
