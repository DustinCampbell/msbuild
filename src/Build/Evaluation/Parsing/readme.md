# MSBuild Expression Parser

This directory contains a unified expression parser for MSBuild that handles both:
1. **Text expressions** - Property (`$(..)`), metadata (`%(..)`), and item list (`@(..)`) references
2. **Conditional expressions** - Complex boolean/comparison expressions used in MSBuild conditions

## Current Status

### ✅ Implemented
- **Parser (`ExpressionParser`)**: Parses MSBuild text expressions into an AST
  - Property references: `$(PropertyName)`, `$(Prop.Method())`, `$([Type]::Static())`
  - Metadata references: `%(Name)`, `%(ItemType.Name)`
  - Item references: `@(Items)`, `@(Items->'transform')`, `@(Items, ';')`
  - Registry references: `$(Registry:HKEY_...\Path@Value)`
  - Nested expressions and chained transforms

- **AST Nodes (`ExpressionNodes.cs`)**: Complete node hierarchy for representing parsed expressions

### 🚧 Not Yet Implemented
- **Evaluator/Visitor**: Logic to walk the AST and actually expand the expressions
- **Integration with Expander**: Replacing the regex-based parsing in `Expander<P,I>`
- **Performance optimizations**: Caching, string pooling, etc.

## Parser API (Prototype)

The current parser is simplified for prototyping:

```C#
// Simplified constructor - no IElementLocation required for prototyping
var parser = new ExpressionParser("$(PropertyName)");
var ast = parser.Parse();

// Errors throw InvalidOperationException with position info
// In production, this should integrate with MSBuild's error reporting
```

**Note**: For production integration, we'll need to:
1. Add `IElementLocation` parameter back for proper error reporting
2. Use MSBuild resource strings instead of literal error messages
3. Integrate with `ProjectErrorUtilities`

## Grammar

The following grammar defines MSBuild text expressions:

```
(* Top Level *)

Expression       = { LiteralText | PropertyRef | MetadataRef | ItemRef } ;
LiteralText      = any text not containing '$', '%', or '@' ;

(* Property References: $(PropertyName) or $(PropertyName.Method()) *)

PropertyRef      = "$(" PropertyBody ")" ;
PropertyBody     = RegistryRef | StaticFunction | PropertyName { MemberAccess } ;
PropertyName     = Identifier ;
MemberAccess     = "." MethodCall | "[" Expression "]" ;
MethodCall       = Identifier [ "(" [ ArgumentList ] ")" ] ;
StaticFunction   = "[" TypeName "]" "::" Identifier "(" [ ArgumentList ] ")" { MemberAccess } ;
TypeName         = Identifier { "." Identifier } ;
RegistryRef      = "Registry:" RegistryPath [ "@" Identifier ] ;
RegistryPath     = any characters except ')' or '@' ;

(* Metadata References: %(Name) or %(ItemType.Name) *)

MetadataRef      = "%(" [ Identifier "." ] Identifier ")" ;

(* Item References: @(ItemType) with optional transforms *)

ItemRef          = "@(" ItemType { Transform } [ Separator ] ")" ;
ItemType         = Identifier ;
Transform        = "->" ( QuotedExpression | ItemFunction ) ;
QuotedExpression = "'" { QuotedContent } "'" | '"' { QuotedContent } '"' | "`" { QuotedContent } "`" ;
QuotedContent    = literal text | PropertyRef | MetadataRef | ItemRef ;
ItemFunction     = Identifier "(" [ ArgumentList ] ")" ;
Separator        = "," QuotedExpression ;

(* Function Arguments *)

ArgumentList     = Argument { "," Argument } ;
Argument         = Expression | QuotedExpression | "null" ;

(* Identifiers *)

Identifier       = ( Letter | "_" ) { Letter | Digit | "_" | "-" } ;
```

## Expression Examples

### Property References

- `$(PropertyName)` - Simple property
- `$(PropertyName.ToUpper())` - Instance method
- `$(PropertyName.Substring(0, 5))` - Method with arguments
- `$(PropertyName.Length)` - Instance property
- `$(PropertyName.ToUpper().Substring(1))` - Chained calls
- `$(PropertyName[0])` - Indexer
- `$(PropertyName[$(Index)])` - Indexer with expression
- `$([System.String]::Concat('a', 'b'))` - Static method
- `$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), 'dir.proj'))` - Intrinsic function
- `$(Registry:HKEY_LOCAL_MACHINE\Software\Microsoft\.NETFramework@InstallRoot)` - Registry lookup

### Metadata References

- `%(MetadataName)` - Unqualified
- `%(ItemType.MetadataName)` - Qualified with item type
- `%(FullPath)` - Built-in metadata
- `%(Filename)` - Built-in metadata

### Item References

- `@(ItemType)` - Simple reference
- `@(ItemType, ';')` - With separator
- `@(ItemType->'%(Filename)')` - With transform
- `@(ItemType->'%(Filename)', ' ')` - Transform + separator
- `@(ItemType->Distinct())` - Function
- `@(ItemType->Distinct()->Reverse())` - Chained functions
- `@(ItemType->WithMetadataValue('Name', 'Value'))` - Function with args
- `@(ItemType->'%(FullPath)'->DirectoryName())` - Transform + function

### Nesting

Expressions can nest freely:

- `$(PropertyA.Substring($(PropertyB.Length)))`
- `@(Items->'$(OutputPath)%(Filename).exe')`
- `%(ItemType.$(MetadataName))`
- `$([MSBuild]::MakeRelative($(BaseDir), @(Files->'%(FullPath)', ';')))`


## Key Features

### 1. Indexer Access

Supported on properties and function results:

- `$(PropertyName[0])`
- `$(PropertyName[$(Index)])`
- `$(PropertyName.ToArray()[5])`

### 2. Chained Member Access

Multiple dots and indexers can be chained:

- `$(Prop.Method1().Method2().Property)`
- `$(Prop[0].Method()[1])`

### 3. Nested Expressions

Full expressions allowed in function arguments:

- `$(Prop.Substring($(OtherProp.Length)))`
- `$([MSBuild]::MakeRelative($(Dir1), $(Dir2)))`

### 4. Multiple Quote Styles

Single quote, double quote, and backtick all supported:

- `@(Items->'%(Filename)')`
- `@(Items->"%(Filename)")`
- ``@(Items->`%(Filename)`)``

### 5. Item Transform Chains

Multiple transforms and functions can be chained:

- `@(Items->'%(FullPath)'->DirectoryName()->Distinct())`

## Special Cases

| Expression | Behavior |
|------------|----------|
| `$()` | Evaluates to empty string |
| `%()` | Invalid - error |
| `$(  PropertyName  )` | Whitespace is trimmed |
| `$(Property.Method( ))` | Whitespace in args is allowed |
| `MSBuildThisFile*` | Special computed properties |

## AST Node Types

The parser will produce these node types:

**Base Types:**
- `ExpressionNode` - Base class with `TextSpan Span` property

**Property Nodes:**
- `PropertyNode` - Represents `$(...)` 
  - `PropertyBodyNode Body`
- `SimplePropertyNode : PropertyBodyNode` - Property with optional member access chain
  - `string Name`
  - `ImmutableArray<MemberAccessNode> MemberAccesses`
- `StaticFunctionNode : PropertyBodyNode` - Static function call `$([Type]::Method())`
  - `string TypeName`
  - `string MethodName`
  - `ImmutableArray<ExpressionNode> Arguments`
  - `ImmutableArray<MemberAccessNode> MemberAccesses`
- `RegistryNode : PropertyBodyNode` - Registry lookup
  - `string KeyPath`
  - `string? ValueName`

**Member Access:**
- `MethodCallNode : MemberAccessNode`
  - `string Name`
  - `ImmutableArray<ExpressionNode> Arguments`
- `PropertyAccessNode : MemberAccessNode`
  - `string Name`
- `IndexerNode : MemberAccessNode`
  - `ExpressionNode Index`

**Other Nodes:**
- `MetadataNode` - `%(...)` references
  - `string? ItemType`
  - `string Name`
- `ItemNode` - `@(...)` references with transforms
  - `string ItemType`
  - `ImmutableArray<TransformNode> Transforms`
  - `ExpressionNode? Separator`
- `LiteralNode` - Plain text between special constructs
  - `string Text`

## Implementation Notes

1. **Source Locations**: All AST nodes track their source span for error reporting
2. **Escaping**: The parser works with escaped text; evaluation handles unescaping
3. **Whitespace**: Generally preserved in literals, trimmed in references
4. **Performance**: 
   - Uses `ReadOnlyMemory<char>` and `ReadOnlySpan<char>` to avoid allocations
   - Uses `ImmutableArray<T>` for collections (value semantics, no null checks)
   - Pattern matching on spans for fast lookahead
   - Avoids regex where possible

## Key Design Decisions

1. **`ref struct` Parser**: Allows stack-only operation, can't be boxed or captured
2. **`ReadOnlyMemory<char>` in Nodes**: Avoids string allocations for substrings
3. **Immutable AST**: Once parsed, tree is immutable (thread-safe, cacheable)
4. **Simplified Errors (Prototype)**: Currently throws `InvalidOperationException`; production will use `ProjectErrorUtilities`

## Testing

See `ExpressionParser_Tests.cs` for comprehensive test coverage including:
- Basic property/item/metadata references
- Complex nesting and chaining
- Edge cases (empty strings, whitespace, special characters)
- Error cases (missing delimiters, invalid syntax)
- Transform chains and item functions

## Integration with Expander

To integrate this parser into `Expander<P,I>`, we need:

### Phase 1: AST Evaluation (Not Yet Implemented)
Create an `ExpressionEvaluator` that:
- Visits AST nodes and produces expanded values
- Integrates with `IPropertyProvider<T>`, `IItemProvider<T>`, `IMetadataTable`
- Respects `ExpanderOptions` (ExpandProperties, ExpandItems, ExpandMetadata, etc.)
- Handles escaping/unescaping per MSBuild rules
- Tracks property usage for diagnostics

### Phase 2: Replace Regex-Based Parsing
Replace calls like:
- `ExpressionShredder.GetReferencedItemExpressions()` → Use `ExpressionParser`
- Regex-based `ItemMetadataRegex` → Use AST visiting
- String-based property expansion → Use AST evaluation

### Phase 3: Performance Validation
Ensure the new implementation:
- Matches or exceeds performance of regex-based approach
- Minimizes allocations (use `ReadOnlyMemory<char>`, object pooling)
- Handles the most common cases efficiently (e.g., literal strings, simple property refs)

## Example Usage (Future)

```C#
// Parse
var parser = new ExpressionParser( "$(Config.ToUpper())_@(Items->'%(Filename)', ';')", elementLocation);
var ast = parser.Parse();

// Evaluate
var evaluator = new ExpressionEvaluator(properties, items, metadata, options);
var result = evaluator.Evaluate(ast);
```
