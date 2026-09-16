// SettingsControl.Install.cs: the Install tab -- what openDash can install, what is on disk, and the
// plugin itself with its reinstall and its update check.
//
// The Install tab and the Rig tab are two views of one list, and adding in either place does the same
// thing. Rig is where you go to configure; Install is where you go to see what is on disk and at what
// version.
using System;
using System.Collections.Generic;
using System.Linq;
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
            return Ui.VStack(0, BuildPackageSection(), BuildPluginSection());
        }

        private FrameworkElement BuildPluginSection()
        {
            dashboardTitle = Ui.Body("OpenDash");
            var caption = Ui.Caption("Reinstall writes every dashboard your rig has again. Your settings are kept.");
            var text = Ui.VStack(4, dashboardTitle, caption);
            text.MaxWidth = 460;
            text.HorizontalAlignment = HorizontalAlignment.Left;

            updateLine = Ui.Caption(string.Empty);
            updateLine.Visibility = Visibility.Collapsed;
            text.Children.Add(updateLine);

            statusHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            reinstallButton = BuildReinstallButton();
            updateButton = BuildUpdateButton();
            restoreButton = BuildRestoreButton();
            var right = Ui.HStack(24, statusHost, restoreButton, updateButton, reinstallButton);

            var section = Ui.Section("This plugin", Ui.Row(text, right), BuildCheckRow());
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
                    updateStatus = new UpdateStatus { State = UpdateState.Disabled, InstalledVersion = plugin.Installer.InstalledVersion };
                }
                RefreshUpdateLine();
            });

            checkButton = BuildSecondaryButton("Check now", "Ask GitHub for the newest release now, without waiting for the daily check.");
            checkButton.Click += (sender, args) => Check(manual: true);

            var right = Ui.HStack(24, checkButton, toggle);
            return Ui.Row(Ui.VStack(4, Ui.Body("Check for updates"), Ui.Caption(UpdateWording.CheckCaption)), right);
        }

        private Button BuildUpdateButton()
        {
            var button = BuildSecondaryButton("Update", "Download the newest release and replace the dashboards installed here.");
            button.Visibility = Visibility.Collapsed;
            button.Click += (sender, args) => ApplyUpdate();
            return button;
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
            var button = BuildSecondaryButton("Put mine back", "Restore the dashboards that were replaced when you last chose to update over your own edits.");
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
                ? "There was nothing of yours to put back."
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
        /// Asks, off the UI thread, and shows whatever came back.
        /// </summary>
        /// <remarks>
        /// Nothing here blocks: a socket that never answers would otherwise freeze the settings page, and on the
        /// thread SimHub calls Init on it would freeze SimHub's start. The answer may land after the tab it was
        /// asked from has gone, so every control it writes to is checked first.
        /// </remarks>
        private void Check(bool manual)
        {
            if (!Settings.CheckForUpdates && !manual) return;
            // Whether a request will be made is decided here rather than on the background thread, because saying
            // "Checking for updates…" and then not checking left the panel on that sentence for as long as it was
            // open, and hid an offer it had already found.
            if (!UpdateCheck.ShouldCheck(Settings.CheckForUpdates, Settings.LastUpdateCheckTicks, DateTime.UtcNow, manual)) return;

            if (checkButton != null) checkButton.IsEnabled = false;
            updateStatus = new UpdateStatus { State = UpdateState.Checking, InstalledVersion = plugin.Installer.InstalledVersion, Manual = manual };
            RefreshUpdateLine();

            var installed = UpdateCheck.ComparableInstalled(plugin.Installer.InstalledVersion, OpenDash.Version);
            UpdateService.InBackground(() =>
            {
                var ticks = Settings.LastUpdateCheckTicks;
                var answer = Updates.Check(installed, Settings.CheckForUpdates, ref ticks, DateTime.UtcNow, manual);
                Dispatcher.Invoke(() =>
                {
                    if (answer == null)
                    {
                        // The service declined after all. Whatever was showing before is still the truth.
                        updateStatus = new UpdateStatus { State = UpdateState.Idle, InstalledVersion = plugin.Installer.InstalledVersion };
                    }
                    if (answer != null)
                    {
                        updateStatus = answer;
                        if (ticks != Settings.LastUpdateCheckTicks)
                        {
                            Settings.LastUpdateCheckTicks = ticks;
                            Save();
                        }
                    }
                    if (checkButton != null) checkButton.IsEnabled = true;
                    confirmingEdited = false;
                    RefreshUpdateLine();
                });
            }, new SimHubInstallLog());
        }

        /// <summary>
        /// Applies the release the last check found, asking once before replacing a dashboard somebody has edited.
        /// </summary>
        private void ApplyUpdate()
        {
            // A second click before the first has been answered used to fall straight through the confirmation,
            // because the confirming branch returned without disabling anything.
            if (applying) return;

            var release = Updates.LastReleases.FirstOrDefault(r => r.Version == updateStatus.LatestVersion);
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

            var edited = plugin.Installer.Packages.Where(p => p.Edited).Select(p => p.FolderName).ToList();
            if (edited.Count > 0 && !confirmingEdited)
            {
                // One click to be told, a second to mean it. A dialog would be the SimHub way and a modal in a
                // settings page is worse than a button that changes what it says.
                confirmingEdited = true;
                updateButton.Content = "Replace anyway";
                updateLine.Text = (edited.Count == 1 ? "1 dashboard has" : edited.Count + " dashboards have")
                    + " changed since OpenDash wrote them: " + string.Join(", ", edited)
                    + ". Updating replaces what is there. A copy of yours is kept beside it in DashTemplates, "
                    + "and \"Put mine back\" restores it.";
                updateLine.Visibility = Visibility.Visible;
                return;
            }

            var replaceEdited = confirmingEdited;
            applying = true;
            updateButton.IsEnabled = false;
            reinstallButton.IsEnabled = false;
            checkButton.IsEnabled = false;
            updateLine.Text = "Downloading " + updateStatus.LatestVersion + "…";
            updateLine.Visibility = Visibility.Visible;

            UpdateService.InBackground(() =>
            {
                var outcome = Updates.Apply(plugin.Installer, release, replaceEdited);
                Dispatcher.Invoke(() =>
                {
                    applying = false;
                    confirmingEdited = false;
                    if (updateButton != null)
                    {
                        updateButton.Content = "Update";
                        updateButton.IsEnabled = true;
                    }
                    if (reinstallButton != null) reinstallButton.IsEnabled = true;
                    if (checkButton != null) checkButton.IsEnabled = true;
                    // The record is written in memory by the installer and saved here, on the UI thread, which is
                    // the moment it is safe to serialise the settings.
                    Save();
                    plugin.Installer.Refresh();
                    RefreshRestoreButton();
                    if (outcome.Ok && outcome.Updated.Count > 0)
                    {
                        updateStatus = new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = plugin.Installer.InstalledVersion, Manual = true };
                    }
                    RefreshStatus();
                    // The button's visibility is computed nowhere but here, so a status that has just stopped
                    // offering an update has to be redrawn or the button outlives the release it was offering.
                    // It runs before the outcome sentence is written because it writes the line as well.
                    RefreshUpdateLine();
                    if (updateLine == null) return;
                    updateLine.Text = outcome.Line;
                    updateLine.Visibility = Visibility.Visible;
                });
            }, new SimHubInstallLog(), mustFinish: true);
        }

        private void RefreshUpdateLine()
        {
            if (updateLine == null) return;
            var line = updateStatus.Line;
            updateLine.Text = line ?? string.Empty;
            updateLine.Visibility = updateStatus.IsVisible && line != null ? Visibility.Visible : Visibility.Collapsed;
            if (updateButton != null)
            {
                updateButton.Visibility = updateStatus.State == UpdateState.UpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
            }
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
            button.ToolTip = "Write every dashboard your rig has into DashTemplates again. Your settings are kept.";
            reinstallLabel = Ui.Text("Reinstall", Theme.SizeBody, FontWeights.Medium, Theme.TextPrimary);
            button.Content = Ui.HStack(PanelMetrics.ButtonIconGap, Ui.Icon(PanelIcons.Refresh, Theme.TextPrimary), reinstallLabel);
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

            var edited = plugin.Installer.Packages.Where(p => p.Edited).Select(p => p.FolderName).ToList();
            if (edited.Count > 0 && !confirmingReinstall)
            {
                confirmingReinstall = true;
                if (reinstallLabel != null) reinstallLabel.Text = "Replace anyway";
                updateLine.Text = (edited.Count == 1 ? "1 dashboard has" : edited.Count + " dashboards have")
                    + " changed since OpenDash wrote them: " + string.Join(", ", edited)
                    + ". Reinstalling replaces what is there. A copy of yours is kept beside it in DashTemplates, "
                    + "and \"Put mine back\" restores it.";
                updateLine.Visibility = Visibility.Visible;
                return;
            }

            var replaceEdited = confirmingReinstall;
            reinstallButton.IsEnabled = false;
            try
            {
                plugin.Installer.Wanted = Settings.RigScreens().Select(s => s.Folder).Where(folder => folder != null).ToList();
                plugin.Installer.EnsureInstalled(true, replaceEdited);
                var replaced = plugin.Installer.Packages.Count(p => p.Extracted);
                var held = plugin.Installer.Packages.Count(p => p.HeldBack);
                // The screens with a namespace of their own are written by ScreenInstaller, because no
                // package carries their folder; the installer above has just done the stock ones.
                var log = new SimHubInstallLog();
                foreach (var screen in Settings.RigScreens().Where(s => !s.IsStock))
                {
                    if (ScreenInstaller.Write(screen, plugin.Installer.PackageSource, plugin.Installer.SimHubRoot, plugin.Installer.Record, log, force: true).Written) replaced++;
                }
                updateLine.Text = held > 0
                    ? "Reinstalled " + replaced + ". " + held + " left alone because you have edited them."
                    : "Reinstalled " + replaced + (replaced == 1 ? " dashboard. " : " dashboards. ") + UpdateWording.Reopen;
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
                confirmingReinstall = false;
                if (reinstallLabel != null) reinstallLabel.Text = "Reinstall";
                // The kit draws a disabled button at the canvas's 40 per cent through a template trigger,
                // so putting IsEnabled back is the whole of the re-enable: nothing here dimmed it by hand.
                if (reinstallButton != null) reinstallButton.IsEnabled = true;
                Save();
                RefreshStatus();
                RefreshRestoreButton();
            }
        }

        /// <summary>Title "OpenDash <installed version> · <n> dashboards" and the status pill: a 6 px dot and a tracked
        /// label showing the worst status across the packages; the tooltip lists every dashboard with its own status.</summary>
        private void RefreshStatus()
        {
            if (dashboardTitle == null || statusHost == null) return;
            var installer = plugin.Installer;
            var version = installer.InstalledVersion ?? installer.EmbeddedVersion ?? OpenDash.Version;
            dashboardTitle.Text = DashboardInstaller.Summary(version, installer.PackageCount);

            string dot;
            var label = Theme.TextPrimary;
            switch (installer.Status)
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
            statusHost.Child = Ui.StatusPill(dot, installer.Status.Label(), label);
            statusHost.ToolTip = installer.HasEmbeddedPackage
                ? (installer.Packages.Count > 0 ? installer.PackageReport() : installer.LastError)
                : "This build of the plugin carries no dashboard package.";
        }
    }
}
