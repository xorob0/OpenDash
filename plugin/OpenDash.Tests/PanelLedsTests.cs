// PanelLedsTests.cs: the LEDs page's decisions and words -- what a card says, what the preview draws, which
// effect switches a shape carries, the Add LEDs sheet, the lines after a press, and where search sends a
// driver. SettingsControl.Lights.cs draws these, so this is where the page is held: every rule the page
// follows (what the switch writes, when Live runs, when a rename reinstalls, what the car line says) is a
// PanelLeds function pinned here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelLedsTests
    {
        [Fact]
        public void The_leds_page_is_titled_and_its_rows_are_named_as_the_artboard_and_voice_md_have_them()
        {
            Assert.Equal("LEDs", PanelLeds.Title);
            Assert.Equal("No strips yet.", PanelLeds.NoStrips);
            // A switch names the thing: the artboard's "Use the car's own rev lights" is the departure
            // docs/design/plugin.md records (#369).
            Assert.Equal("Car's own rev lights", PanelLeds.CarRevLightsTitle);
            Assert.Equal("Off fills the strip left to right.", PanelLeds.CarRevLightsCaption);
            Assert.Equal("Rev lights", PanelLeds.RevLightsTitle);
            // voice.md's own example: "Centre display", never the artboard's "Centre shows".
            Assert.Equal("Centre display", PanelLeds.CentreDisplayTitle);
            // One noun with the switch: the artboard's bare "Width" says nothing in a search result.
            Assert.Equal("Rev light width", PanelLeds.MirrorFitTitle);
            Assert.Equal("Every strip.", PanelLeds.MirrorFitCaption);
            Assert.Equal("This strip", PanelLeds.ThisStripTitle);
            Assert.Equal("Brightness", PanelLeds.BrightnessTitle);
            Assert.Equal("Reverse direction", PanelLeds.ReverseTitle);
            Assert.Equal("Effects", PanelLeds.EffectsTitle);
            Assert.Equal("This strip only", PanelLeds.EffectsCaption);
            // Labels are noun phrases: not "Flags animated", not "Spotter uses the whole strip".
            Assert.Equal("Flag animation", PanelLeds.FlagAnimationTitle);
            Assert.Equal("Full-strip spotter", PanelLeds.SpotterTitle);
            Assert.Equal("Lights the whole strip for a car alongside, instead of just the end nearest it.", PanelLeds.SpotterCaption);
            Assert.Equal("Every strip", PanelLeds.EveryStripTitle);
            Assert.Equal("All devices at once", PanelLeds.AllDevicesAtOnce);
            Assert.Equal("Installed, but not selected in SimHub", PanelLeds.NotSelectedTitle);
            Assert.Equal(new[] { "Ends", "Whole strip" }, PanelLeds.PitLimiterLightsLabels);
            Assert.Equal(PanelLeds.PitLimiterLightsLabels.Length, PanelLeds.PitLimiterLightsValues.Length);
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorBrightness = leds.brightness",
                "AnchorCarTables = leds.car-tables",
                "AnchorCentre = leds.centre",
                "AnchorDevice = leds.device",
                "AnchorEffects = leds.effects",
                "AnchorEveryStrip = leds.every-strip",
                "AnchorFlagAnimation = leds.flag-animation",
                "AnchorMirrorFit = leds.mirror-fit",
                "AnchorPreview = leds.preview",
                "AnchorRevLights = leds.rev-lights",
                "AnchorRevStyle = leds.rev-style",
                "AnchorReverse = leds.reverse",
                "AnchorSpotter = leds.spotter",
                "AnchorStrips = leds.strips",
                "AnchorThisStrip = leds.this-strip",
            }, AnchorTable.Of(typeof(PanelLeds)));
        }

        /// <summary>Every row label and heading the page draws is found, and lands on its row; the effects,
        /// drawn from a list, are found too and land on the grid.</summary>
        [Fact]
        public void Every_row_and_heading_is_found_where_it_is()
        {
            var expected = new Dictionary<string, string>
            {
                { PanelLights.AddBar, PanelLeds.AnchorStrips },
                { PanelLeds.RevLightsTitle, PanelLeds.AnchorRevLights },
                { PanelLeds.CarRevLightsTitle, PanelLeds.AnchorRevStyle },
                { PanelLeds.MirrorFitTitle, PanelLeds.AnchorMirrorFit },
                { PanelLeds.CentreDisplayTitle, PanelLeds.AnchorCentre },
                { PanelLeds.ThisStripTitle, PanelLeds.AnchorThisStrip },
                { PanelLights.BarDeviceTitle, PanelLeds.AnchorDevice },
                { PanelLeds.BrightnessTitle, PanelLeds.AnchorBrightness },
                { PanelLeds.ReverseTitle, PanelLeds.AnchorReverse },
                { PanelLeds.EffectsTitle, PanelLeds.AnchorEffects },
                { PanelLeds.FlagAnimationTitle, PanelLeds.AnchorFlagAnimation },
                { PanelLeds.SpotterTitle, PanelLeds.AnchorSpotter },
                { PanelLeds.EveryStripTitle, PanelLeds.AnchorEveryStrip },
                { PanelLights.CarTablesTitle, PanelLeds.AnchorCarTables },
            };
            foreach (var effect in PanelLeds.Effects) expected.Add(effect.Label, PanelLeds.AnchorEffects);
            Assert.Equal(expected.Count, PanelLeds.Search.Length);
            foreach (var pair in expected)
            {
                Assert.Contains(PanelLeds.Search, entry => entry.Label == pair.Key && entry.Route.Page == PanelPage.Leds && entry.Route.Anchor == pair.Value);
            }
            Assert.All(PanelLeds.Search, entry => Assert.StartsWith("leds.", entry.Route.Anchor, StringComparison.Ordinal));
            // The effects are drawn from each switch's Label, which the page's source says.
            Assert.Equal(PanelLeds.Effects.Select(effect => effect.Label).OrderBy(l => l, StringComparer.Ordinal),
                PanelLeds.SearchDrawnOtherwise.Keys.OrderBy(l => l, StringComparer.Ordinal));
            Assert.All(PanelLeds.SearchDrawnOtherwise.Values, snippet => Assert.Equal("effect.Label", snippet));
            // The old names still find the rows.
            Assert.NotEmpty(PanelSearch.Find(PanelLeds.Search, "car light tables"));
            Assert.NotEmpty(PanelSearch.Find(PanelLeds.Search, "LED device"));
            Assert.NotEmpty(PanelSearch.Find(PanelLeds.Search, "your LED strips"));
            Assert.NotEmpty(PanelSearch.Find(PanelLeds.Search, "true size"));
            Assert.NotEmpty(PanelSearch.Find(PanelLeds.Search, "fill the strip"));
            // And the artboard's words, where the build keeps voice.md's.
            Assert.NotEmpty(PanelSearch.Find(PanelLeds.Search, "add leds"));
            Assert.NotEmpty(PanelSearch.Find(PanelLeds.Search, "use the car's own rev lights"));
        }

        [Fact]
        public void The_greyed_rows_are_the_five_the_registry_gives_the_page()
        {
            Assert.Equal(
                PanelSoon.For(PanelPage.Leds).Select(item => item.Anchor).OrderBy(a => a, StringComparer.Ordinal),
                PanelLeds.SoonDrawn.Select(item => item.Anchor).OrderBy(a => a, StringComparer.Ordinal));
            Assert.Equal(new[] { 434, 509, 485, 300, 479 }, PanelLeds.SoonDrawn.Select(item => item.Ticket));
        }

        // --- The cards and the header ------------------------------------------------------------------------

        [Theory]
        [InlineData("3-9-3", "3 · 9 · 3")]
        [InlineData("3-9-3-fanatec", "3 · 9 · 3")]
        [InlineData("4-14-4-reversed", "4 · 14 · 4")]
        [InlineData("0-15-0", "15")]
        [InlineData("brow-15", "15")]
        public void A_shape_is_written_with_spaced_dots(string shapeId, string dots)
        {
            Assert.Equal(dots, PanelLeds.ShapeDots(shapeId));
        }

        [Fact]
        public void A_shape_it_cannot_read_is_written_as_the_Updates_page_writes_it()
        {
            Assert.Equal(PanelLightRows.ShapeLabel("mystery"), PanelLeds.ShapeDots("mystery"));
            Assert.Equal(0, PanelLeds.Ends("mystery"));
            Assert.Equal(0, PanelLeds.Centre("mystery"));
        }

        [Fact]
        public void The_header_chip_names_the_hardware_before_the_shape()
        {
            Assert.Equal("Fanatec wheel", PanelLeds.Hardware(PanelLights.FanatecShapeId));
            Assert.Equal("Strip", PanelLeds.Hardware("0-15-0"));
            Assert.Equal("Strip", PanelLeds.Hardware("4-14-4-reversed"));
            Assert.Equal("Fanatec wheel · ", PanelLeds.HardwareLead("3-9-3-fanatec"));
            Assert.Equal(3, PanelLeds.Ends("3-9-3-fanatec"));
            Assert.Equal(9, PanelLeds.Centre("3-9-3-fanatec"));
            Assert.Equal(0, PanelLeds.Ends("0-15-0"));
            Assert.Equal(15, PanelLeds.Centre("0-15-0"));
        }

        /// <summary>A card says one thing about its profile, in the order a driver acts on it, and nothing where
        /// SimHub could not be asked.</summary>
        [Theory]
        [InlineData(null, null, null)]
        [InlineData(FlagBoxInstallState.Unavailable, null, null)]
        [InlineData(FlagBoxInstallState.NotInstalled, null, "Not installed")]
        [InlineData(FlagBoxInstallState.Failed, null, "Not installed")]
        [InlineData(FlagBoxInstallState.NotEmbedded, null, "Not installed")]
        [InlineData(FlagBoxInstallState.UpToDate, true, "Showing")]
        [InlineData(FlagBoxInstallState.UpToDate, false, "Not selected in SimHub")]
        [InlineData(FlagBoxInstallState.UpToDate, null, "Installed")]
        [InlineData(FlagBoxInstallState.Outdated, true, "Update available")]
        [InlineData(FlagBoxInstallState.Outdated, null, "Update available")]
        [InlineData(FlagBoxInstallState.Outdated, false, "Not selected in SimHub")]
        public void A_card_says_what_SimHub_shows(FlagBoxInstallState? profile, bool? selected, string state)
        {
            Assert.Equal(state, PanelLeds.StateText(profile, selected));
        }

        [Fact]
        public void The_state_is_green_while_it_shows_and_amber_where_there_is_something_to_do()
        {
            Assert.Equal(Theme.StatusUpToDate, PanelLeds.StateHex(FlagBoxInstallState.UpToDate, true));
            Assert.Equal(Theme.Caution, PanelLeds.StateHex(FlagBoxInstallState.UpToDate, false));
            Assert.Equal(Theme.StatusUpdateAvailable, PanelLeds.StateHex(FlagBoxInstallState.Outdated, true));
            Assert.Equal(Theme.TextSecondary, PanelLeds.StateHex(FlagBoxInstallState.NotInstalled, null));
            Assert.Equal(Theme.TextSecondary, PanelLeds.StateHex(FlagBoxInstallState.UpToDate, null));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(FlagBoxInstallState.Unavailable, null)]
        [InlineData(FlagBoxInstallState.NotEmbedded, null)]
        [InlineData(FlagBoxInstallState.UpToDate, null)]
        [InlineData(FlagBoxInstallState.NotInstalled, "Install")]
        [InlineData(FlagBoxInstallState.Failed, "Install")]
        [InlineData(FlagBoxInstallState.Outdated, "Update")]
        public void The_header_offers_the_press_the_profile_needs(FlagBoxInstallState? profile, string press)
        {
            Assert.Equal(press, PanelLeds.ProfileAction(profile));
            Assert.Equal(press, PanelLeds.ProfileAction(profile, true, true));
        }

        /// <summary>
        /// No press that could only fail: where the build carries no profile for the strip, or SimHub does not
        /// list its device, the header offers neither Install nor Update, whatever the census says, and says why.
        /// </summary>
        /// <remarks>
        /// A pre-grid brow compared against no embedded profile reads Outdated, and an install for a device
        /// SimHub no longer lists takes the working copy out of every device and puts nothing back.
        /// </remarks>
        [Theory]
        [InlineData(FlagBoxInstallState.Outdated)]
        [InlineData(FlagBoxInstallState.NotInstalled)]
        [InlineData(FlagBoxInstallState.Failed)]
        [InlineData(FlagBoxInstallState.UpToDate)]
        public void The_header_offers_no_press_it_cannot_carry_out(FlagBoxInstallState profile)
        {
            Assert.Null(PanelLeds.ProfileAction(profile, false, true));
            Assert.Null(PanelLeds.ProfileAction(profile, true, false));
            Assert.Null(PanelLeds.ProfileAction(profile, false, false));
        }

        [Fact]
        public void The_header_says_why_it_offers_no_press()
        {
            Assert.Null(PanelLeds.ProfileBlocked(true, true));
            Assert.Equal("This build ships no profile for this strip.", PanelLeds.ProfileBlocked(false, true));
            // The build comes first: a device picked would not bring a profile the build lacks.
            Assert.Equal(PanelLeds.NoProfileForStrip, PanelLeds.ProfileBlocked(false, false));
            Assert.Equal("SimHub does not list this strip's device. Choose one under SimHub device.", PanelLeds.ProfileBlocked(true, false));
            Assert.Contains(PanelLights.BarDeviceTitle, PanelLeds.DeviceNotListed);
        }

        /// <summary>
        /// The page's presses go through one guarded install, UpdateBars' own guard, and never straight to
        /// ReinstallBar or InstallBar: read as text because the page is WPF.
        /// </summary>
        [Fact]
        public void Every_install_on_the_page_is_guarded()
        {
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.DoesNotContain("ReinstallBar(", leds);
            var guard = leds.Substring(leds.IndexOf("private static FlagBoxPlan LedsReinstall(", StringComparison.Ordinal));
            guard = guard.Substring(0, guard.IndexOf("return LedsLogged(bar, InstallBar(bar, found.Json));", StringComparison.Ordinal));
            Assert.Contains("var found = EmbeddedProfileOf(bar);", guard);
            Assert.Contains("if (found == null)", guard);
            Assert.Contains("if (LedTargets.Find(bar.Device) == null)", guard);
            // InstallBar is called in two places only: the guard, and a move, which installs on the device just
            // picked from SimHub's own list.
            Assert.Equal(2, Occurrences(leds, "InstallBar(bar, found.Json)"));
            Assert.Contains("PanelLeds.ProfileBlocked(EmbeddedProfileOf(bar) != null, LedTargets.Find(bar.Device) != null)", leds);
            Assert.Contains("var action = blocked == null ? PanelLeds.ProfileAction(profile) : null;", leds);
        }

        private static int Occurrences(string text, string part)
        {
            var count = 0;
            for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
            return count;
        }

        // --- The preview --------------------------------------------------------------------------------------

        [Fact]
        public void The_preview_chips_are_the_artboards_in_its_order()
        {
            Assert.Equal(new[] { "Live", "Shift point", "Yellow", "Blue", "Car left", "Pit limiter" }, PanelLeds.Scenarios.Select(s => s.Value));
            Assert.Equal(PanelLeds.LiveScenario, PanelLeds.Scenarios[0].Key);
            // Every chip but Live is one of the emulation's own moments, drawn by its rules.
            Assert.All(PanelLeds.Scenarios.Skip(1), s => Assert.NotNull(PanelEmulation.Find(s.Key)));
        }

        [Fact]
        public void The_preview_names_each_group_under_it()
        {
            Assert.Equal(new[] { "Left · 3", "Centre · 9", "Right · 3" }, PanelLeds.PreviewLabels(3, 9));
            Assert.Equal(new[] { "15 LEDs" }, PanelLeds.PreviewLabels(0, 15));
        }

        /// <summary>Live is the car's own run through PanelEmulation.LiveFrame while it is what the strip draws,
        /// and the strip at rest otherwise; never a parser of the page's own.</summary>
        [Fact]
        public void Live_is_the_cars_run_while_it_runs_and_the_strip_at_rest_otherwise()
        {
            var packed = string.Concat(Enumerable.Repeat("#FF00D96A", 9));
            var options = PanelLeds.OptionsFor(false, null);
            var live = PanelLeds.PreviewFrame(PanelLeds.LiveScenario, 3, 9, options, true, packed);
            Assert.Equal(3, live.Length);
            Assert.All(live[1], colour => Assert.Equal("#FF00D96A", colour));
            Assert.All(live[0], c => Assert.Null(c));
            Assert.Equal(PanelEmulation.LiveFrame(packed, 3, 9), live);

            var rest = PanelLeds.PreviewFrame(PanelLeds.LiveScenario, 3, 9, options, false, packed);
            Assert.All(rest.SelectMany(group => group), c => Assert.Null(c));
        }

        [Fact]
        public void The_other_chips_are_the_emulations_rules_with_the_strips_own_switches()
        {
            var yellow = PanelLeds.PreviewFrame(PanelEmulation.Yellow, 3, 9, PanelLeds.OptionsFor(false, null), false, null);
            Assert.All(yellow[0], colour => Assert.Equal(Theme.FlagYellow, colour));
            // A strip with its flags switched off does not draw a yellow.
            var off = PanelLeds.PreviewFrame(PanelEmulation.Yellow, 3, 9, PanelLeds.OptionsFor(false, new[] { "flag.black" }), false, null);
            Assert.All(off[0], c => Assert.Null(c));
            Assert.Equal(PanelEmulation.StripFrame(0, 15, PanelEmulation.Shift), PanelLeds.PreviewFrame(PanelEmulation.Shift, 0, 15, null, false, null));
            Assert.Equal(PanelEmulation.StripFrame(3, 9, PanelEmulation.Mid, PanelLeds.OptionsFor(false, null)), PanelLeds.CardFrame("3-9-3", PanelLeds.OptionsFor(false, null)));
        }

        /// <summary>Live draws the car's run only where the profile does: the lights loaded, the switch on and the
        /// centre on the revs.</summary>
        [Theory]
        [InlineData(true, "car", "rpm", true)]
        [InlineData(true, null, null, true)]
        [InlineData(false, "car", "rpm", false)]
        [InlineData(true, "leftToRight", "rpm", false)]
        [InlineData(true, "car", "brake", false)]
        [InlineData(true, "car", "throttleBrake", false)]
        [InlineData(true, "car", "fuel", false)]
        [InlineData(true, "car", "rpmOnly", true)]
        public void Live_runs_the_cars_lights_only_where_the_strip_draws_them(bool ready, string style, string centre, bool runs)
        {
            Assert.Equal(runs, PanelLeds.LiveRuns(ready, style, centre));
        }

        /// <summary>A tick looks again only while Live is pressed, and the preview repaints only when the frame
        /// changed, which is what brings the strip at rest back when the car's lights stop.</summary>
        [Fact]
        public void A_tick_redraws_only_Live_and_only_a_changed_frame()
        {
            Assert.True(PanelLeds.TickRedraws(PanelLeds.LiveScenario));
            Assert.All(PanelLeds.Scenarios.Skip(1), s => Assert.False(PanelLeds.TickRedraws(s.Key)));

            var options = PanelLeds.OptionsFor(false, null);
            var packed = string.Concat(Enumerable.Repeat("#FF00D96A", 9));
            var lit = PanelLeds.FrameKey(PanelLeds.PreviewFrame(PanelLeds.LiveScenario, 3, 9, options, true, packed));
            var rest = PanelLeds.FrameKey(PanelLeds.PreviewFrame(PanelLeds.LiveScenario, 3, 9, options, false, null));
            Assert.NotEqual(lit, rest);
            Assert.Equal(lit, PanelLeds.FrameKey(PanelLeds.PreviewFrame(PanelLeds.LiveScenario, 3, 9, options, true, packed)));
            Assert.Equal(string.Empty, PanelLeds.FrameKey(null));
        }

        /// <summary>The labels sit under the Viewbox in columns weighted as the groups are at the preview's
        /// 30 px LEDs 6 apart, 22 between groups: a 3/9/3 is 566 wide.</summary>
        [Fact]
        public void The_preview_labels_are_laid_in_the_groups_own_proportions()
        {
            Assert.Equal(new double[] { 102, 22, 318, 22, 102 }, PanelLeds.PreviewColumns(3, 9));
            Assert.Equal(566, PanelLeds.PreviewColumns(3, 9).Sum());
            Assert.Equal(new double[] { 894 }, PanelLeds.PreviewColumns(0, 25));
            Assert.Equal(PanelLeds.PreviewLabels(3, 9).Length * 2 - 1, PanelLeds.PreviewColumns(3, 9).Length);
        }

        [Fact]
        public void The_pictures_dim_at_night_to_the_brightness_in_force()
        {
            Assert.Equal(1, PanelLeds.PreviewDim(false, 30, 50));
            Assert.Equal(0.3, PanelLeds.PreviewDim(true, 30, null), 3);
            Assert.Equal(0.3, PanelLeds.PreviewDim(true, 30, 50), 3);
            Assert.Equal(0.2, PanelLeds.PreviewDim(true, 30, 20), 3);
        }

        // --- Rev lights -------------------------------------------------------------------------------------

        /// <summary>#369: on is the car's own style, and a stored retired style reads as off.</summary>
        [Theory]
        [InlineData("car", true)]
        [InlineData(null, true)]
        [InlineData("leftToRight", false)]
        [InlineData("meetInMiddle", false)]
        [InlineData("f1", false)]
        public void The_switch_reads_the_stored_style(string stored, bool on)
        {
            Assert.Equal(on, PanelLeds.UsesCarRevLights(stored));
            // The width row sizes the car's own lights, so it shows only while the switch is on.
            Assert.Equal(on, PanelLeds.ShowsMirrorFit(stored));
        }

        /// <summary>#369: the page writes the car's own style for on and the plain ladder for off, in its own
        /// source because contract.test.ts reads the Contract names there; each reads back as the switch that
        /// wrote it, and the write is the one expression the page carries.</summary>
        [Fact]
        public void The_switch_writes_the_cars_style_or_the_plain_ladder()
        {
            Assert.True(PanelLeds.UsesCarRevLights(Contract.NormaliseChoice(Contract.LedRpmStyleCar, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle)));
            Assert.False(PanelLeds.UsesCarRevLights(Contract.NormaliseChoice(Contract.LedRpmStyleLeftToRight, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle)));
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("live.RpmStyle = Contract.NormaliseChoice(on ? Contract.LedRpmStyleCar : Contract.LedRpmStyleLeftToRight, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle);", leds);
        }

        [Fact]
        public void The_car_line_says_whether_Lovely_Car_Data_has_the_car_and_nothing_otherwise()
        {
            Assert.Equal("Porsche 911 GT3 R (992) is in Lovely Car Data.", PanelLeds.CarLine(true, "Porsche 911 GT3 R (992)", true, true));
            Assert.Equal("Radical SR8 is not in Lovely Car Data.", PanelLeds.CarLine(true, "Radical SR8", false, true));
            Assert.Null(PanelLeds.CarLine(false, "Radical SR8", false, true));
            Assert.Null(PanelLeds.CarLine(true, null, false, true));
            Assert.Null(PanelLeds.CarLine(true, " ", true, true));
            Assert.True(PanelLeds.CarLineGood(true, true));
            Assert.False(PanelLeds.CarLineGood(false, true));
            Assert.Equal(Theme.StatusUpToDate, PanelLeds.CarLineHex(true));
            Assert.Equal(Theme.Caution, PanelLeds.CarLineHex(false));
        }

        /// <summary>With no tables on disk no car can be found in them, so the line never says a car is missing
        /// from Lovely Car Data then: it says the tables are, and where the row that fetches them is.</summary>
        [Fact]
        public void With_no_tables_the_car_line_says_they_are_missing_rather_than_the_car()
        {
            Assert.Equal("Lovely Car Data is not downloaded yet. Download it under Every strip.", PanelLeds.CarLine(true, "Porsche 911 GT3 R (992)", false, false));
            Assert.Equal(PanelLeds.CarTablesMissing, PanelLeds.CarLine(true, null, false, false));
            Assert.Null(PanelLeds.CarLine(false, "Porsche 911 GT3 R (992)", false, false));
            Assert.False(PanelLeds.CarLineGood(true, false));
            Assert.Contains(PanelLights.CarTablesTitle, PanelLeds.CarTablesMissing);
            Assert.Contains(PanelLeds.EveryStripTitle, PanelLeds.CarTablesMissing);
        }

        [Fact]
        public void The_centre_chooser_opens_on_the_stored_centre()
        {
            Assert.Equal(0, PanelLeds.CentreIndex("rpm"));
            Assert.Equal(Array.IndexOf(Contract.LedCentres, "fuel"), PanelLeds.CentreIndex("fuel"));
            Assert.Equal(0, PanelLeds.CentreIndex("rpmOnly"));
            Assert.Equal(Contract.LedCentres.Length, PanelLights.CentreLabels.Length);
        }

        // --- This strip ---------------------------------------------------------------------------------------

        [Fact]
        public void A_strip_is_the_rigs_brightness_or_one_of_its_own_in_tens()
        {
            var labels = PanelLeds.BrightnessLabels(80);
            Assert.Equal("Same as rig · 80%", labels[0]);
            Assert.Equal(new[] { "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%" }, labels.Skip(1));
            Assert.Equal(0, PanelLeds.BrightnessIndex(null));
            Assert.Null(PanelLeds.BrightnessValue(0));
            for (var i = 1; i < labels.Length; i++)
            {
                Assert.Equal(i, PanelLeds.BrightnessIndex(PanelLeds.BrightnessValue(i)));
            }
            // A value written elsewhere lands on the nearest entry.
            Assert.Equal(PanelLeds.BrightnessIndex(60), PanelLeds.BrightnessIndex(62));
            Assert.Equal(1, PanelLeds.BrightnessIndex(0));
        }

        // --- Effects ----------------------------------------------------------------------------------------------

        /// <summary>The fifteen switches, one per setting of the contract, in the artboard's order: each side of
        /// the spotter and of the turn signals is a switch of its own.</summary>
        [Fact]
        public void There_is_one_switch_per_effect_setting()
        {
            Assert.Equal(new[]
            {
                "Flags", "Spotter left", "Spotter right", "Pit lane", "Pit limiter", "Speeding in the pit lane",
                "TC", "ABS", "DRS", "Push to pass", "Low fuel", "Temperature", "Oil pressure", "Turn signal left", "Turn signal right",
            }, PanelLeds.Effects.Select(effect => effect.Label));
            Assert.Equal(
                Contract.LedEffectSettings().OrderBy(s => s, StringComparer.Ordinal),
                PanelLeds.Effects.Select(effect => effect.Setting).OrderBy(s => s, StringComparer.Ordinal));
            Assert.All(PanelLeds.Effects, effect => Assert.Equal(effect.Setting, Contract.LedEffectSetting(effect.Id)));
            Assert.Equal("flag.black", PanelLeds.Effects[0].Id);
        }

        /// <summary>
        /// Each switch's role is the generator's, read out of packages/dash/src/leds/effects.ts, since the role
        /// is what decides which shapes carry it.
        /// </summary>
        [Fact]
        public void Each_switch_has_the_role_the_generator_gives_its_effect()
        {
            var source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "packages", "dash", "src", "leds", "effects.ts"));
            foreach (var effect in PanelLeds.Effects.Where(effect => effect.Setting != Contract.LedEffectFlags))
            {
                var at = source.IndexOf("id: '" + effect.Id + "'", StringComparison.Ordinal);
                Assert.True(at >= 0, effect.Id + " is not in effects.ts");
                var role = source.IndexOf("role: '", at, StringComparison.Ordinal) + "role: '".Length;
                Assert.Equal(effect.Role, source.Substring(role, source.IndexOf('\'', role) - role));
            }
            Assert.Contains("role: 'race' as const", source);
            Assert.Equal(PanelLeds.RoleRace, PanelLeds.Effects[0].Role);
        }

        /// <summary>
        /// A shape is offered the switches of what the generator draws on it. The shapes are pinned here in the
        /// grid's words; the next test holds the rule to every profile the build embeds.
        /// </summary>
        [Fact]
        public void A_shape_carries_the_effects_its_lamps_draw()
        {
            string[] Labels(int left, int right) => PanelLeds.EffectsFor(left, right).Select(effect => effect.Label).ToArray();
            // Three or more at each end: every lamp, so every switch.
            Assert.Equal(PanelLeds.Effects.Select(effect => effect.Label), Labels(3, 3));
            Assert.Equal(PanelLeds.Effects.Select(effect => effect.Label), Labels(4, 4));
            // Two: no aid lamp.
            Assert.Equal(new[]
            {
                "Flags", "Spotter left", "Spotter right", "Pit lane", "Pit limiter", "Speeding in the pit lane",
                "Low fuel", "Temperature", "Oil pressure", "Turn signal left", "Turn signal right",
            }, Labels(2, 2));
            // One: no aid lamp, and no turn signal, which a side of one draws as the green flag and so drops.
            Assert.Equal(new[]
            {
                "Flags", "Spotter left", "Spotter right", "Pit lane", "Pit limiter", "Speeding in the pit lane",
                "Low fuel", "Temperature", "Oil pressure",
            }, Labels(1, 1));
            // A bare run: the flags, a car alongside and the turn signals over the whole run, the pit lane, and
            // the low fuel its fuel centre flashes for; never the aids or the other warnings, which would be the
            // rev LEDs flashing for ABS.
            Assert.Equal(new[]
            {
                "Flags", "Spotter left", "Spotter right", "Pit lane", "Pit limiter", "Speeding in the pit lane",
                "Low fuel", "Turn signal left", "Turn signal right",
            }, Labels(0, 0));
            Assert.Equal(Labels(0, 0), PanelLeds.EffectsFor("0-15-0").Select(effect => effect.Label));
            Assert.Equal(Labels(3, 3), PanelLeds.EffectsFor("3-9-3-fanatec").Select(effect => effect.Label));
            // An id it cannot read is offered everything rather than nothing.
            Assert.Equal(PanelLeds.Effects.Count, PanelLeds.EffectsFor("mystery").Count);
        }

        /// <summary>
        /// Every profile the build embeds reads exactly the switches its shape is offered: a switch the profile
        /// never reads changes nothing, and an effect the profile reads with no switch cannot be turned off.
        /// </summary>
        /// <remarks>
        /// Off the files the plugin embeds, gunzipped as the plugin reads them, as PanelLedBarFormTests walks
        /// the census: each profile's OpenDash.LedEffect* reads against EffectsFor of its shape id.
        /// </remarks>
        [Fact]
        public void Every_profile_reads_exactly_the_switches_its_shape_is_offered()
        {
            var folder = new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() }
                .FirstOrDefault(f => Directory.Exists(f) && Directory.GetFiles(f, "*" + FlagBoxProfile.ProfileExtension)
                    .Any(p => FlagBoxProfile.ShapeIdOf(Path.GetFileName(p)) != null));
            if (folder == null)
            {
                Assert.False(OnCI, "no *" + FlagBoxProfile.ProfileExtension + " in " + RepoPaths.EmbeddedResources() + " or " + RepoPaths.BuildOutput());
                return;
            }
            var read = new System.Text.RegularExpressions.Regex(@"\bLedEffect[A-Za-z]+");
            var checkedShapes = 0;
            foreach (var path in Directory.GetFiles(folder, "*" + FlagBoxProfile.ProfileExtension))
            {
                var id = FlagBoxProfile.ShapeIdOf(Path.GetFileName(path));
                if (id == null) continue;
                var json = FlagBoxProfile.ReadFile(path);
                var reads = read.Matches(json).Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value)
                    .Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
                var offered = PanelLeds.EffectsFor(id).Select(effect => effect.Setting).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                Assert.True(offered.SequenceEqual(reads), id + " reads [" + string.Join(", ", reads) + "] but is offered [" + string.Join(", ", offered) + "]");
                checkedShapes++;
            }
            Assert.True(checkedShapes > 0);
        }

        private static bool OnCI =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        [Fact]
        public void The_full_strip_spotter_is_offered_only_where_an_end_carries_the_spotter()
        {
            Assert.True(PanelLeds.HasFullStripSpotter(3, 3));
            Assert.True(PanelLeds.HasFullStripSpotter(1, 0));
            Assert.False(PanelLeds.HasFullStripSpotter(0, 0));
            Assert.True(PanelLeds.HasFullStripSpotter("3-9-3-fanatec"));
            Assert.False(PanelLeds.HasFullStripSpotter("0-15-0"));
            // An id it cannot read is offered the row, as it is offered every effect.
            Assert.True(PanelLeds.HasFullStripSpotter("mystery"));
        }

        /// <summary>Three effect tiles hold the longest label on one line: "Speeding in the pit lane" is 141 at
        /// 14 px, with the 12 gap, the 40 switch and 24 of padding.</summary>
        [Fact]
        public void An_effect_tile_holds_the_longest_label_beside_its_switch()
        {
            Assert.True(PanelLeds.EffectTileMinWidth >= 141 + 12 + 40 + 24);
        }

        /// <summary>A card's picture shrinks to the card: the widest shape the build embeds, a 25-LED run at the
        /// card's 9 px LEDs 2 apart, is wider than a card's inside at the grid's narrowest (its cell less 30).
        /// Read as text because the page is WPF.</summary>
        [Fact]
        public void A_cards_picture_shrinks_to_the_card()
        {
            var widest = 25 * StripStyle.Card.Led + 24 * StripStyle.Card.Gap;
            Assert.True(widest > PanelLeds.CardMinWidth - 30);
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var cards = leds.Substring(leds.IndexOf("private FrameworkElement LedsCards(", StringComparison.Ordinal));
            cards = cards.Substring(0, cards.IndexOf("Ui.StripCard(", StringComparison.Ordinal));
            Assert.Contains("StretchDirection = StretchDirection.DownOnly", cards);
        }

        // --- The Add LEDs sheet -------------------------------------------------------------------------------

        /// <summary>The sheet takes the artboard's words, and voice.md's name for the press that opens it.</summary>
        [Fact]
        public void The_sheet_takes_the_artboards_words_and_voice_mds_name_for_its_button()
        {
            Assert.Equal("Add an LED strip", PanelLights.AddBar);
            Assert.Equal("Hardware", PanelLeds.HardwareStep);
            Assert.Equal("Shape", PanelLeds.ShapeStep);
            Assert.Equal("SimHub device", PanelLights.BarDeviceTitle);
            Assert.Equal("Name", PanelLights.BarNameTitle);
            Assert.Equal("Fanatec wheel", PanelLights.BarFanatecTitle);
            Assert.Equal("Something else", PanelLeds.SomethingElse);
            Assert.Equal("Any RGB strip", PanelLeds.SomethingElseNote);
            Assert.Equal("Found in SimHub", PanelLeds.FoundInSimHub);
            // No promise about the project: the artboard's "More wheels to come" is not the panel's.
            Assert.Equal("For any other wheel, use Something else.", PanelLeds.OtherWheelNote);
            Assert.Equal("Set by the wheel", PanelLeds.SetByTheWheel);
            Assert.Equal("Fixed", PanelLeds.Fixed);
            Assert.Equal("3 · 9 · 3", PanelLeds.FanatecShape);
            Assert.Equal("No LEDs OpenDash can reach", PanelLeds.NotReachable);
            Assert.Equal("SimHub's log says why.", PanelLeds.PassedOverNote);
            Assert.Equal("Not connected", PanelLeds.NotConnected);
            Assert.Equal("Add and install", PanelLeds.AddAndInstall);
            Assert.Equal("None", PanelLeds.EndsLabel(0));
            Assert.Equal("3", PanelLeds.EndsLabel(3));
        }

        [Fact]
        public void The_footer_says_what_will_be_installed_and_where()
        {
            Assert.Equal("Installs \"Wheel rim\" on Fanatec CSL Elite.", PanelLeds.InstallsOn("Wheel rim", "Fanatec CSL Elite"));
            Assert.Equal("Installs \"Strip\" on Arduino RGB LEDs.", PanelLeds.InstallsOn(" Strip ", "Arduino RGB LEDs"));
            // No device, nothing to install on: no promise, since the press would fail.
            Assert.Null(PanelLeds.InstallsOn("Strip", null));
        }

        /// <summary>The footer names what the press adds, as the settings settle it: a cleared box adds the name
        /// the sheet opened on, and a name the rig has is numbered.</summary>
        [Fact]
        public void The_footer_names_the_strip_the_press_will_add()
        {
            Assert.Equal("Wheel rim", PanelLeds.NameToAdd("  ", "Wheel rim", new string[0]));
            Assert.Equal("Brow", PanelLeds.NameToAdd(" Brow ", "Strip", null));
            Assert.Equal("Strip (2)", PanelLeds.NameToAdd("Strip", "Strip (2)", new[] { "Strip" }));
            Assert.Equal("Strip (2)", PanelLeds.NameToAdd("", PanelLeds.DefaultName("0-15-0", new[] { "Strip" }), new[] { "Strip" }));
        }

        /// <summary>The words the page draws that no other test reads by value.</summary>
        [Fact]
        public void The_presses_and_their_hovers_are_pinned()
        {
            Assert.Equal("Install", PanelLeds.InstallProfile);
            Assert.Equal("Update", PanelLeds.UpdateProfile);
            Assert.Equal("Installs this strip's profile in SimHub.", PanelLeds.InstallTooltip);
            // What the press does, not how: never "this build's version".
            Assert.Equal("Updates this strip's profile in SimHub.", PanelLeds.UpdateTooltip);
            Assert.Equal("Rename", PanelLeds.RenameButton);
            Assert.Equal("Remove", PanelLeds.RemoveButton);
            Assert.Equal("Removes this strip and its profile from SimHub.", PanelLeds.RemoveTooltip);
            Assert.Equal("Remove it", PanelLeds.RemoveConfirm);
            Assert.Equal("Removes the strip, its settings and its profile in SimHub.", PanelLeds.RemoveBody);
            Assert.Equal("Start", PanelLeds.EachLedStart);
            Assert.Equal("This build ships no strip profiles.", PanelLeds.NoProfiles);
            Assert.Equal(new[] { "1 LED" }, PanelLeds.PreviewLabels(0, 1));
        }

        [Fact]
        public void The_name_opens_on_what_the_hardware_is_numbered_as_the_rig_numbers_a_second()
        {
            Assert.Equal("Wheel rim", PanelLeds.DefaultName(PanelLights.BarShapeId(3, 9, true), new string[0]));
            Assert.Equal("Strip", PanelLeds.DefaultName(PanelLights.BarShapeId(0, 15, false), null));
            Assert.Equal("Strip (2)", PanelLeds.DefaultName("4-14-4", new[] { "Strip" }));
        }

        /// <summary>The sheet's shape picture: the ends in the flag's yellow and the centre the whole ladder, and a
        /// bare run the ladder alone, never a yellow fill that reads as a flag.</summary>
        [Fact]
        public void The_shape_picture_is_yellow_ends_around_the_whole_ladder()
        {
            var frame = PanelLeds.ShapeFrame(3, 9);
            Assert.Equal(3, frame.Length);
            Assert.All(frame[0], c => Assert.Equal(Theme.FlagYellow, c));
            Assert.All(frame[2], c => Assert.Equal(Theme.FlagYellow, c));
            Assert.Equal(Theme.ShiftStage1, frame[1][0]);
            Assert.Equal(Theme.ShiftStage3, frame[1][8]);
            Assert.All(frame[1], c => Assert.NotNull(c));
            var bare = PanelLeds.ShapeFrame(0, 11);
            Assert.Single(bare);
            Assert.DoesNotContain(Theme.FlagYellow, bare[0]);
            Assert.Contains(Theme.ShiftStage2, bare[0]);
        }

        /// <summary>A device row names the rig's strips already on it, and whether SimHub is talking to it.</summary>
        [Fact]
        public void A_device_row_names_the_strips_already_on_it()
        {
            Assert.Null(PanelLeds.DeviceMeta(true, null));
            Assert.Equal("Dash brow", PanelLeds.DeviceMeta(true, new[] { "Dash brow" }));
            Assert.Equal("Rim · Dash brow", PanelLeds.DeviceMeta(true, new[] { "Rim", "Dash brow" }));
            Assert.Equal("Not connected", PanelLeds.DeviceMeta(false, new string[0]));
            Assert.Equal("Rim · Not connected", PanelLeds.DeviceMeta(false, new[] { "Rim" }));
        }

        /// <summary>The device picker is bounded so a long name trims, and the lines under a row are capped as the
        /// kit caps a row's caption.</summary>
        [Fact]
        public void Long_words_are_bounded()
        {
            Assert.Equal(260, PanelLeds.DevicePickerMaxWidth);
            Assert.Equal(520, PanelLeds.CaptionMaxWidth);
        }

        [Fact]
        public void The_Fanatec_tile_is_found_by_the_device_name_and_opened_on_only_where_it_is_found()
        {
            Assert.True(PanelLeds.FoundFanatec(new[] { "Arduino RGB LEDs", "Fanatec CSL Elite" }));
            Assert.False(PanelLeds.FoundFanatec(new[] { "Arduino RGB LEDs" }));
            Assert.False(PanelLeds.FoundFanatec(null));
            Assert.True(PanelLeds.StartsOnFanatec(true, true));
            Assert.False(PanelLeds.StartsOnFanatec(true, false));
            Assert.False(PanelLeds.StartsOnFanatec(false, true));
        }

        // --- After a press ------------------------------------------------------------------------------------

        [Fact]
        public void Each_press_says_what_happened_and_the_step_SimHub_does_not_take()
        {
            // One object for the header's press, whatever it did: the strip's profile.
            Assert.Equal("Installed Rim's profile. Select \"Rim\" on Fanatec CSL Elite in SimHub to use it.", PanelLeds.ProfileInstalled("Rim", "Fanatec CSL Elite"));
            Assert.Equal("Installed Rim's profile. Select \"Rim\" in SimHub to use it.", PanelLeds.ProfileInstalled("Rim", null));
            Assert.Equal("Updated Rim's profile.", PanelLeds.ProfileUpdated("Rim"));
            Assert.Equal("Could not install Rim's profile. See SimHub's log.", PanelLeds.ProfileFailed("Rim"));
            // A move installs the profile fresh on the other device, where it is not selected.
            Assert.Equal("Moved Rim's profile to Arduino RGB LEDs. Select \"Rim\" there in SimHub to use it.", PanelLeds.Moved("Rim", "Arduino RGB LEDs"));
            Assert.Equal("Moved Rim's profile. Select \"Rim\" in SimHub to use it.", PanelLeds.Moved("Rim", null));
            Assert.Equal("Renamed to Rim in OpenDash and SimHub.", PanelLeds.Renamed("Rim", true));
            Assert.Equal("Renamed to Rim.", PanelLeds.Renamed("Rim", false));
            // The install takes the old copy out first, so a failure leaves no copy under the old name.
            Assert.Equal("Renamed to Rim, but its profile could not be installed again. See SimHub's log.", PanelLeds.RenameNotInSimHub("Rim"));
            Assert.Equal("Removed Rim and its profile.", PanelLeds.Removed("Rim"));
            Assert.Equal("Reversed Rim.", PanelLeds.ReverseSaid("Rim", true));
            Assert.Equal("Rim runs in its usual direction again.", PanelLeds.ReverseSaid("Rim", false));
        }

        /// <summary>A note the install returned is a step the select waits on, so it comes before the select.</summary>
        [Fact]
        public void A_note_comes_before_the_select_it_enables()
        {
            const string note = "Turn off built-in profiles on your device, or OpenDash's will not be listed.";
            Assert.Equal("Installed Rim's profile. " + note + " Select \"Rim\" on Wheel in SimHub to use it.", PanelLeds.ProfileInstalled("Rim", "Wheel", note));
            Assert.Equal("Updated Rim's profile. " + note, PanelLeds.ProfileUpdated("Rim", note));
            Assert.Equal("Moved Rim's profile to Wheel. " + note + " Select \"Rim\" there in SimHub to use it.", PanelLeds.Moved("Rim", "Wheel", note));
            Assert.Equal(PanelLeds.ProfileInstalled("Rim", "Wheel", note), PanelLeds.InstallSaid(true, FlagBoxInstallState.NotInstalled, "Rim", "Wheel", note));
            Assert.Equal("Reversed Rim. " + note + " Select \"Rim\" on Wheel in SimHub to use it.", PanelLeds.ReverseSaid("Rim", true, false, "Wheel", note));
        }

        /// <summary>A Reverse replaces the copy SimHub held, or installs the twin fresh and unselected where it
        /// held none, which is when the line names the select.</summary>
        [Fact]
        public void A_reverse_says_the_select_where_the_twin_is_new_to_SimHub()
        {
            Assert.Equal("Reversed Rim.", PanelLeds.ReverseSaid("Rim", true, true, "Wheel"));
            Assert.Equal("Rim runs in its usual direction again.", PanelLeds.ReverseSaid("Rim", false, true, "Wheel"));
            Assert.Equal("Reversed Rim. Select \"Rim\" on Wheel in SimHub to use it.", PanelLeds.ReverseSaid("Rim", true, false, "Wheel"));
            Assert.Equal("Rim runs in its usual direction again. Select \"Rim\" in SimHub to use it.", PanelLeds.ReverseSaid("Rim", false, false, null));
            Assert.True(PanelLeds.HeldInSimHub(FlagBoxInstallState.UpToDate));
            Assert.True(PanelLeds.HeldInSimHub(FlagBoxInstallState.Outdated));
            Assert.False(PanelLeds.HeldInSimHub(FlagBoxInstallState.NotInstalled));
            Assert.False(PanelLeds.HeldInSimHub(FlagBoxInstallState.Failed));
            Assert.False(PanelLeds.HeldInSimHub(null));
        }

        /// <summary>A Reverse or a Rename the page cannot follow with an install saves the setting and says what
        /// is left, never the log.</summary>
        [Fact]
        public void A_press_that_cannot_install_says_why()
        {
            Assert.Equal("Reversed Rim. This build ships no profile for this strip.",
                PanelLeds.WithReason(PanelLeds.ReverseSaid("Rim", true), PanelLeds.ProfileBlocked(false, true)));
            Assert.Equal("Renamed to Rim. SimHub does not list this strip's device. Choose one under SimHub device.",
                PanelLeds.WithReason(PanelLeds.Renamed("Rim", false), PanelLeds.ProfileBlocked(true, false)));
            Assert.DoesNotContain("log", PanelLeds.WithReason(PanelLeds.Renamed("Rim", false), PanelLeds.ProfileBlocked(true, false)));
        }

        /// <summary>With no device SimHub lists, the sheet's press adds and promises no install, and the line after
        /// it names the step left.</summary>
        [Fact]
        public void With_no_device_the_press_adds_and_says_the_device_is_left()
        {
            Assert.Equal("Add and install", PanelLeds.AddPress(true));
            Assert.Equal("Add", PanelLeds.AddPress(false));
            Assert.Equal("Added Rim. Choose its SimHub device to install its profile.", PanelLeds.AddedWithoutDevice("Rim"));
            Assert.Contains(PanelLights.BarDeviceTitle, PanelLeds.AddedWithoutDevice("Rim"));
        }

        /// <summary>The header's press says an update where SimHub held an older copy, an install with the step
        /// SimHub does not take otherwise, and the log where it failed.</summary>
        [Fact]
        public void The_install_press_says_an_update_or_an_install()
        {
            Assert.Equal("Updated Rim's profile.", PanelLeds.InstallSaid(true, FlagBoxInstallState.Outdated, "Rim", "Wheel"));
            Assert.Equal(PanelLeds.ProfileInstalled("Rim", "Wheel"), PanelLeds.InstallSaid(true, FlagBoxInstallState.NotInstalled, "Rim", "Wheel"));
            Assert.Equal(PanelLeds.ProfileInstalled("Rim", null), PanelLeds.InstallSaid(true, null, "Rim", null));
            Assert.Equal(PanelLeds.ProfileFailed("Rim"), PanelLeds.InstallSaid(false, FlagBoxInstallState.Outdated, "Rim", "Wheel"));
        }

        /// <summary>A rename reinstalls only where SimHub holds the profile, and a blank name renames nothing.</summary>
        [Theory]
        [InlineData(FlagBoxInstallState.UpToDate, true)]
        [InlineData(FlagBoxInstallState.Outdated, true)]
        [InlineData(FlagBoxInstallState.NotInstalled, false)]
        [InlineData(FlagBoxInstallState.Failed, false)]
        [InlineData(FlagBoxInstallState.Unavailable, false)]
        [InlineData(null, false)]
        public void A_rename_reinstalls_only_where_SimHub_holds_the_profile(FlagBoxInstallState? profile, bool reinstalls)
        {
            Assert.Equal(reinstalls, PanelLeds.RenameReinstalls(profile));
        }

        [Fact]
        public void A_blank_name_cannot_be_renamed_to()
        {
            Assert.False(PanelLeds.CanRename(null));
            Assert.False(PanelLeds.CanRename("   "));
            Assert.True(PanelLeds.CanRename("Rim"));
        }

        /// <summary>voice.md's rules over every word the page model holds: no contractions, no question, no
        /// lowercase "openDash" in a sentence, and no word of the old tabs.</summary>
        [Fact]
        public void No_word_on_the_page_breaks_voice_md()
        {
            var words = typeof(PanelLeds).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(string))
                .Select(field => (string)field.GetValue(null))
                .Where(value => value != null)
                .Concat(PanelLeds.Effects.Select(effect => effect.Label))
                .Concat(PanelLeds.Scenarios.Select(s => s.Value))
                .ToList();
            foreach (var word in words)
            {
                Assert.DoesNotContain("n't", word);
                Assert.DoesNotContain("'re", word);
                Assert.DoesNotContain("'ll", word);
                Assert.DoesNotContain("?", word);
                Assert.DoesNotContain("openDash", word);
                Assert.DoesNotContain(" tab", word);
            }
        }
    }
}
