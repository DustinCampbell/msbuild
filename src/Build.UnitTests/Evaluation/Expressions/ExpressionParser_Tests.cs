// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Evaluation.Expressions;
using Roslyn.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation.Expressions;

public class ExpressionParser_Tests
{
    #region Literal Values

    [Fact]
    public void ParseStringLiteral_Simple()
    {
        var node = Parse("'hello'");

        var stringNode = node.ShouldBeOfType<StringLiteralNode>();
        VerifyNode(stringNode, "'hello'", start: 0);
    }

    [Fact]
    public void ParseNumericLiteral()
    {
        var node = Parse("42");

        var numNode = node.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(numNode, "42", start: 0);
    }

    #endregion

    #region Numeric Literal Edge Cases

    [Fact]
    public void ParseNumericLiteral_NegativeNumber()
    {
        var node = Parse("-42");
        var numNode = node.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(numNode, "-42", start: 0);
    }

    [Fact]
    public void ParseNumericLiteral_DecimalNumber()
    {
        var node = Parse("3.14159");
        var numNode = node.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(numNode, "3.14159", start: 0);
    }

    [Fact]
    public void ParseNumericLiteral_HexNumber()
    {
        var node = Parse("0xFF");
        var numNode = node.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(numNode, "0xFF", start: 0);
    }

    [Fact]
    public void ParseNumericLiteral_HexNumberLowerCase()
    {
        var node = Parse("0xdeadbeef");
        var numNode = node.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(numNode, "0xdeadbeef", start: 0);
    }

    [Fact]
    public void ParseNumericLiteral_LeadingZero()
    {
        var node = Parse("0042");
        var numNode = node.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(numNode, "0042", start: 0);
    }

    [Fact]
    public void ParseNumericLiteral_ScientificNotation()
    {
        // MSBuild may or may not support this - test based on actual behavior
        var result = ExpressionParser.TryParse("1.5e10", out var node);
        // Assert based on actual MSBuild behavior
    }

    #endregion

    #region String Literals with Expansion

    [Fact]
    public void ParseStringLiteral_SinglePropertyReference()
    {
        var node = Parse("'$(Configuration)'");

        var compositeStr = node.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(compositeStr, "'$(Configuration)'", start: 0);

        var propRef = compositeStr.Parts.ShouldHaveSingleItem().ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(propRef, "$(Configuration)", start: 1);

        var identifier = propRef.Expression.ShouldBeOfType<IdentifierNode>();
        VerifyToken(identifier.Name, TokenKind.Identifier, "Configuration", start: 3);
    }

    [Fact]
    public void ParseStringLiteral_SingleItemReference()
    {
        var node = Parse("'@(Items)'");

        var compositeStr = node.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(compositeStr, "'@(Items)'", start: 0);

        var itemRef = compositeStr.Parts.ShouldHaveSingleItem().ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemRef, "@(Items)", start: 1);
        VerifyToken(itemRef.ItemType, TokenKind.Identifier, "Items", start: 3);
    }

    [Fact]
    public void ParseStringLiteral_SingleMetadataReference()
    {
        var node = Parse("'%(Filename)'");

        var compositeStr = node.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(compositeStr, "'%(Filename)'", start: 0);

        var metaRef = compositeStr.Parts.ShouldHaveSingleItem().ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metaRef, "%(Filename)", start: 1);
        VerifyToken(metaRef.MetadataName, TokenKind.Identifier, "Filename", start: 3);
    }

    [Fact]
    public void ParseStringLiteral_WithPrefix()
    {
        var node = Parse("'prefix$(Property)'");

        var composite = node.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(composite, "'prefix$(Property)'", start: 0);
        composite.Parts.Length.ShouldBe(2);

        var prefix = composite.Parts[0].ShouldBeOfType<SimpleTextNode>();
        VerifyNode(prefix, "prefix", start: 1);

        var propRef = composite.Parts[1].ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(propRef, "$(Property)", start: 7);
    }

    [Fact]
    public void ParseStringLiteral_WithSuffix()
    {
        var node = Parse("'$(Property)suffix'");

        var composite = node.ShouldBeOfType<CompositeStringNode>();
        composite.Parts.Length.ShouldBe(2);

        composite.Parts[0].ShouldBeOfType<PropertyReferenceNode>();
        composite.Parts[1].ShouldBeOfType<SimpleTextNode>();
    }

    [Fact]
    public void ParseStringLiteral_WithPrefixAndSuffix()
    {
        var node = Parse("'prefix$(Property)suffix'");

        var composite = node.ShouldBeOfType<CompositeStringNode>();
        composite.Parts.Length.ShouldBe(3);

        composite.Parts[0].ShouldBeOfType<SimpleTextNode>();
        composite.Parts[1].ShouldBeOfType<PropertyReferenceNode>();
        composite.Parts[2].ShouldBeOfType<SimpleTextNode>();
    }

    [Fact]
    public void ParseStringLiteral_MultipleReferences()
    {
        var node = Parse("'$(A)_$(B)'");

        var composite = node.ShouldBeOfType<CompositeStringNode>();
        composite.Parts.Length.ShouldBe(3);

        composite.Parts[0].ShouldBeOfType<PropertyReferenceNode>();
        composite.Parts[1].ShouldBeOfType<SimpleTextNode>();
        composite.Parts[2].ShouldBeOfType<PropertyReferenceNode>();
    }

    [Fact]
    public void ParseStringLiteral_WithEscapeSequence()
    {
        var node = Parse("'Hello%20World'");

        var composite = node.ShouldBeOfType<CompositeStringNode>();
        composite.Parts.Length.ShouldBe(3);

        composite.Parts[0].ShouldBeOfType<SimpleTextNode>();
        composite.Parts[1].ShouldBeOfType<EscapedTextNode>();
        composite.Parts[2].ShouldBeOfType<SimpleTextNode>();
    }

    #endregion

    #region String Literal Edge Cases

    [Fact]
    public void ParseStringLiteral_EmptyString()
    {
        var node = Parse("''");
        var stringNode = node.ShouldBeOfType<StringLiteralNode>();
        VerifyNode(stringNode, "''", start: 0);
    }

    [Fact]
    public void ParseStringLiteral_MultipleConsecutiveEscapes()
    {
        var node = Parse("'%20%20%20'");

        var composite = node.ShouldBeOfType<CompositeStringNode>();
        var escapedText = composite.Parts.ShouldHaveSingleItem().ShouldBeOfType<EscapedTextNode>();
        VerifyNode(escapedText, "%20%20%20", start: 1);
        escapedText.UnescapedText.ShouldBe("   ");
    }

    [Fact]
    public void ParseStringLiteral_InvalidEscapeSequence()
    {
        // %XZ is not a valid hex sequence - 'Z' is not a hex digit
        var node = Parse("'test%XZvalue'");

        var stringLiteral = node.ShouldBeOfType<StringLiteralNode>();
        VerifyNode(stringLiteral, "'test%XZvalue'", start: 0);
    }

    [Fact]
    public void ParseStringLiteral_IncompleteEscapeAtEnd()
    {
        var node = Parse("'test%2'");

        // Incomplete escape sequence - should be treated as literal
        var stringLiteral = node.ShouldBeOfType<StringLiteralNode>();
        VerifyNode(stringLiteral, "'test%2'", start: 0);
    }

    [Fact]
    public void ParseStringLiteral_MixedQuoteTypes()
    {
        // MSBuild supports ', ", and ` as quote characters
        var node1 = Parse("\"hello\"");
        node1.ShouldBeOfType<StringLiteralNode>();

        var node2 = Parse("`hello`");
        node2.ShouldBeOfType<StringLiteralNode>();
    }

    #endregion

    #region Parenthesized Expressions

    [Fact]
    public void ParseParenthesizedExpression()
    {
        var node = Parse("('$(A)' == 'a')");

        var comparison = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(comparison.Operator, TokenKind.EqualTo, "==", start: 8);
    }

    [Fact]
    public void ParseNestedParentheses()
    {
        var node = Parse("(('$(A)' == 'a'))");

        var comparison = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(comparison.Operator, TokenKind.EqualTo, "==", start: 9);
    }

    [Fact]
    public void ParseComplexParenthesizedExpression()
    {
        var node = Parse("('$(A)' == 'a' or '$(B)' == 'b') and '$(C)' == 'c'");

        // Should parse as: (A == a OR B == b) AND (C == c)
        var andNode = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(andNode.Operator, TokenKind.And, "and", start: 33);

        var orNode = andNode.Left.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(orNode.Operator, TokenKind.Or, "or", start: 15);
    }

    #endregion

    #region Whitespace Handling

    [Fact]
    public void ParseExpression_LeadingWhitespace()
    {
        var node = Parse("   '$(A)' == 'a'");
        node.ShouldBeOfType<BinaryOperatorNode>();
    }

    [Fact]
    public void ParseExpression_TrailingWhitespace()
    {
        var node = Parse("'$(A)' == 'a'   ");
        node.ShouldBeOfType<BinaryOperatorNode>();
    }

    [Fact]
    public void ParseExpression_NoSpacesAroundOperators()
    {
        // This is a known MSBuild quirk that should be preserved
        var node = Parse("'$(A)'=='a'and'$(B)'=='b'");
        node.ShouldBeOfType<BinaryOperatorNode>();
    }

    [Fact]
    public void ParseExpression_TabsAndNewlines()
    {
        var node = Parse("'$(A)'\t==\n'a'");
        node.ShouldBeOfType<BinaryOperatorNode>();
    }

    #endregion

    #region Property References

    [Fact]
    public void ParsePropertyReference()
    {
        var node = Parse("$(Configuration)");

        var propertyReferenceNode = node.ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(propertyReferenceNode, "$(Configuration)", start: 0);

        var identifier = propertyReferenceNode.Expression.ShouldBeOfType<IdentifierNode>();
        VerifyNode(identifier, "Configuration", start: 2);
        VerifyToken(identifier.Name, TokenKind.Identifier, "Configuration", 2);
    }

    [Fact]
    public void ParsePropertyFunctionCall()
    {
        var node = Parse("$(Configuration.ToUpper())");

        var propNode = node.ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(propNode, "$(Configuration.ToUpper())", start: 0);

        var funcNode = propNode.Expression.ShouldBeOfType<FunctionCallNode>();
        VerifyNode(funcNode, "Configuration.ToUpper()", start: 2);

        var memberAccess = funcNode.Receiver.ShouldBeOfType<MemberAccessNode>();
        VerifyNode(memberAccess, "Configuration.ToUpper", start: 2);
        VerifyToken(memberAccess.MemberName, TokenKind.Identifier, "ToUpper", start: 16);

        var identifier = memberAccess.Target.ShouldBeOfType<IdentifierNode>();
        VerifyNode(identifier, "Configuration", start: 2);
        VerifyToken(identifier.Name, TokenKind.Identifier, "Configuration", start: 2);

        funcNode.Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void ParseMemberAccess()
    {
        var node = Parse("$(Obj.Property)");

        var propNode = node.ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(propNode, "$(Obj.Property)", start: 0);

        var memberNode = propNode.Expression.ShouldBeOfType<MemberAccessNode>();
        VerifyNode(memberNode, "Obj.Property", start: 2);
        VerifyToken(memberNode.MemberName, TokenKind.Identifier, "Property", start: 6);

        var target = memberNode.Target.ShouldBeOfType<IdentifierNode>();
        VerifyNode(target, "Obj", start: 2);
        VerifyToken(target.Name, TokenKind.Identifier, "Obj", start: 2);
    }

    [Fact]
    public void ParseComplexPropertyFunctionChain()
    {
        var node = Parse("$([System.String]::Copy($(Original)).ToUpper().Replace('WORLD', 'MSBuild'))");

        var propNode = node.ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(propNode, "$([System.String]::Copy($(Original)).ToUpper().Replace('WORLD', 'MSBuild'))", start: 0);

        var replaceCall = propNode.Expression.ShouldBeOfType<FunctionCallNode>();
        VerifyNode(replaceCall, "[System.String]::Copy($(Original)).ToUpper().Replace('WORLD', 'MSBuild')", start: 2);
        replaceCall.Arguments.Length.ShouldBe(2);

        var arg1 = replaceCall.Arguments[0].ShouldBeOfType<StringLiteralNode>();
        VerifyNode(arg1, "'WORLD'", start: 55);

        var arg2 = replaceCall.Arguments[1].ShouldBeOfType<StringLiteralNode>();
        VerifyNode(arg2, "'MSBuild'", start: 64);

        var replaceMemberAccess = replaceCall.Receiver.ShouldBeOfType<MemberAccessNode>();
        VerifyNode(replaceMemberAccess, "[System.String]::Copy($(Original)).ToUpper().Replace", start: 2);
        VerifyToken(replaceMemberAccess.MemberName, TokenKind.Identifier, "Replace", start: 47);

        var toUpperCall = replaceMemberAccess.Target.ShouldBeOfType<FunctionCallNode>();
        VerifyNode(toUpperCall, "[System.String]::Copy($(Original)).ToUpper()", start: 2);
        toUpperCall.Arguments.ShouldBeEmpty();

        var toUpperMemberAccess = toUpperCall.Receiver.ShouldBeOfType<MemberAccessNode>();
        VerifyNode(toUpperMemberAccess, "[System.String]::Copy($(Original)).ToUpper", start: 2);
        VerifyToken(toUpperMemberAccess.MemberName, TokenKind.Identifier, "ToUpper", start: 37);

        var copyCall = toUpperMemberAccess.Target.ShouldBeOfType<FunctionCallNode>();
        VerifyNode(copyCall, "[System.String]::Copy($(Original))", start: 2);
        copyCall.Arguments.Length.ShouldBe(1);

        var copyArg = copyCall.Arguments[0].ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(copyArg, "$(Original)", start: 24);

        var originalIdentifier = copyArg.Expression.ShouldBeOfType<IdentifierNode>();
        VerifyNode(originalIdentifier, "Original", start: 26);
        VerifyToken(originalIdentifier.Name, TokenKind.Identifier, "Original", start: 26);

        var staticMemberAccess = copyCall.Receiver.ShouldBeOfType<StaticMemberAccessNode>();
        VerifyToken(staticMemberAccess.MemberName, TokenKind.Identifier, "Copy", start: 19);

        var typeName = staticMemberAccess.TypeName;
        VerifyNode(typeName, "System.String", start: 3);
        typeName.IsQualified.ShouldBeTrue();
        VerifySourceSpan(typeName.Namespace, "System", start: 3);
        VerifyToken(typeName.Name, TokenKind.Identifier, "String", start: 10);
    }

    #endregion

    #region Item Vectors

    [Fact]
    public void ParseItemVector()
    {
        var node = Parse("@(Compile)");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Compile)", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Compile", start: 2);

        itemNode.Transforms.ShouldBeEmpty();
        itemNode.Separator.ShouldBeNull();
    }

    [Fact]
    public void ParseItemVectorWithTransform()
    {
        var node = Parse("@(Compile->'%(FullPath)')");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Compile->'%(FullPath)')", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Compile", start: 2);

        itemNode.Transforms.Length.ShouldBe(1);

        var transform = itemNode.Transforms[0];
        VerifyNode(transform, "->'%(FullPath)'", start: 9);

        var compositeString = transform.Expression.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(compositeString, "'%(FullPath)'", start: 11);

        var metadataRef = compositeString.Parts.ShouldHaveSingleItem().ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metadataRef, "%(FullPath)", start: 12);

        itemNode.Separator.ShouldBeNull();
    }

    [Fact]
    public void ParseItemVectorWithSeparator()
    {
        var node = Parse("@(Compile, ';')");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Compile, ';')", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Compile", start: 2);

        itemNode.Transforms.ShouldBeEmpty();

        itemNode.Separator.ShouldNotBeNull();
        var separator = itemNode.Separator.ShouldBeOfType<StringLiteralNode>();
        VerifyNode(separator, "';'", start: 11);
    }

    [Fact]
    public void ParseItemVectorWithTransformAndSeparator()
    {
        var node = Parse("@(Compile->'%(FullPath)', ';')");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Compile->'%(FullPath)', ';')", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Compile", start: 2);

        itemNode.Transforms.Length.ShouldBe(1);

        var transform = itemNode.Transforms[0];
        VerifyNode(transform, "->'%(FullPath)'", start: 9);

        var compositeString = transform.Expression.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(compositeString, "'%(FullPath)'", start: 11);

        var metadataRef = compositeString.Parts.ShouldHaveSingleItem().ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metadataRef, "%(FullPath)", start: 12);

        itemNode.Separator.ShouldNotBeNull();
        var separator = itemNode.Separator.ShouldBeOfType<StringLiteralNode>();
    }

    [Fact]
    public void ParseItemWithMultipleTransforms()
    {
        var node = Parse("@(Compile->'%(FullPath)'->'%(Directory)')");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Compile->'%(FullPath)'->'%(Directory)')", start: 0);
        itemNode.Transforms.Length.ShouldBe(2);

        var transform1 = itemNode.Transforms[0];
        VerifyNode(transform1, "->'%(FullPath)'", start: 9);
        var compositeStr1 = transform1.Expression.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(compositeStr1, "'%(FullPath)'", start: 11);
        var metadataRef1 = compositeStr1.Parts.ShouldHaveSingleItem().ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metadataRef1, "%(FullPath)", start: 12);

        var transform2 = itemNode.Transforms[1];
        VerifyNode(transform2, "->'%(Directory)'", start: 24);
        var compositeStr2 = transform2.Expression.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(compositeStr2, "'%(Directory)'", start: 26);
        var metadataRef2 = compositeStr2.Parts.ShouldHaveSingleItem().ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metadataRef2, "%(Directory)", start: 27);
    }

    [Fact]
    public void ParseItemWithHyphensInName()
    {
        var node = Parse("@(Item-With-Hyphens)");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Item-With-Hyphens)", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Item-With-Hyphens", start: 2);
    }

    [Fact]
    public void ParseItemWithUnderscoresInName()
    {
        var node = Parse("@(Item_With_Underscores)");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Item_With_Underscores)", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Item_With_Underscores", start: 2);
    }

    #endregion

    #region Item Vector Edge Cases

    [Fact]
    public void ParseItemVector_EmptyTransform()
    {
        // @(Items->'')
        var node = Parse("@(Items->'')");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        itemNode.Transforms.Length.ShouldBe(1);

        var transform = itemNode.Transforms[0];
        var stringLiteral = transform.Expression.ShouldBeOfType<StringLiteralNode>();
        VerifyNode(stringLiteral, "''", start: 9);
    }

    [Fact]
    public void ParseItemVector_ComplexSeparator()
    {
        var node = Parse("@(Items, '$(Sep)')");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        var separator = itemNode.Separator.ShouldBeOfType<CompositeStringNode>();
    }

    [Fact]
    public void ParseItemVector_TransformWithFunction()
    {
        var node = Parse("@(Compile->'%(Filename).ToUpper()')");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        itemNode.Transforms.Length.ShouldBe(1);

        // The transform expression should contain a composite string
        // with metadata reference and literal text
    }

    [Fact]
    public void ParseItemVector_MultipleTransformsWithSeparator()
    {
        var node = Parse("@(Compile->'%(FullPath)'->'%(Filename)', ';')");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        itemNode.Transforms.Length.ShouldBe(2);
        itemNode.Separator.ShouldNotBeNull();
    }

    #endregion

    #region Item Functions

    [Fact]
    public void ParseChainedItemFunctions()
    {
        var node = Parse("@(Compile->Distinct()->Reverse())");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Compile->Distinct()->Reverse())", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Compile", start: 2);
        itemNode.Transforms.Length.ShouldBe(2);

        var transform1 = itemNode.Transforms[0];
        VerifyNode(transform1, "->Distinct()", start: 9);
        var func1 = transform1.Expression.ShouldBeOfType<FunctionCallNode>();
        var ident1 = func1.Receiver.ShouldBeOfType<IdentifierNode>();
        VerifyToken(ident1.Name, TokenKind.Identifier, "Distinct", start: 11);
        func1.Arguments.ShouldBeEmpty();

        var transform2 = itemNode.Transforms[1];
        VerifyNode(transform2, "->Reverse()", start: 21);
        var func2 = transform2.Expression.ShouldBeOfType<FunctionCallNode>();
        var ident2 = func2.Receiver.ShouldBeOfType<IdentifierNode>();
        VerifyToken(ident2.Name, TokenKind.Identifier, "Reverse", start: 23);
        func2.Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void ParseItemFunctionWithMetadataArgument()
    {
        var node = Parse("@(Compile->Metadata('CustomMetadata'))");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Compile->Metadata('CustomMetadata'))", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Compile", start: 2);
        itemNode.Transforms.Length.ShouldBe(1);

        var transform = itemNode.Transforms[0];
        VerifyNode(transform, "->Metadata('CustomMetadata')", start: 9);

        var funcCall = transform.Expression.ShouldBeOfType<FunctionCallNode>();
        var receiver = funcCall.Receiver.ShouldBeOfType<IdentifierNode>();
        VerifyToken(receiver.Name, TokenKind.Identifier, "Metadata", start: 11);

        funcCall.Arguments.Length.ShouldBe(1);
        var arg = funcCall.Arguments[0].ShouldBeOfType<StringLiteralNode>();
        VerifyNode(arg, "'CustomMetadata'", start: 20);
    }

    [Fact]
    public void ParseItemFunctionWithMultipleArguments()
    {
        var node = Parse("@(Compile->WithMetadataValue('Name', 'Value'))");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        VerifyNode(itemNode, "@(Compile->WithMetadataValue('Name', 'Value'))", start: 0);
        VerifyToken(itemNode.ItemType, TokenKind.Identifier, "Compile", start: 2);
        itemNode.Transforms.Length.ShouldBe(1);

        var transform = itemNode.Transforms[0];
        var funcCall = transform.Expression.ShouldBeOfType<FunctionCallNode>();
        var receiver = funcCall.Receiver.ShouldBeOfType<IdentifierNode>();
        VerifyToken(receiver.Name, TokenKind.Identifier, "WithMetadataValue", start: 11);

        funcCall.Arguments.Length.ShouldBe(2);
        funcCall.Arguments[0].ShouldBeOfType<StringLiteralNode>();
        funcCall.Arguments[1].ShouldBeOfType<StringLiteralNode>();
    }

    [Fact]
    public void ParseItemWithTransformThenFunction()
    {
        var node = Parse("@(Compile->'%(FullPath)'->Distinct())");

        var itemNode = node.ShouldBeOfType<ItemVectorNode>();
        itemNode.Transforms.Length.ShouldBe(2);

        var transform1 = itemNode.Transforms[0];
        var compositeStr1 = transform1.Expression.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(compositeStr1, "'%(FullPath)'", start: 11);
        var metadataRef1 = compositeStr1.Parts.ShouldHaveSingleItem().ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metadataRef1, "%(FullPath)", start: 12);

        var transform2 = itemNode.Transforms[1];
        var funcCall = transform2.Expression.ShouldBeOfType<FunctionCallNode>();
        var receiver = funcCall.Receiver.ShouldBeOfType<IdentifierNode>();
        VerifyToken(receiver.Name, TokenKind.Identifier, "Distinct", start: 26);
    }

    #endregion

    #region Metadata References

    [Fact]
    public void ParseMetadataReference_Unqualified()
    {
        var node = Parse("%(FullPath)");

        var metaNode = node.ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metaNode, "%(FullPath)", start: 0);
        metaNode.ItemType.Kind.ShouldBe(TokenKind.None);
        VerifyToken(metaNode.MetadataName, TokenKind.Identifier, "FullPath", start: 2);
    }

    [Fact]
    public void ParseMetadataReference_Qualified()
    {
        var node = Parse("%(Compile.Object)");

        var metaNode = node.ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metaNode, "%(Compile.Object)", start: 0);
        VerifyToken(metaNode.ItemType, TokenKind.Identifier, "Compile", start: 2);
        VerifyToken(metaNode.MetadataName, TokenKind.Identifier, "Object", start: 10);
    }

    [Fact]
    public void ParseMetadataWithQualifiedItemType()
    {
        var node = Parse("%(Compile.FullPath)");

        var metaNode = node.ShouldBeOfType<MetadataReferenceNode>();
        VerifyNode(metaNode, "%(Compile.FullPath)", start: 0);
        VerifyToken(metaNode.ItemType, TokenKind.Identifier, "Compile", start: 2);
        VerifyToken(metaNode.MetadataName, TokenKind.Identifier, "FullPath", start: 10);
    }

    #endregion

    #region Static Function Calls

    [Fact]
    public void ParseStaticFunctionCall_SimpleType()
    {
        var node = Parse("[String]::Copy('foo')");

        var funcNode = node.ShouldBeOfType<FunctionCallNode>();
        VerifyNode(funcNode, "[String]::Copy('foo')", start: 0);

        var staticMemberAccessNode = funcNode.Receiver.ShouldBeOfType<StaticMemberAccessNode>();

        var typeName = staticMemberAccessNode.TypeName;
        VerifyNode(typeName, "String", start: 1);
        typeName.IsQualified.ShouldBeFalse();
        VerifyToken(typeName.Name, TokenKind.Identifier, "String", start: 1);

        VerifyToken(staticMemberAccessNode.MemberName, TokenKind.Identifier, "Copy", start: 10);

        funcNode.Arguments.Length.ShouldBe(1);
        var arg = funcNode.Arguments[0].ShouldBeOfType<StringLiteralNode>();
        VerifyNode(arg, "'foo'", start: 15);
    }

    [Fact]
    public void ParseStaticFunctionCall_QualifiedType()
    {
        var node = Parse("[System.String]::Copy('foo')");

        var funcNode = node.ShouldBeOfType<FunctionCallNode>();
        VerifyNode(funcNode, "[System.String]::Copy('foo')", start: 0);

        var staticMemberAccessNode = funcNode.Receiver.ShouldBeOfType<StaticMemberAccessNode>();

        var typeName = staticMemberAccessNode.TypeName;
        VerifyNode(typeName, "System.String", start: 1);
        typeName.IsQualified.ShouldBeTrue();
        VerifySourceSpan(typeName.Namespace, "System", start: 1);
        VerifyToken(typeName.Name, TokenKind.Identifier, "String", start: 8);

        VerifyToken(staticMemberAccessNode.MemberName, TokenKind.Identifier, "Copy", start: 17);
    }

    [Fact]
    public void ParseStaticFunctionCall_NestedType()
    {
        var node = Parse("[System.Collections.Generic.List]::new()");

        var funcNode = node.ShouldBeOfType<FunctionCallNode>();
        VerifyNode(funcNode, "[System.Collections.Generic.List]::new()", start: 0);

        var staticMemberAccessNode = funcNode.Receiver.ShouldBeOfType<StaticMemberAccessNode>();

        var typeName = staticMemberAccessNode.TypeName;
        VerifyNode(typeName, "System.Collections.Generic.List", start: 1);
        typeName.IsQualified.ShouldBeTrue();
        VerifySourceSpan(typeName.Namespace, "System.Collections.Generic", start: 1);
        VerifyToken(typeName.Name, TokenKind.Identifier, "List", start: 28);
    }

    [Fact]
    public void ParseStaticPropertyFunctionWithNestedProperty()
    {
        var node = Parse("$([System.IO.Path]::Combine($(Root), 'bin'))");

        var propNode = node.ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(propNode, "$([System.IO.Path]::Combine($(Root), 'bin'))", start: 0);

        var funcCall = propNode.Expression.ShouldBeOfType<FunctionCallNode>();
        var staticAccess = funcCall.Receiver.ShouldBeOfType<StaticMemberAccessNode>();

        var typeName = staticAccess.TypeName;
        typeName.IsQualified.ShouldBeTrue();
        VerifySourceSpan(typeName.Namespace, "System.IO", start: 3);
        VerifyToken(typeName.Name, TokenKind.Identifier, "Path", start: 13);

        VerifyToken(staticAccess.MemberName, TokenKind.Identifier, "Combine", start: 20);

        funcCall.Arguments.Length.ShouldBe(2);

        var arg1 = funcCall.Arguments[0].ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(arg1, "$(Root)", start: 28);

        var arg2 = funcCall.Arguments[1].ShouldBeOfType<StringLiteralNode>();
        VerifyNode(arg2, "'bin'", start: 37);
    }

    [Fact]
    public void ParseMSBuildPropertyFunction()
    {
        var node = Parse("$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), 'Directory.Build.props'))");

        var propNode = node.ShouldBeOfType<PropertyReferenceNode>();
        var funcCall = propNode.Expression.ShouldBeOfType<FunctionCallNode>();
        var staticAccess = funcCall.Receiver.ShouldBeOfType<StaticMemberAccessNode>();

        var typeName = staticAccess.TypeName;
        typeName.IsQualified.ShouldBeFalse();
        VerifyToken(typeName.Name, TokenKind.Identifier, "MSBuild", start: 3);

        VerifyToken(staticAccess.MemberName, TokenKind.Identifier, "GetDirectoryNameOfFileAbove", start: 13);

        funcCall.Arguments.Length.ShouldBe(2);
    }

    #endregion

    #region Function Calls

    [Fact]
    public void ParseFunctionCallWithArguments()
    {
        var node = Parse("Contains('abc', 'b')");

        var funcNode = node.ShouldBeOfType<FunctionCallNode>();
        VerifyNode(funcNode, "Contains('abc', 'b')", start: 0);

        var receiver = funcNode.Receiver.ShouldBeOfType<IdentifierNode>();
        VerifyNode(receiver, "Contains", start: 0);
        VerifyToken(receiver.Name, TokenKind.Identifier, "Contains", start: 0);

        funcNode.Arguments.Length.ShouldBe(2);

        var arg1 = funcNode.Arguments[0].ShouldBeOfType<StringLiteralNode>();
        VerifyNode(arg1, "'abc'", start: 9);

        var arg2 = funcNode.Arguments[1].ShouldBeOfType<StringLiteralNode>();
        VerifyNode(arg2, "'b'", start: 16);
    }

    #endregion

    #region Comparison Operators

    [Fact]
    public void ParseEqualityExpression()
    {
        var node = Parse("'$(Configuration)' == 'Debug'");

        var binOp = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(binOp, "'$(Configuration)' == 'Debug'", start: 0);
        VerifyToken(binOp.Operator, TokenKind.EqualTo, "==", start: 19);

        var left = binOp.Left.ShouldBeOfType<CompositeStringNode>();
        VerifyNode(left, "'$(Configuration)'", start: 0);

        var propRef = left.Parts.ShouldHaveSingleItem().ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(propRef, "$(Configuration)", start: 1);

        var configIdentifier = propRef.Expression.ShouldBeOfType<IdentifierNode>();
        VerifyToken(configIdentifier.Name, TokenKind.Identifier, "Configuration", start: 3);

        var right = binOp.Right.ShouldBeOfType<StringLiteralNode>();
        VerifyNode(right, "'Debug'", start: 22);
    }

    [Fact]
    public void ParseNotEqualExpression()
    {
        var node = Parse("$(Foo) != 'bar'");

        var binOp = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(binOp, "$(Foo) != 'bar'", start: 0);
        VerifyToken(binOp.Operator, TokenKind.NotEqualTo, "!=", start: 7);

        var left = binOp.Left.ShouldBeOfType<PropertyReferenceNode>();
        VerifyNode(left, "$(Foo)", start: 0);

        var right = binOp.Right.ShouldBeOfType<StringLiteralNode>();
        VerifyNode(right, "'bar'", start: 10);
    }

    [Fact]
    public void ParseLessThanExpression()
    {
        var node = Parse("42 < 100");

        var binOp = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(binOp, "42 < 100", start: 0);
        VerifyToken(binOp.Operator, TokenKind.LessThan, "<", start: 3);

        var left = binOp.Left.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(left, "42", start: 0);

        var right = binOp.Right.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(right, "100", start: 5);
    }

    [Fact]
    public void ParseLessThanOrEqualExpression()
    {
        var node = Parse("10 <= 100");

        var binOp = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(binOp.Operator, TokenKind.LessThanOrEqualTo, "<=", start: 3);
    }

    [Fact]
    public void ParseGreaterThanOrEqualExpression()
    {
        var node = Parse("42 >= 10");

        var binOp = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(binOp.Operator, TokenKind.GreaterThanOrEqualTo, ">=", start: 3);

        var left = binOp.Left.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(left, "42", start: 0);

        var right = binOp.Right.ShouldBeOfType<NumericLiteralNode>();
        VerifyNode(right, "10", start: 6);
    }

    #endregion

    #region Logical Operators

    [Fact]
    public void ParseAndExpression()
    {
        var node = Parse("'$(A)' == 'a' and '$(B)' == 'b'");

        var andNode = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(andNode, "'$(A)' == 'a' and '$(B)' == 'b'", start: 0);
        VerifyToken(andNode.Operator, TokenKind.And, "and", start: 14);

        var left = andNode.Left.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(left, "'$(A)' == 'a'", start: 0);

        var right = andNode.Right.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(right, "'$(B)' == 'b'", start: 18);
    }

    [Fact]
    public void ParseOrExpression()
    {
        var node = Parse("'$(A)' == 'a' or '$(B)' == 'b'");

        var orNode = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(orNode, "'$(A)' == 'a' or '$(B)' == 'b'", start: 0);
        VerifyToken(orNode.Operator, TokenKind.Or, "or", start: 14);

        var left = orNode.Left.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(left, "'$(A)' == 'a'", start: 0);

        var right = orNode.Right.ShouldBeOfType<BinaryOperatorNode>();
        VerifyNode(right, "'$(B)' == 'b'", start: 17);
    }

    [Fact]
    public void ParseNotExpression()
    {
        var node = Parse("!('$(Debug)' == 'true')");

        var notNode = node.ShouldBeOfType<UnaryOperatorNode>();
        VerifyToken(notNode.Operator, TokenKind.Not, "!", start: 0);

        var comparison = notNode.Operand.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(comparison.Operator, TokenKind.EqualTo, "==", start: 13);
    }

    [Fact]
    public void ParseComplexBooleanExpression()
    {
        var node = Parse("'$(A)' == 'a' and '$(B)' == 'b' or '$(C)' == 'c'");

        // Should parse as: (A == a AND B == b) OR (C == c)
        var orNode = node.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(orNode.Operator, TokenKind.Or, "or", start: 32);

        var leftAnd = orNode.Left.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(leftAnd.Operator, TokenKind.And, "and", start: 14);

        var rightEq = orNode.Right.ShouldBeOfType<BinaryOperatorNode>();
        VerifyToken(rightEq.Operator, TokenKind.EqualTo, "==", start: 42);
    }

    #endregion

    #region Invalid Expressions

    [Theory]

    [InlineData("")]
    [InlineData("$(")]
    [InlineData("@(")]
    [InlineData("%(")]
    [InlineData("$(Prop")]
    [InlineData("$(Prop.")]
    [InlineData("$(Prop.Method()")]
    [InlineData("[Type]::")]
    [InlineData("[Type]::Method")]
    [InlineData("'string")]
    [InlineData("$(Prop and")]
    [InlineData("==")]
    [InlineData("and")]
    [InlineData("!")]
    [InlineData("^InvalidToken")]
    [InlineData("'unclosed")]
    [InlineData("$(Configuration")]
    [InlineData("$Configuration)")]
    [InlineData("@(Compile")]
    [InlineData("@Compile)")]
    [InlineData("%(FullPath")]
    [InlineData("%FullPath)")]
    [InlineData("@(Item.With.Dots)")]
    [InlineData("%(Item..Meta)")]
    [InlineData("$(Obj.)")]
    [InlineData("$(Prop1 @(Item2")]
    [InlineData("$(Configuration.ToUpper(")]
    [InlineData("$(Valid) and $(Invalid")]
    [InlineData("$(Bad $(Good)")]
    [InlineData("$(Outer->$(Inner@(Item")]
    [InlineData("@(Items->%(FullPath)')")]
    [InlineData("@(Items")]
    [InlineData("$(Prop @(Item)")]
    [InlineData("Contains('abc',)")]
    [InlineData("$([String::Copy('foo'))")]
    [InlineData("$([String]Copy('foo'))")]
    public void DoesNotParse(string expression)
        => ExpressionParser.TryParse(expression, out _).ShouldBeFalse();

    #endregion

    #region Helper Methods

    private static ExpressionNode Parse(string expression)
    {
        ExpressionParser.TryParse(expression, out var root).ShouldBeTrue();
        return root;
    }

    private static void VerifyNode(ExpressionNode node, string text, int start)
    {
        node.Text.ToString().ShouldBe(text);
        VerifySourceSpan(node.Source, text, start);
    }

    private static void VerifyToken(Token token, TokenKind kind, string text, int start)
    {
        token.Kind.ShouldBe(kind);
        VerifySourceSpan(token.Source, text, start);
    }

    private static void VerifySourceSpan(SourceSpan span, string text, int start)
    {
        span.Text.ToString().ShouldBe(text);
        span.Start.ShouldBe(start);
        span.End.ShouldBe(start + text.Length);
        span.Length.ShouldBe(text.Length);
    }

    #endregion
}
