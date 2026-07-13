// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Shared;

namespace Microsoft.Build;

internal static partial class ProjectErrors
{
    public static readonly Resource BuiltInMetadataNotAllowedInThisConditional = PrimaryResource();
    public static readonly Resource CannotPassMultipleItemsIntoScalarParameter = PrimaryResource();
    public static readonly Resource CircularDependencyInTargetGraph = PrimaryResource();
    public static readonly Resource ComparisonOnNonNumericExpression = PrimaryResource();
    public static readonly Resource CustomMetadataNotAllowedInThisConditional = PrimaryResource();
    public static readonly Resource ExpressionDoesNotEvaluateToBoolean = PrimaryResource();
    public static readonly Resource FailedToRetrieveTaskOutputs = PrimaryResource();
    public static readonly Resource IllFormedEqualsInCondition = PrimaryResource();
    public static readonly Resource IllFormedItemListCloseParenthesisInCondition = PrimaryResource();
    public static readonly Resource IllFormedItemListOpenParenthesisInCondition = PrimaryResource();
    public static readonly Resource IllFormedItemListQuoteInCondition = PrimaryResource();
    public static readonly Resource IllFormedPropertyCloseParenthesisInCondition = PrimaryResource();
    public static readonly Resource IllFormedPropertyOpenParenthesisInCondition = PrimaryResource();
    public static readonly Resource IllFormedPropertySpaceInCondition = PrimaryResource();
    public static readonly Resource IllFormedQuotedStringInCondition = PrimaryResource();
    public static readonly Resource ImportedProjectFromExtensionsPathNotFoundFromAppConfig = PrimaryResource();
    public static readonly Resource ImportedProjectNotFound = PrimaryResource();
    public static readonly Resource InvalidAttributeExclusive = PrimaryResource();
    public static readonly Resource InvalidAttributeValue = PrimaryResource();
    public static readonly Resource InvalidAttributeValueWithException = PrimaryResource();
    public static readonly Resource InvalidChildElementDueToDuplication = PrimaryResource();
    public static readonly Resource InvalidContinueOnErrorAttribute = PrimaryResource();
    public static readonly Resource InvalidEvaluatedAttributeValue = PrimaryResource();
    public static readonly Resource InvalidItemFunctionExpression = PrimaryResource();
    public static readonly Resource InvalidTaskParameterValueError = PrimaryResource();
    public static readonly Resource ItemListNotAllowedInThisConditional = PrimaryResource();
    public static readonly Resource NodeMustBeLastUnderElement = PrimaryResource();
    public static readonly Resource PropertyOutsidePropertyGroupInTarget = PrimaryResource();
    public static readonly Resource SetAccessorNotAvailableOnTaskParameter = PrimaryResource();
    public static readonly Resource TargetConditionHasInvalidMetadataReference = PrimaryResource();
    public static readonly Resource TaskFactoryLoadFailure = PrimaryResource();
    public static readonly Resource TaskLoadFailure = PrimaryResource();
    public static readonly Resource TaskLoadFailureInvalidTaskHostFactoryParameter = PrimaryResource();
    public static readonly Resource TaskParametersError = PrimaryResource();
    public static readonly Resource UndefinedFunctionCall = PrimaryResource();
    public static readonly Resource UnexpectedCharacterInCondition = PrimaryResource();
    public static readonly Resource UnexpectedTokenInCondition = PrimaryResource();
    public static readonly Resource UnrecognizedAttribute = PrimaryResource();
    public static readonly Resource UnsupportedTaskParameterTypeError = PrimaryResource();
    public static readonly Resource WildcardResultsInDriveEnumeration = SharedResource();

    public static Resource PrimaryResource([CallerMemberName] string resourceName = "")
        => new(AssemblyResources.PrimaryResources, resourceName);

    public static Resource SharedResource([CallerMemberName] string resourceName = "")
        => new(AssemblyResources.SharedResources, resourceName);
}
