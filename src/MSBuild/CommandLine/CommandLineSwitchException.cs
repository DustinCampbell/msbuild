// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

using Microsoft.Build.Framework.Utilities;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.CommandLine.Experimental
{
    /// <summary>
    /// This exception is used to flag (syntax) errors in command line switches passed to the application.
    /// </summary>
    [Serializable]
    internal sealed class CommandLineSwitchException : Exception // CodeQL [SM02227] The dangerous method is called only in debug build. It's safe for release build.
    {
        /// <summary>
        /// This constructor initializes the exception message.
        /// </summary>
        /// <param name="message"></param>
        private CommandLineSwitchException(string message)
            : base(message)
        {
            // do nothing
        }

        /// <summary>
        /// This constructor initializes the exception message and saves the command line argument containing the switch error.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="commandLineArg"></param>
        private CommandLineSwitchException(string message, string commandLineArg)
            : this(message)
        {
            this.commandLineArg = commandLineArg;
        }

        /// <summary>
        /// Serialization constructor
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        private CommandLineSwitchException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ArgumentNullException.ThrowIfNull(info);

            commandLineArg = info.GetString("commandLineArg");
        }

        /// <summary>
        /// Gets the error message and the invalid switch, or only the error message if no invalid switch is set.
        /// </summary>
        public override string Message
            => commandLineArg == null
                ? base.Message
                : base.Message + Environment.NewLine + AssemblyResources.InvalidSwitchIndicator.FormatStripCode(commandLineArg);

        /// <summary>
        /// Gets the invalid switch that caused the exception.
        /// </summary>
        /// <value>Can be null.</value>
        internal string CommandLineArg => commandLineArg;

        // the invalid switch causing this exception
        private string commandLineArg;

        /// <summary>
        /// Serialize the contents of the class.
        /// </summary>
#if FEATURE_SECURITY_PERMISSIONS
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
#endif
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("commandLineArg", commandLineArg, typeof(string));
        }

        /// <summary>
        /// Throws the exception if the specified condition is not met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="messageResource"></param>
        /// <param name="commandLineArg"></param>
        internal static void VerifyThrow(bool condition, ResourceString messageResource, string commandLineArg)
        {
            if (!condition)
            {
                Throw(messageResource, commandLineArg);
            }
#if DEBUG
            else
            {
                // Force ResourceString.Text to verify that the resource string exists.
                _ = messageResource.Text;
            }
#endif
        }

        /// <summary>
        /// Throws the exception using the given message and the command line argument containing the switch error.
        /// </summary>
        /// <param name="messageResource"></param>
        /// <param name="commandLineArg"></param>
        internal static void Throw(ResourceString messageResource, string commandLineArg)
            => throw new CommandLineSwitchException(messageResource.TextWithoutCode, commandLineArg);

        /// <summary>
        /// Throws the exception using the given message and the command line argument containing the switch error.
        /// </summary>
        /// <param name="messageResource"></param>
        /// <param name="messageArgs"></param>
        internal static void Throw(ResourceString messageResource, string commandLineArg, params string[] messageArgs)
        {
            string errorMessage = messageResource.FormatStripCode(messageArgs);

            throw new CommandLineSwitchException(errorMessage, commandLineArg);
        }
    }
}
