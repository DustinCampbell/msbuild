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
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Internal;

/// <summary>
/// The set of available static methods.
/// NOTE: Do not allow methods here that could do "bad" things under any circumstances.
/// These must be completely benign operations, as they run during project load, which must be safe in VS.
/// Key = Type or Type::Method, Value = AssemblyQualifiedTypeName (where null = mscorlib)
/// </summary>
/// <remarks>
/// Placed here to avoid StyleCop error.
/// </remarks>
internal static class AvailableStaticMethods
{
    private sealed class CachedType
    {
        private object _typeOrName;

        public CachedType(Type type)
            => _typeOrName = type;

        public CachedType(string typeName)
            => _typeOrName = typeName;

        public Type ResolvedType
            => Volatile.Read(ref _typeOrName) switch
            {
                Type type => type,
                string typeName => ResolveType(typeName),

                _ => Assumed.Unreachable<Type>("Invalid type data."),
            };

        [UnconditionalSuppressMessage("Trimming", "IL2096:UnrecognizedReflectionPattern",
            Justification = "The type name is resolved against the curated AvailableStaticMethods allowlist; the case-insensitive lookup only resolves to allowlist types, whose members are preserved for trimming.")]
        private Type ResolveType(string typeName)
        {
            // Get the type from the assembly qualified type name from AvailableStaticMethods
            Type? resolvedType = Type.GetType(typeName, throwOnError: false, ignoreCase: true);

            // If the type information from the cache is not loadable, it means the cache information got corrupted somehow
            // Throw here to prevent adding null types in the cache
            Assumed.NotNull(resolvedType, $"Type information for {typeName} was present in the allowlist cache as {typeName} but the type could not be loaded.");

            Interlocked.CompareExchange(ref _typeOrName, resolvedType, null);

            return resolvedType;
        }
    }

    private readonly record struct Key(StringSegment TypeFullName, StringSegment MethodName)
    {
        public Key(StringSegment typeFullName)
            : this(typeFullName, default)
        {
        }

        public bool Equals(Key other)
            => TypeFullName.Equals(other.TypeFullName, StringComparison.OrdinalIgnoreCase)
            && MethodName.Equals(other.MethodName, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode()
            => TypeFullName.GetHashCode(StringComparison.OrdinalIgnoreCase) ^ MethodName.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Static methods that are allowed in constants. Key = Type or Type::Method, Value = Tuple of AssemblyQualifiedTypeName (where null = mscorlib) or the actual type object
    /// </summary>
    private static ConcurrentDictionary<Key, CachedType> s_availableStaticMethods = CreateDefaultMap();

    /// <summary>
    /// Add an entry if not already present
    /// </summary>
    internal static bool TryAdd(string typeFullName, Type type)
        => s_availableStaticMethods.TryAdd(new Key(typeFullName), new CachedType(type));

    /// <summary>
    /// Constructs the fully qualified method name and adds it to the cache
    /// </summary>
    /// <param name="typeFullName"></param>
    /// <param name="simpleMethodName"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool TryAdd(string typeFullName, string simpleMethodName, Type type)
        => s_availableStaticMethods.TryAdd(new Key(typeFullName, simpleMethodName), new CachedType(type));

    /// <summary>
    /// Get an entry if present
    /// </summary>
    internal static bool TryGetValue(string typeFuleName, [NotNullWhen(true)] out Type? value)
    {
        if (s_availableStaticMethods.TryGetValue(new Key(typeFuleName), out CachedType? typeData))
        {
            value = typeData.ResolvedType;
            return value is not null;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Check the property function allowlist whether this method is available.
    /// </summary>
    public static bool IsStaticMethodAvailable(Type receiverType, string methodName)
    {
        if (receiverType == typeof(IntrinsicFunctions))
        {
            // These are our intrinsic functions, so we're OK with those
            return true;
        }

        // The escape hatch opens everything. The feature switch also preserves the legacy
        // MSBUILDENABLEALLPROPERTYFUNCTIONS environment-variable behavior in untrimmed builds; under
        // trimming it is substituted false, so this wide gate is removed.
        if (FeatureSwitches.EnableAllPropertyFunctions)
        {
            // anything goes
            return true;
        }

        return (s_availableStaticMethods.TryGetValue(new Key(receiverType.FullName), out CachedType? typeData)
             || s_availableStaticMethods.TryGetValue(new Key(receiverType.FullName, methodName), out typeData))
             && typeData.ResolvedType != null;
    }

    /// <summary>
    /// Tries to retrieve the type information for a type name / method name combination.
    ///
    /// It does 2 lookups:
    /// 1st try: 'typeFullName'
    /// 2nd try: 'typeFullName::simpleMethodName'
    ///
    /// </summary>
    /// <param name="typeFullName">namespace qualified type name</param>
    /// <param name="simpleMethodName">name of the method</param>
    /// <returns></returns>
    internal static Type? GetType(string typeFullName, string simpleMethodName)
        => s_availableStaticMethods.TryGetValue(new Key(typeFullName), out CachedType? typeData)
        || s_availableStaticMethods.TryGetValue(new Key(typeFullName, simpleMethodName), out typeData)
            ? typeData.ResolvedType
            : null;

    /// <summary>
    /// Re-initialize.
    /// Unit tests need this when they enable "unsafe" methods -- which will then go in the collection,
    /// and mess up subsequent tests.
    /// </summary>
    internal static void Reset_ForUnitTestsOnly()
    {
        s_availableStaticMethods = CreateDefaultMap();
    }

    /// <summary>
    /// The reflection surface that property-function evaluation uses on an allowlisted receiver
    /// type: public constructors (for <c>[Type]::new(...)</c>) plus public methods, properties, and
    /// fields, reached as static or instance members via <c>Type.InvokeMember</c>, <c>GetMethod(s)</c>,
    /// and <c>GetConstructor(s)</c> (see Expander.Function.Execute and LateBindExecute). The
    /// property-function path never sets <c>BindingFlags.NonPublic</c>, so events, nested types,
    /// interfaces, and non-public members are never reflected over.
    /// </summary>
    private const DynamicallyAccessedMemberTypes PropertyFunctionMembers =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    /// <summary>
    /// Fill up the dictionary for first use
    /// </summary>
    // Preserve the PropertyFunctionMembers set on every type in the property-function allowlist
    // below. Property functions dispatch over the allowlisted receiver type by reflection (see
    // Expander.Function), and receiver types are restricted to this allowlist unless the
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
    // entry (see the TryAdd for it below). That overload (and trimming itself) exists only on .NET, so this
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
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Enum.GetValues(Type) is rooted for the allowlist but is unreachable via property functions; see comment above.")]
    private static ConcurrentDictionary<Key, CachedType> CreateDefaultMap()
    {
        var table = new ConcurrentDictionary<Key, CachedType>();

        var environmentType = new CachedType(typeof(Environment));
        var directoryType = new CachedType(typeof(Directory));
        var fileType = new CachedType(typeof(File));
        var runtimeInformationType = new CachedType(typeof(RuntimeInformation));
        var osPlatformType = new CachedType(typeof(OSPlatform));

        // Make specific static methods available (Assembly qualified type names are *NOT* supported, only null which means mscorlib):
        table.TryAdd(new Key("System.Environment", "ExpandEnvironmentVariables"), environmentType);
        table.TryAdd(new Key("System.Environment", "GetEnvironmentVariable"), environmentType);
        table.TryAdd(new Key("System.Environment", "GetEnvironmentVariables"), environmentType);
        table.TryAdd(new Key("System.Environment", "GetFolderPath"), environmentType);
        table.TryAdd(new Key("System.Environment", "GetLogicalDrives"), environmentType);

        // All the following properties only have getters
        table.TryAdd(new Key("System.Environment", "CommandLine"), environmentType);
        table.TryAdd(new Key("System.Environment", "Is64BitOperatingSystem"), environmentType);
        table.TryAdd(new Key("System.Environment", "Is64BitProcess"), environmentType);
        table.TryAdd(new Key("System.Environment", "MachineName"), environmentType);
        table.TryAdd(new Key("System.Environment", "NewLine"), environmentType);
        table.TryAdd(new Key("System.Environment", "OSVersion"), environmentType);
        table.TryAdd(new Key("System.Environment", "ProcessorCount"), environmentType);
        table.TryAdd(new Key("System.Environment", "StackTrace"), environmentType);
        table.TryAdd(new Key("System.Environment", "SystemDirectory"), environmentType);
        table.TryAdd(new Key("System.Environment", "SystemPageSize"), environmentType);
        table.TryAdd(new Key("System.Environment", "TickCount"), environmentType);
        table.TryAdd(new Key("System.Environment", "UserDomainName"), environmentType);
        table.TryAdd(new Key("System.Environment", "UserInteractive"), environmentType);
        table.TryAdd(new Key("System.Environment", "UserName"), environmentType);
        table.TryAdd(new Key("System.Environment", "Version"), environmentType);
        table.TryAdd(new Key("System.Environment", "WorkingSet"), environmentType);

        table.TryAdd(new Key("System.IO.Directory", "Exists"), directoryType);
        table.TryAdd(new Key("System.IO.Directory", "GetDirectories"), directoryType);
        table.TryAdd(new Key("System.IO.Directory", "GetFiles"), directoryType);
        table.TryAdd(new Key("System.IO.Directory", "GetLastAccessTime"), directoryType);
        table.TryAdd(new Key("System.IO.Directory", "GetLastWriteTime"), directoryType);
        table.TryAdd(new Key("System.IO.Directory", "GetParent"), directoryType);

        table.TryAdd(new Key("System.IO.File", "Exists"), fileType);
        table.TryAdd(new Key("System.IO.File", "GetCreationTime"), fileType);
        table.TryAdd(new Key("System.IO.File", "GetAttributes"), fileType);
        table.TryAdd(new Key("System.IO.File", "GetLastAccessTime"), fileType);
        table.TryAdd(new Key("System.IO.File", "GetLastWriteTime"), fileType);
        table.TryAdd(new Key("System.IO.File", "GetCreationTimeUtc"), fileType);
        table.TryAdd(new Key("System.IO.File", "GetLastWriteTimeUtc"), fileType);
        table.TryAdd(new Key("System.IO.File", "ReadAllText"), fileType);
        table.TryAdd(new Key("System.IO.File", "ReadAllBytes"), fileType);

        table.TryAdd(new Key("System.Globalization.CultureInfo", "GetCultureInfo"), new CachedType(typeof(CultureInfo))); // user request
        table.TryAdd(new Key("System.Globalization.CultureInfo", "new"), new CachedType(typeof(CultureInfo))); // user request
        table.TryAdd(new Key("System.Globalization.CultureInfo", "CurrentUICulture"), new CachedType(typeof(CultureInfo))); // user request

        // All static methods of the following are available (Assembly qualified type names are supported):
        table.TryAdd(new Key("MSBuild"), new CachedType(typeof(IntrinsicFunctions)));
        table.TryAdd(new Key("System.Byte"), new CachedType(typeof(byte)));
        table.TryAdd(new Key("System.Char"), new CachedType(typeof(char)));
        table.TryAdd(new Key("System.Convert"), new CachedType(typeof(Convert)));
        table.TryAdd(new Key("System.DateTime"), new CachedType(typeof(DateTime)));
        table.TryAdd(new Key("System.DateTimeOffset"), new CachedType(typeof(DateTimeOffset)));
        table.TryAdd(new Key("System.Decimal"), new CachedType(typeof(decimal)));
        table.TryAdd(new Key("System.Double"), new CachedType(typeof(double)));
        table.TryAdd(new Key("System.Enum"), new CachedType(typeof(Enum)));
        table.TryAdd(new Key("System.Guid"), new CachedType(typeof(Guid)));
        table.TryAdd(new Key("System.Int16"), new CachedType(typeof(short)));
        table.TryAdd(new Key("System.Int32"), new CachedType(typeof(int)));
        table.TryAdd(new Key("System.Int64"), new CachedType(typeof(long)));
        table.TryAdd(new Key("System.IO.Path"), new CachedType(typeof(Path)));
        table.TryAdd(new Key("System.Math"), new CachedType(typeof(Math)));
        table.TryAdd(new Key("System.UInt16"), new CachedType(typeof(ushort)));
        table.TryAdd(new Key("System.UInt32"), new CachedType(typeof(uint)));
        table.TryAdd(new Key("System.UInt64"), new CachedType(typeof(ulong)));
        table.TryAdd(new Key("System.SByte"), new CachedType(typeof(sbyte)));
        table.TryAdd(new Key("System.Single"), new CachedType(typeof(float)));
        table.TryAdd(new Key("System.String"), new CachedType(typeof(string)));
        table.TryAdd(new Key("System.StringComparer"), new CachedType(typeof(StringComparer)));
        table.TryAdd(new Key("System.TimeSpan"), new CachedType(typeof(TimeSpan)));
        table.TryAdd(new Key("System.Text.RegularExpressions.Regex"), new CachedType(typeof(Regex)));
        table.TryAdd(new Key("System.Uri"), new CachedType(typeof(Uri)));
        table.TryAdd(new Key("System.UriBuilder"), new CachedType(typeof(UriBuilder)));
        table.TryAdd(new Key("System.Version"), new CachedType(typeof(Version)));
        table.TryAdd(new Key("Microsoft.Build.Utilities.ToolLocationHelper"), new CachedType($"Microsoft.Build.Utilities.ToolLocationHelper, Microsoft.Build.Utilities.Core, Version={MSBuildConstants.CurrentAssemblyVersion}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
        table.TryAdd(new Key("System.Runtime.InteropServices.RuntimeInformation"), runtimeInformationType);
        table.TryAdd(new Key("System.Runtime.InteropServices.OSPlatform"), osPlatformType);
#if NET
        var operatingSystemType = new CachedType(typeof(OperatingSystem));
        table.TryAdd(new Key("System.OperatingSystem"), operatingSystemType);
#else
        // Add alternate type for System.OperatingSystem static methods which aren't available on .NET Framework.
        var operatingSystemType = new CachedType($"Microsoft.Build.Framework.OperatingSystem, Microsoft.Build.Framework, Version={MSBuildConstants.CurrentAssemblyVersion}, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
        table.TryAdd(new Key("System.OperatingSystem"), operatingSystemType);
        table.TryAdd(new Key("Microsoft.Build.Framework.OperatingSystem"), operatingSystemType);
#endif

        return table;
    }

    /// <summary>
    /// Given a type name and method name, try to resolve the type.
    /// </summary>
    /// <param name="typeName">May be full name or assembly qualified name.</param>
    /// <param name="simpleMethodName">simple name of the method.</param>
    /// <returns></returns>
    [UnconditionalSuppressMessage("Trimming", "IL2096:UnrecognizedReflectionPattern",
        Justification = "The type name is resolved against the curated AvailableStaticMethods allowlist; the case-insensitive lookup only resolves to allowlist types, whose members are preserved for trimming.")]
    public static Type? GetTypeForStaticMethod(string typeName, string simpleMethodName)
    {
        // If we don't have a type name, we already know that we won't be able to find a type.
        // Go ahead and return here -- otherwise the Type.GetType() calls below will throw.
        if (typeName.IsNullOrWhiteSpace())
        {
            return null;
        }

        // Check if the type is in the allowlist cache. If it is, use it or load it.
        if ((s_availableStaticMethods.TryGetValue(new Key(typeName), out CachedType? typeData) ||
            s_availableStaticMethods.TryGetValue(new Key(typeName, simpleMethodName), out typeData)) &&
            typeData.ResolvedType is Type resolvedType)
        {
            // If we've used it once, chances are that we'll be using it again
            // We can record the type here since we know it's available for calling from the fact that is was in the AvailableStaticMethods table
            TryAdd(typeName, simpleMethodName, resolvedType);
            return resolvedType;
        }

        // Get the type from mscorlib (or the currently running assembly)
        Type? result = Type.GetType(typeName, throwOnError: false, ignoreCase: true);

        if (result != null)
        {
            // DO NOT CACHE THE TYPE HERE!
            // We don't add the resolved type here in the AvailableStaticMethods table. This is because that table is used
            // during function parse, but only later during execution do we check for the ability to call specific methods on specific types.
            // Caching it here would load any type into the allow list.
            return result;
        }

        // The following reflective probing runs only when the EnableAllPropertyFunctions feature
        // switch is enabled (or, in untrimmed builds, the legacy MSBUILDENABLEALLPROPERTYFUNCTIONS
        // environment variable is set). That switch is a [FeatureGuard] for RequiresUnreferencedCode,
        // so the analyzer treats this branch as the trim-unsafe region (no suppression needed). In
        // trimmed / AOT applications the trimmer substitutes the switch false and removes this branch,
        // so only the curated allowlist of receiver types is supported.
        if (FeatureSwitches.EnableAllPropertyFunctions)
        {
            // We didn't find the type, so go probing.
            // Try System first, then System.Core.
            // If wasn't in either of those, trying using the namespace.
            result = GetTypeFromAssembly(typeName, "System")
                ?? GetTypeFromAssembly(typeName, "System.Core")
                ?? GetTypeFromAssemblyUsingNamespace(typeName);

            if (result != null)
            {
                // If we've used it once, chances are that we'll be using it again
                // We can cache the type here, since all functions are enabled
                TryAdd(typeName, result);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the specified type using the namespace to guess the assembly that its in.
    /// </summary>
    [RequiresUnreferencedCode("Resolves a property-function receiver type by probing and loading assemblies at runtime; reachable only via the MSBUILDENABLEALLPROPERTYFUNCTIONS feature switch, which is disabled under trimming.")]
    private static Type? GetTypeFromAssemblyUsingNamespace(string typeName)
    {
        string baseName = typeName;
        int assemblyNameEnd = baseName.Length;

        // If the string has no dot, or is nothing but a dot, we have no
        // namespace to look for, so we can't help.
        if (assemblyNameEnd <= 0)
        {
            return null;
        }

        // We will work our way up the namespace looking for an assembly that matches
        while (assemblyNameEnd > 0)
        {
            string candidateAssemblyName = baseName.Substring(0, assemblyNameEnd);

            // Try to load the assembly with the computed name
            Type? foundType = GetTypeFromAssembly(typeName, candidateAssemblyName);

            if (foundType != null)
            {
                // We have a match, so get the type from that assembly
                return foundType;
            }
            else
            {
                // Keep looking as we haven't found a match yet
                baseName = candidateAssemblyName;
                assemblyNameEnd = baseName.LastIndexOf('.');
            }
        }

        // We didn't find it, so we need to give up
        return null;
    }

    /// <summary>
    /// Get the specified type from the assembly partial name supplied.
    /// </summary>
    [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName", Justification = "Necessary since we don't have the full assembly name. ")]
    [RequiresUnreferencedCode("Resolves a property-function receiver type by loading an assembly by partial name at runtime; reachable only via the MSBUILDENABLEALLPROPERTYFUNCTIONS feature switch, which is disabled under trimming.")]
    private static Type? GetTypeFromAssembly(string typeName, string candidateAssemblyName)
    {
        Type? objectType = null;

        // Try to load the assembly with the computed name
#if FEATURE_GAC
#pragma warning disable 618, 612
        // Unfortunately Assembly.Load is not an alternative to LoadWithPartialName, since
        // Assembly.Load requires the full assembly name to be passed to it.
        // Therefore we must ignore the deprecated warning.
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
            // Swallow the error; LoadWithPartialName returned null when the partial name
            // was not found but Load throws.  Either way we'll provide a nice "couldn't
            // resolve this" error later.
        }
#endif

        if (candidateAssembly != null)
        {
            objectType = candidateAssembly.GetType(typeName, throwOnError: false, ignoreCase: true);
        }

        return objectType;
    }
}
