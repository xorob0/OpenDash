// SettingsControl.Updates.cs: the Updates page, drawn to Updates.dc.html (#503) -- the update card, the check
// row, what OpenDash has put in SimHub and at which version, the copy an update kept, and Support.
//
// Every word and every decision is in PanelUpdates.cs, which the test project compiles; these files draw. The
// page's sections are the files beside this one: .Updates.Plugin.cs (the card, the check row and the update
// flow: applying a release, the restart, Reinstall everything and Put mine back), .Updates.Packages.cs (the
// "In SimHub" table and its dashboards) and .Updates.Lights.cs (its strips and the flag box). The update
// check's own state is the shell's (SettingsControl.Live.cs), because the sidebar's badge and Home read it on
// every page; this page draws it and hears an answer through OnUpdate.
//
// Nothing here calls DrawsLighting(): the build reads the disk and SimHub's LED and matrix settings, and a
// rebuild on a lighting change would run that on SimHub's interface thread while the driver is on track.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        // --- What one build holds, dropped with it ----------------------------------------------------

        /// <summary>The update card's place on the page, redrawn in place when the check answers.</summary>
        private Border updatesCardHost;

        /// <summary>The content width this build was drawn at, up to PanelUpdates.WidthDrawnUpTo, which the card
        /// is redrawn at.</summary>
        private double updatesWidth;

        /// <summary>What the card is showing now.</summary>
        private UpdatesCard updatesCard;

        /// <summary>Download's line on the card: its question, when it has one to ask.</summary>
        private TextBlock updatesCardLine;
        private Button updatesDownload;
        private Border updatesProgressHost;

        private Button updatesCheckNow;
        private TextBlock updatesLastChecked;

        /// <summary>The check row's second line: checking, checks off, or the answer to a press.</summary>
        private TextBlock updatesCheckLine;

        private Button updatesReinstall;

        /// <summary>The presses a download holds off besides Reinstall everything and Check now: Put mine
        /// back and the light rows' Update, drawn after the run started or before it (ShowRun).</summary>
        private readonly List<Button> updatesRunPresses = new List<Button>();

        /// <summary>Reinstall everything's line beside it: its question, when it has one to ask.</summary>
        private TextBlock updatesReinstallLine;

        /// <summary>The page this build drew, which keyboard focus falls back to when a redrawn press has
        /// nothing to hand it to (UpdatesRefocus).</summary>
        private FrameworkElement updatesPage;

        // --- What outlives the build ------------------------------------------------------------------

        /// <summary>Whether a Download pressed on a remembered offer is waiting for the listing it asked for.
        /// Held apart from the build's controls, so a rebuild in place (a resize, the return to the panel)
        /// keeps the press; only leaving the page drops it.</summary>
        private bool applyWaiting;

        /// <summary>The run that is downloading, held outside the build so a rebuild in place draws it again
        /// rather than an offer with live buttons: the line it said and the fraction it last reported.</summary>
        private string applyingLine;
        private double applyingFraction;

        /// <summary>Whether this visit to the page has read the installer's folders from the disk.</summary>
        private bool updatesRead;

        /// <summary>The question Download or Reinstall everything has put on its line, if either has. It
        /// outlives the page, and does not need forgetting with the controls: a question counts only while
        /// its line shows it, and a new build's lines show none.</summary>
        private readonly PanelConfirmation confirmation = new PanelConfirmation();

        private FrameworkElement BuildUpdatesPage(PanelRoute to)
        {
            // Every control the build holds goes with it, so an answer or a finished run that lands after the
            // page has gone writes nowhere rather than into a control nobody can see.
            OnDrop(() =>
            {
                updatesCardHost = null;
                updatesCardLine = null;
                updatesDownload = null;
                updatesProgressHost = null;
                updatesCheckNow = null;
                updatesLastChecked = null;
                updatesCheckLine = null;
                updatesReinstall = null;
                updatesReinstallLine = null;
                updatesPage = null;
                updatesImportLine = null;
                updatesImportPath = null;
                updatesRunPresses.Clear();
            });
            OnLeave("Updates.applyWaiting", () => applyWaiting = false);
            // A Download waiting for its listing waits only while the check it asked for is in flight: an
            // answer that landed while SimHub's panel was closed never reached UpdateAnswered, and the build
            // that follows the return is not a Go, so without this the press would outlive its answer and
            // the next check the page heard would download by itself.
            if (updateStatus.State != UpdateState.Checking) applyWaiting = false;
            // A check starting holds Check now and Download off and says so, on the card while an offer shows
            // and on the row otherwise (UpdatesRefreshCheck); its answer redraws the card.
            OnUpdate(UpdatesRefreshCheck, manual => UpdateAnswered());

            // Read only up to the widest width the page draws anything differently at, so a resize past it
            // does not rebuild the page (PanelShell.RebuildsOnResize).
            updatesWidth = ContentWidthUpTo(PanelUpdates.WidthDrawnUpTo);
            // What the installer says of the rig's folders, read from the disk once per visit: the table's
            // dashboards and the kept card both read it. Not on every build, since a resize or a return to
            // the panel rebuilds the page, and each read re-hashes every folder on this thread and logs a
            // line per package into the log the support report carries; every press that writes a folder
            // reads it again itself. Not during a download either: the run works over the same record and
            // folders on the thread pool, and its completion reads them again before it redraws.
            if (!updatesRead && !applying)
            {
                plugin.Installer.Refresh();
                updatesRead = true;
            }
            OnLeave("Updates.read", () => updatesRead = false);
            // Leaving the panel for another of SimHub's pages is not a Go, and the return rebuilds the page
            // rather than entering it: the read is dropped with the panel, so a dashboard edited in Dash
            // Studio meanwhile is read again rather than offered from what the page read before.
            RoutedEventHandler away = (sender, args) => updatesRead = false;
            Unloaded += away;
            OnDrop(() => Unloaded -= away);
            // "Last checked today" is said against now, and a page left open past midnight must not go on
            // saying it of yesterday's check. A settings read and a format, which WPF ignores when unchanged.
            OnTick(() =>
            {
                if (updatesLastChecked != null) updatesLastChecked.Text = PanelUpdates.LastChecked(Settings.LastUpdateCheckTicks, DateTime.UtcNow);
            });
            var kept = UpdatesKeptCard(updatesWidth);
            updatesPage = PageLayout(PanelUpdates.Title, null,
                Ui.Anchor(BuildPluginSection(), PanelUpdates.AnchorPlugin),
                Ui.Anchor(BuildCheckRow(), PanelUpdates.AnchorCheck),
                Ui.Anchor(BuildInSimHubSection(updatesWidth, kept == null), PanelUpdates.AnchorPackages),
                kept,
                Ui.Anchor(UpdatesSupportSection(), PanelUpdates.AnchorSupport));
            return updatesPage;
        }

        /// <summary>A check's answer, which the shell has already taken: the card, the check row, and a
        /// Download pressed on the remembered offer that was waiting for the listing.</summary>
        private void UpdateAnswered()
        {
            UpdatesDrawCard();
            UpdatesRefreshCheck();
            var waiting = applyWaiting;
            applyWaiting = false;
            // Not over a staged plugin: the old one still runs until the restart, so the check offers the
            // release it has already staged, and downloading it again finishes nothing.
            if (PanelUpdates.AppliesWhenAnswered(waiting, updateStatus.State, UpdatesPending()) && updatesCardHost != null) ApplyUpdate();
        }

        /// <summary>Whether a plugin is staged and waits for SimHub to restart, read off the disk; false, with a
        /// line in the log, when the disk cannot say.</summary>
        private bool UpdatesPending()
        {
            try
            {
                return PluginUpdate.Pending(plugin.Installer.SimHubRoot);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not tell whether an update waits for a restart: " + ex.Message);
                return false;
            }
        }

        // --- The kept copy ----------------------------------------------------------------------------

        /// <summary>
        /// Every folder OpenDash replaced although somebody had edited it, whose copy is still there to put
        /// back and which is not the driver's own again since (PanelUpdates.ShowsKept), each with the name
        /// SimHub lists it under. Off what the installer last read of the disk.
        /// </summary>
        private IList<KeyValuePair<string, string>> UpdatesKept()
        {
            var kept = new List<KeyValuePair<string, string>>();
            var root = plugin.Installer.SimHubRoot;
            var screens = Settings.RigScreens();
            // The rig's folders only: nothing replaces a folder outside the rig, so its copy is not this page's
            // to offer, and it has no name SimHub lists but the folder's.
            foreach (var package in plugin.Installer.Packages.Where(p => p.FolderName != null && !p.OutsideRig).GroupBy(p => p.FolderName, StringComparer.OrdinalIgnoreCase))
            {
                var folder = package.Key;
                IReadOnlyList<string> copies;
                try
                {
                    copies = PackageExtractor.KeptCopies(root, folder);
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not look for a kept copy of " + folder + ": " + ex.Message);
                    continue;
                }
                if (!PanelUpdates.ShowsKept(copies, package.Any(p => p.Edited))) continue;
                kept.Add(new KeyValuePair<string, string>(folder, PanelUpdates.ScreenName(screens, folder)));
            }
            return kept;
        }

        /// <summary>The names of these folders, as the questions before a replacement write them.</summary>
        private IReadOnlyCollection<string> UpdatesNames(IEnumerable<string> folders)
        {
            var screens = Settings.RigScreens();
            return (folders ?? Enumerable.Empty<string>()).Select(folder => PanelUpdates.ScreenName(screens, folder)).ToList();
        }

        /// <summary>The artboard's kept card, present only while a kept copy is there to put back.</summary>
        private FrameworkElement UpdatesKeptCard(double width)
        {
            var kept = UpdatesKept();
            if (kept.Count == 0) return null;

            var title = Ui.Text(PanelUpdates.KeptTitle(kept.Select(k => k.Value).ToList()), PanelUpdates.KeptTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
            title.TextWrapping = TextWrapping.Wrap;
            var text = Ui.VStack(PanelUpdates.KeptTextGap, title, Ui.Caption(PanelUpdates.KeptCaption(kept.Count), BodyWidth));
            var restore = Ui.Button(PanelUpdates.PutMineBack, PanelButtonKind.Outline, PanelButtonSize.Small);
            restore.Click += (sender, args) => RestoreKept();
            restore.IsEnabled = !applying;
            updatesRunPresses.Add(restore);

            var body = UpdatesBeside(text, restore, PanelUpdates.KeptGap, width);
            var card = Ui.CardBox(body, 0);
            card.Padding = new Thickness(PanelUpdates.KeptPaddingX, PanelUpdates.KeptPaddingY, PanelUpdates.KeptPaddingX, PanelUpdates.KeptPaddingY);
            return Ui.Anchor(card, PanelUpdates.AnchorKept);
        }

        /// <summary>
        /// Words with their press on the right, or the press under the words when the content is too narrow
        /// for both (PanelUpdates.ButtonBeside): the update card's head and the kept card.
        /// </summary>
        private static FrameworkElement UpdatesBeside(FrameworkElement text, FrameworkElement press, double gap, double width)
        {
            if (press == null) return text;
            if (!PanelUpdates.ButtonBeside(width))
            {
                press.HorizontalAlignment = HorizontalAlignment.Left;
                press.Margin = new Thickness(0, gap, 0, 0);
                return Ui.VStack(0, text, press);
            }
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            text.VerticalAlignment = VerticalAlignment.Center;
            press.VerticalAlignment = VerticalAlignment.Center;
            press.Margin = new Thickness(gap, 0, 0, 0);
            Grid.SetColumn(text, 0);
            Grid.SetColumn(press, 1);
            grid.Children.Add(text);
            grid.Children.Add(press);
            return grid;
        }

        // --- Support ----------------------------------------------------------------------------------

        /// <summary>The artboard's "Something wrong?", headed Support (voice.md: a heading is never a question).</summary>
        private FrameworkElement UpdatesSupportSection()
        {
            var copy = UpdatesSupportPress(PanelUpdates.CopyReport);
            copy.Click += (sender, args) => UpdatesCopyReport();

            var log = UpdatesSupportPress(PanelUpdates.OpenLog);
            log.Click += (sender, args) => UpdatesOpenLog();

            var issue = UpdatesSupportPress(PanelUpdates.ReportIssue);
            issue.ToolTip = IssuesUrl;
            issue.Click += (sender, args) => Ui.OpenUrl(IssuesUrl);

            var guide = UpdatesSupportPress(PanelUpdates.ReadGuide);
            guide.ToolTip = DocumentationUrl;
            guide.Click += (sender, args) => Ui.OpenUrl(DocumentationUrl);

            var presses = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var press in new FrameworkElement[] { copy, log, issue, guide })
            {
                // The wrap's own gap, right and below, so a row that breaks keeps the artboard's 8.
                press.Margin = new Thickness(0, 0, PanelUpdates.SupportButtonGap, PanelUpdates.SupportButtonGap);
                presses.Children.Add(press);
            }

            var caption = Ui.Prose(PanelUpdates.SupportCaption, PanelUpdates.SupportCaptionSize);
            caption.MaxWidth = BodyWidth;
            caption.HorizontalAlignment = HorizontalAlignment.Left;
            caption.Margin = new Thickness(0, PanelUpdates.SectionGap - PanelUpdates.SupportButtonGap, 0, 0);
            var licence = Ui.Prose(PanelUpdates.Licence, PanelUpdates.SupportCaptionSize, Theme.TextLabel);
            licence.Margin = new Thickness(0, PanelUpdates.SupportButtonGap, 0, 0);
            return PageSection(PanelUpdates.SupportTitle, false, PanelUpdates.SectionGap, presses, caption, licence);
        }

        /// <summary>A Support press, with the artboard's NEW tag inside it after its words while PanelUpdates
        /// counts it new.</summary>
        private static Button UpdatesSupportPress(string label)
        {
            var press = Ui.Button(label, PanelButtonKind.Outline);
            if (PanelUpdates.IsNew(label)) press.Content = Ui.HStack(PanelUpdates.NewTagGap, Ui.Text(label, Theme.SizeBody, FontWeights.Medium, Theme.TextPrimary), Ui.NewTag());
            return press;
        }

        /// <summary>Copy a support report: gathered at the press, written by PanelUpdates.Report, and put on
        /// the clipboard. Nothing is sent anywhere.</summary>
        private void UpdatesCopyReport()
        {
            string report;
            try
            {
                report = PanelUpdates.Report(UpdatesReportInput());
            }
            catch (Exception ex)
            {
                Log.Error("Writing the support report failed", ex);
                Say(PanelUpdates.ReportNotWritten, false);
                return;
            }
            try
            {
                Clipboard.SetText(report);
            }
            catch (Exception ex)
            {
                // Another program holding the clipboard open is the usual reason, and it passes.
                Log.Warn("Putting the support report on the clipboard failed: " + ex.Message);
                Say(PanelUpdates.ReportFailed, false);
                return;
            }
            Say(PanelUpdates.ReportCopied);
        }

        /// <summary>Everything the report says, read now: versions, the rig and its devices, the update
        /// check, the car tables and OpenDash's lines in SimHub's log.</summary>
        private UpdatesReportInput UpdatesReportInput()
        {
            var root = plugin.Installer.SimHubRoot;
            // The log first: reading the folders logs a line per package, and those lines would push out the
            // older ones the report exists to carry.
            var log = UpdatesLogTail(root);
            // Not while a release downloads: the run works over the same record and folders on the thread
            // pool, so the report says what the page last read instead.
            if (!applying) plugin.Installer.Refresh();
            var screens = UpdatesDashboardRows()
                .Select(pair => new UpdatesReportItem(pair.Value.Name, PanelUpdates.ScreenDetail(pair.Key.Kind, pair.Key.Width, pair.Key.Height), pair.Value.Version, pair.Value.State))
                .ToList();

            var strips = new List<UpdatesReportItem>();
            var bars = Settings.LedBarList().Where(bar => bar != null && bar.ProfileShapeId != null).ToList();
            if (bars.Count > 0)
            {
                bool reachable;
                var embedded = EmbeddedJsonFor(bars.Select(bar => bar.ProfileShapeId));
                var census = PanelUpdates.StripPlans(BarCensus(embedded, out reachable), reachable, embedded.Keys);
                foreach (var entry in census)
                {
                    var row = PanelUpdates.StripRow(entry.Key.Name, entry.Value);
                    var detail = PanelUpdates.StripDetail(PanelLightRows.ShapeLabel(entry.Key.ProfileShapeId), entry.Key.Device);
                    strips.Add(new UpdatesReportItem(row.Name, detail, row.Version, row.State));
                }
            }

            var matrices = Settings.MatrixPanels()
                .Select(m => new UpdatesReportItem(PanelUpdates.MatrixName(m), Settings.MatrixName(m), null, null))
                .ToList();
            UpdatesReportItem flagBox = null;
            if (PanelUpdates.DrawsFlagBoxRow(matrices.Count > 0, plugin.FlagBoxJson != null))
            {
                var row = PanelUpdates.FlagBoxRow(FlagBoxName(), SafePlan(), plugin.FlagBox?.Path);
                flagBox = new UpdatesReportItem(row.Name, null, row.Version, row.State);
            }

            var cars = plugin.CarLights;
            var carTables = cars == null ? null : PanelUpdates.CarTables(cars.Status, cars.FetchedAt);

            return new UpdatesReportInput
            {
                GeneratedUtc = DateTime.UtcNow,
                PluginVersion = OpenDash.Version,
                RigVersion = plugin.RigVersion,
                SimHubVersion = UpdatesSimHubVersion(root),
                SimHubRoot = root,
                OsVersion = Environment.OSVersion.VersionString,
                ChecksOn = Settings.CheckForUpdates,
                LastCheckedTicks = Settings.LastUpdateCheckTicks,
                UpdateLine = updateStatus.Line,
                RestartPending = UpdatesPending(),
                Screens = screens,
                Strips = strips,
                Matrices = matrices,
                FlagBox = flagBox,
                CarTables = carTables,
                Log = log,
            };
        }

        /// <summary>SimHub's own version, off its executable, or null.</summary>
        private static string UpdatesSimHubVersion(string root)
        {
            try
            {
                var exe = Path.Combine(root ?? string.Empty, PanelUpdates.SimHubExe);
                return File.Exists(exe) ? FileVersionInfo.GetVersionInfo(exe).FileVersion : null;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read SimHub's version: " + ex.Message);
                return null;
            }
        }

        /// <summary>The last OpenDash lines of SimHub's current log, read beside SimHub, which holds it open.</summary>
        private static IList<string> UpdatesLogTail(string root)
        {
            try
            {
                var path = Path.Combine(root ?? string.Empty, PanelUpdates.LogFolder, PanelUpdates.LogFile);
                if (!File.Exists(path)) return new List<string>();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                {
                    return PanelUpdates.Tail(UpdatesLines(reader));
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read SimHub's log for the support report: " + ex.Message);
                return new List<string>();
            }
        }

        private static IEnumerable<string> UpdatesLines(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null) yield return line;
        }

        /// <summary>Open the log: SimHub's Logs folder in Explorer.</summary>
        private void UpdatesOpenLog()
        {
            var folder = Path.Combine(plugin.Installer.SimHubRoot ?? string.Empty, PanelUpdates.LogFolder);
            if (!Directory.Exists(folder))
            {
                Say(PanelUpdates.LogMissing, false);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warn("Opening SimHub's log folder failed: " + ex.Message);
                Say(PanelUpdates.LogFailed(folder), false);
            }
        }
    }
}
