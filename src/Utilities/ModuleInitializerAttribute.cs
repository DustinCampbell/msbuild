// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETSTANDARD
using System;

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ModuleInitializerAttribute : Attribute
{
}
#endif
