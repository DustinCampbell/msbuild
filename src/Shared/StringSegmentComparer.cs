// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Shared;

internal abstract class StringSegmentComparer : IComparer<StringSegment>, IEqualityComparer<StringSegment>
{
    public static StringSegmentComparer Ordinal => OrdinalCaseSensitiveComparer.Instance;
    public static StringSegmentComparer OrdinalIgnoreCase => OrdinalIgnoreCaseComparer.Instance;

    public abstract int Compare(StringSegment x, StringSegment y);
    public abstract bool Equals(StringSegment x, StringSegment y);
    public abstract int GetHashCode(StringSegment obj);

    private abstract class OrdinalComparer : StringSegmentComparer
    {
        private const string Name = $"{nameof(StringSegmentComparer)}.{nameof(OrdinalComparer)}";

        private readonly bool _ignoreCase;
        private readonly StringComparison _comparisonType;

        public OrdinalComparer(bool ignoreCase)
        {
            _ignoreCase = ignoreCase;
            _comparisonType = ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
            => obj is OrdinalComparer other && _ignoreCase == other._ignoreCase;

        public override int GetHashCode()
        {
            int result = Name.GetHashCode();
            return _ignoreCase ? ~result : result;
        }

        public override int Compare(StringSegment x, StringSegment y)
            => x.Memory.Span.CompareTo(y.Memory.Span, _comparisonType);

        public override bool Equals(StringSegment x, StringSegment y)
            => x.Memory.Span.Equals(y.Memory.Span, _comparisonType);

        public override int GetHashCode(StringSegment obj)
            => obj.GetHashCode(_ignoreCase);
    }

    private sealed class OrdinalCaseSensitiveComparer : OrdinalComparer
    {
        public static readonly OrdinalCaseSensitiveComparer Instance = new();

        private OrdinalCaseSensitiveComparer()
            : base(ignoreCase: true)
        {
        }
    }

    private sealed class OrdinalIgnoreCaseComparer : OrdinalComparer
    {
        public static readonly OrdinalIgnoreCaseComparer Instance = new();

        private OrdinalIgnoreCaseComparer()
            : base(ignoreCase: true)
        {
        }
    }
}
