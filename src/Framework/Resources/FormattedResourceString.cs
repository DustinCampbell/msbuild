// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework.Resources;

/// <summary>
///  Carries one formatting argument without allocating an argument array or boxing the carrier.
/// </summary>
internal readonly struct FormattedResourceString<T1> : IResourceString
{
    private readonly ResourceString _resource;
    private readonly T1 _arg0;

    internal FormattedResourceString(ResourceString resource, T1 arg0)
    {
        _resource = resource;
        _arg0 = arg0;
    }

    public ResolvedResourceString Resolve()
    {
        ResolvedResourceString resolved = _resource.Resolve();
        string text = MessageFormatter.Format(resolved.Text, _arg0);
        string originalText = object.ReferenceEquals(resolved.OriginalText, resolved.Text)
            ? text
            : MessageFormatter.Format(resolved.OriginalText, _arg0);

        return new ResolvedResourceString(
            originalText,
            text,
            resolved.Code,
            resolved.HelpKeyword);
    }
}

/// <summary>
///  Carries two formatting arguments without allocating an argument array or boxing the carrier.
/// </summary>
internal readonly struct FormattedResourceString<T1, T2> : IResourceString
{
    private readonly ResourceString _resource;
    private readonly T1 _arg0;
    private readonly T2 _arg1;

    internal FormattedResourceString(ResourceString resource, T1 arg0, T2 arg1)
    {
        _resource = resource;
        _arg0 = arg0;
        _arg1 = arg1;
    }

    public ResolvedResourceString Resolve()
    {
        ResolvedResourceString resolved = _resource.Resolve();
        string text = MessageFormatter.Format(resolved.Text, _arg0, _arg1);
        string originalText = object.ReferenceEquals(resolved.OriginalText, resolved.Text)
            ? text
            : MessageFormatter.Format(resolved.OriginalText, _arg0, _arg1);

        return new ResolvedResourceString(
            originalText,
            text,
            resolved.Code,
            resolved.HelpKeyword);
    }
}

/// <summary>
///  Carries three formatting arguments without allocating an argument array or boxing the carrier.
/// </summary>
internal readonly struct FormattedResourceString<T1, T2, T3> : IResourceString
{
    private readonly ResourceString _resource;
    private readonly T1 _arg0;
    private readonly T2 _arg1;
    private readonly T3 _arg2;

    internal FormattedResourceString(ResourceString resource, T1 arg0, T2 arg1, T3 arg2)
    {
        _resource = resource;
        _arg0 = arg0;
        _arg1 = arg1;
        _arg2 = arg2;
    }

    public ResolvedResourceString Resolve()
    {
        ResolvedResourceString resolved = _resource.Resolve();
        string text = MessageFormatter.Format(resolved.Text, _arg0, _arg1, _arg2);
        string originalText = object.ReferenceEquals(resolved.OriginalText, resolved.Text)
            ? text
            : MessageFormatter.Format(resolved.OriginalText, _arg0, _arg1, _arg2);

        return new ResolvedResourceString(
            originalText,
            text,
            resolved.Code,
            resolved.HelpKeyword);
    }
}

/// <summary>
///  Carries four formatting arguments without allocating an argument array or boxing the carrier.
/// </summary>
internal readonly struct FormattedResourceString<T1, T2, T3, T4> : IResourceString
{
    private readonly ResourceString _resource;
    private readonly T1 _arg0;
    private readonly T2 _arg1;
    private readonly T3 _arg2;
    private readonly T4 _arg3;

    internal FormattedResourceString(ResourceString resource, T1 arg0, T2 arg1, T3 arg2, T4 arg3)
    {
        _resource = resource;
        _arg0 = arg0;
        _arg1 = arg1;
        _arg2 = arg2;
        _arg3 = arg3;
    }

    public ResolvedResourceString Resolve()
    {
        ResolvedResourceString resolved = _resource.Resolve();
        string text = MessageFormatter.Format(resolved.Text, _arg0, _arg1, _arg2, _arg3);
        string originalText = object.ReferenceEquals(resolved.OriginalText, resolved.Text)
            ? text
            : MessageFormatter.Format(resolved.OriginalText, _arg0, _arg1, _arg2, _arg3);

        return new ResolvedResourceString(
            originalText,
            text,
            resolved.Code,
            resolved.HelpKeyword);
    }
}

/// <summary>
///  Carries five formatting arguments without allocating an argument array or boxing the carrier.
/// </summary>
internal readonly struct FormattedResourceString<T1, T2, T3, T4, T5> : IResourceString
{
    private readonly ResourceString _resource;
    private readonly T1 _arg0;
    private readonly T2 _arg1;
    private readonly T3 _arg2;
    private readonly T4 _arg3;
    private readonly T5 _arg4;

    internal FormattedResourceString(ResourceString resource, T1 arg0, T2 arg1, T3 arg2, T4 arg3, T5 arg4)
    {
        _resource = resource;
        _arg0 = arg0;
        _arg1 = arg1;
        _arg2 = arg2;
        _arg3 = arg3;
        _arg4 = arg4;
    }

    public ResolvedResourceString Resolve()
    {
        ResolvedResourceString resolved = _resource.Resolve();
        string text = MessageFormatter.Format(resolved.Text, _arg0, _arg1, _arg2, _arg3, _arg4);
        string originalText = object.ReferenceEquals(resolved.OriginalText, resolved.Text)
            ? text
            : MessageFormatter.Format(resolved.OriginalText, _arg0, _arg1, _arg2, _arg3, _arg4);

        return new ResolvedResourceString(
            originalText,
            text,
            resolved.Code,
            resolved.HelpKeyword);
    }
}

/// <summary>
///  Carries six formatting arguments without allocating an argument array or boxing the carrier.
/// </summary>
internal readonly struct FormattedResourceString<T1, T2, T3, T4, T5, T6> : IResourceString
{
    private readonly ResourceString _resource;
    private readonly T1 _arg0;
    private readonly T2 _arg1;
    private readonly T3 _arg2;
    private readonly T4 _arg3;
    private readonly T5 _arg4;
    private readonly T6 _arg5;

    internal FormattedResourceString(ResourceString resource, T1 arg0, T2 arg1, T3 arg2, T4 arg3, T5 arg4, T6 arg5)
    {
        _resource = resource;
        _arg0 = arg0;
        _arg1 = arg1;
        _arg2 = arg2;
        _arg3 = arg3;
        _arg4 = arg4;
        _arg5 = arg5;
    }

    public ResolvedResourceString Resolve()
    {
        ResolvedResourceString resolved = _resource.Resolve();
        string text = MessageFormatter.Format(resolved.Text, _arg0, _arg1, _arg2, _arg3, _arg4, _arg5);
        string originalText = object.ReferenceEquals(resolved.OriginalText, resolved.Text)
            ? text
            : MessageFormatter.Format(resolved.OriginalText, _arg0, _arg1, _arg2, _arg3, _arg4, _arg5);

        return new ResolvedResourceString(
            originalText,
            text,
            resolved.Code,
            resolved.HelpKeyword);
    }
}

/// <summary>
///  Carries seven formatting arguments without allocating an argument array or boxing the carrier.
/// </summary>
internal readonly struct FormattedResourceString<T1, T2, T3, T4, T5, T6, T7> : IResourceString
{
    private readonly ResourceString _resource;
    private readonly T1 _arg0;
    private readonly T2 _arg1;
    private readonly T3 _arg2;
    private readonly T4 _arg3;
    private readonly T5 _arg4;
    private readonly T6 _arg5;
    private readonly T7 _arg6;

    internal FormattedResourceString(ResourceString resource, T1 arg0, T2 arg1, T3 arg2, T4 arg3, T5 arg4, T6 arg5, T7 arg6)
    {
        _resource = resource;
        _arg0 = arg0;
        _arg1 = arg1;
        _arg2 = arg2;
        _arg3 = arg3;
        _arg4 = arg4;
        _arg5 = arg5;
        _arg6 = arg6;
    }

    public ResolvedResourceString Resolve()
    {
        ResolvedResourceString resolved = _resource.Resolve();
        string text = MessageFormatter.Format(resolved.Text, _arg0, _arg1, _arg2, _arg3, _arg4, _arg5, _arg6);
        string originalText = object.ReferenceEquals(resolved.OriginalText, resolved.Text)
            ? text
            : MessageFormatter.Format(resolved.OriginalText, _arg0, _arg1, _arg2, _arg3, _arg4, _arg5, _arg6);

        return new ResolvedResourceString(
            originalText,
            text,
            resolved.Code,
            resolved.HelpKeyword);
    }
}
