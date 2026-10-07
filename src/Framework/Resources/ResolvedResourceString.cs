// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Resources;

/// <summary>
///  Contains resolved resource text and its associated diagnostic metadata.
/// </summary>
internal readonly struct ResolvedResourceString
{
    internal ResolvedResourceString(string originalText, string text, string? code, string helpKeyword)
    {
        OriginalText = originalText;
        Text = text;
        Code = code;
        HelpKeyword = helpKeyword;
    }

    /// <summary>
    ///  Gets the localized resource text including any MSBuild diagnostic code.
    /// </summary>
    public string OriginalText { get; }

    /// <summary>
    ///  Gets the localized resource text with any MSBuild diagnostic code removed.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///  Gets the MSBuild diagnostic code parsed from the resource text, if present.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    ///  Gets the exact help keyword associated with the resource key.
    /// </summary>
    public string HelpKeyword { get; }
}
