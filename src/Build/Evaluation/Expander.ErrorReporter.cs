// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    /// <summary>
    ///  Reports errors at a source location.
    /// </summary>
    /// <param name="location">The location associated with the error.</param>
    private readonly struct ErrorReporter(IElementLocation location)
    {
        /// <summary>
        ///  Reports a function that is not permitted on a receiver type.
        /// </summary>
        /// <param name="methodName">The requested method.</param>
        /// <param name="typeName">The receiver type.</param>
        /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
        [DoesNotReturn]
        public void ThrowFunctionMethodUnavailable(string methodName, string typeName)
            => ProjectErrorUtilities.ThrowInvalidProject(location, "InvalidFunctionMethodUnavailable", methodName, typeName);

        /// <summary>
        ///  Reports an invalid function expression with its evaluated details.
        /// </summary>
        /// <param name="expression">The expression being evaluated.</param>
        /// <param name="detail">The reason the expression could not be evaluated.</param>
        /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
        [DoesNotReturn]
        public void ThrowInvalidFunctionExpression(string expression, string detail)
            => ProjectErrorUtilities.ThrowInvalidProject(location, "InvalidFunctionPropertyExpression", expression, detail);

        /// <summary>
        ///  Reports invalid static function syntax.
        /// </summary>
        /// <param name="expression">The expression being evaluated.</param>
        /// <param name="detail">The reason the syntax is invalid.</param>
        /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
        [DoesNotReturn]
        public void ThrowInvalidStaticMethodSyntax(string expression, string detail)
            => ProjectErrorUtilities.ThrowInvalidProject(location, "InvalidFunctionStaticMethodSyntax", expression, detail);
    }
}
