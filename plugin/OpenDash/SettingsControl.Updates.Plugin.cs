// SettingsControl.Updates.Plugin.cs: the Updates page's update card and check row, and the update flow behind
// them -- applying a release, the restart it asks for, Reinstall everything with its twice-asked replace, and
// Put mine back.
//
// The controls it writes into are declared in SettingsControl.Updates.cs, which drops them when the page is
// left or drawn again. Asking for a check and taking its answer are the shell's (SettingsControl.Live.cs),
// since the sidebar's badge reads the answer on every page; this file draws it. What the card shows in each
// state, and every sentence, is PanelUpdates'.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SimHub.Plugins.Styles;
// Aliased rather than imported: System.Windows.Forms carries a Button of its own, and this file is
// full of WPF ones. SHMessageBox answers with the Forms enum whatever the dialog it draws.
using DialogResult = System.Windows.Forms.DialogResult;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The update card's place, which the card is drawn into now and whenever the check answers.</summary>
        private FrameworkElement BuildPluginSection()
        {
            updatesCardHost = new Border();
            UpdatesDrawCard();
            return updatesCardHost;
        }

        /// <summary>
        /// Draws the update card for the state the check, the run and the disk are in: an offer with Download
        /// and its notes, a download with its bar, a staged plugin waiting for the restart, or nothing.
        /// </summary>
        /// <remarks>
        /// Redrawn whole rather than edited, because each state is a different card. Download's question is
        /// on the card's own line, so redrawing withdraws it, as any other writer of the line does.
        /// </remarks>
        private void UpdatesDrawCard()
        {
            if (updatesCardHost == null) return;
            // A press on the card that redraws it takes the pressed control out of the tree, and keyboard focus
            // with it; it is put back on what the redrawn card offers (UpdatesRefocus).
            var hadFocus = updatesCardHost.IsKeyboardFocusWithin;
            updatesCardLine = null;
            updatesDownload = null;
            updatesProgressHost = null;
            updatesCard = PanelUpdates.CardFor(updateStatus.State, applying, UpdatesPending());
            if (updatesCard == UpdatesCard.None)
            {
                updatesCardHost.Child = null;
                updatesCardHost.Visibility = Visibility.Collapsed;
                if (hadFocus) UpdatesRefocus(updatesCheckNow);
                return;
            }
            updatesCardHost.Visibility = Visibility.Visible;

            var latest = updateStatus.LatestVersion;
            var headLine = new WrapPanel { Orientation = Orientation.Horizontal };
            var heading = Ui.Heading(PanelUpdates.Heading(latest), true);
            heading.VerticalAlignment = VerticalAlignment.Bottom;
            headLine.Children.Add(heading);
            var installed = plugin.RigVersion;
            if (PanelUpdates.ShowsYouHave(installed))
            {
                var have = Ui.HStack(PanelUpdates.YouHaveGap,
                    Ui.Text(PanelUpdates.YouHave, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary),
                    Ui.Numeral(installed, PanelUpdates.CardVersionSize, Theme.TextPrimary));
                have.VerticalAlignment = VerticalAlignment.Bottom;
                have.Margin = new Thickness(PanelUpdates.CardHeadingGap, 0, 0, PanelUpdates.YouHaveBaseline);
                headLine.Children.Add(have);
            }

            // A download has no sentence under its heading: the bar's own head says the run, and a caption over
            // it named the run a second time, with a second verb, and the heading's version again.
            var note = PanelUpdates.CardNote(updatesCard, latest);
            var text = note == null ? Ui.VStack(PanelUpdates.CardTextGap, headLine) : Ui.VStack(PanelUpdates.CardTextGap, headLine, Ui.Caption(note, BodyWidth));
            updatesCardLine = Ui.Caption(string.Empty, BodyWidth);
            updatesCardLine.Visibility = Visibility.Collapsed;
            // Added after the stack was built, so it takes the stack's gap itself.
            updatesCardLine.Margin = new Thickness(0, PanelUpdates.CardTextGap, 0, 0);
            text.Children.Add(updatesCardLine);
            if (updatesCard == UpdatesCard.Downloading)
            {
                updatesProgressHost = new Border { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, PanelMetrics.ProgressGap, 0, 0) };
                text.Children.Add(updatesProgressHost);
            }

            if (updatesCard == UpdatesCard.Available)
            {
                updatesDownload = Ui.Button(null, PanelButtonKind.Primary);
                updatesDownload.MinWidth = ButtonMinWidth;
                updatesDownload.IsEnabled = PanelUpdates.DownloadEnabled(updateStatus.State, UpdatesHeld);
                updatesDownload.SetBinding(ContentControl.ContentProperty, UpdatesLabelFrom(updatesCardLine, ReplacingAction.Update));
                updatesDownload.Click += (sender, args) => ApplyUpdate();
            }
            var head = new Border
            {
                Padding = new Thickness(PanelUpdates.CardPaddingX, PanelUpdates.CardPaddingY, PanelUpdates.CardPaddingX, PanelUpdates.CardPaddingY),
                Child = UpdatesBeside(text, updatesDownload, PanelUpdates.CardGap, updatesWidth),
            };

            var card = new StackPanel { Orientation = Orientation.Vertical };
            card.Children.Add(head);
            if (updatesCard == UpdatesCard.Available) card.Children.Add(UpdatesReleaseNotes());
            updatesCardHost.Child = Ui.CardBox(card, 0);
            if (applying) ShowRun();
            if (hadFocus) UpdatesRefocus(updatesDownload, updatesCheckNow);
        }

        /// <summary>
        /// Puts keyboard focus, once the redrawn controls are laid out, on the first of these that can take
        /// it, or on the page's first control when none can: a press that redraws its own place in the page
        /// must not leave the next Tab starting from SimHub's window.
        /// </summary>
        private void UpdatesRefocus(params UIElement[] candidates)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var target = candidates.FirstOrDefault(c => c != null && c.Focusable && c.IsVisible && c.IsEnabled);
                if (target != null) Keyboard.Focus(target);
                else if (updatesPage != null) updatesPage.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }), DispatcherPriority.Loaded);
        }

        /// <summary>The card's foot: the release's opening sentence and the link to every release.</summary>
        /// <remarks>
        /// The heading only over notes: a remembered offer (UpdateMark.Opening) carries none, and a heading
        /// over nothing but the link is not drawn.
        /// </remarks>
        private FrameworkElement UpdatesReleaseNotes()
        {
            var notes = new StackPanel { Orientation = Orientation.Vertical };
            var summary = UpdateWording.Summarise(updateStatus.Notes);
            var heading = PanelUpdates.NotesHeading(summary);
            if (heading != null)
            {
                notes.Children.Add(Ui.Eyebrow(heading));
                var line = Ui.Prose(summary, Theme.SizeSmall, Theme.TextPrimary);
                line.MaxWidth = BodyWidth;
                line.HorizontalAlignment = HorizontalAlignment.Left;
                line.Margin = new Thickness(0, PanelUpdates.NotesGap, 0, 0);
                notes.Children.Add(line);
            }
            FrameworkElement link = BuildLink(PanelUpdates.EveryRelease, UpdateCheck.ReleasesPageUrl);
            // The NEW tag after the link rather than inside it, where the link's own ink would take it.
            if (PanelUpdates.IsNew(PanelUpdates.EveryRelease)) link = UpdatesTagged(link);
            link.HorizontalAlignment = HorizontalAlignment.Left;
            link.Margin = new Thickness(0, heading == null ? 0 : PanelUpdates.NotesGap, 0, 0);
            notes.Children.Add(link);
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(PanelUpdates.CardPaddingX, PanelUpdates.NotesPaddingTop, PanelUpdates.CardPaddingX, PanelUpdates.NotesPaddingBottom),
                Child = notes,
            };
        }

        /// <summary>
        /// The switch, UpdateWording's one sentence about it, when the last check answered, and a press for
        /// somebody who would rather ask now. While the card is gone the row carries its line.
        /// </summary>
        private FrameworkElement BuildCheckRow()
        {
            var toggle = BuildToggle(Settings.CheckForUpdates, on =>
            {
                Settings.CheckForUpdates = on;
                Save();
                // On, back to what the panel opens on, a remembered offer included, as the idle screen's
                // mark shows it again; the card is drawn before asking, so a check the interval allows is
                // said beside the offer it may replace.
                updateStatus = PanelUpdates.Switched(on, plugin.LastUpdateStatus, plugin.OfferedUpdate, plugin.RigVersion);
                UpdatesDrawCard();
                if (on) Check(manual: false);
                UpdatesRefreshCheck();
                // An offer the switch withdrew is the sidebar's badge and Home's line too.
                RefreshAttention();
                RefreshSidebar();
            });

            updatesCheckNow = Ui.Button(PanelUpdates.CheckNow, PanelButtonKind.Outline, PanelButtonSize.Small);
            updatesCheckNow.Click += (sender, args) => Check(manual: true);

            var row = Ui.SettingRow(PanelUpdates.CheckTitle, Ui.HStack(PanelUpdates.CheckControlsGap, updatesCheckNow, toggle), UpdateWording.CheckCaption);
            // The artboard's .row at its own 14, and with no rule over it: it follows the card, not a row.
            row.Padding = new Thickness(0, PanelKit.RowPaddingYUpdates, 0, PanelKit.RowPaddingYUpdates);
            row.BorderThickness = new Thickness(0);

            updatesLastChecked = Ui.Caption(string.Empty, PanelUpdates.CheckLineWidth);
            updatesCheckLine = Ui.Caption(string.Empty, PanelUpdates.CheckLineWidth);
            // Added to the row's stack after it was built, so each takes the caption's gap itself.
            updatesLastChecked.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);
            updatesCheckLine.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);
            var parts = row.Tag as RowParts;
            var left = parts == null ? null : parts.TitleLine.Parent as Panel;
            if (left != null)
            {
                left.Children.Add(updatesLastChecked);
                left.Children.Add(updatesCheckLine);
            }
            UpdatesRefreshCheck();
            return row;
        }

        /// <summary>
        /// The check row's lines and its press, and the offer card's Download and line, from the check's state
        /// and the card's. A check in flight over an offer is said on the card and holds Download off, since
        /// the offer it would act on is being asked for again; the answer redraws the card.
        /// </summary>
        private void UpdatesRefreshCheck()
        {
            if (updatesLastChecked != null) updatesLastChecked.Text = PanelUpdates.LastChecked(Settings.LastUpdateCheckTicks, DateTime.UtcNow);
            if (updatesCheckLine != null)
            {
                var line = PanelUpdates.RowLine(updatesCard, updateStatus);
                updatesCheckLine.Text = line ?? string.Empty;
                updatesCheckLine.Visibility = line == null ? Visibility.Collapsed : Visibility.Visible;
            }
            if (updatesCheckNow != null) updatesCheckNow.IsEnabled = PanelUpdates.CheckNowEnabled(Settings.CheckForUpdates, applying, updateStatus.State);
            if (updatesDownload != null) updatesDownload.IsEnabled = PanelUpdates.DownloadEnabled(updateStatus.State, UpdatesHeld);
            var cardLine = PanelUpdates.CardLine(updatesCard, updateStatus);
            // Only ever written, never cleared, here: the line is Download's question otherwise, and the
            // answer's redraw of the card takes this sentence away with the rest. Written in place of the
            // question rather than through UpdatesAsk, so Reinstall everything's question stands.
            if (cardLine != null && updatesCardLine != null)
            {
                updatesCardLine.Text = cardLine;
                updatesCardLine.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Draws the run that is downloading into this build: its bar at the fraction it last reported, and
        /// the presses it holds off disabled, since each would return without a word while it runs.
        /// </summary>
        private void ShowRun()
        {
            if (updatesReinstall != null) updatesReinstall.IsEnabled = false;
            if (updatesCheckNow != null) updatesCheckNow.IsEnabled = false;
            foreach (var press in updatesRunPresses) press.IsEnabled = false;
            if (updatesProgressHost != null) updatesProgressHost.Child = Ui.Progress(applyingFraction, PanelMetrics.ProgressWidth, PanelUpdates.Downloading);
        }

        /// <summary>
        /// Draws the write Reinstall everything or Put mine back is making into this build: the presses it holds off
        /// disabled (PanelUpdates.RunHolds), and Reinstall everything's bar beside it at the fraction it last reported.
        /// </summary>
        private void ShowWrite()
        {
            if (updatesReinstall != null) updatesReinstall.IsEnabled = false;
            if (updatesDownload != null) updatesDownload.IsEnabled = false;
            foreach (var press in updatesRunPresses) press.IsEnabled = false;
            if (!updatesWritingReinstall) return;
            if (updatesReinstallLine != null) updatesReinstallLine.Visibility = Visibility.Collapsed;
            if (updatesReinstallProgress != null) updatesReinstallProgress.Child = Ui.Progress(updatesWritingFraction, PanelMetrics.ProgressWidth, PanelUpdates.Reinstalling);
        }

        /// <summary>
        /// A binding that draws Download's or Reinstall everything's label from the confirmation each time the
        /// press's own line changes.
        /// </summary>
        /// <remarks>
        /// Bound to the line rather than set beside each sentence written to it, because the question is on the line
        /// and a label set by hand outlived it (#480). With the label a function of the line, whatever takes the
        /// line, a writer added later included, turns it back into the button's words, and it reads "Replace anyway"
        /// only while its own question is what the line shows. The line is built before its button.
        /// </remarks>
        private Binding UpdatesLabelFrom(TextBlock line, ReplacingAction action)
        {
            return new Binding(nameof(TextBlock.Text))
            {
                Source = line,
                Mode = BindingMode.OneWay,
                Converter = new ConfirmationLabel(confirmation, action),
            };
        }

        /// <summary>The converter behind <see cref="UpdatesLabelFrom"/>: the line's text in, the button's label out.</summary>
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
        /// Puts a question on a press's line, and takes the other press's question off its own: there is one
        /// question at a time (PanelConfirmation), so the other line must not go on showing one that has
        /// been withdrawn.
        /// </summary>
        private void UpdatesAsk(TextBlock line, string question)
        {
            foreach (var other in new[] { updatesCardLine, updatesReinstallLine })
            {
                if (other == null || other == line) continue;
                other.Text = string.Empty;
                other.Visibility = Visibility.Collapsed;
            }
            if (line == null) return;
            line.Text = question;
            line.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Put mine back: restores each copy kept when a dashboard somebody edited was replaced.
        /// </summary>
        /// <remarks>
        /// The confirmation before replacing an edited dashboard promises that a copy is kept and can be put back,
        /// and this is the press that keeps the promise. It reads the disk first, so a folder edited in Dash
        /// Studio since the card was drawn is the driver's and is left alone (UpdatesKept): PackageExtractor.Restore
        /// deletes the folder it restores into and keeps no copy of it.
        /// </remarks>
        private void RestoreKept()
        {
            if (UpdatesHeld) return;
            plugin.Installer.Refresh();
            var root = plugin.Installer.SimHubRoot;
            // The card shows a kept copy whatever the driver has done since, as the artboard draws it, so a folder edited again
            // is left as it is and named: Restore keeps no copy of what it replaces, and nothing has asked.
            var held = new List<string>();
            var edited = new HashSet<string>(plugin.Installer.EditedFolders.Where(f => f != null), StringComparer.OrdinalIgnoreCase);
            var putting = new List<KeyValuePair<string, string>>();
            foreach (var kept in UpdatesKept())
            {
                if (PanelUpdates.PutsBack(edited.Contains(kept.Key))) putting.Add(kept);
                else held.Add(kept.Value);
            }
            var restored = 0;
            // A copy that could not be put back is said by name, since the card still offers it once the
            // page is drawn again, and "nothing to put back" under it would say the opposite.
            var failed = new List<string>();
            updatesWriting = true;
            updatesWritingReinstall = false;
            ShowWrite();
            // Off the interface thread (#611): a restore deletes the folder and unzips the kept copy in its place.
            WriteThen(() =>
            {
                foreach (var kept in putting)
                {
                    var folder = kept.Key;
                    try
                    {
                        var copy = PackageExtractor.KeptCopies(root, folder).FirstOrDefault(path => path.Contains(PackageExtractor.EditedSuffix));
                        if (copy == null) continue;
                        if (PackageExtractor.Restore(root, folder, new SimHubInstallLog(), copy)) restored++;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Putting back " + folder + " failed", ex);
                        failed.Add(kept.Value);
                    }
                }
            }, failure =>
            {
                updatesWriting = false;
                // Each folder keeps its own net above, so a failure here is the run's own and is said as nothing put back.
                if (failure != null) failed.AddRange(putting.Select(kept => kept.Value).Except(failed));
                plugin.Installer.Refresh();
                Save();
                Redraw();
                Say(PanelUpdates.PutBack(restored, failed, held), restored > 0 && failed.Count == 0 && held.Count == 0);
            });
        }

        /// <summary>
        /// Applies the release the last check found, asking once before replacing a dashboard somebody has edited.
        /// </summary>
        private void ApplyUpdate()
        {
            // A second click before the first has been answered used to fall straight through the confirmation,
            // because the confirming branch returned without disabling anything.
            if (UpdatesHeld) return;
            // A check in flight holds Download off (UpdatesRefreshCheck), and its answer redraws the card: a
            // press that reached here anyway has nothing to act on yet, and "No release to install" would be
            // false in a moment.
            if (updateStatus.State == UpdateState.Checking) return;

            var release = Updates.LastReleases.FirstOrDefault(r => r.Version == updateStatus.LatestVersion);
            // A release to act on spends a Download that was waiting for its listing, whichever press found
            // it: left standing, the next answer the page heard would download again by itself.
            if (release != null) applyWaiting = false;
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
                // Nothing to act on, so the card is put back where the status says it belongs, which is how a
                // button left over from an earlier state disappears on the press that found it stale, and the
                // line says so.
                UpdatesDrawCard();
                UpdatesRefreshCheck();
                Say(UpdateWording.NothingToApply, false);
                return;
            }

            // Read from the disk at the press rather than from whenever the installer last looked, since a dashboard
            // edited in Dash Studio while the question is open is one the question did not name, and the second press
            // has to see it in order to ask again.
            plugin.Installer.Refresh();
            var edited = plugin.Installer.EditedFolders;
            var question = UpdateWording.ReplaceEditedQuestion(UpdatesNames(edited), onRestart: release.PluginAsset() != null);
            var press = confirmation.Press(ReplacingAction.Update, edited, question, updatesCardLine == null ? null : updatesCardLine.Text);
            if (press == PressOutcome.Ask)
            {
                // One click to be told, a second to mean it; the button reads "Replace anyway" of itself once the
                // question is on its line.
                UpdatesAsk(updatesCardLine, question);
                return;
            }

            var replaceEdited = press == PressOutcome.RunReplacingEdited;
            applying = true;
            applyingFraction = 0;
            UpdatesDrawCard();
            UpdatesRefreshCheck();

            // The run reports per chunk of a several-megabyte download, which is thousands of calls, and every
            // one of them crosses to the interface thread. Only a whole percent is drawn, so only a whole
            // percent is sent. BeginInvoke rather than Invoke, because a download that waited for the panel
            // to paint would be paced by the panel.
            var shown = -1;
            Action<double> report = fraction =>
            {
                var percent = PanelMetrics.PercentOf(fraction);
                if (percent == shown) return;
                shown = percent;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    applyingFraction = fraction;
                    // The build that is showing now, which a rebuild since the press has replaced.
                    if (applying && updatesProgressHost != null) updatesProgressHost.Child = Ui.Progress(fraction, PanelMetrics.ProgressWidth, PanelUpdates.Downloading);
                }));
            };

            // The run is the plugin's, and records what the rig is owed without the page (#613): the yes to
            // replacing edited dashboards on restart and the names the driver gave their screens are settled on
            // the run's thread, and the save is the plugin's. What is left here is drawing how it went.
            plugin.ApplyUpdate(release, replaceEdited, report, outcome =>
            {
                // A run that throws is still finished on the page: without a completion, applying would stay
                // true for the life of the control, the card would say "Downloading" until SimHub restarts, and
                // every press the run holds off would stay held off with it. The exception is in the log, and the
                // page says the failure in its own sentence, the shape Reinstall everything's takes, rather than
                // in UpdateOutcome.Line's reason slot.
                var said = outcome == null ? PanelUpdates.UpdateFailed : outcome.Line;
                // Posted, not Invoked: End runs on the interface thread and waits there for the part of the run
                // that writes (UpdateService.WaitForIdle), so nothing on the run's thread may wait on that one.
                Dispatcher.BeginInvoke(new Action(() => UpdatesApplied(release, outcome ?? new UpdateOutcome(), said)));
            });
        }

        /// <summary>
        /// A run's completion, on the interface thread: the run is over, and the page is drawn in the state the
        /// run left when it is on screen. Nothing the rig is owed waits on it (#613).
        /// </summary>
        /// <remarks>
        /// Posted, so nothing waits on it and nothing it throws reaches the run's own net: it keeps its own,
        /// and clears the run first, so whatever throws cannot leave the page's presses held off until SimHub
        /// restarts. The restart is offered outside that net, since a staged plugin waits for it whatever the
        /// page did.
        ///
        /// A build's controls outlive SimHub showing another of its own pages, so an Updates build that is
        /// there is not one the driver can see. While the panel is away the page is neither redrawn nor
        /// counted as read: the return rebuilds it and reads the disk, so a dashboard edited in Dash Studio
        /// meanwhile is drawn as it now is. Only the line is said, which the return keeps.
        ///
        /// A run that did not finish is said whatever page shows, since the line is the shell's and sits
        /// above every page until the next Go: a run that failed while the driver was on Home would otherwise
        /// leave no word anywhere but SimHub's log, and the card would offer Download again as if it had never
        /// been pressed. One that finished says itself on the Updates page, or by the restart's dialog.
        /// </remarks>
        /// <param name="said">What the page says of the run: its outcome's line, or PanelUpdates.UpdateFailed
        /// for a run that threw.</param>
        private void UpdatesApplied(ReleaseInfo release, UpdateOutcome outcome, string said)
        {
            applying = false;
            applyingFraction = 0;
            try
            {
                // The screens' names are back and the settings saved by now, by the run and the plugin
                // (UpdateService.Settle, OpenDash.ApplyUpdate); the page only reads the disk again.
                plugin.Installer.Refresh();
                // The idle screen's mark compares the release it offers with what the rig now runs, and the
                // dashboards have just moved.
                plugin.RefreshUpdateMark();
                updateStatus = UpdateMark.Applied(updateStatus, outcome.Ok, release.Version, plugin.RigVersion);
                var showing = updatesCardHost != null && IsLoaded;
                if (showing)
                {
                    // That read is this visit's, so the redraw below does not hash every folder again.
                    updatesRead = true;
                    // The page is showing: draw it again in the state the run left, and say how it went.
                    Redraw();
                }
                else
                {
                    // The sidebar's badge and Home read the same answer.
                    RefreshAttention();
                    RefreshSidebar();
                }
                if (updatesCardHost != null || !outcome.Ok) Say(said, outcome.Ok);
            }
            catch (Exception ex)
            {
                Log.Error("Finishing the update on the panel failed", ex);
            }
            // The one thing the run cannot do for itself. Asked here rather than before the download, because
            // until the assembly is staged there is nothing for a restart to put in place.
            if (outcome.PluginStaged) OfferRestart(release.Version);
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
        ///
        /// A "not now" says nothing: the card already reads UpdateWording.RestartLater for as long as the
        /// swap waits.
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
                if (!now) return;
                Say(UpdateWording.RestartGoing);
                Save();
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Warn("Asking about the restart failed, so the plugin swap waits for an ordinary close: " + ex.Message);
                PluginUpdate.AskToReopen(root, false);
                Say(UpdateWording.RestartFailed, false);
            }
        }

        /// <summary>
        /// Reinstall everything: writes every dashboard the rig has again, asking once before replacing one
        /// somebody has edited, then brings each light profile older than this build's forward.
        /// </summary>
        /// <remarks>
        /// A light profile that is current is never rewritten: a rewrite could only cost the edits made to it in
        /// SimHub. One that is older, missing or failed is written (PanelUpdates.BringsForward), the
        /// flag box's only on a rig with a matrix, where the table draws its row.
        /// </remarks>
        private void Reinstall()
        {
            // Two installers over the same DashTemplates folders is the one combination that can delete a folder
            // one of them is extracting into, so whichever starts first holds the field.
            if (UpdatesHeld) return;

            // From the disk at the press, for the reason ApplyUpdate reads it there.
            plugin.Installer.Refresh();
            var edited = plugin.Installer.EditedFolders;
            var question = PanelUpdates.ReinstallQuestion(UpdatesNames(edited));
            var press = confirmation.Press(ReplacingAction.Reinstall, edited, question, updatesReinstallLine == null ? null : updatesReinstallLine.Text);
            if (press == PressOutcome.Ask)
            {
                UpdatesAsk(updatesReinstallLine, question);
                return;
            }

            var replaceEdited = press == PressOutcome.RunReplacingEdited;
            // The rig as it stands at the press, over an installer of the run's own: the run writes what was asked
            // for whatever the page does meanwhile, and the plugin's installer, which every page reads, is never
            // half way through a run (#611). The record is the plugin's, so what is written is remembered.
            var rig = Settings.RigScreens().Where(screen => screen != null).ToList();
            var installer = new DashboardInstaller(plugin.Installer.SimHubRoot, new SimHubInstallLog(), plugin.Installer.PackageSource, plugin.Installer.Record)
            {
                Rig = () => rig,
            };
            int replaced = 0, held = 0;
            var wroteFonts = false;
            updatesWriting = true;
            updatesWritingReinstall = true;
            updatesWritingFraction = 0;
            ShowWrite();
            // A folder is the finest grain the installer reports, so this is a handful of calls rather than the
            // download's thousands; posted all the same, so the run is never paced by the panel.
            Action<double> report = fraction => Dispatcher.BeginInvoke(new Action(() =>
            {
                updatesWritingFraction = fraction;
                // The build that is showing now, which a rebuild since the press has replaced.
                if (updatesWriting && updatesReinstallProgress != null) updatesReinstallProgress.Child = Ui.Progress(fraction, PanelMetrics.ProgressWidth, PanelUpdates.Reinstalling);
            }));
            // Every screen on the rig, a second one of a size included: the installer reads the rig and writes each
            // folder with the screen's own name and namespace, so there is nothing to add after. Off the interface
            // thread, since it extracts every package, hashes every folder and zips the edited ones (#611).
            WriteThen(() =>
            {
                var writing = DateTime.UtcNow;
                installer.EnsureInstalled(true, replaceEdited, report);
                replaced = installer.Packages.Count(p => p.Extracted);
                held = installer.Packages.Count(p => p.HeldBack);
                // A font this press put into DashFonts is not drawn until SimHub restarts.
                wroteFonts = PackageExtractor.FacesWrittenSince(installer.SimHubRoot, writing) > 0;
            }, failure =>
            {
                updatesWriting = false;
                updatesWritingReinstall = false;
                if (failure != null) Log.Error("Reinstall failed", failure);
                // The light profiles are SimHub's own objects, which its interface is bound to, so they are brought
                // forward here on the interface thread once the dashboards are done.
                var lights = failure == null ? UpdatesBringLightsForward() : null;
                plugin.Installer.Refresh();
                Save();
                Redraw();
                if (failure != null) Say(PanelUpdates.ReinstallFailed, false);
                else Say(PanelUpdates.ReinstallSummary(replaced, held, wroteFonts, lights, FlagBoxName(), Settings.MatrixPanels().ToList()), lights.Ok);
            });
        }
    }
}
