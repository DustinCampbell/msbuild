// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Contains the outcome and value produced by attempting to access a well-known member.
/// </summary>
internal readonly struct WellKnownMemberResult
{
    private WellKnownMemberResult(WellKnownMemberStatus status, object? value)
    {
        Status = status;
        Value = value;
    }

    /// <summary>
    ///  Gets a result indicating that the member or overload was not recognized.
    /// </summary>
    public static WellKnownMemberResult NotRecognized { get; } = new(WellKnownMemberStatus.NotRecognized, value: null);

    /// <summary>
    ///  Gets a result indicating that the member was recognized but its arguments were invalid.
    /// </summary>
    public static WellKnownMemberResult InvalidArguments { get; } = new(WellKnownMemberStatus.InvalidArguments, value: null);

    /// <summary>
    ///  Gets the dispatch outcome.
    /// </summary>
    public WellKnownMemberStatus Status { get; }

    /// <summary>
    ///  Gets the member value when <see cref="Status"/> is <see cref="WellKnownMemberStatus.Handled"/>.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    ///  Creates a successful result containing <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The member value, which may be <see langword="null"/>.</param>
    /// <returns>
    ///  A successful result.
    /// </returns>
    public static WellKnownMemberResult Handled(object? value)
        => new(WellKnownMemberStatus.Handled, value);
}
