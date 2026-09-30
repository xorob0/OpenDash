// SettingsControl.Updates.cs: the Updates page -- what the Install tab was for: the plugin's version, the daily
// check and applying an update, Reinstall and Put mine back; what OpenDash has written into SimHub and at which
// version; and the links.
//
// Re-hosted by the #503 foundation so that every control keeps working while the Updates page agent rebuilds it
// to Updates.dc.html. Its sections are the files beside this one -- .Updates.Plugin.cs, .Updates.Packages.cs
// and .Updates.Lights.cs -- and this file is the page that orders them and the controls they share. The update
// check's own state is the shell's (SettingsControl.Live.cs), because the sidebar's badge and Home read it on
// every page; this page draws it and hears an answer through OnUpdate.
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

        /// <summary>Whether an Update pressed on a remembered offer is waiting for the listing it asked for.
        /// Held apart from the build's controls, so a rebuild in place (night mode, a resize) keeps the press;
        /// only leaving the page drops it.</summary>
        private bool applyWaiting;

        /// <summary>Where this build draws an update's progress.</summary>
        private Border updateProgressHost;

        /// <summary>The run that is downloading, held outside the build so a rebuild in place -- a resize, the
        /// return to the panel, a lighting change -- draws it again rather than an idle section with live
        /// buttons: the line it said and the fraction it last reported. Null when nothing is running.</summary>
        private string applyingLine;
        private double applyingFraction;

        /// <summary>The question Update or Reinstall has put on the update line, if either has. It outlives the
        /// page, and does not need forgetting with the controls: a question counts only while its line shows it.</summary>
        private readonly PanelConfirmation confirmation = new PanelConfirmation();

        private FrameworkElement BuildUpdatesPage(PanelRoute to)
        {
            // Every control the build holds goes with it, so an answer or a finished run that lands after the
            // page has gone writes nowhere rather than into a control nobody can see.
            OnDrop(() =>
            {
                dashboardTitle = null;
                statusHost = null;
                reinstallButton = null;
                updateLine = null;
                updateButton = null;
                restoreButton = null;
                checkButton = null;
                updateProgressHost = null;
            });
            OnLeave("Updates.applyWaiting", () => applyWaiting = false);
            OnUpdate(
                () =>
                {
                    if (checkButton != null) checkButton.IsEnabled = false;
                    RefreshUpdateLine();
                },
                manual => UpdateAnswered());

            var links = Ui.HStack(24, BuildLink(PanelUpdates.DocumentationLink, DocumentationUrl), BuildLink(PanelUpdates.ReportIssueLink, IssuesUrl), Ui.Label("MIT licence"));
            return PageLayout(PanelUpdates.Title, null,
                Ui.Anchor(BuildPluginSection(), PanelUpdates.AnchorPlugin),
                Ui.Anchor(BuildPackageSection(), PanelUpdates.AnchorPackages),
                Ui.Anchor(BuildLightsSection(), PanelUpdates.AnchorLights),
                Ui.Anchor(PageSection(PanelUpdates.LinksTitle, links), PanelUpdates.AnchorLinks));
        }

        /// <summary>A check's answer, which the shell has already taken: the line, the button, and an Update
        /// pressed on the remembered offer that was waiting for the listing.</summary>
        private void UpdateAnswered()
        {
            if (checkButton != null) checkButton.IsEnabled = !applying;
            RefreshUpdateLine();
            var waiting = applyWaiting;
            applyWaiting = false;
            if (waiting && updateStatus.State == UpdateState.UpdateAvailable && updateButton != null && updateProgressHost != null) ApplyUpdate();
        }
    }
}
