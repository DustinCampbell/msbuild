// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionShredder
{
    /// <summary>
    ///  Scans a bounded range of an MSBuild expression while maintaining the current position.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="start">The initial scan position.</param>
    /// <param name="end">The exclusive upper bound of the scan.</param>
    private ref struct Scanner(string expression, int start, int end)
    {
        /// <summary>
        ///  The expression being scanned.
        /// </summary>
        private readonly string _expression = expression;

        /// <summary>
        ///  The exclusive upper bound of the scan.
        /// </summary>
        private readonly int _end = end;

        /// <summary>
        ///  The current scan position.
        /// </summary>
        private int _position = start;

        /// <summary>
        ///  Initializes a scanner for an entire expression.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        public Scanner(string expression)
            : this(expression, 0, expression.Length)
        {
        }

        /// <summary>
        ///  Initializes a scanner for a range of an expression.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="range">The range of the expression to scan.</param>
        public Scanner(string expression, Range range)
            : this(
                expression,
                range.Start.GetOffset(expression.Length),
                range.End.GetOffset(expression.Length))
        {
        }

        /// <summary>
        ///  Gets the current scan position.
        /// </summary>
        public readonly int Position => _position;

        /// <summary>
        ///  Finds and parses the next valid item-vector expression at or after the current position.
        /// </summary>
        /// <param name="itemVector">The parsed item-vector expression.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid item-vector expression is found; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        public bool TryGetNextItemVector(out ItemVector itemVector)
        {
            while ((_position = IndexOfItemVectorMarker(_expression, _position)) >= 0)
            {
                if (TryParseItemVector(out itemVector))
                {
                    return true;
                }

                _position += 2;
            }

            itemVector = default;
            return false;
        }

        /// <summary>
        ///  Parses an item-vector expression beginning at the current position.
        /// </summary>
        /// <param name="itemVector">The parsed item-vector expression.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid item-vector expression is parsed; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing <c>)</c>. On failure, the position is unchanged.
        /// </remarks>
        private bool TryParseItemVector(out ItemVector itemVector)
        {
            Debug.Assert(
                _position < _end - 1 &&
                _expression[_position] == '@' &&
                _expression[_position + 1] == '(',
                "The current position must be the start of an item-vector marker.");

            int startIndex = _position;
            _position += 2;

            SkipWhiteSpace();

            if (!TryScanItemVectorName(out Range itemVectorNameRange))
            {
                _position = startIndex;
                itemVector = default;
                return false;
            }

            SkipWhiteSpace();

            TransformsBuilder builder = default;
            if (!TryParseItemVectorTransforms(ref builder))
            {
                _position = startIndex;
                itemVector = default;
                return false;
            }

            SkipWhiteSpace();

            bool hasSeparator = TryScanItemVectorSeparator(out Range separatorRange);

            SkipWhiteSpace();

            if (!TryConsume(')'))
            {
                _position = startIndex;
                itemVector = default;
                return false;
            }

            int length = _position - startIndex;
            string itemType = GetWeakInternedString(itemVectorNameRange);

            (string? separator, int separatorStart) = hasSeparator
                ? (_expression[separatorRange], separatorRange.Start.Value - startIndex)
                : (null, -1);

            // Create an ItemVector that encompasses the entire expression delimited by @( and the )
            // with the item name and any separator contained within it
            // and each transform expression contained within it (i.e. each ->XYZ)
            itemVector = new ItemVector(
                text: Strings.WeakIntern(_expression.AsSpan(startIndex, length)),
                index: startIndex,
                itemType,
                separator,
                separatorStart,
                builder.DrainToImmutable());

            return true;
        }

        /// <summary>
        ///  Parses the item-vector transform chain beginning at the current position.
        /// </summary>
        /// <param name="builder">The builder to which parsed transforms are added.</param>
        /// <returns>
        ///  <see langword="true"/> if every transform is valid; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  Zero transforms is valid. On success, the position is left after any whitespace following the last
        ///  transform. On failure, the position is indeterminate and should be restored by callers that need to
        ///  recover.
        /// </remarks>
        private bool TryParseItemVectorTransforms(ref TransformsBuilder builder)
        {
            while (TryConsume('-', '>'))
            {
                SkipWhiteSpace();

                if (!TryParseQuotedTransform(out ItemTransform transform) &&
                    !TryParseFunctionTransform(out transform))
                {
                    return false;
                }

                builder.Add(transform);

                SkipWhiteSpace();
            }

            return true;
        }

        /// <summary>
        ///  Advances past the item-vector transform chain without constructing transforms.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if every transform is valid; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  Zero transforms is valid. On success, the position is left after any whitespace following the last
        ///  transform. On failure, the position is indeterminate and should be restored by callers that need to
        ///  recover.
        /// </remarks>
        private bool TryScanItemVectorTransforms()
        {
            while (TryConsume('-', '>'))
            {
                SkipWhiteSpace();

                if (!TryScanQuotedTransform() &&
                    !TryScanFunctionTransform())
                {
                    return false;
                }

                SkipWhiteSpace();
            }

            return true;
        }

        /// <summary>
        ///  Collects item names and metadata references in the scanner's bounded range.
        /// </summary>
        /// <param name="pair">The collections to which references are added.</param>
        /// <param name="collectItemTypes">
        ///  <see langword="true"/> to collect item type names; otherwise, <see langword="false"/>.
        /// </param>
        /// <param name="collectMetadataOutsideTransforms">
        ///  <see langword="true"/> to collect metadata outside transforms; otherwise, <see langword="false"/>.
        /// </param>
        /// <remarks>
        ///  Item transforms are scanned for syntax but their contents are excluded. Metadata in item separators is
        ///  collected recursively. When a candidate expression is malformed, scanning resumes from its recovery
        ///  position so nested markers are not skipped.
        /// </remarks>
        public void CollectReferencedItemNamesAndMetadata(
            ref ItemsAndMetadataPair pair,
            bool collectItemTypes,
            bool collectMetadataOutsideTransforms)
        {
            while (_position < _end)
            {
                int markerIndex = _expression.IndexOfAny(s_itemVectorOrMetadataMarkerPrefixes, _position, _end - _position);
                if (markerIndex < 0)
                {
                    break;
                }

                char markerPrefix = _expression[markerIndex];
                _position = markerIndex + 1;

                if (_position >= _end)
                {
                    break;
                }

                if (_expression[_position] != '(')
                {
                    continue;
                }

                _position++;
                int recoveryPosition = _position;

                if (markerPrefix == '@')
                {
                    // Start of a possible item list expression
                    SkipWhiteSpace();

                    if (!TryScanItemVectorName(out Range itemVectorNameRange))
                    {
                        _position = recoveryPosition;
                        continue;
                    }

                    SkipWhiteSpace();

                    if (!TryScanItemVectorTransforms())
                    {
                        _position = recoveryPosition;
                        continue;
                    }

                    SkipWhiteSpace();

                    if (TryScanItemVectorSeparator(out Range separatorRange))
                    {
                        // Look for metadata in the separator expression
                        // e.g., @(foo, '%(bar)') contains batchable metadata 'bar'
                        Scanner separatorScanner = new(_expression, separatorRange);
                        separatorScanner.CollectReferencedItemNamesAndMetadata(
                            ref pair,
                            collectItemTypes: false,
                            collectMetadataOutsideTransforms: true);
                    }

                    SkipWhiteSpace();

                    if (!TryConsume(')'))
                    {
                        _position = recoveryPosition;
                        continue;
                    }

                    // If we've got this far, we know the item expression was
                    // well formed, so make sure the name's in the table
                    if (collectItemTypes)
                    {
                        pair.Items ??= new(MSBuildNameIgnoreCaseComparer.Default);
                        pair.Items.Add(_expression[itemVectorNameRange]);
                    }

                    continue;
                }

                // Start of a possible metadata expression
                if (!TryScanMetadataExpression(out Range? itemTypeRange, out Range metadataNameRange))
                {
                    _position = recoveryPosition;
                    continue;
                }

                if (collectMetadataOutsideTransforms)
                {
                    string? itemName = GetWeakInternedString(itemTypeRange);
                    string metadataName = GetWeakInternedString(metadataNameRange);
                    string qualifiedMetadataName = itemName != null ? $"{itemName}.{metadataName}" : metadataName;
                    pair.Metadata ??= new(MSBuildNameIgnoreCaseComparer.Default);
                    pair.Metadata[qualifiedMetadataName] = new MetadataReference(itemName, metadataName);
                }
            }
        }

        /// <summary>
        ///  Scans an item-vector separator beginning at the current position.
        /// </summary>
        /// <param name="separatorRange">
        ///  The absolute, from-start range of the separator when successful.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> if a valid separator is found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing quote. On failure, the position is unchanged.
        /// </remarks>
        private bool TryScanItemVectorSeparator(out Range separatorRange)
        {
            int start = _position;

            separatorRange = default;

            if (!TryConsume(','))
            {
                return false;
            }

            SkipWhiteSpace();

            if (!TryConsume('\''))
            {
                _position = start;
                return false;
            }

            int contentStart = _position;
            int contentEnd = _expression.IndexOf('\'', contentStart);
            if (contentEnd < 0)
            {
                _position = start;
                return false;
            }

            separatorRange = contentStart..contentEnd;
            _position = contentEnd + 1;
            return true;
        }

        /// <summary>
        ///  Parses a metadata expression after its <c>%(</c> marker has been consumed.
        /// </summary>
        /// <param name="itemType">The item type for qualified metadata; otherwise, <see langword="null"/>.</param>
        /// <param name="metadataName">The metadata name when parsing succeeds.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid metadata expression is parsed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing <c>)</c>. On failure, the position is
        ///  indeterminate and should be restored by callers that need to recover.
        /// </remarks>
        public bool TryParseMetadataExpression(out string? itemType, [NotNullWhen(true)] out string? metadataName)
        {
            if (TryScanMetadataExpression(out Range? itemTypeRange, out Range metadataNameRange))
            {
                itemType = GetWeakInternedString(itemTypeRange);
                metadataName = GetWeakInternedString(metadataNameRange);
                return true;
            }

            itemType = null;
            metadataName = null;
            return false;
        }

        /// <summary>
        ///  Scans a metadata expression after its <c>%(</c> marker has been consumed.
        /// </summary>
        /// <param name="itemTypeRange">
        ///  The absolute, from-start range of the item type when the expression is qualified; otherwise,
        ///  <see langword="null"/>.
        /// </param>
        /// <param name="metadataNameRange">
        ///  The absolute, from-start range of the metadata name when successful.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> if a valid metadata expression is found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing <c>)</c>. On failure, the position is
        ///  indeterminate and should be restored by callers that need to recover.
        /// </remarks>
        private bool TryScanMetadataExpression(out Range? itemTypeRange, out Range metadataNameRange)
        {
            itemTypeRange = null;
            metadataNameRange = default;

            SkipWhiteSpace();

            if (!TryScanName(out Range firstNameRange))
            {
                return false;
            }

            SkipWhiteSpace();

            Range? scannedItemTypeRange;
            Range scannedMetadataNameRange;

            if (TryConsume('.'))
            {
                // Qualified: %(ItemType.Name)
                scannedItemTypeRange = firstNameRange;

                SkipWhiteSpace();

                if (!TryScanName(out scannedMetadataNameRange))
                {
                    return false;
                }

                SkipWhiteSpace();
            }
            else
            {
                // Unqualified: %(Name)
                scannedItemTypeRange = null;
                scannedMetadataNameRange = firstNameRange;
            }

            if (!TryConsume(')'))
            {
                return false;
            }

            itemTypeRange = scannedItemTypeRange;
            metadataNameRange = scannedMetadataNameRange;
            return true;
        }

        /// <summary>
        ///  Parses a quoted item transform beginning at the current position.
        /// </summary>
        /// <param name="transform">The parsed transform when successful.</param>
        /// <returns>
        ///  <see langword="true"/> if a quoted transform was parsed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing quote.
        /// </remarks>
        private bool TryParseQuotedTransform(out ItemTransform transform)
        {
            if (TryScanQuotedTransform(out Range transformRange))
            {
                transform = new ItemTransform(
                    text: _expression[transformRange],
                    index: transformRange.Start.Value);
                return true;
            }

            transform = default;
            return false;
        }

        /// <summary>
        ///  Advances past a quoted item transform without constructing an <see cref="ItemTransform"/>.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if a quoted transform was found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing quote.
        /// </remarks>
        private bool TryScanQuotedTransform()
        {
            if (!TryConsume('\''))
            {
                return false;
            }

            while (_position < _end && _expression[_position] != '\'')
            {
                _position++;
            }

            _position++;

            return _end > _position;
        }

        /// <summary>
        ///  Scans a quoted item transform beginning at the current position.
        /// </summary>
        /// <param name="transformRange">
        ///  The absolute, from-start range of the transform text, excluding quotes, when successful.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> if a quoted transform was found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing quote.
        /// </remarks>
        private bool TryScanQuotedTransform(out Range transformRange)
        {
            if (!TryConsume('\''))
            {
                transformRange = default;
                return false;
            }

            int startTransform = _position;

            while (_position < _end && _expression[_position] != '\'')
            {
                _position++;
            }

            _position++;

            if (_end <= _position)
            {
                transformRange = default;
                return false;
            }

            transformRange = startTransform..(_position - 1);
            return true;
        }

        /// <summary>
        ///  Advances past an argument list without reporting its range.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if a complete argument list was found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing parenthesis.
        /// </remarks>
        private bool TryScanArgumentList()
        {
            if (!TryConsume('('))
            {
                return false;
            }

            return TryScanArgumentListContents();
        }

        /// <summary>
        ///  Scans an argument list beginning at the current position.
        /// </summary>
        /// <param name="argumentsRange">
        ///  The absolute, from-start range of non-empty argument text, excluding parentheses; otherwise,
        ///  <see langword="null"/>.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> if a complete argument list was found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  Nested parentheses are supported. Parentheses inside single quotes, double quotes, and backticks are
        ///  ignored. On success, the position is left one past the closing parenthesis.
        /// </remarks>
        private bool TryScanArgumentList(out Range? argumentsRange)
        {
            if (!TryConsume('('))
            {
                argumentsRange = null;
                return false;
            }

            int startArguments = _position;

            if (!TryScanArgumentListContents())
            {
                argumentsRange = null;
                return false;
            }

            int endArguments = _position - 1;
            argumentsRange = startArguments < endArguments ? startArguments..endArguments : null;
            return true;
        }

        /// <summary>
        ///  Scans argument-list contents after the opening parenthesis has been consumed.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if the closing parenthesis was found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  Nested parentheses are supported. Parentheses inside single quotes, double quotes, and backticks are
        ///  ignored. On success, the position is left one past the closing parenthesis.
        /// </remarks>
        private bool TryScanArgumentListContents()
        {
            Debug.Assert((uint)_end <= (uint)_expression.Length, "The scan end must be within the expression.");

            int nestLevel = 1;
            unsafe
            {
                fixed (char* pchar = _expression)
                {
                    // Scan for our closing ')'
                    while (_position < _end && nestLevel > 0)
                    {
                        char character = pchar[_position];

                        switch (character)
                        {
                            case '\'' or '`' or '"':
                                int index = _position + 1;
                                int closeQuoteIndex = _expression.IndexOf(character, index, _end - index);
                                if (closeQuoteIndex < 0)
                                {
                                    return false;
                                }

                                _position = closeQuoteIndex;
                                break;

                            case '(':
                                nestLevel++;
                                break;

                            case ')':
                                nestLevel--;
                                break;
                        }

                        _position++;
                    }
                }
            }

            return nestLevel == 0;
        }

        /// <summary>
        ///  Parses a function item transform beginning at the current position.
        /// </summary>
        /// <param name="transform">The parsed transform when successful.</param>
        /// <returns>
        ///  <see langword="true"/> if a function transform was parsed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing parenthesis.
        /// </remarks>
        private bool TryParseFunctionTransform(out ItemTransform transform)
        {
            if (TryScanFunctionTransform(out Range functionNameRange, out Range? argumentsRange))
            {
                int startTransform = functionNameRange.Start.Value;
                string functionName = _expression[functionNameRange];
                string? functionArguments = GetWeakInternedString(argumentsRange);

                transform = new ItemTransform(
                    text: _expression.Substring(startTransform, _position - startTransform),
                    index: startTransform,
                    functionName,
                    functionArguments);
                return true;
            }

            transform = default;
            return false;
        }

        /// <summary>
        ///  Advances past a function item transform without constructing an <see cref="ItemTransform"/>.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if a function transform was found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing parenthesis.
        /// </remarks>
        private bool TryScanFunctionTransform()
        {
            if (!TryScanName())
            {
                return false;
            }

            SkipWhiteSpace();

            return TryScanArgumentList();
        }

        /// <summary>
        ///  Scans a function item transform and reports the boundaries of its name and arguments.
        /// </summary>
        /// <param name="functionNameRange">
        ///  The absolute, from-start range of the function name when successful.
        /// </param>
        /// <param name="argumentsRange">
        ///  The absolute, from-start range of non-empty argument text, excluding parentheses; otherwise,
        ///  <see langword="null"/>.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> if a valid function transform is found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  On success, the position is left one past the closing parenthesis.
        /// </remarks>
        private bool TryScanFunctionTransform(out Range functionNameRange, out Range? argumentsRange)
        {
            if (!TryScanName(out functionNameRange))
            {
                argumentsRange = null;
                return false;
            }

            // Eat any whitespace between the function name and its arguments.
            SkipWhiteSpace();

            return TryScanArgumentList(out argumentsRange);
        }

        /// <summary>
        ///  Scans an item-vector name beginning at the current position.
        /// </summary>
        /// <param name="itemVectorNameRange">
        ///  The absolute, from-start range of the item-vector name when successful.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> if a valid item-vector name is found; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryScanItemVectorName(out Range itemVectorNameRange)
        {
            if (!TryScanName(out itemVectorNameRange))
            {
                return false;
            }

            // '-' is a valid name character, but the final '-' in '->' starts the item transform arrow.
            if (_end > _position &&
                _expression[_position - 1] == '-' &&
                _expression[_position] == '>')
            {
                _position--;
                itemVectorNameRange = itemVectorNameRange.Start.._position;
            }

            return true;
        }

        /// <summary>
        ///  Scans a valid name beginning at the current position and reports its range.
        /// </summary>
        /// <param name="nameRange">The absolute, from-start range of the name when successful.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid name is found; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryScanName(out Range nameRange)
        {
            int start = _position;

            if (!TryScanName())
            {
                nameRange = default;
                return false;
            }

            nameRange = start.._position;
            return true;
        }

        /// <summary>
        ///  Scans a valid name beginning at the current position.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> if a valid name is found; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        ///  The accepted grammar is <c>[A-Za-z_][A-Za-z_0-9\-]*</c> (via
        ///  <see cref="XmlUtilities.IsValidInitialElementNameCharacter"/> and
        ///  <see cref="XmlUtilities.IsValidSubsequentElementNameCharacter"/>), which defines a valid item
        ///  type or metadata name. This MUST be kept in sync with
        ///  <see cref="ProjectWriter.itemTypeOrMetadataNameSpecification"/>: if the grammar used to parse
        ///  item/metadata expressions diverges from the one used to write them back out, expressions could
        ///  round-trip incorrectly.
        /// </remarks>
        private bool TryScanName()
        {
            if (_end <= _position ||
                !XmlUtilities.IsValidInitialElementNameCharacter(_expression[_position]))
            {
                return false;
            }

            _position++;

            while (_end > _position &&
                XmlUtilities.IsValidSubsequentElementNameCharacter(_expression[_position]))
            {
                _position++;
            }

            return true;
        }

        /// <summary>
        ///  Returns <see langword="true"/> if the character at the current position is the specified character.
        ///  Leaves the position one past the character.
        /// </summary>
        /// <param name="c">The character to consume.</param>
        /// <returns>
        ///  <see langword="true"/> if the character is consumed; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryConsume(char c)
        {
            if (_position < _end && _expression[_position] == c)
            {
                _position++;
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Returns <see langword="true"/> if the next two characters at the current position are the specified
        ///  sequence. Leaves the position one past the second character.
        /// </summary>
        /// <param name="c1">The first character to consume.</param>
        /// <param name="c2">The second character to consume.</param>
        /// <returns>
        ///  <see langword="true"/> if both characters are consumed; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryConsume(char c1, char c2)
        {
            if (_position < _end - 1 &&
                _expression[_position] == c1 &&
                _expression[_position + 1] == c2)
            {
                _position += 2;
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Moves past all whitespace starting at the current position without scanning beyond the bounded range.
        /// </summary>
        /// <remarks>
        ///  <see cref="char.IsWhiteSpace(char)"/> is not identical in behavior to regex's <c>\s</c> character
        ///  class, but it's extremely close, and it's what we use in conditional expressions.
        /// </remarks>
        private void SkipWhiteSpace()
        {
            while (_position < _end && char.IsWhiteSpace(_expression[_position]))
            {
                _position++;
            }
        }

        /// <summary>
        ///  Gets a weakly interned string from an absolute, from-start range.
        /// </summary>
        /// <param name="range">The range of the string.</param>
        /// <returns>
        ///  The weakly interned string.
        /// </returns>
        private string GetWeakInternedString(Range range)
        {
            int start = range.Start.Value;
            return Strings.WeakIntern(_expression.AsSpan(start, range.End.Value - start));
        }

        /// <summary>
        ///  Gets a weakly interned string from an optional absolute, from-start range.
        /// </summary>
        /// <param name="range">The optional range of the string.</param>
        /// <returns>
        ///  The weakly interned string, or <see langword="null"/> when <paramref name="range"/> is
        ///  <see langword="null"/>.
        /// </returns>
        private string? GetWeakInternedString(Range? range)
            => range is Range value ? GetWeakInternedString(value) : null;
    }
}
