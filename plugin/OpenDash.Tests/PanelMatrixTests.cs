// PanelMatrixTests.cs: the Matrix page's words, numbers and decisions, which SettingsControl.Matrix.cs draws
// and no test can build: the rows and their order, which rows show, the profile's line, the cards, the
// preview's chips, the messages after a press, and what search lists. The page itself is held by pinning the
// statements that wire each press and draw each decision, since nothing here can press them.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelMatrixTests
    {
        private static string MatrixSource()
        {
            return RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Matrix.cs"));
        }

        /// <summary>The page's source with every run of whitespace made one space, so a pin can span lines.</summary>
        private static string FlatSource()
        {
            return System.Text.RegularExpressions.Regex.Replace(MatrixSource(), @"\s+", " ");
        }

        /// <summary>
        /// One method's code in the flattened source, from its signature to the next one's: a pin taken inside
        /// it can only be met by that method, where two helpers carry the same statements.
        /// </summary>
        private static string Slice(string flat, string from, string to)
        {
            var start = flat.IndexOf(from, StringComparison.Ordinal);
            Assert.True(start >= 0, from);
            var end = flat.IndexOf(to, start + from.Length, StringComparison.Ordinal);
            Assert.True(end > start, to);
            return flat.Substring(start, end - start);
        }

        [Fact]
        public void The_matrix_rows_say_what_the_artboard_and_voice_md_say()
        {
            Assert.Equal("Matrix", PanelMatrix.Title);
            Assert.Equal("Priority", PanelMatrix.PriorityTitle);
            Assert.Equal("Drag to reorder", PanelMatrix.DragToReorder);
            Assert.Equal("Flags", PanelMatrix.FlagsTitle);
            Assert.Equal("Pit lane", PanelMatrix.PitLaneTitle);
            Assert.Equal("Spotter", PanelMatrix.SpotterTitle);
            Assert.Equal("Warnings", PanelMatrix.WarningsTitle);
            // The words the Alerts rows use for the two temperatures.
            Assert.Equal("Low fuel, oil temperature and water temperature.", PanelMatrix.WarningsCaption);
            Assert.Equal("Triggers", PanelMatrix.ThresholdsLink);
            // voice.md over the artboard: "Idle display" for "At rest", "Mounting side" for "Cars on",
            // "Spotter bar animation" for "Slide in", "Redline flash" for "Flash at redline".
            Assert.Equal("Idle display", PanelMatrix.IdleDisplayTitle);
            Assert.Equal("Mounting side", PanelMatrix.MountingSideTitle);
            Assert.Equal("Spotter bar animation", PanelMatrix.SpotterAnimationTitle);
            Assert.Equal("Every matrix.", PanelMatrix.SpotterAnimationCaption);
            Assert.Equal("Critical flags only", PanelMatrix.CriticalFlagsOnlyTitle);
            Assert.Equal("Shift colours", PanelMatrix.ShiftColoursTitle);
            Assert.Equal("Redline flash", PanelMatrix.RedlineFlashTitle);
            // Shift points, not thresholds, which is the settings model's word; and "Car-specific", the name
            // voice.md gives the car's own tables.
            Assert.Equal("Car-specific shift points", PanelMatrix.CarShiftPointsTitle);
            Assert.DoesNotContain("threshold", PanelMatrix.CarShiftPointsTitle, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Choose a device", PanelMatrix.SimHubDeviceButton);
            // The artboard draws no caption under Pit lane or Critical flags only, and the page adds none.
            var matrix = MatrixSource();
            Assert.Contains("PanelMatrix.PitLaneTitle, null, null,", matrix);
            Assert.Contains("MatrixOption(PanelMatrix.CriticalFlagsOnlyTitle, null,", matrix);
        }

        /// <summary>
        /// One noun for the thing a driver owns (voice.md, "One word per thing"): a matrix, never a panel. The
        /// empty state is the Matrix page's own and Home reads it, as it reads NoScreens and NoStrips.
        /// </summary>
        [Fact]
        public void A_matrix_is_called_a_matrix()
        {
            Assert.Equal("No matrices yet.", PanelMatrix.NoPanels);
            Assert.Equal("Your matrices", PanelMatrix.PanelsTitle);
            Assert.Equal("Add a matrix", PanelMatrix.AddPanel);
            Assert.Equal("All four matrices are in use.", PanelMatrix.AllInUse);
            Assert.Equal("Adds a matrix.", PanelMatrix.AddTooltip);
            Assert.Equal("Renames this matrix.", PanelMatrix.RenameTooltip);
            // What goes with it, as the Remove sheet says it and as every other Remove hover names it
            // ("Removes this screen, its dashboard and its settings.", "Removes this strip, its settings and its profile.").
            Assert.Equal("Removes this matrix and its settings.", PanelMatrix.RemoveTooltip);
            Assert.StartsWith(PanelMatrix.RemoveTooltip.Replace("this matrix", "Left pillar"), PanelMatrix.RemoveCaption("Left pillar"));
            Assert.Equal("Cancel", PanelMatrix.Cancel);
            Assert.Equal("Preview", PanelMatrix.PreviewChipsName);
            // The code's word for the chips never reaches a driver.
            Assert.DoesNotContain("scenario", PanelMatrix.PreviewChipsName, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("No strips yet.", PanelLeds.NoStrips);
            var home = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Home.cs"));
            Assert.Contains("PanelLeds.NoStrips", home);
            Assert.Contains("PanelMatrix.NoPanels", home);
            foreach (var text in new[]
            {
                PanelMatrix.NoPanels, PanelMatrix.PanelsTitle, PanelMatrix.AddPanel, PanelMatrix.AllInUse, PanelMatrix.AddTooltip,
                PanelMatrix.RenameTooltip, PanelMatrix.RemoveTooltip, PanelMatrix.RemoveCaption("Left pillar"),
                PanelMatrix.AddPanelCaption(2, "OpenDash Flag box", FlagBoxInstallState.UpToDate),
                PanelMatrix.AddPanelCaption(2, "OpenDash Flag box", FlagBoxInstallState.NotEmbedded),
                PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", FlagBoxInstallState.NotInstalled),
                PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", FlagBoxInstallState.UpToDate),
                PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", FlagBoxInstallState.NotEmbedded),
                PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", FlagBoxInstallState.Unavailable),
            })
            {
                Assert.DoesNotContain("panel", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("tab", text.Split(' ', '.'), StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// The profile's line is the Matrix page's own table, so the Updates page's LightRow is never held to
        /// it: the name as SimHub lists it and the version SimHub holds, with Update as the page's one primary
        /// press, and no press at all where there is nothing to install or nowhere to put it.
        /// </summary>
        [Fact]
        public void The_profile_line_names_the_profile_its_version_and_the_press()
        {
            var outdated = PanelMatrix.ProfileRow(FlagBoxInstallState.Outdated, "0.4.0");
            Assert.Equal("0.4.0", outdated.State);
            Assert.Equal("Update", outdated.Button);
            Assert.Equal(PanelButton.Primary, outdated.Style);
            // With no matrix on the rig, Home files no update issue, the sidebar draws no dot and Updates leaves the
            // row out: the press is still there, an outline one like Install and Reinstall, never the primary.
            var unused = PanelMatrix.ProfileRow(FlagBoxInstallState.Outdated, "0.4.0", false);
            Assert.Equal("Update", unused.Button);
            Assert.Equal(PanelButton.Outline, unused.Style);
            Assert.False(PanelUpdates.DrawsFlagBoxRow(false));
            var noMatrix = new AttentionInput { FlagBox = FlagBoxInstallState.Outdated };
            Assert.DoesNotContain(PanelAttention.Find(noMatrix), issue => issue.Id == PanelAttention.FlagBoxOutdated);
            var current = PanelMatrix.ProfileRow(FlagBoxInstallState.UpToDate, "0.5.0");
            Assert.Equal("Reinstall", current.Button);
            Assert.Equal(PanelButton.Outline, current.Style);
            Assert.Equal("Installed", PanelMatrix.ProfileRow(FlagBoxInstallState.UpToDate, null).State);
            Assert.Equal("Install failed", PanelMatrix.ProfileRow(FlagBoxInstallState.Failed, null).State);
            Assert.Equal("Install", PanelMatrix.ProfileRow(FlagBoxInstallState.Failed, null).Button);
            Assert.Equal(PanelButton.Outline, PanelMatrix.ProfileRow(FlagBoxInstallState.Failed, null).Style);
            Assert.Equal("Not installed", PanelMatrix.ProfileRow(FlagBoxInstallState.NotInstalled, null).State);
            Assert.Equal("Install", PanelMatrix.ProfileRow(FlagBoxInstallState.NotInstalled, null).Button);
            Assert.Equal(PanelButton.Outline, PanelMatrix.ProfileRow(FlagBoxInstallState.NotInstalled, null).Style);
            // The states the page draws no press for still read as not installed, with Install as the press,
            // should anything read them.
            foreach (var state in new[] { FlagBoxInstallState.NotEmbedded, FlagBoxInstallState.Unavailable })
            {
                Assert.Equal("Not installed", PanelMatrix.ProfileRow(state, null).State);
                Assert.Equal("Install", PanelMatrix.ProfileRow(state, null).Button);
                Assert.Equal(PanelButton.Outline, PanelMatrix.ProfileRow(state, null).Style);
            }
            // Update is the page's one primary press: of the states that offer a press, only Outdated.
            var pressable = new[] { FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed };
            Assert.Equal(new[] { FlagBoxInstallState.Outdated },
                pressable.Where(state => PanelMatrix.ProfileRow(state, "0.4.0").Style == PanelButton.Primary));
            Assert.Equal(PanelMatrix.Update, outdated.Button);
            // Every state carries the dot's ink, which the page draws itself: the line is the name and the version
            // (as Matrix.dc.html draws it), so the dot is what tells an older profile from a current one, in the
            // amber the sidebar's dot, Home and the Updates artboard give an update. The page hands it to Ui.Brush,
            // which throws on a null one, so a state left without an ink would fail the whole page.
            Assert.Equal(Theme.StatusUpdateAvailable, outdated.StateHex);
            Assert.Equal(Theme.StatusUpToDate, current.StateHex);
            Assert.NotEqual(current.StateHex, outdated.StateHex);
            Assert.Equal(Theme.StatusFailed, PanelMatrix.ProfileRow(FlagBoxInstallState.Failed, null).StateHex);
            Assert.Equal(Theme.StatusNotInstalled, PanelMatrix.ProfileRow(FlagBoxInstallState.NotInstalled, null).StateHex);
            Assert.Equal(Theme.StatusNotInstalled, PanelMatrix.ProfileRow(FlagBoxInstallState.Unavailable, null).StateHex);
            Assert.Equal(Theme.StatusNotInstalled, PanelMatrix.ProfileRow(FlagBoxInstallState.NotEmbedded, null).StateHex);
            foreach (FlagBoxInstallState state in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                Assert.Matches("^#[0-9A-F]{6}$", PanelMatrix.ProfileRow(state, "0.4.0").StateHex);
                Assert.Matches("^#[0-9A-F]{6}$", PanelMatrix.ProfileRow(state, null).StateHex);
            }
            // The page's own ink, never another page's table, which moved under the dot twice.
            Assert.Contains("var action = PanelMatrix.ProfileRow(state, version, Settings.MatrixPanels().Any()); var dot = new Ellipse { Width = PanelMatrix.ProfileDotSize, Height = PanelMatrix.ProfileDotSize, Fill = Ui.Brush(action.StateHex),", FlatSource());
            Assert.DoesNotContain("DotHex", MatrixSource());
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(MatrixSource(), @"PanelMatrix\.ProfileRow\("));

            // The state a plan reads as, the by-hand import, and the failure the page keeps.
            Assert.Equal(FlagBoxInstallState.NotEmbedded, PanelMatrix.StateOf(null));
            Assert.Equal(FlagBoxInstallState.Outdated, PanelMatrix.StateOf(new FlagBoxPlan { State = FlagBoxInstallState.Outdated }));
            foreach (FlagBoxInstallState state in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                Assert.Equal(state == FlagBoxInstallState.Unavailable, PanelMatrix.ShowsImportFallback(state));
                Assert.Equal(state == FlagBoxInstallState.Failed, PanelMatrix.KeepsPressResult(state));
            }

            Assert.Equal("OpenDash Flag box · 0.5.0", PanelMatrix.ProfileLine("OpenDash Flag box", FlagBoxInstallState.UpToDate, "0.5.0"));
            Assert.Equal("OpenDash Flag box · Not installed", PanelMatrix.ProfileLine("OpenDash Flag box", FlagBoxInstallState.NotInstalled, null));
            Assert.Equal("OpenDash Flag box · Install failed", PanelMatrix.ProfileLine("OpenDash Flag box", FlagBoxInstallState.Failed, null));
            Assert.Equal("OpenDash Flag box", PanelMatrix.ProfileLine("OpenDash Flag box", FlagBoxInstallState.Unavailable, null));
            Assert.Equal("This build ships no flag box profile.", PanelMatrix.ProfileLine("OpenDash Flag box", FlagBoxInstallState.NotEmbedded, null));

            Assert.False(PanelMatrix.ProfileHasButton(FlagBoxInstallState.NotEmbedded));
            Assert.False(PanelMatrix.ProfileHasButton(FlagBoxInstallState.Unavailable));
            foreach (var state in new[] { FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed })
            {
                Assert.True(PanelMatrix.ProfileHasButton(state));
            }

            // One message in the order the driver acts on it: the verb (one per press, voice.md's "One word per
            // thing"; the name unquoted), the installer's note, then after a first install the select step in
            // StepsLeft's form.
            var one = new List<int> { 2 };
            var several = new List<int> { 1, 3 };
            var none = new List<int>();
            foreach (var before in new[] { FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Failed })
            {
                var first = PanelMatrix.InstallSaid(before, before, FlagBoxInstallState.UpToDate, "OpenDash Flag box", null, one);
                Assert.Equal("Installed OpenDash Flag box. Select \"OpenDash Flag box\" on your matrix's device in SimHub and set RGB Matrix content to 2.", first.Text);
                Assert.Equal(PanelTone.Info, first.Tone);
            }
            Assert.Equal("Installed OpenDash Flag box. Select \"OpenDash Flag box\" on each matrix's device in SimHub and set RGB Matrix content to the matrix's number.",
                PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "OpenDash Flag box", null, several).Text);
            // A rig with no matrix has no device to name.
            Assert.Equal("Installed OpenDash Flag box.", PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "OpenDash Flag box", null, none).Text);
            Assert.Equal("Installed OpenDash Flag box.", PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "OpenDash Flag box", null, null).Text);
            // The built-in profiles note comes before the select step it blocks, in caution, in one message.
            var noted = PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "OpenDash Flag box", FlagBoxInstallPlan.BuiltInModeNote, one);
            Assert.Equal("Installed OpenDash Flag box. Turn off built-in profiles on your device, or OpenDash's profiles will not be listed. "
                + "Select \"OpenDash Flag box\" on your matrix's device in SimHub and set RGB Matrix content to 2.", noted.Text);
            Assert.Equal(PanelTone.Caution, noted.Tone);
            // The installer's note is said as it reads, without the space round it.
            Assert.Equal(noted.Text, PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "OpenDash Flag box", "  " + FlagBoxInstallPlan.BuiltInModeNote + " ", one).Text);
            // A first install that SimHub reads back as older (FlagBoxInstallPlan.Decide reads Outdated whenever
            // the versions differ) still installed it: the same message as a current one, never a failure.
            var readOlder = PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Outdated, "OpenDash Flag box", null, one);
            Assert.Equal(PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "OpenDash Flag box", null, one).Text, readOlder.Text);
            Assert.StartsWith("Installed OpenDash Flag box. ", readOlder.Text);
            Assert.Equal(PanelTone.Info, readOlder.Tone);
            var updated = PanelMatrix.InstallSaid(FlagBoxInstallState.Outdated, FlagBoxInstallState.Outdated, FlagBoxInstallState.UpToDate, "OpenDash Flag box", null, one);
            Assert.Equal("Updated OpenDash Flag box.", updated.Text);
            Assert.Equal(PanelTone.Info, updated.Tone);
            var reinstalled = PanelMatrix.InstallSaid(FlagBoxInstallState.UpToDate, FlagBoxInstallState.UpToDate, FlagBoxInstallState.UpToDate, "OpenDash Flag box", null, one);
            Assert.Equal("Reinstalled OpenDash Flag box.", reinstalled.Text);
            Assert.Equal(PanelTone.Info, reinstalled.Tone);
            var reinstalledNoted = PanelMatrix.InstallSaid(FlagBoxInstallState.UpToDate, FlagBoxInstallState.UpToDate, FlagBoxInstallState.UpToDate, "OpenDash Flag box", FlagBoxInstallPlan.BuiltInModeNote, one);
            Assert.Equal("Reinstalled OpenDash Flag box. " + FlagBoxInstallPlan.BuiltInModeNote, reinstalledNoted.Text);
            Assert.Equal(PanelTone.Caution, reinstalledNoted.Tone);
            // A failure in the form the other pages give theirs.
            Assert.Equal(PanelTone.Danger, PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Failed, "OpenDash Flag box", null, one).Tone);
            // A failure is said in the verb of the press that was made: the line's state when it was pressed,
            // never the state SimHub held before an earlier failure.
            Assert.Equal("Could not update OpenDash Flag box. See SimHub's log.", PanelMatrix.InstallSaid(FlagBoxInstallState.Outdated, FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed, "OpenDash Flag box", null, one).Text);
            Assert.Equal("Could not reinstall OpenDash Flag box. See SimHub's log.", PanelMatrix.InstallSaid(FlagBoxInstallState.UpToDate, FlagBoxInstallState.UpToDate, FlagBoxInstallState.Failed, "OpenDash Flag box", null, one).Text);
            Assert.Equal("Could not install OpenDash Flag box. See SimHub's log.", PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Failed, "OpenDash Flag box", null, one).Text);
            // The Install offered after a failed Update is an Install, though the copy it was made over was older.
            Assert.Equal("Could not install OpenDash Flag box. See SimHub's log.", PanelMatrix.InstallSaid(FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed, FlagBoxInstallState.Failed, "OpenDash Flag box", null, one).Text);
            // Out of reach, in the words the by-hand import uses for that state and StepsLeft for the step.
            var unreachable = PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Unavailable, "OpenDash Flag box", null, one);
            Assert.Equal("SimHub's matrix settings are not available. Import OpenDash Flag box by hand from the top of this page.", unreachable.Text);
            Assert.Equal(PanelTone.Caution, unreachable.Tone);
            Assert.StartsWith("Import OpenDash Flag box by hand from the top of this page,", PanelMatrix.AddPanelCaption(2, "OpenDash Flag box", FlagBoxInstallState.Unavailable));
            Assert.StartsWith(PanelMatrix.Unreachable, FlagBoxInstallPlan.Summary(new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, null));
            Assert.Null(PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotEmbedded, "x", null, one));

            // The hover on the title's line: one sentence, and none where the line or the import says it all.
            Assert.Null(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.NotEmbedded, null, "OpenDash Flag box", one));
            Assert.Null(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Unavailable, "0.5.0", "OpenDash Flag box", one));
            // Not installed: the line says it, and the press's tooltip says what Install does.
            Assert.Null(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.NotInstalled, "0.5.0", "OpenDash Flag box", one));
            // Up to date: the dot and the version say it; the hover gives the select step the line cannot
            // show, in the one form the message after a first install gives it: for the one matrix with its
            // number, for each of several, and none on a rig with no matrix to name.
            Assert.Equal("Select \"OpenDash Flag box\" on your matrix's device in SimHub and set RGB Matrix content to 2.",
                PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.UpToDate, "0.5.0", "OpenDash Flag box", one));
            Assert.Equal("Select \"OpenDash Flag box\" on each matrix's device in SimHub and set RGB Matrix content to the matrix's number.",
                PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.UpToDate, "0.5.0", "OpenDash Flag box", several));
            Assert.Null(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.UpToDate, "0.5.0", "OpenDash Flag box", none));
            Assert.Null(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.UpToDate, "0.5.0", "OpenDash Flag box", null));
            foreach (var rig in new[] { one, several, none })
            {
                var installed = PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "OpenDash Flag box", null, rig).Text;
                var hover = PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.UpToDate, "0.5.0", "OpenDash Flag box", rig);
                Assert.Equal(PanelMatrix.SelectStep("OpenDash Flag box", rig), hover);
                Assert.Equal("Installed OpenDash Flag box." + (hover == null ? string.Empty : " " + hover), installed);
            }
            // Outdated and failed, in the Updates page's words for the same profile in the same state: the line
            // already shows the version SimHub has, and already says the install failed.
            Assert.Equal("Update brings it to 0.5.0.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Outdated, "0.5.0", "OpenDash Flag box", one));
            Assert.Equal("Update brings it to 0.5.0.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Outdated, " 0.5.0 ", "OpenDash Flag box", one));
            // FlagBoxInstallPlan.Decide reads Outdated whenever the versions differ, one of them missing included.
            Assert.Equal("Update brings it up to date.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Outdated, null, "OpenDash Flag box", one));
            Assert.Equal("Update brings it up to date.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Outdated, " ", "OpenDash Flag box", one));
            Assert.Equal("See SimHub's log.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Failed, "0.5.0", "OpenDash Flag box", one));
            Assert.Equal("See SimHub's log.", PanelMatrix.SeeLog);
            // The Updates page's flag box row, state by state: older and failed alike; a current profile and a
            // missing one hovered apart, as the summary says, and nowhere claimed to match.
            foreach (FlagBoxInstallState state in new[] { FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed })
            {
                Assert.Equal(PanelUpdates.FlagBoxTooltip(new FlagBoxPlan { State = state, EmbeddedVersion = "0.5.0" }, null),
                    PanelMatrix.ProfileLineTooltip(state, "0.5.0", "OpenDash Flag box", one));
            }
            Assert.Null(PanelUpdates.FlagBoxTooltip(new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, EmbeddedVersion = "0.5.0" }, null));
            Assert.NotNull(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.UpToDate, "0.5.0", "OpenDash Flag box", one));
            Assert.NotNull(PanelUpdates.FlagBoxTooltip(new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled, EmbeddedVersion = "0.5.0" }, null));
            Assert.Null(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.NotInstalled, "0.5.0", "OpenDash Flag box", one));
            foreach (FlagBoxInstallState state in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                var hover = PanelMatrix.ProfileLineTooltip(state, "0.5.0", "OpenDash Flag box", one);
                if (hover == null) continue;
                Assert.DoesNotContain(FlagBoxInstallPlan.Replaces, hover);
                Assert.Single(System.Text.RegularExpressions.Regex.Matches(hover, @"\.(\s|$)"));
            }

            // The press's tooltip: Install installs, Update and Reinstall replace; and after a failed press, the
            // Install it offers is warned as the state the failed press was made in, since SimHub may still
            // hold the copy a press then replaces.
            Assert.Equal("Installs the flag box profile in SimHub.", PanelMatrix.InstallTooltip);
            // One verb per press: the hover says the press's own, and "Adds" stays the add tile's.
            Assert.StartsWith("Installs ", PanelMatrix.InstallTooltip);
            Assert.StartsWith("Adds ", PanelMatrix.AddTooltip);
            foreach (FlagBoxInstallState from in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                Assert.Equal(PanelMatrix.InstallTooltip, PanelMatrix.ProfileTooltip(FlagBoxInstallState.NotInstalled, from));
                Assert.Equal(FlagBoxInstallPlan.Replaces, PanelMatrix.ProfileTooltip(FlagBoxInstallState.Outdated, from));
                Assert.Equal(FlagBoxInstallPlan.Replaces, PanelMatrix.ProfileTooltip(FlagBoxInstallState.UpToDate, from));
            }
            Assert.Equal(PanelMatrix.InstallTooltip, PanelMatrix.ProfileTooltip(FlagBoxInstallState.Failed, FlagBoxInstallState.NotInstalled));
            Assert.Equal(FlagBoxInstallPlan.Replaces, PanelMatrix.ProfileTooltip(FlagBoxInstallState.Failed, FlagBoxInstallState.UpToDate));
            Assert.Equal(FlagBoxInstallPlan.Replaces, PanelMatrix.ProfileTooltip(FlagBoxInstallState.Failed, FlagBoxInstallState.Outdated));
            // The state a press is made in carries through a second failure.
            Assert.Equal(FlagBoxInstallState.Outdated, PanelMatrix.PressedFrom(FlagBoxInstallState.Outdated, FlagBoxInstallState.NotInstalled));
            Assert.Equal(FlagBoxInstallState.UpToDate, PanelMatrix.PressedFrom(FlagBoxInstallState.Failed, FlagBoxInstallState.UpToDate));

            var matrix = MatrixSource();
            var flat = FlatSource();
            Assert.Contains("PanelMatrix.ProfileRow(", matrix);
            Assert.Contains("var primary = action.Style == PanelButton.Primary;", matrix);
            Assert.Contains("primary ? PanelButtonKind.Primary : PanelButtonKind.Ghost", matrix);
            Assert.Contains("button.ToolTip = PanelMatrix.ProfileTooltip(state, matrixFailedFrom);", matrix);
            // The by-hand import is the shell's hook, which Matrix and Updates both draw, and the page reaches
            // none of its internals: the fields it fills and the press that copies are the shell's.
            Assert.Contains("PanelMatrix.ShowsImportFallback(PanelMatrix.StateOf(plan)) ? BuildFlagBoxImportFallback(plan) : null", matrix);
            foreach (var internals in new[] { "flagBoxLine", "flagBoxPath", "CopyFlagBoxForImport", "BuildMatrixImportFallback" })
            {
                Assert.DoesNotContain(internals, matrix);
            }
            // The press installs, keeps a failure for the redraw it starts, redraws and says what it did.
            Assert.Contains("var from = PanelMatrix.PressedFrom(state, matrixFailedFrom); "
                + "var result = InstallFlagBox(); "
                + "matrixFailed = PanelMatrix.KeepsPressResult(result.State) ? result : null; "
                + "matrixFailedAsked = null; "
                + "matrixFailedFrom = from; "
                + "Redraw(); "
                + "var said = PanelMatrix.InstallSaid(from, state, result.State, FlagBoxName(), result.Note, Settings.MatrixPanels().ToList()); "
                + "if (said != null) Say(said); };", flat);
            // A repeat of the one press is ignored: the redraw puts the next state's press where it was, with the
            // keyboard's focus, so a double-click's second press or a held Enter would install a second time.
            foreach (var guard in new[]
            {
                "button.PreviewMouseLeftButtonDown += (sender, args) => { if (args.ClickCount > 1) args.Handled = true; };",
                "button.PreviewKeyDown += (sender, args) => { if (args.Key == Key.Enter && args.IsRepeat) args.Handled = true; };",
            })
            {
                Assert.Single(System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape(guard)));
            }
            // A repeat of the click that opened a sheet, or of a sheet's press that closed it and drew the page
            // again under the pointer, is the shell's to drop on the control's root (SettingsControl.Sheet.cs,
            // PanelShellTests, #523), so the page no longer guards its sheet's presses or its root itself.
            foreach (var press in new[] { "add", "save", "remove" })
            {
                Assert.Contains("var " + press + " = Ui.Button(", flat);
                Assert.DoesNotContain(press + ".PreviewMouseLeftButtonDown", flat);
            }
            Assert.DoesNotContain("matrixSheetPressed", flat);
            Assert.DoesNotContain("MatrixIgnoreRepeat", flat);
            // The note is said inside that one message, never as a line of its own.
            Assert.DoesNotContain("Say(PanelMessage.Info(result.Note))", matrix);
            // SimHub is asked again whenever the shell asks what needs fixing (Go, Redraw, Check again and the
            // return to the panel all replace `issues`), and not on a rebuild in place, which keeps the list.
            Assert.Contains("if (matrixPlan == null || !ReferenceEquals(matrixPlanAsked, issues)) { matrixPlan = SafePlan(); matrixPlanAsked = issues; }", flat);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(matrix, @"SafePlan\(\)"));
            Assert.Contains("var plan = MatrixPlan();", matrix);
            // A failed press's result outlives its own redraw and rebuilds in place, and nothing else.
            Assert.Contains("if (matrixFailed != null) { if (matrixFailedAsked == null) matrixFailedAsked = issues; "
                + "if (ReferenceEquals(matrixFailedAsked, issues)) return matrixFailed; matrixFailed = null; }", flat);
            Assert.Contains("OnLeave(\"Matrix.plan\", () => { matrixPlan = null; matrixFailed = null; });", flat);
            // The page's presses clear nothing by hand: their Redraw asks again.
            Assert.DoesNotContain("matrixPlan = null; Redraw();", flat);
            // The shell's return to the panel is what asks again: CatchUp replaces the list the plan is kept by.
            var shell = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.cs"));
            Assert.Contains("askedManually = false; } RefreshAttention(); RebuildPage(); RefreshSidebar(); }", System.Text.RegularExpressions.Regex.Replace(shell, @"\s+", " "));
            var status = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Status.cs"));
            Assert.Contains("issues = PanelAttention.Find(attentionFacts);", status);
            // The plan is kept by the list's identity, so each ask has to hand back a list of its own: a shared
            // empty list for a rig with nothing to fix would keep a stale plan through an Install's Redraw, and
            // the line would say Not installed under a message that says Installed. Held with nothing to fix,
            // with an issue to fix, and for RefreshAttention's catch.
            Assert.NotSame(PanelAttention.Find(new AttentionInput()), PanelAttention.Find(new AttentionInput()));
            Assert.NotSame(PanelAttention.Find(null), PanelAttention.Find(null));
            Func<AttentionInput> withIssue = () => new AttentionInput
            {
                Matrices = new List<AttentionMatrix> { new AttentionMatrix { Slot = 1, Name = "Matrix 1" } },
                FlagBox = FlagBoxInstallState.Outdated,
                FlagBoxName = "OpenDash Flag box",
            };
            Assert.NotEmpty(PanelAttention.Find(withIssue()));
            Assert.NotSame(PanelAttention.Find(withIssue()), PanelAttention.Find(withIssue()));
            Assert.Contains("attentionFacts = new AttentionInput(); issues = new List<PanelIssue>(); }", System.Text.RegularExpressions.Regex.Replace(status, @"\s+", " "));
            Assert.DoesNotContain("PanelCopy.LightRow(", matrix);
            // The line as drawn: the state in the dot's ink (ProfileRow's StateHex, the dot's alone), the name as
            // SimHub lists it, the press only where there is one to offer, the message after it, and the hover.
            foreach (var pin in new[]
            {
                "slot == 0 ? null : BuildMatrixSelected(slot, selectedCard));",
                "Fill = Ui.Brush(action.StateHex),",
                "Width = PanelMatrix.ProfileDotSize, Height = PanelMatrix.ProfileDotSize,",
                "var line = Ui.Text(PanelMatrix.ProfileLine(FlagBoxName(), state, version), Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);",
                "var row = Ui.HStack(PanelMatrix.ProfileGap, dot, line); if (PanelMatrix.ProfileHasButton(state)) {",
                "if (plugin.FlagBoxJson == null) return null;",
                "var hover = PanelMatrix.ProfileLineTooltip(state, plan == null ? null : plan.EmbeddedVersion, FlagBoxName(), Settings.MatrixPanels().ToList()); if (hover != null) row.ToolTip = hover;",
                // The state and the version the line is drawn in are both the plan's, read as the line is built.
                "private FrameworkElement BuildMatrixProfile(FlagBoxPlan plan) { var state = PanelMatrix.StateOf(plan); var version = plan == null ? null : plan.InstalledVersion;",
                // The line sits on the right of the title's line.
                "row.HorizontalAlignment = HorizontalAlignment.Right; return row; }",
            })
            {
                Assert.Contains(pin, flat);
            }
            // StateHex is the dot's alone: the words stay text.secondary in every state.
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(matrix, @"StateHex"));
            // A page that asks SimHub while it is built repaints its lighting in place.
            Assert.DoesNotContain("DrawsLighting()", matrix);
            Assert.Contains("OnLighting(() => Ui.Redim(preview,", matrix);
            // Both pictures are dimmed again at the matrix's own dim, the large 8x8 as well as each card's.
            Assert.Contains("OnLighting(() => Ui.Redim(preview, MatrixDim()));", FlatSource());
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(FlatSource(), System.Text.RegularExpressions.Regex.Escape("OnLighting(() => Ui.Redim(")).Count);
            // The page reads none of the old tab's matrix copy: it has its own.
            Assert.DoesNotContain("PanelLights.", matrix);
            Assert.DoesNotContain("PanelLights.", RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "PanelMatrix.cs")));
        }

        [Fact]
        public void A_card_says_which_content_it_is_and_never_claims_what_nothing_read()
        {
            Assert.Equal("Matrix 2", PanelMatrix.Slot(2));
            Assert.Equal("Matrix 3", PanelMatrix.DefaultName(3));
            Assert.Equal("Matrix 3", PanelMatrix.NameOf(null, 3));
            Assert.Equal("Matrix 3", PanelMatrix.NameOf("  ", 3));
            Assert.Equal("Left pillar", PanelMatrix.NameOf("Left pillar", 3));
            // Mounting side reads as the artboard and the Rig page's spotter chips read: Left | Right | Both
            // sides, each label paired with its value, and every value the contract's.
            Assert.Equal(new[] { "left", "right", "both" }, PanelMatrix.SideValues);
            Assert.Equal(new[] { "Left", "Right", "Both sides" }, PanelMatrix.SideLabels);
            Assert.Equal(PanelMatrix.SideValues.Length, PanelMatrix.SideLabels.Length);
            Assert.Equal(Contract.FlagBoxSides.OrderBy(v => v), PanelMatrix.SideValues.OrderBy(v => v));
            Assert.Equal("Left", PanelMatrix.SideLabel("left"));
            Assert.Equal("Right", PanelMatrix.SideLabel("right"));
            Assert.Equal("Both sides", PanelMatrix.SideLabel("both"));
            Assert.Equal("Both sides", PanelMatrix.SideLabel(null));
            Assert.Equal(new[] { "Dark", "Gear" }, PanelMatrix.RestLabels);
            Assert.Equal(Contract.FlagBoxRests.Length, PanelMatrix.RestLabels.Length);

            Assert.Equal("Matrix 1 · Both sides", PanelMatrix.CardLine("Left pillar", 1, "both", null));
            Assert.Equal("Matrix 2 · Left", PanelMatrix.CardLine("Left pillar", 2, "left", null));
            Assert.Equal("Matrix 2 · Both sides", PanelMatrix.CardLine("Left pillar", 2, "nonsense", null));
            // A card under its default name does not say its number twice.
            Assert.Equal("Left", PanelMatrix.CardLine(PanelMatrix.DefaultName(2), 2, "left", null));
            Assert.Equal("Both sides", PanelMatrix.CardLine("Matrix 3", 3, "both", null));
            Assert.Equal("Matrix 3 · Both sides", PanelMatrix.CardLine("Matrix 2", 3, "both", null));
            Assert.Equal("Showing", PanelMatrix.CardLine("Left pillar", 1, "both", true));
            Assert.Equal("Not shown in SimHub", PanelMatrix.CardLine("Left pillar", 2, "left", false));
            Assert.Equal(Theme.Caution, PanelMatrix.CardLineHex(false));
            Assert.Equal(Theme.TextSecondary, PanelMatrix.CardLineHex(true));
            Assert.Equal(Theme.TextSecondary, PanelMatrix.CardLineHex(null));

            // The add tile's count, and the fourth matrix is the last.
            Assert.Equal("2 / 4", PanelMatrix.AddCount(2));
            Assert.True(PanelMatrix.CanAdd(3));
            Assert.False(PanelMatrix.CanAdd(4));
            Assert.Equal(4, PanelMatrix.MaxPanels);

            // The grid is the artboard's three columns, 12 apart.
            Assert.Equal(3, PanelMatrix.CardColumns);
            Assert.Equal(12, PanelMatrix.CardGap);
            Assert.Equal(208, PanelMatrix.CardMinWidth);
            var matrix = MatrixSource();
            Assert.Contains("Ui.MatrixCard(picture,", matrix);
            Assert.Contains("MatrixStyle.Card", matrix);
            Assert.Contains("Ui.InlineAddCard(PanelMatrix.AddPanel, PanelKit.MatrixAddIcon,", matrix);
            Assert.Contains("Ui.CardGrid(PanelMatrix.CardMinWidth, PanelMatrix.CardGap, PanelMatrix.CardColumns,", matrix);
        }

        /// <summary>Every press on the page, wired to what it says it does: nothing here can press one.</summary>
        [Fact]
        public void Every_press_on_the_page_does_what_it_says()
        {
            Assert.True(PanelMatrix.AddEnabled(3, 4));
            // An empty rig, whose free content is the first, and a rig whose first content is the free one: the
            // tile is the only way to add a matrix.
            Assert.True(PanelMatrix.AddEnabled(0, 1));
            Assert.True(PanelMatrix.AddEnabled(2, 1));
            Assert.False(PanelMatrix.AddEnabled(4, 1));
            Assert.False(PanelMatrix.AddEnabled(2, 0));
            // Driven from the settings the page reads it from: open on a new rig, closed at four.
            var rig = new OpenDashSettings();
            rig.Normalise();
            Assert.True(PanelMatrix.AddEnabled(rig.MatrixPanels().Count(), rig.FreeMatrixSlot()));
            for (var k = 1; k <= PanelMatrix.MaxPanels; k++)
            {
                Assert.True(PanelMatrix.AddEnabled(rig.MatrixPanels().Count(), rig.FreeMatrixSlot()));
                Assert.NotEqual(0, rig.AddMatrixPanel("M" + k));
            }
            Assert.False(PanelMatrix.AddEnabled(rig.MatrixPanels().Count(), rig.FreeMatrixSlot()));
            // Removing the first frees the first content, and the tile opens again.
            rig.RemoveMatrixPanel(1);
            Assert.Equal(1, rig.FreeMatrixSlot());
            Assert.True(PanelMatrix.AddEnabled(rig.MatrixPanels().Count(), rig.FreeMatrixSlot()));
            var matrix = MatrixSource();
            var flat = FlatSource();
            foreach (var pin in new[]
            {
                // The cards: a click selects its matrix and draws the page again; the tile adds.
                "Select(PanelPage.Matrix, PanelMatrix.SlotId(m)); RebuildPage();",
                "Ui.InlineAddCard(PanelMatrix.AddPanel, PanelKit.MatrixAddIcon, ShowAddMatrix, PanelMatrix.AddCount(panels.Count));",
                "if (PanelMatrix.AddEnabled(panels.Count, Settings.FreeMatrixSlot()))",
                "add.IsEnabled = false; add.ToolTip = PanelMatrix.AllInUse;",
                "return Ui.VStack(PanelMatrix.EmptyGap, Ui.Caption(PanelMatrix.NoPanels), grid);",
                // The selected matrix's presses.
                "rename.Click += (sender, args) => ShowRenameMatrix(m);",
                "check.Click += (sender, args) => CheckAgain();",
                // The add sheet.
                "var name = Ui.Input(PanelMatrix.DefaultName(slot), PanelMatrix.NameWidth);",
                "var added = Settings.AddMatrixPanel(name.Text);",
                "Ui.Caption(PanelMatrix.AddPanelCaption(slot, FlagBoxName(), PanelMatrix.StateOf(MatrixPlan()))),",
                "var state = PanelMatrix.StateOf(MatrixPlan());",
                "PanelMatrix.PanelAdded(PanelMatrix.NameOf(Settings.MatrixName(added), added), added, FlagBoxName(), state), PanelMatrix.NeedsInstall(state) ? PanelTone.Caution : PanelTone.Info",
                "ShowSheet(PanelMatrix.AddPanel, body, SheetFooter(null, cancel, add));",
                // The rename and remove sheets.
                "Settings.RenameMatrixPanel(matrix, name.Text); Save(); Redraw();",
                "Settings.RemoveMatrixPanel(matrix); Save(); Select(PanelPage.Matrix, null); Redraw(); Say(PanelMatrix.Removed(name, matrix));",
                "cancel.Click += (sender, args) => CloseSheet();",
                // The links.
                "thresholds.Click += (sender, args) => Go(PanelMatrix.ThresholdsRoute);",
                "all.Click += (sender, args) => Open(PanelPage.Rig, PanelMatrix.PreviewScenario(matrixPreviewScenario, PanelMatrix.OptionsFor(Settings, m)));",
                // Which idle-display rows show, and the switches that change which do.
                "if (PanelMatrix.ShowsGearRows(rest))",
                "if (PanelMatrix.ShowsCarShiftPoints(rest, bands))",
                "if (PanelMatrix.ShowsRedlineFlash(rest, bands))",
                "Settings.FlagBoxMatrixGearBands[i] = on; Save(); RebuildPage();",
                "Settings.SetMatrixRest(m, value); Save(); RebuildPage();",
                "Settings.FlagBoxSide[i] = value; Save(); RebuildPage();",
                // The segmented controls, each with its own labels.
                "BuildSegmented(PanelMatrix.SideValues, PanelMatrix.SideLabels, Settings.MatrixSide(m), value =>",
                "BuildSegmented(Contract.FlagBoxRests, PanelMatrix.RestLabels, rest, value =>",
                // The chips, each pressed only when it is the one drawn, and a press moves the preview to it.
                // The add press saves at once and selects the new matrix; each sheet draws its body.
                // The cards: which matrix the page opens on, a card never claiming what nothing read, and the
                // empty state only on an empty rig.
                "var device = MatrixLayer(Ui.Soon( MatrixLayerHead(null, PanelSoon.SimHubDevice.Title, null, null, Ui.Button(PanelMatrix.SimHubDeviceButton, PanelButtonKind.Outline, PanelButtonSize.Small)), PanelSoon.SimHubDevice));",
                "foreach (var id in offered) { var chosen = id; var chip = Ui.Chip(PanelMatrix.PreviewLabel(chosen), chosen == scenario, () => { matrixPreviewScenario = chosen; repaint(); drawChips(); });",
                "var offered = PanelMatrix.PreviewChips(PanelMatrix.OptionsFor(Settings, m)); var scenario = PanelMatrix.PreviewScenario(matrixPreviewScenario, PanelMatrix.OptionsFor(Settings, m));",
                "var added = Settings.AddMatrixPanel(name.Text); Save(); if (added != 0) Select(PanelPage.Matrix, PanelMatrix.SlotId(added)); Redraw(); if (added == 0) return;",
                "var body = Ui.VStack(PanelMatrix.SheetGap, Ui.Caption(PanelMatrix.AddPanelCaption(slot, FlagBoxName(), PanelMatrix.StateOf(MatrixPlan()))), Ui.SettingRow(PanelMatrix.NameTitle, name, PanelMatrix.NameCaption)); ShowSheet(PanelMatrix.AddPanel, body, SheetFooter(null, cancel, add));",
                "ShowSheet(PanelMatrix.RenameTitle(current), Ui.SettingRow(PanelMatrix.NameTitle, name, PanelMatrix.NameCaption), SheetFooter(null, cancel, save));",
                "ShowSheet(PanelMatrix.RemoveTitle(name), Ui.Caption(PanelMatrix.RemoveCaption(name)), SheetFooter(null, cancel, remove));",
                "var said = PanelMatrix.RenameSaid(before, PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix)); if (said != null) Say(said);",
                "var shown = facts == null ? null : facts.Shown; var name = PanelMatrix.NameOf(Settings.MatrixName(m), m);",
                // Each card and the fix box read their own matrix's facts, and the rename compares the stored name
                // before the press with the one after it, never the typed one.
                "var facts = MatrixFacts(m); var shown = facts == null ? null : facts.Shown;",
                "var facts = MatrixFacts(m); if (facts != null && facts.Shown == false)",
                "save.Click += (sender, args) => { var before = PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix); Settings.RenameMatrixPanel(matrix, name.Text);",
                // The add tile's hover while it adds.
                "if (PanelMatrix.AddEnabled(panels.Count, Settings.FreeMatrixSlot())) { add.ToolTip = PanelMatrix.AddTooltip; } else { add.IsEnabled = false;",
                "OnLighting(() => Ui.Redim(picture, MatrixDim()));",
                "var card = Ui.MatrixCard(picture, name, PanelMatrix.CardLine(name, m, Settings.MatrixSide(m), shown), PanelMatrix.CardLineHex(shown), m == selected, () => { Select(PanelPage.Matrix, PanelMatrix.SlotId(m)); RebuildPage(); }); card.ToolTip = name; AutomationProperties.SetName(card, PanelMatrix.CardName(name, m, Settings.MatrixSide(m), shown)); cards.Add(card);",
                "var slot = PanelMatrix.SelectedSlot(panels, Selected(PanelPage.Matrix));",
                "if (panels.Count > 0) return grid; return Ui.VStack(PanelMatrix.EmptyGap, Ui.Caption(PanelMatrix.NoPanels), grid);",
            })
            {
                Assert.Contains(pin, flat);
            }
            // Every sheet's Cancel closes it.
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("cancel.Click += (sender, args) => CloseSheet();")).Count);
            // A double-click on Cancel: the first press closes the sheet through CloseSheet, which marks the sheet
            // moved, and the shell drops the second press on the control's root (SettingsControl.Sheet.cs, #523),
            // so it never reaches a control the sheet hid. Cancel has no other handler and no guard of its own,
            // and closes the sheet no other way.
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("cancel.Click +=")).Count);
            Assert.DoesNotContain("cancel.Preview", flat);
            var sheetFlat = System.Text.RegularExpressions.Regex.Replace(
                RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Sheet.cs")), @"\s+", " ");
            Assert.Contains("private void CloseSheet() { CloseSheet(restoreFocus: true); }", sheetFlat);
            Assert.Contains("sheetLayer.Visibility = Visibility.Collapsed; sheetPanel.Child = null; SheetMoved();", sheetFlat);
            Assert.Contains("if (PanelShell.SheetSwallowsPress(sheetMoved, args.ClickCount)) { args.Handled = true; return; }", sheetFlat);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(matrix, @"\}, PanelKit\.SegmentedHeightMatrix\);").Count);
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(matrix, @"Ui\.Button\(PanelMatrix\.Cancel, PanelButtonKind\.Ghost, PanelButtonSize\.Large\)").Count);
        }

        /// <summary>
        /// Every part the page builds reaches the screen: the pins above hold what each control reads, writes
        /// and says where it is made, and these hold that it is then added to what is drawn -- each helper's
        /// adds, the page's parts and the selected matrix's two columns, the title's press, the chips and the
        /// repaint -- so a dropped add, a press wired to the wrong event or a picture never repainted fails
        /// here rather than compiling to a page with a hole in it.
        /// </summary>
        [Fact]
        public void Every_part_the_page_builds_is_put_on_screen()
        {
            var flat = FlatSource();
            foreach (var pin in new[]
            {
                // The page: every matrix's card, the selected one handed its picture, and the add tile.
                "var page = PageLayout(PanelMatrix.Title,",
                // The page's order: the profile on the title's line, the by-hand import under the title, the
                // cards, then the selected matrix.
                "var page = PageLayout(PanelMatrix.Title, Ui.Anchor(BuildMatrixProfile(plan), PanelMatrix.AnchorProfile), "
                    + "PanelMatrix.ShowsImportFallback(PanelMatrix.StateOf(plan)) ? BuildFlagBoxImportFallback(plan) : null, "
                    + "Ui.Anchor(cards, PanelMatrix.AnchorPanels), slot == 0 ? null : BuildMatrixSelected(slot, selectedCard)); "
                    + "return page; }",
                "var panels = Settings.MatrixPanels().ToList();",
                "var cards = BuildMatrixCards(panels, slot, picture => selectedCard = picture);",
                "if (m == selected) selectedPicture(picture);",
                "cards.Add(add); var grid = new GroupBorder { Child = Ui.CardGrid(PanelMatrix.CardMinWidth, PanelMatrix.CardGap, PanelMatrix.CardColumns, cards.ToArray()) };",
                "ToolTipService.SetShowOnDisabled(add, true);",
                "AutomationProperties.SetName(grid, PanelMatrix.PanelsTitle);",
                // The title's line: the version SimHub holds, the press for the line's own state, clicked, and
                // put on the line.
                "var version = plan == null ? null : plan.InstalledVersion;",
                "if (PanelMatrix.ProfileHasButton(state)) { var primary = action.Style == PanelButton.Primary; "
                    + "var button = Ui.Button(action.Button, primary ? PanelButtonKind.Primary : PanelButtonKind.Ghost, PanelButtonSize.Small);",
                "button.Click += (sender, args) => { var from = PanelMatrix.PressedFrom(state, matrixFailedFrom); var result = InstallFlagBox();",
                "if (said != null) Say(said); }; row.Children.Add(button); }",
                // The selected matrix: its name as the heading, its number beside it, the fix box when it is
                // drawn, and the preview beside the priority list, or over it.
                "var title = Ui.SubHeading(name);",
                "heading.Children.Add(title);",
                "heading.Children.Add(slot); }",
                // The number beside the name is the slot caption, the gap the name's, so a number that wraps
                // under a long name starts at the column's edge.
                "title.Margin = new Thickness(0, 0, PanelMatrix.HeaderGap, 0); var slot = Ui.Text(slotCaption, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary); "
                    + "slot.VerticalAlignment = VerticalAlignment.Bottom; slot.Margin = new Thickness(0, 0, 0, PanelMatrix.SlotCaptionLift); heading.Children.Add(slot); }",
                "var parts = new List<UIElement> { head };",
                "var check = Ui.Button(PanelAttention.CheckAgain, PanelButtonKind.Outline, PanelButtonSize.Small);",
                "parts.Add(fix); }",
                "var previewColumn = BuildMatrixPreview(m, cardPicture); var priority = BuildMatrixPriority(m, previewColumn.Repaint);",
                "Grid.SetColumn(previewColumn.Element, 0); Grid.SetColumn(priority, 2); body.Children.Add(previewColumn.Element); body.Children.Add(priority); parts.Add(body);",
                "parts.Add(Ui.VStack(PanelMatrix.StackedGap, previewColumn.Element, priority));",
                // The chips: cleared and drawn again on every press, each added, drawn once as the column is
                // built, and the keyboard's focus kept on the chip it was on.
                "chips.Children.Clear();",
                "{ if (chips.Children[i].IsKeyboardFocusWithin) focused = i; }",
                "chips.Children.Add(chip); }",
                "if (focused >= 0 && focused < chips.Children.Count) chips.Children[focused].Focus();",
                "drawChips(); var column = Ui.VStack(PanelMatrix.PreviewGap, tagged, all, chipGroup);",
                "return new MatrixPreviewColumn(Ui.Anchor(column, PanelMatrix.AnchorPreview), repaint);",
                // The repaint: the fresh lamps into the picture already on the page, the card's included.
                "var fresh = Ui.Matrix(cells, style, MatrixDim()); var lamps = fresh.Child; fresh.Child = null; picture.Child = lamps;",
                "if (cardPicture != null) MatrixRepaint(cardPicture,",
                // The repaint reads the settings of the matrix it is drawn for.
                "Action repaint = () => { var options = PanelMatrix.OptionsFor(Settings, m); MatrixRepaint(preview, PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), MatrixDrawnOptions(m)), MatrixStyle.Preview);",
                "return PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness);",
                // The priority list's own words and greyed rows.
                "Ui.Text(PanelMatrix.DragToReorder, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary)",
                // The greyed caption carries the artboard's SOON tag after its words.
                "Ui.Text(PanelMatrix.DragToReorder, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary), Ui.SoonTag(PanelSoon.PriorityOrder));",
                "var head = Ui.Row(Ui.Heading(PanelMatrix.PriorityTitle), Ui.Soon(reorder, PanelSoon.PriorityOrder));",
                "idle.Add(Ui.Soon(MatrixOption(PanelSoon.RpmColourForEverything.Title, null, Ui.Switch(true, null)), PanelSoon.RpmColourForEverything)); var idleLayer",
                // Each helper adds what it is given: a layer its rows, a layer's line its name, number or dot,
                // words, link and control, an option its words, caption, line and control.
                "if (row != null) stack.Children.Add(row);",
                "titleLine.Children.Add(name); var words = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center }; words.Children.Add(titleLine);",
                "line.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0); words.Children.Add(line); }",
                "grid.Children.Add(cell); }",
                // Idle display is unranked, drawn with the dot; a ranked layer with its number; the device row
                // with neither.
                "if (rank == string.Empty) { var dot = new Ellipse {",
                "else if (rank != null) { var number =",
                "grid.Children.Add(number); }",
                "Grid.SetColumn(words, 1); grid.Children.Add(words);",
                "Grid.SetColumn(link, 2); grid.Children.Add(link); }",
                "Grid.SetColumn(control, 3); grid.Children.Add(control); }",
                "under.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0); words.Children.Add(under); }",
                "if (line != null) { line.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0); words.Children.Add(line); }",
                "grid.Children.Add(words); if (control != null)",
                "Grid.SetColumn(control, 1); grid.Children.Add(control); }",
                // The sheets: the content number SimHub will give the new matrix, the presses by their words,
                // the message after adding, and the rename box opening on the name.
                "var slot = Settings.FreeMatrixSlot(); if (slot == 0) return;",
                "var add = Ui.Button(PanelMatrix.AddPanel, PanelButtonKind.Primary, PanelButtonSize.Large); add.Click += (sender, args) =>",
                "Say(new PanelMessage( PanelMatrix.PanelAdded(",
                "var name = Ui.Input(current, PanelMatrix.NameWidth); var save = Ui.Button(PanelMatrix.Rename, PanelButtonKind.Primary, PanelButtonSize.Large);",
                "save.Click += (sender, args) =>",
                "remove.Click += (sender, args) => { Settings.RemoveMatrixPanel(matrix);",
            })
            {
                Assert.Contains(pin, flat);
            }
            // The guards and loops that decide whether a part is added, each with what it adds: a guard flipped
            // or a loop that skips its first item compiles to a page with a hole in it, or one that throws.
            foreach (var pin in new[]
            {
                "foreach (var matrix in panels) { var m = matrix; var facts = MatrixFacts(m);",
                "foreach (var row in rows) { if (row != null) stack.Children.Add(row); }",
                "if (link != null) { link.VerticalAlignment = VerticalAlignment.Center; link.Margin = new Thickness(PanelMatrix.LayerGap, 0, PanelMatrix.ThresholdsGap, 0); Grid.SetColumn(link, 2); grid.Children.Add(link); }",
                "if (control != null) { if (control is ToggleButton) AutomationProperties.SetName(control, title); control.VerticalAlignment = VerticalAlignment.Center; control.HorizontalAlignment = HorizontalAlignment.Right; control.Margin = new Thickness(PanelMatrix.LayerGap, 0, 0, 0); Grid.SetColumn(control, 3); grid.Children.Add(control); }",
                "if (control != null) { if (control is ToggleButton) AutomationProperties.SetName(control, title); control.VerticalAlignment = VerticalAlignment.Center; control.HorizontalAlignment = HorizontalAlignment.Right; control.Margin = new Thickness(PanelMatrix.OptionGap, 0, 0, 0); Grid.SetColumn(control, 1); grid.Children.Add(control); }",
                // The stacked layout is the two-column one's else: both would add the preview to two parents.
                "parts.Add(body); } else { previewColumn.Element.MaxWidth = PanelMatrix.PreviewColumnWidth;",
                // The plan the page draws is the one asked for, and the column hands back what it was built with.
                "if (matrixPlan == null || !ReferenceEquals(matrixPlanAsked, issues)) { matrixPlan = SafePlan(); matrixPlanAsked = issues; } return matrixPlan; }",
                "public MatrixPreviewColumn(FrameworkElement element, Action repaint) { Element = element; Repaint = repaint; }",
                // The selected matrix's heading, the Rename sheet and the Remove sheet each read their own matrix's
                // name, pinned with the method that reads it so the card's identical line cannot meet it.
                "private FrameworkElement BuildMatrixSelected(int matrix, Border cardPicture) { var m = matrix; var name = PanelMatrix.NameOf(Settings.MatrixName(m), m); var title = Ui.SubHeading(name);",
                "private void ShowRenameMatrix(int matrix) { var current = PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix); var name = Ui.Input(current, PanelMatrix.NameWidth);",
                "private void ShowRemoveMatrix(int matrix) { var name = PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix); var remove = Ui.Button(PanelMatrix.RemoveConfirm,",
            })
            {
                Assert.Contains(pin, flat);
            }
            // Each helper's own words, taken inside its own code, so neither can meet the other's pin: its name,
            // wrapping, on the title line, the line in its words, and its caption under it only when it has one,
            // wrapping, 2 below.
            var head = Slice(flat, "private static Border MatrixLayerHead(", "private static Border MatrixOption(");
            var option = Slice(flat, "private static Border MatrixOption(", "private void ShowAddMatrix(");
            Assert.Contains("var name = Ui.Text(title, PanelShell.RowTitleSize, FontWeights.Medium, Theme.TextPrimary); name.TextWrapping = TextWrapping.Wrap; titleLine.Children.Add(name); "
                + "var words = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center }; words.Children.Add(titleLine); "
                + "if (!string.IsNullOrEmpty(caption)) { var line = Ui.Text(caption, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.TextSecondary); "
                + "line.TextWrapping = TextWrapping.Wrap; line.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0); words.Children.Add(line); }", head);
            Assert.Contains("var name = Ui.Text(title, PanelMatrix.OptionTextSize, FontWeights.Normal, Theme.TextPrimary); name.TextWrapping = TextWrapping.Wrap; titleLine.Children.Add(name); "
                + "var words = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center }; words.Children.Add(titleLine); "
                + "if (!string.IsNullOrEmpty(caption)) { var under = Ui.Text(caption, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.TextSecondary); "
                + "under.TextWrapping = TextWrapping.Wrap; under.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0); words.Children.Add(under); } "
                + "if (line != null) { line.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0); words.Children.Add(line); }", option);
            foreach (var helper in new[] { head, option })
            {
                Assert.Contains("var titleLine = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };", helper);
                Assert.Contains("Child = grid, Tag = new RowParts(titleLine, control), };", helper);
            }
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("words.Children.Add(titleLine);")).Count);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("name.TextWrapping = TextWrapping.Wrap;")).Count);
            // Both helpers carry their parts, so Ui.Soon appends the SOON tag after a greyed row's name (#363,
            // #371).
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("Child = grid, Tag = new RowParts(titleLine, control), };")).Count);
            // Each helper's two adds of a caption line: the layer's and the option's, and the option's own line.
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("titleLine.Children.Add(name);")).Count);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("words.Children.Add(line);")).Count);
            // Nothing on the page reads the matrices with a limit or drops the add tile.
            Assert.DoesNotContain(".Take(", flat);
            Assert.DoesNotContain("GC.KeepAlive", flat);
        }

        [Fact]
        public void The_page_opens_on_the_selected_matrix_while_the_rig_still_has_it()
        {
            Assert.Equal(0, PanelMatrix.SelectedSlot(new List<int>(), "1"));
            Assert.Equal(0, PanelMatrix.SelectedSlot(null, null));
            Assert.Equal(1, PanelMatrix.SelectedSlot(new List<int> { 1, 3 }, null));
            Assert.Equal(3, PanelMatrix.SelectedSlot(new List<int> { 1, 3 }, "3"));
            Assert.Equal(1, PanelMatrix.SelectedSlot(new List<int> { 1, 3 }, "2"));
            Assert.Equal(1, PanelMatrix.SelectedSlot(new List<int> { 1, 3 }, "pillar"));
            Assert.Equal("3", PanelMatrix.SlotId(3));
        }

        [Fact]
        public void The_selected_matrix_says_its_number_once_and_asks_once_before_it_goes()
        {
            Assert.Equal("Matrix 2", PanelMatrix.SlotCaption("Left pillar", 2));
            Assert.Null(PanelMatrix.SlotCaption("Matrix 2", 2));
            // A name that only looks like the number in another case is a name: the number is still said.
            Assert.Equal("Matrix 2", PanelMatrix.SlotCaption("matrix 2", 2));
            Assert.Equal("Rename", PanelMatrix.Rename);
            Assert.Equal("Remove", PanelMatrix.Remove);
            Assert.Equal("Rename Left pillar", PanelMatrix.RenameTitle("Left pillar"));
            Assert.Equal("Remove Left pillar", PanelMatrix.RemoveTitle("Left pillar"));
            // The sheet's body names the matrix, since its title trims a long name and the body wraps.
            Assert.Equal("Removes Left pillar and its settings. The flag box profile stays in SimHub.", PanelMatrix.RemoveCaption("Left pillar"));
            Assert.Equal("Remove it", PanelMatrix.RemoveConfirm);
            // SimHub's field, set to the number, in the form the rest of the page gives it.
            Assert.Equal("Removed Left pillar. A device with RGB Matrix content set to 2 stays dark.", PanelMatrix.Removed("Left pillar", 2));
            Assert.Equal("Renamed to Pillar.", PanelMatrix.Renamed("Pillar"));
            // A blank name is ignored by the settings, so the press waits for one and says nothing unchanged.
            Assert.False(PanelMatrix.CanRename(""));
            Assert.False(PanelMatrix.CanRename("  "));
            Assert.False(PanelMatrix.CanRename(null));
            Assert.True(PanelMatrix.CanRename("Pillar"));
            Assert.Equal("Renamed to Pillar.", PanelMatrix.RenameSaid("Left pillar", "Pillar"));
            Assert.Null(PanelMatrix.RenameSaid("Left pillar", "Left pillar"));
            // A change of case is a rename, and is said.
            Assert.Equal("Renamed to left pillar.", PanelMatrix.RenameSaid("Left pillar", "left pillar"));
            Assert.Equal("Name", PanelMatrix.NameTitle);
            // The list named as docs/flag-box.md and the installer's log name it, and as the LEDs row names its own
            // (PanelLights.BarNameCaption, "SimHub's LED profile list"): the title line names a profile SimHub does
            // list, so the bare "profile list" would leave the driver to work out which.
            Assert.Equal("Not shown in SimHub's matrix profile list.", PanelMatrix.NameCaption);
            Assert.Contains("SimHub's matrix profile list", PanelMatrix.NameCaption);
            // Remove is a sheet with one press; Rename is a sheet; neither acts on the first press.
            var matrix = MatrixSource();
            Assert.Contains("ShowSheet(PanelMatrix.RemoveTitle(name),", matrix);
            Assert.Contains("ShowSheet(PanelMatrix.RenameTitle(current),", matrix);
            Assert.Contains("remove.Click += (sender, args) => ShowRemoveMatrix(m);", matrix);
            Assert.Contains("Ui.Button(PanelMatrix.RemoveConfirm, PanelButtonKind.Danger, PanelButtonSize.Large)", matrix);
            Assert.Contains("name.TextChanged += (sender, args) => save.IsEnabled = PanelMatrix.CanRename(name.Text);", matrix);
            Assert.Contains("var said = PanelMatrix.RenameSaid(before, PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix));", matrix);
        }

        [Fact]
        public void Adding_says_the_steps_left_on_the_device()
        {
            // The sheet says, before the press, what the message says after it: the profile first when SimHub
            // has no copy, then the select step, in one form.
            var steps = new Dictionary<FlagBoxInstallState, string>
            {
                { FlagBoxInstallState.UpToDate, "Select \"OpenDash Flag box\" on your matrix's device in SimHub and set RGB Matrix content to 2." },
                { FlagBoxInstallState.Outdated, "Select \"OpenDash Flag box\" on your matrix's device in SimHub and set RGB Matrix content to 2." },
                { FlagBoxInstallState.NotInstalled, "Install OpenDash Flag box at the top of this page, then select it on your matrix's device in SimHub and set RGB Matrix content to 2." },
                { FlagBoxInstallState.Failed, "Install OpenDash Flag box at the top of this page, then select it on your matrix's device in SimHub and set RGB Matrix content to 2." },
                { FlagBoxInstallState.Unavailable, "Import OpenDash Flag box by hand from the top of this page, then select it on your matrix's device in SimHub and set RGB Matrix content to 2." },
                { FlagBoxInstallState.NotEmbedded, "This build ships no flag box profile." },
            };
            foreach (var step in steps)
            {
                // The content number is in the steps and the name box opens on it: the caption does not add it.
                Assert.Equal(step.Value, PanelMatrix.AddPanelCaption(2, "OpenDash Flag box", step.Key));
                Assert.Equal("Added Left pillar. " + step.Value, PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", step.Key));
            }
            Assert.Equal(Enum.GetValues(typeof(FlagBoxInstallState)).Length, steps.Count);
            Assert.Equal("your matrix's device in SimHub", PanelMatrix.YourDevice);
            Assert.Equal("RGB Matrix content", PanelMatrix.ContentField);
            Assert.Equal("each matrix's device in SimHub", PanelMatrix.EachDevice);
            // The select step after a first install ends as the add steps do, on the one form.
            Assert.EndsWith("on " + PanelMatrix.YourDevice + " and set RGB Matrix content to 2.",
                PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "P", null, new List<int> { 2 }).Text);
            // The same select step, in the same words, before the press that adds a matrix and after the one that
            // installs the profile: the profile quoted as the entry to pick in SimHub's list.
            Assert.Equal(PanelMatrix.SelectStep("P", new List<int> { 2 }), PanelMatrix.AddPanelCaption(2, "P", FlagBoxInstallState.UpToDate));
            Assert.EndsWith(PanelMatrix.SelectStep("P", new List<int> { 2 }),
                PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "P", FlagBoxInstallPlan.BuiltInModeNote, new List<int> { 2 }).Text);
            Assert.StartsWith("Select \"P\" on ", PanelMatrix.SelectStep("P", new List<int> { 1, 3 }));
            Assert.Null(PanelMatrix.SelectStep("P", new List<int>()));
            Assert.False(PanelMatrix.NeedsInstall(FlagBoxInstallState.UpToDate));
            Assert.False(PanelMatrix.NeedsInstall(FlagBoxInstallState.Outdated));
            foreach (var state in new[] { FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Failed, FlagBoxInstallState.Unavailable, FlagBoxInstallState.NotEmbedded })
            {
                Assert.True(PanelMatrix.NeedsInstall(state));
            }
            Assert.Equal("Matrix 4", PanelMatrix.DefaultName(4));
        }

        [Fact]
        public void The_fix_box_names_the_device_the_profile_and_the_content()
        {
            // Titled by the card's state (#524, ruling 8); the matrix number is in the Content step.
            Assert.Equal("Not shown in SimHub", PanelMatrix.FixTitle);
            Assert.Equal(PanelMatrix.NotShown, PanelMatrix.FixTitle);
            Assert.Equal(PanelMatrix.NotShown, PanelMatrix.CardLine("Left pillar", 2, "left", false));
            var steps = PanelMatrix.FixSteps(2, "OpenDash Flag box");
            Assert.Equal(3, steps.Count);
            Assert.Equal(new[] { "Devices", "your matrix" }, steps[0]);
            Assert.Equal(new[] { "Profile: OpenDash Flag box" }, steps[1]);
            Assert.Equal(new[] { "RGB Matrix content: 2" }, steps[2]);
            // Drawn only when something read that no device shows it, never on a guess.
            var matrix = MatrixSource();
            Assert.Contains("facts != null && facts.Shown == false", matrix);
            Assert.Contains("PanelKit.FixPaddingYLights", matrix);
        }

        /// <summary>The priority list: the flag box's own fixed order, numbered, with the order greyed #505.</summary>
        [Fact]
        public void The_priority_list_is_numbered_in_the_flag_boxs_order()
        {
            Assert.Equal(new[] { "Flags", "Pit lane", "Spotter", "Warnings" }, PanelMatrix.Layers);
            Assert.Equal("1", PanelMatrix.Rank(PanelMatrix.FlagsTitle));
            Assert.Equal("2", PanelMatrix.Rank(PanelMatrix.PitLaneTitle));
            Assert.Equal("3", PanelMatrix.Rank(PanelMatrix.SpotterTitle));
            Assert.Equal("4", PanelMatrix.Rank(PanelMatrix.WarningsTitle));
            Assert.Equal(string.Empty, PanelMatrix.Rank(PanelMatrix.IdleDisplayTitle));
            Assert.Equal(new PanelRoute(PanelPage.Settings, "settings.alerts"), PanelMatrix.ThresholdsRoute);
            // The link is the plural of the column it lands on, so the two pages say one word for the thing
            // (#524, ruling 7): the Settings column renamed renames the link, and a link reworded here fails.
            Assert.Equal(PanelSettings.ThresholdColumn + "s", PanelMatrix.ThresholdsLink);
            Assert.Equal(new[] { 363, 371, 505 }, PanelMatrix.SoonDrawn.Select(item => item.Ticket).OrderBy(t => t));
            var matrix = MatrixSource();
            Assert.Contains("Ui.Soon(reorder, PanelSoon.PriorityOrder)", matrix);
            // The list is drawn in the order Layers holds, each numbered by Rank, then the idle display and the device.
            var flat = FlatSource();
            Assert.Contains("return Ui.Anchor(Ui.VStack(0, head, Ui.Anchor(flags, PanelMatrix.AnchorFlags), Ui.Anchor(pit, PanelMatrix.AnchorPitLane), "
                + "Ui.Anchor(spotter, PanelMatrix.AnchorSpotter), Ui.Anchor(warnings, PanelMatrix.AnchorWarnings), "
                + "Ui.Anchor(idleLayer, PanelMatrix.AnchorIdleDisplay), device), PanelMatrix.AnchorPriority);", flat);
            foreach (var layer in new[] { "flags", "pit", "spotter", "warnings" })
            {
                var title = new Dictionary<string, string> { { "flags", "FlagsTitle" }, { "pit", "PitLaneTitle" }, { "spotter", "SpotterTitle" }, { "warnings", "WarningsTitle" } }[layer];
                Assert.Contains("var " + layer + " = MatrixLayer( MatrixLayerHead(PanelMatrix.Rank(PanelMatrix." + title + "), PanelMatrix." + title + ",", flat);
            }
            Assert.Contains("MatrixLayerHead(null, PanelSoon.SimHubDevice.Title,", flat);
            // Every write goes through at once; the rig-wide animation is the only one not indexed per matrix,
            // and the oil and water thresholds are Settings' only.
            foreach (var write in new[]
            {
                "Settings.FlagBoxFlags[i] = on; Save();", "Settings.FlagBoxMatrixCriticalOnly[i] = on; Save();",
                "Settings.FlagBoxPit[i] = on; Save();", "Settings.FlagBoxSpotter[i] = on; Save();",
                "Settings.FlagBoxSide[i] = value;", "Settings.FlagBoxSpotterAnimation = on; Save();",
                "Settings.FlagBoxWarnings[i] = on; Save();", "Settings.SetMatrixRest(m, value);",
                "Settings.FlagBoxMatrixGearBands[i] = on; Save();", "Settings.FlagBoxMatrixGearCarLadder[i] = on; Save();",
                "Settings.FlagBoxMatrixGearBlink[i] = on; Save();",
            })
            {
                Assert.Contains(write, matrix);
            }
            Assert.DoesNotContain("OilTemp", matrix);
            Assert.DoesNotContain("WaterTemp", matrix);
            Assert.Contains("PanelKit.SegmentedHeightMatrix", matrix);
            Assert.Contains("PanelKit.ChipPaddingXLights", matrix);
            // Two columns only when the shell says they fit.
            Assert.Contains("if (TwoColumns)", matrix);
            Assert.DoesNotContain("!Narrow", matrix);
        }

        /// <summary>
        /// Every control on the priority list, from its row's title through the setting it shows and the one its
        /// lambda writes, each as one statement: a row that showed or wrote another row's setting, lost its
        /// control, its caption or its link, or a layer that dropped an option would each fail here.
        /// </summary>
        [Fact]
        public void Every_control_is_drawn_with_the_setting_it_reads_and_writes()
        {
            var flat = FlatSource();
            // Every indexed write lands on the matrix whose switch it is: the one index, taken from the slot the
            // list is drawn for, and nothing else named i.
            Assert.Contains("private FrameworkElement BuildMatrixPriority(int matrix, Action repaint) { var m = matrix; var i = PanelMatrix.Index(m);", flat);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(flat, @"var i = ").Count);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(flat, @"for \(var i = 0; i < chips\.Children\.Count; i\+\+\)"));
            foreach (var pin in new[]
            {
                "var flags = MatrixLayer( MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.FlagsTitle), PanelMatrix.FlagsTitle, null, null, BuildToggle(Settings.MatrixFlags(m), on => { Settings.FlagBoxFlags[i] = on; Save(); repaint(); })), MatrixOption(PanelMatrix.CriticalFlagsOnlyTitle, null, BuildToggle(Settings.MatrixCriticalOnly(m), on => { Settings.FlagBoxMatrixCriticalOnly[i] = on; Save(); repaint(); })));",
                "var pit = MatrixLayer( MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.PitLaneTitle), PanelMatrix.PitLaneTitle, null, null, BuildToggle(Settings.MatrixPit(m), on => { Settings.FlagBoxPit[i] = on; Save(); repaint(); })));",
                "var side = BuildSegmented(PanelMatrix.SideValues, PanelMatrix.SideLabels, Settings.MatrixSide(m), value => { Settings.FlagBoxSide[i] = value; Save(); RebuildPage(); }, PanelKit.SegmentedHeightMatrix);",
                "var spotter = MatrixLayer( MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.SpotterTitle), PanelMatrix.SpotterTitle, null, null, BuildToggle(Settings.MatrixSpotter(m), on => { Settings.FlagBoxSpotter[i] = on; Save(); repaint(); })), MatrixOption(PanelMatrix.MountingSideTitle, null, side), Ui.Anchor(MatrixOption(PanelMatrix.SpotterAnimationTitle, PanelMatrix.SpotterAnimationCaption, BuildToggle(Settings.FlagBoxSpotterAnimation, on => { Settings.FlagBoxSpotterAnimation = on; Save(); })), PanelMatrix.AnchorSpotterAnimation));",
                "var warnings = MatrixLayer( MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.WarningsTitle), PanelMatrix.WarningsTitle, PanelMatrix.WarningsCaption, thresholds, BuildToggle(Settings.MatrixWarnings(m), on => { Settings.FlagBoxWarnings[i] = on; Save(); repaint(); })));",
                "var rest = Settings.MatrixRest(m); var bands = Settings.MatrixGearBands(m); var restChoice = BuildSegmented(Contract.FlagBoxRests, PanelMatrix.RestLabels, rest, value => { Settings.SetMatrixRest(m, value); Save(); RebuildPage(); }, PanelKit.SegmentedHeightMatrix);",
                "var idle = new List<UIElement> { MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.IdleDisplayTitle), PanelMatrix.IdleDisplayTitle, null, null, restChoice) };",
                "if (PanelMatrix.ShowsGearRows(rest)) { idle.Add(MatrixOption(PanelMatrix.ShiftColoursTitle, null, BuildToggle(bands, on => { Settings.FlagBoxMatrixGearBands[i] = on; Save(); RebuildPage(); }))); }",
                "if (PanelMatrix.ShowsCarShiftPoints(rest, bands)) {",
                "idle.Add(MatrixOption(PanelMatrix.CarShiftPointsTitle, null, BuildToggle(Settings.MatrixGearCarLadder(m), on => { Settings.FlagBoxMatrixGearCarLadder[i] = on; Save(); readCar(); }), carLine));",
                "if (PanelMatrix.ShowsRedlineFlash(rest, bands)) { idle.Add(MatrixOption(PanelMatrix.RedlineFlashTitle, null, BuildToggle(Settings.MatrixGearBlink(m), on => { Settings.FlagBoxMatrixGearBlink[i] = on; Save(); }))); }",
                "var idleLayer = MatrixLayer(idle.ToArray());",
            })
            {
                Assert.Contains(pin, flat);
            }
            // Each toggle writes exactly one setting, and each setting is written by exactly one toggle.
            foreach (var write in new[]
            {
                "Settings.FlagBoxFlags[i] = on;", "Settings.FlagBoxMatrixCriticalOnly[i] = on;", "Settings.FlagBoxPit[i] = on;",
                "Settings.FlagBoxSpotter[i] = on;", "Settings.FlagBoxSpotterAnimation = on;", "Settings.FlagBoxWarnings[i] = on;",
                "Settings.FlagBoxMatrixGearBands[i] = on;", "Settings.FlagBoxMatrixGearCarLadder[i] = on;", "Settings.FlagBoxMatrixGearBlink[i] = on;",
            })
            {
                Assert.Single(System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape(write)));
            }
            Assert.Equal(9, System.Text.RegularExpressions.Regex.Matches(flat, @"BuildToggle\(").Count);
        }

        /// <summary>
        /// The index every switch writes at is the entry Settings reads the same matrix from: a write at
        /// Index(n) moves matrix n and no other, for each of the four.
        /// </summary>
        [Fact]
        public void A_matrix_writes_the_entry_its_own_settings_are_read_from()
        {
            Assert.Equal(0, PanelMatrix.Index(1));
            Assert.Equal(3, PanelMatrix.Index(PanelMatrix.MaxPanels));
            for (var n = 1; n <= PanelMatrix.MaxPanels; n++)
            {
                var settings = new OpenDashSettings();
                settings.Normalise();
                for (var k = 1; k <= PanelMatrix.MaxPanels; k++) settings.AddMatrixPanel("M" + k);
                var before = Enumerable.Range(1, PanelMatrix.MaxPanels).Select(k => settings.MatrixFlags(k)).ToArray();
                var i = PanelMatrix.Index(n);
                settings.FlagBoxFlags[i] = !settings.MatrixFlags(n);
                settings.FlagBoxSide[i] = settings.MatrixSide(n) == "left" ? "right" : "left";
                for (var k = 1; k <= PanelMatrix.MaxPanels; k++)
                {
                    Assert.Equal(k == n ? !before[k - 1] : before[k - 1], settings.MatrixFlags(k));
                }
                Assert.NotEqual(Contract.DefaultFlagBoxSide, settings.MatrixSide(n));
            }
        }

        [Fact]
        public void The_idle_displays_options_show_only_where_they_act()
        {
            Assert.False(PanelMatrix.ShowsGearRows("dark"));
            Assert.True(PanelMatrix.ShowsGearRows("gear"));
            // The contract's value, as Settings stores it, and nothing else.
            Assert.False(PanelMatrix.ShowsGearRows("Gear"));
            Assert.False(PanelMatrix.ShowsGearRows(null));
            Assert.False(PanelMatrix.ShowsCarShiftPoints("dark", true));
            Assert.False(PanelMatrix.ShowsCarShiftPoints("gear", false));
            Assert.True(PanelMatrix.ShowsCarShiftPoints("gear", true));
            // The flash is the last shift colour, the car's own included, so it shows with the car's points on.
            Assert.True(PanelMatrix.ShowsRedlineFlash("gear", true));
            Assert.False(PanelMatrix.ShowsRedlineFlash("gear", false));
            Assert.False(PanelMatrix.ShowsRedlineFlash("dark", true));
        }

        /// <summary>The car line and the tables' tests are the LEDs page's, called rather than copied (#522): the
        /// two lines agree in every case but the missing tables', which names the page the download is on.</summary>
        [Fact]
        public void The_car_line_is_the_LEDs_pages_own()
        {
            var bools = new[] { false, true };
            foreach (var car in new[] { null, " ", "Porsche 911 GT3 R (992)" })
                foreach (var on in bools) foreach (var hasTable in bools) foreach (var loaded in bools) foreach (var missing in bools) foreach (var covers in bools)
                {
                    var leds = PanelLeds.CarLine(on, car, hasTable, loaded, missing, covers);
                    var matrix = PanelMatrix.CarLine(on, car, hasTable, loaded, missing, covers);
                    Assert.Equal(leds == PanelLeds.CarTablesMissing ? PanelMatrix.CarTablesMissing : leds, matrix);
                }
            foreach (var status in new[] { null, "not loaded", PanelLights.CarTablesNone, "  " + PanelLights.CarTablesNone })
                foreach (var count in new[] { 0, 12 })
                    Assert.Equal(PanelLeds.TablesMissing(count, status), PanelMatrix.TablesMissing(count, status));
            Assert.Equal(PanelLeds.TablesGame, PanelMatrix.TablesGame);
            foreach (var game in new[] { null, "iRacing", " IRacing ", "AssettoCorsa" }) Assert.Equal(PanelLeds.TablesCoverGame(game), PanelMatrix.TablesCoverGame(game));
            var code = System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "PanelMatrix.cs")), @"\s+", " ");
            Assert.Contains("var line = PanelLeds.CarLine(on, car, hasTable, tablesLoaded, tablesMissing, tablesCoverGame);", code);
            Assert.Contains("return PanelLeds.TablesMissing(carCount, status);", code);
        }

        /// <summary>The line under Car-specific shift points says what LEDs says (PanelLeds.CarLine), and only
        /// while the switch is on and a car is loaded in the game the tables cover: with no tables ever
        /// downloaded it says so and where the download is, rather than that the car is missing from them; while
        /// tables on disk are still being read, or could not be read, in any other game, or with no car, it says
        /// nothing.</summary>
        [Fact]
        public void The_car_line_follows_its_switch_and_says_whether_the_tables_have_the_car()
        {
            const string car = "Porsche 911 GT3 R (992)";
            Assert.Equal("Porsche 911 GT3 R (992) is in Lovely Car Data.", PanelMatrix.CarLine(true, car, true, true, false, true));
            Assert.Equal("Porsche 911 GT3 R (992) is not in Lovely Car Data.", PanelMatrix.CarLine(true, car, false, true, false, true));
            // A fresh install: a new matrix has the switch on, and the tables arrive only from the LEDs page,
            // named by that page's own constants.
            Assert.Equal("Lovely Car Data is not downloaded yet. Download it on the LEDs page, under Every strip.", PanelMatrix.CarTablesMissing);
            Assert.Contains(PanelLeds.Title + " page", PanelMatrix.CarTablesMissing);
            Assert.Contains("under " + PanelLeds.EveryStripTitle + ".", PanelMatrix.CarTablesMissing);
            Assert.Equal(PanelMatrix.CarTablesMissing, PanelMatrix.CarLine(true, car, false, false, true, true));
            // Tables on disk still being read at the start, or a copy that could not be read: nothing, since the
            // LEDs page's Lovely Car Data row says Loading or Could not read, and "not downloaded" would be false.
            Assert.Null(PanelMatrix.CarLine(true, car, false, false, false, true));
            Assert.True(PanelMatrix.TablesMissing(0, PanelLights.CarTablesNone));
            Assert.True(PanelMatrix.TablesMissing(0, PanelLights.CarTablesNone + " " + PanelLights.CarTablesFailed("timed out")));
            Assert.True(PanelMatrix.TablesMissing(0, CarLightService.Describe(0, null, DateTime.UtcNow, null)));
            Assert.False(PanelMatrix.TablesMissing(0, "not loaded"));
            Assert.False(PanelMatrix.TablesMissing(0, CarLightService.Unreadable));
            Assert.False(PanelMatrix.TablesMissing(0, null));
            Assert.False(PanelMatrix.TablesMissing(0, PanelLights.CarTablesNone.ToUpperInvariant()));
            Assert.True(PanelMatrix.TablesMissing(0, "  " + PanelLights.CarTablesNone));
            Assert.False(PanelMatrix.TablesMissing(12, PanelLights.CarTablesNone));
            // No car loaded, or a game whose tables the plugin does not read: nothing, tables or not.
            Assert.Null(PanelMatrix.CarLine(true, null, false, false, true, true));
            Assert.Null(PanelMatrix.CarLine(true, null, false, true, false, true));
            Assert.Null(PanelMatrix.CarLine(true, " ", true, true, false, true));
            // SimHub's padding round a car's name is not drawn.
            Assert.Equal("Porsche 911 GT3 R (992) is in Lovely Car Data.", PanelMatrix.CarLine(true, "  " + car + " ", true, true, false, true));
            Assert.Null(PanelMatrix.CarLine(true, "Ferrari 296 GT3", false, true, false, false));
            Assert.Null(PanelMatrix.CarLine(true, "Ferrari 296 GT3", false, false, true, false));
            Assert.Null(PanelMatrix.CarLine(true, car, true, true, false, false));
            // The switch off: nothing.
            Assert.Null(PanelMatrix.CarLine(false, car, true, true, false, true));
            Assert.Null(PanelMatrix.CarLine(false, car, false, true, false, true));
            Assert.Null(PanelMatrix.CarLine(false, car, false, false, true, true));
            // The game the tables cover, as SimHub names it or codes it.
            Assert.True(PanelMatrix.TablesCoverGame("iRacing"));
            Assert.True(PanelMatrix.TablesCoverGame("IRacing"));
            Assert.True(PanelMatrix.TablesCoverGame(" iRacing "));
            Assert.False(PanelMatrix.TablesCoverGame("Assetto Corsa Competizione"));
            Assert.False(PanelMatrix.TablesCoverGame("LMU"));
            Assert.False(PanelMatrix.TablesCoverGame(null));
            Assert.False(PanelMatrix.TablesCoverGame(string.Empty));
            Assert.True(PanelMatrix.CarLineGood(true, true));
            Assert.False(PanelMatrix.CarLineGood(false, true));
            Assert.False(PanelMatrix.CarLineGood(false, false));
            Assert.False(PanelMatrix.CarLineGood(true, false));
            Assert.Equal(Theme.StatusUpToDate, PanelMatrix.CarLineHex(true));
            Assert.Equal(Theme.Caution, PanelMatrix.CarLineHex(false));
            // The read, whole: one snapshot of SimHub's report, the tables asked with its own id, and the line
            // shown, worded and inked from what was read; read once as the page is built and on every tick.
            Assert.Contains("Action readCar = () => { "
                + "var live = plugin.Live ?? LiveStatus.None; "
                + "var known = !string.IsNullOrEmpty(live.CarId) && plugin.CarLights.For(live.CarId) != null; "
                + "var count = plugin.CarLights.CarCount; "
                + "var tables = count > 0; "
                + "var missing = PanelMatrix.TablesMissing(count, plugin.CarLights.Status); "
                + "var covers = PanelMatrix.TablesCoverGame(live.GameName); "
                + "var text = PanelMatrix.CarLine(Settings.MatrixGearCarLadder(m), live.CarModel, known, tables, missing, covers); "
                + "carLine.Text = text ?? string.Empty; "
                + "carLine.Foreground = Ui.Brush(PanelMatrix.CarLineHex(PanelMatrix.CarLineGood(known, tables))); "
                + "carLine.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible; "
                + "}; readCar(); OnTick(readCar);", FlatSource());
            var matrix = MatrixSource();
            // SimHub's live report is read once a tick: the second read LiveCarHasTable makes is not taken.
            Assert.DoesNotContain("LiveCarHasTable", matrix);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(matrix, @"plugin\.Live\b"));
            Assert.Contains("Settings.FlagBoxMatrixGearCarLadder[i] = on; Save(); readCar();", matrix);
        }

        [Fact]
        public void The_preview_draws_the_matrix_as_its_settings_would()
        {
            Assert.Equal(new[] { "Idle display", "Mid revs", "Yellow", "Blue", "Pit limiter", "Car left", "Low fuel", "Chequered" },
                PanelMatrix.PreviewScenarios.Select(PanelMatrix.PreviewLabel));
            // The box at rest: the gear in its one white whatever Shift colours says, as Home and Rig's Idle draw
            // it. The artboard draws Shift colours on its "At rest" chip; the build keeps the idle display true to
            // the box and shows the switch under a chip of its own, the Rig page's "Mid revs".
            Assert.Equal(PanelEmulation.Idle, PanelMatrix.IdleScenario);
            Assert.Equal(PanelEmulation.Mid, PanelMatrix.RevsScenario);
            Assert.Equal("Idle display", PanelMatrix.PreviewLabel(PanelMatrix.IdleScenario));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.PreviewScenario(null));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.PreviewScenario(PanelEmulation.Oil));
            Assert.Equal(PanelEmulation.Yellow, PanelMatrix.PreviewScenario(PanelEmulation.Yellow));
            // The revs chip only on a matrix that rests on the gear: on a dark one it would draw the idle
            // display's dark frame under a second name. A held revs chip falls back to the idle display there,
            // and comes back with the gear.
            var restsOnGear = new MatrixOptions { Rest = "gear" };
            var restsDark = new MatrixOptions { Rest = "dark" };
            Assert.Equal(PanelMatrix.PreviewScenarios, PanelMatrix.PreviewChips(restsOnGear));
            Assert.Equal(PanelMatrix.PreviewScenarios, PanelMatrix.PreviewChips(null));
            Assert.Equal(PanelMatrix.PreviewScenarios.Where(id => id != PanelMatrix.RevsScenario), PanelMatrix.PreviewChips(restsDark));
            Assert.Equal(PanelMatrix.RevsScenario, PanelMatrix.PreviewScenario(PanelMatrix.RevsScenario, restsOnGear));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.PreviewScenario(PanelMatrix.RevsScenario, restsDark));
            Assert.Equal(PanelEmulation.Yellow, PanelMatrix.PreviewScenario(PanelEmulation.Yellow, restsDark));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.PreviewScenario(PanelEmulation.Oil, restsOnGear));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.PreviewScenario(null, restsOnGear));
            // The page reads the held chip through the matrix's own settings, never the list of every chip.
            Assert.DoesNotContain("PanelMatrix.PreviewScenario(matrixPreviewScenario)", MatrixSource());
            Assert.DoesNotContain("foreach (var id in PanelMatrix.PreviewScenarios)", MatrixSource());
            Assert.Equal("All devices at once", PanelMatrix.AllDevices);
            var bandsOn = new MatrixOptions { Rest = "gear", Bands = true };
            var bandsOff = new MatrixOptions { Rest = "gear", Bands = false };
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, bandsOn));
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, bandsOff));
            // Shift colours can be seen: under the revs chip the gear takes its first shift colour with the
            // switch on and stays white with it off; on a matrix that rests dark the chip draws the idle display.
            Assert.Equal(PanelMatrix.RevsScenario, PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, bandsOn));
            Assert.Equal("Gear 4 stage1", PanelEmulation.GlyphFor(PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, bandsOn), bandsOn));
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, bandsOff), bandsOff));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, new MatrixOptions { Rest = "dark", Bands = true }));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, null));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(null, bandsOn));

            // What a screen reader is told the preview draws: the artboard's alt text, as the matrix leaves it.
            var gear = new MatrixOptions { Rest = "gear" };
            var dark = new MatrixOptions { Rest = "dark" };
            Assert.Equal("Gear 4", PanelMatrix.PreviewAlt(PanelMatrix.IdleScenario, gear));
            // The revs chip with Shift colours on is the one picture that differs from the idle display's, and
            // is named for what it draws; the idle display is not.
            Assert.Equal("Gear 4 in the first shift colour", PanelMatrix.PreviewAlt(PanelMatrix.RevsScenario, bandsOn));
            Assert.Equal("Gear 4", PanelMatrix.PreviewAlt(PanelMatrix.IdleScenario, bandsOn));
            Assert.Equal("Gear 4", PanelMatrix.PreviewAlt(PanelMatrix.RevsScenario, bandsOff));
            Assert.Equal("Dark", PanelMatrix.PreviewAlt(PanelMatrix.IdleScenario, dark));
            Assert.Equal(new[] { "Yellow flag", "Blue flag", "Pit limiter frame", "Car on the left", "Fuel pump", "Chequered flag" },
                PanelMatrix.PreviewScenarios.Skip(2).Select(id => PanelMatrix.PreviewAlt(id, gear)));

            // Every setting reaches the picture, each from its own switch: every field is moved off its default.
            var settings = new OpenDashSettings();
            settings.Normalise();
            var slot = settings.AddMatrixPanel("Left pillar");
            var i = slot - 1;
            settings.FlagBoxFlags[i] = false;
            settings.FlagBoxSide[i] = "left";
            settings.SetMatrixRest(slot, "gear");
            settings.FlagBoxMatrixGearBands[i] = !settings.MatrixGearBands(slot);
            settings.FlagBoxMatrixGearCarLadder[i] = !settings.MatrixGearCarLadder(slot);
            var options = PanelMatrix.OptionsFor(settings, slot);
            Assert.False(options.Flags);
            Assert.True(options.Pit);
            Assert.True(options.Spotter);
            Assert.True(options.Warnings);
            Assert.Equal("left", options.Side);
            Assert.Equal("gear", options.Rest);
            Assert.Equal(!Contract.DefaultFlagBoxGearBands, options.Bands);
            Assert.Equal(!Contract.DefaultFlagBoxGearCarLadder, options.CarLadder);
            // The two gear switches read apart: each moved alone moves only its own field.
            settings.FlagBoxMatrixGearBands[i] = true;
            settings.FlagBoxMatrixGearCarLadder[i] = false;
            Assert.True(PanelMatrix.OptionsFor(settings, slot).Bands);
            Assert.False(PanelMatrix.OptionsFor(settings, slot).CarLadder);
            settings.FlagBoxMatrixGearBands[i] = false;
            settings.FlagBoxMatrixGearCarLadder[i] = true;
            Assert.False(PanelMatrix.OptionsFor(settings, slot).Bands);
            Assert.True(PanelMatrix.OptionsFor(settings, slot).CarLadder);

            // A chip the matrix would not show is drawn in its own scenario, as the Rig page draws it
            // (PanelRigMap.MatrixOptionsFor and PanelEmulation.MatrixFrame), so "All devices at once" opens on the
            // same picture: a family switched off leaves the matrix at what it shows when nothing takes it over,
            // which on a gear matrix with Shift colours on is the gear at that chip's revs, in its first shift
            // colour, and never the idle display's white gear (PanelEmulationTests pins the same for a car on the
            // side the matrix is not mounted on). The chip's own family is the only one it leaves.
            var chips = new Dictionary<string, string>
            {
                { "Flags", PanelEmulation.Yellow }, { "Pit", PanelEmulation.Limiter },
                { "Spotter", PanelEmulation.CarLeft }, { "Warnings", PanelEmulation.LowFuel },
            };
            foreach (var family in chips.Keys)
            {
                var one = new OpenDashSettings();
                one.Normalise();
                var n = one.AddMatrixPanel("A");
                one.SetMatrixRest(n, "gear");
                if (family == "Flags") one.FlagBoxFlags[n - 1] = false;
                if (family == "Pit") one.FlagBoxPit[n - 1] = false;
                if (family == "Spotter") one.FlagBoxSpotter[n - 1] = false;
                if (family == "Warnings") one.FlagBoxWarnings[n - 1] = false;
                var read = PanelMatrix.OptionsFor(one, n);
                Assert.Equal(family != "Flags", read.Flags);
                Assert.Equal(family != "Pit", read.Pit);
                Assert.Equal(family != "Spotter", read.Spotter);
                Assert.Equal(family != "Warnings", read.Warnings);
                foreach (var chip in chips)
                {
                    var drawn = PanelMatrix.DrawnScenario(chip.Value, read);
                    Assert.Equal(chip.Value, drawn);
                    var drawnWith = PanelMatrix.OptionsFor(one, n, drawn);
                    var glyph = PanelEmulation.GlyphFor(drawn, drawnWith);
                    // The Rig page's tile for this matrix under the same chip.
                    Assert.Equal(PanelEmulation.GlyphFor(chip.Value, PanelRigMap.MatrixOptionsFor(one, n, chip.Value)), glyph);
                    Assert.NotEqual(PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, read), glyph);
                    if (chip.Key == family)
                    {
                        Assert.Equal("Gear 4 stage1", glyph);
                        Assert.Equal(PanelMatrix.PreviewRevs, PanelMatrix.PreviewAlt(drawn, drawnWith));
                    }
                    else Assert.Equal(PanelMatrix.PreviewAlt(chip.Value, new MatrixOptions()), PanelMatrix.PreviewAlt(drawn, drawnWith));
                }
            }
            // A car on the side the matrix is not mounted on: the gear at the car's revs, named for that, with
            // Shift colours on; the white gear with it off; nothing on a matrix that rests dark.
            var mountedRight = new MatrixOptions { Side = "right", Rest = "gear", Bands = true };
            Assert.Equal(PanelEmulation.CarLeft, PanelMatrix.DrawnScenario(PanelEmulation.CarLeft, mountedRight));
            Assert.Equal("Gear 4 stage1", PanelEmulation.GlyphFor(PanelMatrix.DrawnScenario(PanelEmulation.CarLeft, mountedRight), mountedRight));
            Assert.Equal("Gear 4 in the first shift colour", PanelMatrix.PreviewAlt(PanelEmulation.CarLeft, mountedRight));
            Assert.Equal("Gear 4", PanelMatrix.PreviewAlt(PanelEmulation.CarLeft, new MatrixOptions { Side = "right", Rest = "gear", Bands = false }));
            Assert.Equal("Dark", PanelMatrix.PreviewAlt(PanelEmulation.CarLeft, new MatrixOptions { Side = "right", Rest = "dark" }));
            Assert.Equal("Car on the left", PanelMatrix.PreviewAlt(PanelEmulation.CarLeft, new MatrixOptions { Side = "left", Rest = "gear" }));

            // Critical flags only reaches the picture as the Rig page's does: the flags off under a flag that is
            // news (the chequer, the white, the green), on under one that is critical, and the chip kept.
            var critical = new OpenDashSettings();
            critical.Normalise();
            var c = critical.AddMatrixPanel("A");
            critical.SetMatrixRest(c, "gear");
            critical.FlagBoxMatrixCriticalOnly[c - 1] = true;
            foreach (var news in new[] { PanelEmulation.Chequer, PanelEmulation.White, PanelEmulation.Green })
            {
                Assert.Equal(news, PanelMatrix.DrawnScenario(news, PanelMatrix.OptionsFor(critical, c)));
                Assert.False(PanelMatrix.OptionsFor(critical, c, news).Flags);
                Assert.Equal("Gear 4 stage1", PanelEmulation.GlyphFor(news, PanelMatrix.OptionsFor(critical, c, news)));
            }
            Assert.Equal("Gear 4 in the first shift colour", PanelMatrix.PreviewAlt(PanelEmulation.Chequer, PanelMatrix.OptionsFor(critical, c, PanelEmulation.Chequer)));
            foreach (var flag in new[] { PanelEmulation.Yellow, PanelEmulation.Blue, PanelEmulation.Red, PanelEmulation.Black })
            {
                Assert.True(PanelMatrix.OptionsFor(critical, c, flag).Flags);
                Assert.Equal(flag, PanelEmulation.GlyphFor(flag, PanelMatrix.OptionsFor(critical, c, flag)));
            }
            critical.FlagBoxMatrixCriticalOnly[c - 1] = false;
            Assert.True(PanelMatrix.OptionsFor(critical, c, PanelEmulation.Chequer).Flags);
            Assert.Equal("Chequered flag", PanelMatrix.PreviewAlt(PanelEmulation.Chequer, PanelMatrix.OptionsFor(critical, c, PanelEmulation.Chequer)));
            // The options are the Rig page's for every chip, Critical flags only on or off.
            foreach (var on in new[] { true, false })
            {
                critical.FlagBoxMatrixCriticalOnly[c - 1] = on;
                foreach (var chip in PanelMatrix.PreviewScenarios.Concat(new[] { PanelEmulation.White, PanelEmulation.Green, (string)null }))
                {
                    var mine = PanelMatrix.OptionsFor(critical, c, chip);
                    var rig = PanelRigMap.MatrixOptionsFor(critical, c, chip);
                    Assert.Equal(new object[] { rig.Side, rig.Rest, rig.Bands, rig.CarLadder, rig.Flags, rig.Pit, rig.Spotter, rig.Warnings },
                        new object[] { mine.Side, mine.Rest, mine.Bands, mine.CarLadder, mine.Flags, mine.Pit, mine.Spotter, mine.Warnings });
                }
            }

            var source = MatrixSource();
            var flatSource = FlatSource();
            Assert.Contains("var options = PanelMatrix.OptionsFor(Settings, matrix); return PanelMatrix.DrawnScenario(PanelMatrix.PreviewScenario(matrixPreviewScenario, options), options);", flatSource);
            Assert.Contains("private MatrixOptions MatrixDrawnOptions(int matrix) { return PanelMatrix.OptionsFor(Settings, matrix, MatrixDrawn(matrix)); }", flatSource);
            Assert.Contains("Settings.FlagBoxMatrixCriticalOnly[i] = on; Save(); repaint();", source);
            Assert.Contains("Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), MatrixDrawnOptions(m)), MatrixStyle.Preview, MatrixDim());", source);
            Assert.Contains("MatrixRepaint(preview, PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), MatrixDrawnOptions(m)), MatrixStyle.Preview);", source);
            Assert.Contains("Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, PanelMatrix.IdleScenario, PanelMatrix.OptionsFor(Settings, m)), MatrixStyle.Card, MatrixDim());", source);
            Assert.Contains("MatrixRepaint(cardPicture, PanelEmulation.MatrixFrame(GlyphSheet, PanelMatrix.IdleScenario, options), MatrixStyle.Card);", source);
            Assert.Contains("Open(PanelPage.Rig, PanelMatrix.PreviewScenario(matrixPreviewScenario, PanelMatrix.OptionsFor(Settings, m)))", source);
            // The picture is never drawn in the settings alone, which leave Critical flags only out of it.
            Assert.DoesNotContain("MatrixFrame(GlyphSheet, MatrixDrawn(m), PanelMatrix.OptionsFor(Settings, m))", flatSource);
            Assert.DoesNotContain("MatrixFrame(GlyphSheet, MatrixDrawn(m), options)", flatSource);
            // The preview is named for what it draws, when it is built and on every repaint, on its frame: the
            // one element there that a screen reader is told about, as an image.
            Assert.Contains("var frame = new ImageBorder {", flatSource);
            Assert.Contains("Child = preview, }; AutomationProperties.SetName(frame, PanelMatrix.PreviewAlt(MatrixDrawn(m), MatrixDrawnOptions(m)));", flatSource);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(flatSource, System.Text.RegularExpressions.Regex.Escape("AutomationProperties.SetName(frame, PanelMatrix.PreviewAlt(MatrixDrawn(m), MatrixDrawnOptions(m)));")).Count);
            Assert.Contains("var chipGroup = new GroupBorder { Child = chips }; AutomationProperties.SetName(chipGroup, PanelMatrix.PreviewChipsName);", flatSource);
            foreach (var write in new[] { "Settings.FlagBoxFlags[i] = on; Save(); repaint();", "Settings.FlagBoxPit[i] = on; Save(); repaint();",
                "Settings.FlagBoxSpotter[i] = on; Save(); repaint();", "Settings.FlagBoxWarnings[i] = on; Save(); repaint();" })
            {
                Assert.Contains(write, source);
            }

            // The artboard's numbers.
            Assert.Equal(7, PanelMatrix.ProfileDotSize);
            Assert.Equal(10, PanelMatrix.ProfileGap);
            Assert.Equal(6, PanelMatrix.ProfileButtonPaddingX);
            Assert.Equal(12, PanelMatrix.HeaderGap);
            Assert.Equal(6, PanelMatrix.ActionGap);
            Assert.Equal(12, PanelMatrix.SheetGap);
            Assert.Equal(12, PanelMatrix.EmptyGap);
            Assert.Equal(280, PanelMatrix.NameWidth);
            Assert.Equal(22, PanelMatrix.StackedGap);
            Assert.Equal(8, PanelMatrix.ReorderGap);
            Assert.Equal(8, PanelMatrix.PriorityHeadGap);
            Assert.Equal(12, PanelMatrix.LayerGap);
            Assert.Equal(16, PanelMatrix.RankSize);
            Assert.Equal(14, PanelMatrix.OptionTextSize);
            Assert.Equal(16, PanelMatrix.OptionGap);
            Assert.Equal(12, PanelMatrix.OptionLineSize);
            Assert.Equal(2, PanelMatrix.OptionLineGap);
            Assert.Equal(8, PanelMatrix.ThresholdsGap);
            Assert.Equal(300, PanelMatrix.PreviewColumnWidth);
            Assert.Equal(36, PanelMatrix.BodyGap);
            Assert.Equal(20, PanelMatrix.PreviewFramePadding);
            Assert.Equal(14, PanelMatrix.PreviewGap);
            Assert.Equal(6, PanelMatrix.ChipGap);
            // Without the artboard's grips an option starts where its layer's name does, the rank's 14 and 12 in.
            Assert.Equal(26, PanelMatrix.OptionIndent);
            Assert.Equal(PanelMatrix.RankWidth + PanelMatrix.LayerGap, PanelMatrix.OptionIndent);
            Assert.Equal(4, PanelMatrix.UnrankedDotSize);
            Assert.Equal(8, PanelMatrix.NewTagGap);
            Assert.Equal(4, PanelMatrix.SlotCaptionLift);
            Assert.Equal(7, PanelMatrix.OptionPaddingY);
            Assert.Equal(12, PanelMatrix.LayerPaddingY);
            Assert.Equal(14, PanelMatrix.RankWidth);
            Assert.Equal(20, PanelMatrix.SectionPaddingTop);
            Assert.Equal(18, PanelMatrix.SectionGap);
            // And each where it is drawn.
            var flatNumbers = FlatSource();
            foreach (var pin in new[]
            {
                "if (!primary) button.Padding = new Thickness(PanelMatrix.ProfileButtonPaddingX, 0, PanelMatrix.ProfileButtonPaddingX, 0);",
                "var slotCaption = PanelMatrix.SlotCaption(name, m); if (slotCaption != null) {",
                "title.Margin = new Thickness(0, 0, PanelMatrix.HeaderGap, 0);",
                "slot.Margin = new Thickness(0, 0, 0, PanelMatrix.SlotCaptionLift);",
                "var rename = Ui.Button(PanelMatrix.Rename, PanelButtonKind.Outline, PanelButtonSize.Small); rename.ToolTip = PanelMatrix.RenameTooltip; rename.Click += (sender, args) => ShowRenameMatrix(m);",
                "var remove = Ui.Button(PanelMatrix.Remove, PanelButtonKind.GhostDanger, PanelButtonSize.Small); remove.ToolTip = PanelMatrix.RemoveTooltip; remove.Click += (sender, args) => ShowRemoveMatrix(m);",
                "var head = Ui.Row(heading, Ui.HStack(PanelMatrix.ActionGap, rename, remove));",
                "var fix = Ui.FixBox(PanelMatrix.FixTitle, null, PanelMatrix.FixSteps(m, FlagBoxName()), check);",
                "var all = Ui.LinkButton(PanelMatrix.AllDevices);",
                "var thresholds = Ui.LinkButton(PanelMatrix.ThresholdsLink);",
                "body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelMatrix.PreviewColumnWidth) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelMatrix.BodyGap) });",
                "previewColumn.Element.MaxWidth = PanelMatrix.PreviewColumnWidth;",
                // The responsive rules: the priority list takes the rest of the width beside the preview, and
                // stacked, the preview column keeps to the left rather than centring.
                "body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelMatrix.PreviewColumnWidth) }); "
                    + "body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelMatrix.BodyGap) }); "
                    + "body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });",
                "previewColumn.Element.VerticalAlignment = VerticalAlignment.Top; priority.VerticalAlignment = VerticalAlignment.Top;",
                "previewColumn.Element.MaxWidth = PanelMatrix.PreviewColumnWidth; previewColumn.Element.HorizontalAlignment = HorizontalAlignment.Left;",
                // The rule over the selected matrix and over each layer, and the preview's inset frame.
                "return new Border { BorderBrush = Ui.Brush(Theme.Rule), BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0), "
                    + "Padding = new Thickness(0, PanelMatrix.SectionPaddingTop, 0, 0), Child = Ui.VStack(PanelMatrix.SectionGap, parts.ToArray()), };",
                "return new Border { BorderBrush = Ui.Brush(Theme.Rule), BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0), Child = stack, };",
                "Padding = new Thickness(PanelMatrix.PreviewFramePadding), Background = Ui.Brush(Theme.SurfaceInset), BorderBrush = Ui.Brush(Theme.Rule), "
                    + "BorderThickness = new Thickness(PanelMetrics.BorderWeight), CornerRadius = new CornerRadius(Theme.Radius), Child = preview, };",
                "carLine.TextWrapping = TextWrapping.Wrap;",
                // A layer's line: rank, words that take the rest, link and control; an option's: words and control.
                "grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); "
                    + "grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });",
                "grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.Children.Add(words);",
                "control.VerticalAlignment = VerticalAlignment.Center; control.HorizontalAlignment = HorizontalAlignment.Right; control.Margin = new Thickness(PanelMatrix.LayerGap, 0, 0, 0); Grid.SetColumn(control, 3);",
                "control.VerticalAlignment = VerticalAlignment.Center; control.HorizontalAlignment = HorizontalAlignment.Right; control.Margin = new Thickness(PanelMatrix.OptionGap, 0, 0, 0); Grid.SetColumn(control, 1);",
                "Padding = new Thickness(0, PanelMatrix.SectionPaddingTop, 0, 0), Child = Ui.VStack(PanelMatrix.SectionGap, parts.ToArray()),",
                "Padding = new Thickness(PanelMatrix.PreviewFramePadding),",
                "chip.Margin = new Thickness(0, 0, PanelMatrix.ChipGap, PanelMatrix.ChipGap);",
                "chip.Padding = new Thickness(PanelKit.ChipPaddingXLights, 0, PanelKit.ChipPaddingXLights, 0);",
                "fix.Padding = new Thickness(PanelKit.FixPaddingX, PanelKit.FixPaddingYLights, PanelKit.FixPaddingX, PanelKit.FixPaddingYLights);",
                "var cell = new Border { Width = PanelMatrix.RankWidth, Margin = new Thickness(0, 0, PanelMatrix.LayerGap, 0), Child = dot };",
                "var number = Ui.Text(rank, PanelMatrix.RankSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data); number.Width = PanelMatrix.RankWidth;",
                "var name = Ui.Text(title, PanelShell.RowTitleSize, FontWeights.Medium, Theme.TextPrimary);",
                "var line = Ui.Text(caption, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.TextSecondary);",
                "var name = Ui.Text(title, PanelMatrix.OptionTextSize, FontWeights.Normal, Theme.TextPrimary);",
                "var under = Ui.Text(caption, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.TextSecondary);",
                "control.Margin = new Thickness(PanelMatrix.OptionGap, 0, 0, 0);",
                "var carLine = Ui.Text(string.Empty, PanelMatrix.OptionLineSize, FontWeights.Normal, Theme.StatusUpToDate);",
                "link.Margin = new Thickness(PanelMatrix.LayerGap, 0, PanelMatrix.ThresholdsGap, 0);",
                "Padding = new Thickness(0, PanelMatrix.LayerPaddingY, 0, PanelMatrix.LayerPaddingY),",
                "head.Margin = new Thickness(0, 0, 0, PanelMatrix.PriorityHeadGap);",
                "Ui.HStack(PanelMatrix.ReorderGap,",
                "line.Margin = new Thickness(0, PanelMatrix.OptionLineGap, 0, 0);",
                "Ui.VStack(PanelMatrix.StackedGap, previewColumn.Element, priority)",
                "var name = Ui.Input(PanelMatrix.DefaultName(slot), PanelMatrix.NameWidth);",
                // The 8x8 centred in its frame, the links at the artboard's 13 and their own height, the New tag
                // and the stacked preview at the column's left, and the chips wrapping across the column.
                "preview.HorizontalAlignment = HorizontalAlignment.Center;",
                "all.FontSize = Theme.SizeSmall; all.Height = double.NaN; all.HorizontalAlignment = HorizontalAlignment.Left;",
                "thresholds.FontSize = Theme.SizeSmall; thresholds.Height = double.NaN;",
                "tag.HorizontalAlignment = HorizontalAlignment.Left;",
                "var chips = new WrapPanel { Orientation = Orientation.Horizontal };",
                // The rank's 12 after it, which OptionIndent counts to line the options up under a layer's name,
                // and the unranked dot, drawn in full, centred in the rank's column.
                "number.VerticalAlignment = VerticalAlignment.Center; number.Margin = new Thickness(0, 0, PanelMatrix.LayerGap, 0);",
                "var dot = new Ellipse { Width = PanelMatrix.UnrankedDotSize, Height = PanelMatrix.UnrankedDotSize, Fill = Ui.Brush(Theme.TextSecondary), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, };",
                // The profile's dot and line centred on the title's line.
                "Fill = Ui.Brush(action.StateHex), VerticalAlignment = VerticalAlignment.Center, };",
                "line.VerticalAlignment = VerticalAlignment.Center; var row = Ui.HStack(PanelMatrix.ProfileGap, dot, line);",
                // The chip the page opens on, and no chip held focus until one is found to.
                "private string matrixPreviewScenario = PanelMatrix.IdleScenario;",
                "var focused = -1;",
            })
            {
                Assert.Contains(pin, flatNumbers);
            }
            var matrix = MatrixSource();
            Assert.Contains("MatrixStyle.Preview", matrix);
            // The New tag has the frame's own line, over it and clear of the lamps, never after the Rig link.
            Assert.Contains("var tagged = Ui.VStack(PanelMatrix.NewTagGap, tag, frame);", matrix);
            Assert.Contains("Ui.VStack(PanelMatrix.PreviewGap, tagged, all, chipGroup)", matrix);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(matrix, @"Ui\.NewTag\(\)"));
            Assert.DoesNotContain("all, Ui.NewTag()", matrix);
            Assert.Contains("Child = preview,", matrix);
            // Idle display is unranked, drawn with the artboard's dot; the device row has no rank column.
            Assert.Contains("MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.IdleDisplayTitle), PanelMatrix.IdleDisplayTitle,", matrix);
            Assert.Contains("Width = PanelMatrix.UnrankedDotSize,", matrix);
            Assert.Contains("words.Margin = new Thickness(PanelMatrix.OptionIndent, 0, 0, 0);", matrix);
            Assert.Contains("else { words.Margin = new Thickness(PanelMatrix.OptionIndent, 0, 0, 0); }", FlatSource());
            Assert.Contains("Padding = new Thickness(PanelMatrix.OptionIndent, PanelMatrix.OptionPaddingY, 0, PanelMatrix.OptionPaddingY),", matrix);
            // The selected matrix's name wraps inside its column rather than trimming, and its number wraps
            // under it.
            Assert.Contains("var heading = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };", matrix);
            Assert.Contains("var title = Ui.SubHeading(name);", matrix);
            Assert.Contains("title.TextTrimming = TextTrimming.None; title.TextWrapping = TextWrapping.Wrap;", FlatSource());
            Assert.DoesNotContain("new StackPanel { Orientation = Orientation.Horizontal }", matrix);
        }

        /// <summary>
        /// Every name the page gives a screen reader lands on something WPF tells it about: a Button, a switch,
        /// or the kit's GroupBorder and ImageBorder, which give a border a peer. A Border, a WrapPanel or the card grid's
        /// panel has none, so a name set on one is never heard.
        /// </summary>
        [Fact]
        public void Every_name_for_a_screen_reader_is_set_where_one_is_heard()
        {
            var flat = FlatSource();
            var targets = System.Text.RegularExpressions.Regex.Matches(flat, @"AutomationProperties\.SetName\((\w+),")
                .Cast<System.Text.RegularExpressions.Match>().Select(match => match.Groups[1].Value).ToList();
            Assert.Equal(new[] { "add", "card", "chipGroup", "control", "frame", "grid" }, targets.Distinct().OrderBy(t => t, StringComparer.Ordinal));
            foreach (var pin in new[]
            {
                // The peers are the kit's: the groups a GroupBorder, the preview's frame an ImageBorder.
                "var grid = new GroupBorder {",
                "var chipGroup = new GroupBorder {",
                "var frame = new ImageBorder {",
                // The cards' group, each card by its name and the add tile by its words.
                "AutomationProperties.SetName(grid, PanelMatrix.PanelsTitle);",
                "AutomationProperties.SetName(add, PanelMatrix.AddPanel);",
                "card.ToolTip = name; AutomationProperties.SetName(card, PanelMatrix.CardName(name, m, Settings.MatrixSide(m), shown));",
            })
            {
                Assert.Contains(pin, flat);
            }
            // Each switch, and the greyed device press, by its row's name, in both helpers.
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("if (control is ToggleButton) AutomationProperties.SetName(control, title);")).Count);
            // A press keeps its own words: the greyed device press is heard as what it shows.
            Assert.DoesNotContain("control is ButtonBase", flat);
            Assert.Contains("Ui.Button(PanelMatrix.SimHubDeviceButton, PanelButtonKind.Outline, PanelButtonSize.Small)", flat);
            // Each card is heard with the line under its name, which the card's name would otherwise replace.
            Assert.Equal("Left pillar, Matrix 2, Left", PanelMatrix.CardName("Left pillar", 2, "left", null));
            Assert.Equal("Matrix 3, Both sides", PanelMatrix.CardName("Matrix 3", 3, "both", null));
            Assert.Equal("Left pillar, Showing", PanelMatrix.CardName("Left pillar", 1, "both", true));
            Assert.Equal("Left pillar, Not shown in SimHub", PanelMatrix.CardName("Left pillar", 2, "left", false));
            foreach (var shown in new bool?[] { null, true, false })
            {
                Assert.Equal("Left pillar, " + PanelMatrix.CardLine("Left pillar", 2, "right", shown).Replace(" · ", ", "),
                    PanelMatrix.CardName("Left pillar", 2, "right", shown));
            }
            Assert.DoesNotContain("SetName(preview,", flat);
            Assert.DoesNotContain("SetName(chips,", flat);
        }

        [Fact]
        public void Search_lists_every_row_and_heading_by_the_constants_the_page_draws()
        {
            var labels = PanelMatrix.Search.Select(entry => entry.Label).ToList();
            foreach (var title in new[]
            {
                FlagBoxProfile.ProfileName, PanelMatrix.AddPanel, PanelMatrix.PriorityTitle,
                PanelMatrix.FlagsTitle, PanelMatrix.CriticalFlagsOnlyTitle, PanelMatrix.PitLaneTitle, PanelMatrix.SpotterTitle,
                PanelMatrix.MountingSideTitle, PanelMatrix.SpotterAnimationTitle, PanelMatrix.WarningsTitle,
                PanelMatrix.IdleDisplayTitle, PanelMatrix.ShiftColoursTitle, PanelMatrix.CarShiftPointsTitle, PanelMatrix.RedlineFlashTitle,
            })
            {
                Assert.Contains(title, labels);
            }
            Assert.Equal(labels.Count, labels.Distinct().Count());
            // "Your matrices" is the cards' name for a screen reader and is never drawn, so it is a keyword.
            Assert.DoesNotContain(PanelMatrix.PanelsTitle, labels);
            Assert.Contains("your matrices", PanelMatrix.Search.Single(entry => entry.Label == PanelMatrix.AddPanel).Keywords);
            // Each entry routes to the part of the page that draws it.
            Assert.Equal(new[]
            {
                FlagBoxProfile.ProfileName + " -> " + PanelMatrix.AnchorProfile,
                PanelMatrix.AddPanel + " -> " + PanelMatrix.AnchorPanels,
                PanelMatrix.PriorityTitle + " -> " + PanelMatrix.AnchorPriority,
                PanelMatrix.FlagsTitle + " -> " + PanelMatrix.AnchorFlags,
                PanelMatrix.CriticalFlagsOnlyTitle + " -> " + PanelMatrix.AnchorFlags,
                PanelMatrix.PitLaneTitle + " -> " + PanelMatrix.AnchorPitLane,
                PanelMatrix.SpotterTitle + " -> " + PanelMatrix.AnchorSpotter,
                PanelMatrix.MountingSideTitle + " -> " + PanelMatrix.AnchorSpotter,
                PanelMatrix.SpotterAnimationTitle + " -> " + PanelMatrix.AnchorSpotterAnimation,
                PanelMatrix.WarningsTitle + " -> " + PanelMatrix.AnchorWarnings,
                PanelMatrix.IdleDisplayTitle + " -> " + PanelMatrix.AnchorIdleDisplay,
                PanelMatrix.ShiftColoursTitle + " -> " + PanelMatrix.AnchorIdleDisplay,
                PanelMatrix.CarShiftPointsTitle + " -> " + PanelMatrix.AnchorIdleDisplay,
                PanelMatrix.RedlineFlashTitle + " -> " + PanelMatrix.AnchorIdleDisplay,
            }, PanelMatrix.Search.Select(entry => entry.Label + " -> " + entry.Route.Anchor));
            // Each entry's keywords, the old row names among them ("race flags", "pit status", "car warnings",
            // "slide in", "at rest"), so a search in the words the panel used to use lands on the row that
            // replaced it.
            Assert.Equal(new[]
            {
                FlagBoxProfile.ProfileName + ": flag box profile, install, reinstall, matrix profile, 8x8",
                "Add a matrix: your matrices, new, 8x8, flag box, pillar, panel, panels",
                "Priority: order, layers, takes over",
                "Flags: race flags, yellow, blue",
                "Critical flags only: chequer, white, green",
                "Pit lane: limiter, speeding, pit status",
                "Spotter: cars alongside",
                "Mounting side: left, right, both",
                "Spotter bar animation: slide in",
                "Warnings: fuel, oil, water, car warnings",
                "Idle display: at rest, gear, dark",
                "Shift colours: gear, revs",
                "Car-specific shift points: lovely, car data, car-specific, thresholds",
                "Redline flash: gear, shift",
            }, PanelMatrix.Search.Select(entry => entry.Label + ": " + string.Join(", ", entry.Keywords)));
            // And the page wraps each of those parts in its anchor.
            var flat = FlatSource();
            foreach (var wrap in new[]
            {
                "Ui.Anchor(BuildMatrixProfile(plan), PanelMatrix.AnchorProfile)",
                "Ui.Anchor(cards, PanelMatrix.AnchorPanels)",
                "Ui.Anchor(column, PanelMatrix.AnchorPreview)",
                "Ui.Anchor(flags, PanelMatrix.AnchorFlags)",
                "Ui.Anchor(pit, PanelMatrix.AnchorPitLane)",
                "Ui.Anchor(spotter, PanelMatrix.AnchorSpotter)",
                "on => { Settings.FlagBoxSpotterAnimation = on; Save(); })), PanelMatrix.AnchorSpotterAnimation)",
                "Ui.Anchor(warnings, PanelMatrix.AnchorWarnings)",
                "Ui.Anchor(idleLayer, PanelMatrix.AnchorIdleDisplay)",
                "device), PanelMatrix.AnchorPriority)",
            })
            {
                Assert.Contains(wrap, flat);
            }
            var matrix = MatrixSource();
            foreach (var name in new[]
            {
                "PanelMatrix.AddPanel", "PanelMatrix.PriorityTitle", "PanelMatrix.FlagsTitle",
                "PanelMatrix.CriticalFlagsOnlyTitle", "PanelMatrix.PitLaneTitle", "PanelMatrix.SpotterTitle", "PanelMatrix.MountingSideTitle",
                "PanelMatrix.SpotterAnimationTitle", "PanelMatrix.WarningsTitle", "PanelMatrix.IdleDisplayTitle", "PanelMatrix.ShiftColoursTitle",
                "PanelMatrix.CarShiftPointsTitle", "PanelMatrix.RedlineFlashTitle",
            })
            {
                Assert.Contains(name, matrix);
            }
        }

        /// <summary>
        /// The page justifies its words by what the tree holds -- voice.md's sections, plugin.md's departures
        /// table, the artboards, a ticket -- and never by a numbered ruling in a workflow's scratch files, which
        /// nobody reading the code can open. A ruling posted on a ticket is cited with the ticket ("#524, ruling
        /// 8"). The files are read whole, comments included, since that is where a citation lives.
        /// </summary>
        [Fact]
        public void The_page_cites_only_what_the_tree_holds()
        {
            foreach (var file in new[]
            {
                System.IO.Path.Combine("plugin", "OpenDash", "PanelMatrix.cs"),
                System.IO.Path.Combine("plugin", "OpenDash", "SettingsControl.Matrix.cs"),
                System.IO.Path.Combine("plugin", "OpenDash.Tests", "PanelMatrixTests.cs"),
            })
            {
                var text = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root(), file));
                var flat = System.Text.RegularExpressions.Regex.Replace(text, @"\s*(///|//)?\s+", " ");
                // "ruling" and a number with no ticket before it; the pattern is split so this line is no citation.
                var bare = System.Text.RegularExpressions.Regex.Matches(flat, @"(?<!#\d+, )\b" + "rul" + @"ings? \d+");
                Assert.True(bare.Count == 0, file + ": " + string.Join(" | ", bare.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value)));
                Assert.DoesNotContain("critic's " + "rulings", flat);
                Assert.DoesNotContain("#503 " + "inventory", flat);
                Assert.DoesNotContain("voice_" + "rulings", flat);
            }
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorFlags = matrix.flags",
                "AnchorIdleDisplay = matrix.idle-display",
                "AnchorPanels = matrix.panels",
                "AnchorPitLane = matrix.pit-lane",
                "AnchorPreview = matrix.preview",
                "AnchorPriority = matrix.priority",
                "AnchorProfile = matrix.profile",
                "AnchorSpotter = matrix.spotter",
                "AnchorSpotterAnimation = matrix.spotter-animation",
                "AnchorWarnings = matrix.warnings",
            }, AnchorTable.Of(typeof(PanelMatrix)));
        }
    }
}
