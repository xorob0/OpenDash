// PanelUpdatesTests.cs: the Updates page as Updates.dc.html draws it -- its words, the update card's states, the
// check row's lines, the "In SimHub" table's rows and columns, Reinstall everything, the kept copy and the
// support report.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelUpdatesTests
    {
        /// <summary>The content beside the full sidebar at a control that wide, with a 17 px scroll bar: 879 at
        /// the artboard's 1200 frame, 3519 at 3840. It is PanelShell.ContentWidth(control, 17) once the column
        /// has no ceiling, spelled out so the pin reads the same on either side of that change; the column
        /// has no widest width to pin against.</summary>
        private static double Column(double control) => control - PanelShell.SidebarWidth - 2 * PanelShell.MainPaddingX(PanelLayout.Full) - 17;

        /// <summary>The page's words where voice.md overrules the artboard: a heading is a noun and never a
        /// question, one verb per thing, and no presets.</summary>
        [Fact]
        public void Updates_says_what_the_artboard_says_in_the_panel_s_voice()
        {
            Assert.Equal("Updates", PanelUpdates.Title);
            Assert.Equal("You have", PanelUpdates.YouHave);
            Assert.Equal("Restart SimHub to finish updating. Your settings are kept.", PanelUpdates.RestartNote);
            Assert.Equal("Release notes", PanelUpdates.ReleaseNotesTitle);
            Assert.Equal("Every release on GitHub", PanelUpdates.EveryRelease);
            Assert.Equal("Check for updates", PanelUpdates.CheckTitle);
            Assert.Equal("Check now", PanelUpdates.CheckNow);
            Assert.Equal("In SimHub", PanelUpdates.InSimHubTitle);
            Assert.Equal(new[] { "Item", "Version", "State" }, new[] { PanelUpdates.ItemColumn, PanelUpdates.VersionColumn, PanelUpdates.StateColumn });
            Assert.Equal("Reinstall everything", PanelConfirmation.ReinstallLabel);
            Assert.Equal("Download", PanelConfirmation.UpdateLabel);
            Assert.Equal("Replace anyway", PanelConfirmation.ReplaceAnyway);
            Assert.Equal("Put mine back", PanelUpdates.PutMineBack);
            Assert.Equal("Support", PanelUpdates.SupportTitle);
            Assert.Equal("Copy a support report", PanelUpdates.CopyReport);
            Assert.Equal("Open the log", PanelUpdates.OpenLog);
            Assert.Equal("Report an issue", PanelUpdates.ReportIssue);
            Assert.Equal("Read the guide", PanelUpdates.ReadGuide);
            Assert.Equal("Versions, devices and the last 200 log lines, copied to paste. Nothing is sent.", PanelUpdates.SupportCaption);
            // The noun phrase the old footer drew: the page does not describe itself.
            Assert.Equal("MIT licence", PanelUpdates.Licence);
            // The empty rig in the one phrase the panel has for it, which the Screens page owns and has frozen.
            Assert.Equal(PanelScreens.NoScreens + ". Add a screen on the Screens page.", PanelUpdates.NothingInSimHub);
        }

        /// <summary>The names a label may capitalise after its first word.</summary>
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal)
        {
            "OpenDash", "SimHub", "GitHub", "MIT", "Main",
        };

        [Fact]
        public void No_label_is_a_question_a_contraction_or_the_artboard_s_spelling()
        {
            var labels = new[]
            {
                PanelUpdates.Title, PanelUpdates.ReleaseNotesTitle, PanelUpdates.EveryRelease, PanelUpdates.CheckTitle,
                PanelUpdates.CheckNow, PanelUpdates.InSimHubTitle, PanelConfirmation.ReinstallLabel, PanelConfirmation.UpdateLabel,
                PanelUpdates.PutMineBack, PanelUpdates.SupportTitle, PanelUpdates.CopyReport, PanelUpdates.OpenLog,
                PanelUpdates.ReportIssue, PanelUpdates.ReadGuide, PanelUpdates.RestartNote, PanelUpdates.SupportCaption,
                PanelUpdates.Heading("0.5.1"), PanelUpdates.KeptTitle(new[] { "Main dash" }), PanelUpdates.KeptCaption(1),
                PanelUpdates.Licence,
            };
            foreach (var label in labels)
            {
                Assert.DoesNotContain("?", label);
                Assert.DoesNotContain("'", label);
                Assert.DoesNotContain("openDash", label);
                Assert.DoesNotContain("preset", label, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Repair", label);
                // Sentence case: the first letter is a capital and no other word is, bar the names.
                Assert.True(char.IsUpper(label[0]), label);
                foreach (var sentence in label.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    foreach (var word in sentence.Split(' ').Skip(1))
                    {
                        if (word.Length == 0 || !char.IsUpper(word[0])) continue;
                        Assert.True(Names.Contains(word.TrimEnd('.', ',')), label + ": " + word);
                    }
                }
            }
            Assert.Equal("Check for updates", PanelUpdates.CheckTitle);
            Assert.DoesNotContain("once a day", PanelUpdates.CheckTitle);
            // The switch's caption already says how often, and the label does not say it twice.
            Assert.Contains("once a day", UpdateWording.CheckCaption);
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorCheck = updates.check",
                "AnchorKept = updates.kept",
                "AnchorLights = updates.lights",
                "AnchorPackages = updates.packages",
                "AnchorPlugin = updates.plugin",
                "AnchorReinstall = updates.reinstall",
                "AnchorSupport = updates.support",
            }, AnchorTable.Of(typeof(PanelUpdates)));
        }

        // --- The update card --------------------------------------------------------------------------

        [Fact]
        public void The_card_shows_an_offer_a_download_or_a_restart_and_is_gone_otherwise()
        {
            Assert.Equal(UpdatesCard.Available, PanelUpdates.CardFor(UpdateState.UpdateAvailable, false, false));
            foreach (var state in new[] { UpdateState.Idle, UpdateState.Checking, UpdateState.UpToDate, UpdateState.Unreachable, UpdateState.Disabled })
            {
                Assert.Equal(UpdatesCard.None, PanelUpdates.CardFor(state, false, false));
            }
        }

        /// <summary>A run owns the card until it answers, and a staged plugin outranks the offer that staged
        /// it: a Download under it would fetch the same release again rather than finish anything.</summary>
        [Fact]
        public void A_run_outranks_a_staged_plugin_which_outranks_the_offer()
        {
            Assert.Equal(UpdatesCard.Downloading, PanelUpdates.CardFor(UpdateState.UpdateAvailable, true, true));
            Assert.Equal(UpdatesCard.Downloading, PanelUpdates.CardFor(UpdateState.Idle, true, false));
            Assert.Equal(UpdatesCard.Staged, PanelUpdates.CardFor(UpdateState.UpdateAvailable, false, true));
            Assert.Equal(UpdatesCard.Staged, PanelUpdates.CardFor(UpdateState.Disabled, false, true));
        }

        [Fact]
        public void The_card_is_headed_by_the_release_and_says_the_step()
        {
            Assert.Equal("OpenDash 0.5.1", PanelUpdates.Heading("0.5.1"));
            Assert.Equal("OpenDash 0.5.1", PanelUpdates.Heading(" 0.5.1 "));
            Assert.Equal("OpenDash", PanelUpdates.Heading(null));
            Assert.Equal(PanelUpdates.RestartNote, PanelUpdates.CardNote(UpdatesCard.Available, "0.5.1"));
            Assert.Equal("Downloading 0.5.1…", PanelUpdates.CardNote(UpdatesCard.Downloading, "0.5.1"));
            Assert.Equal("Downloading the update…", PanelUpdates.Downloading(null));
            Assert.Equal(UpdateWording.RestartLater, PanelUpdates.CardNote(UpdatesCard.Staged, "0.5.1"));
            Assert.Null(PanelUpdates.CardNote(UpdatesCard.None, "0.5.1"));
        }

        /// <summary>The card says "You have" only beside a version it knows, and heads its foot "Release notes"
        /// only over notes: a remembered offer carries none, and a heading over the link alone is not drawn.</summary>
        [Fact]
        public void The_card_draws_its_optional_parts_only_over_what_they_name()
        {
            Assert.True(PanelUpdates.ShowsYouHave("0.5.0"));
            Assert.False(PanelUpdates.ShowsYouHave(null));
            Assert.False(PanelUpdates.ShowsYouHave(" "));
            Assert.Equal("Release notes", PanelUpdates.NotesHeading("Faster flag box."));
            Assert.Null(PanelUpdates.NotesHeading(null));
            Assert.Null(PanelUpdates.NotesHeading(""));
            var code = PageCode();
            Assert.Contains("if (PanelUpdates.ShowsYouHave(installed))", code);
            Assert.Contains("var heading = PanelUpdates.NotesHeading(summary);", code);
        }

        [Fact]
        public void The_card_is_drawn_at_the_artboard_s_numbers()
        {
            Assert.Equal(20, PanelUpdates.CardPaddingX);
            Assert.Equal(18, PanelUpdates.CardPaddingY);
            Assert.Equal(18, PanelUpdates.CardGap);
            Assert.Equal(6, PanelUpdates.CardTextGap);
            Assert.Equal(12, PanelUpdates.CardHeadingGap);
            Assert.Equal(15, PanelUpdates.CardVersionSize);
            Assert.Equal(14, PanelUpdates.NotesPaddingTop);
            Assert.Equal(18, PanelUpdates.NotesPaddingBottom);
            Assert.Equal(8, PanelUpdates.NotesGap);
            Assert.Equal(4, PanelUpdates.YouHaveGap);
            Assert.Equal(1, PanelUpdates.YouHaveBaseline);
            var code = PageCode();
            Assert.Contains("Ui.HStack(PanelUpdates.YouHaveGap,", code);
            Assert.Contains("have.Margin = new Thickness(PanelUpdates.CardHeadingGap, 0, 0, PanelUpdates.YouHaveBaseline);", code);
        }

        /// <summary>A press sits beside its words while there is room for both, and under them below that,
        /// whatever the sidebar is doing.</summary>
        [Fact]
        public void A_press_goes_under_its_words_only_when_the_content_is_narrow()
        {
            Assert.True(PanelUpdates.ButtonBeside(Column(1200)));
            Assert.True(PanelUpdates.ButtonBeside(Column(3840)));
            Assert.True(PanelUpdates.ButtonBeside(679));
            Assert.True(PanelUpdates.ButtonBeside(520));
            Assert.False(PanelUpdates.ButtonBeside(519));
            Assert.False(PanelUpdates.ButtonBeside(340));
        }

        // --- The check row ----------------------------------------------------------------------------

        private static readonly DateTime Now = new DateTime(2026, 9, 30, 14, 5, 0, DateTimeKind.Utc);

        [Fact]
        public void The_check_row_says_when_the_last_check_answered()
        {
            var utc = TimeZoneInfo.Utc;
            Assert.Equal("Not checked yet", PanelUpdates.LastChecked(0, Now, utc));
            Assert.Equal("Last checked today, 09:12", PanelUpdates.LastChecked(new DateTime(2026, 9, 30, 9, 12, 0, DateTimeKind.Utc).Ticks, Now, utc));
            Assert.Equal("Last checked yesterday, 18:40", PanelUpdates.LastChecked(new DateTime(2026, 9, 29, 18, 40, 0, DateTimeKind.Utc).Ticks, Now, utc));
            Assert.Equal("Last checked 3 March, 07:05", PanelUpdates.LastChecked(new DateTime(2026, 3, 3, 7, 5, 0, DateTimeKind.Utc).Ticks, Now, utc));
            Assert.Equal("Last checked 31 December 2025, 23:59", PanelUpdates.LastChecked(new DateTime(2025, 12, 31, 23, 59, 0, DateTimeKind.Utc).Ticks, Now, utc));
        }

        [Fact]
        public void The_last_check_is_written_in_the_rig_s_own_time()
        {
            var plusTen = TimeZoneInfo.CreateCustomTimeZone("plus ten", TimeSpan.FromHours(10), "plus ten", "plus ten");
            // 23:30 UTC on the 29th is 09:30 on the 30th at +10, and now is 00:05 on the 1st there.
            Assert.Equal("Last checked yesterday, 09:30", PanelUpdates.LastChecked(new DateTime(2026, 9, 29, 23, 30, 0, DateTimeKind.Utc).Ticks, Now, plusTen));
        }

        [Fact]
        public void The_check_row_carries_the_line_only_while_the_card_is_gone()
        {
            Assert.Equal("Checking for updates…", PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Checking }));
            // The switch beside it already says the checks are off (voice.md).
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Disabled }));
            Assert.Equal("You have the newest release, 0.5.0.", PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = "0.5.0", Manual = true }));
            Assert.Equal("Could not reach GitHub. You have 0.5.0.", PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Unreachable, InstalledVersion = "0.5.0", Manual = true }));
            // A background check that finds nothing says nothing, and an offer is the card's.
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = "0.5.0" }));
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Unreachable, InstalledVersion = "0.5.0" }));
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.UpdateAvailable, LatestVersion = "0.5.1", Manual = true }));
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Idle }));
            Assert.Null(PanelUpdates.CheckLine(null));
        }

        /// <summary>A check in flight is said where the driver is looking: on the offer card, beside the
        /// Download it holds off, while an offer shows, and on the row otherwise.</summary>
        [Fact]
        public void A_check_in_flight_is_said_on_the_card_while_an_offer_shows_and_on_the_row_otherwise()
        {
            var checking = new UpdateStatus { State = UpdateState.Checking };
            Assert.Equal("Checking for updates…", PanelUpdates.CardLine(UpdatesCard.Available, checking));
            Assert.Null(PanelUpdates.RowLine(UpdatesCard.Available, checking));
            Assert.Equal("Checking for updates…", PanelUpdates.RowLine(UpdatesCard.None, checking));
            Assert.Equal("Checking for updates…", PanelUpdates.RowLine(UpdatesCard.Staged, checking));
            Assert.Null(PanelUpdates.CardLine(UpdatesCard.None, checking));

            var answered = new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = "0.5.0", Manual = true };
            Assert.Null(PanelUpdates.CardLine(UpdatesCard.Available, answered));
            Assert.Equal("You have the newest release, 0.5.0.", PanelUpdates.RowLine(UpdatesCard.None, answered));
            Assert.Null(PanelUpdates.RowLine(UpdatesCard.Staged, answered));
            Assert.Null(PanelUpdates.RowLine(UpdatesCard.None, null));
        }

        /// <summary>Download is held off while a check is in flight, whose answer redraws the card, so a
        /// second press cannot find the offer gone and say there is nothing to install.</summary>
        [Fact]
        public void Download_waits_for_a_check_in_flight_and_for_a_run()
        {
            Assert.True(PanelUpdates.DownloadEnabled(UpdateState.UpdateAvailable, false));
            Assert.False(PanelUpdates.DownloadEnabled(UpdateState.Checking, false));
            Assert.False(PanelUpdates.DownloadEnabled(UpdateState.UpdateAvailable, true));
        }

        /// <summary>The check refuses a press while the switch is off (UpdateCheck.ShouldCheck), so the
        /// press is not live then: a press that does nothing and says nothing reads as a broken panel.</summary>
        [Fact]
        public void Check_now_is_live_only_while_the_checks_are_on_and_nothing_is_running()
        {
            Assert.True(PanelUpdates.CheckNowEnabled(true, false, UpdateState.Idle));
            Assert.True(PanelUpdates.CheckNowEnabled(true, false, UpdateState.UpdateAvailable));
            Assert.False(PanelUpdates.CheckNowEnabled(false, false, UpdateState.Idle));
            Assert.False(PanelUpdates.CheckNowEnabled(true, true, UpdateState.Idle));
            Assert.False(PanelUpdates.CheckNowEnabled(true, false, UpdateState.Checking));
            Assert.False(UpdateCheck.ShouldCheck(false, 0, Now, manual: true));
        }

        /// <summary>Turning the switch back on brings the remembered offer back to the card and the badge at
        /// once, as the idle screen's mark shows it again, rather than after the panel is reopened.</summary>
        [Fact]
        public void The_switch_turned_on_brings_back_what_the_panel_opens_on()
        {
            var off = PanelUpdates.Switched(false, null, "0.5.1", "0.5.0");
            Assert.Equal(UpdateState.Disabled, off.State);
            Assert.Equal("0.5.0", off.InstalledVersion);

            var offer = PanelUpdates.Switched(true, null, "0.5.1", "0.5.0");
            Assert.Equal(UpdateState.UpdateAvailable, offer.State);
            Assert.Equal("0.5.1", offer.LatestVersion);

            var heard = new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = "0.5.0" };
            Assert.Same(heard, PanelUpdates.Switched(true, heard, null, "0.5.0"));

            // A "checks are off" heard earlier this start is not what the switch now says.
            var stale = PanelUpdates.Switched(true, new UpdateStatus { State = UpdateState.Disabled }, null, "0.5.0");
            Assert.Equal(UpdateState.Idle, stale.State);
            Assert.Equal("0.5.0", stale.InstalledVersion);
        }

        /// <summary>A Download waiting for its listing runs on the answer only onto an offer, and never over a
        /// staged plugin, which the check offers again while the old one still runs.</summary>
        [Fact]
        public void An_answer_runs_a_waiting_download_only_onto_an_offer_nothing_has_staged()
        {
            Assert.True(PanelUpdates.AppliesWhenAnswered(true, UpdateState.UpdateAvailable, false));
            Assert.False(PanelUpdates.AppliesWhenAnswered(true, UpdateState.UpdateAvailable, true));
            Assert.False(PanelUpdates.AppliesWhenAnswered(false, UpdateState.UpdateAvailable, false));
            Assert.False(PanelUpdates.AppliesWhenAnswered(true, UpdateState.UpToDate, false));
        }

        /// <summary>The page's own code, the draw files, as one string without comments.</summary>
        private static string PageCode()
        {
            return string.Concat(RepoPaths.SettingsControlSources()
                .Where(p => System.IO.Path.GetFileName(p).StartsWith("SettingsControl.Updates", StringComparison.Ordinal))
                .Select(RepoPaths.Code));
        }

        /// <summary>
        /// The page reaches the shell through its hooks alone: pageHost and PendingRestart are the shell's
        /// internals, which it may change without a word to any page. A waiting Download is spent by the
        /// release it finds and dropped when its answer was missed; the disk is not read under a download.
        /// </summary>
        [Fact]
        public void The_page_reads_only_the_shell_s_hooks_and_leaves_the_disk_to_a_run_in_progress()
        {
            var code = PageCode();
            Assert.DoesNotContain("pageHost", code);
            Assert.DoesNotContain("PendingRestart(", code);
            Assert.Contains("PluginUpdate.Pending(plugin.Installer.SimHubRoot)", code);
            Assert.Contains("if (!updatesRead && !applying)", code);
            Assert.Contains("if (!applying) plugin.Installer.Refresh();", code);
            Assert.Contains("if (release != null) applyWaiting = false;", code);
            Assert.Contains("if (updateStatus.State != UpdateState.Checking) applyWaiting = false;", code);
            Assert.Contains("PanelUpdates.AppliesWhenAnswered(waiting, updateStatus.State, UpdatesPending())", code);
        }

        /// <summary>Download and Check now carry no hover: the button and the card's heading already say what
        /// Download fetches, and the row's caption and the button say what Check now asks (voice.md: where the
        /// control already says it, say nothing).</summary>
        [Fact]
        public void Download_and_Check_now_carry_no_hover_that_repeats_them()
        {
            var code = PageCode();
            Assert.DoesNotContain("updatesDownload.ToolTip", code);
            Assert.DoesNotContain("updatesCheckNow.ToolTip", code);
        }

        [Fact]
        public void The_check_row_is_drawn_at_the_artboard_s_numbers()
        {
            Assert.Equal(12, PanelUpdates.CheckControlsGap);
            Assert.Equal(520, PanelUpdates.CheckLineWidth);
            var code = PageCode();
            // The artboard's .row at its own 14 and with no rule over it, held to the kit's number.
            Assert.Contains("row.Padding = new Thickness(0, PanelKit.RowPaddingYUpdates, 0, PanelKit.RowPaddingYUpdates);", code);
            Assert.Contains("row.BorderThickness = new Thickness(0);", code);
            Assert.Contains("updatesLastChecked = Ui.Caption(string.Empty, PanelUpdates.CheckLineWidth);", code);
            Assert.Contains("updatesCheckLine = Ui.Caption(string.Empty, PanelUpdates.CheckLineWidth);", code);
            Assert.Contains("Ui.HStack(PanelUpdates.CheckControlsGap, updatesCheckNow, toggle)", code);
        }

        /// <summary>"Last checked today" is said against now, so the page redraws it every second it shows: a
        /// page left open past midnight would otherwise go on saying "today" of yesterday's check.</summary>
        [Fact]
        public void The_last_check_is_kept_true_while_the_page_stays_open()
        {
            var code = PageCode();
            var tick = code.IndexOf("OnTick(() =>", StringComparison.Ordinal);
            Assert.True(tick >= 0);
            Assert.Contains("updatesLastChecked.Text = PanelUpdates.LastChecked(Settings.LastUpdateCheckTicks, DateTime.UtcNow);", code.Substring(tick, 300));
        }

        /// <summary>The read the page takes once per visit is dropped when the panel leaves the screen, since
        /// the return rebuilds the page and is not a Go: a dashboard edited in Dash Studio meanwhile is read
        /// again, rather than the kept card offering a copy that Put mine back then finds it may not restore.
        /// Through the control's own event, and let go of with the build.</summary>
        [Fact]
        public void The_page_reads_the_disk_again_after_the_panel_was_away()
        {
            var code = PageCode();
            Assert.Contains("RoutedEventHandler away = (sender, args) => updatesRead = false;", code);
            Assert.Contains("Unloaded += away;", code);
            Assert.Contains("OnDrop(() => Unloaded -= away);", code);
            Assert.Contains("OnLeave(\"Updates.read\", () => updatesRead = false);", code);
        }

        /// <summary>
        /// A download's completion is posted to the interface thread, never waited on from the pool: End runs
        /// on that thread and waits for the run (UpdateService.WaitForIdle), so a synchronous Invoke inside
        /// the counted work hung SimHub's close for the whole grace. The yes to replacing edited dashboards on
        /// restart is set before the run counts as finished, so End's save keeps it when the completion does
        /// not run.
        /// </summary>
        [Fact]
        public void A_download_s_completion_never_waits_on_the_thread_that_waits_for_it()
        {
            var code = PageCode();
            var apply = code.Substring(code.IndexOf("private void ApplyUpdate()", StringComparison.Ordinal));
            apply = apply.Substring(0, apply.IndexOf("private async void OfferRestart(", StringComparison.Ordinal));
            Assert.DoesNotContain("Dispatcher.Invoke(", apply);
            var work = apply.IndexOf("outcome = Updates.Apply(plugin.Installer, release, replaceEdited, report);", StringComparison.Ordinal);
            var consent = apply.IndexOf("if (outcome.ReplaceEditedOnRestart) Settings.ReplaceEditedFor = release.Version;", StringComparison.Ordinal);
            var posted = apply.IndexOf("Dispatcher.BeginInvoke(new Action(() =>", work, StringComparison.Ordinal);
            Assert.True(work >= 0 && consent > work && posted > consent, "the consent is set on the pool thread before the completion is posted");
            Assert.Contains("}, new SimHubInstallLog(), mustFinish: true);", apply);

            // A run that throws still posts a completion, carrying a failed outcome whose line points at the log.
            var caught = apply.IndexOf("outcome = new UpdateOutcome { Reason = PanelUpdates.ApplyThrew };", StringComparison.Ordinal);
            Assert.True(caught > work && caught < consent, "a throw inside Apply becomes a failed outcome before the completion is posted");
            Assert.Contains("Dispatcher.BeginInvoke(new Action(() => UpdatesApplied(release, outcome)));", apply);
            Assert.Equal("The update did not finish: see SimHub's log.", new UpdateOutcome { Reason = PanelUpdates.ApplyThrew }.Line);

            // The completion clears the run before anything that can throw, keeps a net of its own, and offers
            // the restart outside it.
            var applied = apply.Substring(apply.IndexOf("private void UpdatesApplied(", StringComparison.Ordinal));
            var cleared = applied.IndexOf("applying = false;", StringComparison.Ordinal);
            var net = applied.IndexOf("try", StringComparison.Ordinal);
            var logged = applied.IndexOf("Log.Error(\"Finishing the update on the panel failed\", ex);", StringComparison.Ordinal);
            var restart = applied.IndexOf("if (outcome.PluginStaged) OfferRestart(release.Version);", StringComparison.Ordinal);
            Assert.True(cleared >= 0 && net > cleared && logged > net && restart > logged, "applying is cleared first, the rest is caught, and the restart is offered after the net");
        }

        /// <summary>
        /// A run that finishes while SimHub shows another of its pages leaves the page to the return: a build's
        /// controls outlive the panel being away, so the completion redraws and counts the disk as read only
        /// while the control is loaded, and the return rebuilds and reads it itself.
        /// </summary>
        [Fact]
        public void A_run_that_finishes_while_the_panel_is_away_leaves_the_page_to_the_return()
        {
            var code = PageCode();
            var applied = code.Substring(code.IndexOf("private void UpdatesApplied(", StringComparison.Ordinal));
            applied = applied.Substring(0, applied.IndexOf("private async void OfferRestart(", StringComparison.Ordinal));
            var showing = applied.IndexOf("var showing = updatesCardHost != null && IsLoaded;", StringComparison.Ordinal);
            var guarded = applied.IndexOf("if (showing)", StringComparison.Ordinal);
            var read = applied.IndexOf("updatesRead = true;", StringComparison.Ordinal);
            var redraw = applied.IndexOf("Redraw();", StringComparison.Ordinal);
            var otherwise = applied.IndexOf("else", guarded, StringComparison.Ordinal);
            Assert.True(showing >= 0 && guarded > showing && read > guarded && redraw > read && otherwise > redraw, "the read and the redraw are the showing page's alone");
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(applied, @"updatesRead = true;"));
            Assert.Contains("if (updatesCardHost != null) Say(outcome.Line, outcome.Ok);", applied);
        }

        // --- In SimHub --------------------------------------------------------------------------------

        [Fact]
        public void The_table_is_drawn_at_the_artboard_s_numbers()
        {
            Assert.Equal(110, PanelUpdates.TableVersionWidth);
            Assert.Equal(150, PanelUpdates.TableStateWidth);
            Assert.Equal(16, PanelUpdates.TableGap);
            Assert.Equal(16, PanelUpdates.TableRowPaddingX);
            Assert.Equal(11, PanelUpdates.TableRowPaddingY);
            Assert.Equal(12, PanelUpdates.TableHeadPaddingTop);
            Assert.Equal(14, PanelUpdates.TableTextSize);
            Assert.Equal(12, PanelUpdates.TableKindSize);
            Assert.Equal(15, PanelUpdates.TableVersionSize);
            Assert.Equal(13, PanelUpdates.TableStateSize);
            Assert.Equal(7, PanelUpdates.TableDot);
            Assert.Equal(8, PanelUpdates.TableDotGap);
            Assert.Equal(12, PanelUpdates.SectionGap);
            Assert.Equal(14, PanelUpdates.ReinstallLineGap);
        }

        [Fact]
        public void The_version_column_goes_before_the_name_runs_out_of_room()
        {
            // The gap is outside each fixed column's own width, as the artboard's grid gap is.
            Assert.Equal(126, PanelUpdates.ColumnWidth(PanelUpdates.TableVersionWidth));
            Assert.Equal(166, PanelUpdates.ColumnWidth(PanelUpdates.TableStateWidth));
            Assert.Equal(0, PanelUpdates.ColumnWidth(0));
            // The card's border, the row's padding and the two fixed columns come off the content before a
            // name has any room.
            Assert.Equal(2, PanelUpdates.TableBorder);
            Assert.Equal(326, PanelUpdates.TableBorder + 2 * PanelUpdates.TableRowPaddingX + PanelUpdates.ColumnWidth(PanelUpdates.TableVersionWidth) + PanelUpdates.ColumnWidth(PanelUpdates.TableStateWidth));
            Assert.Equal(154, PanelUpdates.TableNameMin);
            Assert.Equal(110, PanelUpdates.VersionWidth(Column(1200)));
            Assert.Equal(110, PanelUpdates.VersionWidth(Column(3840)));
            Assert.Equal(110, PanelUpdates.VersionWidth(480));
            Assert.Equal(0, PanelUpdates.VersionWidth(479));
            Assert.Equal(0, PanelUpdates.VersionWidth(340));
            // A light row's Update takes the press column from every row, the name's included, so the version
            // goes sooner: at 480 to 559 it would leave a name 75 to 153 wide beside a full version and state.
            Assert.Equal(80, PanelUpdates.TablePressColumn);
            Assert.Equal(110, PanelUpdates.VersionWidth(560, hasPress: true));
            Assert.Equal(0, PanelUpdates.VersionWidth(559, hasPress: true));
            Assert.Equal(110, PanelUpdates.VersionWidth(Column(1200), hasPress: true));
            Assert.Equal(110, PanelUpdates.VersionWidth(Column(3840), hasPress: true));
        }

        /// <summary>
        /// The build reads the content width only up to the widest width it draws anything differently at:
        /// past 560 the version column, the press beside its words and everything else are the same at every
        /// width, so a resize there does not rebuild the page, which walks SimHub's LED devices, parses the
        /// matrix profile and lists the kept copies on the interface thread, and withdraws a question the
        /// driver was about to answer. The cap is tight: one pixel under it the version column goes.
        /// </summary>
        [Fact]
        public void The_page_reads_the_width_only_as_far_as_it_draws_from_it()
        {
            Assert.Equal(560, PanelUpdates.WidthDrawnUpTo);
            Assert.True(PanelUpdates.WidthDrawnUpTo >= PanelUpdates.ButtonBesideFrom);
            var at = PanelUpdates.WidthDrawnUpTo;
            foreach (var width in new[] { at, at + 1, 879, Column(1200), 2000, Column(3840), 10000 })
            {
                Assert.Equal(PanelUpdates.ButtonBeside(at), PanelUpdates.ButtonBeside(width));
                Assert.Equal(PanelUpdates.VersionWidth(at), PanelUpdates.VersionWidth(width));
                Assert.Equal(PanelUpdates.VersionWidth(at, hasPress: true), PanelUpdates.VersionWidth(width, hasPress: true));
            }
            Assert.NotEqual(PanelUpdates.VersionWidth(at, hasPress: true), PanelUpdates.VersionWidth(at - 1, hasPress: true));
            var code = PageCode();
            Assert.Contains("updatesWidth = ContentWidthUpTo(PanelUpdates.WidthDrawnUpTo);", code);
            Assert.DoesNotContain("= ContentWidth;", code);
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(code, @"\bContentWidth\b(?!UpTo)"), "the Updates files never read the width whole");
        }

        /// <summary>A column that cannot fit stacks and never vanishes: where the version column has gone, the
        /// version follows the row's kind in the name cell, and every paint writes it, so the table says which
        /// version SimHub holds at every width and after every press.</summary>
        [Fact]
        public void The_version_follows_the_kind_where_its_column_has_gone()
        {
            Assert.Equal(" · 0.4.2", PanelUpdates.VersionInName(0, "0.4.2"));
            Assert.Equal(" · Unknown", PanelUpdates.VersionInName(0, PanelUpdates.VersionText(Versioning.UnknownVersion)));
            Assert.Equal(string.Empty, PanelUpdates.VersionInName(110, "0.4.2"));
            Assert.Equal(string.Empty, PanelUpdates.VersionInName(0, string.Empty));
            var code = PageCode();
            Assert.Contains("stacked.Text = PanelUpdates.VersionInName(versionWidth, current.Version);", code);
            Assert.Contains("name.Inlines.Add(stacked);", code);
        }

        /// <summary>The strip-outdated fix lands on the row whose Update is the fix, not on the first light row
        /// whatever it offers, and on the table when no row offers one.</summary>
        [Fact]
        public void The_strip_outdated_fix_lands_on_the_first_row_that_offers_an_update()
        {
            var current = PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate });
            var older = PanelUpdates.StripRow("Dash", new FlagBoxPlan { State = FlagBoxInstallState.Outdated });
            Assert.Equal(1, PanelUpdates.LightsAnchor(new[] { current, older }));
            Assert.Equal(0, PanelUpdates.LightsAnchor(new[] { older, current }));
            Assert.Equal(-1, PanelUpdates.LightsAnchor(new[] { current }));
            Assert.Equal(-1, PanelUpdates.LightsAnchor(null));
            var code = PageCode();
            Assert.Contains("var anchor = PanelUpdates.LightsAnchor(lightRows);", code);
            Assert.Contains("if (anchor >= 0) Ui.Anchor(lights[anchor], PanelUpdates.AnchorLights);", code);
            Assert.Contains("else Ui.Anchor(table, PanelUpdates.AnchorLights);", code);
        }

        /// <summary>Only a row that draws its Update takes the press column: an older light profile on a
        /// device SimHub lists. The flag box's row is drawn only on a rig with a matrix and a build that
        /// carries the profile.</summary>
        [Fact]
        public void The_table_s_press_column_and_flag_box_row_are_drawn_only_when_used()
        {
            var older = new FlagBoxPlan { State = FlagBoxInstallState.Outdated };
            Assert.True(PanelUpdates.TableHasPress(new[] { null, PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate }), PanelUpdates.StripRow("Dash", older) }));
            Assert.True(PanelUpdates.TableHasPress(new[] { PanelUpdates.FlagBoxRow("OpenDash Flag box", older, null) }));
            // An older profile on a device SimHub does not list offers nothing, and takes no room for it.
            Assert.False(PanelUpdates.TableHasPress(new[] { PanelUpdates.StripRow("Dash", older, deviceListed: false) }));
            Assert.False(PanelUpdates.TableHasPress(new[] { PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled }), PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.Failed }), null }));
            Assert.False(PanelUpdates.TableHasPress(null));
            Assert.True(PanelUpdates.DrawsFlagBoxRow(true, true));
            Assert.False(PanelUpdates.DrawsFlagBoxRow(false, true));
            Assert.False(PanelUpdates.DrawsFlagBoxRow(true, false));
        }

        /// <summary>The flag box's by-hand route is drawn only while SimHub's matrix settings are out of reach,
        /// and by this page, so it fits its column: the path box gives up width to the press beside it, never
        /// wider than 320, where the shared route's fixed 320 beside the press is about 555 px and clipped in
        /// the narrow column.</summary>
        [Fact]
        public void The_flag_box_s_by_hand_route_fits_its_column()
        {
            Assert.True(PanelUpdates.ShowsImportFallback(new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }));
            Assert.False(PanelUpdates.ShowsImportFallback(new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled }));
            Assert.False(PanelUpdates.ShowsImportFallback(null));
            Assert.Equal(320, PanelUpdates.ImportPathWidth);
            Assert.Equal(12, PanelUpdates.ImportGap);
            Assert.Equal(8, PanelUpdates.ImportLineGap);
            Assert.Equal("Could not copy the profile. See SimHub's log.", PanelUpdates.CopyForImportFailed);
            Assert.Equal(@"Copied to C:\Users\Rim\Documents\SimHub\flag-box.json. In SimHub, open your device's profiles and press Import.",
                PanelUpdates.CopiedForImport(@"C:\Users\Rim\Documents\SimHub\flag-box.json"));
            var code = PageCode();
            Assert.Contains("if (PanelUpdates.ShowsImportFallback(flagBoxPlan))", code);
            Assert.Contains("var fallback = UpdatesFlagBoxFallback(flagBoxPlan);", code);
            Assert.DoesNotContain("BuildFlagBoxImportFallback(", code);
            Assert.DoesNotContain("MaxWidth = width", code);
            Assert.Contains("new ColumnDefinition { Width = new GridLength(PanelUpdates.ImportBoxWeight, GridUnitType.Star), MaxWidth = PanelUpdates.ImportPathWidth }", code);
            Assert.DoesNotContain("Width = 320", code);
        }

        [Fact]
        public void A_row_s_kind_follows_its_name()
        {
            Assert.Equal(" · dashboard", PanelUpdates.KindCaption(PanelUpdates.DashboardKind));
            Assert.Equal(" · LED profile", PanelUpdates.KindCaption(PanelUpdates.StripKind));
            Assert.Equal(" · matrix profile", PanelUpdates.KindCaption(PanelUpdates.MatrixKind));
            Assert.Equal(string.Empty, PanelUpdates.KindCaption(null));
        }

        [Fact]
        public void A_version_is_written_as_the_copy_says_it()
        {
            Assert.Equal("0.5.0", PanelUpdates.VersionText("0.5.0"));
            Assert.Equal("Unknown", PanelUpdates.VersionText(Versioning.UnknownVersion));
            Assert.Equal(string.Empty, PanelUpdates.VersionText(null));
            Assert.Equal(string.Empty, PanelUpdates.VersionText(" "));
        }

        private static PackageStatus Package(InstallStatus status, string version = "0.5.0", bool edited = false, bool? heldBack = null)
        {
            return new PackageStatus { FolderName = "OpenDash Rim", Status = status, InstalledVersion = version, EmbeddedVersion = "0.5.0", Edited = edited, HeldBack = heldBack ?? edited };
        }

        private static readonly ScreenInstance Rim = new ScreenInstance { Name = "Rim", Kind = Contract.KindFace, Width = 1280, Height = 480, Folder = "OpenDash Rim" };

        [Fact]
        public void A_dashboard_row_says_its_state_in_the_install_words()
        {
            var current = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), true, false);
            Assert.Equal("Rim", current.Name);
            Assert.Equal("dashboard", current.Kind);
            Assert.Equal("0.5.0", current.Version);
            Assert.Equal("Up to date", current.State);
            Assert.Equal(Theme.StatusUpToDate, current.StateHex);
            Assert.Equal(Theme.StatusUpToDate, current.DotHex);
            Assert.Null(current.Tooltip);
            Assert.False(current.OffersUpdate);

            var older = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpdateAvailable, "0.4.2"), true, false);
            Assert.Equal("Update available", older.State);
            Assert.Equal(Theme.StatusUpdateAvailable, older.StateHex);
            Assert.Equal("0.4.2", older.Version);
            Assert.Equal("Reinstall everything brings it to 0.5.0.", older.Tooltip);

            const string edited = "You have edited it, so OpenDash left it alone. Reinstall everything replaces it.";
            Assert.Equal(edited, PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpdateAvailable, "0.4.2", true), true, false).Tooltip);
            // Either fact alone is the driver's work.
            Assert.Equal(edited, PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpdateAvailable, "0.4.2", edited: true, heldBack: false), true, false).Tooltip);
            Assert.Equal(edited, PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpdateAvailable, "0.4.2", edited: false, heldBack: true), true, false).Tooltip);
            Assert.Equal("Reinstall everything brings it up to date.", PanelUpdates.BringsItTo(null));

            var none = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.NotInstalled, null), null, null);
            Assert.Equal("Not installed", none.State);
            Assert.Equal(Theme.TextLabel, none.StateHex);
            Assert.Equal(Theme.StatusNotInstalled, none.DotHex);
            Assert.Equal(string.Empty, none.Version);
            Assert.Equal("Reinstall everything installs it.", none.Tooltip);
        }

        /// <summary>A failure outranks everything, then a folder that has gone, then one SimHub has not loaded
        /// yet, which is said only when the shell's facts know it. The restart is said in ruling 69's words,
        /// which fit the state column on one line where the Screens card's step does not, and the hover says
        /// only the step after it, never the state beside it again.</summary>
        [Fact]
        public void A_dashboard_row_puts_a_failure_then_a_missing_folder_then_a_restart_first()
        {
            var failed = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.Failed), false, true);
            Assert.Equal("Install failed", failed.State);
            Assert.Equal(Theme.StatusFailed, failed.StateHex);
            Assert.Equal("See SimHub's log.", failed.Tooltip);

            var missing = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), false, true);
            Assert.Equal("Missing from SimHub", missing.State);
            Assert.Equal(Theme.StatusFailed, missing.StateHex);
            Assert.Equal(string.Empty, missing.Version);
            Assert.Equal("Reinstall everything installs it again.", missing.Tooltip);

            var waiting = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), true, true);
            Assert.Equal("Waiting for a restart", waiting.State);
            Assert.Equal("Waiting for a restart", PanelUpdates.WaitingForRestart);
            Assert.Equal(Theme.Caution, waiting.StateHex);
            Assert.Equal("0.5.0", waiting.Version);
            Assert.Equal("After the restart, assign \"Rim\" to its display in Dash Studio.", waiting.Tooltip);
            Assert.DoesNotContain("Restart SimHub", waiting.Tooltip);
            // One line in the cell: ruling 69's words at about 113 of the 135 the dot leaves them, where the
            // Screens card's "Restart SimHub to load it" is about 140 and wraps "it" alone.
            Assert.True(StateWidth(PanelUpdates.WaitingForRestart) <= PanelUpdates.TableStateRoom);
            Assert.True(StateWidth("Restart SimHub to load it") > PanelUpdates.TableStateRoom, "the measure tells a state that wraps from one that fits");

            // Unknown facts say nothing of a restart.
            Assert.Equal("Up to date", PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), null, null).State);
        }

        /// <summary>
        /// Every word the State column writes, dashboard and light rows alike, fits the cell on one line: the
        /// 150 column less the 7 dot and its 8 gap, measured in Barlow Regular 13 from the font the panel
        /// bundles. A longer state word wraps and makes its row the one taller row of the table.
        /// </summary>
        [Fact]
        public void Every_state_fits_its_cell_on_one_line()
        {
            Assert.Equal(135, PanelUpdates.TableStateRoom);
            var states = new[]
            {
                PanelUpdates.UpToDate, PanelUpdates.UpdateAvailable, PanelUpdates.MissingFromSimHub, PanelUpdates.WaitingForRestart,
                PanelUpdates.Unknown, PanelCopy.Installed, PanelCopy.NotInstalled, PanelCopy.InstallFailed,
            }.Concat(Enum.GetValues(typeof(FlagBoxInstallState)).Cast<FlagBoxInstallState>().Select(state => PanelCopy.LightRow(state, "0.4.0").State));
            foreach (var state in states)
            {
                var width = StateWidth(state);
                Assert.True(width <= PanelUpdates.TableStateRoom, "\"" + state + "\" is " + width.ToString("0.0") + " px in a " + PanelUpdates.TableStateRoom + " px cell");
            }
        }

        /// <summary>A state's width as the cell draws it: Barlow Regular at the state's 13, off the advances in
        /// the TTF the panel embeds. Kerning is left out; it moves a word this short by well under a pixel.</summary>
        private static double StateWidth(string text)
        {
            return TrueTypeAdvances.Of(System.IO.Path.Combine(RepoPaths.EmbeddedResources(), "fonts", "Barlow-Regular.ttf")).Width(text, PanelUpdates.TableStateSize);
        }

        /// <summary>A screen this build ships nothing for is left as it is by the installer, so its row says
        /// what the facts say and why no press here changes it, rather than "Not installed" with a press that
        /// would leave it alone. Installed is said in the installed ink, dot and words agreeing, as the Screens
        /// card draws it, and facts not read are "Unknown", as a light row's are.</summary>
        [Fact]
        public void A_screen_this_build_ships_nothing_for_says_so()
        {
            var inSimHub = PanelUpdates.DashboardRow(Rim, null, true, false);
            Assert.Equal("Installed", inSimHub.State);
            Assert.Equal(Theme.StatusUpToDate, inSimHub.StateHex);
            Assert.Equal(Theme.StatusUpToDate, inSimHub.DotHex);
            Assert.Equal("This build ships no 1280 × 480 face.", inSimHub.Tooltip);
            Assert.Equal(string.Empty, inSimHub.Version);
            Assert.Equal("Missing from SimHub", PanelUpdates.DashboardRow(Rim, null, false, false).State);
            var unread = PanelUpdates.DashboardRow(Rim, null, null, null);
            Assert.Equal("Unknown", unread.State);
            Assert.Equal(Theme.TextLabel, unread.StateHex);
            var pitWall = new ScreenInstance { Name = "Pit", Kind = Contract.KindPitWall, Width = 1920, Height = 1080 };
            Assert.Equal("This build ships no 1920 × 1080 pit wall.", PanelUpdates.DashboardRow(pitWall, null, null, null).Tooltip);
        }

        /// <summary>The sentences under the table: what the build ships and what the installer could not do,
        /// then the light profiles' own, which uncovered 24 puts here, said only over a light row. Every note
        /// about the build opens the same way, and names the profiles as the rows do.</summary>
        [Fact]
        public void The_table_s_notes_say_what_the_build_ships_and_what_could_not_be_read()
        {
            Assert.Equal("OpenDash never installs a profile on its own.", PanelLightRows.SectionCaption);
            Assert.Equal("SimHub's LED settings are not available.", PanelLightRows.Unavailable);
            Assert.Equal("This build ships no LED profiles.", PanelLightRows.NoProfiles);
            Assert.Equal("This build ships no dashboards.", PanelUpdates.NoDashboards);
            Assert.Contains(PanelUpdates.StripKind, PanelLightRows.NoProfiles);

            Assert.Equal(new[] { PanelLightRows.SectionCaption }, PanelUpdates.TableNotes(true, null, false, true, true, true, true));
            Assert.Equal(new[] { "This build ships no dashboards.", PanelLightRows.SectionCaption },
                PanelUpdates.TableNotes(false, "anything", false, true, false, false, true));
            Assert.Equal(new[] { "OpenDash could not read or write a dashboard. See SimHub's log." },
                PanelUpdates.TableNotes(true, "disk full", false, false, false, true, true));
            // A row already says "Install failed", and the note would repeat it.
            Assert.Empty(PanelUpdates.TableNotes(true, "disk full", true, false, false, true, true));
            Assert.Equal(new[] { PanelLightRows.SectionCaption, PanelLightRows.NoProfiles }, PanelUpdates.TableNotes(true, null, false, true, true, false, true));
            Assert.Equal(new[] { PanelLightRows.SectionCaption, PanelLightRows.Unavailable }, PanelUpdates.TableNotes(true, null, false, true, true, true, false));
            // The flag box row alone is a light row, and the sentence is about it.
            Assert.Equal(new[] { PanelLightRows.SectionCaption }, PanelUpdates.TableNotes(true, null, false, true, false, false, false));
            // No strip and no matrix: no light row, so no sentence about profiles.
            Assert.Empty(PanelUpdates.TableNotes(true, null, false, false, false, false, false));
        }

        /// <summary>A strip row says what its row does not already say: nothing while its profile is current,
        /// the version its Update brings while it is older (what the press costs is the press's own tooltip),
        /// and the press that installs a missing one with the step OpenDash does not take.</summary>
        [Fact]
        public void A_strip_row_names_the_strip_and_offers_an_update_only_while_its_profile_is_older()
        {
            var older = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Outdated, InstalledVersion = "0.4.2", EmbeddedVersion = "0.5.0" });
            Assert.Equal("Wheel rim", older.Name);
            Assert.Equal("LED profile", older.Kind);
            Assert.Equal("0.4.2", older.Version);
            Assert.Equal("Update available", older.State);
            Assert.Equal(Theme.StatusUpdateAvailable, older.StateHex);
            Assert.Equal(Theme.StatusUpdateAvailable, older.DotHex);
            Assert.True(older.OffersUpdate);
            Assert.Equal("Update brings it to 0.5.0.", older.Tooltip);
            Assert.Equal("Update brings it up to date.", PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Outdated }).Tooltip);

            var current = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0" });
            Assert.Equal("Up to date", current.State);
            // As a dashboard's up-to-date row: the state and the version already say it all.
            Assert.Null(current.Tooltip);
            Assert.False(current.OffersUpdate);

            var none = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled });
            Assert.Equal("Not installed", none.State);
            Assert.Equal(Theme.StatusNotInstalled, none.DotHex);
            Assert.Equal("Reinstall everything installs it, then select it on its device.", none.Tooltip);
            Assert.False(none.OffersUpdate);

            // What the page cannot know is said as not known, not as not installed.
            var unreachable = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable });
            Assert.Equal("Unknown", unreachable.State);
            Assert.Equal(Theme.TextLabel, unreachable.StateHex);
            Assert.Equal(PanelLightRows.Unavailable, unreachable.Tooltip);
            var notEmbedded = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded });
            Assert.Equal("Unknown", notEmbedded.State);
            Assert.Equal("This build ships no profile for this strip.", notEmbedded.Tooltip);
            Assert.False(notEmbedded.OffersUpdate);

            // The state already says it failed; the hover says where to look.
            var failed = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Failed });
            Assert.Equal("Install failed", failed.State);
            Assert.Equal("See SimHub's log.", failed.Tooltip);
        }

        /// <summary>A strip whose device SimHub does not list keeps the state SimHub holds, offers no press that
        /// could only fail, and says the step as the LEDs page does, with the page it is on.</summary>
        [Fact]
        public void A_strip_on_a_device_SimHub_does_not_list_says_the_step_and_offers_no_press()
        {
            const string step = "SimHub does not list this strip's device. Choose one under SimHub device on the LEDs page.";
            Assert.Equal(step, PanelUpdates.DeviceNotListed);
            var older = PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.Outdated, InstalledVersion = "0.4.2", EmbeddedVersion = "0.5.0" }, deviceListed: false);
            Assert.Equal("Update available", older.State);
            Assert.Equal("0.4.2", older.Version);
            Assert.False(older.OffersUpdate);
            Assert.Equal(step, older.Tooltip);
            Assert.Equal(step, PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled }, deviceListed: false).Tooltip);
            Assert.Equal(step, PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.Failed }, deviceListed: false).Tooltip);
            // A state no press writes from is said as it is, device or not.
            Assert.Null(PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0" }, deviceListed: false).Tooltip);
            Assert.Equal(PanelLightRows.Unavailable, PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, deviceListed: false).Tooltip);

            var devices = new Dictionary<string, string> { { LedBar.ArduinoDevice, "Arduino" } };
            var arduino = new LedBar { Name = "Rim", Device = LedBar.ArduinoDevice };
            Assert.True(PanelUpdates.DeviceListed(devices, arduino));
            Assert.Equal("Arduino", PanelUpdates.DeviceName(devices, arduino));
            var gone = new LedBar { Name = "Gone", Device = LedBar.DeviceId(new Guid("0b7b8d4e-5f1a-4c3e-9a55-2f7c1d9e6a10")) };
            Assert.False(PanelUpdates.DeviceListed(devices, gone));
            Assert.Null(PanelUpdates.DeviceName(devices, gone));
            Assert.False(PanelUpdates.DeviceListed(null, arduino));
        }

        /// <summary>
        /// A strip whose shape this build carries no profile for is NotEmbedded whatever SimHub holds: the
        /// census compares SimHub's copy with no embedded version, so a stamped copy (rc.2's brow profiles)
        /// reads older and an unstamped one current, and the row would offer an Update that writes nothing.
        /// </summary>
        [Fact]
        public void A_strip_this_build_ships_no_profile_for_reads_unknown_whatever_SimHub_holds()
        {
            var brow = new LedBar { Name = "Brow", Namespace = "LedBrow", Shape = "brow-12", Device = LedBar.ArduinoDevice };
            var rim = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3", Device = LedBar.ArduinoDevice };
            const string stamped = "Shift lights, flags and the spotter on one LED strip. Built by OpenDash 0.3.0-rc.2; do not edit here, it is replaced on update.";
            var arduino = new List<InstalledProfile>
            {
                new InstalledProfile { ProfileId = LedBarProfile.IdFor(brow.Namespace), Description = stamped },
            };
            // What the census says of it when the build has nothing to compare with: older, with a press.
            var raw = LedBarProfile.Plan(brow, null, new[] { arduino });
            Assert.Equal(FlagBoxInstallState.Outdated, raw.State);
            Assert.True(PanelUpdates.StripRow("Brow", raw).OffersUpdate);

            var census = new[]
            {
                new KeyValuePair<LedBar, FlagBoxPlan>(brow, raw),
                new KeyValuePair<LedBar, FlagBoxPlan>(rim, new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled }),
            };
            var plans = PanelUpdates.StripPlans(census, true, new[] { "3-9-3" });
            Assert.Equal(FlagBoxInstallState.NotEmbedded, plans[0].Value.State);
            Assert.Equal("0.3.0-rc.2", plans[0].Value.InstalledVersion);
            Assert.Same(census[1].Value, plans[1].Value);
            var row = PanelUpdates.StripRow("Brow", plans[0].Value);
            Assert.Equal("Unknown", row.State);
            Assert.Equal("0.3.0-rc.2", row.Version);
            Assert.Equal(PanelUpdates.StripNotEmbedded, row.Tooltip);
            Assert.False(row.OffersUpdate);
            Assert.False(PanelUpdates.TableHasPress(new[] { row }));
            // Neither press picks it: Reinstall everything would say it failed with nothing in the log.
            Assert.Equal(new[] { rim }, PanelUpdates.StripsToWrite(plans, true, PanelUpdates.ReinstallWrites, bar => true).Select(e => e.Key));
            Assert.Empty(PanelUpdates.StripsToWrite(plans, true, PanelUpdates.RowUpdateWrites(PanelUpdates.StripKey(brow)), bar => true));

            // An unstamped copy is not "Up to date" either, and a build with no strip profiles says it of each.
            arduino[0].Description = null;
            var unstamped = LedBarProfile.Plan(brow, null, new[] { arduino });
            Assert.Equal(FlagBoxInstallState.UpToDate, unstamped.State);
            Assert.Equal(FlagBoxInstallState.NotEmbedded, PanelUpdates.StripPlans(new[] { new KeyValuePair<LedBar, FlagBoxPlan>(brow, unstamped) }, true, new string[0])[0].Value.State);
            // SimHub out of reach outranks it: nothing is said as known then.
            Assert.All(PanelUpdates.StripPlans(census, false, new[] { "3-9-3" }), entry => Assert.Equal(FlagBoxInstallState.Unavailable, entry.Value.State));
            Assert.Empty(PanelUpdates.StripPlans(null, true, null));
        }

        /// <summary>A light row's dot is its own words' ink, so an older profile's amber words have an
        /// amber dot, whatever the Matrix pill's dot (PanelLightRows.DotHex) says beside its own words.</summary>
        [Fact]
        public void A_light_row_s_dot_is_the_ink_of_its_own_words()
        {
            foreach (FlagBoxInstallState state in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                var plan = new FlagBoxPlan { State = state, InstalledVersion = "0.4.2", EmbeddedVersion = "0.5.0" };
                foreach (var row in new[] { PanelUpdates.StripRow("Wheel rim", plan), PanelUpdates.FlagBoxRow("OpenDash Flag box", plan, null) })
                {
                    var expected = row.StateHex == Theme.TextLabel ? Theme.StatusNotInstalled : row.StateHex;
                    Assert.Equal(expected, row.DotHex);
                }
            }
        }

        [Fact]
        public void The_flag_box_row_is_the_matrix_profile_and_offers_an_update_only_while_it_is_older()
        {
            var older = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.Outdated, InstalledVersion = "0.4.2", EmbeddedVersion = "0.5.0" }, null);
            Assert.Equal("OpenDash Flag box", older.Name);
            Assert.Equal("matrix profile", older.Kind);
            Assert.Equal("0.4.2", older.Version);
            Assert.Equal("Update available", older.State);
            Assert.True(older.OffersUpdate);

            Assert.Equal("Update brings it to 0.5.0.", older.Tooltip);

            var current = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0" }, null);
            Assert.Equal("Up to date", current.State);
            // As a current strip's row: the state and the version already say it all.
            Assert.Null(current.Tooltip);
            Assert.False(current.OffersUpdate);

            var none = PanelUpdates.FlagBoxRow("OpenDash Flag box", null, null);
            Assert.False(none.OffersUpdate);
            Assert.Equal("Reinstall everything installs it, then select it on your device.", none.Tooltip);

            var unreachable = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, @"C:\SimHub\OpenDash\flag-box.json");
            Assert.Equal("Unknown", unreachable.State);
            Assert.Equal(@"SimHub's matrix settings are not available. Import it by hand from C:\SimHub\OpenDash\flag-box.json.", unreachable.Tooltip);
            Assert.Equal("See SimHub's log.", PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.Failed }, null).Tooltip);
        }

        /// <summary>A light row's Update press says what it costs, in the words the Matrix and LEDs pages
        /// warn with.</summary>
        [Fact]
        public void A_light_row_s_update_says_it_replaces_the_driver_s_changes()
        {
            Assert.Equal(FlagBoxInstallPlan.Replaces, PanelUpdates.RowUpdateTooltip);
            Assert.Equal("Replaces the copy in SimHub, including your changes to it.", PanelUpdates.RowUpdateTooltip);
        }

        // --- Reinstall everything ---------------------------------------------------------------------

        [Fact]
        public void Reinstall_everything_asks_by_the_names_SimHub_lists()
        {
            Assert.Equal(
                "You have edited 1 dashboard: Rim. Reinstalling replaces your version. A copy is kept, and \"Put mine back\" restores it.",
                PanelUpdates.ReinstallQuestion(new[] { "Rim" }));
            Assert.Equal(
                "You have edited 2 dashboards: Rim, Pit wall. Reinstalling replaces your version. A copy is kept, and \"Put mine back\" restores it.",
                PanelUpdates.ReinstallQuestion(new[] { "Rim", "Pit wall" }));
        }

        private static readonly FlagBoxPlan Wrote = new FlagBoxPlan { State = FlagBoxInstallState.UpToDate };
        private static readonly FlagBoxPlan Refused = new FlagBoxPlan { State = FlagBoxInstallState.Failed };

        /// <summary>Counts strips one by one, by name, and a flag box write by what it was before.</summary>
        private static UpdatesLightsTally Tally(string[] updated = null, string[] installed = null, string[] notUpdated = null, string[] notInstalled = null, string[] noDevice = null, FlagBoxInstallState? flagBoxBefore = null, FlagBoxInstallState? flagBoxAfter = null, string note = null)
        {
            var tally = new UpdatesLightsTally();
            foreach (var name in updated ?? new string[0]) tally.Strip(name, "Arduino", FlagBoxInstallState.Outdated, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, Note = note });
            foreach (var name in installed ?? new string[0]) tally.Strip(name, name == "Rim" ? "Fanatec" : null, FlagBoxInstallState.NotInstalled, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, Note = note });
            foreach (var name in notUpdated ?? new string[0]) tally.Strip(name, "Arduino", FlagBoxInstallState.Outdated, Refused);
            foreach (var name in notInstalled ?? new string[0]) tally.Strip(name, "Arduino", FlagBoxInstallState.Failed, Refused);
            foreach (var name in noDevice ?? new string[0]) tally.DeviceNotListed(name);
            if (flagBoxBefore.HasValue) tally.FlagBox(flagBoxBefore.Value, new FlagBoxPlan { State = flagBoxAfter.Value });
            return tally;
        }

        private static string[] Strips(params string[] names) => names;

        [Fact]
        public void Reinstall_everything_says_what_it_wrote_and_the_step_left()
        {
            Assert.Equal("Reinstalled 1 dashboard. Close and reopen the dashboard to see it.",
                PanelUpdates.ReinstallSummary(1, 0, false, Tally(), "OpenDash Flag box"));
            Assert.Equal("Reinstalled 1 dashboard. Restart SimHub to see it.",
                PanelUpdates.ReinstallSummary(1, 0, true, null, "OpenDash Flag box"));
            // Several dashboards take the plural step, so "it" never points at one the line has not named.
            Assert.Equal("Reinstalled 3 dashboards. Close and reopen your dashboards to see them.",
                PanelUpdates.ReinstallSummary(3, 0, false, Tally(), "OpenDash Flag box"));
            Assert.Equal("Reinstalled 3 dashboards. Restart SimHub to see them.",
                PanelUpdates.ReinstallSummary(3, 0, true, Tally(), "OpenDash Flag box"));
            // The step follows the dashboards, so it points at them and not at the last profile named, and each
            // profile is named as SimHub lists it, in the LEDs and Matrix pages' words.
            Assert.Equal("Reinstalled 2 dashboards. The one you edited was left alone. Close and reopen your dashboards to see them. Updated the profiles of Rim and Brow. Updated OpenDash Flag box.",
                PanelUpdates.ReinstallSummary(2, 1, false, Tally(updated: Strips("Rim", "Brow"), flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate), "OpenDash Flag box"));
            // Installing a profile adds it without selecting it, and the line names that step for each profile
            // it installed, on the device it went to (voice.md; PanelLeds.SelectIt's form).
            Assert.Equal("Reinstalled 1 dashboard. Close and reopen the dashboard to see it. Updated Dash's profile. Installed the profiles of Rim and Brow. Installed OpenDash Flag box. Select \"Rim\" on Fanatec, \"Brow\" and \"OpenDash Flag box\" in SimHub to use them.",
                PanelUpdates.ReinstallSummary(1, 0, false, Tally(updated: Strips("Dash"), installed: Strips("Rim", "Brow"), flagBoxBefore: FlagBoxInstallState.NotInstalled, flagBoxAfter: FlagBoxInstallState.UpToDate), "OpenDash Flag box"));
            // A rig with strips and no screens: no dashboards clause.
            Assert.Equal("Installed Rim's profile. Select \"Rim\" on Fanatec in SimHub to use it.",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(installed: Strips("Rim")), "OpenDash Flag box"));
            Assert.Equal("The 2 dashboards you edited were left alone.",
                PanelUpdates.ReinstallSummary(0, 2, false, Tally(), "OpenDash Flag box"));
            Assert.Equal("The dashboard you edited was left alone. Updated Rim's profile.",
                PanelUpdates.ReinstallSummary(0, 1, false, Tally(updated: Strips("Rim")), "OpenDash Flag box"));
            Assert.Equal("There was nothing to reinstall.", PanelUpdates.ReinstallSummary(0, 0, false, Tally(), "OpenDash Flag box"));
            Assert.True(Tally().Ok);
            Assert.True(Tally(updated: Strips("Rim"), flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate).Ok);
        }

        /// <summary>A device that lists only its maker's profiles hides the one just installed until the driver
        /// turns that off, so the note comes before the select step it stands in the way of, as on the LEDs and
        /// Matrix pages, and the line is not said in the ordinary ink.</summary>
        [Fact]
        public void What_an_install_reports_about_the_device_is_said_before_the_select_step()
        {
            var tally = Tally(installed: Strips("Rim"), note: FlagBoxInstallPlan.BuiltInModeNote);
            Assert.Equal(FlagBoxInstallPlan.BuiltInModeNote, tally.Note);
            Assert.Equal("Installed Rim's profile. Turn off built-in profiles on your device, or OpenDash's will not be listed. Select \"Rim\" on Fanatec in SimHub to use it.",
                PanelUpdates.ReinstallSummary(0, 0, false, tally, "OpenDash Flag box"));
            Assert.False(tally.Ok);
            Assert.False(tally.Failed);
            Assert.Equal("Updated Rim's profile. Turn off built-in profiles on your device, or OpenDash's will not be listed.",
                PanelUpdates.LightsSaid(Tally(updated: Strips("Rim"), note: FlagBoxInstallPlan.BuiltInModeNote), "OpenDash Flag box"));
            // A failed write's note is not an install's: there is nothing listed to select.
            var failed = new UpdatesLightsTally();
            failed.Strip("Rim", "Fanatec", FlagBoxInstallState.NotInstalled, new FlagBoxPlan { State = FlagBoxInstallState.Failed, Note = FlagBoxInstallPlan.BuiltInModeNote });
            Assert.Null(failed.Note);
        }

        /// <summary>A light row's Update says what it did as the LEDs and Matrix pages' Update does, and nothing
        /// when it found nothing left to write.</summary>
        [Fact]
        public void A_light_row_s_update_says_what_it_did()
        {
            Assert.Equal("Updated Rim's profile.", PanelUpdates.LightsSaid(Tally(updated: Strips("Rim")), "OpenDash Flag box"));
            Assert.Equal("Could not update Rim's profile. See SimHub's log.", PanelUpdates.LightsSaid(Tally(notUpdated: Strips("Rim")), "OpenDash Flag box"));
            Assert.Equal("Updated OpenDash Flag box.", PanelUpdates.LightsSaid(Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate), "OpenDash Flag box"));
            Assert.Equal("Could not update OpenDash Flag box. See SimHub's log.", PanelUpdates.LightsSaid(Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.Failed), "OpenDash Flag box"));
            Assert.Equal("SimHub does not list Rim's device. Choose one under SimHub device on the LEDs page.", PanelUpdates.LightsSaid(Tally(noDevice: Strips("Rim")), "OpenDash Flag box"));
            Assert.Null(PanelUpdates.LightsSaid(Tally(), "OpenDash Flag box"));
            Assert.Null(PanelUpdates.LightsSaid(null, "OpenDash Flag box"));
        }

        [Fact]
        public void A_profile_that_could_not_be_written_is_said_by_name_with_where_to_look()
        {
            var line = PanelUpdates.ReinstallSummary(2, 0, false, Tally(updated: Strips("Rim"), notUpdated: Strips("Dash"), flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.Failed), "OpenDash Flag box");
            Assert.Equal("Reinstalled 2 dashboards. Close and reopen your dashboards to see them. Updated Rim's profile. Could not update Dash's profile. Could not update OpenDash Flag box. See SimHub's log.", line);
            Assert.Equal("Reinstalled 1 dashboard. Restart SimHub to see it. Could not update the profiles of Rim and Dash. Could not install the profiles of Brow, Pit and Top. See SimHub's log.",
                PanelUpdates.ReinstallSummary(1, 0, true, Tally(notUpdated: Strips("Rim", "Dash"), notInstalled: Strips("Brow", "Pit", "Top")), "OpenDash Flag box"));
            Assert.Equal("Could not install OpenDash Flag box. See SimHub's log.",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(flagBoxBefore: FlagBoxInstallState.NotInstalled, flagBoxAfter: FlagBoxInstallState.Failed), "OpenDash Flag box"));
            // A blank name is the profile's own.
            Assert.Equal("Updated " + FlagBoxProfile.ProfileName + ".",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate), " "));
            Assert.False(Tally(notUpdated: Strips("Rim")).Ok);
            Assert.False(Tally(notInstalled: Strips("Rim")).Ok);
            Assert.False(Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.Failed).Ok);
            // The reason is in SimHub's log, and the line says where rather than repeating it (voice.md).
            Assert.Equal("The reinstall did not finish. See SimHub's log.", PanelUpdates.ReinstallFailed);
        }

        /// <summary>A strip left out for want of a device is named with the step, after the failures and their
        /// log, and never pointed at the log: nothing was written, and the reason is the step.</summary>
        [Fact]
        public void A_strip_left_out_for_want_of_a_device_is_named_with_the_step_not_the_log()
        {
            Assert.Equal("Installed Rim's profile. Select \"Rim\" on Fanatec in SimHub to use it. SimHub does not list the devices of Dash and Brow. Choose them under SimHub device on the LEDs page.",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(installed: Strips("Rim"), noDevice: Strips("Dash", "Brow")), "OpenDash Flag box"));
            Assert.Equal("Could not update Rim's profile. See SimHub's log. SimHub does not list Dash's device. Choose one under SimHub device on the LEDs page.",
                PanelUpdates.LightsSaid(Tally(notUpdated: Strips("Rim"), noDevice: Strips("Dash")), "OpenDash Flag box"));
            var tally = Tally(noDevice: Strips("Dash"));
            Assert.False(tally.Ok);
            Assert.False(tally.Failed);
            Assert.Null(PanelUpdates.NoDeviceSaid(new string[0]));
        }

        /// <summary>Two strips of one shape are two strips: one rewritten and one that could not be are one
        /// updated and one that could not be, not two failures.</summary>
        [Fact]
        public void A_run_is_counted_strip_by_strip()
        {
            var tally = new UpdatesLightsTally();
            tally.Strip("Rim", "Arduino", FlagBoxInstallState.Outdated, Wrote);
            tally.Strip("Rim 2", "Arduino", FlagBoxInstallState.Outdated, Refused);
            Assert.Equal(new[] { "Rim" }, tally.Updated);
            Assert.Equal(new[] { "Rim 2" }, tally.NotUpdated);
            Assert.Equal(1, tally.StripsUpdated);
            Assert.Equal(1, tally.StripsNotUpdated);
            Assert.Equal(0, tally.StripsInstalled);
            Assert.Equal(0, tally.StripsNotInstalled);
            Assert.Null(tally.FlagBoxAfter);
        }

        private static LedBar Strip(string name, string ns, string shape = "3-9-3")
        {
            return new LedBar { Name = name, Namespace = ns, Shape = shape, Device = LedBar.ArduinoDevice };
        }

        private static KeyValuePair<LedBar, FlagBoxPlan> Held(LedBar bar, FlagBoxInstallState state)
        {
            return new KeyValuePair<LedBar, FlagBoxPlan>(bar, new FlagBoxPlan { State = state });
        }

        /// <summary>A row's Update writes its own strip and only while it is older; Reinstall everything writes
        /// every older, missing or failed strip and never a current one; neither writes while SimHub's LED
        /// settings cannot be read.</summary>
        [Fact]
        public void A_press_writes_the_strips_its_rule_picks_and_none_it_cannot_reach()
        {
            var rim = Strip("Rim", "LedRim");
            var dash = Strip("Dash", "LedDash");
            var brow = Strip("Brow", "LedBrow", "0-15-0");
            var gone = Strip("Gone", "LedGone");
            var census = new[]
            {
                Held(rim, FlagBoxInstallState.Outdated),
                Held(dash, FlagBoxInstallState.Outdated),
                Held(brow, FlagBoxInstallState.UpToDate),
                Held(gone, FlagBoxInstallState.NotInstalled),
            };

            Func<LedBar, bool> all = bar => true;
            Assert.Equal(new[] { rim }, PanelUpdates.StripsToWrite(census, true, PanelUpdates.RowUpdateWrites(PanelUpdates.StripKey(rim)), all).Select(e => e.Key));
            // A current strip's row has no Update, and a key that names it writes nothing.
            Assert.Empty(PanelUpdates.StripsToWrite(census, true, PanelUpdates.RowUpdateWrites(PanelUpdates.StripKey(brow)), all));
            Assert.Equal(new[] { rim, dash, gone }, PanelUpdates.StripsToWrite(census, true, PanelUpdates.ReinstallWrites, all).Select(e => e.Key));
            Assert.Empty(PanelUpdates.StripsToWrite(census, false, PanelUpdates.ReinstallWrites, all));

            // Keyed by namespace, so two strips of one shape are two keys.
            Assert.Equal("LedRim", PanelUpdates.StripKey(rim));
            Assert.Equal("Rim", PanelUpdates.StripKey(new LedBar { Name = "Rim" }));
            Assert.NotEqual(PanelUpdates.StripKey(rim), PanelUpdates.StripKey(dash));
        }

        /// <summary>A strip whose device SimHub does not list is left out rather than written: installing takes
        /// its copy out of every device first, so a write there would remove a strip that still lights. The
        /// press names it instead of failing it.</summary>
        [Fact]
        public void A_strip_on_a_device_SimHub_does_not_list_is_left_out_rather_than_written()
        {
            var rim = Strip("Rim", "LedRim");
            var dash = Strip("Dash", "LedDash");
            var current = Strip("Brow", "LedBrow");
            var census = new[] { Held(rim, FlagBoxInstallState.Outdated), Held(dash, FlagBoxInstallState.NotInstalled), Held(current, FlagBoxInstallState.UpToDate) };
            Func<LedBar, bool> listed = bar => bar != dash && bar != current;
            Assert.Equal(new[] { rim }, PanelUpdates.StripsToWrite(census, true, PanelUpdates.ReinstallWrites, listed).Select(e => e.Key));
            Assert.Equal(new[] { dash }, PanelUpdates.StripsWithoutDevice(census, true, PanelUpdates.ReinstallWrites, listed));
            // Only the strips the rule picks: a current one is never written, device or not.
            Assert.DoesNotContain(current, PanelUpdates.StripsWithoutDevice(census, true, PanelUpdates.ReinstallWrites, listed));
            Assert.Empty(PanelUpdates.StripsWithoutDevice(census, false, PanelUpdates.ReinstallWrites, listed));
        }

        /// <summary>
        /// Every row is repainted from its own strip after a press (8ff1b54): two strips of one shape, one
        /// written and one whose device has gone, read up to date and failed, not failed twice. A write that
        /// succeeded is repainted from what SimHub now holds, and nothing is said as known while SimHub cannot
        /// be read.
        /// </summary>
        [Fact]
        public void Each_row_is_repainted_from_its_own_strip_after_a_press()
        {
            var rim = Strip("Rim", "LedRim");
            var dash = Strip("Dash", "LedDash");
            var brow = Strip("Brow", "LedBrow", "0-15-0");
            var written = new Dictionary<string, FlagBoxPlan>
            {
                { "LedRim", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0" } },
                { "LedDash", new FlagBoxPlan { State = FlagBoxInstallState.Failed } },
            };
            var after = new[]
            {
                new KeyValuePair<LedBar, FlagBoxPlan>(rim, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0", EmbeddedVersion = "0.5.0" }),
                Held(dash, FlagBoxInstallState.Outdated),
                Held(brow, FlagBoxInstallState.UpToDate),
            };

            var plans = PanelUpdates.AfterWrite(after, true, written);
            Assert.Equal(FlagBoxInstallState.UpToDate, plans["LedRim"].State);
            Assert.Same(after[0].Value, plans["LedRim"]);
            Assert.Equal(FlagBoxInstallState.Failed, plans["LedDash"].State);
            Assert.Equal(FlagBoxInstallState.UpToDate, plans["LedBrow"].State);

            Assert.All(PanelUpdates.AfterWrite(after, false, written).Values, plan => Assert.Equal(FlagBoxInstallState.Unavailable, plan.State));
            Assert.Equal(3, PanelUpdates.AfterWrite(after, true, null).Count);
        }

        /// <summary>The draw file passes these rules to the write rather than restating them: a lambda there
        /// would let Reinstall everything rewrite a current profile with every test above green.</summary>
        [Fact]
        public void The_presses_write_strips_through_the_pinned_rules()
        {
            var code = PageCode();
            Assert.Contains("UpdatesWriteStrips(PanelUpdates.ReinstallWrites, out tally);", code);
            Assert.Contains("UpdatesWriteStrips(PanelUpdates.RowUpdateWrites(key), out said);", code);
            Assert.Contains("var census = PanelUpdates.StripPlans(BarCensus(embedded, out reachable), reachable, embedded.Keys);", code);
            Assert.Contains("PanelUpdates.StripsToWrite(census, reachable, wanted, listed)", code);
            Assert.Contains("PanelUpdates.StripsWithoutDevice(census, reachable, wanted, listed)", code);
            Assert.Contains("var after = PanelUpdates.StripPlans(BarCensus(embedded, out reachable), reachable, embedded.Keys);", code);
            Assert.Contains("return PanelUpdates.AfterWrite(after, reachable, written);", code);
            // The table and the support report read the census through the same rule, so neither says a strip
            // this build has no profile for is older or current.
            Assert.Contains("return PanelUpdates.StripPlans(census, reachable, embedded.Keys);", code);
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(code, @"PanelUpdates\.StripPlans\(BarCensus\(").Count);
            // A row's press is drawn from its row, whose rule is the device's as well as the state's.
            Assert.Contains("PanelUpdates.StripRow(s.Key.Name, s.Value, PanelUpdates.DeviceListed(devices, s.Key))", code);
            Assert.Contains("own(PanelUpdates.StripRow(bar.Name, current, PanelUpdates.DeviceListed(devices, bar)));", code);
            Assert.Contains("PanelUpdates.TableHasPress(lightRows)", code);
            // Each press says what it did, after what needs fixing has been asked again.
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(code, @"RefreshSidebar\(\);\s*UpdatesSay\(said\);").Count);
            Assert.Contains("said.FlagBox(drawn.State, result);", code);
            Assert.Contains("PanelUpdates.BringsFlagBoxForward(", code);
            // The method and its two callers, the row's Update and Reinstall everything: no third press
            // writes strips by a rule of its own.
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(code, @"UpdatesWriteStrips\(").Count);
        }

        /// <summary>Ruling 70: an older, missing or failed profile is written, a current one never, and the
        /// flag box only on a rig with a matrix, where the table draws its row.</summary>
        [Fact]
        public void Reinstall_everything_writes_older_and_missing_profiles_and_never_a_current_one()
        {
            Assert.True(PanelUpdates.BringsForward(FlagBoxInstallState.Outdated));
            Assert.True(PanelUpdates.BringsForward(FlagBoxInstallState.NotInstalled));
            Assert.True(PanelUpdates.BringsForward(FlagBoxInstallState.Failed));
            Assert.False(PanelUpdates.BringsForward(FlagBoxInstallState.UpToDate));
            Assert.False(PanelUpdates.BringsForward(FlagBoxInstallState.Unavailable));
            Assert.False(PanelUpdates.BringsForward(FlagBoxInstallState.NotEmbedded));
            Assert.True(PanelUpdates.BringsFlagBoxForward(true, FlagBoxInstallState.NotInstalled));
            Assert.False(PanelUpdates.BringsFlagBoxForward(false, FlagBoxInstallState.Outdated));
            Assert.False(PanelUpdates.BringsFlagBoxForward(true, FlagBoxInstallState.UpToDate));
            // A current profile is never rewritten, so only an older one's edits are at stake.
            Assert.Equal("Installs every dashboard on your rig again, and each older or missing LED and matrix profile. An older profile you have edited is replaced.", PanelUpdates.ReinstallTooltip);
        }

        // --- The kept copy ----------------------------------------------------------------------------

        [Fact]
        public void The_kept_card_names_what_was_kept()
        {
            Assert.Equal("Your edited Main dash was kept", PanelUpdates.KeptTitle(new[] { "Main dash" }));
            Assert.Equal("Your edited Rim and Main dash were kept", PanelUpdates.KeptTitle(new[] { "Rim", "Main dash" }));
            Assert.Equal("Your edited Rim, Pit wall and Main dash were kept", PanelUpdates.KeptTitle(new[] { "Rim", "Pit wall", "Main dash" }));
            Assert.Equal("Your edited dashboard was kept", PanelUpdates.KeptTitle(new string[0]));
            // An update, Reinstall everything and the Screens page's reinstall each keep a copy, so the
            // caption does not say which replaced it.
            Assert.Equal("OpenDash replaced it. Your copy is still here.", PanelUpdates.KeptCaption(1));
            Assert.Equal("OpenDash replaced them. Your copies are still here.", PanelUpdates.KeptCaption(2));
            Assert.Equal(18, PanelUpdates.KeptPaddingX);
            Assert.Equal(14, PanelUpdates.KeptPaddingY);
            Assert.Equal(16, PanelUpdates.KeptGap);
            Assert.Equal(3, PanelUpdates.KeptTextGap);
            Assert.Equal(15, PanelUpdates.KeptTitleSize);
        }

        /// <summary>
        /// A folder is on the kept card while a copy of edited work is there and the folder in SimHub is not
        /// the driver's own: Put mine back leaves the copy in place, so once it has run, or the driver edits
        /// again, the folder drops off the card rather than claiming a replacement that is over, and Put mine
        /// back never overwrites edits it did not ask about.
        /// </summary>
        [Fact]
        public void The_kept_card_shows_a_copy_only_while_the_folder_in_SimHub_is_not_the_driver_s()
        {
            var yours = new[] { @"C:\SimHub\DashTemplates\OpenDash Rim" + PackageExtractor.EditedSuffix + "20260930.zip", @"C:\SimHub\DashTemplates\OpenDash Rim_backup.zip" };
            Assert.True(PanelUpdates.ShowsKept(yours, edited: false));
            Assert.False(PanelUpdates.ShowsKept(yours, edited: true));
            // The ordinary one-deep backup is not a copy of anybody's work.
            Assert.False(PanelUpdates.ShowsKept(new[] { @"C:\SimHub\DashTemplates\OpenDash Rim_backup.zip" }, edited: false));
            Assert.False(PanelUpdates.ShowsKept(null, edited: false));
            // A folder outside the rig is never replaced, so its copy is not the card's to offer, and it has no
            // name but the folder's, which voice.md never shows.
            Assert.Contains("plugin.Installer.Packages.Where(p => p.FolderName != null && !p.OutsideRig)", PageCode());
        }

        [Fact]
        public void A_folder_is_named_as_SimHub_lists_it()
        {
            var screens = new[] { new ScreenInstance { Name = "Rim", Folder = "OpenDash Rim" }, new ScreenInstance { Name = " ", Folder = "OpenDash Pit" }, null };
            Assert.Equal("Rim", PanelUpdates.ScreenName(screens, "opendash rim"));
            Assert.Equal("OpenDash Pit", PanelUpdates.ScreenName(screens, "OpenDash Pit"));
            Assert.Equal("OpenDash Other", PanelUpdates.ScreenName(screens, "OpenDash Other"));
            Assert.Equal("OpenDash Other", PanelUpdates.ScreenName(null, "OpenDash Other"));
        }

        [Fact]
        public void Put_mine_back_says_what_it_restored()
        {
            Assert.Equal("There was nothing to put back.", PanelUpdates.PutBack(0));
            Assert.Equal("There was nothing to put back.", PanelUpdates.PutBack(0, new string[0]));
            Assert.Equal("Put back 1 dashboard. Close and reopen the dashboard to see it.", PanelUpdates.PutBack(1));
            Assert.Equal("Put back 2 dashboards. Close and reopen your dashboards to see them.", PanelUpdates.PutBack(2));
            // A copy that could not be put back is named, never said as nothing to put back: the card still
            // offers it under the line.
            Assert.Equal("Could not put back Rim. See SimHub's log.", PanelUpdates.PutBack(0, new[] { "Rim" }));
            Assert.Equal("Put back 1 dashboard. Close and reopen the dashboard to see it. Could not put back Rim and Pit wall. See SimHub's log.",
                PanelUpdates.PutBack(1, new[] { "Rim", "Pit wall" }));
        }

        // --- Support ----------------------------------------------------------------------------------

        [Fact]
        public void Support_says_what_happened_after_a_press()
        {
            Assert.Equal("Support report copied. Paste it into your issue.", PanelUpdates.ReportCopied);
            Assert.Equal("Could not copy the support report. Try again.", PanelUpdates.ReportFailed);
            Assert.Equal("Could not write the support report. See SimHub's log.", PanelUpdates.ReportNotWritten);
            Assert.Equal("SimHub has not written a log yet.", PanelUpdates.LogMissing);
            Assert.Equal(@"Could not open C:\SimHub\Logs.", PanelUpdates.LogFailed(@"C:\SimHub\Logs"));
            Assert.Equal("Logs", PanelUpdates.LogFolder);
            Assert.Equal("SimHub.txt", PanelUpdates.LogFile);
            Assert.Equal(8, PanelUpdates.SupportButtonGap);
            Assert.Equal(8, PanelUpdates.NewTagGap);
            Assert.Contains("press.Content = Ui.HStack(PanelUpdates.NewTagGap,", PageCode());
            Assert.Equal(12, PanelUpdates.SupportCaptionSize);
            Assert.Equal("SimHubWPF.exe", PanelUpdates.SimHubExe);
        }

        /// <summary>The NEW tag marks the two Support presses new in this release, for that one release: the
        /// artboard's Copy a support report, and Open the log, which no press did before this page.</summary>
        [Fact]
        public void Only_the_presses_new_in_this_release_carry_the_NEW_tag()
        {
            Assert.Equal(new[] { PanelUpdates.CopyReport, PanelUpdates.OpenLog }, PanelUpdates.NewTagged);
            Assert.True(PanelUpdates.IsNew(PanelUpdates.CopyReport));
            Assert.False(PanelUpdates.IsNew(PanelUpdates.ReportIssue));
            Assert.False(PanelUpdates.IsNew(PanelUpdates.ReadGuide));
            var code = PageCode();
            Assert.Contains("if (PanelUpdates.IsNew(label))", code);
            Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(code, @"Ui\.NewTag\(").Count);
        }

        /// <summary>The caption promises the last 200 log lines, and they are OpenDash's own.</summary>
        [Fact]
        public void The_tail_keeps_the_last_OpenDash_lines_in_order()
        {
            Assert.Equal(200, PanelUpdates.LogLines);
            Assert.Contains("200", PanelUpdates.SupportCaption);
            var lines = new List<string>();
            for (var i = 0; i < 300; i++)
            {
                lines.Add("INFO [OpenDash] line " + i);
                lines.Add("INFO SimHub something else " + i);
            }
            var tail = PanelUpdates.Tail(lines);
            Assert.Equal(200, tail.Count);
            Assert.Equal("INFO [OpenDash] line 100", tail[0]);
            Assert.Equal("INFO [OpenDash] line 299", tail[199]);
            Assert.All(tail, line => Assert.Contains("[OpenDash]", line));
            Assert.Equal(new[] { "a [OpenDash] b" }, PanelUpdates.Tail(new[] { "a [OpenDash] b", null, "other" }));
            Assert.Empty(PanelUpdates.Tail(null));
            Assert.Equal(2, PanelUpdates.Tail(lines, 2).Count);
        }

        /// <summary>What marks a log line as OpenDash's is the prefix Log writes: a renamed prefix would
        /// otherwise empty the report's log section without a word.</summary>
        [Fact]
        public void The_log_marker_is_the_prefix_the_plugin_logs_with()
        {
            var log = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "Log.cs"));
            Assert.Contains("public const string Prefix = \"" + PanelUpdates.LogMarker + " \";", log);
        }

        [Fact]
        public void The_report_names_each_thing_as_the_panel_does()
        {
            Assert.Equal("Face, 1280 × 480", PanelUpdates.ScreenDetail(Contract.KindFace, 1280, 480));
            Assert.Equal("Pit wall, 1920 × 1080", PanelUpdates.ScreenDetail(Contract.KindPitWall, 1920, 1080));
            Assert.Equal("3/9/3 Fanatec on fanatec-1", PanelUpdates.StripDetail("3/9/3 Fanatec", "fanatec-1"));
            Assert.Equal("3/9/3 Fanatec", PanelUpdates.StripDetail("3/9/3 Fanatec", " "));
            Assert.Equal("Matrix 2", PanelUpdates.MatrixName(2));
            // CarLightService.Status is CarLightService.Describe's sentence, which carries the count and its age.
            var status = CarLightService.Describe(412, new DateTime(2026, 9, 28, 7, 0, 0, DateTimeKind.Utc), Now, null);
            Assert.Equal("412 cars, updated 2 days ago (fetched 2026-09-28)", PanelUpdates.CarTables(status, new DateTime(2026, 9, 28, 7, 0, 0, DateTimeKind.Utc)));
            Assert.Equal(PanelLights.CarTablesNone, PanelUpdates.CarTables(CarLightService.Describe(0, null, Now, null), null));
            Assert.Equal("unknown", PanelUpdates.CarTables(null, null));
        }

        [Fact]
        public void The_report_carries_the_versions_the_rig_and_the_log_and_nothing_else()
        {
            var report = PanelUpdates.Report(new UpdatesReportInput
            {
                GeneratedUtc = Now,
                PluginVersion = "0.5.1",
                RigVersion = "0.5.0",
                SimHubVersion = "9.12.6",
                SimHubRoot = @"C:\Program Files (x86)\SimHub",
                OsVersion = "Microsoft Windows NT 10.0.19045.0",
                ChecksOn = true,
                LastCheckedTicks = new DateTime(2026, 9, 30, 9, 12, 0, DateTimeKind.Utc).Ticks,
                UpdateLine = "Version 0.5.2 is available. You have 0.5.0.",
                RestartPending = true,
                Screens = new[] { new UpdatesReportItem("Rim", PanelUpdates.ScreenDetail(Contract.KindFace, 850, 480), "0.5.0", "Up to date") },
                Strips = new[] { new UpdatesReportItem("Wheel rim", "3/9/3 Fanatec on fanatec-1", "0.4.2", "Update available") },
                Matrices = new[] { new UpdatesReportItem("Matrix 1", "Flag box", null, null) },
                FlagBox = new UpdatesReportItem("OpenDash Flag box", null, "0.5.0", "Up to date"),
                CarTables = "412 cars, updated 2 days ago (fetched 2026-09-28)",
                Log = new[] { "INFO [OpenDash] one", "WARN [OpenDash] two" },
            });
            // The whole report, line for line: the caption promises what it holds, and a line added to it (a
            // setting, a path of the driver's) is one this test has to be changed to allow.
            Assert.Equal(new[]
            {
                "OpenDash support report",
                "Written 2026-09-30 14:05 UTC",
                "",
                "OpenDash plugin: 0.5.1",
                "Dashboards on the rig: 0.5.0",
                @"SimHub: 9.12.6, in C:\Program Files (x86)\SimHub",
                "Windows: Microsoft Windows NT 10.0.19045.0",
                "Update check: on. Last checked today, 09:12 (UTC).",
                "Update status: Version 0.5.2 is available. You have 0.5.0.",
                "Update status: " + UpdateWording.RestartLater,
                "",
                "Screens (1)",
                "- Rim (Face, 850 × 480): Up to date, 0.5.0",
                "",
                "LED strips (1)",
                "- Wheel rim (3/9/3 Fanatec on fanatec-1): Update available, 0.4.2",
                "",
                "Matrices (1)",
                "- Matrix 1 (Flag box)",
                "",
                "Flag box profile: OpenDash Flag box: Up to date, 0.5.0",
                "",
                "Lovely Car Data: 412 cars, updated 2 days ago (fetched 2026-09-28)",
                "",
                "SimHub's log, the last 2 OpenDash lines:",
                "INFO [OpenDash] one",
                "WARN [OpenDash] two",
                "",
            }, report.Replace("\r\n", "\n").Split('\n'));
        }

        [Fact]
        public void An_empty_report_says_what_is_unknown_rather_than_failing()
        {
            var report = PanelUpdates.Report(new UpdatesReportInput { GeneratedUtc = Now });
            Assert.Contains("OpenDash plugin: unknown", report);
            Assert.Contains("Update check: off. Not checked yet (UTC).", report);
            Assert.Contains("Screens (0)", report);
            Assert.DoesNotContain("Flag box profile", report);
            Assert.Contains("SimHub's log, the last 0 OpenDash lines:", report);
            Assert.NotNull(PanelUpdates.Report(null));
        }

        /// <summary>One primary per page: the Updates page's one accented press is the update card's Download,
        /// and nothing else it draws is. Every way the panel draws an accented press is counted: the kit's
        /// Primary kind, Ui.PrimaryButton, which the base's light rows drew their Update with, and the shell's
        /// BuildPrimaryButton.</summary>
        [Fact]
        public void The_page_draws_one_primary_and_it_is_Download()
        {
            var primary = Assert.Single(System.Text.RegularExpressions.Regex.Matches(PageCode(), @"PanelButtonKind\.Primary|Ui\.PrimaryButton\(|BuildPrimaryButton\("));
            Assert.Equal("PanelButtonKind.Primary", primary.Value);
            Assert.Contains("updatesDownload = Ui.Button(null, PanelButtonKind.Primary);", PageCode());
        }

        // --- Search -----------------------------------------------------------------------------------

        [Fact]
        public void Search_lists_every_row_and_heading_the_page_draws_and_leads_to_it()
        {
            Assert.Equal(new[]
            {
                PanelUpdates.CheckTitle, PanelUpdates.InSimHubTitle, PanelConfirmation.ReinstallLabel, PanelUpdates.PutMineBack,
                PanelUpdates.SupportTitle, PanelUpdates.CopyReport, PanelUpdates.OpenLog, PanelUpdates.ReportIssue, PanelUpdates.ReadGuide,
            }, PanelUpdates.Search.Select(entry => entry.Label));
            Assert.All(PanelUpdates.Search, entry => Assert.Equal(PanelPage.Updates, entry.Route.Page));
            // Every entry's anchor, so a row moved to another section takes its search entry with it.
            Assert.Equal(new[]
            {
                PanelUpdates.AnchorCheck, PanelUpdates.AnchorPackages, PanelUpdates.AnchorReinstall, PanelUpdates.AnchorKept,
                PanelUpdates.AnchorSupport, PanelUpdates.AnchorSupport, PanelUpdates.AnchorSupport, PanelUpdates.AnchorSupport, PanelUpdates.AnchorSupport,
            }, PanelUpdates.Search.Select(entry => entry.Route.Anchor));
            Assert.Equal(PanelPage.Updates, PanelSearch.Find(PanelSearch.All(), "repair").First().Route.Page);
            Assert.Equal(PanelPage.Updates, PanelSearch.Find(PanelSearch.All(), "support report").First().Route.Page);
            Assert.Empty(PanelUpdates.SoonDrawn);
        }
    }

    /// <summary>
    /// The advances of a TrueType font, read from its head, hhea, hmtx and cmap (format 4) tables: enough to
    /// say how wide a run of text is at a size, which is what a fixed cell has to hold.
    /// </summary>
    internal sealed class TrueTypeAdvances
    {
        private static readonly Dictionary<string, TrueTypeAdvances> Loaded = new Dictionary<string, TrueTypeAdvances>(StringComparer.Ordinal);

        private readonly double unitsPerEm;
        private readonly int[] advances;
        private readonly Dictionary<int, int> glyphs = new Dictionary<int, int>();

        public static TrueTypeAdvances Of(string path)
        {
            lock (Loaded)
            {
                TrueTypeAdvances font;
                if (!Loaded.TryGetValue(path, out font)) Loaded[path] = font = new TrueTypeAdvances(System.IO.File.ReadAllBytes(path));
                return font;
            }
        }

        private TrueTypeAdvances(byte[] data)
        {
            Func<int, int> u16 = at => (data[at] << 8) | data[at + 1];
            Func<int, long> u32 = at => ((long)u16(at) << 16) | (long)u16(at + 2);
            var tables = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < u16(4); i++)
            {
                var record = 12 + 16 * i;
                tables[System.Text.Encoding.ASCII.GetString(data, record, 4)] = (int)u32(record + 8);
            }
            unitsPerEm = u16(tables["head"] + 18);
            var metrics = u16(tables["hhea"] + 34);
            advances = new int[metrics];
            for (var i = 0; i < metrics; i++) advances[i] = u16(tables["hmtx"] + 4 * i);

            var cmap = tables["cmap"];
            for (var i = 0; i < u16(cmap + 2); i++)
            {
                var record = cmap + 4 + 8 * i;
                if (u16(record) != 3 || u16(record + 2) != 1) continue;
                var sub = cmap + (int)u32(record + 4);
                if (u16(sub) != 4) continue;
                var segments = u16(sub + 6) / 2;
                var ends = sub + 14;
                var starts = ends + 2 * segments + 2;
                var deltas = starts + 2 * segments;
                var offsets = deltas + 2 * segments;
                for (var s = 0; s < segments; s++)
                {
                    int start = u16(starts + 2 * s), end = u16(ends + 2 * s), delta = u16(deltas + 2 * s), offset = u16(offsets + 2 * s);
                    for (var c = start; c <= end && c != 0xFFFF; c++)
                    {
                        int glyph;
                        if (offset == 0) glyph = (c + delta) & 0xFFFF;
                        else
                        {
                            glyph = u16(offsets + 2 * s + offset + 2 * (c - start));
                            if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                        }
                        glyphs[c] = glyph;
                    }
                }
                break;
            }
        }

        /// <summary>The width of <paramref name="text"/> at <paramref name="size"/> px.</summary>
        public double Width(string text, double size)
        {
            var units = 0;
            foreach (var c in text ?? string.Empty)
            {
                int glyph;
                glyphs.TryGetValue(c, out glyph);
                units += advances[Math.Min(glyph, advances.Length - 1)];
            }
            return units * size / unitsPerEm;
        }
    }
}
