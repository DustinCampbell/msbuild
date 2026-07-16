// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Build.Framework;

/// <summary>
///  Pure string-formatting helpers relocated from the legacy <c>ResourceUtilities</c> class.
///  These perform no resource lookup; they operate on already-resolved resource strings and are
///  therefore usable from every assembly that references Microsoft.Build.Framework.
/// </summary>
internal static class ResourceHelpers
{
    /// <summary>
    ///  Formats <paramref name="unformatted"/> with the given <paramref name="args"/> using the
    ///  current culture. If no arguments are supplied, the string is returned unchanged.
    /// </summary>
    internal static string FormatString(string unformatted, params object?[]? args)
    {
        string formatted = unformatted;

        // NOTE: String.Format() does not allow a null arguments array.
        if (args?.Length > 0)
        {
            ValidateArgsIfDebug(args);

            // NOTE: all String methods are thread-safe.
            formatted = string.Format(CultureInfo.CurrentCulture, unformatted, args);
        }

        return formatted;
    }

    [Conditional("DEBUG")]
    private static void ValidateArgsIfDebug(object?[] args)
    {
        // If you accidentally pass some random type in that can't be converted to a string,
        // String.Format calls ToString() which returns the full name of the type!
        foreach (object? param in args)
        {
            if (param is not null &&
                string.Equals(param.GetType().ToString(), param.ToString(), System.StringComparison.Ordinal) &&
                param.GetType() != typeof(string))
            {
                InternalError.Throw($"Invalid resource parameter type, was {param.GetType().FullName}");
            }
        }
    }

    /// <summary>
    ///  Extracts the message code (if any) prefixed to <paramref name="message"/>.
    ///  When <paramref name="msbuildCodeOnly"/> is <see langword="true"/>, only MSBuild codes
    ///  matching <c>MSB\d\d\d\d:</c> are recognized; otherwise any <c>[A-Za-z]+\d+:</c> code is.
    /// </summary>
    /// <returns>The message with its code prefix stripped, if a code was present.</returns>
    internal static string ExtractMessageCode(bool msbuildCodeOnly, string message, out string? code)
    {
        Assumed.NotNull(message);

        code = null;
        int i = 0;

        while (i < message.Length && char.IsWhiteSpace(message[i]))
        {
            i++;
        }

        if (msbuildCodeOnly)
        {
            if (message.Length < i + 8 ||
                message[i] != 'M' ||
                message[i + 1] != 'S' ||
                message[i + 2] != 'B' ||
                message[i + 3] < '0' || message[i + 3] > '9' ||
                message[i + 4] < '0' || message[i + 4] > '9' ||
                message[i + 5] < '0' || message[i + 5] > '9' ||
                message[i + 6] < '0' || message[i + 6] > '9' ||
                message[i + 7] != ':')
            {
                return message;
            }

            code = message.Substring(i, 7);

            i += 8;
        }
        else
        {
            int j = i;
            for (; j < message.Length; j++)
            {
                char c = message[j];
                if (((c < 'a') || (c > 'z')) && ((c < 'A') || (c > 'Z')))
                {
                    break;
                }
            }

            if (j == i)
            {
                return message; // Should have been at least one letter
            }

            int k = j;

            for (; k < message.Length; k++)
            {
                char c = message[k];
                if (c < '0' || c > '9')
                {
                    break;
                }
            }

            if (k == j)
            {
                return message; // Should have been at least one digit
            }

            if (k == message.Length || message[k] != ':')
            {
                return message;
            }

            code = message.Substring(i, k - i);

            i = k + 1;
        }

        while (i < message.Length && char.IsWhiteSpace(message[i]))
        {
            i++;
        }

        if (i < message.Length)
        {
            message = message.Substring(i);
        }

        return message;
    }
}
