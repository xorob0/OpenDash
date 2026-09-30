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
            Assert.Equal("OpenDash is free software under the MIT licence.", PanelUpdates.Licence);
            Assert.Equal("Nothing yet. Add a screen on the Screens page.", PanelUpdates.NothingInSimHub);
        }

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
        }

        /// <summary>A press sits beside its words while there is room for both, and under them below that,
        /// whatever the sidebar is doing.</summary>
        [Fact]
        public void A_press_goes_under_its_words_only_when_the_content_is_narrow()
        {
            Assert.True(PanelUpdates.ButtonBeside(PanelShell.ContentMax));
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
            Assert.Equal("Update checks are off.", PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Disabled }));
            Assert.Equal("You have the newest release, 0.5.0.", PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = "0.5.0", Manual = true }));
            Assert.Equal("Could not reach GitHub. You have 0.5.0.", PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Unreachable, InstalledVersion = "0.5.0", Manual = true }));
            // A background check that finds nothing says nothing, and an offer is the card's.
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = "0.5.0" }));
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Unreachable, InstalledVersion = "0.5.0" }));
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.UpdateAvailable, LatestVersion = "0.5.1", Manual = true }));
            Assert.Null(PanelUpdates.CheckLine(new UpdateStatus { State = UpdateState.Idle }));
            Assert.Null(PanelUpdates.CheckLine(null));
        }

        [Fact]
        public void The_check_row_is_drawn_at_the_artboard_s_numbers()
        {
            Assert.Equal(14, PanelKit.RowPaddingYUpdates);
            Assert.Equal(12, PanelUpdates.CheckControlsGap);
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
            Assert.Equal(110, PanelUpdates.VersionWidth(PanelShell.ContentMax));
            Assert.Equal(110, PanelUpdates.VersionWidth(480));
            Assert.Equal(0, PanelUpdates.VersionWidth(479));
            Assert.Equal(0, PanelUpdates.VersionWidth(340));
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

        private static PackageStatus Package(InstallStatus status, string version = "0.5.0", bool edited = false)
        {
            return new PackageStatus { FolderName = "OpenDash Rim", Status = status, InstalledVersion = version, Edited = edited, HeldBack = edited };
        }

        [Fact]
        public void A_dashboard_row_says_its_state_in_the_install_words()
        {
            var current = PanelUpdates.DashboardRow("Rim", Package(InstallStatus.UpToDate), true, false);
            Assert.Equal("Rim", current.Name);
            Assert.Equal("dashboard", current.Kind);
            Assert.Equal("0.5.0", current.Version);
            Assert.Equal("Up to date", current.State);
            Assert.Equal(Theme.StatusUpToDate, current.StateHex);
            Assert.Equal(Theme.StatusUpToDate, current.DotHex);
            Assert.False(current.OffersUpdate);

            var older = PanelUpdates.DashboardRow("Rim", Package(InstallStatus.UpdateAvailable, "0.4.2"), true, false);
            Assert.Equal("Update available", older.State);
            Assert.Equal(Theme.StatusUpdateAvailable, older.StateHex);
            Assert.Equal("0.4.2", older.Version);

            var edited = PanelUpdates.DashboardRow("Rim", Package(InstallStatus.UpdateAvailable, "0.4.2", true), true, false);
            Assert.Contains("You have edited it", edited.Tooltip);

            var none = PanelUpdates.DashboardRow("Rim", Package(InstallStatus.NotInstalled, null), null, null);
            Assert.Equal("Not installed", none.State);
            Assert.Equal(Theme.TextLabel, none.StateHex);
            Assert.Equal(Theme.StatusNotInstalled, none.DotHex);
            Assert.Equal(string.Empty, none.Version);

            Assert.Equal("Not installed", PanelUpdates.DashboardRow("Rim", null, null, null).State);
        }

        /// <summary>A failure outranks everything, then a folder that has gone, then one SimHub has not loaded
        /// yet, which is said only when the shell's facts know it.</summary>
        [Fact]
        public void A_dashboard_row_puts_a_failure_then_a_missing_folder_then_a_restart_first()
        {
            var failed = PanelUpdates.DashboardRow("Rim", Package(InstallStatus.Failed), false, true);
            Assert.Equal("Install failed", failed.State);
            Assert.Equal(Theme.StatusFailed, failed.StateHex);
            Assert.Equal("Install failed. See SimHub's log.", failed.Tooltip);

            var missing = PanelUpdates.DashboardRow("Rim", Package(InstallStatus.UpToDate), false, true);
            Assert.Equal("Missing", missing.State);
            Assert.Equal(Theme.StatusUpdateAvailable, missing.StateHex);
            Assert.Equal(string.Empty, missing.Version);

            var waiting = PanelUpdates.DashboardRow("Rim", Package(InstallStatus.UpToDate), true, true);
            Assert.Equal("Waiting for a restart", waiting.State);
            Assert.Equal(Theme.StatusUpdateAvailable, waiting.StateHex);
            Assert.Equal("0.5.0", waiting.Version);

            // Unknown facts say nothing of a restart.
            Assert.Equal("Up to date", PanelUpdates.DashboardRow("Rim", Package(InstallStatus.UpToDate), null, null).State);
        }

        [Fact]
        public void A_strip_row_names_the_strip_and_offers_an_update_only_while_its_profile_is_older()
        {
            var older = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Outdated, InstalledVersion = "0.4.2", EmbeddedVersion = "0.5.0" });
            Assert.Equal("Wheel rim", older.Name);
            Assert.Equal("LED profile", older.Kind);
            Assert.Equal("0.4.2", older.Version);
            Assert.Equal("Update available", older.State);
            Assert.Equal(Theme.StatusUpdateAvailable, older.StateHex);
            Assert.True(older.OffersUpdate);
            Assert.Contains(FlagBoxInstallPlan.Replaces, older.Tooltip);

            var current = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0" });
            Assert.Equal("Up to date", current.State);
            Assert.False(current.OffersUpdate);

            var none = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled });
            Assert.Equal("Not installed", none.State);
            Assert.Equal("Its profile is not in SimHub.", none.Tooltip);
            Assert.False(none.OffersUpdate);

            var unreachable = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable });
            Assert.Equal("Not installed", unreachable.State);
            Assert.Equal(PanelLightRows.Unavailable, unreachable.Tooltip);

            Assert.Equal("Install failed", PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Failed }).State);
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

            var current = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0" }, null);
            Assert.Equal("Up to date", current.State);
            Assert.False(current.OffersUpdate);
            Assert.False(PanelUpdates.FlagBoxRow("OpenDash Flag box", null, null).OffersUpdate);
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

        [Fact]
        public void Reinstall_everything_says_what_it_wrote_and_the_step_left()
        {
            Assert.Equal("Reinstalled 3 dashboards. Close and reopen the dashboard to see it.",
                PanelUpdates.ReinstallSummary(3, 0, false, 0, 0, "OpenDash Flag box", null));
            Assert.Equal("Reinstalled 1 dashboard. Restart SimHub to see it.",
                PanelUpdates.ReinstallSummary(1, 0, true, 0, 0, "OpenDash Flag box", null));
            Assert.Equal("Reinstalled 2 dashboards. The one you edited was left alone. Updated 2 LED profiles. Updated OpenDash Flag box. Close and reopen the dashboard to see it.",
                PanelUpdates.ReinstallSummary(2, 1, false, 2, 0, "OpenDash Flag box", FlagBoxInstallState.UpToDate));
            Assert.Equal("Reinstalled 0 dashboards. The 2 you edited were left alone.",
                PanelUpdates.ReinstallSummary(0, 2, false, 0, 0, "OpenDash Flag box", null));
            Assert.True(PanelUpdates.ReinstallOk(0, null));
            Assert.True(PanelUpdates.ReinstallOk(0, FlagBoxInstallState.UpToDate));
        }

        [Fact]
        public void A_profile_that_could_not_be_updated_is_said_with_where_to_look()
        {
            var line = PanelUpdates.ReinstallSummary(2, 0, false, 1, 1, "OpenDash Flag box", FlagBoxInstallState.Failed);
            Assert.Equal("Reinstalled 2 dashboards. Updated 1 LED profile. 1 LED profile could not be updated. OpenDash Flag box could not be updated. See SimHub's log.", line);
            Assert.False(PanelUpdates.ReinstallOk(1, null));
            Assert.False(PanelUpdates.ReinstallOk(0, FlagBoxInstallState.Failed));
            Assert.Equal("The reinstall did not finish: disk full", PanelUpdates.ReinstallFailed("disk full"));
        }

        // --- The kept copy ----------------------------------------------------------------------------

        [Fact]
        public void The_kept_card_names_what_was_kept()
        {
            Assert.Equal("Your edited Main dash was kept", PanelUpdates.KeptTitle(new[] { "Main dash" }));
            Assert.Equal("Your edited Rim and Main dash were kept", PanelUpdates.KeptTitle(new[] { "Rim", "Main dash" }));
            Assert.Equal("Your edited Rim, Pit wall and Main dash were kept", PanelUpdates.KeptTitle(new[] { "Rim", "Pit wall", "Main dash" }));
            Assert.Equal("Your edited dashboard was kept", PanelUpdates.KeptTitle(new string[0]));
            Assert.Equal("The last update replaced it. Your copy is still here.", PanelUpdates.KeptCaption(1));
            Assert.Equal("The last update replaced them. Your copies are still here.", PanelUpdates.KeptCaption(2));
            Assert.Equal(18, PanelUpdates.KeptPaddingX);
            Assert.Equal(14, PanelUpdates.KeptPaddingY);
            Assert.Equal(16, PanelUpdates.KeptGap);
            Assert.Equal(3, PanelUpdates.KeptTextGap);
            Assert.Equal(15, PanelUpdates.KeptTitleSize);
        }

        [Fact]
        public void Put_mine_back_says_what_it_restored()
        {
            Assert.Equal("There was nothing to put back.", PanelUpdates.PutBack(0));
            Assert.Equal("Put back 1 dashboard. Close and reopen the dashboard to see it.", PanelUpdates.PutBack(1));
            Assert.Equal("Put back 2 dashboards. Close and reopen the dashboard to see it.", PanelUpdates.PutBack(2));
        }

        // --- Support ----------------------------------------------------------------------------------

        [Fact]
        public void Support_says_what_happened_after_a_press()
        {
            Assert.Equal("Support report copied. Paste it into your issue.", PanelUpdates.ReportCopied);
            Assert.Equal("Could not copy the support report: busy", PanelUpdates.ReportFailed("busy"));
            Assert.Equal("SimHub has not written a log yet.", PanelUpdates.LogMissing);
            Assert.Equal("Could not open SimHub's log folder: denied", PanelUpdates.LogFailed("denied"));
            Assert.Equal("Logs", PanelUpdates.LogFolder);
            Assert.Equal("SimHub.txt", PanelUpdates.LogFile);
            Assert.Equal(8, PanelUpdates.SupportButtonGap);
            Assert.Equal(12, PanelUpdates.SupportCaptionSize);
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
                Screens = new[] { new UpdatesReportItem("Rim", "face 850x480", "0.5.0", "Up to date") },
                Strips = new[] { new UpdatesReportItem("Wheel rim", "3/9/3 Fanatec on fanatec-1", "0.4.2", "Update available") },
                Matrices = new[] { new UpdatesReportItem("Matrix 1", "Flag box", null, null) },
                FlagBox = new UpdatesReportItem("OpenDash Flag box", null, "0.5.0", "Up to date"),
                CarTables = "Ready (412 cars)",
                Log = new[] { "INFO [OpenDash] one", "WARN [OpenDash] two" },
            });
            var lines = report.Replace("\r\n", "\n").Split('\n');
            Assert.Equal("OpenDash support report", lines[0]);
            Assert.Contains("Written 2026-09-30 14:05 UTC", lines);
            Assert.Contains("OpenDash plugin: 0.5.1", lines);
            Assert.Contains("Dashboards on the rig: 0.5.0", lines);
            Assert.Contains(@"SimHub: 9.12.6, in C:\Program Files (x86)\SimHub", lines);
            Assert.Contains("Windows: Microsoft Windows NT 10.0.19045.0", lines);
            Assert.Contains("Update check: on. Last checked today, 09:12 (UTC).", lines);
            Assert.Contains("Update status: Version 0.5.2 is available. You have 0.5.0.", lines);
            Assert.Contains("Update status: " + UpdateWording.RestartLater, lines);
            Assert.Contains("Screens (1)", lines);
            Assert.Contains("- Rim (face 850x480): Up to date, 0.5.0", lines);
            Assert.Contains("LED strips (1)", lines);
            Assert.Contains("- Wheel rim (3/9/3 Fanatec on fanatec-1): Update available, 0.4.2", lines);
            Assert.Contains("Matrices (1)", lines);
            Assert.Contains("- Matrix 1 (Flag box)", lines);
            Assert.Contains("Flag box profile: OpenDash Flag box: Up to date, 0.5.0", lines);
            Assert.Contains("Car tables: Ready (412 cars)", lines);
            Assert.Contains("SimHub's log, the last 2 OpenDash lines:", lines);
            Assert.Equal("WARN [OpenDash] two", lines.Last(line => line.Length > 0));
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
            Assert.Equal(PanelUpdates.AnchorCheck, PanelUpdates.Search.Single(e => e.Label == PanelUpdates.CheckTitle).Route.Anchor);
            Assert.Equal(PanelUpdates.AnchorReinstall, PanelUpdates.Search.Single(e => e.Label == PanelConfirmation.ReinstallLabel).Route.Anchor);
            Assert.Equal(PanelUpdates.AnchorKept, PanelUpdates.Search.Single(e => e.Label == PanelUpdates.PutMineBack).Route.Anchor);
            Assert.Equal(PanelPage.Updates, PanelSearch.Find(PanelSearch.All(), "repair").First().Route.Page);
            Assert.Equal(PanelPage.Updates, PanelSearch.Find(PanelSearch.All(), "support report").First().Route.Page);
            Assert.Empty(PanelUpdates.SoonDrawn);
        }
    }
}
