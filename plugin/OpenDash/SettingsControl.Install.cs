// SettingsControl.Install.cs: the Install tab -- what OpenDash can install, what is on disk, and the
// plugin itself with its reinstall and its update check.
//
// The Install tab and the Rig tab are two views of one list, and adding in either place does the same
// thing. Rig is where you go to configure; Install is where you go to see what is on disk and at what
// version.
//
// Each of the three sections is a file of its own beside this one -- .Packages.cs, .Lights.cs and
// .Plugin.cs -- and this file is the shell that orders them. The controls stay here rather than with the
// section that builds them because SettingsControl.ForgetTabControls drops them by name when the tab is
// left, and that list answers for the whole partial class.
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private TextBlock dashboardTitle;
        private Border statusHost;
        private Button reinstallButton;
        private TextBlock reinstallLabel;
        private TextBlock updateLine;
        private Button updateButton;
        private Button restoreButton;
        private Button checkButton;
        private UpdateStatus updateStatus = new UpdateStatus();
        private UpdateService updateService;
        private bool confirmingEdited;
        private bool applying;
        private bool confirmingReinstall;

        private UpdateService Updates =>
            updateService ?? (updateService = new UpdateService(new ReleaseClient(OpenDash.Version), new SimHubInstallLog()));

        private FrameworkElement BuildInstallTab()
        {
            return Ui.VStack(0, BuildPackageSection(), BuildLightsSection(), BuildPluginSection());
        }
    }
}
