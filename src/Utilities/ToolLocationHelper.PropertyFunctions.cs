// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Build.Framework;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using ReflectionProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
#pragma warning disable CA2255 // The module initializer registers this assembly's reflection-free property-function handler.
    [ModuleInitializer]
    internal static void RegisterPropertyFunctionHandler()
        => PropertyFunctionHandlerRegistry.Register(typeof(ToolLocationHelper), TryInvokePropertyFunction);
#pragma warning restore CA2255

    private static bool TryInvokePropertyFunction(string methodName, object?[] arguments, out object? result)
    {
        if (arguments is [])
        {
            if (methodName.Equals(nameof(CurrentToolsVersion), StringComparison.OrdinalIgnoreCase))
            {
                result = CurrentToolsVersion;
                return true;
            }

            if (methodName.Equals(nameof(PathToSystem), StringComparison.OrdinalIgnoreCase))
            {
                result = PathToSystem;
                return true;
            }
        }

        if (TryInvokeSdkPropertyFunction(methodName, arguments, out result))
        {
            return true;
        }

        if (TryInvokeFrameworkPropertyFunction(methodName, arguments, out result))
        {
            return true;
        }

        result = null;
        return false;
    }

    [UnconditionalSuppressMessage(
        "Interoperability",
        "CA1416:Validate platform compatibility",
        Justification = "The dispatcher preserves the platform behavior of the selected public ToolLocationHelper member.")]
    private static bool TryInvokeSdkPropertyFunction(string methodName, object?[] arguments, out object? result)
    {
        if (methodName.Equals(nameof(GetAssemblyFoldersExInfo), StringComparison.OrdinalIgnoreCase) &&
            arguments is [
                string assemblyRegistryRoot,
                string assemblyFrameworkVersion,
                string assemblySuffix,
                string assemblyOsVersion,
                string assemblyPlatform,
                var assemblyArchitectureValue
            ] &&
            TryConvertToEnum(assemblyArchitectureValue, out ReflectionProcessorArchitecture assemblyArchitecture))
        {
            return Invoked(
                GetAssemblyFoldersExInfo(
                    assemblyRegistryRoot,
                    assemblyFrameworkVersion,
                    assemblySuffix,
                    assemblyOsVersion,
                    assemblyPlatform,
                    assemblyArchitecture),
                out result);
        }

        if (methodName.Equals(nameof(GetAssemblyFoldersFromConfigInfo), StringComparison.OrdinalIgnoreCase) &&
            arguments is [string configFilePath, string configFrameworkVersion, var configArchitectureValue] &&
            TryConvertToEnum(configArchitectureValue, out ReflectionProcessorArchitecture configArchitecture))
        {
            return Invoked(GetAssemblyFoldersFromConfigInfo(configFilePath, configFrameworkVersion, configArchitecture), out result);
        }

        if (methodName.Equals(nameof(GetPlatformExtensionSDKLocations), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string identifier, Version version]
                    => Invoked(GetPlatformExtensionSDKLocations(identifier, version), out result),
                [string[] diskRoots, string registryRoot, string identifier, Version version]
                    => Invoked(GetPlatformExtensionSDKLocations(diskRoots, registryRoot, identifier, version), out result),
                [string[] diskRoots, string[] extensionRoots, string registryRoot, string identifier, Version version]
                    => Invoked(GetPlatformExtensionSDKLocations(diskRoots, extensionRoots, registryRoot, identifier, version), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetPlatformExtensionSDKLocationsAndVersions), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string identifier, Version version]
                    => Invoked(GetPlatformExtensionSDKLocationsAndVersions(identifier, version), out result),
                [string[] diskRoots, string registryRoot, string identifier, Version version]
                    => Invoked(GetPlatformExtensionSDKLocationsAndVersions(diskRoots, registryRoot, identifier, version), out result),
                [string[] diskRoots, string[] multiPlatformRoots, string registryRoot, string identifier, Version version]
                    => Invoked(
                        GetPlatformExtensionSDKLocationsAndVersions(diskRoots, multiPlatformRoots, registryRoot, identifier, version),
                        out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetPlatformExtensionSDKLocation), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string moniker, string identifier, Version version]
                    => Invoked(GetPlatformExtensionSDKLocation(moniker, identifier, version), out result),
                [string moniker, string identifier, Version version, string[] diskRoots, string registryRoot]
                    => Invoked(GetPlatformExtensionSDKLocation(moniker, identifier, version, diskRoots, registryRoot), out result),
                [string moniker, string identifier, Version version, string[] diskRoots, string[] extensionRoots, string registryRoot]
                    => Invoked(
                        GetPlatformExtensionSDKLocation(moniker, identifier, version, diskRoots, extensionRoots, registryRoot),
                        out result),
                [string moniker, string identifier, string version]
                    => Invoked(GetPlatformExtensionSDKLocation(moniker, identifier, version), out result),
                [string moniker, string identifier, string version, string diskRoots, string registryRoot]
                    => Invoked(GetPlatformExtensionSDKLocation(moniker, identifier, version, diskRoots, registryRoot), out result),
                [string moniker, string identifier, string version, string diskRoots, string extensionRoots, string registryRoot]
                    => Invoked(
                        GetPlatformExtensionSDKLocation(moniker, identifier, version, diskRoots, extensionRoots, registryRoot),
                        out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(FilterPlatformExtensionSDKs), StringComparison.OrdinalIgnoreCase) &&
            arguments is [Version filterPlatformVersion, IDictionary<string, string> filterExtensionSdks])
        {
            return Invoked(FilterPlatformExtensionSDKs(filterPlatformVersion, filterExtensionSdks), out result);
        }

        if (methodName.Equals(nameof(GetSDKReferenceFolders), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string sdkRoot] => Invoked(GetSDKReferenceFolders(sdkRoot), out result),
                [string sdkRoot, string configuration, string architecture]
                    => Invoked(GetSDKReferenceFolders(sdkRoot, configuration, architecture), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetSDKRedistFolders), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string sdkRoot] => Invoked(GetSDKRedistFolders(sdkRoot), out result),
                [string sdkRoot, string configuration, string architecture]
                    => Invoked(GetSDKRedistFolders(sdkRoot, configuration, architecture), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetSDKDesignTimeFolders), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string sdkRoot] => Invoked(GetSDKDesignTimeFolders(sdkRoot), out result),
                [string sdkRoot, string configuration, string architecture]
                    => Invoked(GetSDKDesignTimeFolders(sdkRoot, configuration, architecture), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetTargetPlatformSdks), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [] => Invoked(GetTargetPlatformSdks(), out result),
                [string[] diskRoots, string registryRoot] => Invoked(GetTargetPlatformSdks(diskRoots, registryRoot), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(FilterTargetPlatformSdks), StringComparison.OrdinalIgnoreCase) &&
            arguments is [IList<TargetPlatformSDK> targetSdksToFilter, Version targetOsVersion, Version targetVsVersion])
        {
            return Invoked(FilterTargetPlatformSdks(targetSdksToFilter, targetOsVersion, targetVsVersion), out result);
        }

        if (methodName.Equals(nameof(GetPlatformSDKPropsFileLocation), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string sdkIdentifier, string sdkVersion, string platformIdentifier, string minVersion, string platformVersion]
                    => Invoked(
                        GetPlatformSDKPropsFileLocation(sdkIdentifier, sdkVersion, platformIdentifier, minVersion, platformVersion),
                        out result),
                [
                    string sdkIdentifier,
                    string sdkVersion,
                    string platformIdentifier,
                    string minVersion,
                    string platformVersion,
                    string diskRoots,
                    string registryRoot
                ] => Invoked(
                    GetPlatformSDKPropsFileLocation(
                        sdkIdentifier,
                        sdkVersion,
                        platformIdentifier,
                        minVersion,
                        platformVersion,
                        diskRoots,
                        registryRoot),
                    out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetTargetPlatformReferences), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string sdkIdentifier, string sdkVersion, string platformIdentifier, string minVersion, string platformVersion]
                    => Invoked(
                        GetTargetPlatformReferences(sdkIdentifier, sdkVersion, platformIdentifier, minVersion, platformVersion),
                        out result),
                [
                    string sdkIdentifier,
                    string sdkVersion,
                    string platformIdentifier,
                    string minVersion,
                    string platformVersion,
                    string diskRoots,
                    string registryRoot
                ] => Invoked(
                    GetTargetPlatformReferences(
                        sdkIdentifier,
                        sdkVersion,
                        platformIdentifier,
                        minVersion,
                        platformVersion,
                        diskRoots,
                        registryRoot),
                    out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetPlatformOrFrameworkExtensionSdkReferences), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [
                    string extensionMoniker,
                    string sdkIdentifier,
                    string sdkVersion,
                    string diskRoots,
                    string extensionRoots,
                    string registryRoot
                ] => Invoked(
                    GetPlatformOrFrameworkExtensionSdkReferences(
                        extensionMoniker,
                        sdkIdentifier,
                        sdkVersion,
                        diskRoots,
                        extensionRoots,
                        registryRoot),
                    out result),
                [
                    string extensionMoniker,
                    string sdkIdentifier,
                    string sdkVersion,
                    string diskRoots,
                    string extensionRoots,
                    string registryRoot,
                    string platformIdentifier,
                    string platformVersion
                ] => Invoked(
                    GetPlatformOrFrameworkExtensionSdkReferences(
                        extensionMoniker,
                        sdkIdentifier,
                        sdkVersion,
                        diskRoots,
                        extensionRoots,
                        registryRoot,
                        platformIdentifier,
                        platformVersion),
                    out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetSDKContentFolderPath), StringComparison.OrdinalIgnoreCase) &&
            arguments is [
                string contentSdkIdentifier,
                string contentSdkVersion,
                string contentPlatformIdentifier,
                string contentMinVersion,
                string contentPlatformVersion,
                string contentFolderName,
                string contentDiskRoot
            ])
        {
            return Invoked(
                GetSDKContentFolderPath(
                    contentSdkIdentifier,
                    contentSdkVersion,
                    contentPlatformIdentifier,
                    contentMinVersion,
                    contentPlatformVersion,
                    contentFolderName,
                    contentDiskRoot),
                out result);
        }

        if (methodName.Equals(nameof(GetPlatformSDKLocation), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string identifier, Version version] => Invoked(GetPlatformSDKLocation(identifier, version), out result),
                [string identifier, Version version, string[] diskRoots, string registryRoot]
                    => Invoked(GetPlatformSDKLocation(identifier, version, diskRoots, registryRoot), out result),
                [string identifier, string version] => Invoked(GetPlatformSDKLocation(identifier, version), out result),
                [string identifier, string version, string diskRoots, string registryRoot]
                    => Invoked(GetPlatformSDKLocation(identifier, version, diskRoots, registryRoot), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetPlatformSDKDisplayName), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string identifier, string version] => Invoked(GetPlatformSDKDisplayName(identifier, version), out result),
                [string identifier, string version, string diskRoots, string registryRoot]
                    => Invoked(GetPlatformSDKDisplayName(identifier, version, diskRoots, registryRoot), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetPlatformsForSDK), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string identifier, Version version] => Invoked(GetPlatformsForSDK(identifier, version), out result),
                [string identifier, Version version, string[] diskRoots, string registryRoot]
                    => Invoked(GetPlatformsForSDK(identifier, version, diskRoots, registryRoot), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetLatestSDKTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string identifier, string version] => Invoked(GetLatestSDKTargetPlatformVersion(identifier, version), out result),
                [string identifier, string version, string[] sdkRoots]
                    => Invoked(GetLatestSDKTargetPlatformVersion(identifier, version, sdkRoots), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetFoldersInVSInstalls), StringComparison.OrdinalIgnoreCase) &&
            arguments is [var foldersMinimumValue, var foldersMaximumValue, var foldersSubfolderValue] &&
            TryConvertToNullableVersion(foldersMinimumValue, out Version? foldersMinimum) &&
            TryConvertToNullableVersion(foldersMaximumValue, out Version? foldersMaximum) &&
            TryConvertToNullableString(foldersSubfolderValue, out string? foldersSubfolder))
        {
            return Invoked(GetFoldersInVSInstalls(foldersMinimum, foldersMaximum, foldersSubfolder), out result);
        }

        if (methodName.Equals(nameof(GetFoldersInVSInstallsAsString), StringComparison.OrdinalIgnoreCase) &&
            arguments is [var stringMinimumValue, var stringMaximumValue, var stringSubfolderValue] &&
            TryConvertToNullableString(stringMinimumValue, out string? stringMinimum) &&
            TryConvertToNullableString(stringMaximumValue, out string? stringMaximum) &&
            TryConvertToNullableString(stringSubfolderValue, out string? stringSubfolder))
        {
            return Invoked(GetFoldersInVSInstallsAsString(stringMinimum, stringMaximum, stringSubfolder), out result);
        }

        if (methodName.Equals(nameof(FindRootFolderWhereAllFilesExist), StringComparison.OrdinalIgnoreCase) &&
            arguments is [string candidateRoots, string requiredRelativePaths])
        {
            return Invoked(FindRootFolderWhereAllFilesExist(candidateRoots, requiredRelativePaths), out result);
        }

        result = null;
        return false;
    }

#pragma warning disable CS0618 // Property functions preserve access to the shipped obsolete ToolLocationHelper members.
    private static bool TryInvokeFrameworkPropertyFunction(string methodName, object?[] arguments, out object? result)
    {
        if (methodName.Equals(nameof(GetDotNetFrameworkVersionFolderPrefix), StringComparison.OrdinalIgnoreCase) &&
            arguments is [var folderPrefixVersionValue] &&
            TryConvertToEnum(folderPrefixVersionValue, out TargetDotNetFrameworkVersion versionFolderVersion))
        {
            return Invoked(GetDotNetFrameworkVersionFolderPrefix(versionFolderVersion), out result);
        }

        if (methodName.Equals(nameof(GetDotNetFrameworkRootRegistryKey), StringComparison.OrdinalIgnoreCase) &&
            arguments is [var rootRegistryVersionValue] &&
            TryConvertToEnum(rootRegistryVersionValue, out TargetDotNetFrameworkVersion rootRegistryVersion))
        {
            return Invoked(GetDotNetFrameworkRootRegistryKey(rootRegistryVersion), out result);
        }

        if (methodName.Equals(nameof(GetDotNetFrameworkSdkRootRegistryKey), StringComparison.OrdinalIgnoreCase))
        {
            return TryInvokeFrameworkVersionAndVisualStudioVersion(
                arguments,
                GetDotNetFrameworkSdkRootRegistryKey,
                GetDotNetFrameworkSdkRootRegistryKey,
                out result);
        }

        if (methodName.Equals(nameof(GetDotNetFrameworkSdkInstallKeyValue), StringComparison.OrdinalIgnoreCase))
        {
            return TryInvokeFrameworkVersionAndVisualStudioVersion(
                arguments,
                GetDotNetFrameworkSdkInstallKeyValue,
                GetDotNetFrameworkSdkInstallKeyValue,
                out result);
        }

        if (methodName.Equals(nameof(GetPathToDotNetFramework), StringComparison.OrdinalIgnoreCase))
        {
            switch (arguments)
            {
                case [var versionValue] when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version):
                    return Invoked(GetPathToDotNetFramework(version), out result);

                case [var versionValue, var architectureValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version) &&
                         TryConvertToEnum(architectureValue, out DotNetFrameworkArchitecture architecture):
                    return Invoked(GetPathToDotNetFramework(version, architecture), out result);
            }

            return NotInvoked(out result);
        }

        if (methodName.Equals(nameof(GetPathToDotNetFrameworkSdk), StringComparison.OrdinalIgnoreCase))
        {
            switch (arguments)
            {
                case []:
                    return Invoked(GetPathToDotNetFrameworkSdk(), out result);

                case [var versionValue] when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version):
                    return Invoked(GetPathToDotNetFrameworkSdk(version), out result);

                case [var versionValue, var visualStudioVersionValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version) &&
                         TryConvertToEnum(visualStudioVersionValue, out VisualStudioVersion visualStudioVersion):
                    return Invoked(GetPathToDotNetFrameworkSdk(version, visualStudioVersion), out result);
            }

            return NotInvoked(out result);
        }

        if (methodName.Equals(nameof(GetPathToDotNetFrameworkReferenceAssemblies), StringComparison.OrdinalIgnoreCase) &&
            arguments is [var referenceAssemblyVersionValue] &&
            TryConvertToEnum(referenceAssemblyVersionValue, out TargetDotNetFrameworkVersion referenceAssemblyVersion))
        {
            return Invoked(GetPathToDotNetFrameworkReferenceAssemblies(referenceAssemblyVersion), out result);
        }

        if (methodName.Equals(nameof(GetPathToStandardLibraries), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string identifier, string version, string profile]
                    => Invoked(GetPathToStandardLibraries(identifier, version, profile), out result),
                [string identifier, string version, string profile, string platform]
                    => Invoked(GetPathToStandardLibraries(identifier, version, profile, platform), out result),
                [string identifier, string version, string profile, string platform, string root]
                    => Invoked(GetPathToStandardLibraries(identifier, version, profile, platform, root), out result),
                [string identifier, string version, string profile, string platform, string root, string fallbackPaths]
                    => Invoked(GetPathToStandardLibraries(identifier, version, profile, platform, root, fallbackPaths), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetPathToReferenceAssemblies), StringComparison.OrdinalIgnoreCase))
        {
            return arguments switch
            {
                [string identifier, string version, string profile]
                    => Invoked(GetPathToReferenceAssemblies(identifier, version, profile), out result),
                [string identifier, string version, string profile, string root]
                    => Invoked(GetPathToReferenceAssemblies(identifier, version, profile, root), out result),
                [string identifier, string version, string profile, string root, string fallbackPaths]
                    => Invoked(GetPathToReferenceAssemblies(identifier, version, profile, root, fallbackPaths), out result),
                [FrameworkNameVersioning frameworkName]
                    => Invoked(GetPathToReferenceAssemblies(frameworkName), out result),
                [string root, FrameworkNameVersioning frameworkName]
                    => Invoked(GetPathToReferenceAssemblies(root, frameworkName), out result),
                [string root, string fallbackPaths, FrameworkNameVersioning frameworkName]
                    => Invoked(GetPathToReferenceAssemblies(root, fallbackPaths, frameworkName), out result),
                _ => NotInvoked(out result),
            };
        }

        if (methodName.Equals(nameof(GetDisplayNameForTargetFrameworkDirectory), StringComparison.OrdinalIgnoreCase) &&
            arguments is [string displayFrameworkDirectory, FrameworkNameVersioning displayFrameworkName])
        {
            return Invoked(GetDisplayNameForTargetFrameworkDirectory(displayFrameworkDirectory, displayFrameworkName), out result);
        }

        if (methodName.Equals(nameof(ClearSDKStaticCache), StringComparison.OrdinalIgnoreCase) && arguments is [])
        {
            ClearSDKStaticCache();
            result = null;
            return true;
        }

        if (methodName.Equals(nameof(GetPathToDotNetFrameworkSdkFile), StringComparison.OrdinalIgnoreCase))
        {
            switch (arguments)
            {
                case [string fileName]:
                    return Invoked(GetPathToDotNetFrameworkSdkFile(fileName), out result);

                case [string fileName, var versionValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version):
                    return Invoked(GetPathToDotNetFrameworkSdkFile(fileName, version), out result);

                case [string fileName, var versionValue, var thirdValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version):
                    if (TryConvertToEnum(thirdValue, out VisualStudioVersion visualStudioVersion))
                    {
                        return Invoked(GetPathToDotNetFrameworkSdkFile(fileName, version, visualStudioVersion), out result);
                    }

                    if (TryConvertToEnum(thirdValue, out DotNetFrameworkArchitecture architecture))
                    {
                        return Invoked(GetPathToDotNetFrameworkSdkFile(fileName, version, architecture), out result);
                    }

                    break;

                case [string fileName, var versionValue, var visualStudioVersionValue, var architectureValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version) &&
                         TryConvertToEnum(visualStudioVersionValue, out VisualStudioVersion fourArgumentVisualStudioVersion) &&
                         TryConvertToEnum(architectureValue, out DotNetFrameworkArchitecture fourArgumentArchitecture):
                    return Invoked(GetPathToDotNetFrameworkSdkFile(fileName, version, fourArgumentVisualStudioVersion, fourArgumentArchitecture), out result);
            }

            return NotInvoked(out result);
        }

        if (methodName.Equals(nameof(GetPathToWindowsSdk), StringComparison.OrdinalIgnoreCase) &&
            arguments is [var windowsSdkVersionValue, var windowsVisualStudioVersionValue] &&
            TryConvertToEnum(windowsSdkVersionValue, out TargetDotNetFrameworkVersion windowsSdkVersion) &&
            TryConvertToEnum(windowsVisualStudioVersionValue, out VisualStudioVersion windowsVisualStudioVersion))
        {
            return Invoked(GetPathToWindowsSdk(windowsSdkVersion, windowsVisualStudioVersion), out result);
        }

        if (methodName.Equals(nameof(GetPathToWindowsSdkFile), StringComparison.OrdinalIgnoreCase))
        {
            switch (arguments)
            {
                case [string fileName, var versionValue, var visualStudioVersionValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version) &&
                         TryConvertToEnum(visualStudioVersionValue, out VisualStudioVersion visualStudioVersion):
                    return Invoked(GetPathToWindowsSdkFile(fileName, version, visualStudioVersion), out result);

                case [string fileName, var versionValue, var visualStudioVersionValue, var architectureValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version) &&
                         TryConvertToEnum(visualStudioVersionValue, out VisualStudioVersion visualStudioVersion) &&
                         TryConvertToEnum(architectureValue, out DotNetFrameworkArchitecture architecture):
                    return Invoked(GetPathToWindowsSdkFile(fileName, version, visualStudioVersion, architecture), out result);
            }

            return NotInvoked(out result);
        }

        if (methodName.Equals(nameof(GetPathToBuildTools), StringComparison.OrdinalIgnoreCase))
        {
            switch (arguments)
            {
                case [string toolsVersion]:
                    return Invoked(GetPathToBuildTools(toolsVersion), out result);

                case [string toolsVersion, var architectureValue]
                    when TryConvertToEnum(architectureValue, out DotNetFrameworkArchitecture architecture):
                    return Invoked(GetPathToBuildTools(toolsVersion, architecture), out result);
            }

            return NotInvoked(out result);
        }

        if (methodName.Equals(nameof(GetPathToBuildToolsFile), StringComparison.OrdinalIgnoreCase))
        {
            switch (arguments)
            {
                case [string fileName, string toolsVersion]:
                    return Invoked(GetPathToBuildToolsFile(fileName, toolsVersion), out result);

                case [string fileName, string toolsVersion, var architectureValue]
                    when TryConvertToEnum(architectureValue, out DotNetFrameworkArchitecture architecture):
                    return Invoked(GetPathToBuildToolsFile(fileName, toolsVersion, architecture), out result);
            }

            return NotInvoked(out result);
        }

        if (methodName.Equals(nameof(GetPathToDotNetFrameworkFile), StringComparison.OrdinalIgnoreCase))
        {
            switch (arguments)
            {
                case [string fileName, var versionValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version):
                    return Invoked(GetPathToDotNetFrameworkFile(fileName, version), out result);

                case [string fileName, var versionValue, var architectureValue]
                    when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version) &&
                         TryConvertToEnum(architectureValue, out DotNetFrameworkArchitecture architecture):
                    return Invoked(GetPathToDotNetFrameworkFile(fileName, version, architecture), out result);
            }

            return NotInvoked(out result);
        }

        if (methodName.Equals(nameof(GetPathToSystemFile), StringComparison.OrdinalIgnoreCase) &&
            arguments is [string systemFileName])
        {
            return Invoked(GetPathToSystemFile(systemFileName), out result);
        }

        if (methodName.Equals(nameof(GetSupportedTargetFrameworks), StringComparison.OrdinalIgnoreCase) && arguments is [])
        {
            return Invoked(GetSupportedTargetFrameworks(), out result);
        }

        if (methodName.Equals(nameof(HighestVersionOfTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase) &&
            arguments is [string frameworkRoot, string frameworkIdentifier])
        {
            return Invoked(HighestVersionOfTargetFrameworkIdentifier(frameworkRoot, frameworkIdentifier), out result);
        }

        if (methodName.Equals(nameof(GetProgramFilesReferenceAssemblyRoot), StringComparison.OrdinalIgnoreCase) && arguments is [])
        {
            return Invoked(GetProgramFilesReferenceAssemblyRoot(), out result);
        }

        result = null;
        return false;
    }
#pragma warning restore CS0618

    private static bool TryInvokeFrameworkVersionAndVisualStudioVersion(
        object?[] arguments,
        Func<TargetDotNetFrameworkVersion, string> singleArgument,
        Func<TargetDotNetFrameworkVersion, VisualStudioVersion, string> twoArguments,
        out object? result)
    {
        switch (arguments)
        {
            case [var versionValue] when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version):
                return Invoked(singleArgument(version), out result);

            case [var versionValue, var visualStudioVersionValue]
                when TryConvertToEnum(versionValue, out TargetDotNetFrameworkVersion version) &&
                     TryConvertToEnum(visualStudioVersionValue, out VisualStudioVersion visualStudioVersion):
                return Invoked(twoArguments(version, visualStudioVersion), out result);

            default:
                return NotInvoked(out result);
        }
    }

    private static bool Invoked(object? value, out object? result)
    {
        result = value;
        return true;
    }

    private static bool NotInvoked(out object? result)
    {
        result = null;
        return false;
    }

    private static bool TryConvertToNullableString(object? value, out string? result)
    {
        if (value is null or string)
        {
            result = (string?)value;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertToNullableVersion(object? value, out Version? result)
    {
        if (value is null)
        {
            result = null;
            return true;
        }

        return TryConvertToVersion(value, out result);
    }

    private static bool TryConvertToVersion(object? value, out Version? result)
    {
        if (value is Version version)
        {
            result = version;
            return true;
        }

        if (value is string text && Version.TryParse(text, out result))
        {
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryConvertToEnum<T>(object? value, out T result)
        where T : struct, Enum
    {
        if (value is T enumValue)
        {
            result = enumValue;
            return true;
        }

        if (value is string text &&
            !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            !ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            if (text.IndexOf('.') >= 0 || text.IndexOf('|') >= 0)
            {
                text = NormalizeEnumArgument<T>(text);
            }

            return Enum.TryParse(text, out result);
        }

        result = default;
        return false;
    }

    private static string NormalizeEnumArgument<T>(string value)
        where T : struct, Enum
    {
        string fullName = typeof(T).FullName!;
        string leafName = typeof(T).Name;
        var builder = new StringBuilder(value.Length);
        int copyStart = 0;
        int index = 0;

        while (index < value.Length)
        {
            if (value[index] == '|')
            {
                builder.Append(value, copyStart, index - copyStart);
                builder.Append(',');
                copyStart = ++index;
            }
            else if (TryGetEnumQualifierLength(value, index, fullName, out int qualifierLength) ||
                     TryGetEnumQualifierLength(value, index, leafName, out qualifierLength))
            {
                builder.Append(value, copyStart, index - copyStart);
                index += qualifierLength;
                copyStart = index;
            }
            else
            {
                index++;
            }
        }

        builder.Append(value, copyStart, value.Length - copyStart);
        return builder.ToString();
    }

    private static bool TryGetEnumQualifierLength(string value, int startIndex, string typeName, out int result)
    {
        if (value.Length - startIndex > typeName.Length &&
            value[startIndex + typeName.Length] == '.' &&
            string.CompareOrdinal(value, startIndex, typeName, 0, typeName.Length) == 0)
        {
            result = typeName.Length + 1;
            return true;
        }

        result = 0;
        return false;
    }
}
