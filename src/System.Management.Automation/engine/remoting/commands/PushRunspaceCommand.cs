/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Remoting;
using System.Management.Automation.Internal;
using System.Management.Automation.Runspaces;
using Dbg = System.Management.Automation.Diagnostics;


namespace Microsoft.PowerShell.Commands
{
    /// <summary>
    /// Enter-PSSession cmdlet.
    /// </summary>
    [Cmdlet(VerbsCommon.Enter, "PSSession", DefaultParameterSetName = "ComputerName",
        HelpUri = "http://go.microsoft.com/fwlink/?LinkID=135210", RemotingCapability = RemotingCapability.OwnedByCommand)]
    public class EnterPSSessionCommand : PSRemotingBaseCmdlet
    {
        #region Strings

        private const string InstanceIdParameterSet = "InstanceId";
        private const string IdParameterSet = "Id";
        private const string NameParameterSet = "Name";

        #endregion

        #region Members
    
        /// <summary>
        /// Disable ThrottleLimit parameter inherited from base class.
        /// </summary>
        public new Int32 ThrottleLimit { set { } get { return 0; } }
        private ObjectStream stream;

        #endregion

        #region Parameters

        /// <summary>
        /// Computer name parameter.
        /// </summary>
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true, ParameterSetName = ComputerNameParameterSet)]
        [Alias("Cn")]
        [ValidateNotNullOrEmpty]
        public new string ComputerName
        {
            get { return computerName; }
            set { computerName = value; }
        }

        private string computerName;

        /// <summary>
        /// Runspace parameter.
        /// </summary>
        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true,
            ValueFromPipeline = true, ParameterSetName = SessionParameterSet)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Runspace")]
        public new PSSession Session
        {
            get { return remoteRunspaceInfo; }
            set { remoteRunspaceInfo = value; }
        }

        private PSSession remoteRunspaceInfo;

        /// <summary>
        /// ConnectionUri parameter.
        /// </summary>
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true,
            ParameterSetName = UriParameterSet)]
        [ValidateNotNullOrEmpty]
        [Alias("URI", "CU")]
        public new Uri ConnectionUri
        {
            get { return connectionUri; }
            set { connectionUri = value; }
        }

        private Uri connectionUri;

        /// <summary>
        /// RemoteRunspaceId of the remote runspace info object.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true,
        ParameterSetName = InstanceIdParameterSet)]
        [ValidateNotNull]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Runspace")]
        public Guid InstanceId
        {
            get { return remoteRunspaceId; }
            set { remoteRunspaceId = value; }
        }

        private Guid remoteRunspaceId;

        /// <summary>
        /// SessionId of the remote runspace info object.
        /// </summary>
        [Parameter(Position = 0,
            ValueFromPipelineByPropertyName = true,
             ParameterSetName = IdParameterSet)]
        [ValidateNotNull]
        public int Id
        {
            get { return sessionId; }
            set { sessionId = value; }
        }

        private int sessionId;

        /// <summary>
        /// Name of the remote runspace info object.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true,
        ParameterSetName = NameParameterSet)]
        public String Name
        {
            get { return name; }
            set { name = value; }
        }

        private String name;

        /// <summary>
        /// When set and in loopback scenario (localhost) this enables creation of WSMan
        /// host process with the user interactive token, allowing PowerShell script network access, 
        /// i.e., allows going off box.  When this property is true and a PSSession is disconnected, 
        /// reconnection is allowed only if reconnecting from a PowerShell session on the same box.
        /// </summary>
        [Parameter(ParameterSetName = ComputerNameParameterSet)]
        [Parameter(ParameterSetName = UriParameterSet)]
        public SwitchParameter EnableNetworkAccess
        {
            get { return enableNetworkAccess; }
            set { enableNetworkAccess = value; }
        }
        private SwitchParameter enableNetworkAccess;

        /// <summary>
        /// Virtual machine ID.
        /// </summary>
        [ValidateNotNullOrEmpty]
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, 
            ValueFromPipelineByPropertyName = true, ParameterSetName = VMIdParameterSet)]
        [Alias("VMGuid")]
        public new Guid VMId
        {
            get { return vmId; }
            set { vmId = value; }
        }
        private Guid vmId;

        /// <summary>
        /// Virtual machine name.
        /// </summary>
        [ValidateNotNullOrEmpty]
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, 
            ValueFromPipelineByPropertyName = true, ParameterSetName = VMNameParameterSet)]
        public new string VMName
        {
            get { return vmName; }
            set { vmName = value; }
        }
        private string vmName;

        /// <summary>
        /// Specifies the credentials of the user to impersonate in the 
        /// virtual machine. If this parameter is not specified then the 
        /// credentials of the current user process will be assumed.
        /// </summary>
        [Parameter(ValueFromPipelineByPropertyName = true,
                   ParameterSetName = PSRemotingBaseCmdlet.ComputerNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true,
                   ParameterSetName = PSRemotingBaseCmdlet.UriParameterSet)]
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true,
                   ParameterSetName = VMIdParameterSet)]
        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true,
                   ParameterSetName = VMNameParameterSet)]
        [Credential()]
        public override PSCredential Credential
        {
            get { return base.Credential; }
            set { base.Credential = value; }
        }

        /// <summary>
        /// The Id of the target container.
        /// </summary>
        [ValidateNotNullOrEmpty]
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, 
            ValueFromPipelineByPropertyName = true, ParameterSetName = ContainerIdParameterSet)]
        public new string ContainerId
        {
            get { return containerId; }
            set { containerId = value; }
        }
        private string containerId;

        /// <summary>
        /// The name of the target container.
        /// </summary>
        [ValidateNotNullOrEmpty]
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, 
            ValueFromPipelineByPropertyName = true, ParameterSetName = ContainerNameParameterSet)]
        public new string ContainerName
        {
            get { return containerName; }
            set { containerName = value; }
        }
        private string containerName;

        /// <summary>
        /// For WSMan sessions:
        /// If this parameter is not specified then the value specified in
        /// the environment variable DEFAULTREMOTESHELLNAME will be used. If 
        /// this is not set as well, then Microsoft.PowerShell is used.
        ///
        /// For VM/Container sessions:
        /// If this parameter is not specified then no configuration is used.
        /// </summary>      
        [Parameter(ValueFromPipelineByPropertyName = true,
                   ParameterSetName = EnterPSSessionCommand.ComputerNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true,
                   ParameterSetName = EnterPSSessionCommand.UriParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true,
                   ParameterSetName = EnterPSSessionCommand.ContainerIdParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true,
                   ParameterSetName = EnterPSSessionCommand.ContainerNameParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true,
                   ParameterSetName = EnterPSSessionCommand.VMIdParameterSet)]
        [Parameter(ValueFromPipelineByPropertyName = true,
                   ParameterSetName = EnterPSSessionCommand.VMNameParameterSet)]
        public String ConfigurationName
        {
            get
            {
                return shell;
            }
            set
            {
                shell = value;
            }
        }
        private String shell;

        #endregion

        #region Overrides

        /// <summary>
        /// Resolves shellname and appname
        /// </summary>
        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            if (String.IsNullOrEmpty(ConfigurationName))
            {
                if ((ParameterSetName == EnterPSSessionCommand.ComputerNameParameterSet) ||
                    (ParameterSetName == EnterPSSessionCommand.UriParameterSet))
                {
                    // set to default value for WSMan session
                    ConfigurationName = ResolveShell(null);
                }
                else
                {
                    // convert null to String.Empty for VM/Container session
                    ConfigurationName = String.Empty;
                }
            }
        }

        /// <summary>
        /// Process record.
        /// </summary>
        protected override void ProcessRecord()
        {
            // Push the remote runspace on the local host.
            IHostSupportsInteractiveSession host = this.Host as IHostSupportsInteractiveSession;
            if (host == null)
            {
                WriteError(
                    new ErrorRecord(
                        new ArgumentException(GetMessage(RemotingErrorIdStrings.HostDoesNotSupportPushRunspace)),
                        PSRemotingErrorId.HostDoesNotSupportPushRunspace.ToString(),
                        ErrorCategory.InvalidArgument,
                        null));
                return;
            }

            // Check if current host is remote host.  Enter-PSSession on remote host is not
            // currently supported.
            if (!IsParameterSetForVM() &&
                !IsParameterSetForContainer() &&
                !IsParameterSetForVMContainerSession() &&
                this.Context != null &&
                this.Context.EngineHostInterface != null &&
                this.Context.EngineHostInterface.ExternalHost != null &&
                this.Context.EngineHostInterface.ExternalHost is System.Management.Automation.Remoting.ServerRemoteHost)
            {
                WriteError(
                    new ErrorRecord(
                        new ArgumentException(GetMessage(RemotingErrorIdStrings.RemoteHostDoesNotSupportPushRunspace)),
                        PSRemotingErrorId.RemoteHostDoesNotSupportPushRunspace.ToString(),
                        ErrorCategory.InvalidArgument,
                        null));
                return;
            }

            // for the console host and Graphical PowerShell host
            // we want to skip pushing into the the runspace if
            // the host is in a nested prompt
            System.Management.Automation.Internal.Host.InternalHost chost = 
                this.Host as System.Management.Automation.Internal.Host.InternalHost;

            if (!IsParameterSetForVM() && 
                !IsParameterSetForContainer() &&
                !IsParameterSetForVMContainerSession() &&
                chost != null && chost.HostInNestedPrompt())
            {
                ThrowTerminatingError(new ErrorRecord(
                    new InvalidOperationException(PSRemotingErrorInvariants.FormatResourceString(RemotingErrorIdStrings.HostInNestedPrompt)),
                    "HostInNestedPrompt", ErrorCategory.InvalidOperation, chost));

            }

            /*Microsoft.Windows.PowerShell.Gui.Internal.GPSHost ghost = this.Host as Microsoft.Windows.PowerShell.Gui.Internal.GPSHost;

            if (ghost != null && ghost.HostInNestedPrompt())
            {
                ThrowTerminatingError(new ErrorRecord(
                    new InvalidOperationException(PSRemotingErrorInvariants.FormatResourceString(PSRemotingErrorId.HostInNestedPrompt)),
                    "HostInNestedPrompt", ErrorCategory.InvalidOperation, wpshost));
            }*/

            // Get the remote runspace.
            RemoteRunspace remoteRunspace = null;
            switch (ParameterSetName)
            {
                case ComputerNameParameterSet:
                    remoteRunspace = CreateRunspaceWhenComputerNameParameterSpecified();
                    break;

                case UriParameterSet:
                    remoteRunspace = CreateRunspaceWhenUriParameterSpecified();
                    break;

                case SessionParameterSet:
                    remoteRunspace = (RemoteRunspace)remoteRunspaceInfo.Runspace;
                    break;

                case InstanceIdParameterSet:
                    remoteRunspace = GetRunspaceMatchingRunspaceId(this.InstanceId);
                    break;

                case IdParameterSet:
                    remoteRunspace = GetRunspaceMatchingSessionId(this.Id);
                    break;

                case NameParameterSet:
                    remoteRunspace = GetRunspaceMatchingName(this.Name);
                    break;

                case VMIdParameterSet:
                case VMNameParameterSet:
                    remoteRunspace = GetRunspaceForVMSession();
                    break;

                case ContainerIdParameterSet:
                case ContainerNameParameterSet:
                    remoteRunspace = GetRunspaceForContainerSession();
                    break;
            }

            // If runspace is null then the error record has already been written and we can exit.
            if (remoteRunspace == null) { return; }

            // If the runspace is in a disconnected state try to connect.
            bool runspaceConnected = false;
            if (remoteRunspace.RunspaceStateInfo.State == RunspaceState.Disconnected)
            {
                if (!remoteRunspace.CanConnect)
                {
                    string message = StringUtil.Format(RemotingErrorIdStrings.SessionNotAvailableForConnection);
                    WriteError(
                        new ErrorRecord(
                            new RuntimeException(message), "EnterPSSessionCannotConnectDisconnectedSession",
                                ErrorCategory.InvalidOperation, remoteRunspace));

                    return;
                }

                // Connect the runspace.
                Exception ex = null;
                try
                {
                    remoteRunspace.Connect();
                    runspaceConnected = true;
                }
                catch (System.Management.Automation.Remoting.PSRemotingTransportException e)
                {
                    ex = e;
                }
                catch (PSInvalidOperationException e)
                {
                    ex = e;
                }
                catch (InvalidRunspacePoolStateException e)
                {
                    ex = e;
                }

                if (ex != null)
                {
                    string message = StringUtil.Format(RemotingErrorIdStrings.SessionConnectFailed);
                    WriteError(
                        new ErrorRecord(
                            new RuntimeException(message, ex), "EnterPSSessionConnectSessionFailed",
                                ErrorCategory.InvalidOperation, remoteRunspace));

                    return;
                }
            }

            // Verify that the runspace is open.
            if (remoteRunspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                if (ParameterSetName == SessionParameterSet)
                {
                    string sessionName = (this.remoteRunspaceInfo != null) ? this.remoteRunspaceInfo.Name : string.Empty;
                    WriteError(
                        new ErrorRecord(
                            new ArgumentException(GetMessage(RemotingErrorIdStrings.EnterPSSessionBrokenSession,
                                sessionName, remoteRunspace.ConnectionInfo.ComputerName, remoteRunspace.InstanceId)),
                            PSRemotingErrorId.PushedRunspaceMustBeOpen.ToString(),
                            ErrorCategory.InvalidArgument,
                            null));
                }
                else
                {
                    WriteError(
                        new ErrorRecord(
                            new ArgumentException(GetMessage(RemotingErrorIdStrings.PushedRunspaceMustBeOpen)),
                            PSRemotingErrorId.PushedRunspaceMustBeOpen.ToString(),
                            ErrorCategory.InvalidArgument,
                            null));
                }

                return;
            }

            Debugger debugger = null;
            try
            {
                if (host.Runspace != null)
                {
                    debugger = host.Runspace.Debugger;
                }
            }
            catch (PSNotImplementedException) { }

            bool supportRunningCommand = ((debugger != null) && ((debugger.DebugMode & DebugModes.RemoteScript) == DebugModes.RemoteScript));
            if (remoteRunspace.RunspaceAvailability != RunspaceAvailability.Available)
            {
                // Session has running command.
                if (!supportRunningCommand)
                {
                    // Host does not support remote debug and cannot connect to running command.
                    if (runspaceConnected)
                    {
                        // If we succeeded connecting the session (runspace) but it is already running a command,
                        // emit an error for this case because since it is disconnected this session object will
                        // never complete and the user must use *reconstruct* scenario to retrieve data.
                        string message = StringUtil.Format(RemotingErrorIdStrings.EnterPSSessionDisconnected,
                                                        remoteRunspace.PSSessionName);
                        WriteError(
                            new ErrorRecord(
                                new RuntimeException(message), "EnterPSSessionConnectSessionNotAvailable",
                                    ErrorCategory.InvalidOperation, remoteRunspaceInfo));

                        // Leave session in original disconnected state.
                        remoteRunspace.DisconnectAsync();

                        return;
                    }
                    else
                    {
                        // If the remote runspace is currently not available then let user know that this command
                        // will not complete until it becomes available.
                        WriteWarning(GetMessage(RunspaceStrings.RunspaceNotReady));
                    }
                }
                else
                {
                    // Running commands supported.
                    // Warn user that they are entering a session that is running a command and output may
                    // be going to a job object.
                    Job job = FindJobForRunspace(remoteRunspace.InstanceId);
                    string msg;
                    if (job != null)
                    {
                        msg = StringUtil.Format(
                            RunspaceStrings.RunningCmdWithJob,
                            (!string.IsNullOrEmpty(job.Name)) ? job.Name : string.Empty);
                    }
                    else
                    {
                        if(remoteRunspace.RunspaceAvailability == RunspaceAvailability.RemoteDebug)
                        {
                            msg = StringUtil.Format(
                                RunspaceStrings.RunningCmdDebugStop);
                        }
                        else
                        {
                            msg = StringUtil.Format(
                                RunspaceStrings.RunningCmdWithoutJob);
                        }
                    }

                    WriteWarning(msg);
                }
            }

            // Make sure any PSSession object passed in is saved in the local runspace repository.
            if (remoteRunspaceInfo != null)
            {
                this.RunspaceRepository.AddOrReplace(remoteRunspaceInfo);
            }

            // prepare runspace for prompt
            SetRunspacePrompt(remoteRunspace);

            try
            {
                host.PushRunspace(remoteRunspace);
            }
            catch (Exception)
            {
                // A third-party host can throw any exception here..we should
                // clean the runspace created in this case.
                if ((null != remoteRunspace) && (remoteRunspace.ShouldCloseOnPop))
                {
                    remoteRunspace.Close();
                }

                // rethrow the exception after cleanup.
                throw;
            }
        }

        /// <summary>
        /// This method will until the runspace is opened and warnings if any
        /// are reported
        /// </summary>
        protected override void EndProcessing()
        {
            if (null != stream)
            {
                while (true)
                {
                    // Keep reading objects until end of stream is encountered
                    stream.ObjectReader.WaitHandle.WaitOne();

                    if (!stream.ObjectReader.EndOfPipeline)
                    {
                        Object streamObject = stream.ObjectReader.Read();
                        WriteStreamObject((Action<Cmdlet>)streamObject);
                    }
                    else
                    {
                        break;
                    }
                } // while ...
            }
        }// EndProcessing()

        /// <summary>
        /// 
        /// </summary>
        protected override void StopProcessing()
        {
            IHostSupportsInteractiveSession host = this.Host as IHostSupportsInteractiveSession;
            if (host == null)
            {
                WriteError(
                    new ErrorRecord(
                        new ArgumentException(GetMessage(RemotingErrorIdStrings.HostDoesNotSupportPushRunspace)),
                        PSRemotingErrorId.HostDoesNotSupportPushRunspace.ToString(),
                        ErrorCategory.InvalidArgument,
                        null));
                return;
            }
            host.PopRunspace();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Create temporary remote runspace.
        /// </summary>
        private RemoteRunspace CreateTemporaryRemoteRunspace(PSHost host, WSManConnectionInfo connectionInfo)
        {
            // Create and open the runspace.
            int rsId;
            string rsName = PSSession.GenerateRunspaceName(out rsId);
            RemoteRunspace remoteRunspace = new RemoteRunspace(
                Utils.GetTypeTableFromExecutionContextTLS(),
                connectionInfo,
                host,
                this.SessionOption.ApplicationArguments,
                rsName,
                rsId);
            Dbg.Assert(remoteRunspace != null, "Expected remoteRunspace != null");
            remoteRunspace.URIRedirectionReported += HandleURIDirectionReported;

            stream = new ObjectStream();
            try
            {
                remoteRunspace.Open();

                // Mark this temporary runspace so that it closes on pop.
                remoteRunspace.ShouldCloseOnPop = true;
            }
            finally
            {
                // unregister uri redirection handler
                remoteRunspace.URIRedirectionReported -= HandleURIDirectionReported;
                // close the internal object stream after runspace is opened
                // Runspace.Open() might throw exceptions..this will make sure
                // the stream is always closed.
                stream.ObjectWriter.Close();

                // make sure we dispose the temporary runspace if something bad happens
                if (remoteRunspace.RunspaceStateInfo.State != RunspaceState.Opened)
                {
                    remoteRunspace.Dispose();
                    remoteRunspace = null;
                }
            }

            return remoteRunspace;
        }

        /// <summary>
        /// Write error create remote runspace failed.
        /// </summary>
        private void WriteErrorCreateRemoteRunspaceFailed(Exception exception, object argument)
        {
            // set the transport message in the error detail so that
            // the user can directly get to see the message without
            // having to mine through the error record details
            PSRemotingTransportException transException =
                        exception as PSRemotingTransportException;
            String errorDetails = null;
            if ((transException != null) &&
                (transException.ErrorCode ==
                    System.Management.Automation.Remoting.Client.WSManNativeApi.ERROR_WSMAN_REDIRECT_REQUESTED))
            {
                // Handling a special case for redirection..we should talk about
                // AllowRedirection parameter and WSManMaxRedirectionCount preference
                // variables
                string message = PSRemotingErrorInvariants.FormatResourceString(
                    RemotingErrorIdStrings.URIRedirectionReported,
                    transException.Message,
                    "MaximumConnectionRedirectionCount",
                    Microsoft.PowerShell.Commands.PSRemotingBaseCmdlet.DEFAULT_SESSION_OPTION,
                    "AllowRedirection");

                errorDetails = message;
            }

            ErrorRecord errorRecord = new ErrorRecord(exception, argument,
                "CreateRemoteRunspaceFailed", 
                ErrorCategory.InvalidArgument,
                null, null, null, null, null, errorDetails, null);

            WriteError(errorRecord);
        }

        /// <summary>
        /// Write invalid argument error.
        /// </summary>
        private void WriteInvalidArgumentError(PSRemotingErrorId errorId,  string resourceString, object errorArgument)
        {
            String message = GetMessage(resourceString, errorArgument);
            WriteError(new ErrorRecord(new ArgumentException(message), errorId.ToString(),
                ErrorCategory.InvalidArgument, errorArgument));
        }

        /// <summary>
        /// When the client remote session reports a URI redirection, this method will report the
        /// message to the user as a Warning using Host method calls.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void HandleURIDirectionReported(object sender, RemoteDataEventArgs<Uri> eventArgs)
        {
            string message = StringUtil.Format(RemotingErrorIdStrings.URIRedirectWarningToHost, eventArgs.Data.OriginalString);
            Action<Cmdlet> streamObject = delegate(Cmdlet cmdlet)
            {
                cmdlet.WriteWarning(message);
            };
            stream.Write(streamObject);
        }

        /// <summary>
        /// Create runspace when computer name parameter specified.
        /// </summary>
        private RemoteRunspace CreateRunspaceWhenComputerNameParameterSpecified()
        {
            RemoteRunspace remoteRunspace = null;
            string resolvedComputerName = ResolveComputerName(computerName);
            try
            {
                WSManConnectionInfo connectionInfo = null;
                connectionInfo = new WSManConnectionInfo();
                string scheme = UseSSL.IsPresent ? WSManConnectionInfo.HttpsScheme : WSManConnectionInfo.HttpScheme;
                connectionInfo.ComputerName = resolvedComputerName;
                connectionInfo.Port = Port;
                connectionInfo.AppName = ApplicationName;
                connectionInfo.ShellUri = ConfigurationName;
                connectionInfo.Scheme = scheme;
                if (CertificateThumbprint != null)
                {
                    connectionInfo.CertificateThumbprint = CertificateThumbprint;
                }
                else
                {
                    connectionInfo.Credential = Credential;
                } 

                connectionInfo.AuthenticationMechanism = Authentication;
                UpdateConnectionInfo(connectionInfo);

                connectionInfo.EnableNetworkAccess = EnableNetworkAccess;

                remoteRunspace = CreateTemporaryRemoteRunspace(this.Host, connectionInfo);
            }
            catch (InvalidOperationException e)
            {
                WriteErrorCreateRemoteRunspaceFailed(e, resolvedComputerName);
            }
            catch (ArgumentException e)
            {
                WriteErrorCreateRemoteRunspaceFailed(e, resolvedComputerName);
            }
            catch (PSRemotingTransportException e)
            {
                WriteErrorCreateRemoteRunspaceFailed(e, resolvedComputerName);
            }

            return remoteRunspace;
        }

        /// <summary>
        /// Create runspace when uri parameter specified.
        /// </summary>
        private RemoteRunspace CreateRunspaceWhenUriParameterSpecified()
        {
            RemoteRunspace remoteRunspace = null;
            try
            {
                WSManConnectionInfo connectionInfo = new WSManConnectionInfo();
                connectionInfo.ConnectionUri = ConnectionUri;
                connectionInfo.ShellUri = ConfigurationName;
                if (CertificateThumbprint != null)
                {
                    connectionInfo.CertificateThumbprint = CertificateThumbprint;
                }
                else
                {
                    connectionInfo.Credential = Credential;
                }
                connectionInfo.AuthenticationMechanism = Authentication;
                UpdateConnectionInfo(connectionInfo);
                connectionInfo.EnableNetworkAccess = EnableNetworkAccess;
                remoteRunspace = CreateTemporaryRemoteRunspace(this.Host, connectionInfo);
            }
            catch (UriFormatException e)
            {
                WriteErrorCreateRemoteRunspaceFailed(e, ConnectionUri);
            }
            catch (InvalidOperationException e)
            {
                WriteErrorCreateRemoteRunspaceFailed(e, ConnectionUri);
            }
            catch (ArgumentException e)
            {
                WriteErrorCreateRemoteRunspaceFailed(e, ConnectionUri);
            }
            catch (PSRemotingTransportException e)
            {
                WriteErrorCreateRemoteRunspaceFailed(e, ConnectionUri);
            }
            catch (NotSupportedException e)
            {
                WriteErrorCreateRemoteRunspaceFailed(e, ConnectionUri);
            }

            return remoteRunspace;
        }

        /// <summary>
        /// Get runspace matching condition.
        /// </summary>
        private RemoteRunspace GetRunspaceMatchingCondition(
            Predicate<PSSession> condition, 
            PSRemotingErrorId tooFew, 
            PSRemotingErrorId tooMany, 
            string tooFewResourceString,
            string tooManyResourceString,
            object errorArgument)
        {
            // Find matches.
            List<PSSession> matches = this.RunspaceRepository.Runspaces.FindAll(condition);

            // Validate.
            RemoteRunspace remoteRunspace = null;
            if (matches.Count == 0)
            {
                WriteInvalidArgumentError(tooFew, tooFewResourceString, errorArgument);
            }
            else if (matches.Count > 1)
            {
                WriteInvalidArgumentError(tooMany, tooManyResourceString, errorArgument);
            }
            else
            {
                remoteRunspace = (RemoteRunspace)matches[0].Runspace;
                Dbg.Assert(remoteRunspace != null, "Expected remoteRunspace != null");
            }

            return remoteRunspace;
        }

        /// <summary>
        /// Get runspace matching runspace id.
        /// </summary>
        private RemoteRunspace GetRunspaceMatchingRunspaceId(Guid remoteRunspaceId)
        {
            Predicate<PSSession> condition = delegate(PSSession info)
            {
                return info.InstanceId == remoteRunspaceId;
            };
            PSRemotingErrorId tooFew = PSRemotingErrorId.RemoteRunspaceNotAvailableForSpecifiedRunspaceId;
            PSRemotingErrorId tooMany = PSRemotingErrorId.RemoteRunspaceHasMultipleMatchesForSpecifiedRunspaceId;
            string tooFewResourceString = RemotingErrorIdStrings.RemoteRunspaceNotAvailableForSpecifiedRunspaceId;
            string tooManyResourceString = RemotingErrorIdStrings.RemoteRunspaceHasMultipleMatchesForSpecifiedRunspaceId;
            return GetRunspaceMatchingCondition(condition, tooFew, tooMany, tooFewResourceString, tooManyResourceString, remoteRunspaceId);
        }

        /// <summary>
        /// Get runspace matching session id.
        /// </summary>
        private RemoteRunspace GetRunspaceMatchingSessionId(int sessionId)
        {
            Predicate<PSSession> condition = delegate(PSSession info)
            {
                return info.Id == sessionId;
            };
            PSRemotingErrorId tooFew = PSRemotingErrorId.RemoteRunspaceNotAvailableForSpecifiedSessionId;
            PSRemotingErrorId tooMany = PSRemotingErrorId.RemoteRunspaceHasMultipleMatchesForSpecifiedSessionId;
            string tooFewResourceString = RemotingErrorIdStrings.RemoteRunspaceNotAvailableForSpecifiedSessionId;
            string tooManyResourceString = RemotingErrorIdStrings.RemoteRunspaceHasMultipleMatchesForSpecifiedSessionId;
            return GetRunspaceMatchingCondition(condition, tooFew, tooMany, tooFewResourceString, tooManyResourceString, sessionId);
        }

        /// <summary>
        /// Get runspace matching name.
        /// </summary>
        private RemoteRunspace GetRunspaceMatchingName(string name)
        {
            Predicate<PSSession> condition = delegate(PSSession info)
            {
                // doing case-insensitive match for session name
                return info.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
            };
            PSRemotingErrorId tooFew = PSRemotingErrorId.RemoteRunspaceNotAvailableForSpecifiedName;
            PSRemotingErrorId tooMany = PSRemotingErrorId.RemoteRunspaceHasMultipleMatchesForSpecifiedName;
            string tooFewResourceString = RemotingErrorIdStrings.RemoteRunspaceNotAvailableForSpecifiedName;
            string tooManyResourceString = RemotingErrorIdStrings.RemoteRunspaceHasMultipleMatchesForSpecifiedName;
            return GetRunspaceMatchingCondition(condition, tooFew, tooMany, tooFewResourceString, tooManyResourceString, name);
        }

        private Job FindJobForRunspace(Guid id)
        {
            foreach (var repJob in this.JobRepository.Jobs)
            {
                foreach (Job childJob in repJob.ChildJobs)
                {
                    PSRemotingChildJob remotingChildJob = childJob as PSRemotingChildJob;

                    if (remotingChildJob != null &&
                        remotingChildJob.Runspace != null &&
                        remotingChildJob.JobStateInfo.State == JobState.Running &&
                        remotingChildJob.Runspace.InstanceId.Equals(id))
                    {
                        return repJob;
                    }
                }
            }

            return null;
        }

        private bool IsParameterSetForVM()
        {
            return ((ParameterSetName == VMIdParameterSet) ||
                    (ParameterSetName == VMNameParameterSet));
        }

        private bool IsParameterSetForContainer()
        {
            return ((ParameterSetName == ContainerIdParameterSet) ||
                    (ParameterSetName == ContainerNameParameterSet));
        }

        /// <summary>
        /// Whether the input is a session object or property that corresponds to
        /// VM or container.
        /// </summary>
        private bool IsParameterSetForVMContainerSession()
        {
            RemoteRunspace remoteRunspace = null;

            switch (ParameterSetName)
            {
                case SessionParameterSet:
                    if (this.Session != null)
                    {
                        remoteRunspace = (RemoteRunspace)this.Session.Runspace;
                    }
                    break;

                case InstanceIdParameterSet:
                    remoteRunspace = GetRunspaceMatchingRunspaceId(this.InstanceId);
                    break;

                case IdParameterSet:
                    remoteRunspace = GetRunspaceMatchingSessionId(this.Id);
                    break;

                case NameParameterSet:
                    remoteRunspace = GetRunspaceMatchingName(this.Name);
                    break;

                default:
                    break;
            }

            if ((remoteRunspace != null) &&
                (remoteRunspace.ConnectionInfo != null))
            {
                if ((remoteRunspace.ConnectionInfo is VMConnectionInfo) ||
                    (remoteRunspace.ConnectionInfo is ContainerConnectionInfo))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Create runspace for VM session.
        /// </summary>
        private RemoteRunspace GetRunspaceForVMSession()
        {
            RemoteRunspace remoteRunspace = null;
            string command;
            Collection<PSObject> results;

            if (ParameterSetName == VMIdParameterSet)
            {
                command = "Get-VM -Id $args[0]";

                try
                {
                    results = this.InvokeCommand.InvokeScript(
                        command, false, PipelineResultTypes.None, null, this.VMId);
                }
                catch (CommandNotFoundException)
                {
                    WriteError(
                        new ErrorRecord(
                            new ArgumentException(RemotingErrorIdStrings.HyperVModuleNotAvailable),
                            PSRemotingErrorId.HyperVModuleNotAvailable.ToString(),
                            ErrorCategory.NotInstalled,
                            null));
                
                    return null;
                }
                
                if (results.Count != 1)
                {
                    WriteError(
                        new ErrorRecord(
                            new ArgumentException(RemotingErrorIdStrings.InvalidVMId),
                            PSRemotingErrorId.InvalidVMId.ToString(),
                            ErrorCategory.InvalidArgument,
                            null));

                    return null;
                }
                
                this.VMName = (string)results[0].Properties["VMName"].Value;
            }
            else
            {
                Dbg.Assert(ParameterSetName == VMNameParameterSet, "Expected ParameterSetName == VMName");
            
                command = "Get-VM -Name $args";

                try
                {
                    results = this.InvokeCommand.InvokeScript(
                        command, false, PipelineResultTypes.None, null, this.VMName);
                }
                catch (CommandNotFoundException)
                {
                    WriteError(
                        new ErrorRecord(
                            new ArgumentException(RemotingErrorIdStrings.HyperVModuleNotAvailable),
                            PSRemotingErrorId.HyperVModuleNotAvailable.ToString(),
                            ErrorCategory.NotInstalled,
                            null));
                
                    return null;
                }

                if (results.Count == 0)
                {
                    WriteError(
                        new ErrorRecord(
                            new ArgumentException(RemotingErrorIdStrings.InvalidVMNameNoVM),
                            PSRemotingErrorId.InvalidVMNameNoVM.ToString(),
                            ErrorCategory.InvalidArgument,
                            null));

                    return null;
                }
                else if (results.Count > 1)
                {
                    WriteError(
                        new ErrorRecord(
                            new ArgumentException(RemotingErrorIdStrings.InvalidVMNameMultipleVM),
                            PSRemotingErrorId.InvalidVMNameMultipleVM.ToString(),
                            ErrorCategory.InvalidArgument,
                            null));

                    return null;
                }

                this.VMId = (Guid)results[0].Properties["VMId"].Value;
                this.VMName = (string)results[0].Properties["VMName"].Value;
            }
            
            try
            {
                VMConnectionInfo connectionInfo;
                connectionInfo = new VMConnectionInfo(this.Credential, this.VMId, this.VMName, this.ConfigurationName);

                remoteRunspace = CreateTemporaryRemoteRunspaceForPowerShellDirect(this.Host, connectionInfo);
            }
            catch (InvalidOperationException e)
            {
                ErrorRecord errorRecord = new ErrorRecord(e,
                    "CreateRemoteRunspaceForVMFailed", 
                    ErrorCategory.InvalidOperation,
                    null);
                
                WriteError(errorRecord);
            }
            catch (ArgumentException e)
            {
                ErrorRecord errorRecord = new ErrorRecord(e,
                    "CreateRemoteRunspaceForVMFailed", 
                    ErrorCategory.InvalidArgument,
                    null);
                
                WriteError(errorRecord);
            }
            catch (PSRemotingDataStructureException e)
            {
                ErrorRecord errorRecord;

                //
                // In case of PSDirectException, we should output the precise error message
                // in inner exception instead of the generic one in outer exception.
                //
                if ((e.InnerException != null) && (e.InnerException is PSDirectException))
                {
                    errorRecord = new ErrorRecord(e.InnerException,
                        "CreateRemoteRunspaceForVMFailed", 
                        ErrorCategory.InvalidArgument,
                        null);                        
                }
                else
                {
                    errorRecord = new ErrorRecord(e,
                        "CreateRemoteRunspaceForVMFailed", 
                        ErrorCategory.InvalidOperation,
                        null);
                }

                WriteError(errorRecord);
            }
            catch (Exception e)
            {
                ErrorRecord errorRecord = new ErrorRecord(e,
                    "CreateRemoteRunspaceForVMFailed", 
                    ErrorCategory.InvalidOperation,
                    null);
                
                WriteError(errorRecord);
            }

            return remoteRunspace;
        }

        /// <summary>
        /// Create temporary remote runspace.
        /// </summary>
        private RemoteRunspace CreateTemporaryRemoteRunspaceForPowerShellDirect(PSHost host, RunspaceConnectionInfo connectionInfo)
        {
            // Create and open the runspace.
            TypeTable typeTable = TypeTable.LoadDefaultTypeFiles();
            RemoteRunspace remoteRunspace = RunspaceFactory.CreateRunspace(connectionInfo, host, typeTable) as RemoteRunspace;
            remoteRunspace.Name = "PowerShellDirectAttach";
            
            Dbg.Assert(remoteRunspace != null, "Expected remoteRunspace != null");
            try
            {
                remoteRunspace.Open();

                // Mark this temporary runspace so that it closes on pop.
                remoteRunspace.ShouldCloseOnPop = true;
            }
            finally
            {
                // Make sure we dispose the temporary runspace if something bad happens.
                if (remoteRunspace.RunspaceStateInfo.State != RunspaceState.Opened)
                {
                    remoteRunspace.Dispose();
                    remoteRunspace = null;
                }
            }

            return remoteRunspace;
        }

        /// <summary>
        /// Set prompt for VM/Container sessions.
        /// </summary>
        private void SetRunspacePrompt(RemoteRunspace remoteRunspace)
        {
            if (IsParameterSetForVM() ||
                IsParameterSetForContainer() ||
                IsParameterSetForVMContainerSession())                
            {
                string targetName = string.Empty;

                switch (ParameterSetName)
                {
                    case VMIdParameterSet:
                    case VMNameParameterSet:
                        targetName = this.VMName;
                        break;

                    case ContainerIdParameterSet:
                    case ContainerNameParameterSet:
                        targetName = this.ContainerName;
                        break;

                    case SessionParameterSet:
                        targetName = (this.Session != null) ? this.Session.ComputerName : string.Empty;
                        break;
                        
                    case InstanceIdParameterSet:
                    case IdParameterSet:
                    case NameParameterSet:
                        if ((remoteRunspace != null) &&
                            (remoteRunspace.ConnectionInfo != null))
                        {
                            targetName = remoteRunspace.ConnectionInfo.ComputerName;
                        }
                        break;

                    default:
                        Dbg.Assert(false, "Unrecognized parameter set.");
                        break;
                }
            
                string promptFn = StringUtil.Format(RemotingErrorIdStrings.EnterVMSessionPrompt,
                    @"function global:prompt { """,
                    targetName,
                    @"PS $($executionContext.SessionState.Path.CurrentLocation)> "" }");
                
                // Set prompt in pushed named pipe runspace.
                using (System.Management.Automation.PowerShell ps = System.Management.Automation.PowerShell.Create())
                {
                    ps.Runspace = remoteRunspace;
                
                    try
                    {
                        // Set pushed runspace prompt.
                        ps.AddScript(promptFn).Invoke();
                    }
                    catch (Exception e)
                    {
                        // Ignore all non-severe errors.
                        CommandProcessorBase.CheckForSevereException(e);
                    }
                }            
            }

            return;
        }

        /// <summary>
        /// Create runspace for container session.
        /// </summary>
        private RemoteRunspace GetRunspaceForContainerSession()
        {
            RemoteRunspace remoteRunspace = null;

            try
            {
                ContainerConnectionInfo connectionInfo = null;

                //
                // Hyper-V container uses Hype-V socket as transport.
                // Windows Server container uses named pipe as transport.
                //
                if (!String.IsNullOrEmpty(ContainerId))
                {
                    connectionInfo = ContainerConnectionInfo.CreateContainerConnectionInfoById(ContainerId, RunAsAdministrator.IsPresent, this.ConfigurationName);
                }
                else
                {
                    Dbg.Assert(!String.IsNullOrEmpty(ContainerName), "Either ContainerId or ContainerName has to be set.");

                    connectionInfo = ContainerConnectionInfo.CreateContainerConnectionInfoByName(ContainerName, RunAsAdministrator.IsPresent, this.ConfigurationName);
                }

                this.ContainerName = connectionInfo.ComputerName;
                connectionInfo.CreateContainerProcess();
                remoteRunspace = CreateTemporaryRemoteRunspaceForPowerShellDirect(this.Host, connectionInfo);
            }
            catch (InvalidOperationException e)
            {
                ErrorRecord errorRecord = new ErrorRecord(e,
                    "CreateRemoteRunspaceForContainerFailed", 
                    ErrorCategory.InvalidOperation,
                    null);
                
                WriteError(errorRecord);
            }
            catch (ArgumentException e)
            {
                ErrorRecord errorRecord = new ErrorRecord(e,
                    "CreateRemoteRunspaceForContainerFailed", 
                    ErrorCategory.InvalidArgument,
                    null);
                
                WriteError(errorRecord);
            }
            catch (PSRemotingDataStructureException e)
            {
                ErrorRecord errorRecord;

                //
                // In case of PSDirectException, we should output the precise error message
                // in inner exception instead of the generic one in outer exception.
                //
                if ((e.InnerException != null) && (e.InnerException is PSDirectException))
                {
                    errorRecord = new ErrorRecord(e.InnerException,
                        "CreateRemoteRunspaceForContainerFailed", 
                        ErrorCategory.InvalidOperation,
                        null);                        
                }
                else
                {
                    errorRecord = new ErrorRecord(e,
                        "CreateRemoteRunspaceForContainerFailed", 
                        ErrorCategory.InvalidOperation,
                        null);
                }

                WriteError(errorRecord);
            }
            catch (Exception e)
            {
                ErrorRecord errorRecord = new ErrorRecord(e,
                    "CreateRemoteRunspaceForContainerFailed", 
                    ErrorCategory.InvalidOperation,
                    null);
                
                WriteError(errorRecord);
            }

            return remoteRunspace;
        }

        #endregion

        #region Internal Methods

        internal static RemotePipeline ConnectRunningPipeline(RemoteRunspace remoteRunspace)
        {
            RemotePipeline cmd = null;
            if (remoteRunspace.RemoteCommand != null)
            {
                // Reconstruct scenario.
                // Newly connected pipeline object is added to the RemoteRunspace running
                // pipeline list.
                cmd = new RemotePipeline(remoteRunspace);
            }
            else
            {
                // Reconnect scenario.
                cmd = remoteRunspace.GetCurrentlyRunningPipeline() as RemotePipeline;
            }

            // Connect the runspace pipeline so that debugging and output data from
            // remote server can continue.
            if (cmd != null &&
                cmd.PipelineStateInfo.State == PipelineState.Disconnected)
            {
                using (ManualResetEvent connected = new ManualResetEvent(false))
                {
                    cmd.StateChanged += (sender, args) =>
                    {
                        if (args.PipelineStateInfo.State != PipelineState.Disconnected)
                        {
                            try
                            {
                                connected.Set();
                            }
                            catch (ObjectDisposedException) { }
                        }
                    };

                    cmd.ConnectAsync();
                    connected.WaitOne();
                }
            }

            return cmd;
        }

        internal static void ContinueCommand(RemoteRunspace remoteRunspace, Pipeline cmd, PSHost host, bool inDebugMode, System.Management.Automation.ExecutionContext context)
        {
            RemotePipeline remotePipeline = cmd as RemotePipeline;

            if (remotePipeline != null)
            {
                using (System.Management.Automation.PowerShell ps = System.Management.Automation.PowerShell.Create())
                {
                    PSInvocationSettings settings = new PSInvocationSettings()
                    {
                        Host = host
                    };

                    PSDataCollection<PSObject> input = new PSDataCollection<PSObject>();

                    CommandInfo commandInfo = new CmdletInfo("Out-Default", typeof(OutDefaultCommand), null, null, context);
                    Command outDefaultCommand = new Command(commandInfo);
                    ps.AddCommand(outDefaultCommand);
                    IAsyncResult async = ps.BeginInvoke<PSObject>(input, settings, null, null);

                    RemoteDebugger remoteDebugger = remoteRunspace.Debugger as RemoteDebugger;
                    if (remoteDebugger != null)
                    {

                        // Update client with breakpoint information from pushed runspace.
                        // Information will be passed to the client via the Debugger.BreakpointUpdated event.
                        remoteDebugger.SendBreakpointUpdatedEvents();

                        if (!inDebugMode)
                        {
                            // Enter debug mode if remote runspace is in debug stop mode.
                            remoteDebugger.CheckStateAndRaiseStopEvent();
                        }
                    }

                    // Wait for debugged cmd to complete.
                    while (!remotePipeline.Output.EndOfPipeline)
                    {
                        remotePipeline.Output.WaitHandle.WaitOne();
                        while (remotePipeline.Output.Count > 0)
                        {
                            input.Add(remotePipeline.Output.Read());
                        }
                    }

                    input.Complete();
                    ps.EndInvoke(async);
                }
            }
        }

        #endregion
    }
}