// PanelEmulationTests.cs: what the Rig page paints, held to the rules Rig.dc.html scripts and to the glyph
// sheet the build writes.
//
// The strip and band rules are the artboard's (#503 canvas, Rig.dc.html's renderVals), pinned here as
// literals until the artboard is copied to design/canvas/plugin/. The matrix is drawn from the flag box's
// own glyphs, so its test reads the sheet the build wrote -- Resources/ when the plugin has been packaged,
// build/ when only the dash has -- and fails on CI when neither has it.
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelEmulationTests
    {
        private static bool OnCI =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        [Fact]
        public void The_chips_are_the_artboards_five_groups_in_the_panels_words()
        {
            Assert.Equal(new[] { "Flags", "Spotter", "Pit lane", "Warnings", "Revs" }, PanelEmulation.Groups.Select(g => g.Title));
            Assert.Equal(new[] { "Green", "Yellow", "Blue", "White", "Black", "Chequered", "Red" }, PanelEmulation.Groups[0].Scenarios.Select(s => s.Label));
            Assert.Equal(new[] { "Car left", "Car right", "Both sides" }, PanelEmulation.Groups[1].Scenarios.Select(s => s.Label));
            // voice.md: one word per thing, so "Pit limiter" and the temperatures by name.
            Assert.Equal(new[] { "Pit limiter", "Speeding" }, PanelEmulation.Groups[2].Scenarios.Select(s => s.Label));
            Assert.Equal(new[] { "Low fuel", "Oil temperature", "Water temperature" }, PanelEmulation.Groups[3].Scenarios.Select(s => s.Label));
            Assert.Equal(new[] { "Idle", "Mid revs", "Shift point" }, PanelEmulation.Groups[4].Scenarios.Select(s => s.Label));
            var ids = PanelEmulation.Scenarios().Select(s => s.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.NotNull(PanelEmulation.Find(PanelEmulation.Default));
        }

        [Fact]
        public void Every_swatch_is_a_colour_the_theme_carries()
        {
            var theme = typeof(Theme).GetFields().Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null)).ToList();
            foreach (var scenario in PanelEmulation.Scenarios()) Assert.Contains(scenario.SwatchHex, theme);
            foreach (var colour in PanelEmulation.Scenarios().Select(s => s.Id).SelectMany(id => PanelEmulation.StripFrame(3, 9, id)).SelectMany(g => g).Where(c => c != null))
            {
                Assert.Contains(colour, theme);
            }
        }

        [Fact]
        public void The_revs_fill_by_level_in_the_three_stages()
        {
            // Idle: dark. Mid: six tenths lit, 45 per cent green, to 80 amber. Shift: all red.
            Assert.All(PanelEmulation.Revs(10, PanelEmulation.Idle), led => Assert.Null(led));
            Assert.Equal(
                new[] { Theme.ShiftStage1, Theme.ShiftStage1, Theme.ShiftStage1, Theme.ShiftStage1, Theme.ShiftStage2, Theme.ShiftStage2, null, null, null, null },
                PanelEmulation.Revs(10, PanelEmulation.Mid));
            Assert.All(PanelEmulation.Revs(10, PanelEmulation.Shift), led => Assert.Equal(Theme.ShiftStage3, led));
            Assert.Equal(0.6, PanelEmulation.RevLevel(PanelEmulation.Yellow));
            Assert.Empty(PanelEmulation.Revs(0, PanelEmulation.Mid));
        }

        [Fact]
        public void A_flag_fills_the_ends_and_leaves_the_revs_in_the_middle()
        {
            var frame = PanelEmulation.StripFrame(3, 9, PanelEmulation.Yellow);
            Assert.Equal(3, frame.Length);
            Assert.All(frame[0], led => Assert.Equal(Theme.FlagYellow, led));
            Assert.All(frame[2], led => Assert.Equal(Theme.FlagYellow, led));
            Assert.Equal(PanelEmulation.Revs(9, PanelEmulation.Yellow), frame[1]);
            // Black lights white: a black LED is an unlit one.
            Assert.All(PanelEmulation.StripFrame(3, 9, PanelEmulation.Black)[0], led => Assert.Equal(Theme.FlagBlack, led));
        }

        [Fact]
        public void A_bare_run_carries_a_flag_across_itself_and_no_spotter()
        {
            var flag = PanelEmulation.StripFrame(0, 15, PanelEmulation.Blue);
            Assert.Single(flag);
            Assert.All(flag[0], led => Assert.Equal(Theme.FlagBlue, led));
            Assert.Equal(PanelEmulation.Revs(15, PanelEmulation.CarLeft), PanelEmulation.StripFrame(0, 15, PanelEmulation.CarLeft)[0]);
        }

        [Fact]
        public void A_car_alongside_lights_its_own_end()
        {
            var left = PanelEmulation.StripFrame(3, 9, PanelEmulation.CarLeft);
            Assert.All(left[0], led => Assert.Equal(Theme.Caution, led));
            Assert.All(left[2], led => Assert.Null(led));
            var right = PanelEmulation.StripFrame(3, 9, PanelEmulation.CarRight);
            Assert.All(right[0], led => Assert.Null(led));
            Assert.All(right[2], led => Assert.Equal(Theme.Caution, led));
            var both = PanelEmulation.StripFrame(3, 9, PanelEmulation.CarBoth);
            Assert.All(both[0].Concat(both[2]), led => Assert.Equal(Theme.Caution, led));
        }

        [Fact]
        public void The_full_strip_spotter_lights_the_half_on_the_cars_side()
        {
            var frame = PanelEmulation.StripFrame(3, 8, PanelEmulation.CarLeft, new StripOptions { SpotterWhole = true });
            Assert.All(frame[1].Take(4), led => Assert.Equal(Theme.Caution, led));
            Assert.NotEqual(Theme.Caution, frame[1][4]);
        }

        [Fact]
        public void The_limiter_alternates_and_speeding_reddens_the_ends()
        {
            var limiter = PanelEmulation.StripFrame(3, 9, PanelEmulation.Limiter);
            Assert.Equal(new[] { Theme.PitLimiter, null, Theme.PitLimiter }, limiter[0]);
            Assert.Equal(Theme.PitLimiter, limiter[1][0]);
            Assert.Null(limiter[1][1]);
            var speeding = PanelEmulation.StripFrame(3, 9, PanelEmulation.Speeding);
            Assert.All(speeding[0].Concat(speeding[2]), led => Assert.Equal(Theme.Danger, led));
        }

        [Fact]
        public void An_effect_switched_off_is_not_drawn()
        {
            var off = new StripOptions();
            off.EffectsOff.Add("flag.yellow");
            // Every flag row is one switch, so the blue goes with the yellow.
            Assert.All(PanelEmulation.StripFrame(3, 9, PanelEmulation.Blue, off)[0], led => Assert.Null(led));
            var noLeft = new StripOptions();
            noLeft.EffectsOff.Add("spotter.left");
            Assert.All(PanelEmulation.StripFrame(3, 9, PanelEmulation.CarLeft, noLeft)[0], led => Assert.Null(led));
        }

        [Fact]
        public void A_face_band_says_what_the_dash_says()
        {
            Assert.Equal("YELLOW", PanelEmulation.Band(PanelEmulation.Yellow).Text);
            Assert.Equal(Theme.FlagYellow, PanelEmulation.Band(PanelEmulation.Yellow).FillHex);
            Assert.Equal(Theme.OnFlag, PanelEmulation.Band(PanelEmulation.Yellow).TextHex);
            Assert.Equal("WHITE · LAST LAP", PanelEmulation.Band(PanelEmulation.White).Text);
            Assert.True(PanelEmulation.Band(PanelEmulation.Black).Outlined);
            Assert.True(PanelEmulation.Band(PanelEmulation.Chequer).Chequer);
            Assert.Null(PanelEmulation.Band(PanelEmulation.Chequer).Text);
            Assert.Equal("Pit limiter", PanelEmulation.Band(PanelEmulation.Limiter).Text);
            Assert.False(PanelEmulation.Band(PanelEmulation.Mid).Alert);
            Assert.False(PanelEmulation.Band(PanelEmulation.LowFuel).Alert);
        }

        [Fact]
        public void The_lights_dim_to_the_night_brightness()
        {
            Assert.Equal(1, PanelEmulation.Dim(false, 25));
            Assert.Equal(0.25, PanelEmulation.Dim(true, 25));
            Assert.Equal(0, PanelEmulation.Dim(true, -5));
            Assert.Equal(1, PanelEmulation.Dim(true, 150));
            // The Settings preview follows the day brightness as well.
            Assert.Equal(0.8, PanelEmulation.Dim(false, 80, 25));
            Assert.Equal(0.25, PanelEmulation.Dim(true, 80, 25));
            Assert.Equal(1, PanelEmulation.Dim(false, 120, 25));
            Assert.Equal(0, PanelEmulation.Dim(true, 80, -1));
        }

        [Fact]
        public void A_panel_shows_the_glyph_its_families_and_side_allow()
        {
            var box = new MatrixOptions { Rest = "gear" };
            Assert.Equal("yellow", PanelEmulation.GlyphFor(PanelEmulation.Yellow, box));
            Assert.Equal("chequered", PanelEmulation.GlyphFor(PanelEmulation.Chequer, box));
            Assert.Equal("Spotter carBoth", PanelEmulation.GlyphFor(PanelEmulation.CarBoth, box));
            Assert.Equal("Pit limiterInLane", PanelEmulation.GlyphFor(PanelEmulation.Limiter, box));
            Assert.Equal("Warning waterHot", PanelEmulation.GlyphFor(PanelEmulation.Water, box));
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelEmulation.Idle, box));
            Assert.Equal("Gear 4 stage1", PanelEmulation.GlyphFor(PanelEmulation.Mid, box));
            Assert.Equal("Gear 4 redline", PanelEmulation.GlyphFor(PanelEmulation.Shift, box));

            // A panel mounted on the left lights for a car on the left only, and rests otherwise.
            var left = new MatrixOptions { Side = "left", Rest = "gear" };
            Assert.Equal("Spotter carLeft", PanelEmulation.GlyphFor(PanelEmulation.CarBoth, left));
            Assert.Equal("Gear 4 stage1", PanelEmulation.GlyphFor(PanelEmulation.CarRight, left));

            // A family switched off leaves the idle display, and a dark idle display is dark.
            var quiet = new MatrixOptions { Flags = false };
            Assert.Null(PanelEmulation.GlyphFor(PanelEmulation.Yellow, quiet));
            Assert.Equal("Gear 4 rest", PanelEmulation.GlyphFor(PanelEmulation.Shift, new MatrixOptions { Rest = "gear", Bands = false }));
        }

        [Fact]
        public void Without_a_sheet_every_cell_is_dark()
        {
            var cells = PanelEmulation.MatrixFrame(PanelGlyphSheet.Empty, PanelEmulation.Yellow);
            Assert.Equal(64, cells.Length);
            Assert.All(cells, cell => Assert.Null(cell));
            Assert.All(PanelEmulation.MatrixFrame(null, PanelEmulation.Yellow), cell => Assert.Null(cell));
        }

        /// <summary>Every glyph name the emulation asks for is one the build draws, and a red flag is
        /// painted at the frame it holds, not the first of its three growing ones.</summary>
        [Fact]
        public void Every_glyph_the_emulation_asks_for_is_on_the_sheet_the_build_writes()
        {
            var sheet = BuiltSheet();
            if (sheet == null)
            {
                Assert.False(OnCI, "no flag-box-glyphs.json in " + RepoPaths.EmbeddedResources() + " or " + RepoPaths.BuildOutput());
                return;
            }
            var options = new[]
            {
                new MatrixOptions { Rest = "gear" },
                new MatrixOptions { Rest = "gear", Side = "left" },
                new MatrixOptions { Rest = "gear", Side = "right" },
                new MatrixOptions { Rest = "gear", Bands = false },
            };
            foreach (var scenario in PanelEmulation.Scenarios())
            {
                foreach (var option in options)
                {
                    var name = PanelEmulation.GlyphFor(scenario.Id, option);
                    Assert.True(name == null || sheet.Find(name) != null, scenario.Id + " asks for " + name + ", which the sheet does not draw");
                }
            }

            var red = PanelEmulation.MatrixFrame(sheet, PanelEmulation.Red, new MatrixOptions());
            var redGlyph = sheet.Find("red");
            Assert.Equal(Array.IndexOf(redGlyph.DurationsMs, redGlyph.DurationsMs.Max()), Array.IndexOf(redGlyph.Frames, redGlyph.Still));
            Assert.Equal(redGlyph.Still.SelectMany(row => row), red);
            Assert.Contains(Theme.FlagRed, red);
        }

        private static PanelGlyphSheet BuiltSheet()
        {
            foreach (var folder in new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() })
            {
                var path = Path.Combine(folder, "flag-box-glyphs.json");
                if (File.Exists(path)) return PanelGlyphSheet.Parse(File.ReadAllText(path));
            }
            return null;
        }

        [Fact]
        public void The_styles_are_the_artboards_sizes()
        {
            Assert.Equal(11, StripStyle.Home.Led);
            Assert.Equal(14, StripStyle.Rig.Led);
            Assert.Equal(9, StripStyle.Card.Led);
            Assert.Equal(30, StripStyle.Preview.Led);
            Assert.Equal(14, StripStyle.AddLeds.Led);
            Assert.Equal(6, MatrixStyle.Home.Cell);
            Assert.Equal(10, MatrixStyle.Rig.Cell);
            Assert.Equal(5, MatrixStyle.Card.Cell);
            Assert.Equal(26, MatrixStyle.Preview.Cell);
            // Rig.dc.html: eight 10 px cells, 2 apart, inside 6.
            Assert.Equal(8 * 10 + 7 * 2 + 12, MatrixStyle.Rig.Side);
        }

        /// <summary>
        /// Every number each style is drawn with, not only the LED and the cell: a radius, a gap, a group gap
        /// or a padding moved on one style changes that picture on its page and nothing else would say so.
        /// </summary>
        [Fact]
        public void Every_number_of_every_style_is_pinned()
        {
            // led, radius, gap, group gap, pad x, pad y, corner radius
            Assert.Equal(new double[] { 11, 2, 2, 7, 8, 7, Theme.Radius }, Numbers(StripStyle.Home));
            Assert.Equal(new double[] { 14, 3, 3, 8, 8, 6, 3 }, Numbers(StripStyle.Rig));
            Assert.Equal(new double[] { 9, 2, 2, 6, 0, 0, 0 }, Numbers(StripStyle.Card));
            Assert.Equal(new double[] { 30, 3, 6, 24, 0, 0, 0 }, Numbers(StripStyle.Preview));
            Assert.Equal(new double[] { 14, 2, 3, 8, 8, 8, Theme.Radius }, Numbers(StripStyle.AddLeds));
            // cell, gap, pad, corner radius
            Assert.Equal(new double[] { 6, 1, 4, Theme.Radius }, Numbers(MatrixStyle.Home));
            Assert.Equal(new double[] { 10, 2, 6, 3 }, Numbers(MatrixStyle.Rig));
            Assert.Equal(new double[] { 5, 1, 4, 0 }, Numbers(MatrixStyle.Card));
            Assert.Equal(new double[] { 26, 6, 0, 0 }, Numbers(MatrixStyle.Preview));
        }

        private static double[] Numbers(StripStyle style)
        {
            return new[] { style.Led, style.Radius, style.Gap, style.GroupGap, style.PadX, style.PadY, style.CornerRadius };
        }

        private static double[] Numbers(MatrixStyle style)
        {
            return new[] { style.Cell, style.Gap, style.Pad, style.CornerRadius };
        }

        /// <summary>
        /// The emulation writes the dash's own band words (ruling 26), so each face band's text is read out of
        /// packages/dash/src/flags.ts -- and the limiter's out of pitAlerts.ts -- rather than typed twice.
        /// </summary>
        [Theory]
        [InlineData(PanelEmulation.Green, "green")]
        [InlineData(PanelEmulation.Yellow, "yellow")]
        [InlineData(PanelEmulation.Blue, "blue")]
        [InlineData(PanelEmulation.White, "white")]
        [InlineData(PanelEmulation.Black, "black")]
        [InlineData(PanelEmulation.Red, "red")]
        public void A_flag_band_is_the_dashs_own_label(string scenario, string flagId)
        {
            Assert.Equal(DashBandLabel(flagId), PanelEmulation.Band(scenario).Text);
        }

        [Fact]
        public void The_limiter_band_is_the_dashs_own_label()
        {
            var alerts = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root(), "packages", "dash", "src", "components", "pitAlerts.ts"));
            var limiter = System.Text.RegularExpressions.Regex.Match(alerts, @"\{ id: 'limiter', label: '([^']+)'");
            Assert.True(limiter.Success, "pitAlerts.ts no longer declares the limiter's band as { id: 'limiter', label: ... }");
            Assert.Equal(limiter.Groups[1].Value, PanelEmulation.Band(PanelEmulation.Limiter).Text);
        }

        /// <summary>The band label of one condition in flags.ts' catalogue, found by its id.</summary>
        private static string DashBandLabel(string id)
        {
            var flags = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root(), "packages", "dash", "src", "flags.ts"));
            var start = System.Text.RegularExpressions.Regex.Match(flags, @"\bid: '" + id + "',");
            Assert.True(start.Success, "flags.ts has no condition with id '" + id + "'");
            var next = flags.IndexOf("id: '", start.Index + start.Length, StringComparison.Ordinal);
            var entry = next < 0 ? flags.Substring(start.Index) : flags.Substring(start.Index, next - start.Index);
            var label = System.Text.RegularExpressions.Regex.Match(entry, @"label: '([^']+)'");
            Assert.True(label.Success, "flags.ts' '" + id + "' has no band label");
            return label.Groups[1].Value;
        }

        /// <summary>
        /// The car's own rev lights as a strip picture: the centre from CarLights.Run's packed "#AARRGGBB"
        /// colours, a zero alpha drawn unlit, the ends unlit, and a run that is empty or the wrong length
        /// drawn dark. Home and LEDs both draw it, so neither writes a parser of its own.
        /// </summary>
        [Fact]
        public void A_live_run_is_the_centre_the_car_lights_and_the_ends_dark()
        {
            var packed = CarLightService.Packed(new[] { "#FF00FF00", "#00000000", "#FFFF0000", "#00123456" });
            var frame = PanelEmulation.LiveFrame(packed, 3, 4);
            Assert.Equal(3, frame.Length);
            Assert.Equal(new string[] { null, null, null }, frame[0]);
            Assert.Equal(new[] { "#FF00FF00", null, "#FFFF0000", null }, frame[1]);
            Assert.Equal(new string[] { null, null, null }, frame[2]);

            // A bare run is the centre alone, as StripFrame groups it.
            Assert.Single(PanelEmulation.LiveFrame(packed, 0, 4));

            // Run answers "" for a length it has no property for, and a run of another length is not guessed at.
            Assert.Equal(new string[] { null, null, null, null }, PanelEmulation.LiveFrame(string.Empty, 3, 4)[1]);
            Assert.Equal(new string[] { null, null, null, null }, PanelEmulation.LiveFrame(null, 3, 4)[1]);
            Assert.Equal(new string[] { null, null, null, null, null }, PanelEmulation.LiveFrame(packed, 3, 5)[1]);
            Assert.Equal(9, PanelEmulation.PackedColourWidth);
            Assert.Equal(PanelEmulation.PackedColourWidth, CarLightTable.Transparent.Length);
        }
    }
}
