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

        [Fact]
        public void The_matrix_rows_say_what_the_artboard_and_the_voice_rulings_say()
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
            Assert.Equal("Thresholds", PanelMatrix.ThresholdsLink);
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
        /// One noun for the thing a driver owns (ruling 59): a matrix, never a panel. The empty state is the
        /// Matrix page's own and Home reads it, as it reads NoScreens and NoStrips.
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
            Assert.Equal("Removes this matrix.", PanelMatrix.RemoveTooltip);
            Assert.Equal("Cancel", PanelMatrix.Cancel);
            Assert.Equal("What to preview", PanelMatrix.PreviewChipsName);
            Assert.Equal("No strips yet.", PanelLeds.NoStrips);
            var home = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Home.cs"));
            Assert.Contains("PanelLeds.NoStrips", home);
            Assert.Contains("PanelMatrix.NoPanels", home);
            foreach (var text in new[]
            {
                PanelMatrix.NoPanels, PanelMatrix.PanelsTitle, PanelMatrix.AddPanel, PanelMatrix.AllInUse, PanelMatrix.AddTooltip,
                PanelMatrix.RenameTooltip, PanelMatrix.RemoveTooltip, PanelMatrix.RemoveCaption,
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
            // Update is the page's one primary press: of the states that offer a press, only Outdated.
            var pressable = new[] { FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed };
            Assert.Equal(new[] { FlagBoxInstallState.Outdated },
                pressable.Where(state => PanelMatrix.ProfileRow(state, "0.4.0").Style == PanelButton.Primary));

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

            // A first install names the step SimHub leaves; Update says it updated an older copy, and Reinstall
            // over a current one says it reinstalled (ruling 67: one verb per press). The name is unquoted.
            foreach (var before in new[] { FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Failed })
            {
                var first = PanelMatrix.InstallSaid(before, FlagBoxInstallState.UpToDate, "OpenDash Flag box");
                Assert.Equal("Installed OpenDash Flag box. Select it on your matrix's device in SimHub.", first.Text);
                Assert.Equal(PanelTone.Info, first.Tone);
            }
            var updated = PanelMatrix.InstallSaid(FlagBoxInstallState.Outdated, FlagBoxInstallState.UpToDate, "OpenDash Flag box");
            Assert.Equal("Updated OpenDash Flag box.", updated.Text);
            Assert.Equal(PanelTone.Info, updated.Tone);
            var reinstalled = PanelMatrix.InstallSaid(FlagBoxInstallState.UpToDate, FlagBoxInstallState.UpToDate, "OpenDash Flag box");
            Assert.Equal("Reinstalled OpenDash Flag box.", reinstalled.Text);
            Assert.Equal(PanelTone.Info, reinstalled.Tone);
            // A failure in the form the other pages give theirs.
            Assert.Equal(PanelTone.Danger, PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Failed, "OpenDash Flag box").Tone);
            Assert.Equal("Could not install OpenDash Flag box. See SimHub's log.", PanelMatrix.InstallSaid(FlagBoxInstallState.Outdated, FlagBoxInstallState.Failed, "OpenDash Flag box").Text);
            // Out of reach, in the words the by-hand import under the title's line uses for that state.
            var unreachable = PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.Unavailable, "x");
            Assert.Equal("SimHub's matrix settings are not available. Import the profile by hand.", unreachable.Text);
            Assert.Equal(PanelTone.Caution, unreachable.Tone);
            Assert.StartsWith(PanelMatrix.Unreachable, FlagBoxInstallPlan.Summary(new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, null));
            Assert.Null(PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotEmbedded, "x"));

            // The hover on the title's line: one sentence, and none where the line or the import says it all.
            Assert.Null(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.NotEmbedded, null, null));
            Assert.Null(PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Unavailable, null, "0.5.0"));
            Assert.Equal("SimHub does not have this profile yet.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.NotInstalled, null, "0.5.0"));
            Assert.Equal("SimHub has the version this build carries.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.UpToDate, "0.5.0", "0.5.0"));
            Assert.Equal("SimHub has 0.4.0, and this build carries 0.5.0.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Outdated, "0.4.0", "0.5.0"));
            Assert.Equal("SimHub's log says why the last install failed.", PanelMatrix.ProfileLineTooltip(FlagBoxInstallState.Failed, null, "0.5.0"));
            foreach (FlagBoxInstallState state in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                var hover = PanelMatrix.ProfileLineTooltip(state, "0.4.0", "0.5.0");
                if (hover == null) continue;
                Assert.DoesNotContain(FlagBoxInstallPlan.Replaces, hover);
                Assert.Single(System.Text.RegularExpressions.Regex.Matches(hover, @"\.(\s|$)"));
            }

            // The press's tooltip: Install adds, Update and Reinstall replace.
            Assert.Equal("Adds OpenDash's profile to SimHub. Your own profiles are never changed.", PanelMatrix.InstallTooltip);
            Assert.Equal(PanelMatrix.InstallTooltip, PanelMatrix.ProfileTooltip(FlagBoxInstallState.NotInstalled));
            Assert.Equal(PanelMatrix.InstallTooltip, PanelMatrix.ProfileTooltip(FlagBoxInstallState.Failed));
            Assert.Equal(FlagBoxInstallPlan.Replaces, PanelMatrix.ProfileTooltip(FlagBoxInstallState.Outdated));
            Assert.Equal(FlagBoxInstallPlan.Replaces, PanelMatrix.ProfileTooltip(FlagBoxInstallState.UpToDate));

            var matrix = MatrixSource();
            var flat = FlatSource();
            Assert.Contains("PanelMatrix.ProfileRow(", matrix);
            Assert.Contains("var primary = action.Style == PanelButton.Primary;", matrix);
            Assert.Contains("primary ? PanelButtonKind.Primary : PanelButtonKind.Ghost", matrix);
            Assert.Contains("button.ToolTip = PanelMatrix.ProfileTooltip(state);", matrix);
            Assert.Contains("PanelMatrix.ShowsImportFallback(PanelMatrix.StateOf(plan)) ? BuildMatrixImportFallback(plan) : null", matrix);
            // The by-hand import wraps, so its 320 px path box drops under the press in the narrow column
            // rather than being cut off: 223 px of press, 12 and 320 is wider than the column below ~668 px.
            Assert.Contains("var row = new WrapPanel { Orientation = Orientation.Horizontal }; row.Children.Add(copy); row.Children.Add(path);", flat);
            Assert.Contains("Ui.VStack(PanelMatrix.ImportLineGap, flagBoxLine, row);", flat);
            Assert.Contains("copy.Click += (sender, args) => CopyFlagBoxForImport();", flat);
            Assert.Contains("flagBoxPath = path;", flat);
            Assert.DoesNotContain("BuildFlagBoxImportFallback(plan)", matrix);
            // Its words are the shared helper's, copied rather than moved (Profiles.cs is not this page's).
            var profiles = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Profiles.cs"));
            Assert.Contains("\"" + PanelMatrix.ImportCopy + "\"", profiles);
            Assert.Contains("\"" + PanelMatrix.ImportPathTooltip + "\"", profiles);
            Assert.Contains(PanelMatrix.ImportCopyTooltip.Replace("\\", "\\\\"), profiles);
            // The press installs, keeps a failure for the redraw it starts, redraws and says what it did.
            Assert.Contains("var result = InstallFlagBox(); "
                + "matrixFailed = PanelMatrix.KeepsPressResult(result.State) ? result : null; "
                + "matrixFailedAsked = null; "
                + "Redraw(); "
                + "var said = PanelMatrix.InstallSaid(state, result.State, FlagBoxName());", flat);
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
            Assert.DoesNotContain("PanelCopy.LightRow(", matrix);
            // The line as drawn: the state in the dot's ink (ProfileRow's StateHex is not drawn on this page, the
            // dot carries the state), the name as SimHub lists it, the press only where there is one to offer,
            // the message after it, and the hover.
            foreach (var pin in new[]
            {
                "slot == 0 ? null : BuildMatrixSelected(slot, selectedCard));",
                "Fill = Ui.Brush(PanelLightRows.DotHex(state)),",
                "Width = PanelMatrix.ProfileDotSize, Height = PanelMatrix.ProfileDotSize,",
                "var line = Ui.Text(PanelMatrix.ProfileLine(FlagBoxName(), state, version), Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);",
                "var row = Ui.HStack(PanelMatrix.ProfileGap, dot, line); if (PanelMatrix.ProfileHasButton(state)) {",
                "if (plugin.FlagBoxJson == null) return null;",
                "var said = PanelMatrix.InstallSaid(state, result.State, FlagBoxName()); if (said != null) Say(said); if (!string.IsNullOrEmpty(result.Note)) Say(PanelMessage.Info(result.Note));",
                "var hover = PanelMatrix.ProfileLineTooltip(state, version, plan == null ? null : plan.EmbeddedVersion); if (hover != null) row.ToolTip = hover;",
            })
            {
                Assert.Contains(pin, flat);
            }
            Assert.DoesNotContain("StateHex", matrix);
            // A page that asks SimHub while it is built repaints its lighting in place.
            Assert.DoesNotContain("DrawsLighting()", matrix);
            Assert.Contains("OnLighting(() => Ui.Redim(preview,", matrix);
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
            Assert.Equal(new[] { "Both sides", "Left", "Right" }, PanelMatrix.SideLabels);
            Assert.Equal(Contract.FlagBoxSides.Length, PanelMatrix.SideLabels.Length);
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
            Assert.False(PanelMatrix.AddEnabled(4, 1));
            Assert.False(PanelMatrix.AddEnabled(2, 0));
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
                "all.Click += (sender, args) => Open(PanelPage.Rig, PanelMatrix.PreviewScenario(matrixPreviewScenario));",
                // Which idle-display rows show, and the switches that change which do.
                "if (PanelMatrix.ShowsGearRows(rest))",
                "if (PanelMatrix.ShowsCarShiftPoints(rest, bands))",
                "if (PanelMatrix.ShowsRedlineFlash(rest, bands))",
                "Settings.FlagBoxMatrixGearBands[i] = on; Save(); RebuildPage();",
                "Settings.SetMatrixRest(m, value); Save(); RebuildPage();",
                "Settings.FlagBoxSide[i] = value; Save(); RebuildPage();",
                // The segmented controls, each with its own labels.
                "BuildSegmented(Contract.FlagBoxSides, PanelMatrix.SideLabels, Settings.MatrixSide(m), value =>",
                "BuildSegmented(Contract.FlagBoxRests, PanelMatrix.RestLabels, rest, value =>",
                // The chips, each pressed only when it is the one drawn, and a press moves the preview to it.
                // The add press saves at once and selects the new matrix; each sheet draws its body.
                // The cards: which matrix the page opens on, a card never claiming what nothing read, and the
                // empty state only on an empty rig.
                "var device = MatrixLayer(Ui.Soon( MatrixLayerHead(null, PanelSoon.SimHubDevice.Title, null, null, Ui.Button(PanelMatrix.SimHubDeviceButton, PanelButtonKind.Outline, PanelButtonSize.Small)), PanelSoon.SimHubDevice));",
                "foreach (var id in PanelMatrix.PreviewScenarios) { var chosen = id; var chip = Ui.Chip(PanelMatrix.PreviewLabel(chosen), chosen == scenario, () => { matrixPreviewScenario = chosen; repaint(); drawChips(); });",
                "var scenario = PanelMatrix.PreviewScenario(matrixPreviewScenario);",
                "var added = Settings.AddMatrixPanel(name.Text); Save(); if (added != 0) Select(PanelPage.Matrix, PanelMatrix.SlotId(added)); Redraw(); if (added == 0) return;",
                "var body = Ui.VStack(PanelMatrix.SheetGap, Ui.Caption(PanelMatrix.AddPanelCaption(slot, FlagBoxName(), PanelMatrix.StateOf(MatrixPlan()))), Ui.SettingRow(PanelMatrix.NameTitle, name, PanelMatrix.NameCaption)); ShowSheet(PanelMatrix.AddPanel, body, SheetFooter(null, cancel, add));",
                "ShowSheet(PanelMatrix.RenameTitle(current), Ui.SettingRow(PanelMatrix.NameTitle, name, PanelMatrix.NameCaption), SheetFooter(null, cancel, save));",
                "ShowSheet(PanelMatrix.RemoveTitle(name), Ui.Caption(PanelMatrix.RemoveCaption), SheetFooter(null, cancel, remove));",
                "var said = PanelMatrix.RenameSaid(before, PanelMatrix.NameOf(Settings.MatrixName(matrix), matrix)); if (said != null) Say(said);",
                "var shown = facts == null ? null : facts.Shown; var name = PanelMatrix.NameOf(Settings.MatrixName(m), m);",
                "OnLighting(() => Ui.Redim(picture, MatrixDim()));",
                "cards.Add(Ui.MatrixCard(picture, name, PanelMatrix.CardLine(name, m, Settings.MatrixSide(m), shown), PanelMatrix.CardLineHex(shown), m == selected, () => { Select(PanelPage.Matrix, PanelMatrix.SlotId(m)); RebuildPage(); }));",
                "var slot = PanelMatrix.SelectedSlot(panels, Selected(PanelPage.Matrix));",
                "if (panels.Count > 0) return grid; return Ui.VStack(PanelMatrix.EmptyGap, Ui.Caption(PanelMatrix.NoPanels), grid);",
            })
            {
                Assert.Contains(pin, flat);
            }
            // Every sheet's Cancel closes it.
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(flat, System.Text.RegularExpressions.Regex.Escape("cancel.Click += (sender, args) => CloseSheet();")).Count);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(matrix, @"\}, PanelKit\.SegmentedHeightMatrix\);").Count);
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(matrix, @"Ui\.Button\(PanelMatrix\.Cancel, PanelButtonKind\.Ghost, PanelButtonSize\.Large\)").Count);
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
            Assert.Equal("Rename", PanelMatrix.Rename);
            Assert.Equal("Remove", PanelMatrix.Remove);
            Assert.Equal("Rename Left pillar", PanelMatrix.RenameTitle("Left pillar"));
            Assert.Equal("Remove Left pillar", PanelMatrix.RemoveTitle("Left pillar"));
            Assert.Equal("Removes the matrix and its settings. The flag box profile stays in SimHub.", PanelMatrix.RemoveCaption);
            Assert.Equal("Remove it", PanelMatrix.RemoveConfirm);
            Assert.Equal("Removed Left pillar. A device set to Matrix content 2 stays dark.", PanelMatrix.Removed("Left pillar", 2));
            Assert.Equal("Renamed to Pillar.", PanelMatrix.Renamed("Pillar"));
            // A blank name is ignored by the settings, so the press waits for one and says nothing unchanged.
            Assert.False(PanelMatrix.CanRename(""));
            Assert.False(PanelMatrix.CanRename("  "));
            Assert.False(PanelMatrix.CanRename(null));
            Assert.True(PanelMatrix.CanRename("Pillar"));
            Assert.Equal("Renamed to Pillar.", PanelMatrix.RenameSaid("Left pillar", "Pillar"));
            Assert.Null(PanelMatrix.RenameSaid("Left pillar", "Left pillar"));
            Assert.Equal("Name", PanelMatrix.NameTitle);
            Assert.Equal("OpenDash's own label. It is not shown in SimHub's profile list.", PanelMatrix.NameCaption);
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
                { FlagBoxInstallState.UpToDate, "Select \"OpenDash Flag box\" on your matrix's device in SimHub and set Matrix content to 2." },
                { FlagBoxInstallState.Outdated, "Select \"OpenDash Flag box\" on your matrix's device in SimHub and set Matrix content to 2." },
                { FlagBoxInstallState.NotInstalled, "Install OpenDash Flag box at the top of this page, then select it on your matrix's device in SimHub and set Matrix content to 2." },
                { FlagBoxInstallState.Failed, "Install OpenDash Flag box at the top of this page, then select it on your matrix's device in SimHub and set Matrix content to 2." },
                { FlagBoxInstallState.Unavailable, "Import OpenDash Flag box by hand from the top of this page, then select it on your matrix's device in SimHub and set Matrix content to 2." },
                { FlagBoxInstallState.NotEmbedded, "This build ships no flag box profile." },
            };
            foreach (var step in steps)
            {
                Assert.Equal("It will be matrix 2. " + step.Value, PanelMatrix.AddPanelCaption(2, "OpenDash Flag box", step.Key));
                Assert.Equal("Added Left pillar. " + step.Value, PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", step.Key));
            }
            Assert.Equal(Enum.GetValues(typeof(FlagBoxInstallState)).Length, steps.Count);
            Assert.Equal("your matrix's device in SimHub", PanelMatrix.YourDevice);
            Assert.EndsWith("on " + PanelMatrix.YourDevice + ".", PanelMatrix.InstallSaid(FlagBoxInstallState.NotInstalled, FlagBoxInstallState.UpToDate, "P").Text);
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
            Assert.Equal("No device in SimHub shows matrix 2", PanelMatrix.FixTitle(2));
            var steps = PanelMatrix.FixSteps(2, "OpenDash Flag box");
            Assert.Equal(3, steps.Count);
            Assert.Equal(new[] { "Devices", "your matrix" }, steps[0]);
            Assert.Equal(new[] { "Profile: OpenDash Flag box" }, steps[1]);
            Assert.Equal(new[] { "Matrix content: 2" }, steps[2]);
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
            foreach (var pin in new[]
            {
                "var flags = MatrixLayer( MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.FlagsTitle), PanelMatrix.FlagsTitle, null, null, BuildToggle(Settings.MatrixFlags(m), on => { Settings.FlagBoxFlags[i] = on; Save(); repaint(); })), MatrixOption(PanelMatrix.CriticalFlagsOnlyTitle, null, BuildToggle(Settings.MatrixCriticalOnly(m), on => { Settings.FlagBoxMatrixCriticalOnly[i] = on; Save(); repaint(); })));",
                "var pit = MatrixLayer( MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.PitLaneTitle), PanelMatrix.PitLaneTitle, null, null, BuildToggle(Settings.MatrixPit(m), on => { Settings.FlagBoxPit[i] = on; Save(); repaint(); })));",
                "var side = BuildSegmented(Contract.FlagBoxSides, PanelMatrix.SideLabels, Settings.MatrixSide(m), value => { Settings.FlagBoxSide[i] = value; Save(); RebuildPage(); }, PanelKit.SegmentedHeightMatrix);",
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

        [Fact]
        public void The_idle_displays_options_show_only_where_they_act()
        {
            Assert.False(PanelMatrix.ShowsGearRows("dark"));
            Assert.True(PanelMatrix.ShowsGearRows("gear"));
            Assert.False(PanelMatrix.ShowsCarShiftPoints("dark", true));
            Assert.False(PanelMatrix.ShowsCarShiftPoints("gear", false));
            Assert.True(PanelMatrix.ShowsCarShiftPoints("gear", true));
            // The flash is the last shift colour, the car's own included, so it shows with the car's points on.
            Assert.True(PanelMatrix.ShowsRedlineFlash("gear", true));
            Assert.False(PanelMatrix.ShowsRedlineFlash("gear", false));
            Assert.False(PanelMatrix.ShowsRedlineFlash("dark", true));
        }

        /// <summary>The line under Car-specific shift points says what LEDs says (PanelLeds.CarLine), and only
        /// while the switch is on: with no tables on disk it says so and where the download is, rather than that
        /// the car is missing from them.</summary>
        [Fact]
        public void The_car_line_follows_its_switch_and_says_whether_the_tables_have_the_car()
        {
            const string car = "Porsche 911 GT3 R (992)";
            Assert.Equal("Porsche 911 GT3 R (992) is in Lovely Car Data.", PanelMatrix.CarLine(true, car, true, true));
            Assert.Equal("Porsche 911 GT3 R (992) is not in Lovely Car Data.", PanelMatrix.CarLine(true, car, false, true));
            // A fresh install: a new matrix has the switch on, and the tables arrive only from the LEDs page.
            Assert.Equal("Lovely Car Data is not downloaded yet. Download it on the LEDs page, under Every strip.", PanelMatrix.CarTablesMissing);
            Assert.Equal(PanelMatrix.CarTablesMissing, PanelMatrix.CarLine(true, car, false, false));
            Assert.Equal(PanelMatrix.CarTablesMissing, PanelMatrix.CarLine(true, null, false, false));
            Assert.Null(PanelMatrix.CarLine(false, car, true, true));
            Assert.Null(PanelMatrix.CarLine(false, car, false, true));
            Assert.Null(PanelMatrix.CarLine(false, car, false, false));
            Assert.Null(PanelMatrix.CarLine(true, null, false, true));
            Assert.Null(PanelMatrix.CarLine(true, " ", true, true));
            Assert.True(PanelMatrix.CarLineGood(true, true));
            Assert.False(PanelMatrix.CarLineGood(false, true));
            Assert.False(PanelMatrix.CarLineGood(false, false));
            Assert.False(PanelMatrix.CarLineGood(true, false));
            Assert.Equal(Theme.StatusUpToDate, PanelMatrix.CarLineHex(true));
            Assert.Equal(Theme.Caution, PanelMatrix.CarLineHex(false));
            var matrix = MatrixSource();
            Assert.Contains("var tables = plugin.CarLights.CarCount > 0;", matrix);
            Assert.Contains("PanelMatrix.CarLine(Settings.MatrixGearCarLadder(m), live.CarModel, known, tables)", matrix);
            Assert.Contains("carLine.Foreground = Ui.Brush(PanelMatrix.CarLineHex(PanelMatrix.CarLineGood(known, tables)));", matrix);
            Assert.Contains("Settings.FlagBoxMatrixGearCarLadder[i] = on; Save(); readCar();", matrix);
            Assert.Contains("OnTick(readCar);", matrix);
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
            Assert.Equal("All devices at once", PanelMatrix.AllDevices);
            var bandsOn = new MatrixOptions { Rest = "gear", Bands = true };
            var bandsOff = new MatrixOptions { Rest = "gear", Bands = false };
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, bandsOn));
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, bandsOff));
            // Shift colours can be seen: under the revs chip the gear takes its first shift colour with the
            // switch on and stays white with it off; on a matrix that rests dark the chip draws the idle display.
            Assert.Equal(PanelMatrix.RevsScenario, PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, bandsOn, false));
            Assert.Equal("Gear 4 stage1", PanelEmulation.GlyphFor(PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, bandsOn, false), bandsOn));
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, bandsOff, false), bandsOff));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(PanelMatrix.RevsScenario, new MatrixOptions { Rest = "dark", Bands = true }, false));

            // What a screen reader is told the preview draws: the artboard's alt text, as the matrix leaves it.
            var gear = new MatrixOptions { Rest = "gear" };
            var dark = new MatrixOptions { Rest = "dark" };
            Assert.Equal("Gear 4", PanelMatrix.PreviewAlt(PanelMatrix.IdleScenario, gear));
            Assert.Equal("Gear 4", PanelMatrix.PreviewAlt(PanelMatrix.RevsScenario, gear));
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

            // A family switched off leaves the matrix at its idle display under its chip, and only under its chip.
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
                    var drawn = PanelMatrix.DrawnScenario(chip.Value, read, false);
                    if (chip.Key == family) Assert.Equal(PanelMatrix.IdleScenario, drawn);
                    else Assert.Equal(chip.Value, drawn);
                    var glyph = PanelEmulation.GlyphFor(drawn, read);
                    if (chip.Key == family) Assert.Equal(PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, read), glyph);
                    else Assert.NotEqual(PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, read), glyph);
                }
            }
            // A car on the side the matrix is not mounted on leaves it at rest too.
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(PanelEmulation.CarLeft, new MatrixOptions { Side = "right" }, false));
            Assert.Equal(PanelEmulation.CarLeft, PanelMatrix.DrawnScenario(PanelEmulation.CarLeft, new MatrixOptions { Side = "left" }, false));

            // Critical flags only drops the news, the chequer among the chips, and keeps the warnings.
            Assert.Equal(PanelEmulation.Chequer, PanelMatrix.DrawnScenario(PanelEmulation.Chequer, new MatrixOptions(), false));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(PanelEmulation.Chequer, new MatrixOptions(), true));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(PanelEmulation.White, new MatrixOptions(), true));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(PanelEmulation.Green, new MatrixOptions(), true));
            foreach (var critical in new[] { PanelEmulation.Yellow, PanelEmulation.Blue, PanelEmulation.Red, PanelEmulation.Black })
            {
                Assert.Equal(critical, PanelMatrix.DrawnScenario(critical, new MatrixOptions(), true));
                Assert.False(PanelMatrix.IsNewsFlag(critical));
            }
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.DrawnScenario(PanelMatrix.IdleScenario, new MatrixOptions(), true));

            var source = MatrixSource();
            Assert.Contains("PanelMatrix.DrawnScenario(PanelMatrix.PreviewScenario(matrixPreviewScenario), PanelMatrix.OptionsFor(Settings, matrix), Settings.MatrixCriticalOnly(matrix))", source);
            Assert.Contains("Settings.FlagBoxMatrixCriticalOnly[i] = on; Save(); repaint();", source);
            Assert.Contains("Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), PanelMatrix.OptionsFor(Settings, m)), MatrixStyle.Preview, MatrixDim());", source);
            Assert.Contains("MatrixRepaint(preview, PanelEmulation.MatrixFrame(GlyphSheet, MatrixDrawn(m), options), MatrixStyle.Preview);", source);
            Assert.Contains("Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, PanelMatrix.IdleScenario, PanelMatrix.OptionsFor(Settings, m)), MatrixStyle.Card, MatrixDim());", source);
            Assert.Contains("MatrixRepaint(cardPicture, PanelEmulation.MatrixFrame(GlyphSheet, PanelMatrix.IdleScenario, options), MatrixStyle.Card);", source);
            Assert.Contains("Open(PanelPage.Rig, PanelMatrix.PreviewScenario(matrixPreviewScenario))", source);
            // The preview is named for what it draws, when it is built and on every repaint.
            Assert.Contains("AutomationProperties.SetName(preview, PanelMatrix.PreviewAlt(MatrixDrawn(m), PanelMatrix.OptionsFor(Settings, m)));", source);
            Assert.Contains("AutomationProperties.SetName(preview, PanelMatrix.PreviewAlt(MatrixDrawn(m), options));", source);
            Assert.Contains("AutomationProperties.SetName(chips, PanelMatrix.PreviewChipsName);", source);
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
                "slot.Margin = new Thickness(PanelMatrix.HeaderGap, 0, 0, PanelMatrix.SlotCaptionLift);",
                "var rename = Ui.Button(PanelMatrix.Rename, PanelButtonKind.Outline, PanelButtonSize.Small); rename.ToolTip = PanelMatrix.RenameTooltip; rename.Click += (sender, args) => ShowRenameMatrix(m);",
                "var remove = Ui.Button(PanelMatrix.Remove, PanelButtonKind.GhostDanger, PanelButtonSize.Small); remove.ToolTip = PanelMatrix.RemoveTooltip; remove.Click += (sender, args) => ShowRemoveMatrix(m);",
                "var head = Ui.Row(heading, Ui.HStack(PanelMatrix.ActionGap, rename, remove));",
                "var fix = Ui.FixBox(PanelMatrix.FixTitle(m), null, PanelMatrix.FixSteps(m, FlagBoxName()), check);",
                "var all = Ui.LinkButton(PanelMatrix.AllDevices);",
                "var thresholds = Ui.LinkButton(PanelMatrix.ThresholdsLink);",
                "body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelMatrix.PreviewColumnWidth) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelMatrix.BodyGap) });",
                "previewColumn.Element.MaxWidth = PanelMatrix.PreviewColumnWidth;",
                "Padding = new Thickness(0, PanelMatrix.SectionPaddingTop, 0, 0), Child = Ui.VStack(PanelMatrix.SectionGap, parts.ToArray()),",
                "Padding = new Thickness(PanelMatrix.PreviewFramePadding),",
                "chip.Margin = new Thickness(0, 0, PanelMatrix.ChipGap, PanelMatrix.ChipGap);",
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
            })
            {
                Assert.Contains(pin, flatNumbers);
            }
            var matrix = MatrixSource();
            Assert.Contains("MatrixStyle.Preview", matrix);
            // The New tag has the frame's own line, over it and clear of the lamps, never after the Rig link.
            Assert.Contains("var tagged = Ui.VStack(PanelMatrix.NewTagGap, tag, frame);", matrix);
            Assert.Contains("Ui.VStack(PanelMatrix.PreviewGap, tagged, all, chips)", matrix);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(matrix, @"Ui\.NewTag\(\)"));
            Assert.DoesNotContain("all, Ui.NewTag()", matrix);
            Assert.Contains("Child = preview,", matrix);
            // Idle display is unranked, drawn with the artboard's dot; the device row has no rank column.
            Assert.Contains("MatrixLayerHead(PanelMatrix.Rank(PanelMatrix.IdleDisplayTitle), PanelMatrix.IdleDisplayTitle,", matrix);
            Assert.Contains("Width = PanelMatrix.UnrankedDotSize,", matrix);
            Assert.Contains("words.Margin = new Thickness(PanelMatrix.OptionIndent, 0, 0, 0);", matrix);
            Assert.Contains("Padding = new Thickness(PanelMatrix.OptionIndent, PanelMatrix.OptionPaddingY, 0, PanelMatrix.OptionPaddingY),", matrix);
            // The selected matrix's name trims inside its column and its number wraps under it.
            Assert.Contains("var heading = new WrapPanel { Orientation = Orientation.Horizontal", matrix);
            Assert.DoesNotContain("new StackPanel { Orientation = Orientation.Horizontal }", matrix);
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
