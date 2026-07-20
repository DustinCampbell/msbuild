// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
#endif
using System.Globalization;
using System.Threading;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Represents a single named resource string that is loaded on demand from a <see cref="ResourceProvider"/>
///  and then cached. Provides helpers for formatting the string and for inspecting or stripping any MSBuild
///  message code (e.g. <c>MSB1234</c>) prefixed to it.
/// </summary>
/// <param name="provider">The provider used to load the resource string.</param>
/// <param name="name">The name of the resource.</param>
/// <param name="culture">
///  The culture to load the resource for, or <see langword="null"/> to use the current culture.
/// </param>
internal sealed partial class ResourceString(ResourceProvider provider, string name, CultureInfo? culture = null)
{
    private readonly ResourceProvider _provider = provider;
    private readonly CultureInfo? _culture = culture;

    private LocalizedCache? _cache;

    /// <summary>
    ///  Gets the resolved-and-parsed cache for the effective culture. When <see cref="_culture"/> is
    ///  <see langword="null"/>, the resource is resolved against the ambient <see cref="CultureInfo.CurrentUICulture"/>
    ///  at call time and re-resolved if that culture changes. This keeps a reused process (e.g. the persistent
    ///  task host) that switches culture per task from serving text frozen at the first-touched culture, while
    ///  still caching within a single culture (the common case).
    /// </summary>
    private LocalizedCache Cache
    {
        get
        {
            CultureInfo effectiveCulture = _culture ?? CultureInfo.CurrentUICulture;

            LocalizedCache? cache = Volatile.Read(ref _cache);
            if (cache is not null && cache.Culture.Equals(effectiveCulture))
            {
                return cache;
            }

            // Pass effectiveCulture explicitly (not _culture) so the stamped culture matches what was actually
            // loaded, avoiding a race if CurrentUICulture changes between the capture above and the load below.
            string text = _provider.GetString(Name, effectiveCulture);
            object parsedText = (object?)TextAndCode.TryParse(text) ?? text;

            cache = new LocalizedCache(effectiveCulture, text, parsedText);
            Volatile.Write(ref _cache, cache);
            return cache;
        }
    }

    /// <summary>
    ///  Gets the resource name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    ///  Gets the F1-help keyword for the host IDE associated with this resource, in the form
    ///  <c>MSBuild.&lt;Name&gt;</c>.
    /// </summary>
    public string HelpKeyword
        => field ??= $"MSBuild.{Name}";

    /// <summary>
    ///  Gets the text of the resource string.
    /// </summary>
    public string Text
        => Cache.Text;

    /// <summary>
    ///  Gets the resource text with any leading MSBuild message code (e.g. <c>MSB1234:</c>) removed.
    ///  If the text has no code prefix, this is the same instance as <see cref="Text"/>.
    /// </summary>
    public string TextWithoutCode
        => Cache.ParsedText switch
        {
            TextAndCode(var text, _) => text,
            string text => text,
            _ => Assumed.Unreachable<string>(),
        };

    /// <summary>
    ///  Gets the MSBuild message code (e.g. <c>MSB1234</c>) prefixed to the resource text,
    ///  or <see langword="null"/> if the text has no code prefix.
    /// </summary>
    public string? Code
        => Cache.ParsedText switch
        {
            TextAndCode(_, var code) => code,
            string => null,
            _ => Assumed.Unreachable<string>(),
        };

    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="arg0">The object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(object? arg0)
        => MessageFormatter.Format(_culture, Text, arg0);

    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(object? arg0, object? arg1)
        => MessageFormatter.Format(_culture, Text, arg0, arg1);

    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <param name="arg2">The third object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(object? arg0, object? arg1, object? arg2)
        => MessageFormatter.Format(_culture, Text, arg0, arg1, arg2);

    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(params object?[] args)
        => MessageFormatter.Format(_culture, Text, args);

#if NET
    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(params ReadOnlySpan<object?> args)
        => MessageFormatter.Format(_culture, Text, args);
#endif

    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the specified culture.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values.</param>
    /// <param name="arg0">The object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(CultureInfo culture, object? arg0)
        => MessageFormatter.Format(culture, Text, arg0);

    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the specified culture.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values.</param>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(CultureInfo culture, object? arg0, object? arg1)
        => MessageFormatter.Format(culture, Text, arg0, arg1);

    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the specified culture.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values.</param>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <param name="arg2">The third object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(CultureInfo culture, object? arg0, object? arg1, object? arg2)
        => MessageFormatter.Format(culture, Text, arg0, arg1, arg2);

    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the specified culture.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values.</param>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(CultureInfo culture, params object?[] args)
        => MessageFormatter.Format(culture, Text, args);

#if NET
    /// <summary>
    ///  Formats <see cref="Text"/> as a composite format string, using the specified culture.
    /// </summary>
    /// <param name="culture">The culture used to format the substituted values.</param>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted resource string.
    /// </returns>
    public string Format(CultureInfo culture, params ReadOnlySpan<object?> args)
        => MessageFormatter.Format(culture, Text, args);
#endif

    /// <summary>
    ///  Formats <see cref="TextWithoutCode"/> (the resource text with any MSBuild message code prefix removed)
    ///  as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="arg0">The object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted, code-stripped resource string.
    /// </returns>
    public string FormatStripCode(object? arg0)
        => MessageFormatter.Format(_culture, TextWithoutCode, arg0);

    /// <summary>
    ///  Formats <see cref="TextWithoutCode"/> (the resource text with any MSBuild message code prefix removed)
    ///  as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted, code-stripped resource string.
    /// </returns>
    public string FormatStripCode(object? arg0, object? arg1)
        => MessageFormatter.Format(_culture, TextWithoutCode, arg0, arg1);

    /// <summary>
    ///  Formats <see cref="TextWithoutCode"/> (the resource text with any MSBuild message code prefix removed)
    ///  as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="arg0">The first object to substitute into the format string.</param>
    /// <param name="arg1">The second object to substitute into the format string.</param>
    /// <param name="arg2">The third object to substitute into the format string.</param>
    /// <returns>
    ///  The formatted, code-stripped resource string.
    /// </returns>
    public string FormatStripCode(object? arg0, object? arg1, object? arg2)
        => MessageFormatter.Format(_culture, TextWithoutCode, arg0, arg1, arg2);

    /// <summary>
    ///  Formats <see cref="TextWithoutCode"/> (the resource text with any MSBuild message code prefix removed)
    ///  as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted, code-stripped resource string.
    /// </returns>
    public string FormatStripCode(params object?[] args)
        => MessageFormatter.Format(_culture, TextWithoutCode, args);

#if NET
    /// <summary>
    ///  Formats <see cref="TextWithoutCode"/> (the resource text with any MSBuild message code prefix removed)
    ///  as a composite format string, using the resource's culture.
    /// </summary>
    /// <param name="args">The objects to substitute into the format string.</param>
    /// <returns>
    ///  The formatted, code-stripped resource string.
    /// </returns>
    public string FormatStripCode(params ReadOnlySpan<object?> args)
        => MessageFormatter.Format(_culture, TextWithoutCode, args);
#endif

    /// <summary>
    ///  Returns <see cref="Text"/>.
    /// </summary>
    /// <returns>
    ///  The resource text.
    /// </returns>
    public override string ToString()
        => Text;
}
