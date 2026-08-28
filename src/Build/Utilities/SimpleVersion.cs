// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Simple replacement for System.Version used to implement version
/// comparison intrinic property functions.
///
/// Allows major version only (e.g. "3" is 3.0.0.0), ignores leading 'v'
/// (e.g. "v3.0" is 3.0.0.0).
///
/// Ignores semver prerelease and metadata portions (e.g. "1.0.0-preview+info"
/// is 1.0.0.0).
///
/// Treats unspecified components as 0 (e.g. x == x.0 == x.0.0 == x.0.0.0).
///
/// Ignores leading and trailing whitespace, but does not tolerate whitespace
/// between components, unlike System.Version.
///
/// Also unlike System.Version, '+' is ignored as semver metadata as described
/// above, not tolerated as positive sign of integer component.
/// </summary>
/// <remarks>
/// Tolerating leading 'v' allows using $(TargetFrameworkVersion) directly.
///
/// Ignoring semver portions allows, for example, checking >= major.minor
/// while still in development of that release.
///
/// Implemented as a struct and parsed directly from <see cref="StringSegment"/>
/// to avoid heap allocation.
/// </remarks>
internal readonly struct SimpleVersion : IEquatable<SimpleVersion>, IComparable<SimpleVersion>
{
    public readonly int Major;
    public readonly int Minor;
    public readonly int Build;
    public readonly int Revision;

    public SimpleVersion(int major, int minor = 0, int build = 0, int revision = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        ArgumentOutOfRangeException.ThrowIfNegative(build);
        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        Major = major;
        Minor = minor;
        Build = build;
        Revision = revision;
    }

    public bool Equals(SimpleVersion other)
        => Major == other.Major &&
           Minor == other.Minor &&
           Build == other.Build &&
           Revision == other.Revision;

    public int CompareTo(SimpleVersion other)
        => Major != other.Major
            ? (Major > other.Major ? 1 : -1)
            : Minor != other.Minor
                ? (Minor > other.Minor ? 1 : -1)
                : Build != other.Build
                    ? (Build > other.Build ? 1 : -1)
                    : Revision != other.Revision
                        ? (Revision > other.Revision ? 1 : -1)
                        : 0;

    public override bool Equals(object? obj)
        => obj is SimpleVersion v && Equals(v);

    public override int GetHashCode()
        => (Major, Minor, Build, Revision).GetHashCode();

    public override string ToString()
        => FormattableString.Invariant($"{Major}.{Minor}.{Build}.{Revision}");

    public static bool operator ==(SimpleVersion a, SimpleVersion b)
        => a.Equals(b);

    public static bool operator !=(SimpleVersion a, SimpleVersion b)
        => !a.Equals(b);

    public static bool operator <(SimpleVersion a, SimpleVersion b)
        => a.CompareTo(b) < 0;

    public static bool operator <=(SimpleVersion a, SimpleVersion b)
        => a.CompareTo(b) <= 0;

    public static bool operator >(SimpleVersion a, SimpleVersion b)
        => a.CompareTo(b) > 0;

    public static bool operator >=(SimpleVersion a, SimpleVersion b)
        => a.CompareTo(b) >= 0;

    public static SimpleVersion Parse(string input)
        => Parse((StringSegment)input);

    public static SimpleVersion Parse(StringSegment input)
    {
        if (!input.HasValue)
        {
            throw new ArgumentNullException(nameof(input));
        }

        StringSegment value = RemoveTrivia(input);

        int minor = 0, build = 0, revision = 0;

        if (ParseComponent(ref value, out int major) &&
            ParseComponent(ref value, out minor) &&
            ParseComponent(ref value, out build) &&
            ParseComponent(ref value, out revision))
        {
            // More than 4 components (too many dots)
            throw InvalidVersionFormat();
        }

        return new SimpleVersion(major, minor, build, revision);
    }

    private static StringSegment RemoveTrivia(StringSegment input)
    {
        // Ignore leading/trailing whitespace in input.
        StringSegment value = input.Trim();

        // Ignore a leading "v".
        if (value.Length > 0 && (value[0] is 'v' or 'V'))
        {
            value = value[1..];
        }

        // Ignore semver separator and anything after.
        int separatorIndex = value.IndexOfAny('-', '+');
        if (separatorIndex >= 0)
        {
            value = value[..separatorIndex];
        }

        return value;
    }

    private static bool ParseComponent(ref StringSegment version, out int value)
    {
        int dotIndex = version.IndexOf('.');
        if (dotIndex < 0)
        {
            value = ParseComponent(version);
            return false;
        }

        value = ParseComponent(version[..dotIndex]);
        version = version[(dotIndex + 1)..];
        return true;
    }

    private static int ParseComponent(StringSegment component)
        => int.TryParse(component, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw InvalidVersionFormat();

    private static Exception InvalidVersionFormat()
        => new FormatException(ResourceUtilities.GetResourceString(nameof(InvalidVersionFormat)));
}
