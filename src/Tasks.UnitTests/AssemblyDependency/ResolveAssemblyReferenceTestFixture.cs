// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
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

        public ResolveAssemblyReferenceTestFixture(ITestOutputHelper output)
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", "1");

            _output = output;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", null);
        }

        protected static readonly string s_rootPathPrefix = NativeMethodsShared.IsWindows ? "C:\\" : Path.VolumeSeparatorChar.ToString();
        protected static readonly string s_myProjectPath = Path.Combine(s_rootPathPrefix, "MyProject");

        protected static readonly string s_myVersion20Path = Path.Combine(s_rootPathPrefix, "WINNT", "Microsoft.NET", "Framework", "v2.0.MyVersion");
        protected static readonly string s_myVersion40Path = Path.Combine(s_rootPathPrefix, "WINNT", "Microsoft.NET", "Framework", "v4.0.MyVersion");
        protected static readonly string s_myVersion90Path = Path.Combine(s_rootPathPrefix, "WINNT", "Microsoft.NET", "Framework", "v9.0.MyVersion");

        protected static readonly string s_myVersionPocket20Path = s_myVersion20Path + ".PocketPC";

        protected static readonly string s_myMissingAssemblyAbsPath = Path.Combine(s_rootPathPrefix, "MyProject", "MyMissingAssembly.dll");
        protected static readonly string s_myMissingAssemblyRelPath = Path.Combine("MyProject", "MyMissingAssembly.dll");
        protected static readonly string s_myPrivateAssemblyRelPath = Path.Combine("MyProject", "MyPrivateAssembly.exe");

        protected static readonly string s_frameworksPath = Path.Combine(s_rootPathPrefix, "Frameworks");

        protected static readonly string s_myComponents2RootPath = Path.Combine(s_rootPathPrefix, "MyComponents2");
        protected static readonly string s_myComponentsRootPath = Path.Combine(s_rootPathPrefix, "MyComponents");
        protected static readonly string s_myComponents10Path = Path.Combine(s_myComponentsRootPath, "1.0");
        protected static readonly string s_myComponents20Path = Path.Combine(s_myComponentsRootPath, "2.0");
        protected static readonly string s_myComponentsMiscPath = Path.Combine(s_myComponentsRootPath, "misc");

        protected static readonly string s_myComponentsV05Path = Path.Combine(s_myComponentsRootPath, "v0.5");
        protected static readonly string s_myComponentsV10Path = Path.Combine(s_myComponentsRootPath, "v1.0");
        protected static readonly string s_myComponentsV20Path = Path.Combine(s_myComponentsRootPath, "v2.0");
        protected static readonly string s_myComponentsV30Path = Path.Combine(s_myComponentsRootPath, "v3.0");

        protected static readonly string s_unifyMeDll_V05Path = Path.Combine(s_myComponentsV05Path, "UnifyMe.dll");
        protected static readonly string s_unifyMeDll_V10Path = Path.Combine(s_myComponentsV10Path, "UnifyMe.dll");
        protected static readonly string s_unifyMeDll_V20Path = Path.Combine(s_myComponentsV20Path, "UnifyMe.dll");
        protected static readonly string s_unifyMeDll_V30Path = Path.Combine(s_myComponentsV30Path, "UnifyMe.dll");

        protected static readonly string s_myComponents40ComponentPath = Path.Combine(s_myComponentsRootPath, "4.0Component");
        protected static readonly string s_40ComponentDependsOnOnlyv4AssembliesDllPath = Path.Combine(s_myComponents40ComponentPath, "DependsOnOnlyv4Assemblies.dll");

        protected static readonly string s_myLibrariesRootPath = Path.Combine(s_rootPathPrefix, "MyLibraries");
        protected static readonly string s_myLibraries_V1Path = Path.Combine(s_myLibrariesRootPath, "v1");
        protected static readonly string s_myLibraries_V2Path = Path.Combine(s_myLibrariesRootPath, "v2");
        protected static readonly string s_myLibraries_V1_EPath = Path.Combine(s_myLibraries_V1Path, "E");

        protected static readonly string s_myLibraries_ADllPath = Path.Combine(s_myLibrariesRootPath, "A.dll");
        protected static readonly string s_myLibraries_BDllPath = Path.Combine(s_myLibrariesRootPath, "B.dll");
        protected static readonly string s_myLibraries_CDllPath = Path.Combine(s_myLibrariesRootPath, "C.dll");
        protected static readonly string s_myLibraries_TDllPath = Path.Combine(s_myLibrariesRootPath, "T.dll");

        protected static readonly string s_myLibraries_V1_DDllPath = Path.Combine(s_myLibraries_V1Path, "D.dll");
        protected static readonly string s_myLibraries_V1_E_EDllPath = Path.Combine(s_myLibraries_V1_EPath, "E.dll");
        protected static readonly string s_myLibraries_V2_DDllPath = Path.Combine(s_myLibraries_V2Path, "D.dll");
        protected static readonly string s_myLibraries_V1_GDllPath = Path.Combine(s_myLibraries_V1Path, "G.dll");
        protected static readonly string s_myLibraries_V2_GDllPath = Path.Combine(s_myLibraries_V2Path, "G.dll");

        protected static readonly string s_regress454863_ADllPath = Path.Combine(s_rootPathPrefix, "Regress454863", "A.dll");
        protected static readonly string s_regress454863_BDllPath = Path.Combine(s_rootPathPrefix, "Regress454863", "B.dll");

        protected static readonly string s_regress444809RootPath = Path.Combine(s_rootPathPrefix, "Regress444809");
        protected static readonly string s_regress444809_ADllPath = Path.Combine(s_regress444809RootPath, "A.dll");
        protected static readonly string s_regress444809_BDllPath = Path.Combine(s_regress444809RootPath, "B.dll");
        protected static readonly string s_regress444809_CDllPath = Path.Combine(s_regress444809RootPath, "C.dll");
        protected static readonly string s_regress444809_DDllPath = Path.Combine(s_regress444809RootPath, "D.dll");

        protected static readonly string s_regress444809_V2RootPath = Path.Combine(s_regress444809RootPath, "v2");
        protected static readonly string s_regress444809_V2_ADllPath = Path.Combine(s_regress444809_V2RootPath, "A.dll");

        protected static readonly string s_regress442570_RootPath = Path.Combine(s_rootPathPrefix, "Regress442570");
        protected static readonly string s_regress442570_ADllPath = Path.Combine(s_regress442570_RootPath, "A.dll");
        protected static readonly string s_regress442570_BDllPath = Path.Combine(s_regress442570_RootPath, "B.dll");

        protected static readonly string s_myAppRootPath = Path.Combine(s_rootPathPrefix, "MyApp");
        protected static readonly string s_myApp_V05Path = Path.Combine(s_myAppRootPath, "v0.5");
        protected static readonly string s_myApp_V10Path = Path.Combine(s_myAppRootPath, "v1.0");
        protected static readonly string s_myApp_V20Path = Path.Combine(s_myAppRootPath, "v2.0");
        protected static readonly string s_myApp_V30Path = Path.Combine(s_myAppRootPath, "v3.0");

        protected static readonly string s_netstandardLibraryDllPath = Path.Combine(s_rootPathPrefix, "NetStandard", "netstandardlibrary.dll");
        protected static readonly string s_netstandardDllPath = Path.Combine(s_rootPathPrefix, "NetStandard", "netstandard.dll");

        protected static readonly string s_portableDllPath = Path.Combine(s_rootPathPrefix, "SystemRuntime", "Portable.dll");
        protected static readonly string s_systemRuntimeDllPath = Path.Combine(s_rootPathPrefix, "SystemRuntime", "System.Runtime.dll");

        protected static readonly string s_dependsOnNuGet_ADllPath = Path.Combine(s_rootPathPrefix, "DependsOnNuget", "A.dll");
        protected static readonly string s_dependsOnNuGet_NDllPath = Path.Combine(s_rootPathPrefix, "DependsOnNuget", "N.dll");
        protected static readonly string s_dependsOnNuGet_NExePath = Path.Combine(s_rootPathPrefix, "DependsOnNuget", "N.exe");
        protected static readonly string s_dependsOnNuGet_NWinMdPath = Path.Combine(s_rootPathPrefix, "DependsOnNuget", "N.winmd");

        protected static readonly string s_nugetCache_N_Lib_NDllPath = Path.Combine(s_rootPathPrefix, "NugetCache", "N", "lib", "N.dll");

        protected static readonly string s_assemblyFolder_RootPath = Path.Combine(s_rootPathPrefix, "AssemblyFolder");
        protected static readonly string s_assemblyFolder_SomeAssemblyDllPath = Path.Combine(s_assemblyFolder_RootPath, "SomeAssembly.dll");

        /// <summary>
        /// Search paths to use.
        /// </summary>
        private static readonly string[] s_defaultPaths =
        [
            "{RawFileName}",
            "{CandidateAssemblyFiles}",
            s_myProjectPath,
            s_myComponentsMiscPath,
            s_myComponents10Path,
            s_myComponents20Path,
            s_myVersion20Path,
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

        protected static List<string> s_existentFiles = new List<string>
        {
            Path.Combine(s_frameworksPath, "DependsOnFoo4Framework.dll"),
            Path.Combine(s_frameworksPath, "DependsOnFoo45Framework.dll"),
            Path.Combine(s_frameworksPath, "DependsOnFoo35Framework.dll"),
            Path.Combine(s_frameworksPath, "IndirectDependsOnFoo45Framework.dll"),
            Path.Combine(s_frameworksPath, "IndirectDependsOnFoo4Framework.dll"),
            Path.Combine(s_frameworksPath, "IndirectDependsOnFoo35Framework.dll"),
            Path.Combine(Path.GetTempPath(), @"RawFileNameRelative\System.Xml.dll"),
            Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles\System.Xml.dll"),
            Path.Combine(s_myVersion20Path, "System.Data.dll"),
            Path.Combine(s_myVersion20Path, "System.Xml.dll"),
            Path.Combine(s_myVersion20Path, "System.Xml.pdb"),
            Path.Combine(s_myVersion20Path, "System.Xml.xml"),
            Path.Combine(s_myVersion20Path, "en", "System.Xml.resources.dll"),
            Path.Combine(s_myVersion20Path, "en", "System.Xml.resources.pdb"),
            Path.Combine(s_myVersion20Path, "en", "System.Xml.resources.config"),
            Path.Combine(s_myVersion20Path, "xx", "System.Xml.resources.dll"),
            Path.Combine(s_myVersion20Path, "en-GB", "System.Xml.resources.dll"),
            Path.Combine(s_myVersion20Path, "en-GB", "System.Xml.resources.pdb"),
            Path.Combine(s_myVersion20Path, "en-GB", "System.Xml.resources.config"),
            Path.Combine(s_rootPathPrefix, s_myPrivateAssemblyRelPath),
            Path.Combine(s_myProjectPath, "MyCopyLocalAssembly.dll"),
            Path.Combine(s_myProjectPath, "MyDontCopyLocalAssembly.dll"),
            Path.Combine(s_myVersion20Path, "BadImage.dll"),            // An assembly that will give a BadImageFormatException from GetAssemblyName
            Path.Combine(s_myVersion20Path, "BadImage.pdb"),
            Path.Combine(s_myVersion20Path, "MyGacAssembly.dll"),
            Path.Combine(s_myVersion20Path, "MyGacAssembly.pdb"),
            Path.Combine(s_myVersion20Path, "xx", "MyGacAssembly.resources.dll"),
            Path.Combine(s_myVersion20Path, "System.dll"),
            Path.Combine(s_myVersion40Path, "System.dll"),
            Path.Combine(s_myVersion90Path, "System.dll"),
            Path.Combine(s_myVersion20Path, "mscorlib.dll"),
            Path.Combine(s_myVersionPocket20Path, "mscorlib.dll"),
            @"C:\myassemblies\My.Assembly.dll",
            Path.Combine(s_myProjectPath, "mscorlib.dll"),                           // This is an mscorlib.dll that has no metadata (i.e. GetAssemblyName returns null)
            Path.Combine(s_myProjectPath, "System.Data.dll"),                        // This is a System.Data.dll that has the wrong pkt, it shouldn't be matched.
            Path.Combine(s_myComponentsRootPath, "MyGrid.dll"),                      // A vendor component that we should find in the registry.
            @"C:\MyComponentsA\CustomComponent.dll",                                           // A vendor component that we should find in the registry.
            @"C:\MyComponentsB\CustomComponent.dll",                                           // A vendor component that we should find in the registry.
            @"C:\MyWinMDComponents7\MyGridWinMD.winmd",
            @"C:\MyWinMDComponents9\MyGridWinMD.winmd",
            @"C:\MyWinMDComponents\MyGridWinMD.winmd",
            @"C:\MyWinMDComponents2\MyGridWinMD.winmd",
            @"C:\MyWinMDComponentsA\CustomComponentWinMD.winmd",
            @"C:\MyWinMDComponentsB\CustomComponentWinMD.winmd",
            @"C:\MyWinMDComponentsVv1\MyGridWinMD2.winmd",
            @"C:\MyWinMDComponentsV1\MyGridWinMD3.winmd",
            @"C:\MyRawDropControls\MyRawDropControl.dll",                             // A control installed by VSREG under v2.0.x86chk
            @"C:\MyComponents\HKLM Components\MyHKLMControl.dll",                    // A vendor component that is installed under HKLM but not HKCU.
            @"C:\MyComponents\HKCU Components\MyHKLMandHKCUControl.dll",             // A vendor component that is installed under HKLM and HKCU.
            @"C:\MyComponents\HKLM Components\MyHKLMandHKCUControl.dll",             // A vendor component that is installed under HKLM and HKCU.
            @"C:\MyWinMDComponents\HKLM Components\MyHKLMControlWinMD.winmd",                    // A vendor component that is installed under HKLM but not HKCU.
            @"C:\MyWinMDComponents\HKCU Components\MyHKLMandHKCUControlWinMD.winmd",             // A vendor component that is installed under HKLM and HKCU.
            @"C:\MyWinMDComponents\HKLM Components\MyHKLMandHKCUControlWinMD.winmd",             // A vendor component that is installed under HKLM and HKCU.
            Path.Combine(s_myComponentsV30Path, "MyControlWithFutureTargetNDPVersion.dll"),         // The future version of a component.
            Path.Combine(s_myComponentsV20Path, "MyControlWithFutureTargetNDPVersion.dll"),         // The current version of a component.
            Path.Combine(s_myComponentsV10Path, "MyNDP1Control.dll"),                               // A control that only has an NDP 1.0 version
            Path.Combine(s_myComponentsV20Path, "MyControlWithPastTargetNDPVersion.dll"),           // The current version of a component.
            Path.Combine(s_myComponentsV10Path, "MyControlWithPastTargetNDPVersion.dll"),           // The past version of a component.
            @"C:\MyComponentServicePack\MyControlWithServicePack.dll",               // The service pack 1 version of the control
            @"C:\MyComponentBase\MyControlWithServicePack.dll",                      // The non-service pack version of the control.
            @"C:\MyComponentServicePack2\MyControlWithServicePack.dll",              // The service pack 1 version of the control
            Path.Combine(s_myVersionPocket20Path, "mscorlib.dll"),  // A devices mscorlib.
            s_myLibraries_ADllPath,
            @"c:\MyExecutableLibraries\A.exe",
            s_myLibraries_BDllPath,
            s_myLibraries_CDllPath,
            s_myLibraries_V1_DDllPath,
            s_myLibraries_V1_E_EDllPath,
            @"c:\RogueLibraries\v1\D.dll",
            s_myLibraries_V2_DDllPath,
            s_myLibraries_V1_GDllPath,
            s_myLibraries_V2_GDllPath,
            @"c:\MyStronglyNamed\A.dll",
            @"c:\MyWeaklyNamed\A.dll",
            @"c:\MyInaccessible\A.dll",
            @"c:\MyNameMismatch\Foo.dll",
            @"c:\MyEscapedName\=A=.dll",
            @"c:\MyEscapedName\__'ASP'dw0024ry.dll",
            Path.Combine(s_myAppRootPath, "DependsOnSimpleA.dll"),
            @"C:\Regress312873\a.dll",
            @"C:\Regress312873\b.dll",
            @"C:\Regress312873-2\a.dll",
            @"C:\Regress275161\a.dll",
            @"C:\Regress317975\a.dll",
            @"C:\Regress317975\b.dll",
            @"C:\Regress317975\v2\b.dll",
            @"c:\Regress313086\mscorlib.dll",
            @"c:\V1Control\MyDeviceControlAssembly.dll",
            @"c:\V1ControlSP1\MyDeviceControlAssembly.dll",
            @"C:\Regress339786\FolderA\a.dll",
            @"C:\Regress339786\FolderA\c.dll", // v1 of c
            @"C:\Regress339786\FolderB\b.dll",
            @"C:\Regress339786\FolderB\c.dll", // v2 of c
            @"c:\OldClrBug\MyFileLoadExceptionAssembly.dll",
            @"c:\OldClrBug\DependsMyFileLoadExceptionAssembly.dll",
            @"c:\Regress563286\DependsOnBadImage.dll",
            @"C:\Regress407623\CrystalReportsAssembly.dll",
            @"C:\Regress435487\microsoft.build.engine.dll",
            @"C:\Regress313747\Microsoft.Office.Interop.Excel.dll",
            @"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll",
            s_regress442570_ADllPath,
            s_regress442570_BDllPath,
            s_regress454863_ADllPath,
            s_regress454863_BDllPath,
            @"C:\Regress393931\A.metadata_dll",
            @"c:\Regress387218\A.dll",
            @"c:\Regress387218\B.dll",
            @"c:\Regress387218\v1\D.dll",
            @"c:\Regress387218\v2\D.dll",
            @"c:\Regress390219\A.dll",
            @"c:\Regress390219\B.dll",
            @"c:\Regress390219\v1\D.dll",
            @"c:\Regress390219\v2\D.dll",
            @"c:\Regress315619\A\MyAssembly.dll",
            @"c:\Regress315619\B\MyAssembly.dll",
            @"c:\SGenDependeicies\mycomponent.dll",
            @"c:\SGenDependeicies\mycomponent.XmlSerializers.dll",
            @"c:\SGenDependeicies\mycomponent2.dll",
            @"c:\SGenDependeicies\mycomponent2.XmlSerializers.dll",
            @"c:\Regress315619\A\MyAssembly.dll",
            @"c:\Regress315619\B\MyAssembly.dll",
            @"c:\MyRedist\MyRedistRootAssembly.dll",
            @"c:\MyRedist\MyOtherAssembly.dll",
            @"c:\MyRedist\MyThirdAssembly.dll",
            // ==[Related File Extensions Testing]================================================================================================
            s_assemblyFolder_SomeAssemblyDllPath,
            Path.Combine(s_assemblyFolder_RootPath, "SomeAssembly.pdb"),
            Path.Combine(s_assemblyFolder_RootPath, "SomeAssembly.xml"),
            Path.Combine(s_assemblyFolder_RootPath, "SomeAssembly.pri"),
            Path.Combine(s_assemblyFolder_RootPath, "SomeAssembly.licenses"),
            Path.Combine(s_assemblyFolder_RootPath, "SomeAssembly.config"),
            // ==[Related File Extensions Testing]================================================================================================

            // ==[Unification Testing]============================================================================================================
            // @"C:\MyComponents\v0.5\UnifyMe.dll",                                 // For unification testing, a version that doesn't exist.
            s_unifyMeDll_V10Path,
            s_unifyMeDll_V20Path,
            s_unifyMeDll_V30Path,
            // @"C:\MyComponents\v4.0\UnifyMe.dll",
            Path.Combine(s_myApp_V05Path, "DependsOnUnified.dll"),
            Path.Combine(s_myApp_V10Path, "DependsOnUnified.dll"),
            Path.Combine(s_myApp_V20Path, "DependsOnUnified.dll"),
            Path.Combine(s_myApp_V30Path, "DependsOnUnified.dll"),
            Path.Combine(s_myAppRootPath, "DependsOnWeaklyNamedUnified.dll"),
            Path.Combine(s_myApp_V10Path, "DependsOnEverettSystem.dll"),
            @"C:\Framework\Everett\System.dll",
            @"C:\Framework\Whidbey\System.dll",
            // ==[Unification Testing]============================================================================================================

            // ==[Test assemblies reference higher versions than the current target framework=====================================================
            Path.Combine(s_myComponentsMiscPath, "DependsOnOnlyv4Assemblies.dll"),  // Only depends on 4.0.0 assemblies
            Path.Combine(s_myComponentsMiscPath, "ReferenceVersion9.dll"), // Is in redist list and is a 9.0 assembly
            Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"), // Depends on 9.0 assemblies
            Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll"), // Depends on 9.0 assemblies
            Path.Combine(s_myComponents10Path, "DependsOn9.dll"), // Depends on 9.0 assemblies
            Path.Combine(s_myComponents20Path, "DependsOn9.dll"), // Depends on 9.0 assemblies
            s_regress444809_ADllPath,
            s_regress444809_V2_ADllPath,
            s_regress444809_BDllPath,
            s_regress444809_CDllPath,
            s_regress444809_DDllPath,
            s_40ComponentDependsOnOnlyv4AssembliesDllPath,
            @"C:\Regress714052\MSIL\a.dll",
            @"C:\Regress714052\X86\a.dll",
            @"C:\Regress714052\NONE\a.dll",
            @"C:\Regress714052\Mix\a.dll",
            @"C:\Regress714052\Mix\a.winmd",
            @"C:\Regress714052\MSIL\b.dll",
            @"C:\Regress714052\X86\b.dll",
            @"C:\Regress714052\NONE\b.dll",
            @"C:\Regress714052\Mix\b.dll",
            @"C:\Regress714052\Mix\b.winmd",

            Path.Combine(s_myComponentsRootPath, "V.dll"),
            Path.Combine(s_myComponents2RootPath, "W.dll"),
            Path.Combine(s_myComponentsRootPath, "X.dll"),
            Path.Combine(s_myComponentsRootPath, "X.pdb"),
            Path.Combine(s_myComponentsRootPath, "Y.dll"),
            Path.Combine(s_myComponentsRootPath, "Z.dll"),

            Path.Combine(s_myComponentsRootPath, "Microsoft.Build.dll"),
            Path.Combine(s_myComponentsRootPath, "DependsOnMSBuild12.dll"),

            // WinMD sample files
            @"C:\WinMD\v4\mscorlib.dll",  // Fake 4.0 mscorlib so we can actually resolve it for one of the tests. With a version of 4
            @"C:\WinMD\v255\mscorlib.dll",  // Fake 4.0 mscorlib so we can actually resolve it for one of the tests. With a version of 255
            @"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll",
            @"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll",
            @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeAndCLR.dll",
            @"C:\WinMD\SampleWindowsRuntimeAndOther.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeOnly.dll",
            @"C:\WinMD\SampleWindowsRuntimeOnly.pri",
            @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd",
            @"C:\WinMD\SampleClrOnly.Winmd",
            @"C:\WinMD\SampleBadWindowsRuntime.Winmd",
            @"C:\WinMD\WinMDWithVersion255.Winmd",
            @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.Winmd",
            @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll",
            @"C:\WinMDArchVerification\DependsOnAmd64.Winmd",
            @"C:\WinMDArchVerification\DependsOnAmd64.dll",
            @"C:\WinMDArchVerification\DependsOnArm.Winmd",
            @"C:\WinMDArchVerification\DependsOnArm.dll",
            @"C:\WinMDArchVerification\DependsOnArmv7.Winmd",
            @"C:\WinMDArchVerification\DependsOnArmv7.dll",
            @"C:\WinMDArchVerification\DependsOnX86.Winmd",
            @"C:\WinMDArchVerification\DependsOnX86.dll",
            @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.Winmd",
            @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.dll",
            @"C:\WinMDArchVerification\DependsOnIA64.Winmd",
            @"C:\WinMDArchVerification\DependsOnIA64.dll",
            @"C:\WinMDArchVerification\DependsOnUnknown.Winmd",
            @"C:\WinMDArchVerification\DependsOnUnknown.dll",
            @"C:\WinMDLib\LibWithWinmdAndNoDll.lib",
            @"C:\WinMDLib\LibWithWinmdAndNoDll.pri",
            @"C:\WinMDLib\LibWithWinmdAndNoDll.Winmd",
            @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd",
            @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd",
            @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd",
            @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd",
            @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll",
            @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll",
            @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll",
            @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll",
            @"C:\FakeSDK\References\Debug\X86\SDKReference.dll",
            @"C:\DirectoryContainsOnlyDll\a.dll",
            @"C:\DirectoryContainsdllAndWinmd\b.dll",
            @"C:\DirectoryContainsdllAndWinmd\c.winmd",
            @"C:\DirectoryContainstwoWinmd\a.winmd",
            @"C:\DirectoryContainstwoWinmd\c.winmd",
            s_systemRuntimeDllPath,
            s_portableDllPath,
            s_netstandardLibraryDllPath,
            s_netstandardDllPath,
            @"C:\SystemRuntime\Regular.dll",
            s_dependsOnNuGet_ADllPath,
            s_nugetCache_N_Lib_NDllPath,
            @"C:\DirectoryTest\A.dll",
            @"C:\DirectoryTest\B.dll",
        };

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

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <remarks>
        /// NOTE! This test is not in fact completely isolated from its environment: it is reading the real redist lists.
        /// </remarks>
        protected static bool Execute(
            ResolveAssemblyReference task,
            RARSimulationMode rarSimulationMode = RARSimulationMode.LoadAndBuildProject)
            => Execute(task, TestRARServices.Default, buildConsistencyCheck: true, rarSimulationMode);

        /// <summary>
        /// Execute the task with custom services.
        /// </summary>
        internal static bool Execute(
            ResolveAssemblyReference task,
            RARServices services,
            RARSimulationMode rarSimulationMode = RARSimulationMode.LoadAndBuildProject)
            => Execute(task, services, buildConsistencyCheck: true, rarSimulationMode);

        [Flags]
        public enum RARSimulationMode
        {
            LoadProject = 1,
            BuildProject = 2,
            LoadAndBuildProject = LoadProject | BuildProject
        }

        /// <summary>
        /// Execute the task. Without confirming that the number of files resolved with and without find dependencies is identical.
        /// This is because profiles could cause the number of primary references to be different.
        /// </summary>
        protected static bool Execute(
            ResolveAssemblyReference task,
            bool buildConsistencyCheck,
            RARSimulationMode rarSimulationMode = RARSimulationMode.LoadAndBuildProject)
            => Execute(task, TestRARServices.Default, buildConsistencyCheck, rarSimulationMode);

        /// <summary>
        /// Execute the task with custom services and optional consistency check.
        /// </summary>
        internal static bool Execute(
            ResolveAssemblyReference task,
            RARServices services,
            bool buildConsistencyCheck,
            RARSimulationMode rarSimulationMode = RARSimulationMode.LoadAndBuildProject)
        {
            string tempPath = Path.GetTempPath();
            string redistListPath = Path.Combine(tempPath, $"{Guid.NewGuid()}.xml");
            string rarCacheFile = Path.Combine(tempPath, $"{Guid.NewGuid()}.RarCache");

            s_existentFiles.Add(rarCacheFile);

            bool succeeded = false;

            try
            {
                // Set the InstalledAssemblyTables parameter.
                if (task.InstalledAssemblyTables.Length == 0)
                {
                    File.WriteAllText(redistListPath, RedistList);
                    task.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
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
                s_existentFiles.Remove(rarCacheFile);

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
}
