// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// A class which interprets and splits MSBuild expressions
/// </summary>
internal static class ExpressionShredder
{
    /// <summary>
    ///  Splits an expression into fragments at semicolons, except where the semicolons are inside a
    ///  macro or separator expression. Fragments are trimmed and empty fragments are discarded.
    /// </summary>
    /// <param name="expression">The list expression to split.</param>
    /// <returns>
    ///  An <see cref="ExpressionSplitter"/> over the fragments. It is a struct that is its own
    ///  enumerator, so it can be consumed with <c>foreach</c> or a collection-expression spread
    ///  (<c>[.. ...]</c>) without any heap allocation. See <see cref="ExpressionSplitter"/> for
    ///  the full splitting rules.
    /// </returns>
    internal static ExpressionSplitter Split(string expression)
        => new(expression);

    /// <summary>
    /// Given a list of expressions that may contain item list expressions,
    /// returns a pair of tables of all item names found, as K=Name, V=String.Empty;
    /// and all metadata not in transforms, as K=Metadata key, V=MetadataReference,
    /// where metadata key is like "itemname.metadataname" or "metadataname".
    /// PERF: Tables are null if there are no entries, because this is quite a common case.
    /// </summary>
    internal static ItemsAndMetadataPair GetReferencedItemNamesAndMetadata(IReadOnlyList<string> expressions)
    {
        ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

        // PERF: Use for to avoid boxing expressions enumerator
        for (int i = 0; i < expressions.Count; i++)
        {
            string expression = expressions[i];
            GetReferencedItemNamesAndMetadata(expression, ref pair);
        }

        return pair;
    }

    /// <summary>
    /// Returns true if there is a metadata expression (outside of a transform) in the expression.
    /// </summary>
    internal static bool ContainsMetadataExpressionOutsideTransform(string expression)
    {
        ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

        Scanner scanner = new(expression, 0, expression.Length);
        scanner.GetReferencedItemNamesAndMetadata(ref pair, includeItemTypes: false, includeMetadataOutsideTransforms: true);

        return pair.Metadata?.Count > 0;
    }

    /// <summary>
    /// Given an expression, finds referenced item vector expressions (e.g. <c>@(Foo)</c>,
    /// <c>@(Foo->'%(Bar)')</c>).
    /// </summary>
    internal static ItemVectorEnumerator GetReferencedItemExpressions(string expression)
        => new(expression);

    /// <summary>
    /// Given a subexpression, finds referenced item names and inserts them into the table
    /// as K=Name, V=String.Empty.
    /// </summary>
    /// <remarks>
    /// We can ignore any semicolons in the expression, since we're not itemizing it.
    /// </remarks>
    internal static void GetReferencedItemNamesAndMetadata(string expression, ref ItemsAndMetadataPair pair)
    {
        Scanner scanner = new(expression, 0, expression.Length);
        scanner.GetReferencedItemNamesAndMetadata(ref pair, includeItemTypes: true, includeMetadataOutsideTransforms: true);
    }

    /// <summary>
    ///  Attempts to scan a single <c>@(...)</c> item vector expression starting at <paramref name="i"/>.
    /// </summary>
    /// <param name="expression">The expression being scanned.</param>
    /// <param name="i">Current scan position. Advanced past the expression on success.</param>
    /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
    /// <param name="capture">The scanned expression if one was found; otherwise <see langword="default"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if a well-formed item vector expression was scanned.
    /// </returns>
    /// <remarks>
    ///  On success, <paramref name="i"/> is advanced to one past the closing <c>)</c> and
    ///  <paramref name="capture"/> contains the scanned expression. On failure, <paramref name="i"/> is
    ///  left unchanged and the caller should advance past the opening <c>@(</c> before scanning again.
    /// </remarks>
    internal static bool TryScanItemExpressionCapture(string expression, ref int i, int end, out ItemVector capture)
    {
        // Work on a scanner whose position is only committed back to i on success, so a failed
        // scan leaves the caller's position unchanged.
        Scanner scanner = new(expression, i, end);

        if (scanner.TryParseItemVector(out capture))
        {
            i = scanner.Index;
            return true;
        }

        return false;
    }

    /// <summary>
    ///  Attempts to parse a metadata expression of the form <c>%(Name)</c> or <c>%(ItemType.Name)</c>,
    ///  starting just after the <c>%(</c> has been consumed (i.e., <paramref name="i"/> points at
    ///  the first character after the opening parenthesis).
    /// </summary>
    /// <remarks>
    ///  On success, <paramref name="i"/> is left one past the closing <c>)</c>.
    ///  On failure, <paramref name="i"/> is at an indeterminate position and the caller
    ///  should restore it from a saved restart point.
    /// </remarks>
    /// <param name="expression">The expression being scanned.</param>
    /// <param name="i">Current scan position (just after <c>%(</c>). Advanced on success.</param>
    /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
    /// <param name="itemType">The item type if qualified; otherwise <see langword="null"/>.</param>
    /// <param name="metadataName">The metadata name.</param>
    /// <returns>
    ///  <see langword="true"/> if a valid metadata expression was parsed.
    /// </returns>
    internal static bool TryParseMetadataExpression(string expression, ref int i, int end, out string? itemType, [NotNullWhen(true)] out string? metadataName)
    {
        Scanner scanner = new(expression, i, end);
        bool result = scanner.TryParseMetadataExpression(out itemType, out metadataName);
        i = scanner.Index;
        return result;
    }

    /// <summary>
    ///  A stack-allocated cursor over an expression that scans MSBuild item and metadata expressions
    ///  in place. The scan state (the expression, the current position, and the exclusive end of the
    ///  scan range) lives on the struct, so the scanning helpers don't have to thread it through as
    ///  parameters.
    /// </summary>
    private ref struct Scanner
    {
        private static readonly char[] s_markers = ['@', '%'];

        private readonly string _expression;
        private readonly int _end;
        private int _index;

        /// <summary>
        ///  Initializes a new <see cref="Scanner"/> positioned at <paramref name="index"/> that scans
        ///  no character at or beyond <paramref name="end"/>.
        /// </summary>
        /// <param name="expression">The expression being scanned.</param>
        /// <param name="index">The initial scan position.</param>
        /// <param name="end">Exclusive end index of the scan range; must be within <paramref name="expression"/>.</param>
        public Scanner(string expression, int index, int end)
        {
            Assumed.LessThanOrEqual(end, expression.Length);

            _expression = expression;
            _index = index;
            _end = end;
        }

        /// <summary>
        ///  Gets the current scan position.
        /// </summary>
        public readonly int Index => _index;

        /// <summary>
        ///  Attempts to scan a single <c>@(...)</c> item vector expression at the current position.
        /// </summary>
        /// <param name="result">The scanned expression if one was found; otherwise <see langword="default"/>.</param>
        /// <returns>
        ///  <see langword="true"/> if a well-formed item vector expression was scanned.
        /// </returns>
        public bool TryParseItemVector(out ItemVector result)
        {
            result = default;

            int start = _index;

            if (!TryConsume('@', '('))
            {
                return false;
            }

            // Start of a possible item list expression. Store the expression's start point (the '@').
            int startPoint = _index - 2;

            SkipWhiteSpace();

            if (!TryParseName(out ReadOnlySpan<char> itemNameSpan))
            {
                _index = start;
                return false;
            }

            // Hold the name as a span and keep verifying the expression. We defer interning it into a
            // string until we know the whole expression is well-formed (see the capture below), so a
            // malformed expression that bails out early doesn't pay for a WeakIntern.
            SkipWhiteSpace();
            ImmutableArray<ItemTransform>.Builder? transforms = null;

            // If there's an '->' eat it and the subsequent quoted expression or transform function
            while (TryConsume('-', '>'))
            {
                SkipWhiteSpace();

                if (TryParseQuotedTransform(out ItemTransform quotedTransform))
                {
                    // PERF: Almost all expressions have only one capture, so optimize for that case
                    transforms ??= ImmutableArray.CreateBuilder<ItemTransform>(initialCapacity: 1);
                    transforms.Add(quotedTransform);

                    SkipWhiteSpace();
                    continue;
                }

                if (TryParseFunctionTransform(out ItemTransform functionCapture))
                {
                    // PERF: Almost all expressions have only one capture, so optimize for that case
                    transforms ??= ImmutableArray.CreateBuilder<ItemTransform>(initialCapacity: 1);
                    transforms.Add(functionCapture);

                    SkipWhiteSpace();
                    continue;
                }

                // Saw '->' but neither a quoted transform nor a transform function followed: malformed.
                _index = start;
                return false;
            }

            SkipWhiteSpace();

            string? separator = null;
            int separatorStart = -1;

            // If there's a ',', eat it and the subsequent quoted expression
            if (TryConsume(','))
            {
                SkipWhiteSpace();

                if (!TryConsume('\''))
                {
                    _index = start;
                    return false;
                }

                int closingQuote = _expression.IndexOf('\'', _index, _end - _index);
                if (closingQuote == -1)
                {
                    _index = start;
                    return false;
                }

                separatorStart = _index - startPoint;
                separator = _expression.Substring(_index, closingQuote - _index);

                _index = closingQuote + 1;
            }

            SkipWhiteSpace();

            if (!TryConsume(')'))
            {
                _index = start;
                return false;
            }

            int endPoint = _index;

            // Create an expression capture that encompasses the entire expression between the @( and the )
            // with the item name and any separator contained within it
            // and each transform expression contained within it (i.e. each ->XYZ)
            result = new ItemVector(
                text: Strings.WeakIntern(_expression.AsSpan(startPoint, endPoint - startPoint)),
                index: startPoint,
                length: endPoint - startPoint,
                itemType: Strings.WeakIntern(itemNameSpan),
                separator: separator,
                separatorStart: separatorStart,
                transforms: transforms?.DrainToImmutable() ?? []);

            return true;
        }

        /// <summary>
        ///  Finds referenced item names and metadata within the scan range and records them in
        ///  <paramref name="pair"/>.
        /// </summary>
        /// <param name="pair">The table of item names and metadata references to populate.</param>
        /// <param name="includeItemTypes">Whether to record item names found in item list expressions.</param>
        /// <param name="includeMetadataOutsideTransforms">Whether to record metadata references found outside transforms.</param>
        /// <remarks>
        ///  We can ignore any semicolons in the expression, since we're not itemizing it.
        /// </remarks>
        public void GetReferencedItemNamesAndMetadata(ref ItemsAndMetadataPair pair, bool includeItemTypes, bool includeMetadataOutsideTransforms)
        {
            while (_index < _end)
            {
                // Find the next '@' or '%'; bail out if there's no room for a '(' after it.
                _index = _expression.IndexOfAny(s_markers, _index, _end - _index);
                if (_index < 0 || _index + 1 >= _end)
                {
                    return;
                }

                // Only '@(' and '%(' are markers; skip a bare '@' or '%'.
                if (_expression[_index + 1] != '(')
                {
                    _index++;
                    continue;
                }

                char marker = _expression[_index];

                // Skip past the marker's two opening characters. If the expression turns out to be
                // malformed, scanning resumes here.
                _index += 2;
                int restartPoint = _index;

                if (marker == '@')
                {
                    // Start of a possible item list expression.
                    SkipWhiteSpace();

                    if (!TryParseName(out ReadOnlySpan<char> itemNameSpan))
                    {
                        _index = restartPoint;
                        continue;
                    }

                    // Hold the name as a span and continue to verify it's a well-formed expression
                    // before we store it.
                    SkipWhiteSpace();

                    bool malformed = false;

                    // If there's an '->' eat it and the subsequent quoted expression or transform function
                    while (!malformed && TryConsume('-', '>'))
                    {
                        SkipWhiteSpace();

                        if (TryConsumeQuotedTransform())
                        {
                            SkipWhiteSpace();
                            continue;
                        }

                        if (TryConsumeFunctionTransform())
                        {
                            SkipWhiteSpace();
                            continue;
                        }

                        _index = restartPoint;
                        malformed = true;
                    }

                    if (malformed)
                    {
                        continue;
                    }

                    SkipWhiteSpace();

                    // If there's a ',', eat it and the subsequent quoted expression
                    if (TryConsume(','))
                    {
                        SkipWhiteSpace();

                        if (!TryConsume('\''))
                        {
                            _index = restartPoint;
                            continue;
                        }

                        int closingQuote = _expression.IndexOf('\'', _index, _end - _index);
                        if (closingQuote == -1)
                        {
                            _index = restartPoint;
                            continue;
                        }

                        // Look for metadata in the separator expression
                        // e.g., @(foo, '%(bar)') contains batchable metadata 'bar'
                        Scanner separatorScanner = new(_expression, _index, closingQuote);
                        separatorScanner.GetReferencedItemNamesAndMetadata(ref pair, includeItemTypes: false, includeMetadataOutsideTransforms: true);

                        _index = closingQuote + 1;
                    }

                    SkipWhiteSpace();

                    if (!TryConsume(')'))
                    {
                        _index = restartPoint;
                        continue;
                    }

                    // If we've got this far, we know the item expression was
                    // well formed, so make sure the name's in the table
                    if (includeItemTypes)
                    {
                        pair.Items ??= new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
                        pair.Items.Add(itemNameSpan.ToString());
                    }
                }
                else
                {
                    // Start of a possible metadata expression.
                    if (!TryParseMetadataExpression(out string? itemName, out string? metadataName))
                    {
                        _index = restartPoint;
                        continue;
                    }

                    if (includeMetadataOutsideTransforms)
                    {
                        string qualifiedMetadataName = itemName != null ? $"{itemName}.{metadataName}" : metadataName;
                        pair.Metadata ??= new Dictionary<string, MetadataReference>(MSBuildNameIgnoreCaseComparer.Default);
                        pair.Metadata[qualifiedMetadataName] = new MetadataReference(itemName, metadataName);
                    }
                }
            }
        }

        /// <summary>
        ///  Attempts to parse a metadata expression of the form <c>%(Name)</c> or <c>%(ItemType.Name)</c>,
        ///  starting just after the <c>%(</c> has been consumed (i.e., the scan position points at the
        ///  first character after the opening parenthesis).
        /// </summary>
        /// <param name="itemType">The item type if qualified; otherwise <see langword="null"/>.</param>
        /// <param name="metadataName">The metadata name.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid metadata expression was parsed.
        /// </returns>
        /// <remarks>
        ///  On success, the scan position is left one past the closing <c>)</c>. On failure, it is at an
        ///  indeterminate position and the caller should restore it from a saved restart point.
        /// </remarks>
        public bool TryParseMetadataExpression(out string? itemType, [NotNullWhen(true)] out string? metadataName)
        {
            itemType = null;
            metadataName = null;

            int start = _index;

            SkipWhiteSpace();

            if (!TryParseName(out ReadOnlySpan<char> firstNameSpan))
            {
                _index = start;
                return false;
            }

            string firstName = Strings.WeakIntern(firstNameSpan);

            SkipWhiteSpace();

            if (TryConsume('.'))
            {
                // Qualified: %(ItemType.Name)
                itemType = firstName;

                SkipWhiteSpace();

                if (!TryParseName(out ReadOnlySpan<char> metadataNameSpan))
                {
                    _index = start;
                    return false;
                }

                metadataName = Strings.WeakIntern(metadataNameSpan);

                SkipWhiteSpace();
            }
            else
            {
                // Unqualified: %(Name)
                metadataName = firstName;
            }

            if (!TryConsume(')'))
            {
                _index = start;
                return false;
            }

            return true;
        }

        /// <summary>
        ///  Consumes a single-quoted transform (e.g. <c>'foo'</c>) beginning at the current position,
        ///  advancing past the closing quote.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if a single-quoted transform was consumed.
        /// </returns>
        private bool TryConsumeQuotedTransform()
        {
            int start = _index;

            if (!TryConsume('\''))
            {
                return false;
            }

            int startIndex = _index;
            int endIndex = _expression.IndexOf('\'', startIndex, _end - startIndex);

            if (endIndex < 0)
            {
                _index = start;
                return false;
            }

            _index = endIndex + 1;
            return true;
        }

        /// <summary>
        ///  Attempts to parse a single-quoted transform (e.g. <c>'foo'</c>) beginning at the current
        ///  position, capturing its quoted contents into <paramref name="result"/>.
        /// </summary>
        /// <param name="result">The parsed transform if one was found; otherwise <see langword="default"/>.</param>
        /// <returns>
        ///  <see langword="true"/> if a single-quoted transform was parsed.
        /// </returns>
        private bool TryParseQuotedTransform(out ItemTransform result)
        {
            int start = _index;

            if (!TryConsumeQuotedTransform())
            {
                result = default;
                return false;
            }

            // Exclude the enclosing quotes: start is at the opening ' and _index is one past the closing '.
            result = new ItemTransform(text: _expression.Substring(start + 1, _index - start - 2));
            return true;
        }

        /// <summary>
        ///  Consumes a parenthesized argument list (e.g. <c>(a, 'b(c)')</c>) beginning at the current
        ///  position, matching nested parentheses and skipping over quoted sections. Leaves the position
        ///  one past the closing <c>)</c>.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if a balanced argument list was consumed.
        /// </returns>
        private bool TryConsumeArgumentList()
        {
            int start = _index;

            // The opening '(' is required; bail out (without pinning) if it isn't there.
            if (_index >= _end || _expression[_index] != '(')
            {
                return false;
            }

            int nestLevel = 1;
            _index++;

            unsafe
            {
                fixed (char* pchar = _expression)
                {
                    // Scan for our closing ')'
                    while (_index < _end && nestLevel > 0)
                    {
                        char character = pchar[_index];

                        switch (character)
                        {
                            case '\'' or '`' or '"':
                                // Skip to the matching closing quote (the opening one is already consumed).
                                _index++;

                                while (_index < _end && pchar[_index] != character)
                                {
                                    _index++;
                                }

                                if (_index >= _end)
                                {
                                    _index = start;
                                    return false;
                                }

                                break;

                            case '(':
                                nestLevel++;
                                break;

                            case ')':
                                nestLevel--;
                                break;
                        }

                        _index++;
                    }
                }
            }

            if (nestLevel != 0)
            {
                _index = start;
                return false;
            }

            return true;
        }

        /// <summary>
        ///  Consumes a parenthesized argument list at the current position and returns its contents (the
        ///  text between the enclosing parentheses, exclusive) as a span over the expression. This is the
        ///  value-returning analog of <see cref="TryConsumeArgumentList"/>.
        /// </summary>
        /// <param name="arguments">The argument-list contents if one was found; otherwise an empty span.</param>
        /// <returns>
        ///  <see langword="true"/> if a balanced argument list was consumed.
        /// </returns>
        private bool TryParseArgumentList(out ReadOnlySpan<char> arguments)
        {
            int start = _index;

            if (!TryConsumeArgumentList())
            {
                arguments = default;
                return false;
            }

            // Exclude the enclosing parentheses: start is at the '(' and _index is one past the ')'.
            arguments = _expression.AsSpan(start + 1, _index - start - 2);
            return true;
        }

        /// <summary>
        /// Returns true if a item function subexpression begins at the current position
        /// and ends before the end of the scan range.
        /// Leaves the position one past the end of the closing paren.
        /// </summary>
        private bool TryConsumeFunctionTransform()
        {
            int start = _index;

            if (TryConsumeName())
            {
                // Eat any whitespace between the function name and its arguments
                SkipWhiteSpace();

                if (TryConsumeArgumentList())
                {
                    return true;
                }
            }

            _index = start;
            return false;
        }

        /// <summary>
        ///  Attempts to parse an item function transform (e.g. <c>Distinct()</c>) beginning at the
        ///  current position, capturing it into <paramref name="result"/>.
        /// </summary>
        /// <param name="result">The parsed transform if one was found; otherwise <see langword="default"/>.</param>
        /// <returns>
        ///  <see langword="true"/> if an item function transform was parsed.
        /// </returns>
        private bool TryParseFunctionTransform(out ItemTransform result)
        {
            int start = _index;

            if (TryParseName(out ReadOnlySpan<char> functionNameSpan))
            {
                // Eat any whitespace between the function name and its arguments
                SkipWhiteSpace();

                if (TryParseArgumentList(out ReadOnlySpan<char> argumentsSpan))
                {
                    result = new ItemTransform(
                        text: _expression.Substring(start, _index - start),
                        functionName: Strings.WeakIntern(functionNameSpan),
                        functionArguments: argumentsSpan.IsEmpty ? null : Strings.WeakIntern(argumentsSpan));

                    return true;
                }
            }

            result = default;
            _index = start;
            return false;
        }

        /// <summary>
        /// Returns true if a valid name begins at the current position.
        /// Leaves the position one past the end of the name.
        /// </summary>
        /// <remarks>
        /// The accepted grammar is <c>[A-Za-z_][A-Za-z_0-9\-]*</c> (via
        /// <see cref="XmlUtilities.IsValidInitialElementNameCharacter"/> and
        /// <see cref="XmlUtilities.IsValidSubsequentElementNameCharacter"/>), which defines a valid item
        /// type or metadata name. This MUST be kept in sync with
        /// <see cref="ProjectWriter.itemTypeOrMetadataNameSpecification"/>: if the grammar used to parse
        /// item/metadata expressions diverges from the one used to write them back out, expressions could
        /// round-trip incorrectly.
        /// </remarks>
        private bool TryConsumeName()
        {
            if (_end <= _index || !XmlUtilities.IsValidInitialElementNameCharacter(_expression[_index]))
            {
                return false;
            }

            _index++;

            while (_end > _index && XmlUtilities.IsValidSubsequentElementNameCharacter(_expression[_index]))
            {
                _index++;
            }

            // '-' is a legitimate char in an item name, but we should match '->' as an arrow
            // in '@(foo->'x')' rather than as the last char of the item name.
            // The old regex accomplished this by being "greedy"
            if (_end > _index && _expression[_index - 1] == '-' && _expression[_index] == '>')
            {
                _index--;
            }

            return true;
        }

        /// <summary>
        ///  Attempts to consume a valid name at the current position, returning it as a span over the
        ///  expression. This is the value-returning analog of <see cref="TryConsumeName"/>; callers can
        ///  pass the returned span to <see cref="Strings.WeakIntern(ReadOnlySpan{char})"/> to realize a
        ///  string without an intermediate substring allocation.
        /// </summary>
        /// <param name="name">The consumed name if one was found; otherwise an empty span.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid name was consumed.
        /// </returns>
        private bool TryParseName(out ReadOnlySpan<char> name)
        {
            int start = _index;

            if (!TryConsumeName())
            {
                name = default;
                return false;
            }

            name = _expression.AsSpan(start, _index - start);
            return true;
        }

        /// <summary>
        ///  Returns <see langword="true"/> if the character at the current position (which must be before
        ///  the end of the scan range) is the specified char. Leaves the position one past the character.
        /// </summary>
        private bool TryConsume(char c)
        {
            if (_index < _end && _expression[_index] == c)
            {
                _index++;
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Returns <see langword="true"/> if the next two characters at the current position are the specified sequence.
        ///  Leaves the position one past the second character.
        /// </summary>
        private bool TryConsume(char c1, char c2)
        {
            if (_index < _end - 1 && _expression[_index] == c1 && _expression[_index + 1] == c2)
            {
                _index += 2;
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Moves past all whitespace at the current position, without scanning at or beyond the end of
        ///  the scan range.
        /// </summary>
        /// <remarks>
        ///  <see cref="char.IsWhiteSpace(char)"/> is not identical in behavior to regex's <c>\s</c> character class,
        ///  but it's extremely close, and it's what we use in conditional expressions.
        /// </remarks>
        private void SkipWhiteSpace()
        {
            while (_index < _end && char.IsWhiteSpace(_expression[_index]))
            {
                _index++;
            }
        }
    }
}
