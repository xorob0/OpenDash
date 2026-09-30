// PanelMatrixTests.cs: the Matrix page's words, numbers and decisions, which SettingsControl.Matrix.cs draws
// and no test can build: the rows and their order, which rows show, the profile's line, the cards, the
// preview's chips, the messages after a press, and what search lists.
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
            Assert.Equal("Pit limiter and speeding.", PanelMatrix.PitLaneCaption);
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
            Assert.Equal("Stays dark for the chequer, white, green and start gantry.", PanelMatrix.CriticalFlagsOnlyCaption);
            Assert.Equal("Shift colours", PanelMatrix.ShiftColoursTitle);
            Assert.Equal("Redline flash", PanelMatrix.RedlineFlashTitle);
            // Shift points, not thresholds, which is the settings model's word; and "Car-specific", the name
            // voice.md gives the car's own tables.
            Assert.Equal("Car-specific shift points", PanelMatrix.CarShiftPointsTitle);
            Assert.DoesNotContain("threshold", PanelMatrix.CarShiftPointsTitle, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Choose a device", PanelMatrix.SimHubDeviceButton);
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
            Assert.Equal("No strips yet.", PanelLeds.NoStrips);
            var home = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Home.cs"));
            Assert.Contains("PanelLeds.NoStrips", home);
            Assert.Contains("PanelMatrix.NoPanels", home);
            foreach (var text in new[]
            {
                PanelMatrix.NoPanels, PanelMatrix.PanelsTitle, PanelMatrix.AddPanel, PanelMatrix.AllInUse, PanelMatrix.AddTooltip,
                PanelMatrix.RenameTooltip, PanelMatrix.RemoveTooltip, PanelMatrix.RemoveCaption, PanelMatrix.AddPanelCaption(2, "OpenDash Flag box"),
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
            Assert.Equal(Theme.StatusFailed, PanelMatrix.ProfileRow(FlagBoxInstallState.Failed, null).StateHex);
            Assert.Equal("Not installed", PanelMatrix.ProfileRow(FlagBoxInstallState.NotInstalled, null).State);
            Assert.Equal("Install", PanelMatrix.ProfileRow(FlagBoxInstallState.NotInstalled, null).Button);

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

            Assert.Equal("Installed \"OpenDash Flag box\". Select it on each matrix device in SimHub.", PanelMatrix.InstallSaid(FlagBoxInstallState.UpToDate, "OpenDash Flag box").Text);
            Assert.Equal(PanelTone.Danger, PanelMatrix.InstallSaid(FlagBoxInstallState.Failed, "OpenDash Flag box").Tone);
            Assert.Equal("Install failed. See SimHub's log.", PanelMatrix.InstallSaid(FlagBoxInstallState.Failed, "x").Text);
            Assert.Null(PanelMatrix.InstallSaid(FlagBoxInstallState.NotEmbedded, "x"));

            var matrix = MatrixSource();
            Assert.Contains("PanelMatrix.ProfileRow(", matrix);
            Assert.DoesNotContain("PanelCopy.LightRow(", matrix);
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

            Assert.Equal("Matrix 1 · Both sides", PanelMatrix.CardLine(1, "both", null));
            Assert.Equal("Matrix 2 · Left", PanelMatrix.CardLine(2, "left", null));
            Assert.Equal("Matrix 2 · Both sides", PanelMatrix.CardLine(2, "nonsense", null));
            Assert.Equal("Showing", PanelMatrix.CardLine(1, "both", true));
            Assert.Equal("Not shown in SimHub", PanelMatrix.CardLine(2, "left", false));
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
            Assert.Equal("Its settings go with it. The flag box profile stays in SimHub.", PanelMatrix.RemoveCaption);
            Assert.Equal("Removed Left pillar. A device set to matrix content 2 stays dark.", PanelMatrix.Removed("Left pillar", 2));
            Assert.Equal("Renamed to Pillar.", PanelMatrix.Renamed("Pillar"));
            Assert.Equal("Name", PanelMatrix.NameTitle);
            Assert.Equal("OpenDash's own label. It is not shown in SimHub's profile list.", PanelMatrix.NameCaption);
            // Remove is a sheet with one press; Rename is a sheet; neither acts on the first press.
            var matrix = MatrixSource();
            Assert.Contains("ShowSheet(PanelMatrix.RemoveTitle(name),", matrix);
            Assert.Contains("ShowSheet(PanelMatrix.RenameTitle(current),", matrix);
            Assert.Contains("remove.Click += (sender, args) => ShowRemoveMatrix(m);", matrix);
        }

        [Fact]
        public void Adding_says_the_steps_left_on_the_device()
        {
            Assert.Equal("It will be matrix 2. On the device, select \"OpenDash Flag box\" and set Matrix content to 2.",
                PanelMatrix.AddPanelCaption(2, "OpenDash Flag box"));
            Assert.Equal("Added Left pillar. On the device, select \"OpenDash Flag box\" and set Matrix content to 2.",
                PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", FlagBoxInstallState.UpToDate));
            Assert.Equal(PanelMatrix.PanelAdded("Left pillar", 2, "P", FlagBoxInstallState.UpToDate), PanelMatrix.PanelAdded("Left pillar", 2, "P", FlagBoxInstallState.Outdated));
            Assert.Equal("Added Left pillar. Install \"OpenDash Flag box\" at the top of this page, then select it on the device and set Matrix content to 2.",
                PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", FlagBoxInstallState.NotInstalled));
            Assert.Equal(PanelMatrix.PanelAdded("A", 2, "P", FlagBoxInstallState.NotInstalled), PanelMatrix.PanelAdded("A", 2, "P", FlagBoxInstallState.Failed));
            Assert.Equal("Added Left pillar. Import \"OpenDash Flag box\" by hand from the top of this page, then set Matrix content to 2 on the device.",
                PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", FlagBoxInstallState.Unavailable));
            Assert.Equal("Added Left pillar. This build has no flag box profile to install, so it stays dark.",
                PanelMatrix.PanelAdded("Left pillar", 2, "OpenDash Flag box", FlagBoxInstallState.NotEmbedded));
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
        /// while the switch is on and a car is loaded.</summary>
        [Fact]
        public void The_car_line_follows_its_switch_and_says_whether_the_tables_have_the_car()
        {
            Assert.Equal("Porsche 911 GT3 R (992) is in Lovely Car Data.", PanelMatrix.CarLine(true, "Porsche 911 GT3 R (992)", true));
            Assert.Equal("Porsche 911 GT3 R (992) is not in Lovely Car Data.", PanelMatrix.CarLine(true, "Porsche 911 GT3 R (992)", false));
            Assert.Null(PanelMatrix.CarLine(false, "Porsche 911 GT3 R (992)", true));
            Assert.Null(PanelMatrix.CarLine(false, "Porsche 911 GT3 R (992)", false));
            Assert.Null(PanelMatrix.CarLine(true, null, false));
            Assert.Null(PanelMatrix.CarLine(true, " ", true));
            Assert.Equal(Theme.StatusUpToDate, PanelMatrix.CarLineHex(true));
            Assert.Equal(Theme.Caution, PanelMatrix.CarLineHex(false));
            var matrix = MatrixSource();
            Assert.Contains("PanelMatrix.CarLine(Settings.MatrixGearCarLadder(m), live.CarModel, known)", matrix);
            Assert.Contains("carLine.Foreground = Ui.Brush(PanelMatrix.CarLineHex(known));", matrix);
            Assert.Contains("Settings.FlagBoxMatrixGearCarLadder[i] = on; Save(); readCar();", matrix);
            Assert.Contains("OnTick(readCar);", matrix);
        }

        [Fact]
        public void The_preview_draws_the_matrix_as_its_settings_would()
        {
            Assert.Equal(new[] { "Idle display", "Yellow", "Blue", "Pit limiter", "Car left", "Low fuel", "Chequered" },
                PanelMatrix.PreviewScenarios.Select(PanelMatrix.PreviewLabel));
            // The box at rest: the gear in its one white whatever Shift colours says, as Home and Rig's Idle draw it.
            Assert.Equal(PanelEmulation.Idle, PanelMatrix.IdleScenario);
            Assert.Equal("Idle display", PanelMatrix.PreviewLabel(PanelMatrix.IdleScenario));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.PreviewScenario(null));
            Assert.Equal(PanelMatrix.IdleScenario, PanelMatrix.PreviewScenario(PanelEmulation.Oil));
            Assert.Equal(PanelEmulation.Yellow, PanelMatrix.PreviewScenario(PanelEmulation.Yellow));
            Assert.Equal("All devices at once", PanelMatrix.AllDevices);
            var bandsOn = new MatrixOptions { Rest = "gear", Bands = true };
            var bandsOff = new MatrixOptions { Rest = "gear", Bands = false };
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, bandsOn));
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelMatrix.IdleScenario, bandsOff));

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
            foreach (var write in new[] { "Settings.FlagBoxFlags[i] = on; Save(); repaint();", "Settings.FlagBoxPit[i] = on; Save(); repaint();",
                "Settings.FlagBoxSpotter[i] = on; Save(); repaint();", "Settings.FlagBoxWarnings[i] = on; Save(); repaint();" })
            {
                Assert.Contains(write, source);
            }

            // The artboard's numbers.
            Assert.Equal(300, PanelMatrix.PreviewColumnWidth);
            Assert.Equal(36, PanelMatrix.BodyGap);
            Assert.Equal(20, PanelMatrix.PreviewFramePadding);
            Assert.Equal(14, PanelMatrix.PreviewGap);
            Assert.Equal(6, PanelMatrix.ChipGap);
            // Without the artboard's grips an option starts where its layer's name does, the rank's 14 and 12 in.
            Assert.Equal(26, PanelMatrix.OptionIndent);
            Assert.Equal(PanelMatrix.RankWidth + PanelMatrix.LayerGap, PanelMatrix.OptionIndent);
            Assert.Equal(4, PanelMatrix.UnrankedDotSize);
            Assert.Equal(12, PanelMatrix.NewTagGap);
            Assert.Equal(4, PanelMatrix.SlotCaptionLift);
            Assert.Equal(7, PanelMatrix.OptionPaddingY);
            Assert.Equal(12, PanelMatrix.LayerPaddingY);
            Assert.Equal(14, PanelMatrix.RankWidth);
            Assert.Equal(20, PanelMatrix.SectionPaddingTop);
            Assert.Equal(18, PanelMatrix.SectionGap);
            var matrix = MatrixSource();
            Assert.Contains("MatrixStyle.Preview", matrix);
            // The New tag follows the line under the frame, clear of the lamps.
            Assert.Contains("var links = Ui.HStack(PanelMatrix.NewTagGap, all, Ui.NewTag());", matrix);
            Assert.Contains("Ui.VStack(PanelMatrix.PreviewGap, frame, links, chips)", matrix);
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
                FlagBoxProfile.ProfileName, PanelMatrix.PanelsTitle, PanelMatrix.AddPanel, PanelMatrix.PriorityTitle,
                PanelMatrix.FlagsTitle, PanelMatrix.CriticalFlagsOnlyTitle, PanelMatrix.PitLaneTitle, PanelMatrix.SpotterTitle,
                PanelMatrix.MountingSideTitle, PanelMatrix.SpotterAnimationTitle, PanelMatrix.WarningsTitle,
                PanelMatrix.IdleDisplayTitle, PanelMatrix.ShiftColoursTitle, PanelMatrix.CarShiftPointsTitle, PanelMatrix.RedlineFlashTitle,
            })
            {
                Assert.Contains(title, labels);
            }
            Assert.Equal(labels.Count, labels.Distinct().Count());
            var matrix = MatrixSource();
            foreach (var name in new[]
            {
                "PanelMatrix.PanelsTitle", "PanelMatrix.AddPanel", "PanelMatrix.PriorityTitle", "PanelMatrix.FlagsTitle",
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
