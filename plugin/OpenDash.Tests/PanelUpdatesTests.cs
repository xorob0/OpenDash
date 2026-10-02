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
                PanelUpdates.Heading("0.5.1"), PanelUpdates.KeptHeading(1), PanelUpdates.KeptHeading(2), PanelUpdates.KeptLine(new[] { "Main dash" }),
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
            // A download is said by the heading and the bar's head alone: no caption names the run a second time.
            Assert.Null(PanelUpdates.CardNote(UpdatesCard.Downloading, "0.5.1"));
            Assert.Null(PanelUpdates.CardNote(UpdatesCard.Downloading, null));
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
            // A corrupt setting past DateTime's range reads as never checked: the build and every tick read it,
            // and new DateTime(ticks) would throw in both.
            Assert.Equal("Not checked yet", PanelUpdates.LastChecked(long.MaxValue, Now, utc));
            Assert.Equal("Not checked yet", PanelUpdates.LastChecked(DateTime.MaxValue.Ticks + 1, Now, utc));
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
            var caught = apply.IndexOf("said = PanelUpdates.UpdateFailed;", StringComparison.Ordinal);
            Assert.True(caught > work && caught < consent, "a throw inside Apply becomes a failed outcome before the completion is posted");
            Assert.Contains("Dispatcher.BeginInvoke(new Action(() => UpdatesApplied(release, outcome, said)));", apply);
            // In the page's own sentence, the shape its other failure takes, and never in the outcome's reason slot.
            Assert.Equal("The update did not finish. See SimHub's log.", PanelUpdates.UpdateFailed);
            Assert.Equal(PanelUpdates.UpdateFailed, PanelUpdates.ReinstallFailed.Replace("reinstall", "update"));
            Assert.Contains("outcome = Updates.Apply(plugin.Installer, release, replaceEdited, report); said = outcome.Line;", System.Text.RegularExpressions.Regex.Replace(apply, @"\s+", " "));

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
            // A run that did not finish is said on whatever page shows: the line is the shell's, above every
            // page, and the failure would otherwise reach nothing but SimHub's log.
            Assert.Contains("if (updatesCardHost != null || !outcome.Ok) Say(said, outcome.Ok);", applied);
        }

        /// <summary>The page's code with every run of whitespace one space, so a pin reads a call that is
        /// wrapped over lines as one.</summary>
        private static string FlatCode() => System.Text.RegularExpressions.Regex.Replace(PageCode(), @"\s+", " ");

        /// <summary>One method of the page, from its signature to the next member's.</summary>
        private static string Method(string signature)
        {
            var code = FlatCode();
            var start = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, signature + " is on the page");
            var next = code.IndexOf(" private ", start + signature.Length, StringComparison.Ordinal);
            return next < 0 ? code.Substring(start) : code.Substring(start, next - start);
        }

        /// <summary>
        /// Each press reaches what it says it does. A press whose handler went would draw with every pure test
        /// green, and a Support press that copied nothing would still say "Support report copied.".
        /// </summary>
        [Fact]
        public void Every_press_is_wired_to_what_it_says()
        {
            var code = FlatCode();
            Assert.Contains("updatesDownload.Click += (sender, args) => ApplyUpdate();", code);
            Assert.Contains("updatesCheckNow.Click += (sender, args) => Check(manual: true);", code);
            Assert.Contains("updatesReinstall.Click += (sender, args) => Reinstall();", code);
            Assert.Contains("restore.Click += (sender, args) => RestoreKept();", code);
            Assert.Contains("copy.Click += (sender, args) => UpdatesCopyReport();", code);
            Assert.Contains("log.Click += (sender, args) => UpdatesOpenLog();", code);
            Assert.Contains("issue.Click += (sender, args) => Ui.OpenUrl(IssuesUrl);", code);
            Assert.Contains("guide.Click += (sender, args) => Ui.OpenUrl(DocumentationUrl);", code);
            Assert.Contains("BuildLink(PanelUpdates.EveryRelease, UpdateCheck.ReleasesPageUrl)", code);

            var copy = Method("private void UpdatesCopyReport()");
            // Each failure says its own line and returns; only a report that reached the clipboard says so.
            InOrder(copy,
                "report = PanelUpdates.Report(UpdatesReportInput());",
                "catch",
                "Say(PanelUpdates.ReportNotWritten, false); return;",
                "Clipboard.SetText(report);",
                "catch",
                "Say(PanelUpdates.ReportFailed, false); return;",
                "Say(PanelUpdates.ReportCopied);");
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(copy, @"PanelUpdates\.ReportCopied"));

            var log = Method("private void UpdatesOpenLog()");
            Assert.Contains("var folder = Path.Combine(plugin.Installer.SimHubRoot ?? string.Empty, PanelUpdates.LogFolder);", log);
            Assert.Contains("Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });", log);

            // The restart said after the dialog's yes, in UpdateWording's words.
            Assert.Equal("Closing SimHub. It starts again with the new OpenDash.", UpdateWording.RestartGoing);
            Assert.Contains("Say(UpdateWording.RestartGoing);", Method("private async void OfferRestart("));
        }

        /// <summary>The one setting the page writes is the switch's, read and saved where it is flipped.</summary>
        [Fact]
        public void The_switch_reads_and_saves_the_check_setting()
        {
            var row = Method("private FrameworkElement BuildCheckRow()");
            Assert.Contains("var toggle = BuildToggle(Settings.CheckForUpdates, on => { Settings.CheckForUpdates = on; Save();", row);
        }

        /// <summary>
        /// Download and Reinstall everything each ask on their own line and read their label from it, and
        /// asking on one line takes the other's question away (PanelConfirmation: one question at a time). A
        /// Download asking on Reinstall everything's line would ask again on every press and never replace
        /// an edited dashboard.
        /// </summary>
        [Fact]
        public void Each_replacing_press_asks_on_its_own_line_and_withdraws_the_other_s_question()
        {
            var code = FlatCode();
            Assert.Contains("updatesDownload.SetBinding(ContentControl.ContentProperty, UpdatesLabelFrom(updatesCardLine, ReplacingAction.Update));", code);
            Assert.Contains("updatesReinstall.SetBinding(ContentControl.ContentProperty, UpdatesLabelFrom(updatesReinstallLine, ReplacingAction.Reinstall));", code);

            var apply = Method("private void ApplyUpdate()");
            Assert.Contains("confirmation.Press(ReplacingAction.Update, edited, question, updatesCardLine == null ? null : updatesCardLine.Text);", apply);
            Assert.Contains("UpdatesAsk(updatesCardLine, question);", apply);
            Assert.DoesNotContain("updatesReinstallLine", apply);

            var reinstall = Method("private void Reinstall()");
            Assert.Contains("confirmation.Press(ReplacingAction.Reinstall, edited, question, updatesReinstallLine == null ? null : updatesReinstallLine.Text);", reinstall);
            Assert.Contains("UpdatesAsk(updatesReinstallLine, question);", reinstall);
            Assert.DoesNotContain("updatesCardLine", reinstall);

            var ask = Method("private void UpdatesAsk(TextBlock line, string question)");
            Assert.Contains("foreach (var other in new[] { updatesCardLine, updatesReinstallLine }) { if (other == null || other == line) continue; other.Text = string.Empty; other.Visibility = Visibility.Collapsed; }", ask);

            // The question is shown, on a line in the tree: the second press replaces edited dashboards on the
            // strength of it, so a question written to a collapsed or detached line would let Download or
            // Reinstall everything replace them without its names, its kept copy or Put mine back ever showing.
            Assert.Contains("if (line == null) return; line.Text = question; line.Visibility = Visibility.Visible;", ask);
            InOrder(Method("private void UpdatesDrawCard()"),
                "updatesCardLine = Ui.Caption(string.Empty, BodyWidth);",
                "text.Children.Add(updatesCardLine);",
                "updatesDownload.SetBinding(");
            InOrder(Method("private FrameworkElement UpdatesReinstallRow()"),
                "updatesReinstallLine = Ui.Caption(string.Empty, BodyWidth);",
                "updatesReinstall.SetBinding(",
                "grid.Children.Add(updatesReinstall);",
                "grid.Children.Add(updatesReinstallLine);",
                "return grid;");

            // Each press's label is read from its own line, through its own action: "Download" or "Reinstall
            // everything", and "Replace anyway" only while that press's question is what the line shows.
            var label = Method("private Binding UpdatesLabelFrom(TextBlock line, ReplacingAction action)");
            Assert.Contains("return new Binding(nameof(TextBlock.Text)) { Source = line, Mode = BindingMode.OneWay, Converter = new ConfirmationLabel(confirmation, action), };", label);
            Assert.Contains("public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { return confirmation.Label(action, value as string); }", FlatCode());
        }

        /// <summary>
        /// A download holds off every press that writes DashTemplates or SimHub's profiles: two installers over
        /// the same folders can delete one mid-extraction. Each press returns while a run is going, and the
        /// run draws each of them disabled, whenever it was drawn.
        /// </summary>
        [Fact]
        public void A_run_holds_off_every_press_that_writes()
        {
            foreach (var signature in new[] { "private void ApplyUpdate()", "private void RestoreKept()", "private void Reinstall()" })
            {
                var body = Method(signature);
                var guard = body.IndexOf("if (applying) return;", StringComparison.Ordinal);
                Assert.True(guard >= 0 && guard < body.IndexOf("plugin.Installer.Refresh();", StringComparison.Ordinal), signature + " returns during a run before it reads or writes");
            }
            var lights = System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => p.EndsWith("SettingsControl.Updates.Lights.cs", StringComparison.Ordinal))), @"\s+", " ");
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(lights, @"\{ if \(applying\) return; draw\((update|press)\(\)\);").Count);

            var run = Method("private void ShowRun()");
            Assert.Contains("if (updatesReinstall != null) updatesReinstall.IsEnabled = false;", run);
            Assert.Contains("if (updatesCheckNow != null) updatesCheckNow.IsEnabled = false;", run);
            Assert.Contains("foreach (var press in updatesRunPresses) press.IsEnabled = false;", run);
            // Put mine back and both light rows' Update join the presses a run holds off.
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(FlatCode(), @"updatesRunPresses\.Add\(").Count);
            // A light row's repaint takes the press it replaces out of the list, so a run holds off only the
            // presses its build draws, never a detached one left from an earlier paint.
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(lights, @"UpdatesUnhold\(actionHost\.Child as Button\); actionHost\.Child = null;").Count);
            Assert.Contains("if (press != null) updatesRunPresses.Remove(press);", Method("private void UpdatesUnhold(Button press)"));
        }

        /// <summary>
        /// Put mine back never overwrites edits it did not ask about: it reads the disk first, and of what the
        /// kept card offers -- every kept copy, since ruling 5 of #524 -- it restores only a folder the installer
        /// does not find edited again (PutsBack), naming the rest. PackageExtractor.Restore keeps no copy of what
        /// it replaces.
        /// </summary>
        [Fact]
        public void Put_mine_back_restores_only_what_the_card_offers_from_a_fresh_read()
        {
            var code = FlatCode();
            Assert.Contains("if (!PanelUpdates.ShowsKept(copies)) continue;", code);
            var restore = Method("private void RestoreKept()");
            var read = restore.IndexOf("plugin.Installer.Refresh();", StringComparison.Ordinal);
            var edited = restore.IndexOf("var edited = new HashSet<string>(plugin.Installer.EditedFolders.Where(f => f != null), StringComparer.OrdinalIgnoreCase);", StringComparison.Ordinal);
            var each = restore.IndexOf("foreach (var kept in UpdatesKept())", StringComparison.Ordinal);
            var skip = restore.IndexOf("if (!PanelUpdates.PutsBack(edited.Contains(folder))) { held.Add(kept.Value); continue; }", StringComparison.Ordinal);
            var write = restore.IndexOf("PackageExtractor.Restore(", StringComparison.Ordinal);
            Assert.True(read >= 0 && edited > read && each > edited, "the disk is read before the copies are chosen");
            Assert.True(skip > each && write > skip, "an edited folder is passed over before anything is written");
            Assert.Contains("Say(PanelUpdates.PutBack(restored, failed, held),", restore);
            Assert.True(PanelUpdates.PutsBack(edited: false));
            Assert.False(PanelUpdates.PutsBack(edited: true));
        }

        /// <summary>
        /// What a light press costs is said on the presses themselves: a row's Update and Reinstall everything
        /// replace an older profile the driver has changed, and the row's hover names only the version.
        /// </summary>
        [Fact]
        public void The_presses_that_replace_a_changed_profile_say_so()
        {
            var code = FlatCode();
            Assert.Contains("private static Button UpdatesRowPress() { var button = Ui.Button(PanelUpdates.RowUpdate, PanelButtonKind.Outline, PanelButtonSize.Small); button.ToolTip = PanelUpdates.RowUpdateTooltip;", code);
            Assert.Contains("updatesReinstall.ToolTip = PanelUpdates.ReinstallTooltip;", code);
            Assert.Contains("replaced", PanelUpdates.ReinstallTooltip);
            // The flag box only on a rig with a matrix, from what SimHub held before the run.
            Assert.Contains("if (PanelUpdates.BringsFlagBoxForward(Settings.MatrixPanels().Any(), before)) tally.FlagBox(before, InstallFlagBox());", code);
        }

        /// <summary>The draw-time choices that are not a press: the card the state draws, where search's Put
        /// mine back lands while no kept card is drawn, and the notes under the table.</summary>
        [Fact]
        public void The_page_draws_its_choices_from_PanelUpdates()
        {
            var code = FlatCode();
            Assert.Contains("updatesCard = PanelUpdates.CardFor(updateStatus.State, applying, UpdatesPending());", code);
            Assert.Contains("Ui.Anchor(UpdatesInSimHubSection(updatesWidth, kept == null), PanelUpdates.AnchorPackages)", code);
            Assert.Contains("children.Add(keptAnchorHere ? Ui.Anchor(new Border { Child = reinstall }, PanelUpdates.AnchorKept) : reinstall);", code);
            Assert.Contains("var hasStrips = Settings.LedBarList().Any(bar => bar != null && bar.ProfileShapeId != null);", code);
            Assert.Contains("PanelUpdates.TableNotes(plugin.Installer.HasEmbeddedPackage, plugin.Installer.LastError, rowFailed, lights.Count > 0, hasStrips, !hasStrips || EmbeddedShapeIds().Count > 0, stripsReachable);", code);
            Assert.Contains("rowFailed |= pair.Value.State == PanelCopy.InstallFailed;", code);
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
        /// device SimHub lists. The flag box's row is drawn whenever the rig has a matrix.</summary>
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
            // Whenever the rig has a matrix, a build without the profile included: its row reads "Unknown" and
            // says why, where leaving it out left a matrix rig's table with no matrix row and no word.
            Assert.True(PanelUpdates.DrawsFlagBoxRow(true));
            Assert.False(PanelUpdates.DrawsFlagBoxRow(false));
            var unshipped = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded }, null);
            Assert.False(unshipped.OffersUpdate);
            Assert.Equal("This build ships no flag box profile.", unshipped.Tooltip);
            // Drawn from the matrix alone; the writes keep their own check for the profile.
            Assert.Contains("private bool UpdatesDrawsFlagBox() { return PanelUpdates.DrawsFlagBoxRow(Settings.MatrixPanels().Any()); }", FlatCode());
            Assert.Contains("var flagBoxPlan = UpdatesDrawsFlagBox() ? SafePlan() : null;", FlatCode());
            Assert.Contains("if (plugin.FlagBoxJson != null) { var before = SafePlan().State; if (PanelUpdates.BringsFlagBoxForward(Settings.MatrixPanels().Any(), before)) tally.FlagBox(before, InstallFlagBox()); }", Method("private UpdatesLightsTally UpdatesBringLightsForward()"));
        }

        /// <summary>The flag box's by-hand route is drawn only while SimHub's matrix settings are out of reach,
        /// and by this page, so it fits its column: the path box gives up width to the press beside it, never
        /// wider than 320, where the shared route's fixed 320 beside the press is about 555 px and would clip
        /// below 555 px.</summary>
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
            Assert.Equal("Copy to SimHub's import folder", PanelUpdates.CopyForImport);
            Assert.Equal(@"Puts a copy in Documents\SimHub.", PanelUpdates.CopyForImportTooltip);
            Assert.Equal("Where OpenDash left the profile.", PanelUpdates.ImportPathTooltip);
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

            const string edited = "You have edited it. Reinstall everything replaces it.";
            Assert.Equal(edited, PanelUpdates.EditedTooltip);
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
            Assert.Equal("Missing", missing.State);
            Assert.Equal("Missing", PanelUpdates.Missing);
            Assert.Equal(PanelScreens.Missing, PanelUpdates.Missing);
            Assert.Equal(Theme.StatusFailed, missing.StateHex);
            Assert.Equal(string.Empty, missing.Version);
            Assert.Equal("Reinstall everything installs it again.", missing.Tooltip);

            var waiting = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), true, true);
            // The panel's one phrase for the state (#524, ruling 1), which drops ruling 69's "Waiting for a restart".
            Assert.Equal("Restart SimHub to load it", waiting.State);
            Assert.Equal(PanelCopy.RestartToLoad, waiting.State);
            Assert.Equal(Theme.Caution, waiting.StateHex);
            Assert.Equal("0.5.0", waiting.Version);
            Assert.Equal("After the restart, assign \"Rim\" to its display in Dash Studio.", waiting.Tooltip);
            Assert.DoesNotContain("Restart SimHub", waiting.Tooltip);
            // About 140 of the 135 the dot leaves it, so the phrase wraps "it" to a second line: the cell wraps
            // (TextWrapping.Wrap in UpdatesStateCell) and the ruling has the phrase wrap rather than be trimmed.
            Assert.True(StateWidth(PanelCopy.RestartToLoad) > PanelUpdates.TableStateRoom, "the measure tells a state that wraps from one that fits");

            // Unknown facts say nothing of a restart.
            Assert.Equal("Up to date", PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), null, null).State);
        }

        /// <summary>
        /// Every word the State column writes, dashboard and light rows alike, fits the cell on one line: the
        /// 150 column less the 7 dot and its 8 gap, measured in Barlow Regular 13 from the font the panel
        /// bundles. A longer state word wraps and makes its row the one taller row of the table, which the
        /// restart's phrase does by ruling (#524, ruling 1), and so is measured on its own above.
        /// </summary>
        [Fact]
        public void Every_state_fits_its_cell_on_one_line()
        {
            Assert.Equal(135, PanelUpdates.TableStateRoom);
            var states = new[]
            {
                PanelUpdates.UpToDate, PanelUpdates.UpdateAvailable, PanelUpdates.Missing,
                PanelUpdates.Unknown, PanelCopy.Installed, PanelCopy.NotInstalled, PanelCopy.InstallFailed,
            }.Concat(Enum.GetValues(typeof(FlagBoxInstallState)).Cast<FlagBoxInstallState>().Select(state => PanelCopy.LightRow(state, "0.4.0").State));
            foreach (var state in states)
            {
                var width = StateWidth(state);
                Assert.True(width <= PanelUpdates.TableStateRoom, "\"" + state + "\" is " + width.ToString("0.0") + " px in a " + PanelUpdates.TableStateRoom + " px cell");
            }
        }

        /// <summary>A state's width as the cell draws it: Barlow Regular at the state's 13, off the advances in
        /// the tracked source of the TTF the panel embeds, which the build and the packaging only copy, so the
        /// measure needs no build: Resources/fonts is gitignored build output, and a fresh checkout has none.
        /// Kerning is left out; it moves a word this short by well under a pixel.</summary>
        private static double StateWidth(string text)
        {
            return TrueTypeAdvances.Of(System.IO.Path.Combine(RepoPaths.Root(), "packages", "dash", "fonts", "Barlow-Regular.ttf")).Width(text, PanelUpdates.TableStateSize);
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
            Assert.Equal("Missing", PanelUpdates.DashboardRow(Rim, null, false, false).State);
            var unread = PanelUpdates.DashboardRow(Rim, null, null, null);
            Assert.Equal("Unknown", unread.State);
            Assert.Equal(Theme.TextLabel, unread.StateHex);
            var pitWall = new ScreenInstance { Name = "Pit", Kind = Contract.KindPitWall, Width = 1920, Height = 1080 };
            Assert.Equal("This build ships no 1920 × 1080 pit wall.", PanelUpdates.DashboardRow(pitWall, null, null, null).Tooltip);
            // A slots screen in the Screens page's words: a square one is a round screen and a rectangular one a
            // card face (PanelScreens.KindOf, CardsCaption), never "no 480 × 480 round." nor a third name. A
            // release build carries no slots package, so a migrated rectangular one always shows this hover.
            var round = new ScreenInstance { Name = "Round", Kind = Contract.KindSlots, Width = 480, Height = 480 };
            Assert.Equal("This build ships no 480 × 480 round screen.", PanelUpdates.DashboardRow(round, null, null, null).Tooltip);
            var cards = new ScreenInstance { Name = "Cards", Kind = Contract.KindSlots, Width = 1280, Height = 480 };
            Assert.Equal("This build ships no 1280 × 480 card face.", PanelUpdates.DashboardRow(cards, null, null, null).Tooltip);
            Assert.Equal("round screen", PanelUpdates.RoundScreen);
            Assert.Equal("card face", PanelUpdates.CardFace);
            // A size not known is left out, never "0 × 0".
            var unsized = new ScreenInstance { Name = "Phone", Kind = Contract.KindCompanion };
            Assert.Equal("This build ships no companion.", PanelUpdates.DashboardRow(unsized, null, null, null).Tooltip);
            Assert.DoesNotContain("0 × 0", PanelUpdates.ShipsNo(new ScreenInstance { Kind = Contract.KindFace, Width = 1280 }));
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
            // A build with no strip profile is the note for the strips even while SimHub's LED settings are out
            // of reach as well: one sentence about them, the one a press could not change.
            Assert.Equal(new[] { PanelLightRows.SectionCaption, PanelLightRows.NoProfiles }, PanelUpdates.TableNotes(true, null, false, true, true, false, false));
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
            // The press that installs it, as a missing dashboard's hover says; the select step is the press's
            // own line, in its one form with the strip's name and device.
            Assert.Equal("Reinstall everything installs it.", none.Tooltip);
            Assert.Equal(PanelUpdates.NotInstalledTooltip, PanelUpdates.StripNotInstalled);
            Assert.False(none.OffersUpdate);

            // What the page cannot know is said as not known, not as not installed.
            var unreachable = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable });
            Assert.Equal("Unknown", unreachable.State);
            Assert.Equal(Theme.TextLabel, unreachable.StateHex);
            // The note under the table says why (TableNotes), so the row's hover does not say it again.
            Assert.Null(unreachable.Tooltip);
            Assert.Contains(PanelLightRows.Unavailable, PanelUpdates.TableNotes(true, null, false, true, true, true, false));
            var notEmbedded = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded });
            // As the LEDs card and Home say it (#524, ruling 2); the hover says why no press installs it.
            Assert.Equal("Not installed", notEmbedded.State);
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
            const string step = "This strip's device is not in SimHub. Choose one under SimHub device on the LEDs page.";
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
            Assert.Null(PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, deviceListed: false).Tooltip);
            // A build with no profile for the strip has nothing a device would let a press write, so the row
            // says that, not the device step.
            Assert.Equal(PanelUpdates.StripNotEmbedded, PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded }, deviceListed: false).Tooltip);

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
        public void A_strip_this_build_ships_no_profile_for_reads_not_installed_whatever_SimHub_holds()
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
            // The LEDs card's and Home's word for it (#524, ruling 2), beside the version SimHub truly holds.
            Assert.Equal("Not installed", row.State);
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
            // The press that installs it; the select step, with the matrix's content number, is the press's line.
            Assert.Equal("Reinstall everything installs it.", none.Tooltip);
            Assert.Equal(PanelUpdates.NotInstalledTooltip, PanelUpdates.FlagBoxNotInstalled);

            var unreachable = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, @"C:\SimHub\OpenDash\flag-box.json");
            Assert.Equal("Unknown", unreachable.State);
            // The by-hand route under the table prints that sentence (UpdatesFlagBoxFallback), so the hover does not.
            Assert.Null(unreachable.Tooltip);
            Assert.True(PanelUpdates.ShowsImportFallback(new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }));
            Assert.Equal(@"SimHub's matrix settings are not available. Import it by hand from C:\SimHub\OpenDash\flag-box.json.",
                FlagBoxInstallPlan.Summary(new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, @"C:\SimHub\OpenDash\flag-box.json"));
            // And the route draws it, as its line over the press and the path: with the hover gone, it is the
            // only place on the page that says why the row reads "Unknown" and where the file is.
            var fallback = Method("private FrameworkElement UpdatesFlagBoxFallback(FlagBoxPlan plan)");
            Assert.Contains("updatesImportLine = Ui.Caption(FlagBoxInstallPlan.Summary(plan, plugin.FlagBox?.Path), BodyWidth);", fallback);
            Assert.Contains("Text = plugin.FlagBox?.Path ?? string.Empty,", fallback);
            Assert.Contains("return Ui.VStack(PanelUpdates.ImportLineGap, updatesImportLine, row);", fallback);
            // A build with no profile says so on the row, where no note under the table does.
            var notEmbedded = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded }, null);
            Assert.Equal("Unknown", notEmbedded.State);
            Assert.Equal("This build ships no flag box profile.", notEmbedded.Tooltip);
            Assert.False(notEmbedded.OffersUpdate);
            Assert.Equal("See SimHub's log.", PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.Failed }, null).Tooltip);
        }

        /// <summary>A light row's Update press says what it costs, in the words the Matrix page's flag box
        /// Update warns with (the LEDs page's strip Update is a shared request to follow).</summary>
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

        /// <summary>A rig with one matrix, number 1.</summary>
        private static readonly IList<int> Matrix1 = new[] { 1 };

        [Fact]
        public void Reinstall_everything_says_what_it_wrote_and_the_step_left()
        {
            Assert.Equal("Reinstalled 1 dashboard. Close and reopen the dashboard to see it.",
                PanelUpdates.ReinstallSummary(1, 0, false, Tally(), "OpenDash Flag box", Matrix1));
            Assert.Equal("Reinstalled 1 dashboard. Restart SimHub to see it.",
                PanelUpdates.ReinstallSummary(1, 0, true, null, "OpenDash Flag box", Matrix1));
            // Several dashboards take the plural step, so "it" never points at one the line has not named.
            Assert.Equal("Reinstalled 3 dashboards. Close and reopen your dashboards to see them.",
                PanelUpdates.ReinstallSummary(3, 0, false, Tally(), "OpenDash Flag box", Matrix1));
            Assert.Equal("Reinstalled 3 dashboards. Restart SimHub to see them.",
                PanelUpdates.ReinstallSummary(3, 0, true, Tally(), "OpenDash Flag box", Matrix1));
            // The step follows the dashboards, so it points at them and not at the last profile named, and each
            // profile is named as SimHub lists it, in the LEDs and Matrix pages' words.
            Assert.Equal("Reinstalled 2 dashboards. The one you edited was left alone. Close and reopen your dashboards to see them. Updated the profiles of Rim and Brow. Updated OpenDash Flag box.",
                PanelUpdates.ReinstallSummary(2, 1, false, Tally(updated: Strips("Rim", "Brow"), flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate), "OpenDash Flag box", Matrix1));
            // Installing a profile adds it without selecting it, and the line names that step for each profile
            // it installed, on the device it went to (voice.md; PanelLeds.SelectIt's form).
            // The flag box's step is the Matrix page's: on the matrix's device, with its content number.
            Assert.Equal("Reinstalled 1 dashboard. Close and reopen the dashboard to see it. Updated Dash's profile. Installed the profiles of Rim and Brow. Installed OpenDash Flag box. Select \"Rim\" on Fanatec and \"Brow\" in SimHub to use them. Select \"OpenDash Flag box\" on your matrix's device in SimHub and set RGB Matrix content to 1.",
                PanelUpdates.ReinstallSummary(1, 0, false, Tally(updated: Strips("Dash"), installed: Strips("Rim", "Brow"), flagBoxBefore: FlagBoxInstallState.NotInstalled, flagBoxAfter: FlagBoxInstallState.UpToDate), "OpenDash Flag box", Matrix1));
            // A rig with strips and no screens: no dashboards clause.
            Assert.Equal("Installed Rim's profile. Select \"Rim\" on Fanatec in SimHub to use it.",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(installed: Strips("Rim")), "OpenDash Flag box", Matrix1));
            Assert.Equal("The 2 dashboards you edited were left alone.",
                PanelUpdates.ReinstallSummary(0, 2, false, Tally(), "OpenDash Flag box", Matrix1));
            Assert.Equal("The dashboard you edited was left alone. Updated Rim's profile.",
                PanelUpdates.ReinstallSummary(0, 1, false, Tally(updated: Strips("Rim")), "OpenDash Flag box", Matrix1));
            Assert.Equal("There was nothing to reinstall.", PanelUpdates.ReinstallSummary(0, 0, false, Tally(), "OpenDash Flag box", Matrix1));
            // Several dashboards replaced and several left alone.
            Assert.Equal("Reinstalled 3 dashboards. The 2 you edited were left alone. Close and reopen your dashboards to see them.",
                PanelUpdates.ReinstallSummary(3, 2, false, Tally(), "OpenDash Flag box", Matrix1));
            Assert.True(Tally().Ok);
            Assert.True(Tally(updated: Strips("Rim"), flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate).Ok);
        }

        /// <summary>A device that lists only its maker's profiles hides the one just installed until the driver
        /// turns that off, so the note comes before the select step it stands in the way of, as on the LEDs and
        /// Matrix pages, and the line is not said in the ordinary ink. Reinstall everything can name several
        /// devices in one line, and then the note names the one it is about.</summary>
        [Fact]
        public void What_an_install_reports_about_the_device_is_said_before_the_select_step()
        {
            var tally = Tally(installed: Strips("Rim"), note: FlagBoxInstallPlan.BuiltInModeNote);
            Assert.Equal(new[] { "Fanatec" }, tally.NotedOn);
            Assert.Equal(FlagBoxInstallPlan.BuiltInModeNote, tally.Note);
            Assert.Equal("Installed Rim's profile. Turn off built-in profiles on your device, or OpenDash's will not be listed. Select \"Rim\" on Fanatec in SimHub to use it.",
                PanelUpdates.ReinstallSummary(0, 0, false, tally, "OpenDash Flag box", Matrix1));
            Assert.False(tally.Ok);
            Assert.False(tally.Failed);
            Assert.Equal("Updated Rim's profile. Turn off built-in profiles on your device, or OpenDash's will not be listed.",
                PanelUpdates.LightsSaid(Tally(updated: Strips("Rim"), note: FlagBoxInstallPlan.BuiltInModeNote), "OpenDash Flag box", Matrix1));
            // A run that names several devices says which one the note is about, since "your device" would
            // point at neither (voice.md); the form is BuiltInModeNote's with the device named.
            Assert.Equal(FlagBoxInstallPlan.BuiltInModeNote, PanelUpdates.BuiltInOn(new[] { "your device" }));
            var two = new UpdatesLightsTally();
            two.Strip("Rim", "Fanatec", FlagBoxInstallState.NotInstalled, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate });
            two.Strip("Brow", "Moza", FlagBoxInstallState.NotInstalled, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, Note = FlagBoxInstallPlan.BuiltInModeNote });
            Assert.Equal(new[] { "Moza" }, two.NotedOn);
            Assert.Equal("Installed the profiles of Rim and Brow. Turn off built-in profiles on Moza, or OpenDash's will not be listed. Select \"Rim\" on Fanatec and \"Brow\" on Moza in SimHub to use them.",
                PanelUpdates.LightsSaid(two, "OpenDash Flag box", Matrix1));
            // A strip and the flag box both reporting it, beside the matrix's own select step.
            var both = new UpdatesLightsTally();
            both.Strip("Rim", "Fanatec", FlagBoxInstallState.NotInstalled, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, Note = FlagBoxInstallPlan.BuiltInModeNote });
            both.FlagBox(FlagBoxInstallState.NotInstalled, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, Note = FlagBoxInstallPlan.BuiltInModeNote });
            Assert.True(both.FlagBoxNoted);
            Assert.Equal("Installed Rim's profile. Installed OpenDash Flag box. Turn off built-in profiles on Fanatec and your matrix's device, or OpenDash's will not be listed. Select \"Rim\" on Fanatec in SimHub to use it. Select \"OpenDash Flag box\" on your matrix's device in SimHub and set RGB Matrix content to 1.",
                PanelUpdates.LightsSaid(both, "OpenDash Flag box", Matrix1));
            // One device named, or a device SimHub gives no name for, keeps the note as reported.
            Assert.Equal(FlagBoxInstallPlan.BuiltInModeNote, PanelUpdates.NoteSaid(tally, false));
            var unnamed = new UpdatesLightsTally();
            unnamed.Strip("Rim", "Fanatec", FlagBoxInstallState.NotInstalled, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate });
            unnamed.Strip("Brow", null, FlagBoxInstallState.NotInstalled, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, Note = FlagBoxInstallPlan.BuiltInModeNote });
            Assert.Equal(FlagBoxInstallPlan.BuiltInModeNote, PanelUpdates.NoteSaid(unnamed, true));
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
            Assert.Equal("Updated Rim's profile.", PanelUpdates.LightsSaid(Tally(updated: Strips("Rim")), "OpenDash Flag box", Matrix1));
            Assert.Equal("Could not update Rim's profile. See SimHub's log.", PanelUpdates.LightsSaid(Tally(notUpdated: Strips("Rim")), "OpenDash Flag box", Matrix1));
            Assert.Equal("Updated OpenDash Flag box.", PanelUpdates.LightsSaid(Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate), "OpenDash Flag box", Matrix1));
            Assert.Equal("Could not update OpenDash Flag box. See SimHub's log.", PanelUpdates.LightsSaid(Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.Failed), "OpenDash Flag box", Matrix1));
            Assert.Equal("Rim's device is not in SimHub. Choose one under SimHub device on the LEDs page.", PanelUpdates.LightsSaid(Tally(noDevice: Strips("Rim")), "OpenDash Flag box", Matrix1));
            // The flag box installed by its row: the Matrix page's step, on the one matrix's device or on each
            // of several, and none on a rig with no matrix to name.
            var installed = Tally(flagBoxBefore: FlagBoxInstallState.NotInstalled, flagBoxAfter: FlagBoxInstallState.UpToDate);
            Assert.Equal("Installed OpenDash Flag box. Select \"OpenDash Flag box\" on your matrix's device in SimHub and set RGB Matrix content to 2.",
                PanelUpdates.LightsSaid(installed, "OpenDash Flag box", new[] { 2 }));
            Assert.Equal("Installed OpenDash Flag box. Select \"OpenDash Flag box\" on each matrix's device in SimHub and set RGB Matrix content to the matrix's number.",
                PanelUpdates.LightsSaid(installed, "OpenDash Flag box", new[] { 1, 2 }));
            Assert.Equal("Installed OpenDash Flag box.", PanelUpdates.LightsSaid(installed, "OpenDash Flag box", new int[0]));
            Assert.Equal("RGB Matrix content", PanelUpdates.MatrixContentField);
            Assert.Null(PanelUpdates.LightsSaid(Tally(), "OpenDash Flag box", Matrix1));
            Assert.Null(PanelUpdates.LightsSaid(null, "OpenDash Flag box", Matrix1));
        }

        [Fact]
        public void A_profile_that_could_not_be_written_is_said_by_name_with_where_to_look()
        {
            var line = PanelUpdates.ReinstallSummary(2, 0, false, Tally(updated: Strips("Rim"), notUpdated: Strips("Dash"), flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.Failed), "OpenDash Flag box", Matrix1);
            Assert.Equal("Reinstalled 2 dashboards. Close and reopen your dashboards to see them. Updated Rim's profile. Could not update Dash's profile. Could not update OpenDash Flag box. See SimHub's log.", line);
            Assert.Equal("Reinstalled 1 dashboard. Restart SimHub to see it. Could not update the profiles of Rim and Dash. Could not install the profiles of Brow, Pit and Top. See SimHub's log.",
                PanelUpdates.ReinstallSummary(1, 0, true, Tally(notUpdated: Strips("Rim", "Dash"), notInstalled: Strips("Brow", "Pit", "Top")), "OpenDash Flag box", Matrix1));
            Assert.Equal("Could not install OpenDash Flag box. See SimHub's log.",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(flagBoxBefore: FlagBoxInstallState.NotInstalled, flagBoxAfter: FlagBoxInstallState.Failed), "OpenDash Flag box", Matrix1));
            // A blank name is the profile's own.
            Assert.Equal("Updated " + FlagBoxProfile.ProfileName + ".",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate), " ", Matrix1));
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
            Assert.Equal("Installed Rim's profile. Select \"Rim\" on Fanatec in SimHub to use it. The devices of Dash and Brow are not in SimHub. Choose them under SimHub device on the LEDs page.",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(installed: Strips("Rim"), noDevice: Strips("Dash", "Brow")), "OpenDash Flag box", Matrix1));
            Assert.Equal("Could not update Rim's profile. See SimHub's log. Dash's device is not in SimHub. Choose one under SimHub device on the LEDs page.",
                PanelUpdates.LightsSaid(Tally(notUpdated: Strips("Rim"), noDevice: Strips("Dash")), "OpenDash Flag box", Matrix1));
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

            // Every strip is repainted while SimHub cannot be read, each as unknown: an empty answer would leave
            // every row saying what it said before the press.
            var unreachable = PanelUpdates.AfterWrite(after, false, written);
            Assert.Equal(new[] { "LedBrow", "LedDash", "LedRim" }, unreachable.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(unreachable.Values, plan => Assert.Equal(FlagBoxInstallState.Unavailable, plan.State));
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
            // A heading is a noun (#524, ruling 4); what was kept is the first line under it.
            Assert.Equal("Kept copy", PanelUpdates.KeptHeading(1));
            Assert.Equal("Kept copies", PanelUpdates.KeptHeading(2));
            Assert.Equal("Your edited Main dash was kept.", PanelUpdates.KeptClause(new[] { "Main dash" }));
            Assert.Equal("Your edited Rim and Main dash were kept.", PanelUpdates.KeptClause(new[] { "Rim", "Main dash" }));
            Assert.Equal("Your edited Rim, Pit wall and Main dash were kept.", PanelUpdates.KeptClause(new[] { "Rim", "Pit wall", "Main dash" }));
            Assert.Equal("Your edited dashboard was kept.", PanelUpdates.KeptClause(new string[0]));
            Assert.Equal("Your edited Rim was kept. OpenDash replaced it. Your copy is still here.", PanelUpdates.KeptLine(new[] { "Rim" }));
            Assert.Equal("Your edited Rim and Main dash were kept. OpenDash replaced them. Your copies are still here.", PanelUpdates.KeptLine(new[] { "Rim", "Main dash" }));
            var card = Method("private FrameworkElement UpdatesKeptCard(double width)");
            Assert.Contains("var title = Ui.Text(PanelUpdates.KeptHeading(kept.Count), PanelUpdates.KeptTitleSize, FontWeights.SemiBold, Theme.TextPrimary);", card);
            Assert.Contains("Ui.Caption(PanelUpdates.KeptLine(kept.Select(k => k.Value).ToList()), BodyWidth)", card);
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
        /// A folder is on the kept card whenever a copy of edited work is there to put back, as the artboard
        /// draws it (#524, ruling 5), whether or not the driver has edited the folder in SimHub since: Put mine
        /// back is what leaves an edited folder alone (PutsBack).
        /// </summary>
        [Fact]
        public void The_kept_card_shows_a_copy_whenever_one_is_kept()
        {
            var yours = new[] { @"C:\SimHub\DashTemplates\OpenDash Rim" + PackageExtractor.EditedSuffix + "20260930.zip", @"C:\SimHub\DashTemplates\OpenDash Rim_backup.zip" };
            Assert.True(PanelUpdates.ShowsKept(yours));
            // The ordinary one-deep backup is not a copy of anybody's work.
            Assert.False(PanelUpdates.ShowsKept(new[] { @"C:\SimHub\DashTemplates\OpenDash Rim_backup.zip" }));
            Assert.False(PanelUpdates.ShowsKept(null));
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
            // A folder edited since its copy was kept is left alone and named (#524, ruling 5).
            Assert.Equal("Did not put back Rim, which you have edited since.", PanelUpdates.PutBack(0, null, new[] { "Rim" }));
            Assert.Equal("Put back 1 dashboard. Close and reopen the dashboard to see it. Did not put back Rim and Pit wall, which you have edited since.",
                PanelUpdates.PutBack(1, new string[0], new[] { "Rim", "Pit wall" }));
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
            Assert.Contains("var tagged = Ui.HStack(PanelUpdates.NewTagGap, element, Ui.NewTag());", PageCode());
            Assert.Equal(12, PanelUpdates.SupportCaptionSize);
            Assert.Equal("SimHubWPF.exe", PanelUpdates.SimHubExe);
        }

        /// <summary>The NEW tag marks what is new since the last cut, for that one release: the artboard's Copy
        /// a support report, Open the log, which no press did before this page, and the offer card's link to
        /// every release, which no card drew before it. Report an issue and Read the guide were rc.7's footer
        /// links. Every tag is drawn by the one helper, from the one list.</summary>
        [Fact]
        public void Only_the_presses_new_in_this_release_carry_the_NEW_tag()
        {
            Assert.Equal(new[] { PanelUpdates.CopyReport, PanelUpdates.OpenLog, PanelUpdates.EveryRelease }, PanelUpdates.NewTagged);
            Assert.True(PanelUpdates.IsNew(PanelUpdates.CopyReport));
            Assert.True(PanelUpdates.IsNew(PanelUpdates.EveryRelease));
            Assert.False(PanelUpdates.IsNew(PanelUpdates.ReportIssue));
            Assert.False(PanelUpdates.IsNew(PanelUpdates.ReadGuide));
            var code = PageCode();
            Assert.Contains("if (PanelUpdates.IsNew(label))", code);
            Assert.Contains("if (PanelUpdates.IsNew(PanelUpdates.EveryRelease)) link = UpdatesTagged(link);", code);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(code, @"Ui\.NewTag\("));
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(code, @"= UpdatesTagged\(").Count);
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
            // "(UTC)" labels a time, and a rig that never checked has none on the line.
            Assert.Contains("Update check: off. Not checked yet.", report);
            Assert.DoesNotContain("(UTC)", report);
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

        // --- What the draw files call -----------------------------------------------------------------

        /// <summary>Asserts that each part is in the body, each after the one before it.</summary>
        private static void InOrder(string body, params string[] parts)
        {
            var at = -1;
            foreach (var part in parts)
            {
                var next = body.IndexOf(part, at + 1, StringComparison.Ordinal);
                Assert.True(next > at, "\"" + part + "\" is there, after what comes before it");
                at = next;
            }
        }

        /// <summary>
        /// The replacing presses pass on only the yes their second press gave: an edited dashboard is replaced
        /// only after the question was asked and answered (PanelConfirmation), and Reinstall everything hands
        /// that answer, and nothing else, to the installer. A first press that ran with replaceEdited true
        /// would replace the driver's work without asking, with every pure test green.
        /// </summary>
        [Fact]
        public void The_replacing_presses_pass_on_only_the_yes_the_second_press_gave()
        {
            var apply = Method("private void ApplyUpdate()");
            InOrder(apply,
                "if (applying) return;",
                "if (updateStatus.State == UpdateState.Checking) return;",
                "var release = Updates.LastReleases.FirstOrDefault(r => r.Version == updateStatus.LatestVersion);",
                "applyWaiting = true; if (Check(manual: true)) return; applyWaiting = false;",
                "if (release == null) { UpdatesDrawCard(); UpdatesRefreshCheck(); Say(UpdateWording.NothingToApply, false); return; }",
                "plugin.Installer.Refresh();",
                "var edited = plugin.Installer.EditedFolders;",
                "var question = UpdateWording.ReplaceEditedQuestion(UpdatesNames(edited), onRestart: release.PluginAsset() != null);",
                "if (press == PressOutcome.Ask) { UpdatesAsk(updatesCardLine, question); return; }",
                "var replaceEdited = press == PressOutcome.RunReplacingEdited;",
                "applying = true;",
                "UpdatesDrawCard(); UpdatesRefreshCheck();",
                "UpdateService.InBackground(",
                "outcome = Updates.Apply(plugin.Installer, release, replaceEdited, report);");
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(apply, @"var replaceEdited ="));

            var reinstall = Method("private void Reinstall()");
            InOrder(reinstall,
                "if (applying) return;",
                "plugin.Installer.Refresh();",
                "var question = PanelUpdates.ReinstallQuestion(UpdatesNames(edited));",
                "if (press == PressOutcome.Ask) { UpdatesAsk(updatesReinstallLine, question); return; }",
                "var replaceEdited = press == PressOutcome.RunReplacingEdited;",
                "plugin.Installer.EnsureInstalled(true, replaceEdited);",
                "var lights = failure == null ? UpdatesBringLightsForward() : null;",
                "Save();",
                "Redraw();",
                "if (failure != null) Say(PanelUpdates.ReinstallFailed, false);",
                "else Say(PanelUpdates.ReinstallSummary(replaced, held, wroteFonts, lights, FlagBoxName(), Settings.MatrixPanels().ToList()), lights.Ok);");
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(reinstall, @"EnsureInstalled\("));
        }

        /// <summary>
        /// A run is turned on before the work leaves the interface thread, and every press it holds off is
        /// drawn disabled while it runs, whichever build drew it: the guards that return during a run are
        /// pinned above, and these are what makes them hold.
        /// </summary>
        [Fact]
        public void A_run_is_turned_on_before_it_starts_and_draws_its_presses_disabled()
        {
            InOrder(Method("private void ApplyUpdate()"), "if (press == PressOutcome.Ask)", "applying = true;", "UpdateService.InBackground(");
            Assert.Contains("restore.IsEnabled = !applying; updatesRunPresses.Add(restore);", Method("private FrameworkElement UpdatesKeptCard(double width)"));
            Assert.Contains("updatesReinstall.IsEnabled = !applying;", Method("private FrameworkElement UpdatesReinstallRow()"));
            Assert.Contains("var button = UpdatesRowPress(); button.IsEnabled = !applying; updatesRunPresses.Add(button);", Method("private FrameworkElement UpdatesStripRow("));
            Assert.Contains("var button = UpdatesRowPress(); button.IsEnabled = !applying; updatesRunPresses.Add(button);", Method("private FrameworkElement UpdatesFlagBoxRow("));
            InOrder(Method("private void UpdatesDrawCard()"), "updatesCardHost.Child = Ui.CardBox(card, 0);", "if (applying) ShowRun();");
            Assert.Contains("if (updatesProgressHost != null) updatesProgressHost.Child = UpdatesProgress(applyingFraction);", Method("private void ShowRun()"));
            Assert.Contains("applyingFraction = fraction; if (applying && updatesProgressHost != null) updatesProgressHost.Child = UpdatesProgress(fraction);", Method("private void ApplyUpdate()"));
            // The bar's host joins the card: since a download's card has no note, the bar is the only thing on
            // it that says a run is going, and a bar written into a detached host would leave the heading alone.
            InOrder(Method("private void UpdatesDrawCard()"),
                "if (updatesCard == UpdatesCard.Downloading)",
                "updatesProgressHost = new Border",
                "text.Children.Add(updatesProgressHost);",
                "if (applying) ShowRun();");
            // One verb for one run: the bar is headed "Downloading", as the press and the run's line say it,
            // never the shared bar's "Installing".
            Assert.Equal("Downloading", PanelUpdates.Downloading);
            var bar = Method("private static FrameworkElement UpdatesProgress(double fraction)");
            Assert.Contains("var head = Ui.Row(Ui.Label(PanelUpdates.Downloading, Theme.TextPrimary), Ui.Numeral(PanelCopy.Percent(fraction), Theme.SizeNumeral, Theme.TextSecondary));", bar);
            Assert.Contains("Width = PanelMetrics.ProgressFill(fraction, PanelMetrics.ProgressWidth),", bar);
            Assert.Contains("stack.Width = PanelMetrics.ProgressWidth;", bar);
            Assert.DoesNotContain("Ui.Progress(", FlatCode());
        }

        /// <summary>
        /// What each press writes, where the pure tests cannot see it: a strip's profile into its device, the
        /// flag box's into SimHub, a kept copy back into its folder, the driver's titles back over an update's,
        /// and the restart the driver said yes to. A press that wrote nothing would still say what it did.
        /// </summary>
        [Fact]
        public void Each_press_writes_what_it_says_it_wrote()
        {
            var strips = Method("private IDictionary<string, FlagBoxPlan> UpdatesWriteStrips(");
            InOrder(strips,
                "foreach (var entry in PanelUpdates.StripsToWrite(census, reachable, wanted, listed))",
                "var result = InstallBar(bar, embedded[bar.ProfileShapeId]);",
                "tally.Strip(bar.Name, PanelUpdates.DeviceName(devices, bar), entry.Value.State, result);",
                "written[PanelUpdates.StripKey(bar)] = result;",
                "foreach (var bar in PanelUpdates.StripsWithoutDevice(census, reachable, wanted, listed))",
                "tally.DeviceNotListed(bar.Name);",
                "return PanelUpdates.AfterWrite(after, reachable, written);");

            InOrder(Method("private FrameworkElement UpdatesFlagBoxRow("),
                "Func<FlagBoxPlan> press = () => { var result = InstallFlagBox(); said = new UpdatesLightsTally(); said.FlagBox(drawn.State, result); return result; };",
                "{ if (applying) return; draw(press()); RefreshAttention(); RefreshSidebar(); UpdatesSay(said); };");
            Assert.Contains("{ if (applying) return; draw(update()); RefreshAttention(); RefreshSidebar(); UpdatesSay(said); };", Method("private FrameworkElement UpdatesStripRow("));
            Assert.Contains("if (line != null) Say(line, said.Ok);", Method("private void UpdatesSay(UpdatesLightsTally said)"));

            InOrder(Method("private void RestoreKept()"),
                "plugin.Installer.Refresh();",
                "foreach (var kept in UpdatesKept())",
                "if (!PanelUpdates.PutsBack(edited.Contains(folder))) { held.Add(kept.Value); continue; }",
                "var copy = PackageExtractor.KeptCopies(root, folder).FirstOrDefault(path => path.Contains(PackageExtractor.EditedSuffix));",
                "if (copy == null) continue;",
                "if (PackageExtractor.Restore(root, folder, new SimHubInstallLog(), copy)) restored++;",
                "plugin.Installer.Refresh(); Save(); Redraw(); Say(PanelUpdates.PutBack(restored, failed, held), restored > 0 && failed.Count == 0 && held.Count == 0);");

            InOrder(Method("private void UpdatesApplied("),
                "try",
                "foreach (var screen in Settings.RigScreens()) ScreenInstaller.Retitle(screen, plugin.Installer.SimHubRoot, plugin.Installer.Record, titles);",
                "Save();",
                "plugin.Installer.Refresh();",
                "plugin.RefreshUpdateMark();",
                "updateStatus = UpdateMark.Applied(updateStatus, outcome.Ok, release.Version, plugin.RigVersion);",
                "var showing = updatesCardHost != null && IsLoaded;");

            InOrder(Method("private async void OfferRestart("),
                "var now = answer == DialogResult.Yes;",
                "PluginUpdate.AskToReopen(root, now);",
                "if (!now) return;",
                "Say(UpdateWording.RestartGoing);",
                "Save();",
                "Application.Current.Shutdown();",
                "catch",
                "PluginUpdate.AskToReopen(root, false);");

            var import = Method("private void UpdatesCopyForImport()");
            InOrder(import,
                "var copied = FlagBoxProfile.CopyForImport(plugin.FlagBox, null, new SimHubInstallLog());",
                "var ok = copied != null && copied.Status != FlagBoxStatus.Failed && copied.Status != FlagBoxStatus.NotEmbedded;",
                "updatesImportLine.Text = ok ? PanelUpdates.CopiedForImport(copied.Path) : PanelUpdates.CopyForImportFailed;");
        }

        /// <summary>
        /// The check flow reaches the shell's update state through these calls, and the pure decisions are
        /// worth nothing unless the page makes them: an answer that never redraws leaves Check now disabled and
        /// "Checking for updates…" up, and a switch turned off that never withdraws the offer leaves its card.
        /// </summary>
        [Fact]
        public void The_check_flow_draws_every_answer_the_shell_hears()
        {
            Assert.Contains("OnUpdate(UpdatesRefreshCheck, manual => UpdateAnswered());", Method("private FrameworkElement BuildUpdatesPage(PanelRoute to)"));
            InOrder(Method("private void UpdateAnswered()"),
                "UpdatesDrawCard();",
                "UpdatesRefreshCheck();",
                "var waiting = applyWaiting;",
                "applyWaiting = false;",
                "if (PanelUpdates.AppliesWhenAnswered(waiting, updateStatus.State, UpdatesPending()) && updatesCardHost != null) ApplyUpdate();");

            var row = Method("private FrameworkElement BuildCheckRow()");
            Assert.Contains("var toggle = BuildToggle(Settings.CheckForUpdates, on => { Settings.CheckForUpdates = on; Save(); updateStatus = PanelUpdates.Switched(on, plugin.LastUpdateStatus, plugin.OfferedUpdate, plugin.RigVersion); UpdatesDrawCard(); if (on) Check(manual: false); UpdatesRefreshCheck(); RefreshAttention(); RefreshSidebar(); });", row);
            Assert.Contains("var row = Ui.SettingRow(PanelUpdates.CheckTitle, Ui.HStack(PanelUpdates.CheckControlsGap, updatesCheckNow, toggle), UpdateWording.CheckCaption);", row);
            Assert.Contains("left.Children.Add(updatesLastChecked); left.Children.Add(updatesCheckLine);", row);
            InOrder(row, "left.Children.Add(updatesCheckLine);", "UpdatesRefreshCheck(); return row;");

            var refresh = Method("private void UpdatesRefreshCheck()");
            Assert.Contains("if (updatesLastChecked != null) updatesLastChecked.Text = PanelUpdates.LastChecked(Settings.LastUpdateCheckTicks, DateTime.UtcNow);", refresh);
            Assert.Contains("var line = PanelUpdates.RowLine(updatesCard, updateStatus); updatesCheckLine.Text = line ?? string.Empty; updatesCheckLine.Visibility = line == null ? Visibility.Collapsed : Visibility.Visible;", refresh);
            Assert.Contains("if (updatesCheckNow != null) updatesCheckNow.IsEnabled = PanelUpdates.CheckNowEnabled(Settings.CheckForUpdates, applying, updateStatus.State);", refresh);
            Assert.Contains("if (updatesDownload != null) updatesDownload.IsEnabled = PanelUpdates.DownloadEnabled(updateStatus.State, applying);", refresh);
            Assert.Contains("var cardLine = PanelUpdates.CardLine(updatesCard, updateStatus);", refresh);
            Assert.Contains("if (cardLine != null && updatesCardLine != null) { updatesCardLine.Text = cardLine; updatesCardLine.Visibility = Visibility.Visible; }", refresh);

            var card = Method("private void UpdatesDrawCard()");
            Assert.Contains("updatesCard = PanelUpdates.CardFor(updateStatus.State, applying, UpdatesPending());", card);
            Assert.Contains("var heading = Ui.Heading(PanelUpdates.Heading(latest), true);", card);
            Assert.Contains("var note = PanelUpdates.CardNote(updatesCard, latest);", card);
            Assert.Contains("updatesDownload.IsEnabled = PanelUpdates.DownloadEnabled(updateStatus.State, applying);", card);
            Assert.Contains("if (updatesCard == UpdatesCard.Available) card.Children.Add(UpdatesReleaseNotes());", card);
            // The brief's named source for the notes, never the release's raw text.
            var notes = Method("private FrameworkElement UpdatesReleaseNotes()");
            InOrder(notes,
                "var summary = UpdateWording.Summarise(updateStatus.Notes);",
                "var heading = PanelUpdates.NotesHeading(summary);",
                "var line = Ui.Prose(summary, Theme.SizeSmall, Theme.TextPrimary);");
            Assert.DoesNotContain("Ui.Prose(updateStatus.Notes", notes);
        }

        /// <summary>
        /// What the page draws, as drawn: every section in its place, every anchor search and Home route to
        /// placed on the section it names, every row of the table added, and each row's hover, which is the
        /// only way the reasons for "Unknown" and the steps for a device or a restart reach the driver.
        /// </summary>
        [Fact]
        public void The_page_draws_every_section_row_and_anchor_it_says()
        {
            var page = Method("private FrameworkElement BuildUpdatesPage(PanelRoute to)");
            Assert.Contains("var kept = UpdatesKeptCard(updatesWidth); updatesPage = PageLayout(PanelUpdates.Title, null, Ui.Anchor(BuildPluginSection(), PanelUpdates.AnchorPlugin), Ui.Anchor(BuildCheckRow(), PanelUpdates.AnchorCheck), Ui.Anchor(UpdatesInSimHubSection(updatesWidth, kept == null), PanelUpdates.AnchorPackages), kept, Ui.Anchor(UpdatesSupportSection(), PanelUpdates.AnchorSupport));", page);

            var keptCard = Method("private FrameworkElement UpdatesKeptCard(double width)");
            InOrder(keptCard,
                "var kept = UpdatesKept(); if (kept.Count == 0) return null;",
                "Ui.Text(PanelUpdates.KeptHeading(kept.Count),",
                "Ui.Caption(PanelUpdates.KeptLine(kept.Select(k => k.Value).ToList()), BodyWidth)",
                "var restore = Ui.Button(PanelUpdates.PutMineBack,",
                "return Ui.Anchor(card, PanelUpdates.AnchorKept);");

            var section = Method("private FrameworkElement UpdatesInSimHubSection(double width, bool keptAnchorHere)");
            InOrder(section,
                "rows.Children.Add(UpdatesTableHead(versionWidth));",
                "foreach (var pair in UpdatesDashboardRows()) { Action<UpdatesRow> paint; rows.Children.Add(UpdatesTableRow(pair.Value, versionWidth, null, out paint));",
                "var lights = UpdatesLightRows(strips, stripRows, flagBoxPlan, flagBoxRow, versionWidth); foreach (var row in lights) rows.Children.Add(row); if (rows.Children.Count == 1) rows.Children.Add(UpdatesTableEmpty());",
                "foreach (var note in notes) children.Add(UpdatesNote(note));",
                "if (PanelUpdates.ShowsImportFallback(flagBoxPlan)) { var fallback = UpdatesFlagBoxFallback(flagBoxPlan);",
                "children.Add(fallback);",
                "var reinstall = Ui.Anchor(UpdatesReinstallRow(), PanelUpdates.AnchorReinstall);",
                "return PageSection(PanelUpdates.InSimHubTitle, false, PanelUpdates.SectionGap, children.ToArray());");
            Assert.Contains("var text = Ui.Caption(PanelUpdates.NothingInSimHub, BodyWidth);", Method("private static FrameworkElement UpdatesTableEmpty()"));

            var lights = Method("private IList<FrameworkElement> UpdatesLightRows(");
            Assert.Contains("for (var i = 0; i < strips.Count; i++) drawn.Add(UpdatesStripRow(strips[i].Key, stripRows[i], versionWidth, painters));", lights);
            Assert.Contains("if (flagBoxPlan != null) drawn.Add(UpdatesFlagBoxRow(flagBoxPlan, flagBoxRow, versionWidth));", lights);

            // The restart is known only from the shell's facts, and a row fed none never says it.
            Assert.Contains("var row = PanelUpdates.DashboardRow(screen, package, facts == null ? null : facts.Installed, facts == null ? null : facts.AddedSinceStart);", Method("private IList<KeyValuePair<ScreenInstance, UpdatesRow>> UpdatesDashboardRows()"));

            var row = Method("private static Border UpdatesTableRow(");
            Assert.Contains("paint = current => { version.Text = current.Version; stacked.Text = PanelUpdates.VersionInName(versionWidth, current.Version); state.Text = current.State; state.Foreground = Ui.Brush(current.StateHex); dot.Fill = Ui.Brush(current.DotHex); border.ToolTip = current.Tooltip; }; paint(row); return border;", row);

            var support = Method("private FrameworkElement UpdatesSupportSection()");
            InOrder(support,
                "foreach (var press in new FrameworkElement[] { copy, log, issue, guide })",
                "var caption = Ui.Prose(PanelUpdates.SupportCaption, PanelUpdates.SupportCaptionSize);",
                "var licence = Ui.Prose(PanelUpdates.Licence, PanelUpdates.SupportCaptionSize, Theme.TextLabel);",
                "return PageSection(PanelUpdates.SupportTitle, false, PanelUpdates.SectionGap, presses, caption, licence);");

            // The by-hand route's press reaches its copy.
            Assert.Contains("copy.Click += (sender, args) => UpdatesCopyForImport();", Method("private FrameworkElement UpdatesFlagBoxFallback(FlagBoxPlan plan)"));
        }

        /// <summary>
        /// The page's responsive choices are made where it draws, from the width it was given: the pure tests
        /// hold ButtonBeside, VersionWidth and ColumnWidth, and they are worth nothing unless the draw files
        /// call them with the content width. A stack test turned round, a card drawn at a fixed width, or a
        /// version column sized from a constant would put Download beside its words in a narrow column, or
        /// leave an empty 126 px column while the version also prints in the name.
        /// </summary>
        [Fact]
        public void The_page_stacks_and_drops_a_column_from_the_width_it_is_given()
        {
            var beside = Method("private static FrameworkElement UpdatesBeside(");
            InOrder(beside,
                "if (press == null) return text;",
                "if (!PanelUpdates.ButtonBeside(width)) { press.HorizontalAlignment = HorizontalAlignment.Left; press.Margin = new Thickness(0, gap, 0, 0); return Ui.VStack(0, text, press); }",
                "var grid = new Grid();");
            Assert.Contains("Child = UpdatesBeside(text, updatesDownload, PanelUpdates.CardGap, updatesWidth),", Method("private void UpdatesDrawCard()"));
            Assert.Contains("var body = UpdatesBeside(text, restore, PanelUpdates.KeptGap, width);", Method("private FrameworkElement UpdatesKeptCard(double width)"));
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(FlatCode(), @"UpdatesBeside\(text, ").Count);

            var section = Method("private FrameworkElement UpdatesInSimHubSection(double width, bool keptAnchorHere)");
            Assert.Contains("var versionWidth = PanelUpdates.VersionWidth(width, PanelUpdates.TableHasPress(lightRows));", section);
            var grid = Method("private static Grid UpdatesTableGrid(double versionWidth)");
            InOrder(grid,
                "new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }",
                "new ColumnDefinition { Width = new GridLength(PanelUpdates.ColumnWidth(versionWidth)) }",
                "new ColumnDefinition { Width = new GridLength(PanelUpdates.ColumnWidth(PanelUpdates.TableStateWidth)) }",
                "new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = \"UpdatesPress\" }");
            // A hidden version column takes no gap and draws nothing, as the head's and every row's cell.
            var place = Method("private static void UpdatesPlace(");
            Assert.Contains("var gap = column == 0 || column == 3 || (column == 1 && versionWidth <= 0) ? 0 : PanelUpdates.TableGap;", place);
            Assert.Contains("if (column == 1 && versionWidth <= 0) element.Visibility = Visibility.Collapsed;", place);
            InOrder(Method("private static FrameworkElement UpdatesTableHead(double versionWidth)"),
                "UpdatesPlace(grid, Ui.Eyebrow(PanelUpdates.ItemColumn), 0, versionWidth);",
                "UpdatesPlace(grid, Ui.Eyebrow(PanelUpdates.VersionColumn), 1, versionWidth);",
                "UpdatesPlace(grid, Ui.Eyebrow(PanelUpdates.StateColumn), 2, versionWidth);");
        }

        /// <summary>
        /// The table and the cards wrap and stretch rather than clip or centre (hooks rule 2): at the 587 px
        /// narrow column with an Update press the name column is 181 px, and "OpenDash Flag box · matrix
        /// profile" is 192 px in Barlow 14 and 12, so a name that did not wrap would lose its last word. The
        /// rows share the press column, so one with Update keeps its version and state under the head's. The
        /// four Support presses with their NEW tags are about 635 px, so a row that did not wrap would clip
        /// "Read the guide". A caption beside its press measured unbounded clips beside it, and a bar that is
        /// not held left sits centred in the card.
        /// </summary>
        [Fact]
        public void The_table_and_cards_wrap_and_stretch_rather_than_clip()
        {
            var row = Method("private static Border UpdatesTableRow(");
            Assert.Contains("name.TextWrapping = TextWrapping.Wrap;", row);
            Assert.Contains("state.TextWrapping = TextWrapping.Wrap;", row);
            InOrder(row,
                "cell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });",
                "cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });");
            InOrder(Method("private FrameworkElement UpdatesInSimHubSection(double width, bool keptAnchorHere)"),
                "var rows = new StackPanel { Orientation = Orientation.Vertical };",
                "Grid.SetIsSharedSizeScope(rows, true);");

            Assert.Contains("title.TextWrapping = TextWrapping.Wrap;", Method("private FrameworkElement UpdatesKeptCard(double width)"));
            Assert.Contains("var presses = new WrapPanel { Orientation = Orientation.Horizontal };", Method("private FrameworkElement UpdatesSupportSection()"));

            var card = Method("private void UpdatesDrawCard()");
            Assert.Contains("var headLine = new WrapPanel { Orientation = Orientation.Horizontal };", card);
            Assert.Contains("updatesProgressHost = new Border { HorizontalAlignment = HorizontalAlignment.Left,", card);

            // The words take the star column and the press its own width, in both places a press sits beside
            // words, so the words are measured against what the press leaves them.
            InOrder(Method("private static FrameworkElement UpdatesBeside("),
                "grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });",
                "Grid.SetColumn(text, 0); Grid.SetColumn(press, 1);");
            InOrder(Method("private FrameworkElement UpdatesReinstallRow()"),
                "grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });",
                "Grid.SetColumn(updatesReinstall, 0); Grid.SetColumn(updatesReinstallLine, 1);");
        }

        /// <summary>
        /// The draw-time choices that are not a press or a width: the version the card says the rig has, the
        /// names SimHub lists a folder under (voice.md: no folder names), the devices the select step names,
        /// and a waiting Download dropped when the page is left.
        /// </summary>
        [Fact]
        public void The_page_draws_what_the_rig_has_by_the_names_SimHub_lists()
        {
            var card = Method("private void UpdatesDrawCard()");
            InOrder(card,
                "var installed = plugin.RigVersion;",
                "if (PanelUpdates.ShowsYouHave(installed))",
                "Ui.Text(PanelUpdates.YouHave, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary), Ui.Numeral(installed, PanelUpdates.CardVersionSize, Theme.TextPrimary));");
            Assert.DoesNotContain("Ui.Numeral(latest", card);

            var kept = Method("private IList<KeyValuePair<string, string>> UpdatesKept()");
            Assert.Contains("kept.Add(new KeyValuePair<string, string>(folder, PanelUpdates.ScreenName(screens, folder)));", kept);
            Assert.Contains("var screens = Settings.RigScreens();", kept);
            var names = Method("private IReadOnlyCollection<string> UpdatesNames(IEnumerable<string> folders)");
            Assert.Contains("var screens = Settings.RigScreens(); return (folders ?? Enumerable.Empty<string>()).Select(folder => PanelUpdates.ScreenName(screens, folder)).ToList();", names);
            // Both questions name what they replace through UpdatesNames, never the installer's folders.
            Assert.Contains("UpdateWording.ReplaceEditedQuestion(UpdatesNames(edited),", Method("private void ApplyUpdate()"));
            Assert.Contains("PanelUpdates.ReinstallQuestion(UpdatesNames(edited));", Method("private void Reinstall()"));

            Assert.Contains("if (target != null && target.Id != null) devices[target.Id] = target.Name;", Method("private static IDictionary<string, string> UpdatesDevices()"));

            Assert.Contains("OnLeave(\"Updates.applyWaiting\", () => applyWaiting = false);", Method("private FrameworkElement BuildUpdatesPage(PanelRoute to)"));
        }

        /// <summary>
        /// The support report is gathered as its caption promises: OpenDash's last lines of SimHub's current
        /// log, read before the folders are (whose read logs a line per package and would push the older ones
        /// out), and every field the composer is handed. The composer's own test cannot see what it is given.
        /// </summary>
        [Fact]
        public void The_support_report_is_gathered_as_its_caption_says()
        {
            var input = Method("private UpdatesReportInput UpdatesReportInput()");
            InOrder(input, "var log = UpdatesLogTail(root);", "if (!applying) plugin.Installer.Refresh();", "var screens = UpdatesDashboardRows()");
            foreach (var field in new[]
            {
                "GeneratedUtc = DateTime.UtcNow,", "PluginVersion = OpenDash.Version,", "RigVersion = plugin.RigVersion,", "SimHubVersion = UpdatesSimHubVersion(root),",
                "SimHubRoot = root,", "OsVersion = Environment.OSVersion.VersionString,", "ChecksOn = Settings.CheckForUpdates,", "LastCheckedTicks = Settings.LastUpdateCheckTicks,",
                "UpdateLine = updateStatus.Line,", "RestartPending = UpdatesPending(),", "Screens = screens,", "Strips = strips,", "Matrices = matrices,",
                "FlagBox = flagBox,", "CarTables = carTables,", "Log = log,",
            })
            {
                Assert.Contains(field, input);
            }
            Assert.Contains("if (PanelUpdates.DrawsFlagBoxRow(matrices.Count > 0) && plugin.FlagBoxJson != null)", input);
            // What each field is filled with, not only that it is filled: a report naming no screen's kind, no
            // strip's shape, no matrix or no car tables would pass the field pins above.
            foreach (var source in new[]
            {
                ".Select(pair => new UpdatesReportItem(pair.Value.Name, PanelUpdates.ScreenDetail(pair.Key.Kind, pair.Key.Width, pair.Key.Height), pair.Value.Version, pair.Value.State))",
                "var census = PanelUpdates.StripPlans(BarCensus(embedded, out reachable), reachable, embedded.Keys);",
                "var row = PanelUpdates.StripRow(entry.Key.Name, entry.Value);",
                "var detail = PanelUpdates.StripDetail(PanelLightRows.ShapeLabel(entry.Key.ProfileShapeId), entry.Key.Device);",
                "strips.Add(new UpdatesReportItem(row.Name, detail, row.Version, row.State));",
                ".Select(m => new UpdatesReportItem(PanelUpdates.MatrixName(m), Settings.MatrixName(m), null, null))",
                "var row = PanelUpdates.FlagBoxRow(FlagBoxName(), SafePlan(), plugin.FlagBox?.Path); flagBox = new UpdatesReportItem(row.Name, null, row.Version, row.State);",
                "var carTables = cars == null ? null : PanelUpdates.CarTables(cars.Status, cars.FetchedAt);",
            })
            {
                Assert.Contains(source, input);
            }
            var simHub = Method("private static string UpdatesSimHubVersion(string root)");
            InOrder(simHub,
                "var exe = Path.Combine(root ?? string.Empty, PanelUpdates.SimHubExe);",
                "return File.Exists(exe) ? FileVersionInfo.GetVersionInfo(exe).FileVersion : null;");

            var tail = Method("private static IList<string> UpdatesLogTail(string root)");
            InOrder(tail,
                "var path = Path.Combine(root ?? string.Empty, PanelUpdates.LogFolder, PanelUpdates.LogFile);",
                "new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)",
                "return PanelUpdates.Tail(UpdatesLines(reader));");
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
