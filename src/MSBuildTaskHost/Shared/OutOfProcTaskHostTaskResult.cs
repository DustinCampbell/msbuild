// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Build.BackEnd;

#nullable disable

namespace Microsoft.Build.Shared;

/// <summary>
/// A result of executing a target or task.
/// </summary>
internal sealed class OutOfProcTaskHostTaskResult
{
    internal OutOfProcTaskHostTaskResult(TaskCompleteType result)
        : this(result, finalParams: null, taskException: null, exceptionMessage: null, exceptionMessageArgs: null)
    {
    }

    internal OutOfProcTaskHostTaskResult(TaskCompleteType result, IDictionary<string, object> finalParams)
        : this(result, finalParams, taskException: null, exceptionMessage: null, exceptionMessageArgs: null)
    {
    }

    internal OutOfProcTaskHostTaskResult(TaskCompleteType result, Exception taskException)
        : this(result, taskException, exceptionMessage: null, exceptionMessageArgs: null)
    {
    }

    internal OutOfProcTaskHostTaskResult(TaskCompleteType result, Exception taskException, string exceptionMessage, string[] exceptionMessageArgs)
        : this(result, finalParams: null, taskException, exceptionMessage, exceptionMessageArgs)
    {
    }

    internal OutOfProcTaskHostTaskResult(
        TaskCompleteType result,
        IDictionary<string, object> finalParams,
        Exception taskException,
        string exceptionMessage,
        string[] exceptionMessageArgs)
    {
        // If we're returning a crashing result, we should always also be returning the exception that caused the crash, although
        // we may not always be returning an accompanying message.
        if (result is TaskCompleteType.CrashedDuringInitialization
                   or TaskCompleteType.CrashedDuringExecution
                   or TaskCompleteType.CrashedAfterExecution)
        {
            ErrorUtilities.VerifyThrowInternalNull(taskException);
        }

        if (exceptionMessage != null)
        {
            ErrorUtilities.VerifyThrow(
                    result is TaskCompleteType.CrashedDuringInitialization
                           or TaskCompleteType.CrashedDuringExecution
                           or TaskCompleteType.CrashedAfterExecution,
                    "If we have an exception message, the result type should be 'crashed' of some variety.");
        }

        if (exceptionMessageArgs?.Length > 0)
        {
            ErrorUtilities.VerifyThrow(exceptionMessage != null, "If we have message args, we need a message.");
        }

        Result = result;
        FinalParameterValues = finalParams;
        TaskException = taskException;
        ExceptionMessage = exceptionMessage;
        ExceptionMessageArgs = exceptionMessageArgs;
    }

    /// <summary>
    /// The overall result of the task execution.
    /// </summary>
    public TaskCompleteType Result
    {
        get;
        private set;
    }

    /// <summary>
    /// Dictionary of the final values of the task parameters.
    /// </summary>
    public IDictionary<string, object> FinalParameterValues { get; private set; }

    /// <summary>
    /// The exception thrown by the task during initialization or execution, if any.
    /// </summary>
    public Exception TaskException { get; private set; }

    /// <summary>
    /// The name of the resource representing the message to be logged along with the above exception.
    /// </summary>
    public string ExceptionMessage { get; private set; }

    /// <summary>
    /// The arguments to be used when formatting ExceptionMessage.
    /// </summary>
    public string[] ExceptionMessageArgs { get; private set; }
}
