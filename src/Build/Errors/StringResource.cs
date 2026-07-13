// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;

namespace Microsoft.Build;

internal sealed class StringResource
{
    private readonly ResourceManager _resourceManager;
    private readonly string _resourceName;

    private readonly object _gate = new();
    private bool _loaded;

    private string? _message;
    private string? _errorCode;
    private string? _helpKeyword;

    public StringResource(ResourceManager resourceManager, string resourceName)
    {
        Assumed.NotNull(resourceManager);
        Assumed.NotNull(resourceName);

        _resourceManager = resourceManager;
        _resourceName = resourceName;
    }

    public string Message
    {
        get
        {
            LoadResourceIfNeeded();
            return _message;
        }
    }

    public string? ErrorCode
    {
        get
        {
            LoadResourceIfNeeded();
            return _errorCode;
        }
    }

    public string HelpKeyword
        => _helpKeyword ??= "MSBuild." + _resourceName;

    public string Format(object? arg0)
    {
        ValidateArg(arg0);

        return string.Format(CultureInfo.CurrentCulture, Message, arg0);
    }

    public string Format(object? arg0, object? arg1)
    {
        ValidateArg(arg0);
        ValidateArg(arg1);

        return string.Format(CultureInfo.CurrentCulture, Message, arg0, arg1);
    }

    public string Format(object? arg0, object? arg1, object? arg2)
    {
        ValidateArg(arg0);
        ValidateArg(arg1);
        ValidateArg(arg2);

        return string.Format(CultureInfo.CurrentCulture, Message, arg0, arg1, arg2);
    }

    public string Format(object?[] args)
    {
        ValidateArgs(args);

        return args is not [..]
            ? string.Format(CultureInfo.CurrentCulture, Message, args)
            : Message;
    }

    [MemberNotNull(nameof(_message))]
    private void LoadResourceIfNeeded()
    {
        if (_loaded)
        {
            Assumed.NotNull(_message);
            return;
        }

        lock (_gate)
        {
            if (_loaded)
            {
                Assumed.NotNull(_message);
                return;
            }

            string? resourceString = _resourceManager.GetString(_resourceName, CultureInfo.CurrentUICulture);

            Assumed.NotNull(resourceString, $"Missing resource '{_resourceName}'");

            _message = ExtractMessageCode(resourceString, out _errorCode);

            _loaded = true;
        }
    }

    [Conditional("DEBUG")]
    private static void ValidateArg(object? arg)
    {
        // Check it has a real implementation of ToString() and the type is not actually System.String
        if (arg != null)
        {
            if (string.Equals(arg.GetType().ToString(), arg.ToString(), StringComparison.Ordinal) &&
                arg.GetType() != typeof(string))
            {
                InternalError.Throw($"Invalid resource parameter type, was {arg.GetType().FullName}");
            }
        }
    }

    [Conditional("DEBUG")]
    private static void ValidateArgs(object?[] args)
    {
        foreach (object? arg in args)
        {
            ValidateArg(arg);
        }
    }

    /// <summary>
    ///  Extracts the message code (if any) prefixed to the given string.
    /// Thread safe.
    /// </summary>
    /// <param name="message">The string to parse.</param>
    /// <param name="code">[out] The message code, or null if there was no code.</param>
    /// <returns>
    ///  The string without its message code prefix, if any.
    /// </returns>
    private static string ExtractMessageCode(string message, out string? code)
    {
        code = null;
        int i = 0;

        // skip whitespace
        while (i < message.Length && char.IsWhiteSpace(message[i]))
        {
            i++;
        }

        if (message.Length >= i + 8 &&
            message[i] is 'M' &&
            message[i + 1] is 'S' &&
            message[i + 2] is 'B' &&
            message[i + 3] is not < '0' and not > '9' &&
            message[i + 4] is not < '0' and not > '9' &&
            message[i + 5] is not < '0' and not > '9' &&
            message[i + 6] is not < '0' and not > '9' &&
            message[i + 7] is ':')
        {
            code = message.Substring(i, 7);

            i += 8;

            // skip whitespace
            while (i < message.Length && char.IsWhiteSpace(message[i]))
            {
                i++;
            }

            if (i < message.Length)
            {
                message = message.Substring(i);
            }
        }

        return message;
    }
}
