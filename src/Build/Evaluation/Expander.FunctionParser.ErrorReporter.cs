// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private readonly partial struct FunctionParser
    {
        /// <summary>
        ///  Reports localized errors for a property-function expression.
        /// </summary>
        /// <param name="text">The complete property-function expression.</param>
        /// <param name="location">The project location used for error reporting.</param>
        public readonly struct ErrorReporter(string text, IElementLocation location)
        {
            private const string InvalidFunctionPropertyExpression = nameof(InvalidFunctionPropertyExpression);
            private const string InvalidFunctionMethodUnavailable = nameof(InvalidFunctionMethodUnavailable);
            private const string InvalidFunctionStaticMethodSyntax = nameof(InvalidFunctionStaticMethodSyntax);
            private const string InvalidFunctionTypeUnavailable = nameof(InvalidFunctionTypeUnavailable);

            private const string InvalidFunctionPropertyExpressionDetailMismatchedParenthesis = nameof(InvalidFunctionPropertyExpressionDetailMismatchedParenthesis);
            private const string InvalidFunctionPropertyExpressionDetailMismatchedQuote = nameof(InvalidFunctionPropertyExpressionDetailMismatchedQuote);
            private const string InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets = nameof(InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets);

            /// <summary>
            ///  Gets the project location associated with the parsed expression.
            /// </summary>
            public IElementLocation Location => location;

            /// <summary>
            ///  Gets the localized message for an error detail.
            /// </summary>
            /// <param name="detail">The error detail to localize.</param>
            /// <returns>
            ///  The localized error detail.
            /// </returns>
            private static string GetDetailText(ErrorDetail detail)
                => detail switch
                {
                    ErrorDetail.MismatchedParenthesis => AssemblyResources.GetString(InvalidFunctionPropertyExpressionDetailMismatchedParenthesis),
                    ErrorDetail.MismatchedQuote => AssemblyResources.GetString(InvalidFunctionPropertyExpressionDetailMismatchedQuote),
                    ErrorDetail.MismatchedSquareBrackets => AssemblyResources.GetString(InvalidFunctionPropertyExpressionDetailMismatchedSquareBrackets),

                    _ => Assumed.Unreachable<string>()
                };

            /// <summary>
            ///  Throws when a property-function expression is invalid.
            /// </summary>
            /// <param name="condition">
            ///  <see langword="true"/> when the expression is valid; otherwise, <see langword="false"/>.
            /// </param>
            /// <exception cref="Microsoft.Build.Exceptions.InvalidProjectFileException">
            ///  <paramref name="condition"/> is <see langword="false"/>.
            /// </exception>
            public void VerifyThrowInvalidFunctionPropertyExpression([DoesNotReturnIf(false)] bool condition)
                => ProjectErrorUtilities.VerifyThrowInvalidProject(condition, location, InvalidFunctionPropertyExpression, text, string.Empty);

            /// <summary>
            ///  Throws an invalid property-function expression error.
            /// </summary>
            /// <exception cref="Microsoft.Build.Exceptions.InvalidProjectFileException">
            ///  Always thrown.
            /// </exception>
            [DoesNotReturn]
            public void ThrowInvalidFunctionPropertyExpression()
                => ProjectErrorUtilities.ThrowInvalidProject(location, InvalidFunctionPropertyExpression, text, string.Empty);

            /// <summary>
            ///  Throws an invalid property-function expression error with additional detail.
            /// </summary>
            /// <param name="detail">The detail describing the syntax error.</param>
            /// <exception cref="Microsoft.Build.Exceptions.InvalidProjectFileException">
            ///  Always thrown.
            /// </exception>
            [DoesNotReturn]
            public void ThrowInvalidFunctionPropertyExpression(ErrorDetail detail)
                => ProjectErrorUtilities.ThrowInvalidProject(location, InvalidFunctionPropertyExpression, text, GetDetailText(detail));

            /// <summary>
            ///  Throws an unavailable property-function member error.
            /// </summary>
            /// <param name="memberName">The unavailable member name.</param>
            /// <param name="typeName">The receiver type name.</param>
            /// <exception cref="Exceptions.InvalidProjectFileException">
            ///  Always thrown.
            /// </exception>
            [DoesNotReturn]
            public void ThrowInvalidFunctionMethodUnavailable(string memberName, string? typeName)
                => ProjectErrorUtilities.ThrowInvalidProject(location, InvalidFunctionMethodUnavailable, memberName, typeName);

            /// <summary>
            ///  Throws an invalid static property-function syntax error.
            /// </summary>
            /// <exception cref="Microsoft.Build.Exceptions.InvalidProjectFileException">
            ///  Always thrown.
            /// </exception>
            [DoesNotReturn]
            public void ThrowInvalidFunctionStaticMethodSyntax()
                => ProjectErrorUtilities.ThrowInvalidProject(location, InvalidFunctionStaticMethodSyntax, text, string.Empty);

            /// <summary>
            ///  Throws an unavailable property-function type error.
            /// </summary>
            /// <param name="typeName">The type name that could not be resolved.</param>
            /// <exception cref="Microsoft.Build.Exceptions.InvalidProjectFileException">
            ///  Always thrown.
            /// </exception>
            [DoesNotReturn]
            public void ThrowInvalidFunctionTypeUnavailable(string typeName)
                => ProjectErrorUtilities.ThrowInvalidProject(location, InvalidFunctionTypeUnavailable, text, typeName);
        }
    }
}
