// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Describes one invocation within a property-function expression.
/// </summary>
internal readonly struct PropertyFunctionInvocation(
    StringSegment text,
    ReceiverKind receiverKind,
    StringSegmentRange receiver,
    MemberKind memberKind,
    StringSegmentRange memberName,
    OneOrMany<PropertyFunctionArgument> arguments)
{
    private readonly StringSegmentRange _receiver = receiver;
    private readonly StringSegmentRange _memberName = memberName;
    private readonly OneOrMany<PropertyFunctionArgument> _arguments = arguments;

    /// <summary>
    ///  Gets the source text for this invocation.
    /// </summary>
    public StringSegment Text { get; } = text;

    /// <summary>
    ///  Gets how binding obtains the receiver.
    /// </summary>
    public ReceiverKind ReceiverKind { get; } = receiverKind;

    /// <summary>
    ///  Gets the static type name or MSBuild property name, or a null segment for a chained receiver.
    /// </summary>
    public StringSegment Receiver
        => _receiver.ToSegment(Text.Buffer);

    /// <summary>
    ///  Gets the member-access syntax.
    /// </summary>
    public MemberKind MemberKind { get; } = memberKind;

    /// <summary>
    ///  Gets the CLR member name, or an empty segment for an indexer.
    /// </summary>
    public StringSegment MemberName
        => _memberName.ToSegment(Text.Buffer);

    /// <summary>
    ///  Gets the member arguments.
    /// </summary>
    public ArgumentList Arguments
        => new(Text.Buffer, _arguments);
}
