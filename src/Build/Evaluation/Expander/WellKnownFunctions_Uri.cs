// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class UriHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(Type receiverType, object?[] args)
            => receiverType == typeof(Uri)
                ? TryInvokeUriConstructor(args)
                : receiverType == typeof(UriBuilder)
                    ? TryInvokeUriBuilderConstructor(args)
                    : NotHandled;

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(Uri.CheckHostName), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string name] ? Invoked(Uri.CheckHostName(name)) : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.CheckSchemeName), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string name] ? Invoked(Uri.CheckSchemeName(name)) : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.Compare), StringComparison.OrdinalIgnoreCase))
            {
                return args is [Uri uri1, Uri uri2, var parts, var format, var comparison]
                    && ArgumentParser.TryConvertToEnum(parts, out UriComponents convertedParts)
                    && ArgumentParser.TryConvertToEnum(format, out UriFormat convertedFormat)
                    && ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                        ? Invoked(Uri.Compare(uri1, uri2, convertedParts, convertedFormat, convertedComparison))
                        : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.EscapeDataString), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string value] ? Invoked(Uri.EscapeDataString(value)) : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.EscapeUriString), StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable SYSLIB0013
                return args is [string value] ? Invoked(Uri.EscapeUriString(value)) : NotHandled;
#pragma warning restore SYSLIB0013
            }

            if (methodName.Equals(nameof(Uri.HexEscape), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var character] && ArgumentParser.TryConvertToChar(character, out char convertedCharacter)
                    ? Invoked(Uri.HexEscape(convertedCharacter))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.IsHexDigit), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var character] && ArgumentParser.TryConvertToChar(character, out char convertedCharacter)
                    ? Invoked(Uri.IsHexDigit(convertedCharacter))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.IsHexEncoding), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string pattern, var index] && ArgumentParser.TryConvertToInt(index, out int convertedIndex)
                    ? Invoked(Uri.IsHexEncoding(pattern, convertedIndex))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.SchemeDelimiter), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(Uri.SchemeDelimiter) : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.UnescapeDataString), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string value] ? Invoked(Uri.UnescapeDataString(value)) : NotHandled;
            }

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, object instance, object?[] args)
            => instance switch
            {
                Uri uri => TryInvokeUriInstance(methodName, uri, args),
                UriBuilder builder => TryInvokeUriBuilderInstance(methodName, builder, args),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeUriConstructor(object?[] args)
            => args switch
            {
                [string uriString] => Invoked(new Uri(uriString)),
                [string uriString, var uriKind] when ArgumentParser.TryConvertToEnum(uriKind, out UriKind convertedUriKind)
                    => Invoked(new Uri(uriString, convertedUriKind)),
                [Uri baseUri, string relativeUri] => Invoked(new Uri(baseUri, relativeUri)),
                [Uri baseUri, Uri relativeUri] => Invoked(new Uri(baseUri, relativeUri)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeUriBuilderConstructor(object?[] args)
            => args switch
            {
                [] => Invoked(new UriBuilder()),
                [string uri] => Invoked(new UriBuilder(uri)),
                [Uri uri] => Invoked(new UriBuilder(uri)),
                [string scheme, string host] => Invoked(new UriBuilder(scheme, host)),
                [string scheme, string host, var port]
                    when ArgumentParser.TryConvertToInt(port, out int convertedPort)
                    => Invoked(new UriBuilder(scheme, host, convertedPort)),
                [string scheme, string host, var port, string path]
                    when ArgumentParser.TryConvertToInt(port, out int convertedPort)
                    => Invoked(new UriBuilder(scheme, host, convertedPort, path)),
                [string scheme, string host, var port, string path, string extraValue]
                    when ArgumentParser.TryConvertToInt(port, out int convertedPort)
                    => Invoked(new UriBuilder(scheme, host, convertedPort, path, extraValue)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeUriInstance(string methodName, Uri uri, object?[] args)
        {
            if (args is [])
            {
                if (methodName.Equals(nameof(Uri.AbsolutePath), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.AbsolutePath);
                if (methodName.Equals(nameof(Uri.AbsoluteUri), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.AbsoluteUri);
                if (methodName.Equals(nameof(Uri.Authority), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.Authority);
                if (methodName.Equals(nameof(Uri.DnsSafeHost), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.DnsSafeHost);
                if (methodName.Equals(nameof(Uri.Fragment), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.Fragment);
                if (methodName.Equals(nameof(Uri.Host), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.Host);
                if (methodName.Equals(nameof(Uri.HostNameType), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.HostNameType);
#if NET
                if (methodName.Equals(nameof(Uri.IdnHost), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.IdnHost);
#endif
                if (methodName.Equals(nameof(Uri.IsAbsoluteUri), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.IsAbsoluteUri);
                if (methodName.Equals(nameof(Uri.IsDefaultPort), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.IsDefaultPort);
                if (methodName.Equals(nameof(Uri.IsFile), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.IsFile);
                if (methodName.Equals(nameof(Uri.IsLoopback), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.IsLoopback);
                if (methodName.Equals(nameof(Uri.IsUnc), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.IsUnc);
                if (methodName.Equals(nameof(Uri.LocalPath), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.LocalPath);
                if (methodName.Equals(nameof(Uri.OriginalString), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.OriginalString);
                if (methodName.Equals(nameof(Uri.PathAndQuery), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.PathAndQuery);
                if (methodName.Equals(nameof(Uri.Port), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.Port);
                if (methodName.Equals(nameof(Uri.Query), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.Query);
                if (methodName.Equals(nameof(Uri.Scheme), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.Scheme);
                if (methodName.Equals(nameof(Uri.Segments), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.Segments);
                if (methodName.Equals(nameof(Uri.UserEscaped), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.UserEscaped);
                if (methodName.Equals(nameof(Uri.UserInfo), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.UserInfo);
                if (methodName.Equals(nameof(Uri.GetHashCode), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.GetHashCode());
                if (methodName.Equals(nameof(Uri.ToString), StringComparison.OrdinalIgnoreCase)) return Invoked(uri.ToString());
            }

            if (methodName.Equals(nameof(Uri.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return args is [object other] ? Invoked(uri.Equals(other)) : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.GetComponents), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var components, var format]
                    && ArgumentParser.TryConvertToEnum(components, out UriComponents convertedComponents)
                    && ArgumentParser.TryConvertToEnum(format, out UriFormat convertedFormat)
                        ? Invoked(uri.GetComponents(convertedComponents, convertedFormat))
                        : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.IsBaseOf), StringComparison.OrdinalIgnoreCase))
            {
                return args is [Uri other] ? Invoked(uri.IsBaseOf(other)) : NotHandled;
            }

            if (methodName.Equals(nameof(Uri.MakeRelativeUri), StringComparison.OrdinalIgnoreCase))
            {
                return args is [Uri other] ? Invoked(uri.MakeRelativeUri(other)) : NotHandled;
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeUriBuilderInstance(string methodName, UriBuilder builder, object?[] args)
        {
            if (args is not [])
            {
                return methodName.Equals(nameof(UriBuilder.Equals), StringComparison.OrdinalIgnoreCase) && args is [object other]
                    ? Invoked(builder.Equals(other))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(UriBuilder.Fragment), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.Fragment);
            if (methodName.Equals(nameof(UriBuilder.Host), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.Host);
            if (methodName.Equals(nameof(UriBuilder.Password), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.Password);
            if (methodName.Equals(nameof(UriBuilder.Path), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.Path);
            if (methodName.Equals(nameof(UriBuilder.Port), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.Port);
            if (methodName.Equals(nameof(UriBuilder.Query), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.Query);
            if (methodName.Equals(nameof(UriBuilder.Scheme), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.Scheme);
            if (methodName.Equals(nameof(UriBuilder.Uri), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.Uri);
            if (methodName.Equals(nameof(UriBuilder.UserName), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.UserName);
            if (methodName.Equals(nameof(UriBuilder.GetHashCode), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.GetHashCode());
            if (methodName.Equals(nameof(UriBuilder.ToString), StringComparison.OrdinalIgnoreCase)) return Invoked(builder.ToString());

            return NotHandled;
        }
    }
}
