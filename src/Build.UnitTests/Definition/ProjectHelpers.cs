// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace Microsoft.Build.UnitTests.BackEnd;

/// <summary>
///  Contains helper methods for creating projects for testing.
/// </summary>
internal static class ProjectHelpers
{
    /// <summary>
    ///  Creates a project instance with a single empty target named 'foo'.
    /// </summary>
    /// <returns>
    ///  A project instance.
    /// </returns>
    internal static ProjectInstance CreateEmptyProjectInstance()
    {
        const string Content = """
            <Project>
                <Target Name='foo'/>
            </Project>
            """;

        using var reader = XmlReader.Create(new StringReader(Content));
        var project = new Project(
            reader,
            globalProperties: null,
            toolsVersion: null,
            subToolsetVersion: null,
            ProjectCollection.GlobalProjectCollection,
            loadSettings: default);

        return project.CreateProjectInstance();
    }
}
