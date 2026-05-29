// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Node representing a string
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class StringExpressionNode : ExpressionNode
{
    private readonly ReadOnlyMemory<char> _value;
    private readonly bool _expandable;

    /// <summary>
    /// The string value, lazily materialized from <see cref="_value"/>.
    /// </summary>
    private string? _valueText;

    private string? _cachedExpandedValue;
    private bool? _shouldBeTreatedAsVisualStudioVersion;

    /// <param name="value">The unexpanded string value.</param>
    /// <param name="expandable">
    /// Whether the string potentially has expandable content,
    /// such as a property expression or escaped character.
    /// </param>
    public StringExpressionNode(ReadOnlyMemory<char> value, bool expandable)
    {
        _value = value;
        _expandable = expandable;
    }

    private string ValueText => _valueText ??= _value.ToString();

    private static Version CurrentVisualStudioVersion
        => field ??= Version.Parse(MSBuildConstants.CurrentVisualStudioVersion);

    private static double? s_currentVisualStudioVersionAsDouble;

    private static double CurrentVisualStudioVersionAsDouble
        => s_currentVisualStudioVersionAsDouble ??= ConversionUtilities.ConvertDecimalOrHexToDouble(MSBuildConstants.CurrentVisualStudioVersion);

    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        => !_expandable
            ? ConversionUtilities.TryConvertStringToBool(_value.Span, out result)
            : ConversionUtilities.TryConvertStringToBool(GetExpandedValue(state), out result);

    public override bool TryEvaluateAsNumber(ConditionEvaluator.IConditionEvaluationState state, out double result)
    {
        if (ShouldBeTreatedAsVisualStudioVersion(state))
        {
            result = CurrentVisualStudioVersionAsDouble;
            return true;
        }

#if NET
        if (!_expandable)
        {
            return ConversionUtilities.TryConvertDecimalOrHexToDouble(_value.Span, out result);
        }
#endif

        return ConversionUtilities.TryConvertDecimalOrHexToDouble(GetExpandedValue(state), out result);
    }

    public override bool TryEvaluateAsVersion(ConditionEvaluator.IConditionEvaluationState state, out Version? result)
    {
        if (ShouldBeTreatedAsVisualStudioVersion(state))
        {
            result = CurrentVisualStudioVersion;
            return true;
        }

#if NET
        if (!_expandable)
        {
            return Version.TryParse(_value.Span, out result);
        }
#endif

        return Version.TryParse(GetExpandedValue(state), out result);
    }

    /// <summary>
    /// Returns true if this node evaluates to an empty string,
    /// otherwise false.
    /// It may be cheaper to determine whether an expression will evaluate
    /// to empty than to fully evaluate it.
    /// Implementations should cache the result so that calls after the first are free.
    /// </summary>
    internal override bool EvaluatesToEmpty(ConditionEvaluator.IConditionEvaluationState state)
    {
        if (_cachedExpandedValue is null)
        {
            if (_expandable)
            {
                switch (_value.Span)
                {
                    case []:
                        _cachedExpandedValue = string.Empty;
                        return true;

                    // If the length is 1 or 2, it can't possibly be a property, item, or metadata, and it isn't empty.
                    case [_] or [_, _]:
                        _cachedExpandedValue = ValueText;
                        return false;

                    case not ['$' or '%' or '@', '(', .., ')']: // This isn't just a property, item, or metadata value, and it isn't empty.
                        return false;
                }

                string? expandBreakEarly = state.ExpandIntoStringBreakEarly(ValueText);

                if (expandBreakEarly is null)
                {
                    // It broke early: we can't store the value, we just
                    // know it's non empty
                    return false;
                }

                // It didn't break early, the result is accurate,
                // so store it so the work isn't done again.
                _cachedExpandedValue = expandBreakEarly;
            }
            else
            {
                _cachedExpandedValue = ValueText;
            }
        }

        return _cachedExpandedValue.Length == 0;
    }

    internal override bool IsUnexpandedValueEmpty()
        => _value.Length == 0;

    /// <summary>
    /// Value before any item and property expressions are expanded
    /// </summary>
    internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => ValueText;

    /// <summary>
    /// Value after any item and property expressions are expanded
    /// </summary>
    internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => _cachedExpandedValue ??= _expandable
            ? state.ExpandIntoString(ValueText)
            : ValueText;

    /// <summary>
    /// If any expression nodes cache any state for the duration of evaluation,
    /// now's the time to clean it up
    /// </summary>
    internal override void ResetState()
    {
        _cachedExpandedValue = null;
        _shouldBeTreatedAsVisualStudioVersion = null;
    }

    /// <summary>
    /// Should this node be treated as an expansion of VisualStudioVersion, rather than
    /// its literal meaning?
    /// </summary>
    /// <remarks>
    /// Needed to provide a compat shim for numeric/version comparisons
    /// on MSBuildToolsVersion, which were fine when it was a number
    /// but now cause the project to throw InvalidProjectException when
    /// ToolsVersion is "Current". https://github.com/dotnet/msbuild/issues/4150
    /// </remarks>
    private bool ShouldBeTreatedAsVisualStudioVersion(ConditionEvaluator.IConditionEvaluationState state)
        => _shouldBeTreatedAsVisualStudioVersion ??= ComputeShouldBeTreatedAsVisualStudioVersion(state);

    private bool ComputeShouldBeTreatedAsVisualStudioVersion(ConditionEvaluator.IConditionEvaluationState state)
    {
        // A non-expandable string is a literal and can never be $(MSBuildToolsVersion).
        if (_expandable)
        {
            // Treat specially if the node would expand to "Current".
            // Do this check first, because if it's not (common) we can early-out and the next
            // expansion will be cheap because this will populate the cached expanded value.
            if (string.Equals(GetExpandedValue(state), MSBuildConstants.CurrentToolsVersion, StringComparison.Ordinal))
            {
                // and it is just an expansion of MSBuildToolsVersion
                return _value.Span.Equals("$(MSBuildToolsVersion)", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    internal override string DebuggerDisplay => $"\"{ValueText}\"";
}
