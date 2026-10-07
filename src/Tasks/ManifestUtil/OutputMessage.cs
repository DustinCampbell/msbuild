// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Specifies the type of output message as either an error, warning, or informational.
    /// </summary>
    [ComVisible(false)]
    public enum OutputMessageType
    {
        /// <summary>
        /// Indicates an informational message.
        /// </summary>
        Info,
        /// <summary>
        /// Indicates a warning.
        /// </summary>
        Warning,
        /// <summary>
        /// Indicates an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Describes an error, warning, or informational output message for the manifest generator.
    /// </summary>
    [ComVisible(false)]
    public sealed class OutputMessage
    {
        private readonly string[] _arguments;

        internal OutputMessage(OutputMessageType type, ResourceString resource, string text, params string[] arguments)
        {
            Type = type;
            Resource = resource ?? throw new ArgumentNullException(nameof(resource));
            Name = resource.ResourceName;
            _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            Text = text;
        }

        internal ResourceString Resource { get; }

        /// <summary>
        /// Returns a string array of arguments for the message.
        /// </summary>
        /// <returns></returns>
        public string[] GetArguments() { return _arguments; }

        /// <summary>
        /// Specifies an identifier for the message.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Contains the text of the message.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Indicates whether the message is an error, warning, or informational message.
        /// </summary>
        public OutputMessageType Type { get; }
    }

    /// <summary>
    /// Provides a collection for output messages.
    /// </summary>
    [ComVisible(false)]
    public sealed class OutputMessageCollection : IEnumerable
    {
        private readonly List<OutputMessage> _list = new List<OutputMessage>();

        internal OutputMessageCollection()
        {
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the entry to get.</param>
        /// <returns>The file reference instance.</returns>
        public OutputMessage this[int index] => _list[index];

        internal void AddErrorMessage(ResourceString taskResource, params string[] arguments)
        {
            ++ErrorCount;
            string taskText = ResourceStringUtilities.FormatResourceStringIgnoreCodeAndKeyword(taskResource, arguments);
            _list.Add(new OutputMessage(OutputMessageType.Error, taskResource, taskText, arguments));
        }

        internal void AddWarningMessage(ResourceString taskResource, params string[] arguments)
        {
            ++WarningCount;
            string taskText = ResourceStringUtilities.FormatResourceStringIgnoreCodeAndKeyword(taskResource, arguments);
            _list.Add(new OutputMessage(OutputMessageType.Warning, taskResource, taskText, arguments));
        }

        /// <summary>
        /// Removes all objects from the collection.
        /// </summary>
        public void Clear()
        {
            _list.Clear();
            ErrorCount = 0;
            WarningCount = 0;
        }

        /// <summary>
        /// Gets the number of error messages in the collecction.
        /// </summary>
        public int ErrorCount { get; private set; }

        /// <summary>
        /// Returns an enumerator that can iterate through the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        internal bool LogTaskMessages(Task task)
        {
            foreach (OutputMessage message in _list)
            {
                switch (message.Type)
                {
                    case OutputMessageType.Warning:
                        task.Log.LogWarningWithCodeFromResource(message.Resource, message.GetArguments());
                        break;
                    case OutputMessageType.Error:
                        task.Log.LogErrorWithCodeFromResource(message.Resource, message.GetArguments());
                        break;
                }
            }
            return ErrorCount <= 0;
        }

        /// <summary>
        /// Gets the number of warning messages in the collecction.
        /// </summary>
        public int WarningCount { get; private set; }
    }
}
