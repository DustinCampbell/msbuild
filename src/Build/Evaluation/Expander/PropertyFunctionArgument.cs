// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Describes one parsed property-function argument.
/// </summary>
/// <param name="range">The argument's range within the parsed expression.</param>
/// <param name="requirements">The work required before the argument can be consumed.</param>
internal readonly struct PropertyFunctionArgument(StringSegmentRange range, FunctionArgumentRequirements requirements)
{
    /// <summary>
    ///  Gets the argument's range within the parsed expression.
    /// </summary>
    public StringSegmentRange Range { get; } = range;

    /// <summary>
    ///  Gets the work required before the argument can be consumed.
    /// </summary>
    public FunctionArgumentRequirements Requirements { get; } = requirements;

    /// <summary>
    ///  Classifies the materialization requirements of an argument source.
    /// </summary>
    /// <param name="argument">The normalized argument source.</param>
    /// <returns>
    ///  The work required before the argument can be consumed.
    /// </returns>
    public static FunctionArgumentRequirements GetRequirements(StringSegment argument)
    {
        FunctionArgumentRequirements requirements = FunctionArgumentRequirements.None;

        if (argument.Contains("$("))
        {
            requirements |= FunctionArgumentRequirements.ExpandProperties;
        }

        if (EscapingUtilities.ContainsEscapeSequence(argument))
        {
            requirements |= FunctionArgumentRequirements.Unescape;
        }

        return requirements;
    }
}
