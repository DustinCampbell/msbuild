// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Expansion;
using Xunit;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Identifies a test that runs only when the modern expander is selected.
/// </summary>
public sealed class ModernExpanderOnlyFactAttribute : FactAttribute
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="ModernExpanderOnlyFactAttribute"/> class.
    /// </summary>
    /// <param name="sourceFilePath">The source file path supplied by the compiler.</param>
    /// <param name="sourceLineNumber">The source line number supplied by the compiler.</param>
    public ModernExpanderOnlyFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (ExpanderFactory.UseLegacyExpander)
        {
            Skip = "This test requires the modern expander.";
        }
    }
}

/// <summary>
///  Identifies a test that runs only when <c>LegacyExpander</c> is selected.
/// </summary>
public sealed class LegacyExpanderOnlyFactAttribute : FactAttribute
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="LegacyExpanderOnlyFactAttribute"/> class.
    /// </summary>
    /// <param name="sourceFilePath">The source file path supplied by the compiler.</param>
    /// <param name="sourceLineNumber">The source line number supplied by the compiler.</param>
    public LegacyExpanderOnlyFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!ExpanderFactory.UseLegacyExpander)
        {
            Skip = "This test requires LegacyExpander.";
        }
    }
}
