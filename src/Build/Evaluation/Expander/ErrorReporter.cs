// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Identifies localized details for an invalid property-function expression.
/// </summary>
internal enum PropertyFunctionErrorDetail
{
    /// <summary>
    ///  The expression contains mismatched parentheses.
    /// </summary>
    MismatchedParenthesis,

    /// <summary>
    ///  The expression contains mismatched quotation marks.
    /// </summary>
    MismatchedQuote,

    /// <summary>
    ///  The expression contains mismatched square brackets.
    /// </summary>
    MismatchedSquareBrackets,
}

/// <summary>
///  Provides typed descriptors for errors encountered while expanding an expression.
/// </summary>
/// <param name="location">The project element location associated with reported errors.</param>
internal readonly struct ErrorReporter(IElementLocation location)
{
    private static class ResourceNames
    {
        public const string CannotEvaluateItemMetadata = nameof(CannotEvaluateItemMetadata);
        public const string CannotExpandItemMetadata = nameof(CannotExpandItemMetadata);
        public const string EmbeddedItemVectorCannotBeItemized = nameof(EmbeddedItemVectorCannotBeItemized);
        public const string InvalidFunctionMethodUnavailable = nameof(InvalidFunctionMethodUnavailable);
        public const string InvalidFunctionPropertyExpression = nameof(InvalidFunctionPropertyExpression);
        public const string InvalidFunctionStaticMethodSyntax = nameof(InvalidFunctionStaticMethodSyntax);
        public const string InvalidFunctionTypeUnavailable = nameof(InvalidFunctionTypeUnavailable);
        public const string InvalidItemFunctionExpression = nameof(InvalidItemFunctionExpression);
        public const string InvalidItemFunctionSyntax = nameof(InvalidItemFunctionSyntax);
        public const string InvalidRegistryPropertyExpression = nameof(InvalidRegistryPropertyExpression);
        public const string QualifiedMetadataInTransformNotAllowed = nameof(QualifiedMetadataInTransformNotAllowed);
        public const string UnknownItemFunction = nameof(UnknownItemFunction);

        public const string InvalidFunctionPropertyExpressionDetailMismatchedParenthesis =
            nameof(InvalidFunctionPropertyExpressionDetailMismatchedParenthesis);
        public const string InvalidFunctionPropertyExpressionDetailMismatchedQuote =
            nameof(InvalidFunctionPropertyExpressionDetailMismatchedQuote);
        public const string InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets =
            nameof(InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets);
    }

    /// <summary>
    ///  Gets the error raised when item metadata cannot be evaluated.
    /// </summary>
    public ErrorDescriptor CannotEvaluateMetadata
        => new(Location, ResourceNames.CannotEvaluateItemMetadata);

    /// <summary>
    ///  Gets the error raised when metadata cannot be expanded in an expression.
    /// </summary>
    public ErrorDescriptor CannotExpandMetadata
        => new(Location, ResourceNames.CannotExpandItemMetadata);

    /// <summary>
    ///  Gets the error raised when an item vector cannot be used where a list of items is required.
    /// </summary>
    public ErrorDescriptor ItemVectorCannotBeItemized
        => new(Location, ResourceNames.EmbeddedItemVectorCannotBeItemized);

    /// <summary>
    ///  Gets the error raised when a property-function expression is invalid.
    /// </summary>
    public PropertyFunctionErrorDescriptor InvalidPropertyFunction
        => new(Location);

    /// <summary>
    ///  Gets the error raised when an item function fails while processing an item.
    /// </summary>
    public ErrorDescriptor InvalidItemFunction
        => new(Location, ResourceNames.InvalidItemFunctionExpression);

    /// <summary>
    ///  Gets the error raised when an item function receives invalid arguments.
    /// </summary>
    public ErrorDescriptor InvalidItemFunctionArguments
        => new(Location, ResourceNames.InvalidItemFunctionSyntax);

    /// <summary>
    ///  Gets the error raised when a registry property expression is invalid.
    /// </summary>
    public RegistryExpressionErrorDescriptor InvalidRegistryExpression
        => new(Location);

    /// <summary>
    ///  Gets the error raised when a static property-function expression is invalid.
    /// </summary>
    public ExpressionErrorDescriptor InvalidStaticPropertyFunction
        => new(Location, ResourceNames.InvalidFunctionStaticMethodSyntax);

    /// <summary>
    ///  Gets the project element location associated with reported errors.
    /// </summary>
    public IElementLocation Location { get; } = location;

    /// <summary>
    ///  Gets the error raised when qualified metadata is used in an item transform.
    /// </summary>
    public ErrorDescriptor QualifiedMetadataInTransform
        => new(Location, ResourceNames.QualifiedMetadataInTransformNotAllowed);

    /// <summary>
    ///  Gets the error raised when an item function is unknown.
    /// </summary>
    public ErrorDescriptor UnknownItemFunction
        => new(Location, ResourceNames.UnknownItemFunction);

    /// <summary>
    ///  Gets the error raised when a property function is unavailable on a type.
    /// </summary>
    public ErrorDescriptor UnavailablePropertyFunction
        => new(Location, ResourceNames.InvalidFunctionMethodUnavailable);

    /// <summary>
    ///  Gets the error raised when a property-function receiver type is unavailable.
    /// </summary>
    public ErrorDescriptor UnavailablePropertyFunctionType
        => new(Location, ResourceNames.InvalidFunctionTypeUnavailable);

    /// <summary>
    ///  Describes a localized expansion error with positional formatting arguments.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The localized error resource name.</param>
    public readonly struct ErrorDescriptor(IElementLocation location, string resourceName)
    {
        /// <summary>
        ///  Throws the error with one formatting argument.
        /// </summary>
        /// <typeparam name="T1">The type of the formatting argument.</typeparam>
        /// <param name="arg0">The formatting argument.</param>
        [DoesNotReturn]
        public void Throw<T1>(T1 arg0)
            => ProjectErrorUtilities.ThrowInvalidProject(location, resourceName, arg0);

        /// <summary>
        ///  Throws the error with two formatting arguments.
        /// </summary>
        /// <typeparam name="T1">The type of the first formatting argument.</typeparam>
        /// <typeparam name="T2">The type of the second formatting argument.</typeparam>
        /// <param name="arg0">The first formatting argument.</param>
        /// <param name="arg1">The second formatting argument.</param>
        [DoesNotReturn]
        public void Throw<T1, T2>(T1 arg0, T2 arg1)
            => ProjectErrorUtilities.ThrowInvalidProject(location, resourceName, arg0, arg1);

        /// <summary>
        ///  Throws the error with three formatting arguments.
        /// </summary>
        /// <typeparam name="T1">The type of the first formatting argument.</typeparam>
        /// <typeparam name="T2">The type of the second formatting argument.</typeparam>
        /// <typeparam name="T3">The type of the third formatting argument.</typeparam>
        /// <param name="arg0">The first formatting argument.</param>
        /// <param name="arg1">The second formatting argument.</param>
        /// <param name="arg2">The third formatting argument.</param>
        [DoesNotReturn]
        public void Throw<T1, T2, T3>(T1 arg0, T2 arg1, T3 arg2)
            => ProjectErrorUtilities.ThrowInvalidProject(location, resourceName, arg0, arg1, arg2);

        /// <summary>
        ///  Throws the error with one formatting argument when <paramref name="condition"/> is
        ///  <see langword="false"/>.
        /// </summary>
        /// <typeparam name="T1">The type of the formatting argument.</typeparam>
        /// <param name="condition">The condition that must be <see langword="true"/>.</param>
        /// <param name="arg0">The formatting argument.</param>
        public void ThrowIfFalse<T1>([DoesNotReturnIf(false)] bool condition, T1 arg0)
        {
            if (!condition)
            {
                Throw(arg0);
            }
        }

        /// <summary>
        ///  Throws the error with two formatting arguments when <paramref name="condition"/> is
        ///  <see langword="false"/>.
        /// </summary>
        /// <typeparam name="T1">The type of the first formatting argument.</typeparam>
        /// <typeparam name="T2">The type of the second formatting argument.</typeparam>
        /// <param name="condition">The condition that must be <see langword="true"/>.</param>
        /// <param name="arg0">The first formatting argument.</param>
        /// <param name="arg1">The second formatting argument.</param>
        public void ThrowIfFalse<T1, T2>([DoesNotReturnIf(false)] bool condition, T1 arg0, T2 arg1)
        {
            if (!condition)
            {
                Throw(arg0, arg1);
            }
        }
    }

    /// <summary>
    ///  Describes an error whose first argument is an expression and whose second argument is detail text.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    /// <param name="resourceName">The localized error resource name.</param>
    public readonly struct ExpressionErrorDescriptor(IElementLocation location, string resourceName)
    {
        /// <summary>
        ///  Throws the error without additional detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        [DoesNotReturn]
        public void Throw(string expression)
            => Throw(expression, string.Empty);

        /// <summary>
        ///  Throws the error without additional detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        [DoesNotReturn]
        public void Throw(StringSegment expression)
            => Throw(expression.ValueOrEmpty);

        /// <summary>
        ///  Throws the error with additional detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        /// <param name="message">The detail describing the failure.</param>
        [DoesNotReturn]
        public void Throw(StringSegment expression, string message)
            => Throw(expression.ValueOrEmpty, message);

        /// <summary>
        ///  Throws the error with additional detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        /// <param name="message">The detail describing the failure.</param>
        [DoesNotReturn]
        public void Throw(string expression, string message)
            => ProjectErrorUtilities.ThrowInvalidProject(location, resourceName, expression, message);
    }

    /// <summary>
    ///  Describes an invalid property-function expression error.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    public readonly struct PropertyFunctionErrorDescriptor(IElementLocation location)
    {
        private readonly ExpressionErrorDescriptor _error =
            new(location, ResourceNames.InvalidFunctionPropertyExpression);

        /// <summary>
        ///  Throws the error without additional detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        [DoesNotReturn]
        public void Throw(string expression)
            => _error.Throw(expression);

        /// <summary>
        ///  Throws the error without additional detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        [DoesNotReturn]
        public void Throw(StringSegment expression)
            => _error.Throw(expression);

        /// <summary>
        ///  Throws the error with localized detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        /// <param name="detail">The localized detail to include.</param>
        [DoesNotReturn]
        public void Throw(string expression, PropertyFunctionErrorDetail detail)
            => _error.Throw(expression, GetDetailText(detail));

        /// <summary>
        ///  Throws the error with localized detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        /// <param name="detail">The localized detail to include.</param>
        [DoesNotReturn]
        public void Throw(StringSegment expression, PropertyFunctionErrorDetail detail)
            => Throw(expression.ValueOrEmpty, detail);

        /// <summary>
        ///  Throws the error with additional detail.
        /// </summary>
        /// <param name="expression">The invalid expression.</param>
        /// <param name="message">The detail describing the failure.</param>
        [DoesNotReturn]
        public void Throw(string expression, string message)
            => _error.Throw(expression, message);

        /// <summary>
        ///  Throws the error when <paramref name="condition"/> is <see langword="false"/>.
        /// </summary>
        /// <param name="condition">The condition that must be <see langword="true"/>.</param>
        /// <param name="expression">The invalid expression.</param>
        public void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, string expression)
        {
            if (!condition)
            {
                Throw(expression);
            }
        }

        /// <summary>
        ///  Throws the error when <paramref name="condition"/> is <see langword="false"/> without materializing
        ///  the expression segment when the condition is <see langword="true"/>.
        /// </summary>
        /// <param name="condition">The condition that must be <see langword="true"/>.</param>
        /// <param name="expression">The invalid expression.</param>
        public void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, StringSegment expression)
        {
            if (!condition)
            {
                Throw(expression);
            }
        }
    }

    /// <summary>
    ///  Describes an invalid registry property expression error.
    /// </summary>
    /// <param name="location">The project element location associated with the error.</param>
    public readonly struct RegistryExpressionErrorDescriptor(IElementLocation location)
    {
        private readonly ErrorDescriptor _error =
            new(location, ResourceNames.InvalidRegistryPropertyExpression);

        /// <summary>
        ///  Throws the error with additional detail.
        /// </summary>
        /// <param name="expression">The registry expression without the surrounding <c>$(</c> and <c>)</c>.</param>
        /// <param name="message">The detail describing the failure.</param>
        [DoesNotReturn]
        public void Throw(string expression, string message)
            => _error.Throw($"$({expression})", message);

        /// <summary>
        ///  Throws the error when <paramref name="condition"/> is <see langword="false"/>.
        /// </summary>
        /// <param name="condition">The condition that must be <see langword="true"/>.</param>
        /// <param name="expression">The registry expression without the surrounding <c>$(</c> and <c>)</c>.</param>
        public void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, string expression)
        {
            if (!condition)
            {
                Throw(expression, string.Empty);
            }
        }
    }

    private static string GetDetailText(PropertyFunctionErrorDetail detail)
        => detail switch
        {
            PropertyFunctionErrorDetail.MismatchedParenthesis
                => AssemblyResources.GetString(
                    ResourceNames.InvalidFunctionPropertyExpressionDetailMismatchedParenthesis),
            PropertyFunctionErrorDetail.MismatchedQuote
                => AssemblyResources.GetString(
                    ResourceNames.InvalidFunctionPropertyExpressionDetailMismatchedQuote),
            PropertyFunctionErrorDetail.MismatchedSquareBrackets
                => AssemblyResources.GetString(
                    ResourceNames.InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets),
            _ => Assumed.Unreachable<string>(),
        };
}
