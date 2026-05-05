// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using static Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.TestData;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

public partial class ResolveAssemblyReferenceTestFixture : IDisposable
{
    internal readonly MockEngine.GetStringDelegate ResourceDelegate = AssemblyResources.GetString;

    // Performance checks.
    internal static Dictionary<string, int> uniqueFileExists = null;
    internal static Dictionary<string, int> uniqueGetAssemblyName = null;

    internal static bool useFrameworkFileExists = false;

    internal const string RedistList = """
        <FileList  Redist="Microsoft-Windows-CLRCoreComp.4.0" Name=".NET Framework 4" RuntimeVersion="4.0" ToolsVersion="12.0">
          <File AssemblyName="Accessibility" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="CustomMarshalers" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="ISymWrapper" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.Build" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.Build.Conversion.v4.0" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.Build.Engine" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.Build.Framework" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.Build.Tasks.v4.0" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.Build.Utilities.v4.0" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.CSharp" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.JScript" Version="10.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.VisualBasic" Version="10.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.VisualBasic.Compatibility" Version="10.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.VisualBasic.Compatibility.Data" Version="10.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.VisualC" Version="10.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="Microsoft.VisualC.STLCLR" Version="2.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="mscorlib" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="PresentationBuildTasks" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="PresentationCore" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="PresentationFramework.Aero" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="PresentationFramework.Classic" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="PresentationFramework" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="PresentationFramework.Luna" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="PresentationFramework.Royale" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="ReachFramework" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="sysglobl" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Activities" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Activities.Core.Presentation" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Activities.DurableInstancing" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Activities.Presentation" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.AddIn.Contract" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.AddIn" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ComponentModel.Composition" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ComponentModel.DataAnnotations" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Configuration" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Configuration.Install" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Core" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.DataSetExtensions" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.Entity.Design" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.Entity" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.Linq" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.OracleClient" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.Services.Client" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.Services.Design" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.Services" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Data.SqlXml" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Deployment" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Design" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Device" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.DirectoryServices.AccountManagement" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.DirectoryServices" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.DirectoryServices.Protocols" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Drawing.Design" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Drawing" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Dynamic" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.EnterpriseServices" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.IdentityModel" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.IdentityModel.Selectors" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.IO.Log" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Management" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Management.Instrumentation" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Messaging" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Net" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Numerics" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Printing" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Runtime.DurableInstancing" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Runtime.Caching" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Runtime.Remoting" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Runtime.Serialization" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Runtime.Serialization.Formatters.Soap" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Security" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ServiceModel.Activation" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ServiceModel.Activities" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ServiceModel.Channels" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ServiceModel.Discovery" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ServiceModel" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ServiceModel.Routing" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ServiceModel.Web" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.ServiceProcess" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Speech" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Transactions" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.Abstractions" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.ApplicationServices" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.DataVisualization.Design" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.DataVisualization" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.DynamicData.Design" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.DynamicData" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.Entity.Design" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.Entity" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.Extensions.Design" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.Extensions" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.Mobile" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.RegularExpressions" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.Routing" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Web.Services" Version="4.0.0.0" PublicKeyToken="b03f5f7f11d50a3a" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Windows.Forms.DataVisualization.Design" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Windows.Forms.DataVisualization" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Windows.Forms" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Windows.Input.Manipulations" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Windows.Presentation" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Workflow.Activities" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Workflow.ComponentModel" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Workflow.Runtime" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.WorkflowServices" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Xaml" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Xml" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="System.Xml.Linq" Version="4.0.0.0" PublicKeyToken="b77a5c561934e089" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="UIAutomationClient" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="UIAutomationClientsideProviders" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="UIAutomationProvider" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="UIAutomationTypes" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="WindowsBase" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="WindowsFormsIntegration" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
          <File AssemblyName="XamlBuildTask" Version="4.0.0.0" PublicKeyToken="31bf3856ad364e35" Culture="neutral" ProcessorArchitecture="MSIL" InGac="true" />
        </FileList>
        """;

    protected readonly ITestOutputHelper _output;

    private protected TestRARServices DefaultServices
        => field ??= ConfigureDefaultServices();

    public ResolveAssemblyReferenceTestFixture(ITestOutputHelper output)
    {
        Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", "1");

        _output = output;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", null);
    }

    private protected virtual TestRARServices ConfigureDefaultServices()
        => TestRARServices.CreateDefault();

    /// <summary>
    /// Search paths to use.
    /// </summary>
    private static readonly string[] s_defaultPaths =
    [
        "{RawFileName}",
        "{CandidateAssemblyFiles}",
        MyProjectPath,
        MyComponentsMiscPath,
        MyComponents10Path,
        MyComponents20Path,
        MyVersion20Path,
        @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
        "{AssemblyFolders}",
        "{HintPathFromItem}"
    ];

    /// <summary>
    /// Return the default search paths.
    /// </summary>
    /// <value></value>
    internal string[] DefaultPaths => s_defaultPaths;

    /// <summary>
    /// Start monitoring IO calls.
    /// </summary>
    internal void StartIOMonitoring()
    {
        // If tables are present then the corresponding IO function will do some monitoring.
        uniqueFileExists = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        uniqueGetAssemblyName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stop monitoring IO calls and assert if any unnecessary IO was used.
    /// </summary>
    /// <param name="ioThreshold">Maximum number of file existence checks per file</param>
    internal void StopIOMonitoringAndAssert_Minimal_IOUse(int ioThreshold = 1)
    {
        // Check for minimal IO in File.Exists.
        foreach (var entry in uniqueFileExists)
        {
            string path = (string)entry.Key;
            int count = (int)entry.Value;
            if (count > ioThreshold)
            {
                Assert.Fail($"File.Exists() was called {count} times with path {path}.");
            }
        }

        uniqueFileExists = null;
        uniqueGetAssemblyName = null;
    }

    /// <summary>
    /// Stop monitoring IO calls and assert if any IO was used.
    /// </summary>
    internal void StopIOMonitoringAndAssert_Zero_IOUse()
    {
        // Check for minimal IO in File.Exists.
        foreach (var entry in uniqueFileExists)
        {
            string path = (string)entry.Key;
            int count = (int)entry.Value;
            if (count > 0)
            {
                Assert.Fail($"File.Exists() was called {count} times with path {path}.");
            }
        }

        // Check for zero IO in GetAssemblyName.
        foreach (var entry in uniqueGetAssemblyName)
        {
            string path = (string)entry.Key;
            int count = (int)entry.Value;
            if (count > 0)
            {
                Assert.Fail($"GetAssemblyName() was called {count} times with path {path}.");
            }
        }

        uniqueFileExists = null;
        uniqueGetAssemblyName = null;
    }

    internal void StopIOMonitoring()
    {
        uniqueFileExists = null;
        uniqueGetAssemblyName = null;
    }

    /// <summary>
    /// Write out an appConfig file.
    /// Return the filename that was written.
    /// </summary>
    protected static string WriteAppConfig(string redirects, string appConfigNameSuffix = null)
    {
        string appConfigContents = $"""
            <configuration>
                <runtime>
                    {redirects}
                </runtime>
            </configuration>
            """;

        string appConfigFile = FileUtilities.GetTemporaryFileName() + appConfigNameSuffix;
        File.WriteAllText(appConfigFile, appConfigContents);
        return appConfigFile;
    }

    /// <summary>
    /// Determines whether the given item array has an item with the given spec.
    /// </summary>
    /// <param name="items">The item array.</param>
    /// <param name="spec">The spec to search for.</param>
    /// <returns>True if the spec was found.</returns>
    protected static bool ContainsItem(ITaskItem[] items, string spec)
    {
        foreach (ITaskItem item in items)
        {
            if (string.Equals(item.ItemSpec, spec, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [Flags]
    public enum RARSimulationMode
    {
        LoadProject = 1,
        BuildProject = 2,
        LoadAndBuildProject = LoadProject | BuildProject
    }

    /// <summary>
    /// Execute the task.
    /// </summary>
    /// <remarks>
    /// NOTE! This test is not in fact completely isolated from its environment: it is reading the real redist lists.
    /// </remarks>
    protected bool Execute(
        ResolveAssemblyReference task,
        RARSimulationMode rarSimulationMode = RARSimulationMode.LoadAndBuildProject)
        => Execute(task, DefaultServices, buildConsistencyCheck: true, rarSimulationMode);

    /// <summary>
    /// Execute the task with custom services.
    /// </summary>
    internal bool Execute(
        ResolveAssemblyReference task,
        TestRARServices services,
        RARSimulationMode rarSimulationMode = RARSimulationMode.LoadAndBuildProject)
        => Execute(task, services, buildConsistencyCheck: true, rarSimulationMode);

    /// <summary>
    /// Execute the task. Without confirming that the number of files resolved with and without find dependencies is identical.
    /// This is because profiles could cause the number of primary references to be different.
    /// </summary>
    protected bool Execute(
        ResolveAssemblyReference task,
        bool buildConsistencyCheck,
        RARSimulationMode rarSimulationMode = RARSimulationMode.LoadAndBuildProject)
        => Execute(task, DefaultServices, buildConsistencyCheck, rarSimulationMode);

    /// <summary>
    /// Execute the task with custom services and optional consistency check.
    /// </summary>
    internal static bool Execute(
        ResolveAssemblyReference task,
        TestRARServices services,
        bool buildConsistencyCheck,
        RARSimulationMode rarSimulationMode = RARSimulationMode.LoadAndBuildProject)
    {
        string tempPath = Path.GetTempPath();
        string redistListPath = Path.Combine(tempPath, $"{Guid.NewGuid()}.xml");
        string rarCacheFile = Path.Combine(tempPath, $"{Guid.NewGuid()}.RarCache");

        services = services.AddExistentFiles(rarCacheFile);

        bool succeeded = false;

        try
        {
            // Set the InstalledAssemblyTables parameter.
            if (task.InstalledAssemblyTables.Length == 0)
            {
                File.WriteAllText(redistListPath, RedistList);
                task.InstalledAssemblyTables = [new TaskItem(redistListPath)];
            }

            // First, run it in loading-a-project mode.
            if (rarSimulationMode.HasFlag(RARSimulationMode.LoadProject))
            {
                task.Silent = true;
                task.FindDependencies = false;
                task.FindSatellites = false;
                task.FindSerializationAssemblies = false;
                task.FindRelatedFiles = false;
                task.StateFile = null;
                task.Execute(services);

                // A few checks. These should always be true or it may be a perf issue for project load.
                ITaskItem[] loadModeResolvedFiles = [];
                if (task.ResolvedFiles != null)
                {
                    loadModeResolvedFiles = (ITaskItem[])task.ResolvedFiles.Clone();
                }

                Assert.Empty(task.ResolvedDependencyFiles);
                Assert.Empty(task.SatelliteFiles);
                Assert.Empty(task.RelatedFiles);
                Assert.Empty(task.SuggestedRedirects);
                Assert.Empty(task.FilesWritten);

                if (buildConsistencyCheck)
                {
                    // Some consistency checks between load mode and build mode.
                    Assert.Equal(loadModeResolvedFiles.Length, task.ResolvedFiles.Length);
                    for (int i = 0; i < loadModeResolvedFiles.Length; i++)
                    {
                        Assert.Equal(loadModeResolvedFiles[i].ItemSpec, task.ResolvedFiles[i].ItemSpec);
                        Assert.Equal(loadModeResolvedFiles[i].GetMetadata("CopyLocal"), task.ResolvedFiles[i].GetMetadata("CopyLocal"));
                        Assert.Equal(loadModeResolvedFiles[i].GetMetadata("ResolvedFrom"), task.ResolvedFiles[i].GetMetadata("ResolvedFrom"));
                    }
                }
            }

            // Now, run it in building-a-project mode.
            if (rarSimulationMode.HasFlag(RARSimulationMode.BuildProject))
            {
                MockEngine engine = (MockEngine)task.BuildEngine;
                engine.Warnings = 0;
                engine.Errors = 0;
                engine.Log = "";
                task.Silent = false;
                task.FindDependencies = true;
                task.FindSatellites = true;
                task.FindSerializationAssemblies = true;
                task.FindRelatedFiles = true;
                string cache = rarCacheFile;
                task.StateFile = cache;
                File.Delete(task.StateFile);
                succeeded = task.Execute(services);

                if (FileUtilities.FileExistsNoThrow(task.StateFile))
                {
                    ITaskItem fileWritten = Assert.Single(task.FilesWritten);
                    Assert.Equal(cache, fileWritten.ItemSpec);
                }

                File.Delete(task.StateFile);

                // Check attributes on resolve files.
                for (int i = 0; i < task.ResolvedFiles.Length; i++)
                {
                    // OriginalItemSpec attribute on resolved items is to support VS in figuring out which
                    // project file reference caused a particular resolved file.
                    string originalItemSpec = task.ResolvedFiles[i].GetMetadata("OriginalItemSpec");
                    Assert.True(ContainsItem(task.Assemblies, originalItemSpec) || ContainsItem(task.AssemblyFiles, originalItemSpec)); // "Expected to find OriginalItemSpec in Assemblies or AssemblyFiles task parameters"
                }
            }
        }
        finally
        {
            if (File.Exists(redistListPath))
            {
                FileUtilities.DeleteNoThrow(redistListPath);
            }

            if (File.Exists(rarCacheFile))
            {
                FileUtilities.DeleteNoThrow(rarCacheFile);
            }
        }

        return succeeded;
    }

    /// <summary>
    /// Helper method which allows tests to specify additional assembly search paths.
    /// </summary>
    protected void ExecuteRAROnItemsAndRedist(
        ResolveAssemblyReference task,
        MockEngine engine,
        ITaskItem[] items,
        string redistString,
        bool consistencyCheck)
        => ExecuteRAROnItemsAndRedist(task, engine, items, redistString, consistencyCheck, additionalSearchPaths: null);

    /// <summary>
    /// Helper method to get rid of some of the code duplication.
    /// </summary>
    protected void ExecuteRAROnItemsAndRedist(
        ResolveAssemblyReference task,
        MockEngine engine,
        ITaskItem[] items,
        string redistString,
        bool consistencyCheck,
        IEnumerable<string> additionalSearchPaths)
    {
        task.BuildEngine = engine;
        task.Assemblies = items;
        task.SearchPaths = [.. DefaultPaths, .. additionalSearchPaths ?? []];

        string redistFile = FileUtilities.GetTemporaryFileName();
        try
        {
            File.WriteAllText(redistFile, redistString);

            task.InstalledAssemblyTables = [new TaskItem(redistFile)];

            Execute(task, consistencyCheck);
        }
        finally
        {
            File.Delete(redistFile);
        }
    }
}
