// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Offers a default string format for Error and Warning events
    /// </summary>
    internal static class EventArgsFormatting
    {
        /// <summary>
        /// Format the error event message and all the other event data into
        /// a single string.
        /// </summary>
        /// <param name="e">Error to format</param>
        /// <param name="showProjectFile"><code>true</code> to show the project file which issued the event, otherwise <code>false</code>.</param>
        /// <param name="projectConfigurationDescription">Properties to Print along with message</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildErrorEventArgs e, bool showProjectFile, string projectConfigurationDescription)
        {
            return FormatEventMessage("error", e.Subcategory, e.Message,
                            e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId, projectConfigurationDescription);
        }

        /// <summary>
        /// Format the warning message and all the other event data into a
        /// single string.
        /// </summary>
        /// <param name="e">Warning to format</param>
        /// <param name="showProjectFile"><code>true</code> to show the project file which issued the event, otherwise <code>false</code>.</param>
        /// <param name="projectConfigurationDescription">Properties to Print along with message</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildWarningEventArgs e, bool showProjectFile, string projectConfigurationDescription)
        {
            return FormatEventMessage("warning", e.Subcategory, e.Message,
                            e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId, projectConfigurationDescription);
        }

        /// <summary>
        /// Format the message and all the other event data into a
        /// single string.
        /// </summary>
        /// <param name="e">Message to format</param>
        /// <param name="showProjectFile"><code>true</code> to show the project file which issued the event, otherwise <code>false</code>.</param>
        /// <param name="projectConfigurationDescription">Properties to Print along with message</param>
        /// <param name="nonNullMessage">The complete message (including property name) for an environment-derived property</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildMessageEventArgs e, bool showProjectFile, string projectConfigurationDescription, string nonNullMessage = null)
        {
            return FormatEventMessage("message", e.Subcategory, nonNullMessage ?? e.Message,
                            e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId, projectConfigurationDescription);
        }

        /// <summary>
        /// Format the error event message and all the other event data into
        /// a single string.
        /// </summary>
        /// <param name="e">Error to format</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildErrorEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            // "error" should not be localized
            return FormatEventMessage("error", e.Subcategory, e.Message,
                            e.Code, e.File, null, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId, null);
        }

        /// <summary>
        /// Format the error event message and all the other event data into
        /// a single string.
        /// </summary>
        /// <param name="e">Error to format</param>
        /// <param name="showProjectFile"><code>true</code> to show the project file which issued the event, otherwise <code>false</code>.</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildErrorEventArgs e, bool showProjectFile)
        {
            ArgumentNullException.ThrowIfNull(e);

            // "error" should not be localized
            return FormatEventMessage("error", e.Subcategory, e.Message,
                e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId, null);
        }

        /// <summary>
        /// Format the warning message and all the other event data into a
        /// single string.
        /// </summary>
        /// <param name="e">Warning to format</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildWarningEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            // "warning" should not be localized
            return FormatEventMessage("warning", e.Subcategory, e.Message,
                e.Code, e.File, null, e.LineNumber, e.EndLineNumber,
                           e.ColumnNumber, e.EndColumnNumber, e.ThreadId, null);
        }

        /// <summary>
        /// Format the warning message and all the other event data into a
        /// single string.
        /// </summary>
        /// <param name="e">Warning to format</param>
        /// <param name="showProjectFile"><code>true</code> to show the project file which issued the event, otherwise <code>false</code>.</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildWarningEventArgs e, bool showProjectFile)
        {
            ArgumentNullException.ThrowIfNull(e);

            // "warning" should not be localized
            return FormatEventMessage("warning", e.Subcategory, e.Message,
                e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber,
                           e.ColumnNumber, e.EndColumnNumber, e.ThreadId, null);
        }

        /// <summary>
        /// Format the message and all the other event data into a
        /// single string.
        /// </summary>
        /// <param name="e">Message to format</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildMessageEventArgs e)
        {
            return FormatEventMessage(e, false);
        }

        /// <summary>
        /// Format the message and all the other event data into a
        /// single string.
        /// </summary>
        /// <param name="e">Message to format</param>
        /// <param name="showProjectFile">Show project file or not</param>
        /// <param name="nonNullMessage">For an EnvironmentVariableReadEventArgs, adds an explanatory note and the name of the variable.</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildMessageEventArgs e, bool showProjectFile, string nonNullMessage = null)
        {
            ArgumentNullException.ThrowIfNull(e);

            // "message" should not be localized
            return FormatEventMessage("message", e.Subcategory, nonNullMessage ?? e.Message,
                e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber, e.ColumnNumber, e.EndColumnNumber, e.ThreadId, null);
        }

        /// <summary>
        /// Format the event message and all the other event data into a
        /// single string.
        /// </summary>
        /// <param name="category">category ("error" or "warning")</param>
        /// <param name="subcategory">subcategory</param>
        /// <param name="message">event message</param>
        /// <param name="code">error or warning code number</param>
        /// <param name="file">file name</param>
        /// <param name="lineNumber">line number (0 if n/a)</param>
        /// <param name="endLineNumber">end line number (0 if n/a)</param>
        /// <param name="columnNumber">column number (0 if n/a)</param>
        /// <param name="endColumnNumber">end column number (0 if n/a)</param>
        /// <param name="threadId">thread id</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(
            string category,
            string subcategory,
            string message,
            string code,
            string file,
            int lineNumber,
            int endLineNumber,
            int columnNumber,
            int endColumnNumber,
            int threadId)
        {
            return FormatEventMessage(category, subcategory, message, code, file, null, lineNumber, endLineNumber, columnNumber, endColumnNumber, threadId, null);
        }

        /// <summary>
        /// Format the event message and all the other event data into a
        /// single string.
        /// </summary>
        /// <param name="category">category ("error" or "warning")</param>
        /// <param name="subcategory">subcategory</param>
        /// <param name="message">event message</param>
        /// <param name="code">error or warning code number</param>
        /// <param name="file">file name</param>
        /// <param name="projectFile">the project file name</param>
        /// <param name="lineNumber">line number (0 if n/a)</param>
        /// <param name="endLineNumber">end line number (0 if n/a)</param>
        /// <param name="columnNumber">column number (0 if n/a)</param>
        /// <param name="endColumnNumber">end column number (0 if n/a)</param>
        /// <param name="threadId">thread id</param>
        /// <param name="logOutputProperties">log output properties</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(
            string category,
            string subcategory,
            string message,
            string code,
            string file,
            string projectFile,
            int lineNumber,
            int endLineNumber,
            int columnNumber,
            int endColumnNumber,
            int threadId,
            string logOutputProperties)
        {
            // Build the formatted message into a pooled StringBuilder. See AppendEventMessage for details.
            StringBuilder formattedMessage = Microsoft.Build.Framework.StringBuilderCache.Acquire(51);
            AppendEventMessage(
                formattedMessage, category, subcategory, message, code, file, projectFile,
                lineNumber, endLineNumber, columnNumber, endColumnNumber, threadId, logOutputProperties);
            return Microsoft.Build.Framework.StringBuilderCache.GetStringAndRelease(formattedMessage);
        }

        /// <summary>
        /// Appends the formatted <see cref="BuildErrorEventArgs"/> message directly into <paramref name="sb"/>,
        /// so a caller that already has a pooled builder never has to materialize the message as a string.
        /// </summary>
        internal static void AppendEventMessage(StringBuilder sb, BuildErrorEventArgs e, bool showProjectFile, string projectConfigurationDescription)
        {
            AppendEventMessage(sb, "error", e.Subcategory, e.Message,
                            e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId, projectConfigurationDescription);
        }

        /// <summary>
        /// Appends the formatted <see cref="BuildWarningEventArgs"/> message directly into <paramref name="sb"/>,
        /// so a caller that already has a pooled builder never has to materialize the message as a string.
        /// </summary>
        internal static void AppendEventMessage(StringBuilder sb, BuildWarningEventArgs e, bool showProjectFile, string projectConfigurationDescription)
        {
            AppendEventMessage(sb, "warning", e.Subcategory, e.Message,
                            e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId, projectConfigurationDescription);
        }

        /// <summary>
        /// Appends the formatted <see cref="BuildMessageEventArgs"/> message directly into <paramref name="sb"/>,
        /// so a caller that already has a pooled builder never has to materialize the message as a string.
        /// </summary>
        internal static void AppendEventMessage(StringBuilder sb, BuildMessageEventArgs e, bool showProjectFile, string projectConfigurationDescription, string nonNullMessage = null)
        {
            AppendEventMessage(sb, "message", e.Subcategory, nonNullMessage ?? e.Message,
                            e.Code, e.File, showProjectFile ? e.ProjectFile : null, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId, projectConfigurationDescription);
        }

        /// <summary>
        /// Appends the formatted event message and all the other event data into <paramref name="formattedMessage"/>.
        /// </summary>
        /// <remarks>
        /// The formatted message is built directly rather than first constructing a positional format template
        /// string and running it through StringBuilder.AppendFormat. The old approach allocated the throwaway
        /// template string, an argument object[], and boxed all five integer arguments on every call; emitting
        /// the pieces in place avoids all of that (and is allocation-free for the structural parts on .NET).
        /// </remarks>
        internal static void AppendEventMessage(
            StringBuilder formattedMessage,
            string category,
            string subcategory,
            string message,
            string code,
            string file,
            string projectFile,
            int lineNumber,
            int endLineNumber,
            int columnNumber,
            int endColumnNumber,
            int threadId,
            string logOutputProperties)
        {
            // A null message is allowed and is to be treated as a blank line.
            string messageText = message ?? string.Empty;

            // Emit each line of the message with the full location/category/code prefix and project-file suffix.
            // "\r\n" and "\n" are treated as line breaks (matching the former SplitStringOnNewLines), and each
            // line is appended as a substring so no split array or per-line substrings are allocated.
            int lineStart = 0;
            bool firstLine = true;
            while (true)
            {
                int newlineIndex = messageText.IndexOf('\n', lineStart);
                int lineEnd = newlineIndex < 0 ? messageText.Length : newlineIndex;
                int lineLength = lineEnd - lineStart;

                // A '\r' immediately preceding '\n' is part of the "\r\n" break, not line content.
                if (newlineIndex >= 0 && lineLength > 0 && messageText[lineEnd - 1] == '\r')
                {
                    lineLength--;
                }

                if (!firstLine)
                {
                    formattedMessage.AppendLine();
                }

                AppendFormattedLine(
                    formattedMessage, category, subcategory, code, file, projectFile,
                    lineNumber, endLineNumber, columnNumber, endColumnNumber,
                    logOutputProperties, messageText, lineStart, lineLength);

                firstLine = false;

                if (newlineIndex < 0)
                {
                    break;
                }

                lineStart = newlineIndex + 1;
            }
        }

        /// <summary>
        /// Appends a single formatted line (location prefix, subcategory, category, code, message text, and
        /// project-file suffix) to <paramref name="sb"/>, reproducing the layout the former positional format
        /// template produced.
        /// </summary>
        private static void AppendFormattedLine(
            StringBuilder sb,
            string category,
            string subcategory,
            string code,
            string file,
            string projectFile,
            int lineNumber,
            int endLineNumber,
            int columnNumber,
            int endColumnNumber,
            string logOutputProperties,
            string messageText,
            int lineStart,
            int lineLength)
        {
            // File and location: "MSBUILD : " when there is no file, otherwise the file followed by an
            // optional (line[,column][-end]) span, matching the original template branches exactly.
            if (string.IsNullOrEmpty(file))
            {
                sb.Append("MSBUILD : ");    // Should not be localized.
            }
            else
            {
                sb.Append(file);

                if (lineNumber == 0)
                {
                    sb.Append(" : ");
                }
                else if (columnNumber == 0)
                {
                    if (endLineNumber == 0)
                    {
                        sb.Append('(').Append(lineNumber).Append("): ");
                    }
                    else
                    {
                        sb.Append('(').Append(lineNumber).Append('-').Append(endLineNumber).Append("): ");
                    }
                }
                else
                {
                    if (endLineNumber == 0)
                    {
                        if (endColumnNumber == 0)
                        {
                            sb.Append('(').Append(lineNumber).Append(',').Append(columnNumber).Append("): ");
                        }
                        else
                        {
                            sb.Append('(').Append(lineNumber).Append(',').Append(columnNumber).Append('-').Append(endColumnNumber).Append("): ");
                        }
                    }
                    else
                    {
                        if (endColumnNumber == 0)
                        {
                            sb.Append('(').Append(lineNumber).Append('-').Append(endLineNumber).Append(',').Append(columnNumber).Append("): ");
                        }
                        else
                        {
                            sb.Append('(').Append(lineNumber).Append(',').Append(columnNumber).Append(',').Append(endLineNumber).Append(',').Append(endColumnNumber).Append("): ");
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(subcategory))
            {
                sb.Append(subcategory).Append(' ');
            }

            // The category as a string (should not be localized).
            sb.Append(category).Append(' ');

            // Put a code in, if available and necessary.
            if (code == null)
            {
                sb.Append(": ");
            }
            else
            {
                sb.Append(code).Append(": ");
            }

            // The message text for this line (empty when the overall message was null or blank).
            if (lineLength > 0)
            {
                sb.Append(messageText, lineStart, lineLength);
            }

            // If the project file was specified, tack that (and any additional output properties) onto the end.
            if (projectFile != null)
            {
                if (!string.Equals(projectFile, file))
                {
                    if (logOutputProperties?.Length > 0)
                    {
                        sb.Append(" [").Append(projectFile).Append("::").Append(logOutputProperties).Append(']');
                    }
                    else
                    {
                        sb.Append(" [").Append(projectFile).Append(']');
                    }
                }
                else if (logOutputProperties?.Length > 0)
                {
                    // If the file location of the error _was_ the project file, append only the
                    // additional output properties.
                    sb.Append(" [").Append(logOutputProperties).Append(']');
                }
            }
        }
    }
}
