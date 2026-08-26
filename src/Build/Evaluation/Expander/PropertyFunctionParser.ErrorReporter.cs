// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal partial struct PropertyFunctionParser
{
    private enum ErrorDetail
    {
        MismatchedParenthesis,
        MismatchedQuote,
        MismatchedSquareBrackets,
    }

    /// <summary>
    ///  Reports localized errors for a property-function expression.
    /// </summary>
    private readonly struct ErrorReporter(
        StringSegment text,
        IElementLocation location)
    {
        private const string InvalidFunctionPropertyExpression = nameof(InvalidFunctionPropertyExpression);
        private const string InvalidFunctionStaticMethodSyntax = nameof(InvalidFunctionStaticMethodSyntax);

        private const string InvalidFunctionPropertyExpressionDetailMismatchedParenthesis = nameof(InvalidFunctionPropertyExpressionDetailMismatchedParenthesis);
        private const string InvalidFunctionPropertyExpressionDetailMismatchedQuote = nameof(InvalidFunctionPropertyExpressionDetailMismatchedQuote);
        private const string InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets = nameof(InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets);

        /// <summary>
        ///  Throws when a property-function expression is invalid.
        /// </summary>
        public void VerifyThrowInvalidFunctionPropertyExpression(
            [DoesNotReturnIf(false)] bool condition)
        {
            if (!condition)
            {
                ThrowInvalidFunctionPropertyExpression();
            }
        }

        /// <summary>
        ///  Throws an invalid property-function expression error.
        /// </summary>
        [DoesNotReturn]
        public void ThrowInvalidFunctionPropertyExpression()
            => ProjectErrorUtilities.ThrowInvalidProject(
                location,
                InvalidFunctionPropertyExpression,
                text.ValueOrEmpty,
                string.Empty);

        /// <summary>
        ///  Throws an invalid property-function expression error with additional detail.
        /// </summary>
        [DoesNotReturn]
        public void ThrowInvalidFunctionPropertyExpression(ErrorDetail detail)
            => ProjectErrorUtilities.ThrowInvalidProject(
                location,
                InvalidFunctionPropertyExpression,
                text.ValueOrEmpty,
                GetDetailText(detail));

        /// <summary>
        ///  Throws an invalid static property-function syntax error.
        /// </summary>
        [DoesNotReturn]
        public void ThrowInvalidFunctionStaticMethodSyntax()
            => ProjectErrorUtilities.ThrowInvalidProject(
                location,
                InvalidFunctionStaticMethodSyntax,
                text.ValueOrEmpty,
                string.Empty);

        private static string GetDetailText(ErrorDetail detail)
            => detail switch
            {
                ErrorDetail.MismatchedParenthesis => AssemblyResources.GetString(InvalidFunctionPropertyExpressionDetailMismatchedParenthesis),
                ErrorDetail.MismatchedQuote => AssemblyResources.GetString(InvalidFunctionPropertyExpressionDetailMismatchedQuote),
                ErrorDetail.MismatchedSquareBrackets => AssemblyResources.GetString(InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets),
                _ => Assumed.Unreachable<string>(),
            };
    }
}
