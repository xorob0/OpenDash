// SettingsControl.Updates.Plugin.cs: the "This plugin" section of the Updates page -- the version on disk,
// the status pill, the reinstall, the daily update check and what applying one does.
//
// The controls it writes into are declared in SettingsControl.Updates.cs, which drops them when the page is
// left. Asking for a check and taking its answer are the shell's (SettingsControl.Live.cs), since the
// sidebar's badge reads the answer on every page; this section draws it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SimHub.Plugins.Styles;
// Aliased rather than imported: System.Windows.Forms carries a Button of its own, and this file is
// full of WPF ones. SHMessageBox answers with the Forms enum whatever the dialog it draws.
using DialogResult = System.Windows.Forms.DialogResult;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildPluginSection()
        {
            dashboardTitle = Ui.Body("OpenDash");
            var caption = Ui.Caption("Reinstall writes every dashboard on your rig again. Your settings are kept.");
            var text = Ui.VStack(4, dashboardTitle, caption);
            text.MaxWidth = 460;
            text.HorizontalAlignment = HorizontalAlignment.Left;

            updateLine = Ui.Caption(string.Empty);
            updateLine.Visibility = Visibility.Collapsed;
            text.Children.Add(updateLine);

            // The bar is held by the button that starts a run rather than by a field, because a field would
            // have to be dropped in SettingsControl.ForgetTabControls with the rest and a run that finished
            // after the tab was left would otherwise draw into a column nobody is looking at. The button and
            // the bar are built together and go together.
            var progressHost = new Border
            {
                Visibility = Visibility.Collapsed,
                // The 8 the component puts between its own two halves, which is the only rhythm the canvas
                // gives this block; the VStack above applies its gap at construction and this arrives after.
                Margin = new Thickness(0, PanelMetrics.ProgressGap, 0, 0),
            };
            text.Children.Add(progressHost);

            statusHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            reinstallButton = BuildReinstallButton();
            updateProgressHost = progressHost;
            updateButton = BuildUpdateButton(progressHost);
            restoreButton = BuildRestoreButton();
            var right = Ui.HStack(24, statusHost, restoreButton, updateButton, reinstallButton);

            var section = PageSection(PanelUpdates.PluginTitle, Ui.Row(text, right), Ui.Anchor(BuildCheckRow(), PanelUpdates.AnchorCheck));
            RefreshStatus();
            RefreshUpdateLine();
            RefreshRestoreButton();
            return section;
        }

        /// <summary>The switch, its one sentence, and a button for somebody who would rather ask now.</summary>
        private FrameworkElement BuildCheckRow()
        {
            var toggle = BuildToggle(Settings.CheckForUpdates, on =>
            {
                Settings.CheckForUpdates = on;
                Save();
                if (!on)
                {
                    updateStatus = new UpdateStatus { State = UpdateState.Disabled, InstalledVersion = plugin.RigVersion };
                }
                RefreshUpdateLine();
            });

            checkButton = BuildSecondaryButton("Check now", "Check for a new release now.");
            checkButton.Click += (sender, args) => Check(manual: true);

            var right = Ui.HStack(24, checkButton, toggle);
            return Ui.Row("Check for updates", UpdateWording.CheckCaption, right);
        }

        private Button BuildUpdateButton(Border progressHost)
        {
            var button = BuildSecondaryButton(null, "Download the newest release.");
            button.SetBinding(ContentControl.ContentProperty, LabelFromTheLine(ReplacingAction.Update));
            button.Visibility = Visibility.Collapsed;
            button.Click += (sender, args) => ApplyUpdate(progressHost);
            return button;
        }

        /// <summary>
        /// A binding that draws Update's or Reinstall's label from the confirmation each time the update line changes.
        /// </summary>
        /// <remarks>
        /// Bound to the line rather than set beside each sentence written to it, because the question is on the line
        /// and a label set by hand outlived it (#480): Reinstall wrote its own sentence over Update's question and left
        /// Update reading "Replace anyway". With the label a function of the line, whatever takes the line, a writer
        /// added later included, turns it back into the button's verb, and it reads "Replace anyway" only while its own
        /// question is what the line shows. The line is built before either button, so it is there to bind to.
        /// </remarks>
        private Binding LabelFromTheLine(ReplacingAction action)
        {
            return new Binding(nameof(TextBlock.Text))
            {
                Source = updateLine,
                Mode = BindingMode.OneWay,
                Converter = new ConfirmationLabel(confirmation, action),
            };
        }

        /// <summary>The converter behind <see cref="LabelFromTheLine"/>: the line's text in, the button's label out.</summary>
        private sealed class ConfirmationLabel : IValueConverter
        {
            private readonly PanelConfirmation confirmation;
            private readonly ReplacingAction action;

            public ConfirmationLabel(PanelConfirmation confirmation, ReplacingAction action)
            {
                this.confirmation = confirmation;
                this.action = action;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return confirmation.Label(action, value as string);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Puts back the copy kept when a dashboard somebody edited was replaced.
        /// </summary>
        /// <remarks>
        /// The confirmation before replacing an edited dashboard promises that a copy is kept and can be put back.
        /// Until this existed nothing in the plugin could put one back, so the promise was true only for somebody
        /// willing to unzip a file by hand.
        /// </remarks>
        private Button BuildRestoreButton()
        {
            var button = BuildSecondaryButton("Put mine back", "Restore the dashboards your last update replaced.");
            button.Visibility = Visibility.Collapsed;
            button.Click += (sender, args) => RestoreKept();
            return button;
        }

        private void RestoreKept()
        {
            if (applying) return;
            var root = plugin.Installer.SimHubRoot;
            var restored = new List<string>();
            foreach (var folder in plugin.Installer.Packages.Select(p => p.FolderName).Where(f => f != null).Distinct())
            {
                var kept = PackageExtractor.KeptCopies(root, folder).FirstOrDefault(path => path.Contains(PackageExtractor.EditedSuffix));
                if (kept == null) continue;
                try
                {
                    if (PackageExtractor.Restore(root, folder, new SimHubInstallLog(), kept)) restored.Add(folder);
                }
                catch (Exception ex)
                {
                    Log.Error("Putting back " + folder + " failed", ex);
                }
            }
            plugin.Installer.Refresh();
            Save();
            RefreshStatus();
            if (updateLine == null) return;
            updateLine.Text = restored.Count == 0
                ? "There was nothing to put back."
                : "Put back " + (restored.Count == 1 ? "1 dashboard" : restored.Count + " dashboards") + ". " + UpdateWording.Reopen;
            updateLine.Visibility = Visibility.Visible;
            RefreshRestoreButton();
        }

        /// <summary>The button appears only when there is something of the user's to put back.</summary>
        private void RefreshRestoreButton()
        {
            if (restoreButton == null) return;
            var root = plugin.Installer.SimHubRoot;
            var any = plugin.Installer.Packages
                .Select(p => p.FolderName)
                .Where(f => f != null)
                .Distinct()
                .Any(f => PackageExtractor.KeptCopies(root, f).Any(path => path.Contains(PackageExtractor.EditedSuffix)));
            restoreButton.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Applies the release the last check found, asking once before replacing a dashboard somebody has edited.
        /// </summary>
        private void ApplyUpdate(Border progressHost)
        {
            // A second click before the first has been answered used to fall straight through the confirmation,
            // because the confirming branch returned without disabling anything.
            if (applying) return;

            var release = Updates.LastReleases.FirstOrDefault(r => r.Version == updateStatus.LatestVersion);
            if (release == null && updateStatus.State == UpdateState.UpdateAvailable && !applyWaiting)
            {
                // The offer is the remembered one (UpdateMark.Opening): the release is known and its assets are
                // not, since a download URL expires within the hour and none is kept between runs. The press is
                // a request, so the listing is asked for now and the update applied when it answers.
                applyWaiting = true;
                if (Check(manual: true)) return;
                applyWaiting = false;
            }
            if (release == null)
            {
                // Nothing to act on, so the line says so and the button is put back where the status says it
                // belongs, which is how a button left over from an earlier state disappears on the press that
                // found it stale rather than staying to be pressed again.
                RefreshUpdateLine();
                if (updateLine == null) return;
                updateLine.Text = UpdateWording.NothingToApply;
                updateLine.Visibility = Visibility.Visible;
                return;
            }

            // Read from the disk at the press rather than from whenever the installer last looked, since a dashboard
            // edited in Dash Studio while the question is open is one the question did not name, and the second press
            // has to see it in order to ask again.
            plugin.Installer.Refresh();
            var edited = plugin.Installer.EditedFolders;
            var question = UpdateWording.ReplaceEditedQuestion(edited, onRestart: release.PluginAsset() != null);
            var press = confirmation.Press(ReplacingAction.Update, edited, question, updateLine.Text);
            if (press == PressOutcome.Ask)
            {
                // One click to be told, a second to mean it. A dialog would be the SimHub way and a modal in a
                // settings page is worse than a button that changes what it says, which it does of itself once the
                // question is on the line.
                updateLine.Text = question;
                updateLine.Visibility = Visibility.Visible;
                return;
            }

            var replaceEdited = press == PressOutcome.RunReplacingEdited;
            applying = true;
            if (updateButton != null) updateButton.IsEnabled = false;
            if (reinstallButton != null) reinstallButton.IsEnabled = false;
            if (checkButton != null) checkButton.IsEnabled = false;
            updateLine.Text = "Downloading " + updateStatus.LatestVersion + "…";
            updateLine.Visibility = Visibility.Visible;
            progressHost.Child = Ui.Progress(0);
            progressHost.Visibility = Visibility.Visible;

            // The run reports per chunk of a several-megabyte download, which is thousands of calls, and every
            // one of them crosses to the interface thread. Only a whole percent is drawn, so only a whole
            // percent is sent: the bar is redrawn exactly when the number above it would change, which caps the
            // crossings at a hundred and one for the run. BeginInvoke rather than Invoke, because a download
            // that waited for the panel to paint would be paced by the panel.
            var shown = -1;
            Action<double> report = fraction =>
            {
                var percent = PanelMetrics.PercentOf(fraction);
                if (percent == shown) return;
                shown = percent;
                Dispatcher.BeginInvoke(new Action(() => progressHost.Child = Ui.Progress(fraction)));
            };

            UpdateService.InBackground(() =>
            {
                var outcome = Updates.Apply(plugin.Installer, release, replaceEdited, report);
                Dispatcher.Invoke(() =>
                {
                    applying = false;
                    progressHost.Visibility = Visibility.Collapsed;
                    progressHost.Child = null;
                    if (updateButton != null) updateButton.IsEnabled = true;
                    if (reinstallButton != null) reinstallButton.IsEnabled = true;
                    if (checkButton != null) checkButton.IsEnabled = true;
                    // An update writes the stock folders from their packages, so it brings the packages'
                    // own titles with it; the names the driver gave their screens go back on top before
                    // anything is saved. Nothing is rewritten where the title already reads that way.
                    var titles = new SimHubInstallLog();
                    foreach (var screen in Settings.RigScreens()) ScreenInstaller.Retitle(screen, plugin.Installer.SimHubRoot, plugin.Installer.Record, titles);
                    // The yes to replacing edited dashboards is spent by the next start, not by this run, when
                    // the dashboards come inside the plugin; it is saved with the rest just below.
                    if (outcome.ReplaceEditedOnRestart) Settings.ReplaceEditedFor = release.Version;
                    // The record is written in memory by the installer and saved here, on the UI thread, which is
                    // the moment it is safe to serialise the settings.
                    Save();
                    plugin.Installer.Refresh();
                    // The idle screen's mark compares the release it offers with what the rig now runs, and
                    // the dashboards have just moved.
                    plugin.RefreshUpdateMark();
                    RefreshRestoreButton();
                    updateStatus = UpdateMark.Applied(updateStatus, outcome.Ok, release.Version, plugin.RigVersion);
                    RefreshStatus();
                    // The button's visibility is computed nowhere but here, so a status that has just stopped
                    // offering an update has to be redrawn or the button outlives the release it was offering.
                    // It runs before the outcome sentence is written because it writes the line as well.
                    RefreshUpdateLine();
                    if (updateLine != null)
                    {
                        updateLine.Text = outcome.Line;
                        updateLine.Visibility = Visibility.Visible;
                    }
                    // The one thing the run cannot do for itself. Asked here rather than before the
                    // download, because until the assembly is staged there is nothing for a restart to
                    // put in place, and asked at all because the sentence above it was not enough: see
                    // UpdateWording.RestartTitle.
                    // The sidebar's badge and Home read the same answer.
                    RefreshAttention();
                    RefreshSidebar();
                    if (outcome.PluginStaged) OfferRestart(release.Version);
                });
            }, new SimHubInstallLog(), mustFinish: true);
        }

        /// <summary>
        /// Asks whether to close SimHub now, and closes it if the answer is yes.
        /// </summary>
        /// <remarks>
        /// `async void` because it is the tail of a UI callback and there is nothing to await it; the
        /// try/catch is what stops an unobserved exception on that path taking SimHub with it.
        ///
        /// The reopen is asked for before the window is closed and not after, because after is a process
        /// that no longer exists. The swap script reads the request when it finally runs and consumes it
        /// either way, so a driver who says no here never gets a SimHub that reopens itself later.
        ///
        /// `Application.Current.Shutdown()` rather than closing the main window: SimHub can be set to go
        /// to the tray on close, and a driver who has just been asked "close SimHub now?" and said yes
        /// would otherwise watch it minimise and nothing else happen.
        /// </remarks>
        private async void OfferRestart(string version)
        {
            var root = plugin.Installer.SimHubRoot;
            try
            {
                var answer = await SHMessageBox.Show(
                    UpdateWording.RestartQuestion(version),
                    UpdateWording.RestartTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                var now = answer == DialogResult.Yes;
                PluginUpdate.AskToReopen(root, now);
                if (updateLine != null)
                {
                    updateLine.Text = now ? UpdateWording.RestartGoing : UpdateWording.RestartLater;
                    updateLine.Visibility = Visibility.Visible;
                }
                if (!now) return;
                Save();
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Warn("Asking about the restart failed, so the plugin swap waits for an ordinary close: " + ex.Message);
                PluginUpdate.AskToReopen(root, false);
                if (updateLine == null) return;
                updateLine.Text = UpdateWording.RestartFailed;
                updateLine.Visibility = Visibility.Visible;
            }
        }

        private void RefreshUpdateLine()
        {
            if (updateLine == null) return;
            var line = updateStatus.Line;
            // A staged assembly outlives the tab, the panel and the check, so the section says so every
            // time it is drawn rather than only in the moment the download finished. Without this, a
            // driver who says "later" and comes back tomorrow sees a plugin section that looks entirely
            // ordinary and is running the old plugin.
            //
            // It also outranks "a newer version is available": since #438 an update stages the plugin and
            // writes no dashboard, so the status that offered the release is still standing afterwards, and
            // a button left under it would download the same plugin again rather than finish anything.
            var pending = PluginUpdate.Pending(plugin.Installer.SimHubRoot);
            if (pending && (line == null || updateStatus.State == UpdateState.UpdateAvailable)) line = UpdateWording.RestartLater;
            updateLine.Text = line ?? string.Empty;
            updateLine.Visibility = line != null && (updateStatus.IsVisible || updateStatus.Line == null) ? Visibility.Visible : Visibility.Collapsed;
            if (updateButton != null)
            {
                updateButton.Visibility = updateStatus.State == UpdateState.UpdateAvailable && !pending ? Visibility.Visible : Visibility.Collapsed;
            }
            // The pill reads this line's answer as well as the disk's, so it is refreshed with it.
            RefreshStatus();
        }

        /// <summary>
        /// An outline button carrying the refresh icon, which is how Plugin.dc.html draws this row.
        /// </summary>
        /// <remarks>
        /// The component sheet uses the word "Reinstall" as the label on its primary swatches, but the
        /// page itself draws this button transparent inside ui.border, so the swatch is a specimen and
        /// the page is the instruction. The accented press the tab would spend on it is not owed here:
        /// somebody opens Install to see what is on disk, and writing it all again is the repair.
        ///
        /// The label is kept as a block of its own because the confirmation rewrites it; replacing the
        /// button's whole Content, which is what it did while the label was a bare string, would drop
        /// the icon beside it and never put it back.
        /// </remarks>
        private Button BuildReinstallButton()
        {
            var button = Ui.OutlineButton(null);
            button.MinWidth = ButtonMinWidth;
            button.ToolTip = "Install every dashboard on your rig again.";
            var label = Ui.Text(string.Empty, Theme.SizeBody, FontWeights.Medium, Theme.TextPrimary);
            label.SetBinding(TextBlock.TextProperty, LabelFromTheLine(ReplacingAction.Reinstall));
            button.Content = Ui.HStack(PanelMetrics.ButtonIconGap, Ui.Icon(PanelIcons.Refresh, Theme.TextPrimary), label);
            button.Click += (sender, args) => Reinstall();
            return button;
        }

        /// <summary>
        /// Writes every dashboard the rig has again, asking once before replacing one somebody has edited.
        /// </summary>
        /// <remarks>
        /// It used to leave an edited folder alone and report "Up to date", which made the button appear to have
        /// worked while nothing happened. Worse, the only path that could replace an edited folder was the Update
        /// button's second click, and that button appears only while a newer release exists, so a person who had
        /// edited a dashboard had no way at all to get OpenDash's own version back.
        /// </remarks>
        private void Reinstall()
        {
            // Two installers over the same DashTemplates folders is the one combination that can delete a folder
            // one of them is extracting into, so whichever starts first holds the field.
            if (applying) return;

            // From the disk at the press, for the reason ApplyUpdate reads it there.
            plugin.Installer.Refresh();
            var edited = plugin.Installer.EditedFolders;
            var question = "You have edited " + (edited.Count == 1 ? "1 dashboard" : edited.Count + " dashboards")
                + ": " + string.Join(", ", edited)
                + ". Reinstalling replaces your version. A copy is kept, and \"Put mine back\" restores it.";
            var press = confirmation.Press(ReplacingAction.Reinstall, edited, question, updateLine.Text);
            if (press == PressOutcome.Ask)
            {
                updateLine.Text = question;
                updateLine.Visibility = Visibility.Visible;
                return;
            }

            var replaceEdited = press == PressOutcome.RunReplacingEdited;
            reinstallButton.IsEnabled = false;
            try
            {
                // Every screen on the rig, a second one of a size included: the installer reads the rig and
                // writes each folder with the screen's own name and namespace, so there is nothing to add after.
                var writing = DateTime.UtcNow;
                plugin.Installer.EnsureInstalled(true, replaceEdited);
                var replaced = plugin.Installer.Packages.Count(p => p.Extracted);
                var held = plugin.Installer.Packages.Count(p => p.HeldBack);
                // A font this press put into DashFonts is not drawn until SimHub restarts, so reopening, which is
                // enough for everything else a reinstall writes, would leave that face missing.
                var wroteFonts = PackageExtractor.FacesWrittenSince(plugin.Installer.SimHubRoot, writing) > 0;
                updateLine.Text = held > 0
                    ? "Reinstalled " + replaced + ". " + held + " left alone: you have edited them." + (wroteFonts ? " " + UpdateWording.RestartToSee : string.Empty)
                    : "Reinstalled " + replaced + (replaced == 1 ? " dashboard. " : " dashboards. ") + UpdateWording.ToSee(wroteFonts);
                updateLine.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Log.Error("Reinstall failed", ex);
                updateLine.Text = "The reinstall did not finish: " + ex.Message;
                updateLine.Visibility = Visibility.Visible;
            }
            finally
            {
                // The kit draws a disabled button at the canvas's 40 per cent through a template trigger,
                // so putting IsEnabled back is the whole of the re-enable: nothing here dimmed it by hand.
                if (reinstallButton != null) reinstallButton.IsEnabled = true;
                Save();
                RefreshStatus();
                RefreshRestoreButton();
            }
        }

        /// <summary>Title "OpenDash <the version the rig runs>" and the status pill: a 6 px dot and a tracked label
        /// showing the worst status across the packages; the tooltip lists every dashboard with its own status.</summary>
        private void RefreshStatus()
        {
            if (dashboardTitle == null || statusHost == null) return;
            var installer = plugin.Installer;
            // The version the update line under it names, so that the two cannot disagree.
            dashboardTitle.Text = DashboardInstaller.Summary(plugin.RigVersion);

            // The worse of the two questions this section answers: what is on the disk against what
            // the build carries, and what the daily check found waiting. Reading the first alone told a
            // driver they were up to date directly above a line saying a newer release was there.
            var status = DashboardInstaller.PillStatus(installer.Status, updateStatus.State);

            string dot;
            var label = Theme.TextPrimary;
            switch (status)
            {
                case InstallStatus.UpToDate:
                    dot = Theme.StatusUpToDate;
                    break;
                case InstallStatus.UpdateAvailable:
                    dot = Theme.StatusUpdateAvailable;
                    break;
                case InstallStatus.Failed:
                    dot = Theme.StatusFailed;
                    break;
                default:
                    dot = Theme.StatusNotInstalled;
                    label = Theme.TextLabel;
                    break;
            }
            statusHost.Child = Ui.StatusPill(dot, status.Label(), label);
            var report = installer.HasEmbeddedPackage
                ? (installer.Packages.Count > 0 ? installer.PackageReport() : installer.LastError)
                : "This build of OpenDash ships no dashboards.";
            // The release's own version belongs in the tooltip when the offer is what turned the pill:
            // the pill says an update is available and the tooltip says which.
            statusHost.ToolTip = updateStatus.State == UpdateState.UpdateAvailable && updateStatus.Line != null ? $"{report}\n{updateStatus.Line}" : report;
        }
    }
}
