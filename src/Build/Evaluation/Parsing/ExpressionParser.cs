// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation.Parsing;

/// <summary>
/// Parser for MSBuild text expressions containing property, metadata, and item references.
/// </summary>
/// <remarks>
/// This is a ref struct for performance - it can work directly on stack-allocated spans
/// and avoid heap allocations during parsing.
/// </remarks>
internal ref struct ExpressionParser
{
    private readonly ReadOnlyMemory<char> _expression;
    private ReadOnlySpan<char> _remaining;
    private int _position;

    /// <summary>
    /// Initializes a new instance of the ExpressionParser.
    /// </summary>
    /// <param name="expression">The expression text to parse.</param>
    public ExpressionParser(ReadOnlyMemory<char> expression)
    {
        _expression = expression;
        _remaining = expression.Span;
        _position = 0;
    }

    /// <summary>
    /// Initializes a new instance of the ExpressionParser.
    /// </summary>
    /// <param name="expression">The expression text to parse.</param>
    public ExpressionParser(string expression)
        : this(expression.AsMemory())
    {
    }

    /// <summary>
    /// Check if we've reached the end of input.
    /// </summary>
    private readonly bool IsAtEnd => _remaining.IsEmpty;

    /// <summary>
    /// Try to peek at the current character without consuming it.
    /// </summary>
    private readonly bool TryPeek(out char ch)
    {
        if (IsAtEnd)
        {
            ch = default;
            return false;
        }

        ch = _remaining[0];
        return true;
    }

    /// <summary>
    /// Consume and return the current character.
    /// </summary>
    private char Advance()
    {
        ErrorUtilities.VerifyThrow(!IsAtEnd, "Cannot advance past end of expression");
        char ch = _remaining[0];
        _remaining = _remaining[1..];
        _position++;
        return ch;
    }

    /// <summary>
    /// Check if the current position matches the given character and consume it if so.
    /// </summary>
    private bool Consume(char expected)
    {
        if (IsAtEnd || _remaining[0] != expected)
        {
            return false;
        }

        Advance();
        return true;
    }

    /// <summary>
    /// Try to consume the given text at the current position.
    /// </summary>
    private bool Consume(ReadOnlySpan<char> text, StringComparison comparison = StringComparison.Ordinal)
    {
        if (!_remaining.StartsWith(text, comparison))
        {
            return false;
        }

        _remaining = _remaining[text.Length..];
        _position += text.Length;
        return true;
    }

    /// <summary>
    /// Skip whitespace characters.
    /// </summary>
    private void SkipWhitespace()
    {
        int count = 0;
        while (count < _remaining.Length && char.IsWhiteSpace(_remaining[count]))
        {
            count++;
        }

        _remaining = _remaining[count..];
        _position += count;
    }

    /// <summary>
    /// Parse the complete expression.
    /// </summary>
    /// <returns>The root expression node.</returns>
    public ExpressionNode Parse()
    {
        if (IsAtEnd)
        {
            return new LiteralNode(default, _position);
        }

        return _remaining switch
        {
            ['$', '(', ..] => ParsePropertyReference(),
            ['%', '(', ..] => ParseMetadataReference(),
            ['@', '(', ..] => ParseItemReference(),
            _ => ParseLiteralText(),
        };
    }

    /// <summary>
    /// Parse a property reference: $(PropertyBody)
    /// </summary>
    private PropertyNode ParsePropertyReference()
    {
        int start = _position;

        // Expect "$("
        if (!Consume("$("))
        {
            Error("Expected property reference to start with '$('");
        }

        // Parse the body
        PropertyBodyNode body = ParsePropertyBody();

        // Expect ")"
        if (!Consume(')'))
        {
            Error("Expected closing ')' for property reference");
        }

        return new PropertyNode(body, _expression[start.._position], start);
    }

    /// <summary>
    /// Parse a property body (inside the $(...))
    /// </summary>
    private PropertyBodyNode ParsePropertyBody()
    {
        SkipWhitespace();

        return _remaining switch
        {
            // Empty property reference like $()
            [] or [')', ..]
                => new SimplePropertyNode(name: default, memberAccesses: [], text: default, _position),

            // Static function: [TypeName]::Method()
            ['[', ..] => ParseStaticFunction(),

            // Registry reference
            ['R' or 'r', 'e' or 'E', 'g' or 'G', 'i' or 'I', 's' or 'S', 't' or 'T', 'r' or 'R', 'y' or 'Y', ':', ..]
                => ParseRegistryReference(),

            // Simple property with optional member access
            _ => ParseSimpleProperty(),
        };
    }

    /// <summary>
    /// Parse a static function call: [TypeName]::MethodName(args)
    /// </summary>
    private StaticFunctionNode ParseStaticFunction()
    {
        int start = _position;

        // Expect "["
        if (!Consume('['))
        {
            Error("Expected '[' to start static function type name");
        }

        // Parse type name
        ReadOnlyMemory<char> typeName = ParseQualifiedIdentifier();

        // Expect "]"
        if (!Consume(']'))
        {
            Error("Expected ']' after static function type name");
        }

        // Expect "::"
        if (!Consume("::"))
        {
            Error("Expected '::' between type name and method name in static function");
        }

        // Parse method name
        ReadOnlyMemory<char> methodName = ParseIdentifier();

        // Parse arguments
        SkipWhitespace();
        ImmutableArray<ExpressionNode> arguments = [];

        if (Consume('('))
        {
            arguments = ParseArgumentList();
            if (!Consume(')'))
            {
                Error("Expected closing ')' for static function arguments");
            }
        }

        // Parse optional member accesses
        var memberAccesses = ImmutableArray.CreateBuilder<MemberAccessNode>();
        while (_remaining is ['.' or '[', ..])
        {
            memberAccesses.Add(ParseMemberAccess());
        }

        return new StaticFunctionNode(
            typeName,
            methodName,
            arguments,
            memberAccesses.ToImmutable(),
            _expression[start.._position],
            start);
    }

    /// <summary>
    /// Parse a registry reference: Registry:HKEY_...\Path@ValueName
    /// </summary>
    private RegistryNode ParseRegistryReference()
    {
        int start = _position;

        // Skip "Registry:"
        if (!Consume("Registry:", StringComparison.OrdinalIgnoreCase))
        {
            Error("Expected 'Registry:' prefix for registry reference");
        }

        // Parse until '@' or end of property
        int keyStart = _position;
        while (_remaining is not ([] or [')' or '@', ..]))
        {
            Advance();
        }

        ReadOnlyMemory<char> keyPath = _expression[keyStart.._position];
        ReadOnlyMemory<char> valueName = default;

        // Parse optional value name after '@'
        if (Consume('@'))
        {
            int valueStart = _position;
            while (_remaining is not ([] or [')', ..]))
            {
                Advance();
            }

            valueName = _expression[valueStart.._position];
        }

        return new RegistryNode(keyPath, valueName, _expression[start.._position], start);
    }

    /// <summary>
    /// Parse a simple property with optional member access chain.
    /// </summary>
    private SimplePropertyNode ParseSimpleProperty()
    {
        int start = _position;

        // Parse property name
        ReadOnlyMemory<char> name = ParseIdentifier();

        SkipWhitespace();

        // Parse optional member accesses
        var memberAccesses = ImmutableArray.CreateBuilder<MemberAccessNode>();

        while (_remaining is ['.' or '[', ..])
        {
            memberAccesses.Add(ParseMemberAccess());
            SkipWhitespace();
        }

        return new SimplePropertyNode(
            name,
            memberAccesses.ToImmutable(),
            _expression[start.._position],
            start);
    }

    /// <summary>
    /// Parse member access: .Method() or [indexer]
    /// </summary>
    private MemberAccessNode ParseMemberAccess()
    {
        if (Consume('.'))
        {
            int start = _position - 1; // Include the '.'
            SkipWhitespace();

            ReadOnlyMemory<char> name = ParseIdentifier();
            SkipWhitespace();

            // Check for method call
            if (Consume('('))
            {
                var arguments = ParseArgumentList();
                if (!Consume(')'))
                {
                    Error("Expected closing ')' for method call arguments");
                }

                return new MethodCallNode(name, arguments, _expression[start.._position], start);
            }
            else
            {
                // Property access (no parentheses) - treat as method with no args
                return new MethodCallNode(name, arguments: [], _expression[start.._position], start);
            }
        }
        else if (Consume('['))
        {
            int start = _position - 1; // Include the '['
            SkipWhitespace();

            // Parse the index - could be a property/metadata/item reference or a literal
            ExpressionNode index = _remaining switch
            {
                ['$', '(', ..] => ParsePropertyReference(),
                ['%', '(', ..] => ParseMetadataReference(),
                ['@', '(', ..] => ParseItemReference(),
                _ => ParseIndexerLiteral()
            };

            SkipWhitespace();
            if (!Consume(']'))
            {
                Error("Expected closing ']' for indexer");
            }

            return new IndexerNode(index, _expression[start.._position], start);
        }
        else
        {
            string found = TryPeek(out char ch) ? $"'{ch}'" : "end of input";
            Error($"Expected member access ('.' or '[') but found {found}");
            return null!;
        }
    }

    /// <summary>
    /// Parse literal content inside an indexer (everything until ']').
    /// </summary>
    private LiteralNode ParseIndexerLiteral()
    {
        int start = _position;

        // Parse until we hit ']'
        while (_remaining is not ([] or [']', ..]))
        {
            Advance();
        }

        return new LiteralNode(_expression[start.._position], start);
    }

    /// <summary>
    /// Parse a metadata reference: %(ItemType.Name) or %(Name)
    /// </summary>
    private MetadataNode ParseMetadataReference()
    {
        int start = _position;

        // Expect "%("
        if (!Consume("%("))
        {
            Error("Expected metadata reference to start with '%('");
        }

        SkipWhitespace();

        // Must have at least one identifier
        if (_remaining is [] or [')', ..])
        {
            Error("Empty metadata reference '%()' is not allowed");
        }

        ReadOnlyMemory<char> firstIdentifier = ParseIdentifier();

        SkipWhitespace();

        ReadOnlyMemory<char> itemType = default;
        ReadOnlyMemory<char> name;

        // Check if there's a dot (qualified metadata)
        if (Consume('.'))
        {
            SkipWhitespace();
            // First identifier was the item type
            itemType = firstIdentifier;
            // Parse the metadata name
            name = ParseIdentifier();
            SkipWhitespace();
        }
        else
        {
            // No dot - first identifier is the metadata name
            name = firstIdentifier;
        }

        if (!Consume(')'))
        {
            Error("Expected closing ')' for metadata reference");
        }

        return new MetadataNode(itemType, name, _expression[start.._position], start);
    }

    /// <summary>
    /// Parse an item reference: @(ItemType->transforms, 'separator')
    /// </summary>
    private ItemNode ParseItemReference()
    {
        int start = _position;

        // Expect "@("
        if (!Consume("@("))
        {
            Error("Expected item reference to start with '@('");
        }

        SkipWhitespace();

        // Parse item type
        ReadOnlyMemory<char> itemType = ParseIdentifier();

        SkipWhitespace();

        // Parse transforms (can be chained: ->transform1->transform2)
        var transforms = ImmutableArray.CreateBuilder<TransformNode>();

        while (_remaining is ['-', '>', ..])
        {
            transforms.Add(ParseTransform());
            SkipWhitespace();
        }

        // Parse optional separator
        ExpressionNode? separator = null;
        if (Consume(','))
        {
            SkipWhitespace();
            // Separator must be a quoted string
            if (_remaining is ['\'' or '"' or '`', ..])
            {
                int sepStart = _position;
                ReadOnlyMemory<char> quotedContent = ParseQuotedString();
                separator = new LiteralNode(quotedContent, sepStart);
            }
            else
            {
                Error("Item separator must be a quoted string");
            }

            SkipWhitespace();
        }

        if (!Consume(')'))
        {
            Error("Expected closing ')' for item reference");
        }

        return new ItemNode(
            itemType,
            transforms.ToImmutable(),
            separator,
            _expression[start.._position],
            start);
    }

    /// <summary>
    /// Parse a transform: ->'%(Filename)' or ->Distinct()
    /// </summary>
    private TransformNode ParseTransform()
    {
        int start = _position;

        // Expect "->"
        if (!Consume("->"))
        {
            Error("Expected '->' to start item transform");
        }

        SkipWhitespace();

        ExpressionNode expression;

        if (_remaining is ['\'' or '"' or '`', ..])
        {
            // Quoted expression like ->'%(Filename)'
            // For now, capture as literal - we can parse the content later
            int exprStart = _position;
            ReadOnlyMemory<char> quotedContent = ParseQuotedString();
            expression = new LiteralNode(quotedContent, exprStart);
        }
        else
        {
            // Item function like ->Distinct() or ->WithMetadataValue('Name', 'Value')
            int funcStart = _position;
            ReadOnlyMemory<char> funcName = ParseIdentifier();

            SkipWhitespace();

            if (Consume('('))
            {
                var arguments = ParseArgumentList();
                if (!Consume(')'))
                {
                    Error("Expected closing ')' for item function arguments");
                }
                expression = new MethodCallNode(funcName, arguments, _expression[funcStart.._position], funcStart);
            }
            else
            {
                // Function without parentheses - treat as identifier
                expression = new LiteralNode(funcName, funcStart);
            }
        }

        return new TransformNode(expression, _expression[start.._position], start);
    }

    /// <summary>
    /// Parse an identifier (property name, item type, etc.)
    /// </summary>
    private ReadOnlyMemory<char> ParseIdentifier()
    {
        int start = _position;

        // First character must be letter or underscore
        if (!TryPeek(out char first) || !XmlUtilities.IsValidInitialElementNameCharacter(first))
        {
            string found = TryPeek(out char ch) ? $"'{ch}'" : "end of input";
            Error($"Invalid identifier: expected letter or underscore, but found {found}");
            return default;
        }

        Advance();

        // Subsequent characters: letter, digit, underscore, or hyphen
        while (TryPeek(out char ch) && XmlUtilities.IsValidSubsequentElementNameCharacter(ch))
        {
            // Special case: '->' is a transform operator, not part of the identifier
            if (_remaining is ['-', '>', ..])
            {
                break;
            }

            Advance();
        }

        return _expression[start.._position];
    }

    /// <summary>
    /// Parse a qualified identifier (e.g., System.String, System.IO.Path)
    /// </summary>
    private ReadOnlyMemory<char> ParseQualifiedIdentifier()
    {
        int start = _position;

        ParseIdentifier();

        while (Consume('.'))
        {
            ParseIdentifier();
        }

        return _expression[start.._position];
    }

    /// <summary>
    /// Parse literal text (everything that's not a special expression).
    /// </summary>
    private LiteralNode ParseLiteralText()
    {
        int start = _position;

        // Parse until we hit a special character or end
        while (_remaining is not ([] or ['$' or '%' or '@', ..]))
        {
            Advance();
        }

        return new LiteralNode(_expression[start.._position], start);
    }

    /// <summary>
    /// Parse a quoted string (single, double, or backtick).
    /// </summary>
    private ReadOnlyMemory<char> ParseQuotedString()
    {
        char quoteChar = Advance(); // Consume opening quote

        int start = _position;

        // Find matching closing quote
        while (TryPeek(out char ch) && ch != quoteChar)
        {
            Advance();
        }

        ReadOnlyMemory<char> content = _expression[start.._position];

        if (!Consume(quoteChar))
        {
            Error($"Expected closing '{quoteChar}' for quoted string");
        }

        return content;
    }

    /// <summary>
    /// Parse a function argument list.
    /// </summary>
    private ImmutableArray<ExpressionNode> ParseArgumentList()
    {
        SkipWhitespace();

        // Empty argument list
        if (_remaining is [')', ..])
        {
            return [];
        }

        var arguments = ImmutableArray.CreateBuilder<ExpressionNode>();

        while (true)
        {
            SkipWhitespace();

            // Parse argument based on what it starts with
            ExpressionNode argument = _remaining switch
            {
                // Quoted string
                ['\'' or '"' or '`', ..] => ParseQuotedArgument(),

                // Property reference
                ['$', '(', ..] => ParsePropertyReference(),

                // Metadata reference
                ['%', '(', ..] => ParseMetadataReference(),

                // Item reference
                ['@', '(', ..] => ParseItemReference(),

                // Literal - parse until comma or closing paren
                _ => ParseLiteralArgument()
            };

            arguments.Add(argument);
            SkipWhitespace();

            if (!Consume(','))
            {
                break;
            }
        }

        return arguments.ToImmutable();
    }

    /// <summary>
    /// Parse a quoted argument in a function call.
    /// </summary>
    private LiteralNode ParseQuotedArgument()
    {
        int start = _position;
        ReadOnlyMemory<char> quotedContent = ParseQuotedString();
        return new LiteralNode(quotedContent, start);
    }

    /// <summary>
    /// Parse a literal argument (everything until comma or closing paren).
    /// </summary>
    private LiteralNode ParseLiteralArgument()
    {
        int start = _position;
        int parenDepth = 0;

        while (!IsAtEnd)
        {
            char ch = _remaining[0];

            if (ch == '(')
            {
                parenDepth++;
                Advance();
            }
            else if (ch == ')')
            {
                if (parenDepth == 0)
                {
                    break;
                }

                parenDepth--;
                Advance();
            }
            else if (ch == ',' && parenDepth == 0)
            {
                break;
            }
            else
            {
                Advance();
            }
        }

        return new LiteralNode(_expression[start.._position], start);
    }

    /// <summary>
    /// Report an error at the current position.
    /// </summary>
    private readonly void Error(string message)
        => throw new InvalidOperationException($"Parse error at position {_position}: {message}");
}
