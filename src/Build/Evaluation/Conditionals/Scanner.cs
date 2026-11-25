// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Scans MSBuild condition expressions into tokens consumed by <see cref="Parser"/>.
/// </summary>
/// <remarks>
///  Call <see cref="Advance"/> before inspecting <see cref="CurrentToken"/> or <see cref="TokenPosition"/>.
///  Scanning continues until <see cref="TokenKind.EndOfInput"/> or an error is encountered.
///  This lexical grammar describes token shapes, subject to the recognition rules below; it does not
///  define the full property-function or item-expansion language. Brackets denote optional elements
///  and braces denote repetition.
///  <code>
///   token          = punctuation | operator | simpleString | number
///                  | quotedString | property | metadata | itemList ;
///   punctuation    = "," | "(" | ")" ;
///   operator       = "==" | "!=" | "&lt;" | "&gt;" | "&lt;=" | "&gt;=" | "!" ;
///
///   simpleString   = simpleStart, { simpleChar } ;
///   simpleStart    = "_" | letter ;
///   simpleChar     = simpleStart | digit ;
///
///   number         = hexNumber | decimalNumber ;
///   hexNumber      = "0", ( "x" | "X" ), { hexDigit } ;
///   decimalNumber  = [ sign ], digitOrDot, { digitOrDot } | sign ;
///   sign           = "+" | "-" ;
///   digitOrDot     = digit | "." ;
///
///   property       = "$", referenceBody ;
///   metadata       = "%", referenceBody ;
///   referenceBody  = "(", { referenceChar | referenceBody }, ")" ;
///
///   itemList       = "@", itemBody ;
///   itemBody       = "(", { itemChar | itemBody | itemQuotedText }, ")" ;
///   itemQuotedText = "'", { nonQuoteChar }, "'" ;
///   quotedString   = "'", { quotedChar | itemList }, "'" ;
///  </code>
///  <para>
///   <c>letter</c> and <c>digit</c> are characters accepted by <see cref="char.IsLetter(char)"/>
///   and <see cref="char.IsDigit(char)"/>. <c>hexDigit</c> is a digit or an ASCII letter
///   <c>A</c> through <c>F</c>, ignoring case. <c>referenceChar</c> excludes parentheses;
///   <c>itemChar</c> excludes parentheses and single quotes; <c>nonQuoteChar</c> excludes single
///   quotes. <c>quotedChar</c> excludes single quotes and the start of an <c>@(</c> sequence.
///  </para>
///  <para>
///   Whitespace is skipped between tokens using <see cref="char.IsWhiteSpace(char)"/>. Whitespace
///   is not required where other token boundaries suffice; <c>'a'=='a'and'b'=='b'</c> is accepted.
///   Simple strings consume all consecutive simple-string characters. Whole-token, ordinal
///   case-insensitive matches of <c>and</c> and <c>or</c> become operator tokens. Any other simple
///   string becomes a <see cref="TokenKind.FunctionName"/> token if the next non-whitespace
///   character is <c>(</c>; otherwise, it becomes a <see cref="TokenKind.String"/> token.
///   The opening parenthesis remains a separate token.
///  </para>
///  <para>
///   Hexadecimal scanning takes precedence when <c>0x</c> or <c>0X</c> is followed by at least one
///   source character, even if that character is not a hex digit. It consumes all following hex
///   digits, possibly none. Otherwise, decimal scanning consumes an optional sign and all following
///   digits and dots. A sign alone, a dot alone, and multiple dots are accepted as number tokens.
///  </para>
///  <para>
///   Quoted strings exclude their delimiters from the token text. Backslashes do not escape quotes.
///   Embedded item lists are scanned separately so their internal single quotes do not end the
///   outer string. Property and metadata references inside strings are not structurally scanned
///   like unquoted references. Percent escapes are preserved for evaluation.
///  </para>
///  <para>
///   Unquoted property and metadata references balance parentheses without treating quotes specially.
///   Whitespace in otherwise identifier-only bodies is rejected, but punctuation such as <c>.</c>
///   suppresses that check. Nested <c>$(...)</c> references are checked independently, even inside
///   metadata. Names and internal expansion syntax are not fully validated.
///  </para>
///  <para>
///   Item lists balance parentheses outside single-quoted regions. Their internal expansion syntax
///   is left to evaluation. <see cref="ParserOptions"/> restricts item lists and metadata, including
///   references encountered while scanning quoted strings. Property references are always allowed.
///  </para>
/// </remarks>
internal sealed class Scanner
{
    /// <summary>
    ///  Gets the lazily loaded, localized name used for end of input in diagnostics.
    /// </summary>
    private static string EndOfInput
        => field ??= ResourceUtilities.GetResourceString("EndOfInputTokenName");

    /// <summary>
    ///  The condition text being scanned.
    /// </summary>
    private readonly string _text;

    /// <summary>
    ///  The reference forms permitted in the condition.
    /// </summary>
    private readonly ParserOptions _options;

    /// <summary>
    ///  The zero-based position of the next character to scan.
    /// </summary>
    private int _position;

    /// <summary>
    ///  The most recently assigned token, or <see langword="null"/> if none has been assigned.
    /// </summary>
    private Token? _current;

    /// <summary>
    ///  The one-based start of the current token scan, or <c>-1</c> before scanning begins.
    /// </summary>
    private int _tokenPosition;

    /// <summary>
    ///  The diagnostic resource name, or <see langword="null"/> when no error has been recorded.
    /// </summary>
    private string? _errorResource;

    /// <summary>
    ///  The recorded scanning error's position, or <c>-1</c> when no error has been recorded.
    /// </summary>
    private int _errorPosition;

    /// <summary>
    ///  An optional diagnostic argument, usually the unexpected character or metadata name.
    /// </summary>
    private string? _errorFormatArg;

    /// <summary>
    ///  Initializes a scanner for a condition expression.
    /// </summary>
    /// <param name="text">The condition text to scan.</param>
    /// <param name="options">The permitted reference forms. Must include <see cref="ParserOptions.AllowProperties"/>.</param>
    public Scanner(string text, ParserOptions options)
    {
        // We currently have no support (and no scenarios) for disallowing property references
        // in Conditions.
        Assumed.True((options & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

        _text = text;
        _position = 0;
        _tokenPosition = -1;
        _errorPosition = -1; // invalid
        _options = options;
    }

    /// <summary>
    ///  Checks the current token's kind without advancing.
    /// </summary>
    /// <param name="kind">The token kind to compare against.</param>
    /// <returns>
    ///  <see langword="true"/> if the current token has the specified <paramref name="kind"/>;
    ///  otherwise, <see langword="false"/>, including when no token has been assigned.
    /// </returns>
    public bool IsKind(TokenKind kind)
        => _current?.IsKind(kind) is true;

    /// <summary>
    ///  Gets the current token's kind, or <see cref="TokenKind.None"/> if no token has been assigned.
    /// </summary>
    public TokenKind CurrentTokenKind => _current?.Kind ?? TokenKind.None;

    /// <summary>
    ///  Gets the current token's text without advancing.
    /// </summary>
    /// <value>
    ///  The token text, <see cref="string.Empty"/> at end of input, or <see langword="null"/>
    ///  if no token has been assigned.
    /// </value>
    public string? CurrentTokenText => _current?.Text;

    /// <summary>
    ///  Gets the current token, or <see langword="null"/> if none has been assigned.
    /// </summary>
    /// <remarks>
    ///  Inspect the token only after <see cref="Advance"/> returns <see langword="true"/>.
    /// </remarks>
    public Token? CurrentToken => _current;

    /// <summary>
    ///  Gets the one-based start position of the most recent token scan.
    /// </summary>
    /// <remarks>
    ///  At end of input, this is one past the text length. On failure, it is the start of the token
    ///  that could not be scanned, rather than the error's position. Do not read before the first
    ///  call to <see cref="Advance"/>.
    /// </remarks>
    public int TokenPosition
    {
        get
        {
            Assumed.PositiveOrZero(_tokenPosition, $"{nameof(TokenPosition)} called before token position was set.");
            return _tokenPosition;
        }
    }

    /// <summary>
    ///  Records a scanning error, replacing any diagnostic recorded while scanning the same token.
    /// </summary>
    /// <param name="resourceName">The diagnostic resource name.</param>
    /// <param name="position">The position expected by the diagnostic resource.</param>
    /// <param name="formatArg">An optional diagnostic argument, such as an unexpected character or metadata name.</param>
    private void SetError(string resourceName, int position, string? formatArg = null)
    {
        _errorResource = resourceName;
        _errorPosition = position;
        _errorFormatArg = formatArg;
    }

    /// <summary>
    ///  Gets whether a scanning error has been recorded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ErrorResource))]
    public bool HasError => _errorResource is not null;

    /// <summary>
    ///  Gets the resource name for the recorded scanning error.
    /// </summary>
    /// <remarks>
    ///  Read only when <see cref="HasError"/> is <see langword="true"/>. The parser formats the
    ///  resource with the condition text, <see cref="ErrorPosition"/>, and <see cref="ErrorFormatArg"/>.
    /// </remarks>
    public string ErrorResource
    {
        get
        {
            Assumed.NotNull(_errorResource, $"{nameof(ErrorResource)} called before error resource was set.");
            return _errorResource;
        }
    }

    /// <summary>
    ///  Gets the recorded scanning error's position.
    /// </summary>
    /// <remarks>
    ///  Read only when <see cref="HasError"/> is <see langword="true"/>. The position follows the
    ///  diagnostic's existing convention and may differ from <see cref="TokenPosition"/>.
    /// </remarks>
    public int ErrorPosition
    {
        get
        {
            Assumed.PositiveOrZero(_errorPosition, $"{nameof(ErrorPosition)} called before error position was set.");
            return _errorPosition;
        }
    }

    /// <summary>
    ///  Gets the optional argument for the recorded error, or <see langword="null"/> if none is needed.
    /// </summary>
    public string? ErrorFormatArg => _errorFormatArg;

    /// <summary>
    ///  Skips whitespace and scans the next token.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if a token or end of input was reached, in which case
    ///  <see cref="CurrentToken"/> is not <see langword="null"/>; otherwise, <see langword="false"/>
    ///  with diagnostic details available through <see cref="ErrorResource"/>, <see cref="ErrorPosition"/>,
    ///  and <see cref="ErrorFormatArg"/>.
    /// </returns>
    /// <remarks>
    ///  Errors are reported only when encountered. Once an error is recorded, subsequent calls return
    ///  <see langword="false"/> without scanning. Once end of input is reached, subsequent calls return
    ///  <see langword="true"/> without changing the token.
    /// </remarks>
    public bool Advance()
    {
        if (HasError)
        {
            return false;
        }

        if (_current is { Kind: TokenKind.EndOfInput })
        {
            return true;
        }

        SkipWhiteSpace();

        // Capture the token start before scanning advances the cursor.
        _tokenPosition = _position + 1;

        if (IsAtEnd)
        {
            _current = Token.EndOfInput;
            return true;
        }

        switch (_text[_position])
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
                if (!TryScanProperty())
                {
                    return false;
                }

                break;

            case '%':
                if (!TryScanItemMetadata())
                {
                    return false;
                }

                break;

            case '@':
                if (!TryScanItemList())
                {
                    return false;
                }

                break;

            case '!': // negation and not-equal
                _position++;

                if (At('='))
                {
                    _current = Token.NotEqualTo;
                    _position++;
                }
                else
                {
                    _current = Token.Not;
                }

                break;

            case '>': // greater than and greater than or equal
                _position++;

                if (At('='))
                {
                    _current = Token.GreaterThanOrEqualTo;
                    _position++;
                }
                else
                {
                    _current = Token.GreaterThan;
                }

                break;

            case '<': // less than and less than or equal
                _position++;

                if (At('='))
                {
                    _current = Token.LessThanOrEqualTo;
                    _position++;
                }
                else
                {
                    _current = Token.LessThan;
                }

                break;

            case '=':
                _position++;

                if (At('='))
                {
                    _current = Token.EqualTo;
                    _position++;
                }
                else
                {
                    string unexpectedlyFound = !IsAtEnd
                        ? _text[_position].ToString()
                        : EndOfInput;

                    SetError("IllFormedEqualsInCondition", position: _position + 1, formatArg: unexpectedlyFound);

                    return false;
                }

                break;

            case '\'':
                if (!TryScanQuotedString())
                {
                    return false;
                }

                break;

            default:
                // Simple strings, function calls, decimal numbers, hex numbers
                if (TryScanNumber() || TryScanSimpleString())
                {
                    break;
                }

                SetError("UnexpectedCharacterInCondition", position: _position + 1, formatArg: _text[_position].ToString());
                return false;
        }

        return true;
    }

    /// <summary>
    ///  Scans an unquoted property or metadata reference using property-syntax diagnostics.
    /// </summary>
    /// <returns>
    ///  The reference text, including its prefix and parentheses, or <see langword="null"/> after
    ///  recording a syntax error.
    /// </returns>
    private string? ScanPropertyOrItemMetadataText()
    {
        int start = _position; // set start so that we include "$(" or "%("
        _position++;

        if (!IsAtEnd && !At('('))
        {
            SetError("IllFormedPropertyOpenParenthesisInCondition", position: start + 1, formatArg: _text[_position].ToString());
            return null;
        }

        if (!TryScanReferenceBody(_position, out int resultIndex))
        {
            SetError("IllFormedPropertySpaceInCondition", position: resultIndex, formatArg: _text[resultIndex].ToString());
            return null;
        }

        _position = resultIndex;

        // Maybe we need to generate an error for invalid characters in property/metadata name?
        // For now, just wait and let the property/metadata evaluation handle the error case.
        if (IsAtEnd)
        {
            SetError("IllFormedPropertyCloseParenthesisInCondition", position: start + 1, formatArg: EndOfInput);
            return null;
        }

        _position++;
        return _text.Substring(start, _position - start);
    }

    /// <summary>
    ///  Scans for a reference's matching closing parenthesis, checking whitespace along the way.
    /// </summary>
    /// <param name="startIndex">The zero-based index of the opening parenthesis.</param>
    /// <param name="resultIndex">
    ///  The matching closing parenthesis's index, or an index at or beyond the expression length
    ///  if no match was found. On failure, the zero-based index of the unexpected whitespace.
    /// </param>
    /// <returns>
    ///  <see langword="false"/> if disallowed whitespace was found; otherwise, <see langword="true"/>,
    ///  even if no matching closing parenthesis was found.
    /// </returns>
    /// <remarks>
    ///  Pins the condition text for the entire scan, including recursive calls.
    ///  Quotes do not protect parentheses. Nested <c>$(...)</c> references are scanned recursively
    ///  so their whitespace is checked independently, including when the outer reference is metadata.
    /// </remarks>
    private unsafe bool TryScanReferenceBody(int startIndex, out int resultIndex)
    {
        fixed (char* text = _text)
        {
            return TryScanReferenceBodyCore(text, _text.Length, startIndex, out resultIndex);
        }

        static bool TryScanReferenceBodyCore(char* text, int length, int index, out int resultIndex)
        {
            int nestLevel = 0;
            int whitespacePosition = -1;
            bool nonIdentifierCharacterFound = false;

            while (index < length)
            {
                char ch = text[index];
                if (ch == '(')
                {
                    nestLevel++;
                }
                else if (ch == ')')
                {
                    nestLevel--;

                    if (nestLevel == 0)
                    {
                        if (whitespacePosition >= 0 && !nonIdentifierCharacterFound)
                        {
                            resultIndex = whitespacePosition;
                            return false;
                        }

                        resultIndex = index;
                        return true;
                    }
                }
                else if (char.IsWhiteSpace(ch))
                {
                    // UNDONE: The diagnostic reports the last whitespace character's zero-based offset instead of
                    // the first whitespace character's one-based position. Preserve this quirk until a separate
                    // diagnostic fix is made.
                    whitespacePosition = index;
                }
                else if (!nonIdentifierCharacterFound && !XmlUtilities.IsValidSubsequentElementNameCharacter(ch))
                {
                    nonIdentifierCharacterFound = true;
                }

                // UNDONE: This also accepts property references nested inside metadata references, where they
                // are invalid. Rejecting them here would fail conditions whose invalid metadata is currently
                // skipped by boolean short-circuiting, so adding a scanner error would be a breaking change.
                if (ch == '$' && index < length - 1 && text[index + 1] == '(')
                {
                    if (!TryScanReferenceBodyCore(text, length, index + 1, out index))
                    {
                        resultIndex = index;
                        return false;
                    }
                }

                index++;
            }

            resultIndex = index;
            return true;
        }
    }

    /// <summary>
    ///  Scans an unquoted <c>$(...)</c> property-reference token.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the token was scanned; otherwise, <see langword="false"/> after
    ///  recording a syntax error.
    /// </returns>
    private bool TryScanProperty()
    {
        if (ScanPropertyOrItemMetadataText() is string propertyExpression)
        {
            _current = Token.Property(propertyExpression);
            return true;
        }

        return false;
    }

    /// <summary>
    ///  Scans an unquoted <c>%(...)</c> metadata token and checks whether it is permitted.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the token was scanned and permitted; otherwise, <see langword="false"/>
    ///  after recording an error.
    /// </returns>
    /// <remarks>
    ///  Malformed references use the generic unexpected-character diagnostic for compatibility.
    /// </remarks>
    private bool TryScanItemMetadata()
    {
        if (ScanPropertyOrItemMetadataText() is string itemMetadataExpression)
        {
            _current = Token.ItemMetadata(itemMetadataExpression);

            return CheckForUnexpectedMetadata(itemMetadataExpression);
        }

        // The ScanPropertyOrItemMetadataText method returns the correct error resources
        // for parsing properties such as $(propertyname).  At this stage in the Whidbey
        // cycle, we're not allowed to add new string resources, so I can't add a new
        // resource specific to item metadata, so here, we just change the error to
        // the generic "UnexpectedCharacter".
        SetError("UnexpectedCharacterInCondition", _errorPosition, _errorFormatArg);
        return false;
    }

    /// <summary>
    ///  Checks whether the metadata is permitted by the scanner's options.
    /// </summary>
    /// <param name="expression">A metadata name, optionally qualified by item type, or a full <c>%(...)</c> reference.</param>
    /// <returns>
    ///  <see langword="true"/> if the metadata is permitted; otherwise, <see langword="false"/> after
    ///  recording the built-in or custom metadata restriction error.
    /// </returns>
    private bool CheckForUnexpectedMetadata(string expression)
    {
        if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
        {
            return true;
        }

        // Take off %( and )
        if (expression.Length > 3 && expression is ['%', '(', .. string inner, ')'])
        {
            expression = inner;
        }

        // If it's like %(a.b) find 'b'
        int period = expression.IndexOf('.');
        if (period > 0 && period < expression.Length - 1)
        {
            expression = expression.Substring(period + 1);
        }

        bool isItemSpecModifier = ItemSpecModifiers.IsItemSpecModifier(expression);

        // UNDONE: Both metadata restriction diagnostics use the zero-based '%' offset for quoted
        // references, but the one-based ')' position for unquoted references. Pass the reference's
        // one-based start position in a separate diagnostic fix.
        if (((_options & ParserOptions.AllowBuiltInMetadata) == 0) &&
            isItemSpecModifier)
        {
            SetError("BuiltInMetadataNotAllowedInThisConditional", position: _position, formatArg: expression);
            return false;
        }

        if (((_options & ParserOptions.AllowCustomMetadata) == 0) &&
            !isItemSpecModifier)
        {
            SetError("CustomMetadataNotAllowedInThisConditional", position: _position, formatArg: expression);
            return false;
        }

        return true;
    }

    /// <summary>
    ///  Scans an item list's delimiters, balancing parentheses outside single-quoted regions.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if a matching closing parenthesis was found; otherwise, <see langword="false"/>
    ///  after recording a syntax error.
    /// </returns>
    /// <remarks>
    ///  Does not assign <see cref="CurrentToken"/> or validate the internal expansion syntax.
    /// </remarks>
    private bool TryScanItemListText()
    {
        int start = _position;
        _position++;

        if (!IsAtEnd && !At('('))
        {
            // @ was not followed by (
            // Not useful to set unexpectedlyFound here. The message is going to be detailed enough.
            SetError("IllFormedItemListOpenParenthesisInCondition", position: start + 1);
            return false;
        }

        _position++;

        // Maybe we need to generate an error for invalid characters in itemgroup name?
        // For now, just let item evaluation handle the error.
        bool fInReplacement = false;
        int parenToClose = 0;
        while (!IsAtEnd)
        {
            if (At('\''))
            {
                fInReplacement = !fInReplacement;
            }
            else if (At('(') && !fInReplacement)
            {
                parenToClose++;
            }
            else if (At(')') && !fInReplacement)
            {
                if (parenToClose == 0)
                {
                    break;
                }
                else
                {
                    parenToClose--;
                }
            }

            _position++;
        }

        if (IsAtEnd)
        {
            string resourceName = fInReplacement
                ? "IllFormedItemListQuoteInCondition" // @(... ' was never followed by a closing quote before the closing parenthesis
                : "IllFormedItemListCloseParenthesisInCondition"; // @( was never followed by a )

            // Not useful to set unexpectedlyFound here. The message is going to be detailed enough.
            SetError(resourceName, position: start + 1);
            return false;
        }

        _position++;
        return true;
    }

    /// <summary>
    ///  Scans an unquoted <c>@(...)</c> item-list token and checks whether item lists are permitted.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the token was scanned; otherwise, <see langword="false"/> after
    ///  recording a syntax or item-list restriction error.
    /// </returns>
    private bool TryScanItemList()
    {
        int start = _position;

        // If the caller specified that he DOESN'T want to allow item lists ...
        if ((_options & ParserOptions.AllowItemLists) == 0)
        {
            if (Next('('))
            {
                SetError("ItemListNotAllowedInThisConditional", position: start + 1);
                return false;
            }
        }

        if (!TryScanItemListText())
        {
            return false;
        }

        _current = Token.ItemList(_text.Substring(start, _position - start));
        return true;
    }

    /// <summary>
    ///  Scans a single-quoted string, recording whether its contents may require expansion.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the token was scanned; otherwise, <see langword="false"/> after
    ///  recording a syntax or reference-restriction error.
    /// </returns>
    /// <remarks>
    ///  Embedded item lists are scanned to skip their internal quotes. Property and metadata
    ///  references are not structurally validated here. For compatibility, a malformed embedded
    ///  item list's error is replaced by the unterminated outer-string diagnostic.
    /// </remarks>
    private bool TryScanQuotedString()
    {
        _position++;
        int start = _position;
        bool expandable = false;
        while (!IsAtEnd && !At('\''))
        {
            // Standalone percent-sign must be allowed within a condition because it's
            // needed to escape special characters.  However, percent-sign followed
            // by open-parenthesis is an indication of an item metadata reference, and
            // that is only allowed in certain contexts.
            if (At('%', '('))
            {
                expandable = true;
                string name = string.Empty;

                int endOfName = _text.IndexOf(')', _position) - 1;
                if (endOfName < 0)
                {
                    endOfName = _text.Length - 1;
                }

                // If it's %(a.b) the name is just 'b'
                if (_position + 3 < _text.Length)
                {
                    name = _text.Substring(_position + 2, endOfName - _position - 2 + 1);
                }

                if (!CheckForUnexpectedMetadata(name))
                {
                    return false;
                }
            }
            else if (At('@', '('))
            {
                expandable = true;

                // If the caller specified that he DOESN'T want to allow item lists ...
                if ((_options & ParserOptions.AllowItemLists) == 0)
                {
                    SetError("ItemListNotAllowedInThisConditional", position: start + 1);
                    return false;
                }

                // Item lists have to be parsed because of the replacement syntax e.g. @(Foo,'_').
                // I have to know how to parse those so I can skip over the tic marks.  I don't
                // have to do that with other things like propertygroups, hence itemlists are
                // treated specially.
                _ = TryScanItemListText();
                continue;
            }
            else if (At('$', '('))
            {
                expandable = true;
            }
            else if (At('%'))
            {
                // There may be some escaped characters in the expression
                expandable = true;
            }

            _position++;
        }

        if (IsAtEnd)
        {
            // Quoted string wasn't closed
            // The message is going to say "expected after position n" so don't add 1 here.
            SetError("IllFormedQuotedStringInCondition", position: start);

            // Not useful to set unexpectedlyFound here. By definition it got to the end of the string.
            return false;
        }

        string originalTokenString = _text.Substring(start, _position - start);

        _current = Token.String(originalTokenString, expandable);
        _position++;
        return true;
    }

    /// <summary>
    ///  Scans a simple string and classifies it as a string, logical operator, or function name.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if a token was scanned; otherwise, <see langword="false"/> without
    ///  consuming input when the current character cannot start a simple string.
    /// </returns>
    private bool TryScanSimpleString()
    {
        if (!CharacterUtilities.IsSimpleStringStart(_text[_position]))
        {
            return false;
        }

        int start = _position;

        ScanSimpleStringChars();

        // There is a bug here that spaces are not required around 'and' and 'or'. For example,
        // this works perfectly well:
        // Condition="%(a.Identity)!=''and%(a.m)=='1'"
        // Since people now depend on this behavior, we must not change it.
        if (_text.AsSpan(start, _position - start).Equals("and".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            _current = Token.And;
        }
        else if (_text.AsSpan(start, _position - start).Equals("or".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            _current = Token.Or;
        }
        else
        {
            int end = _position;

            SkipWhiteSpace();

            if (At('('))
            {
                _current = Token.FunctionName(_text.Substring(start, end - start));
            }
            else
            {
                string tokenValue = _text.Substring(start, end - start);
                _current = Token.String(tokenValue);
            }
        }

        return true;
    }

    /// <summary>
    ///  Scans a number token.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if a token was scanned; otherwise, <see langword="false"/> without
    ///  consuming input when the current character cannot start a number.
    /// </returns>
    private bool TryScanNumber()
    {
        if (!CharacterUtilities.IsNumberStart(_text[_position]))
        {
            return false;
        }

        int start = _position;

        if ((_text.Length - _position) > 2 && _text[_position] is '0' && _text[_position + 1] is 'x' or 'X')
        {
            // Hex number
            _position += 2;
            ScanHexDigits();
            _current = Token.Number(_text.Substring(start, _position - start));
            return true;
        }

        // Decimal number
        if (_text[_position] is '+' or '-')
        {
            _position++;
        }

        do
        {
            ScanDigits();

            if (At('.'))
            {
                _position++;
            }

            if (!IsAtEnd)
            {
                ScanDigits();
            }
        }
        while (At('.'));

        // Do we need to error on malformed input like 0.00.00)? or will the conversion handle it?
        // For now, let the conversion generate the error.
        _current = Token.Number(_text.Substring(start, _position - start));
        return true;
    }

    /// <summary>
    ///  Skips consecutive characters accepted by <see cref="char.IsWhiteSpace(char)"/>.
    /// </summary>
    private void SkipWhiteSpace()
    {
        while (!IsAtEnd && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }

    /// <summary>
    ///  Scans consecutive characters accepted by <see cref="char.IsDigit(char)"/>.
    /// </summary>
    private void ScanDigits()
    {
        while (!IsAtEnd && char.IsDigit(_text[_position]))
        {
            _position++;
        }
    }

    /// <summary>
    ///  Scans consecutive characters accepted by <see cref="CharacterUtilities.IsHexDigit"/>.
    /// </summary>
    private void ScanHexDigits()
    {
        while (!IsAtEnd && CharacterUtilities.IsHexDigit(_text[_position]))
        {
            _position++;
        }
    }

    /// <summary>
    ///  Scans consecutive characters accepted by <see cref="CharacterUtilities.IsSimpleStringChar"/>.
    /// </summary>
    private void ScanSimpleStringChars()
    {
        while (!IsAtEnd && CharacterUtilities.IsSimpleStringChar(_text[_position]))
        {
            _position++;
        }
    }

    /// <summary>
    ///  Gets whether the scanning position is at or beyond the end of the condition text.
    /// </summary>
    private bool IsAtEnd => _position >= _text.Length;

    /// <summary>
    ///  Checks the character at the current scanning position without advancing.
    /// </summary>
    /// <param name="expected">The character to compare against.</param>
    /// <returns>
    ///  <see langword="true"/> if the character exists and matches <paramref name="expected"/>;
    ///  otherwise, <see langword="false"/>, including at or beyond end of input.
    /// </returns>
    private bool At(char expected)
        => !IsAtEnd && _text[_position] == expected;

    /// <summary>
    ///  Checks a two-character sequence at the current scanning position without advancing.
    /// </summary>
    /// <param name="first">The expected first character.</param>
    /// <param name="second">The expected second character.</param>
    /// <returns>
    ///  <see langword="true"/> if both characters exist and match <paramref name="first"/> and
    ///  <paramref name="second"/>; otherwise, <see langword="false"/>.
    /// </returns>
    private bool At(char first, char second)
    {
        int next = _position + 1;
        return next < _text.Length && _text[_position] == first && _text[next] == second;
    }

    /// <summary>
    ///  Checks the character immediately after the current scanning position without advancing.
    /// </summary>
    /// <param name="expected">The character to compare against.</param>
    /// <returns>
    ///  <see langword="true"/> if the following character exists and matches <paramref name="expected"/>;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    private bool Next(char expected)
    {
        int next = _position + 1;
        return next < _text.Length && _text[next] == expected;
    }
}
