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

        [Fact]
        public void The_card_and_the_check_row_say_what_their_presses_do()
        {
            Assert.Equal("Downloads OpenDash 0.5.1.", PanelUpdates.DownloadTooltip("0.5.1"));
            Assert.Equal("Asks GitHub for the newest release now.", PanelUpdates.CheckNowTooltip);
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
            // The gap is outside each fixed column's own width, as the artboard's grid gap is.
            Assert.Equal(126, PanelUpdates.ColumnWidth(PanelUpdates.TableVersionWidth));
            Assert.Equal(166, PanelUpdates.ColumnWidth(PanelUpdates.TableStateWidth));
            Assert.Equal(0, PanelUpdates.ColumnWidth(0));
            Assert.Equal(324, 2 * PanelUpdates.TableRowPaddingX + PanelUpdates.ColumnWidth(PanelUpdates.TableVersionWidth) + PanelUpdates.ColumnWidth(PanelUpdates.TableStateWidth));
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
        /// yet, which is said only when the shell's facts know it, each in the table's own words: none is read
        /// from the Screens page, whose card words are its own to change.</summary>
        [Fact]
        public void A_dashboard_row_puts_a_failure_then_a_missing_folder_then_a_restart_first()
        {
            var failed = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.Failed), false, true);
            Assert.Equal("Install failed", failed.State);
            Assert.Equal(Theme.StatusFailed, failed.StateHex);
            Assert.Equal("Install failed. See SimHub's log.", failed.Tooltip);

            var missing = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), false, true);
            Assert.Equal("Missing from SimHub", missing.State);
            Assert.Equal(Theme.StatusFailed, missing.StateHex);
            Assert.Equal(string.Empty, missing.Version);
            Assert.Equal("Rim's dashboard is missing from SimHub. Reinstall everything installs it again.", missing.Tooltip);

            var waiting = PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), true, true);
            Assert.Equal("Waiting for a restart", waiting.State);
            Assert.Equal(Theme.Caution, waiting.StateHex);
            Assert.Equal("0.5.0", waiting.Version);
            Assert.Equal("Restart SimHub, then assign \"Rim\" to this display in Dash Studio.", waiting.Tooltip);

            // Unknown facts say nothing of a restart.
            Assert.Equal("Up to date", PanelUpdates.DashboardRow(Rim, Package(InstallStatus.UpToDate), null, null).State);
        }

        /// <summary>A screen this build ships nothing for is left as it is by the installer, so its row says
        /// what the facts say and why no press here changes it, rather than "Not installed" with a press that
        /// would leave it alone.</summary>
        [Fact]
        public void A_screen_this_build_ships_nothing_for_says_so()
        {
            var inSimHub = PanelUpdates.DashboardRow(Rim, null, true, false);
            Assert.Equal("In SimHub", inSimHub.State);
            Assert.Equal("This build of OpenDash ships no 1280 × 480 face.", inSimHub.Tooltip);
            Assert.Equal(string.Empty, inSimHub.Version);
            Assert.Equal("Missing from SimHub", PanelUpdates.DashboardRow(Rim, null, false, false).State);
            Assert.Equal("Not installed", PanelUpdates.DashboardRow(Rim, null, null, null).State);
            var pitWall = new ScreenInstance { Name = "Pit", Kind = Contract.KindPitWall, Width = 1920, Height = 1080 };
            Assert.Equal("This build of OpenDash ships no 1920 × 1080 pit wall.", PanelUpdates.DashboardRow(pitWall, null, null, null).Tooltip);
        }

        /// <summary>The sentences under the table: what the build ships and what the installer could not do,
        /// then the light profiles' own, which uncovered 24 puts here.</summary>
        [Fact]
        public void The_table_s_notes_say_what_the_build_ships_and_what_could_not_be_read()
        {
            Assert.Equal("OpenDash never installs a profile on its own.", PanelLightRows.SectionCaption);
            Assert.Equal("SimHub's LED settings are not available.", PanelLightRows.Unavailable);
            Assert.Equal("This build ships no light profiles.", PanelLightRows.NoProfiles);

            Assert.Equal(new[] { PanelLightRows.SectionCaption }, PanelUpdates.TableNotes(true, null, false, true, true, true));
            Assert.Equal(new[] { "This build of OpenDash ships no dashboards.", PanelLightRows.SectionCaption },
                PanelUpdates.TableNotes(false, "anything", false, false, false, true));
            Assert.Equal(new[] { "OpenDash could not read or write a dashboard. See SimHub's log.", PanelLightRows.SectionCaption },
                PanelUpdates.TableNotes(true, "disk full", false, false, true, true));
            // A row already says "Install failed", and the note would repeat it.
            Assert.Equal(new[] { PanelLightRows.SectionCaption }, PanelUpdates.TableNotes(true, "disk full", true, false, true, true));
            Assert.Equal(new[] { PanelLightRows.SectionCaption, PanelLightRows.NoProfiles }, PanelUpdates.TableNotes(true, null, false, true, false, true));
            Assert.Equal(new[] { PanelLightRows.SectionCaption, PanelLightRows.Unavailable }, PanelUpdates.TableNotes(true, null, false, true, true, false));
            // No strip, no sentence about strips.
            Assert.Equal(new[] { PanelLightRows.SectionCaption }, PanelUpdates.TableNotes(true, null, false, false, false, false));
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
            Assert.Equal(Theme.StatusUpdateAvailable, older.DotHex);
            Assert.True(older.OffersUpdate);
            Assert.Equal("A newer profile is available (0.4.2 to 0.5.0). Replaces the copy in SimHub, including your changes to it.", older.Tooltip);

            var current = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0" });
            Assert.Equal("Up to date", current.State);
            Assert.Equal("Installed and up to date.", current.Tooltip);
            Assert.False(current.OffersUpdate);

            var none = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled });
            Assert.Equal("Not installed", none.State);
            Assert.Equal(Theme.StatusNotInstalled, none.DotHex);
            Assert.Equal("Its profile is not in SimHub. Reinstall everything installs it.", none.Tooltip);
            Assert.False(none.OffersUpdate);

            var unreachable = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable });
            Assert.Equal("Not installed", unreachable.State);
            Assert.Equal(PanelLightRows.Unavailable, unreachable.Tooltip);

            var failed = PanelUpdates.StripRow("Wheel rim", new FlagBoxPlan { State = FlagBoxInstallState.Failed });
            Assert.Equal("Install failed", failed.State);
            Assert.Equal("Install failed. See SimHub's log.", failed.Tooltip);
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

            Assert.Equal("A newer profile is available (0.4.2 to 0.5.0). Replaces the copy in SimHub, including your changes to it.", older.Tooltip);

            var current = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.5.0" }, null);
            Assert.Equal("Up to date", current.State);
            // The row has no Reinstall, so it is not told about one.
            Assert.Equal("Installed and up to date.", current.Tooltip);
            Assert.False(current.OffersUpdate);

            var none = PanelUpdates.FlagBoxRow("OpenDash Flag box", null, null);
            Assert.False(none.OffersUpdate);
            Assert.Equal("Not installed. Reinstall everything installs it, then select it on your device.", none.Tooltip);

            var unreachable = PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, @"C:\SimHub\OpenDash\flag-box.json");
            Assert.Equal(@"SimHub's matrix settings are not available. Import it by hand from C:\SimHub\OpenDash\flag-box.json.", unreachable.Tooltip);
            Assert.Equal("Install failed. See SimHub's log.", PanelUpdates.FlagBoxRow("OpenDash Flag box", new FlagBoxPlan { State = FlagBoxInstallState.Failed }, null).Tooltip);
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

        /// <summary>Counts strips one by one, and a flag box write by what it was before.</summary>
        private static UpdatesLightsTally Tally(int updated = 0, int installed = 0, int notUpdated = 0, int notInstalled = 0, FlagBoxInstallState? flagBoxBefore = null, FlagBoxInstallState? flagBoxAfter = null)
        {
            var tally = new UpdatesLightsTally();
            for (var i = 0; i < updated; i++) tally.Strip(FlagBoxInstallState.Outdated, FlagBoxInstallState.UpToDate);
            for (var i = 0; i < installed; i++) tally.Strip(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate);
            for (var i = 0; i < notUpdated; i++) tally.Strip(FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed);
            for (var i = 0; i < notInstalled; i++) tally.Strip(FlagBoxInstallState.Failed, FlagBoxInstallState.Failed);
            if (flagBoxBefore.HasValue) tally.FlagBox(flagBoxBefore.Value, flagBoxAfter.Value);
            return tally;
        }

        [Fact]
        public void Reinstall_everything_says_what_it_wrote_and_the_step_left()
        {
            Assert.Equal("Reinstalled 3 dashboards. Close and reopen the dashboard to see it.",
                PanelUpdates.ReinstallSummary(3, 0, false, Tally(), "OpenDash Flag box"));
            Assert.Equal("Reinstalled 1 dashboard. Restart SimHub to see it.",
                PanelUpdates.ReinstallSummary(1, 0, true, null, "OpenDash Flag box"));
            // The step follows the dashboards, so "it" is one of them and not the last profile named.
            Assert.Equal("Reinstalled 2 dashboards. The one you edited was left alone. Close and reopen the dashboard to see it. Updated 2 LED profiles. Updated OpenDash Flag box.",
                PanelUpdates.ReinstallSummary(2, 1, false, Tally(updated: 2, flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate), "OpenDash Flag box"));
            Assert.Equal("Reinstalled 1 dashboard. Close and reopen the dashboard to see it. Updated 1 LED profile. Installed 2 LED profiles. Installed OpenDash Flag box.",
                PanelUpdates.ReinstallSummary(1, 0, false, Tally(updated: 1, installed: 2, flagBoxBefore: FlagBoxInstallState.NotInstalled, flagBoxAfter: FlagBoxInstallState.UpToDate), "OpenDash Flag box"));
            Assert.Equal("Reinstalled 0 dashboards. The 2 you edited were left alone.",
                PanelUpdates.ReinstallSummary(0, 2, false, Tally(), "OpenDash Flag box"));
            Assert.True(Tally().Ok);
            Assert.True(Tally(updated: 1, flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate).Ok);
        }

        [Fact]
        public void A_profile_that_could_not_be_written_is_said_with_where_to_look_and_the_step_still_said()
        {
            var line = PanelUpdates.ReinstallSummary(2, 0, false, Tally(updated: 1, notUpdated: 1, flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.Failed), "OpenDash Flag box");
            Assert.Equal("Reinstalled 2 dashboards. Close and reopen the dashboard to see it. Updated 1 LED profile. 1 LED profile could not be updated. OpenDash Flag box could not be updated. See SimHub's log.", line);
            Assert.Equal("Reinstalled 1 dashboard. Restart SimHub to see it. 2 LED profiles could not be updated. 3 LED profiles could not be installed. See SimHub's log.",
                PanelUpdates.ReinstallSummary(1, 0, true, Tally(notUpdated: 2, notInstalled: 3), "OpenDash Flag box"));
            Assert.Equal("Reinstalled 0 dashboards. OpenDash Flag box could not be installed. See SimHub's log.",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(flagBoxBefore: FlagBoxInstallState.NotInstalled, flagBoxAfter: FlagBoxInstallState.Failed), "OpenDash Flag box"));
            // A blank name is the profile's own.
            Assert.Equal("Reinstalled 0 dashboards. Updated " + FlagBoxProfile.ProfileName + ".",
                PanelUpdates.ReinstallSummary(0, 0, false, Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.UpToDate), " "));
            Assert.False(Tally(notUpdated: 1).Ok);
            Assert.False(Tally(notInstalled: 1).Ok);
            Assert.False(Tally(flagBoxBefore: FlagBoxInstallState.Outdated, flagBoxAfter: FlagBoxInstallState.Failed).Ok);
            // The reason is in SimHub's log, and the line says where rather than repeating it (voice.md).
            Assert.Equal("The reinstall did not finish. See SimHub's log.", PanelUpdates.ReinstallFailed);
        }

        /// <summary>Two strips of one shape are two strips: one rewritten and one whose device has gone is
        /// one updated and one that could not be, not two failures.</summary>
        [Fact]
        public void A_run_is_counted_strip_by_strip()
        {
            var tally = new UpdatesLightsTally();
            tally.Strip(FlagBoxInstallState.Outdated, FlagBoxInstallState.UpToDate);
            tally.Strip(FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed);
            Assert.Equal(1, tally.StripsUpdated);
            Assert.Equal(1, tally.StripsNotUpdated);
            Assert.Equal(0, tally.StripsInstalled);
            Assert.Equal(0, tally.StripsNotInstalled);
            Assert.Null(tally.FlagBoxAfter);
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
            Assert.Equal("Installs every dashboard on your rig again, and each older or missing LED and matrix profile. A profile you have edited is replaced.", PanelUpdates.ReinstallTooltip);
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
            Assert.Equal("Put back 1 dashboard. Close and reopen the dashboard to see it.", PanelUpdates.PutBack(1));
            Assert.Equal("Put back 2 dashboards. Close and reopen the dashboard to see it.", PanelUpdates.PutBack(2));
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
            Assert.Equal("Ready (412 cars, fetched 2026-09-28)", PanelUpdates.CarTables("Ready", 412, new DateTime(2026, 9, 28, 7, 0, 0, DateTimeKind.Utc)));
            Assert.Equal("Not fetched (1 car)", PanelUpdates.CarTables("Not fetched", 1, null));
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
                CarTables = "Ready (412 cars)",
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
                "Car tables: Ready (412 cars)",
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
        /// and nothing else it draws is (PanelCopyTests holds the table's pairings to none).</summary>
        [Fact]
        public void The_page_draws_one_primary_and_it_is_Download()
        {
            var code = string.Concat(RepoPaths.SettingsControlSources()
                .Where(p => System.IO.Path.GetFileName(p).StartsWith("SettingsControl.Updates", StringComparison.Ordinal))
                .Select(RepoPaths.Code));
            Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(code, @"PanelButtonKind\.Primary").Count);
            Assert.Contains("updatesDownload = Ui.Button(null, PanelButtonKind.Primary);", code);
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
