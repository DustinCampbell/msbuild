// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Enumerates the top-level <c>@(...)</c> item vector expressions contained within an expression.
/// </summary>
/// <remarks>
///  Follows the standard enumerator pattern: call <see cref="MoveNext"/> to advance and read the current
///  match from <see cref="Current"/>.
/// </remarks>
internal struct ItemVectorEnumerator
{
    private readonly string _expression;
    private int _index;
    private ItemVector _current;

    /// <summary>
    ///  Initializes a new instance of the <see cref="ItemVectorEnumerator"/> struct that scans
    ///  <paramref name="expression"/> for <c>@(...)</c> item vector expressions.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    public ItemVectorEnumerator(string expression)
    {
        _expression = expression;

        _index = expression.IndexOf('@');

        if (_index < 0)
        {
            // If there isn't a '@' character, ensure that MoveNext() will return false.
            _index = _expression.Length;
        }
    }

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
        while (_index < _expression.Length)
        {
            if (ExpressionShredder.TryScanItemExpressionCapture(_expression, ref _index, _expression.Length, out _current))
            {
                return true;
            }

            _index++;
        }

        _current = default;
        return false;
    }
}
