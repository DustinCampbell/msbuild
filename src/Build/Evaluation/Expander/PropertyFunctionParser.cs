// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
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
internal partial struct PropertyFunctionParser
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
    ///  Attempts to parse and eagerly validate a complete root property-function expression.
    /// </summary>
    /// <param name="text">The complete content inside the outer <c>$(...)</c>.</param>
    /// <param name="location">The project location used for error reporting.</param>
    /// <param name="expression">The validated expression when parsing succeeds.</param>
    /// <returns>
    ///  <see langword="true"/> when <paramref name="text"/> contains a property function; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    /// <exception cref="Exceptions.InvalidProjectFileException">
    ///  <paramref name="text"/> has recognized but invalid property-function syntax.
    /// </exception>
    public static bool TryParse(StringSegment text, IElementLocation location, out PropertyFunctionExpression expression)
    {
        ErrorReporter errors = new(text, location);
        errors.VerifyThrowInvalidFunctionPropertyExpression(!text.IsNullOrEmpty);

        PropertyFunctionParser parser = new(text, errors);
        using OneOrMany<PropertyFunctionInvocation>.Builder invocations = default;

        while (parser.TryParseNext(out PropertyFunctionInvocation invocation))
        {
            invocations.Add(invocation);
        }

        if (invocations.IsEmpty)
        {
            expression = default;
            return false;
        }

        expression = new PropertyFunctionExpression(text, invocations.ToOneOrMany());
        return true;
    }

    private bool TryParseNext(out PropertyFunctionInvocation invocation)
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

    private PropertyFunctionInvocation ParseStaticRoot()
    {
        int typeEndIndex = _text.IndexOf(']', start: 1);
        int openParenthesisIndex = _text.IndexOf('(');
        if (typeEndIndex < 1
            || (openParenthesisIndex >= 0 && typeEndIndex > openParenthesisIndex))
        {
            _errors.ThrowInvalidFunctionStaticMethodSyntax();
        }

        StringSegment typeName = _text[1..typeEndIndex];
        int memberStartIndex = typeEndIndex + 1;
        if (memberStartIndex + 2 >= _text.Length
            || _text[memberStartIndex] != ':'
            || _text[memberStartIndex + 1] != ':')
        {
            _errors.ThrowInvalidFunctionStaticMethodSyntax();
        }

        memberStartIndex += 2;
        return ParseMember(invocationStartIndex: 0, memberStartIndex, ReceiverKind.Static, typeName);
    }

    private bool TryParseMSBuildPropertyRoot(out PropertyFunctionInvocation invocation)
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
            _errors.ThrowInvalidFunctionPropertyExpression();
        }

        invocation = ParseAccessSuffix(invocationStartIndex: 0, firstAccessIndex, ReceiverKind.MSBuildProperty, propertyName);
        return true;
    }

    private readonly StringSegment GetInvocationText(int invocationStartIndex)
        => _nextInvocationStartIndex >= 0
            ? _text[invocationStartIndex.._nextInvocationStartIndex]
            : _text[invocationStartIndex..];

    private PropertyFunctionInvocation ParseAccessSuffix(int invocationStartIndex, int accessStartIndex, ReceiverKind receiverKind, StringSegment receiver)
        => _text[accessStartIndex] switch
        {
            '.' => ParseMember(invocationStartIndex, accessStartIndex + 1, receiverKind, receiver),
            '[' => ParseIndexer(invocationStartIndex, accessStartIndex, receiverKind, receiver),
            _ => Assumed.Unreachable<PropertyFunctionInvocation>(),
        };

    private PropertyFunctionInvocation ParseMember(int invocationStartIndex, int memberStartIndex, ReceiverKind receiverKind, StringSegment receiver)
    {
        StringSegment memberText = _text[memberStartIndex..];
        int openParenthesisIndex = memberText.IndexOf('(');
        int firstAccessIndex = memberText.IndexOfAny('.', '[');

        if (openParenthesisIndex >= 0
            && (firstAccessIndex < 0 || openParenthesisIndex < firstAccessIndex))
        {
            StringSegment name = memberText[..openParenthesisIndex].Trim();
            _errors.VerifyThrowInvalidFunctionPropertyExpression(!name.IsEmpty);

            int argumentsStartIndex = openParenthesisIndex + 1;
            int argumentsEndIndex = ScanForClosingParenthesis(memberText, argumentsStartIndex);

            StringSegment argumentsText = memberText[argumentsStartIndex..argumentsEndIndex];
            OneOrMany<StringSegmentRange> arguments = ParseArguments(argumentsText);

            _nextInvocationStartIndex = GetNextInvocationStartIndex(memberStartIndex + argumentsEndIndex + 1);

            return new PropertyFunctionInvocation(
                GetInvocationText(invocationStartIndex),
                receiverKind,
                receiver,
                MemberKind.Method,
                name,
                arguments);
        }

        int memberEndIndex = firstAccessIndex >= 0 ? firstAccessIndex : memberText.Length;
        StringSegment propertyOrFieldName = memberText[..memberEndIndex].Trim();
        _errors.VerifyThrowInvalidFunctionPropertyExpression(!propertyOrFieldName.IsEmpty);

        _nextInvocationStartIndex = firstAccessIndex >= 0
            ? memberStartIndex + firstAccessIndex
            : -1;

        return new PropertyFunctionInvocation(
            GetInvocationText(invocationStartIndex),
            receiverKind,
            receiver,
            MemberKind.PropertyOrField,
            propertyOrFieldName,
            arguments: default);
    }

    private PropertyFunctionInvocation ParseIndexer(int invocationStartIndex, int indexerStartIndex, ReceiverKind receiverKind, StringSegment receiver)
    {
        int indexerEndIndex = ScanForClosingSquareBracket(_text, indexerStartIndex + 1);

        StringSegment argumentsText = _text[(indexerStartIndex + 1)..indexerEndIndex];
        OneOrMany<StringSegmentRange> arguments = ParseArguments(argumentsText);

        _nextInvocationStartIndex = GetNextInvocationStartIndex(indexerEndIndex + 1);
        return new PropertyFunctionInvocation(
            GetInvocationText(invocationStartIndex),
            receiverKind,
            receiver,
            MemberKind.Indexer,
            memberName: default,
            arguments);
    }

    private readonly int GetNextInvocationStartIndex(int startIndex)
    {
        StringSegment remainder = _text[startIndex..].TrimStart();

        switch (remainder)
        {
            case []:
                return -1;

            case ['.' or '[', ..]:
                return remainder.Offset - _text.Offset;
        }

        _errors.ThrowInvalidFunctionPropertyExpression();
        return Assumed.Unreachable<int>();
    }

    private readonly int ScanForClosingParenthesis(StringSegment text, int index)
        => ScanForClosingDelimiter(text, index, openingDelimiter: '(', closingDelimiter: ')', ErrorDetail.MismatchedParenthesis);

    private readonly int ScanForClosingSquareBracket(StringSegment text, int index)
        => ScanForClosingDelimiter(text, index, openingDelimiter: '[', closingDelimiter: ']', ErrorDetail.MismatchedSquareBrackets);

    /// <summary>
    ///  Finds the closing delimiter matching the opening delimiter preceding <paramref name="index"/>.
    /// </summary>
    /// <param name="text">The expression to scan.</param>
    /// <param name="index">The index at which to begin scanning.</param>
    /// <param name="openingDelimiter">The delimiter that increases the nesting level.</param>
    /// <param name="closingDelimiter">The delimiter that decreases the nesting level.</param>
    /// <param name="mismatchedDelimiter">The error to report when the closing delimiter is missing.</param>
    /// <returns>
    ///  The index of the matching closing delimiter.
    /// </returns>
    private readonly int ScanForClosingDelimiter(
        StringSegment text,
        int index,
        char openingDelimiter,
        char closingDelimiter,
        ErrorDetail mismatchedDelimiter)
    {
        int nestLevel = 1;

        while (index < text.Length)
        {
            char ch = text[index];

            if (ch is '\'' or '`' or '"')
            {
                index = ScanForClosingQuote(text, index);
            }
            else if (ch == openingDelimiter)
            {
                nestLevel++;
            }
            else if (ch == closingDelimiter && --nestLevel == 0)
            {
                return index;
            }

            index++;
        }

        _errors.ThrowInvalidFunctionPropertyExpression(mismatchedDelimiter);
        return Assumed.Unreachable<int>();
    }

    /// <summary>
    ///  Extracts and validates the top-level arguments in an argument list.
    /// </summary>
    /// <param name="argumentText">The argument-list content without its surrounding delimiters.</param>
    /// <returns>
    ///  The validated arguments.
    /// </returns>
    private readonly OneOrMany<StringSegmentRange> ParseArguments(StringSegment argumentText)
    {
        using OneOrMany<StringSegmentRange>.Builder builder = default;
        int argumentStartIndex = 0;
        int index = 0;

        while (index < argumentText.Length)
        {
            switch (argumentText[index])
            {
                case '`' or '"' or '\'':
                    index = ScanForClosingQuote(argumentText, index);
                    break;

                case '$' when index < argumentText.Length - 1 && argumentText[index + 1] == '(':
                    index = ScanForClosingParenthesis(argumentText, index + 2);
                    break;

                case ',':
                    builder.Add(Normalize(argumentText[argumentStartIndex..index]));
                    argumentStartIndex = index + 1;
                    break;
            }

            index++;
        }

        if (argumentStartIndex < argumentText.Length)
        {
            builder.Add(Normalize(argumentText[argumentStartIndex..]));
        }

        return builder.ToOneOrMany();

        static StringSegmentRange Normalize(StringSegment argument)
        {
            argument = argument.Trim();

            if (argument.IsEmpty)
            {
                return argument;
            }

            if (argument.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return StringSegmentRange.Null;
            }

            char quoteChar = argument[0];
            if (quoteChar is '\'' or '`' or '"'
                && argument[^1] == quoteChar)
            {
                argument = argument.Trim(quoteChar);
            }

            return argument;
        }
    }

    private readonly int ScanForClosingQuote(StringSegment text, int index)
    {
        int closeQuoteIndex = text.IndexOf(text[index], index + 1);
        if (closeQuoteIndex < 0)
        {
            _errors.ThrowInvalidFunctionPropertyExpression(ErrorDetail.MismatchedQuote);
        }

        return closeQuoteIndex;
    }

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
}
