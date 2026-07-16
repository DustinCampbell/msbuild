// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Tasks;

internal sealed class SystemDesignSR
{
    internal const string ClassDocComment = "ClassDocComment";
    internal const string ClassComments1 = "ClassComments1";
    internal const string ClassComments3 = "ClassComments3";
    internal const string StringPropertyComment = "StringPropertyComment";
    internal const string StringPropertyTruncatedComment = "StringPropertyTruncatedComment";
    internal const string NonStringPropertyComment = "NonStringPropertyComment";
    internal const string NonStringPropertyDetailedComment = "NonStringPropertyDetailedComment";
    internal const string CulturePropertyComment1 = "CulturePropertyComment1";
    internal const string CulturePropertyComment2 = "CulturePropertyComment2";
    internal const string ResMgrPropertyComment = "ResMgrPropertyComment";
    internal const string MismatchedResourceName = "MismatchedResourceName";
    internal const string InvalidIdentifier = "InvalidIdentifier";

    private readonly MainAssemblyFallbackResourceManager _resources = new("System.Design", typeof(SystemDesignSR).Assembly);

    private SystemDesignSR()
    {
    }

    private static SystemDesignSR Loader
        => field ?? InterlockedOperations.Initialize(ref field, new());

    public static string? GetString(string name)
        => Loader.GetResourceString(name);

    public static string? GetString(string name, params object?[]? args)
    {
        string? resourceString = Loader.GetResourceString(name);

        if (resourceString is null || args is null or [])
        {
            return resourceString;
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is string { Length: > 1024 } value)
            {
                args[i] = value.Substring(0, 1024 - 3) + "...";
            }
        }

        return string.Format(CultureInfo.CurrentCulture, resourceString, args);
    }

    private string? GetResourceString(string name)
        => _resources.GetString(name);

    /// <summary>
    ///  The containing assembly is set to lookup resources for the neutral language in satellite assemblies, not in the main assembly.
    ///  System.Design resources are not meant to be translated, so the ResourceManager should not look for satellite assemblies.
    ///  This ResourceManager forces resource lookup to be constrained to the current assembly and not look for satellites.
    /// </summary>
    private sealed class MainAssemblyFallbackResourceManager : ResourceManager
    {
        public MainAssemblyFallbackResourceManager(string baseName, Assembly assembly)
            : base(baseName, assembly)
            => FallbackLocation = UltimateResourceFallbackLocation.MainAssembly;
    }
}
