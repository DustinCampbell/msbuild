// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Node representing a string.
/// </summary>
internal sealed class StringExpressionNode : OperandExpressionNode
{
    private static Version CurrentVisualStudioVersion
        => field ??= Version.Parse(MSBuildConstants.CurrentVisualStudioVersion);

    private static double? s_currentVisualStudioVersionNumber;

    private static double CurrentVisualStudioVersionNumber
        => s_currentVisualStudioVersionNumber ??= ConversionUtilities.ConvertDecimalOrHexToDouble(MSBuildConstants.CurrentVisualStudioVersion);

    private bool? _shouldBeTreatedAsVisualStudioVersion;

    public string Value { get; }

    /// <summary>
    /// Whether the string potentially has expandable content,
    /// such as a property expression or escaped character.
    /// </summary>
    public bool Expandable { get; }

    private string? _cachedExpandedValue;

    internal StringExpressionNode(ReadOnlyMemory<char> value, bool expandable)
    {
        Value = value.ToString();
        Expandable = expandable;
    }

    internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        => ConversionUtilities.TryConvertStringToBool(GetExpandedValue(state), out result);

    internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result)
    {
        if (ShouldBeTreatedAsVisualStudioVersion(state))
        {
            result = CurrentVisualStudioVersionNumber;
            return true;
        }

        return ConversionUtilities.TryConvertDecimalOrHexToDouble(GetExpandedValue(state), out result);
    }

    internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, [NotNullWhen(true)] out Version? result)
    {
        if (ShouldBeTreatedAsVisualStudioVersion(state))
        {
            result = CurrentVisualStudioVersion;
            return true;
        }

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
        if (_cachedExpandedValue == null)
        {
            if (Expandable)
            {
                switch (Value.Length)
                {
                    case 0:
                        _cachedExpandedValue = string.Empty;
                        return true;

                    // If the length is 1 or 2, it can't possibly be a property, item, or metadata, and it isn't empty.
                    case 1:
                    case 2:
                        _cachedExpandedValue = Value;
                        return false;

                    default:
                        if (Value is not ['$' or '%' or '@', '(', .., ')'])
                        {
                            // This isn't just a property, item, or metadata value, and it isn't empty.
                            return false;
                        }

                        break;
                }

                string expandBreakEarly = state.ExpandIntoStringBreakEarly(Value);

                if (expandBreakEarly == null)
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
                _cachedExpandedValue = Value;
            }
        }

        return _cachedExpandedValue.Length == 0;
    }

    /// <inheritdoc cref="GenericExpressionNode"/>
    internal override bool IsUnexpandedValueEmpty()
        => string.IsNullOrEmpty(Value);

    /// <summary>
    /// Value before any item and property expressions are expanded.
    /// </summary>
    internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => Value;

    /// <summary>
    /// Value after any item and property expressions are expanded.
    /// </summary>
    internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => _cachedExpandedValue ??= Expandable
            ? state.ExpandIntoString(Value)
            : Value;

    /// <summary>
    /// If any expression nodes cache any state for the duration of evaluation,
    /// now's the time to clean it up.
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
    {
        return _shouldBeTreatedAsVisualStudioVersion ??= ShouldBeTreatedAsVisualStudioVersionCore();

        bool ShouldBeTreatedAsVisualStudioVersionCore()
            // Treat specially if the node would expand to "Current".
            // Do this check first, because if it's not (common) we can early-out and the next
            // expansion will be cheap because this will populate the cached expanded value.
            => string.Equals(GetExpandedValue(state), MSBuildConstants.CurrentToolsVersion, StringComparison.Ordinal)
                // and the original value is just an expansion of MSBuildToolsVersion
                && string.Equals(Value, "$(MSBuildToolsVersion)", StringComparison.OrdinalIgnoreCase);
    }

    internal override string GetDebuggerDisplay()
        => $"\"{Value}\"";
}
