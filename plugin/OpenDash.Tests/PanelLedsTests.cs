// PanelLedsTests.cs: the LEDs page's decisions and words -- what a card says, what the preview draws, which
// effect switches a shape carries, the Add LEDs sheet, the lines after a press, and where search sends a
// driver. SettingsControl.Lights.cs draws these, so this is where the page is held: the rules the page follows
// (when Live runs, when a rename reinstalls, what the car line says, which strip and which chip it opens on)
// are PanelLeds functions pinned here, and what each control writes, and which anchor each row carries, is
// read out of the page's own source, since the net8.0 test project cannot compile a line of WPF.
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
            // No caption: the artboard draws none on the row, and the one there was described off while the
            // switch was on, and wrongly, since the revs fill only the centre and not at all on a brake centre.
            Assert.Null(typeof(PanelLeds).GetField("CarRevLightsCaption"));
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("LedsRow(PanelLeds.CarRevLightsTitle, style)", leds);
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
            Assert.Equal("This strip's device is not in SimHub. Choose one under SimHub device.", PanelLeds.ProfileBlocked(true, false));
            Assert.Contains(PanelLights.BarDeviceTitle, PanelLeds.DeviceNotListed);
            // One phrase for the state, with the picker's value for it.
            Assert.Contains(PanelLights.DeviceGone.Substring("Device ".Length), PanelLeds.DeviceNotListed);
            Assert.Equal(PanelLeds.DeviceNotListed, PanelLeds.ProfileBlocked(true, false, 2, null));
            // With no device offered there is no picker to point at: the line is the device row's own caption,
            // whose first step is in SimHub, or which names a device passed over and the log.
            Assert.Equal(PanelLights.NoDevices, PanelLeds.ProfileBlocked(true, false, 0, null));
            Assert.Equal("Rim has no LEDs OpenDash can reach. See SimHub's log.", PanelLeds.ProfileBlocked(true, false, 0, new[] { "Rim" }));
            Assert.Equal(PanelLeds.DeviceRow(null, "arduino", new[] { "Rim" }).Caption, PanelLeds.ProfileBlocked(true, false, 0, new[] { "Rim" }));
            Assert.Null(PanelLeds.ProfileBlocked(true, true, 0, null));
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
            Assert.Contains("PanelLeds.ProfileBlocked(EmbeddedProfileOf(bar) != null, LedTargets.Find(bar.Device) != null, offered, declined)", leds);
            Assert.Contains("var offered = LedTargets.All(out declined).Count;", leds);
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

        /// <summary>The preview's groups at its 30 px LEDs 6 apart, 22 between groups, which the labels are placed
        /// under: a 3/9/3 is 566 wide.</summary>
        [Fact]
        public void The_preview_columns_are_the_groups_at_the_previews_size()
        {
            Assert.Equal(new double[] { 102, 22, 318, 22, 102 }, PanelLeds.PreviewColumns(3, 9));
            Assert.Equal(566, PanelLeds.PreviewColumns(3, 9).Sum());
            Assert.Equal(new double[] { 894 }, PanelLeds.PreviewColumns(0, 25));
            Assert.Equal(PanelLeds.PreviewLabels(3, 9).Length * 2 - 1, PanelLeds.PreviewColumns(3, 9).Length);
        }

        /// <summary>
        /// Every label under the preview stays in the room and clear of its neighbours, under its own group, for
        /// every shape the build embeds, at full size and as the Viewbox shrinks it to the narrow column: a
        /// label is never cut, which is the rule textFit.test.ts holds the dash to.
        /// </summary>
        /// <remarks>
        /// Measured as the panel draws an eyebrow, 11 px with the label tracking after every glyph, in Barlow Bold's
        /// advances from packages/dash/src/design/advances.ts: the panel draws SemiBold, which the table does not
        /// carry and Bold is at least as wide as. The shapes are every ends and centre the embedded profiles have
        /// (0 to 5 at each end, 4 to 14 in a centre between ends, up to 25 in a bare run).
        /// </remarks>
        [Fact]
        public void Every_preview_label_fits_under_its_group()
        {
            var bold = Advances("BarlowBold");
            double Width(string text) => text.Sum(ch => (bold.TryGetValue(ch, out var em) ? em : 0.75) * PanelShell.EyebrowSize + Math.Round(PanelShell.EyebrowSize * Theme.TrackingLabel, 2));
            // The 1-LED end the review measured: wider than its group, so a cell as wide as the group cuts it.
            Assert.True(Width("Right · 1") > PanelLeds.PreviewColumns(1, 4)[0]);

            var shapes = new List<(int, int)>();
            for (var ends = 1; ends <= 5; ends++) for (var centre = 4; centre <= 14; centre++) shapes.Add((ends, centre));
            for (var centre = 1; centre <= 25; centre++) shapes.Add((0, centre));
            foreach (var (ends, centre) in shapes)
            {
                var columns = PanelLeds.PreviewColumns(ends, centre);
                var labels = PanelLeds.PreviewLabels(ends, centre);
                var widths = labels.Select(Width).ToArray();
                // Full size (in no less room than the narrowest tried), the narrow column's inside (527 less the
                // preview's 22 each side and its border), and narrower still.
                foreach (var room in new[] { Math.Max(columns.Sum(), 360), 700, 481, 400, 360 })
                {
                    var lefts = PanelLeds.PreviewLabelLefts(columns, widths, room);
                    var scale = Math.Min(1, room / columns.Sum());
                    var offset = Math.Max(0, (room - columns.Sum() * scale) / 2);
                    var x = 0.0;
                    for (var i = 0; i < labels.Length; i++)
                    {
                        var shape = ends + "-" + centre + "-" + ends + " at " + room + ": " + labels[i];
                        Assert.True(lefts[i] >= 0 && lefts[i] + widths[i] <= room + 1e-9, shape + " leaves the room");
                        if (i > 0) Assert.True(lefts[i - 1] + widths[i - 1] + 4 <= lefts[i], shape + " meets the label before it");
                        // Under its own group: the label and the group overlap.
                        var groupLeft = offset + x * scale;
                        var groupRight = offset + (x + columns[2 * i]) * scale;
                        Assert.True(lefts[i] < groupRight && lefts[i] + widths[i] > groupLeft, shape + " is not under its group");
                        x += columns[2 * i] + (2 * i + 1 < columns.Length ? columns[2 * i + 1] : 0);
                    }
                }
            }
            // The page places the labels by this rule, in one cell as wide as the preview, not a column each.
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("PanelLeds.PreviewLabelLefts(columns, widths, room)", leds);
        }

        [Fact]
        public void A_label_is_centred_under_its_group_and_moved_in_at_the_edge()
        {
            var columns = PanelLeds.PreviewColumns(3, 9);
            // At full size a 40 wide label under the 102 wide left end starts 31 in.
            Assert.Equal(new[] { 31.0, 283.0 - 30, 566 - 102 / 2.0 - 20 }, PanelLeds.PreviewLabelLefts(columns, new[] { 40.0, 60, 40 }, 566));
            // A label wider than its end at the room's edge is moved in, never out.
            Assert.Equal(0.0, PanelLeds.PreviewLabelLefts(PanelLeds.PreviewColumns(1, 4), new[] { 50.0, 60, 50 }, 242)[0]);
            Assert.Equal(192.0, PanelLeds.PreviewLabelLefts(PanelLeds.PreviewColumns(1, 4), new[] { 50.0, 60, 50 }, 242)[2]);
            // In more room than the preview needs, it is centred as the Viewbox is.
            Assert.Equal(131.0 + 283 - 30, PanelLeds.PreviewLabelLefts(columns, new[] { 40.0, 60, 40 }, 828)[1]);
        }

        /// <summary>The advances of one face in packages/dash/src/design/advances.ts, by character.</summary>
        private static Dictionary<char, double> Advances(string face)
        {
            var source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "packages", "dash", "src", "design", "advances.ts"));
            var start = source.IndexOf("const " + face + ":", StringComparison.Ordinal);
            Assert.True(start >= 0, face + " is not in advances.ts");
            var open = source.IndexOf('{', start);
            var close = source.IndexOf("};", open, StringComparison.Ordinal);
            var table = new Dictionary<char, double>();
            var entry = new System.Text.RegularExpressions.Regex(@"'((?:\\.|[^'\\])+)': ([0-9.]+)");
            foreach (System.Text.RegularExpressions.Match m in entry.Matches(source.Substring(open, close - open)))
            {
                var key = System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value);
                table[key[0]] = double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            Assert.True(table.Count > 90);
            return table;
        }

        /// <summary>The Viewbox draws the preview and the card as they are, so a centre that does not show the
        /// revs is at rest at the shift point and half way: the profile never draws the revs there.</summary>
        [Fact]
        public void A_centre_that_is_not_the_revs_is_at_rest_at_a_rev_moment()
        {
            var options = PanelLeds.OptionsFor(false, null);
            Assert.True(PanelLeds.CentreShowsRevs("rpm"));
            Assert.True(PanelLeds.CentreShowsRevs(null));
            Assert.False(PanelLeds.CentreShowsRevs("fuel"));
            Assert.False(PanelLeds.CentreShowsRevs("brake"));
            var shift = PanelLeds.PreviewFrame(PanelEmulation.Shift, 3, 9, options, false, null, false);
            Assert.All(shift[1], c => Assert.Null(c));
            Assert.Equal(PanelEmulation.StripFrame(3, 9, PanelEmulation.Shift, options), PanelLeds.PreviewFrame(PanelEmulation.Shift, 3, 9, options, false, null, true));
            Assert.All(PanelLeds.CardFrame("0-15-0", options, false)[0], c => Assert.Null(c));
            Assert.Contains(PanelLeds.CardFrame("0-15-0", options, true)[0], c => c != null);
            // Not a rev moment: the effect is drawn as the emulation draws it.
            Assert.Equal(PanelEmulation.StripFrame(0, 15, PanelEmulation.Yellow, options), PanelLeds.PreviewFrame(PanelEmulation.Yellow, 0, 15, options, false, null, false));
            Assert.True(PanelLeds.IsRevMoment(PanelEmulation.Mid));
            Assert.False(PanelLeds.IsRevMoment(PanelEmulation.CarLeft));
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Equal(2, Occurrences(leds, "PanelLeds.CentreShowsRevs(Settings.BarCentre(ns))"));
        }

        /// <summary>A card draws its strip lit only while SimHub shows the profile: a card that says it is not
        /// selected or not installed draws every LED dark, as the artboard draws the unselected brow.</summary>
        [Theory]
        [InlineData(FlagBoxInstallState.UpToDate, true, true)]
        [InlineData(FlagBoxInstallState.UpToDate, false, false)]
        [InlineData(FlagBoxInstallState.UpToDate, null, false)]
        [InlineData(FlagBoxInstallState.Outdated, true, false)]
        [InlineData(FlagBoxInstallState.Outdated, false, false)]
        [InlineData(FlagBoxInstallState.NotInstalled, null, false)]
        [InlineData(FlagBoxInstallState.Unavailable, null, false)]
        [InlineData(null, null, false)]
        public void A_card_is_lit_only_while_SimHub_shows_its_strip(FlagBoxInstallState? profile, bool? selected, bool lit)
        {
            Assert.Equal(lit, PanelLeds.CardLit(profile, selected));
            var options = PanelLeds.OptionsFor(false, null);
            var frame = PanelLeds.CardFrame("3-9-3", options, true, PanelLeds.CardLit(profile, selected));
            Assert.Equal(new[] { 3, 9, 3 }, frame.Select(group => group.Length));
            if (lit) Assert.Contains(frame.SelectMany(group => group), c => c != null);
            else Assert.All(frame.SelectMany(group => group), c => Assert.Null(c));
        }

        [Fact]
        public void A_dark_card_keeps_its_shape_and_the_page_passes_whether_it_is_lit()
        {
            Assert.Equal(new[] { 15 }, PanelLeds.CardFrame("0-15-0", null, true, false).Select(group => group.Length));
            Assert.Equal(PanelLeds.CardFrame("3-9-3", PanelLeds.OptionsFor(false, null)), PanelLeds.CardFrame("3-9-3", PanelLeds.OptionsFor(false, null), true, true));
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("var lit = PanelLeds.CardLit(profile, selected);", leds);
            Assert.Contains("PanelLeds.CardFrame(live.Shape, LedsOptions(live), PanelLeds.CentreShowsRevs(Settings.BarCentre(ns)), lit)", leds);
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
            Assert.Equal("Porsche 911 GT3 R (992) is in Lovely Car Data.", PanelLeds.CarLine(true, "Porsche 911 GT3 R (992)", true, true, true));
            Assert.Equal("Radical SR8 is not in Lovely Car Data.", PanelLeds.CarLine(true, "Radical SR8", false, true, true));
            Assert.Null(PanelLeds.CarLine(false, "Radical SR8", false, true, true));
            Assert.Null(PanelLeds.CarLine(true, null, false, true, true));
            Assert.Null(PanelLeds.CarLine(true, " ", true, true, true));
            Assert.True(PanelLeds.CarLineGood(true, true));
            Assert.False(PanelLeds.CarLineGood(false, true));
            Assert.Equal(Theme.StatusUpToDate, PanelLeds.CarLineHex(true));
            Assert.Equal(Theme.Caution, PanelLeds.CarLineHex(false));
        }

        /// <summary>With no tables on disk no car can be found in them, so the line never says a car is missing
        /// from Lovely Car Data then: it says the tables are, and where the row that fetches them is. With no car
        /// loaded, which is every fresh install at the desk, it says nothing (ruling 48).</summary>
        [Fact]
        public void With_no_tables_the_car_line_says_they_are_missing_rather_than_the_car()
        {
            Assert.Equal("Lovely Car Data is not downloaded yet. Download it under Every strip.", PanelLeds.CarLine(true, "Porsche 911 GT3 R (992)", false, false, true));
            Assert.Null(PanelLeds.CarLine(true, null, false, false, true));
            Assert.Null(PanelLeds.CarLine(true, " ", false, false, true));
            Assert.Null(PanelLeds.CarLine(false, "Porsche 911 GT3 R (992)", false, false, true));
            Assert.False(PanelLeds.CarLineGood(true, false));
            Assert.Contains(PanelLights.CarTablesTitle, PanelLeds.CarTablesMissing);
            Assert.Contains(PanelLeds.EveryStripTitle, PanelLeds.CarTablesMissing);
            Assert.True(PanelLeds.TablesLoaded(84));
            Assert.False(PanelLeds.TablesLoaded(0));
        }

        /// <summary>The plugin reads iRacing's tables alone, so in any other game a car the archive does carry
        /// would read as missing: the line says nothing there, tables or not.</summary>
        [Theory]
        [InlineData("iRacing", true)]
        [InlineData("IRacing", true)]
        [InlineData(" iracing ", true)]
        [InlineData("Assetto Corsa Competizione", false)]
        [InlineData("AssettoCorsaCompetizione", false)]
        [InlineData("Le Mans Ultimate", false)]
        [InlineData(null, false)]
        public void The_car_line_speaks_only_in_the_game_the_tables_cover(string game, bool covered)
        {
            Assert.Equal(covered, PanelLeds.TablesCoverGame(game));
            var said = PanelLeds.CarLine(true, "Porsche 911 GT3 R (992)", false, true, PanelLeds.TablesCoverGame(game));
            Assert.Equal(covered ? "Porsche 911 GT3 R (992) is not in Lovely Car Data." : null, said);
            Assert.Equal(covered ? PanelLeds.CarTablesMissing : null, PanelLeds.CarLine(true, "Porsche 911 GT3 R (992)", false, false, PanelLeds.TablesCoverGame(game)));
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

        /// <summary>At night a strip that follows the rig runs at the night brightness, as its pictures are dimmed
        /// to, so the first entry names that value and not the day's.</summary>
        [Fact]
        public void The_rigs_entry_names_the_brightness_in_force_at_night()
        {
            Assert.Equal(80, PanelLeds.RigBrightnessInForce(false, 80, 30));
            Assert.Equal(30, PanelLeds.RigBrightnessInForce(true, 80, 30));
            Assert.Equal("Same as rig · 30%", PanelLeds.BrightnessLabels(PanelLeds.RigBrightnessInForce(true, 80, 30))[0]);
            // The same value the pictures are dimmed to.
            Assert.Equal(PanelEmulation.Dim(true, PanelLeds.RigBrightnessInForce(true, 80, 30)), PanelLeds.PreviewDim(true, 30, null), 3);
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("PanelLeds.RigBrightnessInForce(Settings.LightsNightMode, Settings.LightsBrightness, Settings.LightsNightBrightness)", leds);
            Assert.Contains("PanelLeds.BrightnessLabels(rigShown)", leds);
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

        /// <summary>
        /// An effect tile at its narrowest holds every label on one line beside its switch, each measured at the
        /// tile's 14 px from the bundled fonts' advances: the label, the 12 gap, the switch and the tile's 24 of
        /// padding.
        /// </summary>
        /// <remarks>
        /// The tile draws Barlow Regular, which advances.ts does not carry; Barlow Medium, the narrowest face it
        /// does, is at least as wide, so a label that fits measured in it fits as drawn. "Speeding in the pit lane"
        /// is the widest, at about 142.
        /// </remarks>
        [Fact]
        public void An_effect_tile_holds_the_longest_label_beside_its_switch()
        {
            var medium = Advances("BarlowMedium");
            double Width(string text) => text.Sum(ch => medium.TryGetValue(ch, out var em) ? em : 0.75) * Theme.SizeBody;
            foreach (var effect in PanelLeds.Effects)
            {
                var room = PanelLeds.EffectTileMinWidth - 12 - PanelShell.SwitchWidth - 24;
                Assert.True(Width(effect.Label) <= room, effect.Label + " is " + Width(effect.Label) + " in a tile that leaves it " + room);
            }
            Assert.Equal("Speeding in the pit lane", PanelLeds.Effects.OrderByDescending(effect => Width(effect.Label)).First().Label);
            // The tile is drawn with those numbers.
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var tile = leds.Substring(leds.IndexOf("private static Border LedsEffectTile(", StringComparison.Ordinal));
            tile = tile.Substring(0, tile.IndexOf("private FrameworkElement LedsEveryStripSection(", StringComparison.Ordinal));
            Assert.Contains("toggle.Margin = new Thickness(12, 0, 0, 0);", tile);
            Assert.Contains("Ui.Text(label, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);", tile);
            Assert.Contains("Padding = new Thickness(12, 10, 12, 10),", tile);
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

        /// <summary>
        /// The sheet's pictures shrink to the room rather than being cut: the tiles' and the shape step's, as a
        /// card's does, and the grid never lays a tile narrower than the Fanatec tile's picture inside its padding
        /// and border. Read as text because the page is WPF.
        /// </summary>
        [Fact]
        public void The_sheets_pictures_fit_the_sheet()
        {
            var style = StripStyle.Card;
            double Group(int n) => n * style.Led + (n - 1) * style.Gap;
            var fanatec = Group(PanelLights.FanatecSide) * 2 + Group(PanelLights.FanatecCentre) + 2 * style.GroupGap + 2 * style.PadX;
            Assert.Equal(171, fanatec);
            Assert.True(PanelLeds.HardwareTileMinWidth >= fanatec + 2 * (PanelKit.ChoiceTilePadding + PanelMetrics.BorderWeight));
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var tile = leds.Substring(leds.IndexOf("private static Button LedsHardwareTile(", StringComparison.Ordinal));
            tile = tile.Substring(0, tile.IndexOf("Ui.ChoiceTile(", StringComparison.Ordinal));
            Assert.Contains("StretchDirection = StretchDirection.DownOnly", tile);
            Assert.Contains("Child = Ui.Strip(frame, StripStyle.Card),", tile);
            var shape = leds.Substring(leds.IndexOf("Action showPicture = () =>", StringComparison.Ordinal));
            shape = shape.Substring(0, shape.IndexOf("note.Text = ", StringComparison.Ordinal));
            Assert.Contains("StretchDirection = StretchDirection.DownOnly", shape);
            Assert.Contains("Child = Ui.Strip(PanelLeds.ShapeFrame(side, centre), StripStyle.AddLeds),", shape);
        }

        /// <summary>
        /// The header puts the state and its presses under the name where two columns do not fit, in a WrapPanel so
        /// they drop to a second line rather than running out of the column, and the chip trims its hardware
        /// before its numerals are cut. Read as text because the page is WPF.
        /// </summary>
        [Fact]
        public void The_header_stacks_where_it_would_not_fit()
        {
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var header = leds.Substring(leds.IndexOf("private FrameworkElement LedsHeader(", StringComparison.Ordinal));
            header = header.Substring(0, header.IndexOf("private static string LedsProfileBlocked(", StringComparison.Ordinal));
            Assert.Contains("var stacked = !TwoColumns;", header);
            Assert.Contains("var row = stacked ? (FrameworkElement)Ui.VStack(8, name, actions) : Ui.Row(name, actions);", header);
            Assert.Contains("lead.TextTrimming = TextTrimming.CharacterEllipsis;", header);
            Assert.Contains("DockPanel.SetDock(numerals, Dock.Right);", header);
            var actions = leds.Substring(leds.IndexOf("private FrameworkElement BuildLedBarActions(", StringComparison.Ordinal));
            actions = actions.Substring(0, actions.IndexOf("private void InstallLedBarProfile(", StringComparison.Ordinal));
            Assert.Contains("var actions = new WrapPanel", actions);
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
            // The one form every line on the page points at the log in.
            Assert.Equal("See SimHub's log.", PanelLeds.PassedOverNote);
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

        /// <summary>
        /// The footer's name is the name the press adds: PanelLeds.NameToAdd is held to what
        /// OpenDashSettings.AddLedBar settles, for a typed name, a cleared box (the page passes the name the sheet
        /// opened on, as AddLedBar would otherwise fall back to the shape id) and a name the rig has.
        /// </summary>
        [Theory]
        [InlineData(" Rim ")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Strip")]
        [InlineData("strip")]
        [InlineData("Wheel rim")]
        public void The_footers_name_is_the_one_the_settings_add(string typed)
        {
            var settings = new OpenDashSettings();
            settings.AddLedBar("0-15-0", "Strip", null);
            settings.AddLedBar("3-9-3-fanatec", "Wheel rim", null);
            var taken = settings.LedBarList().Select(bar => bar.Name).ToList();
            foreach (var shape in new[] { "0-15-0", "3-9-3-fanatec" })
            {
                var opened = PanelLeds.DefaultName(shape, taken);
                var footer = PanelLeds.NameToAdd(typed, opened, taken);
                var copy = new OpenDashSettings();
                copy.AddLedBar("0-15-0", "Strip", null);
                copy.AddLedBar("3-9-3-fanatec", "Wheel rim", null);
                // As SettingsControl.Lights.cs's AddLedBar hands it over.
                var added = copy.AddLedBar(shape, string.IsNullOrWhiteSpace(typed) ? opened : typed, null);
                Assert.Equal(footer, added.Name);
            }
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("Settings.AddLedBar(shape, string.IsNullOrWhiteSpace(name) ? DefaultBarName(shape) : name, device)", leds);
            Assert.Contains("PanelLeds.NameToAdd(name.Text, DefaultBarName(PanelLights.BarShapeId(side, centre, fanatec)), TakenBarNames())", leds);
        }

        /// <summary>The sheet's rules: whether it has anything to offer, which tile and shape it opens on, the note
        /// beside the Fanatec tile and the device step's empty state.</summary>
        [Fact]
        public void The_sheet_opens_where_the_build_and_SimHub_say()
        {
            Assert.False(PanelLeds.SheetHasShapes(0, false));
            Assert.True(PanelLeds.SheetHasShapes(0, true));
            Assert.True(PanelLeds.SheetHasShapes(3, false));
            // The Fanatec tile wherever the build has no plain shape, since it is then the only tile.
            Assert.True(PanelLeds.SheetStartsOnFanatec(false, true, false));
            Assert.True(PanelLeds.SheetStartsOnFanatec(true, true, true));
            Assert.False(PanelLeds.SheetStartsOnFanatec(true, true, false));
            Assert.False(PanelLeds.SheetStartsOnFanatec(true, false, true));
            // 3 · 9 · 3 where the build has it, else the fewest, and the Fanatec wheel's with no plain shape.
            Assert.Equal(3, PanelLeds.StartSide(new[] { 0, 1, 2, 3, 4 }));
            Assert.Equal(0, PanelLeds.StartSide(new[] { 0, 4 }));
            Assert.Equal(PanelLights.FanatecSide, PanelLeds.StartSide(new int[0]));
            Assert.Equal(9, PanelLeds.KeptCentre(new[] { 4, 9, 12 }, 9));
            Assert.Equal(12, PanelLeds.KeptCentre(new[] { 4, 9, 12 }, 12));
            Assert.Equal(9, PanelLeds.KeptCentre(new[] { 4, 9, 12 }, 25));
            Assert.Equal(10, PanelLeds.KeptCentre(new[] { 10, 11 }, 25));
            Assert.Equal(PanelLights.FanatecCentre, PanelLeds.KeptCentre(new int[0], 9));
            Assert.True(PanelLeds.ShowsOtherWheelNote(true));
            Assert.False(PanelLeds.ShowsOtherWheelNote(false));
            // A device passed over is a disabled row saying so, so the prose says only what no row can.
            Assert.True(PanelLeds.ShowsNoDevices(0, 0));
            Assert.False(PanelLeds.ShowsNoDevices(0, 1));
            Assert.False(PanelLeds.ShowsNoDevices(1, 0));
            Assert.Equal("Remove Rim", PanelLeds.RemoveTitle("Rim"));
        }

        /// <summary>The strip the page shows, and the chip its preview opens on: the one selected, else the first,
        /// and the chip pressed only for the strip it was pressed for.</summary>
        [Fact]
        public void The_page_shows_the_selected_strip_and_a_new_one_opens_on_Live()
        {
            Assert.Equal(-1, PanelLeds.ShownStrip(new string[0], "LedRim"));
            Assert.Equal(1, PanelLeds.ShownStrip(new[] { "LedBrow", "LedRim" }, "LedRim"));
            // A selection that is gone falls back to the first.
            Assert.Equal(0, PanelLeds.ShownStrip(new[] { "LedBrow", "LedRim" }, "LedGone"));
            Assert.Equal(0, PanelLeds.ShownStrip(new[] { "LedBrow" }, null));
            Assert.Equal(PanelEmulation.Yellow, PanelLeds.ScenarioFor(PanelEmulation.Yellow, "LedRim", "LedRim"));
            Assert.Equal(PanelLeds.LiveScenario, PanelLeds.ScenarioFor(PanelEmulation.Yellow, "LedRim", "LedBrow"));
            Assert.Equal(PanelLeds.LiveScenario, PanelLeds.ScenarioFor(PanelEmulation.Yellow, null, "LedBrow"));
        }

        /// <summary>
        /// What each control writes, and the gate that decides whether it is drawn, read out of the page's own
        /// source: a control that stopped writing its setting, or a row drawn for every strip in place of the
        /// strips that carry it, would otherwise leave every test green. Each write is saved at once.
        /// </summary>
        [Fact]
        public void Each_control_writes_its_own_setting_and_saves_it()
        {
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            foreach (var write in new[]
            {
                "Settings.SetBarEffect(ns, id, on);",
                "Settings.SetBarBrightness(ns, value);",
                "if (bar == null || !Settings.SetBarReversed(ns, reversed)) return;",
                "if (live != null) live.FlagAnimation = on;",
                "if (live != null) live.SpotterWhole = on;",
                "if (live != null) live.Centre = Contract.LedCentres[i];",
                "Settings.LedMirrorFit = value;",
                "if (live != null) live.RpmStyle = Contract.NormaliseChoice(on ? Contract.LedRpmStyleCar : Contract.LedRpmStyleLeftToRight, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle);",
                "Settings.RenameLedBar(ns, wanted);",
                "bar.Device = LedBar.NormaliseDevice(device);",
            })
            {
                var at = leds.IndexOf(write, StringComparison.Ordinal);
                Assert.True(at >= 0, "the page no longer carries: " + write);
                Assert.StartsWith("Save();", leds.Substring(at + write.Length).TrimStart(), StringComparison.Ordinal);
            }
            // What each control reads, and which strips it is drawn for.
            foreach (var read in new[]
            {
                "foreach (var effect in PanelLeds.EffectsFor(bar.Shape))",
                "LedsEffectTile(effect.Label, Settings.BarEffectEnabled(ns, id),",
                "PanelLeds.BrightnessIndex(Settings.BarBrightness(ns))",
                "if (bar.SupportsReversal)",
                "var reverse = Ui.Switch(Settings.BarReversed(ns), on => ReverseLedBar(ns, on));",
                "Ui.Switch(Settings.BarFlagAnimation(ns),",
                "if (PanelLeds.HasFullStripSpotter(bar.Shape))",
                "Ui.Switch(Settings.BarSpotterWhole(ns),",
                "PanelLeds.CentreIndex(Settings.BarCentre(ns))",
                "BuildSegmented(Contract.LedMirrorFits, PanelLights.MirrorFitLabels, Settings.LedMirrorFit,",
                "BuildLedDeviceRow(targets, declined, Settings.BarDevice(ns), PanelLeds.DevicePickerWidth(ContentWidth, TwoColumns), value => MoveLedBar(ns, value))",
                "var row = PanelLeds.DeviceRow(targets.Select(t => new LedDeviceEntry(t.Id, t.Name, t.Connected)).ToList(), current, declined);",
                "picker.MaxWidth = pickerWidth;",
                "link.Click += (sender, args) => Go(PanelPage.Rig);",
                "Ui.Switch(PanelLeds.UsesCarRevLights(Settings.BarRpmStyle(ns)),",
                // Ruling 47: the width row only while the switch is on.
                "width.Visibility = PanelLeds.ShowsMirrorFit(Settings.BarRpmStyle(ns)) ? Visibility.Visible : Visibility.Collapsed;",
                // Ruling 48: the car line only while the switch is on, in the game the tables cover.
                "var on = PanelLeds.UsesCarRevLights(Settings.BarRpmStyle(ns));",
                "PanelLeds.CarLine(on, live.CarModel, plugin.LiveCarHasTable, loaded, PanelLeds.TablesCoverGame(live.GameName))",
                // Ruling 49: Live draws the car's run only where the profile does.
                "var running = PanelLeds.LiveRuns(lights.Ready, Settings.BarRpmStyle(ns), Settings.BarCentre(ns));",
                // The fix box, for a profile SimHub holds and has not selected.
                "var issue = PanelAttention.Of(issues, PanelAttention.StripUnselected, bar.Namespace);",
                "if (fix != null) parts.Add(fix);",
                // The car tables' button greys out while a download is out.
                "carTablesButton.IsEnabled = !carTablesDownloading;",
            })
            {
                Assert.True(leds.Contains(read), "the page no longer carries: " + read);
            }
            Assert.DoesNotContain("PanelLeds.Effects)", leds);
            // NEW on Brightness, Reverse direction and the Effects heading, for one release: taking them off at the
            // next cut moves this count, deliberately.
            Assert.Equal(3, Occurrences(leds, "Ui.NewTag()"));
            Assert.Contains("LedsRow(PanelLeds.BrightnessTitle, brightness, null, Ui.NewTag())", leds);
            Assert.Contains("LedsRow(PanelLeds.ReverseTitle, reverse, null, Ui.NewTag())", leds);
        }

        /// <summary>Every anchor the page model names is one the page attaches to a row, so search, Home's fix rows
        /// and the capture scripts land on it rather than on the page's top.</summary>
        [Fact]
        public void Every_anchor_is_attached_to_a_row()
        {
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var anchors = typeof(PanelLeds).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.Name.StartsWith("Anchor", StringComparison.Ordinal))
                .Select(field => field.Name)
                .ToList();
            Assert.Equal(15, anchors.Count);
            foreach (var name in anchors)
            {
                Assert.True(leds.Contains(", PanelLeds." + name + ")"), "no Ui.Anchor(..., PanelLeds." + name + ") on the page");
            }
        }

        /// <summary>The artboard's layout numbers the page draws: the .dcard grid's gap and its three columns, the
        /// .fx grid's gap and its three to a row, and the .hw tiles two to a row 8 apart.</summary>
        [Fact]
        public void The_grids_are_laid_as_the_artboards_lay_them()
        {
            Assert.Equal(12, PanelLeds.CardGap);
            Assert.Equal(3, PanelLeds.CardColumns);
            Assert.Equal(210, PanelLeds.CardMinWidth);
            Assert.Equal(6, PanelLeds.EffectTileGap);
            Assert.Equal(3, PanelLeds.EffectColumns);
            Assert.Equal(220, PanelLeds.EffectTileMinWidth);
            Assert.Equal(200, PanelLeds.HardwareTileMinWidth);
            Assert.Equal(8, PanelLeds.HardwareTileGap);
            Assert.Equal(22, PanelLeds.PreviewGroupGap);
            Assert.Equal(10, PanelLeds.FoundInSimHubSize);
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

        /// <summary>
        /// The picker takes at most half the room its row has, so "SimHub device" and the caption under it keep
        /// the other half: where two columns first fit (760 of content, a column of 360) a fixed 260 left the
        /// title 80 px, less than the 97.5 it needs at 15 px Medium.
        /// </summary>
        [Fact]
        public void The_device_picker_leaves_the_row_its_title()
        {
            Assert.Equal(40, PanelLeds.ColumnGap);
            Assert.Equal(170, PanelLeds.DevicePickerWidth(760, true));
            Assert.Equal(260, PanelLeds.DevicePickerWidth(1600, true));
            Assert.Equal(193, PanelLeds.DevicePickerWidth(406, false));
            Assert.Equal(PanelLeds.DevicePickerMinWidth, PanelLeds.DevicePickerWidth(200, false));
            var medium = Advances("BarlowMedium");
            var title = PanelLights.BarDeviceTitle.Sum(ch => medium.TryGetValue(ch, out var em) ? em : 0.75) * 15;
            foreach (var (content, two) in new[] { (760.0, true), (1000.0, true), (1600.0, true), (406.0, false), (527.0, false), (744.0, false) })
            {
                var column = two ? (content - PanelLeds.ColumnGap) / 2 : content;
                var left = column - PanelKit.RowGapLeds - PanelLeds.DevicePickerWidth(content, two);
                Assert.True(left >= title, "at " + content + " the title has " + left + " for " + title);
            }
        }

        /// <summary>
        /// The SimHub device row's shapes, decided by PanelLeds.DeviceRow: no device says so, one device the strip
        /// is on says where it goes and draws no picker, a strip on a device SimHub does not list keeps its own
        /// entry first and selected, even beside one device, and a device SimHub is not talking to says so.
        /// </summary>
        [Fact]
        public void The_device_row_says_where_the_profile_goes_and_asks_only_where_there_is_a_choice()
        {
            var wheel = new LedDeviceEntry("wheel", "Fanatec CSL Elite", true);
            var arduino = new LedDeviceEntry(LedBar.ArduinoDevice, "Arduino RGB LEDs", true);
            var unplugged = new LedDeviceEntry("hub", "Button hub", false);

            var none = PanelLeds.DeviceRow(new LedDeviceEntry[0], LedBar.ArduinoDevice, null);
            Assert.False(none.HasPicker);
            Assert.Equal(PanelLights.NoDevices, none.Caption);
            Assert.Equal("Rim has no LEDs OpenDash can reach. See SimHub's log.", PanelLeds.DeviceRow(null, "wheel", new[] { "Rim" }).Caption);

            var one = PanelLeds.DeviceRow(new[] { wheel }, "wheel", null);
            Assert.False(one.HasPicker);
            Assert.Equal("Goes to Fanatec CSL Elite.", one.Caption);
            Assert.Equal("Goes to Button hub (not connected).", PanelLeds.DeviceRow(new[] { unplugged }, "hub", null).Caption);

            // The strip's own device, gone from SimHub: first and selected, never the device that sorted first.
            var gone = PanelLeds.DeviceRow(new[] { wheel }, "old", null);
            Assert.True(gone.HasPicker);
            Assert.Equal(new[] { "old", "wheel" }, gone.Ids);
            Assert.Equal(new[] { PanelLights.DeviceGone, "Fanatec CSL Elite" }, gone.Labels);
            Assert.Equal(0, gone.Selected);
            Assert.Null(gone.Caption);

            var two = PanelLeds.DeviceRow(new[] { arduino, unplugged }, "hub", new[] { "Rim" });
            Assert.True(two.HasPicker);
            Assert.Equal(new[] { LedBar.ArduinoDevice, "hub" }, two.Ids);
            Assert.Equal(new[] { "Arduino RGB LEDs", "Button hub (not connected)" }, two.Labels);
            Assert.Equal(1, two.Selected);
            Assert.Equal("Rim has no LEDs OpenDash can reach. See SimHub's log.", two.Caption);
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
            Assert.Equal("Moved Rim's profile to Arduino RGB LEDs. Select \"Rim\" on Arduino RGB LEDs in SimHub to use it.", PanelLeds.Moved("Rim", "Arduino RGB LEDs"));
            Assert.Equal("Moved Rim's profile. Select \"Rim\" in SimHub to use it.", PanelLeds.Moved("Rim", null));
            Assert.Equal("Renamed to Rim in OpenDash and SimHub.", PanelLeds.Renamed("Rim", true));
            Assert.Equal("Renamed to Rim.", PanelLeds.Renamed("Rim", false));
            // The install takes the old copy out first, so a failure leaves no copy under the old name.
            Assert.Equal("Renamed to Rim, but its profile could not be installed again. See SimHub's log.", PanelLeds.RenameNotInSimHub("Rim"));
            Assert.Equal("Removed Rim and its profile.", PanelLeds.Removed("Rim", true, true));
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
            Assert.Equal("Moved Rim's profile to Wheel. " + note + " Select \"Rim\" on Wheel in SimHub to use it.", PanelLeds.Moved("Rim", "Wheel", note));
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
            Assert.Equal("Renamed to Rim. This strip's device is not in SimHub. Choose one under SimHub device.",
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
            // The steps left in order: a device in SimHub, the profile installed here, then the restart the strip's
            // own settings wait on, as BarAdded says it.
            Assert.Equal("Added Rim. Add your wheel or Arduino in SimHub, install Rim's profile here, then restart SimHub.", PanelLeds.AddedWithoutDevice("Rim"));
            Assert.Contains("Restart SimHub", PanelLights.BarAdded("Rim", "Wheel"));
        }

        /// <summary>
        /// Every press that installs names the select in one form, PanelLeds.SelectIt, and an Add says it behind
        /// the restart.
        /// </summary>
        [Fact]
        public void The_select_step_has_one_form()
        {
            var select = PanelLeds.SelectIt("Rim", "Wheel");
            Assert.Equal("Select \"Rim\" on Wheel in SimHub to use it.", select);
            Assert.EndsWith(select, PanelLeds.ProfileInstalled("Rim", "Wheel"));
            Assert.EndsWith(select, PanelLeds.Moved("Rim", "Wheel"));
            Assert.EndsWith(select, PanelLeds.ReverseSaid("Rim", true, false, "Wheel"));
            Assert.EndsWith(PanelLights.BarAddedRestart + "s" + select.Substring(1), PanelLights.BarAdded("Rim", "Wheel"));
            Assert.EndsWith(PanelLights.BarAddedRestart + "s" + PanelLeds.SelectIt("Rim", null).Substring(1), PanelLights.BarAdded("Rim", null));
        }

        /// <summary>
        /// Remove says what it took out: the profile where SimHub gave it up, the strip alone where SimHub never
        /// held one, and the log where SimHub held one and kept it or the uninstall threw.
        /// </summary>
        [Fact]
        public void Remove_says_whether_the_profile_left_SimHub()
        {
            Assert.Equal("Removed Rim and its profile.", PanelLeds.Removed("Rim", true, true));
            Assert.Equal("Removed Rim and its profile.", PanelLeds.Removed("Rim", false, true));
            Assert.Equal("Removed Rim.", PanelLeds.Removed("Rim", false, false));
            Assert.Equal("Removed Rim, but its profile could not be taken out of SimHub. See SimHub's log.", PanelLeds.Removed("Rim", true, false));
            Assert.Equal(PanelLeds.Removed("Rim", true, false), PanelLeds.Removed("Rim", false, null));
            Assert.True(PanelLeds.RemovedCleanly(true, true));
            Assert.True(PanelLeds.RemovedCleanly(false, false));
            Assert.False(PanelLeds.RemovedCleanly(true, false));
            Assert.False(PanelLeds.RemovedCleanly(false, null));
            // The page keeps what the uninstall returned, and says the line in caution where it failed.
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("takenOut = StripInstaller.UninstallEverywhere(LedBarProfile.IdFor(ns));", leds);
            Assert.Contains("Say(PanelLeds.Removed(name, held, takenOut), PanelLeds.RemovedCleanly(held, takenOut));", leds);
        }

        /// <summary>
        /// Focus handed back after a redraw does not scroll: a focused control asks to be brought into view, which
        /// scrolled the line Say had just shown at the top of the page away again. Read as text because the page
        /// is WPF.
        /// </summary>
        [Fact]
        public void Focus_handed_back_leaves_the_line_in_view()
        {
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var focus = leds.Substring(leds.IndexOf("private void LedsFocusLater(", StringComparison.Ordinal));
            focus = focus.Substring(0, focus.IndexOf("DispatcherPriority.Loaded);", StringComparison.Ordinal));
            Assert.Contains("element.AddHandler(FrameworkElement.RequestBringIntoViewEvent, stay);", focus);
            Assert.Contains("element.Focus();", focus);
            Assert.Contains("element.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent, stay);", focus);
            Assert.Contains("(sender, args) => args.Handled = true", focus);
            // And no control on the page is focused around it.
            Assert.Equal(1, Occurrences(leds, ".Focus()"));
        }

        /// <summary>
        /// A wheel's brightness press repaints the Brightness chooser's first entry, but never under its open list:
        /// the redraw waits for the list to close.
        /// </summary>
        [Fact]
        public void The_brightness_chooser_is_not_replaced_under_its_open_list()
        {
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var lighting = leds.Substring(leds.IndexOf("var owed = false;", StringComparison.Ordinal));
            lighting = lighting.Substring(0, lighting.IndexOf("rows.Add(", StringComparison.Ordinal));
            Assert.Contains("if (open == null || open.IsChecked != true)", lighting);
            Assert.Contains("open.Unchecked += closed;", lighting);
            Assert.Contains("open.Unchecked -= closed;", lighting);
        }

        /// <summary>A Brightness pick says what it set (ruling 50), in a line of its own.</summary>
        [Fact]
        public void A_brightness_pick_says_what_it_set()
        {
            Assert.Equal("Set Rim's brightness to 60%.", PanelLeds.BrightnessSaid("Rim", 60));
            Assert.Equal("Rim's brightness follows the rig's.", PanelLeds.BrightnessSaid("Rim", null));
            var leds = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var pick = leds.Substring(leds.IndexOf("Settings.SetBarBrightness(ns, value);", StringComparison.Ordinal));
            pick = pick.Substring(0, pick.IndexOf("}, 160);", StringComparison.Ordinal));
            Assert.Contains("ClearMessages();", pick);
            Assert.Contains("Say(PanelLeds.BrightnessSaid(live.Name, value));", pick);
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
