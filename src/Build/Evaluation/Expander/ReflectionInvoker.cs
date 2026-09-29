// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Binds and invokes property-function members through reflection.
/// </summary>
internal readonly struct ReflectionInvoker
{
    private const DynamicallyAccessedMemberTypes PublicMemberSurface =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicallyAccessedMembers(PublicMemberSurface)]
    private readonly Type _receiverType;

    private readonly string _methodName;
    private readonly BindingFlags _bindingFlags;

    public ReflectionInvoker(
        [DynamicallyAccessedMembers(PublicMemberSurface)] Type receiverType,
        string methodName,
        BindingFlags bindingFlags)
    {
        Assumed.Zero(
            (int)(bindingFlags & BindingFlags.NonPublic),
            $"Property-function binding flags '{bindingFlags}' must not include {BindingFlags.NonPublic}.");

        _receiverType = receiverType;
        _methodName = methodName;
        _bindingFlags = bindingFlags;
    }

    public object? InvokeConstructor(object?[] args)
        => LateBindConstructor(args);

    [UnconditionalSuppressMessage("Trimming", "IL2080:UnrecognizedReflectionPattern",
        Justification = "_bindingFlags is constrained to public members, and _receiverType preserves the complete public member surface.")]
    public object? InvokeMember(object? objectInstance, object?[] args)
    {
        try
        {
            // Out arguments require trying each candidate because their types are not present in the expression.
            if (ContainsOutArgument(args))
            {
                MethodInfo[] methods = _receiverType.GetMethods(_bindingFlags);
                return GetMethodResult(objectInstance, methods, args, 0);
            }

            return _receiverType.InvokePublicMember(_methodName, _bindingFlags, objectInstance, args);
        }
        catch (MissingMethodException ex) when ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
        {
            // The standard binder failed, so try the broader coercion rules used by property functions.
            return LateBindMethod(objectInstance, args, ex);
        }
    }

    private static bool ContainsOutArgument(object?[] args)
    {
        foreach (object? argument in args)
        {
            if ("out _".Equals(argument))
            {
                return true;
            }
        }

        return false;
    }

    private object? GetMethodResult(object? objectInstance, MethodInfo[] methods, object?[] args, int index)
    {
        for (int i = index; i < args.Length; i++)
        {
            if (args[i]!.Equals("out _"))
            {
                object? result = null;
                foreach (MethodInfo method in methods)
                {
                    if (!method.Name.Equals(_methodName))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != args.Length)
                    {
                        continue;
                    }

                    Type type = parameters[i].ParameterType;
                    args[i] = type.CreateDefault();
                    object? currentResult = GetMethodResult(objectInstance, methods, args, i + 1);
                    if (currentResult is not null)
                    {
                        if (result is null)
                        {
                            result = currentResult;
                        }
                        else if (!result.Equals(currentResult))
                        {
                            throw new ArgumentException(
                                ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
                                    "CouldNotDifferentiateBetweenCompatibleMethods", _methodName, args.Length));
                        }
                    }
                }

                return result;
            }
        }

        try
        {
            return _receiverType.InvokePublicMember(_methodName, _bindingFlags, objectInstance, args) ?? "null";
        }
        catch (Exception)
        {
            // This candidate is not viable, but another set of out-parameter types may work.
            return null;
        }
    }

    /// <summary>
    ///  Finds and invokes a public constructor, preferring an exact all-string signature before coercing arguments.
    /// </summary>
    /// <param name="args">The evaluated constructor arguments.</param>
    /// <returns>
    ///  The constructed object.
    /// </returns>
    private object LateBindConstructor(object?[] args)
    {
        ConstructorInfo[] constructors = _receiverType.GetConstructors();
        ConstructorInfo? constructor = null;

        // Prefer an exact signature in which every argument is a string.
        foreach (ConstructorInfo candidate in constructors)
        {
            if (HasStringParameters(candidate, args.Length))
            {
                constructor = candidate;
                break;
            }
        }

        if (constructor is null)
        {
            // Otherwise, inspect members with the correct arity and apply the property-function coercion rules.
            if (!TryBindWithCoercedArguments(constructors, filterByName: false, args, out constructor, out object?[]? coercedArgs))
            {
                throw LateBindException();
            }

            args = coercedArgs;
        }

        return constructor.Invoke(args) ?? throw LateBindException();

        static TargetInvocationException LateBindException()
            => new(new MissingMethodException());
    }

    /// <summary>
    ///  Late-binds and invokes a public method after the standard binder fails.
    /// </summary>
    /// <param name="objectInstance">
    ///  The method receiver, or <see langword="null"/> for a static method.
    /// </param>
    /// <param name="args">The evaluated method arguments.</param>
    /// <param name="ex">The exception raised by the standard binder.</param>
    /// <returns>
    ///  The method result, which may be <see langword="null"/>.
    /// </returns>
    /// <exception cref="MissingMethodException">No compatible method could be found.</exception>
    /// <remarks>
    ///  This reflective invocation can in principle reach any public method of an allowlisted receiver type.
    ///  The only such method marked with <c>RequiresDynamicCodeAttribute</c> is
    ///  <see cref="Enum.GetValues(Type)"/> on <see cref="Enum"/>.
    ///  <para>
    ///   Reaching that method would require an author to pass a <see cref="Type"/> argument, and a property
    ///   function has no way to produce one: <see cref="string"/> does not coerce to <see cref="Type"/>
    ///   (evaluation reports MSB4186, "method not found"), and <c>[System.Type]::GetType(...)</c> is not an
    ///   available property function (MSB4185, even with <c>MSBUILDENABLEALLPROPERTYFUNCTIONS=1</c>). The
    ///   receiver is a runtime <see cref="Type"/>, so the static <c>Enum.GetValues&lt;TEnum&gt;()</c> overload
    ///   cannot be substituted either. The case is therefore blocked before invocation, identically on JIT and
    ///   AOT, and would still fail observably with
    ///   <see cref="InvalidProjectFileException"/> if reached; it never fails
    ///   silently. This behavior is verified under Native AOT by
    ///   <c>src/aot-validation/PropertyFunctionAotTests.cs</c>.
    ///  </para>
    /// </remarks>
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "The only RDC method reachable here is Enum.GetValues(Type), which is unreachable via property functions; see comment above.")]
    [UnconditionalSuppressMessage("Trimming", "IL2080:UnrecognizedReflectionPattern",
        Justification = "_bindingFlags is constrained to public members, and _receiverType preserves the complete public member surface.")]
    private object? LateBindMethod(object? objectInstance, object?[] args, MissingMethodException ex)
    {
        MethodInfo[] methods = _receiverType.GetMethods(_bindingFlags);
        MethodInfo? method = null;

        // Prefer an exact signature in which every argument is a string.
        foreach (MethodInfo candidate in methods)
        {
            if (!_methodName.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (HasStringParameters(candidate, args.Length))
            {
                method = candidate;
                break;
            }
        }

        if (method is null)
        {
            // Otherwise, inspect members with the correct arity and apply the property-function coercion rules.
            if (_receiverType == typeof(IntrinsicFunctions) && IntrinsicFunctionOverload.IsKnownOverloadMethodName(_methodName))
            {
                Array.Sort(methods, IntrinsicFunctionOverload.IntrinsicFunctionOverloadMethodComparer);
            }

            if (!TryBindWithCoercedArguments(methods, filterByName: true, args, out method, out object?[]? coercedArgs))
            {
                throw ex;
            }

            args = coercedArgs;
        }

        return method.Invoke(objectInstance, args);
    }

    private static bool HasStringParameters(MethodBase method, int parameterCount)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length != parameterCount)
        {
            return false;
        }

        foreach (ParameterInfo parameter in parameters)
        {
            if (parameter.ParameterType != typeof(string))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryBindWithCoercedArguments<TMethod>(
        TMethod[] methods,
        bool filterByName,
        object?[] args,
        [NotNullWhen(true)] out TMethod? method,
        [NotNullWhen(true)] out object?[]? coercedArgs)
        where TMethod : MethodBase
    {
        foreach (TMethod candidate in methods)
        {
            if (filterByName && !_methodName.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryGetCoercedArguments(candidate, args, out coercedArgs))
            {
                method = candidate;
                return true;
            }
        }

        method = null;
        coercedArgs = null;
        return false;
    }

    private static bool TryGetCoercedArguments(MethodBase candidate, object?[] args, [NotNullWhen(true)] out object?[]? coercedArgs)
    {
        ParameterInfo[] parameters = candidate.GetParameters();
        if (parameters.Length != args.Length)
        {
            coercedArgs = null;
            return false;
        }

        if (args is [])
        {
            coercedArgs = [];
            return true;
        }

        try
        {
            object?[] result = new object?[args.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                object? argument = args[i];
                if (argument is null)
                {
                    continue;
                }

                result[i] = CoerceArgument(parameters[i], argument);
            }

            coercedArgs = result;
            return true;
        }
        catch (InvalidCastException)
        {
            coercedArgs = null;
            return false;
        }
        catch (FormatException)
        {
            coercedArgs = null;
            return false;
        }
        catch (OverflowException)
        {
            // https://github.com/dotnet/msbuild/issues/2882
            // Test: PropertyFunctionMathMaxOverflow
            coercedArgs = null;
            return false;
        }

        static object? CoerceArgument(ParameterInfo parameter, object argument)
        {
            if (parameter.ParameterType == typeof(char[]))
            {
                return argument.ToString()!.ToCharArray();
            }

            if (parameter.ParameterType.GetTypeInfo().IsEnum && argument is string s && s.IndexOf('.') >= 0)
            {
                Type enumType = parameter.ParameterType;
                string enumValue = ArgumentParser.NormalizeEnumArgument(enumType, s);
                return Enum.Parse(enumType, enumValue);
            }

            return Convert.ChangeType(argument, parameter.ParameterType, CultureInfo.InvariantCulture);
        }
    }
}
