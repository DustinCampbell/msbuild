// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal static class ConditionErrors
{
    /// <summary>
    ///  Lazily format resource string to help avoid (in some perf critical cases) even loading
    ///  resources at all.
    /// </summary>
    public static string EndOfInputTokenName
        => field ??= ResourceUtilities.GetResourceString("EndOfInputTokenName");

    public const string BuiltInMetadataNotAllowed = "BuiltInMetadataNotAllowedInThisConditional";
    public const string CustomMetadataNotAllowed = "CustomMetadataNotAllowedInThisConditional";
    public const string ItemListNotAllowed = "ItemListNotAllowedInThisConditional";

    public const string IllFormedEquals = "IllFormedEqualsInCondition";

    public const string IllFormedPropertyOpenParenthesis = "IllFormedPropertyOpenParenthesisInCondition";
    public const string IllFormedPropertyCloseParenthesis = "IllFormedPropertyCloseParenthesisInCondition";

    public const string IllFormedItemMetadataOpenParenthesis = "IllFormedItemMetadataOpenParenthesisInCondition";
    public const string IllFormedItemMetadataCloseParenthesis = "IllFormedItemMetadataCloseParenthesisInCondition";
    public const string IllFormedPropertySpace = "IllFormedPropertySpaceInCondition";

    public const string IllFormedItemListOpenParenthesis = "IllFormedItemListOpenParenthesisInCondition";
    public const string IllFormedItemListCloseParenthesis = "IllFormedItemListCloseParenthesisInCondition";
    public const string IllFormedItemListQuote = "IllFormedItemListQuoteInCondition";

    public const string IllFormedQuotedString = "IllFormedQuotedStringInCondition";

    public const string UnexpectedCharacter = "UnexpectedCharacterInCondition";
    public const string UnexpectedToken = "UnexpectedTokenInCondition";
}
