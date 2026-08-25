// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
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
    ///  The input is the complete content inside the outer <c>$(...)</c>. The parser returns one
    ///  <see cref="ParsedFunction"/> for each member or element access, in execution order.
    ///  <code>
    ///   root-input          ::= static | msbuild-property
    ///   static              ::= "[" type-name "]" "::" member access-suffix*
    ///   msbuild-property    ::= msbuild-property-name access-suffix+
    ///   member              ::= member-name invocation?
    ///   access-suffix       ::= member-access | element-access
    ///   member-access       ::= "." member
    ///   element-access      ::= "[" arguments? "]"
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
        public readonly record struct ParsedMember(
            string Name,
            string[] Arguments,
            MemberKind Kind);

        /// <summary>
        ///  Describes one parsed property-function operation.
        /// </summary>
        /// <param name="Text">The complete parser input.</param>
        /// <param name="StartIndex">The start of this operation within <paramref name="Text"/>.</param>
        /// <param name="ReceiverKind">How binding obtains the receiver.</param>
        /// <param name="Receiver">
        ///  The static type name or MSBuild property name, or <see langword="null"/> for a chained receiver.
        /// </param>
        /// <param name="Member">The CLR member access.</param>
        /// <param name="Location">The project location used for diagnostics.</param>
        public readonly record struct ParsedFunction(
            string Text,
            int StartIndex,
            ReceiverKind ReceiverKind,
            string? Receiver,
            ParsedMember Member,
            IElementLocation Location);

        /// <summary>
        ///  Attempts to parse a complete root property-function expression.
        /// </summary>
        /// <param name="text">The root property-function expression.</param>
        /// <param name="location">The project location used for error reporting.</param>
        /// <param name="functions">The ordered function operations when parsing succeeds.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="text"/> contains a property function; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has recognized but invalid property-function syntax.
        /// </exception>
        public static bool TryParse(
            string text,
            IElementLocation location,
            out ParsedFunction[] functions)
        {
            FunctionParser parser = new(text, location);
            using RefArrayBuilder<ParsedFunction> builder = default;

            int nextAccessIndex;
            if (text[0] == '[')
            {
                builder.Add(parser.ParseStaticRoot(text, out nextAccessIndex));
            }
            else if (parser.TryParseMSBuildPropertyRoot(text, out ParsedFunction function, out nextAccessIndex))
            {
                builder.Add(function);
            }
            else
            {
                functions = [];
                return false;
            }

            while (nextAccessIndex >= 0)
            {
                int accessStartIndex = nextAccessIndex;
                ParsedMember member = parser.ParseAccessSuffix(text, accessStartIndex, out nextAccessIndex);
                builder.Add(new ParsedFunction(
                    text,
                    accessStartIndex,
                    ReceiverKind.Chained,
                    Receiver: null,
                    member,
                    location));
            }

            functions = builder.AsSpan().ToArray();
            return true;
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

        private ParsedFunction ParseStaticRoot(string text, out int nextAccessIndex)
        {
            Assumed.Equal(text[0], '[');

            int typeEndIndex = text.IndexOf(']', 1);
            int invocationStartIndex = text.IndexOf('(');
            if (typeEndIndex < 1
                || (invocationStartIndex >= 0 && typeEndIndex > invocationStartIndex))
            {
                _errors.ThrowInvalidFunctionStaticMethodSyntax();
            }

            string typeName = Strings.WeakIntern(text.AsSpan(1, typeEndIndex - 1));
            int memberStartIndex = typeEndIndex + 1;
            if (memberStartIndex + 2 >= text.Length
                || text[memberStartIndex] != ':'
                || text[memberStartIndex + 1] != ':')
            {
                _errors.ThrowInvalidFunctionStaticMethodSyntax();
            }

            memberStartIndex += 2;
            ParsedMember member = ParseMember(text, memberStartIndex, out nextAccessIndex);
            return new ParsedFunction(text, 0, ReceiverKind.Static, typeName, member, _errors.Location);
        }

        private bool TryParseMSBuildPropertyRoot(
            string text,
            out ParsedFunction function,
            out int nextAccessIndex)
        {
            Assumed.NotEqual(text[0], '[');

            int firstAccessIndex = GetFirstIndex(text.IndexOf('.'), text.IndexOf('['));
            if (firstAccessIndex < 0)
            {
                function = default;
                nextAccessIndex = -1;
                return false;
            }

            string propertyName = Strings.WeakIntern(text.AsSpan(0, firstAccessIndex).Trim());
            if (!IsValidPropertyName(propertyName))
            {
                _errors.ThrowInvalidFunctionPropertyExpression();
            }

            ParsedMember member = ParseAccessSuffix(text, firstAccessIndex, out nextAccessIndex);
            function = new ParsedFunction(
                text,
                0,
                ReceiverKind.MSBuildProperty,
                propertyName,
                member,
                _errors.Location);
            return true;
        }

        private ParsedMember ParseAccessSuffix(
            string text,
            int accessStartIndex,
            out int nextAccessIndex)
        {
            switch (text[accessStartIndex])
            {
                case '.':
                    return ParseMember(text, accessStartIndex + 1, out nextAccessIndex);
                case '[':
                    return ParseIndexer(text, accessStartIndex, out nextAccessIndex);
                default:
                    nextAccessIndex = 0;
                    return Assumed.Unreachable<ParsedMember>();
            }
        }

        private ParsedMember ParseMember(
            string text,
            int memberStartIndex,
            out int nextAccessIndex)
        {
            int invocationStartIndex = text.IndexOf('(', memberStartIndex);
            int firstAccessIndex = GetFirstIndex(
                text.IndexOf('.', memberStartIndex),
                text.IndexOf('[', memberStartIndex));

            if (invocationStartIndex >= 0
                && (firstAccessIndex < 0 || invocationStartIndex < firstAccessIndex))
            {
                ReadOnlySpan<char> name = text.AsSpan(
                    memberStartIndex,
                    invocationStartIndex - memberStartIndex).Trim();
                _errors.VerifyThrowInvalidFunctionPropertyExpression(!name.IsEmpty);

                int argumentsStartIndex = invocationStartIndex + 1;
                int argumentsEndIndex = ScanForClosingParenthesis(text, argumentsStartIndex);
                if (argumentsEndIndex == -1)
                {
                    _errors.ThrowInvalidFunctionPropertyExpression(ErrorDetail.MismatchedParenthesis);
                }

                ReadOnlySpan<char> argumentsSpan = text.AsSpan(
                    argumentsStartIndex,
                    argumentsEndIndex - argumentsStartIndex);
                string[] arguments = !argumentsSpan.IsEmpty
                    ? ExtractFunctionArguments(argumentsSpan, _errors)
                    : [];

                nextAccessIndex = GetNextAccessIndex(text, argumentsEndIndex + 1);
                return new ParsedMember(name.ToString(), arguments, MemberKind.Method);
            }

            int memberEndIndex = firstAccessIndex >= 0 ? firstAccessIndex : text.Length;
            ReadOnlySpan<char> propertyOrFieldName = text.AsSpan(
                memberStartIndex,
                memberEndIndex - memberStartIndex).Trim();
            _errors.VerifyThrowInvalidFunctionPropertyExpression(!propertyOrFieldName.IsEmpty);

            nextAccessIndex = firstAccessIndex;
            return new ParsedMember(
                propertyOrFieldName.ToString(),
                Arguments: [],
                MemberKind.PropertyOrField);
        }

        private ParsedMember ParseIndexer(
            string text,
            int indexerStartIndex,
            out int nextAccessIndex)
        {
            int indexerEndIndex = text.IndexOf(']', indexerStartIndex + 1);
            if (indexerEndIndex < 0)
            {
                _errors.ThrowInvalidFunctionPropertyExpression(ErrorDetail.MismatchedSquareBrackets);
            }

            ReadOnlySpan<char> argumentsSpan = text.AsSpan(
                indexerStartIndex + 1,
                indexerEndIndex - indexerStartIndex - 1);
            string[] arguments = !argumentsSpan.IsEmpty
                ? ExtractFunctionArguments(argumentsSpan, _errors)
                : [];

            nextAccessIndex = GetNextAccessIndex(text, indexerEndIndex + 1);
            return new ParsedMember(string.Empty, arguments, MemberKind.Indexer);
        }

        private int GetNextAccessIndex(string text, int startIndex)
        {
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
            {
                startIndex++;
            }

            if (startIndex == text.Length)
            {
                return -1;
            }

            if (text[startIndex] is '.' or '[')
            {
                return startIndex;
            }

            _errors.ThrowInvalidFunctionPropertyExpression();
            return -1;
        }

        private static int GetFirstIndex(int first, int second)
            => first < 0 ? second : second < 0 ? first : Math.Min(first, second);
    }
}
