using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Workflow.Activities;
using Composite.C1Console.Actions;
using Composite.C1Console.Events;
using Composite.C1Console.Users;
using Composite.C1Console.Workflow;
using Composite.Core.Configuration;
using Composite.Core.Logging;
using Composite.Core.PackageSystem;
using Composite.Core.ResourceSystem;


namespace Composite.Plugins.Elements.ElementProviders.PackageElementProvider
{
    /// <exclude />
    [Obsolete("Is used while processing upgrade packages from C1 3.1 and older. To be removed once lower requirement for upgrade package is at least v3.2")]
    [AllowPersistingWorkflow(WorkflowPersistingType.Idle)]
    public sealed partial class InstallRemotePackageWorkflow : Composite.C1Console.Workflow.Activities.FormsWorkflow
    {
        bool _packageIsFree = false;

        /// <exclude />
        public InstallRemotePackageWorkflow()
        {
            InitializeComponent();
        }



        private PackageDescription GetPackageDescription()
        {
            PackageElementProviderAvailablePackagesItemEntityToken castedEntityToken = (PackageElementProviderAvailablePackagesItemEntityToken)this.EntityToken;

            PackageDescription packageDescription =
                (from description in PackageSystemServices.GetFilteredAllAvailablePackages()
                 where description.Id == castedEntityToken.PackageId
                 select description).SingleOrDefault();

            if (packageDescription == null)
            {
                this.UpdateBinding("ServerError", true);
            }

            return packageDescription;
        }



        private void IsPackageFree(object sender, ConditionalEventArgs e)
        {
            e.Result = _packageIsFree;
        }



        private void EulaAccepted(object sender, ConditionalEventArgs e)
        {
            e.Result = this.GetBinding<bool>("EulaAccepted");
        }



        private void DidValidate(object sender, System.Workflow.Activities.ConditionalEventArgs e)
        {
            e.Result = this.BindingExist("Errors") == false;
        }



        private void initializeStateCodeActivity_Initialize_ExecuteCode(object sender, EventArgs e)
        {
            this.UpdateBinding("LayoutLabel", StringResourceSystemFacade.GetString("Composite.Plugins.PackageElementProvider", "InstallRemotePackage.ShowError.LayoutLabel"));
            this.UpdateBinding("TableCaption", StringResourceSystemFacade.GetString("Composite.Plugins.PackageElementProvider", "InstallRemotePackage.ShowError.InfoTableCaption"));

            try
            {
                _packageIsFree = GetPackageDescription().IsFree;
            }
            catch (Exception ex)
            {
                LoggingService.LogVerbose("InstallRemotePackageWorkflowRGB(100, 100, 255)", ex.Message);
                this.UpdateBinding("Errors", new List<List<string>> { new List<string> { ex.Message, "" } });
            }
        }



        private void step2StateStepcodeActivity_Initialize_ExecuteCode(object sender, EventArgs e)
        {
            try
            {
                if (this.BindingExist("EulaText") == false)
                {
                    PackageDescription packageDescription = GetPackageDescription();
                    string eulaText = PackageSystemServices.GetEulaText(packageDescription);
                    this.Bindings.Add("EulaText", eulaText);
                }

                if (this.BindingExist("EulaAccepted") == false)
                {
                    this.Bindings.Add("EulaAccepted", false);
                }

            }
            catch (Exception ex)
            {
                this.UpdateBinding("Errors", new List<List<string>> { new List<string> { ex.Message, "" } });
            }
        }



        private void step3CodeActivity_DownloadAndValidate_ExecuteCode(object sender, EventArgs e)
        {
            try
            {
                PackageDescription packageDescription = GetPackageDescription();

                string packageServerSource = PackageSystemServices.GetPackageSourceNameByPackageId(packageDescription.Id, InstallationInformationFacade.InstallationId, UserSettings.CultureInfo);

                System.IO.Stream installFileStream = PackageServerFacade.GetInstallFileStream(packageDescription.PackageFileDownloadUrl);

                PackageManagerInstallProcess packageManagerInstallProcess = PackageManager.Install(installFileStream, false, packageServerSource);
                this.Bindings.Add("PackageManagerInstallProcess", packageManagerInstallProcess);

                this.Bindings.Add("FlushOnCompletion", packageManagerInstallProcess.FlushOnCompletion);
                this.Bindings.Add("ReloadConsoleOnCompletion", packageManagerInstallProcess.ReloadConsoleOnCompletion);

                if (packageManagerInstallProcess.PreInstallValidationResult.Count > 0)
                {
                    this.UpdateBinding("Errors", WorkflowHelper.ValidationResultToBinding(packageManagerInstallProcess.PreInstallValidationResult));
                }
                else
                {
                    List<PackageFragmentValidationResult> validationResult = packageManagerInstallProcess.Validate();

                    if (validationResult.Count > 0)
                    {
                        this.UpdateBinding("LayoutLabel", StringResourceSystemFacade.GetString("Composite.Plugins.PackageElementProvider", "InstallRemotePackage.ShowWarning.LayoutLabel"));
                        this.UpdateBinding("TableCaption", StringResourceSystemFacade.GetString("Composite.Plugins.PackageElementProvider", "InstallRemotePackage.ShowWarning.InfoTableCaption"));
                        this.UpdateBinding("Errors", WorkflowHelper.ValidationResultToBinding(validationResult));
                    }
                    else
                    {
                        this.UpdateBinding("Uninstallable", packageManagerInstallProcess.CanBeUninstalled == false);
                    }
                }
            }
            catch (Exception ex)
            {
                this.UpdateBinding("Errors", new List<List<string>> { new List<string> { ex.Message, "" } });
            }
        }



        private void showErrorCodeActivity_Initialize_ExecuteCode(object sender, EventArgs e)
        {
            List<string> rowHeader = new List<string>();
            rowHeader.Add(StringResourceSystemFacade.ParseString("${Composite.Plugins.PackageElementProvider, InstallRemotePackage.ShowError.MessageTitle}"));

            this.UpdateBinding("ErrorHeader", rowHeader);
        }



        private void step4CodeActivity_Install_ExecuteCode(object sender, EventArgs e)
        {
            PackageDescription packageDescription = GetPackageDescription();

            PackageManagerInstallProcess packageManagerInstallProcess = this.GetBinding<PackageManagerInstallProcess>("PackageManagerInstallProcess");

            bool installOk = false;
            string packageServerUrl = null;
            Exception exception = null;
            try
            {
                packageServerUrl = PackageSystemServices.GetPackageSourceNameByPackageId(packageDescription.Id, InstallationInformationFacade.InstallationId, UserSettings.CultureInfo);

                List<PackageFragmentValidationResult> installResult = packageManagerInstallProcess.Install();
                if (installResult.Count > 0)
                {
                    this.UpdateBinding("Errors", WorkflowHelper.ValidationResultToBinding(installResult));
                }
                else
                {
                    installOk = true;
                }
            }
            catch (Exception ex)
            {
                exception = ex;

                this.UpdateBinding("Errors", new List<List<string>> { new List<string> { ex.Message, "" } });
            }

            try
            {
                if (installOk)
                {
                    PackageServerFacade.RegisterPackageInstallationCompletion(packageServerUrl, InstallationInformationFacade.InstallationId, packageDescription.Id, UserSettings.Username, UserSettings.UserIPAddress.ToString());
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    if (exception != null)
                    {
                        sb.Append(exception.ToString());
                    }
                    else
                    {
                        List<List<string>> errors = this.GetBinding<List<List<string>>>("Errors");
                        foreach (List<string> list in errors)
                        {
                            sb.AppendLine(list[0]);
                        }
                    }

                    PackageServerFacade.RegisterPackageInstallationFailure(packageServerUrl, InstallationInformationFacade.InstallationId, packageDescription.Id, UserSettings.Username, UserSettings.UserIPAddress.ToString(), sb.ToString());
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning("InstallRemotePackageWorkflow", ex);
            }
        }



        private void cleanupCodeActivity_Cleanup_ExecuteCode(object sender, EventArgs e)
        {
            PackageManagerInstallProcess packageManagerInstallProcess;
            if (this.TryGetBinding<PackageManagerInstallProcess>("PackageManagerInstallProcess", out packageManagerInstallProcess))
            {
                packageManagerInstallProcess.CancelInstallation();
            }
        }



        private void step5CodeActivity_RefreshTree_ExecuteCode(object sender, EventArgs e)
        {
            if (this.GetBinding<bool>("ReloadConsoleOnCompletion"))
            {
                ConsoleMessageQueueFacade.Enqueue(new RebootConsoleMessageQueueItem(), null);
            }

            if (this.GetBinding<bool>("FlushOnCompletion"))
            {
                GlobalEventSystemFacade.FlushTheSystem();
            }

            SpecificTreeRefresher specificTreeRefresher = this.CreateSpecificTreeRefresher();
            specificTreeRefresher.PostRefreshMesseges(new PackageElementProviderRootEntityToken());

            if (this.GetBinding<bool>("ReloadConsoleOnCompletion") == false)
            {

                PackageElementProviderAvailablePackagesItemEntityToken castedEntityToken = (PackageElementProviderAvailablePackagesItemEntityToken)this.EntityToken;

                InstalledPackageInformation installedPackage = PackageManager.GetInstalledPackages().FirstOrDefault(f => f.Id == castedEntityToken.PackageId);

                var installedPackageEntityToken = new PackageElementProviderInstalledPackageItemEntityToken(
                    installedPackage.Id,
                    installedPackage.GroupName,
                    installedPackage.IsLocalInstalled,
                    installedPackage.CanBeUninstalled);

                ExecuteWorklow(installedPackageEntityToken, WorkflowFacade.GetWorkflowType("Composite.Plugins.Elements.ElementProviders.PackageElementProvider.ViewInstalledPackageInfoWorkflow"));
            }

        }
    }
}

