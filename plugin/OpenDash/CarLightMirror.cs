// CarLightMirror.cs: the car's bar, drawn onto a run of LEDs that is not the car's bar.
//
// One pure function of (table, gear, RPM, run length, fit, clock) to one colour per LED. Every
// pattern a driver can name -- left to right, meet in the middle, three blocks, one at a time, all
// red, green-yellow-red, a gap in the middle, a single shift lamp -- comes out of it without a case
// for any of them, because the pattern is the thresholds and the colours rather than a mode. That
// is the whole argument of ADR 0018 in one file.
//
// No SimHub or WPF types: compiled into OpenDash.Tests, which is where the placement rules are
// pinned. The clock is a parameter for the same reason.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>What to do when the car's bar and the strip are not the same length.</summary>
    public enum MirrorFit
    {
        /// <summary>Spread the car's bar over the whole run, repeating LEDs where the run is longer.</summary>
        Stretch,

        /// <summary>Draw the bar at its own length, centred, and leave the rest of the run dark.</summary>
        Exact,
    }

    public static class CarLightMirror
    {
        /// <summary>
        /// One colour per LED of the run, or null when this car and gear have nothing to draw — which
        /// is the caller's signal to fall back to the published ladder rather than to draw darkness.
        /// </summary>
        /// <param name="gear">The gear as SimHub spells it: "R", "N", "1" … "8".</param>
        /// <param name="rpm">Engine speed now.</param>
        /// <param name="runLength">How many LEDs the strip gives this effect.</param>
        /// <param name="clockMs">Monotonic milliseconds, for the over-rev flash. Nothing else is timed.</param>
        public static string[] Colors(CarLightTable table, string gear, double rpm, int runLength, MirrorFit fit, long clockMs)
        {
            if (table == null || runLength < 1) return null;
            var row = GearFor(table, gear);
            if (row == null || row.Thresholds == null || row.Thresholds.Length != table.LedCount) return null;

            var placement = Placement(table, row, runLength, fit);
            // Three states rather than two: an ordinary bar, and the two halves of the over-rev flash.
            // Collapsing the dark half into "not flashing" would draw the ordinary bar between
            // flashes, which is a bar that shimmers rather than one that flashes.
            var overRev = OverRev(table, row, rpm);
            var flashLit = overRev && FlashOn(table, clockMs);
            var colors = new string[runLength];
            for (var k = 0; k < runLength; k++)
            {
                var led = placement[k];
                // Not every LED of the run has a car LED behind it: `exact` leaves the ends of a long
                // run empty rather than inventing bar to fill it with.
                if (led < 0)
                {
                    colors[k] = CarLightTable.Transparent;
                    continue;
                }
                var color = table.Colors[led];
                // A gap in the car's bar stays a gap, flashing or not: the car has no LED there, and
                // an over-rev is not a reason to invent one.
                if (IsTransparent(color)) colors[k] = CarLightTable.Transparent;
                else if (overRev) colors[k] = flashLit ? table.BlinkColor : CarLightTable.Transparent;
                // SimHub's RPMSegments lights a segment on `rpms > startValue`, and this is the same
                // comparison: a threshold of zero then means lit from idle and dark with the engine off.
                else colors[k] = rpm > row.Thresholds[led] ? color : CarLightTable.Transparent;
            }
            return colors;
        }

        /// <summary>
        /// The gear's own row, falling back down the box rather than refusing.
        ///
        /// <para>32 of the 85 measured cars light differently in different gears, so the row is the
        /// answer and not a detail. A gear the table does not carry — an eighth in a car measured to
        /// six, a neutral nobody wrote down — takes the highest gear below it, and a car with nothing
        /// numbered at all takes whatever single row it has. Falling back beats going dark: the bar a
        /// driver sees is then one gear's thresholds rather than none.</para>
        /// </summary>
        public static CarLightGear GearFor(CarLightTable table, string gear)
        {
            if (table == null || table.Gears == null || table.Gears.Count == 0) return null;
            var key = (gear ?? string.Empty).Trim();
            CarLightGear found;
            if (key.Length > 0 && table.Gears.TryGetValue(key, out found)) return found;

            int number;
            if (int.TryParse(key, out number))
            {
                var below = table.Gears.Keys
                    .Select(k => { int n; return int.TryParse(k, out n) ? n : -1; })
                    .Where(n => n > 0 && n <= number)
                    .ToList();
                if (below.Count > 0) return table.Gears[below.Max().ToString()];
            }
            // Neutral, reverse or nothing at all, on a table that spells none of them.
            return table.Gears.TryGetValue("N", out found) ? found : table.Gears.Values.First();
        }

        /// <summary>
        /// Which car LED each LED of the run shows, or -1 for one that shows none.
        ///
        /// <para><b>Stretch</b> maps the ends to the ends and everything between in proportion, which
        /// is what keeps the shape: a symmetric bar stays symmetric at any run length, a block stays a
        /// block, and a run shorter than the bar drops LEDs from the middle of each band rather than
        /// off one end. <b>Exact</b> draws the bar once in the middle of the run — and becomes stretch
        /// when the run is too short to hold it, because the alternative is to cut the bar in half.</para>
        ///
        /// <para>A one-LED run is the odd case and gets the one answer that means something on every
        /// pattern: the last LED to light in this gear, which is a shift lamp whether the car fills
        /// left to right or from both ends inwards.</para>
        /// </summary>
        public static int[] Placement(CarLightTable table, CarLightGear row, int runLength, MirrorFit fit)
        {
            var leds = table.LedCount;
            var placement = new int[runLength];
            if (runLength == 1)
            {
                placement[0] = Last(row);
                return placement;
            }
            if (fit == MirrorFit.Exact && runLength >= leds)
            {
                var offset = (runLength - leds) / 2;
                for (var k = 0; k < runLength; k++) placement[k] = k >= offset && k < offset + leds ? k - offset : -1;
                return placement;
            }
            if (leds == 1)
            {
                for (var k = 0; k < runLength; k++) placement[k] = 0;
                return placement;
            }
            // Sampled in integers and mirrored rather than computed straight through, and both halves
            // of that are load-bearing. `k * (leds - 1) / (runLength - 1)` in floating point lands
            // exactly on a half step whenever the two lengths share a factor -- 8 LEDs over 15 does it
            // seven times -- and rounding a half always the same way tips those samples one way down
            // the whole run, which turns a symmetric bar into a lopsided one. Mirroring the second
            // half from the first makes the sampling symmetric by construction instead of by luck.
            //
            // The middle LED of an odd run over an even bar is its own mirror and cannot be
            // symmetric: it picks one of the car's two centre LEDs. On a bar that fills from both
            // ends those two share a threshold and a colour, so what a driver sees is symmetric even
            // where the indices are not.
            var span = runLength - 1;
            for (var k = 0; k <= span / 2; k++)
            {
                // Round half down, in integers: floor((2a + b - 1) / 2b) for a/b.
                placement[k] = (2 * k * (leds - 1) + span - 1) / (2 * span);
                // The middle of an odd-length run is its own mirror, and mirroring it would overwrite
                // the sample with the other centre LED rather than leaving it where it landed.
                if (span - k != k) placement[span - k] = leds - 1 - placement[k];
            }
            return placement;
        }

        /// <summary>
        /// The car's own bar as three numbers a screen can draw with, for the gear and RPM now.
        ///
        /// <para>A strip mirrors the bar LED for LED and needs colours; a rev bar is fifteen segments in
        /// OpenDash's own palette and needs only to know how far up the car's bar the engine is (#353,
        /// ADR 0018 amended). So this is the whole of what a screen is given: how long this gear's ladder
        /// is, how much of it is lit, and the RPM its top third lights at. The table itself never leaves
        /// the plugin.</para>
        ///
        /// <para>Counted as LEDs lit rather than by index, so a bar that fills from both ends inwards
        /// reads exactly as one that fills left to right does. A threshold of zero is left out of the
        /// count entirely: it means lit from idle, which is how the files spell an LED that is not part
        /// of the ladder -- a marker, or the unlit half of a gap -- and counting those would put a
        /// stationary car two thirds of the way up its own bar.</para>
        /// </summary>
        public struct CarLadder
        {
            /// <summary>How many of this gear's LEDs are rungs of the ladder. Zero when there is no ladder to read.</summary>
            public readonly int Lamps;

            /// <summary>How many of them are lit at this RPM.</summary>
            public readonly int Lit;

            /// <summary>
            /// The RPM the top third of the bar lights at, which is the number a readout beside a rev bar
            /// has to print. Zero when there is no ladder.
            /// </summary>
            public readonly int TopRpm;

            public CarLadder(int lamps, int lit, int topRpm)
            {
                Lamps = lamps;
                Lit = lit;
                TopRpm = topRpm;
            }

            /// <summary>
            /// How many of the car's own three shift bands the engine has entered: 0 below the first LED,
            /// then 1, 2 and 3 for the thirds of the bar. -1 when this car and gear have nothing to say,
            /// which is the caller's signal to fall back to the ladder the sim publishes.
            ///
            /// <para>Thirds of the bar rather than thirds of the rev range, because that is what the rev
            /// bar's own three colours are: <c>stageOf</c> in components/revSegments.ts splits fifteen
            /// segments the same way, and a digit banded on one rule beside a bar banded on another is two
            /// answers to one question.</para>
            ///
            /// <para>Derived from <see cref="Lit"/> and <see cref="Lamps"/> rather than counted a second
            /// time, so that the digit changes band on the frame the bar's top segment lights: a screen
            /// lights segment k of n on <c>lit * n &gt; k * lamps</c>, and at the top third that is this
            /// same comparison.</para>
            /// </summary>
            public int Stage
            {
                get
                {
                    if (Lamps <= 0) return -1;
                    if (Lit <= 0) return 0;
                    // Rounded up, so that one LED of nine is the first band rather than none of them.
                    var stage = (Lit * 3 + Lamps - 1) / Lamps;
                    return stage > 3 ? 3 : stage;
                }
            }

            /// <summary>No ladder, shared rather than made: what every way of having no table reports.</summary>
            public static readonly CarLadder None = new CarLadder(0, 0, 0);
        }

        /// <summary>
        /// This car and gear's ladder at this RPM, or <see cref="CarLadder.None"/> when there is nothing
        /// to read -- no table, no row, or a row whose length disagrees with the bar's.
        /// </summary>
        public static CarLadder Ladder(CarLightTable table, string gear, double rpm)
        {
            if (table == null) return CarLadder.None;
            var row = GearFor(table, gear);
            if (row == null || row.Thresholds == null || row.Thresholds.Length != table.LedCount) return CarLadder.None;
            return Ladder(row, rpm);
        }

        /// <summary>One row's ladder, which is the half with no lookup in it.</summary>
        public static CarLadder Ladder(CarLightGear row, double rpm)
        {
            var lamps = 0;
            var lit = 0;
            foreach (var threshold in row.Thresholds)
            {
                if (threshold <= 0) continue;
                lamps++;
                // The same comparison the bar draws with, so a band is entered on the frame its first
                // LED lights and not one either side of it.
                if (rpm > threshold) lit++;
            }
            if (lamps == 0) return CarLadder.None;
            return new CarLadder(lamps, lit, TopRpm(row, lamps));
        }

        /// <summary>
        /// The RPM at which the bar's top third lights: the <c>n</c>th lowest rung, where <c>n</c> is the
        /// fewest lit LEDs that make <see cref="CarLadder.Stage"/> 3.
        ///
        /// <para>Found by rank rather than by sorting a copy of the row, because this runs inside a frame
        /// and a bar is at most sixteen LEDs long. Equal thresholds -- a block of LEDs that light together
        /// -- share a rank, which is why the test is "at or below" rather than "below".</para>
        /// </summary>
        private static int TopRpm(CarLightGear row, int lamps)
        {
            // Stage reaches 3 on `lit * 3 > 2 * lamps`, so this is the fewest lit LEDs that does it.
            var wanted = ((2 * lamps) / 3) + 1;
            var top = 0;
            foreach (var threshold in row.Thresholds)
            {
                if (threshold <= 0) continue;
                if (top > 0 && threshold >= top) continue;
                var atOrBelow = 0;
                foreach (var other in row.Thresholds)
                {
                    if (other > 0 && other <= threshold) atOrBelow++;
                }
                if (atOrBelow >= wanted) top = threshold;
            }
            return top;
        }

        /// <summary>
        /// How many of the car's own three shift bands the engine has entered, from a table: the flag
        /// box's digit asks it this way, and <see cref="CarLadder.Stage"/> is the definition.
        /// </summary>
        public static int Stage(CarLightTable table, string gear, double rpm)
        {
            return Ladder(table, gear, rpm).Stage;
        }

        /// <summary>The LED that lights last in this gear, which is the one a single lamp has to be.</summary>
        private static int Last(CarLightGear row)
        {
            var best = 0;
            for (var i = 1; i < row.Thresholds.Length; i++)
            {
                if (row.Thresholds[i] > row.Thresholds[best]) best = i;
            }
            return best;
        }

        /// <summary>
        /// Whether the bar is over-revving, and so flashing rather than showing the ladder.
        ///
        /// <para>The redline is this gear's own and the interval is the car's, which is the half of
        /// the mirror a generated profile could not do: <c>RPMSegments</c> blinks on SimHub's redline
        /// at a delay baked into the file, and the cars that flash at all do it at one of five
        /// different speeds. 47 of the 85 do not flash, and say so with a zero — for them this is
        /// false at any RPM, because a flash the car never gives is a warning OpenDash invented.</para>
        /// </summary>
        public static bool OverRev(CarLightTable table, CarLightGear row, double rpm)
        {
            if (table.BlinkIntervalMs <= 0 || row.Redline <= 0) return false;
            if (IsTransparent(table.BlinkColor)) return false;
            return rpm >= row.Redline;
        }

        /// <summary>
        /// Which half of the flash the clock is in. The interval is the half period, the way SimHub's
        /// own <c>BlinkDelay</c> is: lit for one interval, dark for the next.
        /// </summary>
        public static bool FlashOn(CarLightTable table, long clockMs)
        {
            if (table.BlinkIntervalMs <= 0) return true;
            return (Math.Abs(clockMs) / table.BlinkIntervalMs) % 2 == 0;
        }

        /// <summary>Whether a colour leaves the LED dark, in either of the two spellings the files use.</summary>
        public static bool IsTransparent(string color)
        {
            var text = (color ?? string.Empty).Trim();
            if (text.Length == 0) return true;
            if (string.Equals(text, CarLightTable.Transparent, StringComparison.OrdinalIgnoreCase)) return true;
            // #AARRGGBB with a zero alpha, which is how the files spell a gap in the bar.
            return text.Length == 9 && text[0] == '#' && text[1] == '0' && text[2] == '0';
        }

        /// <summary>A whole run left dark, for a caller with a run to fill and no mirror to fill it with.</summary>
        public static string[] Dark(int runLength)
        {
            return Enumerable.Repeat(CarLightTable.Transparent, Math.Max(0, runLength)).ToArray();
        }
    }
}
