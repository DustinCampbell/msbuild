// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace Microsoft.Build.Evaluation.Expander;

internal partial class WellKnownFunctions
{
    internal static bool TryExecuteStaticCultureInfoFunction(
        StringSegment methodName,
        FunctionArguments args,
        out object? result)
    {
        if (args.Length == 0)
        {
            if (methodName.Equals(nameof(CultureInfo.CurrentUICulture), StringComparison.OrdinalIgnoreCase))
            {
                result = CultureInfo.CurrentUICulture;
                return true;
            }

            if (methodName.Equals(nameof(CultureInfo.CurrentCulture), StringComparison.OrdinalIgnoreCase))
            {
                result = CultureInfo.CurrentCulture;
                return true;
            }
        }

        if (methodName.Equals(nameof(CultureInfo.GetCultureInfo), StringComparison.OrdinalIgnoreCase) &&
            args.TryGetArg(out string? cultureName) &&
            cultureName is not null)
        {
            result = CultureInfo.GetCultureInfo(cultureName);
            return true;
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteCultureInfoFunction(
        StringSegment methodName,
        CultureInfo culture,
        FunctionArguments args,
        out object? result)
    {
        if (args.Length == 0)
        {
            if (methodName.Equals(nameof(CultureInfo.Name), StringComparison.OrdinalIgnoreCase))
            {
                result = culture.Name;
                return true;
            }

            if (methodName.Equals(nameof(CultureInfo.LCID), StringComparison.OrdinalIgnoreCase))
            {
                result = culture.LCID;
                return true;
            }

            if (methodName.Equals(nameof(CultureInfo.TwoLetterISOLanguageName), StringComparison.OrdinalIgnoreCase))
            {
                result = culture.TwoLetterISOLanguageName;
                return true;
            }
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteRuntimeInformationFunction(
        StringSegment methodName,
        FunctionArguments args,
        out object? result)
    {
        if (args.Length == 0)
        {
            if (methodName.Equals(nameof(RuntimeInformation.ProcessArchitecture), StringComparison.OrdinalIgnoreCase))
            {
                result = RuntimeInformation.ProcessArchitecture;
                return true;
            }

            if (methodName.Equals(nameof(RuntimeInformation.OSArchitecture), StringComparison.OrdinalIgnoreCase))
            {
                result = RuntimeInformation.OSArchitecture;
                return true;
            }

            if (methodName.Equals(nameof(RuntimeInformation.FrameworkDescription), StringComparison.OrdinalIgnoreCase))
            {
                result = RuntimeInformation.FrameworkDescription;
                return true;
            }

            if (methodName.Equals(nameof(RuntimeInformation.OSDescription), StringComparison.OrdinalIgnoreCase))
            {
                result = RuntimeInformation.OSDescription;
                return true;
            }
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteEnvironmentFunction(
        StringSegment methodName,
        FunctionArguments args,
        out object? result)
    {
        if (args.Length == 0)
        {
            if (methodName.Equals(nameof(Environment.Is64BitProcess), StringComparison.OrdinalIgnoreCase))
            {
                result = Environment.Is64BitProcess;
                return true;
            }

            if (methodName.Equals(nameof(Environment.Is64BitOperatingSystem), StringComparison.OrdinalIgnoreCase))
            {
                result = Environment.Is64BitOperatingSystem;
                return true;
            }

            if (methodName.Equals(nameof(Environment.ProcessorCount), StringComparison.OrdinalIgnoreCase))
            {
                result = Environment.ProcessorCount;
                return true;
            }

            if (methodName.Equals(nameof(Environment.NewLine), StringComparison.OrdinalIgnoreCase))
            {
                result = Environment.NewLine;
                return true;
            }
        }

        if (methodName.Equals(nameof(Environment.GetEnvironmentVariable), StringComparison.OrdinalIgnoreCase) &&
            args.TryGetArg(out string? variable) &&
            variable is not null)
        {
            result = Environment.GetEnvironmentVariable(variable);
            return true;
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteStaticDateTimeFunction(
        StringSegment methodName,
        FunctionArguments args,
        out object? result)
    {
        if (args.Length == 0)
        {
            if (methodName.Equals(nameof(DateTime.UtcNow), StringComparison.OrdinalIgnoreCase))
            {
                result = DateTime.UtcNow;
                return true;
            }

            if (methodName.Equals(nameof(DateTime.Now), StringComparison.OrdinalIgnoreCase))
            {
                result = DateTime.Now;
                return true;
            }

            if (methodName.Equals(nameof(DateTime.Today), StringComparison.OrdinalIgnoreCase))
            {
                result = DateTime.Today;
                return true;
            }
        }

        if (methodName.Equals(nameof(DateTime.Parse), StringComparison.OrdinalIgnoreCase) &&
            args.TryGetArg(out string? value) &&
            value is not null)
        {
            result = DateTime.Parse(value, CultureInfo.CurrentCulture);
            return true;
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteDateTimeFunction(
        StringSegment methodName,
        DateTime value,
        FunctionArguments args,
        out object? result)
    {
        if (methodName.Equals(nameof(DateTime.ToString), StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0)
            {
                result = value.ToString();
                return true;
            }

            if (args.TryGetArg(out string? format))
            {
                result = value.ToString(format, CultureInfo.CurrentCulture);
                return true;
            }
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteRegexFunction(
        StringSegment methodName,
        FunctionArguments args,
        out object? result)
    {
        if (methodName.Equals(nameof(Regex.Match), StringComparison.OrdinalIgnoreCase) &&
            args.TryGetArgs(out string? input, out string? pattern) &&
            input is not null &&
            pattern is not null)
        {
            result = Regex.Match(input, pattern);
            return true;
        }

        if (methodName.Equals(nameof(Regex.IsMatch), StringComparison.OrdinalIgnoreCase) &&
            args.TryGetArgs(out input, out pattern) &&
            input is not null &&
            pattern is not null)
        {
            result = Regex.IsMatch(input, pattern);
            return true;
        }

        if (methodName.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) &&
            args.TryGetArgs(out input, out pattern, out string? replacement) &&
            input is not null &&
            pattern is not null &&
            replacement is not null)
        {
            result = Regex.Replace(input, pattern, replacement);
            return true;
        }

        if (methodName.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase) &&
            args.Length == 4 &&
            args.TryGetSegment(0, out StringSegment replaceInput) &&
            args.TryGetSegment(1, out StringSegment replacePattern) &&
            args.TryGetSegment(2, out StringSegment replaceValue) &&
            TryGetRegexOptions(args, 3, out RegexOptions options))
        {
            result = Regex.Replace(
                replaceInput.ValueOrEmpty,
                replacePattern.ValueOrEmpty,
                replaceValue.ValueOrEmpty,
                options);
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryGetRegexOptions(FunctionArguments args, int index, out RegexOptions options)
    {
        if (!args.TryGetSegment(index, out StringSegment name))
        {
            if (args.GetValue(index) is RegexOptions typedOptions)
            {
                options = typedOptions;
                return true;
            }

            options = default;
            return false;
        }

        const string FullPrefix = "System.Text.RegularExpressions.RegexOptions.";
        const string Prefix = "RegexOptions.";
        if (name.StartsWith(FullPrefix))
        {
            name = name[FullPrefix.Length..];
        }
        else if (name.StartsWith(Prefix))
        {
            name = name[Prefix.Length..];
        }

#if NET
        return Enum.TryParse(name.AsSpan(), ignoreCase: true, out options);
#else
        return Enum.TryParse(name.Value, ignoreCase: true, out options);
#endif
    }

    internal static bool TryExecuteFileFunction(
        StringSegment methodName,
        FunctionArguments args,
        out object? result)
    {
        if (args.Length == 1 && TryGetFileSystemPath(args, 0, out string? path))
        {
            if (methodName.Equals(nameof(File.Exists), StringComparison.OrdinalIgnoreCase))
            {
                result = File.Exists(path);
                return true;
            }

            if (methodName.Equals(nameof(File.GetCreationTime), StringComparison.OrdinalIgnoreCase))
            {
                result = File.GetCreationTime(path);
                return true;
            }

            if (methodName.Equals(nameof(File.GetCreationTimeUtc), StringComparison.OrdinalIgnoreCase))
            {
                result = File.GetCreationTimeUtc(path);
                return true;
            }

            if (methodName.Equals(nameof(File.GetAttributes), StringComparison.OrdinalIgnoreCase))
            {
                result = File.GetAttributes(path);
                return true;
            }

            if (methodName.Equals(nameof(File.GetLastAccessTime), StringComparison.OrdinalIgnoreCase))
            {
                result = File.GetLastAccessTime(path);
                return true;
            }

            if (methodName.Equals(nameof(File.GetLastWriteTime), StringComparison.OrdinalIgnoreCase))
            {
                result = File.GetLastWriteTime(path);
                return true;
            }

            if (methodName.Equals(nameof(File.GetLastWriteTimeUtc), StringComparison.OrdinalIgnoreCase))
            {
                result = File.GetLastWriteTimeUtc(path);
                return true;
            }

            if (methodName.Equals(nameof(File.ReadAllText), StringComparison.OrdinalIgnoreCase))
            {
                result = File.ReadAllText(path);
                return true;
            }

            if (methodName.Equals(nameof(File.ReadAllBytes), StringComparison.OrdinalIgnoreCase))
            {
                result = File.ReadAllBytes(path);
                return true;
            }
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteDirectoryFunction(
        StringSegment methodName,
        FunctionArguments args,
        out object? result)
    {
        if (args.Length >= 1 && TryGetFileSystemPath(args, 0, out string? path))
        {
            if (methodName.Equals(nameof(Directory.Exists), StringComparison.OrdinalIgnoreCase) && args.Length == 1)
            {
                result = Directory.Exists(path);
                return true;
            }

            if (methodName.Equals(nameof(Directory.GetParent), StringComparison.OrdinalIgnoreCase) && args.Length == 1)
            {
                result = Directory.GetParent(path);
                return true;
            }

            if (methodName.Equals(nameof(Directory.GetLastAccessTime), StringComparison.OrdinalIgnoreCase) && args.Length == 1)
            {
                result = Directory.GetLastAccessTime(path);
                return true;
            }

            if (methodName.Equals(nameof(Directory.GetLastWriteTime), StringComparison.OrdinalIgnoreCase) && args.Length == 1)
            {
                result = Directory.GetLastWriteTime(path);
                return true;
            }

            if (methodName.Equals(nameof(Directory.GetFiles), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 1)
                {
                    result = Directory.GetFiles(path);
                    return true;
                }

                if (args.TryGetSegment(1, out StringSegment searchPattern) && args.Length == 2)
                {
                    result = Directory.GetFiles(path, searchPattern.ValueOrEmpty);
                    return true;
                }
            }

            if (methodName.Equals(nameof(Directory.GetDirectories), StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 1)
                {
                    result = Directory.GetDirectories(path);
                    return true;
                }

                if (args.TryGetSegment(1, out StringSegment directorySearchPattern) && args.Length == 2)
                {
                    result = Directory.GetDirectories(path, directorySearchPattern.ValueOrEmpty);
                    return true;
                }
            }
        }

        result = null;
        return false;
    }

    private static bool TryGetFileSystemPath(
        FunctionArguments args,
        int index,
        [NotNullWhen(true)] out string? path)
    {
        if (!args.TryGetSegment(index, out StringSegment segment))
        {
            path = null;
            return false;
        }

        segment = FileUtilities.FixFilePath(segment);
        path = segment.ValueOrEmpty;

        AbsolutePath? resolved = FileUtilities.MakeFullPathFromThreadWorkingDirectory(path);
        if (resolved.HasValue)
        {
            path = (string)resolved.GetValueOrDefault();
        }

        return true;
    }
}
