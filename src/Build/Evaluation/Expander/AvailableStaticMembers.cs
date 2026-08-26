// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Defines and resolves the static members available to property-function evaluation.
/// </summary>
/// <remarks>
///  Do not allow members that could perform unsafe operations. Property functions run during project load,
///  which must remain safe in Visual Studio.
/// </remarks>
internal static partial class AvailableStaticMembers
{
    /// <summary>
    ///  Static members that are allowed in property functions.
    /// </summary>
    private static ConcurrentDictionary<MemberKey, TypeEntry> s_availableMembers = CreateAvailableMembers();

    private static ConcurrentDictionary<MemberKey, TypeEntry> AvailableMembers
        => Volatile.Read(ref s_availableMembers);

    /// <summary>
    ///  Determines whether a static member is available to property-function evaluation.
    /// </summary>
    /// <param name="receiverType">The type declaring the member.</param>
    /// <param name="memberName">The simple member name.</param>
    /// <returns>
    ///  <see langword="true"/> when the member is available; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsAvailable(Type receiverType, StringSegment memberName)
        => receiverType == typeof(IntrinsicFunctions)
        || FeatureSwitches.EnableAllPropertyFunctions
        || (receiverType.FullName is string typeName && ContainsEntry(typeName, memberName));

    /// <summary>
    ///  Attempts to resolve a static property-function receiver type.
    /// </summary>
    /// <param name="typeName">The full type name.</param>
    /// <param name="memberName">The simple member name.</param>
    /// <param name="receiverType">The resolved receiver type when this method returns <see langword="true"/>.</param>
    /// <returns>
    ///  <see langword="true"/> when the receiver type is resolved; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryResolveType(StringSegment typeName, StringSegment memberName, [NotNullWhen(true)] out Type? receiverType)
    {
        if (typeName.IsNullOrWhiteSpace())
        {
            receiverType = null;
            return false;
        }

        if (TryGetEntry(typeName, memberName, out TypeEntry? entry))
        {
            receiverType = entry.Resolve();
            return true;
        }

        // Type.GetType resolves core-library types without opening them up in the allowlist.
        string typeNameString = typeName.ValueOrEmpty;
        receiverType = ResolveTypeByName(typeNameString);
        if (receiverType is not null)
        {
            return true;
        }

        if (FeatureSwitches.EnableAllPropertyFunctions)
        {
            receiverType = GetTypeFromAssembly(typeNameString, "System")
                ?? GetTypeFromAssembly(typeNameString, "System.Core")
                ?? GetTypeFromAssemblyUsingNamespace(typeNameString);

            if (receiverType is not null)
            {
                AvailableMembers.TryAdd(new MemberKey(typeNameString), new TypeEntry(receiverType));
                return true;
            }
        }

        return false;
    }

    private static bool ContainsEntry(StringSegment typeName, StringSegment memberName)
        => AvailableMembers.ContainsKey(new MemberKey(typeName))
        || AvailableMembers.ContainsKey(new MemberKey(typeName, memberName));

    private static bool TryGetEntry(StringSegment typeName, StringSegment memberName, [NotNullWhen(true)] out TypeEntry? entry)
        => AvailableMembers.TryGetValue(new MemberKey(typeName), out entry)
        || AvailableMembers.TryGetValue(new MemberKey(typeName, memberName), out entry);

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2096:UnrecognizedReflectionPattern",
        Justification = "Property-function type resolution is constrained by the preserved allowlist and the subsequent static-member availability gate.")]
    private static Type? ResolveTypeByName(string typeName)
        => Type.GetType(typeName, throwOnError: false, ignoreCase: true);

    [RequiresUnreferencedCode("Resolves a property-function receiver type by probing and loading assemblies at runtime; reachable only via the MSBUILDENABLEALLPROPERTYFUNCTIONS feature switch, which is disabled under trimming.")]
    private static Type? GetTypeFromAssemblyUsingNamespace(string typeName)
    {
        string candidateAssemblyName = typeName;
        int assemblyNameEnd = candidateAssemblyName.Length;

        while (assemblyNameEnd > 0)
        {
            candidateAssemblyName = candidateAssemblyName.Substring(0, assemblyNameEnd);

            Type? resolvedType = GetTypeFromAssembly(typeName, candidateAssemblyName);
            if (resolvedType is not null)
            {
                return resolvedType;
            }

            assemblyNameEnd = candidateAssemblyName.LastIndexOf('.');
        }

        return null;
    }

    [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName", Justification = "Necessary since we don't have the full assembly name. ")]
    [RequiresUnreferencedCode("Resolves a property-function receiver type by loading an assembly by partial name at runtime; reachable only via the MSBUILDENABLEALLPROPERTYFUNCTIONS feature switch, which is disabled under trimming.")]
    private static Type? GetTypeFromAssembly(string typeName, string candidateAssemblyName)
    {
#if FEATURE_GAC
#pragma warning disable 618, 612
        // Assembly.Load requires a full assembly name, so retain the partial-name lookup on .NET Framework.
        Assembly? candidateAssembly = Assembly.LoadWithPartialName(candidateAssemblyName);
#pragma warning restore 618, 612
#else
        Assembly? candidateAssembly = null;
        try
        {
            candidateAssembly = Assembly.Load(new AssemblyName(candidateAssemblyName));
        }
        catch (FileNotFoundException)
        {
            // Match LoadWithPartialName by returning null when the assembly cannot be found.
        }
#endif

        return candidateAssembly?.GetType(typeName, throwOnError: false, ignoreCase: true);
    }

    /// <summary>
    ///  Recreates the allowlist after a unit test changes property-function feature switches.
    /// </summary>
    internal static void Reset_ForUnitTestsOnly()
        => Interlocked.Exchange(ref s_availableMembers, CreateAvailableMembers());

    /// <summary>
    ///  The public member surface used by property-function evaluation on allowlisted receiver types.
    /// </summary>
    /// <remarks>
    ///  Property functions invoke constructors, methods, properties, and fields, but never bind
    ///  <see cref="BindingFlags.NonPublic"/> members.
    /// </remarks>
    private const DynamicallyAccessedMemberTypes PropertyFunctionMembers =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    /// <summary>
    ///  Creates the initial property-function static-member allowlist.
    /// </summary>
    /// <returns>
    ///  The initialized allowlist.
    /// </returns>
    // Preserve the PropertyFunctionMembers set on every type in the property-function allowlist
    // below. Property functions dispatch over the allowlisted receiver type by reflection (see
    // PropertyFunctionExecutor), and receiver types are restricted to this allowlist unless the
    // MSBUILDENABLEALLPROPERTYFUNCTIONS feature switch is enabled. Preserving these members is what
    // makes the IL2072/IL2074/IL2080/IL2096 suppressions in Expander honest under trimming. Keep in
    // sync with the entries added below.
    [DynamicDependency(PropertyFunctionMembers, typeof(Environment))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Directory))]
    [DynamicDependency(PropertyFunctionMembers, typeof(File))]
    [DynamicDependency(PropertyFunctionMembers, typeof(RuntimeInformation))]
    [DynamicDependency(PropertyFunctionMembers, typeof(OSPlatform))]
    [DynamicDependency(PropertyFunctionMembers, typeof(CultureInfo))]
    [DynamicDependency(PropertyFunctionMembers, typeof(IntrinsicFunctions))]
    [DynamicDependency(PropertyFunctionMembers, typeof(byte))]
    [DynamicDependency(PropertyFunctionMembers, typeof(char))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Convert))]
    [DynamicDependency(PropertyFunctionMembers, typeof(DateTime))]
    [DynamicDependency(PropertyFunctionMembers, typeof(DateTimeOffset))]
    [DynamicDependency(PropertyFunctionMembers, typeof(decimal))]
    [DynamicDependency(PropertyFunctionMembers, typeof(double))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Enum))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Guid))]
    [DynamicDependency(PropertyFunctionMembers, typeof(short))]
    [DynamicDependency(PropertyFunctionMembers, typeof(int))]
    [DynamicDependency(PropertyFunctionMembers, typeof(long))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Path))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Math))]
    [DynamicDependency(PropertyFunctionMembers, typeof(ushort))]
    [DynamicDependency(PropertyFunctionMembers, typeof(uint))]
    [DynamicDependency(PropertyFunctionMembers, typeof(ulong))]
    [DynamicDependency(PropertyFunctionMembers, typeof(sbyte))]
    [DynamicDependency(PropertyFunctionMembers, typeof(float))]
    [DynamicDependency(PropertyFunctionMembers, typeof(string))]
    [DynamicDependency(PropertyFunctionMembers, typeof(StringComparer))]
    [DynamicDependency(PropertyFunctionMembers, typeof(TimeSpan))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Regex))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Uri))]
    [DynamicDependency(PropertyFunctionMembers, typeof(UriBuilder))]
    [DynamicDependency(PropertyFunctionMembers, typeof(Version))]
#if NET
    // ToolLocationHelper lives in Microsoft.Build.Utilities.Core in the SDK, which Microsoft.Build does not
    // directly reference, so it cannot be named with typeof here like the entries above. Root it with the
    // (memberTypes, typeName, assemblyName) string overload instead - it is otherwise an ordinary allowlist
    // entry (see the AddType call below). That overload (and trimming itself) exists only on .NET, so this
    // entry is guarded for the .NET build; the allowlist still includes the type at run time on .NET Framework.
    [DynamicDependency(PropertyFunctionMembers, "Microsoft.Build.Utilities.ToolLocationHelper", "Microsoft.Build.Utilities.Core")]
    [DynamicDependency(PropertyFunctionMembers, typeof(OperatingSystem))]
#endif

    // The DynamicDependency allowlist above preserves each property-function receiver type's public
    // surface so trimming keeps it. Across all of those types the only member carrying
    // [RequiresDynamicCode] is Enum.GetValues(Type) (rooted by typeof(Enum)) - this is the IL3050
    // suppressed below.
    //
    // It is rooted but unreachable from a property function: an author cannot invoke Enum.GetValues(Type)
    // (or any reflective Type-taking method) because there is no way to supply a System.Type argument.
    //   - string does not coerce to Type, so the overload never binds and evaluation reports MSB4186
    //     ("method not found ... parameters of the correct type").
    //   - [System.Type]::GetType(...) is not an available property function (MSB4185), and stays
    //     unavailable even with MSBUILDENABLEALLPROPERTYFUNCTIONS=1.
    // So the case is blocked before any reflective invoke, identically on JIT and AOT, and would still
    // fail observably (InvalidProjectFileException) if it were ever reached - never silently. This is
    // verified end to end under Native AOT by src/aot-validation/PropertyFunctionAotTests.cs.
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Enum.GetValues(Type) is rooted for the allowlist but is unreachable via property functions; see comment above.")]
    private static ConcurrentDictionary<MemberKey, TypeEntry> CreateAvailableMembers()
    {
        var map = new ConcurrentDictionary<MemberKey, TypeEntry>();

        // Make specific static members available.
        AddMembers(
            typeof(Environment),
            "ExpandEnvironmentVariables",
            "GetEnvironmentVariable",
            "GetEnvironmentVariables",
            "GetFolderPath",
            "GetLogicalDrives",

            // All the following properties only have getters.
            "CommandLine",
            "Is64BitOperatingSystem",
            "Is64BitProcess",
            "MachineName",
            "NewLine",
            "OSVersion",
            "ProcessorCount",
            "StackTrace",
            "SystemDirectory",
            "SystemPageSize",
            "TickCount",
            "UserDomainName",
            "UserInteractive",
            "UserName",
            "Version",
            "WorkingSet");

        AddMembers(
            typeof(Directory),
            "Exists",
            "GetDirectories",
            "GetFiles",
            "GetLastAccessTime",
            "GetLastWriteTime",
            "GetParent");

        AddMembers(
            typeof(File),
            "Exists",
            "GetCreationTime",
            "GetAttributes",
            "GetLastAccessTime",
            "GetLastWriteTime",
            "GetCreationTimeUtc",
            "GetLastWriteTimeUtc",
            "ReadAllText",
            "ReadAllBytes");

        AddMembers(
            typeof(CultureInfo),
            "GetCultureInfo",
            "new",
            "CurrentUICulture");

        // Make all public static members on the following types available.
        AddType(typeof(IntrinsicFunctions), "MSBuild");
        AddType(typeof(byte));
        AddType(typeof(char));
        AddType(typeof(Convert));
        AddType(typeof(DateTime));
        AddType(typeof(DateTimeOffset));
        AddType(typeof(decimal));
        AddType(typeof(double));
        AddType(typeof(Enum));
        AddType(typeof(Guid));
        AddType(typeof(short));
        AddType(typeof(int));
        AddType(typeof(long));
        AddType(typeof(Path));
        AddType(typeof(Math));
        AddType(typeof(ushort));
        AddType(typeof(uint));
        AddType(typeof(ulong));
        AddType(typeof(sbyte));
        AddType(typeof(float));
        AddType(typeof(string));
        AddType(typeof(StringComparer));
        AddType(typeof(TimeSpan));
        AddType(typeof(Regex));
        AddType(typeof(Uri));
        AddType(typeof(UriBuilder));
        AddType(typeof(Version));
        AddNamedType(
            "Microsoft.Build.Utilities.ToolLocationHelper",
            $"Microsoft.Build.Utilities.ToolLocationHelper, Microsoft.Build.Utilities.Core, Version={MSBuildConstants.CurrentAssemblyVersion}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        AddType(typeof(RuntimeInformation));
        AddType(typeof(OSPlatform));
#if NET
        AddType(typeof(OperatingSystem));
#else
        // Add alternate type for System.OperatingSystem static methods which aren't available on .NET Framework.
        const string operatingSystemTypeName = $"Microsoft.Build.Framework.OperatingSystem, Microsoft.Build.Framework, Version={MSBuildConstants.CurrentAssemblyVersion}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
        AddNamedType("System.OperatingSystem", operatingSystemTypeName);
        AddNamedType("Microsoft.Build.Framework.OperatingSystem", operatingSystemTypeName);
#endif

        return map;

        void AddType(Type type, string? typeName = null)
        {
            typeName ??= type.FullName;
            Assumed.NotNull(typeName);

            map.TryAdd(new MemberKey(typeName), new TypeEntry(type));
        }

        void AddMembers(Type type, params string[] memberNames)
        {
            string? typeName = type.FullName;
            Assumed.NotNull(typeName);

            var entry = new TypeEntry(type);
            foreach (string memberName in memberNames)
            {
                map.TryAdd(new MemberKey(typeName, memberName), entry);
            }
        }

        void AddNamedType(string typeName, string assemblyQualifiedName)
            => map.TryAdd(new MemberKey(typeName), new TypeEntry(assemblyQualifiedName));
    }
}
