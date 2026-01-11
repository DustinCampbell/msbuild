// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
internal sealed class Scanner
{
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
        _current = Token.None;
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

        if (_current?.IsKind(TokenKind.EndOfInput) == true)
        {
            return true;
        }

        SkipWhiteSpace();

        // Update error position after skipping whitespace
        _errorPosition = _position + 1;

        if (_position >= _expression.Length)
        {
            _current = Token.EndOfInput;
        }
        else
        {
            switch (_expression[_position])
            {
                case ',':
                    _current = Token.Comma;
                    _position++;
                    break;

                case '(':
                    _current = Token.LeftParenthesis;
                    _position++;
                    break;

                case ')':
                    _current = Token.RightParenthesis;
                    _position++;
                    break;

                case '$':
                    if (!ParseProperty())
                    {
                        return false;
                    }

                    break;

                case '%':
                    if (!ParseItemMetadata())
                    {
                        return false;
                    }

                    break;

                case '@':
                    int start = _position;
                    // If the caller specified that he DOESN'T want to allow item lists ...
                    if ((_options & ParserOptions.AllowItemLists) == 0)
                    {
                        if ((_position + 1) < _expression.Length && _expression[_position + 1] == '(')
                        {
                            _errorPosition = start + 1;
                            _errorState = true;
                            _errorResource = "ItemListNotAllowedInThisConditional";
                            return false;
                        }
                    }

                    if (!ParseItemList())
                    {
                        return false;
                    }

                    break;

                case '!':
                    // negation and not-equal
                    if ((_position + 1) < _expression.Length && _expression[_position + 1] == '=')
                    {
                        _current = Token.NotEqualTo;
                        _position += 2;
                    }
                    else
                    {
                        _current = Token.Not;
                        _position++;
                    }

                    break;

                case '>':
                    // gt and gte
                    if ((_position + 1) < _expression.Length && _expression[_position + 1] == '=')
                    {
                        _current = Token.GreaterThanOrEqualTo;
                        _position += 2;
                    }
                    else
                    {
                        _current = Token.GreaterThan;
                        _position++;
                    }

                    break;

                case '<':
                    // lt and lte
                    if ((_position + 1) < _expression.Length && _expression[_position + 1] == '=')
                    {
                        _current = Token.LessThanOrEqualTo;
                        _position += 2;
                    }
                    else
                    {
                        _current = Token.LessThan;
                        _position++;
                    }

                    break;

                case '=':
                    if ((_position + 1) < _expression.Length && _expression[_position + 1] == '=')
                    {
                        _current = Token.EqualTo;
                        _position += 2;
                    }
                    else
                    {
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
                    }

                    break;

                case '\'':
                    if (!ParseQuotedString())
                    {
                        return false;
                    }

                    break;

                default:
                    // Simple strings, function calls, decimal numbers, hex numbers
                    if (!ParseRemaining())
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses either the $(propertyname) syntax or the %(metadataname) syntax,
    /// and returns the parsed string beginning with the '$' or '%', and ending with the
    /// closing parenthesis.
    /// </summary>
    /// <returns></returns>
    private string? ParsePropertyOrItemMetadata()
    {
        int start = _position; // set start so that we include "$(" or "%("
        _position++;

        if (_position < _expression.Length && _expression[_position] != '(')
        {
            _errorState = true;
            _errorPosition = start + 1;
            _errorResource = "IllFormedPropertyOpenParenthesisInCondition";
            _unexpectedlyFound = _expression[_position].ToString();
            return null;
        }

        if (!ScanForPropertyExpressionEnd(_expression, _position++, out int indexResult))
        {
            _errorState = true;
            _errorPosition = indexResult;
            _errorResource = "IllFormedPropertySpaceInCondition";
            _unexpectedlyFound = _expression[indexResult].ToString();
            return null;
        }

        _position = indexResult;

        // Maybe we need to generate an error for invalid characters in property/metadata name?
        // For now, just wait and let the property/metadata evaluation handle the error case.
        if (_position >= _expression.Length)
        {
            _errorState = true;
            _errorPosition = start + 1;
            _errorResource = "IllFormedPropertyCloseParenthesisInCondition";
            _unexpectedlyFound = EndOfInput;
            return null;
        }

        _position++;
        return _expression.Substring(start, _position - start);
    }

    /// <summary>
    /// Scan for the end of the property expression
    /// </summary>
    /// <param name="expression">property expression to parse</param>
    /// <param name="index">current index to start from</param>
    /// <param name="indexResult">If successful, the index corresponds to the end of the property expression.
    /// In case of scan failure, it is the error position index.</param>
    /// <returns>result indicating whether or not the scan was successful.</returns>
    private static bool ScanForPropertyExpressionEnd(string expression, int index, out int indexResult)
    {
        int nestLevel = 0;
        bool whitespaceFound = false;
        bool nonIdentifierCharacterFound = false;
        indexResult = -1;
        unsafe
        {
            fixed (char* pchar = expression)
            {
                while (index < expression.Length)
                {
                    char character = pchar[index];
                    if (character == '(')
                    {
                        nestLevel++;
                    }
                    else if (character == ')')
                    {
                        nestLevel--;
                    }
                    else if (char.IsWhiteSpace(character))
                    {
                        whitespaceFound = true;
                        indexResult = index;
                    }
                    else if (!XmlUtilities.IsValidSubsequentElementNameCharacter(character))
                    {
                        nonIdentifierCharacterFound = true;
                    }

                    if (character == '$' && index < expression.Length - 1 && pchar[index + 1] == '(')
                    {
                        if (!ScanForPropertyExpressionEnd(expression, index + 1, out index))
                        {
                            indexResult = index;
                            return false;
                        }
                    }

                    // We have reached the end of the parenthesis nesting
                    // this should be the end of the property expression
                    // If it is not then the calling code will determine that
                    if (nestLevel == 0)
                    {
                        if (whitespaceFound && !nonIdentifierCharacterFound)
                        {
                            return false;
                        }

                        indexResult = index;
                        return true;
                    }
                    else
                    {
                        index++;
                    }
                }
            }
        }

        indexResult = index;
        return true;
    }

    /// <summary>
    /// Parses a string of the form $(propertyname).
    /// </summary>
    /// <returns></returns>
    private bool ParseProperty()
    {
        string? propertyExpression = ParsePropertyOrItemMetadata();

        if (propertyExpression == null)
        {
            return false;
        }
        else
        {
            _current = Token.Property(propertyExpression);
            return true;
        }
    }

    /// <summary>
    /// Parses a string of the form %(itemmetadataname).
    /// </summary>
    /// <returns></returns>
    private bool ParseItemMetadata()
    {
        string? itemMetadataExpression = ParsePropertyOrItemMetadata();

        if (itemMetadataExpression == null)
        {
            // The ParsePropertyOrItemMetadata method returns the correct error resources
            // for parsing properties such as $(propertyname).  At this stage in the Whidbey
            // cycle, we're not allowed to add new string resources, so I can't add a new
            // resource specific to item metadata, so here, we just change the error to
            // the generic "UnexpectedCharacter".
            _errorResource = "UnexpectedCharacterInCondition";
            return false;
        }

        _current = Token.ItemMetadata(itemMetadataExpression);

        return CheckForUnexpectedMetadata(itemMetadataExpression);
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

        // Take off %( and )
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

    private bool ParseItemList()
    {
        int start = _position;
        if (!ParseInternalItemList())
        {
            return false;
        }

        _current = Token.ItemList(_expression.Substring(start, _position - start));
        return true;
    }

    /// <summary>
    /// Parse any part of the conditional expression that is quoted. It may contain a property, item, or
    /// metadata element that needs expansion during evaluation.
    /// </summary>
    private bool ParseQuotedString()
    {
        _position++;
        int start = _position;
        bool expandable = false;
        while (_position < _expression.Length && _expression[_position] != '\'')
        {
            // Standalone percent-sign must be allowed within a condition because it's
            // needed to escape special characters.  However, percent-sign followed
            // by open-parenthesis is an indication of an item metadata reference, and
            // that is only allowed in certain contexts.
            if ((_expression[_position] == '%') && ((_position + 1) < _expression.Length) && (_expression[_position + 1] == '('))
            {
                expandable = true;
                string name = String.Empty;

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
            else if (_expression[_position] == '@' && ((_position + 1) < _expression.Length) && (_expression[_position + 1] == '('))
            {
                expandable = true;

                // If the caller specified that he DOESN'T want to allow item lists ...
                if ((_options & ParserOptions.AllowItemLists) == 0)
                {
                    _errorPosition = start + 1;
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
            else if (_expression[_position] == '$' && ((_position + 1) < _expression.Length) && (_expression[_position + 1] == '('))
            {
                expandable = true;
            }
            else if (_expression[_position] == '%')
            {
                // There may be some escaped characters in the expression
                expandable = true;
            }

            _position++;
        }

        if (_position >= _expression.Length)
        {
            // Quoted string wasn't closed
            _errorState = true;
            _errorPosition = start; // The message is going to say "expected after position n" so don't add 1 here.
            _errorResource = "IllFormedQuotedStringInCondition";
            // Not useful to set unexpectedlyFound here. By definition it got to the end of the string.
            return false;
        }

        string originalTokenString = _expression.Substring(start, _position - start);

        _current = Token.String(originalTokenString, expandable);
        _position++;
        return true;
    }

    private bool ParseRemaining()
    {
        int start = _position;
        if (CharacterUtilities.IsNumberStart(_expression[_position])) // numeric
        {
            if (!ParseNumeric(start))
            {
                return false;
            }
        }
        else if (CharacterUtilities.IsSimpleStringStart(_expression[_position])) // simple string (handle 'and' and 'or')
        {
            if (!ParseSimpleStringOrFunction(start))
            {
                return false;
            }
        }
        else
        {
            // Something that wasn't a number or a letter, like a newline (%0a)
            _errorState = true;
            _errorPosition = start + 1;
            _errorResource = "UnexpectedCharacterInCondition";
            _unexpectedlyFound = _expression[_position].ToString();
            return false;
        }

        return true;
    }

    // There is a bug here that spaces are not required around 'and' and 'or'. For example,
    // this works perfectly well:
    // Condition="%(a.Identity)!=''and%(a.m)=='1'"
    // Since people now depend on this behavior, we must not change it.
    private bool ParseSimpleStringOrFunction(int start)
    {
        SkipSimpleStringChars();

        if (_expression.AsSpan(start, _position - start).Equals("and".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            _current = Token.And;
        }
        else if (_expression.AsSpan(start, _position - start).Equals("or".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            _current = Token.Or;
        }
        else
        {
            int end = _position;

            SkipWhiteSpace();

            if (_position < _expression.Length && _expression[_position] == '(')
            {
                _current = Token.Function(_expression.Substring(start, end - start));
            }
            else
            {
                string tokenValue = _expression.Substring(start, end - start);
                _current = Token.String(tokenValue);
            }
        }

        return true;
    }

    private bool ParseNumeric(int start)
    {
        if ((_expression.Length - _position) > 2 && _expression[_position] == '0' && (_expression[_position + 1] == 'x' || _expression[_position + 1] == 'X'))
        {
            // Hex number
            _position += 2;
            SkipHexDigits();
            _current = Token.Numeric(_expression.Substring(start, _position - start));
        }
        else if (CharacterUtilities.IsNumberStart(_expression[_position]))
        {
            // Decimal number
            if (_expression[_position] == '+')
            {
                _position++;
            }
            else if (_expression[_position] == '-')
            {
                _position++;
            }

            do
            {
                SkipDigits();
                if (_position < _expression.Length && _expression[_position] == '.')
                {
                    _position++;
                }
                if (_position < _expression.Length)
                {
                    SkipDigits();
                }
            }
            while (_position < _expression.Length && _expression[_position] == '.');

            // Do we need to error on malformed input like 0.00.00)? or will the conversion handle it?
            // For now, let the conversion generate the error.
            _current = Token.Numeric(_expression.Substring(start, _position - start));
        }
        else
        {
            // Unreachable
            _errorState = true;
            _errorPosition = start + 1;
            return false;
        }

        return true;
    }

    private void SkipWhiteSpace()
    {
        while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipDigits()
    {
        while (_position < _expression.Length && char.IsDigit(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipHexDigits()
    {
        while (_position < _expression.Length && CharacterUtilities.IsHexDigit(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipSimpleStringChars()
    {
        while (_position < _expression.Length && CharacterUtilities.IsSimpleStringChar(_expression[_position]))
        {
            _position++;
        }
    }
}
