// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal static class ParseErrors
{
    public const string BuiltInMetadataNotAllowed = "BuiltInMetadataNotAllowedInThisConditional";
    public const string CustomMetadataNotAllowed = "CustomMetadataNotAllowedInThisConditional";

    public const string IllFormedEquals = "IllFormedEqualsInCondition";

    public const string IllFormedItemListCloseParenthesis = "IllFormedItemListCloseParenthesisInCondition";
    public const string IllFormedItemListOpenParenthesis = "IllFormedItemListOpenParenthesisInCondition";
    public const string IllFormedItemListQuote = "IllFormedItemListQuoteInCondition";

    public const string IllFormedPropertyCloseParenthesis = "IllFormedPropertyCloseParenthesisInCondition";
    public const string IllFormedPropertyOpenParenthesis = "IllFormedPropertyOpenParenthesisInCondition";

    public const string IllFormedItemMetadataCloseParenthesis = "IllFormedItemMetadataCloseParenthesisInCondition";
    public const string IllFormedItemMetadataOpenParenthesis = "IllFormedItemMetadataOpenParenthesisInCondition";

    public const string IllFormedSpace = "IllFormedSpaceInCondition";
    public const string IllFormedQuotedString = "IllFormedQuotedStringInCondition";

    public const string IncorrectNumberOfFunctionArguments = "IncorrectNumberOfFunctionArguments";

    public const string ItemListNotAllowed = "ItemListNotAllowedInThisConditional";
    public const string ItemMetadataNotAllowed = "ItemMetadataNotAllowedInThisConditional";

    public const string UndefinedFunctionCall = "UndefinedFunctionCall";
    public const string UnexpectedToken = "UnexpectedTokenInCondition";
}
