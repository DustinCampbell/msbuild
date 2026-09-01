// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework.Utilities;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Parses complete property-function expressions using <see cref="StringSegment"/>-backed syntax.
/// </summary>
/// <remarks>
///  The input is the complete content inside the outer <c>$(...)</c>.
///  <code>
///   property-function-expression   ::= root-invocation chained-invocation*
///   root-invocation                ::= static-invocation | msbuild-property-invocation
///   static-invocation              ::= "[" type-name "]" "::" member
///   msbuild-property-invocation    ::= msbuild-property-name access
///   chained-invocation             ::= access
///   access                         ::= member-access | element-access
///   member-access                  ::= "." member
///   element-access                 ::= "[" arguments? "]"
///   member                         ::= member-name argument-list?
///   argument-list                  ::= "(" arguments? ")"
///   arguments                      ::= argument ("," argument)*
///  </code>
///  <c>msbuild-property-name</c> names the MSBuild property whose value supplies the initial
///  receiver. <c>member-name</c> names a CLR method, property, or field accessed on that receiver.
///  Quoted segments and nested <c>$(...)</c> expressions are treated atomically while splitting
///  arguments, so commas inside them do not delimit arguments.
/// </remarks>
internal struct PropertyFunctionParser
{
    private readonly StringSegment _text;
    private readonly ErrorReporter _errors;
    private int _nextInvocationStartIndex;
    private bool _parsedRoot;

    private PropertyFunctionParser(StringSegment text, ErrorReporter errors)
    {
        _text = text;
        _errors = errors;
        _nextInvocationStartIndex = -1;
    }

    /// <summary>
    ///  Attempts to parse and eagerly validate a complete property function.
    /// </summary>
    /// <param name="text">The complete content inside the outer <c>$(...)</c>.</param>
    /// <param name="location">The project location used for error reporting.</param>
    /// <param name="expression">The validated property function when parsing succeeds.</param>
    /// <returns>
    ///  <see langword="true"/> when <paramref name="text"/> contains a property function; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    /// <exception cref="Exceptions.InvalidProjectFileException">
    ///  <paramref name="text"/> has recognized but invalid property-function syntax.
    /// </exception>
    public static bool TryParse(StringSegment text, IElementLocation location, out PropertyFunction expression)
    {
        ErrorReporter errors = new(location);
        errors.InvalidPropertyFunction.ThrowIfFalse(!text.IsNullOrEmpty, text);

        PropertyFunctionParser parser = new(text, errors);
        if (!parser.TryParseNext(out Invocation invocation))
        {
            expression = default;
            return false;
        }

        if (parser._nextInvocationStartIndex >= 0)
        {
            return ParseMultipleInvocations(ref parser, text, invocation, out expression);
        }

        expression = new PropertyFunction(text, new OneOrMany<Invocation>(invocation));
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ParseMultipleInvocations(
        ref PropertyFunctionParser parser,
        StringSegment text,
        Invocation firstInvocation,
        out PropertyFunction expression)
    {
        using OneOrMany<Invocation>.Builder invocations = default;
        invocations.Add(firstInvocation);

        while (parser.TryParseNext(out Invocation invocation))
        {
            invocations.Add(invocation);
        }

        expression = new PropertyFunction(text, invocations.ToOneOrMany());
        return true;
    }

    /// <summary>
    ///  Parses a standalone function argument list.
    /// </summary>
    /// <param name="text">The argument-list content without surrounding delimiters.</param>
    /// <param name="expression">The complete containing expression used for diagnostics.</param>
    /// <param name="location">The project location used for diagnostics.</param>
    /// <returns>
    ///  The parsed arguments.
    /// </returns>
    public static ArgumentList ParseArguments(StringSegment text, StringSegment expression, IElementLocation location)
    {
        if (text.IsNullOrEmpty)
        {
            return ArgumentList.Empty;
        }

        PropertyFunctionParser parser = new(expression, new ErrorReporter(location));
        return new ArgumentList(text.Buffer, parser.ParseArguments(text));
    }

    private bool TryParseNext(out Invocation invocation)
    {
        if (!_parsedRoot)
        {
            _parsedRoot = true;

            if (_text[0] == '[')
            {
                invocation = ParseStaticRoot();
                return true;
            }

            return TryParseMSBuildPropertyRoot(out invocation);
        }

        if (_nextInvocationStartIndex < 0)
        {
            invocation = default;
            return false;
        }

        int accessStartIndex = _nextInvocationStartIndex;
        invocation = ParseAccessSuffix(
            invocationStartIndex: accessStartIndex,
            accessStartIndex,
            ReceiverKind.Chained,
            receiver: default);
        return true;
    }

    private Invocation ParseStaticRoot()
    {
        int typeEndIndex = _text.IndexOf(']', start: 1);
        int openParenthesisIndex = _text.IndexOf('(');
        if (typeEndIndex < 1
            || (openParenthesisIndex >= 0 && typeEndIndex > openParenthesisIndex))
        {
            _errors.InvalidStaticPropertyFunction.Throw(_text);
        }

        StringSegment typeName = _text[1..typeEndIndex];
        int memberStartIndex = typeEndIndex + 1;
        if (memberStartIndex + 2 >= _text.Length
            || _text[memberStartIndex] != ':'
            || _text[memberStartIndex + 1] != ':')
        {
            _errors.InvalidStaticPropertyFunction.Throw(_text);
        }

        memberStartIndex += 2;
        return ParseMember(invocationStartIndex: 0, memberStartIndex, ReceiverKind.Static, typeName);
    }

    private bool TryParseMSBuildPropertyRoot(out Invocation invocation)
    {
        int firstAccessIndex = _text.IndexOfAny('.', '[');
        if (firstAccessIndex < 0)
        {
            invocation = default;
            return false;
        }

        StringSegment propertyName = _text[..firstAccessIndex].Trim();
        if (!IsValidPropertyName(propertyName))
        {
            _errors.InvalidPropertyFunction.Throw(_text);
        }

        invocation = ParseAccessSuffix(invocationStartIndex: 0, firstAccessIndex, ReceiverKind.MSBuildProperty, propertyName);
        return true;
    }

    private readonly StringSegment GetInvocationText(int invocationStartIndex)
        => _nextInvocationStartIndex >= 0
            ? _text[invocationStartIndex.._nextInvocationStartIndex]
            : _text[invocationStartIndex..];

    private Invocation ParseAccessSuffix(int invocationStartIndex, int accessStartIndex, ReceiverKind receiverKind, StringSegment receiver)
        => _text[accessStartIndex] switch
        {
            '.' => ParseMember(invocationStartIndex, accessStartIndex + 1, receiverKind, receiver),
            '[' => ParseIndexer(invocationStartIndex, accessStartIndex, receiverKind, receiver),
            _ => Assumed.Unreachable<Invocation>(),
        };

    private Invocation ParseMember(int invocationStartIndex, int memberStartIndex, ReceiverKind receiverKind, StringSegment receiver)
    {
        StringSegment memberText = _text[memberStartIndex..];
        int firstDelimiterIndex = memberText.IndexOfAny('(', '.', '[');

        if (firstDelimiterIndex >= 0 && memberText[firstDelimiterIndex] == '(')
        {
            StringSegment name = memberText[..firstDelimiterIndex].Trim();
            _errors.InvalidPropertyFunction.ThrowIfFalse(!name.IsEmpty, _text);
            VerifyMemberName(name, receiverKind);

            OneOrMany<Argument> arguments = ParseParenthesizedArguments(memberText, firstDelimiterIndex + 1, out int argumentsEndIndex);

            _nextInvocationStartIndex = GetNextInvocationStartIndex(memberStartIndex + argumentsEndIndex + 1);

            return new Invocation(
                GetInvocationText(invocationStartIndex),
                receiverKind,
                receiver,
                MemberKind.Method,
                name,
                arguments);
        }

        StringSegment propertyOrFieldName = firstDelimiterIndex >= 0
            ? memberText[..firstDelimiterIndex].Trim()
            : memberText.Trim();

        _errors.InvalidPropertyFunction.ThrowIfFalse(!propertyOrFieldName.IsEmpty, _text);
        VerifyMemberName(propertyOrFieldName, receiverKind);

        _nextInvocationStartIndex = firstDelimiterIndex >= 0
            ? memberStartIndex + firstDelimiterIndex
            : -1;

        return new Invocation(
            GetInvocationText(invocationStartIndex),
            receiverKind,
            receiver,
            MemberKind.PropertyOrField,
            propertyOrFieldName,
            arguments: default);
    }

    private Invocation ParseIndexer(int invocationStartIndex, int indexerStartIndex, ReceiverKind receiverKind, StringSegment receiver)
    {
        OneOrMany<Argument> arguments = ParseBracketedArguments(_text, indexerStartIndex + 1, out int indexerEndIndex);

        _nextInvocationStartIndex = GetNextInvocationStartIndex(indexerEndIndex + 1);
        return new Invocation(
            GetInvocationText(invocationStartIndex),
            receiverKind,
            receiver,
            MemberKind.Indexer,
            memberName: default,
            arguments);
    }

    private readonly int GetNextInvocationStartIndex(int startIndex)
    {
        if (startIndex == _text.Length)
        {
            return -1;
        }

        StringSegment remainder = _text[startIndex..].TrimStart();

        switch (remainder)
        {
            case []:
                return -1;

            case ['.' or '[', ..]:
                return remainder.Offset - _text.Offset;
        }

        _errors.InvalidPropertyFunction.Throw(_text);
        return Assumed.Unreachable<int>();
    }

    /// <summary>
    ///  Finds the closing parenthesis for a nested property expression while collecting argument flags.
    /// </summary>
    /// <param name="text">The text containing the nested property expression.</param>
    /// <param name="index">The index after the opening parenthesis.</param>
    /// <param name="flags">The flags already detected for the containing argument.</param>
    /// <returns>
    ///  The closing-parenthesis index and the accumulated flags.
    /// </returns>
    private readonly (int Index, ArgumentFlags Flags) ScanForClosingParenthesis(StringSegment text, int index, ArgumentFlags flags)
    {
        int nestLevel = 1;

        while (index < text.Length)
        {
            char ch = text[index];

            switch (ch)
            {
                case '\'' or '`' or '"':
                    (index, flags) = ScanForClosingQuote(text, index + 1, ch, flags);
                    break;

                case '(':
                    nestLevel++;
                    break;

                case ')' when --nestLevel == 0:
                    return (index, flags);

                default:
                    if (flags != ArgumentFlags.All)
                    {
                        flags = UpdateFlags(text, index, ch, flags);
                    }

                    break;
            }

            index++;
        }

        _errors.InvalidPropertyFunction.Throw(_text, ErrorDetail.MismatchedParenthesis);
        return Assumed.Unreachable<(int, ArgumentFlags)>();
    }

    /// <summary>
    ///  Extracts and validates arguments from the complete <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The argument-list content without its surrounding delimiters.</param>
    /// <returns>
    ///  The validated arguments.
    /// </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly OneOrMany<Argument> ParseArguments(StringSegment text)
    {
        using OneOrMany<Argument>.Builder builder = default;
        int argumentStartIndex = 0;
        int index = 0;
        ArgumentFlags flags = 0;

        while (index < text.Length)
        {
            char ch = text[index];

            switch (ch)
            {
                case '`' or '"' or '\'':
                    (index, flags) = ScanForClosingQuote(text, index + 1, ch, flags);
                    break;

                case '$' when index < text.Length - 1 && text[index + 1] == '(':
                    (index, flags) = ScanForClosingParenthesis(text, index + 2, flags | ArgumentFlags.ExpandProperties);
                    break;

                case ',':
                    builder.Add(Normalize(text[argumentStartIndex..index], flags));
                    argumentStartIndex = index + 1;
                    flags = 0;
                    break;

                default:
                    if (flags != ArgumentFlags.All)
                    {
                        flags = UpdateFlags(text, index, ch, flags);
                    }

                    break;
            }

            index++;
        }

        if (argumentStartIndex < text.Length)
        {
            builder.Add(Normalize(text[argumentStartIndex..], flags));
        }

        return builder.ToOneOrMany();
    }

    /// <summary>
    ///  Parses a parenthesized argument list.
    /// </summary>
    /// <param name="text">The text containing the argument list.</param>
    /// <param name="startIndex">The index after the opening parenthesis.</param>
    /// <param name="closingParenthesisIndex">The index of the matching closing parenthesis.</param>
    /// <returns>
    ///  The validated arguments.
    /// </returns>
    private readonly OneOrMany<Argument> ParseParenthesizedArguments(
        StringSegment text,
        int startIndex,
        out int closingParenthesisIndex)
        => ParseArgumentsUntilClosingDelimiter(
            text,
            startIndex,
            openingDelimiter: '(',
            closingDelimiter: ')',
            ErrorDetail.MismatchedParenthesis,
            out closingParenthesisIndex);

    /// <summary>
    ///  Parses a bracketed argument list.
    /// </summary>
    /// <param name="text">The text containing the argument list.</param>
    /// <param name="startIndex">The index after the opening square bracket.</param>
    /// <param name="closingBracketIndex">The index of the matching closing square bracket.</param>
    /// <returns>
    ///  The validated arguments.
    /// </returns>
    private readonly OneOrMany<Argument> ParseBracketedArguments(
        StringSegment text,
        int startIndex,
        out int closingBracketIndex)
        => ParseArgumentsUntilClosingDelimiter(
            text,
            startIndex,
            openingDelimiter: '[',
            closingDelimiter: ']',
            ErrorDetail.MismatchedSquareBrackets,
            out closingBracketIndex);

    /// <summary>
    ///  Parses arguments while locating the closing delimiter matching the delimiter before
    ///  <paramref name="startIndex"/>.
    /// </summary>
    /// <param name="text">The text containing the argument list and closing delimiter.</param>
    /// <param name="startIndex">The index of the first argument character.</param>
    /// <param name="openingDelimiter">The delimiter that increases the nesting level.</param>
    /// <param name="closingDelimiter">The delimiter that decreases the nesting level.</param>
    /// <param name="mismatchedDelimiter">The detail reported when the closing delimiter is missing.</param>
    /// <param name="closingDelimiterIndex">The index of the matching closing delimiter.</param>
    /// <returns>
    ///  The validated arguments.
    /// </returns>
    /// <remarks>
    ///  For compatibility, commas split arguments at any nesting level unless they occur inside a quoted string
    ///  or nested property expression.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly OneOrMany<Argument> ParseArgumentsUntilClosingDelimiter(
        StringSegment text,
        int startIndex,
        char openingDelimiter,
        char closingDelimiter,
        ErrorDetail mismatchedDelimiter,
        out int closingDelimiterIndex)
    {
        using OneOrMany<Argument>.Builder builder = default;
        closingDelimiterIndex = text.Length;
        int argumentStartIndex = startIndex;
        int index = startIndex;
        int nestLevel = 1;
        ArgumentFlags flags = 0;

        while (index < text.Length)
        {
            char ch = text[index];

            switch (ch)
            {
                case '`' or '"' or '\'':
                    (index, flags) = ScanForClosingQuote(text, index + 1, ch, flags);
                    break;

                case '$' when index < text.Length - 1 && text[index + 1] == '(':
                    (index, flags) = ScanForClosingParenthesis(text, index + 2, flags | ArgumentFlags.ExpandProperties);
                    break;

                case ',':
                    builder.Add(Normalize(text[argumentStartIndex..index], flags));
                    argumentStartIndex = index + 1;
                    flags = 0;
                    break;

                default:
                    if (ch == openingDelimiter)
                    {
                        nestLevel++;
                    }
                    else if (ch == closingDelimiter && --nestLevel == 0)
                    {
                        if (argumentStartIndex < index)
                        {
                            builder.Add(Normalize(text[argumentStartIndex..index], flags));
                        }

                        closingDelimiterIndex = index;
                        return builder.ToOneOrMany();
                    }

                    if (flags != ArgumentFlags.All)
                    {
                        flags = UpdateFlags(text, index, ch, flags);
                    }

                    break;
            }

            index++;
        }

        _errors.InvalidPropertyFunction.Throw(_text, mismatchedDelimiter);
        return Assumed.Unreachable<OneOrMany<Argument>>();
    }

    /// <summary>
    ///  Normalizes one argument into a source-backed range.
    /// </summary>
    /// <param name="argument">The argument source.</param>
    /// <param name="flags">The argument's processing flags.</param>
    /// <returns>
    ///  The normalized argument.
    /// </returns>
    private static Argument Normalize(StringSegment argument, ArgumentFlags flags)
    {
        argument = argument.Trim();

        if (argument.IsEmpty)
        {
            return Argument.Empty(argument);
        }

        if (argument.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return Argument.Null;
        }

        char quoteChar = argument[0];
        if (quoteChar is '\'' or '`' or '"'
            && argument[^1] == quoteChar)
        {
            argument = argument.Trim(quoteChar);
        }

        return new Argument(argument, flags);
    }

    /// <summary>
    ///  Finds a closing quote without collecting argument flags.
    /// </summary>
    /// <param name="text">The text containing the quoted value.</param>
    /// <param name="startIndex">The index after the opening quote.</param>
    /// <param name="quote">The quote character.</param>
    /// <returns>
    ///  The closing-quote index.
    /// </returns>
    private readonly int ScanForClosingQuote(StringSegment text, int startIndex, char quote)
    {
        int quoteIndex = text.IndexOf(quote, startIndex);

        if (quoteIndex >= 0)
        {
            return quoteIndex;
        }

        _errors.InvalidPropertyFunction.Throw(_text, ErrorDetail.MismatchedQuote);
        return Assumed.Unreachable<int>();
    }

    /// <summary>
    ///  Finds a closing quote while collecting argument flags.
    /// </summary>
    /// <param name="text">The text containing the quoted value.</param>
    /// <param name="startIndex">The index after the opening quote.</param>
    /// <param name="quote">The quote character.</param>
    /// <param name="flags">The flags already detected for the containing argument.</param>
    /// <returns>
    ///  The closing-quote index and the accumulated flags.
    /// </returns>
    private readonly (int Index, ArgumentFlags Flags) ScanForClosingQuote(StringSegment text, int startIndex, char quote, ArgumentFlags flags)
    {
        if (flags != ArgumentFlags.All)
        {
#if NET
            Span<char> chars = [quote, '$', '%'];
#else
            Span<char> chars = stackalloc char[3] { quote, '$', '%' };
#endif

            do
            {
                int nextIndex = text.IndexOfAny(chars, startIndex);
                if (nextIndex < 0)
                {
                    _errors.InvalidPropertyFunction.Throw(_text, ErrorDetail.MismatchedQuote);
                    return Assumed.Unreachable<(int, ArgumentFlags)>();
                }

                char ch = text[nextIndex];
                if (ch == quote)
                {
                    return (nextIndex, flags);
                }

                flags = UpdateFlags(text, nextIndex, ch, flags);
                startIndex = nextIndex + 1;
            }
            while (flags != ArgumentFlags.All);
        }

        return (ScanForClosingQuote(text, startIndex, quote), ArgumentFlags.All);
    }

    /// <summary>
    ///  Updates the processing flags for the character at <paramref name="index"/>.
    /// </summary>
    /// <param name="text">The argument source.</param>
    /// <param name="index">The index of <paramref name="ch"/>.</param>
    /// <param name="ch">The character to inspect.</param>
    /// <param name="flags">The flags already detected for the argument.</param>
    /// <returns>
    ///  The updated flags.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArgumentFlags UpdateFlags(StringSegment text, int index, char ch, ArgumentFlags flags)
        => ch switch
        {
            '$' when (flags & ArgumentFlags.ExpandProperties) == 0
                    && index < text.Length - 1
                    && text[index + 1] == '('
                => flags | ArgumentFlags.ExpandProperties,

            '%' when (flags & ArgumentFlags.Unescape) == 0
                    && index < text.Length - 2
                    && HexConverter.IsHexChar(text[index + 1])
                    && HexConverter.IsHexChar(text[index + 2])
                => flags | ArgumentFlags.Unescape,

            _ => flags,
        };

    private static bool IsValidPropertyName(StringSegment propertyName)
    {
        if (propertyName.IsEmpty
            || !XmlUtilities.IsValidInitialElementNameCharacter(propertyName[0]))
        {
            return false;
        }

        for (int i = 1; i < propertyName.Length; i++)
        {
            if (!XmlUtilities.IsValidSubsequentElementNameCharacter(propertyName[i]))
            {
                return false;
            }
        }

        return true;
    }

    private readonly void VerifyMemberName(StringSegment memberName, ReceiverKind receiverKind)
    {
        if (receiverKind != ReceiverKind.Static && memberName.Contains("::"))
        {
            _errors.InvalidStaticPropertyFunction.Throw(_text);
        }
    }
}
