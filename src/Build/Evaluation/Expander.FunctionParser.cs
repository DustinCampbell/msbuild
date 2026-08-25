// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Parses property-function expressions into syntax independent of runtime receiver values.
    /// </summary>
    /// <remarks>
    ///  A root input is the content inside the outer <c>$(...)</c>. A continuation input is the
    ///  unparsed suffix of a preceding function. The parser consumes one member at a time and leaves
    ///  subsequent access syntax in <see cref="ParsedMember.Remainder"/>.
    ///  <code>
    ///   root-input          ::= static | msbuild-property
    ///   continuation-input  ::= access-suffix
    ///   static              ::= "[" type-name "]" "::" member
    ///   msbuild-property    ::= msbuild-property-name member-access
    ///   member              ::= member-name invocation? access-suffix?
    ///   access-suffix       ::= member-access | element-access
    ///   member-access       ::= "." member
    ///   element-access      ::= "[" arguments? "]" access-suffix?
    ///   invocation          ::= "(" arguments? ")"
    ///   arguments           ::= argument ("," argument)*
    ///  </code>
    ///  <c>msbuild-property-name</c> names the MSBuild property whose value supplies the initial
    ///  receiver. <c>member-name</c> names a CLR method, property, or field accessed on that receiver.
    ///  Quoted spans and nested <c>$(...)</c> expressions are treated atomically while splitting
    ///  arguments, so commas inside them do not delimit arguments.
    /// </remarks>
    private readonly partial struct FunctionParser
    {
        private readonly ErrorReporter _errors;

        /// <summary>
        ///  Identifies how a parsed function obtains its receiver.
        /// </summary>
        public enum ReceiverKind
        {
            /// <summary>
            ///  The receiver is a type named by a static property-function expression, such as
            ///  <c>$([System.String]::Concat('a', 'b'))</c>.
            /// </summary>
            Static,

            /// <summary>
            ///  The receiver is the value of an MSBuild property named by the expression, such as
            ///  <c>$(Configuration.ToUpperInvariant())</c>.
            /// </summary>
            MSBuildProperty,

            /// <summary>
            ///  The receiver is the result of the preceding function in a chained expression, such as
            ///  <c>$([System.IO.Path]::GetFileName('a.txt').ToUpperInvariant())</c>.
            /// </summary>
            Chained,
        }

        /// <summary>
        ///  Identifies the syntax used to access a member.
        /// </summary>
        public enum MemberKind
        {
            /// <summary>
            ///  The member is invoked as a method, such as <c>$(Value.Trim())</c>.
            /// </summary>
            Method,

            /// <summary>
            ///  The member is read as a property or field, such as <c>$(Value.Length)</c>.
            /// </summary>
            PropertyOrField,

            /// <summary>
            ///  The receiver is indexed, such as <c>$(Value[0])</c>.
            /// </summary>
            Indexer,
        }

        /// <summary>
        ///  Describes one parsed CLR member access.
        /// </summary>
        /// <param name="Name">
        ///  The CLR member name, or an empty string when <paramref name="Kind"/> is
        ///  <see cref="MemberKind.Indexer"/>.
        /// </param>
        /// <param name="Arguments">The unexpanded argument text.</param>
        /// <param name="Kind">The member-access syntax.</param>
        /// <param name="Remainder">The unparsed access suffix following this member.</param>
        public readonly record struct ParsedMember(
            string Name,
            string[] Arguments,
            MemberKind Kind,
            string Remainder);

        /// <summary>
        ///  Describes one parsed property-function operation.
        /// </summary>
        /// <param name="Text">The complete parser input.</param>
        /// <param name="ReceiverKind">How binding obtains the receiver.</param>
        /// <param name="Receiver">
        ///  The static type name or MSBuild property name, or <see langword="null"/> for a chained receiver.
        /// </param>
        /// <param name="Member">The CLR member access.</param>
        /// <param name="Location">The project location used for diagnostics.</param>
        public readonly record struct ParsedFunction(
            string Text,
            ReceiverKind ReceiverKind,
            string? Receiver,
            ParsedMember Member,
            IElementLocation Location);

        /// <summary>
        ///  Attempts to parse a root property-function expression.
        /// </summary>
        /// <param name="text">The property-function expression.</param>
        /// <param name="location">The project location used for error reporting.</param>
        /// <param name="function">The parsed function syntax when this method returns <see langword="true"/>.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="text"/> contains a property function; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has recognized but invalid property-function syntax.
        /// </exception>
        public static bool TryParseRoot(
            string text,
            IElementLocation location,
            out ParsedFunction function)
        {
            FunctionParser parser = new(text, location);

            if (text[0] == '[')
            {
                function = parser.ParseStaticExpression(text);
                return true;
            }

            return parser.TryParseMSBuildPropertyExpression(text, out function);
        }

        /// <summary>
        ///  Attempts to parse an access suffix against a receiver produced by a preceding function.
        /// </summary>
        /// <param name="text">The continuation text.</param>
        /// <param name="location">The project location used for error reporting.</param>
        /// <param name="function">The parsed continuation when this method returns <see langword="true"/>.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="text"/> contains an access suffix; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        public static bool TryParseContinuation(
            string text,
            IElementLocation location,
            out ParsedFunction function)
        {
            FunctionParser parser = new(text, location);

            if (text[0] == '[')
            {
                function = parser.ParseIndexerExpression(text);
                return true;
            }

            return parser.TryParseChainedExpression(text, out function);
        }

        /// <summary>
        ///  Initializes a parser for the supplied expression and evaluation context.
        /// </summary>
        /// <param name="text">The property-function expression.</param>
        /// <param name="location">The project location used for error reporting.</param>
        private FunctionParser(string text, IElementLocation location)
        {
            _errors = new(text, location);
            _errors.VerifyThrowInvalidFunctionPropertyExpression(!text.IsNullOrEmpty());
        }

        /// <summary>
        ///  Parses a static expression.
        /// </summary>
        /// <param name="text">The static property-function expression.</param>
        /// <returns>
        ///  The parsed function.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has invalid static property-function syntax.
        /// </exception>
        private ParsedFunction ParseStaticExpression(string text)
        {
            Assumed.Equal(text[0], '[');

            int argumentStartIndex = text.IndexOf('(');
            int typeEndIndex = text.IndexOf(']', startIndex: 1);

            if (typeEndIndex < 1 || (argumentStartIndex >= 0 && typeEndIndex > argumentStartIndex))
            {
                _errors.ThrowInvalidFunctionStaticMethodSyntax();
            }

            string typeName = Strings.WeakIntern(text.AsSpan(1, typeEndIndex - 1));

            int methodStartIndex = typeEndIndex + 1;
            int expressionRootLength = argumentStartIndex >= 0
                ? argumentStartIndex
                : text.Length;

            if (expressionRootLength <= methodStartIndex + 2 || text[methodStartIndex] != ':' || text[methodStartIndex + 1] != ':')
            {
                _errors.ThrowInvalidFunctionStaticMethodSyntax();
            }

            // skip over the "::"
            methodStartIndex += 2;

            ParsedMember member = ParseMember(text, argumentStartIndex, methodStartIndex);
            return new ParsedFunction(text, ReceiverKind.Static, typeName, member, _errors.Location);
        }

        /// <summary>
        ///  Parses an indexer applied to a chained receiver.
        /// </summary>
        /// <param name="text">The indexer expression.</param>
        /// <returns>
        ///  The parsed indexer syntax.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has mismatched square brackets.
        /// </exception>
        private ParsedFunction ParseIndexerExpression(string text)
        {
            Assumed.Equal(text[0], '[');

            int indexerEndIndex = text.IndexOf(']', 1);
            if (indexerEndIndex < 1)
            {
                _errors.ThrowInvalidFunctionPropertyExpression(ErrorDetail.MismatchedSquareBrackets);
            }

            ReadOnlySpan<char> argumentsSpan = text.AsSpan(1, indexerEndIndex - 1);
            string[] arguments = !argumentsSpan.IsEmpty
                ? ExtractFunctionArguments(argumentsSpan, _errors)
                : [];

            ParsedMember member = new(
                string.Empty,
                arguments,
                MemberKind.Indexer,
                text.Substring(indexerEndIndex + 1));

            return new ParsedFunction(text, ReceiverKind.Chained, Receiver: null, member, _errors.Location);
        }

        /// <summary>
        ///  Attempts to parse an expression whose receiver is an MSBuild property.
        /// </summary>
        /// <param name="text">The instance property-function expression.</param>
        /// <param name="function">The parsed function syntax.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="text"/> contains an MSBuild property receiver and
        ///  member access.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has invalid property-function syntax.
        /// </exception>
        private bool TryParseMSBuildPropertyExpression(string text, out ParsedFunction function)
        {
            Assumed.NotEqual(text[0], '[');

            int argumentStartIndex = text.IndexOf('(');
            int rootEndIndex = text.IndexOf('.');
            if (rootEndIndex == -1 || (argumentStartIndex >= 0 && rootEndIndex > argumentStartIndex))
            {
                function = default;
                return false;
            }

            string propertyName = Strings.WeakIntern(text.AsSpan(0, rootEndIndex).Trim());
            if (!IsValidPropertyName(propertyName))
            {
                _errors.ThrowInvalidFunctionPropertyExpression();
            }

            ParsedMember member = ParseMember(text, argumentStartIndex, rootEndIndex + 1);
            function = new ParsedFunction(
                text,
                ReceiverKind.MSBuildProperty,
                propertyName,
                member,
                _errors.Location);
            return true;
        }

        /// <summary>
        ///  Attempts to parse a member-access suffix against the result of a preceding function.
        /// </summary>
        /// <param name="text">The member-access suffix.</param>
        /// <param name="function">The parsed function syntax.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="text"/> begins with member access; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has invalid property-function syntax.
        /// </exception>
        private bool TryParseChainedExpression(string text, out ParsedFunction function)
        {
            if (text[0] != '.')
            {
                function = default;
                return false;
            }

            int argumentStartIndex = text.IndexOf('(');
            ParsedMember member = ParseMember(text, argumentStartIndex, memberStartIndex: 1);
            function = new ParsedFunction(
                text,
                ReceiverKind.Chained,
                Receiver: null,
                member,
                _errors.Location);
            return true;
        }

        /// <summary>
        ///  Parses a member name, access kind, arguments, and remainder.
        /// </summary>
        /// <param name="text">The property-function expression.</param>
        /// <param name="argumentStartIndex">The index of the opening parenthesis, or <c>-1</c> for property access.</param>
        /// <param name="memberStartIndex">The index at which the member name begins.</param>
        /// <returns>
        ///  The parsed member invocation.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has invalid property-function syntax.
        /// </exception>
        private ParsedMember ParseMember(string text, int argumentStartIndex, int memberStartIndex)
        {
            // The unevaluated and unexpanded arguments for this function
            string[] arguments;

            // The name of the function that will be invoked
            ReadOnlySpan<char> name;

            // What's left of the expression once the function has been constructed
            ReadOnlySpan<char> remainder = default;

            MemberKind memberKind;

            // There are arguments that need to be passed to the function
            if (argumentStartIndex > -1 && text.IndexOf('.', memberStartIndex, argumentStartIndex - memberStartIndex) == -1)
            {
                // separate the function and the arguments
                name = text.AsSpan(memberStartIndex, argumentStartIndex - memberStartIndex).Trim();

                // Skip the '('
                argumentStartIndex++;

                // Scan for the matching closing bracket, skipping any nested ones
                int argumentsEndIndex = ScanForClosingParenthesis(text, argumentStartIndex);

                if (argumentsEndIndex == -1)
                {
                    _errors.ThrowInvalidFunctionPropertyExpression(ErrorDetail.MismatchedParenthesis);
                }

                memberKind = MemberKind.Method;

                ReadOnlySpan<char> argumentsSpan = text.AsSpan(argumentStartIndex, argumentsEndIndex - argumentStartIndex);
                arguments = !argumentsSpan.IsEmpty
                    ? ExtractFunctionArguments(argumentsSpan, _errors)
                    : [];

                remainder = text.AsSpan(argumentsEndIndex + 1).Trim();
            }
            else
            {
                int remainderStartIndex = text.IndexOf('.', memberStartIndex);
                int indexerIndex = text.IndexOf('[', memberStartIndex);

                // We don't want to consume the indexer
                if (indexerIndex >= 0 && (remainderStartIndex == -1 || indexerIndex < remainderStartIndex))
                {
                    remainderStartIndex = indexerIndex;
                }

                arguments = [];

                int methodEndIndex;
                if (remainderStartIndex >= 0)
                {
                    methodEndIndex = remainderStartIndex;
                    remainder = text.AsSpan(remainderStartIndex).Trim();
                }
                else
                {
                    methodEndIndex = text.Length;
                }

                name = text.AsSpan(memberStartIndex, methodEndIndex - memberStartIndex).Trim();
                _errors.VerifyThrowInvalidFunctionPropertyExpression(!name.IsEmpty);

                memberKind = MemberKind.PropertyOrField;
            }

            // either there are no functions left or what we have is another function or an indexer
            if (remainder is [] or ['.' or '[', ..])
            {
                return new ParsedMember(name.ToString(), arguments, memberKind, remainder.ToString());
            }

            // We ended up with something other than a function expression
            _errors.ThrowInvalidFunctionPropertyExpression();
            return default;
        }
    }
}
