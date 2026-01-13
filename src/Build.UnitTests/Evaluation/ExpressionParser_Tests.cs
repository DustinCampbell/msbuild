// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation.Parsing;
using Microsoft.Build.Exceptions;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public class ExpressionParser_Tests
{
    [Fact]
    public void ParseEmptyString()
    {
        var parser = new ExpressionParser(ReadOnlyMemory<char>.Empty);
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<LiteralNode>();

        var literal = (LiteralNode)node;
        literal.Text.IsEmpty.ShouldBeTrue();
        literal.Position.ShouldBe(0);
    }

    [Fact]
    public void ParseSimplePropertyReference()
    {
        var parser = new ExpressionParser("$(PropertyName)");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<PropertyNode>();

        var propertyNode = (PropertyNode)node;
        propertyNode.Body.ShouldBeOfType<SimplePropertyNode>();

        var simpleProperty = (SimplePropertyNode)propertyNode.Body;
        simpleProperty.Name.ToString().ShouldBe("PropertyName");
        simpleProperty.MemberAccesses.Length.ShouldBe(0);
    }

    [Fact]
    public void ParsePropertyWithMethod()
    {
        var parser = new ExpressionParser("$(PropertyName.ToUpper())");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<PropertyNode>();

        var propertyNode = (PropertyNode)node;
        propertyNode.Body.ShouldBeOfType<SimplePropertyNode>();

        var simpleProperty = (SimplePropertyNode)propertyNode.Body;
        simpleProperty.Name.ToString().ShouldBe("PropertyName");
        simpleProperty.MemberAccesses.Length.ShouldBe(1);

        var methodCall = simpleProperty.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        methodCall.Name.ToString().ShouldBe("ToUpper");
        methodCall.Arguments.Length.ShouldBe(0);
    }

    [Fact]
    public void ParsePropertyWithIndexer()
    {
        var parser = new ExpressionParser("$(PropertyName[0])");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<PropertyNode>();

        var propertyNode = (PropertyNode)node;
        propertyNode.Body.ShouldBeOfType<SimplePropertyNode>();

        var simpleProperty = (SimplePropertyNode)propertyNode.Body;
        simpleProperty.Name.ToString().ShouldBe("PropertyName");
        simpleProperty.MemberAccesses.Length.ShouldBe(1);

        var indexer = simpleProperty.MemberAccesses[0].ShouldBeOfType<IndexerNode>();
        indexer.Index.ShouldBeOfType<LiteralNode>();

        var literal = (LiteralNode)indexer.Index;
        literal.Text.ToString().ShouldBe("0");
    }

    [Fact]
    public void ParseStaticFunction()
    {
        var parser = new ExpressionParser("$([System.String]::Concat('a', 'b'))");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<PropertyNode>();

        var propertyNode = (PropertyNode)node;
        propertyNode.Body.ShouldBeOfType<StaticFunctionNode>();

        var staticFunction = (StaticFunctionNode)propertyNode.Body;
        staticFunction.TypeName.ToString().ShouldBe("System.String");
        staticFunction.MethodName.ToString().ShouldBe("Concat");
        staticFunction.Arguments.Length.ShouldBe(2);
    }

    [Fact]
    public void ParseRegistryReference()
    {
        var parser = new ExpressionParser("$(Registry:HKEY_LOCAL_MACHINE\\Software\\Microsoft\\.NETFramework@InstallRoot)");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<PropertyNode>();

        var propertyNode = (PropertyNode)node;
        propertyNode.Body.ShouldBeOfType<RegistryNode>();

        var registryNode = (RegistryNode)propertyNode.Body;
        registryNode.KeyPath.ToString().ShouldBe("HKEY_LOCAL_MACHINE\\Software\\Microsoft\\.NETFramework");
        registryNode.ValueName.ToString().ShouldBe("InstallRoot");
    }

    [Fact]
    public void ParseMetadataReference()
    {
        var parser = new ExpressionParser("%(Filename)");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<MetadataNode>();

        var metadataNode = (MetadataNode)node;
        metadataNode.ItemType.ToString().ShouldBeEmpty();
        metadataNode.Name.ToString().ShouldBe("Filename");
    }

    [Fact]
    public void ParseQualifiedMetadataReference()
    {
        var parser = new ExpressionParser("%(Compile.Filename)");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<MetadataNode>();

        var metadataNode = (MetadataNode)node;
        metadataNode.ItemType.ToString().ShouldBe("Compile");
        metadataNode.Name.ToString().ShouldBe("Filename");
    }

    [Fact]
    public void ParseItemReference()
    {
        var parser = new ExpressionParser("@(Compile)");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<ItemNode>();

        var itemNode = (ItemNode)node;
        itemNode.ItemType.ToString().ShouldBe("Compile");
        itemNode.Transforms.Length.ShouldBe(0);
        itemNode.Separator.ShouldBeNull();
    }

    [Fact]
    public void ParseItemWithSeparator()
    {
        var parser = new ExpressionParser("@(Compile, ';')");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<ItemNode>();

        var itemNode = (ItemNode)node;
        itemNode.ItemType.ToString().ShouldBe("Compile");
        itemNode.Transforms.Length.ShouldBe(0);
        itemNode.Separator.ShouldNotBeNull();
        itemNode.Separator.ShouldBeOfType<LiteralNode>();
    }

    [Fact]
    public void ParseItemWithTransform()
    {
        var parser = new ExpressionParser("@(Compile->'%(Filename)')");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<ItemNode>();

        var itemNode = (ItemNode)node;
        itemNode.ItemType.ToString().ShouldBe("Compile");
        itemNode.Transforms.Length.ShouldBe(1);
        itemNode.Separator.ShouldBeNull();
    }

    [Fact]
    public void ParseLiteralText()
    {
        var parser = new ExpressionParser("plain text");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<LiteralNode>();

        var literalNode = (LiteralNode)node;
        literalNode.Text.ToString().ShouldBe("plain text");
    }

    [Fact]
    public void ParseChainedMethodCalls()
    {
        var parser = new ExpressionParser("$(Prop.ToUpper().Substring(1))");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<PropertyNode>();

        var propertyNode = (PropertyNode)node;
        propertyNode.Body.ShouldBeOfType<SimplePropertyNode>();

        var simpleProperty = (SimplePropertyNode)propertyNode.Body;
        simpleProperty.Name.ToString().ShouldBe("Prop");
        simpleProperty.MemberAccesses.Length.ShouldBe(2);

        var firstMethod = simpleProperty.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        firstMethod.Name.ToString().ShouldBe("ToUpper");

        var secondMethod = simpleProperty.MemberAccesses[1].ShouldBeOfType<MethodCallNode>();
        secondMethod.Name.ToString().ShouldBe("Substring");
        secondMethod.Arguments.Length.ShouldBe(1);
    }

    [Fact]
    public void ParseNestedPropertyReferences()
    {
        var parser = new ExpressionParser("$(PropertyA.Substring($(PropertyB.Length)))");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<PropertyNode>();

        var propertyNode = (PropertyNode)node;
        propertyNode.Body.ShouldBeOfType<SimplePropertyNode>();

        var simpleProperty = (SimplePropertyNode)propertyNode.Body;
        simpleProperty.Name.ToString().ShouldBe("PropertyA");
        simpleProperty.MemberAccesses.Length.ShouldBe(1);

        var method = simpleProperty.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        method.Name.ToString().ShouldBe("Substring");
        method.Arguments.Length.ShouldBe(1);

        // The argument should be another property reference
        method.Arguments[0].ShouldBeOfType<PropertyNode>();
    }

    [Fact]
    public void ParseEmptyPropertyReference()
    {
        var parser = new ExpressionParser("$()");
        var node = parser.Parse();

        node.ShouldNotBeNull();
        node.ShouldBeOfType<PropertyNode>();

        var propertyNode = (PropertyNode)node;
        propertyNode.Body.ShouldBeOfType<SimplePropertyNode>();

        var simpleProperty = (SimplePropertyNode)propertyNode.Body;
        simpleProperty.Name.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void ParseInvalidPropertyMissingClosingParen()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var parser = new ExpressionParser("$(PropertyName");
            return parser.Parse();
        });
    }

    [Fact]
    public void ParseInvalidMetadataEmptyName()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var parser = new ExpressionParser("%()");

            return parser.Parse();
        });
    }

    [Fact]
    public void ParseDeeplyNestedPropertyReferences()
    {
        var parser = new ExpressionParser("$(A.Method($(B.Method($(C.Method($(D)))))))");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        simple.Name.ToString().ShouldBe("A");

        var method = simple.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        method.Arguments[0].ShouldBeOfType<PropertyNode>();
    }

    [Fact]
    public void ParseStaticFunctionWithMemberAccess()
    {
        var parser = new ExpressionParser("$([System.IO.Path]::GetFullPath('test').ToUpper())");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var staticFunc = (StaticFunctionNode)prop.Body;

        staticFunc.TypeName.ToString().ShouldBe("System.IO.Path");
        staticFunc.MethodName.ToString().ShouldBe("GetFullPath");
        staticFunc.MemberAccesses.Length.ShouldBe(1);

        var memberCall = staticFunc.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        memberCall.Name.ToString().ShouldBe("ToUpper");
    }

    [Fact]
    public void ParsePropertyWithMultipleIndexers()
    {
        var parser = new ExpressionParser("$(Prop[0][1][2])");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        simple.MemberAccesses.Length.ShouldBe(3);

        simple.MemberAccesses[0].ShouldBeOfType<IndexerNode>();
        simple.MemberAccesses[1].ShouldBeOfType<IndexerNode>();
        simple.MemberAccesses[2].ShouldBeOfType<IndexerNode>();
    }

    [Fact]
    public void ParsePropertyWithNestedIndexer()
    {
        var parser = new ExpressionParser("$(Prop[$(Index)])");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;

        var indexer = simple.MemberAccesses[0].ShouldBeOfType<IndexerNode>();
        indexer.Index.ShouldBeOfType<PropertyNode>();
    }

    [Fact]
    public void ParseItemWithMultipleTransforms()
    {
        // This should work: function followed by quoted transform
        var parser = new ExpressionParser("@(Items->Distinct()->'%(Filename)')");
        var node = parser.Parse();

        node.ShouldBeOfType<ItemNode>();
        var item = (ItemNode)node;
        item.Transforms.Length.ShouldBe(2);
        
        // First transform: Distinct() function
        item.Transforms[0].Expression.ShouldBeOfType<MethodCallNode>();
        
        // Second transform: quoted expression
        item.Transforms[1].Expression.ShouldBeOfType<LiteralNode>();
    }

    [Fact]
    public void ParseStaticFunctionWithMultipleArguments()
    {
        var parser = new ExpressionParser("$([System.String]::Join(';', 'a', 'b', 'c'))");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var staticFunc = (StaticFunctionNode)prop.Body;
        staticFunc.Arguments.Length.ShouldBe(4);
    }

    [Fact]
    public void ParseStaticFunctionWithNestedFunctionArgument()
    {
        var parser = new ExpressionParser("$([System.String]::Concat($([System.String]::Join(',', 'a', 'b')), 'c'))");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var staticFunc = (StaticFunctionNode)prop.Body;
        staticFunc.Arguments.Length.ShouldBe(2);
        staticFunc.Arguments[0].ShouldBeOfType<PropertyNode>();
    }

    [Fact]
    public void ParsePropertyWithWhitespace()
    {
        var parser = new ExpressionParser("$(  PropertyName  )");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        simple.Name.ToString().ShouldBe("PropertyName");
    }

    [Fact]
    public void ParseMethodWithWhitespaceInArguments()
    {
        var parser = new ExpressionParser("$(Prop.Method(  'arg'  ,  'arg2'  ))");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        var method = simple.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        method.Arguments.Length.ShouldBe(2);
    }

    [Fact]
    public void ParseItemWithWhitespace()
    {
        var parser = new ExpressionParser("@(  ItemType  )");
        var node = parser.Parse();

        node.ShouldBeOfType<ItemNode>();
        var item = (ItemNode)node;
        item.ItemType.ToString().ShouldBe("ItemType");
    }

    [Fact]
    public void ParsePropertyWithHyphenInName()
    {
        var parser = new ExpressionParser("$(Property-Name)");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        simple.Name.ToString().ShouldBe("Property-Name");
    }

    [Fact]
    public void ParsePropertyWithUnderscoreInName()
    {
        var parser = new ExpressionParser("$(Property_Name)");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        simple.Name.ToString().ShouldBe("Property_Name");
    }

    [Fact]
    public void ParsePropertyWithDigitsInName()
    {
        var parser = new ExpressionParser("$(Property123)");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        simple.Name.ToString().ShouldBe("Property123");
    }

    [Fact]
    public void ParsePropertyStartingWithUnderscore()
    {
        var parser = new ExpressionParser("$(_InternalProperty)");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        simple.Name.ToString().ShouldBe("_InternalProperty");
    }

    [Fact]
    public void ParseFunctionWithSingleQuotedArgument()
    {
        var parser = new ExpressionParser("$(Prop.Method('arg'))");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        var method = simple.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        method.Arguments.Length.ShouldBe(1);
    }

    [Fact]
    public void ParseFunctionWithDoubleQuotedArgument()
    {
        var parser = new ExpressionParser("$(Prop.Method(\"arg\"))");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        var method = simple.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        method.Arguments.Length.ShouldBe(1);
    }

    [Fact]
    public void ParseFunctionWithBacktickQuotedArgument()
    {
        var parser = new ExpressionParser("$(Prop.Method(`arg`))");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var simple = (SimplePropertyNode)prop.Body;
        var method = simple.MemberAccesses[0].ShouldBeOfType<MethodCallNode>();
        method.Arguments.Length.ShouldBe(1);
    }

    [Fact]
    public void ParseInvalidItemMissingClosingParen()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var parser = new ExpressionParser("@(ItemType");
            return parser.Parse();
        });
    }

    [Fact]
    public void ParseInvalidMetadataMissingClosingParen()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var parser = new ExpressionParser("%(Metadata");
            return parser.Parse();
        });
    }

    [Fact]
    public void ParseInvalidStaticFunctionMissingClosingBracket()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var parser = new ExpressionParser("$([System.String::Concat('a'))");
            return parser.Parse();
        });
    }

    [Fact]
    public void ParseInvalidStaticFunctionMissingDoubleColon()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var parser = new ExpressionParser("$([System.String]Concat('a'))");
            return parser.Parse();
        });
    }

    [Fact]
    public void ParseInvalidPropertyStartingWithDigit()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var parser = new ExpressionParser("$(123Property)");
            return parser.Parse();
        });
    }

    [Fact]
    public void ParseInvalidQuotedStringMissingClosingQuote()
    {
        Should.Throw<InvalidOperationException>(() =>
        {
            var parser = new ExpressionParser("$(Prop.Method('unclosed))");
            return parser.Parse();
        });
    }

    [Fact]
    public void ParseRegistryWithoutValueName()
    {
        var parser = new ExpressionParser("$(Registry:HKEY_LOCAL_MACHINE\\Software)");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        var registry = (RegistryNode)prop.Body;
        registry.KeyPath.ToString().ShouldBe("HKEY_LOCAL_MACHINE\\Software");
        registry.ValueName.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void ParseRegistryCaseInsensitive()
    {
        var parser = new ExpressionParser("$(REGISTRY:HKEY_LOCAL_MACHINE\\Software@Value)");
        var node = parser.Parse();

        node.ShouldBeOfType<PropertyNode>();
        var prop = (PropertyNode)node;
        prop.Body.ShouldBeOfType<RegistryNode>();
    }

    [Fact]
    public void ParseItemTransformWithQuotedExpression()
    {
        var parser = new ExpressionParser("@(Items->'prefix_%(Identity)_suffix')");
        var node = parser.Parse();

        node.ShouldBeOfType<ItemNode>();
        var item = (ItemNode)node;
        item.Transforms.Length.ShouldBe(1);
    }

    [Fact]
    public void ParseItemTransformWithFunctionAndArguments()
    {
        var parser = new ExpressionParser("@(Items->WithMetadataValue('Name', 'Value'))");
        var node = parser.Parse();

        node.ShouldBeOfType<ItemNode>();
        var item = (ItemNode)node;
        item.Transforms.Length.ShouldBe(1);

        var transform = item.Transforms[0];
        transform.Expression.ShouldBeOfType<MethodCallNode>();
    }

    [Fact]
    public void ParseItemSeparatorWithPropertyReference()
    {
        var parser = new ExpressionParser("@(Items, '$(Separator)')");
        var node = parser.Parse();

        node.ShouldBeOfType<ItemNode>();
        var item = (ItemNode)node;
        item.Separator.ShouldNotBeNull();
    }

    [Fact]
    public void ParseItemWithChainedFunctions()
    {
        // Valid: chained item functions
        var parser = new ExpressionParser("@(Items->Reverse()->Distinct())");
        var node = parser.Parse();

        node.ShouldBeOfType<ItemNode>();
        var item = (ItemNode)node;
        item.Transforms.Length.ShouldBe(2);

        // First transform
        var firstFunc = item.Transforms[0].Expression.ShouldBeOfType<MethodCallNode>();
        firstFunc.Name.ToString().ShouldBe("Reverse");

        // Second transform
        var secondFunc = item.Transforms[1].Expression.ShouldBeOfType<MethodCallNode>();
        secondFunc.Name.ToString().ShouldBe("Distinct");
    }
}
