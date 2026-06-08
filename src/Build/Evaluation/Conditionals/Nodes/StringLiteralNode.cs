// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Node representing a string.
/// </summary>
/// <param name="value">The unexpanded string value.</param>
/// <param name="expandable">
///  Whether the string potentially has expandable content,
///  such as a property expression or escaped character.
/// </param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class StringLiteralNode(StringSegment value, bool expandable) : ExpressionNode
{
    private readonly StringSegment _value = value;
    private readonly bool _expandable = expandable;

    /// <summary>
    ///  The string value, lazily materialized from <see cref="_value"/>.
    /// </summary>
    private string? _valueText;

    private string? _cachedExpandedValue;
    private bool? _shouldBeTreatedAsVisualStudioVersion;

    private string ValueText => _valueText ??= _value.ToString();

    private static Version CurrentVisualStudioVersion
        => field ??= Version.Parse(MSBuildConstants.CurrentVisualStudioVersion);

    private static double? s_currentVisualStudioVersionAsDouble;

    private static double CurrentVisualStudioVersionAsDouble
        => s_currentVisualStudioVersionAsDouble ??= ConversionUtilities.ConvertDecimalOrHexToDouble(MSBuildConstants.CurrentVisualStudioVersion);

    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        => !_expandable
            ? ConversionUtilities.TryConvertStringToBool(_value, out result)
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
            return ConversionUtilities.TryConvertDecimalOrHexToDouble(_value.AsSpan(), out result);
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
            return Version.TryParse(_value.AsSpan(), out result);
        }
#endif

        return Version.TryParse(GetExpandedValue(state), out result);
    }

    /// <summary>
    ///  Returns true if this node evaluates to an empty string,
    ///  otherwise false.
    ///  It may be cheaper to determine whether an expression will evaluate
    ///  to empty than to fully evaluate it.
    ///  Implementations should cache the result so that calls after the first are free.
    /// </summary>
    public override bool EvaluatesToEmpty(ConditionEvaluator.IConditionEvaluationState state)
    {
        if (_cachedExpandedValue is null)
        {
            if (_expandable)
            {
                if (_value.Length == 0)
                {
                    _cachedExpandedValue = string.Empty;
                    return true;
                }

                // If the length is 1 or 2, it can't possibly be a property, item, or metadata, and it isn't empty.
                if (_value.Length is 1 or 2)
                {
                    _cachedExpandedValue = ValueText;
                    return false;
                }

                if (_value is not ['$' or '%' or '@', '(', .., ')'])
                {
                    // This isn't just a property, item, or metadata value, and it isn't empty.
                    return false;
                }

                if (!TryExpandIntoStringBreakEarly(ValueText, state, out string? expandedValue))
                {
                    // It broke early: we can't store the value, we just
                    // know it's non empty
                    return false;
                }

                // It didn't break early, the result is accurate,
                // so store it so the work isn't done again.
                _cachedExpandedValue = expandedValue;
            }
            else
            {
                _cachedExpandedValue = ValueText;
            }
        }

        return _cachedExpandedValue.Length == 0;

        // NoInlining prevents the JIT from pulling the expansion pipeline into callers,
        // which bloats the method with a large stack-zeroing prologue paid on every call.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool TryExpandIntoStringBreakEarly(string value, ConditionEvaluator.IConditionEvaluationState state, [NotNullWhen(true)] out string? result)
        {
            result = state.ExpandIntoStringBreakEarly(value);
            return result is not null;
        }
    }

    public override bool IsUnexpandedValueEmpty()
        => _value.Length == 0;

    public override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => ValueText;

    public override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => _cachedExpandedValue ??= _expandable
            ? state.ExpandIntoString(ValueText)
            : ValueText;

    public override void ResetState()
    {
        _cachedExpandedValue = null;
        _shouldBeTreatedAsVisualStudioVersion = null;
    }

    /// <summary>
    ///  Should this node be treated as an expansion of VisualStudioVersion, rather than
    ///  its literal meaning?
    /// </summary>
    /// <remarks>
    ///  Needed to provide a compat shim for numeric/version comparisons
    ///  on MSBuildToolsVersion, which were fine when it was a number
    ///  but now cause the project to throw InvalidProjectException when
    ///  ToolsVersion is "Current". https://github.com/dotnet/msbuild/issues/4150
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
                return _value.Equals("$(MSBuildToolsVersion)", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    public override string DebuggerDisplay => $"\"{ValueText}\"";
}
