// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation.Expander;

internal static class PropertyFunctionInvoker
{
    private const DynamicallyAccessedMemberTypes PublicMemberSurface =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields;

    public static object? InvokeConstructor(
        [DynamicallyAccessedMembers(PublicMemberSurface)] Type receiverType,
        object?[] args)
        => LateBind(
            receiverType,
            memberName: null,
            previousException: null,
            BindingFlags.Public | BindingFlags.Instance,
            receiver: null,
            args,
            isConstructor: true);

    public static object? InvokeMember(
        [DynamicallyAccessedMembers(PublicMemberSurface)] Type receiverType,
        string memberName,
        BindingFlags bindingFlags,
        object? receiver,
        object?[] args)
    {
        Assumed.Zero(
            (int)(bindingFlags & BindingFlags.NonPublic),
            $"'{BindingFlags.NonPublic}' is not permitted for {nameof(InvokeMember)}; only public members may be bound.");

        using RefArrayBuilder<int> outArgIndices = new(stackalloc int[4]);
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "out _")
            {
                outArgIndices.Add(i);
            }
        }

        if (!outArgIndices.IsEmpty)
        {
            return TryBindAndInvokeMethodWithOutArguments(
                receiverType,
                memberName,
                bindingFlags,
                receiver,
                args,
                outArgIndices.AsSpan(),
                out object? result)
                ? result
                : null;
        }

        try
        {
            return receiverType.InvokePublicMember(memberName, bindingFlags, receiver, args);
        }
        catch (MissingMethodException ex) when ((bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
        {
            return LateBind(receiverType, memberName, ex, bindingFlags, receiver, args, isConstructor: false);
        }
    }

    /// <summary>
    ///  Attempts to bind and invoke a method call containing discarded <c>out</c> arguments.
    /// </summary>
    /// <remarks>
    ///  Candidate discovery and binding do not execute user code. Only the method selected by the default binder
    ///  is invoked.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "InvokeMember rejects BindingFlags.NonPublic before reaching this private helper; receiverType preserves the public property-function member surface.")]
    private static bool TryBindAndInvokeMethodWithOutArguments(
        [DynamicallyAccessedMembers(PublicMemberSurface)] Type receiverType,
        string memberName,
        BindingFlags bindingFlags,
        object? receiver,
        object?[] args,
        ReadOnlySpan<int> outArgIndices,
        out object? result)
    {
        result = null;

        MethodInfo[] candidates = receiverType.GetMethods(bindingFlags);
        int candidateCount = 0;

        // Compact compatible declarations in place so the binder cannot consider another member name,
        // arity, or a normal parameter at a discarded-out position.
        for (int i = 0; i < candidates.Length; i++)
        {
            MethodInfo candidate = candidates[i];
            if (!memberName.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length == args.Length && HasOutParameters(parameters, outArgIndices))
            {
                candidates[candidateCount++] = candidate;
            }
        }

        if (candidateCount == 0)
        {
            return false;
        }

        Array.Resize(ref candidates, candidateCount);

        // BindToMethod may mutate or replace its argument array. Clone it so the original materialized values
        // remain available for diagnostics.
        object?[] boundArgs = (object?[])args.Clone();
        foreach (int index in outArgIndices)
        {
            // An out argument has no input value. Null leaves its type unconstrained so the binder can use the
            // candidate parameter metadata and the remaining arguments to select an overload.
            boundArgs[index] = null;
        }

        MethodBase? method;
        try
        {
            method = Type.DefaultBinder.BindToMethod(
                bindingFlags,
                candidates,
                ref boundArgs,
                modifiers: null,
                culture: CultureInfo.InvariantCulture,
                names: null,
                out _);
        }
        catch (MissingMethodException)
        {
            return false;
        }
        catch (AmbiguousMatchException)
        {
            return false;
        }

        if (method is null)
        {
            return false;
        }

        // Invoke exactly once after binding; method-body exceptions must propagate to the normal error path.
        result = method.Invoke(receiver, boundArgs);
        return true;
    }

    private static bool HasOutParameters(ParameterInfo[] parameters, ReadOnlySpan<int> outArgIndices)
    {
        foreach (int index in outArgIndices)
        {
            if (!parameters[index].IsOut)
            {
                return false;
            }
        }

        return true;
    }

    private static object?[]? CoerceArguments(object?[] args, ParameterInfo[] parameters)
    {
        object?[] coercedArguments = new object?[args.Length];

        try
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] is null)
                {
                    continue;
                }

                if (parameters[i].ParameterType == typeof(char[]))
                {
                    coercedArguments[i] = args[i]!.ToString()!.ToCharArray();
                }
                else if (parameters[i].ParameterType.GetTypeInfo().IsEnum &&
                         args[i] is string value &&
                         value.IndexOf('.') >= 0)
                {
                    Type enumType = parameters[i].ParameterType;
                    string typeLeafName = $"{enumType.Name}.";
                    string typeFullName = $"{enumType.FullName}.";
                    string argument = value.Replace('|', ',').Replace(typeFullName, "").Replace(typeLeafName, "");
                    coercedArguments[i] = Enum.Parse(enumType, argument);
                }
                else
                {
                    coercedArguments[i] = Convert.ChangeType(args[i], parameters[i].ParameterType, CultureInfo.InvariantCulture);
                }
            }
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }

        return coercedArguments;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "InvokeMember rejects BindingFlags.NonPublic before reaching this private helper; receiverType preserves the public property-function member surface.")]
    private static MethodInfo? FindPublicMethodBySignature(
        [DynamicallyAccessedMembers(PublicMemberSurface)] Type receiverType,
        string memberName,
        BindingFlags bindingFlags,
        Type[] parameterTypes)
    {
        foreach (MethodInfo method in receiverType.GetMethods(bindingFlags))
        {
            if (!string.Equals(method.Name, memberName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != parameterTypes.Length)
            {
                continue;
            }

            bool match = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != parameterTypes[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return method;
            }
        }

        return null;
    }

    // This reflective invoke can in principle reach any public method of an allowlisted receiver type.
    // The only such method carrying [RequiresDynamicCode] is Enum.GetValues(Type) (on System.Enum).
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "The only RDC method reachable here is Enum.GetValues(Type), which is unreachable via property functions.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:UnrecognizedReflectionPattern",
        Justification = "InvokeMember rejects BindingFlags.NonPublic before reaching this private helper; constructor binding supplies public-only flags; receiverType preserves the public property-function member surface.")]
    private static object? LateBind(
        [DynamicallyAccessedMembers(PublicMemberSurface)] Type receiverType,
        string? memberName,
        MissingMethodException? previousException,
        BindingFlags bindingFlags,
        object? receiver,
        object?[] args,
        bool isConstructor)
    {
        Type[] types = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            types[i] = typeof(string);
        }

        MethodBase? memberInfo;
        string resolvedMemberName;
        if (isConstructor)
        {
            resolvedMemberName = string.Empty;
            memberInfo = receiverType.GetConstructor(types);
        }
        else
        {
            Assumed.NotNull(memberName);
            resolvedMemberName = memberName;
            memberInfo = FindPublicMethodBySignature(receiverType, resolvedMemberName, bindingFlags, types);
        }

        if (memberInfo is null)
        {
            MemberInfo[] members;
            bool filterByName;

            if (isConstructor)
            {
                members = receiverType.GetConstructors();
                filterByName = false;
            }
            else if (receiverType == typeof(IntrinsicFunctions) &&
                     IntrinsicFunctionOverload.IsKnownOverloadMethodName(resolvedMemberName))
            {
                MemberInfo[] foundMembers = typeof(IntrinsicFunctions).FindMembers(
                    MemberTypes.Method,
                    bindingFlags,
                    (info, criteria) => string.Equals(info.Name, (string?)criteria, StringComparison.OrdinalIgnoreCase),
                    resolvedMemberName);
                Array.Sort(foundMembers, IntrinsicFunctionOverload.IntrinsicFunctionOverloadMethodComparer);
                members = foundMembers;
                filterByName = false;
            }
            else
            {
                members = receiverType.GetMethods(bindingFlags);
                filterByName = true;
            }

            foreach (MemberInfo candidate in members)
            {
                if (filterByName && !string.Equals(candidate.Name, resolvedMemberName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                MethodBase member = (MethodBase)candidate;
                ParameterInfo[] parameters = member.GetParameters();
                if (parameters.Length == args.Length)
                {
                    object?[]? coercedArguments = CoerceArguments(args, parameters);
                    if (coercedArguments is not null)
                    {
                        memberInfo = member;
                        args = coercedArguments;
                        break;
                    }
                }
            }
        }

        object? result = null;
        if (memberInfo is not null)
        {
            result = isConstructor
                ? ((ConstructorInfo)memberInfo).Invoke(args)
                : ((MethodInfo)memberInfo).Invoke(receiver, args);
        }
        else if (!isConstructor)
        {
            Assumed.NotNull(previousException);
            throw previousException;
        }

        if (result is null && isConstructor)
        {
            throw new TargetInvocationException(new MissingMethodException());
        }

        return result;
    }
}
