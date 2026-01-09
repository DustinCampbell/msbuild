// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
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
        private string _expression;
        private int _parsePoint;
        private Token _lookahead;
        internal bool _errorState;
        private int _errorPosition;
        // What we found instead of what we were looking for
        private string _unexpectedlyFound = null;
        private ParserOptions _options;
        private string _errorResource = null;

        //
        // Constructor takes the string to parse and the culture.
        //
        internal Scanner(string expressionToParse, ParserOptions options)
        {
            // We currently have no support (and no scenarios) for disallowing property references
            // in Conditions.
            ErrorUtilities.VerifyThrow(0 != (options & ParserOptions.AllowProperties),
                "Properties should always be allowed.");

            _expression = expressionToParse;
            _parsePoint = 0;
            _errorState = false;
            _errorPosition = -1; // invalid
            _options = options;
        }

        private void SetError(int position, string resource, string unexpectedlyFound = null)
        {
            Debug.Assert(!_errorState, "Error state should not already be set when calling SetError.");

            _errorState = true;
            _errorPosition = position;
            _errorResource = resource;
            _unexpectedlyFound = unexpectedlyFound;
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
                _unexpectedlyFound = ConditionErrors.EndOfInputTokenName;
                return ConditionErrors.UnexpectedCharacter;
            }
            else
            {
                return _errorResource;
            }
        }

        internal bool IsNext(TokenKind type)
        {
            return _lookahead.IsKind(type);
        }

        internal string IsNextString()
        {
            return _lookahead.Text;
        }

        internal Token CurrentToken
        {
            get { return _lookahead; }
        }

        internal int GetErrorPosition()
        {
            Debug.Assert(-1 != _errorPosition); // We should have set it
            return _errorPosition;
        }

        // The string (usually a single character) we found unexpectedly.
        // We might want to show it in the error message, to help the user spot the error.
        internal string UnexpectedlyFound
        {
            get
            {
                return _unexpectedlyFound;
            }
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

            if (_lookahead.IsKind(TokenKind.EndOfInput))
            {
                return true;
            }

            SkipWhiteSpace();

            // Update error position after skipping whitespace
            _errorPosition = _parsePoint + 1;

            if (_parsePoint >= _expression.Length)
            {
                _lookahead = Token.EndOfInput;
            }
            else
            {
                switch (_expression[_parsePoint])
                {
                    case ',':
                        _lookahead = Token.Comma;
                        _parsePoint++;
                        break;
                    case '(':
                        _lookahead = Token.LeftParenthesis;
                        _parsePoint++;
                        break;
                    case ')':
                        _lookahead = Token.RightParenthesis;
                        _parsePoint++;
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
                        int start = _parsePoint;
                        // If the caller specified that he DOESN'T want to allow item lists ...
                        if ((_options & ParserOptions.AllowItemLists) == 0)
                        {
                            if ((_parsePoint + 1) < _expression.Length && _expression[_parsePoint + 1] == '(')
                            {
                                SetError(start + 1, ConditionErrors.ItemListNotAllowed);
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
                        if ((_parsePoint + 1) < _expression.Length && _expression[_parsePoint + 1] == '=')
                        {
                            _lookahead = Token.NotEqualTo;
                            _parsePoint += 2;
                        }
                        else
                        {
                            _lookahead = Token.Not;
                            _parsePoint++;
                        }
                        break;
                    case '>':
                        // gt and gte
                        if ((_parsePoint + 1) < _expression.Length && _expression[_parsePoint + 1] == '=')
                        {
                            _lookahead = Token.GreaterThanOrEqualTo;
                            _parsePoint += 2;
                        }
                        else
                        {
                            _lookahead = Token.GreaterThan;
                            _parsePoint++;
                        }
                        break;
                    case '<':
                        // lt and lte
                        if ((_parsePoint + 1) < _expression.Length && _expression[_parsePoint + 1] == '=')
                        {
                            _lookahead = Token.LessThanOrEqualTo;
                            _parsePoint += 2;
                        }
                        else
                        {
                            _lookahead = Token.LessThan;
                            _parsePoint++;
                        }
                        break;
                    case '=':
                        if ((_parsePoint + 1) < _expression.Length && _expression[_parsePoint + 1] == '=')
                        {
                            _lookahead = Token.EqualTo;
                            _parsePoint += 2;
                        }
                        else
                        {
                            int errorPosition = _parsePoint + 2; // expression[parsePoint + 1], counting from 1

                            if ((_parsePoint + 1) < _expression.Length)
                            {
                                SetError(errorPosition, ConditionErrors.IllFormedEquals, Convert.ToString(_expression[_parsePoint + 1], CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                SetError(errorPosition, ConditionErrors.IllFormedEquals, ConditionErrors.EndOfInputTokenName);
                            }

                            _parsePoint++;
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
                        if (TryParseNumeric())
                        {
                            return true;
                        }

                        if (TryParseSimpleStringOrFunction())
                        {
                            return true;
                        }

                        // Something that wasn't a number or a letter, like a newline (%0a)
                        SetError(_parsePoint + 1, ConditionErrors.UnexpectedCharacter, _expression[_parsePoint].ToString());
                        return false;
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
        private string ParsePropertyOrItemMetadata(bool isProperty)
        {
            int start = _parsePoint; // set start so that we include "$(" or "%("
            _parsePoint++;

            if (_parsePoint < _expression.Length && _expression[_parsePoint] != '(')
            {
                if (isProperty)
                {
                    SetError(start + 1, ConditionErrors.IllFormedPropertyOpenParenthesis, Convert.ToString(_expression[_parsePoint], CultureInfo.InvariantCulture));
                }
                else
                {
                    SetError(start + 1, ConditionErrors.IllFormedItemMetadataOpenParenthesis, Convert.ToString(_expression[_parsePoint], CultureInfo.InvariantCulture));
                }

                return null;
            }

            var result = ScanForPropertyExpressionEnd(_expression, _parsePoint++, out int indexResult);
            if (!result)
            {
                SetError(indexResult, ConditionErrors.IllFormedPropertySpace, Convert.ToString(_expression[indexResult], CultureInfo.InvariantCulture));
                return null;
            }

            _parsePoint = indexResult;
            // Maybe we need to generate an error for invalid characters in property/metadata name?
            // For now, just wait and let the property/metadata evaluation handle the error case.
            if (_parsePoint >= _expression.Length)
            {
                if (isProperty)
                {
                    SetError(start + 1, ConditionErrors.IllFormedPropertyCloseParenthesis, ConditionErrors.EndOfInputTokenName);
                }
                else
                {
                    SetError(start + 1, ConditionErrors.IllFormedItemMetadataCloseParenthesis, ConditionErrors.EndOfInputTokenName);
                }

                return null;
            }

            _parsePoint++;
            return _expression.Substring(start, _parsePoint - start);
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
            string propertyExpression = this.ParsePropertyOrItemMetadata(isProperty: true);

            if (propertyExpression == null)
            {
                return false;
            }
            else
            {
                _lookahead = Token.Property(propertyExpression);
                return true;
            }
        }

        /// <summary>
        /// Parses a string of the form %(itemmetadataname).
        /// </summary>
        /// <returns></returns>
        private bool ParseItemMetadata()
        {
            string itemMetadataExpression = this.ParsePropertyOrItemMetadata(isProperty: false);

            if (itemMetadataExpression == null)
            {
                return false;
            }

            _lookahead = Token.ItemMetadata(itemMetadataExpression);

            if (!CheckForUnexpectedMetadata(itemMetadataExpression))
            {
                return false;
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
                SetError(_parsePoint, ConditionErrors.BuiltInMetadataNotAllowed, expression);
                return false;
            }

            if (((_options & ParserOptions.AllowCustomMetadata) == 0) &&
                !isItemSpecModifier)
            {
                SetError(_parsePoint, ConditionErrors.CustomMetadataNotAllowed, expression);
                return false;
            }

            return true;
        }

        private bool ParseInternalItemList()
        {
            int start = _parsePoint;
            _parsePoint++;

            if (_parsePoint < _expression.Length && _expression[_parsePoint] != '(')
            {
                // @ was not followed by (
                SetError(start + 1, ConditionErrors.IllFormedItemListOpenParenthesis);
                return false;
            }
            _parsePoint++;
            // Maybe we need to generate an error for invalid characters in itemgroup name?
            // For now, just let item evaluation handle the error.
            bool fInReplacement = false;
            int parenToClose = 0;
            while (_parsePoint < _expression.Length)
            {
                if (_expression[_parsePoint] == '\'')
                {
                    fInReplacement = !fInReplacement;
                }
                else if (_expression[_parsePoint] == '(' && !fInReplacement)
                {
                    parenToClose++;
                }
                else if (_expression[_parsePoint] == ')' && !fInReplacement)
                {
                    if (parenToClose == 0)
                    {
                        break;
                    }
                    else { parenToClose--; }
                }
                _parsePoint++;
            }
            if (_parsePoint >= _expression.Length)
            {
                string errorResource = fInReplacement
                    // @( ... ' was never followed by a closing quote before the closing parenthesis
                    ? ConditionErrors.IllFormedItemListQuote
                    // @( was never followed by a )
                    : ConditionErrors.IllFormedItemListCloseParenthesis;

                SetError(start + 1, errorResource);
                return false;
            }
            _parsePoint++;
            return true;
        }

        private bool ParseItemList()
        {
            int start = _parsePoint;
            if (!ParseInternalItemList())
            {
                return false;
            }
            _lookahead = Token.ItemList(_expression.Substring(start, _parsePoint - start));
            return true;
        }

        /// <summary>
        /// Parse any part of the conditional expression that is quoted. It may contain a property, item, or
        /// metadata element that needs expansion during evaluation.
        /// </summary>
        private bool ParseQuotedString()
        {
            _parsePoint++;
            int start = _parsePoint;
            bool expandable = false;
            while (_parsePoint < _expression.Length && _expression[_parsePoint] != '\'')
            {
                // Standalone percent-sign must be allowed within a condition because it's
                // needed to escape special characters.  However, percent-sign followed
                // by open-parenthesis is an indication of an item metadata reference, and
                // that is only allowed in certain contexts.
                if ((_expression[_parsePoint] == '%') && ((_parsePoint + 1) < _expression.Length) && (_expression[_parsePoint + 1] == '('))
                {
                    expandable = true;
                    string name = String.Empty;

                    int endOfName = _expression.IndexOf(')', _parsePoint) - 1;
                    if (endOfName < 0)
                    {
                        endOfName = _expression.Length - 1;
                    }

                    // If it's %(a.b) the name is just 'b'
                    if (_parsePoint + 3 < _expression.Length)
                    {
                        name = _expression.Substring(_parsePoint + 2, endOfName - _parsePoint - 2 + 1);
                    }

                    if (!CheckForUnexpectedMetadata(name))
                    {
                        return false;
                    }
                }
                else if (_expression[_parsePoint] == '@' && ((_parsePoint + 1) < _expression.Length) && (_expression[_parsePoint + 1] == '('))
                {
                    expandable = true;

                    // If the caller specified that he DOESN'T want to allow item lists ...
                    if ((_options & ParserOptions.AllowItemLists) == 0)
                    {
                        SetError(start + 1, ConditionErrors.ItemListNotAllowed);
                        return false;
                    }

                    // Item lists have to be parsed because of the replacement syntax e.g. @(Foo,'_').
                    // I have to know how to parse those so I can skip over the tic marks.  I don't
                    // have to do that with other things like propertygroups, hence itemlists are
                    // treated specially.

                    ParseInternalItemList();
                    continue;
                }
                else if (_expression[_parsePoint] == '$' && ((_parsePoint + 1) < _expression.Length) && (_expression[_parsePoint + 1] == '('))
                {
                    expandable = true;
                }
                else if (_expression[_parsePoint] == '%')
                {
                    // There may be some escaped characters in the expression
                    expandable = true;
                }
                _parsePoint++;
            }

            if (_parsePoint >= _expression.Length)
            {
                // Quoted string wasn't closed
                SetError(start, ConditionErrors.IllFormedQuotedString);
                return false;
            }
            string originalTokenString = _expression.Substring(start, _parsePoint - start);

            _lookahead = Token.String(originalTokenString, expandable);
            _parsePoint++;
            return true;
        }

        private bool TryParseSimpleStringOrFunction()
        {
            ReadOnlySpan<char> span = _expression.AsSpan(_parsePoint);

            if (!TryLexIdentifier(span, out var identifier))
            {
                return false;
            }

            _parsePoint += identifier.Length;

            if (identifier.Equals("and", StringComparison.OrdinalIgnoreCase))
            {
                _lookahead = Token.And;
                return true;
            }

            if (identifier.Equals("or", StringComparison.OrdinalIgnoreCase))
            {
                _lookahead = Token.Or;
                return true;
            }

            // Look ahead to see if there's a '(' character. TrimStart() skips whitespace.
            span = span[identifier.Length..].TrimStart();

            _lookahead = span is ['(', ..]
                ? Token.Function(identifier.ToString())
                : Token.String(identifier.ToString());

            return true;
        }

        private bool TryParseNumeric()
        {
            ReadOnlySpan<char> span = _expression.AsSpan(_parsePoint);

            if (TryLexHexNumber(span, out var number) ||
                TryLexDecimalNumber(span, out number))
            {
                _parsePoint += number.Length;
                _lookahead = Token.Numeric(number.ToString());
                return true;
            }

            return false;
        }

        private static bool TryLexIdentifier(ReadOnlySpan<char> span, out ReadOnlySpan<char> result)
        {
            if (span.IsEmpty || !CharacterUtilities.IsSimpleStringStart(span[0]))
            {
                result = default;
                return false;
            }

            int index = 1;

            while (index < span.Length && CharacterUtilities.IsSimpleStringChar(span[index]))
            {
                index++;
            }

            result = span[..index];
            return true;
        }

        private static bool TryLexHexNumber(ReadOnlySpan<char> span, out ReadOnlySpan<char> result)
        {
            // UNDONE: Verify that the character after 0x is a hex digit.
            // Otherwise, this shouldn't be considered a hex number.
            if (span is not ['0', ('x' or 'X'), ..])
            {
                result = default;
                return false;
            }

            int index = 2;

            while (index < span.Length && CharacterUtilities.IsHexDigit(span[index]))
            {
                index++;
            }

            result = span[..index];
            return true;
        }

        private static bool TryLexDecimalNumber(ReadOnlySpan<char> span, out ReadOnlySpan<char> result)
        {
            // UNDONE: If the first character was a '+', '-', or a '.', we should check that the next character is a digit.
            if (span.IsEmpty || !CharacterUtilities.IsNumberStart(span[0]))
            {
                result = default;
                return false;
            }

            // UNDONE: The loop below allows dots and no digits, which matches the original implementation.
            // UNDONE: Decimals with multiple dots, such as 0.0.0, should not be supported.
            int index = 1;

            while (index < span.Length && IsDigitOrDot(span[index]))
            {
                index++;
            }

            result = span[..index];
            return true;

            static bool IsDigitOrDot(char ch)
                => char.IsDigit(ch) || ch == '.';
        }

        private void SkipWhiteSpace()
        {
            while (_parsePoint < _expression.Length && char.IsWhiteSpace(_expression[_parsePoint]))
            {
                _parsePoint++;
            }

            return;
        }
    }
}
