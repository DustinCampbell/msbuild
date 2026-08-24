// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Parses property-function expressions and creates executable <see cref="Function"/> instances.
    /// </summary>
    private readonly partial struct FunctionParser
    {
        private readonly ErrorReporter _errors;
        private readonly PropertiesUseTracker _propertiesUseTracker;
        private readonly IFileSystem _fileSystem;
        private readonly LoggingContext _loggingContext;
        private readonly IElementLocation _location;

        /// <summary>
        ///  Contains the parsed member invocation independent of its receiver.
        /// </summary>
        /// <param name="Name">The member name.</param>
        /// <param name="Arguments">The unexpanded function arguments.</param>
        /// <param name="BindingFlags">The flags describing how to bind the member.</param>
        /// <param name="Remainder">The unparsed expression following the member.</param>
        private readonly record struct ParsedFunction(string Name, string[] Arguments, BindingFlags BindingFlags, string Remainder);

        /// <summary>
        ///  Attempts to parse a property-function expression.
        /// </summary>
        /// <param name="text">The property-function expression.</param>
        /// <param name="propertyValue">The current receiver value, or <see langword="null"/> for an initial expression.</param>
        /// <param name="location">The project location used for error reporting.</param>
        /// <param name="propertiesUseTracker">Tracks property reads performed while evaluating the function.</param>
        /// <param name="fileSystem">The file system used by file and directory property functions.</param>
        /// <param name="loggingContext">The logging context for the operation.</param>
        /// <param name="function">The parsed function when this method returns <see langword="true"/>.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="text"/> contains a property function; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has recognized but invalid property-function syntax.
        /// </exception>
        public static bool TryParse(
            string text,
            object? propertyValue,
            IElementLocation location,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem,
            LoggingContext loggingContext,
            [NotNullWhen(true)] out Function? function)
        {
            FunctionParser parser = new(text, location, propertiesUseTracker, fileSystem, loggingContext);

            if (text[0] == '[')
            {
                // A static property or function is the content that follows the last "::", the rest being the type.
                function = propertyValue is null
                    ? parser.ParseStaticPropertyFunction(text)
                    : parser.ParseInstanceIndexerFunction(text, propertyValue);

                return true;
            }

            function = parser.ParseInstancePropertyFunction(text, propertyValue);
            return function is not null;
        }

        /// <summary>
        ///  Initializes a parser for the supplied expression and evaluation context.
        /// </summary>
        /// <param name="text">The property-function expression.</param>
        /// <param name="location">The project location used for error reporting.</param>
        /// <param name="propertiesUseTracker">Tracks property reads performed while evaluating the function.</param>
        /// <param name="fileSystem">The file system used by file and directory property functions.</param>
        /// <param name="loggingContext">The logging context for the operation.</param>
        private FunctionParser(
            string text,
            IElementLocation location,
            PropertiesUseTracker propertiesUseTracker,
            IFileSystem fileSystem,
            LoggingContext loggingContext)
        {
            _errors = new(text, location);
            _errors.VerifyThrowInvalidFunctionPropertyExpression(!text.IsNullOrEmpty());

            _propertiesUseTracker = propertiesUseTracker;
            _fileSystem = fileSystem;
            _loggingContext = loggingContext;
            _location = location;
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
        private Function ParseStaticPropertyFunction(string text)
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

            ParsedFunction parsedFunction = ParseFunction(text, argumentStartIndex, methodStartIndex);

            // Locate a type that matches the body of the expression.
            if (!AvailableStaticMembers.TryResolveType(typeName, parsedFunction.Name, out Type? receiverType))
            {
                _errors.ThrowInvalidFunctionTypeUnavailable(typeName);
            }

            return CreateFunction(receiverType, text, receiver: null, parsedFunction);
        }

        /// <summary>
        ///  Extracts an instance indexer.
        /// </summary>
        /// <param name="text">The indexer expression.</param>
        /// <param name="propertyValue">The value on which to invoke the indexer.</param>
        /// <returns>
        ///  The parsed indexer function.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has mismatched square brackets.
        /// </exception>
        private Function ParseInstanceIndexerFunction(string text, object propertyValue)
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

            string name = propertyValue switch
            {
                Array => "GetValue",
                string => "get_Chars",
                _ => "get_Item",
            };

            ParsedFunction parsedFunction = new(
                name,
                arguments,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod,
                text.Substring(indexerEndIndex + 1));

            return CreateFunction(propertyValue.GetType(), text, receiver: null, parsedFunction);
        }

        /// <summary>
        ///  Extracts an instance property or function.
        /// </summary>
        /// <param name="text">The instance property-function expression.</param>
        /// <param name="propertyValue">The current receiver value, or <see langword="null"/> for an initial expression.</param>
        /// <returns>
        ///  The parsed function, or <see langword="null"/> when <paramref name="text"/> does not contain an
        ///  instance member invocation.
        /// </returns>
        /// <exception cref="Exceptions.InvalidProjectFileException">
        ///  <paramref name="text"/> has invalid property-function syntax.
        /// </exception>
        private Function? ParseInstancePropertyFunction(string text, object? propertyValue)
        {
            Assumed.NotEqual(text[0], '[');

            int argumentStartIndex = text.IndexOf('(');

            // Look for an instance function call next, such as in SomeStuff.ToLower()
            int rootEndIndex = text.IndexOf('.');
            if (rootEndIndex == -1 || (argumentStartIndex >= 0 && rootEndIndex > argumentStartIndex))
            {
                // We don't have a function invocation in the expression root, return null
                return null;
            }

            // If this is an instance function rather than a static, then we'll capture the name of the property referenced
            string functionReceiver = Strings.WeakIntern(text.AsSpan(0, rootEndIndex).Trim());

            // If propertyValue is null (we're not recursing), then we're expecting a valid property name
            if (propertyValue == null && !IsValidPropertyName(functionReceiver))
            {
                // We extracted something that wasn't a valid property name, fail.
                _errors.ThrowInvalidFunctionPropertyExpression();
            }

            // If we are recursively acting on a type that has been already produced then pass that type inwards (e.g. we are interpreting a function call chain)
            // Otherwise, the receiver of the function is a string
            Type receiverType = propertyValue?.GetType() ?? typeof(string);

            // Skip over the '.'.
            ParsedFunction parsedFunction = ParseFunction(text, argumentStartIndex, rootEndIndex + 1);

            return CreateFunction(receiverType, text, functionReceiver, parsedFunction);
        }

        /// <summary>
        ///  Creates a property function from a receiver type whose preserved members are known by invariant.
        /// </summary>
        /// <param name="receiverType">The type that declares the member.</param>
        /// <param name="text">The complete property-function expression.</param>
        /// <param name="receiver">The property name supplying the receiver, or <see langword="null"/>.</param>
        /// <param name="parsedFunction">The parsed member invocation.</param>
        /// <returns>
        ///  The executable function.
        /// </returns>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2067",
            Justification = "Receiver type comes from the static-member allowlist (public members preserved by AvailableStaticMembers.PropertyFunctionMembers) or a runtime GetType(); only public members are bound.")]
        private Function CreateFunction(
            Type receiverType,
            string text,
            string? receiver,
            ParsedFunction parsedFunction)
            => new(
                receiverType,
                text,
                receiver,
                parsedFunction.Name,
                parsedFunction.Arguments,
                parsedFunction.BindingFlags,
                parsedFunction.Remainder,
                _propertiesUseTracker,
                _fileSystem,
                _loggingContext,
                _location);

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
        private ParsedFunction ParseFunction(string text, int argumentStartIndex, int methodStartIndex)
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
                return new ParsedFunction(name.ToString(), arguments, defaultBindingFlags, remainder.ToString());
            }

            // We ended up with something other than a function expression
            _errors.ThrowInvalidFunctionPropertyExpression();
            return default;
        }
    }
}
