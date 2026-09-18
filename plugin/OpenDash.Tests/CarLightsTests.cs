// CarLightsTests: the reader that survives somebody else's files, and the renderer that draws a
// car's bar onto a strip that is not the car's bar.
//
// The fixtures are invented rather than copied. openDash carries no measurements it has not made
// (ADR 0018), and a test that pinned a real car's RPMs would be carrying them -- so each fixture is
// the *shape* of a real pattern with round numbers: ascending, symmetric, blocked, gapped, single.
// What is being tested is that the shape survives, which is the whole claim.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class CarLightsTests
    {
        private const string Off = CarLightTable.Transparent;

        /// <summary>Ascending thresholds and three colours: the left-to-right bar, six LEDs.</summary>
        private const string LeftToRight = @"{
            ""carName"": ""Test Ascending"", ""carId"": ""test ascending"", ""carClass"": ""TST"",
            ""ledNumber"": 6, ""redlineBlinkInterval"": 200,
            ""ledColor"": [""#FF0000FF"", ""#FF00FF00"", ""#FF00FF00"", ""#FFFFFF00"", ""#FFFFFF00"", ""#FFFF0000"", ""#FFFF0000""],
            ""ledRpm"": [{
                ""R"": [7000, 1000, 2000, 3000, 4000, 5000, 6000],
                ""N"": [7000, 1000, 2000, 3000, 4000, 5000, 6000],
                ""1"": [7000, 1000, 2000, 3000, 4000, 5000, 6000],
                ""2"": [8000, 2000, 3000, 4000, 5000, 6000, 7000]
            }]
        }";

        /// <summary>Symmetric thresholds: the bar that fills from both ends towards the middle.</summary>
        private const string MeetInMiddle = @"{
            ""carName"": ""Test Symmetric"", ""carId"": ""test symmetric"",
            ""ledNumber"": 8, ""redlineBlinkInterval"": 0,
            ""ledColor"": [""#00000000"", ""#FF00FF00"", ""#FFFFFF00"", ""#FFFF0000"", ""#FFFF0000"", ""#FFFF0000"", ""#FFFF0000"", ""#FFFFFF00"", ""#FF00FF00""],
            ""ledRpm"": [{ ""1"": [9000, 1000, 2000, 3000, 4000, 4000, 3000, 2000, 1000] }]
        }";

        /// <summary>Two blocks of three, and a gap in the middle of the bar.</summary>
        private const string BlocksWithGap = @"{
            ""carName"": ""Test Blocks"", ""carId"": ""test blocks"",
            ""ledNumber"": 7, ""redlineBlinkInterval"": 250,
            ""ledColor"": [""#FFFF0000"", ""#FF00FF00"", ""#FF00FF00"", ""#FF00FF00"", ""#00000000"", ""#FFFF0000"", ""#FFFF0000"", ""#FFFF0000""],
            ""ledRpm"": [{ ""1"": [6000, 3000, 3000, 3000, 0, 5000, 5000, 5000] }]
        }";

        private static CarLightTable Parsed(string json)
        {
            var table = CarLightTable.Parse(json);
            Assert.NotNull(table);
            return table;
        }

        [Fact]
        public void Reads_a_car_file_including_the_gear_keys_that_are_not_legal_xml_names()
        {
            var table = Parsed(LeftToRight);
            Assert.Equal("test ascending", table.CarId);
            Assert.Equal("Test Ascending", table.CarName);
            Assert.Equal(6, table.LedCount);
            Assert.Equal(200, table.BlinkIntervalMs);
            // Index 0 of each array is the redline entry, and the LEDs are what follows it.
            Assert.Equal("#FF0000FF", table.BlinkColor);
            Assert.Equal(6, table.Colors.Length);
            Assert.Equal("#FF00FF00", table.Colors[0]);
            // "1" and "2" are numbers, so the JSON reader hands them over as an attribute rather than
            // as an element name. A reader that missed this would find R and N and no gears at all.
            Assert.Equal(new[] { "1", "2", "N", "R" }, table.Gears.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(7000, table.Gears["1"].Redline);
            Assert.Equal(new[] { 1000, 2000, 3000, 4000, 5000, 6000 }, table.Gears["1"].Thresholds);
            Assert.Equal(new[] { 2000, 3000, 4000, 5000, 6000, 7000 }, table.Gears["2"].Thresholds);
        }

        [Theory]
        // Not JSON at all, and neither is an HTML error page, which is what a proxy returns.
        [InlineData("")]
        [InlineData("<html>404</html>")]
        // No id to key it by, so nothing could ever select it.
        [InlineData(@"{ ""ledNumber"": 2, ""ledColor"": [""Red"", ""Red"", ""Red""], ""ledRpm"": [{ ""1"": [1, 2, 3] }] }")]
        // A bar with no LEDs.
        [InlineData(@"{ ""carId"": ""x"", ""ledNumber"": 0, ""ledColor"": [""Red""], ""ledRpm"": [{ ""1"": [1] }] }")]
        // One colour short: this is stockcars-fordtaurus03 in the real database, and the reason the
        // reader is suspicious rather than trusting. Which LED the colour belongs to is unknowable.
        [InlineData(@"{ ""carId"": ""x"", ""ledNumber"": 1, ""ledColor"": [""Red""], ""ledRpm"": [{ ""1"": [7000, 6000] }] }")]
        // A gear row that does not match the LED count.
        [InlineData(@"{ ""carId"": ""x"", ""ledNumber"": 2, ""ledColor"": [""Red"", ""Red"", ""Red""], ""ledRpm"": [{ ""1"": [7000, 6000] }] }")]
        // No gears at all, so there is nothing to draw in any gear.
        [InlineData(@"{ ""carId"": ""x"", ""ledNumber"": 1, ""ledColor"": [""Red"", ""Red""], ""ledRpm"": [{}] }")]
        public void A_file_that_does_not_agree_with_itself_is_refused_rather_than_half_read(string json)
        {
            // Null is the fallback signal: the car keeps the ladder iRacing publishes, which is what
            // every car had before the mirror existed.
            Assert.Null(CarLightTable.Parse(json));
        }

        [Fact]
        public void Lights_the_bar_left_to_right_as_the_revs_rise()
        {
            var table = Parsed(LeftToRight);
            Assert.Equal(new[] { Off, Off, Off, Off, Off, Off }, CarLightMirror.Colors(table, "1", 500, 6, MirrorFit.Stretch, 0));
            Assert.Equal(new[] { "#FF00FF00", Off, Off, Off, Off, Off }, CarLightMirror.Colors(table, "1", 1500, 6, MirrorFit.Stretch, 0));
            Assert.Equal(new[] { "#FF00FF00", "#FF00FF00", "#FFFFFF00", Off, Off, Off }, CarLightMirror.Colors(table, "1", 3500, 6, MirrorFit.Stretch, 0));
            Assert.Equal(new[] { "#FF00FF00", "#FF00FF00", "#FFFFFF00", "#FFFFFF00", "#FFFF0000", "#FFFF0000" }, CarLightMirror.Colors(table, "1", 6500, 6, MirrorFit.Stretch, 0));
        }

        [Fact]
        public void The_gear_is_the_answer_and_not_a_detail()
        {
            var table = Parsed(LeftToRight);
            // 32 of the 85 measured cars move their thresholds with the gear, and the four RPMs
            // iRacing publishes cannot: one block in the session string, one set for the car.
            Assert.Equal("#FF00FF00", CarLightMirror.Colors(table, "1", 1500, 6, MirrorFit.Stretch, 0)[0]);
            Assert.Equal(Off, CarLightMirror.Colors(table, "2", 1500, 6, MirrorFit.Stretch, 0)[0]);
            // A gear nobody measured takes the highest one below it rather than going dark.
            Assert.Equal(CarLightMirror.GearFor(table, "2"), CarLightMirror.GearFor(table, "6"));
            Assert.Equal(CarLightMirror.GearFor(table, "N"), CarLightMirror.GearFor(table, ""));
        }

        [Fact]
        public void A_symmetric_bar_stays_symmetric_at_every_run_length()
        {
            var table = Parsed(MeetInMiddle);
            // The claim the fit rule exists for: whatever the strip's length, a bar that fills from
            // both ends still does. Nothing in the renderer knows the word "symmetric".
            foreach (var run in new[] { 8, 9, 10, 12, 14, 15, 16, 18, 20, 25, 6, 5 })
            {
                var lit = CarLightMirror.Colors(table, "1", 2500, run, MirrorFit.Stretch, 0);
                // Enumerable.Reverse by name: on an array a newer compiler binds `.Reverse()` to the
                // Span overload, which returns void and reverses in place. Same trap as #320.
                Assert.Equal(lit, Enumerable.Reverse(lit).ToArray());
                // And it is filling from the ends: at 2500 rpm the outer pair is lit and the middle is not.
                Assert.NotEqual(Off, lit[0]);
                Assert.Equal(Off, lit[run / 2]);
            }
        }

        [Fact]
        public void A_block_stays_a_block_and_a_gap_stays_a_gap()
        {
            var table = Parsed(BlocksWithGap);
            var lit = CarLightMirror.Colors(table, "1", 4000, 7, MirrorFit.Stretch, 0);
            // Three LEDs sharing one threshold light together, which is what "three blocks" is.
            Assert.Equal(new[] { "#FF00FF00", "#FF00FF00", "#FF00FF00", Off, Off, Off, Off }, lit);
            // The gap is lit from idle in the table and transparent in the colours, so it stays dark:
            // the car has no LED there and an empty position is not an unlit one.
            Assert.Equal(Off, CarLightMirror.Colors(table, "1", 5500, 7, MirrorFit.Stretch, 0)[3]);
        }

        [Fact]
        public void Over_rev_flashes_the_whole_bar_at_the_cars_own_interval()
        {
            var table = Parsed(BlocksWithGap);
            var lit = CarLightMirror.Colors(table, "1", 6500, 7, MirrorFit.Stretch, 0);
            Assert.Equal(new[] { "#FFFF0000", "#FFFF0000", "#FFFF0000", Off, "#FFFF0000", "#FFFF0000", "#FFFF0000" }, lit);
            // 250 ms is the half period, so the next quarter-second is dark and the one after is lit.
            Assert.Equal(CarLightMirror.Dark(7), CarLightMirror.Colors(table, "1", 6500, 7, MirrorFit.Stretch, 250));
            Assert.Equal(lit, CarLightMirror.Colors(table, "1", 6500, 7, MirrorFit.Stretch, 500));
            // Below the redline it is an ordinary bar again, at whatever phase the clock is in.
            Assert.Equal("#FF00FF00", CarLightMirror.Colors(table, "1", 5900, 7, MirrorFit.Stretch, 250)[0]);
        }

        [Fact]
        public void A_car_that_does_not_flash_does_not_flash()
        {
            // 47 of the 85 have a zero interval, and a mirror that invented a flash for them would be
            // showing a warning the car never gives.
            var table = Parsed(MeetInMiddle);
            Assert.Equal(0, table.BlinkIntervalMs);
            foreach (var clock in new long[] { 0, 125, 250, 999 })
            {
                Assert.Equal(CarLightMirror.Colors(table, "1", 99000, 8, MirrorFit.Stretch, 0), CarLightMirror.Colors(table, "1", 99000, 8, MirrorFit.Stretch, clock));
            }
        }

        [Fact]
        public void Stretch_spreads_a_short_bar_over_a_long_run_and_keeps_the_ends()
        {
            var table = Parsed(LeftToRight);
            var row = CarLightMirror.GearFor(table, "1");
            // Six LEDs over fourteen: the ends are the ends, and the repeats are even and symmetric.
            Assert.Equal(new[] { 0, 0, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5 }, CarLightMirror.Placement(table, row, 14, MirrorFit.Stretch));
            // Six over six is the identity, which is the case that has to be exact.
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, CarLightMirror.Placement(table, row, 6, MirrorFit.Stretch));
        }

        [Fact]
        public void A_run_shorter_than_the_bar_resamples_rather_than_truncating()
        {
            var table = Parsed(MeetInMiddle);
            var row = CarLightMirror.GearFor(table, "1");
            var placement = CarLightMirror.Placement(table, row, 5, MirrorFit.Stretch);
            // Eight LEDs into five: both ends survive rather than three being cut off one end.
            Assert.Equal(new[] { 0, 2, 3, 5, 7 }, placement);
            // The middle LED of an odd run over an even bar has to pick one of the car's two centre
            // LEDs, so the indices cannot mirror. What a driver sees still does, because those two
            // share a threshold and a colour on a bar that fills from both ends.
            var thresholds = placement.Select(i => row.Thresholds[i]).ToArray();
            Assert.Equal(thresholds, Enumerable.Reverse(thresholds).ToArray());
        }

        [Fact]
        public void Exact_draws_the_bar_once_in_the_middle_and_leaves_the_rest_dark()
        {
            var table = Parsed(LeftToRight);
            var row = CarLightMirror.GearFor(table, "1");
            Assert.Equal(new[] { -1, -1, 0, 1, 2, 3, 4, 5, -1, -1 }, CarLightMirror.Placement(table, row, 10, MirrorFit.Exact));
            var lit = CarLightMirror.Colors(table, "1", 6500, 10, MirrorFit.Exact, 0);
            Assert.Equal(new[] { Off, Off, "#FF00FF00", "#FF00FF00", "#FFFFFF00", "#FFFFFF00", "#FFFF0000", "#FFFF0000", Off, Off }, lit);
            // A run too short to hold the bar cannot draw it exactly, so it stretches rather than
            // showing the left two thirds of a car's lights and calling it a mirror.
            Assert.Equal(CarLightMirror.Placement(table, row, 4, MirrorFit.Stretch), CarLightMirror.Placement(table, row, 4, MirrorFit.Exact));
        }

        [Fact]
        public void A_one_led_car_fills_the_run_and_a_one_led_run_is_a_shift_lamp()
        {
            var lamp = Parsed(@"{ ""carId"": ""test lamp"", ""ledNumber"": 1, ""redlineBlinkInterval"": 0,
                ""ledColor"": [""#00000000"", ""#FFFF0000""], ""ledRpm"": [{ ""1"": [0, 5000] }] }");
            Assert.Equal(new[] { Off, Off, Off }, CarLightMirror.Colors(lamp, "1", 4000, 3, MirrorFit.Stretch, 0));
            Assert.Equal(new[] { "#FFFF0000", "#FFFF0000", "#FFFF0000" }, CarLightMirror.Colors(lamp, "1", 6000, 3, MirrorFit.Stretch, 0));

            // The other way round: one LED to say what a whole bar says. The last light to come on is
            // the only choice that means the same thing on an ascending bar and a symmetric one.
            var table = Parsed(MeetInMiddle);
            Assert.Equal(new[] { Off }, CarLightMirror.Colors(table, "1", 3500, 1, MirrorFit.Stretch, 0));
            Assert.Equal(new[] { "#FFFF0000" }, CarLightMirror.Colors(table, "1", 4500, 1, MirrorFit.Stretch, 0));
        }

        [Fact]
        public void No_table_and_no_gear_are_both_a_null_rather_than_a_dark_bar()
        {
            // The difference matters: null falls back to the published ladder, and a dark bar would
            // leave a driver with an unlit strip and no way to know why.
            Assert.Null(CarLightMirror.Colors(null, "1", 5000, 8, MirrorFit.Stretch, 0));
            Assert.Null(CarLightMirror.GearFor(null, "1"));
            var table = Parsed(LeftToRight);
            Assert.Null(CarLightMirror.Colors(table, "1", 5000, 0, MirrorFit.Stretch, 0));
        }
    }
}
