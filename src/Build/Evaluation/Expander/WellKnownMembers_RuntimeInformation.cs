// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class RuntimeInformationHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length == 12 && name.Equals(nameof(RuntimeInformation.IsOSPlatform), StringComparison.OrdinalIgnoreCase)
                ? TryInvokeIsOSPlatform(ref arguments)
                : NotRecognized;

        internal WellKnownMemberResult TryGetStatic(StringSegment name)
            => name.Length == 14 && name.Equals(nameof(RuntimeInformation.OSArchitecture), StringComparison.OrdinalIgnoreCase)
                ? Handled(RuntimeInformation.OSArchitecture)
                : NotRecognized;

        private static WellKnownMemberResult TryInvokeIsOSPlatform(ref FunctionArguments arguments)
            => arguments.Count == 1 &&
               FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out OSPlatform osPlatform)
                ? Handled(RuntimeInformation.IsOSPlatform(osPlatform))
                : NotRecognized;
    }
}
