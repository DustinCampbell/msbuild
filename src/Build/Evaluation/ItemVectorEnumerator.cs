// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Enumerates the top-level <c>@(...)</c> item vector expressions contained within an expression.
/// </summary>
/// <remarks>
///  Follows the standard enumerator pattern: call <see cref="MoveNext"/> to advance and read the current
///  match from <see cref="Current"/>.
/// </remarks>
/// <remarks>
///  Initializes a new instance of the <see cref="ItemVectorEnumerator"/> struct that scans
///  <paramref name="expression"/> for <c>@(...)</c> item vector expressions.
/// </remarks>
/// <param name="expression">The expression to scan.</param>
internal struct ItemVectorEnumerator(string expression)
{
    private readonly string _expression = expression;

    private int _index;
    private ItemVector _current;

    /// <summary>
    ///  Gets the item vector expression found by the most recent successful call to <see cref="MoveNext"/>.
    /// </summary>
    public readonly ItemVector Current => _current;

    /// <summary>
    ///  Advances the enumerator to the next item vector expression within the expression.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if another item vector expression was found; otherwise, <see langword="false"/>.
    /// </returns>
    public bool MoveNext()
    {
        while ((_index = _expression.IndexOf("@(", _index, StringComparison.Ordinal)) >= 0)
        {
            if (ExpressionShredder.TryScanItemExpressionCapture(_expression, ref _index, _expression.Length, out _current))
            {
                return true;
            }

            // The "@(" at _index did not begin a well-formed capture. Skip past it; neither the '@'
            // nor the '(' can begin the next "@(", so advance _index by 2.
            _index += 2;
        }

        _current = default;
        return false;
    }
}
