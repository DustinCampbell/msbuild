// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Microsoft.Build.Framework;

/// <summary>
///  A lightweight, type-safe handle to a localized MSBuild string resource. Wraps a
///  <see cref="ResourceProvider"/> and a resource name; the underlying string is looked up and
///  formatted only when one of the <see cref="Format()"/> members is invoked.
/// </summary>
/// <remarks>
///  <para>
///   Instances are produced by a generated per-assembly catalog (e.g. <c>Strings</c> in
///   Microsoft.Build), giving compile-time-checked identity in place of magic resource-name strings.
///  </para>
///  <para>
///   Two formatting families are provided: the <see cref="Format()"/> overloads mirror the old
///   <c>ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword</c> behavior (used for messages
///   and logs), while the <see cref="FormatStripCode()"/> overloads mirror
///   <c>ResourceUtilities.FormatResourceStringStripCodeAndKeyword</c> (used when constructing
///   exceptions that carry an MSBuild error code separately).
///  </para>
/// </remarks>
internal readonly struct ResourceString
{
    private readonly ResourceProvider _provider;

    internal ResourceString(ResourceProvider provider, string name)
    {
        _provider = provider;
        Name = name;
    }

    /// <summary>
    ///  Gets the resource name this handle refers to.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///  Gets the raw, unformatted resource string. Equivalent to the old
    ///  <c>ResourceUtilities.GetResourceString</c>.
    /// </summary>
    public string Text => _provider.GetString(Name);

    public override string ToString() => Text;

    // ---- Ignore-code-and-keyword formatting (messages and logs) ----

    public string Format() => Text;

    public string Format(object? arg0) => FormatString(Text, arg0);

    public string Format(object? arg0, object? arg1) => FormatString(Text, arg0, arg1);

    public string Format(object? arg0, object? arg1, object? arg2) => FormatString(Text, arg0, arg1, arg2);

    public string Format(params object?[]? args) => FormatString(Text, args);

    // ---- Strip-code-and-keyword formatting (exception messages) ----
    // Generic so callers that check a condition first do not box value-type args unless a throw occurs.

    internal string FormatStripCode() => ExtractMessageCode(Text, out _);

    internal string FormatStripCode<T0>(T0 arg0) => ExtractMessageCode(FormatString(Text, arg0), out _);

    internal string FormatStripCode<T0, T1>(T0 arg0, T1 arg1) => ExtractMessageCode(FormatString(Text, arg0, arg1), out _);

    internal string FormatStripCode<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2) => ExtractMessageCode(FormatString(Text, arg0, arg1, arg2), out _);

    internal string FormatStripCode(params object?[]? args) => ExtractMessageCode(FormatString(Text, args), out _);

    /// <summary>
    ///  Formats the resource, returning the message with any MSBuild error-code prefix stripped and
    ///  reporting the extracted <paramref name="code"/> and F1-help <paramref name="helpKeyword"/>.
    ///  Used when constructing <c>InvalidProjectFileException</c>s.
    /// </summary>
    internal string FormatStripCode(out string? code, out string? helpKeyword, params object?[]? args)
    {
        helpKeyword = "MSBuild." + Name;
        return ExtractMessageCode(FormatString(Text, args), out code);
    }

    // ---- Helpers delegating to the shared ResourceHelpers relocated from ResourceUtilities ----

    private static string FormatString(string unformatted, params object?[]? args)
        => ResourceHelpers.FormatString(unformatted, args);

    /// <summary>
    ///  Extracts the MSBuild message code (if any) prefixed to <paramref name="message"/>.
    ///  MSBuild codes match <c>^\s*(?&lt;CODE&gt;MSB\d\d\d\d):\s*(?&lt;MESSAGE&gt;.*)$</c>.
    /// </summary>
    /// <returns>The message without its code prefix, if any.</returns>
    private static string ExtractMessageCode(string message, out string? code)
        => ResourceHelpers.ExtractMessageCode(msbuildCodeOnly: true, message, out code);
}
