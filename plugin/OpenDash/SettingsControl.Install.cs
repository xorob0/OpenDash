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
        private TextBlock updateLine;
        private Button updateButton;
        private Button restoreButton;
        private Button checkButton;
        private UpdateStatus updateStatus = new UpdateStatus();
        /// <summary>Whether the answer on its way was asked for by a press, which it owes a sentence either way.</summary>
        private bool askedManually;

        /// <summary>Where an Update pressed on a remembered offer draws its progress, once the listing it asked
        /// for has answered. Null when no such press is waiting.</summary>
        private Border pendingApply;
        private bool applying;

        /// <summary>The question Update or Reinstall has put on the update line, if either has. It outlives the tab,
        /// and does not need forgetting with the controls: a question counts only while its line shows it.</summary>
        private readonly PanelConfirmation confirmation = new PanelConfirmation();

        /// <summary>The plugin's, not the panel's own: the check Init queued and the one this panel asks for are the
        /// same service, so the releases either found are the ones the Update button applies.</summary>
        private UpdateService Updates => plugin.Updates;

        private FrameworkElement BuildInstallTab()
        {
            return Ui.VStack(0, BuildPackageSection(), BuildLightsSection(), BuildPluginSection());
        }
    }
}
