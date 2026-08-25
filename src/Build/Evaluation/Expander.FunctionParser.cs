// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
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
    private readonly partial struct FunctionParser
    {
        private readonly ErrorReporter _errors;

        /// <summary>
        ///  Identifies how a parsed function obtains its receiver.
        /// </summary>
        public enum ReceiverKind
        {
            Static,
            Property,
            Current,
            Indexer,
        }

        public readonly record struct ParsedMember(
            string Name,
            string[] Arguments,
            BindingFlags BindingFlags,
            string Remainder);

        public readonly record struct ParsedFunction(
            string Text,
            ReceiverKind ReceiverKind,
            string? Receiver,
            ParsedMember Member,
            IElementLocation Location);

        /// <summary>
        ///  Attempts to parse a property-function expression.
        /// </summary>
        /// <param name="text">The property-function expression.</param>
        /// <param name="hasReceiver">
        ///  <see langword="true"/> when parsing a chained expression with a current receiver.
        /// </param>
        /// <param name="location">The project location used for error reporting.</param>
        /// <param name="function">The parsed function syntax when this method returns <see langword="true"/>.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="text"/> contains a property function; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has recognized but invalid property-function syntax.
        /// </exception>
        public static bool TryParse(
            string text,
            bool hasReceiver,
            IElementLocation location,
            out ParsedFunction function)
        {
            FunctionParser parser = new(text, location);

            if (text[0] == '[')
            {
                function = !hasReceiver
                    ? parser.ParseStaticPropertyFunction(text)
                    : parser.ParseInstanceIndexerFunction(text);

                return true;
            }

            return parser.TryParseInstancePropertyFunction(text, hasReceiver, out function);
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
        ///  Extracts a static property or function.
        /// </summary>
        /// <param name="text">The static property-function expression.</param>
        /// <returns>
        ///  The parsed function.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has invalid static property-function syntax or names an unavailable type.
        /// </exception>
        private ParsedFunction ParseStaticPropertyFunction(string text)
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
        ///  Extracts an instance indexer.
        /// </summary>
        /// <param name="text">The indexer expression.</param>
        /// <returns>
        ///  The parsed indexer syntax.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has mismatched square brackets.
        /// </exception>
        private ParsedFunction ParseInstanceIndexerFunction(string text)
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
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod,
                text.Substring(indexerEndIndex + 1));

            return new ParsedFunction(text, ReceiverKind.Indexer, Receiver: null, member, _errors.Location);
        }

        /// <summary>
        ///  Extracts an instance property or function.
        /// </summary>
        /// <param name="text">The instance property-function expression.</param>
        /// <param name="hasReceiver">
        ///  <see langword="true"/> when parsing a chained expression with a current receiver.
        /// </param>
        /// <param name="function">The parsed function syntax.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="text"/> contains an instance member invocation.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has invalid property-function syntax.
        /// </exception>
        private bool TryParseInstancePropertyFunction(
            string text,
            bool hasReceiver,
            out ParsedFunction function)
        {
            Assumed.NotEqual(text[0], '[');

            int argumentStartIndex = text.IndexOf('(');

            // Look for an instance function call next, such as in SomeStuff.ToLower()
            int rootEndIndex = text.IndexOf('.');
            if (rootEndIndex == -1 || (argumentStartIndex >= 0 && rootEndIndex > argumentStartIndex))
            {
                // We don't have a function invocation in the expression root, return null
                function = default;
                return false;
            }

            // If this is an instance function rather than a static, then we'll capture the name of the property referenced
            string functionReceiver = Strings.WeakIntern(text.AsSpan(0, rootEndIndex).Trim());

            // If propertyValue is null (we're not recursing), then we're expecting a valid property name
            if (!hasReceiver && !IsValidPropertyName(functionReceiver))
            {
                // We extracted something that wasn't a valid property name, fail.
                _errors.ThrowInvalidFunctionPropertyExpression();
            }

            // Skip over the '.'.
            ParsedMember member = ParseMember(text, argumentStartIndex, rootEndIndex + 1);
            ReceiverKind receiverKind = hasReceiver ? ReceiverKind.Current : ReceiverKind.Property;
            string? receiver = hasReceiver ? null : functionReceiver;
            function = new ParsedFunction(text, receiverKind, receiver, member, _errors.Location);
            return true;
        }

        /// <summary>
        ///  Parses the name, arguments, binding flags, invocation type, and remainder of a static or instance
        ///  function.
        /// </summary>
        /// <param name="text">The property-function expression.</param>
        /// <param name="argumentStartIndex">The index of the opening parenthesis, or <c>-1</c> for property access.</param>
        /// <param name="methodStartIndex">The index at which the member name begins.</param>
        /// <returns>
        ///  The parsed member invocation.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has invalid property-function syntax.
        /// </exception>
        private ParsedMember ParseMember(string text, int argumentStartIndex, int methodStartIndex)
        {
            // The unevaluated and unexpanded arguments for this function
            string[] arguments;

            // The name of the function that will be invoked
            ReadOnlySpan<char> name;

            // What's left of the expression once the function has been constructed
            ReadOnlySpan<char> remainder = default;

            // The binding flags that we will use for this function's execution
            BindingFlags defaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

            // There are arguments that need to be passed to the function
            if (argumentStartIndex > -1 && text.IndexOf('.', methodStartIndex, argumentStartIndex - methodStartIndex) == -1)
            {
                // separate the function and the arguments
                name = text.AsSpan(methodStartIndex, argumentStartIndex - methodStartIndex).Trim();

                // Skip the '('
                argumentStartIndex++;

                // Scan for the matching closing bracket, skipping any nested ones
                int argumentsEndIndex = ScanForClosingParenthesis(text, argumentStartIndex);

                if (argumentsEndIndex == -1)
                {
                    _errors.ThrowInvalidFunctionPropertyExpression(ErrorDetail.MismatchedParenthesis);
                }

                // We have been asked for a method invocation
                defaultBindingFlags |= BindingFlags.InvokeMethod;

                ReadOnlySpan<char> argumentsSpan = text.AsSpan(argumentStartIndex, argumentsEndIndex - argumentStartIndex);
                arguments = !argumentsSpan.IsEmpty
                    ? ExtractFunctionArguments(argumentsSpan, _errors)
                    : [];

                remainder = text.AsSpan(argumentsEndIndex + 1).Trim();
            }
            else
            {
                int remainderStartIndex = text.IndexOf('.', methodStartIndex);
                int indexerIndex = text.IndexOf('[', methodStartIndex);

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

                name = text.AsSpan(methodStartIndex, methodEndIndex - methodStartIndex).Trim();
                _errors.VerifyThrowInvalidFunctionPropertyExpression(!name.IsEmpty);

                // We have been asked for a property or a field
                defaultBindingFlags |= BindingFlags.GetProperty | BindingFlags.GetField;
            }

            // either there are no functions left or what we have is another function or an indexer
            if (remainder is [] or ['.' or '[', ..])
            {
                return new ParsedMember(name.ToString(), arguments, defaultBindingFlags, remainder.ToString());
            }

            // We ended up with something other than a function expression
            _errors.ThrowInvalidFunctionPropertyExpression();
            return default;
        }
    }
}
