// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Class:       Scanner
/// This class does the scanning of the input and returns tokens.
/// The usage pattern is:
///    Scanner s = new Scanner(expression, CultureInfo)
///    do {
///      s.Advance();
///    while (s.IsNext(Token.EndOfInput));
///
///  After Advance() is called, you can get the current token (s.CurrentToken),
///  check it's type (s.IsNext()), get the string for it (s.NextString()).
/// </summary>
internal ref struct Scanner
{
    private static readonly FrozenSet<string> s_truthyValues =
        new[] { "true", "on", "yes", "!false", "!off", "!no" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> s_falseyValues =
        new[] { "false", "off", "no", "!true", "!on", "!yes" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, TokenKind> s_keywordKindMap = new Dictionary<string, TokenKind>(StringComparer.OrdinalIgnoreCase)
    {
        { "and", TokenKind.And },
        { "or", TokenKind.Or },
        { "true", TokenKind.True },
        { "false", TokenKind.False },
        { "on", TokenKind.On },
        { "off", TokenKind.Off },
        { "yes", TokenKind.Yes },
        { "no", TokenKind.No },
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly string _expression;
    private readonly ParserOptions _options;

    private int _position;
    private Token _current;

    internal bool _errorState;
    private int _errorPosition;
    private string? _errorResource;
    // What we found instead of what we were looking for
    private string? _unexpectedlyFound;

    /// <summary>
    /// Lazily format resource string to help avoid (in some perf critical cases) even loading
    /// resources at all.
    /// </summary>
    private static string EndOfInput => field ??= ResourceUtilities.GetResourceString("EndOfInputTokenName");

    //
    // Constructor takes the string to parse and the culture.
    //
    public Scanner(string expressionToParse, ParserOptions options)
    {
        // We currently have no support (and no scenarios) for disallowing property references
        // in Conditions.
        ErrorUtilities.VerifyThrow((options & ParserOptions.AllowProperties) != 0,
            "Properties should always be allowed.");

        _expression = expressionToParse;
        _position = 0;
        _current = default;
        _errorState = false;
        _errorPosition = -1; // invalid
        _options = options;
    }

    /// <summary>
    /// If the lexer errors, it has the best knowledge of the error message to show. For example,
    /// 'unexpected character' or 'illformed operator'. This method returns the name of the resource
    /// string that the parser should display.
    /// </summary>
    /// <remarks>Intentionally not a property getter to avoid the debugger triggering the Assert dialog</remarks>
    /// <returns></returns>
    internal string GetErrorResource()
    {
        if (_errorResource == null)
        {
            // I do not believe this is reachable, but provide a reasonable default.
            Debug.Assert(false, "What code path did not set an appropriate error resource? Expression: " + _expression);
            _unexpectedlyFound = EndOfInput;
            return "UnexpectedCharacterInCondition";
        }
        else
        {
            return _errorResource;
        }
    }

    public bool IsCurrent(TokenKind type)
        => _current.IsKind(type);

    public Token Current
        => _current;

    internal int GetErrorPosition()
    {
        Debug.Assert(-1 != _errorPosition); // We should have set it
        return _errorPosition;
    }

    // The string (usually a single character) we found unexpectedly.
    // We might want to show it in the error message, to help the user spot the error.
    internal string? UnexpectedlyFound => _unexpectedlyFound;

    private bool TryPeek(out char ch)
    {
        if (_position + 1 < _expression.Length)
        {
            ch = _expression[_position + 1];
            return true;
        }

        ch = default;
        return false;
    }

    /// <summary>
    /// Advance
    /// returns true on successful advance
    ///     and false on an erroneous token
    ///
    /// Doesn't return error until the bogus input is encountered.
    /// Advance() returns true even after EndOfInput is encountered.
    /// </summary>
    internal bool Advance()
    {
        if (_errorState)
        {
            return false;
        }

        if (_current.IsKind(TokenKind.EndOfInput))
        {
            return true;
        }

        SkipWhiteSpace();

        // Update error position after skipping whitespace
        _errorPosition = _position + 1;

        ReadOnlyMemory<char> expression = _expression.AsMemory(_position);

        if (expression.IsEmpty)
        {
            _current = Token.EndOfInput(_position);
        }
        else
        {
            switch (expression.Span)
            {
                case [',', ..]:
                    _current = Token.Comma(expression[..1], _position);
                    _position++;
                    break;

                case ['.', >= '0' and <= '9', ..]:
                    // Floating point number starting with a dot
                    if (TryParseNumeric())
                    {
                        return true;
                    }

                    // If we couldn't parse a number, it's an error
                    _current = Token.Unknown(expression[..1], _position);
                    _position++;
                    break;

                case ['.', ..]:
                    _current = Token.Dot(expression[..1], _position);
                    _position++;
                    break;

                case ['(', ..]:
                    _current = Token.LeftParenthesis(expression[..1], _position);
                    _position++;
                    break;

                case [')', ..]:
                    _current = Token.RightParenthesis(expression[..1], _position);
                    _position++;
                    break;

                case ['[', ..]:
                    _current = Token.LeftBracket(expression[..1], _position);
                    _position++;
                    break;

                case [']', ..]:
                    _current = Token.RightBracket(expression[..1], _position);
                    _position++;
                    break;

                case [':', ':', ..]:
                    _current = Token.DoubleColon(expression[..2], _position);
                    _position += 2;
                    break;

                case ['-', '>', ..]:
                    _current = Token.Arrow(expression[..2], _position);
                    _position += 2;
                    break;

                case ['$', ..]:
                    _current = Token.DollarSign(expression[..1], _position);
                    _position++;
                    break;

                case ['%', ..]:
                    _current = Token.PercentSign(expression[..1], _position);
                    _position++;
                    break;

                case ['@', ..]:
                    _current = Token.AtSign(expression[..1], _position);
                    _position++;
                    break;

                case ['!', '=', ..]:
                    _current = Token.NotEqualTo(expression[..2], _position);
                    _position += 2;
                    break;

                case ['!', ..]:
                    _current = Token.Not(expression[..1], _position);
                    _position++;
                    break;

                case ['>', '=', ..]:
                    _current = Token.GreaterThanOrEqualTo(expression[..2], _position);
                    _position += 2;
                    break;

                case ['>', ..]:
                    _current = Token.GreaterThan(expression[..1], _position);
                    _position++;
                    break;

                case ['<', '=', ..]:
                    _current = Token.LessThanOrEqualTo(expression[..2], _position);
                    _position += 2;
                    break;

                case ['<', ..]:
                    _current = Token.LessThan(expression[..1], _position);
                    _position++;
                    break;

                case ['=', '=', ..]:
                    _current = Token.EqualTo(expression[..2], _position);
                    _position += 2;
                    break;

                case ['=', ..]:
                    _errorPosition = _position + 2; // expression[parsePoint + 1], counting from 1
                    _errorResource = "IllFormedEqualsInCondition";
                    if ((_position + 1) < _expression.Length)
                    {
                        // store the char we found instead
                        _unexpectedlyFound = _expression[_position + 1].ToString();
                    }
                    else
                    {
                        _unexpectedlyFound = EndOfInput;
                    }

                    _position++;
                    _errorState = true;
                    return false;

                case ['\'' or '"' or '`', ..]:
                    if (!ParseQuotedString())
                    {
                        return false;
                    }

                    break;

                default:
                    if (TryParseKeywordOrIdentifier())
                    {
                        return true;
                    }

                    if (TryParseNumeric())
                    {
                        return true;
                    }

                    _current = Token.Unknown(expression[..1], _position);
                    _position++;
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Helper to verify that any AllowBuiltInMetadata or AllowCustomMetadata
    /// specifications are not respected.
    /// Returns true if it is ok, otherwise false.
    /// </summary>
    private bool CheckForUnexpectedMetadata(string expression)
    {
        if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
        {
            return true;
        }

        // Take off %(and )
        if (expression.Length > 3 && expression[0] == '%' && expression[1] == '(' && expression[expression.Length - 1] == ')')
        {
            expression = expression.Substring(2, expression.Length - 1 - 2);
        }

        // If it's like %(a.b) find 'b'
        int period = expression.IndexOf('.');
        if (period > 0 && period < expression.Length - 1)
        {
            expression = expression.Substring(period + 1);
        }

        bool isItemSpecModifier = FileUtilities.ItemSpecModifiers.IsItemSpecModifier(expression);

        if (((_options & ParserOptions.AllowBuiltInMetadata) == 0) &&
            isItemSpecModifier)
        {
            _errorPosition = _position;
            _errorState = true;
            _errorResource = "BuiltInMetadataNotAllowedInThisConditional";
            _unexpectedlyFound = expression;
            return false;
        }

        if (((_options & ParserOptions.AllowCustomMetadata) == 0) &&
            !isItemSpecModifier)
        {
            _errorPosition = _position;
            _errorState = true;
            _errorResource = "CustomMetadataNotAllowedInThisConditional";
            _unexpectedlyFound = expression;
            return false;
        }

        return true;
    }

    private bool ParseInternalItemList()
    {
        int start = _position;
        _position++;

        if (_position < _expression.Length && _expression[_position] != '(')
        {
            // @ was not followed by (
            _errorPosition = start + 1;
            _errorResource = "IllFormedItemListOpenParenthesisInCondition";
            // Not useful to set unexpectedlyFound here. The message is going to be detailed enough.
            _errorState = true;
            return false;
        }

        _position++;

        // Maybe we need to generate an error for invalid characters in itemgroup name?
        // For now, just let item evaluation handle the error.
        bool fInReplacement = false;
        int parenToClose = 0;
        while (_position < _expression.Length)
        {
            if (_expression[_position] == '\'')
            {
                fInReplacement = !fInReplacement;
            }
            else if (_expression[_position] == '(' && !fInReplacement)
            {
                parenToClose++;
            }
            else if (_expression[_position] == ')' && !fInReplacement)
            {
                if (parenToClose == 0)
                {
                    break;
                }
                else { parenToClose--; }
            }

            _position++;
        }

        if (_position >= _expression.Length)
        {
            _errorPosition = start + 1;
            if (fInReplacement)
            {
                // @( ... ' was never followed by a closing quote before the closing parenthesis
                _errorResource = "IllFormedItemListQuoteInCondition";
            }
            else
            {
                // @( was never followed by a )
                _errorResource = "IllFormedItemListCloseParenthesisInCondition";
            }

            // Not useful to set unexpectedlyFound here. The message is going to be detailed enough.
            _errorState = true;
            return false;
        }

        _position++;
        return true;
    }

    /// <summary>
    /// Parse any part of the conditional expression that is quoted. It may contain a property, item, or
    /// metadata element that needs expansion during evaluation.
    /// </summary>
    private bool ParseQuotedString()
    {
        int start = _position;
        char quoteChar = _expression[_position];
        _position++;
        int stringStart = _position;
        TokenFlags flags = TokenFlags.None;

        // Look for the MATCHING quote character
        while (_position < _expression.Length && _expression[_position] != quoteChar)
        {
            // Standalone percent-sign must be allowed within a condition because it's
            // needed to escape special characters.  However, percent-sign followed
            // by open-parenthesis is an indication of an item metadata reference, and
            // that is only allowed in certain contexts.
            if ((_expression[_position] == '%') && TryPeek(out char ch) && ch == '(')
            {
                flags |= TokenFlags.Expandable;
                string name = string.Empty;

                int endOfName = _expression.IndexOf(')', _position) - 1;
                if (endOfName < 0)
                {
                    endOfName = _expression.Length - 1;
                }

                // If it's %(a.b) the name is just 'b'
                if (_position + 3 < _expression.Length)
                {
                    name = _expression.Substring(_position + 2, endOfName - _position - 2 + 1);
                }

                if (!CheckForUnexpectedMetadata(name))
                {
                    return false;
                }
            }
            else if (_expression[_position] == '@' && TryPeek(out ch) && ch == '(')
            {
                flags |= TokenFlags.Expandable;

                // If the caller specified that he DOESN'T want to allow item lists ...
                if ((_options & ParserOptions.AllowItemLists) == 0)
                {
                    _errorPosition = stringStart + 1;
                    _errorState = true;
                    _errorResource = "ItemListNotAllowedInThisConditional";
                    return false;
                }

                // Item lists have to be parsed because of the replacement syntax e.g. @(Foo,'_').
                // I have to know how to parse those so I can skip over the tic marks.  I don't
                // have to do that with other things like propertygroups, hence itemlists are
                // treated specially.

                ParseInternalItemList();
                continue;
            }
            else if (_expression[_position] == '$' && TryPeek(out ch) && ch == '(')
            {
                flags |= TokenFlags.Expandable;
            }
            else if (_expression[_position] == '%')
            {
                // There may be some escaped characters in the expression
                flags |= TokenFlags.Expandable;
            }

            _position++;
        }

        if (_position >= _expression.Length)
        {
            // Quoted string wasn't closed
            _errorState = true;
            _errorPosition = stringStart; // The message is going to say "expected after position n" so don't add 1 here.
            _errorResource = "IllFormedQuotedStringInCondition";
            // Not useful to set unexpectedlyFound here. By definition it got to the end of the string.
            return false;
        }

        string originalTokenString = _expression.Substring(stringStart, _position - stringStart);

        if (s_truthyValues.Contains(originalTokenString))
        {
            flags |= TokenFlags.IsBooleanTrue;
        }
        else if (s_falseyValues.Contains(originalTokenString))
        {
            flags |= TokenFlags.IsBooleanFalse;
        }

        _current = Token.String(_expression.AsMemory(stringStart, _position - stringStart), flags, start);
        _position++;
        return true;
    }

    // There is a bug here that spaces are not required around 'and' and 'or'. For example,
    // this works perfectly well:
    // Condition="%(a.Identity)!=''and%(a.m)=='1'"
    // Since people now depend on this behavior, we must not change it.
    private bool TryParseKeywordOrIdentifier()
    {
        if (!TryLexIdentifier(out ReadOnlyMemory<char> identifier))
        {
            return false;
        }

        int start = _position;
        _position += identifier.Length;

        ReadOnlySpan<char> span = identifier.Span;

#if NET
        if (s_keywordKindMap.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(span, out TokenKind keywordKind))
        {
            _current = new(keywordKind, identifier, start);
            return true;
        }
#else
        foreach (KeyValuePair<string, TokenKind> kvp in s_keywordKindMap)
        {
            if (span.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                _current = new(kvp.Value, identifier, start);
                return true;
            }
        }
#endif

        _current = Token.Identifier(identifier, start);
        return true;
    }

    private readonly bool TryLexIdentifier(out ReadOnlyMemory<char> result)
    {
        ReadOnlyMemory<char> expression = _expression.AsMemory(_position);
        ReadOnlySpan<char> span = expression.Span;

        if (span.IsEmpty || !XmlUtilities.IsValidInitialElementNameCharacter(span[0]))
        {
            result = default;
            return false;
        }

        int index = 1;

        while (index < span.Length && XmlUtilities.IsValidSubsequentElementNameCharacter(span[index]))
        {
            if (span[index] == '-' && index + 1 < span.Length && span[index + 1] == '>')
            {
                // '-' is a valid "identifier" character for an XML element. However, it can also be the
                // start of an array operator. We don't want to include the '-' as part of the identifier in that case.
                break;
            }

            index++;
        }

        result = expression[..index];
        return true;
    }

    private bool TryParseNumeric()
    {
        if (TryLexHexNumber(out ReadOnlyMemory<char> number) ||
            TryLexDecimalNumber(out number))
        {
            _current = Token.Numeric(number, _position);
            _position += number.Length;
            return true;
        }

        return false;
    }

    private readonly bool TryLexHexNumber(out ReadOnlyMemory<char> result)
    {
        ReadOnlyMemory<char> expression = _expression.AsMemory(_position);
        ReadOnlySpan<char> span = expression.Span;

        if (span is not (['0', 'x' or 'X', char next, ..]) || !CharacterUtilities.IsHexDigit(next))
        {
            result = default;
            return false;
        }

        // We know we have at least one hex digit after the 0x prefix.
        int index = 3;
        while (index < span.Length && CharacterUtilities.IsHexDigit(span[index]))
        {
            index++;
        }

        result = expression[..index];
        return true;
    }

    private readonly bool TryLexDecimalNumber(out ReadOnlyMemory<char> result)
    {
        ReadOnlyMemory<char> expression = _expression.AsMemory(_position);
        ReadOnlySpan<char> span = expression.Span;

        if (span.IsEmpty || !CharacterUtilities.IsNumberStart(span[0]))
        {
            result = default;
            return false;
        }

        bool foundDigits = char.IsDigit(span[0]);

        int index = 1;

        // Skip initial digits
        while (index < span.Length && char.IsDigit(span[index]))
        {
            foundDigits = true;
            index++;
        }

        // Allow multiple dot-separated segments for version numbers like 1.2.3.4
        // Also allow trailing dots like 1.2.3.
        while (index < span.Length && span[index] == '.')
        {
            index++; // Skip the dot

            // If we immediately see another dot, stop here (include the first dot)
            // This prevents sequences like ".." from being part of the number
            if (index < span.Length && span[index] == '.')
            {
                // We have ".." - stop, but include the first dot
                break;
            }

            // Continue consuming digits (if any)
            while (index < span.Length && char.IsDigit(span[index]))
            {
                foundDigits = true;
                index++;
            }
        }

        if (!foundDigits)
        {
            result = default;
            return false;
        }

        result = expression[..index];
        return true;
    }

    private void SkipWhiteSpace()
    {
        while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
        {
            _position++;
        }
    }
}
