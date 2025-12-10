// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Build.Evaluation;

internal static class IntrinsicFunctionOverload
{
    private static readonly FrozenSet<string> s_knownOverloadNames = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase, "Add", "Subtract", "Multiply", "Divide", "Modulo");

    internal static IComparer<MemberInfo> IntrinsicFunctionOverloadMethodComparer
        => LongBeforeDoubleComparer;

    // Order by the TypeCode of the first parameter.
    // When change wave is enabled, order long before double.
    // Otherwise preserve prior behavior of double before long.
    // For reuse, the comparer is cached in a non-generic type.
    // Both comparer instances can be cached to support change wave testing.
    private static IComparer<MemberInfo> LongBeforeDoubleComparer
        => field ??= Comparer<MemberInfo>.Create((member1, member2) =>
        {
            return SelectTypeOfFirstParameter(member1).CompareTo(SelectTypeOfFirstParameter(member2));
        });

    internal static bool IsKnownOverloadMethodName(string methodName)
        => s_knownOverloadNames.Contains(methodName);

    private static TypeCode SelectTypeOfFirstParameter(MemberInfo member)
    {
        if (member is not MethodBase method)
        {
            return TypeCode.Empty;
        }

        return method.GetParameters() is [var parameter, ..]
            ? Type.GetTypeCode(parameter.ParameterType)
            : TypeCode.Empty;
    }
}
