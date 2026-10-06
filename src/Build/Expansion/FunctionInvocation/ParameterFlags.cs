// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Expansion.FunctionInvocation;

/// <summary>
///  Describes parameter shapes that affect argument binding.
/// </summary>
[Flags]
internal enum ParameterFlags : byte
{
    /// <summary>
    ///  The parameter consumes one argument.
    /// </summary>
    None = 0,

    /// <summary>
    ///  The array parameter can collect trailing arguments as its element type.
    /// </summary>
    ParamArray = 1,
}
