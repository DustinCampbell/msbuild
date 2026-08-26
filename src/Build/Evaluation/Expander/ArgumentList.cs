// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides allocation-free access to packed argument ranges.
/// </summary>
internal readonly struct ArgumentList
{
    private readonly string? _buffer;
    private readonly OneOrMany<PropertyFunctionArgument> _arguments;

    internal ArgumentList(string? buffer, OneOrMany<PropertyFunctionArgument> arguments)
    {
        _buffer = buffer;
        _arguments = arguments;
    }

    /// <summary>
    ///  Gets the number of arguments.
    /// </summary>
    public int Count
        => _arguments.Count;

    /// <summary>
    ///  Gets the argument at the specified index.
    /// </summary>
    public StringSegment this[int index]
        => _arguments[index].Range.ToSegment(_buffer);

    public bool RequiresExpansion(int index)
        => _arguments[index].RequiresExpansion;

    public bool ContainsExpandableExpression()
    {
        foreach (PropertyFunctionArgument argument in _arguments)
        {
            if (argument.RequiresExpansion)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Returns an allocation-free enumerator over the arguments.
    /// </summary>
    public Enumerator GetEnumerator()
        => new(_buffer, _arguments);

    /// <summary>
    ///  Enumerates the arguments.
    /// </summary>
    public struct Enumerator(string? buffer, OneOrMany<PropertyFunctionArgument> arguments)
    {
        private OneOrMany<PropertyFunctionArgument>.Enumerator _arguments = arguments.GetEnumerator();

        /// <summary>
        ///  Gets the current argument.
        /// </summary>
        public readonly StringSegment Current
            => _arguments.Current.Range.ToSegment(buffer);

        /// <summary>
        ///  Advances to the next argument.
        /// </summary>
        public bool MoveNext()
            => _arguments.MoveNext();
    }
}
