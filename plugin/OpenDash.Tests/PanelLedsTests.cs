// PanelLedsTests.cs: the LEDs page's decisions and words -- what a card says, what the preview draws, which
// effect switches a shape carries, the Add LEDs sheet, the lines after a press, and where search sends a
// driver. SettingsControl.Lights.cs draws these and decides nothing, so this is where the page is held.
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
            // #369 decided the artboard's words for the switch; the noun form would be "Car's own rev lights".
            Assert.Equal("Use the car's own rev lights", PanelLeds.CarRevLightsTitle);
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
        }

        [Fact]
        public void The_car_line_says_whether_Lovely_Car_Data_has_the_car_and_nothing_otherwise()
        {
            Assert.Equal("Porsche 911 GT3 R (992) is in Lovely Car Data.", PanelLeds.CarLine(true, "Porsche 911 GT3 R (992)", true));
            Assert.Equal("Radical SR8 is not in Lovely Car Data.", PanelLeds.CarLine(true, "Radical SR8", false));
            Assert.Null(PanelLeds.CarLine(false, "Radical SR8", false));
            Assert.Null(PanelLeds.CarLine(true, null, false));
            Assert.Null(PanelLeds.CarLine(true, " ", true));
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
        /// A shape is offered the switches of what the generator draws on it: rpmStrip.ts effects() and the lamps
        /// of lamps.ts. The rules are read here so a change there fails this rather than offering a switch for
        /// something the strip never shows.
        /// </summary>
        [Fact]
        public void A_shape_carries_the_effects_its_lamps_draw()
        {
            var strip = File.ReadAllText(Path.Combine(RepoPaths.Root(), "packages", "dash", "src", "leds", "rpmStrip.ts"));
            Assert.Contains("placed.length === 0 ? ['strip', 'race', 'side'] : ['strip']", strip);
            var lamps = System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(Path.Combine(RepoPaths.Root(), "packages", "dash", "src", "leds", "lamps.ts")), @"\s+", " ");
            Assert.Contains("[lamp('side', 'side, flag and car', ['side', 'race', 'car'])]", lamps);
            Assert.Contains("[SIDE, lamp('car', 'car and flag', ['car', 'race'])]", lamps);
            Assert.Contains("[SIDE, RACE, lamp('car', 'car and aid', ['car', 'aid'])]", lamps);

            string[] Labels(int left, int right) => PanelLeds.EffectsFor(left, right).Select(effect => effect.Label).ToArray();
            // Three or more at each end: every lamp, so every switch.
            Assert.Equal(PanelLeds.Effects.Select(effect => effect.Label), Labels(3, 3));
            Assert.Equal(PanelLeds.Effects.Select(effect => effect.Label), Labels(4, 4));
            // One or two: no aid lamp.
            Assert.Equal(new[]
            {
                "Flags", "Spotter left", "Spotter right", "Pit lane", "Pit limiter", "Speeding in the pit lane",
                "Low fuel", "Temperature", "Oil pressure", "Turn signal left", "Turn signal right",
            }, Labels(2, 2));
            Assert.Equal(Labels(2, 2), Labels(1, 1));
            // A bare run: the flags, a car alongside and the turn signals over the whole run, and the pit lane;
            // never the aids or the car's warnings, which would be the rev LEDs flashing for ABS.
            Assert.Equal(new[]
            {
                "Flags", "Spotter left", "Spotter right", "Pit lane", "Pit limiter", "Speeding in the pit lane",
                "Turn signal left", "Turn signal right",
            }, Labels(0, 0));
            Assert.Equal(Labels(0, 0), PanelLeds.EffectsFor("0-15-0").Select(effect => effect.Label));
            Assert.Equal(Labels(3, 3), PanelLeds.EffectsFor("3-9-3-fanatec").Select(effect => effect.Label));
            // An id it cannot read is offered everything rather than nothing.
            Assert.Equal(PanelLeds.Effects.Count, PanelLeds.EffectsFor("mystery").Count);
        }

        [Fact]
        public void The_full_strip_spotter_is_offered_only_where_an_end_carries_the_spotter()
        {
            Assert.True(PanelLeds.HasFullStripSpotter(3, 3));
            Assert.True(PanelLeds.HasFullStripSpotter(1, 0));
            Assert.False(PanelLeds.HasFullStripSpotter(0, 0));
        }

        // --- The Add LEDs sheet -------------------------------------------------------------------------------

        [Fact]
        public void The_sheet_is_worded_as_voice_md_has_it()
        {
            Assert.Equal("Add LEDs", PanelLights.AddBar);
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
            Assert.Equal("Not connected", PanelLeds.NotConnected);
            Assert.Equal("Add and install", PanelLeds.AddAndInstall);
            Assert.Equal("None", PanelLeds.EndsLabel(0));
            Assert.Equal("3", PanelLeds.EndsLabel(3));
        }

        [Fact]
        public void The_footer_says_what_will_be_installed_and_where()
        {
            Assert.Equal("Installs \"Wheel rim\" on Fanatec CSL Elite.", PanelLeds.InstallsOn("Wheel rim", "Fanatec CSL Elite"));
            Assert.Equal("Installs \"Strip\".", PanelLeds.InstallsOn(" Strip ", null));
        }

        [Fact]
        public void The_name_opens_on_what_the_hardware_is_numbered_as_the_rig_numbers_a_second()
        {
            Assert.Equal("Wheel rim", PanelLeds.DefaultName(PanelLights.BarShapeId(3, 9, true), new string[0]));
            Assert.Equal("Strip", PanelLeds.DefaultName(PanelLights.BarShapeId(0, 15, false), null));
            Assert.Equal("Strip (2)", PanelLeds.DefaultName("4-14-4", new[] { "Strip" }));
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
            Assert.Equal("Installed Rim. Select \"Rim\" on Fanatec CSL Elite in SimHub to use it.", PanelLeds.ProfileInstalled("Rim", "Fanatec CSL Elite"));
            Assert.Equal("Installed Rim. Select \"Rim\" on your LED device in SimHub to use it.", PanelLeds.ProfileInstalled("Rim", null));
            Assert.Equal("Updated Rim's profile.", PanelLeds.ProfileUpdated("Rim"));
            Assert.Equal("Could not install Rim's profile. See SimHub's log.", PanelLeds.ProfileFailed("Rim"));
            Assert.Equal("Moved Rim's profile to Arduino RGB LEDs.", PanelLeds.Moved("Rim", "Arduino RGB LEDs"));
            Assert.Equal("Renamed to Rim, in SimHub too.", PanelLeds.Renamed("Rim", true));
            Assert.Equal("Renamed to Rim.", PanelLeds.Renamed("Rim", false));
            Assert.Equal("Renamed to Rim, but SimHub still lists the old name. See SimHub's log.", PanelLeds.RenameNotInSimHub("Rim"));
            Assert.Equal("Removed Rim and its profile.", PanelLeds.Removed("Rim"));
            Assert.Equal("Reversed Rim.", PanelLeds.ReverseSaid("Rim", true));
            Assert.Equal("Rim runs in its usual direction again.", PanelLeds.ReverseSaid("Rim", false));
            Assert.Equal("Rim is at 60%.", PanelLeds.BrightnessSaid("Rim", 60));
            Assert.Equal("Rim follows the rig's brightness.", PanelLeds.BrightnessSaid("Rim", null));
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
