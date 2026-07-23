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

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This exception is used to flag failures in application initialization, either due to invalid parameters on the command
    /// line, or because the application was invoked in an invalid context.
    /// </summary>
    /// <remarks>
    /// Unlike the CommandLineSwitchException, this exception is NOT thrown for syntax errors in switches.
    /// </remarks>
    [Serializable]
    internal sealed class InitializationException : Exception // CodeQL [SM02227] The dangerous method is called only in debug build. It's safe for release build.
    {
        /// <summary>
        /// This constructor initializes the exception message.
        /// </summary>
        /// <param name="message"></param>
        private InitializationException(string message)
            : base(message)
        {
            // do nothing
        }

        /// <summary>
        /// This constructor initializes the exception message and saves the switch that caused the initialization failure.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="invalidSwitch">Can be null.</param>
        private InitializationException(string message, string invalidSwitch)
            : this(message)
        {
            this.invalidSwitch = invalidSwitch;
        }

        /// <summary>
        /// Serialization constructor
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        private InitializationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ArgumentNullException.ThrowIfNull(info);

            invalidSwitch = info.GetString("invalidSwitch");
        }

        /// <summary>
        /// Gets the error message and the invalid switch, or only the error message if no invalid switch is set.
        /// </summary>
        public override string Message
            => invalidSwitch != null
                ? base.Message + Environment.NewLine + AssemblyResources.InvalidSwitchIndicator.FormatStripCode(invalidSwitch)
                : base.Message;

        // the invalid switch causing this exception (can be null)
        private string invalidSwitch;

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

            info.AddValue("invalidSwitch", invalidSwitch, typeof(string));
        }

        /// <summary>
        /// Throws the exception if the specified condition is not met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="messageResource"></param>
        internal static void VerifyThrow(bool condition, ResourceString messageResource)
            => VerifyThrow(condition, messageResource, invalidSwitch: null);

        /// <summary>
        /// Throws the exception if the specified condition is not met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="messageResource"></param>
        /// <param name="invalidSwitch"></param>
        internal static void VerifyThrow(bool condition, ResourceString messageResource, string invalidSwitch)
        {
            if (!condition)
            {
                Throw(messageResource, invalidSwitch, e: null, showStackTrace: false);
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
        /// Throws the exception using the given exception context.
        /// </summary>
        /// <param name="messageResource"></param>
        /// <param name="invalidSwitch"></param>
        /// <param name="e"></param>
        /// <param name="showStackTrace"></param>
        internal static void Throw(ResourceString messageResource, string invalidSwitch, Exception e, bool showStackTrace)
        {
            string errorMessage = showStackTrace && e != null
                ? messageResource.Text + Environment.NewLine + e.ToString()
                // the exception message can contain a format item i.e. "{0}" to hold the given exception's message
                : messageResource.Format(e?.Message ?? string.Empty);

            Throw(errorMessage, invalidSwitch);
        }

        /// <summary>
        /// Throws the exception using the given exception context and can include the logger name.
        /// </summary>
        internal static void Throw(ResourceString messageResource, string invalidSwitch, Exception e, bool showStackTrace, params object[] formatArgs)
        {
            // the exception message can contain a format item i.e.
            // "{0}" to hold the logger name
            // "{1}" to hold the given exception's message
            string errorMessage = messageResource.Format(formatArgs);

            if (showStackTrace && e != null)
            {
                errorMessage += Environment.NewLine + e.ToString();
            }

            Throw(errorMessage, invalidSwitch);
        }

        /// <summary>
        /// Throws the exception if the specified condition is not met.
        /// </summary>
        internal static void VerifyThrow(bool condition, ResourceString messageResource, string invalidSwitch, params object[] args)
        {
            if (!condition)
            {
                string errorMessage = messageResource.Format(args);

                Throw(errorMessage, invalidSwitch);
            }
        }

        /// <summary>
        /// Throws the exception using the given exception context.
        /// </summary>
        /// <param name="messageResourceName"></param>
        /// <param name="invalidSwitch"></param>
        /// <param name="e"></param>
        /// <param name="showStackTrace"></param>
        internal static void Throw(string message, string invalidSwitch)
        {
            Assumed.NotNull(message, "The string must exist.");
            throw new InitializationException(message, invalidSwitch);
        }
    }
}
