// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private readonly partial struct FunctionParser
    {
        /// <summary>
        ///  Identifies additional detail for an invalid property-function expression.
        /// </summary>
        public enum ErrorDetail
        {
            /// <summary>
            ///  The expression contains mismatched parentheses.
            /// </summary>
            MismatchedParenthesis,

            /// <summary>
            ///  The expression contains a mismatched quote.
            /// </summary>
            MismatchedQuote,

            /// <summary>
            ///  The expression contains mismatched square brackets.
            /// </summary>
            MismatchedSquareBrackets,
        }
    }
}
