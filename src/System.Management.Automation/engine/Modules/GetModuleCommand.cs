// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.Management.Infrastructure;
using Dbg = System.Management.Automation.Diagnostics;

//
// Now define the set of commands for manipulating modules.
//

namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Implements a cmdlet that gets the list of loaded modules...
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Module", DefaultParameterSetName = ParameterSet_Loaded,
        HelpUri = "https://go.microsoft.com/fwlink/?LinkID=141552")]
    [OutputType(typeof(PSModuleInfo))]
    public sealed class GetModuleCommand : ModuleCmdletBase, IDisposable
    {
        #region Cmdlet parameters

        private const string ParameterSet_Loaded = "Loaded";
        private const string ParameterSet_AvailableLocally = "Available";
        private const string ParameterSet_AvailableInPsrpSession = "PsSession";
        private const string ParameterSet_AvailableInCimSession = "CimSession";

        /// <summary>
        /// This parameter specifies the current pipeline object
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_Loaded, ValueFromPipeline = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSet_AvailableLocally, ValueFromPipeline = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInPsrpSession, ValueFromPipeline = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInCimSession, ValueFromPipeline = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays",
            Justification = "Cmdlets use arrays for parameters.")]
        public string[] Name { get; set; }

        /// <summary>
        /// This parameter specifies the current pipeline object
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_Loaded, ValueFromPipelineByPropertyName = true)]
        [Parameter(ParameterSetName = ParameterSet_AvailableLocally, ValueFromPipelineByPropertyName = true)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInPsrpSession, ValueFromPipelineByPropertyName = true)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInCimSession, ValueFromPipelineByPropertyName = true)]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays",
            Justification = "Cmdlets use arrays for parameters.")]
        public ModuleSpecification[] FullyQualifiedName { get; set; }

        /// <summary>
        /// If specified, all loaded modules should be returned, otherwise only the visible
        /// modules should be returned.
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_Loaded)]
        [Parameter(ParameterSetName = ParameterSet_AvailableLocally)]
        public SwitchParameter All { get; set; }

        /// <summary>
        /// If specified, then Get-Module will return the set of available modules...
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_AvailableLocally, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInPsrpSession)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInCimSession)]
        public SwitchParameter ListAvailable { get; set; }

        /// <summary>
        /// If specified, then Get-Module will return the set of available modules which supports the specified PowerShell edition...
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_AvailableLocally)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInPsrpSession)]
        [ArgumentCompleter(typeof(PSEditionArgumentCompleter))]
        public string PSEdition { get; set; }

        /// <summary>
        /// If specified, then Get-Module refreshes the internal cmdlet analysis cache
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_AvailableLocally)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInPsrpSession)]
        [Parameter(ParameterSetName = ParameterSet_AvailableInCimSession)]
        public SwitchParameter Refresh { get; set; }

        /// <summary>
        /// If specified, then Get-Module will attempt to discover PowerShell modules on a remote computer using the specified session
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_AvailableInPsrpSession, Mandatory = true)]
        [ValidateNotNull]
        public PSSession PSSession { get; set; }

        /// <summary>
        /// If specified, then Get-Module will attempt to discover PS-CIM modules on a remote computer using the specified session
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_AvailableInCimSession, Mandatory = true)]
        [ValidateNotNull]
        public CimSession CimSession { get; set; }

        /// <summary>
        /// For interoperability with 3rd party CIM servers, user can specify custom resource URI
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_AvailableInCimSession, Mandatory = false)]
        [ValidateNotNull]
        public Uri CimResourceUri { get; set; }

        /// <summary>
        /// For interoperability with 3rd party CIM servers, user can specify custom namespace
        /// </summary>
        [Parameter(ParameterSetName = ParameterSet_AvailableInCimSession, Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string CimNamespace { get; set; }

        #endregion Cmdlet parameters

        #region Remote discovery

        private IEnumerable<PSModuleInfo> GetAvailableViaPsrpSessionCore(string[] moduleNames, Runspace remoteRunspace)
        {
            Dbg.Assert(remoteRunspace != null, "Caller should verify remoteRunspace != null");

            using (var powerShell = System.Management.Automation.PowerShell.Create())
            {
                powerShell.Runspace = remoteRunspace;
                powerShell.AddCommand("Get-Module");
                powerShell.AddParameter("ListAvailable", true);

                if (Refresh.IsPresent)
                {
                    powerShell.AddParameter("Refresh", true);
                }

                if (moduleNames != null)
                {
                    powerShell.AddParameter("Name", moduleNames);
                }

                string errorMessageTemplate = string.Format(
                    CultureInfo.InvariantCulture,
                    Modules.RemoteDiscoveryRemotePsrpCommandFailed,
                    "Get-Module");
                foreach (
                    PSObject outputObject in
                        RemoteDiscoveryHelper.InvokePowerShell(powerShell, this.CancellationToken, this,
                                                               errorMessageTemplate))
                {
                    PSModuleInfo moduleInfo = RemoteDiscoveryHelper.RehydratePSModuleInfo(outputObject);
                    yield return moduleInfo;
                }
            }
        }

        private PSModuleInfo GetModuleInfoForRemoteModuleWithoutManifest(RemoteDiscoveryHelper.CimModule cimModule)
        {
            return new PSModuleInfo(cimModule.ModuleName, null, null);
        }

        private PSModuleInfo ConvertCimModuleInfoToPSModuleInfo(RemoteDiscoveryHelper.CimModule cimModule,
                                                                string computerName)
        {
            try
            {
                bool containedErrors = false;

                if (cimModule.MainManifest == null)
                {
                    return GetModuleInfoForRemoteModuleWithoutManifest(cimModule);
                }

                string temporaryModuleManifestPath = Path.Combine(
                    RemoteDiscoveryHelper.GetModulePath(cimModule.ModuleName, null, computerName,
                                                        this.Context.CurrentRunspace),
                    Path.GetFileName(cimModule.ModuleName));

                Hashtable mainData = null;
                if (!containedErrors)
                {
                    mainData = RemoteDiscoveryHelper.ConvertCimModuleFileToManifestHashtable(
                        cimModule.MainManifest,
                        temporaryModuleManifestPath,
                        this,
                        ref containedErrors);
                    if (mainData == null)
                    {
                        return GetModuleInfoForRemoteModuleWithoutManifest(cimModule);
                    }
                }

                if (!containedErrors)
                {
                    mainData = RemoteDiscoveryHelper.RewriteManifest(mainData);
                }

                Hashtable localizedData = mainData; // TODO/FIXME - this needs full path support from the provider

                PSModuleInfo moduleInfo = null;
                if (!containedErrors)
                {
                    ImportModuleOptions throwAwayOptions = new ImportModuleOptions();
                    moduleInfo = LoadModuleManifest(
                        temporaryModuleManifestPath,
                        null, //scriptInfo
                        mainData,
                        localizedData,
                        0 /* - don't write errors, don't load elements, don't return null on first error */,
                        this.BaseMinimumVersion,
                        this.BaseMaximumVersion,
                        this.BaseRequiredVersion,
                        this.BaseGuid,
                        ref throwAwayOptions,
                        ref containedErrors);
                }

                if ((moduleInfo == null) || containedErrors)
                {
                    moduleInfo = GetModuleInfoForRemoteModuleWithoutManifest(cimModule);
                }

                return moduleInfo;
            }
            catch (Exception e)
            {
                ErrorRecord errorRecord = RemoteDiscoveryHelper.GetErrorRecordForProcessingOfCimModule(e, cimModule.ModuleName);
                this.WriteError(errorRecord);
                return null;
            }
        }

        private IEnumerable<PSModuleInfo> GetAvailableViaCimSessionCore(IEnumerable<string> moduleNames,
                                                                        CimSession cimSession, Uri resourceUri,
                                                                        string cimNamespace)
        {
            IEnumerable<RemoteDiscoveryHelper.CimModule> remoteModules = RemoteDiscoveryHelper.GetCimModules(
                cimSession,
                resourceUri,
                cimNamespace,
                moduleNames,
                true /* onlyManifests */,
                this,
                this.CancellationToken);

            IEnumerable<PSModuleInfo> remoteModuleInfos = remoteModules
                .Select(cimModule => this.ConvertCimModuleInfoToPSModuleInfo(cimModule, cimSession.ComputerName))
                .Where(moduleInfo => moduleInfo != null);

            return remoteModuleInfos;
        }

        #endregion Remote discovery

        #region Cancellation support

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationToken CancellationToken
        {
            get { return _cancellationTokenSource.Token; }
        }

        /// <summary>
        /// When overridden in the derived class, interrupts currently
        /// running code within the command. It should interrupt BeginProcessing,
        /// ProcessRecord, and EndProcessing.
        /// Default implementation in the base class just returns.
        /// </summary>
        protected override void StopProcessing()
        {
            _cancellationTokenSource.Cancel();
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Releases resources associated with this object
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources associated with this object
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _cancellationTokenSource.Dispose();
            }

            _disposed = true;
        }

        private bool _disposed;

        #endregion

        private void AssertListAvailableMode()
        {
            if (!this.ListAvailable.IsPresent)
            {
                string errorMessage = Modules.RemoteDiscoveryWorksOnlyInListAvailableMode;
                ArgumentException argumentException = new ArgumentException(errorMessage);
                ErrorRecord errorRecord = new ErrorRecord(
                    argumentException,
                    nameof(Modules.RemoteDiscoveryWorksOnlyInListAvailableMode),
                    ErrorCategory.InvalidArgument,
                    null);
                this.ThrowTerminatingError(errorRecord);
            }
        }

        /// <summary>
        /// Write out the specified modules...
        /// </summary>
        protected override void ProcessRecord()
        {
            // Name and FullyQualifiedName should not be specified at the same time.
            // Throw out terminating error if this is the case.
            if (Name != null && FullyQualifiedName != null)
            {
                string errMsg = StringUtil.Format(SessionStateStrings.GetContent_TailAndHeadCannotCoexist, nameof(Name), nameof(FullyQualifiedName));
                ErrorRecord error = new ErrorRecord(new InvalidOperationException(errMsg), "NameAndFullyQualifiedNameCannotBeSpecifiedTogether", ErrorCategory.InvalidOperation, null);
                ThrowTerminatingError(error);
            }

            var strNames = new List<string>();
            if (Name != null)
            {
                strNames.AddRange(Name);
            }

            Dictionary<string, ModuleSpecification> moduleSpecTable = null;
            if (FullyQualifiedName != null)
            {
                moduleSpecTable = FullyQualifiedName.ToDictionary(moduleSpecification => moduleSpecification.Name, StringComparer.OrdinalIgnoreCase);
                strNames.AddRange(FullyQualifiedName.Select(spec => spec.Name));
            }

            string[] names = strNames.Count > 0 ? strNames.ToArray() : null;

            if (ParameterSetName.Equals(ParameterSet_Loaded, StringComparison.OrdinalIgnoreCase))
            {
                AssertNameDoesNotResolveToAPath(
                    names,
                    Modules.ModuleDiscoveryForLoadedModulesWorksOnlyForUnQualifiedNames,
                    nameof(Modules.ModuleDiscoveryForLoadedModulesWorksOnlyForUnQualifiedNames));
                GetLoadedModules(names, moduleSpecTable, this.All);
                return;
            }

            if (ParameterSetName.Equals(ParameterSet_AvailableLocally, StringComparison.OrdinalIgnoreCase))
            {
                if (ListAvailable.IsPresent)
                {
                    GetAvailableLocallyModules(names, moduleSpecTable, this.All);
                    return;
                }

                AssertNameDoesNotResolveToAPath(
                    names,
                    Modules.ModuleDiscoveryForLoadedModulesWorksOnlyForUnQualifiedNames,
                    nameof(Modules.ModuleDiscoveryForLoadedModulesWorksOnlyForUnQualifiedNames));
                GetLoadedModules(names, moduleSpecTable, this.All);
                return;
            }

            if (ParameterSetName.Equals(ParameterSet_AvailableInPsrpSession, StringComparison.OrdinalIgnoreCase))
            {
                AssertListAvailableMode();
                AssertNameDoesNotResolveToAPath(names,
                                                Modules.RemoteDiscoveryWorksOnlyForUnQualifiedNames,
                                                nameof(Modules.RemoteDiscoveryWorksOnlyForUnQualifiedNames));
                GetAvailableViaPsrpSession(names, moduleSpecTable, this.PSSession);
                return;
            }

            if (ParameterSetName.Equals(ParameterSet_AvailableInCimSession, StringComparison.OrdinalIgnoreCase))
            {
                AssertListAvailableMode();
                AssertNameDoesNotResolveToAPath(names,
                                                Modules.RemoteDiscoveryWorksOnlyForUnQualifiedNames,
                                                nameof(Modules.RemoteDiscoveryWorksOnlyForUnQualifiedNames));
                GetAvailableViaCimSession(names, moduleSpecTable, this.CimSession,
                                          this.CimResourceUri, this.CimNamespace);
                return;
            }

            Dbg.Assert(false, "Unrecognized parameter set");
        }

        private void AssertNameDoesNotResolveToAPath(string[] names, string stringFormat, string resourceId)
        {
            if (names != null)
            {
                foreach (var name in names)
                {
                    if (name.IndexOf(StringLiterals.DefaultPathSeparator) != -1 || name.IndexOf(StringLiterals.AlternatePathSeparator) != -1)
                    {
                        string errorMessage = StringUtil.Format(stringFormat, name);
                        var argumentException = new ArgumentException(errorMessage);
                        var errorRecord = new ErrorRecord(
                            argumentException,
                            resourceId,
                            ErrorCategory.InvalidArgument,
                            name);
                        this.ThrowTerminatingError(errorRecord);
                    }
                }
            }
        }

        /// <summary>
        /// Write all the available modules in a CIM session to the output stream.
        /// </summary>
        /// <param name="names">The names of the modules to look for, with null meaning all.</param>
        /// <param name="moduleSpecTable">The module specifications to match modules against.</param>
        /// <param name="cimSession">The CIM session to get available modules from.</param>
        /// <param name="resourceUri">The URI of the CIM resource.</param>
        /// <param name="cimNamespace">The CIM namespace of the resource.</param>
        private void GetAvailableViaCimSession(IEnumerable<string> names, IDictionary<string, ModuleSpecification> moduleSpecTable,
                                               CimSession cimSession, Uri resourceUri, string cimNamespace)
        {
            IEnumerable<PSModuleInfo> remoteModules = GetAvailableViaCimSessionCore(names, cimSession, resourceUri, cimNamespace)
                .Where(remoteModule => ModuleMatch(remoteModule, moduleSpecTable, PSEdition));

            foreach (PSModuleInfo remoteModule in remoteModules)
            {
                RemoteDiscoveryHelper.AssociatePSModuleInfoWithSession(
                    remoteModule,
                    cimSession,
                    resourceUri,
                    cimNamespace);
                this.WriteObject(remoteModule);
            }
        }

        /// <summary>
        /// Write all available modules in a given PowerShell Remote Protocol session to the output stream.
        /// </summary>
        /// <param name="names">The names of the modules to get, with null meaning all.</param>
        /// <param name="moduleSpecTable">The module specifications to match modules against.</param>
        /// <param name="session">The PSRP session to get available modules from.</param>
        private void GetAvailableViaPsrpSession(string[] names, IDictionary<string, ModuleSpecification> moduleSpecTable, PSSession session)
        {
            IEnumerable<PSModuleInfo> remoteModules = GetAvailableViaPsrpSessionCore(names, session.Runspace)
                .Where(remoteModule => ModuleMatch(remoteModule, moduleSpecTable, PSEdition));

            foreach (PSModuleInfo remoteModule in remoteModules)
            {
                RemoteDiscoveryHelper.AssociatePSModuleInfoWithSession(remoteModule, session);
                this.WriteObject(remoteModule);
            }
        }

        /// <summary>
        /// Write all available modules in the local PowerShell session to the output stream.
        /// </summary>
        /// <param name="names">The names of the modules to get, with null meaning all.</param>
        /// <param name="moduleSpecTable">The module specifications to match modules against.</param>
        /// <param name="all">If true, include all nested modules regardless of visibility, otherwise only list available installed modules.</param>
        private void GetAvailableLocallyModules(string[] names, IDictionary<string, ModuleSpecification> moduleSpecTable, bool all)
        {
            bool refresh = Refresh.IsPresent;
            IEnumerable<PSModuleInfo> modules = GetModule(names, all, refresh)
                .GetModulesInPathOrder()
                .Where(module => ModuleMatch(module, moduleSpecTable, PSEdition));

            foreach (PSModuleInfo psModule in modules)
            {
                var modInfoObj = new PSObject(psModule);
                modInfoObj.TypeNames.Insert(0, "ModuleInfoGrouping");
                WriteObject(psModule);
            }
        }

        /// <summary>
        /// Write all modules loaded in the current PowerShell session to the output stream.
        /// </summary>
        /// <param name="names">The names of the modules to get, with null meaning all.</param>
        /// <param name="moduleSpecTable">The module specifications to match modules against.</param>
        /// <param name="all">If true, include nested modules regardless of visibility.</param>
        private void GetLoadedModules(string[] names, IDictionary<string, ModuleSpecification> moduleSpecTable, bool all)
        {
            IEnumerable<PSModuleInfo> modulesToWrite = Context.Modules.GetModules(names, all)
                .Where(moduleInfo => ModuleMatch(moduleInfo, moduleSpecTable, PSEdition));

            foreach (PSModuleInfo moduleInfo in modulesToWrite)
            {
                WriteObject(moduleInfo);
            }
        }

        /// <summary>
        /// Determine whether a module info matches a given module specification table and specified PSEdition value.
        /// </summary>
        /// <param name="moduleInfo">The module info object to check for matching the given constraints.</param>
        /// <param name="moduleSpecTable">If non-null, specifies a table to look up the attributes the module must have.</param>
        /// <param name="edition">The PowerShell edition the module must declare compatibility with.</param>
        /// <returns>True if the given module info object satisfies all the provided constraints.</returns>
        private static bool ModuleMatch(PSModuleInfo moduleInfo, IDictionary<string, ModuleSpecification> moduleSpecTable, string edition)
        {
            if (!String.IsNullOrEmpty(edition) && !moduleInfo.CompatiblePSEditions.Contains(edition, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (moduleSpecTable == null || moduleSpecTable.Count == 0)
            {
                return true;
            }

            if (moduleSpecTable.TryGetValue(moduleInfo.Name, out ModuleSpecification moduleSpecification))
            {
                return ModuleIntrinsics.IsModuleMatchingModuleSpec(moduleInfo, moduleSpecification);
            }

            // This shouldn't return true when it fails to find a table entry,
            // but the table doesn't handle relative paths well. It's not a problem
            // since the internal GetModule() filters the names properly, but makes
            // the logic of this method hard to describe sensibly.
            return true;
        }
    }

    /// <summary>
    /// PSEditionArgumentCompleter for PowerShell Edition names.
    /// </summary>
    public class PSEditionArgumentCompleter : IArgumentCompleter
    {
        /// <summary>
        /// CompleteArgument
        /// </summary>
        public IEnumerable<CompletionResult> CompleteArgument(string commandName, string parameterName, string wordToComplete, CommandAst commandAst, IDictionary fakeBoundParameters)
        {
            var wordToCompletePattern = WildcardPattern.Get(string.IsNullOrWhiteSpace(wordToComplete) ? "*" : wordToComplete + "*", WildcardOptions.IgnoreCase);

            foreach (var edition in Utils.AllowedEditionValues)
            {
                if (wordToCompletePattern.IsMatch(edition))
                {
                    yield return new CompletionResult(edition, edition, CompletionResultType.Text, edition);
                }
            }
        }
    }
} // Microsoft.PowerShell.Commands

